using System;
using System.Collections.Generic;

namespace Broodline.Model.Catalogs
{
    public sealed class FacilityInfo
    {
        public string Id;
        public string Name;
        public string Role;       // one line, what it does for the player
        public string Icon;       // icons.uss glyph
    }

    /// THE GENE LAB'S SIX FACILITIES - Phase 10 Task 1.5, bible §7.2. The
    /// Core caps every other facility's tier (the town-hall pattern), and
    /// each Core tier also needs a campaign milestone the catalog does not
    /// know about - that gate lives on the server when the Lab is real. The
    /// cost and timer ladders are `specs/broodline_economy_model.md`'s Core
    /// ladder (150 → 240,000 shards, 2 min → 48 h over twelve tiers),
    /// interpolated geometrically; the other five run at 60% of it.
    public static class FacilityCatalog
    {
        public const int MaxTier = 12;
        public const string CoreId = "core";

        public static readonly IReadOnlyList<FacilityInfo> All = new[]
        {
            new FacilityInfo { Id = CoreId,      Name = "Ark Core",       Role = "Caps every other facility",     Icon = "ark" },
            new FacilityInfo { Id = "splicing",  Name = "Splice Chamber", Role = "Odds, locks and mutation",       Icon = "splice" },
            new FacilityInfo { Id = "hatchery",  Name = "Hatchery",       Role = "Roster capacity",               Icon = "sparkle" },
            new FacilityInfo { Id = "vault",     Name = "Gene Vault",     Role = "Samples and archive",           Icon = "lock" },
            new FacilityInfo { Id = "harvest",   Name = "Harvest Rig",    Role = "Node yield and Collectors",     Icon = "map" },
            new FacilityInfo { Id = "drive",     Name = "Travel Drive",   Role = "Relocation speed",              Icon = "timer" },
        };

        public static FacilityInfo Find(string id)
        {
            foreach (var f in All) if (f.Id == id) return f;
            return null;
        }

        /// Shards to go from `tier` to `tier + 1`.
        public static int UpgradeCost(string id, int tier)
        {
            if (tier < 1 || tier >= MaxTier) return 0;
            var t = (tier - 1) / (double)(MaxTier - 2);
            var core = 150.0 * Math.Pow(240000.0 / 150.0, t);
            return (int)Math.Round((id == CoreId ? core : core * 0.6) / 10.0) * 10;
        }

        /// Time to go from `tier` to `tier + 1`.
        public static TimeSpan UpgradeTime(string id, int tier)
        {
            if (tier < 1 || tier >= MaxTier) return TimeSpan.Zero;
            var t = (tier - 1) / (double)(MaxTier - 2);
            var minutes = 2.0 * Math.Pow((48.0 * 60.0) / 2.0, t);
            return TimeSpan.FromMinutes(Math.Round(id == CoreId ? minutes : minutes * 0.6));
        }

        /// The Core caps the rest: a facility can never exceed the Core's
        /// tier, and the Core itself only tops out at MaxTier.
        public static bool CanUpgrade(string id, int tier, int coreTier, out string blocker)
        {
            blocker = null;
            if (tier >= MaxTier) { blocker = "Already at the top tier."; return false; }
            if (id != CoreId && tier >= coreTier) { blocker = "Raise the Ark Core first."; return false; }
            return true;
        }
    }
}
