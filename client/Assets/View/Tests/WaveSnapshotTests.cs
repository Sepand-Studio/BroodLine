using NUnit.Framework;
using Broodline.Sim.Combat;
using Broodline.View;

namespace Broodline.View.Tests
{
    public class WaveSnapshotTests
    {
        // Every bounded loop below carries `&& runner.Step()`. Without it the
        // condition spins forever once the run terminates - Step stops
        // incrementing Tick - so a balance change that ends wave 6 early would
        // HANG the EditMode runner with no timeout and no result rather than
        // failing. SimRunnerTests uses the same idiom.
        //
        // The price is that those loops stop BEFORE the terminating step's
        // capture, which models the clock as it was before the terminating tick
        // was captured at all. TheBreachFrameIsCapturedAndDrawn is the one that
        // runs to the end, because that frame is the whole of wave 6's verdict
        // and nothing else here reaches it.

        // The roster lives in WaveFixture - it was typed out here and in the
        // sibling test file, identically, in the same asmdef.
        static SimRunner Runner() => WaveFixture.Runner();

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

        [Test]
        public void TheBreachFrameIsCapturedAndDrawn()
        {
            // Driven the way WaveClock drives it: Advance after EVERY Step,
            // including the one that returns false.
            //
            // Two separate bugs met on this frame. The clock used to return
            // before the terminating capture, so the pair held the
            // second-to-last tick forever - the Courser froze one tile short of
            // the Ark while the HUD printed Loss. Capturing it was necessary
            // and not sufficient: Phases.Breach clears RaiderAlive on the same
            // tick it takes the integrity, and both renderers gated on
            // aliveness, so the fixed clock drew the breach as a
            // DISAPPEARANCE instead.
            var runner = Runner();
            var pair = new WavePair(runner);

            // Bounded by the engine's own hard cap, so a balance change that
            // never terminates fails here rather than hanging the runner.
            for (int i = 0; i <= Stats.HardTickCap + 1; i++)
            {
                bool more = runner.Step();
                pair.Advance(runner);
                if (!more) break;
            }

            Assert.IsTrue(runner.Done, "the wave never terminated");
            Assert.AreEqual(runner.Outcome.Ticks, pair.Current.Tick,
                "the terminating tick was not captured");

            Assert.IsFalse(pair.Current.RaiderAlive(0),
                "Phases.Breach clears aliveness on the breach tick - if this is true the test is stale");
            Assert.IsTrue(pair.Current.RaiderBreaching(0));
            Assert.IsTrue(pair.Current.RaiderVisible(0),
                "the breach frame is not drawn - the Courser vanishes instead of reaching the Ark");
            Assert.Greater(pair.Current.RaiderTile(0), Stats.LaneTiles - 1,
                "the breaching raider should be drawn at the Ark");
        }

        [Test]
        public void CreaturePositionComesFromTheSnapshot()
        {
            // "ONE source per entity" covers position too. The HUD and the View
            // both read the pocket tile live off the runner while everything
            // else came from the pair, so a Skittish reposition mid-frame moved
            // a body and its bar at different moments.
            var runner = Runner();
            var pair = new WavePair(runner);

            Assert.AreEqual(runner.CreatureCount, pair.Current.CreatureCount);
            for (int c = 0; c < runner.CreatureCount; c++)
                Assert.AreEqual(runner.Lane.PocketTiles[runner.CreaturePocket[c]],
                                pair.Current.CreatureTile(c));
        }
    }
}
