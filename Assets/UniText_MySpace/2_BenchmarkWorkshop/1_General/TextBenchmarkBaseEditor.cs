#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(TextBenchmarkBase), true)]
[CanEditMultipleObjects]
public class TextBenchmarkBaseEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Benchmark Inspection", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUI.DisabledScope(!Application.isPlaying || AnyRunning()))
            {
                if (GUILayout.Button("Inspect Selected Phase"))
                    ForEachBenchmark(b => b.InspectSelectedPhase());
            }

            using (new EditorGUI.DisabledScope(AnyRunning()))
            {
                if (GUILayout.Button("Clear Inspection"))
                    ForEachBenchmark(b => b.ClearInspection());
            }
        }
    }

    bool AnyRunning()
    {
        foreach (var targetObject in targets)
        {
            var benchmark = targetObject as TextBenchmarkBase;
            if (benchmark != null && benchmark.IsRunning)
                return true;
        }

        return false;
    }

    void ForEachBenchmark(Action<TextBenchmarkBase> action)
    {
        foreach (var targetObject in targets)
        {
            var benchmark = targetObject as TextBenchmarkBase;
            if (benchmark != null)
                action(benchmark);
        }
    }
}
#endif
