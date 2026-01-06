using LightSide;
using UnityEngine;

public class UniTextBenchmark : TextBenchmarkBase
{
    public override string SystemName => "UniText";

    protected override void OnBeforeAllTests()
    {
        UniText.UseParallel = false;
        UniTextDebug.Enabled = true;
        UniTextPoolStats.ResetAll();
    }

    protected override void OnAfterAllTests()
    {
        UniText.UseParallel = true;
        UniTextPoolStats.LogAll();
        UniTextDebug.Enabled = false;
    }

    protected override Component CreateInstance(GameObject go)
    {
        return go.GetComponent<UniText>();
    }

    protected override void SetText(Component instance, string text)
    {
        ((UniText)instance).Text = text;
    }

    protected override string GetText(Component instance)
    {
        return ((UniText)instance).Text;
    }

    protected override void SetFontSize(Component instance, float size)
    {
        ((UniText)instance).FontSize = size;
    }

    protected override void SetColor(Component instance, Color color)
    {
        ((UniText)instance).color = color;
    }

    protected override void SetWordWrap(Component instance, bool enabled)
    {
        ((UniText)instance).WordWrap = enabled;
    }

    protected override void SetAutoSize(Component instance, bool enabled)
    {
        ((UniText)instance).AutoSize = enabled;
    }

    protected override void SetRectSize(Component instance, float width, float height)
    {
        var rt = ((UniText)instance).rectTransform;
        rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
    }

#if UNITY_EDITOR
    [UnityEditor.MenuItem("UniText/Run UniText Benchmark")]
    private static void RunFromMenu()
    {
        var test = FindFirstObjectByType<UniTextBenchmark>();
        if (test != null)
            test.RunBenchmark();
        else
            Debug.LogError("No UniTextBenchmark found in scene.");
    }
#endif
}
