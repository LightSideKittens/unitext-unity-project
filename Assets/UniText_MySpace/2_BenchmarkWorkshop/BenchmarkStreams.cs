using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

/// <summary>
/// Splits a run's combined result JSON into per-suite site streams for the benchmark viewer integrations
/// (<c>run-text-*.js</c>, <c>run-glyph-*.js</c>, and <c>run-motion-*.js</c>, each pushing into its own window
/// global). Runtime — not editor-gated — so a device build writes drop-in site files next to
/// <c>benchmarkResults.json</c>, carrying the full meta/systemInfo into each stream. A suite with no data is omitted.
/// </summary>
public static class BenchmarkStreams
{
    static readonly string[] Suites = { "text", "glyph", "motion" };
    static readonly string[] Sections = { "textBenchmarks", "glyphRasterization", "motionBenchmarks" };

    static (string keep, string global) Spec(string suite) => suite switch
    {
        "text" => ("textBenchmarks", "__unitextTextRuns"),
        "glyph" => ("glyphRasterization", "__unitextGlyphRuns"),
        "motion" => ("motionBenchmarks", "__unitextMotionRuns"),
        _ => throw new ArgumentOutOfRangeException(nameof(suite), suite, null)
    };

    /// <summary>Per non-empty suite: its site .js file name and contents (the combined JSON trimmed to that suite, wrapped as a push into the suite global).</summary>
    public static List<(string suite, string fileName, string contents)> Split(string combinedJson, string stamp)
    {
        var root = JObject.Parse(combinedJson);
        var files = new List<(string, string, string)>();

        foreach (var suite in Suites)
        {
            var (keep, global) = Spec(suite);
            if (root[keep] is not JObject section || section.Count == 0) continue;

            var clone = (JObject)root.DeepClone();
            foreach (string sectionName in Sections)
                if (sectionName != keep)
                    clone.Remove(sectionName);
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
