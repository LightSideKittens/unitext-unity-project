using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using TMPro;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;
using Debug = UnityEngine.Debug;

/// <summary>
/// Measures TMP glyph rasterization — enabling many text objects when the atlas is empty.
/// Captures glyph rasterization + text generation + mesh build in one frame.
///
/// Setup:
///   1. Place disabled TMP text GameObjects as children of this object.
///   2. Assign font assets to clear (or leave empty for auto-detect).
///   3. Play → press Space or click "Run Benchmark" in Inspector.
/// </summary>
public class TMP_GlyphRasterizationBenchmark : MonoBehaviour
{
    [Header("Settings")]
    public int iterations = 5;
    public int warmupIterations = 1;

    [Header("Status")]
    [SerializeField] bool isRunning;
    [SerializeField, TextArea(15, 30)] string lastResult = "";

    GameObject[] textObjects;
    TMP_FontAsset[] fontAssets;
    readonly Stopwatch sw = new();
    readonly StringBuilder report = new();

    public struct GlyphRasterResults
    {
        public List<float> frameTimes;
        public List<int> glyphCounts;
        public long managedAlloc;
    }

    public GlyphRasterResults LastResults { get; private set; }

    public IEnumerator RunBenchmarkCoroutine()
    {
        yield return Run();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space) && !isRunning)
            RunBenchmark();
    }

    [ContextMenu("Run Benchmark")]
    public void RunBenchmark()
    {
        if (isRunning) return;
        StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        isRunning = true;
        report.Clear();

        CollectChildren();

        if (textObjects.Length == 0)
        {
            Debug.LogError("[TMP GlyphRaster] No disabled TMP_Text children found.");
            isRunning = false;
            yield break;
        }

        CollectFonts();

        report.AppendLine("═══════════════════════════════════════════════");
        report.AppendLine("         TMP GLYPH RASTERIZATION BENCHMARK");
        report.AppendLine("═══════════════════════════════════════════════");
        report.AppendLine($"  Objects: {textObjects.Length}");
        report.AppendLine($"  Font assets: {fontAssets.Length}");
        report.AppendLine($"  Iterations: {iterations}  Warmup: {warmupIterations}");
        report.AppendLine("═══════════════════════════════════════════════");

        var frameTimes = new List<float>();
        var glyphCounts = new List<int>();
        long totalManagedAlloc = 0;

        for (int iter = -warmupIterations; iter < iterations; iter++)
        {
            for (int i = 0; i < textObjects.Length; i++)
                textObjects[i].SetActive(false);
            yield return null;

            string atlasBeforeClear = GetAtlasDiagnostics("BEFORE clear");

            for (int i = 0; i < fontAssets.Length; i++)
                fontAssets[i].ClearFontAssetData(false);
            yield return null;

            string atlasAfterClear = GetAtlasDiagnostics("AFTER clear");

            int glyphsBefore = CountGlyphs();

            using var gcRec = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");

            sw.Restart();
            for (int i = 0; i < textObjects.Length; i++)
                textObjects[i].SetActive(true);
            Canvas.ForceUpdateCanvases();
            sw.Stop();

            float ms = (float)sw.Elapsed.TotalMilliseconds;
            int glyphsAfter = CountGlyphs();
            int uniqueGlyphs = glyphsAfter - glyphsBefore;

            string atlasAfterRaster = GetAtlasDiagnostics("AFTER raster");

            bool isWarmup = iter < 0;
            string tag = isWarmup ? "warmup" : $"iter {iter + 1}";

            Debug.Log($"[TMP GlyphRaster] {tag}: {ms:F2}ms, +{uniqueGlyphs} glyphs (total {glyphsAfter})\n" +
                      $"  {atlasBeforeClear}\n  {atlasAfterClear}\n  {atlasAfterRaster}");

            if (!isWarmup)
            {
                frameTimes.Add(ms);
                glyphCounts.Add(uniqueGlyphs);
                totalManagedAlloc += gcRec.LastValue;
            }
        }

        for (int i = 0; i < textObjects.Length; i++)
            textObjects[i].SetActive(false);

        LastResults = new GlyphRasterResults
        {
            frameTimes = new List<float>(frameTimes),
            glyphCounts = new List<int>(glyphCounts),
            managedAlloc = totalManagedAlloc
        };

        FormatResults(frameTimes, glyphCounts, totalManagedAlloc);
        lastResult = report.ToString();
        Debug.Log(lastResult);

        isRunning = false;
    }

    void CollectChildren()
    {
        var list = new List<GameObject>();
        foreach (var tmp in GetComponentsInChildren<TMP_Text>(true))
        {
            if (tmp.gameObject != gameObject)
                list.Add(tmp.gameObject);
        }
        textObjects = list.ToArray();
    }

    void CollectFonts()
    {
        var first = textObjects[0].GetComponent<TMP_Text>();
        var font = first != null ? first.font : null;
        fontAssets = font != null ? new[] { font } : System.Array.Empty<TMP_FontAsset>();
    }

    int CountGlyphs()
    {
        int total = 0;
        for (int i = 0; i < fontAssets.Length; i++)
            total += fontAssets[i].glyphTable.Count;
        return total;
    }

    string GetAtlasDiagnostics(string label)
    {
        var sb = new StringBuilder();
        sb.Append($"[Atlas {label}] ");

        for (int f = 0; f < fontAssets.Length; f++)
        {
            var font = fontAssets[f];
            var textures = font.atlasTextures;
            int glyphs = font.glyphTable.Count;
            int chars = font.characterTable.Count;

            long pixelChecksum = 0;
            if (textures != null && textures.Length > 0 && textures[0] != null)
            {
                var tex = textures[0];
                if (tex.isReadable)
                {
                    var raw = tex.GetRawTextureData<byte>();
                    int step = Mathf.Max(1, raw.Length / 128);
                    for (int j = 0; j < raw.Length; j += step)
                        pixelChecksum += raw[j];
                }
            }

            sb.Append($"'{font.name}': glyphs={glyphs} chars={chars} " +
                      $"atlasCount={textures?.Length ?? 0} pixelChecksum={pixelChecksum}  ");
        }

        return sb.ToString();
    }

    void FormatResults(List<float> frameTimes, List<int> glyphCounts, long managedAlloc)
    {
        frameTimes.Sort();
        float median = frameTimes[frameTimes.Count / 2];
        float min = frameTimes[0];
        float max = frameTimes[frameTimes.Count - 1];

        float sum = 0;
        for (int i = 0; i < frameTimes.Count; i++) sum += frameTimes[i];
        float avg = sum / frameTimes.Count;

        int typicalGlyphs = glyphCounts[0];

        report.AppendLine();
        for (int i = 0; i < frameTimes.Count; i++)
            report.AppendLine($"  Run {i + 1}: {frameTimes[i]:F2} ms   ({glyphCounts[i]} glyphs)");

        report.AppendLine();
        report.AppendLine($"  Median:  {median:F2} ms");
        report.AppendLine($"  Average: {avg:F2} ms");
        report.AppendLine($"  Min:     {min:F2} ms");
        report.AppendLine($"  Max:     {max:F2} ms");
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
