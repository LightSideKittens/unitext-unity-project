namespace LightSide.Hub
{
    /// <summary>
    /// Everything the Hub needs to know about where LightSide lives. This is the only file that has to
    /// change when a channel moves.
    /// </summary>
    internal static class HubConfig
    {
        /// <summary>Version of this Hub build, compared against the newest GitHub release. Bumped by the release pipeline.</summary>
        public const string Version = "1.0.0";

        /// <summary>Owner and repository whose GitHub releases publish the Hub. The repository must be public: the Hub queries it before the user has any credential.</summary>
        public const string ReleaseRepository = "LightSideMeowshop/lightside-core";

        /// <summary>Name of the release asset carrying the Hub, matched case-insensitively as a suffix.</summary>
        public const string ReleaseAsset = ".unitypackage";

        /// <summary>Branch the product catalogue is read from, so a new product ships without a new Hub.</summary>
        public const string CatalogUrl =
            "https://raw.githubusercontent.com/" + ReleaseRepository + "/main/hub/products.json";

        /// <summary>Package registry serving the paid products.</summary>
        public const string RegistryUrl = "https://registry.lightside.media";

        /// <summary>Package-name prefix the scoped registry claims.</summary>
        public const string Scope = "media.lightside";

        /// <summary>Name shown for the scoped registry in the project manifest and the Package Manager.</summary>
        public const string RegistryDisplayName = "Light Side";

        /// <summary>Where a buyer manages licences and tokens.</summary>
        public const string AccountUrl = "https://unity.lightside.media";
    }
}
