using System;
using System.Collections.Generic;
using System.Linq;

namespace Broodline.UI.Diagnostics
{
    /// The species palette's separation under colour-vision deficiency.
    ///
    /// IN Broodline.UI RATHER THAN THE TEST ASSEMBLY, because the baseline
    /// emitter lives in Assembly-CSharp-Editor and Broodline.UI.Tests is
    /// autoReferenced:false behind UNITY_INCLUDE_TESTS - nothing outside it
    /// can call in. A baseline written by different code than the code that
    /// checks it is not a baseline, so both read this.
    ///
    /// Matrices: Machado, Oliveira and Fernandes (2009), severity 1.0.
    ///
    /// TWO THINGS HERE DIFFER FROM THE PHASE 8 PLAN'S DRAFT, both because the
    /// draft was measured against external references and found wrong. Both
    /// are recorded so the call can be argued with rather than rediscovered.
    ///
    /// 1. THE MATRICES ARE APPLIED TO LINEAR RGB, NOT TO GAMMA-ENCODED sRGB.
    ///    Machado's model is a linear projection in LMS cone space, and LMS is
    ///    a linear transform of linear RGB via XYZ; multiplying gamma-encoded
    ///    values by it mixes encoded numbers as though they were light. This
    ///    is a known, named defect: the R `colorspace` package shipped the
    ///    gamma-space version for years, Matthew Petroff reported it, and it
    ///    was fixed in colorspace 2.1-0. The difference is negligible for
    ///    desaturated colours and material for saturated red, orange and
    ///    purple - which is most of this palette.
    ///
    ///    Structural confirmation that the transcription below is right:
    ///    every row of every matrix sums to 1.0 (white maps to white), and
    ///    det(protan) and det(deutan) are 0 to six decimal places, i.e. they
    ///    are the rank-2 projections a dichromat's response must be.
    ///    det(tritan) is 0.236, NOT rank 2 - Machado's tritan fit at severity
    ///    1.0 is an extrapolation rather than a true dichromat projection, so
    ///    every "tritan" row this file emits is weaker evidence than its
    ///    protan and deutan siblings. Say so before acting on one.
    ///
    /// 2. IT MEASURES dE76 AS WELL AS dL*, AND dE IS THE ONE THAT MATTERS.
    ///    The plan asked for lightness separation alone. Measured, dL* does
    ///    not rank confusability - it inverts it. Vetch/Ember under
    ///    tritanopia is dL* 0.1, which by lightness alone is a total collapse;
    ///    its dE76 is 83.2, the second most separated pair in the table. Two
    ///    equally bright, completely different colours.
    ///
    ///    This is not academic. Ranking by lightness is what sent both
    ///    documents after the wrong pairs: the palette's genuine worst was
    ///    Vetch/Pale under protanopia at dE76 7.2 - an unremarkable dL* of
    ///    4.8, named by neither the design nor the plan - and finding it is
    ///    what moved Pale. See palette-decision.md.
    ///
    ///    dE76 (plain Euclidean distance in CIE L*a*b*) rather than dE2000:
    ///    dE76 is hand-checkable, and dE2000's extra machinery is exactly the
    ///    kind of transcription this comment exists to warn about. It is used
    ///    to RANK pairs, not to certify a threshold, and for ranking the two
    ///    agree closely enough that the extra risk buys nothing.
    ///
    /// dL* is kept anyway. It is the right measure for a different question -
    /// what survives in greyscale, which is the channel bible 10.2's
    /// silhouette rule leans on - and a repaint that held hue while
    /// collapsing value would be invisible to dE alone.
    public static class PaletteContrast
    {
        /// Separation between one pair of species under one deficiency.
        public readonly struct Separation
        {
            /// Lightness difference only. The greyscale channel.
            public readonly double DeltaL;
            /// Full CIE L*a*b* distance. The confusability channel.
            public readonly double DeltaE;
            public Separation(double deltaL, double deltaE) { DeltaL = deltaL; DeltaE = deltaE; }
        }

        /// broodline_bible.md section 1.2, broodline_accessibility.md section
        /// 4 and the design handoff README all carry these six.
        ///
        /// FIVE OF THEM AGREE WITH ALL THREE. Pale does not: it was #a9b0c4
        /// everywhere and is #c6cede here and in Tokens.uss, moved by Phase 8
        /// Task 6 because Vetch/Pale was the palette's worst pair under every
        /// viewer, colour-blind or not. Those three documents are stale on
        /// this one row until Task 19 records the decision; the token layer
        /// and this array are the live values, and the test below asserts
        /// they cannot drift apart.
        public static readonly (string Name, string Hex)[] Species =
        {
            ("Vetch",  "#6ba7c0"), ("Ember", "#e5867a"), ("Skitter", "#e8b34a"),
            ("Hollow", "#7a6ac0"), ("Loam",  "#7cc492"), ("Pale",    "#c6cede"),
        };

        /// Four species share a hex with the token that names them, which is
        /// intended. TWO share a hex with a token that names a UI ROLE, which
        /// is not, and no amount of CVD simulation will find it: it is not a
        /// confusion between two species, it is one species being the same
        /// colour as a piece of interface chrome. dE is 0.0 at normal vision,
        /// which is the strongest collision there is.
        ///
        /// Pinned by PaletteContrastTests against Tokens.uss so that a future
        /// edit cannot silently create a third or erase one of these two.
        public static readonly (string Species, string Token, string Role)[] TokenIdentities =
        {
            ("Ember",   "--coral",  "Ember, danger"),
            ("Hollow",  "--violet", "primary brand, CTAs"),      // COLLISION
            ("Loam",    "--green",  "Loam, success"),
            ("Pale",    "--slate",  "Pale, neutral"),
            ("Skitter", "--amber",  "Apex / gold, warnings"),    // COLLISION
            ("Vetch",   "--teal",   "Vetch, info"),
        };

        static readonly Dictionary<string, double[][]> Cvd = new Dictionary<string, double[][]>
        {
            ["deutan"] = new[] { new[]{ 0.367322, 0.860646,-0.227968},
                                 new[]{ 0.280085, 0.672501, 0.047413},
                                 new[]{-0.011820, 0.042940, 0.968881} },
            ["protan"] = new[] { new[]{ 0.152286, 1.052583,-0.204868},
                                 new[]{ 0.114503, 0.786281, 0.099216},
                                 new[]{-0.003882,-0.048116, 1.051998} },
            ["tritan"] = new[] { new[]{ 1.255528,-0.076749,-0.178779},
                                 new[]{-0.078411, 0.930809, 0.147602},
                                 new[]{ 0.004733, 0.691367, 0.303900} },
        };

        /// The deficiency keys, ordinal-sorted, so callers can iterate in the
        /// same order the emitted file uses.
        public static IEnumerable<string> Deficiencies =>
            Cvd.Keys.OrderBy(x => x, StringComparer.Ordinal);

        static double[] Rgb(string hex) => new[]
        {
            Convert.ToInt32(hex.Substring(1, 2), 16) / 255.0,
            Convert.ToInt32(hex.Substring(3, 2), 16) / 255.0,
            Convert.ToInt32(hex.Substring(5, 2), 16) / 255.0,
        };

        /// sRGB transfer function, inverted. IEC 61966-2-1.
        static double Linear(double c) =>
            c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);

        /// Gamma-encoded sRGB hex to linear RGB. The space the matrices want.
        static double[] LinearRgb(string hex) => Rgb(hex).Select(Linear).ToArray();

        /// The CVD projection, applied in linear RGB and clamped back into
        /// gamut there. Out-of-gamut is expected: the matrices carry negative
        /// coefficients precisely because a dichromat's gamut is a subset.
        static double[] Simulate(double[] linearRgb, string deficiency)
        {
            var m = Cvd[deficiency];
            return Enumerable.Range(0, 3)
                             .Select(i => Math.Clamp(m[i][0] * linearRgb[0] +
                                                     m[i][1] * linearRgb[1] +
                                                     m[i][2] * linearRgb[2], 0, 1))
                             .ToArray();
        }

        static double F(double t) =>
            t > 216.0 / 24389.0 ? Math.Cbrt(t) : (841.0 / 108.0) * t + 4.0 / 29.0;

        /// Linear sRGB to CIE L*a*b* under D65. Verified against published
        /// values: #ffffff gives L* 100.000, #808080 gives 53.585, and #ff0000
        /// gives (53.241, 80.092, 67.203).
        static double[] Lab(double[] linear)
        {
            var x = 0.4124564 * linear[0] + 0.3575761 * linear[1] + 0.1804375 * linear[2];
            var y = 0.2126729 * linear[0] + 0.7151522 * linear[1] + 0.0721750 * linear[2];
            var z = 0.0193339 * linear[0] + 0.1191920 * linear[1] + 0.9503041 * linear[2];
            double fx = F(x / 0.95047), fy = F(y / 1.00000), fz = F(z / 1.08883);
            return new[] { 116.0 * fy - 16.0, 500.0 * (fx - fy), 200.0 * (fy - fz) };
        }

        /// Lab of one species hex as the named viewer sees it. Pass "normal"
        /// for no deficiency - which is how the token collisions above get
        /// their dE of 0.
        public static double[] SeenAs(string hex, string deficiency) =>
            deficiency == "normal" ? Lab(LinearRgb(hex))
                                   : Lab(Simulate(LinearRgb(hex), deficiency));

        /// Every unordered pair under every deficiency, ordinal-sorted so the
        /// emitted file is stable and a diff is readable. Keys are
        /// "A/B\tdeficiency" in Species declaration order, which is NOT
        /// alphabetical within the pair - the plan's worked example transposed
        /// three of them.
        public static SortedDictionary<string, Separation> Measure()
        {
            var result = new SortedDictionary<string, Separation>(StringComparer.Ordinal);
            for (int i = 0; i < Species.Length; i++)
                for (int j = i + 1; j < Species.Length; j++)
                    foreach (var k in Deficiencies)
                    {
                        var a = SeenAs(Species[i].Hex, k);
                        var b = SeenAs(Species[j].Hex, k);
                        var dl = Math.Abs(a[0] - b[0]);
                        var de = Math.Sqrt((a[0] - b[0]) * (a[0] - b[0]) +
                                           (a[1] - b[1]) * (a[1] - b[1]) +
                                           (a[2] - b[2]) * (a[2] - b[2]));
                        result[$"{Species[i].Name}/{Species[j].Name}\t{k}"] =
                            new Separation(Math.Round(dl, 1), Math.Round(de, 1));
                    }
            return result;
        }
    }
}
