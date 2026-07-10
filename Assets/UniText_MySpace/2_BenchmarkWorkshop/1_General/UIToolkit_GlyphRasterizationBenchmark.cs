using System.Collections;
using LightSide;
using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;
using Debug = UnityEngine.Debug;

/// <summary>
/// UI Toolkit glyph rasterization. Clears a dynamic TextCore <see cref="FontAsset"/> and times
/// re-rasterizing the shared corpus through <c>FontAsset.TryAddCharacters</c>.
/// </summary>
public class UIToolkit_GlyphRasterizationBenchmark : GlyphRasterBenchmarkBase
{
    [Tooltip("Dynamic TextCore FontAsset under test. Assigned per font by BenchmarkFontSelector.")]
    public FontAsset fontAsset;

    [Tooltip("Optional PanelRenderer used only to display the glyph corpus after timing.")]
    public PanelRenderer panelRenderer;

    string glyphText;
    VisualElement root;
    VisualElement previewContainer;
    Label previewLabel;
    bool reloadHooked;

    protected override string EngineName => "UIToolkit";

    public IEnumerator RunBenchmarkCoroutine() => RunPass(null);

    void Start() => EnsurePreviewPanel();

    void OnDestroy()
    {
        if (reloadHooked && panelRenderer != null)
            panelRenderer.UnregisterUIReloadCallback(OnUIReload);
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
        root = rootElement;
        if (previewContainer != null && previewContainer.parent == null)
            root.Add(previewContainer);
    }

    protected override void ClearCaches() => fontAsset.ClearFontAssetData(true);

    protected override void Rasterize() => fontAsset.TryAddCharacters(glyphText, false);

    protected override void ShowPreview()
    {
        if (root == null) return;

        previewContainer ??= new VisualElement { name = "UIToolkitGlyphRasterPreview" };
        previewContainer.style.position = Position.Absolute;
        previewContainer.style.left = 10;
        previewContainer.style.top = 10;
        previewContainer.style.width = 1068.9f;
        previewContainer.style.height = Length.Percent(100);
        previewContainer.style.display = DisplayStyle.Flex;
        previewContainer.pickingMode = PickingMode.Ignore;

        previewLabel ??= new Label { name = "UIToolkitGlyphRasterPreviewLabel", pickingMode = PickingMode.Ignore };
        previewLabel.text = glyphText;
        previewLabel.enableRichText = false;
        previewLabel.style.fontSize = 28;
        previewLabel.style.color = Color.white;
        previewLabel.style.whiteSpace = WhiteSpace.Normal;
        previewLabel.style.unityFontDefinition = new StyleFontDefinition(fontAsset);

        if (previewLabel.parent == null)
            previewContainer.Add(previewLabel);
        if (previewContainer.parent == null)
            root.Add(previewContainer);
    }

    protected override void Deactivate()
    {
        if (previewContainer != null)
            previewContainer.style.display = DisplayStyle.None;
    }

    protected override int CountGlyphs() => fontAsset.glyphTable.Count;

    protected override string Diagnostics(string label) =>
        $"[Atlas {label}] '{fontAsset.name}': glyphs={fontAsset.glyphTable.Count} chars={fontAsset.characterTable.Count} atlasCount={fontAsset.atlasTextureCount}";
}
