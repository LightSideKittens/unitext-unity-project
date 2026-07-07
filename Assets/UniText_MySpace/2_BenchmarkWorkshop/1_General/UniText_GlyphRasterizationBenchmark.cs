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
    [SerializeField] bool forceCPURasterization;

    UniText[] targets;
    bool pendingSingleThreaded, pendingMaxStroke;
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
        GlyphAtlas.forceCpuRasterization = forceCPURasterization;
        wasParallel = UniTextBase.UseParallel;
        wasForceST = GlyphAtlas.forceSingleThreaded;
        wasMuted = CatZones.MuteAll;
        wasSysDisabled = SystemFont.Disabled;
        wasEmojiDisabled = EmojiFont.Disabled;
        UniTextBase.UseParallel = !pendingSingleThreaded;
        GlyphAtlas.forceSingleThreaded = pendingSingleThreaded;
        CatZones.MuteAll = true;
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
        UniTextBase.UseParallel = wasParallel;
        GlyphAtlas.forceSingleThreaded = wasForceST;
        CatZones.MuteAll = wasMuted;
        SystemFont.Disabled = wasSysDisabled;
        EmojiFont.Disabled = wasEmojiDisabled;
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
#if UNITEXT_DEBUG
        double dispatchStart = Time.realtimeSinceStartupAsDouble - cpuMs / 1000.0;
        var sdf = GlyphAtlas.GetInstance(UniTextRenderMode.SDF);
        var msdf = GlyphAtlas.GetInstance(UniTextRenderMode.MSDF);
        for (int guard = 0; guard < 600 && !(sdf.GpuRasterComplete && msdf.GpuRasterComplete); guard++)
            yield return null;
        lastE2eMs = (float)((Time.realtimeSinceStartupAsDouble - dispatchStart) * 1000.0);
#else
        lastE2eMs = cpuMs;
        yield break;
#endif
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
            sb.Append($"pages={atlas.PageCount} texSize={tex.width}x{tex.height}x{depth} pixelChecksum={BenchmarkAtlasUtils.Checksum(tex)}  ");
        });
        return sb.ToString();
    }
}
