namespace SRF.UI
{
    using Internal;
    using UnityEngine;
    using UnityEngine.UI;

        [RequireComponent(typeof(RectTransform))]
    [ExecuteInEditMode]
    [AddComponentMenu(ComponentMenuPaths.CopySizeIntoLayoutElement)]
    public class CopySizeIntoLayoutElement : LayoutElement
    {
        public RectTransform CopySource;
        public float PaddingHeight;
        public float PaddingWidth;

        public bool SetPreferredSize = false;
        public bool SetMinimumSize = false;

        public override float preferredWidth
        {
            get
            {
                if (!SetPreferredSize || CopySource == null || !IsActive())
                {
                    return -1f;
                }
                return CopySource.rect.width + PaddingWidth;
            }
        }

        public override float preferredHeight
        {
            get
            {
                if (!SetPreferredSize || CopySource == null || !IsActive())
                {
                    return -1f;
                }
                return CopySource.rect.height + PaddingHeight;
            }
        }
        public override float minWidth
        {
            get
            {
                if (!SetMinimumSize || CopySource == null || !IsActive())
                {
                    return -1f;
                }
                return CopySource.rect.width + PaddingWidth;
            }
        }

        public override float minHeight
        {
            get
            {
                if (!SetMinimumSize || CopySource == null || !IsActive())
                {
                    return -1f;
                }
                return CopySource.rect.height + PaddingHeight;
            }
        }

        public override int layoutPriority
        {
            get { return 2; }
        }
    }
}
