using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace LightSide.Promo
{
    /// <summary>Builds the short showreel into the open scene, as a rig of its own.</summary>
    /// <remarks>
    /// A separate rig, not a second mode of the argument reel. The two films answer different questions — one makes
    /// a case to a developer weighing engines, the other shows a stranger in twenty seconds why this is worth a
    /// minute of their attention — and a slide list that has to serve both serves neither.
    /// </remarks>
    internal static class PromoShowreelCommands
    {
        private const int UiLayer = 5;

        [MenuItem(PromoMenu.Tools.CreateShowreel, false, 12)]
        private static void CreateShowreel()
        {
            var existing = GameObject.Find(PromoMenu.ShowreelObjectName);
            if (existing)
            {
                if (!EditorUtility.DisplayDialog("Promo",
                        $"'{existing.name}' already exists. Replace it?", "Replace", "Cancel"))
                    return;

                Undo.DestroyObjectImmediate(existing);
            }

            var root = new GameObject(PromoMenu.ShowreelObjectName, typeof(RectTransform)) { layer = UiLayer };
            Undo.RegisterCreatedObjectUndo(root, "Create Promo Showreel");

            var camera = new GameObject("Showreel Camera", typeof(Camera)).GetComponent<Camera>();
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

            var scene = AddSlide<ShowreelScene>(reelRect, "Showreel", 5f, new Cut());

            Wire(reel, "theme.displayFace", FindAsset<UniTextFont>("FiraSans-ExtraBold"));
            Wire(reel, "theme.bodyFace", FindAsset<UniTextFont>("FiraSans-Medium"));
            Wire(scene, "displayFont", FindAsset<UniTextFont>("Modak-Regular"));

            reel.Rebuild();

            Selection.activeGameObject = reelObject;
            EditorGUIUtility.PingObject(reelObject);
        }

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
    }
}
