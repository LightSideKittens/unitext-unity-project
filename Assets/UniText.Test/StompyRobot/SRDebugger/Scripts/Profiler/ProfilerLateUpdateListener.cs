namespace SRDebugger.Profiler
{
    using System;
    using UnityEngine;

        public class ProfilerLateUpdateListener : MonoBehaviour
    {
        public Action OnLateUpdate;

        private void LateUpdate()
        {
            if (OnLateUpdate != null)
            {
                OnLateUpdate();
            }
        }
    }
}
