using UnityEngine;

namespace LightSide.Promo
{
    /// <summary>
    /// One highlight spanning several lines: a single connected surface under a gradient, growing as the text arrives.
    /// </summary>
    /// <remarks>
    /// Block geometry is the case worth the seconds. Every other engine draws a highlight as one rectangle per line
    /// and leaves seams where they meet; this is one mesh, rounded only where the range truly begins and ends, and
    /// square where it merely wrapped.
    /// <para>
    /// <c>MergeThreshold</c> snaps line edges that differ by less than it to a shared edge, which is what turns two
    /// lines of nearly equal length into one clean block instead of a block with a two-pixel step in its side.
    /// </para>
    /// <para>
    /// The reveal collapses. A highlight is a surface drawn behind a range and takes no notice of the alpha of the
    /// glyphs above it, so text faded to nothing still carries its decoration — the bar would arrive before the words
    /// it belongs to.
    /// </para>
    /// </remarks>
    public sealed class HighlightPaintsSlide : Slide
    {
        [SerializeField] private string headline = "Highlight anything. Paint it anything.";
        [SerializeField] private string sub = "Search hits, mentions, spoilers — a gradient, not a grey box.";

        /// <summary>
        /// The sentence the highlight is drawn over.
        /// </summary>
        /// <remarks>
        /// The range opens mid-line on purpose. Its first fragment is a stub at the right of one line and its second
        /// runs the full width of the next, so the two share no horizontal span — the case block geometry has to
        /// answer for, and the one that used to drag each fragment out over the words beside it.
        /// </remarks>
        [SerializeField, TextArea(3, 8)]
        private string body =
            "Mark a range and it becomes a surface: <hl>one connected mesh across every line it wraps onto, " +
            "rounded where the range really begins and ends, and seamless everywhere it merely broke</hl>.";

        private Claim claim;
        private Showcase panel;
        private RevealModifier typewriter;
        private HighlightModifier highlight;

        protected override void OnBuild(Stage stage)
        {
            stage.Backdrop(stage.Root);
            claim = stage.Claim(stage.Root, headline, sub);

            panel = stage.Showcase("Highlight", stage.Root, "One HighlightModifier, block geometry",
                body, stage.ContentHeight * 0.11f);

            highlight = new HighlightModifier
            {
                Provider = stage.BrandCatalog(Swatch, 12f),
                Paint = PaintRef.Named(Swatch),
                GeometryMapping = GeometryMapping.Block,
                PaintMapping = RangePaintMapping.TextBlock,
                Height = RangeHeight.Content,
                Padding = new UnitVector2(new Vector2(0.22f, 0.12f), UnitKind.Em),
                CornerRadius = UnitValue.Em(0.3f),
                MergeThreshold = UnitValue.Em(0.1f)
            };

            panel.Body.Styles.Add(Style.Tag(highlight, "hl"));
            typewriter = stage.Typewriter(panel.Body, new FadeRevealHandler());

            Cue("writeon", First);
            Cue("morph", Morph);
        }

        protected override void OnRender(float local)
        {
            claim.Pose(local);
            panel.Pose(local);
            typewriter.Front = UnitValue.Percent(GlyphReveal.Frontier.Window(local, First, WriteOn) * 100f);

            var breath = 0.5f + 0.5f * Mathf.Cos(Mathf.Max(0f, local - Morph) * Mathf.PI * 0.6f);
            highlight.CornerRadius = UnitValue.Em(Mathf.LerpUnclamped(Square, Round, breath));
        }

        private const string Swatch = "promo.highlight";

        private const float First = 0.35f;
        private const float WriteOn = 2.3f;

        /// <summary>When the corner radius starts breathing — the geometry knob, made visible.</summary>
        private const float Morph = 2.8f;

        /// <summary>
        /// The band the corner radius travels, in em.
        /// </summary>
        /// <remarks>
        /// Bounded by what the wrap can carry. Where two line fragments overlap horizontally by less than the
        /// radius, the rounding has nowhere to land and swells the bridge between them out over the words either
        /// side. A range that starts at the beginning of a line keeps that overlap full-width; the ceiling here is
        /// what keeps the shape honest when it does not.
        /// </remarks>
        private const float Square = 0.08f;

        private const float Round = 0.3f;
    }
}
