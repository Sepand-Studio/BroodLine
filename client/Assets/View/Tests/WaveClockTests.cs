using NUnit.Framework;
using Broodline.Sim.Combat;
using Broodline.View;

namespace Broodline.View.Tests
{
    public class WaveClockTests
    {
        const double Frame60 = 1.0 / 60.0;
        const double Tick30  = 1.0 / 30.0;

        // The roster lives in WaveFixture - it was typed out here and in the
        // sibling test file, identically, in the same asmdef.
        static SimRunner Runner() => WaveFixture.Runner();

        [Test]
        public void SixtyHertzRenderingStepsTheSimEveryOtherFrame()
        {
            var clock = new WaveClock();
            var runner = Runner();

            clock.Advance(runner, Frame60, null);
            Assert.AreEqual(0, clock.StepsLastFrame);
            clock.Advance(runner, Frame60, null);
            Assert.AreEqual(1, clock.StepsLastFrame);
            clock.Advance(runner, Frame60, null);
            Assert.AreEqual(0, clock.StepsLastFrame);
            clock.Advance(runner, Frame60, null);
            Assert.AreEqual(1, clock.StepsLastFrame);

            Assert.AreEqual(2, runner.Tick);
        }

        [Test]
        public void ATwoHundredMillisecondStallCatchesUpAndDropsNoTick()
        {
            // The rule at client_architecture section 2: a dropped FRAME must
            // never drop a TICK. 200ms is six ticks.
            //
            // The half-tick is deliberate. 0.200 and 1.0/30.0 are both inexact
            // in binary, so 0.200 / (1.0/30.0) lands either side of 6 depending
            // on rounding, and a test written against the exact value would be
            // asserting a floating-point tie rather than the behaviour. The
            // extra half-tick puts it clear of the boundary.
            var clock = new WaveClock();
            var runner = Runner();

            clock.Advance(runner, Tick30 * 6.5, null);

            Assert.AreEqual(6, clock.StepsLastFrame);
            Assert.AreEqual(6, runner.Tick);
        }

        [Test]
        public void AStallBeyondTheCapRunsSlowRatherThanSkipping()
        {
            // 15 ticks owed, capped at 8. The remaining 7 stay in the
            // accumulator and are paid off by later frames. Every tick still
            // executes, in order - the wave runs SLOW, which is recoverable,
            // rather than SKIPPING, which is a run the server will not
            // reproduce.
            var clock = new WaveClock();
            var runner = Runner();

            clock.Advance(runner, Tick30 * 15.5, null);
            Assert.AreEqual(WaveClock.MaxCatchUpSteps, clock.StepsLastFrame);
            Assert.AreEqual(8, runner.Tick);

            clock.Advance(runner, 0.0, null);
            Assert.AreEqual(7, clock.StepsLastFrame);
            Assert.AreEqual(15, runner.Tick);
        }

        [Test]
        public void AlphaIsTheFractionOfATickElapsed()
        {
            var clock = new WaveClock();
            var runner = Runner();

            clock.Advance(runner, Tick30 / 2, null);
            Assert.AreEqual(0.5, clock.Alpha, 1e-9);
            Assert.AreEqual(0, runner.Tick);
        }

        [Test]
        public void RallyIsConsumedAtATickBoundaryNotAtTheTapTime()
        {
            // client_architecture section 2: "A Rally tap records the tick
            // index, not a timestamp." The tap arrives mid-frame; it must be
            // consumed by the NEXT step and recorded at that step's tick.
            var clock = new WaveClock();
            var runner = Runner();

            clock.Advance(runner, Tick30 * 3.5, null);    // three steps, half left
            Assert.AreEqual(3, runner.Tick);

            clock.RequestRally(2);
            Assert.AreEqual(-1, runner.ReadRecord().RallyTick, "consumed before a tick boundary");

            clock.Advance(runner, Tick30 * 0.6, null);    // now over a boundary
            Assert.AreEqual(3, runner.ReadRecord().RallyTick);
            Assert.AreEqual(2, runner.ReadRecord().RallyCreature);
        }

        [Test]
        public void OnTickFiresOncePerSimulationStep()
        {
            var clock = new WaveClock();
            var runner = Runner();
            int fired = 0;

            clock.Advance(runner, Tick30 * 6.5, () => fired++);

            Assert.AreEqual(6, fired);
        }

        [Test]
        public void OnTickFiresOnTheTerminatingStepToo()
        {
            // Step runs all eight phases and THEN returns false, so the final
            // tick's mutations - the breach, the integrity drop - are in
            // SimState whether or not the loop continues. Returning before
            // onTick left the pair holding the second-to-last tick forever.
            //
            // Outcome.Ticks counts the steps that did NOT terminate, so the
            // number of Step calls, and of fires, is one more than that. An
            // equality rather than a Greater: off by one in either direction is
            // the bug this is here for.
            var clock = new WaveClock();
            var runner = Runner();
            int fired = 0;

            for (int i = 0; i < 500 && !clock.Terminated; i++)
                clock.Advance(runner, Tick30 * 6.5, () => fired++);

            Assert.IsTrue(runner.Done);
            Assert.AreEqual(runner.Outcome.Ticks + 1, fired);

            // Pinned at a whole tick, so the final frame renders Current - the
            // tick that just terminated - rather than lerping back to Previous.
            Assert.AreEqual(1.0, clock.Alpha, 1e-9);
        }

        [Test]
        public void BacklogIsVisibleWhereStepsLastFrameSaturates()
        {
            // StepsLastFrame stops at MaxCatchUpSteps by construction, so it
            // reports 8 whether the debt is nine ticks or three hundred. The
            // number that actually grows is the accumulator.
            var clock = new WaveClock();
            var runner = Runner();

            clock.Advance(runner, Tick30 * 15.5, null);

            Assert.AreEqual(WaveClock.MaxCatchUpSteps, clock.StepsLastFrame);
            Assert.AreEqual(7.5, clock.BacklogTicks, 1e-6);
            Assert.AreEqual(1.0, clock.Alpha, 1e-9, "Alpha must never exceed one tick");
        }

        [Test]
        public void TerminationStopsTheClockWithoutBurningFrames()
        {
            var clock = new WaveClock();
            var runner = Runner();

            for (int i = 0; i < 500 && !clock.Terminated; i++)
                clock.Advance(runner, Tick30 * 6.5, null);

            Assert.IsTrue(clock.Terminated);
            Assert.IsTrue(runner.Done);
            Assert.AreEqual(Result.Loss, runner.Outcome.Result);

            clock.Advance(runner, Tick30 * 6.5, null);
            Assert.AreEqual(0, clock.StepsLastFrame);
        }
    }
}
