using System.Collections.Generic;
using Xunit;

namespace Broodline.Sim.Tests
{
    public class ToySimTests
    {
        [Fact]
        public void SameSeedAndInputs_ProduceTheSameHash()
        {
            var inputs = new[] { new ToyInput { Tick = 5, EntityId = 1 } };
            Assert.Equal(ToySim.Run(7, 4, inputs, 90), ToySim.Run(7, 4, inputs, 90));
        }

        [Fact]
        public void DifferentSeed_ProducesADifferentHash()
        {
            var inputs = new ToyInput[0];
            Assert.NotEqual(ToySim.Run(1, 4, inputs, 90), ToySim.Run(2, 4, inputs, 90));
        }

        [Fact]
        public void InputChangesTheOutcome()
        {
            var none = new ToyInput[0];
            var one = new[] { new ToyInput { Tick = 3, EntityId = 0 } };
            Assert.NotEqual(ToySim.Run(9, 4, none, 60), ToySim.Run(9, 4, one, 60));
        }

        [Fact]
        public void CheckpointsAreStableAcrossRuns()
        {
            var inputs = new[] { new ToyInput { Tick = 10, EntityId = 2 } };
            var a = new List<ulong>();
            var b = new List<ulong>();
            ToySim.RunToTick(3, 5, inputs, 128, 64, a);
            ToySim.RunToTick(3, 5, inputs, 128, 64, b);
            Assert.Equal(a, b);
            Assert.NotEmpty(a);

            // Stability alone is a weak claim: RunToTick appending a constant
            // every checkpointEvery ticks would satisfy every assertion above.
            // 128 ticks at one checkpoint per 64 gives exactly two, taken 64
            // ticks apart in a simulation that mutates every entity every tick,
            // so they are known to be distinct — which is what makes "these two
            // runs agree" evidence that the hash is tracking the run.
            Assert.Equal(2, a.Count);
            Assert.NotEqual(a[0], a[1]);

            // Distinctness alone doesn't pin the VALUE: RunToTick appending
            // (ulong)tick every checkpointEvery ticks would also produce two
            // distinct checkpoints. The tick-127 checkpoint is folded in and
            // appended last, and nothing after that mutates the hash before
            // RunToTick returns hash.Value, so the final checkpoint must equal
            // this same run's own result.
            Assert.Equal(ToySim.Run(3, 5, inputs, 128), a[1]);
        }

        [Fact]
        public void GoldenHash_IsPinned()
        {
            // A change here is a deliberate simulation change and invalidates every
            // stored replay. If this fails unexpectedly, something drifted.
            var inputs = new[] { new ToyInput { Tick = 5, EntityId = 1 } };
            ulong actual = ToySim.Run(7, 4, inputs, 90);
            Assert.Equal(GoldenValue, actual);
        }

        // Observed on first implementation. Changing the simulation changes this
        // value; that is the point. An unexplained change is drift.
        const ulong GoldenValue = 17571883179532809288UL;
    }
}
