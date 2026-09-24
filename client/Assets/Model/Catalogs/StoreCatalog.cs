using System.Collections.Generic;

namespace Broodline.Model.Catalogs
{
    public sealed class StorePack
    {
        public string Id;
        public string Name;
        public string Price;      // display string; no purchase path exists yet
        public int Shards;
        public string Note;
    }

    public sealed class ChestOption
    {
        public string Id;
        public string Title;
        public string SmallTitle;
        public string LargeTitle;
        public string Detail;
        public string Icon;       // an icons.uss glyph name

        public string TitleFor(string tierId)
        {
            if (tierId == "small") return SmallTitle;
            if (tierId == "large") return LargeTitle;
            return Title;
        }
    }

    public sealed class ChestTier
    {
        public string Id;
        public string Label;
        public string Price;
    }

    /// THE STORE'S CONTENTS, AS THE BIBLE LAYS THEM OUT - Phase 10 Task 1.5,
    /// bible §8.3 and `specs/broodline_economy_model.md`'s pack ladder. A
    /// catalog only: nothing here is purchasable, `StubLedger` records a
    /// "(preview)" tap and `NoticeToast` says so. No offers, no popups, no
    /// discounts (`specs/broodline_offers.md`), and Marks never appear.
    public static class StoreCatalog
    {
        public const string DailyGiftId = "daily-gift";
        public const int DailyGiftShards = 40;

        /// The five mixed-content bundles from the canonical economy ladder.
        public static readonly IReadOnlyList<StorePack> Packs = new[]
        {
            new StorePack { Id = "pack-1",  Name = "Starter Splice",       Price = "$0.99",  Shards = 100,  Note = "5 charges · 500 XP" },
            new StorePack { Id = "pack-5",  Name = "Lab Bundle",           Price = "$4.99",  Shards = 600,  Note = "15 charges · 1,500 XP · 1 sample pull" },
            new StorePack { Id = "pack-10", Name = "Lab Expansion",        Price = "$9.99",  Shards = 1400, Note = "40 charges · 5,000 XP · 3 sample pulls" },
            new StorePack { Id = "pack-15", Name = "Mythic Lab Access",    Price = "$14.99", Shards = 1500, Note = "Unlimited charges for 48h" },
            new StorePack { Id = "pack-20", Name = "Geneticist's Vault",   Price = "$19.99", Shards = 3200, Note = "100 charges · 15,000 XP · 8 sample pulls · exclusive skin" },
        };

        public static readonly IReadOnlyList<StorePack> DirectShardPacks = new[]
        {
            new StorePack { Id = "pack-50",  Name = "Vault of shards",   Price = "$49.99", Shards = 9000,  Note = "180 per dollar" },
            new StorePack { Id = "pack-100", Name = "Reserve of shards", Price = "$99.99", Shards = 20000, Note = "200 per dollar" },
        };

        /// Pick three of six; the chest holds only what was picked.
        public static readonly IReadOnlyList<ChestOption> ChestOptions = new[]
        {
            new ChestOption { Id = "charges",  SmallTitle = "6 charges",              Title = "15 charges",              LargeTitle = "30 charges",              Detail = "Splice all day", Icon = "charge" },
            new ChestOption { Id = "shards",   SmallTitle = "250 shards",              Title = "600 shards",              LargeTitle = "1,200 shards",            Detail = "Hard currency", Icon = "shard" },
            new ChestOption { Id = "xp",       SmallTitle = "1,000 XP",                Title = "2,500 XP",                LargeTitle = "5,000 XP",                Detail = "Geneticist tier", Icon = "tier" },
            new ChestOption { Id = "pulls",    SmallTitle = "1 sample pull",           Title = "2 sample pulls",          LargeTitle = "4 sample pulls",          Detail = "Coverage for held traits", Icon = "sparkle" },
            new ChestOption { Id = "aura",     SmallTitle = "4 cosmetic fragments",    Title = "10 cosmetic fragments",    LargeTitle = "20 cosmetic fragments",   Detail = "Cosmetic only", Icon = "splice" },
            new ChestOption { Id = "speedups", SmallTitle = "2h speed-ups",            Title = "6h speed-ups",            LargeTitle = "12h speed-ups",           Detail = "Facility timers", Icon = "timer" },
        };
        public const int ChestPicks = 3;
        public static readonly IReadOnlyList<ChestTier> ChestTiers = new[]
        {
            new ChestTier { Id = "small", Label = "Small", Price = "$1.99" },
            new ChestTier { Id = "standard", Label = "Standard", Price = "$4.99" },
            new ChestTier { Id = "large", Label = "Large", Price = "$9.99" },
        };

        public const string SeasonPassId = "season-pass";
        public const string SeasonPassPrice = "$9.99";
        public const string DoubleRegenId = "double-regen";
        public const string DoubleRegenPrice = "$9.99";
    }
}
