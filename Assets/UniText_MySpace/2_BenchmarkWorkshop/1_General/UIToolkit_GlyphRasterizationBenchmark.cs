using System.Collections;
using LightSide;
using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;
using Debug = UnityEngine.Debug;

/// <summary>
/// UI Toolkit glyph rasterization. Clears a dynamic TextCore <see cref="FontAsset"/>, enables a
/// live <see cref="Label"/>, and measures until that panel render has populated the atlas.
/// </summary>
public class UIToolkit_GlyphRasterizationBenchmark : GlyphRasterBenchmarkBase
{
    const double RenderTimeoutSeconds = 5.0;

    [Tooltip("Dynamic TextCore FontAsset under test. Assigned per font by BenchmarkFontSelector.")]
    public FontAsset fontAsset;

    [Tooltip("Live PanelRenderer whose Label activation triggers the measured UI Toolkit rasterization.")]
    public PanelRenderer panelRenderer;

    string glyphText;
    VisualElement root;
    VisualElement previewContainer;
    Label previewLabel;
    bool reloadHooked;
    bool abortRun;
    int panelVersion;
    int rasterPanelVersion;
    int renderSequence;
    int rasterRenderSequence;
    double renderCompletedAt;
    readonly WaitForEndOfFrame waitForEndOfFrame = new();

    protected override string EngineName => "UIToolkit";
    protected override bool HasE2E => true;

    public IEnumerator RunBenchmarkCoroutine() => RunPass(null);

    void Start() => EnsurePreviewPanel();

    void OnDestroy()
    {
        if (reloadHooked && panelRenderer != null)
            panelRenderer.UnregisterUIReloadCallback(OnUIReload);
        if (previewLabel != null)
            previewLabel.generateVisualContent -= OnGenerateVisualContent;
    }

    [ContextMenu("Run Benchmark")]
    public void RunBenchmark()
    {
        if (!isRunning) StartCoroutine(RunPass(null));
    }

    protected override bool CollectTargets()
    {
        if (fontAsset == null)
        {
            Debug.LogError("[UIToolkit GlyphRaster] No FontAsset - assign one on the BenchmarkFontSelector's font list (uiToolkitFont).");
            return false;
        }
        if (fontAsset.atlasPopulationMode != AtlasPopulationMode.Dynamic)
        {
            Debug.LogWarning($"[UIToolkit GlyphRaster] '{fontAsset.name}' is not a Dynamic FontAsset - it cannot re-rasterize after a clear; skipping.");
            return false;
        }

        glyphText = BenchmarkConfig.Instance != null ? BenchmarkConfig.Instance.GlyphRasterText : null;
        if (string.IsNullOrEmpty(glyphText))
        {
            Debug.LogError("[UIToolkit GlyphRaster] No glyph corpus - set the Glyph Rasterization text on the BenchmarkConfig in the scene.");
            return false;
        }

        EnsurePreviewPanel();
        if (panelRenderer == null || panelRenderer.panelSettings == null || root == null)
        {
            Debug.LogError("[UIToolkit GlyphRaster] No live PanelRenderer root is available.");
            return false;
        }
        if (!UIToolkitFontIsolation.Validate(panelRenderer.panelSettings, fontAsset, out var fallbackError))
        {
            Debug.LogError($"[UIToolkit GlyphRaster] Font isolation failed: {fallbackError}");
            return false;
        }

        EnsurePreviewElements();
        abortRun = false;
        Debug.Log("[UIToolkit GlyphRaster] Trigger=Label display enable; fallback local/global/default/emoji/Dynamic OS=off; completion=panel render.");
        return true;
    }

    void EnsurePreviewPanel()
    {
        var visualBenchmark = ObjectUtils.FindAny<UIToolkitBenchmark>();
        if (root == null)
            root = visualBenchmark?.RootElement;
        if (panelRenderer == null)
            panelRenderer = visualBenchmark?.panelRenderer ?? ObjectUtils.FindAny<PanelRenderer>();

        if (panelRenderer == null || reloadHooked) return;
        panelRenderer.RegisterUIReloadCallback(OnUIReload);
        reloadHooked = true;
    }

    void OnUIReload(PanelRenderer renderer, VisualElement rootElement)
    {
        panelVersion++;
        root = rootElement;
        if (previewContainer != null && previewContainer.parent == null)
            root.Add(previewContainer);
    }

    void EnsurePreviewElements()
    {
        previewContainer ??= new VisualElement { name = "UIToolkitGlyphRasterPreview" };
        previewContainer.style.position = Position.Absolute;
        previewContainer.style.left = 10;
        previewContainer.style.top = 10;
        previewContainer.style.width = 1068.9f;
        previewContainer.style.height = Length.Percent(100);
        previewContainer.style.display = DisplayStyle.None;
        previewContainer.pickingMode = PickingMode.Ignore;

        if (previewLabel == null)
        {
            previewLabel = new Label { name = "UIToolkitGlyphRasterPreviewLabel", pickingMode = PickingMode.Ignore };
            previewLabel.generateVisualContent += OnGenerateVisualContent;
        }
        previewLabel.text = glyphText;
        previewLabel.enableRichText = false;
        previewLabel.emojiFallbackSupport = false;
        previewLabel.style.fontSize = 28;
        previewLabel.style.color = Color.white;
        previewLabel.style.whiteSpace = WhiteSpace.Normal;
        previewLabel.style.unityFontDefinition = new StyleFontDefinition(fontAsset);
#if UNITY_6000_0_OR_NEWER
        previewLabel.style.unityTextGenerator = TextGeneratorType.Advanced;
#endif

        if (previewLabel.parent == null)
            previewContainer.Add(previewLabel);
        if (previewContainer.parent == null)
            root.Add(previewContainer);
    }

    void OnGenerateVisualContent(MeshGenerationContext _)
    {
        renderCompletedAt = Time.realtimeSinceStartupAsDouble;
        renderSequence++;
    }

    /// <summary>Empties the dynamic atlas the run rasterized so its grown texture stops occupying memory between runs; in the editor also persists the shrunk font (the atlas is a sub-asset that otherwise survives play mode and bloats the saved .asset).</summary>
    protected override void OnAfterRun()
    {
        if (fontAsset == null) return;
        fontAsset.ClearFontAssetData(true);
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(fontAsset);
        UnityEditor.AssetDatabase.SaveAssetIfDirty(fontAsset);
#endif
    }

    protected override void ClearCaches() => fontAsset.ClearFontAssetData(true);

    protected override void Rasterize()
    {
        rasterPanelVersion = panelVersion;
        rasterRenderSequence = renderSequence;
        previewContainer.style.display = DisplayStyle.Flex;
        previewLabel.MarkDirtyRepaint();
    }

    protected override IEnumerator AwaitAsyncCompletion(float cpuMs)
    {
        double dispatchStart = Time.realtimeSinceStartupAsDouble - cpuMs / 1000.0;
        double timeoutAt = Time.realtimeSinceStartupAsDouble + RenderTimeoutSeconds;
        while (renderSequence == rasterRenderSequence && Time.realtimeSinceStartupAsDouble < timeoutAt)
        {
            if (panelVersion != rasterPanelVersion)
            {
                AbortPanelMeasurement("the PanelRenderer root reloaded during the measured pass");
                yield break;
            }
            yield return waitForEndOfFrame;
        }

        if (panelVersion != rasterPanelVersion)
        {
            AbortPanelMeasurement("the PanelRenderer root reloaded during the measured pass");
            yield break;
        }

        if (renderSequence == rasterRenderSequence)
        {
            AbortPanelMeasurement($"no Label render occurred within {RenderTimeoutSeconds:F0} seconds");
            yield break;
        }

        lastE2eMs = (float)((renderCompletedAt - dispatchStart) * 1000.0);
    }

    void AbortPanelMeasurement(string reason)
    {
        abortRun = true;
        lastE2eMs = float.NaN;
        Debug.LogWarning($"[UIToolkit GlyphRaster] Aborted: {reason}.");
    }

    protected override void ShowPreview()
    {
        if (previewContainer != null)
            previewContainer.style.display = DisplayStyle.Flex;
    }

    protected override void Deactivate()
    {
        if (previewContainer != null)
            previewContainer.style.display = DisplayStyle.None;
    }

    protected override int CountGlyphs() => FontAssetUtils.GlyphCount(fontAsset);

    protected override bool ShouldAbortRun() => abortRun;

    protected override GlyphExecutionSample CaptureExecutionDiagnostics()
    {
        var sample = new GlyphExecutionSample
        {
            trigger = "VisualElement.display=Flex",
            rasterBackend = "CpuTextCore",
            threading = "mainThread",
            completion = "panelRender",
            fallback = "localGlobalDefaultEmojiAndDynamicOsDisabled"
        };
        var textures = fontAsset.atlasTextures;
        if (textures != null)
            for (int i = 0; i < textures.Length; i++)
            {
                var texture = textures[i];
                if (texture == null) continue;
                sample.atlases.Add(new GlyphAtlasExecutionData
                {
                    mode = $"atlas{i}",
                    backend = "CpuTextCore",
                    storage = texture.GetType().Name,
                    cpuMirror = texture.isReadable,
                    gpuUploadTarget = false,
                    preparation = "mainThread",
                    writePaths = new System.Collections.Generic.List<string> { "textCoreInternal" }
                });
            }
        return sample;
    }

    protected override string Diagnostics(string label)
    {
        int glyphs = FontAssetUtils.GlyphCount(fontAsset);
        if (label == "AFTER raster" && glyphs == 0)
        {
            abortRun = true;
            Debug.LogWarning("[UIToolkit GlyphRaster] The Label rendered without populating the selected FontAsset; the pass is invalid.");
        }
        return $"[Atlas {label}] '{fontAsset.name}': glyphs={glyphs} chars={FontAssetUtils.CharacterCount(fontAsset)} atlasCount={fontAsset.atlasTextureCount} trigger=Label.display raster=CPU-TextCore completion=panel-render cpuMirror={fontAsset.atlasTexture != null && fontAsset.atlasTexture.isReadable} fallback=local/global/default/emoji/DynamicOS-off";
    }
}
