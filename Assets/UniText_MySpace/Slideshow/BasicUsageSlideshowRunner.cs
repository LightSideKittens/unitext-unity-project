#if UNITEXT_SLIDESHOW
using System;
using System.Collections;
using System.IO;
using LightSide;
using LightSide.Samples;
using UnityEngine;

/// <summary>
/// Drives the BasicUsage sample through every slide and captures one screenshot per slide for the
/// CI artifact. Bootstrapped in UNITEXT_SLIDESHOW builds (CIBuildSettings, -ciSlideshow) — the
/// shipped sample scene stays untouched. Reuses the golden-test screenshot and result-delivery
/// channels, so every platform's collection path works unchanged.
/// </summary>
public class BasicUsageSlideshowRunner : MonoBehaviour
{
    private const int settleFrames = 12;

    private BasicUsageExampleBase demo;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void OnRuntimeStart()
    {
        try
        {
            var markerPath = Path.Combine(Application.persistentDataPath, "test_started.txt");
            File.WriteAllText(markerPath, $"Slideshow started at {DateTime.UtcNow}");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[BasicUsageSlideshow] Failed to write start marker: {e.Message}");
        }

        var demo = ObjectUtils.FindAny<BasicUsageExampleBase>();
        if (demo == null)
        {
            Debug.LogError("[BasicUsageSlideshow] No BasicUsageExampleBase found in the loaded scene");
            if (Application.isBatchMode) Application.Quit(1);
            return;
        }

        var runner = new GameObject(nameof(BasicUsageSlideshowRunner)).AddComponent<BasicUsageSlideshowRunner>();
        runner.demo = demo;
    }

    private IEnumerator Start()
    {
        var results = new TestResultCollection();
        var count = demo.ExampleCount;
        Debug.Log($"[BasicUsageSlideshow] Capturing {count} slides");

        for (var i = 0; i < count; i++)
        {
            if (i > 0) demo.NextExample();
            for (var f = 0; f < settleFrames; f++) yield return null;

            var name = $"slide-{i:D2}";
            var start = DateTime.UtcNow;
            TestScreenshot.Capture(name);
            results.Add(new TestResult
            {
                ClassName = "BasicUsageSlideshow",
                MethodName = name,
                Passed = true,
                StartTime = start,
                EndTime = DateTime.UtcNow
            });
        }

        TestScreenshot.Cleanup();
        Debug.Log($"[BasicUsageSlideshow] Captured {count} slides");
        TestRunReporter.Report(results, "[BasicUsageSlideshow]");
    }
}
#endif
