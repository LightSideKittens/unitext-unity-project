using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
using UnityEngine.Networking;

namespace LightSide.Hub
{
    /// <summary>
    /// A LightSide fork of a Unity package, unpacked into the project and pointed at by a
    /// <c>file:</c> dependency. A local package wins over the built-in one by source priority
    /// whatever its version number, so the fork does not have to match the editor's own.
    /// </summary>
    /// <remarks>
    /// Both directions go through <see cref="Client"/> rather than a manifest edit plus
    /// <see cref="Client.Resolve"/>: a soft resolve does not reliably re-resolve a package that ships
    /// with the editor, which leaves the change invisible until the next restart. Because a real
    /// resolve reloads the domain and destroys the callback in flight, the revert's folder deletion is
    /// recorded in <see cref="SessionState"/> and finished on the next load.
    /// </remarks>
    internal static class EmbeddedFork
    {
        private const string PendingKey = "LightSide.Hub.PendingForkCleanup";

        /// <summary>Absolute path the fork is unpacked to.</summary>
        public static string FolderPath(HubRequirement requirement)
            => Path.Combine(ProjectRoot, requirement.Folder.Replace('/', Path.DirectorySeparatorChar));

        /// <summary>Version of the unpacked fork, read from its own manifest; null when nothing is unpacked.</summary>
        public static string InstalledVersion(HubRequirement requirement)
        {
            var manifest = Path.Combine(FolderPath(requirement), "package.json");
            if (!File.Exists(manifest)) return null;
            var parsed = MiniJson.Parse(File.ReadAllText(manifest)) as Dictionary<string, object>;
            return MiniJson.String(parsed, "version");
        }

        /// <summary>Whether the project currently depends on the fork rather than the built-in package.</summary>
        public static bool IsActive(HubRequirement requirement)
            => ProjectManifest.Dependency(requirement.Package) == FileReference(requirement);

        /// <summary>
        /// Downloads <paramref name="version"/> from the registry, unpacks it and switches the project
        /// onto it. <paramref name="onResult"/> receives null on success and a message otherwise; it
        /// may never run, because a successful resolve reloads the domain.
        /// </summary>
        public static void Install(HubRequirement requirement, string version, Action<string> onResult)
        {
            var request = Tarball(requirement.Package, version);
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

                try
                {
                    TarGz.ExtractPackage(data, FolderPath(requirement));
                }
                catch (Exception e)
                {
                    onResult(e.Message);
                    return;
                }

                var add = Client.Add($"{requirement.Package}@{FileReference(requirement)}");
                Track(add, () =>
                {
                    if (add.Status == StatusCode.Success)
                    {
                        SessionState.EraseString(PendingKey);
                        onResult(null);
                    }
                    else
                    {
                        onResult(add.Error?.message ?? "Package Manager refused the local package.");
                    }
                });
            };
        }

        /// <summary>
        /// Returns the project to Unity's own copy of the package and deletes the unpacked fork.
        /// <paramref name="onResult"/> receives null on success and a message otherwise.
        /// </summary>
        public static void Revert(HubRequirement requirement, Action<string> onResult)
        {
            SessionState.SetString(PendingKey, requirement.Package + "|" + requirement.Folder);

            var remove = Client.Remove(requirement.Package);
            Track(remove, () =>
            {
                if (remove.Status == StatusCode.Success)
                {
                    Delete(FolderPath(requirement));
                    SessionState.EraseString(PendingKey);
                    onResult(null);
                    return;
                }

                ProjectManifest.RemoveDependency(requirement.Package);
                Delete(FolderPath(requirement));
                SessionState.EraseString(PendingKey);
                Client.Resolve();
                onResult("Reverted. Restart Unity if the Package Manager still lists the fork.");
            });
        }

        /// <summary>
        /// Finishes a revert whose completion callback the resolve's domain reload destroyed. The
        /// marker survives until the dependency is actually gone, so a resolve still in flight is not
        /// mistaken for a finished one.
        /// </summary>
        [InitializeOnLoadMethod]
        private static void FinishPendingCleanup()
        {
            var pending = SessionState.GetString(PendingKey, "");
            if (string.IsNullOrEmpty(pending)) return;

            var separator = pending.IndexOf('|');
            if (separator < 0)
            {
                SessionState.EraseString(PendingKey);
                return;
            }

            var package = pending.Substring(0, separator);
            var folder = pending.Substring(separator + 1);
            if (ProjectManifest.Dependency(package) != null) return;

            SessionState.EraseString(PendingKey);
            Delete(Path.Combine(ProjectRoot, folder.Replace('/', Path.DirectorySeparatorChar)));
        }

        private static UnityWebRequest Tarball(string packageName, string version)
        {
            var token = HubRegistry.Token;
            var manifestUrl = ProjectManifest.RegistryUrl(HubConfig.Scope);
            var useUrlToken = manifestUrl != null && manifestUrl.Contains("/t/");
            var baseUrl = useUrlToken
                ? $"{HubConfig.RegistryUrl}/t/{token}"
                : HubConfig.RegistryUrl;

            var request = UnityWebRequest.Get(
                $"{baseUrl}/{packageName}/-/{packageName}-{version}.tgz");
            if (!useUrlToken && !string.IsNullOrEmpty(token))
                request.SetRequestHeader("Authorization", "Bearer " + token);
            return request;
        }

        private static string FileReference(HubRequirement requirement) => "file:../" + requirement.Folder;

        private static string ProjectRoot => Directory.GetParent(Application.dataPath)!.FullName;

        private static void Delete(string folder)
        {
            if (!Directory.Exists(folder)) return;
            try
            {
                Directory.Delete(folder, true);
            }
            catch (IOException e)
            {
                Debug.LogWarning($"[LightSide] The fork folder is still locked: {e.Message}");
            }
            catch (UnauthorizedAccessException e)
            {
                Debug.LogWarning($"[LightSide] The fork folder could not be removed: {e.Message}");
            }
        }

        /// <summary>Polls a Package Manager request to completion; a domain reload before it finishes is expected and recovered from on the next load.</summary>
        private static void Track(Request request, Action onComplete)
        {
            void Poll()
            {
                if (!request.IsCompleted) return;
                EditorApplication.update -= Poll;
                onComplete();
            }
            EditorApplication.update += Poll;
        }
    }
}
