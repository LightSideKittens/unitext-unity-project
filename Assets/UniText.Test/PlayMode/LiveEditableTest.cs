using System.Collections;
using LightSide;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.PlayMode
{
    public abstract class LiveEditableTest
    {
        protected GameObject canvasGo;
        protected UniText uniText;
        protected UniTextEditable editable;

#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
        protected const NativeModifiers WordMod = NativeModifiers.Alt;
#else
        protected const NativeModifiers WordMod = NativeModifiers.Ctrl;
#endif

        protected IEnumerator BuildField(bool singleLine = true)
        {
            canvasGo = new GameObject("Canvas", typeof(Canvas));
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

            var go = new GameObject("Field", typeof(RectTransform));
            go.transform.SetParent(canvasGo.transform, false);
            var rt = (RectTransform)go.transform;
            if (singleLine)
            {
                rt.sizeDelta = new Vector2(400f, 60f);
            }
            else
            {
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
            }

            uniText = go.AddComponent<UniText>();
#if UNITY_EDITOR
            uniText.FontStack = UnityEditor.AssetDatabase.LoadAssetAtPath<UniTextFontStack>(
                "Assets/UniText/Defaults/UniTextFonts_Default.asset");
#endif
            if (uniText.FontStack == null)
                Assert.Ignore("Default font stack not loadable here; the fixture needs a font (run in-editor).");

            uniText.Text = string.Empty;

            editable = go.AddComponent<UniTextEditable>();
            if (singleLine)
            {
                uniText.WordWrap = false;
                editable.AddBehavior(new SingleLineBehavior());
            }
            yield return null;
            editable.Activate(showKeyboard: false);
            yield return null;
            yield return null;
        }

        protected IEnumerator Seed(string text)
        {
            editable.InsertText(text);
            yield return null;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (canvasGo != null) UnityEngine.Object.DestroyImmediate(canvasGo);
            canvasGo = null;
            uniText = null;
            editable = null;
            yield return null;
        }
    }
}
