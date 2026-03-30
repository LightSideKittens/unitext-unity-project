using System.Collections;
using LightSide;
using UnityEngine;

public class UniTextBenchmark : TextBenchmarkBase
{
    public override string SystemName => parallelMode ? "UniText (Parallel)" : "UniText";

    bool parallelMode;

    protected override void OnBeforeAllTests()
    {
        UniText.UseParallel = parallelMode;
        UniTextDebug.Enabled = true;
        UniTextPoolStats.ResetAll();
    }
    

    protected override void OnAfterAllTests()
    {
        UniText.UseParallel = true;
        UniTextDebug.Enabled = false;
    }

    protected override void OnPhaseComplete(string phaseName)
    {
        if (UniTextDebug.Pool_CumulativeRents > 0)
        {
            Debug.Log($"[{SystemName}] Pool after {phaseName}:\n{UniTextDebug.GetReport()}");
            UniTextPoolStats.LogAll();
        }
        UniTextDebug.ResetAllCounters();
        UniTextPoolStats.ResetAll();
    }

    protected override void OnBeforePhaseIterations(string phaseName)
    {
        UniTextDebug.Enabled = true;
    }

    protected override void OnAfterPhaseIterations(string phaseName)
    {
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

    [ContextMenu("Run Benchmark (Single-Threaded)")]
    public new void RunBenchmark()
    {
        parallelMode = false;
        base.RunBenchmark();
    }

    [ContextMenu("Run Benchmark (Parallel)")]
    public void RunBenchmarkParallel()
    {
        parallelMode = true;
        base.RunBenchmark();
    }

    public IEnumerator RunBenchmarkCoroutine(bool silent, bool parallel)
    {
        parallelMode = parallel;
        yield return RunBenchmarkCoroutine(silent);
    }

#if UNITY_EDITOR
    [UnityEditor.MenuItem("UniText/Run UniText Benchmark (Single-Threaded)")]
    private static void RunFromMenu()
    {
        var test = FindObjectOfType<UniTextBenchmark>();
        if (test != null)
        {
            test.parallelMode = false;
            test.RunBenchmark();
        }
        else
            Debug.LogError("No UniTextBenchmark found in scene.");
    }

    [UnityEditor.MenuItem("UniText/Run UniText Benchmark (Parallel)")]
    private static void RunFromMenuParallel()
    {
        var test = FindObjectOfType<UniTextBenchmark>();
        if (test != null)
            test.RunBenchmarkParallel();
        else
            Debug.LogError("No UniTextBenchmark found in scene.");
    }
#endif
}
