using UnityEngine;

namespace LightSide.Promo
{
    /// <summary>
    /// A plain word becomes something elaborate one layer at a time, with the stack assembling beside it.
    /// </summary>
    /// <remarks>
    /// Every layer exists from the first frame and arrives by growing its own magnitude from zero. Adding styles as
    /// the shot runs would be a structural change per beat — a re-parse, and a slide that cannot be scrubbed
    /// backwards. Growing a width is a mesh update and a pure function of time.
    /// <para>
    /// Styles are declared back to front, which is the reverse of the order they arrive in: a stroke aligned outward
    /// renders behind the fill, so the widest has to be furthest back or it buries the ones inside it.
    /// </para>
    /// </remarks>
    public sealed class KitStackSlide : Slide
    {
        [SerializeField] private string word = "PLAY";
        [SerializeField] private string headline = "Add as many layers as you like.";
        [SerializeField] private string sub = "One set of paints — colour, gradient, photo — on any layer you like.";
        [SerializeField] private string payoff = "No extra shaders. One draw call.";

        /// <summary>
        /// A display face for the specimen. Left empty, the system-font cascade supplies a text face, whose thin
        /// stems leave the outer strokes nothing to sit on.
        /// </summary>
        [SerializeField] private UniTextFont displayFont;

        /// <summary>
        /// One catalog of named paints, shared by every layer on the specimen.
        /// </summary>
        /// <remarks>
        /// The point of the shot is that this is a set, not a property of any one layer: a stroke, a shadow, a glow
        /// and a fill all take their colour from the same names, and a name can be a flat colour, a gradient or a
        /// texture without any of them knowing which.
        /// </remarks>
        [SerializeField] private UniTextPaints paints;

        private readonly Ease enter = Ease.EmphasizedIn;

        private UniText specimen;
        private Ledger stack;
        private Chip[] chips;

        private GlowModifier glow;
        private ShadowModifier shadow;
        private StrokeModifier outer;
        private StrokeModifier middle;
        private StrokeModifier inner;
        private FillModifier fill;

        private Claim claim;
        private Claim note;

        protected override void OnBuild(Stage stage)
        {
            var theme = stage.Theme;
            stage.Backdrop(stage.Root);

            claim = stage.Claim(stage.Root, headline, sub);
            note = stage.Claim(stage.Root, payoff, top: false);

            var column = stage.Width * 0.58f;
            var strip = stage.ContentHeight * 0.26f;

            specimen = stage.Label(stage.Root, word, stage.ContentHeight * 0.36f, theme.Text);
            stage.Box(specimen.rectTransform,
                new Vector2(-stage.Width * 0.19f, stage.ContentCentre + strip * 0.5f),
                new Vector2(column, stage.ContentHeight * 0.72f));
            if (displayFont) specimen.Font = displayFont;

            var catalog = new AssetPaintProvider { Asset = paints };

            glow = new GlowModifier
            {
                Provider = catalog,
                Paint = PaintRef.Named("shine"),
                Radius = UnitValue.Em(0f)
            };
            shadow = new ShadowModifier
            {
                Paint = PaintRef.Solid(new Color32(0, 0, 0, 190)),
                Offset = new UnitVector2(Vector2.zero, UnitKind.Em),
                Blur = UnitValue.Em(0f),
                Spread = UnitValue.Em(0f)
            };
            outer = Stroke(catalog, PaintRef.Named("neon"));
            middle = Stroke(catalog, PaintRef.Named("candy"));
            inner = Stroke(catalog, PaintRef.Solid(Color.white));
            fill = new FillModifier { Provider = catalog, Paint = PaintRef.Named(Swatches[0]) };
            fill.Tint = Clear;

            specimen.Styles.Add(Style.WholeText(glow));
            specimen.Styles.Add(Style.WholeText(shadow));
            specimen.Styles.Add(Style.WholeText(outer));
            specimen.Styles.Add(Style.WholeText(middle));
            specimen.Styles.Add(Style.WholeText(inner));
            specimen.Styles.Add(Style.WholeText(fill));

            BuildPalette(stage, catalog, -stage.Width * 0.48f, stage.ContentBottom + strip * 0.62f, strip);

            stack = stage.Ledger("Stack", stage.Root, "One paint set, every layer", new[]
            {
                new LedgerEntry("FillModifier", Swatches[0], "1", theme.Violet),
                new LedgerEntry("StrokeModifier", "white", "2", theme.Violet),
                new LedgerEntry("StrokeModifier", "candy", "3", theme.Violet),
                new LedgerEntry("StrokeModifier", "neon", "4", theme.Violet),
                new LedgerEntry("ShadowModifier", "black", "5", theme.Violet),
                new LedgerEntry("GlowModifier", "shine", "6", theme.Violet)
            }, new Vector2(stage.Width * 0.29f, stage.ContentCentre), stage.Width * 0.34f, onBrand: true);

            for (var i = 0; i < Steps; i++) Cue("snap", First + i * Step);
            for (var i = 1; i < Swatches.Length; i++) Cue("repaint", Repaint + i * Swap);
        }

        protected override void OnRender(float local)
        {
            fill.Tint = Color32.Lerp(Clear, Opaque, Arrive(local, 0));

            inner.Width = UnitValue.Em(0.08f * Arrive(local, 1));
            middle.Width = UnitValue.Em(0.18f * Arrive(local, 2));
            outer.Width = UnitValue.Em(0.30f * Arrive(local, 3));

            var settle = Arrive(local, 4);
            shadow.Blur = UnitValue.Em(0.12f * settle);
            shadow.Spread = UnitValue.Em(0.27f * settle);
            shadow.Offset = new UnitVector2(new Vector2(0f, -0.09f * settle), UnitKind.Em);

            glow.Radius = UnitValue.Em(0.35f * Arrive(local, 5));

            var active = Mathf.Clamp(Mathf.FloorToInt((local - Repaint) / Swap) + 1, 0, Swatches.Length - 1);
            fill.Paint = PaintRef.Named(Swatches[active]);
            stack.SetTrailing(0, Swatches[active]);

            var shown = enter.Window(local, Palette, 0.5f);
            for (var i = 0; i < chips.Length; i++)
            {
                var chip = chips[i];
                var lift = i == active ? Spring.Pop.Evaluate(local - (Repaint + (i - 1) * Swap)) : 0f;

                Stage.Scale(chip.Rect, Mathf.Lerp(1f, 1.16f, lift) * shown);
                Stage.Alpha(chip.Block, Mathf.Lerp(Resting, 1f, lift) * shown);
                Stage.Alpha(chip.Label, Mathf.Lerp(Resting, 1f, lift) * shown);
            }

            for (var i = 0; i < Steps; i++) stack.Pose(i, enter.Window(local, First + i * Step, RowIn));

            claim.Pose(local);
            note.Pose(local - (First + Steps * Step));
        }

        /// <summary>
        /// The catalog laid out as a row of chips: the paint itself above, its name beneath.
        /// </summary>
        /// <remarks>
        /// Every swatch is on screen at once, and the one the fill is wearing lifts out of the row. That pairing is
        /// the whole argument — a viewer who reads nothing still sees a set of paints, and sees one of them travel
        /// from the row onto the letters.
        /// </remarks>
        private void BuildPalette(Stage stage, IPaintProvider catalog, float left, float middle, float height)
        {
            var theme = stage.Theme;
            var slot = BlockSize * SlotGap;
            var width = Swatches.Length * slot;
            var row = stage.Node("Palette", stage.Root);
            stage.Box(row, new Vector2(left + width * 0.5f, middle), new Vector2(width, height));

            chips = new Chip[Swatches.Length];
            for (var i = 0; i < Swatches.Length; i++)
            {
                var cell = stage.Node("Chip" + i, row);
                stage.Box(cell, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                    new Vector2((i + 0.5f) * slot, 0f), new Vector2(slot, height));

                var swatch = stage.Label(cell, Block, BlockSize, Color.white);
                stage.Anchor(swatch.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                    Vector2.zero, new Vector2(0f, height * 0.58f));
                swatch.WordWrap = false;
                swatch.Styles.Add(Style.WholeText(new FillModifier
                {
                    Provider = catalog,
                    Paint = PaintRef.Named(Swatches[i])
                }));

                var label = stage.Label(cell, Swatches[i], theme.Small * 0.9f, theme.TextSoft);
                stage.Anchor(label.rectTransform, Vector2.zero, new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                    Vector2.zero, new Vector2(0f, height * 0.34f));
                label.WordWrap = false;

                chips[i] = new Chip(cell, swatch, label);
            }
        }

        /// <summary>
        /// How far layer <paramref name="index"/> has arrived, on a spring so each one lands with a knock rather
        /// than easing politely into place.
        /// </summary>
        private static float Arrive(float local, int index) =>
            Mathf.Clamp01(Spring.Pop.Evaluate(local - (First + index * Step)));

        private static StrokeModifier Stroke(IPaintProvider catalog, PaintRef paint) => new StrokeModifier
        {
            Provider = catalog,
            Paint = paint,
            Width = UnitValue.Em(0f),
            Align = 1f
        };

        /// <summary>
        /// What the fill wears, in order, once the stack has assembled.
        /// </summary>
        /// <remarks>
        /// A gradient, a photograph and another gradient, all from the same catalog and all reached by name. The
        /// texture in the middle is the load-bearing one: a layer that accepts an image accepts anything, and no
        /// modifier here knows or asks which kind a name resolves to.
        /// </remarks>
        private static readonly string[] Swatches = { "gold", "fire", "ice", "neon", "candy", "photo", "shine" };

        /// <summary>
        /// A chip of one paint, drawn as text so the catalog stays the only place a paint is defined.
        /// </summary>
        /// <remarks>
        /// A shape's paint and a text's paint are different systems: painting the chip as a <see cref="UniShape"/>
        /// would mean re-authoring each gradient and re-pointing each texture, and the shot would then be claiming
        /// one set of paints while showing two. A block glyph under a <see cref="FillModifier"/> is the paint itself.
        /// </remarks>
        private readonly struct Chip
        {
            internal Chip(RectTransform rect, UniText block, UniText label)
            {
                Rect = rect;
                Block = block;
                Label = label;
            }

            public RectTransform Rect { get; }
            public UniText Block { get; }
            public UniText Label { get; }
        }

        private static readonly Color32 Clear = new Color32(255, 255, 255, 0);
        private static readonly Color32 Opaque = new Color32(255, 255, 255, 255);

        private const int Steps = 6;
        private const float First = 0.55f;
        private const float Step = 0.62f;
        private const float RowIn = 0.34f;

        /// <summary>When the palette fades up — before the fill starts drawing from it.</summary>
        private const float Palette = First + Steps * Step * 0.4f;

        /// <summary>When the fill starts changing paint — after the last layer has landed.</summary>
        private const float Repaint = First + Steps * Step + 0.5f;

        private const float Swap = 0.45f;

        /// <summary>How visible a chip is while some other paint is on the letters.</summary>
        private const float Resting = 0.42f;

        /// <summary>A full block — a swatch of the paint itself, drawn by the text engine.</summary>
        /// <remarks>
        /// A full block advances by about one em, so a swatch is <see cref="BlockSize"/> wide and its width is not
        /// settable independently of its height. The slot has to be sized from it, never the other way round.
        /// </remarks>
        private const string Block = "█";

        /// <summary>Point size of a swatch block, and therefore also its width.</summary>
        private const float BlockSize = 87f;

        /// <summary>A slot in multiples of <see cref="BlockSize"/>; the surplus is the air between two swatches.</summary>
        private const float SlotGap = 1.28f;
    }
}
