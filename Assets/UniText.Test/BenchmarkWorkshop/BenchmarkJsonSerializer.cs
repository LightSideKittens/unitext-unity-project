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
            ["version"] = "1.0",
            ["timestamp"] = data.timestamp,
            ["systemInfo"] = SerializeSystemInfo(),
            ["config"] = new JObject
            {
                ["objectCount"] = data.objectCount,
                ["iterations"] = data.iterations,
                ["warmupIterations"] = data.warmupIterations
            },
            ["textBenchmarks"] = SerializeTextBenchmarks(data.textBenchmarks),
            ["glyphRasterization"] = SerializeGlyphRasterization(data.glyphRasterization),
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
            ["platform"] = Application.platform.ToString()
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

    static JObject SerializeGlyphRasterization(Dictionary<string, GlyphRasterData> data)
    {
        var obj = new JObject();
        foreach (var kvp in data)
            obj[kvp.Key] = SerializeGlyphRaster(kvp.Value);
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

        return new JObject
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
    }
}

public class BenchmarkRunData
{
    public string timestamp;
    public int objectCount;
    public int iterations;
    public int warmupIterations;
    public Dictionary<string, TextBenchmarkBase.TestResults> textBenchmarks = new();
    public Dictionary<string, GlyphRasterData> glyphRasterization = new();
    public List<string> errors = new();
}

public class GlyphRasterData
{
    public List<float> frameTimes;
    public int uniqueGlyphs;
    public long managedAlloc;
}
