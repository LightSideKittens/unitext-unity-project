using UnityEngine;

namespace LightSide.Promo
{
    /// <summary>A real editable text field: a focusable, caret-bearing, selectable document on a promo surface.</summary>
    /// <remarks>
    /// Everything here is the shipped component. A slide drives it with the same public verbs an application would —
    /// <c>Activate</c>, <c>Select</c>, <c>Copy</c>, <c>Paste</c> — so what the film shows is the pipeline itself and
    /// not a re-enactment of it.
    /// <para>
    /// Those verbs are mutations, so a slide must route them through a <see cref="Script"/> rather than call them
    /// from <c>OnRender</c>. A selection is the exception: it is idempotent and depends only on where the pointer
    /// is, so it is posed.
    /// </para>
    /// </remarks>
    public readonly struct EditableField
    {
        internal EditableField(Widget surface, UniText text, UniTextEditable editable, StrokeLayer ring,
            float textSize, float inset)
        {
            Surface = surface;
            Text = text;
            Editable = editable;
            Ring = ring;
            TextSize = textSize;
            Inset = inset;
        }

        public Widget Surface { get; }

        public UniText Text { get; }

        public UniTextEditable Editable { get; }

        /// <summary>The focus ring, whose width <see cref="PoseFocus"/> drives.</summary>
        public StrokeLayer Ring { get; }

        public float TextSize { get; }

        /// <summary>Padding between the well's edge and the text.</summary>
        public float Inset { get; }

        public RectTransform Rect => Surface.Rect;

        /// <summary>
        /// What the reader sees, with the serialized markup removed.
        /// </summary>
        /// <remarks>
        /// This, never <c>Editable.Text</c>, is what an index into the content means. <c>Text</c> is the markup
        /// source, so a phrase located in it lands several characters off for every tag ahead of it.
        /// </remarks>
        public string VisibleText => Editable ? Editable.VisibleText : string.Empty;

        /// <summary>
        /// Where the caret sits after <paramref name="prefix"/>, in <paramref name="space"/>'s own coordinates.
        /// </summary>
        /// <remarks>
        /// The prefix is supplied rather than sliced out of the field, and that is the whole point: this answers
        /// correctly on the frame the field was built, while <see cref="CaretPoint"/> cannot. The editable has been
        /// handed its markup but has not parsed it yet, so its visible content is still empty and every index into
        /// it resolves to the start of the line.
        /// </remarks>
        public Vector2 PointAfter(string prefix, RectTransform space)
        {
            var rect = Text.rectTransform;
            var local = new Vector2(
                -rect.rect.width * 0.5f + Stage.Advance(Text, prefix),
                rect.rect.height * 0.5f - TextSize * 0.62f);

            return space.InverseTransformPoint(rect.TransformPoint(local));
        }

        /// <summary>
        /// Where a codepoint sits on the first line, in <paramref name="space"/>'s own coordinates.
        /// </summary>
        /// <remarks>
        /// The offset is <em>measured</em>, not estimated. <see cref="UniTextBase.MeasureText"/> snapshots and
        /// restores the layout, so it answers correctly while the field is still being built — and an average
        /// advance guessed per character drifts by whole words over a sentence, which is exactly far enough to put
        /// a pointer past the phrase it is supposed to be selecting.
        /// <para>
        /// Wrapped text is not accounted for: the point is always on the first line. A field whose content runs
        /// past one line needs real glyph bounds instead.
        /// </para>
        /// </remarks>
        public Vector2 CaretPoint(int codepointIndex, RectTransform space)
        {
            var rect = Text.rectTransform;
            var content = VisibleText;
            var clamped = Mathf.Clamp(codepointIndex, 0, content.Length);
            var prefix = content.Substring(0, clamped);

            var advance = Stage.Advance(Text, prefix);

            var local = new Vector2(
                -rect.rect.width * 0.5f + advance,
                rect.rect.height * 0.5f - TextSize * 0.62f);

            return space.InverseTransformPoint(rect.TransformPoint(local));
        }

        /// <summary>
        /// Where a pointer should aim to land the caret at <paramref name="codepointIndex"/>.
        /// </summary>
        /// <remarks>
        /// Nudged inward from <see cref="CaretPoint"/>, which at index zero is the text's exact top-left corner —
        /// the seam of its own cursor region. Nobody clicks a field on its corner, and aiming there leaves the
        /// pointer straddling the boundary.
        /// </remarks>
        public Vector2 AimPoint(int codepointIndex, RectTransform space) =>
            CaretPoint(codepointIndex, space) + new Vector2(TextSize * 0.3f, 0f);

        /// <summary>Widens the focus ring, <paramref name="t"/> seconds after the field was clicked.</summary>
        public void PoseFocus(float t) =>
            Ring.Width = Mathf.Lerp(RestRing, FocusRing, Spring.Pop.Evaluate(t));

        /// <summary>
        /// Selects from <paramref name="anchor"/> to the codepoint <paramref name="fraction"/> of the way to
        /// <paramref name="head"/>.
        /// </summary>
        public void Sweep(int anchor, int head, float fraction) =>
            Editable.Select(anchor, Mathf.RoundToInt(Mathf.Lerp(anchor, head, Mathf.Clamp01(fraction))));

        private const float RestRing = 3f;
        private const float FocusRing = 7f;
    }
}
