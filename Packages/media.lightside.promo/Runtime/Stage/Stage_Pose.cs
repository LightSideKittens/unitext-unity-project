using UnityEngine;
using UnityEngine.UI;

namespace LightSide.Promo
{
    /// <summary>Poses a slide already built — the vocabulary of <see cref="Slide.OnRender"/>.</summary>
    public sealed partial class Stage
    {
        /// <summary>Sets one graphic's opacity, leaving its colour alone.</summary>
        /// <remarks>
        /// Reaches that graphic only. A <see cref="Graphic"/>'s colour never propagates to its children, so a panel
        /// faded this way keeps every child it owns at full opacity. To fade a subtree, build it a
        /// <see cref="CanvasGroup"/> with <see cref="Stage.Group"/> and set that instead.
        /// </remarks>
        public static void Alpha(Graphic graphic, float alpha)
        {
            var color = graphic.color;
            color.a = Mathf.Clamp01(alpha);
            graphic.color = color;
        }

        /// <summary>
        /// Drives the fill rect returned by <see cref="Bar"/> to <paramref name="progress"/> of its track.
        /// </summary>
        /// <remarks>
        /// The fill's width is entirely anchor-derived, so this is a real resize: the mesh re-populates on every
        /// call, and under an ancestor layout group a layout pass is queued as well. It is cheap enough to drive
        /// per frame, but it is not free.
        /// </remarks>
        public static void Progress(RectTransform fill, float progress)
        {
            var max = fill.anchorMax;
            max.x = Mathf.Clamp01(progress);
            fill.anchorMax = max;
        }

        /// <summary>Scales <paramref name="rect"/> uniformly in the plane.</summary>
        public static void Scale(RectTransform rect, float scale) =>
            rect.localScale = new Vector3(scale, scale, 1f);
    }
}
