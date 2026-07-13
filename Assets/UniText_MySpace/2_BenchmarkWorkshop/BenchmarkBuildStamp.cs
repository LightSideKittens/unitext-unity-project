#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// Writes <see cref="BenchmarkBuildInfo"/> into <c>Assets/UniText_MySpace/Resources/</c> before a player build,
/// capturing the commit from <c>GITHUB_SHA</c> (CI) or git (local) — so the on-device benchmark JSON carries the
/// real commit even though the device can neither run git nor see the runner's env.
/// </summary>
public class BenchmarkBuildStamp : IPreprocessBuildWithReport
{
    static string ResourcePath => $"Assets/UniText_MySpace/Resources/{BenchmarkBuildInfo.ResourceName}.json";

    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        var info = new BenchmarkBuildInfo
        {
            commit = Env("GITHUB_SHA") ?? Git("rev-parse HEAD") ?? "unknown",
            branch = Env("GITHUB_REF_NAME") ?? Git("rev-parse --abbrev-ref HEAD") ?? "unknown",
            dirty = GitDirty("diff-index --quiet HEAD"),
            submoduleCommit = Git("-C Assets/UniText rev-parse HEAD") ?? "unknown",
            submoduleBranch = Git("-C Assets/UniText rev-parse --abbrev-ref HEAD") ?? "unknown",
            submoduleDirty = GitDirty("-C Assets/UniText diff-index --quiet HEAD"),
        };

        Directory.CreateDirectory(Path.GetDirectoryName(ResourcePath)!);
        File.WriteAllText(ResourcePath, JsonUtility.ToJson(info, true));
        AssetDatabase.ImportAsset(ResourcePath);
        Debug.Log($"[BenchmarkBuildStamp] Baked build info: {info.commit} ({info.branch})");
    }

    static string Env(string key)
    {
        var v = Environment.GetEnvironmentVariable(key);
        return string.IsNullOrEmpty(v) ? null : v;
    }

    static string Git(string args)
    {
        try
        {
            var psi = new ProcessStartInfo("git", args)
            {
                WorkingDirectory = Directory.GetParent(Application.dataPath)!.FullName,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var p = Process.Start(psi);
            var output = p!.StandardOutput.ReadToEnd().Trim();
            p.WaitForExit(3000);
            return p.ExitCode == 0 && output.Length > 0 ? output : null;
        }
        catch { return null; }
    }

    static bool GitDirty(string args)
    {
        try
        {
            var psi = new ProcessStartInfo("git", args)
            {
                WorkingDirectory = Directory.GetParent(Application.dataPath)!.FullName,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var p = Process.Start(psi);
            if (!p!.WaitForExit(3000)) { try { p.Kill(); } catch { } return false; }
            return p.ExitCode == 1;
        }
        catch { return false; }
    }
}
#endif
