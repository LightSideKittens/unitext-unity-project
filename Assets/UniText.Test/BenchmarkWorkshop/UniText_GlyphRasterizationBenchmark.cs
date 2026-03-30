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

    [Tooltip("Font assets to clear before each run. Auto-detected if empty.")]
    public UniTextFont[] fontAssets;

    [Header("Status")]
    [SerializeField] bool isRunning;
    [SerializeField, TextArea(15, 30)] string lastResult = "";

    GameObject[] textObjects;
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

    public IEnumerator RunBenchmarkCoroutine(bool singleThreaded)
    {
        yield return RunCore(singleThreaded);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space) && !isRunning)
            RunBenchmark();
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

    [ContextMenu("Run Benchmark (Both)")]
    public void RunBenchmarkBoth()
    {
        if (isRunning) return;
        StartCoroutine(RunBoth());
    }

    IEnumerator RunBoth()
    {
        yield return RunCore(singleThreaded: true);
        yield return RunCore(singleThreaded: false);
    }

    IEnumerator RunCore(bool singleThreaded)
    {
        isRunning = true;
        report.Clear();

        bool wasParallel = UniText.UseParallel;
        bool wasForceST = GlyphAtlas.ForceSingleThreaded;

        UniText.UseParallel = !singleThreaded;
        GlyphAtlas.ForceSingleThreaded = singleThreaded;

        string mode = singleThreaded ? "SINGLE-THREADED" : "PARALLEL";

        CollectChildren();
        CollectFonts();

        if (textObjects.Length == 0)
        {
            Debug.LogError("[UniText GlyphRaster] No disabled UniText children found.");
            isRunning = false;
            yield break;
        }

        report.AppendLine("═══════════════════════════════════════════════");
        report.AppendLine($"    UNITEXT GLYPH RASTERIZATION BENCHMARK ({mode})");
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

            int entriesBefore = 0;
            GlyphAtlas.ForEachInstance(a => entriesBefore += a.EntryCount);

            string atlasBeforeClear = GetAtlasDiagnostics("BEFORE clear");

            for (int i = 0; i < fontAssets.Length; i++)
                fontAssets[i].ClearDynamicData();
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
            int glyphsAfter = CountGlyphs();
            int uniqueGlyphs = glyphsAfter - glyphsBefore;

            int entriesAfterRaster = 0;
            GlyphAtlas.ForEachInstance(a => entriesAfterRaster += a.EntryCount);

            string atlasAfterRaster = GetAtlasDiagnostics("AFTER raster");

            bool isWarmup = iter < 0;
            string tag = isWarmup ? "warmup" : $"iter {iter + 1}";

            Debug.Log($"[UniText GlyphRaster {mode}] {tag}: {ms:F2}ms, +{uniqueGlyphs} glyphs (total {glyphsAfter})\n" +
                      $"  entries: {entriesBefore}→{entriesAfterClear}→{entriesAfterRaster} (before→afterClear→afterRaster)\n" +
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

        UniText.UseParallel = wasParallel;
        GlyphAtlas.ForceSingleThreaded = wasForceST;

        LastResults = new GlyphRasterResults
        {
            frameTimes = new List<float>(frameTimes),
            glyphCounts = new List<int>(glyphCounts),
            managedAlloc = totalManagedAlloc,
            mode = mode
        };

        FormatResults(frameTimes, glyphCounts, totalManagedAlloc, mode);
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
        if (fontAssets != null && fontAssets.Length > 0) return;

        var set = new HashSet<UniTextFont>();
        foreach (var ut in GetComponentsInChildren<UniText>(true))
        {
            var font = ut.PrimaryFont;
            if (font != null) set.Add(font);
        }
        fontAssets = new UniTextFont[set.Count];
        set.CopyTo(fontAssets);
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

        GlyphAtlas.ForEachInstance(atlas =>
        {
            var tex = atlas.AtlasTexture as Texture2DArray;
            if (tex == null)
            {
                sb.Append($"pages={atlas.PageCount} tex=NULL  ");
                return;
            }

            long pixelChecksum = 0;
            int sampledPages = Mathf.Min(atlas.PageCount, 2);
            for (int p = 0; p < sampledPages; p++)
            {
                var raw = tex.GetPixelData<ushort>(0, p);
                int step = Mathf.Max(1, raw.Length / 64);
                for (int j = 0; j < raw.Length; j += step)
                    pixelChecksum += raw[j];
            }

            sb.Append($"pages={atlas.PageCount} texSize={tex.width}x{tex.height}x{tex.depth} " +
                      $"pixelChecksum={pixelChecksum}  ");
        });

        return sb.ToString();
    }

    void FormatResults(List<float> frameTimes, List<int> glyphCounts, long managedAlloc, string mode)
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
        report.AppendLine($"  Mode: {mode}");
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
