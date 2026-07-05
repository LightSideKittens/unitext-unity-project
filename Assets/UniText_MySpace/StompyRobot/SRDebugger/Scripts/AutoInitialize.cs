using SRDebugger.Services;
using SRF.Service;

namespace SRDebugger
{
    using UnityEngine;

    public static class AutoInitialize
    {
                [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void OnLoadBeforeScene()
        {
            // Populate service manager with types from SRDebugger assembly (asmdef)
            SRServiceManager.RegisterAssembly<IDebugService>();

            if (Settings.Instance.IsEnabled)
            {
                // Initialize console if it hasn't already initialized.
                SRServiceManager.GetService<IConsoleService>();
            }
        }

                [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        public static void OnLoad()
        {
            if (Settings.Instance.IsEnabled)
            {
                SRDebug.Init();
            }
        }
    }
}