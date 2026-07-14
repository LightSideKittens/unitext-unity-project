using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Backbuffer screenshots for benchmark artifacts — one capture after each text appearance, strictly
/// OUTSIDE every measured window (callers invoke it only after all timing/alloc bookkeeping of the
/// pass is read). Captures the real presented frame (<see cref="ScreenCapture"/>), so Screen Space -
/// Overlay canvases are included, and routes the PNG through <see cref="TestScreenshot.Save"/> — the
/// same artifact channel CI already collects (persistentDataPath/Screenshots, WebGL JS bridge, iOS
/// game-loop results; Android benchmark runs bundle the folder into the game-loop archive).
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
        Texture2D texture = null;
        try
        {
            texture = ScreenCapture.CaptureScreenshotAsTexture();
            TestScreenshot.Save($"bench-{ordinal++:D3}-{BenchmarkStreams.Sanitize(name)}", texture.EncodeToPNG());
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[BenchmarkScreenshot] Capture failed: {exception.GetType().Name}: {exception.Message}");
        }
        finally
        {
            if (texture != null) UnityEngine.Object.Destroy(texture);
        }
    }
}
