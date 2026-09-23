using Broodline.Model.Catalogs;

namespace Broodline.UI
{
    public sealed class StoreScreenModel
    {
        public bool GiftAvailable = true;
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
        public static string ChestPick(int picked) => picked >= StoreCatalog.ChestPicks
            ? "Buy chest (preview)" : "Pick " + (StoreCatalog.ChestPicks - picked) + " more";
        public const string ChestNote = "Pick any three of the six. Nothing you did not choose ends up in the chest.";
        public const string PassTitle = "Season pass";
        public const string PassDetail = "A chapter of rewards on two tracks. Does not renew by itself.";
        public const string RegenTitle = "Double regeneration";
        public const string RegenDetail = "Splice Charges return twice as fast, permanently.";
        public const string Preview = "Purchases are previews in this build - nothing is charged.";
        public const string GiftNotice = "Daily gift noted (preview) - shards arrive when the store is live.";
        public const string PreviewNotice = "Noted as a preview - no purchase was made.";
    }
}
