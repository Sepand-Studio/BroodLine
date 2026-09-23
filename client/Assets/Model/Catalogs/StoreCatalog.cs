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
        public string Detail;
        public string Icon;       // an icons.uss glyph name
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

        /// Value per dollar always rises down the ladder.
        public static readonly IReadOnlyList<StorePack> Packs = new[]
        {
            new StorePack { Id = "pack-1",   Name = "Handful of shards", Price = "$0.99",  Shards = 100,   Note = "101 per dollar" },
            new StorePack { Id = "pack-5",   Name = "Pouch of shards",   Price = "$4.99",  Shards = 600,   Note = "120 per dollar" },
            new StorePack { Id = "pack-10",  Name = "Case of shards",    Price = "$9.99",  Shards = 1400,  Note = "140 per dollar" },
            new StorePack { Id = "pack-20",  Name = "Crate of shards",   Price = "$19.99", Shards = 3200,  Note = "160 per dollar" },
            new StorePack { Id = "pack-50",  Name = "Vault of shards",   Price = "$49.99", Shards = 9000,  Note = "180 per dollar" },
            new StorePack { Id = "pack-100", Name = "Reserve of shards", Price = "$99.99", Shards = 20000, Note = "200 per dollar" },
        };

        /// Pick three of six; the chest holds only what was picked.
        public static readonly IReadOnlyList<ChestOption> ChestOptions = new[]
        {
            new ChestOption { Id = "charges",  Title = "15 charges",    Detail = "Splice all day",     Icon = "charge" },
            new ChestOption { Id = "shards",   Title = "600 shards",    Detail = "Hard currency",      Icon = "shard" },
            new ChestOption { Id = "xp",       Title = "2,500 XP",      Detail = "Geneticist tier",    Icon = "tier" },
            new ChestOption { Id = "pulls",    Title = "2 trait pulls", Detail = "Rare trait roll",    Icon = "sparkle" },
            new ChestOption { Id = "aura",     Title = "Hybrid aura",   Detail = "Cosmetic only",      Icon = "splice" },
            new ChestOption { Id = "speedups", Title = "6h speed-ups",  Detail = "Facility timers",    Icon = "timer" },
        };
        public const int ChestPicks = 3;
        public static readonly IReadOnlyList<(string Id, string Price, string Label)> ChestPrices = new[]
        {
            ("small", "$1.99", "Small"), ("standard", "$4.99", "Standard"), ("large", "$9.99", "Large"),
        };

        public const string SeasonPassId = "season-pass";
        public const string SeasonPassPrice = "$4.99";
        public const string DoubleRegenId = "double-regen";
        public const string DoubleRegenPrice = "$9.99";
    }
}
