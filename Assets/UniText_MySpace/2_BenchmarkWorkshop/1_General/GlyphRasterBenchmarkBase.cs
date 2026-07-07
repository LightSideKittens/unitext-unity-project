using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Unity.Profiling;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// Shared glyph-rasterization harness: clears the engine's glyph atlas, times the re-fill of one
/// workload, and counts the newly rasterized glyphs — once per iteration with warmup, GC and
/// atlas-delta bookkeeping. Concretes plug in how their engine clears, rasterizes and counts.
/// </summary>
public abstract class GlyphRasterBenchmarkBase : MonoBehaviour
{
    [Header("Settings")]
    public int iterations = 5;
    public int warmupIterations = 1;

    [Header("Status")]
    [SerializeField] protected bool isRunning;
    [SerializeField, TextArea(15, 30)] protected string lastResult = "";

    protected readonly Stopwatch sw = new();
    protected readonly StringBuilder report = new();
    protected float lastE2eMs;

    public GlyphRasterData LastResults { get; private set; }

    protected abstract string EngineName { get; }

    /// <summary>Gather the workload (children, fonts, target atlas) and apply the shared corpus. Return false — after logging — when there is nothing to run.</summary>
    protected abstract bool CollectTargets();

    protected abstract void ClearCaches();

    /// <summary>The timed work: re-rasterize the workload into the freshly cleared atlas.</summary>
    protected abstract void Rasterize();

    protected abstract int CountGlyphs();

    protected virtual void OnBeforeRun() { }
    protected virtual void OnAfterRun() { }

    /// <summary>Turn the workload off before clearing so nothing re-rasterizes during the clear frame. No-op for engines that rasterize a font asset directly.</summary>
    protected virtual void Deactivate() { }

    protected virtual string Diagnostics(string label) => "";

    /// <summary>True when rasterization is async (UniText's GPU path) and <see cref="AwaitAsyncCompletion"/> yields a distinct end-to-end time. False for synchronous CPU rasterizers (TMP, UI Toolkit).</summary>
    protected virtual bool HasE2E => false;

    protected virtual IEnumerator AwaitAsyncCompletion(float cpuMs)
    {
        lastE2eMs = cpuMs;
        yield break;
    }

    protected IEnumerator RunPass(string mode)
    {
        isRunning = true;
        report.Clear();
        LastResults = null;
        OnBeforeRun();

        if (!CollectTargets())
        {
            OnAfterRun();
            isRunning = false;
            yield break;
        }

        AppendHeader(mode);

        var frameTimes = new List<float>();
        var e2eTimes = HasE2E ? new List<float>() : null;
        var glyphCounts = new List<int>();
        long totalManagedAlloc = 0;

        for (int iter = -warmupIterations; iter < iterations; iter++)
        {
            Deactivate();
            yield return null;

            string beforeClear = Diagnostics("BEFORE clear");
            ClearCaches();
            yield return null;
            string afterClear = Diagnostics("AFTER clear");

            int glyphsBefore = CountGlyphs();
            using var gcRec = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");

            sw.Restart();
            Rasterize();
            sw.Stop();

            float ms = (float)sw.Elapsed.TotalMilliseconds;
            lastE2eMs = ms;
            if (HasE2E) yield return AwaitAsyncCompletion(ms);
            float e2eMs = lastE2eMs;

            int uniqueGlyphs = CountGlyphs() - glyphsBefore;
            string afterRaster = Diagnostics("AFTER raster");

            bool isWarmup = iter < 0;
            string tag = isWarmup ? "warmup" : $"iter {iter + 1}";
            Debug.Log($"[{EngineName} GlyphRaster{(mode != null ? $" {mode}" : "")}] {tag}: {ms:F2}ms" +
                      (HasE2E ? $" (e2e {e2eMs:F2}ms)" : "") + $", +{uniqueGlyphs} glyphs\n  {beforeClear}\n  {afterClear}\n  {afterRaster}");

            if (!isWarmup)
            {
                frameTimes.Add(ms);
                e2eTimes?.Add(e2eMs);
                glyphCounts.Add(uniqueGlyphs);
                totalManagedAlloc += gcRec.LastValue;
            }
        }

        Deactivate();
        OnAfterRun();

        LastResults = new GlyphRasterData
        {
            frameTimes = frameTimes,
            e2eTimes = e2eTimes,
            uniqueGlyphs = glyphCounts.Count > 0 ? glyphCounts[0] : 0,
            managedAlloc = totalManagedAlloc
        };

        AppendResults(frameTimes, e2eTimes, glyphCounts, totalManagedAlloc, mode);
        lastResult = report.ToString();
        Debug.Log(lastResult);
        isRunning = false;
    }

    private void AppendHeader(string mode)
    {
        report.AppendLine("═══════════════════════════════════════════════");
        report.AppendLine($"    {EngineName.ToUpperInvariant()} GLYPH RASTERIZATION BENCHMARK{(mode != null ? $" ({mode})" : "")}");
        report.AppendLine("═══════════════════════════════════════════════");
        report.AppendLine($"  Iterations: {iterations}  Warmup: {warmupIterations}");
        report.AppendLine("═══════════════════════════════════════════════");
    }

    private void AppendResults(List<float> frameTimes, List<float> e2eTimes, List<int> glyphCounts, long managedAlloc, string mode)
    {
        if (frameTimes.Count == 0) return;

        var sorted = new List<float>(frameTimes);
        sorted.Sort();
        float median = sorted[sorted.Count / 2];
        float sum = 0;
        for (int i = 0; i < sorted.Count; i++) sum += sorted[i];
        int typicalGlyphs = glyphCounts[0];

        report.AppendLine();
        for (int i = 0; i < frameTimes.Count; i++)
            report.AppendLine($"  Run {i + 1}: {frameTimes[i]:F2} ms" +
                              (e2eTimes != null ? $"   e2e {e2eTimes[i]:F2} ms" : "") + $"   ({glyphCounts[i]} glyphs)");

        report.AppendLine();
        if (mode != null) report.AppendLine($"  Mode: {mode}");
        report.AppendLine($"  Median:  {median:F2} ms");
        report.AppendLine($"  Average: {sum / sorted.Count:F2} ms");
        report.AppendLine($"  Min:     {sorted[0]:F2} ms");
        report.AppendLine($"  Max:     {sorted[sorted.Count - 1]:F2} ms");
        if (e2eTimes is { Count: > 0 })
        {
            var sortedE2e = new List<float>(e2eTimes);
            sortedE2e.Sort();
            report.AppendLine($"  Median e2e (CPU + GPU raster): {sortedE2e[sortedE2e.Count / 2]:F2} ms");
        }
        report.AppendLine($"  Unique glyphs: {typicalGlyphs}");
        report.AppendLine($"  Managed alloc: {TextBenchmarkBase.FormatBytes(managedAlloc)} (total across {iterations} runs)");
        if (typicalGlyphs > 0)
            report.AppendLine($"  Per-glyph (median): {(median * 1000.0) / typicalGlyphs:F1} us");
        report.AppendLine("═══════════════════════════════════════════════");
    }
}
