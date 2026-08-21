using LightSide.Benchmark;
using UnityEngine;

/// <summary>
/// Hands the shared benchmark harness what only this project can supply: UniText's own build
/// configuration for the run's <c>systemInfo</c>, and the golden-test camera path used to capture
/// benchmark screenshots.
/// </summary>
public static class UniTextBenchmarkEnvironment
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Register()
    {
        BenchmarkScreenshot.Capturer = name => TestScreenshot.Capture(name);

        BenchmarkEnvironment.ExtraSystemInfo["unitextDebugDefine"] =
#if UNITEXT_DEBUG
            true;
#else
            false;
#endif
        BenchmarkEnvironment.ExtraSystemInfo["unitextProfileDefine"] =
#if UNITEXT_PROFILE
            true;
#else
            false;
#endif
    }
}
