using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using LightSide;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;
using Debug = UnityEngine.Debug;

/// <summary>
/// Measures UniText glyph rasterization — enabling many text objects when the atlas is empty.
/// Captures glyph rasterization + text generation + mesh build in one frame.
///
/// Setup:
///   1. Place disabled UniText GameObjects as children of this object.
///   2. Assign font assets to clear (or leave empty for auto-detect).
///   3. Play → press Space or click "Run Benchmark" in Inspector.
/// </summary>
public class UniText_GlyphRasterizationBenchmark : MonoBehaviour
{
    [Header("Settings")]
    public int iterations = 5;
    public int warmupIterations = 1;

    [Header("Status")]
    [SerializeField] bool forceCPURasterization;
    [SerializeField] bool isRunning;
    [SerializeField, TextArea(15, 30)] string lastResult = "";

    GameObject[] textObjects;
    UniTextFont[] fontAssets;
    readonly Stopwatch sw = new();
    readonly StringBuilder report = new();

    public struct GlyphRasterResults
    {
        public List<float> frameTimes;
        public List<int> glyphCounts;
        public long managedAlloc;
        public string mode;
    }

    public GlyphRasterResults LastResults { get; private set; }

    public IEnumerator RunBenchmarkCoroutine(bool singleThreaded, bool maxStroke = false)
    {
        yield return RunCore(singleThreaded, maxStroke);
    }

    [ContextMenu("Run Benchmark (Single-Threaded)")]
    public void RunBenchmark()
    {
        if (isRunning) return;
        StartCoroutine(RunCore(singleThreaded: true));
    }

    [ContextMenu("Run Benchmark (Parallel)")]
    public void RunBenchmarkParallel()
    {
        if (isRunning) return;
        StartCoroutine(RunCore(singleThreaded: false));
    }

    [ContextMenu("Run Benchmark (Single-Threaded + Max Stroke)")]
    public void RunBenchmarkMaxStroke()
    {
        if (isRunning) return;
        StartCoroutine(RunCore(singleThreaded: true, maxStroke: true));
    }

    [ContextMenu("Run Benchmark (Parallel + Max Stroke)")]
    public void RunBenchmarkParallelMaxStroke()
    {
        if (isRunning) return;
        StartCoroutine(RunCore(singleThreaded: false, maxStroke: true));
    }

    [ContextMenu("Run Benchmark (Both)")]
    public void RunBenchmarkBoth()
    {
        if (isRunning) return;
        StartCoroutine(RunBoth());
    }

    [ContextMenu("Run Benchmark (All 4)")]
    public void RunBenchmarkAll()
    {
        if (isRunning) return;
        StartCoroutine(RunAll());
    }

    IEnumerator RunBoth()
    {
        yield return RunCore(singleThreaded: true);
        yield return RunCore(singleThreaded: false);
    }

    IEnumerator RunAll()
    {
        yield return RunCore(singleThreaded: true);
        yield return RunCore(singleThreaded: false);
        yield return RunCore(singleThreaded: true, maxStroke: true);
        yield return RunCore(singleThreaded: false, maxStroke: true);
    }

    IEnumerator RunCore(bool singleThreaded, bool maxStroke = false)
    {
        isRunning = true;
        report.Clear();

        GlyphAtlas.forceCpuRasterization = forceCPURasterization;
        bool wasParallel = UniTextBase.UseParallel;
        bool wasForceST = GlyphAtlas.forceSingleThreaded;

        UniTextBase.UseParallel = !singleThreaded;
        GlyphAtlas.forceSingleThreaded = singleThreaded;

        string mode = (singleThreaded ? "SINGLE-THREADED" : "PARALLEL") + (maxStroke ? " + MAX-STROKE" : "");

        CollectChildren();

        if (textObjects.Length == 0)
        {
            Debug.LogError("[UniText GlyphRaster] No disabled UniText children found.");
            isRunning = false;
            yield break;
        }

        CollectFonts();

        List<(UniText text, Style style)> strokeStyles = null;
        if (maxStroke)
        {
            strokeStyles = new List<(UniText, Style)>();
            for (int i = 0; i < textObjects.Length; i++)
            {
                var ut = textObjects[i].GetComponent<UniText>();
                if (ut == null) continue;
                var style = Style.WholeText(new StrokeModifier
                {
                    Width = UnitValue.Em(1f),
                    Align = 1f
                });
                ut.AddStyle(style);
                strokeStyles.Add((ut, style));
            }
        }

        report.AppendLine("═══════════════════════════════════════════════");
        report.AppendLine($"    UNITEXT GLYPH RASTERIZATION BENCHMARK ({mode})");
        report.AppendLine("═══════════════════════════════════════════════");
        report.AppendLine($"  Objects: {textObjects.Length}");
        report.AppendLine($"  Font assets: {fontAssets.Length}");
        report.AppendLine($"  Iterations: {iterations}  Warmup: {warmupIterations}");
        report.AppendLine("═══════════════════════════════════════════════");

        var frameTimes = new List<float>();
        var e2eTimes = new List<float>();
        var glyphCounts = new List<int>();
        long totalManagedAlloc = 0;

        for (int iter = -warmupIterations; iter < iterations; iter++)
        {
            for (int i = 0; i < textObjects.Length; i++)
                textObjects[i].SetActive(false);
            yield return null;

            int entriesBefore = 0;
            GlyphAtlas.ForEachInstance(a => entriesBefore += a.EntryCount);

            string atlasBeforeClear = GetAtlasDiagnostics("BEFORE clear");

            UniTextFont.Core.DisposeAllLive();
            yield return null;

            int entriesAfterClear = 0;
            GlyphAtlas.ForEachInstance(a => entriesAfterClear += a.EntryCount);

            string atlasAfterClear = GetAtlasDiagnostics("AFTER clear");

            int glyphsBefore = CountGlyphs();

            using var gcRec = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");

            sw.Restart();
            for (int i = 0; i < textObjects.Length; i++)
                textObjects[i].SetActive(true);
            Canvas.ForceUpdateCanvases();
            sw.Stop();

            float ms = (float)sw.Elapsed.TotalMilliseconds;
            float e2eMs = ms;
#if UNITEXT_DEBUG
            {
                double activationStart = Time.realtimeSinceStartupAsDouble - sw.Elapsed.TotalSeconds;
                var sdfAtlas = GlyphAtlas.GetInstance(UniTextRenderMode.SDF);
                var msdfAtlas = GlyphAtlas.GetInstance(UniTextRenderMode.MSDF);
                for (int guard = 0; guard < 600 && !(sdfAtlas.GpuRasterComplete && msdfAtlas.GpuRasterComplete); guard++)
                    yield return null;
                e2eMs = (float)((Time.realtimeSinceStartupAsDouble - activationStart) * 1000.0);
            }
#endif
            int glyphsAfter = CountGlyphs();
            int uniqueGlyphs = glyphsAfter - glyphsBefore;

            int entriesAfterRaster = 0;
            GlyphAtlas.ForEachInstance(a => entriesAfterRaster += a.EntryCount);

            string atlasAfterRaster = GetAtlasDiagnostics("AFTER raster");

            bool isWarmup = iter < 0;
            string tag = isWarmup ? "warmup" : $"iter {iter + 1}";

            Debug.Log($"[UniText GlyphRaster {mode}] {tag}: {ms:F2}ms (e2e {e2eMs:F2}ms), +{uniqueGlyphs} glyphs (total {glyphsAfter})\n" +
                      $"  entries: {entriesBefore}→{entriesAfterClear}→{entriesAfterRaster} (before→afterClear→afterRaster)\n" +
                      $"  {atlasBeforeClear}\n  {atlasAfterClear}\n  {atlasAfterRaster}");

            if (!isWarmup)
            {
                frameTimes.Add(ms);
                e2eTimes.Add(e2eMs);
                glyphCounts.Add(uniqueGlyphs);
                totalManagedAlloc += gcRec.LastValue;
            }
        }

        for (int i = 0; i < textObjects.Length; i++)
            textObjects[i].SetActive(false);

        if (strokeStyles != null)
            for (int i = 0; i < strokeStyles.Count; i++)
                strokeStyles[i].text.RemoveStyle(strokeStyles[i].style);

        UniText.UseParallel = wasParallel;
        GlyphAtlas.forceSingleThreaded = wasForceST;

        LastResults = new GlyphRasterResults
        {
            frameTimes = new List<float>(frameTimes),
            glyphCounts = new List<int>(glyphCounts),
            managedAlloc = totalManagedAlloc,
            mode = mode
        };

        FormatResults(frameTimes, e2eTimes, glyphCounts, totalManagedAlloc, mode);
        lastResult = report.ToString();
        Debug.Log(lastResult);

        isRunning = false;
    }

    void CollectChildren()
    {
        var list = new List<GameObject>();
        foreach (var ut in GetComponentsInChildren<UniText>(true))
        {
            if (ut.gameObject != gameObject)
                list.Add(ut.gameObject);
        }
        textObjects = list.ToArray();
    }

    void CollectFonts()
    {
        var first = textObjects[0].GetComponent<UniText>();
        var font = first != null ? first.PrimaryFont : null;
        fontAssets = font != null ? new[] { font } : System.Array.Empty<UniTextFont>();
    }

    int CountGlyphs() => GlyphAtlas.GetInstance(UniTextRenderMode.SDF).EntryCount;

    string GetAtlasDiagnostics(string label)
    {
        var sb = new StringBuilder();
        sb.Append($"[Atlas {label}] ");

        GlyphAtlas.ForEachInstance(atlas =>
        {
            var tex = atlas.AtlasTexture;
            if (tex == null)
            {
                sb.Append($"pages={atlas.PageCount} tex=NULL  ");
                return;
            }

            int depth = tex is RenderTexture rt ? rt.volumeDepth
                : tex is Texture2DArray ta ? ta.depth : 1;
            sb.Append($"pages={atlas.PageCount} texSize={tex.width}x{tex.height}x{depth} " +
                      $"pixelChecksum={BenchmarkAtlasUtils.Checksum(tex)}  ");
        });

        return sb.ToString();
    }

    void FormatResults(List<float> frameTimes, List<float> e2eTimes, List<int> glyphCounts, long managedAlloc, string mode)
    {
        var unsortedFrameTimes = new List<float>(frameTimes);
        frameTimes.Sort();
        float median = frameTimes[frameTimes.Count / 2];
        float min = frameTimes[0];
        float max = frameTimes[frameTimes.Count - 1];

        float sum = 0;
        for (int i = 0; i < frameTimes.Count; i++) sum += frameTimes[i];
        float avg = sum / frameTimes.Count;

        var sortedE2e = new List<float>(e2eTimes);
        sortedE2e.Sort();
        float e2eMedian = sortedE2e[sortedE2e.Count / 2];

        int typicalGlyphs = glyphCounts[0];

        report.AppendLine();
        for (int i = 0; i < unsortedFrameTimes.Count; i++)
            report.AppendLine($"  Run {i + 1}: {unsortedFrameTimes[i]:F2} ms   e2e {e2eTimes[i]:F2} ms   ({glyphCounts[i]} glyphs)");

        report.AppendLine();
        report.AppendLine($"  Mode: {mode}");
        report.AppendLine($"  Median:  {median:F2} ms");
        report.AppendLine($"  Average: {avg:F2} ms");
        report.AppendLine($"  Min:     {min:F2} ms");
        report.AppendLine($"  Max:     {max:F2} ms");
        report.AppendLine($"  Median e2e (CPU + GPU raster): {e2eMedian:F2} ms");
        report.AppendLine($"  Unique glyphs: {typicalGlyphs}");
        report.AppendLine($"  Managed alloc: {FormatBytes(managedAlloc)} (total across {iterations} runs)");

        if (typicalGlyphs > 0)
        {
            double usPerGlyph = (median * 1000.0) / typicalGlyphs;
            report.AppendLine($"  Per-glyph (median): {usPerGlyph:F1} us");
        }

        report.AppendLine("═══════════════════════════════════════════════");
    }

    static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024f:F1} KB";
        return $"{bytes / (1024f * 1024f):F2} MB";
    }
}
