using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace LightSide
{
    [InitializeOnLoad]
    internal static class UniTextSetupAutoOpen
    {
        private const string SessionKey = "UniTextSetup_ShownThisSession";

        static UniTextSetupAutoOpen()
        {
            if (SessionState.GetBool(SessionKey, false)) return;
            EditorApplication.delayCall += () =>
            {
                if (UniTextSetupWindow.IsSetupNeeded())
                {
                    SessionState.SetBool(SessionKey, true);
                    UniTextSetupWindow.Open();
                }
            };
        }
    }

    internal sealed class UniTextSetupWindow : EditorWindow
    {
        private const string RegistryUrl = "https://registry.lightside.media";
        private const string PackageName = "media.lightside.unitext";
        private const string ScopeName = "Light Side";
        private const string Scope = "media.lightside";
        private const string UguiPackage = "com.unity.ugui";
        private const string UguiFileRef = "file:../LocalPackages/com.unity.ugui";
        private const string PendingUguiCleanupKey = "LightSide.UniText.PendingUguiCleanup";

        private static readonly Regex TokenPattern = new(
            @"^(lst_[A-Za-z0-9]{32}|[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})$");

        private static string ManifestPath => Path.Combine(Application.dataPath, "..", "Packages", "manifest.json");

        private string token = "";
        private string setupStatus = "";
        private MessageType setupStatusType;
        private string versionsStatus = "";
        private MessageType versionsStatusType;

        private int tab;

        private List<VersionEntry> versions = new();
        private string installedVersion = "";
        private bool showPreRelease;
        private bool fetching;
        private bool settingUp;
        private Vector2 scrollPos;

        private List<VersionEntry> drawVersions = new();
        private string drawInstalled = "";
        private bool drawFetching;

        private List<VersionEntry> uguiVersions = new();
        private string uguiInstalledVersion = "";
        private bool uguiShowPreRelease;
        private bool uguiFetching;
        private bool uguiBusy;
        private bool uguiLoaded;
        private string uguiStatus = "";
        private MessageType uguiStatusType;
        private Vector2 uguiScrollPos;

        private List<VersionEntry> drawUguiVersions = new();
        private string drawUguiInstalled = "";
        private bool drawUguiFetching;

        private struct VersionEntry
        {
            public string version;
            public bool isPreRelease;
            public bool isInstalled;
            public bool isLatest;
        }

        private enum SetupState { FreshInstall, NeedsToken, Authenticated }

        [MenuItem("Tools/UniText/Setup", false, 0)]
        public static void Open()
        {
            var window = GetWindow<UniTextSetupWindow>("UniText");
            window.minSize = new Vector2(480, 400);
            window.Show();
        }

        public static bool IsSetupNeeded() => GetSetupState() != SetupState.Authenticated;

        private static SetupState GetSetupState()
        {
            if (!File.Exists(ManifestPath)) return SetupState.FreshInstall;
            var manifestText = File.ReadAllText(ManifestPath);
            if (!manifestText.Contains(RegistryUrl)) return SetupState.FreshInstall;

            if (!string.IsNullOrEmpty(ReadTokenFromManifest())) return SetupState.Authenticated;

            return string.IsNullOrEmpty(ReadConfiguredToken()) ? SetupState.NeedsToken : SetupState.Authenticated;
        }

        private static string ReadTokenFromManifest()
        {
            var url = ReadRegistryUrl();
            if (url == null) return null;
            var match = Regex.Match(url, @"/t/([A-Za-z0-9_\-]+)(?:/pre)?$");
            return match.Success ? match.Groups[1].Value : null;
        }

        private static string ReadRegistryUrl()
        {
            if (!File.Exists(ManifestPath)) return null;
            var manifest = MiniJson.Parse(File.ReadAllText(ManifestPath)) as Dictionary<string, object>;
            if (manifest == null) return null;
            if (!manifest.TryGetValue("scopedRegistries", out var sr) || sr is not List<object> list) return null;
            foreach (var entry in list)
            {
                if (entry is Dictionary<string, object> reg &&
                    reg.TryGetValue("scopes", out var sc) && sc is List<object> scopes &&
                    scopes.Any(s => s is string ss && ss == Scope) &&
                    reg.TryGetValue("url", out var u) && u is string url)
                {
                    return url;
                }
            }
            return null;
        }

        private static string EmbeddedUguiPath =>
            Path.Combine(Directory.GetParent(Application.dataPath).FullName, "LocalPackages", UguiPackage);

        private static string ReadConfiguredToken()
        {
            var configPath = UpmConfigWriter.GetConfigPath();
            if (!File.Exists(configPath)) return null;

            var lines = File.ReadAllLines(configPath);
            var inSection = false;
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.Contains(RegistryUrl)) { inSection = true; continue; }
                if (inSection && trimmed.StartsWith("[")) break;
                if (inSection && trimmed.StartsWith("token"))
                {
                    var eqIdx = trimmed.IndexOf('=');
                    if (eqIdx < 0) continue;
                    return trimmed.Substring(eqIdx + 1).Trim().Trim('"');
                }
            }
            return null;
        }

        private void OnEnable()
        {
            tab = IsSetupNeeded() ? 0 : 1;
            if (tab == 1)
            {
                DetectInstalledVersion();
                if (installedVersion.Contains("-") && EnsureChannel(true))
                    UnityEditor.PackageManager.Client.Resolve();
                FetchVersions();
            }
        }

        private GUIContent[] tabContents;

        private GUIContent[] TabContents => tabContents ??= new[]
        {
            new GUIContent("  Setup", EditorGUIUtility.IconContent("d_Settings").image),
            new GUIContent("  UniText", EditorGUIUtility.IconContent("d_Package Manager").image),
            new GUIContent("  Unity UI", EditorGUIUtility.IconContent("d_Package Manager").image),
        };

        private void OnGUI()
        {
            if (Event.current.type == EventType.Layout)
            {
                drawVersions = new List<VersionEntry>(versions);
                drawInstalled = installedVersion;
                drawFetching = fetching;
                drawUguiVersions = new List<VersionEntry>(uguiVersions);
                drawUguiInstalled = uguiInstalledVersion;
                drawUguiFetching = uguiFetching;
            }

            GUILayout.Space(8);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            tab = GUILayout.SelectionGrid(tab, TabContents, TabContents.Length, "LargeButton", GUILayout.Height(28));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(4);

            if (tab == 2 && !uguiLoaded)
            {
                uguiLoaded = true;
                DetectEmbeddedUguiVersion();
                FetchUguiVersions();
            }

            switch (tab)
            {
                case 0: DrawSetupTab(); break;
                case 1: DrawVersionsTab(); break;
                case 2: DrawUguiTab(); break;
            }
        }

        private void DrawSetupTab()
        {
            var state = GetSetupState();

            string title;
            string description;
            string buttonLabel;
            switch (state)
            {
                case SetupState.NeedsToken:
                    title = "Authenticate UniText";
                    description = "This project uses UniText. Enter your personal access token to download the package.\n\nIf you don't have a token, ask your license owner or check the email from your purchase.";
                    buttonLabel = "Authenticate";
                    break;
                case SetupState.Authenticated:
                    title = "Update Access Token";
                    description = "Paste a new token to replace the existing one:";
                    buttonLabel = "Update Token";
                    break;
                default:
                    title = "Configure Registry Access";
                    description = "Paste your access token from the purchase email:";
                    buttonLabel = "Set Up";
                    break;
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            EditorGUILayout.LabelField(description, EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space(4);
            token = EditorGUILayout.TextField(token);

            var trimmed = token?.Trim() ?? "";
            if (trimmed.Length > 0 && !TokenPattern.IsMatch(trimmed))
                EditorGUILayout.HelpBox("Token format looks unexpected. Paste the token from your purchase email exactly as shown.", MessageType.Warning);

            EditorGUILayout.Space(8);

            GUI.enabled = TokenPattern.IsMatch(trimmed) && !settingUp;
            if (GUILayout.Button(settingUp ? "Setting up…" : buttonLabel, GUILayout.Height(28)))
                RunSetup(trimmed);
            GUI.enabled = true;

            if (!string.IsNullOrEmpty(setupStatus))
            {
                EditorGUILayout.Space(4);
                EditorGUILayout.HelpBox(setupStatus, setupStatusType);
            }
        }

        private void RunSetup(string token)
        {
            try
            {
                settingUp = true;
                setupStatus = "Verifying token…";
                setupStatusType = MessageType.Info;

                var registryUrl = $"{RegistryUrl}/t/{token}";
                var url = $"{registryUrl}/{PackageName}";
                var request = UnityWebRequest.Get(url);

                request.SendWebRequest().completed += _ =>
                {
                    settingUp = false;

                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        setupStatus = request.responseCode == 401
                            ? "Invalid or revoked token"
                            : $"Connection failed: {request.error}";
                        setupStatusType = MessageType.Error;
                        request.Dispose();
                        Repaint();
                        return;
                    }

                    try
                    {
                        DetectInstalledVersion();
                        ManifestEditor.EnsureScopedRegistry(
                            installedVersion.Contains("-") ? $"{registryUrl}/pre" : registryUrl,
                            ScopeName, Scope);

                        if (string.IsNullOrEmpty(installedVersion))
                        {
                            var latest = ParseLatestVersion(request.downloadHandler.text);
                            if (!string.IsNullOrEmpty(latest))
                                InstallVersion(latest);
                            else
                                UnityEditor.PackageManager.Client.Resolve();
                        }
                        else
                        {
                            UnityEditor.PackageManager.Client.Resolve();
                        }

                        setupStatus = "";
                        tab = 1;
                        FetchVersions();
                    }
                    catch (Exception e)
                    {
                        setupStatus = $"Setup failed: {e.Message}";
                        setupStatusType = MessageType.Error;
                        Debug.LogError($"[UniText] {e}");
                    }

                    request.Dispose();
                    Repaint();
                };
            }
            catch (Exception e)
            {
                settingUp = false;
                setupStatus = $"Setup failed: {e.Message}";
                setupStatusType = MessageType.Error;
                Debug.LogError($"[UniText] {e}");
            }
        }

        private static string ParseLatestVersion(string json)
        {
            var root = MiniJson.Parse(json) as Dictionary<string, object>;
            if (root == null) return null;
            var tags = root.TryGetValue("dist-tags", out var dt) ? dt as Dictionary<string, object> : null;
            if (tags == null) return null;
            return tags.TryGetValue("latest", out var v) && v is string s ? s : null;
        }

        private static readonly Color InstalledColor = new(0.3f, 0.8f, 0.45f);
        private static readonly Color LatestColor = new(0.4f, 0.65f, 0.95f);
        private static readonly Color PreReleaseColor = new(0.95f, 0.7f, 0.25f);

        private void DrawVersionsTab()
        {
            GUILayout.Space(8);

            if (!string.IsNullOrEmpty(drawInstalled))
            {
                var cardRect = EditorGUILayout.BeginHorizontal(GUILayout.Height(50));
                EditorGUI.DrawRect(cardRect, new Color(0.15f, 0.15f, 0.15f, 0.5f));
                EditorGUI.DrawRect(new Rect(cardRect.x, cardRect.y, 3, cardRect.height), InstalledColor);

                GUILayout.Space(12);

                EditorGUILayout.BeginVertical();
                GUILayout.FlexibleSpace();

                EditorGUILayout.BeginHorizontal();
                DrawIcon("d_GreenCheckmark@2x", "GreenCheckmark@2x", "GreenCheckmark");
                GUILayout.Space(4);
                var labelStyle = new GUIStyle(EditorStyles.label) { fontSize = 12, normal = { textColor = new Color(0.7f, 0.7f, 0.7f) } };
                GUILayout.Label("Installed", labelStyle);
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();

                var vStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 20 };
                GUILayout.Label(drawInstalled, vStyle);

                GUILayout.FlexibleSpace();
                EditorGUILayout.EndVertical();

                GUILayout.Space(12);
                EditorGUILayout.EndHorizontal();

                GUILayout.Space(12);
            }

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar, GUILayout.Height(22));
            var prev = showPreRelease;
            showPreRelease = GUILayout.Toggle(showPreRelease, " Pre-release", EditorStyles.toolbarButton, GUILayout.Width(100));
            if (prev != showPreRelease) FetchVersions();
            GUILayout.FlexibleSpace();
            GUI.enabled = !drawFetching;
            if (GUILayout.Button(new GUIContent(" Refresh", EditorGUIUtility.IconContent("Refresh").image), EditorStyles.toolbarButton, GUILayout.Width(75)))
                FetchVersions();
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            if (drawFetching)
            {
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField("Loading...", EditorStyles.centeredGreyMiniLabel);
                GUILayout.FlexibleSpace();
                return;
            }

            if (drawVersions.Count == 0)
            {
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField("No versions found", EditorStyles.centeredGreyMiniLabel);
                GUILayout.FlexibleSpace();
                return;
            }

            GUILayout.Space(8);
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            foreach (var v in drawVersions)
                DrawVersionRow(v);

            EditorGUILayout.EndScrollView();

            if (!string.IsNullOrEmpty(versionsStatus))
            {
                GUILayout.Space(4);
                EditorGUILayout.HelpBox(versionsStatus, versionsStatusType);
            }
        }

        private void DrawVersionRow(VersionEntry v)
        {
            var rowRect = EditorGUILayout.BeginHorizontal(GUILayout.Height(48));

            if (v.isInstalled)
                EditorGUI.DrawRect(rowRect, new Color(0.3f, 0.8f, 0.45f, 0.07f));

            var barColor = v.isInstalled ? InstalledColor : v.isLatest ? LatestColor : v.isPreRelease ? PreReleaseColor : new Color(0.5f, 0.5f, 0.5f, 0.3f);
            EditorGUI.DrawRect(new Rect(rowRect.x, rowRect.y + 2, 3, rowRect.height - 4), barColor);

            GUILayout.Space(12);

            EditorGUILayout.BeginVertical();
            GUILayout.FlexibleSpace();

            var verStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14 };
            GUILayout.Label(v.version, verStyle);

            var badgeText = "";
            var badgeColor = Color.grey;
            if (v.isInstalled) { badgeText = "Installed"; badgeColor = InstalledColor; }
            else if (v.isLatest) { badgeText = "Latest"; badgeColor = LatestColor; }
            if (v.isPreRelease) { badgeText += (badgeText.Length > 0 ? " \u00b7 " : "") + "Pre-release"; if (!v.isInstalled) badgeColor = PreReleaseColor; }

            if (badgeText.Length > 0)
            {
                var badgeStyle = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = badgeColor } };
                GUILayout.Label(badgeText, badgeStyle);
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndVertical();

            GUILayout.FlexibleSpace();

            EditorGUILayout.BeginVertical();
            GUILayout.FlexibleSpace();
            if (v.isInstalled)
            {
                if (GUILayout.Button("Remove", GUILayout.Width(80), GUILayout.Height(28)))
                    RemovePackage();
            }
            else
            {
                var label = string.IsNullOrEmpty(drawInstalled) ? "Install" : "Switch";
                if (GUILayout.Button(label, GUILayout.Width(80), GUILayout.Height(28)))
                    InstallVersion(v.version);
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndVertical();

            GUILayout.Space(12);
            EditorGUILayout.EndHorizontal();

            var sepRect = GUILayoutUtility.GetRect(0, 1, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(sepRect, new Color(0.5f, 0.5f, 0.5f, 0.15f));
        }

        private static void DrawIcon(params string[] names)
        {
            var rect = GUILayoutUtility.GetRect(20, 20, GUILayout.Width(20));
            foreach (var name in names)
            {
                var tex = EditorGUIUtility.IconContent(name).image;
                if (tex == null) continue;
                GUI.DrawTexture(rect, tex, ScaleMode.ScaleToFit);
                return;
            }
        }

        private void FetchVersions()
        {
            fetching = true;
            versions.Clear();
            versionsStatus = "";

            var manifestToken = ReadTokenFromManifest();
            string baseUrl;
            string authToken = null;
            if (!string.IsNullOrEmpty(manifestToken))
            {
                baseUrl = $"{RegistryUrl}/t/{manifestToken}";
            }
            else
            {
                authToken = ReadConfiguredToken();
                if (string.IsNullOrEmpty(authToken))
                {
                    fetching = false;
                    versionsStatus = "No token configured. Use the Setup tab first.";
                    versionsStatusType = MessageType.Warning;
                    tab = 0;
                    return;
                }
                baseUrl = RegistryUrl;
            }

            var url = $"{baseUrl}/{PackageName}" + (showPreRelease ? "?prerelease=true" : "");
            var request = UnityWebRequest.Get(url);
            if (authToken != null)
                request.SetRequestHeader("Authorization", $"Bearer {authToken}");

            var op = request.SendWebRequest();
            op.completed += _ =>
            {
                fetching = false;

                if (request.result != UnityWebRequest.Result.Success)
                {
                    versionsStatus = $"Failed: {request.error}";
                    versionsStatusType = MessageType.Error;
                    request.Dispose();
                    Repaint();
                    return;
                }

                try
                {
                    ParseVersionsResponse(request.downloadHandler.text);
                }
                catch (Exception e)
                {
                    versionsStatus = $"Parse error: {e.Message}";
                    versionsStatusType = MessageType.Error;
                }

                request.Dispose();
                Repaint();
            };
        }

        private void ParseVersionsResponse(string json)
        {
            var root = MiniJson.Parse(json) as Dictionary<string, object>;
            if (root == null) return;

            var distTags = root.TryGetValue("dist-tags", out var dt)
                ? dt as Dictionary<string, object> : null;
            var latest = distTags != null && distTags.TryGetValue("latest", out var lt)
                ? lt as string : "";

            var versionMap = root.TryGetValue("versions", out var vs)
                ? vs as Dictionary<string, object> : null;
            if (versionMap == null) return;

            versions = versionMap.Keys
                .Select(v => new VersionEntry
                {
                    version = v,
                    isPreRelease = v.Contains("-"),
                    isInstalled = v == installedVersion,
                    isLatest = v == latest
                })
                .OrderByDescending(v => v.version, new SemVerComparer())
                .ToList();
        }

        private void DetectInstalledVersion()
        {
            installedVersion = "";
            if (!File.Exists(ManifestPath)) return;

            var manifest = MiniJson.Parse(File.ReadAllText(ManifestPath)) as Dictionary<string, object>;
            if (manifest == null) return;

            if (manifest.TryGetValue("dependencies", out var deps) &&
                deps is Dictionary<string, object> depsDict &&
                depsDict.TryGetValue(PackageName, out var ver) &&
                ver is string verStr)
            {
                installedVersion = verStr;
            }
        }

        private static bool EnsureChannel(bool preRelease)
        {
            var token = ReadTokenFromManifest();
            if (token == null && preRelease) token = ReadConfiguredToken();
            if (string.IsNullOrEmpty(token)) return false;
            var url = $"{RegistryUrl}/t/{token}" + (preRelease ? "/pre" : "");
            if (ReadRegistryUrl() == url) return false;
            ManifestEditor.EnsureScopedRegistry(url, ScopeName, Scope);
            return true;
        }

        private void InstallVersion(string version)
        {
            try
            {
                EnsureChannel(version.Contains("-"));
                var manifest = MiniJson.Parse(File.ReadAllText(ManifestPath)) as Dictionary<string, object>;
                if (manifest == null) return;

                if (manifest.TryGetValue("dependencies", out var deps) && deps is Dictionary<string, object> d)
                    d[PackageName] = version;

                File.WriteAllText(ManifestPath, MiniJson.Serialize(manifest, pretty: true) + "\n");
                UnityEditor.PackageManager.Client.Resolve();

                installedVersion = version;
                versionsStatus = $"Switched to {version}";
                versionsStatusType = MessageType.Info;
                RefreshInstalledFlags();
            }
            catch (Exception e)
            {
                versionsStatus = $"Install failed: {e.Message}";
                versionsStatusType = MessageType.Error;
                Debug.LogError($"[UniText] {e}");
            }
        }

        private void RemovePackage()
        {
            if (!EditorUtility.DisplayDialog("Remove UniText",
                "Are you sure you want to remove UniText from this project?", "Remove", "Cancel"))
                return;

            try
            {
                EnsureChannel(false);
                var manifest = MiniJson.Parse(File.ReadAllText(ManifestPath)) as Dictionary<string, object>;
                if (manifest == null) return;

                if (manifest.TryGetValue("dependencies", out var deps) && deps is Dictionary<string, object> d)
                    d.Remove(PackageName);

                File.WriteAllText(ManifestPath, MiniJson.Serialize(manifest, pretty: true) + "\n");
                UnityEditor.PackageManager.Client.Resolve();

                installedVersion = "";
                versionsStatus = "Package removed";
                versionsStatusType = MessageType.Info;
                RefreshInstalledFlags();
            }
            catch (Exception e)
            {
                versionsStatus = $"Remove failed: {e.Message}";
                versionsStatusType = MessageType.Error;
                Debug.LogError($"[UniText] {e}");
            }
        }

        private void RefreshInstalledFlags()
        {
            for (var i = 0; i < versions.Count; i++)
            {
                var v = versions[i];
                v.isInstalled = v.version == installedVersion;
                versions[i] = v;
            }
        }

        private void EmbedUguiVersion(string version)
        {
            var manifestToken = ReadTokenFromManifest();
            string baseUrl;
            string authToken = null;
            if (!string.IsNullOrEmpty(manifestToken))
            {
                baseUrl = $"{RegistryUrl}/t/{manifestToken}";
            }
            else
            {
                authToken = ReadConfiguredToken();
                if (string.IsNullOrEmpty(authToken))
                {
                    uguiStatus = "Set up your access token first (Setup tab).";
                    uguiStatusType = MessageType.Warning;
                    return;
                }
                baseUrl = RegistryUrl;
            }

            uguiBusy = true;
            uguiStatus = $"Downloading Unity UI {version}…";
            uguiStatusType = MessageType.Info;
            Repaint();

            var tarballUrl = $"{baseUrl}/{UguiPackage}/-/{UguiPackage}-{version}.tgz";
            var dlReq = UnityWebRequest.Get(tarballUrl);
            if (authToken != null) dlReq.SetRequestHeader("Authorization", $"Bearer {authToken}");
            dlReq.SendWebRequest().completed += _ =>
            {
                uguiBusy = false;
                if (dlReq.result != UnityWebRequest.Result.Success)
                {
                    uguiStatus = $"Download failed: {dlReq.error}";
                    uguiStatusType = MessageType.Error;
                    dlReq.Dispose();
                    Repaint();
                    return;
                }

                var data = dlReq.downloadHandler.data;
                dlReq.Dispose();
                try
                {
                    ExtractEmbeddedPackage(data);
                    // The fork keeps its own real version (e.g. 2.0.2). A file: package wins by
                    // source priority over the built-in regardless of version number, so there's
                    // no need to match the editor's built-in uGUI version anymore.
                    // Use the official async Client.Add (not a manual manifest edit + soft
                    // Client.Resolve, which doesn't reliably re-resolve a core package). Add
                    // writes the file: dependency AND forces a full resolve + domain reload, so
                    // the fork actually takes over the built-in this session — not after restart.
                    uguiBusy = true;
                    uguiStatus = $"Installing Unity UI {version} (resolving packages, the editor will reload)…";
                    uguiStatusType = MessageType.Info;
                    var add = UnityEditor.PackageManager.Client.Add($"{UguiPackage}@{UguiFileRef}");
                    TrackUguiRequest(add, () =>
                    {
                        if (add.Status == UnityEditor.PackageManager.StatusCode.Success)
                        {
                            SessionState.EraseString(PendingUguiCleanupKey);
                            DetectEmbeddedUguiVersion();
                            RefreshUguiInstalledFlags();
                            uguiStatus = $"Unity UI {version} installed — overrides the built-in via LocalPackages.";
                            uguiStatusType = MessageType.Info;
                        }
                        else
                        {
                            uguiStatus = $"Install failed: {add.Error?.message}";
                            uguiStatusType = MessageType.Error;
                        }
                    });
                }
                catch (Exception e)
                {
                    uguiBusy = false;
                    DetectEmbeddedUguiVersion();
                    uguiStatus = $"Install failed: {e.Message}";
                    uguiStatusType = MessageType.Error;
                    Debug.LogError($"[UniText] {e}");
                }
                Repaint();
            };
        }

        private void RemoveEmbeddedUgui()
        {
            try
            {
                uguiBusy = true;
                uguiStatus = "Reverting to the built-in Unity UI (resolving packages, the editor will reload)…";
                uguiStatusType = MessageType.Info;
                // Stash the folder so a domain reload (triggered by the resolve) can still finish
                // the cleanup even though it destroys this callback — see FinishPendingUguiCleanup.
                SessionState.SetString(PendingUguiCleanupKey, EmbeddedUguiPath);

                // Client.Remove drops the direct file: entry and forces a real resolve back to
                // the editor's built-in uGUI. (Manual edit + soft Client.Resolve didn't take
                // effect until restart — that was the "Remove does nothing" bug.)
                var rm = UnityEditor.PackageManager.Client.Remove(UguiPackage);
                TrackUguiRequest(rm, () =>
                {
                    if (rm.Status == UnityEditor.PackageManager.StatusCode.Success)
                    {
                        TryDeleteEmbeddedUgui();
                        SessionState.EraseString(PendingUguiCleanupKey);
                        DetectEmbeddedUguiVersion();
                        RefreshUguiInstalledFlags();
                        uguiStatus = "Reverted to Unity's built-in Unity UI.";
                        uguiStatusType = MessageType.Info;
                    }
                    else
                    {
                        // Fallback if Client.Remove refuses the core package: drop the entry by
                        // hand and soft-resolve. May need a restart to fully apply.
                        ManifestEditor.RemoveDependency(UguiPackage);
                        TryDeleteEmbeddedUgui();
                        SessionState.EraseString(PendingUguiCleanupKey);
                        DetectEmbeddedUguiVersion();
                        RefreshUguiInstalledFlags();
                        UnityEditor.PackageManager.Client.Resolve();
                        uguiStatus = "Reverted to built-in. Restart Unity if Package Manager still lists the fork.";
                        uguiStatusType = MessageType.Warning;
                    }
                });
            }
            catch (Exception e)
            {
                uguiBusy = false;
                SessionState.EraseString(PendingUguiCleanupKey);
                DetectEmbeddedUguiVersion();
                uguiStatus = $"Revert failed: {e.Message}";
                uguiStatusType = MessageType.Error;
                Debug.LogError($"[UniText] {e}");
            }
        }

        private void DetectEmbeddedUguiVersion()
        {
            uguiInstalledVersion = "";
            var pkgJson = Path.Combine(EmbeddedUguiPath, "package.json");
            if (!File.Exists(pkgJson)) return;
            var manifest = MiniJson.Parse(File.ReadAllText(pkgJson)) as Dictionary<string, object>;
            if (manifest == null) return;
            if (manifest.TryGetValue("version", out var v) && v is string s)
                uguiInstalledVersion = s;
        }

        private void RefreshUguiInstalledFlags()
        {
            for (var i = 0; i < uguiVersions.Count; i++)
            {
                var v = uguiVersions[i];
                v.isInstalled = v.version == uguiInstalledVersion;
                uguiVersions[i] = v;
            }
        }

        // Polls a Package Manager request to completion. If the resolve changes assemblies it
        // also triggers a domain reload, which kills this callback — that's expected; state is
        // recovered after the reload via DetectEmbeddedUguiVersion and FinishPendingUguiCleanup.
        private void TrackUguiRequest(UnityEditor.PackageManager.Requests.Request request, Action onComplete)
        {
            if (request == null) { uguiBusy = false; return; }
            void Poll()
            {
                if (!request.IsCompleted) return;
                EditorApplication.update -= Poll;
                uguiBusy = false;
                try { onComplete(); }
                catch (Exception e) { Debug.LogError($"[UniText] {e}"); }
                Repaint();
            }
            EditorApplication.update += Poll;
        }

        private static void TryDeleteEmbeddedUgui()
        {
            try
            {
                if (Directory.Exists(EmbeddedUguiPath))
                    Directory.Delete(EmbeddedUguiPath, true);
            }
            catch (Exception e) { Debug.LogError($"[UniText] Could not delete embedded Unity UI: {e.Message}"); }
        }

        private static bool ManifestHasUgui()
        {
            try
            {
                var manifest = MiniJson.Parse(File.ReadAllText(ManifestPath)) as Dictionary<string, object>;
                return manifest != null
                    && manifest.TryGetValue("dependencies", out var d)
                    && d is Dictionary<string, object> deps
                    && deps.ContainsKey(UguiPackage);
            }
            catch { return false; }
        }

        // A successful Client.Remove resolves and then domain-reloads, which destroys the
        // in-flight completion callback before it can delete the orphaned fork folder. This runs
        // on every load and finishes that cleanup once the override is gone from the manifest.
        [InitializeOnLoadMethod]
        private static void FinishPendingUguiCleanup()
        {
            var pending = SessionState.GetString(PendingUguiCleanupKey, "");
            if (string.IsNullOrEmpty(pending)) return;
            if (ManifestHasUgui()) return; // remove didn't land yet; leave the marker
            SessionState.EraseString(PendingUguiCleanupKey);
            TryDeleteEmbeddedUgui();
        }

        private void FetchUguiVersions()
        {
            uguiFetching = true;
            uguiVersions.Clear();
            uguiStatus = "";

            var manifestToken = ReadTokenFromManifest();
            string baseUrl;
            string authToken = null;
            if (!string.IsNullOrEmpty(manifestToken))
            {
                baseUrl = $"{RegistryUrl}/t/{manifestToken}";
            }
            else
            {
                authToken = ReadConfiguredToken();
                if (string.IsNullOrEmpty(authToken))
                {
                    uguiFetching = false;
                    uguiStatus = "No token configured. Use the Setup tab first.";
                    uguiStatusType = MessageType.Warning;
                    return;
                }
                baseUrl = RegistryUrl;
            }

            var url = $"{baseUrl}/{UguiPackage}" + (uguiShowPreRelease ? "?prerelease=true" : "");
            var request = UnityWebRequest.Get(url);
            if (authToken != null) request.SetRequestHeader("Authorization", $"Bearer {authToken}");

            request.SendWebRequest().completed += _ =>
            {
                uguiFetching = false;
                if (request.result != UnityWebRequest.Result.Success)
                {
                    uguiStatus = $"Failed: {request.error}";
                    uguiStatusType = MessageType.Error;
                    request.Dispose();
                    Repaint();
                    return;
                }
                try { ParseUguiVersions(request.downloadHandler.text); }
                catch (Exception e) { uguiStatus = $"Parse error: {e.Message}"; uguiStatusType = MessageType.Error; }
                request.Dispose();
                Repaint();
            };
        }

        private void ParseUguiVersions(string json)
        {
            var root = MiniJson.Parse(json) as Dictionary<string, object>;
            if (root == null) return;

            var distTags = root.TryGetValue("dist-tags", out var dt) ? dt as Dictionary<string, object> : null;
            var latest = distTags != null && distTags.TryGetValue("latest", out var lt) ? lt as string : "";

            var versionMap = root.TryGetValue("versions", out var vs) ? vs as Dictionary<string, object> : null;
            if (versionMap == null) return;

            uguiVersions = versionMap.Keys
                .Select(v => new VersionEntry
                {
                    version = v,
                    isPreRelease = v.Contains("-"),
                    isInstalled = v == uguiInstalledVersion,
                    isLatest = v == latest
                })
                .OrderByDescending(v => v.version, new SemVerComparer())
                .ToList();
        }

        private void DrawUguiTab()
        {
            GUILayout.Space(8);

            var installed = !string.IsNullOrEmpty(drawUguiInstalled);
            var cardRect = EditorGUILayout.BeginHorizontal(GUILayout.Height(50));
            EditorGUI.DrawRect(cardRect, new Color(0.15f, 0.15f, 0.15f, 0.5f));
            EditorGUI.DrawRect(new Rect(cardRect.x, cardRect.y, 3, cardRect.height), installed ? InstalledColor : new Color(0.5f, 0.5f, 0.5f, 0.5f));
            GUILayout.Space(12);
            EditorGUILayout.BeginVertical();
            GUILayout.FlexibleSpace();
            EditorGUILayout.BeginHorizontal();
            if (installed) DrawIcon("d_GreenCheckmark@2x", "GreenCheckmark@2x", "GreenCheckmark");
            GUILayout.Space(4);
            var labelStyle = new GUIStyle(EditorStyles.label) { fontSize = 12, normal = { textColor = new Color(0.7f, 0.7f, 0.7f) } };
            GUILayout.Label(installed ? "Light Side fork (embedded)" : "Unity built-in", labelStyle);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            var vStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 20 };
            GUILayout.Label(installed ? drawUguiInstalled : "stock uGUI", vStyle);
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndVertical();
            GUILayout.Space(12);
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(12);

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar, GUILayout.Height(22));
            var prev = uguiShowPreRelease;
            uguiShowPreRelease = GUILayout.Toggle(uguiShowPreRelease, " Pre-release", EditorStyles.toolbarButton, GUILayout.Width(100));
            if (prev != uguiShowPreRelease) FetchUguiVersions();
            GUILayout.FlexibleSpace();
            GUI.enabled = !drawUguiFetching && !uguiBusy;
            if (GUILayout.Button(new GUIContent(" Refresh", EditorGUIUtility.IconContent("Refresh").image), EditorStyles.toolbarButton, GUILayout.Width(75)))
                FetchUguiVersions();
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            if (drawUguiFetching)
            {
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField("Loading...", EditorStyles.centeredGreyMiniLabel);
                GUILayout.FlexibleSpace();
                return;
            }

            if (drawUguiVersions.Count == 0)
            {
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField("No versions found", EditorStyles.centeredGreyMiniLabel);
                GUILayout.FlexibleSpace();
            }
            else
            {
                GUILayout.Space(8);
                uguiScrollPos = EditorGUILayout.BeginScrollView(uguiScrollPos);
                foreach (var v in drawUguiVersions)
                    DrawUguiVersionRow(v);
                EditorGUILayout.EndScrollView();
            }

            if (!string.IsNullOrEmpty(uguiStatus))
            {
                GUILayout.Space(4);
                EditorGUILayout.HelpBox(uguiStatus, uguiStatusType);
            }
        }

        private void DrawUguiVersionRow(VersionEntry v)
        {
            var rowRect = EditorGUILayout.BeginHorizontal(GUILayout.Height(48));

            if (v.isInstalled)
                EditorGUI.DrawRect(rowRect, new Color(0.3f, 0.8f, 0.45f, 0.07f));

            var barColor = v.isInstalled ? InstalledColor : v.isLatest ? LatestColor : v.isPreRelease ? PreReleaseColor : new Color(0.5f, 0.5f, 0.5f, 0.3f);
            EditorGUI.DrawRect(new Rect(rowRect.x, rowRect.y + 2, 3, rowRect.height - 4), barColor);

            GUILayout.Space(12);

            EditorGUILayout.BeginVertical();
            GUILayout.FlexibleSpace();

            var verStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 14 };
            GUILayout.Label(v.version, verStyle);

            var badgeText = "";
            var badgeColor = Color.grey;
            if (v.isInstalled) { badgeText = "Installed"; badgeColor = InstalledColor; }
            else if (v.isLatest) { badgeText = "Latest"; badgeColor = LatestColor; }
            if (v.isPreRelease) { badgeText += (badgeText.Length > 0 ? " · " : "") + "Pre-release"; if (!v.isInstalled) badgeColor = PreReleaseColor; }

            if (badgeText.Length > 0)
            {
                var badgeStyle = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = badgeColor } };
                GUILayout.Label(badgeText, badgeStyle);
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndVertical();

            GUILayout.FlexibleSpace();

            EditorGUILayout.BeginVertical();
            GUILayout.FlexibleSpace();
            GUI.enabled = !uguiBusy && !drawUguiFetching;
            if (v.isInstalled)
            {
                if (GUILayout.Button("Remove", GUILayout.Width(80), GUILayout.Height(28)))
                    RemoveEmbeddedUgui();
            }
            else
            {
                var label = string.IsNullOrEmpty(drawUguiInstalled) ? "Install" : "Switch";
                if (GUILayout.Button(label, GUILayout.Width(80), GUILayout.Height(28)))
                    EmbedUguiVersion(v.version);
            }
            GUI.enabled = true;
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndVertical();

            GUILayout.Space(12);
            EditorGUILayout.EndHorizontal();

            var sepRect = GUILayoutUtility.GetRect(0, 1, GUILayout.ExpandWidth(true));
            EditorGUI.DrawRect(sepRect, new Color(0.5f, 0.5f, 0.5f, 0.15f));
        }

        // Extracts an npm tarball (.tgz) into the embedded package folder using pure C#
        // (gzip + a minimal ustar/pax TAR reader). No dependency on a system `tar`, whose
        // path handling is unreliable when spawned on Windows.
        private static void ExtractEmbeddedPackage(byte[] tarball)
        {
            var dest = EmbeddedUguiPath;
            if (Directory.Exists(dest)) Directory.Delete(dest, true);
            Directory.CreateDirectory(dest);

            byte[] tar;
            using (var input = new MemoryStream(tarball))
            using (var gz = new System.IO.Compression.GZipStream(input, System.IO.Compression.CompressionMode.Decompress))
            using (var output = new MemoryStream())
            {
                gz.CopyTo(output);
                tar = output.ToArray();
            }

            var pos = 0;
            string nameOverride = null;
            var wrote = 0;
            while (pos + 512 <= tar.Length)
            {
                if (IsZeroBlock(tar, pos)) break;

                var name = ReadTarString(tar, pos, 100);
                var prefix = ReadTarString(tar, pos + 345, 155);
                var size = (int)ParseOctal(ReadTarString(tar, pos + 124, 12));
                var typeFlag = (char)tar[pos + 156];
                pos += 512;

                var fullName = string.IsNullOrEmpty(prefix) ? name : prefix + "/" + name;
                if (nameOverride != null) { fullName = nameOverride; nameOverride = null; }

                // pax extended headers ('x' for next entry, 'g' global) carry long paths
                if (typeFlag == 'x' || typeFlag == 'g')
                {
                    var paxPath = ParsePaxPath(Encoding.UTF8.GetString(tar, pos, size));
                    if (typeFlag == 'x' && paxPath != null) nameOverride = paxPath;
                    pos += RoundUp512(size);
                    continue;
                }

                var rel = StripFirstComponent(fullName); // drop the leading "package/"
                if (typeFlag == '5' || fullName.EndsWith("/"))
                {
                    if (rel.Length > 0) Directory.CreateDirectory(Path.Combine(dest, rel));
                }
                else if (typeFlag == '0' || typeFlag == '\0')
                {
                    if (rel.Length > 0)
                    {
                        var outPath = Path.Combine(dest, rel.Replace('/', Path.DirectorySeparatorChar));
                        Directory.CreateDirectory(Path.GetDirectoryName(outPath));
                        var bytes = new byte[size];
                        Array.Copy(tar, pos, bytes, 0, size);
                        File.WriteAllBytes(outPath, bytes);
                        wrote++;
                    }
                }

                pos += RoundUp512(size);
            }

            if (wrote == 0)
                throw new Exception("Tarball contained no package files.");
            Debug.Log($"[UniText] Unity UI extracted to {dest} ({wrote} files)");
        }

        private static bool IsZeroBlock(byte[] b, int off)
        {
            for (var i = 0; i < 512; i++)
                if (b[off + i] != 0) return false;
            return true;
        }

        private static string ReadTarString(byte[] b, int off, int len)
        {
            var end = off;
            var max = Math.Min(off + len, b.Length);
            while (end < max && b[end] != 0) end++;
            return Encoding.UTF8.GetString(b, off, end - off);
        }

        private static long ParseOctal(string s)
        {
            s = s.Trim();
            long v = 0;
            foreach (var c in s)
            {
                if (c < '0' || c > '7') break;
                v = v * 8 + (c - '0');
            }
            return v;
        }

        private static int RoundUp512(int n) => (n + 511) / 512 * 512;

        private static string StripFirstComponent(string path)
        {
            path = path.Replace('\\', '/').TrimStart('/');
            var idx = path.IndexOf('/');
            return idx < 0 ? "" : path.Substring(idx + 1);
        }

        private static string ParsePaxPath(string pax)
        {
            foreach (var line in pax.Split('\n'))
            {
                var sp = line.IndexOf(' ');
                if (sp < 0) continue;
                var kv = line.Substring(sp + 1);
                var eq = kv.IndexOf('=');
                if (eq > 0 && kv.Substring(0, eq) == "path")
                    return kv.Substring(eq + 1).TrimEnd('\r', '\n');
            }
            return null;
        }
    }

    internal class SemVerComparer : IComparer<string>
    {
        public int Compare(string a, string b)
        {
            if (a == b) return 0;
            if (a == null) return -1;
            if (b == null) return 1;

            var pa = Parse(a);
            var pb = Parse(b);

            for (var i = 0; i < 3; i++)
            {
                var cmp = pa.nums[i].CompareTo(pb.nums[i]);
                if (cmp != 0) return cmp;
            }

            if (pa.pre == "" && pb.pre != "") return 1;
            if (pa.pre != "" && pb.pre == "") return -1;
            return string.Compare(pa.pre, pb.pre, StringComparison.Ordinal);
        }

        private static (int[] nums, string pre) Parse(string v)
        {
            var pre = "";
            var dash = v.IndexOf('-');
            if (dash >= 0) { pre = v.Substring(dash + 1); v = v.Substring(0, dash); }
            var parts = v.Split('.');
            var nums = new int[3];
            for (var i = 0; i < Math.Min(parts.Length, 3); i++)
                int.TryParse(parts[i], out nums[i]);
            return (nums, pre);
        }
    }

    internal static class UpmConfigWriter
    {
        public static void SetAuth(string registryUrl, string token)
        {
            var configPath = GetConfigPath();
            var dir = Path.GetDirectoryName(configPath);
            if (dir != null && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

            var sectionHeader = $"[npmAuth.\"{registryUrl}\"]";
            var newBlock = new[] { sectionHeader, $"token = \"{token}\"", "alwaysAuth = true" };

            if (!File.Exists(configPath))
            {
                File.WriteAllLines(configPath, newBlock);
                Debug.Log($"[UniText] Created {configPath}");
                return;
            }

            Backup(configPath);
            var lines = File.ReadAllLines(configPath).ToList();
            RemoveSection(lines, sectionHeader);
            if (lines.Count > 0 && !string.IsNullOrWhiteSpace(lines.Last())) lines.Add("");
            lines.AddRange(newBlock);
            File.WriteAllLines(configPath, lines);
            Debug.Log($"[UniText] Auth configured in {configPath}");
        }

        private static void RemoveSection(List<string> lines, string header)
        {
            var start = lines.FindIndex(l => l.Trim() == header);
            if (start < 0) return;
            var end = start + 1;
            while (end < lines.Count && !(lines[end].Trim().StartsWith("[") && !lines[end].Trim().StartsWith("[["))) end++;
            if (start > 0 && string.IsNullOrWhiteSpace(lines[start - 1])) start--;
            lines.RemoveRange(start, end - start);
        }

        public static string GetConfigPath()
        {
            var env = Environment.GetEnvironmentVariable("UPM_USER_CONFIG_FILE");
            return !string.IsNullOrEmpty(env) ? env :
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".upmconfig.toml");
        }

        private static void Backup(string path)
        {
            File.Copy(path, path + ".backup", overwrite: true);
            Debug.Log($"[UniText] Backup saved to {path}.backup");
        }
    }

    internal static class ManifestEditor
    {
        public static void EnsureScopedRegistry(string url, string name, string scope)
        {
            var path = Path.Combine(Application.dataPath, "..", "Packages", "manifest.json");
            if (!File.Exists(path)) throw new FileNotFoundException("manifest.json not found");

            Backup(path);
            var manifest = MiniJson.Parse(File.ReadAllText(path)) as Dictionary<string, object>
                ?? throw new InvalidOperationException("Failed to parse manifest.json");

            var registries = manifest.TryGetValue("scopedRegistries", out var ex) && ex is List<object> list
                ? list : new List<object>();

            var filtered = new List<object>();
            foreach (var entry in registries)
            {
                if (entry is Dictionary<string, object> reg &&
                    reg.TryGetValue("scopes", out var sc) && sc is List<object> scopes &&
                    scopes.Any(s => s is string ss && ss == scope))
                {
                    continue;
                }
                filtered.Add(entry);
            }

            filtered.Add(new Dictionary<string, object>
            {
                ["name"] = name,
                ["url"] = url,
                ["scopes"] = new List<object> { scope },
            });

            manifest["scopedRegistries"] = filtered;
            File.WriteAllText(path, MiniJson.Serialize(manifest, pretty: true) + "\n");
            Debug.Log("[UniText] Scoped registry configured in manifest.json");
        }

        public static void SetDependency(string name, string value)
        {
            var path = Path.Combine(Application.dataPath, "..", "Packages", "manifest.json");
            if (!File.Exists(path)) throw new FileNotFoundException("manifest.json not found");
            Backup(path);
            var manifest = MiniJson.Parse(File.ReadAllText(path)) as Dictionary<string, object>
                ?? throw new InvalidOperationException("Failed to parse manifest.json");
            var deps = manifest.TryGetValue("dependencies", out var d) && d is Dictionary<string, object> dict
                ? dict : new Dictionary<string, object>();
            deps[name] = value;
            manifest["dependencies"] = deps;
            File.WriteAllText(path, MiniJson.Serialize(manifest, pretty: true) + "\n");
            Debug.Log($"[UniText] manifest dependency set: {name} = {value}");
        }

        public static void RemoveDependency(string name)
        {
            var path = Path.Combine(Application.dataPath, "..", "Packages", "manifest.json");
            if (!File.Exists(path)) return;
            var manifest = MiniJson.Parse(File.ReadAllText(path)) as Dictionary<string, object>;
            if (manifest == null) return;
            if (manifest.TryGetValue("dependencies", out var d) && d is Dictionary<string, object> deps && deps.Remove(name))
            {
                Backup(path);
                File.WriteAllText(path, MiniJson.Serialize(manifest, pretty: true) + "\n");
                Debug.Log($"[UniText] manifest dependency removed: {name}");
            }
        }

        private static void Backup(string path)
        {
            File.Copy(path, path + ".backup", overwrite: true);
            Debug.Log($"[UniText] Backup saved to {path}.backup");
        }
    }

    internal static class MiniJson
    {
        public static object Parse(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            var i = 0;
            return ParseValue(json, ref i);
        }

        private static object ParseValue(string j, ref int i)
        {
            Skip(j, ref i);
            if (i >= j.Length) return null;
            return j[i] switch
            {
                '{' => ParseObject(j, ref i),
                '[' => ParseArray(j, ref i),
                '"' => ParseString(j, ref i),
                't' or 'f' => ParseBool(j, ref i),
                'n' => ParseNull(j, ref i),
                _ => ParseNumber(j, ref i)
            };
        }

        private static Dictionary<string, object> ParseObject(string j, ref int i)
        {
            var o = new Dictionary<string, object>();
            i++; Skip(j, ref i);
            if (i < j.Length && j[i] == '}') { i++; return o; }
            while (i < j.Length)
            {
                Skip(j, ref i);
                var k = ParseString(j, ref i);
                Skip(j, ref i); i++;
                o[k] = ParseValue(j, ref i);
                Skip(j, ref i);
                if (i < j.Length && j[i] == ',') i++; else break;
            }
            if (i < j.Length && j[i] == '}') i++;
            return o;
        }

        private static List<object> ParseArray(string j, ref int i)
        {
            var a = new List<object>();
            i++; Skip(j, ref i);
            if (i < j.Length && j[i] == ']') { i++; return a; }
            while (i < j.Length)
            {
                a.Add(ParseValue(j, ref i));
                Skip(j, ref i);
                if (i < j.Length && j[i] == ',') i++; else break;
            }
            if (i < j.Length && j[i] == ']') i++;
            return a;
        }

        private static string ParseString(string j, ref int i)
        {
            i++;
            var sb = new StringBuilder();
            while (i < j.Length)
            {
                var c = j[i++];
                if (c == '"') break;
                if (c == '\\' && i < j.Length)
                {
                    var n = j[i++];
                    switch (n)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'u':
                            sb.Append((char)Convert.ToInt32(j.Substring(i, 4), 16));
                            i += 4;
                            break;
                        default: sb.Append(n); break;
                    }
                }
                else sb.Append(c);
            }
            return sb.ToString();
        }

        private static double ParseNumber(string j, ref int i)
        {
            var s = i;
            while (i < j.Length && "0123456789.eE+-".IndexOf(j[i]) >= 0) i++;
            return double.Parse(j.Substring(s, i - s), System.Globalization.CultureInfo.InvariantCulture);
        }

        private static bool ParseBool(string j, ref int i) { if (j.Substring(i, 4) == "true") { i += 4; return true; } i += 5; return false; }
        private static object ParseNull(string j, ref int i) { i += 4; return null; }
        private static void Skip(string j, ref int i) { while (i < j.Length && char.IsWhiteSpace(j[i])) i++; }

        public static string Serialize(object obj, bool pretty = false)
        {
            var sb = new StringBuilder();
            Write(sb, obj, pretty, 0);
            return sb.ToString();
        }

        private static void Write(StringBuilder sb, object v, bool p, int d)
        {
            if (v == null) sb.Append("null");
            else if (v is Dictionary<string, object> dict) WriteObj(sb, dict, p, d);
            else if (v is List<object> list) WriteArr(sb, list, p, d);
            else if (v is string s) WriteStr(sb, s);
            else if (v is bool b) sb.Append(b ? "true" : "false");
            else if (v is double n) sb.Append(n.ToString(System.Globalization.CultureInfo.InvariantCulture));
            else sb.Append(v);
        }

        private static void WriteObj(StringBuilder sb, Dictionary<string, object> o, bool p, int d)
        {
            sb.Append('{');
            var f = true;
            foreach (var kv in o) { if (!f) sb.Append(','); f = false; if (p) { sb.Append('\n'); Ind(sb, d + 1); } WriteStr(sb, kv.Key); sb.Append(p ? ": " : ":"); Write(sb, kv.Value, p, d + 1); }
            if (p && o.Count > 0) { sb.Append('\n'); Ind(sb, d); }
            sb.Append('}');
        }

        private static void WriteArr(StringBuilder sb, List<object> a, bool p, int d)
        {
            sb.Append('[');
            for (var i = 0; i < a.Count; i++) { if (i > 0) sb.Append(','); if (p) { sb.Append('\n'); Ind(sb, d + 1); } Write(sb, a[i], p, d + 1); }
            if (p && a.Count > 0) { sb.Append('\n'); Ind(sb, d); }
            sb.Append(']');
        }

        private static void WriteStr(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (var c in s) sb.Append(c switch { '"' => "\\\"", '\\' => "\\\\", '\n' => "\\n", '\r' => "\\r", '\t' => "\\t", _ => c < 0x20 ? $"\\u{(int)c:x4}" : c.ToString() });
            sb.Append('"');
        }

        private static void Ind(StringBuilder sb, int d) { for (var i = 0; i < d; i++) sb.Append("  "); }
    }
}
