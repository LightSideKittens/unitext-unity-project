namespace SRDebugger.UI.Other
{
    using System;
    using Controls;
    using SRF;
    using UnityEngine;
    using UnityEngine.Serialization;

    public class SRTab : SRMonoBehaviourEx
    {
                public RectTransform HeaderExtraContent;

        [Obsolete] [HideInInspector] public Sprite Icon;

                public RectTransform IconExtraContent;

        public string IconStyleKey = "Icon_Stompy";
        public int SortIndex;

        [HideInInspector] public SRTabButton TabButton;

        public string Title
        {
            get { return _title; }
        }

        public string LongTitle
        {
            get { return !string.IsNullOrEmpty(_longTitle) ? _longTitle : _title; }
        }

        public string Key
        {
            get { return _key; }
        }
#pragma warning disable 649

        [SerializeField] [FormerlySerializedAs("Title")] private string _title;

        [SerializeField] private string _longTitle;

        [SerializeField] private string _key;

#pragma warning restore 649
    }
}
