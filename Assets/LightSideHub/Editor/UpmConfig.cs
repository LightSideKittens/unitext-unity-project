using System;
using System.IO;

namespace LightSide.Hub
{
    /// <summary>
    /// The user-level <c>.upmconfig.toml</c> Unity reads registry credentials from. The Hub only reads
    /// it: a project set up through the Hub carries its token in the scoped-registry URL, but a project
    /// set up by hand may hold it here instead, and both must resolve.
    /// </summary>
    internal static class UpmConfig
    {
        /// <summary>Path Unity resolves the config from, honouring the <c>UPM_USER_CONFIG_FILE</c> override.</summary>
        public static string Path
        {
            get
            {
                var overridden = Environment.GetEnvironmentVariable("UPM_USER_CONFIG_FILE");
                return string.IsNullOrEmpty(overridden)
                    ? System.IO.Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                        ".upmconfig.toml")
                    : overridden;
            }
        }

        /// <summary>The token stored for <paramref name="registryUrl"/>, or null when the registry has no entry.</summary>
        public static string ReadToken(string registryUrl)
        {
            if (!File.Exists(Path)) return null;

            var inSection = false;
            foreach (var line in File.ReadAllLines(Path))
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("["))
                {
                    inSection = trimmed.Contains(registryUrl);
                    continue;
                }
                if (!inSection || !trimmed.StartsWith("token")) continue;

                var separator = trimmed.IndexOf('=');
                if (separator < 0) continue;
                return trimmed.Substring(separator + 1).Trim().Trim('"');
            }
            return null;
        }
    }
}
