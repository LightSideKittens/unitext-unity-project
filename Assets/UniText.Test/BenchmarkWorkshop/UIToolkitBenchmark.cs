using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.UIElements;
using Debug = UnityEngine.Debug;

public class UIToolkitBenchmark : MonoBehaviour
{
    [Header("UI Toolkit")]
    public UIDocument uiDocument;
    public PanelSettings panelSettings;

    [Tooltip("UXML template containing a Label to clone")]
    public VisualTreeAsset labelTemplate;

    [Header("Settings")]
    public int objectCount = 100;
    public int iterations = 10;
    public int warmupIterations = 3;

    [Header("Test Control")]
    public bool runCreationDestructionTest = true;
    public bool runFullRebuildTest = true;
    public bool runLayoutRebuildTest = true;
    public bool runMeshRebuildTest = true;

    [Header("Status")]
    [SerializeField] private bool isRunning;
    [SerializeField] private string currentTest = "";

    private VisualElement container;
    private string[] testStrings;
    private string prefabText;
    private readonly StringBuilder results = new();
    private TestResults testResults;
    private readonly Stopwatch stopwatch = new();

    public const string SystemName = "UIToolkit";

    public TestResults Results => testResults;

    private void HandlePostRender(Camera cam)
    {
        if (!stopwatch.IsRunning) return;
        stopwatch.Stop();
    }
    public bool IsRunning => isRunning;

    #region Data Structures

    public struct TestMetrics
    {
        public List<float> frameTimes;
        public long totalAlloc;
        public long managedAlloc;
        public int gcGen0;
        public int gcGen1;
        public int gcGen2;

        public static TestMetrics Create() => new()
        {
            frameTimes = new List<float>(),
            totalAlloc = 0,
            managedAlloc = 0,
            gcGen0 = 0,
            gcGen1 = 0,
            gcGen2 = 0
        };

        public float MedianFrameTime
        {
            get
            {
                if (frameTimes == null || frameTimes.Count == 0) return 0;
                var sorted = new List<float>(frameTimes);
                sorted.Sort();
                return sorted[sorted.Count / 2];
            }
        }

        public float TotalTime
        {
            get
            {
                if (frameTimes == null || frameTimes.Count == 0) return 0;
                float sum = 0;
                for (int i = 0; i < frameTimes.Count; i++)
                    sum += frameTimes[i];
                return sum;
            }
        }
    }

    public struct TestResults
    {
        public TestMetrics creation;
        public TestMetrics destruction;
        public TestMetrics fullRebuild;
        public TestMetrics layoutWrapNoAuto;
        public TestMetrics layoutWrapAuto;
        public TestMetrics layoutNoWrapNoAuto;
        public TestMetrics layoutNoWrapAuto;
        public TestMetrics meshRebuild;

        public static TestResults Create() => new()
        {
            creation = TestMetrics.Create(),
            destruction = TestMetrics.Create(),
            fullRebuild = TestMetrics.Create(),
            layoutWrapNoAuto = TestMetrics.Create(),
            layoutWrapAuto = TestMetrics.Create(),
            layoutNoWrapNoAuto = TestMetrics.Create(),
            layoutNoWrapAuto = TestMetrics.Create(),
            meshRebuild = TestMetrics.Create()
        };
    }

    #endregion

    #region Test Strings

    private void Start()
    {
        EnsureUIDocument();
    }

    private void GenerateTestStringsFromPrefab()
    {
        if (string.IsNullOrEmpty(prefabText) || prefabText.Length < 10)
        {
            testStrings = new[]
            {
                "Test string variant 0",
                "Test string variant 1",
                "Test string variant 2",
                "Test string variant 3"
            };
            return;
        }

        testStrings = new string[10];
        var len = prefabText.Length;
        var mid = len / 2;
        var chunk10 = Math.Max(50, len / 10);
        var chunk20 = Math.Max(100, len / 5);
        var chunk30 = Math.Max(150, len * 3 / 10);

        testStrings[0] = prefabText.Substring(chunk10);
        testStrings[1] = prefabText.Substring(chunk20);
        testStrings[2] = prefabText.Substring(chunk30);

        testStrings[3] = prefabText.Substring(0, len - chunk10);
        testStrings[4] = prefabText.Substring(0, len - chunk20);

        testStrings[5] = prefabText.Substring(0, mid - chunk10 / 2) + prefabText.Substring(mid + chunk10 / 2);
        testStrings[6] = prefabText.Substring(0, mid - chunk20 / 2) + prefabText.Substring(mid + chunk20 / 2);

        var pad10 = new string('X', chunk10 / 2);
        var pad20 = new string('Y', chunk20 / 2);
        testStrings[7] = pad10 + prefabText + pad10;
        testStrings[8] = pad20 + prefabText + pad20;

        var replacement = new string('Z', chunk20);
        testStrings[9] = prefabText.Substring(0, mid - chunk20 / 2) + replacement + prefabText.Substring(mid + chunk20 / 2);
    }

    private void EnsureUIDocument()
    {
        if (uiDocument == null)
        {
            uiDocument = GetComponent<UIDocument>();
            if (uiDocument == null)
            {
                uiDocument = gameObject.AddComponent<UIDocument>();
            }
        }

        if (panelSettings != null && uiDocument.panelSettings == null)
        {
            uiDocument.panelSettings = panelSettings;
        }
    }

    #endregion

    #region Label Operations

    private Label CreateLabel()
    {
        if (labelTemplate != null)
        {
            var root = labelTemplate.CloneTree();
            var label = root.Q<Label>();
            if (label != null)
                return label;

            Debug.LogWarning("No Label found in UXML template, using fallback");
        }

        var fallback = new Label();
        fallback.style.position = Position.Absolute;
        fallback.style.width = 200;
        fallback.style.height = 100;
        return fallback;
    }

    private static void SetText(Label label, string text)
    {
        label.text = text;
    }

    private static void SetFontSize(Label label, float size)
    {
        label.style.fontSize = new Length(size, LengthUnit.Pixel);
    }

    private static void SetColor(Label label, Color color)
    {
        label.style.color = new StyleColor(color);
    }

    private static void SetWordWrap(Label label, bool enabled)
    {
        label.style.whiteSpace = enabled ? WhiteSpace.Normal : WhiteSpace.NoWrap;
    }

    private static void SetAutoSize(Label label, bool enabled)
    {
#if UNITY_6000_0_OR_NEWER
        if (enabled)
        {
            label.style.unityTextAutoSize = new StyleTextAutoSize(
                new TextAutoSize(TextAutoSizeMode.BestFit, minSize: 10, maxSize: 72)
            );
        }
        else
        {
            label.style.unityTextAutoSize = new StyleTextAutoSize(
                new TextAutoSize(TextAutoSizeMode.None, 28, 28)
            );
        }
#else
        _ = label;
        _ = enabled;
#endif
    }

    private static void SetLabelSize(Label label, float width, float height)
    {
        label.style.width = new Length(width, LengthUnit.Pixel);
        label.style.height = new Length(height, LengthUnit.Pixel);
    }

    #endregion

    #region Run Benchmark

    [ContextMenu("Run Benchmark")]
    public void RunBenchmark()
    {
        if (isRunning)
        {
            Debug.LogWarning("Benchmark already running!");
            return;
        }

        EnsureUIDocument();

        if (uiDocument == null || uiDocument.panelSettings == null)
        {
            Debug.LogError("UIDocument or PanelSettings not assigned!");
            return;
        }

        if (labelTemplate == null)
        {
            Debug.LogWarning("No labelTemplate assigned, using simple Label fallback");
        }

        StartCoroutine(RunBenchmarkCoroutine());
    }

    [ContextMenu("Stop Benchmark")]
    public void StopBenchmark()
    {
        if (!isRunning) return;
        StopAllCoroutines();
        Cleanup();
        isRunning = false;
        Debug.Log("Benchmark stopped.");
    }

    private IEnumerator RunBenchmarkCoroutine()
    {
        yield return RunBenchmarkCoroutine(silent: false);
    }

    public IEnumerator RunBenchmarkCoroutine(bool silent)
    {
        isRunning = true;
        results.Clear();
        Camera.onPostRender += HandlePostRender;

        container = new VisualElement();
        container.name = "BenchmarkContainer";
        container.style.position = Position.Absolute;
        container.style.width = Length.Percent(100);
        container.style.height = Length.Percent(100);
        uiDocument.rootVisualElement.Add(container);

        testResults = TestResults.Create();

        if (!silent)
            AppendHeader();

        yield return RunAllTests();

        if (!silent)
        {
            AppendResults();
            Debug.Log(results.ToString());
        }

        Cleanup();
        Camera.onPostRender -= HandlePostRender;
        isRunning = false;
        currentTest = "Complete";
    }

    #endregion

    #region Tests

    private IEnumerator RunAllTests()
    {
        Label[] labels = null;

        if (runCreationDestructionTest)
        {
            currentTest = $"{SystemName}: Creation/Destruction";
            Debug.Log($"  [{SystemName}] Creation/Destruction test...");

            for (int iter = 0; iter < warmupIterations; iter++)
            {
                labels = new Label[objectCount];
                for (int i = 0; i < objectCount; i++)
                {
                    labels[i] = CreateLabel();
                    container.Add(labels[i]);
                }
                yield return null;

                for (int i = 0; i < objectCount; i++)
                    container.Remove(labels[i]);
                yield return null;
                yield return null;
            }

            int gc0Before = GC.CollectionCount(0);
            int gc1Before = GC.CollectionCount(1);
            int gc2Before = GC.CollectionCount(2);

            using var creationGcRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");

            for (int iter = 0; iter < iterations; iter++)
            {
                long iterAllocBefore = Profiler.GetTotalAllocatedMemoryLong();
                labels = new Label[objectCount];
                stopwatch.Restart();
                for (int i = 0; i < objectCount; i++)
                {
                    labels[i] = CreateLabel();
                    container.Add(labels[i]);
                }
                yield return null;
                long iterAlloc = Profiler.GetTotalAllocatedMemoryLong() - iterAllocBefore;
                if (iterAlloc > 0) testResults.creation.totalAlloc += iterAlloc;
                testResults.creation.managedAlloc += creationGcRecorder.LastValue;
                testResults.creation.frameTimes.Add((float)stopwatch.Elapsed.TotalMilliseconds);

                stopwatch.Restart();
                for (int i = 0; i < objectCount; i++)
                    container.Remove(labels[i]);
                yield return null;
                testResults.destruction.frameTimes.Add((float)stopwatch.Elapsed.TotalMilliseconds);
                yield return null;
            }

            testResults.creation.gcGen0 = GC.CollectionCount(0) - gc0Before;
            testResults.creation.gcGen1 = GC.CollectionCount(1) - gc1Before;
            testResults.creation.gcGen2 = GC.CollectionCount(2) - gc2Before;

            AppendTestResult("Creation/Destruction",
                testResults.creation.TotalTime,
                testResults.destruction.TotalTime,
                testResults.creation.totalAlloc,
                testResults.creation.managedAlloc,
                testResults.creation.gcGen0, testResults.creation.gcGen1, testResults.creation.gcGen2);
        }

        if (runFullRebuildTest || runLayoutRebuildTest || runMeshRebuildTest)
        {
            Debug.Log($"  [{SystemName}] Creating labels for rebuild tests...");
            labels = new Label[objectCount];
            for (int i = 0; i < objectCount; i++)
            {
                labels[i] = CreateLabel();
                container.Add(labels[i]);
            }
            prefabText = labels[0].text;
            GenerateTestStringsFromPrefab();
            yield return null;
        }

        if (runFullRebuildTest)
        {
            currentTest = $"{SystemName}: Full Rebuild";
            Debug.Log($"  [{SystemName}] Full Rebuild test...");

            for (int w = 0; w < warmupIterations; w++)
            {
                string text = testStrings[w % testStrings.Length];
                for (int i = 0; i < objectCount; i++)
                    SetText(labels[i], text);
                yield return null;
            }

            int gc0Before = GC.CollectionCount(0);
            int gc1Before = GC.CollectionCount(1);
            int gc2Before = GC.CollectionCount(2);

            using var fullRebuildGcRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");

            for (int iter = 0; iter < iterations; iter++)
            {
                long iterAllocBefore = Profiler.GetTotalAllocatedMemoryLong();
                string text = testStrings[iter % testStrings.Length];
                stopwatch.Restart();
                for (int i = 0; i < objectCount; i++)
                    SetText(labels[i], text);
                yield return null;
                long iterAlloc = Profiler.GetTotalAllocatedMemoryLong() - iterAllocBefore;
                if (iterAlloc > 0) testResults.fullRebuild.totalAlloc += iterAlloc;
                testResults.fullRebuild.managedAlloc += fullRebuildGcRecorder.LastValue;
                testResults.fullRebuild.frameTimes.Add((float)stopwatch.Elapsed.TotalMilliseconds);
            }

            testResults.fullRebuild.gcGen0 = GC.CollectionCount(0) - gc0Before;
            testResults.fullRebuild.gcGen1 = GC.CollectionCount(1) - gc1Before;
            testResults.fullRebuild.gcGen2 = GC.CollectionCount(2) - gc2Before;

            AppendSingleTestResult("Full Rebuild (Text)", testResults.fullRebuild);
        }

        if (runLayoutRebuildTest)
        {
            currentTest = $"{SystemName}: Layout Rebuild";
            Debug.Log($"  [{SystemName}] Layout Rebuild test...");

            yield return RunLayoutVariation(labels, true, false, "Wrap=ON, Auto=OFF",
                m => testResults.layoutWrapNoAuto = m);
            yield return RunLayoutVariation(labels, true, true, "Wrap=ON, Auto=ON",
                m => testResults.layoutWrapAuto = m);
            yield return RunLayoutVariation(labels, false, false, "Wrap=OFF, Auto=OFF",
                m => testResults.layoutNoWrapNoAuto = m);
            yield return RunLayoutVariation(labels, false, true, "Wrap=OFF, Auto=ON",
                m => testResults.layoutNoWrapAuto = m);
        }

        if (runMeshRebuildTest)
        {
            currentTest = $"{SystemName}: Mesh Rebuild";
            Debug.Log($"  [{SystemName}] Mesh Rebuild test...");

            for (int i = 0; i < objectCount; i++)
            {
                SetAutoSize(labels[i], false);
                SetLabelSize(labels[i], 300, 300);
                SetText(labels[i], prefabText);
            }
            yield return null;

            for (int w = 0; w < warmupIterations; w++)
            {
                var c = new Color(w * 0.1f, 0.5f, 0.5f, 1f);
                for (int i = 0; i < objectCount; i++)
                    SetColor(labels[i], c);
                yield return null;
            }

            int gc0Before = GC.CollectionCount(0);
            int gc1Before = GC.CollectionCount(1);
            int gc2Before = GC.CollectionCount(2);

            using var meshRebuildGcRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");

            for (int iter = 0; iter < iterations; iter++)
            {
                long iterAllocBefore = Profiler.GetTotalAllocatedMemoryLong();
                var c = new Color((iter * 0.1f) % 1f, 0.5f, 0.5f, 1f);
                stopwatch.Restart();
                for (int i = 0; i < objectCount; i++)
                    SetColor(labels[i], c);
                yield return null;
                long iterAlloc = Profiler.GetTotalAllocatedMemoryLong() - iterAllocBefore;
                if (iterAlloc > 0) testResults.meshRebuild.totalAlloc += iterAlloc;
                testResults.meshRebuild.managedAlloc += meshRebuildGcRecorder.LastValue;
                testResults.meshRebuild.frameTimes.Add((float)stopwatch.Elapsed.TotalMilliseconds);
            }

            testResults.meshRebuild.gcGen0 = GC.CollectionCount(0) - gc0Before;
            testResults.meshRebuild.gcGen1 = GC.CollectionCount(1) - gc1Before;
            testResults.meshRebuild.gcGen2 = GC.CollectionCount(2) - gc2Before;

            AppendSingleTestResult("Mesh Rebuild (Color)", testResults.meshRebuild);
        }

        if (labels != null)
        {
            for (int i = 0; i < labels.Length; i++)
                if (labels[i] != null)
                    container.Remove(labels[i]);
            yield return null;
        }
    }

    private IEnumerator RunLayoutVariation(Label[] labels, bool wordWrap, bool autoSize, string name,
        Action<TestMetrics> setResult)
    {
        for (int i = 0; i < objectCount; i++)
        {
            SetWordWrap(labels[i], wordWrap);
            SetAutoSize(labels[i], autoSize);
            SetText(labels[i], prefabText);
        }
        yield return null;

        for (int w = 0; w < warmupIterations; w++)
        {
            float rectSize = 100f + (w % 3) * 2500f;
            for (int i = 0; i < objectCount; i++)
                SetLabelSize(labels[i], rectSize, rectSize);
            yield return null;
        }

        var metrics = TestMetrics.Create();

        int gc0Before = GC.CollectionCount(0);
        int gc1Before = GC.CollectionCount(1);
        int gc2Before = GC.CollectionCount(2);

        using var layoutGcRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");

        for (int iter = 0; iter < iterations; iter++)
        {
            long iterAllocBefore = Profiler.GetTotalAllocatedMemoryLong();
            float rectSize = 100f + (iter % 10) * 550f;
            stopwatch.Restart();
            for (int i = 0; i < objectCount; i++)
                SetLabelSize(labels[i], rectSize, rectSize);
            yield return null;
            long iterAlloc = Profiler.GetTotalAllocatedMemoryLong() - iterAllocBefore;
            if (iterAlloc > 0) metrics.totalAlloc += iterAlloc;
            metrics.managedAlloc += layoutGcRecorder.LastValue;
            metrics.frameTimes.Add((float)stopwatch.Elapsed.TotalMilliseconds);
        }

        metrics.gcGen0 = GC.CollectionCount(0) - gc0Before;
        metrics.gcGen1 = GC.CollectionCount(1) - gc1Before;
        metrics.gcGen2 = GC.CollectionCount(2) - gc2Before;

        setResult(metrics);
        AppendSingleTestResult($"Layout ({name})", metrics);
    }

    #endregion

    #region Results Output

    private void AppendHeader()
    {
        results.AppendLine("═══════════════════════════════════════════════════════════════════════════════");
        results.AppendLine($"                         {SystemName} BENCHMARK");
        results.AppendLine("═══════════════════════════════════════════════════════════════════════════════");
        results.AppendLine($"  Objects: {objectCount}");
        results.AppendLine($"  Iterations: {iterations}");
        results.AppendLine($"  Warmup: {warmupIterations}");
        results.AppendLine("═══════════════════════════════════════════════════════════════════════════════");
    }

    private void AppendTestResult(string testName, float createTime, float destroyTime, long alloc, long managed, int gc0, int gc1, int gc2)
    {
        results.AppendLine($"\n[{SystemName}] {testName}");
        results.AppendLine($"  Create: {createTime:F2}ms | Destroy: {destroyTime:F2}ms");
        results.AppendLine($"  Alloc: {FormatBytes(alloc)} (Managed: {FormatBytes(managed)}) | GC: Gen0={gc0}, Gen1={gc1}, Gen2={gc2}");
    }

    private void AppendSingleTestResult(string testName, TestMetrics metrics)
    {
        results.AppendLine($"\n[{SystemName}] {testName}");
        results.AppendLine($"  Time: {metrics.TotalTime:F2}ms");
        results.AppendLine($"  Alloc: {FormatBytes(metrics.totalAlloc)} (Managed: {FormatBytes(metrics.managedAlloc)}) | GC: Gen0={metrics.gcGen0}, Gen1={metrics.gcGen1}, Gen2={metrics.gcGen2}");
    }

    private void AppendResults()
    {
        results.AppendLine("\n═══════════════════════════════════════════════════════════════════════════════");
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 0) return $"-{FormatBytes(-bytes)}";
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024f:F1} KB";
        return $"{bytes / (1024f * 1024f):F2} MB";
    }

    #endregion

    #region Helpers

    private void Cleanup()
    {
        if (container != null && uiDocument != null && uiDocument.rootVisualElement != null)
        {
            uiDocument.rootVisualElement.Remove(container);
            container = null;
        }
    }

    private void OnDisable()
    {
        StopBenchmark();
    }

    #endregion

#if UNITY_EDITOR
    [UnityEditor.MenuItem("UniText/Run UI Toolkit Benchmark")]
    private static void RunFromMenu()
    {
        var test = FindFirstObjectByType<UIToolkitBenchmark>();
        if (test != null)
            test.RunBenchmark();
        else
            Debug.LogError("No UIToolkitBenchmark found in scene. Add UIToolkitBenchmark component to a GameObject with UIDocument.");
    }
#endif
}