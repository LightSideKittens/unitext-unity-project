namespace LightSide.Promo
{
    /// <summary>
    /// Single source of truth for every menu path this package contributes, so its user-visible layout can be
    /// reviewed — and reorganized — in one place.
    /// </summary>
    internal static class PromoMenu
    {
        private const string Root = "Promo";

        internal const string ReelObjectName = "Promo Reel";

        internal static class Tools
        {
            private const string P = "Tools/" + Root + "/";
            public const string CreateReel = P + "Create Reel";
            public const string Rebuild = P + "Rebuild";
            public const string CaptureFrames = P + "Capture Frames";
            public const string ContactSheet = P + "Contact Sheet";
        }

        internal static class AddComponent
        {
            private const string P = Root + "/";
            public const string Reel = P + "Reel";
        }
    }
}
