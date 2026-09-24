using System;
using System.Collections.Generic;
using Broodline.Model.Catalogs;
using Broodline.Model.Stub;

namespace Broodline.UI
{
    public sealed class FacilityRow
    {
        public string Id, Name, Role, Icon, Detail, Blocker;
        public int Tier;
        public int NextCostShards;
        public TimeSpan NextUpgradeTime;
        public bool CanUpgrade;
        public TimeSpan? Upgrading;
        public DateTime? UpgradeEndsAt;
    }

    public sealed class LabScreenModel
    {
        public int CoreTier;
        public IReadOnlyList<FacilityRow> Rows;
    }

    /// The Gene Lab's words and model - Phase 10 Task 1.5. Six facilities,
    /// the Core capping the rest, tiers and timers from the stub ledger until
    /// the Lab has a backend. Upgrades are "(preview)" and count down for
    /// real, locally.
    public static class LabScreen
    {
        public const string Title = "Gene Lab";
        public const string Eyebrow = "FACILITIES";
        public const string PlotHeading = "THE ARK'S FACILITIES";
        public const string PlotHint = "Tap a plot or facility below to inspect its next tier.";
        public const string Upgrade = "Upgrade (preview)";
        public const string Preview = "Facility upgrades are previews in this build - nothing is spent.";
        public const string PreviewNotice = "Upgrade started (preview) - the timer is real, the shards are not spent.";

        public static string TierLabel(int tier) => "Tier " + tier;

        public static string Cost(int shards, TimeSpan time)
            => shards.ToString("N0", System.Globalization.CultureInfo.InvariantCulture) + " shards · " + Duration(time);

        public static string Duration(TimeSpan t)
        {
            if (t.TotalHours >= 1) return (int)t.TotalHours + " h" + (t.Minutes > 0 ? " " + t.Minutes + " min" : "");
            return Math.Max(1, (int)Math.Round(t.TotalMinutes)) + " min";
        }

        public static LabScreenModel Build(StubLedger ledger, DateTime now)
        {
            if (ledger == null) throw new ArgumentNullException(nameof(ledger));
            ledger.Settle();
            var core = ledger.FacilityTier(FacilityCatalog.CoreId);
            var rows = new List<FacilityRow>();
            foreach (var f in FacilityCatalog.All)
            {
                var tier = ledger.FacilityTier(f.Id);
                var ends = ledger.UpgradeEndsAt(f.Id);
                var upgrading = ends != null && ends > now ? ends.Value - now : (TimeSpan?)null;
                var can = FacilityCatalog.CanUpgrade(f.Id, tier, core, out var blocker) && upgrading == null;
                rows.Add(new FacilityRow
                {
                    Id = f.Id, Name = f.Name, Role = f.Role, Icon = f.Icon, Tier = tier,
                    NextCostShards = FacilityCatalog.UpgradeCost(f.Id, tier),
                    NextUpgradeTime = FacilityCatalog.UpgradeTime(f.Id, tier),
                    Upgrading = upgrading, UpgradeEndsAt = upgrading != null ? ends : null,
                    CanUpgrade = can, Blocker = blocker,
                    Detail = upgrading != null ? "Upgrading · " + Duration(upgrading.Value)
                           : can ? f.Role + " · " + Cost(FacilityCatalog.UpgradeCost(f.Id, tier), FacilityCatalog.UpgradeTime(f.Id, tier))
                           : f.Role + " · " + blocker,
                });
            }
            return new LabScreenModel { CoreTier = core, Rows = rows };
        }
    }
}
