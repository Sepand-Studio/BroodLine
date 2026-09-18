using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Broodline.UI.Diagnostics;
using NUnit.Framework;

namespace Broodline.UI.Tests
{
    /// WHY THIS IS A BASELINE AND NOT A THRESHOLD.
    ///
    /// broodline_accessibility.md section 4 asks to "widen the lightness
    /// separation between the two collapsing pairs". Measured, that
    /// prescription does not survive contact, but NOT for the reason the
    /// Phase 8 plan gave. Both the design and the plan ranked the palette by
    /// lightness alone, and lightness does not rank confusability:
    ///
    ///   - Vetch/Hollow, which section 4 names as collapsing, does not:
    ///     dE76 26.9 under deuteranopia, 32.9 under protanopia. Comfortable.
    ///   - Ember/Loam, which section 4 also names, IS real: dE76 11.0 under
    ///     deuteranopia. Section 4 is right about this one, and it is now the
    ///     second worst pair in the palette.
    ///   - Skitter/Loam under tritanopia, section 4's third claim, is dE76
    ///     63.0 - among the BEST separated pairs there is. Section 4 is wrong.
    ///   - The palette's worst pair was Vetch/Pale at dE76 7.2 under
    ///     protanopia, named by neither section 4 nor the plan. It was also
    ///     the closest pair at NORMAL vision (17.7, where every other pair is
    ///     above 43) - too close for every player, not just colour-blind
    ///     ones. THAT PAIR IS THE ONE THING THIS TASK CHANGED: Pale moved
    ///     #a9b0c4 to #c6cede, and the pair is now 17.3 and 23.9.
    ///   - The plan's nominated worst pair, Skitter/Pale under protanopia, was
    ///     dL* 0.6 and dE76 71.4. Equally bright, wildly different. Never a
    ///     confusion at all.
    ///
    /// The worst pair is now Vetch/Loam under tritanopia at dE76 10.5, and a
    /// threshold is still not available. Six hues give 15 pairs x 3
    /// deficiencies = 45 constraints against 6 free colours, and deleting Pale
    /// outright would ALSO leave 10.5 - Vetch/Loam is a ceiling Pale cannot
    /// reach, so roughly dE 10-12 is the floor for six saturated hues in this
    /// family however they are moved. It is also a tritan number, which
    /// PaletteContrast.cs explains is the weakest of the three.
    ///
    /// So this asserts no threshold. It pins all 45 measurements on both
    /// channels and fails when any of them gets WORSE, which makes the
    /// palette's CVD behaviour a tracked artifact in the shape of
    /// corpus-baseline.txt and leaves the real decision - repaint, or rely on
    /// silhouette - a human one with numbers attached. See
    /// implementation/results/palette-decision.md for the costed options.
    /// bible 10.4 already leans: "Colour never carries information alone."
    public class PaletteContrastTests
    {
        static readonly string BaselinePath = Path.GetFullPath(Path.Combine(
            UnityEngine.Application.dataPath, "../../implementation/results/palette-cvd-baseline.txt"));

        static readonly string TokensPath = Path.GetFullPath(Path.Combine(
            UnityEngine.Application.dataPath, "UI/Shell/Tokens.uss"));

        static SortedDictionary<string, (double L, double E)> ReadBaseline()
        {
            Assert.IsTrue(File.Exists(BaselinePath), $"no baseline at {BaselinePath}");
            var d = new SortedDictionary<string, (double, double)>(StringComparer.Ordinal);
            foreach (var line in File.ReadAllLines(BaselinePath))
            {
                if (line.StartsWith("#") || line.Trim().Length == 0) continue;
                var p = line.Split('\t');
                Assert.AreEqual(4, p.Length, $"malformed baseline row: {line}");
                d[$"{p[0]}\t{p[1]}"] = (double.Parse(p[2], CultureInfo.InvariantCulture),
                                        double.Parse(p[3], CultureInfo.InvariantCulture));
            }
            return d;
        }

        [Test]
        public void EveryPairIsStillMeasured()
        {
            var now = PaletteContrast.Measure();
            Assert.AreEqual(45, now.Count, "six species give fifteen pairs across three deficiencies");
            CollectionAssert.AreEquivalent(ReadBaseline().Keys, now.Keys,
                "the set of measured pairs changed - a species was added, removed or renamed");
        }

        [Test]
        public void NoPairSeparatesLessWellThanItDidWhenPinned()
        {
            var now = PaletteContrast.Measure();
            var pinned = ReadBaseline();
            var worse = new List<string>();
            foreach (var kv in now)
            {
                var was = pinned[kv.Key];
                var name = kv.Key.Replace('\t', ' ');
                if (kv.Value.DeltaE < was.E - 0.05)
                    worse.Add($"  {name}: dE76 {was.E:F1} -> {kv.Value.DeltaE:F1}");
                if (kv.Value.DeltaL < was.L - 0.05)
                    worse.Add($"  {name}: dL* {was.L:F1} -> {kv.Value.DeltaL:F1}");
            }
            Assert.IsEmpty(worse,
                "a species colour changed and made CVD separation WORSE:\n" + string.Join("\n", worse) +
                "\n\ndE76 is the confusability channel and dL* is the greyscale one; a repaint " +
                "can regress either. If this is intentional, re-pin palette-cvd-baseline.txt in " +
                "the same commit and say in the message which pair you traded away and for what.");
        }

        /// THE COLLISION THE CVD MATHS CANNOT SEE.
        ///
        /// Four species have a token that names them, which is intended. Two
        /// share their exact hex with a token that names a UI ROLE instead:
        /// a Hollow is the same violet as every CTA (Theme.uss Button,
        /// TraitPip, LineageView .node.mutated) and a Skitter is the same
        /// amber as every warning and every Founder marker (CreatureCard
        /// .founder, LineageView .node.founder). No simulation will flag
        /// either, because neither is a confusion between two species - it is
        /// one species being indistinguishable from interface chrome, at
        /// dE76 0.0, for every viewer.
        ///
        /// Pinned here so a future edit to Tokens.uss cannot silently erase
        /// one of these two or create a third without this failing and
        /// sending the reader to palette-decision.md.
        [Test]
        public void SpeciesColoursAndRoleTokensStillCollideExactlyWhereRecorded()
        {
            Assert.IsTrue(File.Exists(TokensPath), $"no Tokens.uss at {TokensPath}");
            var uss = File.ReadAllText(TokensPath);
            var species = PaletteContrast.Species.ToDictionary(s => s.Name, s => s.Hex);

            foreach (var (name, token, role) in PaletteContrast.TokenIdentities)
            {
                var m = Regex.Match(uss, Regex.Escape(token) + @"\s*:\s*(#[0-9a-fA-F]{6})\s*;");
                Assert.IsTrue(m.Success, $"{token} is gone from Tokens.uss - palette-decision.md names it");
                Assert.AreEqual(species[name], m.Groups[1].Value.ToLowerInvariant(),
                    $"{token} ({role}) no longer equals {name}'s species colour. If that is " +
                    "deliberate, palette-decision.md records why these six were identical and " +
                    "needs updating in the same commit.");
            }
        }
    }
}
