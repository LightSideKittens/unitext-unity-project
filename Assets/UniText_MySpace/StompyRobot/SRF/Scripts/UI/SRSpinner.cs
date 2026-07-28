namespace SRF.UI
{
    using System;
    using Internal;
    using UnityEngine;
    using UnityEngine.EventSystems;
    using UnityEngine.UI;

    [AddComponentMenu(ComponentMenuPaths.SRSpinner)]
    public class SRSpinner : Selectable, IDragHandler, IBeginDragHandler
    {
        private float _dragDelta;

        public float DragThreshold = 20f;

        public event Action OnSpinIncrement;

        public event Action OnSpinDecrement;

        public void OnBeginDrag(PointerEventData eventData)
        {
            _dragDelta = 0;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!interactable)
            {
                return;
            }

            _dragDelta += eventData.delta.x;

            if (Mathf.Abs(_dragDelta) > DragThreshold)
            {
                var direction = Mathf.Sign(_dragDelta);
                var quantity = Mathf.FloorToInt(Mathf.Abs(_dragDelta)/DragThreshold);

                if (direction > 0)
                {
                    OnIncrement(quantity);
                }
                else
                {
                    OnDecrement(quantity);
                }

                _dragDelta -= quantity*DragThreshold*direction;
            }
        }

        private void OnIncrement(int amount)
        {
            for (var i = 0; i < amount; i++)
            {
                OnSpinIncrement?.Invoke();
            }
        }

        private void OnDecrement(int amount)
        {
            for (var i = 0; i < amount; i++)
            {
                OnSpinDecrement?.Invoke();
            }
        }
    }
}
