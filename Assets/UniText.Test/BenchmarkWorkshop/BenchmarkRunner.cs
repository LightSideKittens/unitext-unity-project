using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using LightSide;
using UnityEngine;
using UnityEngine.SceneManagement;

public class BenchmarkRunner : MonoBehaviour
{
    const string GlyphRasterizationScene = "GlyphRasterization_BenchmarkTest";
    const float WatchdogTimeout = 600f;

    BenchmarkRunData data;

    void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }

#if UNITEXT_BENCHMARK
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void OnRuntimeStart()
    {
        Debug.Log("[BenchmarkRunner] OnRuntimeStart called");

        var runner = ObjectUtils.FindFirst<BenchmarkRunner>();
        if (runner == null)
        {
            Debug.LogError("[BenchmarkRunner] BenchmarkRunner not found on scene!");
            Application.Quit(1);
            return;
        }

#if UNITY_EDITOR
        return; 
#else
        Debug.Log("[BenchmarkRunner] Starting benchmarks...");
        runner.StartCoroutine(runner.RunAllBenchmarks());
#endif
    }
#endif

    [ContextMenu("Run All Benchmarks")]
    public void RunFromMenu()
    {
        StartCoroutine(RunAllBenchmarks());
    }

    IEnumerator RunAllBenchmarks()
    {
        data = new BenchmarkRunData
        {
            timestamp = DateTime.UtcNow.ToString("o")
        };

        for (int i = 0; i < 5; i++)
            yield return null;

        Debug.Log("[BenchmarkRunner] === BENCHMARK START ===");

        yield return RunTextBenchmarks();

        if (CheckWatchdog())
        {
            Debug.Log("[BenchmarkRunner] Loading glyph rasterization scene...");
            SceneManager.LoadScene(GlyphRasterizationScene);
            yield return null;
            yield return null;

            yield return RunGlyphRasterizationBenchmarks();
        }

        Debug.Log("[BenchmarkRunner] === BENCHMARK COMPLETE ===");

        var json = BenchmarkJsonSerializer.Serialize(data);
        OutputResults(json);
    }

    IEnumerator RunTextBenchmarks()
    {
        var uniTextBench = ObjectUtils.FindFirst<UniTextBenchmark>();
        if (uniTextBench != null)
        {
            data.objectCount = uniTextBench.objectCount;
            data.iterations = uniTextBench.iterations;
            data.warmupIterations = uniTextBench.warmupIterations;

            Debug.Log("[BenchmarkRunner] Running UniText (Single-Threaded)...");
            yield return SafeRun("unitextSingleThreaded",
                () => uniTextBench.RunBenchmarkCoroutine(silent: true, parallel: false),
                () => data.textBenchmarks["unitextSingleThreaded"] = uniTextBench.Results);

            if (!CheckWatchdog()) yield break;

            Debug.Log("[BenchmarkRunner] Running UniText (Parallel)...");
            yield return SafeRun("unitextParallel",
                () => uniTextBench.RunBenchmarkCoroutine(silent: true, parallel: true),
                () => data.textBenchmarks["unitextParallel"] = uniTextBench.Results);

            if (!CheckWatchdog()) yield break;
        }
        else
        {
            data.errors.Add("UniTextBenchmark not found on scene");
            Debug.LogWarning("[BenchmarkRunner] UniTextBenchmark not found");
        }

        var tmpBench = ObjectUtils.FindFirst<TMPBenchmark>();
        if (tmpBench != null)
        {
            ApplyConfig(tmpBench);
            Debug.Log("[BenchmarkRunner] Running TMP...");
            yield return SafeRun("tmp",
                () => tmpBench.RunBenchmarkCoroutine(silent: true),
                () => data.textBenchmarks["tmp"] = tmpBench.Results);

            if (!CheckWatchdog()) yield break;
        }
        else
        {
            data.errors.Add("TMPBenchmark not found on scene");
            Debug.LogWarning("[BenchmarkRunner] TMPBenchmark not found");
        }

        var uitkBench = ObjectUtils.FindFirst<UIToolkitBenchmark>();
        if (uitkBench != null)
        {
            uitkBench.objectCount = data.objectCount;
            uitkBench.iterations = data.iterations;
            uitkBench.warmupIterations = data.warmupIterations;

            Debug.Log("[BenchmarkRunner] Running UIToolkit...");
            yield return SafeRun("uiToolkit",
                () => uitkBench.RunBenchmarkCoroutine(silent: true),
                () => data.textBenchmarks["uiToolkit"] = ConvertUIToolkitResults(uitkBench.Results));

            if (!CheckWatchdog()) yield break;
        }
        else
        {
            Debug.LogWarning("[BenchmarkRunner] UIToolkitBenchmark not found (optional)");
        }
    }

    IEnumerator RunGlyphRasterizationBenchmarks()
    {
        var uniGlyph = ObjectUtils.FindFirst<UniText_GlyphRasterizationBenchmark>();
        if (uniGlyph != null)
        {
            Debug.Log("[BenchmarkRunner] Running UniText Glyph Rasterization (Single-Threaded)...");
            yield return SafeRun("unitextGlyphST",
                () => uniGlyph.RunBenchmarkCoroutine(singleThreaded: true),
                () => data.glyphRasterization["unitextSingleThreaded"] = ConvertGlyphResults(uniGlyph.LastResults));

            if (!CheckWatchdog()) yield break;

            Debug.Log("[BenchmarkRunner] Running UniText Glyph Rasterization (Parallel)...");
            yield return SafeRun("unitextGlyphParallel",
                () => uniGlyph.RunBenchmarkCoroutine(singleThreaded: false),
                () => data.glyphRasterization["unitextParallel"] = ConvertGlyphResults(uniGlyph.LastResults));

            if (!CheckWatchdog()) yield break;
        }
        else
        {
            data.errors.Add("UniText_GlyphRasterizationBenchmark not found on glyph rasterization scene");
            Debug.LogWarning("[BenchmarkRunner] UniText_GlyphRasterizationBenchmark not found");
        }

        var tmpGlyph = ObjectUtils.FindFirst<TMP_GlyphRasterizationBenchmark>();
        if (tmpGlyph != null)
        {
            Debug.Log("[BenchmarkRunner] Running TMP Glyph Rasterization...");
            yield return SafeRun("tmpGlyph",
                () => tmpGlyph.RunBenchmarkCoroutine(),
                () => data.glyphRasterization["tmp"] = ConvertTmpGlyphResults(tmpGlyph.LastResults));
        }
        else
        {
            data.errors.Add("TMP_GlyphRasterizationBenchmark not found on glyph rasterization scene");
            Debug.LogWarning("[BenchmarkRunner] TMP_GlyphRasterizationBenchmark not found");
        }
    }

    IEnumerator SafeRun(string name, Func<IEnumerator> coroutineFactory, Action onComplete)
    {
        IEnumerator coroutine;
        try
        {
            coroutine = coroutineFactory();
        }
        catch (Exception e)
        {
            data.errors.Add($"{name}: {e.Message}");
            Debug.LogError($"[BenchmarkRunner] Failed to start {name}: {e}");
            yield break;
        }

        bool done = false;
        Exception caught = null;

        StartCoroutine(WrapCoroutine(coroutine, () => done = true, ex => { caught = ex; done = true; }));

        while (!done)
        {
            if (!CheckWatchdog())
            {
                data.errors.Add($"{name}: watchdog timeout");
                yield break;
            }
            yield return null;
        }

        if (caught != null)
        {
            data.errors.Add($"{name}: {caught.Message}");
            Debug.LogError($"[BenchmarkRunner] {name} failed: {caught}");
        }
        else
        {
            try { onComplete(); }
            catch (Exception e)
            {
                data.errors.Add($"{name} result collection: {e.Message}");
                Debug.LogError($"[BenchmarkRunner] Failed to collect {name} results: {e}");
            }
        }
    }

    static IEnumerator WrapCoroutine(IEnumerator inner, Action onDone, Action<Exception> onError)
    {
        while (true)
        {
            bool hasNext;
            try { hasNext = inner.MoveNext(); }
            catch (Exception e) { onError(e); yield break; }
            if (!hasNext) break;
            yield return inner.Current;
        }
        onDone();
    }

    bool CheckWatchdog()
    {
        if (Time.realtimeSinceStartup > WatchdogTimeout)
        {
            Debug.LogWarning($"[BenchmarkRunner] Watchdog timeout ({WatchdogTimeout}s), writing partial results");
            data.errors.Add($"Watchdog timeout at {Time.realtimeSinceStartup:F0}s");
            return false;
        }
        return true;
    }

    void ApplyConfig(TextBenchmarkBase bench)
    {
        bench.objectCount = data.objectCount;
        bench.iterations = data.iterations;
        bench.warmupIterations = data.warmupIterations;
    }

    #region Result Conversion

    static TextBenchmarkBase.TestResults ConvertUIToolkitResults(UIToolkitBenchmark.TestResults src)
    {
        return new TextBenchmarkBase.TestResults
        {
            creation = ConvertMetrics(src.creation),
            destruction = ConvertMetrics(src.destruction),
            fullRebuild = ConvertMetrics(src.fullRebuild),
            layoutWrapNoAuto = ConvertMetrics(src.layoutWrapNoAuto),
            layoutWrapAuto = ConvertMetrics(src.layoutWrapAuto),
            layoutNoWrapNoAuto = ConvertMetrics(src.layoutNoWrapNoAuto),
            layoutNoWrapAuto = ConvertMetrics(src.layoutNoWrapAuto),
            meshRebuild = ConvertMetrics(src.meshRebuild)
        };
    }

    static TextBenchmarkBase.TestMetrics ConvertMetrics(UIToolkitBenchmark.TestMetrics src)
    {
        return new TextBenchmarkBase.TestMetrics
        {
            frameTimes = src.frameTimes,
            totalAlloc = src.totalAlloc,
            managedAlloc = src.managedAlloc,
            gcGen0 = src.gcGen0,
            gcGen1 = src.gcGen1,
            gcGen2 = src.gcGen2
        };
    }

    static GlyphRasterData ConvertGlyphResults(UniText_GlyphRasterizationBenchmark.GlyphRasterResults src)
    {
        return new GlyphRasterData
        {
            frameTimes = src.frameTimes ?? new List<float>(),
            uniqueGlyphs = src.glyphCounts is { Count: > 0 } ? src.glyphCounts[0] : 0,
            managedAlloc = src.managedAlloc
        };
    }

    static GlyphRasterData ConvertTmpGlyphResults(TMP_GlyphRasterizationBenchmark.GlyphRasterResults src)
    {
        return new GlyphRasterData
        {
            frameTimes = src.frameTimes ?? new List<float>(),
            uniqueGlyphs = src.glyphCounts is { Count: > 0 } ? src.glyphCounts[0] : 0,
            managedAlloc = src.managedAlloc
        };
    }

    #endregion

    #region Output

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void ReportBenchmarkResults(string json);
#endif

    void OutputResults(string json)
    {
        var jsonPath = Path.Combine(Application.persistentDataPath, "benchmarkResults.json");
        File.WriteAllText(jsonPath, json);
        Debug.Log($"[BenchmarkRunner] Results saved to: {jsonPath}");
        Debug.Log($"[BenchmarkRunner] JSON length: {json.Length} chars");

        Console.WriteLine($"BENCHMARK_RESULTS_PATH={jsonPath}");

#if UNITY_WEBGL && !UNITY_EDITOR
        ReportBenchmarkResults(json);
#elif UNITY_IOS && !UNITY_EDITOR
        FirebaseTestLabiOS.WriteResults("benchmarkResults.json", json);
        FirebaseTestLabiOS.NotifyTestComplete();
        System.Threading.Thread.Sleep(500);
#elif UNITY_ANDROID && !UNITY_EDITOR
        FirebaseTestLabAndroid.WriteResults("benchmarkResults.json", json);
        FirebaseTestLabAndroid.NotifyTestComplete();
#endif

#if !UNITY_EDITOR && !UNITY_WEBGL
        Application.Quit(0);
#endif
    }

    #endregion
}
