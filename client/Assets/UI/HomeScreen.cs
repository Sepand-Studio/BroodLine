using System.Collections.Generic;

namespace Broodline.UI
{
    /// One facility marker on the Ark base, placed by normalized viewport point
    /// (0..1 from the stage's top-left) that the Game layer projects from the
    /// 3D plot's world position. UI never sees a camera; it sees a fraction.
    public sealed class HomeHotspot
    {
        public string Id;
        public string Label;
        public int Tier;
        public float X01, Y01;
    }

    public sealed class HomeScreenModel
    {
        public string ArkName = HomeScreen.DefaultArkName;
        public string RegionName;
        public int CoreTier = 1;
        public IReadOnlyList<HomeHotspot> Hotspots = new HomeHotspot[0];
    }

    /// The home base's words - Phase 10 Task 1.4. The Ark tab lands here: the
    /// base in three-quarter view, the six facilities as markers on it, and
    /// the four things a player does from home.
    public static class HomeScreen
    {
        public const string DefaultArkName = "The Ark";
        public const string Eyebrow = "GENE ARK";
        public const string DefendLabel = "Defend the Ark";
        public const string RosterLabel = "Roster";
        public const string CodexLabel = "Codex";
        public const string StoreLabel = "Store";

        public static string Subtitle(string region, int coreTier)
            => (string.IsNullOrEmpty(region) ? "Somewhere on the frontier" : region) + " · Core tier " + coreTier;

        public static string HotspotLabel(string name, int tier) => name + " " + ToRoman(tier);

        public static string ToRoman(int n)
        {
            if (n <= 0) return "";
            var pairs = new[] { (10, "X"), (9, "IX"), (5, "V"), (4, "IV"), (1, "I") };
            var s = ""; foreach (var (v, r) in pairs) while (n >= v) { s += r; n -= v; }
            return s;
        }
    }
}
