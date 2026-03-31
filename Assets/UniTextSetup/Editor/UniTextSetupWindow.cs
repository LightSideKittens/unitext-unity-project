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

        private static readonly Regex TokenPattern = new(@"^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$");

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
        private Vector2 scrollPos;

        private struct VersionEntry
        {
            public string version;
            public bool isPreRelease;
            public bool isInstalled;
            public bool isLatest;
        }

        [MenuItem("Light Side/UniText Setup", false, 0)]
        public static void Open()
        {
            var window = GetWindow<UniTextSetupWindow>("UniText");
            window.minSize = new Vector2(480, 400);
            window.Show();
        }

        public static bool IsSetupNeeded()
        {
            return !File.Exists(ManifestPath) || !File.ReadAllText(ManifestPath).Contains(RegistryUrl);
        }

        private void OnEnable()
        {
            tab = IsSetupNeeded() ? 0 : 1;
            if (tab == 1)
            {
                DetectInstalledVersion();
                FetchVersions();
            }
        }

        private GUIContent[] tabContents;

        private GUIContent[] TabContents => tabContents ??= new[]
        {
            new GUIContent("  Setup", EditorGUIUtility.IconContent("d_Settings").image),
            new GUIContent("  Versions", EditorGUIUtility.IconContent("d_Package Manager").image),
        };

        private void OnGUI()
        {
            GUILayout.Space(8);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            tab = GUILayout.SelectionGrid(tab, TabContents, TabContents.Length, "LargeButton", GUILayout.Height(28));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            GUILayout.Space(4);

            switch (tab)
            {
                case 0: DrawSetupTab(); break;
                case 1: DrawVersionsTab(); break;
            }
        }

        private void DrawSetupTab()
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Configure Registry Access", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            EditorGUILayout.LabelField("Paste your access token from the purchase email:");
            EditorGUILayout.Space(4);
            token = EditorGUILayout.TextField(token);

            var trimmed = token?.Trim() ?? "";
            if (trimmed.Length > 0 && !TokenPattern.IsMatch(trimmed))
                EditorGUILayout.HelpBox("Token should be a UUID (e.g. a1b2c3d4-e5f6-7890-abcd-ef1234567890)", MessageType.Warning);

            EditorGUILayout.Space(8);

            GUI.enabled = TokenPattern.IsMatch(trimmed);
            if (GUILayout.Button(IsSetupNeeded() ? "Set Up" : "Update Token", GUILayout.Height(28)))
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
                setupStatus = "";
                UpmConfigWriter.SetAuth(RegistryUrl, token);
                ManifestEditor.EnsureScopedRegistry(RegistryUrl, ScopeName, Scope);
                UnityEditor.PackageManager.Client.Resolve();
                setupStatus = "Setup complete!";
                setupStatusType = MessageType.Info;
                tab = 1;
                DetectInstalledVersion();
                FetchVersions();
            }
            catch (Exception e)
            {
                setupStatus = $"Setup failed: {e.Message}";
                setupStatusType = MessageType.Error;
                Debug.LogError($"[UniText] {e}");
            }
        }

        private static readonly Color InstalledColor = new(0.3f, 0.8f, 0.45f);
        private static readonly Color LatestColor = new(0.4f, 0.65f, 0.95f);
        private static readonly Color PreReleaseColor = new(0.95f, 0.7f, 0.25f);

        private void DrawVersionsTab()
        {
            GUILayout.Space(8);

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
            GUILayout.Label(string.IsNullOrEmpty(installedVersion) ? "\u2014" : installedVersion, vStyle);

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndVertical();

            GUILayout.Space(12);
            EditorGUILayout.EndHorizontal();

            GUILayout.Space(12);

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar, GUILayout.Height(22));
            var prev = showPreRelease;
            showPreRelease = GUILayout.Toggle(showPreRelease, " Pre-release", EditorStyles.toolbarButton, GUILayout.Width(100));
            if (prev != showPreRelease) FetchVersions();
            GUILayout.FlexibleSpace();
            GUI.enabled = !fetching;
            if (GUILayout.Button(new GUIContent(" Refresh", EditorGUIUtility.IconContent("Refresh").image), EditorStyles.toolbarButton, GUILayout.Width(75)))
                FetchVersions();
            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();

            if (fetching)
            {
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField("Loading...", EditorStyles.centeredGreyMiniLabel);
                GUILayout.FlexibleSpace();
                return;
            }

            if (versions.Count == 0)
            {
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField("No versions found", EditorStyles.centeredGreyMiniLabel);
                GUILayout.FlexibleSpace();
                return;
            }

            GUILayout.Space(8);
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);

            foreach (var v in versions)
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
                var label = string.IsNullOrEmpty(installedVersion) ? "Install" : "Switch";
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

            var authToken = ReadTokenFromConfig();
            if (string.IsNullOrEmpty(authToken))
            {
                fetching = false;
                versionsStatus = "No token configured. Use the Setup tab first.";
                versionsStatusType = MessageType.Warning;
                tab = 0;
                return;
            }

            var url = $"{RegistryUrl}/{PackageName}" + (showPreRelease ? "?prerelease=true" : "");
            var request = UnityWebRequest.Get(url);
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

        private string ReadTokenFromConfig()
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

        private void InstallVersion(string version)
        {
            try
            {
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

            var found = false;
            foreach (var entry in registries)
            {
                if (entry is Dictionary<string, object> reg &&
                    reg.TryGetValue("url", out var u) && u is string s && s == url)
                {
                    reg["name"] = name;
                    reg["scopes"] = new List<object> { scope };
                    found = true;
                    break;
                }
            }

            if (!found)
                registries.Add(new Dictionary<string, object>
                    { ["name"] = name, ["url"] = url, ["scopes"] = new List<object> { scope } });

            manifest["scopedRegistries"] = registries;
            File.WriteAllText(path, MiniJson.Serialize(manifest, pretty: true) + "\n");
            Debug.Log("[UniText] Scoped registry configured in manifest.json");
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
