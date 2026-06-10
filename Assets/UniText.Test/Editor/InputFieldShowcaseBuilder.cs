#if UNITY_EDITOR
using System;
using LightSide;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LightSideEditor
{
    /// <summary>
    /// Builds (idempotently) one <see cref="UniTextInputField"/> per supported configuration,
    /// each grouped under a labelled row, into the active scene for visual inspection.
    /// Menu: UniText ▸ Build Input Field Showcase.
    /// </summary>
    public static class InputFieldShowcaseBuilder
    {
        private const string FontStackPath = "Assets/UniText/Defaults/UniTextFonts_Default.asset";
        private const string RootName = "Input Field Showcase";

        private const float LabelWidth = 320f;
        private const float FieldWidth = 360f;
        private const float RowHeight = 44f;
        private const float Gap = 12f;
        private const float RowStep = 56f;

        [MenuItem("UniText/Build Input Field Showcase")]
        public static void Build()
        {
            var stack = AssetDatabase.LoadAssetAtPath<UniTextFontStack>(FontStackPath);
            if (stack == null)
            {
                Debug.LogError("[UniText] Default font stack not found: " + FontStackPath);
                return;
            }

            var scene = SceneManager.GetActiveScene();
            foreach (var existing in scene.GetRootGameObjects())
                if (existing.name == RootName)
                    UnityEngine.Object.DestroyImmediate(existing);

            if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() == null)
                new GameObject("EventSystem", typeof(EventSystem));

            var rootGo = new GameObject(RootName, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            rootGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var root = rootGo.transform;

            var y = -40f;
            AddRow(root, stack, "Single-line", ref y, null, null, "Type here…");
            AddRow(root, stack, "Prefilled + placeholder", ref y, null, "Prefilled value", "Hint when empty");
            AddRow(root, stack, "Multi-line", ref y, null, null, "Line 1\nLine 2", singleLine: false);
            AddRow(root, stack, "Password", ref y, (f, e) => e.AddBehavior(new PasswordBehavior()), "secret", null);
            AddRow(root, stack, "Password (star mask)", ref y, (f, e) => e.AddBehavior(new PasswordBehavior { MaskChar = "*" }), "secret", null);
            AddRow(root, stack, "Read-only", ref y, (f, e) => e.ReadOnly = true, "Read-only content", null);
            AddRow(root, stack, "Character limit 10", ref y, (f, e) => e.AddBehavior(new CharacterLimitBehavior { Limit = 10 }), null, "Max 10 chars");
            AddRow(root, stack, "Integer validator", ref y, (f, e) => e.AddBehavior(new IntegerValidator()), null, "Digits only");
            AddRow(root, stack, "Decimal validator", ref y, (f, e) => e.AddBehavior(new DecimalValidator()), null, "e.g. 3.14");
            AddRow(root, stack, "Email validator", ref y, (f, e) => e.AddBehavior(new EmailValidator()), null, "you@site.com");
            AddRow(root, stack, "RTL / BiDi", ref y, null, "abc" + (char)0x05D0 + (char)0x05D1 + (char)0x05D2, null);
            AddRow(root, stack, "Emoji", ref y, null, "Hi " + (char)0xD83D + (char)0xDE00, null);
            AddRow(root, stack, "Multi-line + Tab inserts tab", ref y, (f, e) => e.AddBehavior(new TabKeyBehavior()), null, "Press Tab", singleLine: false);

            Selection.activeObject = rootGo;
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[UniText] '" + RootName + "' rebuilt in scene '" + scene.name + "'. Save the scene to keep it.");
        }

        private static void AddRow(Transform parent, UniTextFontStack stack, string label, ref float y,
            Action<UniTextInputField, UniTextEditable> configure, string presetText, string placeholderText,
            bool singleLine = true)
        {
            var rowGo = new GameObject(label, typeof(RectTransform));
            rowGo.transform.SetParent(parent, false);
            Place((RectTransform)rowGo.transform, 20f, y, LabelWidth + Gap + FieldWidth, RowHeight);

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(rowGo.transform, false);
            Place((RectTransform)labelGo.transform, 0f, 0f, LabelWidth, RowHeight);
            var labelText = labelGo.AddComponent<UniText>();
            labelText.FontStack = stack;
            labelText.Text = label;

            var fieldGo = new GameObject("Field", typeof(RectTransform), typeof(RectMask2D), typeof(Image));
            fieldGo.SetActive(false);
            fieldGo.transform.SetParent(rowGo.transform, false);
            Place((RectTransform)fieldGo.transform, LabelWidth + Gap, 0f, FieldWidth, RowHeight);
            fieldGo.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.08f);
            var field = fieldGo.AddComponent<UniTextInputField>();

            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(fieldGo.transform, false);
            Stretch((RectTransform)textGo.transform, 6f, 4f);
            var uniText = textGo.AddComponent<UniText>();
            uniText.FontStack = stack;
            var editable = textGo.AddComponent<UniTextEditable>();
            uniText.Text = presetText ?? string.Empty;
            if (singleLine)
            {
                uniText.WordWrap = false;
                editable.AddBehavior(new SingleLineBehavior());
            }

            UniText placeholder = null;
            if (!string.IsNullOrEmpty(placeholderText))
            {
                var phGo = new GameObject("Placeholder", typeof(RectTransform));
                phGo.transform.SetParent(fieldGo.transform, false);
                Stretch((RectTransform)phGo.transform, 6f, 4f);
                placeholder = phGo.AddComponent<UniText>();
                placeholder.FontStack = stack;
                placeholder.Text = placeholderText;
                placeholder.color = new Color(1f, 1f, 1f, 0.4f);
            }

            var so = new SerializedObject(field);
            so.FindProperty("editor").objectReferenceValue = editable;
            if (placeholder != null) so.FindProperty("placeholder").objectReferenceValue = placeholder;
            so.ApplyModifiedPropertiesWithoutUndo();

            fieldGo.SetActive(true);
            configure?.Invoke(field, editable);
            field.TextComponent.Padding = new (8, 8, 6, 6);

            y -= RowStep;
        }

        private static void Place(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
        }

        private static void Stretch(RectTransform rt, float padX, float padY)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(padX, padY);
            rt.offsetMax = new Vector2(-padX, -padY);
        }
    }
}
#endif
