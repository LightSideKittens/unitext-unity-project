#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace LightSide.Benchmark
{
    /// <summary>
    /// Writes <see cref="BenchmarkBuildInfo"/> into <c>Assets/Resources/</c> before a player build, capturing
    /// the commit from <c>GITHUB_SHA</c> (CI) or git (local) — so the on-device benchmark JSON carries the
    /// real commit even though the device can neither run git nor see the runner's env.
    /// </summary>
    public class BenchmarkBuildStamp : IPreprocessBuildWithReport
    {
        /// <summary>
        /// Repository-relative path of the submodule whose revision is stamped beside the project's own.
        /// Empty leaves the submodule fields unknown. Set it from an <c>[InitializeOnLoadMethod]</c>; the
        /// build callback runs in the same domain.
        /// </summary>
        public static string SubmodulePath = "";

        static string ResourcePath => $"Assets/Resources/{BenchmarkBuildInfo.ResourceName}.json";

        public int callbackOrder => 0;

        public void OnPreprocessBuild(BuildReport report)
        {
            var info = new BenchmarkBuildInfo
            {
                commit = Env("GITHUB_SHA") ?? Git("rev-parse HEAD") ?? "unknown",
                branch = Env("GITHUB_REF_NAME") ?? Git("rev-parse --abbrev-ref HEAD") ?? "unknown",
                dirty = GitDirty("diff-index --quiet HEAD"),
                submoduleCommit = Submodule("rev-parse HEAD") ?? "unknown",
                submoduleBranch = Submodule("rev-parse --abbrev-ref HEAD") ?? "unknown",
                submoduleDirty = SubmodulePath.Length != 0 &&
                                 GitDirty($"-C {SubmodulePath} diff-index --quiet HEAD"),
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

        static string Submodule(string args) =>
            SubmodulePath.Length == 0 ? null : Git($"-C {SubmodulePath} {args}");

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
}
#endif
