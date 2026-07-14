using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Screenshots for benchmark artifacts — one capture after each text appearance, strictly
/// OUTSIDE every measured window (callers invoke it only after all timing/alloc bookkeeping of the
/// pass is read). Captures through <see cref="TestScreenshot.Capture"/> — the golden-test camera
/// path and artifact channel CI already collects (persistentDataPath/Screenshots, WebGL JS bridge,
/// iOS game-loop results; Android benchmark runs bundle the folder into the game-loop archive).
/// Enabled by default in players, disabled in the editor; UNITEXT_BENCH_SCREENSHOTS=1/0 overrides both.
/// </summary>
public static class BenchmarkScreenshot
{
    static readonly bool enabled;
    static int ordinal;

    static BenchmarkScreenshot()
    {
        var env = Environment.GetEnvironmentVariable("UNITEXT_BENCH_SCREENSHOTS");
        enabled = env != null ? env != "0" : !Application.isEditor;
    }

    /// <summary>Names are prefixed with a run-wide ordinal so artifacts sort in execution order and repeated passes never overwrite each other.</summary>
    public static IEnumerator Capture(string name)
    {
        if (!enabled || Application.isBatchMode) yield break;
        yield return new WaitForEndOfFrame();
        TestScreenshot.Capture($"bench-{ordinal++:D3}-{BenchmarkStreams.Sanitize(name)}");
    }
}
