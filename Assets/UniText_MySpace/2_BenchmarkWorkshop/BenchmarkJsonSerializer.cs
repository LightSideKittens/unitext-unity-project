using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

public static class BenchmarkJsonSerializer
{
    public static string Serialize(BenchmarkRunData data)
    {
        var root = new JObject
        {
            ["version"] = "1.1",
            ["timestamp"] = data.timestamp,
            ["meta"] = new JObject
            {
                ["utc"] = data.utc,
                ["commit"] = data.commit,
                ["branch"] = data.branch,
                ["dirty"] = data.dirty,
                ["submoduleCommit"] = data.submoduleCommit,
                ["submoduleBranch"] = data.submoduleBranch,
                ["submoduleDirty"] = data.submoduleDirty,
                ["source"] = data.source
            },
            ["systemInfo"] = SerializeSystemInfo(),
            ["config"] = new JObject
            {
                ["objectCount"] = data.objectCount,
                ["iterations"] = data.iterations,
                ["warmupIterations"] = data.warmupIterations
            },
            ["textBenchmarks"] = SerializeTextBenchmarks(data.textBenchmarks),
            ["glyphRasterization"] = SerializeGlyphRasterization(data.glyphRasterization),
            ["phaseNotes"] = new JObject
            {
                ["fullRebuild"] = "Incremental edit of one document — warm caches where an engine has them (UniText reuses unchanged-paragraph shaping).",
                ["fullRebuildUnique"] = "Every paragraph unique per iteration — the cold path for all engines.",
                ["fullRebuildRichText"] = "Unique variants of the corpus saturated with markup (91 tag pairs: <b> <i> <u> <s> <color=#hex> <size=%> <sub> <sup> <uppercase> <lowercase>) parsed by all three engines with byte-identical syntax (UniText via registered tag styles, TMP/UIToolkit richText). The previous phase measures the same content tag-free, so the delta is the markup cost.",
                ["meshRebuild"] = "Engine-incremental color change: UniText re-emits mesh only, TMP re-runs full layout, UIToolkit repaints tint — different work classes by design.",
                ["corpus.multilingual"] = "Arabic/bidi/emoji showcase: UniText produces correct shaped output; TMP and legacy UIToolkit do not shape or reorder this text — visual output is NOT equivalent.",
                ["corpus.latin"] = "Plain Latin text — every engine performs comparable work; the apples-to-apples case.",
                ["glyphRasterization"] = "Clear the glyph atlas, time re-rasterizing the shared corpus. UIToolkit and TMP funnel through the same TextCore FreeType path (apples-to-apples). UniText rasterizes async on the GPU — its e2e time is a different-architecture reference, not a like-for-like figure."
            },
            ["errors"] = new JArray(data.errors.ToArray())
        };

        return root.ToString(Formatting.Indented);
    }

    static JObject SerializeSystemInfo()
    {
        string backend;
#if ENABLE_IL2CPP
        backend = "IL2CPP";
#else
        backend = "Mono";
#endif

        return new JObject
        {
            ["deviceModel"] = SystemInfo.deviceModel,
            ["deviceName"] = SystemInfo.deviceName,
            ["operatingSystem"] = SystemInfo.operatingSystem,
            ["processorType"] = SystemInfo.processorType,
            ["processorCount"] = SystemInfo.processorCount,
            ["processorFrequency"] = SystemInfo.processorFrequency,
            ["systemMemorySize"] = SystemInfo.systemMemorySize,
            ["graphicsDeviceName"] = SystemInfo.graphicsDeviceName,
            ["graphicsDeviceType"] = SystemInfo.graphicsDeviceType.ToString(),
            ["graphicsMemorySize"] = SystemInfo.graphicsMemorySize,
            ["graphicsDeviceVersion"] = SystemInfo.graphicsDeviceVersion,
            ["screenWidth"] = Screen.width,
            ["screenHeight"] = Screen.height,
            ["screenDpi"] = Screen.dpi,
            ["unityVersion"] = Application.unityVersion,
            ["scriptingBackend"] = backend,
            ["platform"] = Application.platform.ToString(),
            ["isEditor"] = Application.isEditor,
            ["isDebugBuild"] = UnityEngine.Debug.isDebugBuild,
            ["unitextDebugDefine"] =
#if UNITEXT_DEBUG
                true
#else
                false
#endif
        };
    }

    static JObject SerializeTextBenchmarks(Dictionary<string, TextBenchmarkBase.TestResults> benchmarks)
    {
        var obj = new JObject();
        foreach (var kvp in benchmarks)
            obj[kvp.Key] = SerializeTestResults(kvp.Value);
        return obj;
    }

    static JObject SerializeTestResults(TextBenchmarkBase.TestResults r)
    {
        return new JObject
        {
            ["creation"] = SerializeMetrics(r.creation),
            ["destruction"] = SerializeMetrics(r.destruction),
            ["fullRebuild"] = SerializeMetrics(r.fullRebuild),
            ["fullRebuildUnique"] = SerializeMetrics(r.fullRebuildUnique),
            ["fullRebuildRichText"] = SerializeMetrics(r.fullRebuildRichText),
            ["layoutWrapNoAuto"] = SerializeMetrics(r.layoutWrapNoAuto),
            ["layoutWrapAuto"] = SerializeMetrics(r.layoutWrapAuto),
            ["layoutNoWrapNoAuto"] = SerializeMetrics(r.layoutNoWrapNoAuto),
            ["layoutNoWrapAuto"] = SerializeMetrics(r.layoutNoWrapAuto),
            ["meshRebuild"] = SerializeMetrics(r.meshRebuild)
        };
    }

    static JObject SerializeMetrics(TextBenchmarkBase.TestMetrics m)
    {
        var times = m.frameTimes ?? new List<float>();
        var sorted = new List<float>(times);
        sorted.Sort();

        float median = sorted.Count > 0 ? sorted[sorted.Count / 2] : 0;
        float min = sorted.Count > 0 ? sorted[0] : 0;
        float max = sorted.Count > 0 ? sorted[sorted.Count - 1] : 0;

        return new JObject
        {
            ["totalMs"] = m.TotalTime,
            ["frameTimes"] = new JArray(times.ToArray()),
            ["median"] = median,
            ["min"] = min,
            ["max"] = max,
            ["totalAlloc"] = m.totalAlloc,
            ["managedAlloc"] = m.managedAlloc,
            ["gc"] = new JArray(m.gcGen0, m.gcGen1, m.gcGen2)
        };
    }

    static JObject SerializeGlyphRasterization(Dictionary<string, Dictionary<string, GlyphRasterData>> data)
    {
        var obj = new JObject();
        foreach (var engine in data)
        {
            var byFont = new JObject();
            foreach (var font in engine.Value)
                if (font.Value?.frameTimes != null)
                    byFont[font.Key] = SerializeGlyphRaster(font.Value);
            if (byFont.Count > 0)
                obj[engine.Key] = byFont;
        }
        return obj;
    }

    static JObject SerializeGlyphRaster(GlyphRasterData d)
    {
        var sorted = new List<float>(d.frameTimes);
        sorted.Sort();

        float median = sorted.Count > 0 ? sorted[sorted.Count / 2] : 0;
        float min = sorted.Count > 0 ? sorted[0] : 0;
        float max = sorted.Count > 0 ? sorted[sorted.Count - 1] : 0;
        float sum = 0;
        for (int i = 0; i < sorted.Count; i++) sum += sorted[i];
        float avg = sorted.Count > 0 ? sum / sorted.Count : 0;
        double perGlyphUs = d.uniqueGlyphs > 0 ? (median * 1000.0) / d.uniqueGlyphs : 0;

        var obj = new JObject
        {
            ["frameTimes"] = new JArray(d.frameTimes.ToArray()),
            ["median"] = median,
            ["min"] = min,
            ["max"] = max,
            ["average"] = avg,
            ["uniqueGlyphs"] = d.uniqueGlyphs,
            ["perGlyphMedianUs"] = perGlyphUs,
            ["managedAlloc"] = d.managedAlloc
        };

        if (d.e2eTimes is { Count: > 0 })
        {
            var sortedE2e = new List<float>(d.e2eTimes);
            sortedE2e.Sort();
            obj["e2eTimes"] = new JArray(d.e2eTimes.ToArray());
            obj["e2eMedian"] = sortedE2e[sortedE2e.Count / 2];
            obj["perGlyphE2eMedianUs"] = d.uniqueGlyphs > 0 ? (sortedE2e[sortedE2e.Count / 2] * 1000.0) / d.uniqueGlyphs : 0;
        }

        return obj;
    }
}

public class BenchmarkRunData
{
    public string timestamp;
    public string utc;
    public string commit = "unknown";
    public string branch = "unknown";
    public bool dirty;
    public string submoduleCommit = "unknown";
    public string submoduleBranch = "unknown";
    public bool submoduleDirty;
    public string source = "unknown";
    public int objectCount;
    public int iterations;
    public int warmupIterations;
    public Dictionary<string, TextBenchmarkBase.TestResults> textBenchmarks = new();
    public Dictionary<string, Dictionary<string, GlyphRasterData>> glyphRasterization = new();
    public List<string> errors = new();
}

public class GlyphRasterData
{
    public List<float> frameTimes;

    /// <summary>End-to-end times (CPU dispatch + GPU raster completion); empty for engines whose rasterization is fully synchronous inside <see cref="frameTimes"/>.</summary>
    public List<float> e2eTimes;

    public int uniqueGlyphs;
    public long managedAlloc;
}
