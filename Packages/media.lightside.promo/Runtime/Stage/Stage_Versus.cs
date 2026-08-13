using UnityEngine;
using HAlign = LightSide.HorizontalAlignment;
using VAlign = LightSide.VerticalAlignment;

namespace LightSide.Promo
{
    /// <summary>Builds the side-by-side comparison rig.</summary>
    public sealed partial class Stage
    {
        /// <summary>
        /// Columns across the frame, one per engine, each with its verdict pill tucked under it.
        /// </summary>
        /// <remarks>
        /// Every measurement derives from the frame, so the rig fills whatever it is given rather than assuming a
        /// size. <paramref name="inset"/> is the margin on all four sides as a fraction of the frame's height.
        /// </remarks>
        public VersusRig Versus(Transform parent, VersusEntry[] entries, float inset = 0.055f, float gap = 0.026f)
        {
            var margin = Height * inset;
            var gutter = Height * gap;
            var chipRow = ChipHeight + gutter;

            var span = (Width - margin * 2f - gutter * (entries.Length - 1)) / entries.Length;
            var top = Half.y - margin;
            var bottom = -Half.y + margin + chipRow;

            var columns = new VersusRig.Column[entries.Length];
            for (var i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                var left = -Half.x + margin + i * (span + gutter);
                var ink = entry.IsProduct ? Theme.Text : Theme.Ink;
                var inkSoft = entry.IsProduct ? Theme.TextSoft : Theme.InkSoft;

                var surface = entry.IsProduct ? Panel("Column" + i, parent) : Card("Column" + i, parent);
                Box(surface.Rect, Centre, new Vector2(0f, 0.5f), new Vector2(left, (top + bottom) * 0.5f),
                    new Vector2(span, top - bottom));

                var title = Label(surface.Rect, entry.Title, Theme.Small, inkSoft, HAlign.Center, VAlign.Middle, false);
                Anchor(title.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                    new Vector2(0f, -Theme.PadLg), new Vector2(-Theme.PadLg * 2f, Theme.Small * 1.8f));

                var body = Node("Body", surface.Rect);
                Stretch(body, Theme.PadLg, Theme.PadLg, Theme.PadLg, Theme.PadLg + Theme.Small * 2.4f);

                var verdict = Chip("Verdict" + i, parent, entry.Verdict, entry.VerdictFill, Color.white, out var label);
                Box(verdict.Rect, Centre, Centre,
                    new Vector2(left + span * 0.5f, bottom - gutter - ChipHeight * 0.5f),
                    new Vector2(ChipWidth(entry.Verdict), ChipHeight));

                columns[i] = new VersusRig.Column(surface, body, title, verdict, label);
            }

            return new VersusRig(columns);
        }

        private static readonly Vector2 Centre = new Vector2(0.5f, 0.5f);
    }
}
