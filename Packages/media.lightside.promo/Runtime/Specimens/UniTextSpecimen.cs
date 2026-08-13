using UnityEngine;

namespace LightSide.Promo
{
    /// <summary>A <see cref="Specimen"/> rendered by UniText, with no font asset assigned.</summary>
    /// <remarks>
    /// Leaving the font unset is deliberate and is itself part of what several shots claim: the system-font cascade
    /// is the primary, so every script in the demo string resolves without anything being imported.
    /// </remarks>
    public sealed class UniTextSpecimen : Specimen
    {
        private readonly UniText text;
        private readonly GlyphReveal reveal;

        internal UniTextSpecimen(UniText text, GlyphReveal reveal)
        {
            this.text = text;
            this.reveal = reveal;
        }

        /// <summary>The live component, for annotations that read glyph positions.</summary>
        public UniText Text => text;

        public override RectTransform Rect => text.rectTransform;

        public override string EngineName => "UniText";

        public override void SetText(string value) => text.Text = value;

        public override void Reveal(float fill) => reveal.Fill = fill;

        public override void SetAlpha(float alpha) => Stage.Alpha(text, alpha);
    }
}
