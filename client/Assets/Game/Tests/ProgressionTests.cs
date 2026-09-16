using System.Collections.Generic;
using Broodline.Model;
using NUnit.Framework;

namespace Broodline.Game.Tests
{
    /// client_architecture section 9: the bar is a PURE FUNCTION of progress
    /// and thresholds. task-13-brief.md Step 1, verbatim.
    public class ProgressionTests
    {
        [TestCase(0, new[] { "Map", "Ark" })]
        [TestCase(1, new[] { "Map", "Ark" })]
        [TestCase(2, new[] { "Map", "Ark", "Splice" })]
        [TestCase(60, new[] { "Map", "Ark", "Splice" })]
        [TestCase(61, new[] { "Map", "Ark", "Splice", "Lab", "Allies" })]
        public void TabsFor_RevealsInFixedOrderAtTheBundleThresholds(int cleared, string[] expected)
        {
            var thresholds = new Dictionary<string, int> { ["Map"] = 0, ["Ark"] = 0, ["Splice"] = 2, ["Lab"] = 61, ["Allies"] = 61 };
            CollectionAssert.AreEqual(expected, Progression.TabsFor(cleared, thresholds));
        }

        [Test]
        public void TabsFor_WithNoThresholds_ShowsTheMinimumTwo()
        {
            // A fresh install with no cached snapshot: correct for a new player, and
            // wrong for a reinstalling veteran only for the length of one sync.
            CollectionAssert.AreEqual(new[] { "Map", "Ark" }, Progression.TabsFor(0, new Dictionary<string, int>()));
        }
    }
}
