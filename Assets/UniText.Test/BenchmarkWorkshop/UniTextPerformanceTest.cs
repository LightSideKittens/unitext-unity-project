using System.Collections;
using System.Diagnostics;
using LightSide;
using UnityEngine;
using UnityEngine.Profiling;
using Debug = UnityEngine.Debug;


public class UniTextPerformanceTest : MonoBehaviour
{
    public bool useParallel = true;
    
    [Header("Test Settings")] [Tooltip("Prefab with UniText component to instantiate")]
    public GameObject prefab;

    [Tooltip("Parent transform for instantiated objects (optional)")]
    public Transform parent;

    [Tooltip("Number of objects to create per iteration")] [Min(1)]
    public int objectsPerIteration = 100;

    [Tooltip("Number of test iterations")] [Min(1)]
    public int iterations = 10;

    [Tooltip("Frames to wait between creation and destruction")] [Min(1)]
    public int framesBetween = 1;

    [Header("CPU Results")] [SerializeField]
    private float lastInstantiateTimeMs;

    [SerializeField] private float lastDestroyTimeMs;

    [SerializeField] private float avgInstantiateTimeMs;

    [SerializeField] private float avgDestroyTimeMs;

    [SerializeField] private float totalTestTimeMs;

    [Header("Memory Results")] [SerializeField]
    private long lastInstantiateAllocBytes;

    [SerializeField] private long lastDestroyAllocBytes;

    [SerializeField] private long avgInstantiateAllocBytes;

    [SerializeField] private long avgDestroyAllocBytes;

    [SerializeField] private int gcCollectionsDuringTest;

    [Header("Detailed Memory")]
    [SerializeField] private long totalAllocatedBefore;
    [SerializeField] private long totalAllocatedAfter;
    [SerializeField] private long totalReservedBefore;
    [SerializeField] private long totalReservedAfter;
    [SerializeField] private long monoHeapBefore;
    [SerializeField] private long monoHeapAfter;
    [SerializeField] private long monoUsedBefore;
    [SerializeField] private long monoUsedAfter;
    [SerializeField] private long managedMemoryBefore;
    [SerializeField] private long managedMemoryAfter;
    [SerializeField] private int gcGen0Collections;
    [SerializeField] private int gcGen1Collections;
    [SerializeField] private int gcGen2Collections;

    [SerializeField] private bool isRunning;

    private GameObject[] instances;
    private readonly Stopwatch stopwatch = new();

    private double totalInstantiateTime;
    private double totalDestroyTime;
    private long totalInstantiateAlloc;
    private long totalDestroyAlloc;
    private int completedIterations;

    [ContextMenu("Run Test")]
    public void RunTest()
    {
        if (isRunning)
        {
            Debug.LogWarning("Test already running!");
            return;
        }

        if (prefab == null)
        {
            Debug.LogError("UniTextPerformanceTest: Prefab not assigned!");
            return;
        }

        StartCoroutine(RunTestCoroutine());
    }

    [ContextMenu("Stop Test")]
    public void StopTest()
    {
        if (!isRunning) return;

        StopAllCoroutines();
        CleanupInstances();
        isRunning = false;
        Debug.Log("Test stopped.");
    }

    private IEnumerator RunTestCoroutine()
    {
        UniText.UseParallel = useParallel;
        isRunning = true;
        totalInstantiateTime = 0;
        totalDestroyTime = 0;
        totalInstantiateAlloc = 0;
        totalDestroyAlloc = 0;
        completedIterations = 0;

        System.GC.Collect();
        System.GC.WaitForPendingFinalizers();
        System.GC.Collect();

        var gcGen0Before = System.GC.CollectionCount(0);
        var gcGen1Before = System.GC.CollectionCount(1);
        var gcGen2Before = System.GC.CollectionCount(2);

        totalAllocatedBefore = Profiler.GetTotalAllocatedMemoryLong();
        totalReservedBefore = Profiler.GetTotalReservedMemoryLong();
        monoHeapBefore = Profiler.GetMonoHeapSizeLong();
        monoUsedBefore = Profiler.GetMonoUsedSizeLong();
        managedMemoryBefore = System.GC.GetTotalMemory(false);

        var totalStopwatch = Stopwatch.StartNew();

        Debug.Log($"═══════════════════════════════════════════════════════════════");
        Debug.Log($"  UniText Performance Test Started");
        Debug.Log($"  Objects per iteration: {objectsPerIteration}");
        Debug.Log($"  Iterations: {iterations}");
        Debug.Log($"  Frames between create/destroy: {framesBetween}");
        Debug.Log($"───────────────────────────────────────────────────────────────");
        Debug.Log($"  MEMORY BEFORE TEST:");
        Debug.Log($"    Total Allocated:  {FormatBytes(totalAllocatedBefore)}");
        Debug.Log($"    Total Reserved:   {FormatBytes(totalReservedBefore)}");
        Debug.Log($"    Mono Heap Size:   {FormatBytes(monoHeapBefore)}");
        Debug.Log($"    Mono Used:        {FormatBytes(monoUsedBefore)}");
        Debug.Log($"    Managed Memory:   {FormatBytes(managedMemoryBefore)}");
        Debug.Log($"═══════════════════════════════════════════════════════════════");

        instances = new GameObject[objectsPerIteration];
        var parentTransform = parent != null ? parent : transform;

        for (var iter = 0; iter < iterations; iter++)
        {
            var allocBefore = Profiler.GetTotalAllocatedMemoryLong();
            stopwatch.Restart();

            for (var i = 0; i < objectsPerIteration; i++) instances[i] = Instantiate(prefab, parentTransform);

            stopwatch.Stop();
            var allocAfter = Profiler.GetTotalAllocatedMemoryLong();

            var instantiateMs = stopwatch.Elapsed.TotalMilliseconds;
            var instantiateAlloc = allocAfter - allocBefore;

            totalInstantiateTime += instantiateMs;
            totalInstantiateAlloc += instantiateAlloc;
            lastInstantiateTimeMs = (float)instantiateMs;
            lastInstantiateAllocBytes = instantiateAlloc;

            for (var f = 0; f < framesBetween; f++) yield return null;

            allocBefore = Profiler.GetTotalAllocatedMemoryLong();
            stopwatch.Restart();

            for (var i = 0; i < objectsPerIteration; i++)
                if (instances[i] != null)
                    Destroy(instances[i]);

            stopwatch.Stop();
            allocAfter = Profiler.GetTotalAllocatedMemoryLong();

            var destroyMs = stopwatch.Elapsed.TotalMilliseconds;
            var destroyAlloc = allocAfter - allocBefore;

            totalDestroyTime += destroyMs;
            totalDestroyAlloc += destroyAlloc;
            lastDestroyTimeMs = (float)destroyMs;
            lastDestroyAllocBytes = destroyAlloc;

            completedIterations++;

            Debug.Log(
                $"[{iter + 1}/{iterations}] Create: {instantiateMs:F2}ms ({FormatBytes(instantiateAlloc)}) | Destroy: {destroyMs:F2}ms ({FormatBytes(destroyAlloc)})");

            yield return null;
        }

        totalStopwatch.Stop();
        totalTestTimeMs = (float)totalStopwatch.Elapsed.TotalMilliseconds;

        totalAllocatedAfter = Profiler.GetTotalAllocatedMemoryLong();
        totalReservedAfter = Profiler.GetTotalReservedMemoryLong();
        monoHeapAfter = Profiler.GetMonoHeapSizeLong();
        monoUsedAfter = Profiler.GetMonoUsedSizeLong();
        managedMemoryAfter = System.GC.GetTotalMemory(false);

        gcGen0Collections = System.GC.CollectionCount(0) - gcGen0Before;
        gcGen1Collections = System.GC.CollectionCount(1) - gcGen1Before;
        gcGen2Collections = System.GC.CollectionCount(2) - gcGen2Before;
        gcCollectionsDuringTest = gcGen0Collections;

        avgInstantiateTimeMs = (float)(totalInstantiateTime / iterations);
        avgDestroyTimeMs = (float)(totalDestroyTime / iterations);
        avgInstantiateAllocBytes = totalInstantiateAlloc / iterations;
        avgDestroyAllocBytes = totalDestroyAlloc / iterations;

        var avgTimePerObject = avgInstantiateTimeMs / objectsPerIteration;
        var avgDestroyTimePerObject = avgDestroyTimeMs / objectsPerIteration;
        var avgAllocPerObject = avgInstantiateAllocBytes / objectsPerIteration;

        Debug.Log($"═══════════════════════════════════════════════════════════════");
        Debug.Log($"  Test Complete!");
        Debug.Log($"───────────────────────────────────────────────────────────────");
        Debug.Log($"  Total time: {totalTestTimeMs:F2}ms");
        Debug.Log($"───────────────────────────────────────────────────────────────");
        Debug.Log($"  GC COLLECTIONS DURING TEST:");
        Debug.Log($"    Gen 0: {gcGen0Collections}");
        Debug.Log($"    Gen 1: {gcGen1Collections}");
        Debug.Log($"    Gen 2: {gcGen2Collections}");
        Debug.Log($"───────────────────────────────────────────────────────────────");
        Debug.Log($"  MEMORY AFTER TEST:");
        Debug.Log($"    Total Allocated:  {FormatBytes(totalAllocatedAfter)} (Δ {FormatBytesDelta(totalAllocatedAfter - totalAllocatedBefore)})");
        Debug.Log($"    Total Reserved:   {FormatBytes(totalReservedAfter)} (Δ {FormatBytesDelta(totalReservedAfter - totalReservedBefore)})");
        Debug.Log($"    Mono Heap Size:   {FormatBytes(monoHeapAfter)} (Δ {FormatBytesDelta(monoHeapAfter - monoHeapBefore)})");
        Debug.Log($"    Mono Used:        {FormatBytes(monoUsedAfter)} (Δ {FormatBytesDelta(monoUsedAfter - monoUsedBefore)})");
        Debug.Log($"    Managed Memory:   {FormatBytes(managedMemoryAfter)} (Δ {FormatBytesDelta(managedMemoryAfter - managedMemoryBefore)})");
        Debug.Log($"    Unused Reserved:  {FormatBytes(Profiler.GetTotalUnusedReservedMemoryLong())}");
        Debug.Log($"───────────────────────────────────────────────────────────────");
        Debug.Log($"  INSTANTIATE:");
        Debug.Log($"    Avg time: {avgInstantiateTimeMs:F2}ms ({avgTimePerObject:F4}ms per object)");
        Debug.Log($"    Avg alloc: {FormatBytes(avgInstantiateAllocBytes)} ({FormatBytes(avgAllocPerObject)} per object)");
        Debug.Log($"───────────────────────────────────────────────────────────────");
        Debug.Log($"  DESTROY:");
        Debug.Log($"    Avg time: {avgDestroyTimeMs:F2}ms ({avgDestroyTimePerObject:F4}ms per object)");
        Debug.Log($"    Avg alloc: {FormatBytes(avgDestroyAllocBytes)}");
        Debug.Log($"───────────────────────────────────────────────────────────────");
        Debug.Log($"  SUMMARY:");
        Debug.Log($"    Total objects: {objectsPerIteration * iterations}");
        Debug.Log($"    Total alloc during test: {FormatBytes(totalInstantiateAlloc + totalDestroyAlloc)}");
        Debug.Log($"═══════════════════════════════════════════════════════════════");

        UniTextPoolStats.LogAll();

        instances = null;
        isRunning = false;
        UniText.UseParallel = true;
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 0) return $"-{FormatBytes(-bytes)}";
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024f:F1} KB";
        return $"{bytes / (1024f * 1024f):F2} MB";
    }

    private static string FormatBytesDelta(long bytes)
    {
        var sign = bytes >= 0 ? "+" : "-";
        var abs = bytes >= 0 ? bytes : -bytes;
        string formatted;
        if (abs < 1024) formatted = $"{abs} B";
        else if (abs < 1024 * 1024) formatted = $"{abs / 1024f:F1} KB";
        else formatted = $"{abs / (1024f * 1024f):F2} MB";
        return sign + formatted;
    }

    private void CleanupInstances()
    {
        if (instances == null) return;

        for (var i = 0; i < instances.Length; i++)
            if (instances[i] != null)
                Destroy(instances[i]);

        instances = null;
    }

    private void OnDisable()
    {
        StopTest();
    }

#if UNITY_EDITOR
    [UnityEditor.MenuItem("UniText/Run Performance Test")]
    private static void RunFromMenu()
    {
        var test = FindFirstObjectByType<UniTextPerformanceTest>();
        if (test != null)
            test.RunTest();
        else
            Debug.LogError("No UniTextPerformanceTest found in scene. Add the component to a GameObject first.");
    }
#endif
}