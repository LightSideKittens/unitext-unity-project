using System.Collections;
using System.Collections.Generic;
using LightSide;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// UniText glyph rasterization: clears all live glyph data, then times re-rasterizing the shared
/// glyph corpus (BenchmarkConfig) across the child text objects. Rasterization is async/GPU, so the
/// end-to-end time (CPU dispatch + GPU completion) is reported alongside the CPU frame time.
/// </summary>
public class UniText_GlyphRasterizationBenchmark : GlyphRasterBenchmarkBase
{
    const double PreparationTimeoutSeconds = 10.0;

    [SerializeField, Tooltip("Un-mutes the raster Cat zone so the per-pass ContourUnion phase/bail line reaches Logs/unitext.log. Needs the UNITEXT_DEBUG scripting define, else the counters read 0.")]
    bool dumpPhases;

    [SerializeField, Min(0), Tooltip("Diagnostic: when > 0, overrides GlyphAtlasGpuRaster submission budget (ms) for the run. Lower it (e.g. 15) to force fine submission splitting — with dumpPhases on, the log then reveals whether the boundary is available on this backend and how many chunks the heuristic flags oversized. 0 leaves the platform default.")]
    float gpuSubmissionBudgetMsOverride;

    UniText[] targets;
    bool pendingSingleThreaded, pendingMaxStroke;
    BenchmarkGlyphRasterPath pendingPath;
    bool abortRun;
    string completionMethod;
    float savedBudgetMs;
    bool wasParallel, wasForceST, wasMuted, wasSysDisabled, wasEmojiDisabled;
    GlyphAtlas.ExecutionPath wasExecutionPath;
    List<(UniText text, Style style)> strokeStyles;
    readonly List<GlyphAtlas> targetAtlases = new();

    protected override string EngineName => "UniText";
    protected override bool HasE2E => true;
    protected override string RequestedPath => BenchmarkGlyphRasterPaths.Token(pendingPath);

    /// <summary>Runs one UniText glyph pass through the requested fail-closed raster and atlas-write route.</summary>
    public IEnumerator RunBenchmarkCoroutine(bool singleThreaded, bool maxStroke = false,
        BenchmarkGlyphRasterPath path = BenchmarkGlyphRasterPath.Auto)
    {
        pendingSingleThreaded = singleThreaded;
        pendingMaxStroke = maxStroke;
        pendingPath = path;
        yield return RunPass((singleThreaded ? "SINGLE-THREADED" : "PARALLEL") + (maxStroke ? " + MAX-STROKE" : ""));
    }

    void Run(bool singleThreaded, bool maxStroke)
    {
        if (isRunning) return;
        var selector = ObjectUtils.FindAny<BenchmarkGlyphPathSelector>();
        if (selector != null && !string.IsNullOrEmpty(selector.LaunchOverrideError))
        {
            Debug.LogError($"[UniText GlyphRaster] {selector.LaunchOverrideError}; select a valid path before starting.");
            return;
        }
        var path = selector != null ? selector.SelectedPath : BenchmarkGlyphRasterPath.Auto;
        StartCoroutine(RunBenchmarkCoroutine(singleThreaded, maxStroke, path));
    }

    [ContextMenu("Run Benchmark (Single-Threaded)")] public void RunBenchmark() => Run(true, false);
    [ContextMenu("Run Benchmark (Parallel)")] public void RunBenchmarkParallel() => Run(false, false);
    [ContextMenu("Run Benchmark (Single-Threaded + Max Stroke)")] public void RunBenchmarkMaxStroke() => Run(true, true);
    [ContextMenu("Run Benchmark (Parallel + Max Stroke)")] public void RunBenchmarkParallelMaxStroke() => Run(false, true);

    protected override void OnBeforeRun()
    {
        abortRun = false;
        completionMethod = "synchronous";
        savedBudgetMs = GlyphAtlasGpuRaster.submissionBudgetMs;
        wasParallel = UniTextBase.UseParallel;
        wasForceST = GlyphAtlas.forceSingleThreaded;
        wasExecutionPath = GlyphAtlas.executionPathOverride;
        wasMuted = CatZones.MuteAll;
        wasSysDisabled = SystemFont.Disabled;
        wasEmojiDisabled = EmojiFont.Disabled;
        foreach (var target in GetComponentsInChildren<UniText>(true))
            if (target.gameObject != gameObject)
                target.gameObject.SetActive(false);
        UniTextFont.Core.DisposeAllLive();
        GlyphAtlas.DisposeTextInstances();
        GlyphAtlas.executionPathOverride = BenchmarkGlyphRasterPaths.ToExecutionPath(pendingPath);
        if (gpuSubmissionBudgetMsOverride > 0f)
            GlyphAtlasGpuRaster.submissionBudgetMs = gpuSubmissionBudgetMsOverride;
        UniTextBase.UseParallel = !pendingSingleThreaded;
        GlyphAtlas.forceSingleThreaded = pendingSingleThreaded;
        CatZones.MuteAll = !dumpPhases;
        SystemFont.Disabled = true;
        EmojiFont.Disabled = true;
    }

    protected override void OnAfterRun()
    {
        GlyphAtlasGpuRaster.submissionBudgetMs = savedBudgetMs;
        UniTextBase.UseParallel = wasParallel;
        GlyphAtlas.forceSingleThreaded = wasForceST;
        GlyphAtlas.executionPathOverride = wasExecutionPath;
        CatZones.MuteAll = wasMuted;
        SystemFont.Disabled = wasSysDisabled;
        EmojiFont.Disabled = wasEmojiDisabled;

        if (strokeStyles != null)
        {
            foreach (var (text, style) in strokeStyles)
                text.RemoveStyle(style);
            strokeStyles = null;
        }

        UniTextFont.Core.DisposeAllLive();
        GlyphAtlas.DisposeTextInstances();
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
            SetRunStatus("skipped", "No UniText children were found");
            Debug.LogError("[UniText GlyphRaster] No UniText children found.");
            return false;
        }

        targetAtlases.Clear();
        var targetModes = new List<UniTextRenderMode>();
        foreach (var target in targets)
        {
            var mode = target.RenderMode;
            if (targetModes.Contains(mode)) continue;
            targetModes.Add(mode);
            targetAtlases.Add(GlyphAtlas.GetInstance(mode));
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

    protected override IEnumerator PrepareRun()
    {
        double timeoutAt = Time.realtimeSinceStartupAsDouble + PreparationTimeoutSeconds;
        while (true)
        {
            bool ready = true;
            string pendingReason = null;
            foreach (var atlas in targetAtlases)
            {
                GlyphAtlas.ExecutionPathPreparation preparation = default;
                string reason = null;
                System.Exception failure = null;
                try
                {
                    preparation = atlas.PrepareExecutionPath(out reason);
                }
                catch (System.Exception exception)
                {
                    failure = exception;
                }
                if (failure != null)
                {
                    SetRunStatus("failed", $"{failure.GetType().Name}: {failure.Message}");
                    Debug.LogException(failure);
                    yield break;
                }
                if (preparation == GlyphAtlas.ExecutionPathPreparation.Unsupported)
                {
                    SetRunStatus("unsupported", reason);
                    Debug.LogWarning($"[UniText GlyphRaster] {RequestedPath} is unsupported: {reason}.");
                    yield break;
                }
                if (preparation != GlyphAtlas.ExecutionPathPreparation.Preparing) continue;
                ready = false;
                pendingReason = reason;
            }

            if (ready) yield break;
            if (Time.realtimeSinceStartupAsDouble >= timeoutAt)
            {
                SetRunStatus("failed", $"{RequestedPath} preparation timed out after {PreparationTimeoutSeconds:F0} seconds"
                                       + (string.IsNullOrEmpty(pendingReason) ? "" : $": {pendingReason}"));
                Debug.LogWarning($"[UniText GlyphRaster] {RequestedPath} preparation exceeded {PreparationTimeoutSeconds:F0} seconds.");
                yield break;
            }
            yield return null;
        }
    }

    protected override void Deactivate()
    {
        if (targets == null) return;
        foreach (var ut in targets)
            ut.gameObject.SetActive(false);
    }

    protected override void ClearCaches() => UniTextFont.Core.DisposeAllLive();

    protected override void ResetExecutionDiagnostics()
    {
        foreach (var atlas in targetAtlases)
            atlas.ResetDiagnosticCounters();
    }

    protected override void Rasterize()
    {
        try
        {
            foreach (var ut in targets)
                ut.gameObject.SetActive(true);
            Canvas.ForceUpdateCanvases();
        }
        catch (System.Exception exception)
        {
            abortRun = true;
            SetRunStatus("failed", $"{exception.GetType().Name}: {exception.Message}");
            Debug.LogException(exception);
        }
    }

    protected override int CountGlyphs()
    {
        int count = 0;
        foreach (var atlas in targetAtlases)
            count += atlas.EntryCount;
        return count;
    }

    protected override IEnumerator AwaitAsyncCompletion(float cpuMs)
    {
        double dispatchStart = Time.realtimeSinceStartupAsDouble - cpuMs / 1000.0;
        completionMethod = "asyncGpuReadback";
        var textures = new List<Texture>(2);
        foreach (var atlas in targetAtlases)
            if (atlas.AtlasTexture != null)
                textures.Add(atlas.AtlasTexture);
        yield return AwaitGpuTextureCompletion(dispatchStart, textures,
            AbortGpuCompletionMeasurement);
    }

    void AbortGpuCompletionMeasurement(string reason)
    {
        abortRun = true;
        completionMethod = "unavailable";
        lastE2eMs = float.NaN;
        SetRunStatus("failed", reason);
        Debug.LogWarning($"[UniText GlyphRaster] {reason}; E2E measurement aborted.");
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
        foreach (var atlas in targetAtlases)
        {
            var snapshot = atlas.GetDiagnosticSnapshot();
            bool valid = ValidateRequestedPath(snapshot, out var reason);
            if (valid)
                valid = ValidateMaterialBinding(atlas, snapshot, out reason);
            if (!valid && runStatus == "measured")
            {
                abortRun = true;
                SetRunStatus("mismatch", reason);
                Debug.LogError($"[UniText GlyphRaster] {reason}");
            }
            sample.atlases.Add(ToExecutionData(snapshot));
        }
        sample.rasterBackend = AggregateBackend(sample.atlases);
        return sample;
    }

    static bool ValidateMaterialBinding(GlyphAtlas atlas,
        GlyphAtlas.DiagnosticSnapshot snapshot, out string reason)
    {
        var texture = atlas.AtlasTexture;
        var material = snapshot.Label == nameof(UniTextRenderMode.MSDF)
            ? UniTextMaterialCache.Msdf
            : UniTextMaterialCache.Sdf;
        if (texture != null && material != null
                            && object.ReferenceEquals(material.mainTexture, texture))
        {
            reason = null;
            return true;
        }

        reason = $"Atlas material binding mismatch for {snapshot.Label}: "
                 + $"atlas={(texture != null ? ObjectUtils.GetInstanceIdCompat(texture) : 0)}, "
                 + $"materialTexture={(material != null && material.mainTexture != null ? ObjectUtils.GetInstanceIdCompat(material.mainTexture) : 0)}";
        return false;
    }

    bool ValidateRequestedPath(GlyphAtlas.DiagnosticSnapshot snapshot, out string reason)
    {
        var expectedPath = BenchmarkGlyphRasterPaths.ToExecutionPath(pendingPath);
        if (snapshot.RequestedPath != expectedPath)
        {
            reason = $"Requested {expectedPath}, atlas resolved {snapshot.RequestedPath}";
            return false;
        }
        if (pendingPath == BenchmarkGlyphRasterPath.Auto)
        {
            reason = null;
            return true;
        }

        GlyphAtlas.RasterBackend backend;
        GlyphAtlas.StorageKind storage;
        GlyphAtlas.WritePath writePath;
        bool cpuMirror;
        bool uploadTarget;
        bool cpuForced;
        switch (pendingPath)
        {
            case BenchmarkGlyphRasterPath.GpuComputeDirect:
                backend = GlyphAtlas.RasterBackend.GpuCompute;
                storage = GlyphAtlas.StorageKind.RenderTextureArray;
                writePath = GlyphAtlas.WritePath.ComputeDirect;
                cpuMirror = false;
                uploadTarget = false;
                cpuForced = false;
                break;
            case BenchmarkGlyphRasterPath.CpuGpuUpload:
                backend = GlyphAtlas.RasterBackend.Cpu;
                storage = GlyphAtlas.StorageKind.Texture2DArray;
                writePath = GlyphAtlas.WritePath.GpuUpload;
                cpuMirror = false;
                uploadTarget = true;
                cpuForced = true;
                break;
            case BenchmarkGlyphRasterPath.CpuCopyTexture:
                backend = GlyphAtlas.RasterBackend.Cpu;
                storage = GlyphAtlas.StorageKind.Texture2DArray;
                writePath = GlyphAtlas.WritePath.CopyTexture;
                cpuMirror = false;
                uploadTarget = false;
                cpuForced = true;
                break;
            case BenchmarkGlyphRasterPath.CpuReadableApply:
                backend = GlyphAtlas.RasterBackend.Cpu;
                storage = GlyphAtlas.StorageKind.Texture2DArray;
                writePath = GlyphAtlas.WritePath.ReadableApply;
                cpuMirror = true;
                uploadTarget = false;
                cpuForced = true;
                break;
            default:
                reason = $"Unknown requested path {pendingPath}";
                return false;
        }

        if (snapshot.Backend == backend && snapshot.Storage == storage
            && snapshot.WritePaths == writePath && snapshot.HasCpuMirror == cpuMirror
            && snapshot.HasGpuUploadTarget == uploadTarget
            && snapshot.CpuRasterizationForced == cpuForced
            && snapshot.GpuUploadFallbacks == 0)
        {
            reason = null;
            return true;
        }

        reason = $"Requested {expectedPath}, actual backend={snapshot.Backend}, storage={snapshot.Storage}, "
                 + $"writes={snapshot.WritePaths}, cpuMirror={snapshot.HasCpuMirror}, "
                 + $"gpuUploadTarget={snapshot.HasGpuUploadTarget}, cpuForced={snapshot.CpuRasterizationForced}, "
                 + $"uploadFallbacks={snapshot.GpuUploadFallbacks}, "
                 + $"sourceOverflows={snapshot.GpuUploadSourceOverflows}, "
                 + $"sourceOverflowBytes={snapshot.GpuUploadSourceOverflowCurrentBytes}/"
                 + $"{snapshot.GpuUploadSourceOverflowBudgetBytes}, "
                 + $"sourceOverflowPeakBytes={snapshot.GpuUploadSourceOverflowPeakBytes}, "
                 + $"sourceOverflowRetainedBytes={snapshot.GpuUploadSourceOverflowRetainedBytes}, "
                 + $"backpressureWaits={snapshot.GpuUploadBackpressureWaits}, "
                 + $"backpressureWaitMs={snapshot.GpuUploadBackpressureWaitMilliseconds:F3}, "
                 + $"backpressureMaxWaitMs={snapshot.GpuUploadBackpressureMaxWaitMilliseconds:F3}";
        return false;
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
            requestedPath = BenchmarkGlyphRasterPaths.Token(snapshot.RequestedPath),
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
            gpuUploadSourceOverflows = snapshot.GpuUploadSourceOverflows,
            gpuUploadSourceOverflowBudgetBytes = snapshot.GpuUploadSourceOverflowBudgetBytes,
            gpuUploadSourceOverflowCurrentBytes = snapshot.GpuUploadSourceOverflowCurrentBytes,
            gpuUploadSourceOverflowPeakBytes = snapshot.GpuUploadSourceOverflowPeakBytes,
            gpuUploadSourceOverflowRetainedBytes = snapshot.GpuUploadSourceOverflowRetainedBytes,
            gpuUploadBackpressureWaits = snapshot.GpuUploadBackpressureWaits,
            gpuUploadBackpressureWaitMilliseconds = snapshot.GpuUploadBackpressureWaitMilliseconds,
            gpuUploadBackpressureMaxWaitMilliseconds = snapshot.GpuUploadBackpressureMaxWaitMilliseconds,
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
        foreach (var atlas in targetAtlases)
        {
            var tex = atlas.AtlasTexture;
            if (tex == null)
            {
                sb.Append($"pages={atlas.PageCount} tex=NULL  ");
                continue;
            }
            int depth = tex is RenderTexture rt ? rt.volumeDepth : tex is Texture2DArray ta ? ta.depth : 1;
            long storageMb = BenchmarkAtlasUtils.EstimatedStorageBytes(tex) / (1024 * 1024);
            int contentSlices = BenchmarkAtlasUtils.ContentSliceCount(tex, atlas.PageCount);
            string content = contentSlices >= 0 ? $" contentSlices={contentSlices}" : "";
            var runtime = atlas.GetDiagnosticSnapshot();
            sb.Append($"pages={atlas.PageCount} texSize={tex.width}x{tex.height}x{depth} texMB={storageMb}{content} pixelChecksum={BenchmarkAtlasUtils.Checksum(tex, atlas.PageCount)} requestedPath={runtime.RequestedPath} backend={runtime.Backend} prep={runtime.PreparationModes} storage={runtime.Storage} cpuMirror={runtime.HasCpuMirror} writes={runtime.WritePaths} gpuUploadBatches={runtime.GpuUploadBatches} copyRegions={runtime.CopyTextureRegions} readableApply={runtime.ReadableApplyFlushes} uploadFallbacks={runtime.GpuUploadFallbacks} sourceOverflows={runtime.GpuUploadSourceOverflows} sourceOverflowBytes={runtime.GpuUploadSourceOverflowCurrentBytes}/{runtime.GpuUploadSourceOverflowBudgetBytes} sourceOverflowPeakBytes={runtime.GpuUploadSourceOverflowPeakBytes} sourceOverflowRetainedBytes={runtime.GpuUploadSourceOverflowRetainedBytes} backpressureWaits={runtime.GpuUploadBackpressureWaits} backpressureWaitMs={runtime.GpuUploadBackpressureWaitMilliseconds:F3} backpressureMaxWaitMs={runtime.GpuUploadBackpressureMaxWaitMilliseconds:F3} completion={completionMethod}  ");
        }
        return sb.ToString();
    }
}
