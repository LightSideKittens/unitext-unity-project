using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace LightSide.Promo
{
    /// <summary>Builds the capture rig into the open scene, entirely from code.</summary>
    internal static class PromoSceneCommands
    {
        private const int UiLayer = 5;

        /// <summary>
        /// Builds the capture rig and the demo slides into the open scene, replacing any rig already there.
        /// </summary>
        /// <remarks>
        /// Replaces rather than adopts. A rig left by an earlier version of this command has whatever slides that
        /// version knew about, and silently handing it back looks identical to having created a current one.
        /// <para>
        /// Only a rig this command made is replaced — found by walking up to the name it gives its root. A reel the
        /// author added to their own hierarchy is reported and left alone; deleting the scene root that happens to
        /// be above it would take everything else with it.
        /// </para>
        /// <para>
        /// The canvas is deliberately <c>ConstantPixelSize</c> at scale 1. A scaler that follows the Game View would
        /// compose the film for whatever shape the window happens to be and capture it at another; the reel pins its
        /// own rect to its frame instead, and the camera is sized to show exactly that.
        /// </para>
        /// </remarks>
        [MenuItem(PromoMenu.Tools.CreateReel, false, 10)]
        private static void CreateReel()
        {
            var existing = FindReel();
            if (existing)
            {
                var host = existing.transform;
                while (host && host.name != PromoMenu.ReelObjectName) host = host.parent;

                if (!host)
                {
                    Debug.LogWarning(
                        $"A Reel already exists on '{existing.name}', outside any '{PromoMenu.ReelObjectName}' " +
                        "rig. Remove it before running " + PromoMenu.Tools.CreateReel + ".");
                    return;
                }

                if (!EditorUtility.DisplayDialog("Promo",
                        $"'{host.name}' already exists. Replace it?", "Replace", "Cancel"))
                    return;

                Undo.DestroyObjectImmediate(host.gameObject);
            }

            var root = new GameObject(PromoMenu.ReelObjectName, typeof(RectTransform)) { layer = UiLayer };
            Undo.RegisterCreatedObjectUndo(root, "Create Promo Reel");

            var camera = new GameObject("Promo Camera", typeof(Camera)).GetComponent<Camera>();
            camera.transform.SetParent(root.transform, false);
            camera.transform.localPosition = new Vector3(0f, 0f, -100f);
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.cullingMask = 1 << UiLayer;

            var canvasObject = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster))
            {
                layer = UiLayer
            };
            canvasObject.transform.SetParent(root.transform, false);

            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 100f;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.scaleFactor = 1f;

            var reelObject = new GameObject("Reel", typeof(RectTransform), typeof(Reel)) { layer = UiLayer };
            var reelRect = (RectTransform)reelObject.transform;
            reelRect.SetParent(canvasObject.transform, false);

            var reel = reelObject.GetComponent<Reel>();
            camera.orthographicSize = reel.FrameSize.y * 0.5f;

            AddSlide<ScriptsSlide>(reelRect, "01 Script Wall", 5.5f, new Cut());
            AddSlide<VersusSlide>(reelRect, "02 Versus", 6.5f, new Push());
            AddSlide<ConformanceSlide>(reelRect, "03 Conformance", 4.5f, new Push());
            AddSlide<SystemFontsSlide>(reelRect, "04 System Fonts", 5f, new Lift());
            AddSlide<EmojiSlide>(reelRect, "05 Emoji", 4.5f, new Push());
            AddSlide<AssetChurnSlide>(reelRect, "06 Asset Churn", 5f, new Push());
            AddSlide<BuildSizeSlide>(reelRect, "07 Build Size", 5.5f, new Push());
            var richText = AddSlide<RichTextSlide>(reelRect, "08 Rich Text", 4.5f, new Lift());
            AddSlide<HighlightPaintsSlide>(reelRect, "09 Highlights", 5f, new Push());
            var kit = AddSlide<KitStackSlide>(reelRect, "10 Kit Stack", 8.5f, new Lift());
            var extrude = AddSlide<ExtrudeSlide>(reelRect, "11 Extrude", 5f, new Push());
            AddSlide<LivingTextSlide>(reelRect, "12 Living Text", 5.5f, new Push());
            var variable = AddSlide<VariableFontSlide>(reelRect, "12b Variable Font", 6f, new Push());
            var worldText = AddSlide<WorldTextSlide>(reelRect, "13 World Text", 5.5f, new Push());
            AddSlide<MentionsSlide>(reelRect, "14 Mentions", 5f, new Push());
            AddSlide<SpoilersSlide>(reelRect, "15 Spoilers", 6f, new Push());
            AddSlide<FindSlide>(reelRect, "16 Find", 6f, new Push());
            var copyPaste = AddSlide<FieldCopySlide>(reelRect, "17 Copy Paste", 5f, new Push());
            var inlineMedia = AddSlide<InlineMediaSlide>(reelRect, "18 Inline Media", 5f, new Push());
            AddSlide<InputKitSlide>(reelRect, "19 Input Kit", 7.75f, new Lift());
            var math = AddSlide<MathSlide>(reelRect, "20 Math", 5f, new Push());
            AddSlide<TypographySlide>(reelRect, "21 Typography", 5f, new Push());
            AddSlide<SpeedSlide>(reelRect, "22 Speed", 4.5f, new Push());
            var endCard = AddSlide<TitleSlide>(reelRect, "23 End Card", 3.5f, new CrossFade());

            var links = FindAsset<ModifierGraphPreset>("LinkPreset");
            var display = FindAsset<UniTextFont>("Modak-Regular");
            Wire(richText, "linkPreset", links);
            Wire(copyPaste, "linkPreset", links);
            Wire(kit, "displayFont", display);
            Wire(kit, "paints", FindAsset<UniTextPaints>("DemoPaints"));
            Wire(extrude, "displayFont", display);
            Wire(math, "mathFont", FindAsset<UniTextFont>("STIXTwoMath-Regular"));
            Wire(inlineMedia, "image", FindAsset<Sprite>("cat-sulejman"));
            Wire(worldText, "tmpFont", FindAsset<TMP_FontAsset>("NotoSans-Regular SDF"));
            Wire(variable, "variableFont", FindAsset<UniTextFont>("NotoSans-VariableFont_wdth,wght"));
            Wire(endCard, "logo", FindAsset<Texture2D>("unitext-icon"));
            WireArray(copyPaste, "toolbarIcons", ToolbarIcons);

            reel.Rebuild();

            Selection.activeGameObject = reelObject;
            EditorGUIUtility.PingObject(reelObject);
        }

        /// <summary>
        /// A slide child, sized to the reel. Named with a leading ordinal because a reel plays its children in
        /// sibling order and the hierarchy is the only place that order is visible.
        /// </summary>
        private static T AddSlide<T>(RectTransform parent, string name, float seconds, SlideTransition enter)
            where T : Slide
        {
            var go = new GameObject(name, typeof(RectTransform)) { layer = UiLayer };
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var slide = go.AddComponent<T>();
            slide.Seconds = seconds;
            slide.Enter = enter;
            return slide;
        }

        [MenuItem(PromoMenu.Tools.Rebuild, false, 11)]
        private static void Rebuild()
        {
            var reel = FindReel();
            if (!reel)
            {
                Debug.LogWarning("No Reel in the open scene. Run " + PromoMenu.Tools.CreateReel + " first.");
                return;
            }
            reel.Rebuild();
        }

        /// <summary>
        /// The first non-editor asset of type <typeparamref name="T"/> named <paramref name="name"/>, or null.
        /// </summary>
        /// <remarks>
        /// Found by search rather than by path: the shipped assets live inside another package, whose folder layout
        /// is not this package's to depend on.
        /// <para>
        /// Assets under an <c>Editor</c> folder are skipped. Every art asset the reel uses ships in two copies — one
        /// for the inspector, one for consumers — and a scene that referenced the editor copy would resolve to
        /// nothing outside the editor. The reel is captured in the editor either way, so the failure would surface
        /// only in a build, long after the wiring was believed correct.
        /// </para>
        /// </remarks>
        private static T FindAsset<T>(string name) where T : Object
        {
            var guids = AssetDatabase.FindAssets($"{name} t:{typeof(T).Name}");
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (path.Contains("/Editor/")) continue;

                var asset = AssetDatabase.LoadAssetAtPath<T>(path);
                if (asset && asset.name == name) return asset;
            }
            return null;
        }

        /// <summary>The formatting glyphs a document editor's toolbar carries, in reading order.</summary>
        private static readonly string[] ToolbarIcons =
        {
            "bold", "italic", "underline", "strikethrough", "color", "link"
        };

        /// <summary>Fills a serialized array with the assets named in <paramref name="names"/>, skipping any missing.</summary>
        private static void WireArray(Object target, string field, string[] names)
        {
            var found = new List<Object>(names.Length);
            for (var i = 0; i < names.Length; i++)
            {
                var asset = FindAsset<Texture2D>(names[i]);
                if (asset) found.Add(asset);
                else Debug.LogWarning($"[Promo] No icon named '{names[i]}' for '{target.name}.{field}'.");
            }

            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field);
            if (property == null) return;

            property.arraySize = found.Count;
            for (var i = 0; i < found.Count; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = found[i];

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Assigns <paramref name="value"/> to a serialized field, so a slide's shipped dependencies are wired when
        /// the rig is created rather than hunted for by hand.
        /// </summary>
        private static void Wire(Object target, string field, Object value)
        {
            if (!value)
            {
                Debug.LogWarning($"[Promo] Could not find the asset for '{target.name}.{field}'; assign it by hand.");
                return;
            }

            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(field);
            if (property == null)
            {
                Debug.LogWarning($"[Promo] '{target.GetType().Name}' has no serialized field '{field}'.");
                return;
            }

            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// The reel in the open scene, found by component rather than by adopting whatever canvas happens to exist.
        /// </summary>
        internal static Reel FindReel() => ObjectUtils.FindAny<Reel>(true);
    }
}
