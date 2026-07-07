using System.Collections;
using UnityEngine;
using UnityEngine.TextCore.Text;
using Debug = UnityEngine.Debug;

/// <summary>
/// UI Toolkit glyph rasterization. Clears a dynamic TextCore <see cref="FontAsset"/> (assigned by
/// <c>BenchmarkFontSelector</c>) and times re-rasterizing the shared corpus through the same
/// synchronous FreeType path TMP uses (<c>FontAsset.TryAddCharacters</c>). Apples-to-apples with TMP;
/// UniText's number is a different architecture (async/GPU) — a reference, not a like-for-like figure.
/// </summary>
public class UIToolkit_GlyphRasterizationBenchmark : GlyphRasterBenchmarkBase
{
    [Tooltip("Dynamic TextCore FontAsset under test. Assigned per font by BenchmarkFontSelector.")]
    public FontAsset fontAsset;

    string glyphText;

    protected override string EngineName => "UIToolkit";

    public IEnumerator RunBenchmarkCoroutine() => RunPass(null);

    [ContextMenu("Run Benchmark")]
    public void RunBenchmark()
    {
        if (!isRunning) StartCoroutine(RunPass(null));
    }

    protected override bool CollectTargets()
    {
        if (fontAsset == null)
        {
            Debug.LogError("[UIToolkit GlyphRaster] No FontAsset — assign one on the BenchmarkFontSelector's font list (uiToolkitFont).");
            return false;
        }
        if (fontAsset.atlasPopulationMode != AtlasPopulationMode.Dynamic)
        {
            Debug.LogWarning($"[UIToolkit GlyphRaster] '{fontAsset.name}' is not a Dynamic FontAsset — it cannot re-rasterize after a clear; skipping.");
            return false;
        }

        glyphText = BenchmarkConfig.Instance != null ? BenchmarkConfig.Instance.GlyphRasterText : null;
        if (string.IsNullOrEmpty(glyphText))
        {
            Debug.LogError("[UIToolkit GlyphRaster] No glyph corpus — set the Glyph Rasterization text on the BenchmarkConfig in the scene.");
            return false;
        }
        return true;
    }

    protected override void ClearCaches() => fontAsset.ClearFontAssetData(true);

    protected override void Rasterize() => fontAsset.TryAddCharacters(glyphText, false);

    protected override int CountGlyphs() => fontAsset.glyphTable.Count;

    protected override string Diagnostics(string label) =>
        $"[Atlas {label}] '{fontAsset.name}': glyphs={fontAsset.glyphTable.Count} chars={fontAsset.characterTable.Count} atlasCount={fontAsset.atlasTextureCount}";
}
