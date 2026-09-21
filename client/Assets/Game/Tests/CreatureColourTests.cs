using System.Linq;
using Broodline.Creatures;
using Broodline.UI.Diagnostics;
using NUnit.Framework;

namespace Broodline.Game.Tests
{
    /// Broodline.Creatures references nothing, so its species colours are a
    /// mirror of PaletteContrast.Species. This is the one place that sees
    /// both, and it fails when they part.
    public class CreatureColourTests
    {
        [Test]
        public void SpeciesColours_MirrorThePalette()
        {
            foreach (var (name, hex) in PaletteContrast.Species)
                Assert.AreEqual(hex.ToLowerInvariant(), SpeciesColours.BaseHex(name).ToLowerInvariant(), name);
        }
    }
}
