#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

[InitializeOnLoad]
public static class CIBuildSettings
{
    private const string TestScenePath = "Assets/UniText_MySpace/1_TestWorkshop/UniTextTest.unity";
    private const string BenchmarkScenePath = "Assets/UniText_MySpace/2_BenchmarkWorkshop/1_General/General_BenchmarkTest.unity";
    private const string SlideshowScenePath = "Packages/media.lightside.unitext/Samples/BasicUsage/BasicUsage.unity";

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
        var slideshowArg = GetCommandLineArg(args, "-ciSlideshow");

        if (debugArg == null && benchmarkArg == null && testsArg == null && slideshowArg == null)
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
        var slideshowArg = GetCommandLineArg(args, "-ciSlideshow");

        Debug.Log($"[CIBuildSettings] -ciDebug={debugArg ?? "null"}, -ciBenchmark={benchmarkArg ?? "null"}, -ciTests={testsArg ?? "null"}, -ciSlideshow={slideshowArg ?? "null"}");

        if (debugArg == null && benchmarkArg == null && testsArg == null && slideshowArg == null)
        {
            Debug.Log("[CIBuildSettings] Not in CI environment, skipping configuration");
            return;
        }

        ConfigureIOSForDevice();
        SetHighStripping();
        SetWebGLExceptions(debugArg == "true");
        EnableAndroidSymbols();

        if (benchmarkArg == "true")
        {
            SetBuildScene(BenchmarkScenePath);
            EnableBenchmark();
            DisableTests();
            DisableSlideshow();
            if (debugArg == "true")
            {
                EnableDebug();
                Debug.Log("[CIBuildSettings] Benchmark build with UNITEXT_DEBUG — diagnostic run, timings are polluted by logging");
            }
            else
            {
                DisableDebug();
            }
            Debug.Log("[CIBuildSettings] Benchmark build configured");
        }
        else
        {
            DisableBenchmark();

            if (slideshowArg == "true")
            {
                SetBuildScene(SlideshowScenePath);
                EnableSlideshow();
                DisableTests();
                Debug.Log("[CIBuildSettings] Slideshow build configured");
            }
            else if (testsArg == "true")
            {
                SetBuildScene(TestScenePath);
                EnableTests();
                DisableSlideshow();
                Debug.Log("[CIBuildSettings] Test build configured");
            }
            else
            {
                DisableTests();
                DisableSlideshow();
                Debug.Log("[CIBuildSettings] Generic build — EditorBuildSettings.scenes left untouched");
            }

            if (debugArg == "true")
                EnableDebug();
            else
                DisableDebug();
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

    [MenuItem("UniText/CI/Set Build Scene - Slideshow")]
    public static void SetSlideshowScene() => SetBuildScene(SlideshowScenePath);

    private static void SetBuildScene(string scenePath)
    {
        SetBuildScenes(scenePath);
    }

    private static void SetBuildScenes(params string[] scenePaths)
    {
        var scenes = new EditorBuildSettingsScene[scenePaths.Length];
        for (int i = 0; i < scenePaths.Length; i++)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePaths[i]) == null)
                throw new InvalidOperationException($"CI build scene not found: {scenePaths[i]}");

            scenes[i] = new EditorBuildSettingsScene(scenePaths[i], true);
        }
        EditorBuildSettings.scenes = scenes;

        Debug.Log($"[CIBuildSettings] Build scenes set to: {string.Join(", ", scenePaths)}");
    }

    [MenuItem("UniText/CI/Set High Stripping")]
    public static void SetHighStripping()
    {
        foreach (var target in AllTargets)
            SetStrippingLevel(target, ManagedStrippingLevel.High);

        Debug.Log("[CIBuildSettings] Managed Stripping Level set to High for all platforms");
    }

    [MenuItem("UniText/CI/Enable UNITEXT_DEBUG Symbol")]
    public static void EnableDebug()
    {
        SetDefineSymbol("UNITEXT_DEBUG", true);
        SetDefineSymbol("LIGHTSIDE_DEBUG", true);
    }

    [MenuItem("UniText/CI/Disable UNITEXT_DEBUG Symbol")]
    public static void DisableDebug()
    {
        SetDefineSymbol("UNITEXT_DEBUG", false);
        SetDefineSymbol("LIGHTSIDE_DEBUG", false);
    }

    [MenuItem("UniText/CI/Enable UNITEXT_TESTS Symbol")]
    public static void EnableTests() => SetDefineSymbol("UNITEXT_TESTS", true);

    [MenuItem("UniText/CI/Disable UNITEXT_TESTS Symbol")]
    public static void DisableTests() => SetDefineSymbol("UNITEXT_TESTS", false);

    [MenuItem("UniText/CI/Enable UNITEXT_BENCHMARK Symbol")]
    public static void EnableBenchmark() => SetDefineSymbol("UNITEXT_BENCHMARK", true);

    [MenuItem("UniText/CI/Disable UNITEXT_BENCHMARK Symbol")]
    public static void DisableBenchmark() => SetDefineSymbol("UNITEXT_BENCHMARK", false);

    [MenuItem("UniText/CI/Enable UNITEXT_SLIDESHOW Symbol")]
    public static void EnableSlideshow() => SetDefineSymbol("UNITEXT_SLIDESHOW", true);

    [MenuItem("UniText/CI/Disable UNITEXT_SLIDESHOW Symbol")]
    public static void DisableSlideshow() => SetDefineSymbol("UNITEXT_SLIDESHOW", false);

    private static void SetDefineSymbol(string symbol, bool enabled)
    {
        foreach (var target in AllTargets)
        {
            var defines = GetDefines(target);

            if (enabled)
            {
                if (!defines.Contains(symbol))
                {
                    defines = string.IsNullOrEmpty(defines) ? symbol : defines + ";" + symbol;
                    SetDefines(target, defines);
                }
            }
            else
            {
                if (defines.Contains(symbol))
                {
                    defines = defines.Replace(";" + symbol, "").Replace(symbol + ";", "").Replace(symbol, "");
                    SetDefines(target, defines);
                }
            }
        }

        Debug.Log($"[CIBuildSettings] {symbol} {(enabled ? "added to" : "removed from")} all platforms");
    }

    private static void SetStrippingLevel(BuildTargetGroup target, ManagedStrippingLevel level)
    {
#if UNITY_2021_2_OR_NEWER
        PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.FromBuildTargetGroup(target), level);
#else
        PlayerSettings.SetManagedStrippingLevel(target, level);
#endif
    }

    private static string GetDefines(BuildTargetGroup target)
    {
#if UNITY_2021_2_OR_NEWER
        return PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.FromBuildTargetGroup(target));
#else
        return PlayerSettings.GetScriptingDefineSymbolsForGroup(target);
#endif
    }

    private static void SetDefines(BuildTargetGroup target, string defines)
    {
#if UNITY_2021_2_OR_NEWER
        PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.FromBuildTargetGroup(target), defines);
#else
        PlayerSettings.SetScriptingDefineSymbolsForGroup(target, defines);
#endif
    }

    private static void SetWebGLExceptions(bool withStacktrace)
    {
        var level = withStacktrace
            ? WebGLExceptionSupport.FullWithStacktrace
            : WebGLExceptionSupport.FullWithoutStacktrace;
        PlayerSettings.WebGL.exceptionSupport = level;
        Debug.Log($"[CIBuildSettings] WebGL exceptions set to {level}");
    }

    /// <summary>CI Android builds emit symbols.zip (public symbols for libunity/libil2cpp) so device tombstones from Firebase Test Lab symbolicate without hunting for the exact GameCI editor image.</summary>
    [MenuItem("UniText/CI/Enable Android Symbols")]
    public static void EnableAndroidSymbols()
    {
#pragma warning disable CS0618
        EditorUserBuildSettings.androidCreateSymbols = AndroidCreateSymbols.Public;
#pragma warning restore CS0618
        Debug.Log("[CIBuildSettings] Android symbols.zip enabled (Public)");
    }

    private static void ConfigureIOSForDevice()
    {
        PlayerSettings.iOS.sdkVersion = iOSSdkVersion.DeviceSDK;
        Debug.Log("[CIBuildSettings] iOS SDK set to DeviceSDK");
    }
}
#endif
