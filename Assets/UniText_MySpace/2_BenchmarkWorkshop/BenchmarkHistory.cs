#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Committed benchmark history under &lt;project&gt;/Benchmarks. A run's combined JSON is split by suite
/// into two independent streams — <c>run-text-*.js</c> and <c>run-glyph-*.js</c> — each pushing into its
/// own global and indexed by its own <c>{suite}-runs-index.js</c>, so the file:// viewer shows the text
/// pipeline and glyph rasterization as separate tabs. Editor runs save automatically; CI artifacts (one
/// combined JSON) come in through the import menu and are split the same way.
/// </summary>
public static class BenchmarkHistory
{
    static readonly string[] Suites = { "text", "glyph" };

    static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;
    static string BenchmarksDir => Path.Combine(ProjectRoot, "Benchmarks");
    static string RunsDir => Path.Combine(BenchmarksDir, "runs");

    /// <summary>The kept section per suite, and the section stripped out of that suite's file.</summary>
    static (string keep, string drop) Sections(string suite) =>
        suite == "glyph" ? ("glyphRasterization", "textBenchmarks") : ("textBenchmarks", "glyphRasterization");

    static string RunsGlobal(string suite) => suite == "glyph" ? "__unitextGlyphRuns" : "__unitextTextRuns";
    static string IndexGlobal(string suite) => suite == "glyph" ? "__unitextGlyphIndex" : "__unitextTextIndex";
    static string IndexFile(string suite) => $"{suite}-runs-index.js";

    /// <summary>Splits one run's combined JSON into the per-suite streams; a suite with no data is skipped.</summary>
    public static void SaveRun(string json)
    {
        var root = JObject.Parse(json);
        var stamp = $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Application.platform}-{Sanitize(SystemInfo.deviceName)}";
        foreach (var suite in Suites)
            SaveStream(suite, root, $"run-{suite}-{stamp}.js");
        RebuildIndex();
    }

    static void SaveStream(string suite, JObject root, string fileName)
    {
        var (keep, drop) = Sections(suite);
        if (root[keep] is not JObject section || section.Count == 0) return;

        var clone = (JObject)root.DeepClone();
        clone.Remove(drop);
        clone["suite"] = suite;
        WriteRunFile(suite, fileName, clone.ToString(Formatting.Indented));
        Debug.Log($"[BenchmarkHistory] {suite} run saved to Benchmarks/runs/{fileName}");
    }

    [MenuItem("Tools/UniText/Benchmarks/Import Run JSON...")]
    static void ImportRunJson()
    {
        var path = EditorUtility.OpenFilePanel("Import benchmark run", "", "json");
        if (string.IsNullOrEmpty(path)) return;

        var root = JObject.Parse(File.ReadAllText(path));
        var stamp = $"imported-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Sanitize(Path.GetFileNameWithoutExtension(path))}";
        foreach (var suite in Suites)
            SaveStream(suite, root, $"run-{suite}-{stamp}.js");
        RebuildIndex();
    }

    [MenuItem("Tools/UniText/Benchmarks/Rebuild History Index")]
    public static void RebuildIndex()
    {
        foreach (var suite in Suites)
            RebuildIndex(suite);
    }

    static void RebuildIndex(string suite)
    {
        Directory.CreateDirectory(RunsDir);

        var files = Directory.GetFiles(RunsDir, $"run-{suite}-*.js");
        Array.Sort(files, StringComparer.OrdinalIgnoreCase);

        var sb = new StringBuilder();
        sb.AppendLine($"window.{IndexGlobal(suite)} = [");
        foreach (var f in files)
            sb.AppendLine($"  \"runs/{Path.GetFileName(f)}\",");
        sb.AppendLine("];");

        File.WriteAllText(Path.Combine(BenchmarksDir, IndexFile(suite)), sb.ToString());
    }

    [MenuItem("Tools/UniText/Benchmarks/Open History Page")]
    static void OpenHistoryPage()
    {
        var page = Path.Combine(BenchmarksDir, "index.html");
        if (File.Exists(page)) Application.OpenURL("file:///" + page.Replace('\\', '/'));
        else Debug.LogWarning($"[BenchmarkHistory] Viewer not found at {page}");
    }

    static void WriteRunFile(string suite, string fileName, string json)
    {
        Directory.CreateDirectory(RunsDir);
        var g = RunsGlobal(suite);
        var content = $"window.{g} = window.{g} || [];\n" +
                      $"window.{g}.push(\n" + json + "\n);\n";
        File.WriteAllText(Path.Combine(RunsDir, fileName), content);
    }

    static string Sanitize(string s)
    {
        if (string.IsNullOrEmpty(s)) return "unknown";
        var sb = new StringBuilder(s.Length);
        foreach (var c in s)
            sb.Append(char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '-');
        return sb.ToString();
    }
}
#endif
