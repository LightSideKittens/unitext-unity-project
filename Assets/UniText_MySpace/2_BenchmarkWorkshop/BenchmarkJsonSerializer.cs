using System;
using System.Collections.Generic;
using System.Text;
using LightSide;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Unity.Jobs.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

public static class BenchmarkJsonSerializer
{
    public static string Serialize(BenchmarkRunData data) => Serialize(data, out _);

    internal static string Serialize(BenchmarkRunData data, out string gpuUploadSummary)
    {
        var gpuUpload = SerializeGpuUpload();
        gpuUploadSummary = "[Benchmark GpuUpload Post-Run] " + gpuUpload.ToString(Formatting.None);
        var root = new JObject
        {
            ["version"] = "1.3",
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
            ["systemInfo"] = SerializeSystemInfo(gpuUpload),
            ["config"] = new JObject
            {
                ["objectCount"] = data.objectCount,
                ["iterations"] = data.iterations,
                ["warmupIterations"] = data.warmupIterations,
                ["memoryProbeRepeats"] = data.memoryProbeRepeats
            },
            ["textBenchmarks"] = SerializeTextBenchmarks(data.textBenchmarks),
            ["glyphRasterization"] = SerializeGlyphRasterization(data.glyphRasterization),
            ["phaseNotes"] = new JObject
            {
                ["fullRebuild"] = "Incremental edit of one document — warm caches where an engine has them (UniText reuses unchanged-paragraph shaping).",
                ["fullRebuildUnique"] = "Every paragraph unique per iteration — the cold path for all engines.",
                ["fullRebuildRichText"] = "Unique variants of the corpus saturated with markup (91 tag pairs: <b> <i> <u> <s> <color=#hex> <size=%> <sub> <sup> <uppercase> <lowercase>) parsed by all three engines with byte-identical syntax (UniText via registered tag styles, TMP/UIToolkit richText). The previous phase measures the same content tag-free, so the delta is the markup cost.",
                ["meshRebuild"] = "Engine-incremental color change: UniText re-emits mesh only, TMP re-runs full layout, UIToolkit repaints tint — different work classes by design.",
                ["corpus.multilingual"] = "Arabic/bidi/emoji showcase. UniText and UIToolkit both shape it (this project enables UITK's Advanced Text Generator: HarfBuzz+ICU, see UIToolkitProjectSettings) — like-for-like on shaping. TMP has no shaper — its output is NOT equivalent.",
                ["corpus.latin"] = "Plain Latin text — every engine performs comparable work; the apples-to-apples case.",
                ["memory"] = "Resident is the OS-reported app footprint. Retained is positive post-GC growth between equal live states across warmup and measured work; phase setup net change is reported separately. GC reclaimed is managed garbage at the state-normalized checkpoint. Repeat growth is a leak candidate from an identical state-normalized untimed cycle, not proof of a leak. Deep profiler capture runs separately after these checkpoints.",
                ["glyphRasterization"] = "Every engine starts from a cleared atlas and a disabled pre-created text component, then rasterization is triggered only by enabling that component. CPU trigger/dispatch and component-to-atlas-ready latency are separated where completion is deferred. The recorded execution samples are authoritative for CPU/GPU raster, atlas write path, CPU mirror residency, GpuUpload use, and completion method.",
                ["fontIsolation"] = "UI Toolkit uses explicit Panel Text Settings with local/global/default/sprite/emoji/Dynamic OS fallbacks disabled; TMP and UniText disable their corresponding fallback sources for the glyph suite."
            },
            ["errors"] = new JArray(data.errors.ToArray())
        };

        return root.ToString(Formatting.Indented);
    }

    static JObject SerializeSystemInfo(JObject gpuUpload)
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
            ["graphicsDeviceVendor"] = SystemInfo.graphicsDeviceVendor,
            ["graphicsDeviceType"] = SystemInfo.graphicsDeviceType.ToString(),
            ["graphicsMemorySize"] = SystemInfo.graphicsMemorySize,
            ["graphicsDeviceVersion"] = SystemInfo.graphicsDeviceVersion,
            ["graphicsShaderLevel"] = SystemInfo.graphicsShaderLevel,
            ["graphicsMultiThreaded"] = SystemInfo.graphicsMultiThreaded,
            ["renderingThreadingMode"] = SystemInfo.renderingThreadingMode.ToString(),
            ["screenWidth"] = Screen.width,
            ["screenHeight"] = Screen.height,
            ["screenDpi"] = Screen.dpi,
            ["targetFrameRate"] = Application.targetFrameRate,
            ["vSyncCount"] = QualitySettings.vSyncCount,
            ["colorSpace"] = QualitySettings.activeColorSpace.ToString(),
            ["unityVersion"] = Application.unityVersion,
            ["scriptingBackend"] = backend,
            ["platform"] = Application.platform.ToString(),
            ["isEditor"] = Application.isEditor,
            ["isBatchMode"] = Application.isBatchMode,
            ["isDebugBuild"] = UnityEngine.Debug.isDebugBuild,
            ["jobWorkerCount"] = JobsUtility.JobWorkerCount,
            ["jobWorkerMaximumCount"] = JobsUtility.JobWorkerMaximumCount,
            ["graphicsCapabilities"] = new JObject
            {
                ["computeShaders"] = SystemInfo.supportsComputeShaders,
                ["texture2DArray"] = SystemInfo.supports2DArrayTextures,
                ["maxComputeBufferInputs"] = SystemInfo.maxComputeBufferInputsCompute,
                ["randomWriteRHalf"] = SystemInfo.SupportsRandomWriteOnRenderTextureFormat(RenderTextureFormat.RHalf),
                ["randomWriteArgbHalf"] = SystemInfo.SupportsRandomWriteOnRenderTextureFormat(RenderTextureFormat.ARGBHalf),
                ["copyTextureSupport"] = SystemInfo.copyTextureSupport.ToString(),
                ["graphicsFence"] = SystemInfo.supportsGraphicsFence,
                ["asyncGpuReadback"] = SystemInfo.supportsAsyncGPUReadback
            },
            ["gpuUpload"] = gpuUpload,
            ["unitextDebugDefine"] =
#if UNITEXT_DEBUG
                true,
#else
                false,
#endif
            ["unitextProfileDefine"] =
#if UNITEXT_PROFILE
                true,
#else
                false,
#endif
            ["lightsideDebugDefine"] =
#if LIGHTSIDE_DEBUG
                true
#else
                false
#endif
        };
    }

    static JObject SerializeGpuUpload()
    {
        bool supported = GpuUpload.IsSupported;
        bool ready = supported && GpuUpload.IsReady;
        var obj = new JObject
        {
            ["observation"] = "postRunProbe",
            ["probeMayInitializeBackend"] = true,
            ["supported"] = supported,
            ["ready"] = ready,
            ["poolGeneration"] = GpuUpload.PoolGeneration.ToString()
        };
        if (!supported) return obj;

        var info = GpuUpload.Info;
        obj["renderer"] = info.Renderer.ToString();
        obj["abi"] = $"{info.AbiMajor}.{info.AbiMinor}";
        obj["capabilities"] = info.Capabilities.ToString();
        obj["graphicsDeviceEpoch"] = info.GraphicsDeviceEpoch.ToString();
        obj["maxStagingBytes"] = info.MaxStagingBytes.ToString();
        obj["maxConcurrentSubmissions"] = info.MaxConcurrentSubmissions;
        obj["rHalfTexture2DArray"] = GpuUpload.Supports(GraphicsFormat.R16_SFloat, TextureDimension.Tex2DArray);
        obj["rgbaHalfTexture2DArray"] = GpuUpload.Supports(GraphicsFormat.R16G16B16A16_SFloat, TextureDimension.Tex2DArray);

        if (GpuUpload.TryGetStats(out var stats, out var error))
            obj["stats"] = new JObject
            {
                ["submissionsAccepted"] = stats.SubmissionsAccepted.ToString(),
                ["submissionsRejected"] = stats.SubmissionsRejected.ToString(),
                ["submissionsEncoded"] = stats.SubmissionsEncoded.ToString(),
                ["backpressureCount"] = stats.BackpressureCount.ToString(),
                ["encodedPayloadBytes"] = stats.EncodedPayloadBytes.ToString(),
                ["poolNodes"] = stats.PoolNodes.ToString(),
                ["poolNodesFree"] = stats.PoolNodesFree.ToString(),
                ["poolNodesInFlight"] = stats.PoolNodesInFlight.ToString()
            };
        else
            obj["statsError"] = error.ToString();
        return obj;
    }

    internal static string EnvironmentSummary()
    {
        var sb = new StringBuilder(512);
        sb.AppendLine("[Benchmark Environment]");
        sb.Append("Unity ").Append(Application.unityVersion).Append(" | ").Append(Application.platform)
          .Append(" | ").Append(Application.isEditor ? "Editor" : "Player")
          .Append(" | ").Append(UnityEngine.Debug.isDebugBuild ? "Debug" : "Release").AppendLine();
        sb.Append("CPU: ").Append(SystemInfo.processorType).Append(" | logical=").Append(SystemInfo.processorCount)
          .Append(" | MHz=").Append(SystemInfo.processorFrequency)
          .Append(" | jobs=").Append(JobsUtility.JobWorkerCount).Append('/').Append(JobsUtility.JobWorkerMaximumCount).AppendLine();
        sb.Append("GPU: ").Append(SystemInfo.graphicsDeviceName).Append(" | ").Append(SystemInfo.graphicsDeviceType)
          .Append(" | ").Append(SystemInfo.renderingThreadingMode)
          .Append(" | graphicsMT=").Append(SystemInfo.graphicsMultiThreaded).AppendLine();
        sb.Append("Frame pacing: target=").Append(Application.targetFrameRate).Append(" | vSync=").Append(QualitySettings.vSyncCount)
          .Append(" | resolution=").Append(Screen.width).Append('x').Append(Screen.height).AppendLine();
        sb.Append("Graphics gates: compute=").Append(SystemInfo.supportsComputeShaders)
          .Append(" | texture2DArray=").Append(SystemInfo.supports2DArrayTextures)
          .Append(" | computeBuffers=").Append(SystemInfo.maxComputeBufferInputsCompute)
          .Append(" | copyTexture=").Append(SystemInfo.copyTextureSupport)
          .Append(" | fence=").Append(SystemInfo.supportsGraphicsFence)
          .Append(" | asyncReadback=").Append(SystemInfo.supportsAsyncGPUReadback);
        return sb.ToString();
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
        var obj = new JObject
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
        if (r.memory.available)
            obj["memory"] = SerializeRunMemory(r.memory);
        return obj;
    }

    static JObject SerializeMetrics(TextBenchmarkBase.TestMetrics m)
    {
        var times = m.frameTimes ?? new List<float>();
        var sorted = new List<float>(times);
        sorted.Sort();

        float median = sorted.Count > 0 ? sorted[sorted.Count / 2] : 0;
        float min = sorted.Count > 0 ? sorted[0] : 0;
        float max = sorted.Count > 0 ? sorted[sorted.Count - 1] : 0;

        var obj = new JObject
        {
            ["totalMs"] = m.TotalTime,
            ["frameTimes"] = new JArray(times.ToArray()),
            ["median"] = median,
            ["min"] = min,
            ["max"] = max,
            ["managedAlloc"] = Number(m.managedAlloc),
            ["gc"] = new JArray(m.gcGen0, m.gcGen1, m.gcGen2)
        };
        if (m.memory.available)
            obj["memory"] = SerializePhaseMemory(m.memory);
        return obj;
    }

    internal static string SerializeMemoryProfile(string engine, string corpus, string phase, string runId,
        string checkpoint, TextBenchmarkBase.PhaseMemoryMetrics memory) => new JObject
    {
        ["version"] = "1.1",
        ["kind"] = "phase-memory",
        ["runId"] = runId,
        ["checkpoint"] = checkpoint,
        ["engine"] = engine,
        ["corpus"] = corpus,
        ["phase"] = phase,
        ["memory"] = SerializePhaseMemory(memory)
    }.ToString(Formatting.Indented);

    internal static string SerializeMemoryProfileText(string engine, string corpus, string phase, string runId,
        string checkpoint, TextBenchmarkBase.PhaseMemoryMetrics memory)
    {
        var sb = new StringBuilder(1024);
        sb.Append("memory: ").Append(engine).Append('/').Append(corpus).Append('/').Append(phase).AppendLine();
        sb.Append("run: ").Append(runId).Append(" | checkpoint: ").Append(checkpoint).AppendLine();
        AppendSnapshot(sb, "before phase setup", memory.beforeWarmup);
        AppendSnapshot(sb, "state-normalized baseline + GC", memory.normalizedBaseline);
        AppendSnapshot(sb, "after warmup + GC", memory.afterWarmup);
        AppendSnapshot(sb, "measured peak", memory.measuredPeak);
        AppendSnapshot(sb, "measured end before state normalization", memory.measuredEnd);
        AppendSnapshot(sb, "state-normalized end before GC", memory.normalizedEnd);
        AppendSnapshot(sb, "after measured + GC", memory.afterMeasured);
        AppendDelta(sb, "phase setup net delta", memory.beforeWarmup, memory.normalizedBaseline);
        AppendDelta(sb, "warmup post-GC delta", memory.normalizedBaseline, memory.afterWarmup);
        AppendDelta(sb, "measured post-GC delta", memory.afterWarmup, memory.afterMeasured);
        AppendDelta(sb, "total state-normalized post-GC delta", memory.normalizedBaseline, memory.afterMeasured);
        sb.Append("retained candidate (Unity used): ")
          .Append(FormatDelta(memory.afterMeasured.used, memory.normalizedBaseline.used, true)).AppendLine();
        sb.Append("GC garbage reclaimed at state-normalized checkpoint: ")
          .Append(FormatDelta(memory.normalizedEnd.gcUsed, memory.afterMeasured.gcUsed, true)).AppendLine();

        var previous = memory.beforeProbes;
        var probeCount = memory.probes?.Count ?? 0;
        for (int i = 0; i < probeCount; i++)
        {
            var cycle = memory.probes[i];
            sb.AppendLine();
            sb.Append("probe ").Append(i + 1).Append(" managed allocation traffic: ")
              .Append(FormatValue(cycle.managedAlloc)).AppendLine();
            AppendSnapshot(sb, "peak", cycle.peak);
            AppendSnapshot(sb, "end", cycle.end);
            AppendSnapshot(sb, "after GC", cycle.afterCollect);
            var before = cycle.before.used >= 0 || cycle.before.resident >= 0 ? cycle.before : previous;
            AppendDelta(sb, "repeat post-GC delta", before, cycle.afterCollect);
            sb.Append("leak candidate (Unity used): ")
              .Append(FormatDelta(cycle.afterCollect.used, before.used, true)).AppendLine();
            sb.Append("GC garbage reclaimed: ").Append(FormatDelta(cycle.end.gcUsed, cycle.afterCollect.gcUsed, true)).AppendLine();
            previous = cycle.afterCollect;
        }
        return sb.ToString();
    }

    internal static string SerializeRunMemoryProfile(string engine, string corpus, string runId,
        TextBenchmarkBase.RunMemoryMetrics memory) => new JObject
    {
        ["version"] = "1.1",
        ["kind"] = "run-memory",
        ["runId"] = runId,
        ["checkpoint"] = "run",
        ["engine"] = engine,
        ["corpus"] = corpus,
        ["memory"] = SerializeRunMemory(memory)
    }.ToString(Formatting.Indented);

    internal static string SerializeRunMemoryProfileText(string engine, string corpus, string runId,
        TextBenchmarkBase.RunMemoryMetrics memory)
    {
        var sb = new StringBuilder(512);
        sb.Append("run memory: ").Append(engine).Append('/').Append(corpus).AppendLine();
        sb.Append("run: ").Append(runId).Append(" | checkpoint: run").AppendLine();
        AppendSnapshot(sb, "baseline", memory.baseline);
        AppendSnapshot(sb, "sampled workload peak", memory.peak);
        AppendSnapshot(sb, "before cleanup", memory.beforeCleanup);
        AppendSnapshot(sb, "after cleanup + GC", memory.afterCleanup);
        AppendDelta(sb, "post-run app delta (includes retained benchmark results and optional profile replay)", memory.baseline, memory.afterCleanup);
        return sb.ToString();
    }

    static JObject SerializePhaseMemory(TextBenchmarkBase.PhaseMemoryMetrics memory)
    {
        var probes = new JArray();
        var derivedProbes = new JArray();
        var previous = memory.beforeProbes;
        if (memory.probes != null)
            foreach (var probe in memory.probes)
            {
                probes.Add(new JObject
                {
                    ["before"] = SerializeSnapshot(probe.before),
                    ["peak"] = SerializeSnapshot(probe.peak),
                    ["end"] = SerializeSnapshot(probe.end),
                    ["afterCollect"] = SerializeSnapshot(probe.afterCollect),
                    ["managedAlloc"] = Number(probe.managedAlloc)
                });
                var before = probe.before.used >= 0 || probe.before.resident >= 0 ? probe.before : previous;
                derivedProbes.Add(new JObject
                {
                    ["peakDelta"] = SerializeDelta(before, probe.peak),
                    ["postGcGrowth"] = SerializeDelta(before, probe.afterCollect),
                    ["leakCandidateUnityUsed"] = PositiveDelta(before.used, probe.afterCollect.used),
                    ["gcReclaimed"] = Reclaimed(probe.end.gcUsed, probe.afterCollect.gcUsed)
                });
                previous = probe.afterCollect;
            }

        return new JObject
        {
            ["beforeWarmup"] = SerializeSnapshot(memory.beforeWarmup),
            ["normalizedBaseline"] = SerializeSnapshot(memory.normalizedBaseline),
            ["afterWarmup"] = SerializeSnapshot(memory.afterWarmup),
            ["measuredPeak"] = SerializeSnapshot(memory.measuredPeak),
            ["measuredEnd"] = SerializeSnapshot(memory.measuredEnd),
            ["normalizedEnd"] = SerializeSnapshot(memory.normalizedEnd),
            ["afterMeasured"] = SerializeSnapshot(memory.afterMeasured),
            ["beforeProbes"] = SerializeSnapshot(memory.beforeProbes),
            ["probes"] = probes,
            ["derived"] = new JObject
            {
                ["phaseSetupNetDelta"] = SerializeDelta(memory.beforeWarmup, memory.normalizedBaseline),
                ["warmupPostGcDelta"] = SerializeDelta(memory.normalizedBaseline, memory.afterWarmup),
                ["measuredPostGcDelta"] = SerializeDelta(memory.afterWarmup, memory.afterMeasured),
                ["retainedCandidate"] = SerializeDelta(memory.normalizedBaseline, memory.afterMeasured),
                ["retainedCandidateUnityUsed"] = PositiveDelta(memory.normalizedBaseline.used, memory.afterMeasured.used),
                ["measuredPeakDelta"] = SerializeDelta(memory.afterWarmup, memory.measuredPeak),
                ["checkpointGcReclaimed"] = Reclaimed(memory.normalizedEnd.gcUsed, memory.afterMeasured.gcUsed),
                ["probes"] = derivedProbes
            }
        };
    }

    static JObject SerializeRunMemory(TextBenchmarkBase.RunMemoryMetrics memory) => new()
    {
        ["baseline"] = SerializeSnapshot(memory.baseline),
        ["peak"] = SerializeSnapshot(memory.peak),
        ["beforeCleanup"] = SerializeSnapshot(memory.beforeCleanup),
        ["afterCleanup"] = SerializeSnapshot(memory.afterCleanup),
        ["derived"] = new JObject
        {
            ["sampledPeakDelta"] = SerializeDelta(memory.baseline, memory.peak),
            ["postRunAppDelta"] = SerializeDelta(memory.baseline, memory.afterCleanup)
        }
    };

    static JObject SerializeSnapshot(TextBenchmarkBase.MemorySnapshot snapshot) => new()
    {
        ["resident"] = Number(snapshot.resident),
        ["unityUsed"] = Number(snapshot.used),
        ["unityReserved"] = Number(snapshot.reserved),
        ["gcUsed"] = Number(snapshot.gcUsed),
        ["gcReserved"] = Number(snapshot.gcReserved),
        ["buffers"] = Number(snapshot.buffers)
    };

    static JObject SerializeDelta(TextBenchmarkBase.MemorySnapshot from, TextBenchmarkBase.MemorySnapshot to) => new()
    {
        ["resident"] = Delta(from.resident, to.resident),
        ["unityUsed"] = Delta(from.used, to.used),
        ["unityReserved"] = Delta(from.reserved, to.reserved),
        ["gcUsed"] = Delta(from.gcUsed, to.gcUsed),
        ["gcReserved"] = Delta(from.gcReserved, to.gcReserved),
        ["buffers"] = Delta(from.buffers, to.buffers)
    };

    static JToken Number(long value) => value >= 0 ? new JValue(value) : JValue.CreateNull();

    static JToken Delta(long from, long to) => from >= 0 && to >= 0
        ? new JValue(to - from)
        : JValue.CreateNull();

    static JToken Reclaimed(long before, long after) => before >= 0 && after >= 0
        ? new JValue(Math.Max(0, before - after))
        : JValue.CreateNull();

    static JToken PositiveDelta(long from, long to) => from >= 0 && to >= 0
        ? new JValue(Math.Max(0, to - from))
        : JValue.CreateNull();

    static void AppendSnapshot(StringBuilder sb, string name, TextBenchmarkBase.MemorySnapshot value)
    {
        sb.Append(name).Append(": resident ").Append(FormatValue(value.resident))
          .Append(" | Unity used ").Append(FormatValue(value.used))
          .Append(" | reserved ").Append(FormatValue(value.reserved))
          .Append(" | GC ").Append(FormatValue(value.gcUsed)).Append('/').Append(FormatValue(value.gcReserved))
          .Append(" | buffers ").Append(FormatValue(value.buffers))
          .AppendLine();
    }

    static void AppendDelta(StringBuilder sb, string name, TextBenchmarkBase.MemorySnapshot from,
        TextBenchmarkBase.MemorySnapshot to)
    {
        sb.Append(name).Append(": resident ").Append(FormatDelta(to.resident, from.resident))
          .Append(" | Unity used ").Append(FormatDelta(to.used, from.used))
          .Append(" | reserved ").Append(FormatDelta(to.reserved, from.reserved))
          .Append(" | GC ").Append(FormatDelta(to.gcUsed, from.gcUsed))
          .Append(" | buffers ").Append(FormatDelta(to.buffers, from.buffers))
          .AppendLine();
    }

    static string FormatValue(long value) => value >= 0 ? TextBenchmarkBase.FormatBytes(value) : "n/a";

    static string FormatDelta(long to, long from, bool positiveOnly = false)
    {
        if (to < 0 || from < 0) return "n/a";
        var delta = to - from;
        if (positiveOnly) delta = Math.Max(0, delta);
        return TextBenchmarkBase.FormatBytes(delta);
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

        if (d.benchmark != null)
            obj["benchmark"] = new JObject
            {
                ["mode"] = d.benchmark.mode,
                ["iterations"] = d.benchmark.iterations,
                ["warmupIterations"] = d.benchmark.warmupIterations,
                ["previewFrames"] = d.benchmark.previewFrames,
                ["captureProfile"] = d.benchmark.captureProfile,
                ["captureAlloc"] = d.benchmark.captureAlloc,
                ["captureSample"] = d.benchmark.captureSample
            };
        if (d.executionSamples is { Count: > 0 })
        {
            var samples = new JArray();
            foreach (var sample in d.executionSamples)
                samples.Add(SerializeExecutionSample(sample));
            obj["executionSamples"] = samples;
        }

        return obj;
    }

    static JObject SerializeExecutionSample(GlyphExecutionSample sample)
    {
        var atlases = new JArray();
        if (sample.atlases != null)
            foreach (var atlas in sample.atlases)
                atlases.Add(SerializeAtlasExecution(atlas));
        var obj = new JObject
        {
            ["trigger"] = sample.trigger,
            ["rasterBackend"] = sample.rasterBackend,
            ["threading"] = sample.threading,
            ["completion"] = sample.completion,
            ["fallback"] = sample.fallback,
            ["atlases"] = atlases
        };
        if (sample.gpuSubmissionBudgetMs > 0)
            obj["gpuSubmissionBudgetMs"] = sample.gpuSubmissionBudgetMs;
        return obj;
    }

    static JObject SerializeAtlasExecution(GlyphAtlasExecutionData atlas)
    {
        var obj = new JObject
        {
            ["mode"] = atlas.mode,
            ["backend"] = atlas.backend,
            ["storage"] = atlas.storage,
            ["preparation"] = atlas.preparation,
            ["writePaths"] = new JArray((atlas.writePaths ?? new List<string>()).ToArray())
        };
        Add(obj, "cpuMirror", atlas.cpuMirror);
        Add(obj, "gpuUploadTarget", atlas.gpuUploadTarget);
        Add(obj, "cpuRasterizationForced", atlas.cpuRasterizationForced);
        Add(obj, "computeDirectBatches", atlas.computeDirectBatches);
        Add(obj, "gpuUploadBatches", atlas.gpuUploadBatches);
        Add(obj, "copyTextureRegions", atlas.copyTextureRegions);
        Add(obj, "readableApplyFlushes", atlas.readableApplyFlushes);
        Add(obj, "gpuUploadFallbacks", atlas.gpuUploadFallbacks);
        if (!string.IsNullOrEmpty(atlas.lastGpuUploadError))
            obj["lastGpuUploadError"] = atlas.lastGpuUploadError;
        return obj;
    }

    static void Add(JObject obj, string name, bool? value)
    {
        if (value.HasValue) obj[name] = value.Value;
    }

    static void Add(JObject obj, string name, int? value)
    {
        if (value.HasValue) obj[name] = value.Value;
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
    public int memoryProbeRepeats;
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
    public GlyphBenchmarkConfig benchmark;
    public List<GlyphExecutionSample> executionSamples;
}

public sealed class GlyphBenchmarkConfig
{
    public string mode;
    public int iterations;
    public int warmupIterations;
    public int previewFrames;
    public bool captureProfile;
    public bool captureAlloc;
    public bool captureSample;
}

public sealed class GlyphExecutionSample
{
    public string trigger;
    public string rasterBackend;
    public string threading;
    public string completion;
    public string fallback;
    public float gpuSubmissionBudgetMs;
    public List<GlyphAtlasExecutionData> atlases = new();
}

public sealed class GlyphAtlasExecutionData
{
    public string mode;
    public string backend;
    public string storage;
    public string preparation;
    public bool? cpuMirror;
    public bool? gpuUploadTarget;
    public bool? cpuRasterizationForced;
    public List<string> writePaths = new();
    public int? computeDirectBatches;
    public int? gpuUploadBatches;
    public int? copyTextureRegions;
    public int? readableApplyFlushes;
    public int? gpuUploadFallbacks;
    public string lastGpuUploadError;
}
