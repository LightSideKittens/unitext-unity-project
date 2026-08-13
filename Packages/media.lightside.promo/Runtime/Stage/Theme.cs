using UnityEngine;
using Ramp = LightSide.Gradient;
using Stop = LightSide.GradientStop;
using Interp = LightSide.GradientInterpolation;

namespace LightSide.Promo
{
    /// <summary>
    /// The look a reel is drawn in: palette, radius and padding scales, elevation and type sizes.
    /// </summary>
    /// <remarks>
    /// Carried on <see cref="Stage"/> and propagated to every nested stage, so a whole reel changes appearance by
    /// assigning one instance. It is deliberately neither a static class nor an asset: a palette is policy, and a
    /// reel that cannot swap it cannot show the same shot in two liveries.
    /// <para>
    /// The product surface is dark and carries <see cref="Brand"/>; every surface that is not the product uses the
    /// neutral paper set. A viewer who has never heard of the product must be able to tell in one frame which
    /// window is the one being sold.
    /// </para>
    /// </remarks>
    public class Theme
    {
        /// <summary>Smallest radius a corner may be reduced to before it stops reading as a corner at all.</summary>
        public const float MinRadius = 4f;

        public Color Coral { get; set; } = Hex("#F5726B");
        public Color Orange { get; set; } = Hex("#F58E4A");
        public Color Magenta { get; set; } = Hex("#F03CD0");
        public Color Violet { get; set; } = Hex("#7B3FF2");
        public Color Glow { get; set; } = Hex("#C13BE0");

        public Color Background { get; set; } = Hex("#14101F");
        public Color Bar { get; set; } = Hex("#1D1730");
        public Color Surface { get; set; } = Hex("#221B38");
        public Color Line { get; set; } = Hex("#332A50");
        public Color Text { get; set; } = Hex("#F2EEFF");
        public Color TextSoft { get; set; } = Hex("#A79BC8");

        public Color Paper { get; set; } = Hex("#FFFFFF");
        public Color PaperDim { get; set; } = Hex("#F3F4FB");
        public Color PaperLine { get; set; } = Hex("#E3E5F2");
        public Color Canvas { get; set; } = Hex("#F6F6FC");
        public Color Ink { get; set; } = Hex("#141420");
        public Color InkSoft { get; set; } = Hex("#54546A");
        public Color Accent { get; set; } = Hex("#7A5BFF");

        public Color Shadow { get; set; } = new Color(0.03f, 0.02f, 0.08f, 0.55f);

        /// <summary>Corner radii, smallest to largest.</summary>
        public float RadiusXs { get; set; } = 10f;
        public float RadiusSm { get; set; } = 14f;
        public float RadiusMd { get; set; } = 20f;
        public float RadiusLg { get; set; } = 28f;
        public float RadiusXl { get; set; } = 36f;
        public float RadiusXxl { get; set; } = 48f;

        /// <summary>How square a rounded corner is drawn: 0 is a circular arc, 1 a squircle.</summary>
        public float Smoothing { get; set; } = 0.35f;

        /// <summary>
        /// How many glyphs are mid-arrival at once during a reveal. One is a pop; several overlap into a wave.
        /// </summary>
        public float RevealSpread { get; set; } = 4.5f;

        public float PadXs { get; set; } = 6f;
        public float PadSm { get; set; } = 10f;
        public float PadMd { get; set; } = 16f;
        public float PadLg { get; set; } = 24f;
        public float PadXl { get; set; } = 34f;

        /// <summary>
        /// Type sizes for a 1920x1080 frame. These are a floor and not a target: a hero frame wants roughly twice
        /// <see cref="Title"/>, and every frame wants one element that dominates it.
        /// </summary>
        public float Hero { get; set; } = 168f;
        public float Title { get; set; } = 104f;
        public float Head { get; set; } = 64f;
        public float Body { get; set; } = 42f;
        public float Small { get; set; } = 32f;

        /// <summary>The brand ramp, in Oklab so the sweep through magenta stays saturated instead of greying out.</summary>
        public Ramp Brand => new Ramp(
            new[]
            {
                new Stop(0f, Orange),
                new Stop(0.22f, Coral),
                new Stop(0.6f, Magenta),
                new Stop(1f, Violet)
            },
            Interp.Perceptual);

        /// <summary>
        /// Radius of a child inset by <paramref name="gap"/> inside a parent of radius <paramref name="outer"/>.
        /// </summary>
        /// <remarks>
        /// Only equal centres keep two arcs parallel. Giving parent and child the same radius swells the visible gap
        /// from <c>gap</c> along the straight edges to <c>gap * sqrt(2)</c> at the corner — a 41% bulge that reads as
        /// wrong without being nameable. Never type the second radius by hand.
        /// </remarks>
        public float Inner(float outer, float gap) => Mathf.Max(MinRadius, outer - gap);

        private static Color Hex(string value) =>
            ColorParsing.TryParse(value, out var parsed) ? (Color)parsed : Color.magenta;
    }
}
