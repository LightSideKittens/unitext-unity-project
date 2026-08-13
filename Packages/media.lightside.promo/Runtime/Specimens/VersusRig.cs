using System.Collections.Generic;
using UnityEngine;

namespace LightSide.Promo
{
    /// <summary>One engine's column, as data.</summary>
    public readonly struct VersusEntry
    {
        public VersusEntry(string title, string verdict, Color verdictFill, bool isProduct = false)
        {
            Title = title;
            Verdict = verdict;
            VerdictFill = verdictFill;
            IsProduct = isProduct;
        }

        public string Title { get; }
        public string Verdict { get; }
        public Color VerdictFill { get; }

        /// <summary>Whether this column gets the dark branded surface rather than neutral paper.</summary>
        public bool IsProduct { get; }
    }

    /// <summary>
    /// Side-by-side surfaces for comparing text engines: a titled column each, and a verdict pill under each.
    /// </summary>
    /// <remarks>
    /// The product column is the dark branded surface and every other column is neutral paper, so a viewer knows
    /// which engine is being sold from the surface alone and never has to read a label to find it.
    /// <para>
    /// Titles and verdicts are supplied when the rig is built and never assigned afterwards. Text written after
    /// construction goes through the runtime source and leaves the serialized field empty, which survives exactly
    /// until the next re-parse and then shows nothing.
    /// </para>
    /// <para>
    /// Build the specimen into <see cref="Column.Body"/>, not into the surface: the surface carries the chrome, and
    /// a specimen parented to it would be laid out over the padding rather than inside it.
    /// </para>
    /// </remarks>
    public sealed class VersusRig
    {
        /// <summary>One engine's built column.</summary>
        public readonly struct Column
        {
            internal Column(Widget surface, RectTransform body, UniText title, Widget verdict, UniText verdictLabel)
            {
                Surface = surface;
                Body = body;
                Title = title;
                Verdict = verdict;
                VerdictLabel = verdictLabel;
            }

            public Widget Surface { get; }

            /// <summary>Where the specimen goes.</summary>
            public RectTransform Body { get; }

            public UniText Title { get; }
            public Widget Verdict { get; }
            public UniText VerdictLabel { get; }
        }

        private readonly Column[] columns;

        internal VersusRig(Column[] columns)
        {
            this.columns = columns;
        }

        public IReadOnlyList<Column> Columns => columns;

        public Column this[int index] => columns[index];

        public int Count => columns.Length;

        /// <summary>Pops a column's verdict in, <paramref name="t"/> seconds after it was called for.</summary>
        public void PoseVerdict(int index, float t)
        {
            var column = columns[index];
            var pop = Spring.Pop.Evaluate(t);
            Stage.Scale(column.Verdict.Rect, Mathf.Lerp(0.85f, 1f, pop));
            Stage.Alpha(column.Verdict.Shape, pop);
            Stage.Alpha(column.VerdictLabel, pop);
        }

        /// <summary>Fades a whole column, chrome and all.</summary>
        public void PoseColumn(int index, float alpha) =>
            Stage.Group(columns[index].Surface.Rect).alpha = Mathf.Clamp01(alpha);
    }
}
