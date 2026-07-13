using TMPro;
using UnityEngine.TextCore.Text;

/// <summary>Glyph/character counts for a font asset, uniform across TextCore and TMP assets. TextCore's tables are marked <c>[Obsolete]</c> under the Advanced Text Generator in Unity 6+ (still populated, but with no public replacement — the data moved internal/native), while on Unity 2021.x they are the normal, non-obsolete accessors. Reading them here is the single place that suppresses that deprecation, so no benchmark call site has to.</summary>
public static class FontAssetUtils
{
#pragma warning disable CS0618
    public static int GlyphCount(FontAsset font) => font.glyphTable.Count;
    public static int CharacterCount(FontAsset font) => font.characterTable.Count;
#pragma warning restore CS0618

    public static int GlyphCount(TMP_FontAsset font) => font.glyphTable.Count;
    public static int CharacterCount(TMP_FontAsset font) => font.characterTable.Count;
}
