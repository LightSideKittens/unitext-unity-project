using UnityEditor;

namespace LightSide.Promo
{
    /// <summary>Builds the short showreel into the open scene, as a rig of its own.</summary>
    /// <remarks>
    /// A separate rig, not a second mode of the argument reel. The two films answer different questions — one makes
    /// a case to a developer weighing engines, the other shows a stranger in twenty seconds why this is worth a
    /// minute of their attention — and a slide list that has to serve both serves neither.
    /// </remarks>
    internal static class PromoShowreelCommands
    {
        [MenuItem(PromoMenu.Tools.CreateShowreel, false, 12)]
        private static void CreateShowreel()
        {
            var reel = PromoRig.Create(PromoMenu.ShowreelObjectName, "Create Promo Showreel", out var reelRect);
            if (!reel) return;

            var scene = PromoRig.AddSlide<ShowreelScene>(reelRect, "Showreel", 5f, new Cut());

            PromoRig.Wire(reel, "theme.displayFace", PromoRig.FindAsset<UniTextFont>("FiraSans-ExtraBold"));
            PromoRig.Wire(reel, "theme.bodyFace", PromoRig.FindAsset<UniTextFont>("FiraSans-Medium"));
            PromoRig.Wire(scene, "displayFont", PromoRig.FindAsset<UniTextFont>("Modak-Regular"));

            PromoRig.Finish(reel);
        }
    }
}
