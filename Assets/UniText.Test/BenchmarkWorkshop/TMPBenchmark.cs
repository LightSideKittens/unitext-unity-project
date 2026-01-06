using TMPro;
using UnityEngine;


public class TMPBenchmark : TextBenchmarkBase
{
    public override string SystemName => "TMP";

    protected override void OnBeforeAllTests() { }

    protected override void OnAfterAllTests() { }

    protected override Component CreateInstance(GameObject go)
    {
        return go.GetComponent<TMP_Text>();
    }

    protected override void SetText(Component instance, string text)
    {
        ((TMP_Text)instance).text = text;
    }

    protected override string GetText(Component instance)
    {
        return ((TMP_Text)instance).text;
    }

    protected override void SetFontSize(Component instance, float size)
    {
        ((TMP_Text)instance).fontSize = size;
    }

    protected override void SetColor(Component instance, Color color)
    {
        ((TMP_Text)instance).color = color;
    }

    protected override void SetWordWrap(Component instance, bool enabled)
    {
        ((TMP_Text)instance).enableWordWrapping = enabled;
    }

    protected override void SetAutoSize(Component instance, bool enabled)
    {
        ((TMP_Text)instance).enableAutoSizing = enabled;
    }

    protected override void SetRectSize(Component instance, float width, float height)
    {
        var rt = ((TMP_Text)instance).rectTransform;
        rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
    }

#if UNITY_EDITOR
    [UnityEditor.MenuItem("UniText/Run TMP Benchmark")]
    private static void RunFromMenu()
    {
        var test = FindFirstObjectByType<TMPBenchmark>();
        if (test != null)
            test.RunBenchmark();
        else
            Debug.LogError("No TMPBenchmark found in scene.");
    }
#endif
}
