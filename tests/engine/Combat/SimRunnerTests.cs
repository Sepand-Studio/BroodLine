using Xunit;
using Broodline.Sim.Combat;

namespace Broodline.Sim.Tests.Combat
{
    public class SimRunnerTests
    {
        [Fact]
        public void SteppedToCompletion_ReproducesGoldenA()
        {
            // Asserted against the PINNED LITERAL, not against Sim.Run - once
            // Run delegates to SimRunner, comparing the two proves nothing.
            var r = new SimRunner(WaveDef.Wave6(), Lane.Defile(),
                                  GoldenTests.DeploymentWithoutChill(), GoldenTests.Seed);
            while (r.Step()) { }

            Assert.Equal(4169973534116968225UL, r.Outcome.Hash);
            Assert.Equal(Result.Loss, r.Outcome.Result);
            Assert.Equal(1, r.Outcome.BreachCount);
            Assert.False(r.Outcome.Breaches[0].Access);
        }

        [Fact]
        public void SteppedToCompletion_ReproducesGoldenB()
        {
            var r = new SimRunner(WaveDef.Wave6(), Lane.Defile(),
                                  GoldenTests.DeploymentWithChill(), GoldenTests.Seed);
            while (r.Step()) { }

            Assert.Equal(434502781243215433UL, r.Outcome.Hash);
            Assert.Equal(Result.Win, r.Outcome.Result);
            Assert.Equal(2, r.Outcome.IntegrityRemaining);
        }

        [Fact]
        public void TrueReturnsEqualOutcomeTicks()
        {
            // The loop increments Tick only on a step that does NOT terminate,
            // so the count of true returns is exactly Outcome.Ticks. This is
            // the invariant that catches an off-by-one in the extraction - the
            // single likeliest way to get this wrong.
            var r = new SimRunner(WaveDef.Wave6(), Lane.Defile(),
                                  GoldenTests.DeploymentWithoutChill(), GoldenTests.Seed);
            int steps = 0;
            while (r.Step()) steps++;

            Assert.Equal(r.Outcome.Ticks, steps);
        }

        [Fact]
        public void StepAfterTermination_IsFalseAndHarmless()
        {
            var r = new SimRunner(WaveDef.Wave6(), Lane.Defile(),
                                  GoldenTests.DeploymentWithoutChill(), GoldenTests.Seed);
            while (r.Step()) { }
            ulong hash = r.Outcome.Hash;

            Assert.False(r.Step());
            Assert.False(r.Step());
            Assert.Equal(hash, r.Outcome.Hash);
        }

        [Fact]
        public void TickAdvancesOneAtATime()
        {
            var r = new SimRunner(WaveDef.Wave6(), Lane.Defile(),
                                  GoldenTests.DeploymentWithoutChill(), GoldenTests.Seed);
            Assert.Equal(0, r.Tick);
            r.Step();
            Assert.Equal(1, r.Tick);
            r.Step();
            Assert.Equal(2, r.Tick);
        }
    }
}
