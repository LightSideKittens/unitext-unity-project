using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using LightSide;
using UnityEngine;

public class BenchmarkRunner : MonoBehaviour
{
    const float WatchdogTimeout = 1800f;
    const double InteractiveSelectionSeconds = 10.0;

    BenchmarkRunData data;
    bool suiteRunning;
    bool watchdogTriggered;
    bool requestedText;
    bool requestedGlyph;
    bool requestedMotion;
    bool runFailed;
    float suiteStartedAt;

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
        runner.StartCoroutine(runner.AutoStart());
#endif
    }
#endif

    /// <summary>Starts every suite unless another benchmark suite is already running.</summary>
    [ContextMenu("Run All Benchmarks")]
    public void RunFromMenu() => StartSuite(runText: true, runGlyph: true, runMotion: true);

    /// <summary>Starts only the text-pipeline suite unless another benchmark suite is already running.</summary>
    [ContextMenu("Run Text Pipeline Only")]
    public void RunTextFromMenu() => StartSuite(runText: true, runGlyph: false, runMotion: false);

    /// <summary>Starts only the glyph suite unless another suite is already running.</summary>
    [ContextMenu("Run Glyph Rasterization Only")]
    public void RunGlyphFromMenu() => StartSuite(runText: false, runGlyph: true, runMotion: false);

    /// <summary>Starts only the motion-engine suite unless another suite is already running.</summary>
    [ContextMenu("Run Motion Only")]
    public void RunMotionFromMenu() => StartSuite(runText: false, runGlyph: false, runMotion: true);

    IEnumerator AutoStart()
    {
        FirebaseTestLabAndroid.Initialize();
        FirebaseTestLabiOS.Initialize();
        var (runText, runGlyph, runMotion, explicitSuite) = ResolveLaunchSuite();
        if (!Application.isBatchMode && !explicitSuite)
        {
            Debug.Log($"[BenchmarkRunner] Waiting up to {InteractiveSelectionSeconds:F0} seconds before the unattended start; a Run button starts immediately.");
            double deadline = Time.realtimeSinceStartupAsDouble + InteractiveSelectionSeconds;
            while (!suiteRunning && Time.realtimeSinceStartupAsDouble < deadline)
                yield return null;
        }
        if (!suiteRunning)
            StartSuite(runText, runGlyph, runMotion);
    }

    /// <summary>
    /// Launch-time suite selection: <c>-unitextSuite text|glyph|motion|all</c> (or the <c>=</c> form) and env
    /// <c>UNITEXT_SUITE</c> on desktop, the Firebase game-loop scenario on devices (1 = all, 2 = text
    /// pipeline, 3 = glyph rasterization, 4 = motion), and the page URL's <c>?suite=</c> query on WebGL.
    /// An explicit selection skips the interactive wait so CI starts immediately.
    /// </summary>
    static (bool runText, bool runGlyph, bool runMotion, bool explicitSuite) ResolveLaunchSuite()
    {
        string suite = null;
        var args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length; i++)
        {
            if (string.Equals(args[i], "-unitextSuite", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                suite = args[i + 1];
            else if (args[i].StartsWith("-unitextSuite=", StringComparison.OrdinalIgnoreCase))
                suite = args[i].Substring("-unitextSuite=".Length);
        }
        suite ??= Environment.GetEnvironmentVariable("UNITEXT_SUITE");

        if (suite == null)
        {
            int scenario = FirebaseTestLabAndroid.ScenarioNumber;
            if (scenario <= 0) scenario = FirebaseTestLabiOS.ScenarioNumber;
            if (scenario == 2) suite = "text";
            else if (scenario == 3) suite = "glyph";
            else if (scenario == 4) suite = "motion";
            else if (scenario == 1) suite = "all";
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        if (suite == null && !string.IsNullOrEmpty(Application.absoluteURL))
        {
            var url = Application.absoluteURL;
            int q = url.IndexOf("suite=", StringComparison.OrdinalIgnoreCase);
            if (q >= 0)
            {
                int start = q + "suite=".Length;
                int end = url.IndexOfAny(new[] { '&', '#' }, start);
                suite = end < 0 ? url.Substring(start) : url.Substring(start, end - start);
            }
        }
#endif

        suite = suite?.Trim().ToLowerInvariant();
        if (suite != null)
            Debug.Log($"[BenchmarkRunner] Launch suite: {suite}");
        return suite switch
        {
            "text" => (true, false, false, true),
            "glyph" => (false, true, false, true),
            "motion" => (false, false, true, true),
            "all" => (true, true, true, true),
            _ => (true, true, true, false)
        };
    }

    void StartSuite(bool runText, bool runGlyph, bool runMotion)
    {
        if (suiteRunning) return;
        suiteRunning = true;
        watchdogTriggered = false;
        requestedText = runText;
        requestedGlyph = runGlyph;
        requestedMotion = runMotion;
        runFailed = false;
        suiteStartedAt = Time.realtimeSinceStartup;
        Debug.Log("[BenchmarkRunner] Starting benchmarks...");
        StartCoroutine(GuardedRunSuite(runText, runGlyph, runMotion));
    }

    IEnumerator GuardedRunSuite(bool runText, bool runGlyph, bool runMotion)
    {
        var routine = new OwnedEnumerator(RunSuite(runText, runGlyph, runMotion));
        Exception failure = null;
        bool cleanupFailure = false;
        bool completed = false;
        try
        {
            while (true)
            {
                if (!routine.MoveNext(out var current, out failure, out cleanupFailure))
                    break;
                yield return current;
            }
            completed = failure == null && routine.Completed;
        }
        finally
        {
            failure = routine.Dispose(failure, ref cleanupFailure);
            if (!completed && failure == null)
                failure = new InvalidOperationException("Benchmark suite ended before completion.");
            if (failure != null)
            {
                EnsureRunData();
                data.errors.Add($"suite: {failure.Message}");
                Debug.LogError($"[BenchmarkRunner] Suite failed: {failure}");
            }
            bool successful = completed && failure == null && !watchdogTriggered && !runFailed && data.errors.Count == 0;
            if (!RequestedSuitesComplete(out var incompleteReason))
            {
                successful = false;
                EnsureRunData();
                data.errors.Add(incompleteReason);
                Debug.LogError($"[BenchmarkRunner] {incompleteReason}");
            }
            suiteRunning = false;
            PersistResults(successful);
        }
    }

    /// <summary>One combined result JSON is always written; <see cref="BenchmarkHistory"/> splits it into per-suite site streams, so a single-suite run persists only its selected stream.</summary>
    IEnumerator RunSuite(bool runText, bool runGlyph, bool runMotion)
    {
        data = new BenchmarkRunData
        {
            timestamp = DateTime.UtcNow.ToString("o")
        };
        FillMeta(data);

        for (int i = 0; i < 5; i++)
            yield return null;

        Debug.Log("[BenchmarkRunner] === BENCHMARK START ===");
        Debug.Log(BenchmarkJsonSerializer.EnvironmentSummary());

        if (runText)
            yield return RunTextBenchmarks();

        if (runGlyph && CheckWatchdog())
        {
            yield return EngineCooldown();
            WarnIfSceneNotSterile();
            yield return RunGlyphRasterizationBenchmarks();
        }

        if (runMotion && CheckWatchdog())
        {
            yield return EngineCooldown();
            yield return RunMotionBenchmarks();
        }
    }

    IEnumerator RunMotionBenchmarks()
    {
        var benchmark = ObjectUtils.FindAny<MotionBenchmark>();
        if (benchmark == null)
        {
            data.errors.Add("MotionBenchmark not found on scene");
            Debug.LogError("[BenchmarkRunner] MotionBenchmark not found on scene");
            yield break;
        }

        Debug.Log("[BenchmarkRunner] Running motion engines...");
        var previousResults = benchmark.Results;
        yield return SafeRun("motion",
            benchmark.RunBenchmarkCoroutine,
            () => data.motionBenchmarks = benchmark.Results,
            () =>
            {
                if (!ReferenceEquals(benchmark.Results, previousResults))
                    data.motionBenchmarks = benchmark.Results;
            });
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
            data.memoryProbeRepeats = cfg != null ? cfg.memoryProbeRepeats : uniTextBench.memoryProbeRepeats;

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
        var fontSelector = ObjectUtils.FindAny<BenchmarkFontSelector>();
        if (fontSelector != null && fontSelector.Fonts.Count > 0)
        {
            foreach (var pair in fontSelector.Fonts)
            {
                fontSelector.Apply(pair);
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
        GlyphRasterBenchmarkBase.CurrentFontLabel = font;
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

    IEnumerator SafeRun(string name, Func<IEnumerator> coroutineFactory, Action onComplete,
        Action onFailure = null)
    {
        IEnumerator coroutine = null;
        Exception startFailure = null;
        try
        {
            coroutine = coroutineFactory();
        }
        catch (Exception e)
        {
            startFailure = e;
        }
        if (startFailure != null)
        {
            runFailed = true;
            data.errors.Add($"{name}: {startFailure.Message}");
            Debug.LogError($"[BenchmarkRunner] Failed to start {name}: {startFailure}");
            yield break;
        }
        if (coroutine == null)
        {
            runFailed = true;
            var failure = new InvalidOperationException($"{name} returned no coroutine.");
            RecordRunFailure(name, failure);
            yield break;
        }

        var routine = new OwnedEnumerator(coroutine);
        bool succeeded = false;
        bool failureRecorded = false;
        Exception caught = null;
        bool cleanupFailure = false;
        try
        {
            while (true)
            {
                if (!CheckWatchdog())
                {
                    data.errors.Add($"{name}: watchdog timeout");
                    runFailed = true;
                    break;
                }

                if (!routine.MoveNext(out var current, out caught, out var moveCleanupFailure))
                {
                    cleanupFailure |= moveCleanupFailure;
                    if (caught == null)
                        succeeded = true;
                    break;
                }
                yield return current;
            }
        }
        finally
        {
            caught = routine.Dispose(caught, ref cleanupFailure);
            if (caught != null && !failureRecorded)
            {
                RecordRunFailure(name, caught);
                failureRecorded = true;
            }
            if (!succeeded)
                runFailed = true;
            var collect = succeeded ? onComplete : onFailure;
            if (collect != null)
            {
                if (!CollectResults(name, collect))
                    runFailed = true;
            }
            if (cleanupFailure && caught != null)
                throw caught is BenchmarkCleanupException
                    ? caught
                    : new BenchmarkCleanupException($"{name} cleanup failed.", caught);
        }
    }

    bool CollectResults(string name, Action collect)
    {
        try
        {
            collect();
            return true;
        }
        catch (Exception exception)
        {
            data.errors.Add($"{name} result collection: {exception.Message}");
            Debug.LogError($"[BenchmarkRunner] Failed to collect {name} results: {exception}");
            return false;
        }
    }

    void RecordRunFailure(string name, Exception exception)
    {
        data.errors.Add($"{name}: {exception.Message}");
        Debug.LogError($"[BenchmarkRunner] {name} failed: {exception}");
    }

    bool CheckWatchdog()
    {
        if (Time.realtimeSinceStartup - suiteStartedAt > WatchdogTimeout)
        {
            if (!watchdogTriggered)
            {
                watchdogTriggered = true;
                Debug.LogWarning($"[BenchmarkRunner] Watchdog timeout ({WatchdogTimeout}s), writing partial results");
                data.errors.Add($"Watchdog timeout at {Time.realtimeSinceStartup:F0}s");
            }
            return false;
        }
        return true;
    }

    bool RequestedSuitesComplete(out string reason)
    {
        EnsureRunData();
        var missing = new List<string>();
        if (requestedText && data.textBenchmarks.Count == 0)
            missing.Add("text");
        if (requestedGlyph)
        {
            bool hasMeasuredGlyph = false;
            foreach (var engine in data.glyphRasterization.Values)
            {
                foreach (var result in engine.Values)
                {
                    hasMeasuredGlyph |= result.status == "measured";
                    if (result.status == "failed" || result.status == "partial" ||
                        result.status == "mismatch" || result.status == "measuring")
                    {
                        reason = $"Requested glyph suite ended with result status '{result.status}'.";
                        return false;
                    }
                }
            }
            if (!hasMeasuredGlyph)
                missing.Add("glyph");
        }
        if (requestedMotion)
        {
            bool hasMeasuredEngine = false;
            if (data.motionBenchmarks != null)
            {
                foreach (var engine in data.motionBenchmarks.engines.Values)
                {
                    hasMeasuredEngine |= engine.status == "measured";
                    if (engine.status == "failed" || engine.status == "partial" || engine.status == "measuring")
                    {
                        reason = $"Requested motion suite ended with engine status '{engine.status}'.";
                        return false;
                    }
                }
            }
            if (!hasMeasuredEngine)
                missing.Add("motion");
        }

        if (missing.Count == 0)
        {
            reason = null;
            return true;
        }
        reason = $"Requested benchmark suite data is missing: {string.Join(", ", missing)}.";
        return false;
    }

    void ApplyConfig(TextBenchmarkBase bench)
    {
        bench.objectCount = data.objectCount;
        bench.iterations = data.iterations;
        bench.warmupIterations = data.warmupIterations;
        bench.memoryProbeRepeats = data.memoryProbeRepeats;
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
#else
        ApplyBakedBuildInfo(data);
#endif
    }

    /// <summary>Player builds have no git and cannot see the runner's env; <see cref="BenchmarkBuildStamp"/> baked the commit into Resources at build time.</summary>
    static void ApplyBakedBuildInfo(BenchmarkRunData data)
    {
        var asset = Resources.Load<TextAsset>(BenchmarkBuildInfo.ResourceName);
        if (asset == null) return;

        var info = JsonUtility.FromJson<BenchmarkBuildInfo>(asset.text);
        if (info == null) return;

        if (!string.IsNullOrEmpty(info.commit)) data.commit = info.commit;
        if (!string.IsNullOrEmpty(info.branch)) data.branch = info.branch;
        data.dirty = info.dirty;
        if (!string.IsNullOrEmpty(info.submoduleCommit)) data.submoduleCommit = info.submoduleCommit;
        if (!string.IsNullOrEmpty(info.submoduleBranch)) data.submoduleBranch = info.submoduleBranch;
        data.submoduleDirty = info.submoduleDirty;
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

    void EnsureRunData()
    {
        data ??= new BenchmarkRunData
        {
            timestamp = DateTime.UtcNow.ToString("o")
        };
    }

    void PersistResults(bool completed)
    {
        EnsureRunData();
        try
        {
            var json = BenchmarkJsonSerializer.Serialize(data, out var postRunSummary);
            Debug.Log(completed
                ? "[BenchmarkRunner] === BENCHMARK COMPLETE ==="
                : "[BenchmarkRunner] === PARTIAL BENCHMARK RESULTS ===");
            Debug.Log(postRunSummary);
            OutputResults(json, completed ? 0 : 1);
        }
        catch (Exception exception)
        {
            Debug.LogError($"[BenchmarkRunner] Failed to persist benchmark results: {exception}");
#if !UNITY_EDITOR && !UNITY_WEBGL
            Application.Quit(1);
#endif
        }
    }

    #region Output

#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void ReportBenchmarkResults(string json);
#endif

    void OutputResults(string json, int exitCode)
    {
        var jsonPath = Path.Combine(Application.persistentDataPath, "benchmarkResults.json");
        File.WriteAllText(jsonPath, json);
        Debug.Log($"[BenchmarkRunner] Results saved to: {jsonPath}");

        var streams = WriteSiteStreams(json);

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
        FirebaseTestLabAndroid.WriteResultsArchive(BuildBenchmarkArchive(json, streams), "benchmarkResults.zip");
        FirebaseTestLabAndroid.NotifyTestComplete();
#endif

#if !UNITY_EDITOR && !UNITY_WEBGL
        Application.Quit(exitCode);
#endif
    }

    /// <summary>
    /// Android game-loop collects exactly ONE output file (the intent fd), so the run's whole artifact
    /// set — combined JSON, per-suite viewer streams, benchmark screenshots — ships as one zip that CI
    /// unpacks (the same channel golden tests use for their screenshots).
    /// </summary>
    static byte[] BuildBenchmarkArchive(string json, List<(string fileName, string contents)> streams)
    {
        using var ms = new MemoryStream();
        using (var zip = new System.IO.Compression.ZipArchive(ms, System.IO.Compression.ZipArchiveMode.Create, true))
        {
            using (var w = new StreamWriter(zip.CreateEntry("benchmarkResults.json").Open()))
                w.Write(json);

            foreach (var s in streams)
                using (var w = new StreamWriter(zip.CreateEntry("benchmark-streams/" + s.fileName).Open()))
                    w.Write(s.contents);

            var screenshotsDir = Path.Combine(Application.persistentDataPath, "Screenshots");
            if (Directory.Exists(screenshotsDir))
            {
                foreach (var png in Directory.GetFiles(screenshotsDir, "*.png"))
                {
                    using var entry = zip.CreateEntry("screenshots/" + Path.GetFileName(png)).Open();
                    var bytes = File.ReadAllBytes(png);
                    entry.Write(bytes, 0, bytes.Length);
                }
            }

            var logsDir = Path.Combine(Application.persistentDataPath, "Logs");
            if (Directory.Exists(logsDir))
            {
                foreach (var logFile in Directory.GetFiles(logsDir, "*.log"))
                {
                    try
                    {
                        byte[] bytes;
                        using (var stream = new FileStream(logFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                        {
                            bytes = new byte[stream.Length];
                            int read = stream.Read(bytes, 0, bytes.Length);
                            if (read != bytes.Length) System.Array.Resize(ref bytes, read);
                        }
                        using var entry = zip.CreateEntry("logs/" + Path.GetFileName(logFile)).Open();
                        entry.Write(bytes, 0, bytes.Length);
                    }
                    catch (IOException) { }
                }
            }
        }
        return ms.ToArray();
    }

    /// <summary>Device parity for the viewer files: the editor persists split streams to Benchmarks/runs via <see cref="BenchmarkHistory"/>, so player builds emit them next to benchmarkResults.json in persistentDataPath — ready to drop into Benchmarks/runs. iOS additionally copies them into the game-loop results dir; Android bundles them into the game-loop archive.</summary>
    List<(string fileName, string contents)> WriteSiteStreams(string json)
    {
        var written = new List<(string fileName, string contents)>();
        if (Application.isEditor) return written;
        try
        {
            var stamp = BenchmarkStreams.Stamp(Application.platform.ToString(), SystemInfo.deviceName);
            foreach (var f in BenchmarkStreams.Split(json, stamp))
            {
                var path = Path.Combine(Application.persistentDataPath, f.fileName);
                File.WriteAllText(path, f.contents);
                written.Add((f.fileName, f.contents));
#if UNITY_IOS && !UNITY_EDITOR
                FirebaseTestLabiOS.WriteResults(f.fileName, f.contents);
#endif
                Debug.Log($"[BenchmarkRunner] Site stream saved: {path}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[BenchmarkRunner] Failed to write site streams: {e}");
        }
        return written;
    }

    #endregion
}

sealed class OwnedEnumerator
{
    readonly Stack<IEnumerator> stack = new();

    internal OwnedEnumerator(IEnumerator root) => stack.Push(root);

    internal bool Completed => stack.Count == 0;

    internal bool MoveNext(out object current, out Exception failure, out bool cleanupFailure)
    {
        current = null;
        failure = null;
        cleanupFailure = false;
        while (stack.Count != 0)
        {
            var routine = stack.Peek();
            bool moved;
            try
            {
                moved = routine.MoveNext();
                if (moved) current = routine.Current;
            }
            catch (Exception exception)
            {
                failure = exception;
                cleanupFailure = exception is BenchmarkCleanupException;
                return false;
            }

            if (!moved)
            {
                stack.Pop();
                try
                {
                    (routine as IDisposable)?.Dispose();
                }
                catch (Exception exception)
                {
                    failure = exception;
                    cleanupFailure = true;
                    return false;
                }
                continue;
            }
            if (current is IEnumerator nested && current is not CustomYieldInstruction)
            {
                stack.Push(nested);
                current = null;
                continue;
            }
            return true;
        }
        return false;
    }

    internal Exception Dispose(Exception failure, ref bool cleanupFailure)
    {
        while (stack.Count != 0)
        {
            var routine = stack.Pop();
            try
            {
                (routine as IDisposable)?.Dispose();
            }
            catch (Exception exception)
            {
                cleanupFailure = true;
                failure = failure == null ? exception : new AggregateException(failure, exception);
            }
        }
        return failure;
    }
}

sealed class BenchmarkCleanupException : Exception
{
    internal BenchmarkCleanupException(string message, Exception innerException)
        : base($"{message} {innerException.Message}", innerException)
    {
    }
}

static class BenchmarkCleanup
{
    internal static Exception Capture(Exception failure, Action cleanup)
    {
        try
        {
            cleanup();
        }
        catch (Exception exception)
        {
            return failure == null ? exception : new AggregateException(failure, exception);
        }
        return failure;
    }
}
