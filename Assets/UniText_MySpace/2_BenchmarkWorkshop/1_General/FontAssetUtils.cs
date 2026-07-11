using TMPro;
using UnityEngine.TextCore.Text;

/// <summary>Glyph/character counts for font assets read from the lookup tables both TextCore and TMP font assets expose — the accessors that stay valid across supported Unity versions, unlike the <c>glyphTable</c>/<c>characterTable</c> lists deprecated by the Advanced Text Generator.</summary>
public static class FontAssetUtils
{
    public static int GlyphCount(FontAsset font) => font.glyphLookupTable.Count;
    public static int CharacterCount(FontAsset font) => font.characterLookupTable.Count;

    public static int GlyphCount(TMP_FontAsset font) => font.glyphLookupTable.Count;
    public static int CharacterCount(TMP_FontAsset font) => font.characterLookupTable.Count;
}
