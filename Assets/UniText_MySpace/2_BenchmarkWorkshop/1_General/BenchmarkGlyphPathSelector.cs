using System;
using System.Collections.Generic;
using LightSide;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Exact UniText raster-and-atlas-write route requested for a glyph benchmark run; concrete values are fail-closed and never fall back to another route.</summary>
public enum BenchmarkGlyphRasterPath
{
    Auto,
    GpuComputeDirect,
    CpuGpuUpload,
    CpuCopyTexture,
    CpuReadableApply
}

static class BenchmarkGlyphRasterPaths
{
    internal static string Token(BenchmarkGlyphRasterPath path) => path switch
    {
        BenchmarkGlyphRasterPath.Auto => "auto",
        BenchmarkGlyphRasterPath.GpuComputeDirect => "gpuComputeDirect",
        BenchmarkGlyphRasterPath.CpuGpuUpload => "cpuGpuUpload",
        BenchmarkGlyphRasterPath.CpuCopyTexture => "cpuCopyTexture",
        BenchmarkGlyphRasterPath.CpuReadableApply => "cpuReadableApply",
        _ => throw new ArgumentOutOfRangeException(nameof(path), path, null)
    };

    internal static string Label(BenchmarkGlyphRasterPath path) => path switch
    {
        BenchmarkGlyphRasterPath.Auto => "Auto",
        BenchmarkGlyphRasterPath.GpuComputeDirect => "GPU Compute Direct",
        BenchmarkGlyphRasterPath.CpuGpuUpload => "CPU + GpuUpload",
        BenchmarkGlyphRasterPath.CpuCopyTexture => "CPU + CopyTexture",
        BenchmarkGlyphRasterPath.CpuReadableApply => "CPU + Readable Apply",
        _ => throw new ArgumentOutOfRangeException(nameof(path), path, null)
    };

    internal static GlyphAtlas.ExecutionPath ToExecutionPath(BenchmarkGlyphRasterPath path) => path switch
    {
        BenchmarkGlyphRasterPath.Auto => GlyphAtlas.ExecutionPath.Auto,
        BenchmarkGlyphRasterPath.GpuComputeDirect => GlyphAtlas.ExecutionPath.GpuComputeDirect,
        BenchmarkGlyphRasterPath.CpuGpuUpload => GlyphAtlas.ExecutionPath.CpuGpuUpload,
        BenchmarkGlyphRasterPath.CpuCopyTexture => GlyphAtlas.ExecutionPath.CpuCopyTexture,
        BenchmarkGlyphRasterPath.CpuReadableApply => GlyphAtlas.ExecutionPath.CpuReadableApply,
        _ => throw new ArgumentOutOfRangeException(nameof(path), path, null)
    };

    internal static string Token(GlyphAtlas.ExecutionPath path) => path switch
    {
        GlyphAtlas.ExecutionPath.Auto => "auto",
        GlyphAtlas.ExecutionPath.GpuComputeDirect => "gpuComputeDirect",
        GlyphAtlas.ExecutionPath.CpuGpuUpload => "cpuGpuUpload",
        GlyphAtlas.ExecutionPath.CpuCopyTexture => "cpuCopyTexture",
        GlyphAtlas.ExecutionPath.CpuReadableApply => "cpuReadableApply",
        _ => throw new ArgumentOutOfRangeException(nameof(path), path, null)
    };

    internal static bool TryParse(string value, out BenchmarkGlyphRasterPath path)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            string normalized = value.Replace("-", "").Replace("_", "").Trim();
            foreach (BenchmarkGlyphRasterPath candidate in Enum.GetValues(typeof(BenchmarkGlyphRasterPath)))
                if (string.Equals(normalized, Token(candidate).Replace("-", "").Replace("_", ""),
                        StringComparison.OrdinalIgnoreCase)
                    || string.Equals(normalized, candidate.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    path = candidate;
                    return true;
                }
        }
        path = default;
        return false;
    }
}

/// <summary>Provides one runtime-selectable UniText glyph path for the next suite, with command-line, environment, and Android Intent overrides for unattended builds.</summary>
[RequireComponent(typeof(BenchmarkFontSelector))]
public sealed class BenchmarkGlyphPathSelector : MonoBehaviour
{
    [SerializeField] BenchmarkGlyphRasterPath selectedPath;

    readonly List<Toggle> toggles = new();

    /// <summary>The path captured once when the glyph suite starts so a run cannot mix routes.</summary>
    public BenchmarkGlyphRasterPath SelectedPath => selectedPath;

    /// <summary>True when the launch environment selected a path explicitly, allowing unattended builds to skip the interactive selection window.</summary>
    public bool HasLaunchOverride { get; private set; }

    /// <summary>Describes an explicit launch value that could not be mapped to a path; unattended execution must stop rather than measure Auto.</summary>
    public string LaunchOverrideError { get; private set; }

    void Awake()
    {
        string requested = Environment.GetEnvironmentVariable("UNITEXT_GLYPH_PATH");
        var args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length; i++)
        {
            const string prefix = "-unitextGlyphPath=";
            if (args[i].StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                requested = args[i].Substring(prefix.Length);
            else if (string.Equals(args[i], "-unitextGlyphPath", StringComparison.OrdinalIgnoreCase)
                     && i + 1 < args.Length)
                requested = args[++i];
        }
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
            using var intent = activity.Call<AndroidJavaObject>("getIntent");
            requested = intent.Call<string>("getStringExtra", "unitextGlyphPath") ?? requested;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[Benchmark GlyphPath] Android launch override failed: {exception.GetType().Name}");
        }
#endif
        if (BenchmarkGlyphRasterPaths.TryParse(requested, out var parsed))
        {
            selectedPath = parsed;
            HasLaunchOverride = true;
        }
        else if (!string.IsNullOrWhiteSpace(requested))
        {
            HasLaunchOverride = true;
            LaunchOverrideError = $"Unknown glyph path launch value '{requested}'";
            Debug.LogError($"[Benchmark GlyphPath] {LaunchOverrideError}; benchmark will not fall back to Auto.");
        }
    }

    void Start()
    {
        var fontSelector = GetComponent<BenchmarkFontSelector>();
        foreach (BenchmarkGlyphRasterPath path in Enum.GetValues(typeof(BenchmarkGlyphRasterPath)))
        {
            var toggle = Instantiate(fontSelector.togglePrefab, fontSelector.content.transform);
            toggle.group = null;
            BenchmarkFontSelector.SetLabel(toggle, $"Path: {BenchmarkGlyphRasterPaths.Label(path)}");
            toggles.Add(toggle);
            toggle.onValueChanged.AddListener(isOn =>
            {
                if (isOn) Select(path, toggle);
                else if (path == selectedPath) toggle.SetIsOnWithoutNotify(true);
            });
            toggle.SetIsOnWithoutNotify(path == selectedPath);
        }
        Debug.Log($"[Benchmark GlyphPath] Selected {BenchmarkGlyphRasterPaths.Token(selectedPath)}");
    }

    void Select(BenchmarkGlyphRasterPath path, Toggle selected)
    {
        selectedPath = path;
        LaunchOverrideError = null;
        for (int i = 0; i < toggles.Count; i++)
            if (toggles[i] != selected)
                toggles[i].SetIsOnWithoutNotify(false);
        Debug.Log($"[Benchmark GlyphPath] Selected {BenchmarkGlyphRasterPaths.Token(path)}");
    }
}
