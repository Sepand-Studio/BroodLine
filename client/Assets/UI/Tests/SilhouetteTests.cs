using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Broodline.UI.Tests
{
    /// bible 10.2 rule 1, asserted against the baked production card bodies;
    /// a collision here is a collision in the authored Frontier silhouettes:
    ///
    ///   "All six species must be distinguishable as flat black shapes at
    ///    40px; if two are confusable, one is wrong."
    ///
    /// `FrontierBaker` now renders the same procedural bodies used by the
    /// live portrait and deploy preview into the card resource paths. A
    /// collision should lead to an art change, never to a hand-edited mask.
    ///
    /// The threshold is deliberately low. This is a collision detector, not a
    /// quality bar: two shapes differing on fewer than 8% of a 40x40 field are
    /// the same shape at a glance.
    ///
    /// ALL SIX ARE BAKED. Through Task 14 this list was narrowed to
    /// `{ "vetch" }`, because asserting a pairwise detector against five
    /// species that did not exist would have been asserting against absent
    /// art. Task 15 authored the other five, and this is the restoration:
    /// six species, fifteen pairs, and the collision detector doing the job
    /// it was written for.
    ///
    /// The old Phase 9 closest-pair measurement no longer describes these
    /// sprites. The test logs the current pairwise distances after each bake.
    public class SilhouetteTests
    {
        const int Size = 40;
        const double MinDifferingFraction = 0.08;

        // bible 1.2's order.
        static readonly string[] Species = { "vetch", "ember", "skitter", "hollow", "loam", "pale" };

        /// A second, independent statement of "how many species are baked right
        /// now", asserted against `Species.Length` at the top of the pairwise
        /// test below. A pairwise loop over too few species compares too few
        /// pairs and PASSES on an empty `collisions` list, which is
        /// indistinguishable from a clean sweep - and the only trace of that
        /// was a log line this project's own test command never shows. Keep
        /// this and `Species` in step; leaving one behind fails loudly instead
        /// of the test staying quiet.
        const int ExpectedBakedSpecies = 6;

        /// 40x40 coverage mask: true where the baked body is opaque.
        static bool[] Mask(string species)
        {
            var path = $"Assets/UI/Resources/Art/creatures/bodies/{species}.png";
            var src = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            Assert.IsNotNull(src, $"no baked body at {path} - run implementation/scripts/bake-frontier.sh");

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
        public void EveryBakedBody_IsNotBlank()
        {
            foreach (var s in Species)
            {
                var filled = Mask(s).Count(b => b);
                Assert.That(filled, Is.GreaterThan(Size * Size / 20),
                    $"{s}'s baked body is blank or nearly so - a blank mask would pass the pairwise test trivially");
            }
        }

        [Test]
        public void EveryBakedBody_HasATransparentBackground()
        {
            // A baked body on an opaque background would make every mask a
            // filled square and pass the pairwise test trivially. The bake
            // clears to alpha 0; this is the check that it did.
            foreach (var s in Species)
            {
                var mask = Mask(s);
                Assert.IsFalse(mask[0], s + "'s top-left corner is opaque - the bake did not clear to transparent");
                Assert.IsFalse(mask[mask.Length - 1], s + "'s bottom-right corner is opaque");
            }
        }

        [Test]
        public void NoTwoSpeciesAreConfusableAsFlatBlackShapesAt40px()
        {
            // The precondition this whole test is meaningless without: with
            // fewer species baked than `Species` claims (or more), the loop
            // below compares the wrong number of pairs - most dangerously,
            // silently compares FEWER, which is how a collision would go
            // undetected. See `ExpectedBakedSpecies`'s comment.
            Assert.AreEqual(ExpectedBakedSpecies, Species.Length,
                "Species and ExpectedBakedSpecies have drifted apart - update both together");

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

            // How many pairs `ExpectedBakedSpecies` species implies, asserted
            // against how many the loop above actually ran - so a detector that
            // silently compared fewer pairs than the roster implies is caught
            // rather than reported as a clean sweep. Six species, fifteen pairs.
            int expectedPairs = ExpectedBakedSpecies * (ExpectedBakedSpecies - 1) / 2;
            Assert.AreEqual(expectedPairs, measured.Count,
                $"compared {measured.Count} pairs, not the {expectedPairs} that {ExpectedBakedSpecies} baked " +
                "species implies - the pairwise loop's bounds have drifted from Species");

            // The MARGIN is the interesting number and a pass throws it away.
            // Logged rather than asserted: a floor on the closest pair would
            // be a second, tighter threshold nobody agreed to, and tightening
            // this detector is how it stops detecting. `verify-uss-tokens.sh`
            // makes the same distinction - measure, print, gate on one line.
            // The previous 9.6% Ember/Hollow reading was from Phase 9 art.
            // This log is the current measurement after a Frontier bake.
            Debug.Log("bible 10.2 rule 1, pairwise at 40px (floor " +
                      $"{MinDifferingFraction:P0}): " + string.Join(", ", measured));

            Assert.IsEmpty(collisions,
                "bible 10.2 rule 1 fails on these pairs - one of each is wrong, and the fix is a " +
                "DESIGN change to bible 1.2's silhouette column or to a SpeciesRecipe, not a re-bake:\n" +
                string.Join("\n", collisions));
        }
    }
}
