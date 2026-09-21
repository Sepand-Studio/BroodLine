using Broodline.Sim.Combat;
using Broodline.View;
using NUnit.Framework;
using UnityEngine;

namespace Broodline.View.Tests
{
    public class WaveViewTests
    {
        [Test]
        public void Build_MakesOneAssembledCreaturePerDeploymentSlot_AndDressesTheLane()
        {
            var wave = WaveDef.ForId(1);
            var deployment = new[]
            {
                new CreatureSpec { Species = Species.Vetch, Trait1 = Trait.Carapace, Tier1 = 1, Trait2 = Trait.Taunt, Tier2 = 1, Instinct = Instinct.Vanguard, Pocket = 0 },
                new CreatureSpec { Species = Species.Vetch, Trait1 = Trait.Taunt, Tier1 = 1, Trait2 = Trait.Carapace, Tier2 = 1, Instinct = Instinct.Vanguard, Pocket = 1 },
            };
            var runner = new SimRunner(wave, wave.Lane, deployment, 6UL);
            var go = new GameObject("view");
            try
            {
                var view = go.AddComponent<WaveView>();
                view.Build(runner, deployment, wave);

                var creatures = go.transform.Find("creatures");
                Assert.AreEqual(2, creatures.childCount);
                Assert.AreEqual("carapace", creatures.GetChild(0).Find("sk_dorsal").GetChild(0).name);
                Assert.AreEqual("taunt", creatures.GetChild(1).Find("sk_dorsal").GetChild(0).name, "slot index, not trait, picks the socket");
                Assert.IsNotNull(go.transform.Find("dressing/ark"));
                Assert.IsNotNull(go.transform.Find("dressing/path"));
                Assert.AreEqual(runner.RaiderHp.Length, go.transform.Find("raiders").childCount, "one body per raider slot, inactive until visible");
            }
            finally { Object.DestroyImmediate(go); }
        }

        /// Wave 1's Skirmishers have no `RaiderRecipes` entry and `Species.Loam`
        /// has no `SpeciesRecipes` entry - both Task 15. `CreatureAssembler
        /// .Build` answers either with a magenta "missing" sphere and a
        /// `Debug.LogError`, and Unity's Test Framework fails a test on any
        /// unhandled `LogError` - which is exactly what would turn every wave
        /// with an unbuilt raider or species into a broken PlayMode capture
        /// test the moment one ran. `WaveView.Build` is expected to check the
        /// recipe itself and draw nothing for a body it has none for, rather
        /// than handing an unbuildable look to the assembler. Nothing here
        /// calls `LogAssert.Expect` for an error - if the guard failed to
        /// stop it, THIS TEST WOULD FAIL from the unhandled error alone.
        [Test]
        public void Build_SkipsBodiesWithNoRecipeYet_NoErrorAndNoMagentaSphere()
        {
            var wave = WaveDef.ForId(1);
            var deployment = new[]
            {
                new CreatureSpec { Species = Species.Loam, Instinct = Instinct.Vanguard, Pocket = 0 },
            };
            var runner = new SimRunner(wave, wave.Lane, deployment, 6UL);
            var go = new GameObject("view");
            try
            {
                var view = go.AddComponent<WaveView>();
                view.Build(runner, deployment, wave);

                var creatures = go.transform.Find("creatures");
                Assert.AreEqual(1, creatures.childCount);
                Assert.IsNull(creatures.GetChild(0).GetComponent<Renderer>(),
                    "no recipe yet: an empty placeholder, not CreatureAssembler's magenta sphere");

                var raiders = go.transform.Find("raiders");
                Assert.AreEqual(runner.RaiderHp.Length, raiders.childCount);
                Assert.IsNull(raiders.GetChild(0).GetComponent<Renderer>(),
                    "no recipe yet: an empty placeholder, not CreatureAssembler's magenta sphere");
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
