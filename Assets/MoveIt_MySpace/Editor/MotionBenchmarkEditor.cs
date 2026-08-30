using LightSide;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>Inspector for the motion-engine comparison rig.</summary>
[CustomEditor(typeof(MotionBenchmark))]
[SingleObjectEditor]
internal sealed class MotionBenchmarkEditor : FullWidthEditor
{
    public override VisualElement CreateInspectorGUI()
    {
        serializedObject.Update();
        var root = InspectorVisuals.CreateRoot();

        var targetsSection = InspectorVisuals.CreateSection("Targets");
        targetsSection.Add(SerializedPropertyField.Create(serializedObject, "sharedTransform"));
        targetsSection.Add(SerializedPropertyField.Create(serializedObject, "distinctTransformPrefab"));
        targetsSection.Add(SerializedPropertyField.Create(serializedObject, "contextRoot"));

        var participants = InspectorVisuals.CreateSection("Participants");
        participants.Add(SerializedPropertyField.Create(serializedObject, "adapters"));

        var workloads = InspectorVisuals.CreateSection("Workloads", collapsible: true);
        workloads.Add(SerializedPropertyField.Create(serializedObject, "workloadFilter"));
        var scale = InspectorVisuals.CreateRow();
        scale.Add(ScaleButton("×½",
            "Halves every workload count; sequence length and measurement settings stay.", 0.5f));
        scale.Add(ScaleButton("×2",
            "Doubles every workload count; sequence length and measurement settings stay.", 2f));
        workloads.Add(scale);
        workloads.Add(SerializedPropertyField.Create(serializedObject, "sharedMotionCount"));
        workloads.Add(SerializedPropertyField.Create(serializedObject, "keyedFloatCount"));
        workloads.Add(SerializedPropertyField.Create(serializedObject, "distinctTransformCount"));
        workloads.Add(SerializedPropertyField.Create(serializedObject, "sequenceCount"));
        workloads.Add(SerializedPropertyField.Create(serializedObject, "sequenceLength"));
        workloads.Add(SerializedPropertyField.Create(serializedObject, "steadyMotionDuration"));

        var measurement = InspectorVisuals.CreateSection("Measurement", collapsible: true);
        var profile = InspectorVisuals.CreateRow();
        profile.Add(PresetButton("Quick",
            "Rough ranking pass: 24 measured frames, no phase detail, 8 creation samples, 1 creation warmup batch.",
            24, false, 8, 1));
        profile.Add(PresetButton("Full",
            "Publication pass: 120 measured frames, phase detail, 32 creation samples, 3 creation warmup batches.",
            120, true, 32, 3));
        measurement.Add(profile);
        measurement.Add(SerializedPropertyField.Create(serializedObject, "warmupFrames"));
        measurement.Add(SerializedPropertyField.Create(serializedObject, "measuredFrames"));
        measurement.Add(SerializedPropertyField.Create(serializedObject, "settleGarbage"));
        measurement.Add(SerializedPropertyField.Create(serializedObject, "measurePhaseDetail"));

        var creation = InspectorVisuals.CreateSection("Creation", collapsible: true);
        creation.Add(SerializedPropertyField.Create(serializedObject, "creationBatchSize"));
        creation.Add(SerializedPropertyField.Create(serializedObject, "creationWarmupBatches"));
        creation.Add(SerializedPropertyField.Create(serializedObject, "creationSamples"));

        var sections = InspectorVisuals.CreateSectionStack();
        sections.Add(targetsSection);
        sections.Add(participants);
        sections.Add(workloads);
        sections.Add(measurement);
        sections.Add(creation);
        root.Add(sections);
        return root;
    }

    Button ScaleButton(string text, string tooltip, float factor)
    {
        var button = new Button(() =>
        {
            serializedObject.Update();
            ScaleCount("sharedMotionCount", factor);
            ScaleCount("keyedFloatCount", factor);
            ScaleCount("distinctTransformCount", factor);
            ScaleCount("sequenceCount", factor);
            ScaleCount("creationBatchSize", factor);
            serializedObject.ApplyModifiedProperties();
        }) { text = text, tooltip = tooltip };
        button.style.flexGrow = 1f;
        return button;
    }

    void ScaleCount(string path, float factor)
    {
        var property = InspectorHelpers.RequireProperty(serializedObject, path);
        property.intValue = Mathf.Max(1, Mathf.RoundToInt(property.intValue * factor));
    }

    Button PresetButton(string text, string tooltip, int measuredFrames, bool measurePhaseDetail,
        int creationSamples, int creationWarmupBatches)
    {
        var button = new Button(() =>
        {
            serializedObject.Update();
            InspectorHelpers.RequireProperty(serializedObject, "measuredFrames").intValue = measuredFrames;
            InspectorHelpers.RequireProperty(serializedObject, "measurePhaseDetail").boolValue = measurePhaseDetail;
            InspectorHelpers.RequireProperty(serializedObject, "creationSamples").intValue = creationSamples;
            InspectorHelpers.RequireProperty(serializedObject, "creationWarmupBatches").intValue =
                creationWarmupBatches;
            serializedObject.ApplyModifiedProperties();
        }) { text = text, tooltip = tooltip };
        button.style.flexGrow = 1f;
        return button;
    }
}
