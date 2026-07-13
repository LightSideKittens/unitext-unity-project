using System.Collections.Generic;
using System.Reflection;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

static class UIToolkitFontIsolation
{
    static readonly FieldInfo osFallbackField = typeof(TextSettings).GetField(
        "m_FallbackOSFontAssets", BindingFlags.Instance | BindingFlags.NonPublic);

    /// <summary>Verifies that the panel cannot resolve missing glyphs through local, global, sprite, emoji, default-font, or Dynamic OS fallback sources.</summary>
    public static bool Validate(PanelSettings panelSettings, FontAsset primaryFont, out string error)
    {
        error = null;
        if (panelSettings == null)
            return Fail("PanelSettings is not assigned.", out error);

        var settings = panelSettings.textSettings;
        if (settings == null)
            return Fail("Panel Text Settings is not assigned; Unity would create default settings with Dynamic OS fallback.", out error);
        if (osFallbackField == null)
            return Fail("This Unity version does not expose the serialized OS fallback field expected by the benchmark.", out error);
        if (osFallbackField.GetValue(settings) is not List<FontAsset> osFallbacks)
            return Fail("The OS fallback list is null; Unity would populate it from system fonts on first use.", out error);
        if (osFallbacks.Count != 0)
            return Fail($"Panel Text Settings contains {osFallbacks.Count} Dynamic OS fallback font(s).", out error);
        if (settings.fallbackFontAssets is { Count: > 0 })
            return Fail($"Panel Text Settings contains {settings.fallbackFontAssets.Count} global fallback font(s).", out error);
        if (settings.defaultFontAsset != null)
            return Fail($"Panel Text Settings default font '{settings.defaultFontAsset.name}' is a fallback source.", out error);
        if (settings.enableEmojiSupport || settings.emojiFallbackTextAssets is { Count: > 0 })
            return Fail("Panel Text Settings emoji fallback is enabled.", out error);
        if (settings.defaultSpriteAsset != null || settings.fallbackSpriteAssets is { Count: > 0 })
            return Fail("Panel Text Settings sprite fallback is enabled.", out error);
        if (primaryFont != null && primaryFont.fallbackFontAssetTable is { Count: > 0 })
            return Fail($"Primary font '{primaryFont.name}' contains {primaryFont.fallbackFontAssetTable.Count} local fallback font(s).", out error);
        return true;
    }

    static bool Fail(string message, out string error)
    {
        error = message;
        return false;
    }
}
