using UnityEngine;

namespace LightSide.Promo
{
    /// <summary>
    /// Real sentences in eight writing systems, arriving one after another on the product surface.
    /// </summary>
    /// <remarks>
    /// The strings are joined, bidirectional and conjunct-forming on purpose. A row of isolated codepoints proves
    /// nothing about shaping — it is what a broken engine also produces.
    /// </remarks>
    public sealed class ScriptsSlide : Slide
    {
        [SerializeField] private string[] lines =
        {
            "مرحبا بالعالم",
            "שלום עולם",
            "नमस्ते दुनिया",
            "สวัสดีชาวโลก",
            "こんにちは世界",
            "გამარჯობა მსოფლიო",
            "Բարեւ աշխարհ",
            "안녕하세요 세계"
        };

        [SerializeField] private string caption = "Every writing system on Earth";
        [SerializeField] private float step = 0.22f;

        private readonly Ease reveal = GlyphReveal.Frontier;

        private Widget panel;
        private GlyphReveal[] reveals;
        private Claim footer;

        protected override void OnBuild(Stage stage)
        {
            stage.Backdrop(stage.Root);

            footer = stage.Claim(stage.Root, caption, top: false);

            panel = stage.Panel("Panel", stage.Root);
            stage.Box(panel.Rect, new Vector2(0f, stage.ContentCentre),
                new Vector2(stage.Width * 0.78f, stage.ContentHeight * 0.9f));

            var column = stage.Node("Rows", panel.Rect);
            stage.Stretch(column, stage.Theme.PadXl, stage.Theme.PadXl, stage.Theme.PadXl, stage.Theme.PadXl + 20f);
            stage.Column(column.gameObject, 6f, Stage.Pad(0), TextAnchor.MiddleCenter,
                expandWidth: true, expandHeight: true);

            reveals = new GlyphReveal[lines.Length];
            for (var i = 0; i < lines.Length; i++)
            {
                var cell = stage.Node("Row" + i, column);
                stage.Size(cell.gameObject, preferredHeight: stage.Theme.Head * 1.15f, flexibleHeight: 1f);

                reveals[i] = stage.Reveal(stage.Label(cell, lines[i], stage.Theme.Head, stage.Theme.Text),
                    new FadeRevealHandler());
            }
        }

        protected override void OnRender(float local)
        {
            for (var i = 0; i < reveals.Length; i++)
                reveals[i].Fill = reveal.Window(local, 0.25f + i * step, 0.55f);

            footer.Pose(local - (0.25f + reveals.Length * step));
        }
    }
}
