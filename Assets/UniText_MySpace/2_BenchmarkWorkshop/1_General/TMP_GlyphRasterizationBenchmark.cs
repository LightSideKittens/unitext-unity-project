using System.Collections;
using System.Collections.Generic;
using System.Text;
using LightSide;
using TMPro;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>TMP glyph rasterization: clears the font atlas, times component-driven CPU rasterization, and observes the same GPU atlas-completion boundary as the other engines.</summary>
public class TMP_GlyphRasterizationBenchmark : GlyphRasterBenchmarkBase
{
    GameObject[] targets;
    TMP_Text[] textTargets;
    TMP_FontAsset[] fontAssets;

    List<TMP_FontAsset> savedGlobalFallback;
    TMP_FontAsset savedDefaultFont;
    List<TMP_Asset> savedEmojiFallback;
    TMP_SpriteAsset savedDefaultSprite;
    bool savedEmojiEnabled;
    List<TMP_FontAsset>[] savedPerAssetFallback;
    bool[] savedEmojiSupport;
    TMP_SpriteAsset[] savedSpriteAssets;
    bool abortRun;
    string completionMethod;

    protected override string EngineName => "TMP";
    protected override bool HasE2E => true;

    public IEnumerator RunBenchmarkCoroutine() => RunPass(null);

    /// <summary>Strips font, sprite, and emoji fallback paths so component activation can rasterize only the selected primary font.</summary>
    protected override void OnBeforeRun()
    {
        abortRun = false;
        completionMethod = "asyncGpuReadback";
        savedGlobalFallback = TMP_Settings.fallbackFontAssets;
        savedDefaultFont = TMP_Settings.defaultFontAsset;
        savedEmojiFallback = TMP_Settings.emojiFallbackTextAssets;
        savedDefaultSprite = TMP_Settings.defaultSpriteAsset;
        savedEmojiEnabled = TMP_Settings.enableEmojiSupport;
        savedPerAssetFallback = null;
        savedEmojiSupport = null;
        savedSpriteAssets = null;
        TMP_Settings.fallbackFontAssets = new List<TMP_FontAsset>();
        TMP_Settings.defaultFontAsset = null;
        TMP_Settings.emojiFallbackTextAssets = new List<TMP_Asset>();
        TMP_Settings.defaultSpriteAsset = null;
        TMP_Settings.enableEmojiSupport = false;
    }

    /// <summary>Restores the fallback settings, then empties the dynamic atlas the run rasterized so its grown texture stops occupying memory between runs; in the editor also persists the shrunk font (the atlas is a sub-asset that otherwise survives play mode and bloats the saved .asset).</summary>
    protected override void OnAfterRun()
    {
        TMP_Settings.fallbackFontAssets = savedGlobalFallback;
        TMP_Settings.defaultFontAsset = savedDefaultFont;
        TMP_Settings.emojiFallbackTextAssets = savedEmojiFallback;
        TMP_Settings.defaultSpriteAsset = savedDefaultSprite;
        TMP_Settings.enableEmojiSupport = savedEmojiEnabled;
        if (savedPerAssetFallback != null && fontAssets != null)
            for (int i = 0; i < fontAssets.Length && i < savedPerAssetFallback.Length; i++)
                fontAssets[i].fallbackFontAssetTable = savedPerAssetFallback[i];
        if (textTargets != null && savedEmojiSupport != null && savedSpriteAssets != null)
            for (int i = 0; i < textTargets.Length && i < savedEmojiSupport.Length && i < savedSpriteAssets.Length; i++)
            {
                textTargets[i].emojiFallbackSupport = savedEmojiSupport[i];
                textTargets[i].spriteAsset = savedSpriteAssets[i];
            }

        if (fontAssets != null)
            foreach (var font in fontAssets)
            {
                if (font == null) continue;
                font.ClearFontAssetData(true);
#if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(font);
                UnityEditor.AssetDatabase.SaveAssetIfDirty(font);
#endif
            }
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
        var texts = new List<TMP_Text>();
        foreach (var tmp in GetComponentsInChildren<TMP_Text>(true))
            if (tmp.gameObject != gameObject)
            {
                list.Add(tmp.gameObject);
                texts.Add(tmp);
            }
        targets = list.ToArray();
        textTargets = texts.ToArray();

        if (targets.Length == 0)
        {
            SetRunStatus("skipped", "No TMP_Text children were found");
            Debug.LogError("[TMP GlyphRaster] No TMP_Text children found.");
            return false;
        }

        savedEmojiSupport = new bool[textTargets.Length];
        savedSpriteAssets = new TMP_SpriteAsset[textTargets.Length];
        for (int i = 0; i < textTargets.Length; i++)
        {
            var text = textTargets[i];
            savedEmojiSupport[i] = text.emojiFallbackSupport;
            savedSpriteAssets[i] = text.spriteAsset;
            text.emojiFallbackSupport = false;
            text.spriteAsset = null;
        }

        var glyphText = BenchmarkConfig.Instance != null ? BenchmarkConfig.Instance.GlyphRasterText : null;
        if (!string.IsNullOrEmpty(glyphText))
            foreach (var text in textTargets)
                text.text = glyphText;

        var fonts = new List<TMP_FontAsset>();
        foreach (var text in textTargets)
            if (text.font != null && !fonts.Contains(text.font))
                fonts.Add(text.font);
        fontAssets = fonts.ToArray();
        if (fontAssets.Length == 0)
        {
            SetRunStatus("skipped", "No primary TMP_FontAsset is assigned");
            Debug.LogError("[TMP GlyphRaster] No primary TMP_FontAsset is assigned.");
            return false;
        }
        foreach (var font in fontAssets)
            if (font.atlasPopulationMode != AtlasPopulationMode.Dynamic)
            {
                SetRunStatus("unsupported", $"'{font.name}' is not a Dynamic FontAsset");
                Debug.LogError($"[TMP GlyphRaster] '{font.name}' is not a Dynamic FontAsset backed by the assigned source font file.");
                return false;
            }

        savedPerAssetFallback = new List<TMP_FontAsset>[fontAssets.Length];
        for (int i = 0; i < fontAssets.Length; i++)
        {
            savedPerAssetFallback[i] = fontAssets[i].fallbackFontAssetTable;
            fontAssets[i].fallbackFontAssetTable = new List<TMP_FontAsset>();
        }
        Debug.Log("[TMP GlyphRaster] Trigger=GameObject enable; fallback local/global/default/sprite/emoji=off; completion=async GPU readback.");
        return true;
    }

    protected override void Deactivate()
    {
        if (targets == null) return;
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

    protected override IEnumerator AwaitAsyncCompletion(float cpuMs)
    {
        double dispatchStart = Time.realtimeSinceStartupAsDouble - cpuMs / 1000.0;
        var textures = new List<Texture>();
        foreach (var font in fontAssets)
        {
            var atlases = font.atlasTextures;
            if (atlases == null) continue;
            for (int i = 0; i < atlases.Length; i++)
                if (atlases[i] != null)
                    textures.Add(atlases[i]);
        }
        yield return AwaitGpuTextureCompletion(dispatchStart, textures,
            AbortGpuCompletionMeasurement);
    }

    void AbortGpuCompletionMeasurement(string reason)
    {
        abortRun = true;
        completionMethod = "unavailable";
        lastE2eMs = float.NaN;
        SetRunStatus("failed", reason);
        Debug.LogWarning($"[TMP GlyphRaster] {reason}; E2E measurement aborted.");
    }

    protected override bool ShouldAbortRun() => abortRun;

    protected override int CountGlyphs()
    {
        int total = 0;
        foreach (var font in fontAssets)
            total += FontAssetUtils.GlyphCount(font);
        return total;
    }

    protected override GlyphExecutionSample CaptureExecutionDiagnostics()
    {
        var sample = new GlyphExecutionSample
        {
            trigger = "GameObject.SetActive(true)",
            rasterBackend = "CpuTextCore",
            threading = "mainThread",
            completion = completionMethod,
            fallback = "localGlobalDefaultSpriteAndEmojiDisabled"
        };
        foreach (var font in fontAssets)
        {
            var textures = font.atlasTextures;
            if (textures == null) continue;
            for (int i = 0; i < textures.Length; i++)
            {
                var texture = textures[i];
                if (texture == null) continue;
                sample.atlases.Add(new GlyphAtlasExecutionData
                {
                    mode = $"{font.name}:atlas{i}",
                    requestedPath = "engineNative",
                    backend = "CpuTextCore",
                    storage = texture.GetType().Name,
                    cpuMirror = texture.isReadable,
                    gpuUploadTarget = false,
                    preparation = "mainThread",
                    writePaths = new List<string> { "textCoreInternal" }
                });
            }
        }
        return sample;
    }

    protected override string Diagnostics(string label)
    {
        var sb = new StringBuilder();
        sb.Append($"[Atlas {label}] ");
        foreach (var font in fontAssets)
        {
            var textures = font.atlasTextures;
            long checksum = textures is { Length: > 0 } ? BenchmarkAtlasUtils.Checksum(textures[0]) : 0;
            bool cpuMirror = textures is { Length: > 0 } && textures[0] != null && textures[0].isReadable;
            sb.Append($"'{font.name}': glyphs={FontAssetUtils.GlyphCount(font)} chars={FontAssetUtils.CharacterCount(font)} atlasCount={textures?.Length ?? 0} pixelChecksum={checksum} trigger=GameObject-enable raster=CPU-TextCore completion={completionMethod} cpuMirror={cpuMirror} fallback=local/global/default/sprite/emoji-off  ");
        }
        return sb.ToString();
    }
}
