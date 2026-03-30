namespace SRF.UI
{
    using Internal;
    using UnityEngine;
    using UnityEngine.UI;

        [RequireComponent(typeof (CanvasScaler))]
    [AddComponentMenu(ComponentMenuPaths.RetinaScaler)]
    public class SRRetinaScaler : SRMonoBehaviour
    {
        [SerializeField] private float _retinaScale = 2f;

        [SerializeField] private int _thresholdDpi = 250;

        [SerializeField] private bool _disablePixelPerfect = false;

                public int ThresholdDpi
        {
            get { return _thresholdDpi; }
        }

        public float RetinaScale
        {
            get { return _retinaScale; }
        }

        private void Start()
        {
            var dpi = Screen.dpi;

            if (dpi <= 0)
            {
                return;
            }

            if (dpi > ThresholdDpi)
            {
                var scaler = GetComponent<CanvasScaler>();

                scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                scaler.scaleFactor = scaler.scaleFactor * RetinaScale;

                if (_disablePixelPerfect)
                {
                    GetComponent<Canvas>().pixelPerfect = false;
                }
            }
        }
    }
}
