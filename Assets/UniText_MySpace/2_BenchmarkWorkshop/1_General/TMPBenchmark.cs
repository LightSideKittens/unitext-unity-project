using LightSide;
using TMPro;
using UnityEngine;

public class TMPBenchmark : UGuiTextBenchmarkBase
{
    public override string SystemName => "TMP";

    protected override void OnBeforeAllTests() { }
    protected override void OnAfterAllTests() { }

    protected override Component GetInstanceComponent(GameObject go) => go.GetComponent<TMP_Text>();

    protected override void SetText(Component instance, string text) => ((TMP_Text)instance).text = text;
    protected override void SetFontSize(Component instance, float size) => ((TMP_Text)instance).fontSize = size;
    protected override void SetColor(Component instance, Color color) => ((TMP_Text)instance).color = color;
    protected override void SetAutoSize(Component instance, bool enabled) => ((TMP_Text)instance).enableAutoSizing = enabled;
    protected override void SetRichText(Component instance, bool enabled) => ((TMP_Text)instance).richText = enabled;

    protected override void SetWordWrap(Component instance, bool enabled)
    {
#if UNITY_2023_1_OR_NEWER
        ((TMP_Text)instance).textWrappingMode = enabled ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
#else
        ((TMP_Text)instance).enableWordWrapping = enabled;
#endif
    }

#if UNITY_EDITOR
    [UnityEditor.MenuItem("UniText/Run TMP Benchmark")]
    private static void RunFromMenu()
    {
        var test = ObjectUtils.FindAny<TMPBenchmark>();
        if (test != null)
            test.RunBenchmark();
        else
            Debug.LogError("No TMPBenchmark found in scene.");
    }
#endif
}
