using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.TextCore.Text;

namespace Broodline.UI.Tests
{
    /// bible 10.6, asserted rather than assumed: "Numbers must not jitter as
    /// they tick, and 1/l/I and 0/O must be unambiguous - misread stats erode
    /// trust."
    ///
    /// Tabular figures means every digit advances the pen by the same amount.
    /// That is a property of the FONT, readable from its glyph metrics, and
    /// it is the one half of 10.6 that no amount of USS can satisfy. The
    /// other half - the 11px floor - is a SIZE property and lives in
    /// verify-uss-tokens.sh.
    ///
    /// If this test fails on Baloo 2, 10.6 already prescribes the remedy and
    /// it is not a judgement call: "keep it for titles and CTAs and pair a
    /// tabular face for numerals only." Record the measurement, do that, and
    /// point DisplayFace below at the paired face.
    public class TypographyTests
    {
        /// MEASURED, NOT CHOSEN. Baloo 2 Bold's digits were read off the
        /// generated FontAsset and advance 0-9 as:
        ///
        ///     54, 34.5625, 46.70313, 46.34375, 53.01563,
        ///     47.34375, 50.04688, 42.84375, 50.57813, 50.125
        ///
        /// Ten distinct advances - '1' is 0.64 of '0'. Proportional, so a
        /// ticking counter in Baloo 2 would visibly jitter. 10.6's prescribed
        /// remedy applies verbatim: keep it for titles and CTAs, pair a
        /// tabular face for numerals only. Nunito Bold's ten are identical,
        /// so it is the face every numeral is set in and it is what
        /// DisplayFace points at. Theme.uss's selectors mirror this split:
        /// Baloo 2 on .t-screen-title/.t-section/.btn-primary only.
        /// THREE ROLES, TWO FACES, AND THEY ARE NOT INTERCHANGEABLE. These
        /// used to be two constants holding the SAME string, which made two of
        /// the tests below byte-identical and left Baloo 2 - the face on every
        /// heading and every CTA - asserted by nothing at all. That was a side
        /// effect of correctly repointing the numeral face at Nunito: the
        /// coverage of the face that still ships went with it.
        const string NumeralFace = "Assets/UI/Fonts/Nunito-Bold SDF.asset";
        const string BodyFace    = "Assets/UI/Fonts/Nunito-Bold SDF.asset";
        const string DisplayFace = "Assets/UI/Fonts/Baloo2-Bold SDF.asset";

        static float[] DigitAdvances(string path)
        {
            var font = AssetDatabase.LoadAssetAtPath<FontAsset>(path);
            Assert.IsNotNull(font, $"no FontAsset at {path} - run Broodline/Rebuild Font Assets");
            font.TryAddCharacters("0123456789");

            return "0123456789".Select(c =>
            {
                Assert.IsTrue(font.characterLookupTable.TryGetValue(c, out var ch),
                              $"{font.name} has no glyph for '{c}'");
                return ch.glyph.metrics.horizontalAdvance;
            }).ToArray();
        }

        [Test]
        public void TheNumeralFacesDigitsAllAdvanceTheSameWidth()
        {
            var a = DigitAdvances(NumeralFace);
            Assert.That(a.Distinct().Count(), Is.EqualTo(1),
                "numeral face digits are proportional, so every ticking number will jitter. " +
                "Advances 0-9: " + string.Join(", ", a) + ". bible 10.6 says what to do.");
        }

        [Test]
        public void TheBodyFacesDigitsAllAdvanceTheSameWidth()
        {
            var a = DigitAdvances(BodyFace);
            Assert.That(a.Distinct().Count(), Is.EqualTo(1),
                "body face numerals are proportional. Advances 0-9: " + string.Join(", ", a));
        }

        /// BOTH FACES, because 10.6's second requirement is about anything a
        /// player reads, and the two split the screen between them: Baloo 2
        /// on titles, sections and CTAs, Nunito on everything else. Asserting
        /// it on one of them proves half of it.
        [TestCase(DisplayFace)]
        [TestCase(BodyFace)]
        public void AFaceDistinguishesOneFromEllAndZeroFromOh(string path)
        {
            var font = AssetDatabase.LoadAssetAtPath<FontAsset>(path);
            Assert.IsNotNull(font, $"no FontAsset at {path} - run Broodline/Rebuild Font Assets");
            font.TryAddCharacters("1lI0O");
            foreach (var pair in new[] { ('1', 'l'), ('1', 'I'), ('0', 'O') })
            {
                Assert.IsTrue(font.characterLookupTable.TryGetValue(pair.Item1, out var a));
                Assert.IsTrue(font.characterLookupTable.TryGetValue(pair.Item2, out var b));
                Assert.That(a.glyph.index, Is.Not.EqualTo(b.glyph.index),
                    $"in {font.name}, '{pair.Item1}' and '{pair.Item2}' share a glyph - "
                    + "10.6's second requirement fails");
            }
        }
    }
}
