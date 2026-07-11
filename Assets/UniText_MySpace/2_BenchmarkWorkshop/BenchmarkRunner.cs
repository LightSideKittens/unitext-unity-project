using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using LightSide;
using UnityEngine;

public class BenchmarkRunner : MonoBehaviour
{
    const float WatchdogTimeout = 600f;

    BenchmarkRunData data;

#if UNITEXT_BENCHMARK
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void OnRuntimeStart()
    {
        Debug.Log("[BenchmarkRunner] OnRuntimeStart called");

        var runner = ObjectUtils.FindAny<BenchmarkRunner>();
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
        FillMeta(data);

        for (int i = 0; i < 5; i++)
            yield return null;

        Debug.Log("[BenchmarkRunner] === BENCHMARK START ===");

        yield return RunTextBenchmarks();

        if (CheckWatchdog())
        {
            yield return EngineCooldown();
            WarnIfSceneNotSterile();
            yield return RunGlyphRasterizationBenchmarks();
        }

        Debug.Log("[BenchmarkRunner] === BENCHMARK COMPLETE ===");

        var json = BenchmarkJsonSerializer.Serialize(data);
        OutputResults(json);
    }

    IEnumerator RunTextBenchmarks()
    {
        var cfg = BenchmarkConfig.Instance;
        var uniTextBench = ObjectUtils.FindAny<UniTextBenchmark>();
        if (uniTextBench != null)
        {
            data.objectCount = cfg != null ? cfg.objectCount : uniTextBench.objectCount;
            data.iterations = cfg != null ? cfg.iterations : uniTextBench.iterations;
            data.warmupIterations = cfg != null ? cfg.warmupIterations : uniTextBench.warmupIterations;

            Debug.Log("[BenchmarkRunner] Running UniText (Single-Threaded)...");
            yield return SafeRun("unitextSingleThreaded",
                () => uniTextBench.RunBenchmarkCoroutine(silent: true, parallel: false),
                () => data.textBenchmarks["unitextSingleThreaded"] = uniTextBench.Results);

            if (!CheckWatchdog()) yield break;
            yield return EngineCooldown();

            Debug.Log("[BenchmarkRunner] Running UniText (Parallel)...");
            yield return SafeRun("unitextParallel",
                () => uniTextBench.RunBenchmarkCoroutine(silent: true, parallel: true),
                () => data.textBenchmarks["unitextParallel"] = uniTextBench.Results);

            if (!CheckWatchdog()) yield break;
            yield return EngineCooldown();
        }
        else
        {
            data.errors.Add("UniTextBenchmark not found on scene");
            Debug.LogWarning("[BenchmarkRunner] UniTextBenchmark not found");
        }

        var tmpBench = ObjectUtils.FindAny<TMPBenchmark>();
        if (tmpBench != null)
        {
            ApplyConfig(tmpBench);
            Debug.Log("[BenchmarkRunner] Running TMP...");
            yield return SafeRun("tmp",
                () => tmpBench.RunBenchmarkCoroutine(silent: true),
                () => data.textBenchmarks["tmp"] = tmpBench.Results);

            if (!CheckWatchdog()) yield break;
            yield return EngineCooldown();
        }
        else
        {
            data.errors.Add("TMPBenchmark not found on scene");
            Debug.LogWarning("[BenchmarkRunner] TMPBenchmark not found");
        }

        var uitkBench = ObjectUtils.FindAny<UIToolkitBenchmark>();
        if (uitkBench != null)
        {
            ApplyConfig(uitkBench);

            Debug.Log("[BenchmarkRunner] Running UIToolkit...");
            yield return SafeRun("uiToolkit",
                () => uitkBench.RunBenchmarkCoroutine(silent: true),
                () => data.textBenchmarks["uiToolkit"] = uitkBench.Results);

            if (!CheckWatchdog()) yield break;
        }
        else
        {
            Debug.LogWarning("[BenchmarkRunner] UIToolkitBenchmark not found (optional)");
        }

        if (runLatinCorpus)
            yield return RunLatinCorpusPass(uniTextBench, tmpBench, uitkBench);
    }

    /// <summary>Glyph benchmarks count global atlas deltas — any enabled text component left over from the text phase deflates them via warm cache hits.</summary>
    void WarnIfSceneNotSterile()
    {
        int live = ObjectUtils.FindAll<UniTextBase>().Length
                 + ObjectUtils.FindAll<TMPro.TMP_Text>().Length;
        if (live == 0) return;
        data.errors.Add($"{live} enabled text component(s) alive before glyph phase — counts may be skewed");
        Debug.LogWarning($"[BenchmarkRunner] {live} enabled text component(s) alive before glyph phase");
    }

    IEnumerator RunGlyphRasterizationBenchmarks()
    {
        var selector = ObjectUtils.FindAny<BenchmarkFontSelector>();
        if (selector != null && selector.Fonts.Count > 0)
        {
            foreach (var pair in selector.Fonts)
            {
                selector.Apply(pair);
                yield return null;
                yield return RunGlyphForFont(pair.Name);
                if (!CheckWatchdog()) yield break;
            }
        }
        else
        {
            yield return RunGlyphForFont("default");
        }
    }

    IEnumerator RunGlyphForFont(string font)
    {
        var uniGlyph = ObjectUtils.FindAny<UniText_GlyphRasterizationBenchmark>();
        if (uniGlyph != null)
        {
            var variants = new (string key, bool singleThreaded, bool maxStroke)[]
            {
                ("unitextSingleThreaded", true, false),
                ("unitextParallel", false, false),
                ("unitextSingleThreadedMaxStroke", true, true),
                ("unitextParallelMaxStroke", false, true),
            };
            foreach (var v in variants)
            {
                Debug.Log($"[BenchmarkRunner] Running UniText Glyph Rasterization ({v.key}, {font})...");
                yield return SafeRun($"unitextGlyph.{v.key}.{font}",
                    () => uniGlyph.RunBenchmarkCoroutine(v.singleThreaded, v.maxStroke),
                    () => StoreGlyph(v.key, font, uniGlyph.LastResults));
                if (!CheckWatchdog()) yield break;
            }
        }

        var tmpGlyph = ObjectUtils.FindAny<TMP_GlyphRasterizationBenchmark>();
        if (tmpGlyph != null)
        {
            Debug.Log($"[BenchmarkRunner] Running TMP Glyph Rasterization ({font})...");
            yield return SafeRun($"tmpGlyph.{font}",
                () => tmpGlyph.RunBenchmarkCoroutine(),
                () => StoreGlyph("tmp", font, tmpGlyph.LastResults));
            if (!CheckWatchdog()) yield break;
        }

        var uitkGlyph = ObjectUtils.FindAny<UIToolkit_GlyphRasterizationBenchmark>();
        if (uitkGlyph != null)
        {
            Debug.Log($"[BenchmarkRunner] Running UIToolkit Glyph Rasterization ({font})...");
            yield return SafeRun($"uiToolkitGlyph.{font}",
                () => uitkGlyph.RunBenchmarkCoroutine(),
                () => StoreGlyph("uiToolkit", font, uitkGlyph.LastResults));
        }
    }

    void StoreGlyph(string engineKey, string font, GlyphRasterData result)
    {
        if (result == null) return;
        if (!data.glyphRasterization.TryGetValue(engineKey, out var byFont))
            data.glyphRasterization[engineKey] = byFont = new Dictionary<string, GlyphRasterData>();
        byFont[font] = result;
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

    /// <summary>Second pass over every engine with the plain-Latin corpus — the apples-to-apples case (result keys get a ".latin" suffix). Creation/destruction is skipped there: it does not depend on text content.</summary>
    public bool runLatinCorpus = true;

    IEnumerator RunLatinCorpusPass(UniTextBenchmark uniTextBench, TMPBenchmark tmpBench, UIToolkitBenchmark uitkBench)
    {
        Debug.Log("[BenchmarkRunner] === LATIN CORPUS PASS ===");
        var latin = BenchmarkConfig.Latin;

        if (uniTextBench != null)
        {
            uniTextBench.corpusName = "latin";
            uniTextBench.corpusOverrideText = latin;
            var wasCreation = uniTextBench.runCreationDestructionTest;
            uniTextBench.runCreationDestructionTest = false;

            yield return EngineCooldown();
            yield return SafeRun("unitextSingleThreaded.latin",
                () => uniTextBench.RunBenchmarkCoroutine(silent: true, parallel: false),
                () => data.textBenchmarks["unitextSingleThreaded.latin"] = uniTextBench.Results);
            if (!CheckWatchdog()) yield break;

            yield return EngineCooldown();
            yield return SafeRun("unitextParallel.latin",
                () => uniTextBench.RunBenchmarkCoroutine(silent: true, parallel: true),
                () => data.textBenchmarks["unitextParallel.latin"] = uniTextBench.Results);
            if (!CheckWatchdog()) yield break;

            uniTextBench.corpusOverrideText = null;
            uniTextBench.corpusName = "multilingual";
            uniTextBench.runCreationDestructionTest = wasCreation;
        }

        if (tmpBench != null)
        {
            tmpBench.corpusName = "latin";
            tmpBench.corpusOverrideText = latin;
            var wasCreation = tmpBench.runCreationDestructionTest;
            tmpBench.runCreationDestructionTest = false;

            yield return EngineCooldown();
            yield return SafeRun("tmp.latin",
                () => tmpBench.RunBenchmarkCoroutine(silent: true),
                () => data.textBenchmarks["tmp.latin"] = tmpBench.Results);
            if (!CheckWatchdog()) yield break;

            tmpBench.corpusOverrideText = null;
            tmpBench.corpusName = "multilingual";
            tmpBench.runCreationDestructionTest = wasCreation;
        }

        if (uitkBench != null)
        {
            uitkBench.corpusName = "latin";
            uitkBench.corpusOverrideText = latin;
            var wasCreation = uitkBench.runCreationDestructionTest;
            uitkBench.runCreationDestructionTest = false;

            yield return EngineCooldown();
            yield return SafeRun("uiToolkit.latin",
                () => uitkBench.RunBenchmarkCoroutine(silent: true),
                () => data.textBenchmarks["uiToolkit.latin"] = uitkBench.Results);
            if (!CheckWatchdog()) yield break;

            uitkBench.corpusOverrideText = null;
            uitkBench.corpusName = "multilingual";
            uitkBench.runCreationDestructionTest = wasCreation;
        }
    }

    /// <summary>
    /// Run provenance: UTC stamp, commit/branch (git in the editor, GITHUB_* env on CI runners),
    /// dirty flag and origin — the history viewer separates publishable numbers from tainted ones by these.
    /// </summary>
    static void FillMeta(BenchmarkRunData data)
    {
        data.utc = DateTime.UtcNow.ToString("o");
        data.commit = Environment.GetEnvironmentVariable("GITHUB_SHA") ?? "unknown";
        data.branch = Environment.GetEnvironmentVariable("GITHUB_REF_NAME") ?? "unknown";
        data.source = Application.isEditor ? "editor"
            : Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true" ? "ci"
            : "local-player";

#if UNITY_EDITOR
        data.commit = RunGit("rev-parse HEAD") ?? data.commit;
        data.branch = RunGit("rev-parse --abbrev-ref HEAD") ?? data.branch;
        data.dirty = GitDirty("diff-index --quiet HEAD");

        data.submoduleCommit = RunGit("-C Assets/UniText rev-parse HEAD") ?? data.submoduleCommit;
        data.submoduleBranch = RunGit("-C Assets/UniText rev-parse --abbrev-ref HEAD") ?? data.submoduleBranch;
        data.submoduleDirty = GitDirty("-C Assets/UniText diff-index --quiet HEAD");
#endif
    }

#if UNITY_EDITOR
    static string RunGit(string args)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo("git", args)
            {
                WorkingDirectory = Directory.GetParent(Application.dataPath).FullName,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var p = System.Diagnostics.Process.Start(psi);
            var output = p.StandardOutput.ReadToEnd().Trim();
            p.WaitForExit(3000);
            return p.ExitCode == 0 && output.Length > 0 ? output : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Working-tree dirtiness via <c>git diff-index --quiet HEAD</c> (exit 1 = tracked changes present).
    /// Used instead of <c>git status --porcelain</c> because status scans the whole tree (~3s on a large
    /// Unity repo) while diff-index short-circuits on the first change (~40ms). Untracked-only changes do
    /// not count as dirty. Bounded by a timeout + kill so a stalled git can never hang the run.
    /// </summary>
    static bool GitDirty(string args)
    {
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo("git", args)
            {
                WorkingDirectory = Directory.GetParent(Application.dataPath).FullName,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var p = System.Diagnostics.Process.Start(psi);
            if (!p.WaitForExit(3000)) { try { p.Kill(); } catch { } return false; }
            return p.ExitCode == 1;
        }
        catch
        {
            return false;
        }
    }
#endif

    /// <summary>
    /// Heap normalization between engine runs: without it, later engines inherit the previous run's
    /// heap debt and pending finalizers, contaminating GC counts and alloc deltas in run order.
    /// </summary>
    static IEnumerator EngineCooldown()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        for (var i = 0; i < 10; i++) yield return null;
    }

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

#if UNITY_EDITOR
        BenchmarkHistory.SaveRun(json);
#endif
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
