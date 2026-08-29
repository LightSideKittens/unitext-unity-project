using UnityEngine;

namespace LightSide.Promo
{
    /// <summary>Choreography of <see cref="ShowreelScene"/> — every phase posed off one clock.</summary>
    public sealed partial class ShowreelScene
    {
        protected override void OnRender(float local)
        {
            PoseButton(local);
            PoseCard(local);
            PoseStyles(local);
            PosePayload(local);
            PoseWord(local);
            PoseFinale(local);
            pointer.Pose(local - Aim);
        }

        private void PoseButton(float local)
        {
            var land = Motion.Punch.Window(local, Land, 0.6f);
            var hit = Mathf.Clamp01((local - press) / 0.3f);
            var squash = hit <= 0f || hit >= 1f ? 0f : Mathf.Sin(hit * Mathf.PI);
            var gone = Motion.Whip.Window(local, morph, 0.38f);

            buttonGroup.alpha = Mathf.Clamp01(land * 2f) * (1f - gone);
            Stage.Scale(button.Rect,
                land * (1f + squash * 0.12f + gone * 0.9f),
                land * (1f - squash * 0.16f + gone * 0.9f));

            var blast = Mathf.Clamp01((local - press) / 0.7f);
            var wave = Motion.Snap.Evaluate(blast);
            burst.Rect.gameObject.SetActive(blast > 0f && blast < 1f);
            Stage.Scale(burst.Rect, Mathf.LerpUnclamped(1f, 2.6f, wave));
            Stage.Alpha(burst.Shape, 1f - wave);
        }

        /// <summary>
        /// The card: born from the button, moved aside for the typing beat, and then held for the rest of the film.
        /// </summary>
        private void PoseCard(float local)
        {
            card.Pose(local - morph);

            var birth = Mathf.Clamp01(Spring.Pop.Evaluate(local - morph));
            var moved = Motion.Snap.Window(local, aside, 0.6f);
            var width = world.rect.width;

            card.Rect.anchoredPosition = new Vector2(Mathf.LerpUnclamped(0f, width * 0.25f, moved), 0f);
            Stage.Scale(card.Rect,
                Mathf.LerpUnclamped(0.42f, 1f, birth) * Mathf.LerpUnclamped(1f, 0.76f, moved));

            PoseFontFocus(local);
        }

        /// <summary>
        /// The no-font beat: everything but the Font row dims, the row swells on its own plate, and the line below
        /// shouts it — all undone before the card moves aside.
        /// </summary>
        private void PoseFontFocus(float local)
        {
            var release = Mathf.Clamp01((local - (aside - 0.4f)) / 0.35f);
            var pick = Mathf.Clamp01(Spring.Bouncy.Evaluate(local - focus)) * (1f - release);
            var pulse = 0.5f + 0.5f * Mathf.Cos(Mathf.Max(0f, local - focus) * Mathf.PI * 1.6f);

            for (var i = 0; i < card.Count; i++)
            {
                if (i == FontRow) continue;
                var dim = Mathf.LerpUnclamped(1f, 0.25f, pick);
                Stage.Alpha(card[i].Label, dim);
                Stage.Alpha(card[i].Well.Shape, dim);
                if (card[i].Value) Stage.Alpha(card[i].Value, dim);
            }

            Stage.Alpha(card.Title, Mathf.LerpUnclamped(1f, 0.35f, pick));
            Stage.Alpha(card.Icon.Shape, Mathf.LerpUnclamped(1f, 0.35f, pick));
            Stage.Scale(card[FontRow].Rect, Mathf.LerpUnclamped(1f, 1.13f, pick));

            Stage.Alpha(haloPlate.Shape, pick);
            Stage.Alpha(halo.Shape, pick * Mathf.LerpUnclamped(0.4f, 1f, pulse));
            if (pick > 0f) halo.Outline.Radius = Mathf.LerpUnclamped(24f, 40f, pulse);

            var shoutIn = Motion.Punch.Window(local, focus + 0.5f, 0.5f);
            var shoutOut = Motion.Whip.Window(local, aside - 0.45f, 0.35f);
            var baseY = -world.rect.height * 0.395f;

            Stage.Alpha(shout, Mathf.Clamp01(shoutIn * 1.6f) * (1f - shoutOut));
            Stage.Scale(shout.rectTransform, Mathf.LerpUnclamped(0.7f, 1f, shoutIn));
            shout.rectTransform.anchoredPosition = new Vector2(0f, baseY - 70f * shoutOut);
        }

        /// <summary>
        /// The Styles list, kept in step with the specimens.
        /// </summary>
        /// <remarks>
        /// A paint row arrives on the same beat as the layer it names lands on the word, and leaves with the word;
        /// the reveal row arrives for the finale and its picker names whichever line is landing. The list is the
        /// reel's only claim that these are modifiers rather than video effects, so it may never run ahead of, or
        /// behind, what it describes.
        /// </remarks>
        private void PoseStyles(float local)
        {
            var cleared = 1f - Motion.Snap.Window(local, exitWord, 0.45f);

            for (var i = 0; i < PaintCount; i++)
                styles.Pose(i, Motion.Back.Window(local, strikes + i * StrikeStep, 0.45f) * cleared);

            var arrived = Motion.Back.Window(local, finale, 0.5f);
            styles.Pose(RevealRow, arrived);
            if (arrived > 0f) styles[RevealRow].Pick.SetText(RevealNames[Landing(local)]);
        }

        /// <summary>Which finale line is the newest to have started arriving.</summary>
        private int Landing(float local)
        {
            var index = 0;
            for (var i = 1; i < lines.Length; i++)
                if (local >= finale + FinaleLead + i * FinaleStep - PickLead)
                    index = i;

            return index;
        }

        private void PosePayload(float local)
        {
            var entered = Motion.Back.Window(local, aside + 0.1f, 0.6f);
            var gone = Motion.Whip.Window(local, wipe, 0.5f);
            var width = world.rect.width;

            payload.anchoredPosition = new Vector2(
                Mathf.LerpUnclamped(-width * 0.55f, 0f, entered) - width * 0.7f * gone, 0f);

            var poured = Motion.Meter.Window(local, pour, PourFor);
            wallReveal.Fill = poured;
            fieldReveal.Fill = Mathf.Clamp01(poured * 1.15f);
            scroll.anchoredPosition = new Vector2(0f, scrollBy * poured);

            for (var i = 0; i < bars.Count; i++)
            {
                bars.Pose(i, entered);
                Stage.Progress(bars[i].Fill, bars[i].Fraction * poured);
            }

            bars[0].Value.SetText(Read(uniMiB * poured));
            bars[1].Value.SetText(Read(tmpMiB * poured));
        }

        private void PoseWord(float local)
        {
            wordReveal.Fill = GlyphReveal.Frontier.Window(local, slam, 0.45f);

            fill.Tint = Color32.Lerp(ClearTint, FullTint, Strike(local, 0));
            inner.Width = UnitValue.Em(0.07f * Strike(local, 1));
            middle.Width = UnitValue.Em(0.16f * Strike(local, 2));
            outer.Width = UnitValue.Em(0.27f * Strike(local, 3));

            var settle = Strike(local, 4);
            shadow.Blur = UnitValue.Em(0.11f * settle);
            shadow.Spread = UnitValue.Em(0.24f * settle);
            shadow.Offset = new UnitVector2(new Vector2(0f, -0.08f * settle), UnitKind.Em);
            glow.Radius = UnitValue.Em(0.4f * Strike(local, 5));

            var knock = Knock(local);
            var gone = Motion.Whip.Window(local, exitWord, 0.4f);

            word.rectTransform.anchoredPosition = new Vector2(
                -world.rect.width * 0.24f, -world.rect.height * 0.9f * gone);
            Stage.Scale(word.rectTransform,
                (1f + knock * 0.05f) * (1f - 0.35f * gone),
                (1f - knock * 0.07f) * (1f - 0.35f * gone));
            Stage.Alpha(word, 1f - gone);
            Stage.Scale(world, 1f + knock * 0.012f);
        }

        /// <summary>How far layer <paramref name="index"/> has arrived, on a spring so it lands with a blow.</summary>
        private float Strike(float local, int index) =>
            Mathf.Clamp01(Spring.Bouncy.Evaluate(local - (strikes + index * StrikeStep)));

        /// <summary>
        /// The recoil of every nearby strike, summed.
        /// </summary>
        /// <remarks>
        /// Summed rather than taken from the newest alone: the strikes land closer together than the recoil is long,
        /// so the word is still shaking off one blow when the next arrives — which is what makes six strikes read as
        /// a beating rather than as six pictures.
        /// </remarks>
        private float Knock(float local)
        {
            var total = 0f;
            for (var i = 0; i < PaintCount; i++)
            {
                var t = (local - (strikes + i * StrikeStep)) / 0.5f;
                if (t > 0f && t < 1f) total += Mathf.Sin(t * Mathf.PI) * (1f - t);
            }

            return Mathf.Clamp01(total);
        }

        private void PoseFinale(float local)
        {
            for (var i = 0; i < arrivals.Length; i++)
                arrivals[i].Fill = GlyphReveal.Frontier.Window(
                    local, finale + FinaleLead + i * FinaleStep, FinaleArrive);
        }

        /// <summary>When the button lands, opening the reel.</summary>
        private const float Land = 0.12f;

        /// <summary>How far ahead of a line's arrival the picker switches to its name.</summary>
        private const float PickLead = 0.18f;
    }
}
