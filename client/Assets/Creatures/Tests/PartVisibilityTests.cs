using System.Collections.Generic;
using System.IO;
using System.Linq;
using Broodline.Creatures;
using NUnit.Framework;
using UnityEngine;

namespace Broodline.Creatures.Tests
{
    /// bible 10.4, which had NO coverage anywhere in this codebase until now:
    ///
    ///   "The two combat traits are visible on the body. Under hard counters a
    ///    player scanning the roster must see at a glance who carries Chill and
    ///    who carries Reach - that read decides waves. This is the single most
    ///    important functional requirement in the art direction."
    ///
    /// The only assertion about part colour that existed was
    /// `RecipeTests.PartRecipes_HaveColoursAndAreLookedUpByTrait` - `part.Base
    /// != part.Under`, which relates a part to ITSELF. Nothing related a part
    /// to the body it mounts on, and that absence is exactly what let round one
    /// ship ten of twelve parts whose base hex was BYTE-IDENTICAL to their own
    /// species' body: `pale screen@sk_flank` measured deltaE 0.93.
    ///
    /// WHY THIS IS NOT bible 10.2 RULE 1. That rule governs the six species as
    /// flat black shapes at 40px - bare bodies, no colour, no parts - and
    /// `Broodline.UI.Tests.SilhouetteTests` already asserts it. Colour cannot
    /// help there and shape cannot help here. Two requirements, two gates.
    ///
    /// WHY deltaE AND NOT A LUMINANCE RATIO. A luminance floor fails
    /// `vetch splash@sk_flank` at 1.006 - coral on teal, which any sighted
    /// player can see instantly - while passing nothing a hue-blind one gains.
    /// bible 1.2 already reasons in deltaE: it records the palette's worst
    /// colour-blind pair at 7.2 and the fix that moved it to 17.3. So this uses
    /// the instrument the project already chose, and logs the luminance ratio
    /// beside it without gating on it.
    ///
    /// READ OFF THE BAKED SPRITES, not off a render set up here. The creature
    /// card stacks `bodies/{species}.png` and `parts/{species}-{socket}-
    /// {trait}.png`, both from `CreatureBaker`'s one fixed camera, so where a
    /// part's sprite covers the body's sprite is literally what a player sees.
    /// Loaded through `Texture2D.LoadImage` rather than the AssetDatabase so
    /// the bytes are the stored sRGB ones and no import setting or colour-space
    /// conversion sits between the measurement and the file.
    public class PartVisibilityTests
    {
        /// bible 1.2 calls 7.2 the palette's worst colour-blind pair and
        /// changed a species colour over it. A floor just above that says: no
        /// part may separate from a body by less than the worst thing this
        /// project has ever shipped and then fixed. It is a detector, not a
        /// quality bar - the shipped minimum is 17.18, better than twice it, and
        /// tightening a detector is how it stops detecting.
        const double MinDeltaE = 8.0;

        /// Below this many overlapping pixels the mean colour of "the part
        /// where it covers the body" is a handful of anti-aliased edge samples
        /// rather than a measurement. 29 of the 144 combinations are parts that
        /// sit almost entirely OFF their body - a taunt mast on Hollow overlaps
        /// 6px of a body it clears by 262 - and for those the colour question
        /// is against the card, not against the hide. The nearest combination
        /// below the floor overlaps 21px, so 40 is not a knife-edge.
        const int MinOverlapPixels = 40;

        /// What the loop below must actually compare, asserted rather than
        /// assumed. A change that quietly drops combinations under the sample
        /// floor would weaken this gate without failing it.
        const int ExpectedCombinations = 144;
        const int ExpectedMeasured = 115;

        /// A part whose sprite is nearly empty cannot be read however well it
        /// separates. The smallest in the roster is `hollow reach@sk_flank` at
        /// 92px of 192x192.
        const int MinPartPixels = 60;

        static string ArtRoot => Path.GetFullPath(Path.Combine(
            Application.dataPath, "UI/Resources/Art/creatures"));

        static Color32[] Load(string path, out int size)
        {
            Assert.IsTrue(File.Exists(path), "no baked sprite at " + path +
                          " - run generate-creatures.sh with BAKE=1");
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Assert.IsTrue(tex.LoadImage(File.ReadAllBytes(path)), "could not decode " + path);
                size = tex.width;
                return tex.GetPixels32();
            }
            finally { Object.DestroyImmediate(tex); }
        }

        // ------------------------------------------------------------- colour

        struct Reading { public double DeltaE; public double Luminance; public int Overlap; public int Pixels; }

        static Dictionary<string, Reading> ReadAll()
        {
            var readings = new Dictionary<string, Reading>();
            foreach (var body in SpeciesRecipes.All)
            {
                var hide = Load(Path.Combine(ArtRoot, "bodies", body.Id + ".png"), out int size);
                foreach (var socket in new[] { Sockets.Dorsal, Sockets.Flank })
                    foreach (var part in PartRecipes.All)
                    {
                        var sprite = Load(Path.Combine(ArtRoot, "parts",
                            body.Id + "-" + socket + "-" + part.Id + ".png"), out int s2);
                        Assert.AreEqual(size, s2, "the body and part sprites must be the same size to stack");

                        double pr = 0, pg = 0, pb = 0, br = 0, bg = 0, bb = 0;
                        int overlap = 0, pixels = 0;
                        for (int i = 0; i < sprite.Length; i++)
                        {
                            if (sprite[i].a <= 127) continue;
                            pixels++;
                            if (hide[i].a <= 127) continue;
                            overlap++;
                            pr += sprite[i].r; pg += sprite[i].g; pb += sprite[i].b;
                            br += hide[i].r;   bg += hide[i].g;   bb += hide[i].b;
                        }

                        var reading = new Reading { Overlap = overlap, Pixels = pixels };
                        if (overlap > 0)
                        {
                            var p = new Vector3((float)(pr / overlap), (float)(pg / overlap), (float)(pb / overlap)) / 255f;
                            var b = new Vector3((float)(br / overlap), (float)(bg / overlap), (float)(bb / overlap)) / 255f;
                            reading.DeltaE = DeltaE(p, b);
                            double lp = Luminance(p), lb = Luminance(b);
                            reading.Luminance = (Mathf.Max((float)lp, (float)lb) + 0.05) /
                                                (Mathf.Min((float)lp, (float)lb) + 0.05);
                        }
                        readings[body.Id + " " + part.Id + "@" + socket] = reading;
                    }
            }
            return readings;
        }

        /// CIE76 between two sRGB-encoded means, through linear RGB and XYZ
        /// (D65). CIE76 rather than a later formula because bible 1.2's own
        /// recorded numbers - 7.2 and 17.3 - are the scale this floor is set
        /// against, and a different formula would not be comparable to them.
        static double DeltaE(Vector3 a, Vector3 b)
        {
            var la = Lab(a); var lb = Lab(b);
            return (la - lb).magnitude;
        }

        static Vector3 Lab(Vector3 srgb)
        {
            float lr = ToLinear(srgb.x), lg = ToLinear(srgb.y), lb = ToLinear(srgb.z);
            float x = (0.4124f * lr + 0.3576f * lg + 0.1805f * lb) / 0.95047f;
            float y = (0.2126f * lr + 0.7152f * lg + 0.0722f * lb);
            float z = (0.0193f * lr + 0.1192f * lg + 0.9505f * lb) / 1.08883f;
            float fx = Pivot(x), fy = Pivot(y), fz = Pivot(z);
            return new Vector3(116f * fy - 16f, 500f * (fx - fy), 200f * (fy - fz));
        }

        static float Pivot(float t) =>
            t > 0.008856f ? Mathf.Pow(t, 1f / 3f) : 7.787f * t + 16f / 116f;

        static float ToLinear(float c) =>
            c <= 0.04045f ? c / 12.92f : Mathf.Pow((c + 0.055f) / 1.055f, 2.4f);

        static double Luminance(Vector3 c) =>
            0.2126 * ToLinear(c.x) + 0.7152 * ToLinear(c.y) + 0.0722 * ToLinear(c.z);

        [Test]
        public void EveryPartOnEveryBody_SeparatesFromTheHideItSitsOn()
        {
            var readings = ReadAll();
            Assert.AreEqual(ExpectedCombinations, readings.Count,
                "compared " + readings.Count + " combinations, not the " + ExpectedCombinations +
                " that " + SpeciesRecipes.All.Count + " bodies, " + PartRecipes.All.Count +
                " parts and two sockets imply");

            var measured = readings.Where(kv => kv.Value.Overlap >= MinOverlapPixels)
                                   .OrderBy(kv => kv.Value.DeltaE).ToList();
            Assert.AreEqual(ExpectedMeasured, measured.Count,
                measured.Count + " combinations carry enough overlap to measure, not the " +
                ExpectedMeasured + " this gate was written against - a part that moved off its " +
                "body is a part this gate stopped covering, and it should say so rather than " +
                "quietly compare fewer");

            var failures = measured.Where(kv => kv.Value.DeltaE < MinDeltaE)
                .Select(kv => "  " + kv.Key + ": deltaE " + kv.Value.DeltaE.ToString("F2") +
                              " (luminance " + kv.Value.Luminance.ToString("F3") + ", " +
                              kv.Value.Overlap + "px)")
                .ToList();

            // The MARGIN is the interesting number and a pass throws it away -
            // the same call `SilhouetteTests` and `AssemblerTests` make.
            Debug.Log("bible 10.4, part against its body over " + measured.Count +
                      " combinations (floor deltaE " + MinDeltaE + "): tightest " +
                      measured[0].Value.DeltaE.ToString("F2") + " on " + measured[0].Key +
                      ". Ten closest: " + string.Join(", ", measured.Take(10).Select(
                          kv => kv.Key + " " + kv.Value.DeltaE.ToString("F1"))));

            Assert.IsEmpty(failures,
                "bible 10.4 makes trait visibility the single most important functional " +
                "requirement in the art direction, and these parts do not separate from the " +
                "body they mount on. The fix is a DESIGN change in PartRecipes - see its note " +
                "on PartValue and PartSaturation - not a re-bake:\n" + string.Join("\n", failures));
        }

        [Test]
        public void EveryPartOnEveryBody_IsDrawnAtAll()
        {
            // The other half of "visible": `AssemblerTests` proves two parts do
            // not overlap each other and nothing proved either of them renders.
            var readings = ReadAll();
            var blank = readings.Where(kv => kv.Value.Pixels < MinPartPixels)
                .Select(kv => "  " + kv.Key + ": " + kv.Value.Pixels + "px").ToList();
            Assert.IsEmpty(blank,
                "these parts bake to almost nothing - a socket scale or position has put them " +
                "inside the body or outside the camera:\n" + string.Join("\n", blank));

            var smallest = readings.OrderBy(kv => kv.Value.Pixels).First();
            Debug.Log("bible 10.4, part sprite coverage over " + readings.Count +
                      " combinations (floor " + MinPartPixels + "px of 192x192): smallest " +
                      smallest.Value.Pixels + "px on " + smallest.Key);
        }
    }
}
