using TMPro;
using UnityEngine;

namespace LightSide.Promo
{
    /// <summary>A <see cref="Specimen"/> rendered by a live TextMeshPro component.</summary>
    /// <remarks>
    /// Write-on is a visible-character clamp rather than a frontier, and it needs the layout to have run at least
    /// once before <c>textInfo.characterCount</c> is meaningful — so the count is re-read on every reveal instead of
    /// cached at build time.
    /// </remarks>
    public class TmpSpecimen : Specimen
    {
        private readonly TextMeshProUGUI text;

        internal TmpSpecimen(TextMeshProUGUI text)
        {
            this.text = text;
        }

        /// <summary>The live component, for annotations that read character positions.</summary>
        public TextMeshProUGUI Text => text;

        public override RectTransform Rect => text.rectTransform;

        public override string EngineName => "TextMeshPro";

        public override void SetText(string value)
        {
            text.text = value;
            text.ForceMeshUpdate();
        }

        public override void Reveal(float fill)
        {
            var total = text.textInfo != null ? text.textInfo.characterCount : 0;
            text.maxVisibleCharacters = Mathf.CeilToInt(Mathf.Clamp01(fill) * total);
        }

        public override void SetAlpha(float alpha) => Stage.Alpha(text, alpha);
    }
}
