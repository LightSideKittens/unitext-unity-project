using TMPro;
using UnityEngine;

namespace LightSide.Promo
{
    /// <summary>
    /// The same holiday message rendered live by three engines at once: TextMeshPro, TextMeshPro with the community
    /// RTL fixer, and UniText.
    /// </summary>
    /// <remarks>
    /// Assign <see cref="tmpFont"/> to the font asset a developer would actually reach for. Whatever it cannot cover
    /// renders as tofu or as nothing at all, and that is not a flaw in the demo — it is the demonstration.
    /// </remarks>
    public sealed class VersusSlide : Slide
    {
        [SerializeField] private TMP_FontAsset tmpFont;
        [SerializeField, TextArea(6, 12)] private string message = DemoText.Postcard;

        /// <summary>Body size as a fraction of the frame's height, so the columns fill it at any frame.</summary>
        [SerializeField, Range(0.012f, 0.05f)] private float textScale = 0.0305f;

        private readonly Ease reveal = GlyphReveal.Frontier;

        private VersusRig rig;
        private Specimen[] specimens;

        protected override void OnBuild(Stage stage)
        {
            var theme = stage.Theme;
            stage.Backdrop(stage.Root);

            rig = stage.Versus(stage.Root, new[]
            {
                new VersusEntry("TextMeshPro", "BROKEN", theme.Coral),
                new VersusEntry("TextMeshPro + RTL plugin", "PARTIAL", theme.Orange),
                new VersusEntry("UniText", "PERFECT", theme.Violet, isProduct: true)
            });

            var size = stage.Height * textScale;
            specimens = new Specimen[]
            {
                stage.TmpSpecimen(rig[0].Body, tmpFont, size, theme.Ink),
                stage.RtlTmpSpecimen(rig[1].Body, tmpFont, size, theme.Ink),
                stage.UniTextSpecimen(rig[2].Body, size, theme.Text)
            };

            for (var i = 0; i < specimens.Length; i++) specimens[i].SetText(message);

            Cue("writeon", 0f);
            for (var i = 0; i < specimens.Length; i++) Cue("verdict", Verdict + i * Stagger);
        }

        protected override void OnRender(float local)
        {
            var fill = reveal.Window(local, 0f, WriteOn);
            for (var i = 0; i < specimens.Length; i++)
            {
                specimens[i].Reveal(fill);
                rig.PoseVerdict(i, local - Verdict - i * Stagger);
            }
        }

        private const float WriteOn = 2.4f;
        private const float Verdict = 3.6f;
        private const float Stagger = 0.22f;
    }
}
