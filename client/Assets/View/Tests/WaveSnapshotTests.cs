using NUnit.Framework;
using Broodline.Sim.Combat;
using Broodline.View;

namespace Broodline.View.Tests
{
    public class WaveSnapshotTests
    {
        // Every step loop below carries `&& runner.Step()`. Without it the
        // condition spins forever once the run terminates - Step stops
        // incrementing Tick - so a balance change that ends wave 6 early would
        // HANG the EditMode runner with no timeout and no result rather than
        // failing. SimRunnerTests uses the same idiom.

        static SimRunner Runner() => new SimRunner(
            WaveDef.Wave6(), Lane.Defile(), Deployment(), 6UL);

        static CreatureSpec[] Deployment() => new[]
        {
            new CreatureSpec { Species = Species.Vetch, Pocket = 0, Instinct = Instinct.Vanguard },
            new CreatureSpec { Species = Species.Vetch, Pocket = 1, Instinct = Instinct.Vanguard },
            new CreatureSpec { Species = Species.Vetch, Pocket = 2, Instinct = Instinct.Vanguard },
            new CreatureSpec { Species = Species.Vetch, Pocket = 3, Instinct = Instinct.Vanguard },
            new CreatureSpec { Species = Species.Loam,  Pocket = 4, Instinct = Instinct.Vanguard }
        };

        [Test]
        public void APairRetainsPreviousAndCurrent()
        {
            // Interpolation needs both. client_architecture section 2.
            var runner = Runner();
            var pair = new WavePair(runner);

            while (runner.Tick < 100 && runner.Step()) { pair.Advance(runner); }
            float a = pair.Previous.RaiderTile(0);

            runner.Step(); pair.Advance(runner);
            float b = pair.Previous.RaiderTile(0);

            Assert.AreNotEqual(a, b, "Previous did not advance - the pair is not swapping");
            Assert.Greater(pair.Current.RaiderTile(0), pair.Previous.RaiderTile(0),
                "the Courser moves toward the Ark, so current must lead previous");
        }

        [Test]
        public void CaptureConvertsFixedPointWithoutLosingTheTile()
        {
            var runner = Runner();
            var pair = new WavePair(runner);
            while (runner.Tick < 120 && runner.Step()) { pair.Advance(runner); }

            // The engine's own value is the authority. Fix64 carries no float
            // conversion because floats are banned there, so this pins the one
            // place the conversion happens.
            var raw = runner.RaiderProgress[0].Raw / 4294967296.0;
            Assert.AreEqual(raw, pair.Current.RaiderTile(0), 1e-4);
        }

        [Test]
        public void SnapshotsDoNotAliasEngineArrays()
        {
            // View copies what it needs. If a snapshot held the engine's array
            // instead, Previous and Current would be the same object and
            // interpolation would render nothing.
            var runner = Runner();
            var pair = new WavePair(runner);
            while (runner.Tick < 100 && runner.Step()) { pair.Advance(runner); }

            float before = pair.Previous.RaiderTile(0);
            for (int i = 0; i < 10; i++) runner.Step();
            Assert.AreEqual(before, pair.Previous.RaiderTile(0),
                "the snapshot changed when the engine did - it is aliasing");
        }
    }
}
