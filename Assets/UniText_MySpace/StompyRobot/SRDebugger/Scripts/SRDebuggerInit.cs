using System;

namespace SRDebugger
{
    using SRF;
    using UnityEngine;

        [AddComponentMenu("")]
    [Obsolete("No longer required, use Automatic initialization mode or call SRDebug.Init() manually.")]
    public class SRDebuggerInit : SRMonoBehaviourEx
    {
        protected override void Awake()
        {
            base.Awake();

            if (!Settings.Instance.IsEnabled)
            {
                return;
            }

            SRDebug.Init();
        }

        protected override void Start()
        {
            base.Start();

            Destroy(CachedGameObject);
        }
    }
}
