using System.Collections.Generic;
using Broodline.Model.Catalogs;

namespace Broodline.UI
{
    public sealed class StoreScreenModel
    {
        public bool GiftAvailable = true;
        public ChestSelectionModel ChestSelection { get; } = new ChestSelectionModel();
    }

    public sealed class ChestSelectionModel
    {
        readonly HashSet<string> _picked = new HashSet<string>();

        public ChestTier Tier { get; private set; }
        public string PurchaseId => "chest:" + Tier.Id;
        public int Count => _picked.Count;
        public string Cta => Count >= StoreCatalog.ChestPicks
            ? Tier.Price + " · Buy " + Tier.Label + " chest (preview)"
            : Tier.Price + " · Pick " + (StoreCatalog.ChestPicks - Count) + " more";
        public string Summary
        {
            get
            {
                var titles = new List<string>();
                foreach (var option in StoreCatalog.ChestOptions)
                    if (_picked.Contains(option.Id)) titles.Add(TitleFor(option));
                var contents = titles.Count == 0
                    ? "Choose three rewards"
                    : string.Join(" + ", titles);
                return Tier.Label.ToUpperInvariant() + " CHEST · " + Tier.Price + " · " + contents;
            }
        }

        public ChestSelectionModel()
        {
            foreach (var tier in StoreCatalog.ChestTiers)
                if (tier.Id == "standard") Tier = tier;
        }

        public bool Toggle(string optionId)
        {
            if (_picked.Remove(optionId)) return true;
            if (_picked.Count >= StoreCatalog.ChestPicks) return false;
            return _picked.Add(optionId);
        }

        public bool SelectTier(string tierId)
        {
            foreach (var tier in StoreCatalog.ChestTiers)
            {
                if (tier.Id != tierId) continue;
                Tier = tier;
                return true;
            }
            return false;
        }

        public bool IsPicked(string optionId) => _picked.Contains(optionId);

        public string TitleFor(ChestOption option) => option.TitleFor(Tier.Id);
    }

    /// The Store's words - Phase 10 Task 1.5. Bible §8.3's layout: the free
    /// daily gift first, then the pack ladder, the Custom Chest and the Season
    /// Pass; every purchase CTA says "(preview)" because no purchase path
    /// exists, and the tap lands in StubLedger. No offer, timer or discount
    /// copy appears here on purpose (`specs/broodline_offers.md`).
    public static class StoreScreen
    {
        public const string Title = "Store";
        public const string Eyebrow = "GENE SHARDS";
        public const string PacksTab = "Packs", ChestTab = "Custom chest", PassTab = "Season pass";
        public const string GiftTitle = "Free daily gift";
        public static string GiftDetail => StoreCatalog.DailyGiftShards + " shards, once a day, no strings.";
        public const string GiftClaim = "Claim (preview)";
        public const string GiftClaimed = "Claimed today";
        public const string Buy = "Buy (preview)";
        public static string Shards(int n) => n.ToString("N0", System.Globalization.CultureInfo.InvariantCulture) + " shards";
        public const string ChestHeading = "Build your chest";
        public static string ChestProgress(int picked) => picked + " / " + StoreCatalog.ChestPicks + " REWARDS CHOSEN";
        public static string ChestPick(int picked) => picked >= StoreCatalog.ChestPicks
            ? "Buy chest (preview)" : "Pick " + (StoreCatalog.ChestPicks - picked) + " more";
        public const string ChestNote = "Pick any three of the six. Nothing you did not choose ends up in the chest.";
        public const string PassTitle = "Season pass";
        public const string PassDetail = "Four-week season. Free and paid tracks. Does not renew by itself.";
        public const string RegenTitle = "Double regeneration";
        public const string RegenDetail = "Splice Charges return twice as fast, permanently.";
        public const string Preview = "Purchases are previews in this build - nothing is charged.";
        public const string GiftNotice = "Daily gift noted (preview) - shards arrive when the store is live.";
        public const string PreviewNotice = "Noted as a preview - no purchase was made.";
    }
}
