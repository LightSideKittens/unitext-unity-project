using UnityEngine;
using HAlign = LightSide.HorizontalAlignment;
using VAlign = LightSide.VerticalAlignment;

namespace LightSide.Promo
{
    /// <summary>
    /// The rows that drop into a component's Styles list as the shot adds them, and the picker one of them carries.
    /// </summary>
    /// <remarks>
    /// The list is the causal link the reel is built on: every effect the viewer sees on the specimen has a row here
    /// that arrived a beat earlier. Without it a stroke appearing on a word is a video effect; with it, it is a
    /// modifier someone added.
    /// </remarks>
    public sealed class StyleStack
    {
        /// <summary>One entry: its plate, its name, and the picker it may carry.</summary>
        public readonly struct Entry
        {
            internal Entry(RectTransform rect, Widget plate, UniText label, Widget picker, UniText pick)
            {
                Rect = rect;
                Plate = plate;
                Label = label;
                Picker = picker;
                Pick = pick;
            }

            public RectTransform Rect { get; }
            public Widget Plate { get; }
            public UniText Label { get; }

            /// <summary>The dropdown-shaped chip on the right, or a default widget on a row without one.</summary>
            public Widget Picker { get; }

            /// <summary>The picker's current choice, rewritten as the shot cycles it.</summary>
            public UniText Pick { get; }

            public bool HasPicker => Pick;
        }

        private readonly Entry[] entries;

        internal StyleStack(Entry[] entries) => this.entries = entries;

        public Entry this[int index] => entries[index];

        public int Count => entries.Length;

        /// <summary>
        /// Drops entry <paramref name="index"/> in from the right at <paramref name="t"/> of its arrival.
        /// </summary>
        /// <remarks>
        /// From the right and on an overshoot, because the row lands on the same beat as the layer it names lands on
        /// the specimen. Two things arriving together read as one event; the same two eased politely read as a list
        /// updating.
        /// </remarks>
        public void Pose(int index, float t)
        {
            var entry = entries[index];
            var k = Motion.Back.Evaluate(Mathf.Clamp01(t));
            var alpha = Mathf.Clamp01(t * 3f);

            entry.Rect.anchoredPosition = new Vector2(Mathf.LerpUnclamped(Drop, 0f, k),
                entry.Rect.anchoredPosition.y);
            Stage.Scale(entry.Rect, Mathf.LerpUnclamped(0.9f, 1f, k));
            Stage.Alpha(entry.Plate.Shape, alpha);
            Stage.Alpha(entry.Label, alpha);

            if (!entry.HasPicker) return;
            Stage.Alpha(entry.Picker.Shape, alpha);
            Stage.Alpha(entry.Pick, alpha);
        }

        private const float Drop = 160f;
    }

    /// <summary>Builds a <see cref="Promo.StyleStack"/> inside a component field's well.</summary>
    public sealed partial class Stage
    {
        /// <summary>
        /// A stack of style rows filling <paramref name="well"/>, one per entry of <paramref name="names"/>.
        /// </summary>
        /// <remarks>
        /// <paramref name="pickerAt"/> gives one row a dropdown chip; pass a negative index for none. Row height
        /// divides the well rather than being typed, so a stack of three and a stack of six both fill it.
        /// </remarks>
        public StyleStack StyleStack(Transform well, string[] names, float textSize,
            int pickerAt = -1, string pick = null)
        {
            var rows = new StyleStack.Entry[names.Length];
            var slot = 1f / Mathf.Max(1, names.Length);

            for (var i = 0; i < names.Length; i++)
            {
                var rect = Node("Style" + i, well);
                Anchor(rect, new Vector2(0f, 1f - slot * (i + 1)), new Vector2(1f, 1f - slot * i),
                    new Vector2(0.5f, 0.5f), Vector2.zero,
                    new Vector2(-Theme.PadMd * 2f, -Theme.PadXs * 2f));

                var plate = Shape("Plate" + i, rect, ShapeKind.RoundedRect, Theme.RadiusSm);
                Fit(plate.Rect);
                Solid(plate.Fill, Theme.Lift(Theme.Surface, 0.1f));
                AddStroke(plate.Shape, Theme.Line, 2f, -1f);

                var label = Label(rect, names[i], textSize, Theme.Text, HAlign.Left, VAlign.Middle);
                Stretch(label.rectTransform, Theme.PadMd, 0f, Theme.PadMd, 0f);
                label.WordWrap = false;

                rows[i] = i == pickerAt
                    ? WithPicker(rect, plate, label, textSize, pick)
                    : new StyleStack.Entry(rect, plate, label, default, null);
            }

            return new StyleStack(rows);
        }

        private StyleStack.Entry WithPicker(RectTransform rect, Widget plate, UniText label,
            float textSize, string pick)
        {
            var width = textSize * 6.4f;
            var chip = Shape("Picker", rect, ShapeKind.RoundedRect, Theme.RadiusXs);
            Anchor(chip.Rect, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f),
                new Vector2(-Theme.PadMd, 0f), new Vector2(width, -Theme.PadXs * 2f));
            Solid(chip.Fill, Theme.Sink(Theme.Surface, 0.35f));
            AddStroke(chip.Shape, Theme.Violet, 2f, -1f);

            var text = Label(chip.Rect, pick, textSize * 0.92f, Theme.Text, HAlign.Left, VAlign.Middle);
            Stretch(text.rectTransform, Theme.PadSm, 0f, textSize, 0f);
            text.WordWrap = false;

            var caret = Label(chip.Rect, "▾", textSize * 0.92f, Theme.TextSoft,
                HAlign.Right, VAlign.Middle);
            Stretch(caret.rectTransform, 0f, 0f, Theme.PadSm, 0f);
            caret.WordWrap = false;

            label.rectTransform.sizeDelta = new Vector2(-(Theme.PadMd * 2f + width + Theme.PadMd), 0f);
            return new StyleStack.Entry(rect, plate, label, chip, text);
        }
    }
}
