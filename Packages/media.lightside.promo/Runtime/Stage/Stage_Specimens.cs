using RTLTMPro;
using TMPro;
using UnityEngine;
using HAlign = LightSide.HorizontalAlignment;
using VAlign = LightSide.VerticalAlignment;

namespace LightSide.Promo
{
    /// <summary>Builds live text engines side by side.</summary>
    public sealed partial class Stage
    {
        /// <summary>
        /// A UniText specimen with no font asset, so the system-font cascade resolves every script in the string.
        /// </summary>
        /// <remarks>
        /// The livery's <see cref="Promo.Theme.BodyFace"/> is cleared rather than never applied: it dresses the
        /// frame's chrome, and a specimen is not chrome. A comparison is only worth showing when each engine is
        /// given exactly what the claim says it needs, and here that is nothing.
        /// </remarks>
        public UniTextSpecimen UniTextSpecimen(Transform parent, float size, Color color,
            HAlign horizontal = HAlign.Left, VAlign vertical = VAlign.Top)
        {
            var label = Label(parent, string.Empty, size, color, horizontal, vertical);
            label.Font = null;
            return new UniTextSpecimen(label, Reveal(label, new FadeRevealHandler()));
        }

        /// <summary>A live TextMeshPro specimen on <paramref name="font"/>.</summary>
        public TmpSpecimen TmpSpecimen(Transform parent, TMP_FontAsset font, float size, Color color,
            TextAlignmentOptions alignment = TextAlignmentOptions.TopLeft) =>
            new TmpSpecimen(BuildTmp<TextMeshProUGUI>("TMP", parent, font, size, color, alignment));

        /// <summary>A live TextMeshPro specimen with the community RTL fixer in front of it.</summary>
        /// <remarks>
        /// The fixer is configured the way it serves it best. <c>PreserveNumbers</c> is on because with it off the
        /// plugin rewrites Western digits into Arabic-Indic ones, and a frame showing <c>٥</c> where the source said
        /// <c>5</c> reads as corruption rather than as the transliteration feature it is — a comparison that wins by
        /// misconfiguring the other side proves nothing about either.
        /// </remarks>
        public RtlTmpSpecimen RtlTmpSpecimen(Transform parent, TMP_FontAsset font, float size, Color color,
            TextAlignmentOptions alignment = TextAlignmentOptions.TopRight)
        {
            var text = BuildTmp<RTLTextMeshPro>("RTL TMP", parent, font, size, color, alignment);
            text.PreserveNumbers = true;
            text.Farsi = false;
            text.FixTags = true;
            return new RtlTmpSpecimen(text);
        }

        private T BuildTmp<T>(string name, Transform parent, TMP_FontAsset font, float size, Color color,
            TextAlignmentOptions alignment) where T : TextMeshProUGUI
        {
            var rect = Node(name, parent);
            Stretch(rect);

            var text = rect.gameObject.AddComponent<T>();
            text.raycastTarget = false;
            text.font = font;
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Overflow;
            return text;
        }
    }
}
