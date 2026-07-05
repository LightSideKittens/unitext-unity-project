using System.Runtime.InteropServices;
using LightSide.Samples;
using UnityEngine;

namespace LightSide.Samples
{
    /// <summary>
    /// WebGL-specific variant of <see cref="BasicUsageExampleBase"/> that mirrors text changes
    /// to the embedding HTML page (used by the public unity.lightside.media demo).
    /// </summary>
    /// <remarks>
    /// Bridge contract:
    /// <list type="bullet">
    /// <item>JS → Unity: <c>unityInstance.SendMessage(browserBridgeObjectName, "SetDemoText", text)</c> pushes text from the page textarea.</item>
    /// <item>Unity → JS: <c>UniTextDemo_EmitTextChanged(text)</c> (jslib) forwards Unity-originated changes to the page.</item>
    /// </list>
    /// </remarks>
    public class BasicUsageExampleWebGL : BasicUsageExampleBase
    {
        [Header("Browser Bridge")]
        [Tooltip("GameObject name React uses with unityInstance.SendMessage. " +
                 "Must match the value in DemoPage.tsx on the website.")]
        [SerializeField] private string browserBridgeObjectName = "DemoController";

        private string lastSyncedText;

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void UniTextDemo_EmitTextChanged(string text);
#endif

        protected override void OnInit()
        {
            if (!string.IsNullOrEmpty(browserBridgeObjectName) && gameObject.name != browserBridgeObjectName)
                gameObject.name = browserBridgeObjectName;

#if UNITY_WEBGL && !UNITY_EDITOR
            WebGLInput.captureAllKeyboardInput = false;
#endif
        }

        protected override void ApplyText(string text)
        {
            if (demoText == null || text == null) return;

            demoText.Text = text;

            if (text != lastSyncedText)
            {
                lastSyncedText = text;
                PushTextToBrowser(text);
            }
        }

        protected override void ApplySetText(char[] buffer, int offset, int length, string browserPayload)
        {
            if (demoText == null || browserPayload == null) return;

            demoText.SetText(buffer, offset, length);

            if (browserPayload != lastSyncedText)
            {
                lastSyncedText = browserPayload;
                PushTextToBrowser(browserPayload);
            }
        }

        /// <summary>
        /// JS → Unity. Invoked by React via <c>unityInstance.SendMessage(browserBridgeObjectName, "SetDemoText", text)</c>.
        /// </summary>
        public void SetDemoText(string text)
        {
            if (demoText == null || text == null) return;
            if (text == lastSyncedText) return;

            lastSyncedText = text;
            demoText.Text = text;
        }

        private static void PushTextToBrowser(string text)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            UniTextDemo_EmitTextChanged(text);
#endif
        }
    }
}
