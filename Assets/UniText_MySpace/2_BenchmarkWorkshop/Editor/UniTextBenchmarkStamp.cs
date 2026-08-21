using LightSide.Benchmark;
using UnityEditor;

/// <summary>
/// Points the build-time provenance stamp at the UniText package submodule, so a device run's result
/// document carries the package revision beside the host project's.
/// </summary>
public static class UniTextBenchmarkStamp
{
    [InitializeOnLoadMethod]
    static void Register() => BenchmarkBuildStamp.SubmodulePath = "Packages/media.lightside.unitext";
}
