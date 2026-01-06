#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class CIBuildSettings
{
    private const string TestScenePath = "Assets/UniText.Test/TestWorkshop/UniTextTest.unity";
    private const string BenchmarkScenePath = "Assets/UniText.Test/BenchmarkWorkshop/UniText_BenchmarkTest.unity";

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

        var isBenchmark = benchmarkArg == "true";

        if (isBenchmark)
        {
            SetBuildScene(BenchmarkScenePath);
            DisableDebug();
            Debug.Log("[CIBuildSettings] Benchmark build configured");
        }
        else
        {
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
    public static void SetBenchmarkScene() => SetBuildScene(BenchmarkScenePath);

    private static void SetBuildScene(string scenePath)
    {
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(scenePath, true)
        };

        Debug.Log($"[CIBuildSettings] Build scene set to: {scenePath}");
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
    public static void EnableDebug()
    {
        foreach (var target in AllTargets)
        {
            var defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(target);
            if (!defines.Contains("UNITEXT_DEBUG"))
            {
                defines = string.IsNullOrEmpty(defines) ? "UNITEXT_DEBUG" : defines + ";UNITEXT_DEBUG";
                PlayerSettings.SetScriptingDefineSymbolsForGroup(target, defines);
            }
        }

        Debug.Log("[CIBuildSettings] UNITEXT_DEBUG symbol added to all platforms");
    }

    [MenuItem("UniText/CI/Disable UNITEXT_DEBUG Symbol")]
    public static void DisableDebug()
    {
        foreach (var target in AllTargets)
        {
            var defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(target);
            if (defines.Contains("UNITEXT_DEBUG"))
            {
                defines = defines.Replace(";UNITEXT_DEBUG", "").Replace("UNITEXT_DEBUG;", "").Replace("UNITEXT_DEBUG", "");
                PlayerSettings.SetScriptingDefineSymbolsForGroup(target, defines);
            }
        }

        Debug.Log("[CIBuildSettings] UNITEXT_DEBUG symbol removed from all platforms");
    }

    [MenuItem("UniText/CI/Enable UNITEXT_TESTS Symbol")]
    public static void EnableTests()
    {
        foreach (var target in AllTargets)
        {
            var defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(target);
            if (!defines.Contains("UNITEXT_TESTS"))
            {
                defines = string.IsNullOrEmpty(defines) ? "UNITEXT_TESTS" : defines + ";UNITEXT_TESTS";
                PlayerSettings.SetScriptingDefineSymbolsForGroup(target, defines);
            }
        }

        Debug.Log("[CIBuildSettings] UNITEXT_TESTS symbol added to all platforms");
    }

    [MenuItem("UniText/CI/Disable UNITEXT_TESTS Symbol")]
    public static void DisableTests()
    {
        foreach (var target in AllTargets)
        {
            var defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(target);
            if (defines.Contains("UNITEXT_TESTS"))
            {
                defines = defines.Replace(";UNITEXT_TESTS", "").Replace("UNITEXT_TESTS;", "").Replace("UNITEXT_TESTS", "");
                PlayerSettings.SetScriptingDefineSymbolsForGroup(target, defines);
            }
        }

        Debug.Log("[CIBuildSettings] UNITEXT_TESTS symbol removed from all platforms");
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
