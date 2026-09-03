using UnityEngine;

namespace LightSide.Promo
{
    /// <summary>Choreography of <see cref="ShapesReelScene"/> — every phase posed off one clock.</summary>
    public sealed partial class ShapesReelScene
    {
        protected override void OnRender(float local)
        {
            PoseZoom(local);
            PoseHero(local);
            PoseKind(local);
            PoseLayers(local);
            PosePaint(local);
            PoseBoolean(local);
            PoseBolt(local);
            PoseControls(local);
            PoseApp(local);
            PoseEnd(local);
            PoseClaims(local);
        }

        /// <summary>
        /// The camera: how many times larger than life the bolt is drawn, and where the frame is held while it
        /// grows.
        /// </summary>
        /// <remarks>
        /// A zoom by transform would scale the tangent stream with the shape; the shape is rebuilt larger instead —
        /// the path, the markers on it and every layer's magnitude multiplied by the same factor — which is a
        /// genuine re-evaluation at the new size, and the crisp edge is the field itself at five times the pixels.
        /// </remarks>
        private void PoseZoom(float local)
        {
            var went = Motion.Snap.Window(local, zoomAt, 0.8f);
            var came = Motion.Whip.Window(local, zoomAt + ZoomFor - 0.5f, 0.45f);
            zoomNow = Mathf.LerpUnclamped(1f, ZoomScale, went * (1f - came));
        }

        /// <summary>
        /// The hero: born on a pop, wearing whichever provider its phase is about, held on the zoom's focus while
        /// it is enlarged, and thrown off for the payoff.
        /// </summary>
        private void PoseHero(float local)
        {
            hero.Shape.Shape = ProviderAt(local);

            for (var i = 0; i < ChainCount; i++)
                chainMorphs[i].Progress = Ease.Emphasized.Window(local, MorphAt + i * MorphStep, MorphFor);
            boltMorph.Progress = Ease.Emphasized.Window(local, vectorAt + MorphLag, BoltFor);
            starMorph.Progress = Ease.Emphasized.Window(local, motionAt + MorphLag, StarFor);

            var turned = Ease.Emphasized.Window(local, radiusRun.x, RunFor);
            dial.Radius = radius.At(turned);
            radius.Pose(turned);

            var smoothed = Ease.Emphasized.Window(local, smoothRun.x, RunFor);
            dial.Smoothing = smoothed;
            smooth.Pose(smoothed);

            var grown = Mathf.Clamp(Mathf.FloorToInt((local - pointsAt) / PointStep), 0, PointsMax - PointsMin);
            star.StarPoints = PointsMin + grown;
            star.StarSharpness = Mathf.LerpUnclamped(0.5f, 0.72f, grown / (float)(PointsMax - PointsMin));

            var born = Spring.Pop.Evaluate(local - Land);
            var gone = Motion.Whip.Window(local, finale, 0.45f);
            var size = Mathf.LerpUnclamped(0.35f, 1f, born) * (1f - 0.3f * gone);
            var position = new Vector2(0f, -frameHeight * 0.9f * gone) + zoomFocus * (1f - zoomNow);

            chainBase.Radius = heroSize * RadiusFrom * Mathf.Clamp01(size);
            hero.Rect.sizeDelta = Vector2.one * (heroSize * size);
            hero.Rect.anchoredPosition = position;
            heroGroup.alpha = Mathf.Clamp01((local - Land) * 8f) * (1f - gone);

            glow.Rect.sizeDelta = Vector2.one * (heroSize * GlowSpread * size * zoomNow);
            glow.Rect.anchoredPosition = position;
            Stage.Alpha(glow.Shape, Mathf.Clamp01(born) * (1f - gone));
        }

        /// <summary>
        /// Which provider the hero wears at <paramref name="local"/>: a composite while a morph runs, the plain
        /// outline it ends on otherwise.
        /// </summary>
        private IShapeProvider ProviderAt(float local)
        {
            if (local < chainEnd) return chain;
            if (local < booleanAt) return dial;
            if (local < boolEnd) return boolean;
            if (local < vectorAt + MorphLag) return dial;
            if (local < boltEnd) return toBolt;
            if (local < motionAt + MorphLag) return bolt;
            if (local < starEnd) return toStar;
            return star;
        }

        /// <summary>The pill under the cold open naming whichever outline the hero is becoming.</summary>
        private void PoseKind(float local)
        {
            var newest = -1;
            for (var i = 0; i < ChainCount; i++)
                if (local >= MorphAt + i * MorphStep)
                    newest = i;

            kindLabel.SetText(newest < 0 ? RectName : ChainNames[newest]);
        }

        /// <summary>
        /// The layers, each landing with a blow on its own beat, and grown with the zoom so they keep their
        /// proportion to the shape.
        /// </summary>
        /// <remarks>
        /// The pill naming a layer arrives on the same beat as the layer lands on the hero. It is the reel's only
        /// claim that these are layers rather than video effects, so it may never run ahead of, or behind, what it
        /// names.
        /// </remarks>
        private void PoseLayers(float local)
        {
            var cast = Strike(local, 0);
            shadow.Paint.Color = Theme.Fade(theme.Magenta, ShadowAlpha * Mathf.Clamp01(cast));
            shadow.Blur = ShadowBlur * Mathf.Max(0f, cast) * zoomNow;
            shadow.Spread = ShadowSpread * Mathf.Max(0f, cast) * zoomNow;
            shadow.Offset = new Vector2(0f, -ShadowDrop * Mathf.Max(0f, cast) * zoomNow);

            stroke.Width = StrokeMax * Mathf.Max(0f, Strike(local, 1)) * zoomNow;

            var sunk = Strike(local, 2);
            inner.Paint.Color = new Color(1f, 1f, 1f, GlassAlpha * Mathf.Clamp01(sunk));
            inner.Blur = heroSize * GlassBlur * Mathf.Max(0f, sunk) * zoomNow;
            inner.Offset = new Vector2(0f, -heroSize * GlassDrop * Mathf.Max(0f, sunk) * zoomNow);
        }

        /// <summary>How far layer <paramref name="index"/> has arrived, on a spring so it lands with a blow.</summary>
        private float Strike(float local, int index) => Spring.Bouncy.Evaluate(local - strikes[index]);

        /// <summary>
        /// The fill's projection, snapped on each beat of the paint phase and kept moving in between.
        /// </summary>
        /// <remarks>
        /// The ramp assigned each frame is the same value each frame, which costs nothing; what moves is the
        /// projection — an angle, a zoom, a distance scale — never the stops.
        /// </remarks>
        private void PosePaint(float local)
        {
            var active = 0;
            for (var i = 0; i < paints.Length; i++)
                if (local >= paints[i])
                    active = i + 1;

            var fill = hero.Fill;
            var paint = fill.Paint;
            var spread = active == DistancePaint ? PaintSpread.Repeat : PaintSpread.Clamp;
            if (paint.projection.spread != spread)
            {
                paint.projection.spread = spread;
                fill.Paint = paint;
            }

            fill.FollowShape = active == DistancePaint;
            switch (active)
            {
                case 0:
                    fill.ProjectionKind = PaintProjectionKind.Linear;
                    fill.Gradient = brand;
                    fill.Scale = 1f;
                    fill.Angle = LinearAngle + local * SweepRate;
                    break;

                case 1:
                    fill.ProjectionKind = PaintProjectionKind.Radial;
                    fill.Gradient = brand;
                    fill.Angle = 0f;
                    fill.Scale = 1f / Mathf.LerpUnclamped(0.85f, 1.25f, Breath(local - paints[0], 1.4f));
                    break;

                case 2:
                    fill.ProjectionKind = PaintProjectionKind.Angular;
                    fill.Gradient = seamless;
                    fill.Scale = 1f;
                    fill.Angle = Mathf.Repeat(180f - (local - paints[1]) * SpinRate, 360f);
                    break;

                default:
                    fill.ProjectionKind = PaintProjectionKind.Linear;
                    fill.Gradient = neon;
                    fill.Angle = 0f;
                    fill.Scale = Mathf.LerpUnclamped(NeonPitch, NeonPitch * 0.75f, Breath(local - paints[2], 0.9f));
                    break;
            }

            fillPick.SetText(PaintNames[active]);
        }

        /// <summary>A slow in-and-out, 0 at the start and never resting.</summary>
        private static float Breath(float seconds, float rate) => 0.5f - 0.5f * Mathf.Cos(seconds * Mathf.PI * rate);

        /// <summary>
        /// The combine beat: one pulse of the circle per boolean op — grown out of the hero, held drifting so the op
        /// is seen working, shrunk away — with the op switched only while the circle is gone, so no switch changes the
        /// picture.
        /// </summary>
        /// <remarks>
        /// The circle's size runs through its element's padding. For union, subtract and exclude a vanished circle
        /// folds in nothing and the rectangle stands alone; for intersect a vanished circle would leave nothing, so
        /// that pulse runs the other way — from a circle large enough that the intersection is the rectangle itself,
        /// in to the lens, and back out. Either way both ends of every pulse are the plain rectangle, and so is the
        /// hand-over to the plain provider on either side of the beat. The seam fillet is generous under the union,
        /// where the two read as one liquid, and tight under the cutting ops, where a soft edge would read as a blur
        /// rather than a cut.
        /// </remarks>
        private void PoseBoolean(float local)
        {
            var step = 0;
            for (var i = 0; i < opAt.Length; i++)
                if (local >= opAt[i])
                    step = i;

            var op = OpSequence[step];
            var since = local - opAt[step];
            var envelope = Mathf.Max(0f, Motion.Back.Window(since, 0f, GrowFor)
                                        - Motion.Back.Window(since, PulseFor - ShrinkFor, ShrinkFor));

            var diameter = op == CompositeOp.Intersect
                ? heroSize * Mathf.LerpUnclamped(BiteHuge, BiteSize, envelope)
                : heroSize * BiteSize * envelope;

            bite.Padding = Vector4.one * ((heroSize - diameter) * 0.5f);
            bite.Operation = op;
            bite.Blend = heroSize * (op == CompositeOp.Union ? FilletUnion : FilletCut) * Mathf.Clamp01(envelope);

            var turn = (local - booleanAt) * DriftRate * Mathf.PI * 2f;
            bite.Offset = (BiteRest + new Vector2(DriftX * Mathf.Sin(turn), DriftY * Mathf.Cos(turn))) * heroSize;

            opPick.SetText(OpNames[(int)op]);
        }

        /// <summary>
        /// The bolt's path and the markers on its anchors: one anchor carried across the frame, then the whole
        /// outline grown for the zoom — every knot written from the same place, so the drag and the zoom cannot
        /// disagree about where an anchor is.
        /// </summary>
        /// <remarks>
        /// Every knot is a corner, so its handles sit on its anchor and travel with it; a handle left behind would
        /// turn the corner into a curve.
        /// </remarks>
        private void PoseBolt(float local)
        {
            var draggedBy = Ease.Emphasized.Window(local, knotRun.x, KnotFor);
            var shown = Motion.Back.Window(local, knotAt, 0.45f);
            var gone = Motion.Whip.Window(local, motionAt - 0.15f, 0.3f);
            var held = Motion.Back.Window(local, knotRun.x - 0.25f, 0.3f) *
                       (1f - Motion.Back.Window(local, knotRun.y + 0.2f, 0.3f));

            knotsGroup.alpha = Mathf.Clamp01(shown * 2f) * (1f - gone);

            for (var i = 0; i < knots.Length; i++)
            {
                var authored = i == DragKnot ? Vector2.LerpUnclamped(knotFrom, knotTo, draggedBy) : Bolt[i] * boltScale;
                var point = authored * zoomNow;

                var knot = bolt.Path[i];
                if (knot.position != point)
                {
                    knot.position = point;
                    knot.inHandle = point;
                    knot.outHandle = point;
                    bolt.Path[i] = knot;
                }

                var landed = Motion.Back.Window(local, knotAt + i * 0.05f, 0.4f);
                var grip = i == DragKnot ? 1f + 0.35f * held : 1f;
                knots[i].Rect.anchoredPosition = point;
                knots[i].Rect.sizeDelta = Vector2.one * (KnotSize * zoomNow);
                Stage.Scale(knots[i].Rect, Mathf.LerpUnclamped(0.2f, 1f, landed) * (1f - 0.5f * gone) * grip);
            }
        }

        /// <summary>One control on screen at a time: each pops onto the spot for its beat and drops away for the next.</summary>
        private void PoseControls(float local)
        {
            for (var i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                var came = Motion.Back.Window(local, slot.At, 0.45f);
                var gone = Motion.Whip.Window(local, slot.Until, 0.3f);

                slot.Control.Group.alpha = Mathf.Clamp01(came * 2f) * (1f - gone);
                slot.Control.Rect.anchoredPosition = new Vector2(0f, controlY - ControlDrop * gone);
                Stage.Scale(slot.Control.Rect, Mathf.LerpUnclamped(0.85f, 1f, came));
            }
        }

        /// <summary>One headline pair at a time, each punched in for its phase and thrown off at the next.</summary>
        private void PoseClaims(float local)
        {
            for (var i = 0; i < LineCount; i++)
            {
                var gone = Motion.Whip.Window(local, claimUntil[i], ClaimOut);
                PoseLine(claims[i].Headline, Motion.Punch.Window(local, claimAt[i], ClaimIn), gone);
                if (claims[i].Sub) PoseLine(claims[i].Sub, Motion.Punch.Window(local, claimAt[i] + ClaimLag, ClaimIn), gone);
            }
        }

        private static void PoseLine(UniText line, float came, float gone)
        {
            Stage.Alpha(line, Mathf.Clamp01(came * 1.6f) * (1f - gone));
            Stage.Scale(line.rectTransform, Mathf.LerpUnclamped(0.92f, 1f, came));
        }

        private const float ShadowAlpha = 0.6f;
        private const float ShadowBlur = 44f;
        private const float ShadowSpread = 5f;
        private const float StrokeMax = 12f;

        /// <summary>The glass highlight: a white inner shadow cast upward, so it lies along the top edge and fades down.</summary>
        private const float GlassAlpha = 0.9f;

        /// <summary>Its falloff and its reach down from the edge, as fractions of the hero's size.</summary>
        private const float GlassBlur = 0.1f;

        private const float GlassDrop = 0.09f;

        /// <summary>Degrees per second the linear ramp sweeps while nothing else is happening to the fill.</summary>
        private const float SweepRate = 24f;

        private const float SpinRate = 110f;

        /// <summary>The neon band's depth as a fraction of the half-size; with a repeating spread, the ring pitch.</summary>
        private const float NeonPitch = 0.26f;

        /// <summary>How far a control drops as it leaves the spot.</summary>
        private const float ControlDrop = 40f;

        private const float ClaimIn = 0.45f;
        private const float ClaimOut = 0.35f;
        private const float ClaimLag = 0.18f;
    }
}
