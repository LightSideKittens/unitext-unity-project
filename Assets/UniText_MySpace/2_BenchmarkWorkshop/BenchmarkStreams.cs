using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

/// <summary>
/// Splits a run's combined result JSON into the per-suite site streams the HTML viewer loads
/// (<c>run-text-*.js</c> / <c>run-glyph-*.js</c>, each pushing into its own window global). Runtime — not
/// editor-gated — so a device build writes drop-in site files next to <c>benchmarkResults.json</c>, carrying
/// the full meta/systemInfo (platform, environment, commit) into each stream. A suite with no data is omitted.
/// </summary>
public static class BenchmarkStreams
{
    static readonly string[] Suites = { "text", "glyph" };

    static (string keep, string drop, string global) Spec(string suite) => suite == "glyph"
        ? ("glyphRasterization", "textBenchmarks", "__unitextGlyphRuns")
        : ("textBenchmarks", "glyphRasterization", "__unitextTextRuns");

    /// <summary>Per non-empty suite: its site .js file name and contents (the combined JSON trimmed to that suite, wrapped as a push into the suite global).</summary>
    public static List<(string suite, string fileName, string contents)> Split(string combinedJson, string stamp)
    {
        var root = JObject.Parse(combinedJson);
        var files = new List<(string, string, string)>();

        foreach (var suite in Suites)
        {
            var (keep, drop, global) = Spec(suite);
            if (root[keep] is not JObject section || section.Count == 0) continue;

            var clone = (JObject)root.DeepClone();
            clone.Remove(drop);
            clone["suite"] = suite;

            var contents = $"window.{global} = window.{global} || [];\n" +
                           $"window.{global}.push(\n" + clone.ToString(Formatting.Indented) + "\n);\n";
            files.Add((suite, $"run-{suite}-{stamp}.js", contents));
        }

        return files;
    }

    /// <summary>Timestamp+platform+device stem shared by every suite stream from one run.</summary>
    public static string Stamp(string platform, string deviceName) =>
        $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{platform}-{Sanitize(deviceName)}";

    public static string Sanitize(string s)
    {
        if (string.IsNullOrEmpty(s)) return "unknown";
        var sb = new StringBuilder(s.Length);
        foreach (var c in s)
            sb.Append(char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '-');
        return sb.ToString();
    }
}
