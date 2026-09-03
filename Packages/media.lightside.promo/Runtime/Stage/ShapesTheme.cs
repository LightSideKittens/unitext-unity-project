using System;
using UnityEngine;
using Ramp = LightSide.Gradient;
using Stop = LightSide.GradientStop;
using Interp = LightSide.GradientInterpolation;

namespace LightSide.Promo
{
    /// <summary>
    /// The UniShapes livery: warm charcoal under the ramp the mark is drawn in, yellow through orange to hot pink.
    /// </summary>
    /// <remarks>
    /// <see cref="Brand"/> is the only member that changes shape; the four ramp fields keep their names and are
    /// retuned to this ramp's hues, so <see cref="Theme.Orange"/> is its second stop here rather than its first and
    /// <see cref="Theme.Violet"/> is a pink deep enough to stroke a picker on a warm surface.
    /// </remarks>
    [Serializable]
    [TypeDescription("Warm charcoal under the UniShapes ramp: yellow through orange to hot pink.")]
    public sealed class ShapesTheme : Theme
    {
        [SerializeField] private Color yellow = Hex("#FFD84A");

        public ShapesTheme()
        {
            Background = Hex("#15100D");
            Bar = Hex("#1E1712");
            Surface = Hex("#261D17");
            Line = Hex("#3E2F25");
            Text = Hex("#FFF5EB");
            TextSoft = Hex("#C8B2A0");

            Orange = Hex("#FF8A1F");
            Coral = Hex("#FF4B45");
            Magenta = Hex("#FF1F8E");
            Violet = Hex("#C21C9B");
            Glow = Hex("#FF3D7A");
            Accent = Hex("#FF7A1A");
            Shadow = new Color(0.05f, 0.02f, 0.01f, 0.6f);
        }

        /// <summary>The ramp's light end, so a sweep starts warm and ends hot.</summary>
        public Color Yellow
        {
            get => yellow;
            set => yellow = value;
        }

        public override Ramp Brand => new Ramp(
            new[]
            {
                new Stop(0f, Yellow),
                new Stop(0.36f, Orange),
                new Stop(0.68f, Coral),
                new Stop(1f, Magenta)
            },
            Interp.Perceptual);
    }
}
