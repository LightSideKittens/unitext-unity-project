using UnityEngine;

namespace LightSide.Promo
{
    /// <summary>
    /// The official Unicode conformance suites, each run to completion, beside the total they add up to.
    /// </summary>
    /// <remarks>
    /// Every bar settles full, which is the point: a conformance suite is not a score to be high on, it is a gate that
    /// is either passed or not. A shot that drew these to a scale would invite the viewer to compare four numbers
    /// nobody can compare — the comparison that matters is against every engine that publishes no figure at all.
    /// </remarks>
    public sealed class ConformanceSlide : Slide
    {
        [SerializeField] private string headline = "Correct is not an opinion here.";
        [SerializeField] private string sub = "Every official Unicode suite, run in full.";
        [SerializeField] private string total = "891 757";
        [SerializeField] private string totalCaption = "conformance tests · zero failures";

        private readonly Ease enter = Ease.EmphasizedIn;

        private Claim claim;
        private Metric metric;
        private Meters suites;

        protected override void OnBuild(Stage stage)
        {
            stage.Backdrop(stage.Root);
            claim = stage.Claim(stage.Root, headline, sub);

            metric = stage.Metric("Total", stage.Root, total, totalCaption,
                new Vector2(-stage.Width * 0.26f, stage.ContentCentre), stage.Width * 0.4f,
                stage.ContentHeight * 0.3f);

            suites = stage.Meters("Suites", stage.Root, "Unicode conformance", new[]
            {
                new MeterEntry("UAX #9 · Bidirectional Algorithm", "861 948", 1f, true),
                new MeterEntry("UAX #14 · Line Breaking", "19 338", 1f, true),
                new MeterEntry("UAX #24 · Script Detection", "9 705", 1f, true),
                new MeterEntry("UAX #29 · Grapheme Clusters", "766", 1f, true)
            }, new Vector2(stage.Width * 0.25f, stage.ContentCentre), stage.Width * 0.42f);

            Cue("figure", First);
            for (var i = 0; i < suites.Count; i++) Cue("bar", First + Gap + i * Step);
        }

        protected override void OnRender(float local)
        {
            claim.Pose(local);
            metric.Pose(local - First);

            for (var i = 0; i < suites.Count; i++)
                suites.Pose(i, enter.Window(local, First + Gap + i * Step, BarIn));
        }

        private const float First = 0.4f;
        private const float Gap = 0.5f;
        private const float Step = 0.24f;
        private const float BarIn = 0.6f;
    }
}
