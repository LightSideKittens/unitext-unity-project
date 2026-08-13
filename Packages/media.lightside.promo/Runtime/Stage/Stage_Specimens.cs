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
        public UniTextSpecimen UniTextSpecimen(Transform parent, float size, Color color,
            HAlign horizontal = HAlign.Left, VAlign vertical = VAlign.Top)
        {
            var label = Label(parent, string.Empty, size, color, horizontal, vertical);
            return new UniTextSpecimen(label, Reveal(label, new FadeRevealHandler()));
        }

        /// <summary>A live TextMeshPro specimen on <paramref name="font"/>.</summary>
        public TmpSpecimen TmpSpecimen(Transform parent, TMP_FontAsset font, float size, Color color,
            TextAlignmentOptions alignment = TextAlignmentOptions.TopLeft) =>
            new TmpSpecimen(BuildTmp<TextMeshProUGUI>("TMP", parent, font, size, color, alignment));

        /// <summary>A live TextMeshPro specimen with the community RTL fixer in front of it.</summary>
        public RtlTmpSpecimen RtlTmpSpecimen(Transform parent, TMP_FontAsset font, float size, Color color,
            TextAlignmentOptions alignment = TextAlignmentOptions.TopRight)
        {
            var text = BuildTmp<RTLTextMeshPro>("RTL TMP", parent, font, size, color, alignment);
            text.PreserveNumbers = false;
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
