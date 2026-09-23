using Broodline.Creatures;
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

        /// THE GAP THIS TEST GUARDED IS CLOSED, AND THE OLD ASSERTIONS HAD
        /// STOPPED BEING ABLE TO FAIL.
        ///
        /// Through Task 14 neither `Species.Loam` nor Wave 1's Skirmishers had
        /// a recipe. `CreatureAssembler.Build` answers an unbuildable look with
        /// a magenta sphere and a `Debug.LogError`, and Unity's Test Framework
        /// fails a test on any unhandled `LogError` - so `WaveView.Build`
        /// checks the recipe itself and draws an inert placeholder instead.
        /// The old form asserted that placeholder by reading
        /// `GetComponent<Renderer>()` on the creature ROOT, and that is null
        /// EITHER WAY: `CreatureGenerator` puts the renderer on a "body" child,
        /// so the assertion passed for a body that was skipped and passed for a
        /// body that was built.
        ///
        /// Task 15 authored the last five species and all three raider types -
        /// `Species` has six members and `RaiderType` three, and every one of
        /// them now has a recipe - so the placeholder branch is unreachable
        /// from real content. The honest test is the inverted one: the roster
        /// is complete, and every body the lane asks for draws something. The
        /// guard stays in `WaveView` for the next species that does not exist
        /// yet; what is asserted here is that there is no such species today.
        ///
        /// A `SkinnedMeshRenderer` is the right thing to look for because it
        /// catches BOTH failure modes at once: the inert placeholder has no
        /// renderer at all, and `CreatureAssembler`'s magenta stand-in is a
        /// primitive sphere carrying a plain `MeshRenderer`.
        [Test]
        public void Build_DrawsEverySpeciesAndEveryRaiderType_TheRosterIsComplete()
        {
            foreach (Species s in System.Enum.GetValues(typeof(Species)))
                Assert.IsNotNull(SpeciesRecipes.For(s.ToString()),
                    s + " has no body recipe - the lane would draw an empty placeholder for it");
            foreach (RaiderType t in System.Enum.GetValues(typeof(RaiderType)))
                Assert.IsNotNull(RaiderRecipes.For(t.ToString()),
                    t + " has no raider recipe - the lane would draw nothing for it");

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
