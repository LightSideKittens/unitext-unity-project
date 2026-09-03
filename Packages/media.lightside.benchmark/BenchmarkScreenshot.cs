using System;
using System.Collections;
using UnityEngine;

namespace LightSide.Benchmark
{
    /// <summary>
    /// Screenshots for benchmark artifacts — one capture after each text appearance, strictly
    /// OUTSIDE every measured window (callers invoke it only after all timing/alloc bookkeeping of the
    /// pass is read). Capturing is an optional artifact channel: with no <see cref="Capturer"/> registered
    /// a run produces no images and measures exactly the same.
    /// Enabled by default in players, disabled in the editor; BENCHMARK_SCREENSHOTS=1/0 overrides both.
    /// </summary>
    public static class BenchmarkScreenshot
    {
        static readonly bool enabled;
        static int ordinal;

        static BenchmarkScreenshot()
        {
            var env = Environment.GetEnvironmentVariable("BENCHMARK_SCREENSHOTS");
            enabled = env != null ? env != "0" : !Application.isEditor;
        }

        /// <summary>
        /// Writes one capture under the given name. Registered by the hosting project, which owns the
        /// camera path and the artifact channel CI collects; the harness only decides when to call it.
        /// </summary>
        public static Action<string> Capturer { get; set; }

        /// <summary>Names are prefixed with a run-wide ordinal so artifacts sort in execution order and repeated passes never overwrite each other.</summary>
        public static IEnumerator Capture(string name)
        {
            if (!enabled || Capturer == null || Application.isBatchMode) yield break;
            yield return new WaitForEndOfFrame();
            Capturer($"bench-{ordinal++:D3}-{BenchmarkStreams.Sanitize(name)}");
        }
    }
}
