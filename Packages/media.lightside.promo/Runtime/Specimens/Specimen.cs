using UnityEngine;

namespace LightSide.Promo
{
    /// <summary>
    /// One text engine rendering one string, behind an interface that hides which engine it is.
    /// </summary>
    /// <remarks>
    /// A comparison slide must not branch on the engine. Every engine writes text on differently — UniText advances
    /// a reveal frontier, TextMeshPro clamps a visible-character count — and a slide that knows the difference ends
    /// up written three times, drifting apart at every edit.
    /// <para>
    /// Every specimen renders <em>live</em>. Nothing here draws a picture of what an engine would do; the failures a
    /// comparison shows are the failures that engine actually produces from the same string, which is the only
    /// reason the comparison is worth showing at all.
    /// </para>
    /// </remarks>
    public abstract class Specimen
    {
        /// <summary>The rect the text occupies, for anything that annotates or measures it.</summary>
        public abstract RectTransform Rect { get; }

        /// <summary>The engine's name as a viewer should read it.</summary>
        public abstract string EngineName { get; }

        /// <summary>Replaces the whole string.</summary>
        public abstract void SetText(string text);

        /// <summary>
        /// Shows the leading <paramref name="fill"/> fraction of the string, 0 to 1.
        /// </summary>
        /// <remarks>
        /// Pure: the same argument always produces the same picture, so a comparison can be scrubbed and captured
        /// frame by frame. Drive it from a <see cref="GlyphReveal.Frontier"/> window, never from a clock.
        /// </remarks>
        public abstract void Reveal(float fill);

        /// <summary>Opacity of the whole specimen.</summary>
        public abstract void SetAlpha(float alpha);
    }
}
