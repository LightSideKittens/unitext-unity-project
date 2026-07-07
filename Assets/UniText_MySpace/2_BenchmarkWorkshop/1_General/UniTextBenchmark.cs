using System.Collections;
using LightSide;
using UnityEngine;

public class UniTextBenchmark : UGuiTextBenchmarkBase
{
    public override string SystemName => parallelMode ? "UniText (Parallel)" : "UniText";

    bool parallelMode;
    StylePreset richPreset;

    protected override void OnBeforeAllTests()
    {
        UniText.UseParallel = parallelMode;
        GlyphAtlas.forceSingleThreaded = !parallelMode;
        CatZones.MuteAll = true;
        UniTextDebug.Enabled = true;
        PoolStats.ResetAll();
    }

    protected override void OnAfterAllTests()
    {
        UniText.UseParallel = true;
        GlyphAtlas.forceSingleThreaded = false;
        CatZones.MuteAll = false;
        UniTextDebug.Enabled = false;
        if (richPreset != null)
        {
            Destroy(richPreset);
            richPreset = null;
        }
    }

    protected override void OnPhaseComplete(string phaseName)
    {
#if UNITEXT_POOL_DEBUG
        Debug.Log($"[{SystemName}] Counters after {phaseName}:\n{UniTextDebug.GetReport()}");
#endif
        PoolStats.LogAll();
        UniTextDebug.ResetAllCounters();
        PoolStats.ResetAll();
    }

    protected override void OnBeforePhaseIterations(string phaseName) => UniTextDebug.Enabled = true;
    protected override void OnAfterPhaseIterations(string phaseName) => UniTextDebug.Enabled = false;

    protected override Component GetInstanceComponent(GameObject go) => go.GetComponent<UniText>();

    protected override void SetText(Component instance, string text) => ((UniText)instance).Text = text;
    protected override void SetFontSize(Component instance, float size) => ((UniText)instance).FontSize = size;
    protected override void SetColor(Component instance, Color color) => ((UniText)instance).color = color;
    protected override void SetWordWrap(Component instance, bool enabled) => ((UniText)instance).WordWrap = enabled;
    protected override void SetAutoSize(Component instance, bool enabled) => ((UniText)instance).AutoSize = enabled;

    /// <summary>
    /// Rich on = register a runtime preset whose tag rules parse the corpus's markup (the same
    /// &lt;b&gt;/&lt;i&gt;/&lt;u&gt;/&lt;s&gt;/&lt;color&gt;/&lt;size&gt;/&lt;sub&gt;/&lt;sup&gt;/&lt;uppercase&gt;/&lt;lowercase&gt; set TMP and UI Toolkit
    /// parse natively); rich off = remove it. The project's global preset is force-disabled either way,
    /// so markup parses only in the rich phase — fair against TMP/UITK, which likewise start rich off.
    /// </summary>
    protected override void SetRichText(Component instance, bool enabled)
    {
        var text = (UniText)instance;
        text.UseGlobalStylePreset = false;
        if (enabled)
        {
            richPreset ??= BuildRichPreset();
            text.AddStylePreset(richPreset);
        }
        else if (richPreset != null)
        {
            text.RemoveStylePreset(richPreset);
        }
    }

    static StylePreset BuildRichPreset()
    {
        var preset = ScriptableObject.CreateInstance<StylePreset>();
        preset.AddStyle(Style.Tag(new BoldModifier(), "b"));
        preset.AddStyle(Style.Tag(new ItalicModifier(), "i"));
        preset.AddStyle(Style.Tag(new UnderlineModifier(), "u"));
        preset.AddStyle(Style.Tag(new StrikethroughModifier(), "s"));
        preset.AddStyle(Style.Tag(new ColorModifier(), "color"));
        preset.AddStyle(Style.Tag(new SizeModifier(), "size"));
        preset.AddStyle(Style.Tag(new ScriptPositionModifier(), "sup", "super"));
        preset.AddStyle(Style.Tag(new ScriptPositionModifier(), "sub", "sub"));
        preset.AddStyle(Style.Tag(new UppercaseModifier(), "uppercase"));
        preset.AddStyle(Style.Tag(new LowercaseModifier(), "lowercase"));
        return preset;
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
        var test = ObjectUtils.FindAny<UniTextBenchmark>();
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
        var test = ObjectUtils.FindAny<UniTextBenchmark>();
        if (test != null)
            test.RunBenchmarkParallel();
        else
            Debug.LogError("No UniTextBenchmark found in scene.");
    }
#endif
}
