#if UNITEXT_SLIDESHOW
using System;
using System.Collections;
using System.IO;
using LightSide;
using LightSide.Samples;
using UnityEngine;
using UnityEngine.LowLevel;

/// <summary>
/// Drives the BasicUsage sample through every slide and captures its contents for the CI artifact.
/// Bootstrapped in UNITEXT_SLIDESHOW builds (CIBuildSettings, -ciSlideshow), so the shipped sample
/// scene stays untouched. Reuses the golden-test screenshot and result-delivery channels.
/// </summary>
public class BasicUsageSlideshowRunner : MonoBehaviour
{
    private const int settleFrames = 12;
    private const float pageOverlap = 0.2f;

    private BasicUsageExampleBase demo;
    private RectTransform draggerRect;
    private UniText[] draggableTexts;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void RepublishPlayerLoop()
    {
        var loop = PlayerLoop.GetCurrentPlayerLoop();
        PlayerLoop.SetPlayerLoop(loop);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void OnRuntimeStart()
    {
        Debug.Log("[BasicUsageSlideshow] Runtime initialization started");
        try
        {
            var markerPath = Path.Combine(Application.persistentDataPath, "test_started.txt");
            File.WriteAllText(markerPath, $"Slideshow started at {DateTime.UtcNow}");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[BasicUsageSlideshow] Failed to write start marker: {e.Message}");
        }

        Debug.Log("[BasicUsageSlideshow] Looking for sample controller");
        var demo = ObjectUtils.FindAny<BasicUsageExampleBase>();
        Debug.Log($"[BasicUsageSlideshow] Sample controller lookup completed, found={demo != null}");
        if (demo == null)
        {
            Debug.LogError("[BasicUsageSlideshow] No BasicUsageExampleBase found in the loaded scene");
            if (Application.isBatchMode) Application.Quit(1);
            return;
        }

        Debug.Log("[BasicUsageSlideshow] Creating runner");
        var runner = new GameObject(nameof(BasicUsageSlideshowRunner)).AddComponent<BasicUsageSlideshowRunner>();
        runner.demo = demo;
        Debug.Log("[BasicUsageSlideshow] Runtime initialization completed");
    }

    private IEnumerator Start()
    {
        Debug.Log("[BasicUsageSlideshow] Start entered");
        var dragger = ObjectUtils.FindAny<DraggableRect>();
        Debug.Log($"[BasicUsageSlideshow] Draggable lookup completed, found={dragger != null}");
        draggerRect = dragger.GetComponent<RectTransform>();
        draggableTexts = dragger.GetComponentsInChildren<UniText>(true);

        var results = new TestResultCollection();
        var count = demo.ExampleCount;
        Debug.Log($"[BasicUsageSlideshow] Capturing {count} slides");

        for (var i = 0; i < count; i++)
        {
            if (i > 0) demo.NextExample();
            for (var f = 0; f < settleFrames; f++) yield return null;

            var name = $"slide-{i:D2}";
            var systemFontText = FindSystemFontText();
            if (systemFontText == null)
                Capture(results, name);
            else
                yield return CaptureTallSlide(results, name, systemFontText);
        }

        TestScreenshot.Cleanup();
        Debug.Log($"[BasicUsageSlideshow] Captured {results.Total} screenshots from {count} slides");
        TestRunReporter.Report(results, "[BasicUsageSlideshow]");
    }

    private UniText FindSystemFontText()
    {
        foreach (var text in draggableTexts)
            if (text.Font == null && text.FontStack == null)
                return text;

        return null;
    }

    private IEnumerator CaptureTallSlide(TestResultCollection results, string name, UniText text)
    {
        var originalPosition = draggerRect.anchoredPosition;
        var viewport = draggerRect.parent as RectTransform;
        var pageHeight = viewport.rect.height * (1f - pageOverlap);
        var pageCount = Mathf.Max(2, Mathf.CeilToInt(text.PreferredHeight / pageHeight));

        try
        {
            for (var page = 0; page < pageCount; page++)
            {
                draggerRect.anchoredPosition = originalPosition + Vector2.up * (pageHeight * page);
                if (page > 0) yield return null;

                Capture(results, page == 0 ? name : $"{name}-{page:D2}");
            }
        }
        finally
        {
            draggerRect.anchoredPosition = originalPosition;
        }
    }

    private static void Capture(TestResultCollection results, string name)
    {
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
}
#endif
