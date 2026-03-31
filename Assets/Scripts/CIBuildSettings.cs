#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class CIBuildSettings
{
    private const string TestScenePath = "Assets/UniText.Test/TestWorkshop/UniTextTest.unity";
    private const string BenchmarkScenePath = "Assets/UniText.Test/BenchmarkWorkshop/UniText_BenchmarkTest.unity";
    private const string GlyphRasterizationBenchmarkScenePath = "Assets/UniText.Test/BenchmarkWorkshop/GlyphRasterization_BenchmarkTest.unity";

    static readonly BuildTargetGroup[] AllTargets =
    {
        BuildTargetGroup.Standalone,
        BuildTargetGroup.Android,
        BuildTargetGroup.iOS,
        BuildTargetGroup.WebGL
    };

    private const string ConfiguredKey = "CIBuildSettings_Configured";

    static CIBuildSettings()
    {
        if (SessionState.GetBool(ConfiguredKey, false))
            return;

        var args = Environment.GetCommandLineArgs();
        var debugArg = GetCommandLineArg(args, "-ciDebug");
        var benchmarkArg = GetCommandLineArg(args, "-ciBenchmark");
        var testsArg = GetCommandLineArg(args, "-ciTests");

        if (debugArg == null && benchmarkArg == null && testsArg == null)
            return;

        SessionState.SetBool(ConfiguredKey, true);

        ConfigureBuild();
        UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();
    }
    
        public static void ConfigureBuild()
    {
        var args = Environment.GetCommandLineArgs();
        var debugArg = GetCommandLineArg(args, "-ciDebug");
        var benchmarkArg = GetCommandLineArg(args, "-ciBenchmark");
        var testsArg = GetCommandLineArg(args, "-ciTests");

        Debug.Log($"[CIBuildSettings] -ciDebug={debugArg ?? "null"}, -ciBenchmark={benchmarkArg ?? "null"}, -ciTests={testsArg ?? "null"}");

        if (debugArg == null && benchmarkArg == null && testsArg == null)
        {
            Debug.Log("[CIBuildSettings] Not in CI environment, skipping configuration");
            return;
        }

        if (testsArg == "true")
        {
            EnableTests();
            ConfigureIOSForDevice();
        }
        else
        {
            DisableTests();
            ConfigureIOSForDevice();
        }

        SetHighStripping();
        SetWebGLExceptions(debugArg == "true");

        var isBenchmark = benchmarkArg == "true"
;
        if (isBenchmark)
        {
            SetBuildScenes(BenchmarkScenePath, GlyphRasterizationBenchmarkScenePath);
            EnableBenchmark();
            DisableTests();
            DisableDebug();
            Debug.Log("[CIBuildSettings] Benchmark build configured (2 scenes)");
        }
        else
        {
            DisableBenchmark();
            SetBuildScene(TestScenePath);

            if (debugArg == "true")
            {
                EnableDebug();
            }
            else
            {
                DisableDebug();
            }
        }

        Debug.Log("[CIBuildSettings] Build configured successfully");
    }

    private static string GetCommandLineArg(string[] args, string argName)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == argName)
                return args[i + 1];
        }
        return null;
    }

    [MenuItem("UniText/CI/Set Build Scene - Test")]
    public static void SetTestScene() => SetBuildScene(TestScenePath);

    [MenuItem("UniText/CI/Set Build Scene - Benchmark")]
    public static void SetBenchmarkScene() => SetBuildScenes(BenchmarkScenePath, GlyphRasterizationBenchmarkScenePath);

    private static void SetBuildScene(string scenePath)
    {
        SetBuildScenes(scenePath);
    }

    private static void SetBuildScenes(params string[] scenePaths)
    {
        var scenes = new EditorBuildSettingsScene[scenePaths.Length];
        for (int i = 0; i < scenePaths.Length; i++)
            scenes[i] = new EditorBuildSettingsScene(scenePaths[i], true);
        EditorBuildSettings.scenes = scenes;

        Debug.Log($"[CIBuildSettings] Build scenes set to: {string.Join(", ", scenePaths)}");
    }

    [MenuItem("UniText/CI/Set High Stripping")]
    public static void SetHighStripping()
    {
        foreach (var target in AllTargets)
        {
            PlayerSettings.SetManagedStrippingLevel(target, ManagedStrippingLevel.High);
        }

        Debug.Log("[CIBuildSettings] Managed Stripping Level set to High for all platforms");
    }

    [MenuItem("UniText/CI/Enable UNITEXT_DEBUG Symbol")]
    public static void EnableDebug() => SetDefineSymbol("UNITEXT_DEBUG", true);

    [MenuItem("UniText/CI/Disable UNITEXT_DEBUG Symbol")]
    public static void DisableDebug() => SetDefineSymbol("UNITEXT_DEBUG", false);

    [MenuItem("UniText/CI/Enable UNITEXT_TESTS Symbol")]
    public static void EnableTests() => SetDefineSymbol("UNITEXT_TESTS", true);

    [MenuItem("UniText/CI/Disable UNITEXT_TESTS Symbol")]
    public static void DisableTests() => SetDefineSymbol("UNITEXT_TESTS", false);

    [MenuItem("UniText/CI/Enable UNITEXT_BENCHMARK Symbol")]
    public static void EnableBenchmark() => SetDefineSymbol("UNITEXT_BENCHMARK", true);

    [MenuItem("UniText/CI/Disable UNITEXT_BENCHMARK Symbol")]
    public static void DisableBenchmark() => SetDefineSymbol("UNITEXT_BENCHMARK", false);

    private static void SetDefineSymbol(string symbol, bool enabled)
    {
        foreach (var target in AllTargets)
        {
            var defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(target);

            if (enabled)
            {
                if (!defines.Contains(symbol))
                {
                    defines = string.IsNullOrEmpty(defines) ? symbol : defines + ";" + symbol;
                    PlayerSettings.SetScriptingDefineSymbolsForGroup(target, defines);
                }
            }
            else
            {
                if (defines.Contains(symbol))
                {
                    defines = defines.Replace(";" + symbol, "").Replace(symbol + ";", "").Replace(symbol, "");
                    PlayerSettings.SetScriptingDefineSymbolsForGroup(target, defines);
                }
            }
        }

        Debug.Log($"[CIBuildSettings] {symbol} {(enabled ? "added to" : "removed from")} all platforms");
    }

    private static void SetWebGLExceptions(bool withStacktrace)
    {
        var level = withStacktrace
            ? WebGLExceptionSupport.FullWithStacktrace
            : WebGLExceptionSupport.FullWithoutStacktrace;
        PlayerSettings.WebGL.exceptionSupport = level;
        Debug.Log($"[CIBuildSettings] WebGL exceptions set to {level}");
    }

    private static void ConfigureIOSForSimulator()
    {
        PlayerSettings.iOS.sdkVersion = iOSSdkVersion.SimulatorSDK;

#if UNITY_2022_1_OR_NEWER
        PlayerSettings.iOS.simulatorSdkArchitecture = AppleMobileArchitectureSimulator.X86_64;
        Debug.Log("[CIBuildSettings] iOS SDK set to SimulatorSDK (x86_64)");
#else
        Debug.Log("[CIBuildSettings] iOS SDK set to SimulatorSDK");
#endif
    }

    private static void ConfigureIOSForDevice()
    {
        PlayerSettings.iOS.sdkVersion = iOSSdkVersion.DeviceSDK;
        Debug.Log("[CIBuildSettings] iOS SDK set to DeviceSDK");
    }
}
#endif
