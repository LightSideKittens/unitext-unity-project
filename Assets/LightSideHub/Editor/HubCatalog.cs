using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace LightSide.Hub
{
    /// <summary>What a product needs from the project beyond its own package.</summary>
    internal sealed class HubRequirement
    {
        /// <summary>Discriminator naming the kind of work involved; a kind the Hub does not implement is skipped.</summary>
        public string Kind;

        /// <summary>Package the requirement is about.</summary>
        public string Package;

        /// <summary>Project-relative folder the requirement materialises into, where the kind uses one.</summary>
        public string Folder;

        /// <summary>One line telling the user why this exists.</summary>
        public string Summary;

        /// <summary>Kind that replaces a Unity package with a LightSide fork embedded in the project.</summary>
        public const string EmbeddedFork = "embeddedFork";
    }

    /// <summary>One LightSide product as the Hub presents it.</summary>
    internal sealed class HubProduct
    {
        public string Id;
        public string PackageName;
        public string DisplayName;
        public string Summary;
        public string DocsUrl;
        public HubRequirement[] Requirements = System.Array.Empty<HubRequirement>();
    }

    /// <summary>
    /// The products the Hub offers. Read from a file published beside the Hub rather than compiled in,
    /// so a new product reaches existing installs without a Hub release; the compiled-in set is what a
    /// fresh offline install starts from, and the last successful fetch is cached under <c>Library</c>.
    /// </summary>
    internal static class HubCatalog
    {
        private static List<HubProduct> products;

        private static string CachePath =>
            Path.Combine(Application.dataPath, "..", "Library", "LightSideHub", "products.json");

        /// <summary>The catalogue, loaded from the cache on first use and never null.</summary>
        public static IReadOnlyList<HubProduct> Products => products ??= Load();

        /// <summary>Replaces the catalogue with <paramref name="json"/> and caches it for the next session.</summary>
        public static void Accept(string json)
        {
            var parsed = ParseProducts(json);
            if (parsed == null || parsed.Count == 0) return;

            products = parsed;
            var directory = Path.GetDirectoryName(CachePath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            File.WriteAllText(CachePath, json);
        }

        private static List<HubProduct> Load()
        {
            if (File.Exists(CachePath))
            {
                var cached = ParseProducts(File.ReadAllText(CachePath));
                if (cached != null && cached.Count > 0) return cached;
            }
            return BuiltIn();
        }

        private static List<HubProduct> ParseProducts(string json)
        {
            var root = MiniJson.Parse(json) as Dictionary<string, object>;
            var entries = MiniJson.Array(root, "products");
            if (entries == null) return null;

            var parsed = new List<HubProduct>(entries.Count);
            foreach (var entry in entries)
            {
                if (entry is not Dictionary<string, object> node) continue;
                var package = MiniJson.String(node, "package");
                if (string.IsNullOrEmpty(package)) continue;

                parsed.Add(new HubProduct
                {
                    Id = MiniJson.String(node, "id") ?? package,
                    PackageName = package,
                    DisplayName = MiniJson.String(node, "name") ?? package,
                    Summary = MiniJson.String(node, "summary") ?? "",
                    DocsUrl = MiniJson.String(node, "docs") ?? "",
                    Requirements = ParseRequirements(MiniJson.Array(node, "requirements")),
                });
            }
            return parsed;
        }

        private static HubRequirement[] ParseRequirements(List<object> entries)
        {
            if (entries == null || entries.Count == 0) return System.Array.Empty<HubRequirement>();

            var parsed = new List<HubRequirement>(entries.Count);
            foreach (var entry in entries)
            {
                if (entry is not Dictionary<string, object> node) continue;
                var kind = MiniJson.String(node, "kind");
                if (string.IsNullOrEmpty(kind)) continue;

                parsed.Add(new HubRequirement
                {
                    Kind = kind,
                    Package = MiniJson.String(node, "package"),
                    Folder = MiniJson.String(node, "folder"),
                    Summary = MiniJson.String(node, "summary") ?? "",
                });
            }
            return parsed.ToArray();
        }

        /// <summary>
        /// The catalogue a Hub carries before it has ever reached the network. It is the same shape the
        /// published file uses, so the two never drift into different capabilities.
        /// </summary>
        private static List<HubProduct> BuiltIn() => new()
        {
            new HubProduct
            {
                Id = "unitext",
                PackageName = "media.lightside.unitext",
                DisplayName = "UniText",
                Summary = "Unicode text engine: HarfBuzz shaping, full RTL, extensible markup.",
                DocsUrl = "https://unity.lightside.media",
                Requirements = new[]
                {
                    new HubRequirement
                    {
                        Kind = HubRequirement.EmbeddedFork,
                        Package = "com.unity.ugui",
                        Folder = "LocalPackages/com.unity.ugui",
                        Summary = "UniText renders through a fork of Unity UI, embedded in the project.",
                    },
                },
            },
            new HubProduct
            {
                Id = "unishapes",
                PackageName = "media.lightside.unishapes",
                DisplayName = "UniShapes",
                Summary = "SDF vector UI shapes for uGUI, built from composable layers.",
                DocsUrl = "https://unity.lightside.media",
            },
            new HubProduct
            {
                Id = "unilottie",
                PackageName = "media.lightside.unilottie",
                DisplayName = "UniLottie",
                Summary = "Lottie animation player with native ThorVG rendering.",
                DocsUrl = "https://unity.lightside.media",
            },
            new HubProduct
            {
                Id = "moveit",
                PackageName = "media.lightside.moveit",
                DisplayName = "MoveIt",
                Summary = "Burst-evaluated tweens, springs and timelines over dense native storage.",
                DocsUrl = "https://unity.lightside.media",
            },
        };
    }
}
