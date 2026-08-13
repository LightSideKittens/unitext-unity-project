using UnityEngine;

namespace LightSide.Promo
{
    /// <summary>A headline and the quieter line under it.</summary>
    public readonly struct Claim
    {
        internal Claim(UniText headline, UniText sub)
        {
            Headline = headline;
            Sub = sub;
        }

        public UniText Headline { get; }

        /// <summary>The second line, or null when the claim is a single sentence.</summary>
        public UniText Sub { get; }

        /// <summary>Fades both lines, the second a beat behind the first so they read in order.</summary>
        public void Pose(float t)
        {
            Stage.Alpha(Headline, Ease.EmphasizedIn.Window(t, 0f, Fade));
            if (Sub) Stage.Alpha(Sub, Ease.EmphasizedIn.Window(t, Gap, Fade));
        }

        private const float Fade = 0.55f;
        private const float Gap = 0.3f;
    }

    /// <summary>Builds the lines a shot states its point with.</summary>
    public sealed partial class Stage
    {
        /// <summary>
        /// A headline at the top or bottom of the frame, with an optional quieter line beneath it.
        /// </summary>
        /// <remarks>
        /// The headline names what the viewer <em>gets</em>, in the plainest words available. Mechanism belongs on
        /// the second line or in the picture: a viewer who has to work out the benefit from a description of how
        /// something works has already stopped watching.
        /// <para>
        /// Neither line wraps, and the band reserved for them assumes it. A phrase too long for the frame runs off
        /// its edges instead of quietly growing downward into the shot — visible in one glance, which a second line
        /// silently overlapping the content is not.
        /// </para>
        /// </remarks>
        public Claim Claim(Transform parent, string headline, string sub = null, bool top = true)
        {
            var edge = top ? Half.y : -Half.y;
            var direction = top ? -1f : 1f;
            var margin = Height * 0.055f;
            var first = edge + direction * (margin + Theme.Head * 0.75f);

            var title = Label(parent, headline, Theme.Head, Theme.Text);
            Box(title.rectTransform, new Vector2(0f, first), new Vector2(Width * 0.86f, Theme.Head * 1.5f));
            title.WordWrap = false;

            var used = margin + Theme.Head * 1.5f;
            UniText note = null;

            if (!string.IsNullOrEmpty(sub))
            {
                note = Label(parent, sub, Theme.Body, Theme.TextSoft);
                Box(note.rectTransform, new Vector2(0f, first + direction * (Theme.Head * 0.9f + Theme.Body * 0.6f)),
                    new Vector2(Width * 0.86f, Theme.Body * 1.6f));
                note.WordWrap = false;
                used += Theme.Body * 1.7f;
            }

            Reserve(top, used + Gutter);
            return new Claim(title, note);
        }

        /// <summary>Breathing room between a headline and whatever the shot puts under it.</summary>
        private float Gutter => Height * 0.035f;
    }
}
