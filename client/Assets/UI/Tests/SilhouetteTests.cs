using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Broodline.UI.Tests
{
    /// bible 10.2 rule 1, asserted against the BAKED bodies from Phase 9's
    /// pipeline; a collision here is a collision in the recipes:
    ///
    ///   "All six species must be distinguishable as flat black shapes at
    ///    40px; if two are confusable, one is wrong."
    ///
    /// Through Task 8 this ran against six interim proxy PNGs, drawn from
    /// bible 1.2's silhouette column so a collision between them would be a
    /// collision in the DESIGN rather than in the drawing. `CreatureBaker`
    /// retires that stand-in: the mask now comes from `CreatureAssembler`'s
    /// actual mesh, rendered by the one fixed bake camera, so a collision
    /// here is a collision in a RECIPE (`SpeciesRecipes`) rather than in a
    /// hand-drawn placeholder. rig_proof.md section 6 still routes a finding
    /// back to the bible when two recipes genuinely read the same at 40px;
    /// it is the recipes that would need to change to fix it, not this test.
    ///
    /// The threshold is deliberately low. This is a collision detector, not a
    /// quality bar: two shapes differing on fewer than 8% of a 40x40 field are
    /// the same shape at a glance.
    ///
    /// ONLY VETCH IS BAKED. `SpeciesRecipes.All` and `PartRecipes.All` carry
    /// one body and three parts until Task 15 fills in the other five
    /// species; asserting `NoTwoSpeciesAreConfusableAsFlatBlackShapesAt40px`
    /// against a five-species gap would be asserting against art that does
    /// not exist. `Species` below is narrowed to `{ "vetch" }` for this task
    /// - Task 15 restores the six, and with them the pairwise comparison.
    public class SilhouetteTests
    {
        const int Size = 40;
        const double MinDifferingFraction = 0.08;

        // Task 15 restores the six ({ "vetch", "ember", "skitter", "hollow",
        // "loam", "pale" }) once every species is baked.
        static readonly string[] Species = { "vetch" };

        /// A second, independent statement of "how many species are baked right
        /// now", asserted against `Species.Length` at the top of the pairwise
        /// test below. With fewer than two species there are zero pairs to
        /// compare, so `NoTwoSpeciesAreConfusableAsFlatBlackShapesAt40px`'s
        /// `collisions` list stays empty and it PASSES whether the detector
        /// ran a real comparison or none at all - the only trace of that was a
        /// log line this project's own test command never shows. Bump this
        /// alongside `Species` when Task 15 restores the six; leaving one of
        /// the two behind fails loudly instead of the test staying quiet.
        const int ExpectedBakedSpecies = 1;

        /// 40x40 coverage mask: true where the baked body is opaque.
        static bool[] Mask(string species)
        {
            var path = $"Assets/UI/Resources/Art/creatures/bodies/{species}.png";
            var src = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            Assert.IsNotNull(src, $"no baked body at {path} - run generate-creatures.sh with BAKE=1");

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
            // against how many the loop above actually ran - so a collision
            // detector that silently compared zero pairs (today: 1 species,
            // 0 pairs, by design) is at least an ASSERTED zero, not a quiet
            // one indistinguishable from six species that all passed clean.
            int expectedPairs = ExpectedBakedSpecies * (ExpectedBakedSpecies - 1) / 2;
            Assert.AreEqual(expectedPairs, measured.Count,
                $"compared {measured.Count} pairs, not the {expectedPairs} that {ExpectedBakedSpecies} baked " +
                "species implies - the pairwise loop's bounds have drifted from Species");

            // The MARGIN is the interesting number and a pass throws it away.
            // Logged rather than asserted: a floor on the closest pair would
            // be a second, tighter threshold nobody agreed to, and tightening
            // this detector is how it stops detecting. `verify-uss-tokens.sh`
            // makes the same distinction - measure, print, gate on one line.
            // With only Vetch baked, `measured` is empty and this line logs
            // nothing to compare - Task 15's job, not this one's.
            Debug.Log("bible 10.2 rule 1, pairwise at 40px (floor " +
                      $"{MinDifferingFraction:P0}): " + string.Join(", ", measured));

            Assert.IsEmpty(collisions,
                "bible 10.2 rule 1 fails on these pairs - one of each is wrong, and the fix is a " +
                "DESIGN change to bible 1.2's silhouette column or to a SpeciesRecipe, not a re-bake:\n" +
                string.Join("\n", collisions));
        }
    }
}
