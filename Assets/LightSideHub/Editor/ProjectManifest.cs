using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace LightSide.Hub
{
    /// <summary>
    /// Reads and rewrites the project's <c>Packages/manifest.json</c>. Every write is preceded by a
    /// <c>.backup</c> copy beside the file, because a manifest the Hub corrupts costs the user their
    /// whole package set.
    /// </summary>
    internal static class ProjectManifest
    {
        private const string Dependencies = "dependencies";
        private const string ScopedRegistries = "scopedRegistries";

        /// <summary>Absolute path of the project manifest, which exists in every Unity project.</summary>
        public static string Path =>
            System.IO.Path.Combine(Application.dataPath, "..", "Packages", "manifest.json");

        /// <summary>The parsed manifest, or null when the file is missing or is not a JSON object.</summary>
        public static Dictionary<string, object> Read()
        {
            if (!File.Exists(Path)) return null;
            return MiniJson.Parse(File.ReadAllText(Path)) as Dictionary<string, object>;
        }

        /// <summary>The version or source string a package is pinned to, or null when it is not a dependency.</summary>
        public static string Dependency(string packageName)
            => MiniJson.String(MiniJson.Object(Read(), Dependencies), packageName);

        /// <summary>The registry URL serving <paramref name="scope"/>, or null when no scoped registry claims it.</summary>
        public static string RegistryUrl(string scope)
        {
            var registries = MiniJson.Array(Read(), ScopedRegistries);
            if (registries == null) return null;
            foreach (var entry in registries)
            {
                if (entry is not Dictionary<string, object> registry) continue;
                if (!ClaimsScope(registry, scope)) continue;
                return MiniJson.String(registry, "url");
            }
            return null;
        }

        /// <summary>
        /// Points <paramref name="scope"/> at <paramref name="url"/>, replacing whichever registry
        /// claimed that scope before. Registries serving other scopes are left untouched.
        /// </summary>
        /// <exception cref="FileNotFoundException">The project has no manifest.</exception>
        /// <exception cref="InvalidDataException">The manifest is not a JSON object.</exception>
        public static void SetScopedRegistry(string url, string displayName, string scope)
        {
            var manifest = Require();
            var kept = new List<object>();
            var registries = MiniJson.Array(manifest, ScopedRegistries);
            if (registries != null)
                foreach (var entry in registries)
                    if (entry is not Dictionary<string, object> registry || !ClaimsScope(registry, scope))
                        kept.Add(entry);

            kept.Add(new Dictionary<string, object>
            {
                ["name"] = displayName,
                ["url"] = url,
                ["scopes"] = new List<object> { scope },
            });

            manifest[ScopedRegistries] = kept;
            Write(manifest);
        }

        /// <summary>Pins <paramref name="packageName"/> to <paramref name="version"/>, adding the dependency when absent.</summary>
        /// <exception cref="FileNotFoundException">The project has no manifest.</exception>
        /// <exception cref="InvalidDataException">The manifest is not a JSON object.</exception>
        public static void SetDependency(string packageName, string version)
        {
            var manifest = Require();
            var dependencies = MiniJson.Object(manifest, Dependencies) ?? new Dictionary<string, object>();
            dependencies[packageName] = version;
            manifest[Dependencies] = dependencies;
            Write(manifest);
        }

        /// <summary>Drops <paramref name="packageName"/> from the dependencies; a package that is not there is left alone.</summary>
        public static void RemoveDependency(string packageName)
        {
            var manifest = Read();
            var dependencies = MiniJson.Object(manifest, Dependencies);
            if (dependencies == null || !dependencies.Remove(packageName)) return;
            Write(manifest);
        }

        private static bool ClaimsScope(Dictionary<string, object> registry, string scope)
        {
            var scopes = MiniJson.Array(registry, "scopes");
            if (scopes == null) return false;
            foreach (var entry in scopes)
                if (entry is string value && value == scope) return true;
            return false;
        }

        private static Dictionary<string, object> Require()
        {
            if (!File.Exists(Path))
                throw new FileNotFoundException("Packages/manifest.json not found.", Path);
            return MiniJson.Parse(File.ReadAllText(Path)) as Dictionary<string, object>
                   ?? throw new InvalidDataException("Packages/manifest.json is not a JSON object.");
        }

        private static void Write(Dictionary<string, object> manifest)
        {
            File.Copy(Path, Path + ".backup", overwrite: true);
            File.WriteAllText(Path, MiniJson.Serialize(manifest, pretty: true) + "\n");
        }
    }
}
