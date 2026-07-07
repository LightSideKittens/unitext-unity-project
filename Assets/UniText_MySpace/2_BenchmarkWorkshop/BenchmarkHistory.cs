#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Committed benchmark history under &lt;project&gt;/Benchmarks: one .js file per run (the file://
/// viewer loads runs via script tags — pages opened from disk cannot fetch JSON) plus a generated
/// runs-index.js. Editor runs save automatically; CI artifacts come in through the import menu.
/// </summary>
public static class BenchmarkHistory
{
    static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;
    static string BenchmarksDir => Path.Combine(ProjectRoot, "Benchmarks");
    static string RunsDir => Path.Combine(BenchmarksDir, "runs");

    public static void SaveRun(string json)
    {
        var device = Sanitize(SystemInfo.deviceName);
        var file = $"run-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Application.platform}-{device}.js";
        WriteRunFile(file, json);
        RebuildIndex();
        Debug.Log($"[BenchmarkHistory] Run saved to Benchmarks/runs/{file}");
    }

    [MenuItem("Tools/UniText/Benchmarks/Import Run JSON...")]
    static void ImportRunJson()
    {
        var path = EditorUtility.OpenFilePanel("Import benchmark run", "", "json");
        if (string.IsNullOrEmpty(path)) return;

        var json = File.ReadAllText(path);
        var file = $"run-imported-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Sanitize(Path.GetFileNameWithoutExtension(path))}.js";
        WriteRunFile(file, json);
        RebuildIndex();
        Debug.Log($"[BenchmarkHistory] Imported to Benchmarks/runs/{file}");
    }

    [MenuItem("Tools/UniText/Benchmarks/Rebuild History Index")]
    public static void RebuildIndex()
    {
        Directory.CreateDirectory(RunsDir);

        var files = Directory.GetFiles(RunsDir, "run-*.js");
        Array.Sort(files, StringComparer.OrdinalIgnoreCase);

        var sb = new StringBuilder();
        sb.AppendLine("window.__unitextBenchIndex = [");
        foreach (var f in files)
            sb.AppendLine($"  \"runs/{Path.GetFileName(f)}\",");
        sb.AppendLine("];");

        File.WriteAllText(Path.Combine(BenchmarksDir, "runs-index.js"), sb.ToString());
    }

    [MenuItem("Tools/UniText/Benchmarks/Open History Page")]
    static void OpenHistoryPage()
    {
        var page = Path.Combine(BenchmarksDir, "index.html");
        if (File.Exists(page)) Application.OpenURL("file:///" + page.Replace('\\', '/'));
        else Debug.LogWarning($"[BenchmarkHistory] Viewer not found at {page}");
    }

    static void WriteRunFile(string fileName, string json)
    {
        Directory.CreateDirectory(RunsDir);
        var content = "window.__unitextBenchRuns = window.__unitextBenchRuns || [];\n" +
                      "window.__unitextBenchRuns.push(\n" + json + "\n);\n";
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
