using System.Linq;
using Broodline.Model.Catalogs;
using NUnit.Framework;

namespace Broodline.UI.Tests
{
    public sealed class StoreCatalogTests
    {
        [Test]
        public void FixedBundlesMatchTheCanonicalEconomyLadder()
        {
            var expected = new[]
            {
                ("pack-1", "Starter Splice", "$0.99", 100),
                ("pack-5", "Lab Bundle", "$4.99", 600),
                ("pack-10", "Lab Expansion", "$9.99", 1400),
                ("pack-15", "Mythic Lab Access", "$14.99", 1500),
                ("pack-20", "Geneticist's Vault", "$19.99", 3200),
            };

            Assert.AreEqual(expected.Length, StoreCatalog.Packs.Count);
            for (var i = 0; i < expected.Length; i++)
            {
                Assert.AreEqual(expected[i].Item1, StoreCatalog.Packs[i].Id);
                Assert.AreEqual(expected[i].Item2, StoreCatalog.Packs[i].Name);
                Assert.AreEqual(expected[i].Item3, StoreCatalog.Packs[i].Price);
                Assert.AreEqual(expected[i].Item4, StoreCatalog.Packs[i].Shards);
            }
        }

        [Test]
        public void DirectShardPurchasesKeepTheHighTierValueLadder()
        {
            var expected = new[]
            {
                ("pack-50", "$49.99", 9000),
                ("pack-100", "$99.99", 20000),
            };

            Assert.AreEqual(expected.Length, StoreCatalog.DirectShardPacks.Count);
            for (var i = 0; i < expected.Length; i++)
            {
                Assert.AreEqual(expected[i].Item1, StoreCatalog.DirectShardPacks[i].Id);
                Assert.AreEqual(expected[i].Item2, StoreCatalog.DirectShardPacks[i].Price);
                Assert.AreEqual(expected[i].Item3, StoreCatalog.DirectShardPacks[i].Shards);
            }
        }

        [Test]
        public void FixedBundlesDescribeTheirNonShardContents()
        {
            var expected = new[]
            {
                "5 charges · 500 XP",
                "15 charges · 1,500 XP · 1 sample pull",
                "40 charges · 5,000 XP · 3 sample pulls",
                "Unlimited charges for 48h",
                "100 charges · 15,000 XP · 8 sample pulls · exclusive skin",
            };

            for (var i = 0; i < expected.Length; i++)
                Assert.AreEqual(expected[i], StoreCatalog.Packs[i].Note, StoreCatalog.Packs[i].Id);
        }

        [Test]
        public void ChestTiersScaleEveryApprovedReward()
        {
            var expected = new[]
            {
                ("small", "Small", "$1.99", new[]
                {
                    "6 charges", "250 shards", "1,000 XP", "1 sample pull",
                    "4 cosmetic fragments", "2h speed-ups",
                }),
                ("standard", "Standard", "$4.99", new[]
                {
                    "15 charges", "600 shards", "2,500 XP", "2 sample pulls",
                    "10 cosmetic fragments", "6h speed-ups",
                }),
                ("large", "Large", "$9.99", new[]
                {
                    "30 charges", "1,200 shards", "5,000 XP", "4 sample pulls",
                    "20 cosmetic fragments", "12h speed-ups",
                }),
            };

            Assert.AreEqual(expected.Length, StoreCatalog.ChestTiers.Count);
            for (var i = 0; i < expected.Length; i++)
            {
                var tier = StoreCatalog.ChestTiers[i];
                Assert.AreEqual(expected[i].Item1, tier.Id);
                Assert.AreEqual(expected[i].Item2, tier.Label);
                Assert.AreEqual(expected[i].Item3, tier.Price);
                CollectionAssert.AreEqual(expected[i].Item4,
                    StoreCatalog.ChestOptions.Select(option => option.TitleFor(tier.Id)).ToArray());
            }
        }

        [Test]
        public void ChestSelectionDefaultsToTheStandardTier()
        {
            var selection = new ChestSelectionModel();

            Assert.AreEqual("standard", selection.Tier.Id);
            Assert.AreEqual("$4.99", selection.Tier.Price);
            Assert.AreEqual("chest:standard", selection.PurchaseId);
        }

        [Test]
        public void StoreModelKeepsChestSelectionAcrossGiftRefresh()
        {
            var model = new StoreScreenModel();
            model.ChestSelection.SelectTier("large");
            model.ChestSelection.Toggle("charges");
            model.ChestSelection.Toggle("shards");

            model.GiftAvailable = false;

            Assert.AreEqual("large", model.ChestSelection.Tier.Id);
            Assert.IsTrue(model.ChestSelection.IsPicked("charges"));
            Assert.IsTrue(model.ChestSelection.IsPicked("shards"));
        }

        [Test]
        public void ChestSelectionStopsAtThreeRewards()
        {
            var selection = new ChestSelectionModel();

            Assert.IsTrue(selection.Toggle("charges"));
            Assert.IsTrue(selection.Toggle("shards"));
            Assert.IsTrue(selection.Toggle("xp"));
            Assert.IsFalse(selection.Toggle("pulls"));
            Assert.AreEqual(3, selection.Count);

            Assert.IsTrue(selection.Toggle("charges"));
            Assert.AreEqual(2, selection.Count);
        }

        [Test]
        public void ChangingChestTierPreservesSelectedRewardIds()
        {
            var selection = new ChestSelectionModel();
            selection.Toggle("charges");
            selection.Toggle("shards");

            Assert.IsTrue(selection.SelectTier("large"));
            Assert.IsTrue(selection.IsPicked("charges"));
            Assert.IsTrue(selection.IsPicked("shards"));
            Assert.AreEqual(2, selection.Count);
            Assert.AreEqual("30 charges", selection.TitleFor(StoreCatalog.ChestOptions[0]));
            Assert.AreEqual("1,200 shards", selection.TitleFor(StoreCatalog.ChestOptions[1]));
            Assert.AreEqual("chest:large", selection.PurchaseId);
        }

        [Test]
        public void ChestCopyAlwaysNamesTheCurrentTierAndPrice()
        {
            var selection = new ChestSelectionModel();
            Assert.AreEqual("STANDARD CHEST · $4.99 · Choose three rewards", selection.Summary);
            Assert.AreEqual("$4.99 · Pick 3 more", selection.Cta);

            selection.SelectTier("large");
            selection.Toggle("charges");
            selection.Toggle("shards");
            selection.Toggle("xp");

            Assert.AreEqual(
                "LARGE CHEST · $9.99 · 30 charges + 1,200 shards + 5,000 XP",
                selection.Summary);
            Assert.AreEqual("$9.99 · Buy Large chest (preview)", selection.Cta);
        }
    }
}
