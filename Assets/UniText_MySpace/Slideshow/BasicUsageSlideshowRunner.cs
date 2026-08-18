#if UNITEXT_SLIDESHOW
using System;
using System.Collections;
using System.IO;
using LightSide;
using LightSide.Samples;
using UnityEngine;
#if !UNITY_WEBGL
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
#endif

/// <summary>
/// Drives the BasicUsage sample through every slide and captures its contents for the CI artifact.
/// Bootstrapped in UNITEXT_SLIDESHOW builds (CIBuildSettings, -ciSlideshow), so the shipped sample
/// scene stays untouched. Reuses the golden-test screenshot and result-delivery channels.
/// </summary>
public class BasicUsageSlideshowRunner : MonoBehaviour
{
    private const int settleFrames = 12;
    private const float pageOverlap = 0.2f;
    private const float keyboardTimeout = 10f;
    private const float stateDwell = 10f;

    private BasicUsageExampleWebGL demo;
    private RectTransform draggerRect;
    private UniText[] draggableTexts;

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

        var demo = ObjectUtils.FindAny<BasicUsageExampleWebGL>();
        if (demo == null)
        {
            Debug.LogError("[BasicUsageSlideshow] No BasicUsageExampleWebGL found in the loaded scene");
            if (Application.isBatchMode) Application.Quit(1);
            return;
        }

        var runner = new GameObject(nameof(BasicUsageSlideshowRunner)).AddComponent<BasicUsageSlideshowRunner>();
        runner.demo = demo;
    }

    private IEnumerator Start()
    {
        var dragger = ObjectUtils.FindAny<DraggableRect>();
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

        if (Application.isMobilePlatform)
            yield return CaptureKeyboardStates(results, count);

#if !UNITY_WEBGL
        yield return CaptureAddressablePrefab(results, count + (Application.isMobilePlatform ? 2 : 0));
        if (Application.isMobilePlatform)
            yield return RestoreKeyboardRecordingTail();
#endif

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

    /// <summary>Captures the editable screen with the soft keyboard raised in portrait and landscape orientations.</summary>
    private IEnumerator CaptureKeyboardStates(TestResultCollection results, int slideCount)
    {
        var field = demo.KeyboardCaptureField;
        if (field == null)
        {
            Debug.LogError("[BasicUsageSlideshow] Keyboard Capture Field is not assigned on the demo component");
            Record(results, "keyboard-capture-field", DateTime.UtcNow, "Keyboard Capture Field is not assigned");
            yield break;
        }

        demo.ShowEditable(true);
        for (var f = 0; f < settleFrames; f++) yield return null;

        Screen.orientation = ScreenOrientation.Portrait;
        yield return WaitForOrientation(portrait: true);

        field.Activate();
        yield return CaptureKeyboardState(results, $"slide-{slideCount:D2}-keyboard-portrait");

        Screen.orientation = ScreenOrientation.LandscapeLeft;
        yield return WaitForOrientation(portrait: false);
        yield return CaptureKeyboardState(results, $"slide-{slideCount + 1:D2}-keyboard-landscape");
    }

    /// <summary>Waits out the system keyboard animation and the field's reaction to it, then captures.</summary>
    private static IEnumerator CaptureKeyboardState(TestResultCollection results, string name)
    {
        var start = DateTime.UtcNow;
        yield return SettleKeyboardState();

        var visible = UniTextNativeInput.IsKeyboardVisible;
        TestScreenshot.Capture(name);
        Record(results, name, start, visible ? null : "Soft keyboard never became visible");
    }

    /// <summary>
    /// Holds the state long enough for the frame cut out of the device screen recording to land
    /// inside it; the extraction in the run-app action is offset against this dwell.
    /// </summary>
    private static IEnumerator SettleKeyboardState()
    {
        var deadline = Time.realtimeSinceStartup + keyboardTimeout;
        while (!UniTextNativeInput.IsKeyboardVisible && Time.realtimeSinceStartup < deadline)
            yield return null;

        var dwellUntil = Time.realtimeSinceStartup + stateDwell;
        while (Time.realtimeSinceStartup < dwellUntil) yield return null;
    }

    private static IEnumerator WaitForOrientation(bool portrait)
    {
        var deadline = Time.realtimeSinceStartup + keyboardTimeout;
        while ((Screen.height > Screen.width) != portrait && Time.realtimeSinceStartup < deadline)
            yield return null;

        for (var f = 0; f < settleFrames; f++) yield return null;
    }

#if !UNITY_WEBGL
    private IEnumerator CaptureAddressablePrefab(TestResultCollection results, int index)
    {
        var name = $"slide-{index:D2}-addressable";
        var start = DateTime.UtcNow;
        var reference = demo.AddressablePrefab;
        if (reference == null || !reference.RuntimeKeyIsValid())
        {
            Record(results, name, start, "Addressable Prefab is not assigned");
            yield break;
        }

        var text = demo.SlideshowText;
        var parent = text != null ? text.rectTransform.parent : null;
        if (parent == null)
        {
            Record(results, name, start, "DemoText has no canvas parent");
            yield break;
        }

        demo.ShowEditable(false);
        var textObject = text.gameObject;
        var wasActive = textObject.activeSelf;
        var siblingIndex = text.transform.GetSiblingIndex();
        textObject.SetActive(false);

        AsyncOperationHandle<GameObject> handle = default;
        try
        {
            GameObject instance = null;
            Exception error = null;
            try
            {
                handle = reference.InstantiateAsync(parent, false);
                instance = handle.WaitForCompletion();
                if (handle.Status != AsyncOperationStatus.Succeeded || instance == null)
                    error = handle.OperationException ??
                            new InvalidOperationException("Addressable prefab instantiation returned no instance");
            }
            catch (Exception e)
            {
                error = e;
            }

            if (error != null)
            {
                Debug.LogException(error);
                Record(results, name, start, error.ToString());
                yield break;
            }

            instance.transform.SetSiblingIndex(siblingIndex);
            for (var f = 0; f < settleFrames; f++) yield return null;
            Capture(results, name);
        }
        finally
        {
            if (handle.IsValid()) Addressables.ReleaseInstance(handle);
            text.transform.SetSiblingIndex(siblingIndex);
            textObject.SetActive(wasActive);
        }
    }

    private IEnumerator RestoreKeyboardRecordingTail()
    {
        var field = demo.KeyboardCaptureField;
        if (field == null) yield break;

        demo.ShowEditable(true);
        for (var f = 0; f < settleFrames; f++) yield return null;

        Screen.orientation = ScreenOrientation.Portrait;
        yield return WaitForOrientation(portrait: true);
        field.Activate();
        yield return SettleKeyboardState();

        Screen.orientation = ScreenOrientation.LandscapeLeft;
        yield return WaitForOrientation(portrait: false);
        yield return SettleKeyboardState();
    }
#endif

    private static void Capture(TestResultCollection results, string name)
    {
        var start = DateTime.UtcNow;
        TestScreenshot.Capture(name);
        Record(results, name, start, null);
    }

    private static void Record(TestResultCollection results, string name, DateTime start, string error)
    {
        results.Add(new TestResult
        {
            ClassName = "BasicUsageSlideshow",
            MethodName = name,
            Passed = error == null,
            ErrorMessage = error,
            StartTime = start,
            EndTime = DateTime.UtcNow
        });
    }
}
#endif
