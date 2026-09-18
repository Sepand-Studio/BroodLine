using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Broodline.UI.Tests
{
    /// bible 10.2 rule 1, asserted before Phase 9 generates anything:
    ///
    ///   "All six species must be distinguishable as flat black shapes at
    ///    40px; if two are confusable, one is wrong."
    ///
    /// These are interim proxies, not production art - but they are drawn from
    /// bible 1.2's silhouette column, so if two of THEM collide, the collision
    /// is in the DESIGN and not in the drawing. rig_proof.md section 6 routes
    /// that finding back to the bible rather than to the art team, and section
    /// 5 says item 7 is "the one that decides the art direction". Finding it
    /// here costs six PNGs; finding it after Phase 9 costs the asset budget.
    ///
    /// The threshold is deliberately low. This is a collision detector, not a
    /// quality bar: two shapes differing on fewer than 8% of a 40x40 field are
    /// the same shape at a glance.
    ///
    /// IT READS ALPHA, NOT COLOUR, AND THAT IS WHY THE SOURCES ARE WHITE.
    /// The plan's Step 1 says "flat black on transparent". The card tints
    /// these through -unity-background-image-tint-color, which MULTIPLIES -
    /// black times any tint is black, so black sources would have rendered
    /// all six species as one identical mark with no error anywhere. The
    /// files fill white and this test is unaffected, because a coverage mask
    /// is a question about alpha. "Flat black shapes" is what the RULE is
    /// about and this is the measurement of it. CreatureCard.uss's proxy
    /// block has the whole account.
    ///
    /// MEASURED MARGIN, so a later drift is readable against something. On
    /// the six as first drawn, the closest pair is hollow/pale and the widest
    /// is vetch/pale; every pair is comfortably clear of the 8% line and the
    /// run logs the full ranked matrix. No pair collided on a first faithful
    /// reading of bible 1.2, which is the finding this task was built to look
    /// for and did not find.
    public class SilhouetteTests
    {
        const int Size = 40;
        const double MinDifferingFraction = 0.08;

        static readonly string[] Species = { "vetch", "ember", "skitter", "hollow", "loam", "pale" };

        /// 40x40 coverage mask: true where the silhouette is opaque.
        static bool[] Mask(string species)
        {
            var path = $"Assets/UI/Art/proxies/{species}.png";
            var src = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            Assert.IsNotNull(src, $"no proxy at {path}");

            var rt = RenderTexture.GetTemporary(Size, Size, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(src, rt);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var small = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            small.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
            small.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);

            var mask = small.GetPixels().Select(p => p.a > 0.5f).ToArray();
            Object.DestroyImmediate(small);
            return mask;
        }

        [Test]
        public void EverySpeciesHasAProxyThatIsNotBlank()
        {
            foreach (var s in Species)
            {
                var filled = Mask(s).Count(b => b);
                Assert.That(filled, Is.GreaterThan(Size * Size / 20),
                    $"{s}'s proxy is blank or nearly so - a blank mask would pass the pairwise test trivially");
            }
        }

        [Test]
        public void NoTwoSpeciesAreConfusableAsFlatBlackShapesAt40px()
        {
            var masks = Species.ToDictionary(s => s, Mask);
            var collisions = new List<string>();
            var measured = new List<string>();

            for (int i = 0; i < Species.Length; i++)
                for (int j = i + 1; j < Species.Length; j++)
                {
                    var a = masks[Species[i]];
                    var b = masks[Species[j]];
                    var differing = a.Where((v, idx) => v != b[idx]).Count() / (double)a.Length;
                    measured.Add($"{Species[i]}/{Species[j]} {differing:P1}");
                    if (differing < MinDifferingFraction)
                        collisions.Add($"  {Species[i]}/{Species[j]}: {differing:P1} of the field differs");
                }

            // The MARGIN is the interesting number and a pass throws it away.
            // Logged rather than asserted: a floor on the closest pair would
            // be a second, tighter threshold nobody agreed to, and tightening
            // this detector is how it stops detecting. `verify-uss-tokens.sh`
            // makes the same distinction - measure, print, gate on one line.
            Debug.Log("bible 10.2 rule 1, pairwise at 40px (floor " +
                      $"{MinDifferingFraction:P0}): " + string.Join(", ", measured));

            Assert.IsEmpty(collisions,
                "bible 10.2 rule 1 fails on these pairs - one of each is wrong, and the fix is a " +
                "DESIGN change to bible 1.2's silhouette column, not a redraw:\n" +
                string.Join("\n", collisions));
        }
    }
}
