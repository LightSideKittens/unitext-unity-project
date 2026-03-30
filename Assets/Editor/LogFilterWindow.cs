using UnityEditor;
using UnityEngine;

namespace LightSide.Editor
{
    sealed class LogFilterWindow : EditorWindow
    {
        Vector2 scroll;
        

        [MenuItem("Tools/UniText/Log Filter")]
        static void Open()
        {
            var window = GetWindow<LogFilterWindow>("Log Filter");
            window.minSize = new Vector2(250, 200);
        }

        void OnGUI()
        {
            var categories = LogFilter.KnownCategories;

            // toolbar
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button("Show All", EditorStyles.toolbarButton))
                LogFilter.EnableAll();
            if (GUILayout.Button("Hide All", EditorStyles.toolbarButton))
                LogFilter.SuppressAll();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Reset Counts", EditorStyles.toolbarButton))
                LogFilter.ResetCounts();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);
            EnsureStyles();

            int suppressedCount = 0;
            foreach (var cat in categories)
                if (LogFilter.IsSuppressed(cat))
                    suppressedCount++;

            if (suppressedCount > 0)
                EditorGUILayout.HelpBox($"{suppressedCount} categories hidden — their logs won't reach Console or cat.log", MessageType.Info);

            // category list
            scroll = EditorGUILayout.BeginScrollView(scroll);

            for (int i = 0; i < categories.Count; i++)
            {
                var cat = categories[i];
                bool suppressed = LogFilter.IsSuppressed(cat);
                int count = LogFilter.GetCount(cat);

                EditorGUILayout.BeginHorizontal();

                bool shown = EditorGUILayout.ToggleLeft(cat, !suppressed);
                if (shown == suppressed) // toggled
                    LogFilter.SetSuppressed(cat, !shown);

                if (count > 0)
                {
                    var style = suppressed ? dimCountStyle : countStyle;
                    GUILayout.Label(count.ToString(), style, GUILayout.ExpandWidth(false));
                }

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();

            // auto-repaint to update counts
            if (Event.current.type == EventType.Repaint && focusedWindow == this)
                Repaint();
        }

        static GUIStyle countStyle;
        static GUIStyle dimCountStyle;

        void OnEnable()
        {
            countStyle = null;
            dimCountStyle = null;
        }

        void EnsureStyles()
        {
            if (countStyle != null) return;
            countStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = new Color(0.6f, 0.6f, 0.6f) }
            };
            dimCountStyle = new GUIStyle(countStyle)
            {
                normal = { textColor = new Color(0.4f, 0.4f, 0.4f) }
            };
        }

        void OnInspectorUpdate() => Repaint();
    }
}
