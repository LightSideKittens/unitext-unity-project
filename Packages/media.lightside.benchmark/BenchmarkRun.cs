using System;
using UnityEngine;

namespace LightSide.Benchmark
{
    /// <summary>
    /// Identity of the current benchmark run — a UTC timestamp plus a monotonic counter, stamped fresh by
    /// <see cref="Begin"/> by the suite at the start of every run. Profiler
    /// captures are grouped under <c>Benchmarks/prof/{Id}/</c> by this id so each run's deep trees accumulate
    /// as history. The counter guarantees two runs never collide even within the same wall-clock second.
    /// </summary>
    public static class BenchmarkRun
    {
        static int counter;
        static string id;
        public static string Id => id ??= NewId();
        public static void Begin() => id = NewId();
        static string NewId() => $"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Application.platform}-{++counter}";
    }
}
