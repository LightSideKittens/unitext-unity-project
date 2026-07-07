using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;
using Debug = UnityEngine.Debug;

/// <summary>
/// Engine-agnostic half of the text harness: shared config, result structs, corpus/string generation,
/// reporting and run control. The instance-typed measurement loops live in <see cref="TextBenchmarkBase{TInstance}"/>.
/// Split so <see cref="TestResults"/> stays ONE type across uGUI (Component) and UI Toolkit (Label).
/// </summary>
public abstract class TextBenchmarkBase : MonoBehaviour
{
    [Header("Settings")]
    public int objectCount = 100;
    public int iterations = 10;
    public int warmupIterations = 3;

    [Header("Test Control")]
    public bool runCreationDestructionTest = true;
    public bool runFullRebuildTest = true;
    public bool runLayoutRebuildTest = true;
    public bool runMeshRebuildTest = true;

    /// <summary>Corpus label recorded with the results ("multilingual" = the showcase text, "latin" = the apples-to-apples pass).</summary>
    [NonSerialized] public string corpusName = "multilingual";

    /// <summary>Swaps the run's workload for another code corpus (the Latin pass); null uses <see cref="BenchmarkConfig.Multilingual"/>. The workload never comes from a prefab or scene field.</summary>
    [NonSerialized] public string corpusOverrideText;

    [Header("Status")]
    [SerializeField] protected bool isRunning;
    [SerializeField] protected string currentTest = "";

    protected string[] testStrings;
    protected string[] uniqueTestStrings;
    protected string[] richTestStrings;
    protected string corpus;
    protected string richCorpus;
    protected readonly StringBuilder results = new();
    protected TestResults testResults;

    protected readonly Stopwatch stopwatch = new();
    protected readonly WaitForEndOfFrame waitForEndOfFrame = new();

    public TestResults Results => testResults;
    public bool IsRunning => isRunning;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Init()
    {
        Application.targetFrameRate = 1000;
    }

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
        public TestMetrics fullRebuildUnique;
        public TestMetrics fullRebuildRichText;
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
            fullRebuildUnique = TestMetrics.Create(),
            fullRebuildRichText = TestMetrics.Create(),
            layoutWrapNoAuto = TestMetrics.Create(),
            layoutWrapAuto = TestMetrics.Create(),
            layoutNoWrapNoAuto = TestMetrics.Create(),
            layoutNoWrapAuto = TestMetrics.Create(),
            meshRebuild = TestMetrics.Create()
        };
    }

    #endregion

    #region Engine hooks

    public abstract string SystemName { get; }
    protected abstract void OnBeforeAllTests();
    protected abstract void OnAfterAllTests();
    protected abstract void SetupContainer();
    protected abstract void TeardownContainer();
    protected abstract IEnumerator RunAllTests();

    protected virtual bool ValidateSetup() => true;
    protected virtual void OnPhaseComplete(string phaseName) { }
    protected virtual void OnBeforePhaseIterations(string phaseName) { }
    protected virtual void OnAfterPhaseIterations(string phaseName) { }

    #endregion

    #region Test Strings

    /// <summary>Derives edit/unique/rich variants from the shared <see cref="corpus"/> — the incremental, cold and markup workloads.</summary>
    protected void GenerateTestStrings()
    {
        testStrings = new string[10];
        var len = corpus.Length;
        var mid = len / 2;
        var chunk10 = Math.Max(50, len / 10);
        var chunk20 = Math.Max(100, len / 5);
        var chunk30 = Math.Max(150, len * 3 / 10);

        testStrings[0] = corpus.Substring(chunk10);
        testStrings[1] = corpus.Substring(chunk20);
        testStrings[2] = corpus.Substring(chunk30);

        testStrings[3] = corpus.Substring(0, len - chunk10);
        testStrings[4] = corpus.Substring(0, len - chunk20);

        testStrings[5] = corpus.Substring(0, mid - chunk10 / 2) + corpus.Substring(mid + chunk10 / 2);
        testStrings[6] = corpus.Substring(0, mid - chunk20 / 2) + corpus.Substring(mid + chunk20 / 2);

        var pad10 = new string('X', chunk10 / 2);
        var pad20 = new string('Y', chunk20 / 2);
        testStrings[7] = pad10 + corpus + pad10;
        testStrings[8] = pad20 + corpus + pad20;

        var replacement = new string('Z', chunk20);
        testStrings[9] = corpus.Substring(0, mid - chunk20 / 2) + replacement + corpus.Substring(mid + chunk20 / 2);

        uniqueTestStrings = GenerateUniqueVariants(corpus, testStrings.Length);
        richTestStrings = GenerateUniqueVariants(richCorpus, testStrings.Length);
    }

    /// <summary>
    /// Variants where EVERY paragraph's content differs between iterations (a fixed-width token is
    /// injected at the start of each line). Engines with content-addressed caches (UniText's
    /// per-paragraph shape cache) get zero reuse here — the true cold "full rebuild" case, while the
    /// incremental variants measure the warm editing case. Same strings go to every engine, so the
    /// split is fair by construction. Shared by all benchmark harnesses.
    /// </summary>
    internal static string[] GenerateUniqueVariants(string corpus, int count)
    {
        var variants = new string[count];
        var sb = new StringBuilder(corpus.Length + 512);
        var lines = corpus.Split('\n');

        for (int v = 0; v < count; v++)
        {
            sb.Clear();
            for (int l = 0; l < lines.Length; l++)
            {
                if (l > 0) sb.Append('\n');
                sb.Append('v').Append(v).Append('.').Append(l % 10).Append(' ').Append(lines[l]);
            }
            variants[v] = sb.ToString();
        }

        return variants;
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

        if (!ValidateSetup()) return;

        StartCoroutine(RunBenchmarkCoroutine(silent: false));
    }

    [ContextMenu("Stop Benchmark")]
    public void StopBenchmark()
    {
        if (!isRunning) return;
        StopAllCoroutines();
        TeardownContainer();
        isRunning = false;
        Debug.Log("Benchmark stopped.");
    }

    public IEnumerator RunBenchmarkCoroutine(bool silent)
    {
        isRunning = true;
        results.Clear();

        var cfg = BenchmarkConfig.Instance;
        if (cfg != null)
        {
            objectCount = cfg.objectCount;
            iterations = cfg.iterations;
            warmupIterations = cfg.warmupIterations;
        }

        OnBeforeAllTests();
        SetupContainer();
        testResults = TestResults.Create();

        if (!silent)
            AppendHeader();

        yield return RunAllTests();

        if (!silent)
        {
            AppendResults();
            Debug.Log(results.ToString());
        }

        TeardownContainer();
        OnAfterAllTests();
        isRunning = false;
        currentTest = "Complete";
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

    protected void AppendCreationDestructionResult()
    {
        results.AppendLine($"\n[{SystemName}] Creation/Destruction");
        results.AppendLine($"  Create: {testResults.creation.TotalTime:F2}ms | Destroy: {testResults.destruction.TotalTime:F2}ms");
        results.AppendLine($"  Alloc: {FormatBytes(testResults.creation.totalAlloc)} (Managed: {FormatBytes(testResults.creation.managedAlloc)}) | GC: Gen0={testResults.creation.gcGen0}, Gen1={testResults.creation.gcGen1}, Gen2={testResults.creation.gcGen2}");
    }

    protected void AppendSingleTestResult(string testName, TestMetrics metrics)
    {
        results.AppendLine($"\n[{SystemName}] {testName}");
        results.AppendLine($"  Time: {metrics.TotalTime:F2}ms");
        results.AppendLine($"  Alloc: {FormatBytes(metrics.totalAlloc)} (Managed: {FormatBytes(metrics.managedAlloc)}) | GC: Gen0={metrics.gcGen0}, Gen1={metrics.gcGen1}, Gen2={metrics.gcGen2}");
    }

    private void AppendResults()
    {
        results.AppendLine("\n═══════════════════════════════════════════════════════════════════════════════");
    }

    public static string FormatBytes(long bytes)
    {
        if (bytes < 0) return $"-{FormatBytes(-bytes)}";
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024f:F1} KB";
        return $"{bytes / (1024f * 1024f):F2} MB";
    }

    #endregion

    private void OnDisable()
    {
        StopBenchmark();
    }
}

/// <summary>
/// The measurement loops, generic over the per-object handle each engine exposes (a <c>Component</c>
/// for uGUI text, a <c>Label</c> for UI Toolkit). Subclasses supply object lifecycle + property setters;
/// the timing windows, warmup, GC/alloc bookkeeping and phase order live here once for every engine.
/// </summary>
public abstract class TextBenchmarkBase<TInstance> : TextBenchmarkBase where TInstance : class
{
    protected TInstance[] instances;

    protected abstract TInstance CreateInstance(int index);
    protected abstract void DestroyInstance(TInstance instance);
    protected abstract void SetText(TInstance instance, string text);
    protected abstract void SetFontSize(TInstance instance, float size);
    protected abstract void SetColor(TInstance instance, Color color);
    protected abstract void SetWordWrap(TInstance instance, bool enabled);
    protected abstract void SetAutoSize(TInstance instance, bool enabled);
    protected abstract void SetRectSize(TInstance instance, float width, float height);

    /// <summary>Enables the engine's markup path so the bold tags in the rich variants are parsed and styled.</summary>
    protected abstract void SetRichText(TInstance instance, bool enabled);

    protected override IEnumerator RunAllTests()
    {
        if (runCreationDestructionTest)
        {
            currentTest = $"{SystemName}: Creation/Destruction";
            Debug.Log($"  [{SystemName}] Creation/Destruction test...");
            yield return RunCreationDestruction();
            AppendCreationDestructionResult();
            OnPhaseComplete("Creation/Destruction");
        }

        if (!(runFullRebuildTest || runLayoutRebuildTest || runMeshRebuildTest))
            yield break;

        Debug.Log($"  [{SystemName}] Creating instances for rebuild tests...");
        instances = new TInstance[objectCount];
        for (int i = 0; i < objectCount; i++)
            instances[i] = CreateInstance(i);

        if (string.IsNullOrEmpty(corpusOverrideText))
        {
            corpus = BenchmarkConfig.MultilingualPlain;
            richCorpus = BenchmarkConfig.Multilingual;
        }
        else
        {
            corpus = richCorpus = corpusOverrideText;
        }

        for (int i = 0; i < objectCount; i++)
        {
            SetText(instances[i], corpus);
            SetRichText(instances[i], false);
        }

        GenerateTestStrings();
        yield return null;

        if (runFullRebuildTest)
        {
            currentTest = $"{SystemName}: Full Rebuild";
            Debug.Log($"  [{SystemName}] Full Rebuild test...");
            yield return RunMeasuredPhase("Full Rebuild (Incremental Edit)",
                w => ApplyText(testStrings, w), iter => ApplyText(testStrings, iter),
                m => testResults.fullRebuild = m);
            OnPhaseComplete("Full Rebuild");

            yield return RunMeasuredPhase("Full Rebuild (Unique Text)",
                w => ApplyText(uniqueTestStrings, w), iter => ApplyText(uniqueTestStrings, iter),
                m => testResults.fullRebuildUnique = m);
            OnPhaseComplete("Full Rebuild Unique");

            for (int i = 0; i < objectCount; i++) SetRichText(instances[i], true);
            yield return RunMeasuredPhase("Full Rebuild (Rich Text)",
                w => ApplyText(richTestStrings, w), iter => ApplyText(richTestStrings, iter),
                m => testResults.fullRebuildRichText = m);
            for (int i = 0; i < objectCount; i++) SetRichText(instances[i], false);
            OnPhaseComplete("Full Rebuild Rich Text");
        }

        if (runLayoutRebuildTest)
        {
            currentTest = $"{SystemName}: Layout Rebuild";
            Debug.Log($"  [{SystemName}] Layout Rebuild test...");
            yield return RunLayoutVariation(true, false, "Wrap=ON, Auto=OFF", m => testResults.layoutWrapNoAuto = m);
            yield return RunLayoutVariation(true, true, "Wrap=ON, Auto=ON", m => testResults.layoutWrapAuto = m);
            yield return RunLayoutVariation(false, false, "Wrap=OFF, Auto=OFF", m => testResults.layoutNoWrapNoAuto = m);
            yield return RunLayoutVariation(false, true, "Wrap=OFF, Auto=ON", m => testResults.layoutNoWrapAuto = m);
            OnPhaseComplete("Layout Rebuild");
        }

        if (runMeshRebuildTest)
        {
            currentTest = $"{SystemName}: Mesh Rebuild";
            Debug.Log($"  [{SystemName}] Mesh Rebuild test...");
            for (int i = 0; i < objectCount; i++)
            {
                SetAutoSize(instances[i], false);
                SetRectSize(instances[i], 300, 300);
                SetText(instances[i], corpus);
            }
            yield return null;

            yield return RunMeasuredPhase("Mesh Rebuild (Color)",
                w => ApplyColor(w * 0.1f),
                iter => ApplyColor((iter * 0.1f) % 1f),
                m => testResults.meshRebuild = m,
                phaseHookName: "Mesh Rebuild");
            OnPhaseComplete("Mesh Rebuild");
        }

        for (int i = 0; i < objectCount; i++)
            DestroyInstance(instances[i]);
        instances = null;
        yield return null;
    }

    private void ApplyText(string[] source, int index)
    {
        var text = source[index % source.Length];
        for (int i = 0; i < objectCount; i++)
            SetText(instances[i], text);
    }

    private void ApplyColor(float r)
    {
        var c = new Color(r, 0.5f, 0.5f, 1f);
        for (int i = 0; i < objectCount; i++)
            SetColor(instances[i], c);
    }

    private IEnumerator RunCreationDestruction()
    {
        for (int iter = 0; iter < warmupIterations; iter++)
        {
            instances = new TInstance[objectCount];
            for (int i = 0; i < objectCount; i++) instances[i] = CreateInstance(i);
            yield return null;
            for (int i = 0; i < objectCount; i++) DestroyInstance(instances[i]);
            yield return null;
            yield return null;
        }

        int gc0 = GC.CollectionCount(0), gc1 = GC.CollectionCount(1), gc2 = GC.CollectionCount(2);
        using var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");

        for (int iter = 0; iter < iterations; iter++)
        {
            long allocBefore = Profiler.GetTotalAllocatedMemoryLong();
            instances = new TInstance[objectCount];
            stopwatch.Restart();
            for (int i = 0; i < objectCount; i++) instances[i] = CreateInstance(i);
            yield return waitForEndOfFrame;
            stopwatch.Stop();
            long alloc = Profiler.GetTotalAllocatedMemoryLong() - allocBefore;
            if (alloc > 0) testResults.creation.totalAlloc += alloc;
            testResults.creation.managedAlloc += recorder.LastValue;
            testResults.creation.frameTimes.Add((float)stopwatch.Elapsed.TotalMilliseconds);

            stopwatch.Restart();
            for (int i = 0; i < objectCount; i++) DestroyInstance(instances[i]);
            yield return waitForEndOfFrame;
            stopwatch.Stop();
            testResults.destruction.frameTimes.Add((float)stopwatch.Elapsed.TotalMilliseconds);
            yield return null;
        }

        testResults.creation.gcGen0 = GC.CollectionCount(0) - gc0;
        testResults.creation.gcGen1 = GC.CollectionCount(1) - gc1;
        testResults.creation.gcGen2 = GC.CollectionCount(2) - gc2;
    }

    private IEnumerator RunLayoutVariation(bool wordWrap, bool autoSize, string name, Action<TestMetrics> setResult)
    {
        for (int i = 0; i < objectCount; i++)
        {
            SetWordWrap(instances[i], wordWrap);
            SetAutoSize(instances[i], autoSize);
            SetText(instances[i], corpus);
        }
        yield return null;

        yield return RunMeasuredPhase($"Layout ({name})",
            w => ApplyRectSize(100f + (w % 3) * 2500f),
            iter => ApplyRectSize(100f + (iter % 10) * 550f),
            m => { setResult(m); },
            phaseHookName: $"Layout ({name})");
    }

    private void ApplyRectSize(float size)
    {
        for (int i = 0; i < objectCount; i++)
            SetRectSize(instances[i], size, size);
    }

    /// <summary>
    /// The one measured-phase envelope: warmup, then <see cref="iterations"/> timed frames of
    /// <paramref name="iterationStep"/> with per-frame alloc + managed-GC + gen-count bookkeeping.
    /// Every rebuild/layout/mesh phase runs through here so their timing windows can never drift apart.
    /// The measured index continues past the warmup indices (<c>iter + warmupIterations</c>) so the first
    /// timed frame applies a value different from the last warmup — otherwise the engine dedups the
    /// unchanged input and the frame measures a no-op, not a rebuild.
    /// </summary>
    protected IEnumerator RunMeasuredPhase(string reportName, Action<int> warmupStep, Action<int> iterationStep,
        Action<TestMetrics> commit, string phaseHookName = null)
    {
        for (int w = 0; w < warmupIterations; w++)
        {
            warmupStep(w);
            yield return null;
        }

        var metrics = TestMetrics.Create();
        int gc0 = GC.CollectionCount(0), gc1 = GC.CollectionCount(1), gc2 = GC.CollectionCount(2);
        using var recorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");

        if (phaseHookName != null) OnBeforePhaseIterations(phaseHookName);
        for (int iter = 0; iter < iterations; iter++)
        {
            long allocBefore = Profiler.GetTotalAllocatedMemoryLong();
            stopwatch.Restart();
            iterationStep(iter + warmupIterations);
            yield return waitForEndOfFrame;
            stopwatch.Stop();
            long alloc = Profiler.GetTotalAllocatedMemoryLong() - allocBefore;
            if (alloc > 0) metrics.totalAlloc += alloc;
            metrics.managedAlloc += recorder.LastValue;
            metrics.frameTimes.Add((float)stopwatch.Elapsed.TotalMilliseconds);
        }
        if (phaseHookName != null) OnAfterPhaseIterations(phaseHookName);

        metrics.gcGen0 = GC.CollectionCount(0) - gc0;
        metrics.gcGen1 = GC.CollectionCount(1) - gc1;
        metrics.gcGen2 = GC.CollectionCount(2) - gc2;

        commit(metrics);
        AppendSingleTestResult(reportName, metrics);
    }
}
