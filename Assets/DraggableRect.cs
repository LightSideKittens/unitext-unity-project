using UnityEngine;
using UnityEngine.EventSystems;

namespace LightSide
{
    /// <summary>
    /// Drags the attached <see cref="RectTransform"/> with the pointer while preserving the
    /// initial grab offset. Requires a Canvas in the parent chain and an active
    /// <c>GraphicRaycaster</c>; intended for samples and quick prototyping.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class DraggableRect : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private RectTransform rectTransform;
        private Canvas canvas;
        private Vector2 dragOffset;

        private void Awake()
        {
            rectTransform = GetComponent<RectTransform>();
            canvas = GetComponentInParent<Canvas>();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rectTransform.parent as RectTransform,
                eventData.position,
                eventData.pressEventCamera,
                out var localPoint);

            dragOffset = rectTransform.anchoredPosition - localPoint;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    rectTransform.parent as RectTransform,
                    eventData.position,
                    eventData.pressEventCamera,
                    out var localPoint))
            {
                rectTransform.anchoredPosition = localPoint + dragOffset;
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
        }
    }
}