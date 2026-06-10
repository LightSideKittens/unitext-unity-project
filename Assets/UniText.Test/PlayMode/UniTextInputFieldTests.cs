using System.Collections;
using System.Reflection;
using LightSide;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.TestTools;

namespace Tests.PlayMode
{
    public sealed class UniTextInputFieldTests
    {
        private GameObject canvasGo;
        private UniTextInputField field;
        private UniText placeholder;

        private IEnumerator BuildInputField()
        {
            canvasGo = new GameObject("Canvas", typeof(Canvas));
            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

            var fieldGo = new GameObject("Field", typeof(RectTransform), typeof(RectMask2D));
            fieldGo.transform.SetParent(canvasGo.transform, false);
            fieldGo.SetActive(false);
            ((RectTransform)fieldGo.transform).sizeDelta = new Vector2(400f, 60f);
            field = fieldGo.AddComponent<UniTextInputField>();

            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(fieldGo.transform, false);
            var editorText = textGo.AddComponent<UniText>();
            var editable = textGo.AddComponent<UniTextEditable>();

            var phGo = new GameObject("Placeholder", typeof(RectTransform));
            phGo.transform.SetParent(fieldGo.transform, false);
            placeholder = phGo.AddComponent<UniText>();

#if UNITY_EDITOR
            var stack = UnityEditor.AssetDatabase.LoadAssetAtPath<UniTextFontStack>(
                "Assets/UniText/Defaults/UniTextFonts_Default.asset");
            editorText.FontStack = stack;
            placeholder.FontStack = stack;
#endif
            if (editorText.FontStack == null)
                Assert.Ignore("Default font stack not loadable here; the fixture needs a font (run in-editor).");

            SetPrivate(field, "editor", editable);
            SetPrivate(field, "placeholder", placeholder);

            fieldGo.SetActive(true);
            yield return null;
            yield return null;
        }

        private static void SetPrivate(object target, string fieldName, object value)
        {
            var fi = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(fi, $"Field '{fieldName}' not found on {target.GetType().Name} — wrapper fixture needs updating.");
            fi.SetValue(target, value);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (canvasGo != null) Object.DestroyImmediate(canvasGo);
            canvasGo = null;
            field = null;
            placeholder = null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator ForwardsTextToEditor()
        {
            yield return BuildInputField();
            field.Text = "hi";
            Assert.AreEqual("hi", field.Text);
            Assert.AreEqual("hi", field.Editor.Text);
        }

        [UnityTest]
        public IEnumerator Placeholder_ShownWhenEmpty_HiddenWhenTyped()
        {
            yield return BuildInputField();
            Assert.IsTrue(placeholder.gameObject.activeSelf);
            field.Editor.InsertText("x");
            yield return null;
            Assert.IsFalse(placeholder.gameObject.activeSelf);
        }

        [UnityTest]
        public IEnumerator ActivateInputField_FocusesEditor()
        {
            yield return BuildInputField();
            field.ActivateInputField();
            Assert.IsTrue(field.Editor.IsActive);
        }

        [UnityTest]
        public IEnumerator Focused_FiresOnActivate()
        {
            yield return BuildInputField();
            var fired = false;
            field.Focused += () => fired = true;
            field.ActivateInputField();
            Assert.IsTrue(fired);
        }
    }
}
