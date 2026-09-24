using Broodline.Frontier;
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
                Assert.AreEqual("carapace", creatures.GetChild(0).GetComponent<FrontierCreature>().Dorsal.GetChild(0).name);
                Assert.AreEqual("taunt", creatures.GetChild(1).GetComponent<FrontierCreature>().Dorsal.GetChild(0).name, "slot index, not trait, picks the socket");
                Assert.IsNotNull(go.transform.Find("dressing/ark"));
                Assert.IsNotNull(go.transform.Find("dressing/path"));
                Assert.AreEqual(runner.RaiderHp.Length, go.transform.Find("raiders").childCount, "one body per raider slot, inactive until visible");
            }
            finally { Object.DestroyImmediate(go); }
        }

        /// Every implemented sim body has a Frontier visual and the live view
        /// builds a skinned mesh for each slot, including inactive raiders.
        [Test]
        public void Build_DrawsEverySpeciesAndEveryRaiderType_TheRosterIsComplete()
        {
            foreach (Species s in System.Enum.GetValues(typeof(Species)))
                Assert.IsTrue(FrontierVisuals.Has(s.ToString()),
                    s + " has no Frontier body - the lane would draw an empty placeholder for it");
            foreach (RaiderType t in System.Enum.GetValues(typeof(RaiderType)))
                Assert.IsTrue(FrontierVisuals.Has(t.ToString()),
                    t + " has no Frontier raider - the lane would draw nothing for it");

            var wave = WaveDef.ForId(1);
            var deployment = new[]
            {
                new CreatureSpec { Species = Species.Loam, Instinct = Instinct.Vanguard, Pocket = 0 },
                new CreatureSpec { Species = Species.Pale, Instinct = Instinct.Vanguard, Pocket = 1 },
            };
            var runner = new SimRunner(wave, wave.Lane, deployment, 6UL);
            var go = new GameObject("view");
            try
            {
                var view = go.AddComponent<WaveView>();
                view.Build(runner, deployment, wave);

                var creatures = go.transform.Find("creatures");
                Assert.AreEqual(2, creatures.childCount);
                for (int i = 0; i < creatures.childCount; i++)
                    Assert.IsNotNull(creatures.GetChild(i).GetComponentInChildren<SkinnedMeshRenderer>(true),
                        creatures.GetChild(i).name + " drew nothing - it has a recipe, so it should have a body");

                // Raider bodies are built inactive (they appear as they spawn),
                // so the search has to include inactive children.
                var raiders = go.transform.Find("raiders");
                Assert.AreEqual(runner.RaiderHp.Length, raiders.childCount);
                for (int i = 0; i < raiders.childCount; i++)
                    Assert.IsNotNull(raiders.GetChild(i).GetComponentInChildren<SkinnedMeshRenderer>(true),
                        raiders.GetChild(i).name + " drew nothing");
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
