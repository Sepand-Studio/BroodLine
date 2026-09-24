using System;
using Broodline.Model.Catalogs;
using Broodline.Model.Stub;
using NUnit.Framework;

namespace Broodline.UI.Tests
{
    /// Phase 10 Task 1.5: the catalogs match the specs they transcribe.
    public sealed class CatalogTests
    {
        [Test]
        public void TheRegionGraphIsTheSpecs30Regions43EdgesAnd8Gates()
        {
            Assert.AreEqual(30, RegionCatalog.All.Count);
            Assert.AreEqual(43, RegionCatalog.EdgeCount());
            Assert.AreEqual(8, RegionCatalog.Gates.Count);
            foreach (var r in RegionCatalog.All)
                foreach (var n in r.Neighbours)
                {
                    Assert.IsNotNull(RegionCatalog.Find(n), r.Name + " borders unknown " + n);
                    Assert.IsTrue(RegionCatalog.AreAdjacent(n, r.Id), r.Name + " -> " + n + " is one-way");
                }
            foreach (var (a, b) in RegionCatalog.Gates)
            {
                Assert.IsTrue(RegionCatalog.AreAdjacent(a, b), a + "-" + b);
                Assert.AreNotEqual(RegionCatalog.Find(a).Band, RegionCatalog.Find(b).Band, "a gate crosses bands");
            }
        }

        [Test]
        public void TravelTimesAreTheSpecsReconciledMinutes()
        {
            Assert.AreEqual(175, RegionCatalog.TravelMinutes("holdfast", "deepscree", out var route));
            Assert.AreEqual(6, route.Count, "five segments");
            Assert.AreEqual(225, RegionCatalog.TravelMinutes("holdfast", "weltering", out route));
            Assert.AreEqual(8, route.Count, "seven segments: the deepest region");
            Assert.AreEqual(50, RegionCatalog.HopMinutes("tellin", "greyspan"));
            Assert.AreEqual(25, RegionCatalog.HopMinutes("holdfast", "tellin"));
            Assert.AreEqual(-1, RegionCatalog.HopMinutes("holdfast", "weltering"));
        }

        [Test]
        public void LocateMapsTheServersRegionIdsOntoTheCatalog()
        {
            Assert.AreEqual("holdfast", RegionCatalog.Locate(null).Id);
            Assert.AreEqual("holdfast", RegionCatalog.Locate("region-1").Id);
            Assert.AreEqual("tellin", RegionCatalog.Locate("region-2").Id);
            Assert.AreEqual("weltering", RegionCatalog.Locate("weltering").Id);
            Assert.AreEqual("holdfast", RegionCatalog.Locate("region-99").Id);
        }

        [Test]
        public void TheCoreCapsEveryOtherFacility()
        {
            Assert.IsTrue(FacilityCatalog.CanUpgrade("core", 1, 1, out _));
            Assert.IsFalse(FacilityCatalog.CanUpgrade("splicing", 1, 1, out var blocker));
            StringAssert.Contains("Core", blocker);
            Assert.IsTrue(FacilityCatalog.CanUpgrade("splicing", 1, 2, out _));
            Assert.IsFalse(FacilityCatalog.CanUpgrade("core", FacilityCatalog.MaxTier, FacilityCatalog.MaxTier, out _));
            Assert.That(FacilityCatalog.UpgradeCost("core", 1), Is.EqualTo(150).Within(10));
            Assert.That(FacilityCatalog.UpgradeCost("core", FacilityCatalog.MaxTier - 1), Is.EqualTo(240000).Within(100));
            Assert.That(FacilityCatalog.UpgradeTime("core", 1).TotalMinutes, Is.EqualTo(2).Within(0.5));
            Assert.That(FacilityCatalog.UpgradeTime("core", FacilityCatalog.MaxTier - 1).TotalHours, Is.EqualTo(48).Within(0.1));
        }

        [Test]
        public void ThePackLadderAlwaysRisesInValuePerDollar()
        {
            double last = 0;
            foreach (var pack in StoreCatalog.Packs)
            {
                var dollars = double.Parse(pack.Price.TrimStart('$'), System.Globalization.CultureInfo.InvariantCulture);
                var perDollar = pack.Shards / dollars;
                Assert.That(perDollar, Is.GreaterThan(last), pack.Id);
                last = perDollar;
            }
            Assert.AreEqual(6, StoreCatalog.ChestOptions.Count);
        }

        [Test]
        public void TheStubLedgerPromotesAFacilityWhenItsTimerEnds()
        {
            var now = new DateTime(2026, 9, 23, 12, 0, 0, DateTimeKind.Utc);
            var lines = new System.Collections.Generic.List<string>();
            var ledger = new StubLedger(new StubLedger.MemoryStore(), lines.Add, () => now);
            Assert.AreEqual(1, ledger.FacilityTier("core"));
            ledger.RememberFacility("core");
            ledger.StartUpgrade("core", TimeSpan.FromMinutes(2));
            ledger.Settle();
            Assert.AreEqual(1, ledger.FacilityTier("core"), "not yet");
            now = now.AddMinutes(3);
            ledger.Settle();
            Assert.AreEqual(2, ledger.FacilityTier("core"));
            Assert.IsNull(ledger.UpgradeEndsAt("core"));
            Assert.AreEqual(4, lines.Count, "remember, start, tier and timer clear must all be logged");
            foreach (var line in lines)
                StringAssert.StartsWith("[stub]", line, "every local write must be identifiable in diagnostics");

            Assert.IsTrue(ledger.DailyGiftAvailable());
            ledger.ClaimDailyGift();
            Assert.IsFalse(ledger.DailyGiftAvailable());
            now = now.AddDays(1);
            Assert.IsTrue(ledger.DailyGiftAvailable());
        }

        [Test]
        public void TheLabModelSaysUpgradingCostOrBlockerPerFacility()
        {
            var now = new DateTime(2026, 9, 23, 12, 0, 0, DateTimeKind.Utc);
            var ledger = new StubLedger(new StubLedger.MemoryStore(), now: () => now);
            ledger.RememberFacility("splicing");
            ledger.StartUpgrade("splicing", TimeSpan.FromMinutes(42));
            var model = LabScreen.Build(ledger, now);
            Assert.AreEqual(6, model.Rows.Count);
            var core = model.Rows[0]; var splicing = model.Rows[1]; var hatchery = model.Rows[2];
            Assert.IsTrue(core.CanUpgrade);
            StringAssert.Contains("shards", core.Detail);
            Assert.IsNotNull(splicing.Upgrading);
            StringAssert.Contains("Upgrading", splicing.Detail);
            Assert.IsFalse(hatchery.CanUpgrade);
            StringAssert.Contains("Core", hatchery.Detail);
        }

        [Test]
        public void TheMapModelMarksHereAndGroupsByRing()
        {
            var m = MapScreen.Build("region-1");
            Assert.AreEqual("holdfast", m.CurrentRegionId);
            Assert.AreEqual(3, m.Bands.Count);
            Assert.AreEqual(8, m.Bands[0].Rows.Count);
            Assert.AreEqual(12, m.Bands[1].Rows.Count);
            Assert.AreEqual(10, m.Bands[2].Rows.Count);
            Assert.IsTrue(m.Bands[0].Rows[0].Here);
            StringAssert.Contains(MapScreen.HereDetail, m.Bands[0].Rows[0].Detail);
            StringAssert.Contains("3 h 45 min away", m.Bands[2].Rows[8].Detail);
        }
    }
}
