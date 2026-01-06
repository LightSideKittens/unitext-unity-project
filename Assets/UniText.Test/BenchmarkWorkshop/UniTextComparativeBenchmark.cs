using System;
using System.Collections;
using System.Text;
using UnityEngine;

public class UniTextComparativeBenchmark : MonoBehaviour
{
    [Header("Benchmark Components")]
    public UniTextBenchmark uniTextBenchmark;
    public TMPBenchmark tmpBenchmark;

    [Header("Settings (applied to both)")]
    public int objectCount = 100;
    public int iterations = 10;
    public int warmupIterations = 3;

    [Header("Test Control (applied to both)")]
    public bool runCreationDestructionTest = true;
    public bool runFullRebuildTest = true;
    public bool runLayoutRebuildTest = true;
    public bool runMeshRebuildTest = true;

    [Header("Status")]
    [SerializeField] private bool isRunning;
#pragma warning disable CS0414
    [SerializeField] private string currentTest = "";
#pragma warning restore CS0414

    private readonly StringBuilder results = new();

    [ContextMenu("Run Comparative Benchmark")]
    public void RunAllBenchmarks()
    {
        if (isRunning)
        {
            Debug.LogWarning("Benchmark already running!");
            return;
        }

        if (uniTextBenchmark == null || tmpBenchmark == null)
        {
            Debug.LogError("Please assign both UniTextBenchmark and TMPBenchmark components!");
            return;
        }

        if (uniTextBenchmark.prefab == null || tmpBenchmark.prefab == null)
        {
            Debug.LogError("Please assign prefabs to both benchmark components!");
            return;
        }

        StartCoroutine(RunComparativeCoroutine());
    }

    [ContextMenu("Stop Benchmark")]
    public void StopBenchmark()
    {
        if (!isRunning) return;
        StopAllCoroutines();
        uniTextBenchmark?.StopBenchmark();
        tmpBenchmark?.StopBenchmark();
        isRunning = false;
        Debug.Log("Comparative benchmark stopped.");
    }

    private IEnumerator RunComparativeCoroutine()
    {
        isRunning = true;
        results.Clear();

        ApplySettings(uniTextBenchmark);
        ApplySettings(tmpBenchmark);

        results.AppendLine("═══════════════════════════════════════════════════════════════════════════════");
        results.AppendLine("                    UNITEXT vs TMP COMPARATIVE BENCHMARK");
        results.AppendLine("═══════════════════════════════════════════════════════════════════════════════");
        results.AppendLine($"  Objects: {objectCount}");
        results.AppendLine($"  Iterations: {iterations}");
        results.AppendLine($"  Warmup: {warmupIterations}");
        results.AppendLine("═══════════════════════════════════════════════════════════════════════════════");

        results.AppendLine("\n▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓");
        results.AppendLine("                              UNITEXT TESTS");
        results.AppendLine("▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓");
        Debug.Log("=== STARTING UNITEXT TESTS ===");

        currentTest = "UniText";
        yield return uniTextBenchmark.RunBenchmarkCoroutine(silent: true);
        AppendBenchmarkResults(uniTextBenchmark);

        results.AppendLine("\n▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓");
        results.AppendLine("                                TMP TESTS");
        results.AppendLine("▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓▓");
        Debug.Log("=== STARTING TMP TESTS ===");

        currentTest = "TMP";
        yield return tmpBenchmark.RunBenchmarkCoroutine(silent: true);
        AppendBenchmarkResults(tmpBenchmark);

        AppendComparisonResults();

        Debug.Log(results.ToString());

        isRunning = false;
        currentTest = "Complete";
    }

    private void ApplySettings(TextBenchmarkBase benchmark)
    {
        benchmark.objectCount = objectCount;
        benchmark.iterations = iterations;
        benchmark.warmupIterations = warmupIterations;
        benchmark.runCreationDestructionTest = runCreationDestructionTest;
        benchmark.runFullRebuildTest = runFullRebuildTest;
        benchmark.runLayoutRebuildTest = runLayoutRebuildTest;
        benchmark.runMeshRebuildTest = runMeshRebuildTest;
    }

    private void AppendBenchmarkResults(TextBenchmarkBase benchmark)
    {
        var r = benchmark.Results;
        var name = benchmark.SystemName;

        if (runCreationDestructionTest)
        {
            results.AppendLine($"\n[{name}] Creation/Destruction");
            results.AppendLine($"  Create: {r.creation.MedianFrameTime:F2}ms | Destroy: {r.destruction.MedianFrameTime:F2}ms");
            results.AppendLine($"  Alloc: {FormatBytes(r.creation.totalAlloc)} | GC: Gen0={r.creation.gcGen0}, Gen1={r.creation.gcGen1}, Gen2={r.creation.gcGen2}");
        }

        if (runFullRebuildTest)
        {
            results.AppendLine($"\n[{name}] Full Rebuild (Text)");
            results.AppendLine($"  Time: {r.fullRebuild.MedianFrameTime:F2}ms/frame");
            results.AppendLine($"  Alloc: {FormatBytes(r.fullRebuild.totalAlloc)} | GC: Gen0={r.fullRebuild.gcGen0}, Gen1={r.fullRebuild.gcGen1}, Gen2={r.fullRebuild.gcGen2}");
        }

        if (runLayoutRebuildTest)
        {
            AppendLayoutResult(name, "Wrap=ON, Auto=OFF", r.layoutWrapNoAuto);
            AppendLayoutResult(name, "Wrap=ON, Auto=ON", r.layoutWrapAuto);
            AppendLayoutResult(name, "Wrap=OFF, Auto=OFF", r.layoutNoWrapNoAuto);
            AppendLayoutResult(name, "Wrap=OFF, Auto=ON", r.layoutNoWrapAuto);
        }

        if (runMeshRebuildTest)
        {
            results.AppendLine($"\n[{name}] Mesh Rebuild (Color)");
            results.AppendLine($"  Time: {r.meshRebuild.MedianFrameTime:F2}ms/frame");
            results.AppendLine($"  Alloc: {FormatBytes(r.meshRebuild.totalAlloc)} | GC: Gen0={r.meshRebuild.gcGen0}, Gen1={r.meshRebuild.gcGen1}, Gen2={r.meshRebuild.gcGen2}");
        }
    }

    private void AppendLayoutResult(string name, string variation, TextBenchmarkBase.TestMetrics m)
    {
        results.AppendLine($"\n[{name}] Layout ({variation})");
        results.AppendLine($"  Time: {m.MedianFrameTime:F2}ms/frame");
        results.AppendLine($"  Alloc: {FormatBytes(m.totalAlloc)} | GC: Gen0={m.gcGen0}, Gen1={m.gcGen1}, Gen2={m.gcGen2}");
    }

    private void AppendComparisonResults()
    {
        var uni = uniTextBenchmark.Results;
        var tmp = tmpBenchmark.Results;

        results.AppendLine("\n═══════════════════════════════════════════════════════════════════════════════");
        results.AppendLine("                           COMPARISON RESULTS");
        results.AppendLine("═══════════════════════════════════════════════════════════════════════════════");

        if (runCreationDestructionTest)
        {
            results.AppendLine("\n[CREATION/DESTRUCTION]");
            AppendComparison("Create", uni.creation.MedianFrameTime, tmp.creation.MedianFrameTime);
            AppendComparison("Destroy", uni.destruction.MedianFrameTime, tmp.destruction.MedianFrameTime);
            AppendAllocComparison("Alloc", uni.creation.totalAlloc, tmp.creation.totalAlloc);
        }

        if (runFullRebuildTest)
        {
            results.AppendLine("\n[FULL REBUILD]");
            AppendComparison("Time", uni.fullRebuild.MedianFrameTime, tmp.fullRebuild.MedianFrameTime);
            AppendAllocComparison("Alloc", uni.fullRebuild.totalAlloc, tmp.fullRebuild.totalAlloc);
        }

        if (runLayoutRebuildTest)
        {
            results.AppendLine("\n[LAYOUT REBUILD]");
            AppendComparison("Wrap=ON, Auto=OFF", uni.layoutWrapNoAuto.MedianFrameTime, tmp.layoutWrapNoAuto.MedianFrameTime);
            AppendAllocComparison("  └─ Alloc", uni.layoutWrapNoAuto.totalAlloc, tmp.layoutWrapNoAuto.totalAlloc);
            AppendComparison("Wrap=ON, Auto=ON", uni.layoutWrapAuto.MedianFrameTime, tmp.layoutWrapAuto.MedianFrameTime);
            AppendAllocComparison("  └─ Alloc", uni.layoutWrapAuto.totalAlloc, tmp.layoutWrapAuto.totalAlloc);
            AppendComparison("Wrap=OFF, Auto=OFF", uni.layoutNoWrapNoAuto.MedianFrameTime, tmp.layoutNoWrapNoAuto.MedianFrameTime);
            AppendAllocComparison("  └─ Alloc", uni.layoutNoWrapNoAuto.totalAlloc, tmp.layoutNoWrapNoAuto.totalAlloc);
            AppendComparison("Wrap=OFF, Auto=ON", uni.layoutNoWrapAuto.MedianFrameTime, tmp.layoutNoWrapAuto.MedianFrameTime);
            AppendAllocComparison("  └─ Alloc", uni.layoutNoWrapAuto.totalAlloc, tmp.layoutNoWrapAuto.totalAlloc);
        }

        if (runMeshRebuildTest)
        {
            results.AppendLine("\n[MESH REBUILD]");
            AppendComparison("Time", uni.meshRebuild.MedianFrameTime, tmp.meshRebuild.MedianFrameTime);
            AppendAllocComparison("Alloc", uni.meshRebuild.totalAlloc, tmp.meshRebuild.totalAlloc);
        }

        results.AppendLine("\n═══════════════════════════════════════════════════════════════════════════════");
        results.AppendLine("  Ratio > 1.0 = TMP slower | < 1.0 = UniText slower");
        results.AppendLine("═══════════════════════════════════════════════════════════════════════════════");
    }

    private void AppendComparison(string label, float uniValue, float tmpValue)
    {
        float ratio = uniValue > 0 ? tmpValue / uniValue : 0;
        string winner = ratio > 1f ? "UniText ✓" : ratio < 1f ? "TMP ✓" : "Equal";
        results.AppendLine($"  {label}: UniText {uniValue:F2}ms | TMP {tmpValue:F2}ms | {ratio:F2}x ({winner})");
    }

    private void AppendAllocComparison(string label, long uniValue, long tmpValue)
    {
        long uniAbs = Math.Max(0, uniValue);
        long tmpAbs = Math.Max(0, tmpValue);

        string winner;
        string ratioStr;

        if (uniAbs == 0 && tmpAbs == 0)
        {
            winner = "Equal";
            ratioStr = "N/A";
        }
        else if (uniAbs == 0)
        {
            winner = "UniText ✓";
            ratioStr = "∞";
        }
        else if (tmpAbs == 0)
        {
            winner = "TMP ✓";
            ratioStr = "0.00x";
        }
        else
        {
            float ratio = (float)tmpAbs / uniAbs;
            winner = ratio > 1f ? "UniText ✓" : ratio < 1f ? "TMP ✓" : "Equal";
            ratioStr = $"{ratio:F2}x";
        }

        results.AppendLine($"  {label}: UniText {FormatBytes(uniValue)} | TMP {FormatBytes(tmpValue)} | {ratioStr} ({winner})");
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 0) return $"-{FormatBytes(-bytes)}";
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024 * 1024) return $"{bytes / 1024f:F1} KB";
        return $"{bytes / (1024f * 1024f):F2} MB";
    }

    private void OnDisable()
    {
        StopBenchmark();
    }

#if UNITY_EDITOR
    [UnityEditor.MenuItem("UniText/Run Comparative Benchmark")]
    private static void RunFromMenu()
    {
        var test = FindFirstObjectByType<UniTextComparativeBenchmark>();
        if (test != null)
            test.RunAllBenchmarks();
        else
            Debug.LogError("No UniTextComparativeBenchmark found in scene.");
    }
#endif
}
