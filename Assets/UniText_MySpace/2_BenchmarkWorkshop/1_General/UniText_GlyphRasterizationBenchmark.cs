using System.Collections;
using System.Collections.Generic;
using LightSide;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;

/// <summary>
/// UniText glyph rasterization: clears all live glyph data, then times re-rasterizing the shared
/// glyph corpus (BenchmarkConfig) across the child text objects. Rasterization is async/GPU, so the
/// end-to-end time (CPU dispatch + GPU completion) is reported alongside the CPU frame time.
/// </summary>
public class UniText_GlyphRasterizationBenchmark : GlyphRasterBenchmarkBase
{
    const double CompletionTimeoutSeconds = 15.0;

    [SerializeField] bool forceCPURasterization;

    [SerializeField, Tooltip("Un-mutes the raster Cat zone so the per-pass ContourUnion phase/bail line reaches Logs/unitext.log. Needs the UNITEXT_DEBUG scripting define, else the counters read 0.")]
    bool dumpPhases;

    [SerializeField, Min(0), Tooltip("Diagnostic: when > 0, overrides GlyphAtlasGpuRaster submission budget (ms) for the run. Lower it (e.g. 15) to force fine submission splitting — with dumpPhases on, the log then reveals whether the boundary is available on this backend and how many chunks the heuristic flags oversized. 0 leaves the platform default.")]
    float gpuSubmissionBudgetMsOverride;

    UniText[] targets;
    bool pendingSingleThreaded, pendingMaxStroke;
    bool abortRun;
    bool readbackFallbackLogged;
    string completionMethod;
    float savedBudgetMs;
    bool wasParallel, wasForceST, wasMuted, wasSysDisabled, wasEmojiDisabled;
    List<(UniText text, Style style)> strokeStyles;

    protected override string EngineName => "UniText";
    protected override bool HasE2E => true;

    public IEnumerator RunBenchmarkCoroutine(bool singleThreaded, bool maxStroke = false)
    {
        pendingSingleThreaded = singleThreaded;
        pendingMaxStroke = maxStroke;
        yield return RunPass((singleThreaded ? "SINGLE-THREADED" : "PARALLEL") + (maxStroke ? " + MAX-STROKE" : ""));
    }

    void Run(bool singleThreaded, bool maxStroke)
    {
        if (!isRunning) StartCoroutine(RunBenchmarkCoroutine(singleThreaded, maxStroke));
    }

    [ContextMenu("Run Benchmark (Single-Threaded)")] public void RunBenchmark() => Run(true, false);
    [ContextMenu("Run Benchmark (Parallel)")] public void RunBenchmarkParallel() => Run(false, false);
    [ContextMenu("Run Benchmark (Single-Threaded + Max Stroke)")] public void RunBenchmarkMaxStroke() => Run(true, true);
    [ContextMenu("Run Benchmark (Parallel + Max Stroke)")] public void RunBenchmarkParallelMaxStroke() => Run(false, true);

    protected override void OnBeforeRun()
    {
        abortRun = false;
        readbackFallbackLogged = false;
        completionMethod = "synchronous";
        GlyphAtlas.forceCpuRasterization = forceCPURasterization;
        savedBudgetMs = GlyphAtlasGpuRaster.submissionBudgetMs;
        if (gpuSubmissionBudgetMsOverride > 0f)
            GlyphAtlasGpuRaster.submissionBudgetMs = gpuSubmissionBudgetMsOverride;
        wasParallel = UniTextBase.UseParallel;
        wasForceST = GlyphAtlas.forceSingleThreaded;
        wasMuted = CatZones.MuteAll;
        wasSysDisabled = SystemFont.Disabled;
        wasEmojiDisabled = EmojiFont.Disabled;
        UniTextBase.UseParallel = !pendingSingleThreaded;
        GlyphAtlas.forceSingleThreaded = pendingSingleThreaded;
        CatZones.MuteAll = !dumpPhases;
        SystemFont.Disabled = true;
        EmojiFont.Disabled = true;
    }

    protected override void OnAfterRun()
    {
        if (strokeStyles != null)
        {
            foreach (var (text, style) in strokeStyles)
                text.RemoveStyle(style);
            strokeStyles = null;
        }
        GlyphAtlasGpuRaster.submissionBudgetMs = savedBudgetMs;
        UniTextBase.UseParallel = wasParallel;
        GlyphAtlas.forceSingleThreaded = wasForceST;
        CatZones.MuteAll = wasMuted;
        SystemFont.Disabled = wasSysDisabled;
        EmojiFont.Disabled = wasEmojiDisabled;

        UniTextFont.Core.DisposeAllLive();
    }

    protected override bool CollectTargets()
    {
        var list = new List<UniText>();
        foreach (var ut in GetComponentsInChildren<UniText>(true))
            if (ut.gameObject != gameObject)
                list.Add(ut);
        targets = list.ToArray();

        if (targets.Length == 0)
        {
            Debug.LogError("[UniText GlyphRaster] No UniText children found.");
            return false;
        }

        var glyphText = BenchmarkConfig.Instance != null ? BenchmarkConfig.Instance.GlyphRasterText : null;
        if (!string.IsNullOrEmpty(glyphText))
            foreach (var ut in targets)
                ut.Text = glyphText;

        if (pendingMaxStroke)
        {
            strokeStyles = new List<(UniText, Style)>();
            foreach (var ut in targets)
            {
                var style = Style.WholeText(new StrokeModifier { Width = UnitValue.Em(1f), Align = 1f });
                ut.AddStyle(style);
                strokeStyles.Add((ut, style));
            }
        }
        return true;
    }

    protected override void Deactivate()
    {
        foreach (var ut in targets)
            ut.gameObject.SetActive(false);
    }

    protected override void ClearCaches() => UniTextFont.Core.DisposeAllLive();

    protected override void Rasterize()
    {
        foreach (var ut in targets)
            ut.gameObject.SetActive(true);
        Canvas.ForceUpdateCanvases();
    }

    protected override int CountGlyphs() => GlyphAtlas.GetInstance(UniTextRenderMode.SDF).EntryCount;

    protected override IEnumerator AwaitAsyncCompletion(float cpuMs)
    {
        bool usesGpu = false;
        bool canTrackCompletion = true;
        GlyphAtlas.ForEachInstance(atlas =>
        {
            if (!atlas.UsesGpuRasterization) return;
            usesGpu = true;
            canTrackCompletion &= atlas.CanTrackGpuRasterCompletion;
        });
        if (!usesGpu)
        {
            completionMethod = "synchronous";
            lastE2eMs = cpuMs;
            yield break;
        }
        double dispatchStart = Time.realtimeSinceStartupAsDouble - cpuMs / 1000.0;
        if (!canTrackCompletion)
        {
            completionMethod = "asyncGpuReadback";
            yield return AwaitReadbackCompletion(dispatchStart);
            yield break;
        }

        completionMethod = "graphicsFence";

        var sdf = GlyphAtlas.GetInstance(UniTextRenderMode.SDF);
        var msdf = GlyphAtlas.GetInstance(UniTextRenderMode.MSDF);
        double timeoutAt = Time.realtimeSinceStartupAsDouble + CompletionTimeoutSeconds;
        while (Time.realtimeSinceStartupAsDouble < timeoutAt)
        {
            if (sdf.GpuRasterComplete && msdf.GpuRasterComplete) break;
            if (!sdf.CanTrackGpuRasterCompletion || !msdf.CanTrackGpuRasterCompletion)
            {
                completionMethod = "asyncGpuReadback";
                yield return AwaitReadbackCompletion(dispatchStart);
                yield break;
            }
            yield return null;
        }

        if (!(sdf.GpuRasterComplete && msdf.GpuRasterComplete))
        {
            abortRun = true;
            Debug.LogWarning($"[UniText GlyphRaster] GPU raster did not complete within {CompletionTimeoutSeconds:F0} seconds.");
        }

        lastE2eMs = (float)((Time.realtimeSinceStartupAsDouble - dispatchStart) * 1000.0);
    }

    IEnumerator AwaitReadbackCompletion(double dispatchStart)
    {
        var textures = new List<Texture>(2);
        GlyphAtlas.ForEachInstance(atlas =>
        {
            if (atlas.UsesGpuRasterization && atlas.AtlasTexture != null)
                textures.Add(atlas.AtlasTexture);
        });
        if (textures.Count == 0)
        {
            lastE2eMs = (float)((Time.realtimeSinceStartupAsDouble - dispatchStart) * 1000.0);
            yield break;
        }

        double timeoutAt = Time.realtimeSinceStartupAsDouble + CompletionTimeoutSeconds;
        if (!SystemInfo.supportsAsyncGPUReadback)
        {
            yield return WaitUntil(timeoutAt);
            AbortGpuCompletionMeasurement();
            yield break;
        }

        var requests = new List<AsyncGPUReadbackRequest>(2);
        bool queueFailed = false;
        for (int i = 0; i < textures.Count; i++)
        {
            try
            {
                var texture = textures[i];
                if (!SystemInfo.IsFormatSupported(texture.graphicsFormat, FormatUsage.ReadPixels))
                {
                    queueFailed = true;
                    Debug.LogWarning($"[UniText GlyphRaster] Async readback completion probe does not support {texture.graphicsFormat}.");
                    continue;
                }
                int depth = texture is RenderTexture rt ? rt.volumeDepth : ((Texture2DArray)texture).depth;
                requests.Add(AsyncGPUReadback.Request(texture, 0, 0, 1, 0, 1, 0, depth));
            }
            catch (System.Exception exception)
            {
                queueFailed = true;
                Debug.LogWarning($"[UniText GlyphRaster] Async readback completion probe could not be queued: {exception.GetType().Name}.");
            }
        }

        if (requests.Count == 0)
        {
            yield return WaitUntil(timeoutAt);
            AbortGpuCompletionMeasurement();
            yield break;
        }
        if (!readbackFallbackLogged)
        {
            readbackFallbackLogged = true;
            Debug.Log("[UniText GlyphRaster] Using a 1x1-per-layer async readback probe for GPU completion timing.");
        }

        var completed = new bool[requests.Count];
        bool failed = queueFailed;
        bool timedOut = false;
        while (true)
        {
            bool done = true;
            for (int i = 0; i < requests.Count; i++)
            {
                if (completed[i]) continue;
                var request = requests[i];
                if (!request.done)
                {
                    done = false;
                    continue;
                }
                failed |= request.hasError;
                completed[i] = true;
            }
            double now = Time.realtimeSinceStartupAsDouble;
            if (!timedOut && now >= timeoutAt)
            {
                timedOut = true;
                Debug.LogWarning($"[UniText GlyphRaster] Async readback completion probe exceeded {CompletionTimeoutSeconds:F0} seconds; draining outstanding requests before aborting.");
            }
            if (done && (!queueFailed || now >= timeoutAt))
            {
                if (failed || timedOut)
                    AbortGpuCompletionMeasurement();
                else
                    lastE2eMs = (float)((now - dispatchStart) * 1000.0);
                yield break;
            }
            yield return null;
        }
    }

    static IEnumerator WaitUntil(double deadline)
    {
        while (Time.realtimeSinceStartupAsDouble < deadline)
            yield return null;
    }

    void AbortGpuCompletionMeasurement()
    {
        abortRun = true;
        completionMethod = "unavailable";
        lastE2eMs = float.NaN;
        Debug.LogWarning("[UniText GlyphRaster] GPU completion tracking is unavailable on this backend; E2E measurement aborted.");
    }

    protected override bool ShouldAbortRun() => abortRun;

    protected override GlyphExecutionSample CaptureExecutionDiagnostics()
    {
        var sample = new GlyphExecutionSample
        {
            trigger = "GameObject.SetActive(true)",
            threading = pendingSingleThreaded ? "forcedSingleThread" : "parallelAllowed",
            completion = completionMethod,
            fallback = "systemAndEmojiDisabled",
            gpuSubmissionBudgetMs = GlyphAtlasGpuRaster.submissionBudgetMs
        };
        GlyphAtlas.ForEachInstance(atlas => sample.atlases.Add(ToExecutionData(atlas.GetDiagnosticSnapshot())));
        sample.rasterBackend = AggregateBackend(sample.atlases);
        return sample;
    }

    static GlyphAtlasExecutionData ToExecutionData(GlyphAtlas.DiagnosticSnapshot snapshot)
    {
        var paths = new List<string>();
        if ((snapshot.WritePaths & GlyphAtlas.WritePath.ComputeDirect) != 0) paths.Add("computeDirect");
        if ((snapshot.WritePaths & GlyphAtlas.WritePath.GpuUpload) != 0) paths.Add("gpuUploadRegions");
        if ((snapshot.WritePaths & GlyphAtlas.WritePath.CopyTexture) != 0) paths.Add("stagingApplyCopyTexture");
        if ((snapshot.WritePaths & GlyphAtlas.WritePath.ReadableApply) != 0) paths.Add("readableAtlasApply");
        return new GlyphAtlasExecutionData
        {
            mode = snapshot.Label,
            backend = snapshot.Backend.ToString(),
            storage = snapshot.Storage.ToString(),
            cpuMirror = snapshot.HasCpuMirror,
            gpuUploadTarget = snapshot.HasGpuUploadTarget,
            preparation = snapshot.PreparationModes.ToString(),
            cpuRasterizationForced = snapshot.CpuRasterizationForced,
            writePaths = paths,
            computeDirectBatches = snapshot.ComputeDirectBatches,
            gpuUploadBatches = snapshot.GpuUploadBatches,
            copyTextureRegions = snapshot.CopyTextureRegions,
            readableApplyFlushes = snapshot.ReadableApplyFlushes,
            gpuUploadFallbacks = snapshot.GpuUploadFallbacks,
            lastGpuUploadError = snapshot.LastGpuUploadError?.ToString()
        };
    }

    static string AggregateBackend(List<GlyphAtlasExecutionData> atlases)
    {
        string backend = null;
        for (int i = 0; i < atlases.Count; i++)
        {
            if (backend == null) backend = atlases[i].backend;
            else if (backend != atlases[i].backend) return "Mixed";
        }
        return backend ?? "Unresolved";
    }

    protected override string Diagnostics(string label)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append($"[Atlas {label}] ");
        GlyphAtlas.ForEachInstance(atlas =>
        {
            var tex = atlas.AtlasTexture;
            if (tex == null)
            {
                sb.Append($"pages={atlas.PageCount} tex=NULL  ");
                return;
            }
            int depth = tex is RenderTexture rt ? rt.volumeDepth : tex is Texture2DArray ta ? ta.depth : 1;
            long storageMb = BenchmarkAtlasUtils.EstimatedStorageBytes(tex) / (1024 * 1024);
            int contentSlices = BenchmarkAtlasUtils.ContentSliceCount(tex, atlas.PageCount);
            string content = contentSlices >= 0 ? $" contentSlices={contentSlices}" : "";
            var runtime = atlas.GetDiagnosticSnapshot();
            sb.Append($"pages={atlas.PageCount} texSize={tex.width}x{tex.height}x{depth} texMB={storageMb}{content} pixelChecksum={BenchmarkAtlasUtils.Checksum(tex, atlas.PageCount)} backend={runtime.Backend} prep={runtime.PreparationModes} storage={runtime.Storage} cpuMirror={runtime.HasCpuMirror} writes={runtime.WritePaths} gpuUploadBatches={runtime.GpuUploadBatches} copyRegions={runtime.CopyTextureRegions} readableApply={runtime.ReadableApplyFlushes} uploadFallbacks={runtime.GpuUploadFallbacks} completion={completionMethod}  ");
        });
        return sb.ToString();
    }
}
