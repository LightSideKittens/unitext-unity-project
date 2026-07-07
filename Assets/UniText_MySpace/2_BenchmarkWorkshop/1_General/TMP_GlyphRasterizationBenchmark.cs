using System.Collections;
using System.Collections.Generic;
using System.Text;
using LightSide;
using TMPro;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>TMP glyph rasterization: clears the font atlas, then times re-rasterizing the shared glyph corpus across the child TMP objects. Fully synchronous (CPU).</summary>
public class TMP_GlyphRasterizationBenchmark : GlyphRasterBenchmarkBase
{
    GameObject[] targets;
    TMP_FontAsset[] fontAssets;

    List<TMP_FontAsset> savedGlobalFallback;
    TMP_FontAsset savedDefaultFont;
    List<TMP_FontAsset>[] savedPerAssetFallback;

    protected override string EngineName => "TMP";

    public IEnumerator RunBenchmarkCoroutine() => RunPass(null);

    /// <summary>Strips every fallback path (per-asset table + the two global TMP_Settings lists) so TMP rasterizes only the primary font's glyphs — matching UI Toolkit's TryAddCharacters, which never falls back.</summary>
    protected override void OnBeforeRun()
    {
        savedGlobalFallback = TMP_Settings.fallbackFontAssets;
        savedDefaultFont = TMP_Settings.defaultFontAsset;
        savedPerAssetFallback = null;
        TMP_Settings.fallbackFontAssets = new List<TMP_FontAsset>();
        TMP_Settings.defaultFontAsset = null;
    }

    protected override void OnAfterRun()
    {
        TMP_Settings.fallbackFontAssets = savedGlobalFallback;
        TMP_Settings.defaultFontAsset = savedDefaultFont;
        if (savedPerAssetFallback != null && fontAssets != null)
            for (int i = 0; i < fontAssets.Length && i < savedPerAssetFallback.Length; i++)
                fontAssets[i].fallbackFontAssetTable = savedPerAssetFallback[i];
    }

    void Update()
    {
        if (InputUtils.GetKeyDown(KeyCode.Space) && !isRunning)
            RunBenchmark();
    }

    [ContextMenu("Run Benchmark")]
    public void RunBenchmark()
    {
        if (!isRunning) StartCoroutine(RunPass(null));
    }

    protected override bool CollectTargets()
    {
        var list = new List<GameObject>();
        foreach (var tmp in GetComponentsInChildren<TMP_Text>(true))
            if (tmp.gameObject != gameObject)
                list.Add(tmp.gameObject);
        targets = list.ToArray();

        if (targets.Length == 0)
        {
            Debug.LogError("[TMP GlyphRaster] No TMP_Text children found.");
            return false;
        }

        var glyphText = BenchmarkConfig.Instance != null ? BenchmarkConfig.Instance.GlyphRasterText : null;
        if (!string.IsNullOrEmpty(glyphText))
            foreach (var go in targets)
                go.GetComponent<TMP_Text>().text = glyphText;

        var font = targets[0].GetComponent<TMP_Text>().font;
        fontAssets = font != null ? new[] { font } : System.Array.Empty<TMP_FontAsset>();

        savedPerAssetFallback = new List<TMP_FontAsset>[fontAssets.Length];
        for (int i = 0; i < fontAssets.Length; i++)
        {
            savedPerAssetFallback[i] = fontAssets[i].fallbackFontAssetTable;
            fontAssets[i].fallbackFontAssetTable = new List<TMP_FontAsset>();
        }
        return true;
    }

    protected override void Deactivate()
    {
        foreach (var go in targets)
            go.SetActive(false);
    }

    protected override void ClearCaches()
    {
        foreach (var font in fontAssets)
            font.ClearFontAssetData(false);
    }

    protected override void Rasterize()
    {
        foreach (var go in targets)
            go.SetActive(true);
        Canvas.ForceUpdateCanvases();
    }

    protected override int CountGlyphs()
    {
        int total = 0;
        foreach (var font in fontAssets)
            total += font.glyphTable.Count;
        return total;
    }

    protected override string Diagnostics(string label)
    {
        var sb = new StringBuilder();
        sb.Append($"[Atlas {label}] ");
        foreach (var font in fontAssets)
        {
            var textures = font.atlasTextures;
            long checksum = textures is { Length: > 0 } ? BenchmarkAtlasUtils.Checksum(textures[0]) : 0;
            sb.Append($"'{font.name}': glyphs={font.glyphTable.Count} chars={font.characterTable.Count} atlasCount={textures?.Length ?? 0} pixelChecksum={checksum}  ");
        }
        return sb.ToString();
    }
}
