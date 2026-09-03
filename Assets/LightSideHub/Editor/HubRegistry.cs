using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor.PackageManager;
using UnityEngine.Networking;

namespace LightSide.Hub
{
    /// <summary>One published version of a package.</summary>
    internal readonly struct HubVersion
    {
        public HubVersion(string version, bool latest)
        {
            Version = version;
            IsLatest = latest;
        }

        public string Version { get; }
        public bool IsLatest { get; }
        public bool IsPreRelease => SemVerComparer.IsPreRelease(Version);
    }

    /// <summary>
    /// The LightSide package registry as the project is wired to it. Two credential shapes reach the
    /// same registry: a token carried in the scoped-registry URL, which the Hub writes, and a token in
    /// the user's <c>.upmconfig.toml</c>, which a hand-configured project may use instead. The
    /// pre-release channel exists only on the URL form, so a project on the header form sees releases
    /// only.
    /// </summary>
    internal static class HubRegistry
    {
        private const string PreReleaseSegment = "/pre";

        /// <summary>Shape of a token issued for the registry: the current prefixed form, or a bare UUID from an older issue.</summary>
        public static readonly Regex TokenPattern = new(
            @"^(lst_[A-Za-z0-9]{32}|[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})$",
            RegexOptions.Compiled);

        private static readonly Regex UrlToken = new(
            @"/t/([A-Za-z0-9_\-]+)(?:/pre)?$", RegexOptions.Compiled);

        /// <summary>Whether the project can reach the registry at all.</summary>
        public static bool IsConfigured => !string.IsNullOrEmpty(Token);

        /// <summary>Whether the project is pointed at the channel that also serves pre-release versions.</summary>
        public static bool PreReleaseChannel
        {
            get
            {
                var url = ProjectManifest.RegistryUrl(HubConfig.Scope);
                return url != null && url.EndsWith(PreReleaseSegment, StringComparison.Ordinal);
            }
        }

        /// <summary>Whether the pre-release channel can be selected at all, which the header credential form cannot do.</summary>
        public static bool SupportsPreRelease => !string.IsNullOrEmpty(ManifestToken);

        /// <summary>The token the project authenticates with, from the manifest URL or the user config; null when neither holds one.</summary>
        public static string Token => ManifestToken ?? UpmConfig.ReadToken(HubConfig.RegistryUrl);

        private static string ManifestToken
        {
            get
            {
                var url = ProjectManifest.RegistryUrl(HubConfig.Scope);
                if (string.IsNullOrEmpty(url)) return null;
                var match = UrlToken.Match(url);
                return match.Success ? match.Groups[1].Value : null;
            }
        }

        /// <summary>
        /// Points the project at <paramref name="token"/>'s channel. The token travels in the registry
        /// URL, which is what the pre-release channel is selected by.
        /// </summary>
        public static void Configure(string token, bool preRelease)
            => ProjectManifest.SetScopedRegistry(
                ChannelUrl(token, preRelease), HubConfig.RegistryDisplayName, HubConfig.Scope);

        /// <summary>
        /// Moves the project to the channel that can serve <paramref name="version"/>. A project whose
        /// token lives in the user configuration has only one channel and stays where it is.
        /// </summary>
        private static void EnsureChannelFor(string version)
        {
            var token = ManifestToken;
            if (string.IsNullOrEmpty(token)) return;

            var wanted = ChannelUrl(token, SemVerComparer.IsPreRelease(version));
            if (ProjectManifest.RegistryUrl(HubConfig.Scope) == wanted) return;

            ProjectManifest.SetScopedRegistry(wanted, HubConfig.RegistryDisplayName, HubConfig.Scope);
        }

        /// <summary>A request for a package's registry metadata, already carrying whichever credential the project uses.</summary>
        public static UnityWebRequest MetadataRequest(string packageName, bool includePreRelease)
        {
            var token = ManifestToken;
            var baseUrl = token != null
                ? $"{HubConfig.RegistryUrl}/t/{token}"
                : HubConfig.RegistryUrl;

            var url = $"{baseUrl}/{packageName}" + (includePreRelease ? "?prerelease=true" : "");
            var request = UnityWebRequest.Get(url);
            if (token == null)
            {
                var header = UpmConfig.ReadToken(HubConfig.RegistryUrl);
                if (!string.IsNullOrEmpty(header))
                    request.SetRequestHeader("Authorization", "Bearer " + header);
            }
            return request;
        }

        /// <summary>
        /// A request for the packages <paramref name="token"/> grants. It answers both questions the
        /// Hub has about a token — whether it is valid, and what it covers — in one call.
        /// </summary>
        public static UnityWebRequest EntitlementsRequest(string token)
            => UnityWebRequest.Get($"{HubConfig.RegistryUrl}/t/{token}/-/all");

        /// <summary>The package names in an entitlement listing; the metadata keys around them are ignored.</summary>
        public static HashSet<string> ParseEntitlements(string json)
        {
            var entitled = new HashSet<string>(StringComparer.Ordinal);
            if (MiniJson.Parse(json) is not Dictionary<string, object> root) return entitled;
            foreach (var pair in root)
                if (pair.Value is Dictionary<string, object> && !pair.Key.StartsWith("_"))
                    entitled.Add(pair.Key);
            return entitled;
        }

        /// <summary>
        /// The published versions in the registry's package document, newest first.
        /// </summary>
        public static List<HubVersion> ParseVersions(string json)
        {
            var root = MiniJson.Parse(json) as Dictionary<string, object>;
            var versions = MiniJson.Object(root, "versions");
            var parsed = new List<HubVersion>();
            if (versions == null) return parsed;

            var latest = MiniJson.String(MiniJson.Object(root, "dist-tags"), "latest");
            foreach (var version in versions.Keys)
                parsed.Add(new HubVersion(version, version == latest));

            parsed.Sort((a, b) => SemVerComparer.Instance.Compare(b.Version, a.Version));
            return parsed;
        }

        /// <summary>The version tag a package's registry document marks as latest, or null when it names none.</summary>
        public static string ParseLatest(string json)
            => MiniJson.String(
                MiniJson.Object(MiniJson.Parse(json) as Dictionary<string, object>, "dist-tags"),
                "latest");

        /// <summary>The version a package is pinned to in this project, or null when it is not installed.</summary>
        public static string InstalledVersion(string packageName)
            => ProjectManifest.Dependency(packageName);

        /// <summary>Pins <paramref name="packageName"/> to <paramref name="version"/> and asks Unity to resolve.</summary>
        public static void Install(string packageName, string version)
        {
            EnsureChannelFor(version);
            ProjectManifest.SetDependency(packageName, version);
            Client.Resolve();
        }

        /// <summary>Drops <paramref name="packageName"/> from the project and asks Unity to resolve.</summary>
        public static void Remove(string packageName)
        {
            ProjectManifest.RemoveDependency(packageName);
            Client.Resolve();
        }

        private static string ChannelUrl(string token, bool preRelease)
            => $"{HubConfig.RegistryUrl}/t/{token}" + (preRelease ? PreReleaseSegment : "");
    }
}
