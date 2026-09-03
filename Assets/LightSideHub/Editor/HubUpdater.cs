using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace LightSide.Hub
{
    /// <summary>A Hub build published on GitHub.</summary>
    internal sealed class HubRelease
    {
        public string Version;
        public string DownloadUrl;
        public string Notes;

        /// <summary>Whether this build supersedes the one running.</summary>
        public bool IsNewer => SemVerComparer.IsNewer(Version, HubConfig.Version);
    }

    /// <summary>
    /// Keeps the Hub current from its GitHub releases. The channel is deliberately not the package
    /// registry: the Hub is what configures registry access, so it cannot depend on having it. The
    /// GitHub endpoints used here are public and need no credential — sixty calls an hour per address,
    /// against one check a day.
    /// </summary>
    internal static class HubUpdater
    {
        private const string LastCheckKey = "LightSide.Hub.LastUpdateCheck";
        private const double CheckIntervalHours = 24.0;

        private static string DownloadPath => Path.Combine(
            Application.dataPath, "..", "Library", "LightSideHub", "update.unitypackage");

        /// <summary>
        /// Asks GitHub for the newest published Hub. <paramref name="onResult"/> receives the release
        /// and a null message on success, or null and a message on failure.
        /// </summary>
        public static void CheckLatest(Action<HubRelease, string> onResult)
        {
            var request = Api($"https://api.github.com/repos/{HubConfig.ReleaseRepository}/releases/latest");
            request.SendWebRequest().completed += _ =>
            {
                var failure = request.result != UnityWebRequest.Result.Success ? request.error : null;
                var body = failure == null ? request.downloadHandler.text : null;
                request.Dispose();

                if (failure != null)
                {
                    onResult(null, failure);
                    return;
                }

                EditorPrefs.SetString(LastCheckKey, DateTime.UtcNow.ToString("o"));
                var release = ParseRelease(body);
                onResult(release, release == null ? "The release carries no Unity package." : null);
            };
        }

        /// <summary>Whether enough time has passed to look for a new build again.</summary>
        public static bool DueForCheck()
        {
            var stamp = EditorPrefs.GetString(LastCheckKey, "");
            if (string.IsNullOrEmpty(stamp)) return true;
            return !DateTime.TryParse(stamp, null,
                       System.Globalization.DateTimeStyles.RoundtripKind, out var last)
                   || (DateTime.UtcNow - last).TotalHours >= CheckIntervalHours;
        }

        /// <summary>
        /// Downloads <paramref name="release"/> and imports it over the running Hub, which Unity then
        /// recompiles. <paramref name="onResult"/> receives null on success and a message otherwise.
        /// </summary>
        public static void Apply(HubRelease release, Action<string> onResult)
        {
            var request = Api(release.DownloadUrl);
            request.SendWebRequest().completed += _ =>
            {
                if (request.result != UnityWebRequest.Result.Success)
                {
                    var error = request.error;
                    request.Dispose();
                    onResult(error);
                    return;
                }

                var data = request.downloadHandler.data;
                request.Dispose();

                if (data == null || data.Length == 0)
                {
                    onResult("The download was empty.");
                    return;
                }

                try
                {
                    var path = DownloadPath;
                    Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
                    File.WriteAllBytes(path, data);
                    AssetDatabase.ImportPackage(path, false);
                }
                catch (Exception e)
                {
                    onResult(e.Message);
                    return;
                }

                onResult(null);
            };
        }

        private static HubRelease ParseRelease(string json)
        {
            var root = MiniJson.Parse(json) as Dictionary<string, object>;
            if (root == null) return null;

            var assets = MiniJson.Array(root, "assets");
            if (assets == null) return null;

            foreach (var entry in assets)
            {
                if (entry is not Dictionary<string, object> asset) continue;
                var name = MiniJson.String(asset, "name");
                if (name == null ||
                    !name.EndsWith(HubConfig.ReleaseAsset, StringComparison.OrdinalIgnoreCase))
                    continue;

                var url = MiniJson.String(asset, "browser_download_url");
                if (string.IsNullOrEmpty(url)) continue;

                return new HubRelease
                {
                    Version = StripTagPrefix(MiniJson.String(root, "tag_name")),
                    DownloadUrl = url,
                    Notes = MiniJson.String(root, "body") ?? "",
                };
            }
            return null;
        }

        /// <summary>Release tags are written <c>v1.2.3</c>; versions compare as <c>1.2.3</c>.</summary>
        private static string StripTagPrefix(string tag)
        {
            if (string.IsNullOrEmpty(tag)) return "";
            return tag[0] == 'v' || tag[0] == 'V' ? tag.Substring(1) : tag;
        }

        /// <summary>GitHub rejects a request that names no client, so every call identifies the Hub.</summary>
        private static UnityWebRequest Api(string url)
        {
            var request = UnityWebRequest.Get(url);
            request.SetRequestHeader("User-Agent", "LightSideHub/" + HubConfig.Version);
            request.SetRequestHeader("Accept", "application/vnd.github+json");
            return request;
        }
    }
}
