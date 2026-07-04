using System.Collections;
using LightSide;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LightSide.Tests
{
    /// <summary>
    /// Code-built PlayMode fixture: Canvas + UniText + default font stack + Selectable + Editable,
    /// activated without the soft keyboard. Call <see cref="Build"/> in [UnitySetUp] and yield
    /// <see cref="Settle"/> after every mutation so the process pass updates analysis state
    /// (grapheme breaks, coordinate map) before assertions — edits are applied synchronously but
    /// analyzed on the per-frame pass.
    /// </summary>
    public class LiveEditableFixture
    {
        private const string DefaultFontsGuid = "72f6e3edfbc82804791ebe8e4814fa46";

        protected GameObject root;
        protected UniTextEditable editable;

        protected void Build()
        {
            root = new GameObject("TestCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

            if (Object.FindObjectOfType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            var textGo = new GameObject("Editable", typeof(RectTransform));
            textGo.transform.SetParent(root.transform, false);
            var rect = (RectTransform)textGo.transform;
            rect.sizeDelta = new Vector2(400f, 100f);

            var text = textGo.AddComponent<UniText>();
#if UNITY_EDITOR
            var path = UnityEditor.AssetDatabase.GUIDToAssetPath(DefaultFontsGuid);
            if (!string.IsNullOrEmpty(path))
                text.FontStack = UnityEditor.AssetDatabase.LoadAssetAtPath<UniTextFontStack>(path);
#endif
            textGo.AddComponent<UniTextSelectable>();
            editable = textGo.AddComponent<UniTextEditable>();
        }

        protected void Teardown()
        {
            if (root != null) Object.Destroy(root);
            var es = Object.FindObjectOfType<EventSystem>();
            if (es != null && es.gameObject.name == "EventSystem") Object.Destroy(es.gameObject);
        }

        protected static IEnumerator Settle(int frames = 2)
        {
            for (var i = 0; i < frames; i++) yield return null;
        }
    }
}
