using System;
using System.Linq;
using Broodline.Model.Stub;
using Broodline.UI.Components;
using Broodline.UI.Screens;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Broodline.UI.Tests
{
    public sealed class LabStageTests
    {
        [Test]
        public void CropProjectionKeepsTheArkPlotCentredOnDifferentFrames()
        {
            foreach (var (width, height) in new[] { (360f, 520f), (430f, 932f), (390f, 844f) })
            {
                var centre = ArkStageProjection.Point(.5f, .5f, width, height);
                Assert.That(centre.x, Is.EqualTo(width * .5f).Within(.001f));
                Assert.That(centre.y, Is.EqualTo(height * .5f).Within(.001f));
            }
            var croppedTop = ArkStageProjection.Point(.5f, 0f, 360f, 520f);
            Assert.That(croppedTop.y, Is.LessThan(0f), "a short frame crops the source vertically");
        }

        [Test]
        public void FacilitySheetShowsTheModelsCostAndCoreCap()
        {
            var now = new DateTime(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc);
            var ledger = new StubLedger(new StubLedger.MemoryStore(), now: () => now);
            var model = LabScreen.Build(ledger, now);
            var core = model.Rows.Single(row => row.Id == "core");
            var splicing = model.Rows.Single(row => row.Id == "splicing");
            Assert.AreEqual(150, core.NextCostShards);

            var sheet = new FacilitySheet();
            sheet.Bind(core, () => { }, () => { }, now: () => now);
            Assert.That(sheet.Q<Label>("cost").text, Does.Contain("150 shards"));
            Assert.AreEqual(DisplayStyle.Flex, sheet.Q<Button>("upgrade").style.display.value);

            sheet.Bind(splicing, () => { }, () => { }, now: () => now);
            Assert.That(sheet.Q<Label>("blocker").text, Does.Contain("Ark Core"));
            Assert.AreEqual(DisplayStyle.None, sheet.Q<Button>("upgrade").style.display.value);
        }
    }
}
