using Xunit;

namespace Broodline.Sim.Tests
{
    public class RngTests
    {
        [Fact]
        public void SameSeed_ProducesSameSequence()
        {
            var a = new Rng(0x9E3779B97F4A7C15UL);
            var b = new Rng(0x9E3779B97F4A7C15UL);
            for (int i = 0; i < 1000; i++)
                Assert.Equal(a.NextULong(), b.NextULong());
        }

        [Fact]
        public void DifferentSeeds_Diverge()
        {
            var a = new Rng(1);
            var b = new Rng(2);
            bool differed = false;
            for (int i = 0; i < 100 && !differed; i++)
                differed = a.NextULong() != b.NextULong();
            Assert.True(differed, "two seeds produced identical output for 100 draws");
        }

        [Fact]
        public void NextInt_StaysInRange()
        {
            var r = new Rng(42);
            for (int i = 0; i < 10000; i++)
            {
                int v = r.NextInt(7);
                Assert.InRange(v, 0, 6);
            }
        }

        [Fact]
        public void NextInt_NonPositiveBound_ReturnsZeroWithoutConsumingADraw()
        {
            // exclusiveMax <= 0: the range is empty, so NextInt must return 0
            // *and* must not advance the stream. Proven here by observing the
            // stream position, not just the return value: if a draw had been
            // consumed, r's next NextULong() would diverge from a fresh Rng's
            // first draw.
            var r = new Rng(42);
            Assert.Equal(0, r.NextInt(0));
            Assert.Equal(0, r.NextInt(-100));

            var fresh = new Rng(42);
            Assert.Equal(fresh.NextULong(), r.NextULong());
        }

        [Fact]
        public void NextInt_One_ReturnsZeroAndConsumesExactlyOneDraw()
        {
            // exclusiveMax == 1 is the one-element case, not the degenerate
            // one: it must return 0 (the only value in range) but -- unlike
            // exclusiveMax <= 0 -- it DOES consume exactly one draw. Task 7's
            // Corpus.RunScenario calls NextInt(entityCount) with counts
            // starting at 1, so every corpus stream's alignment depends on
            // this consuming exactly one draw, never zero.
            //
            // Proven by comparing against a fresh Rng with one draw skipped:
            // if NextInt(1) consumed zero draws, r's next draw would match
            // fresh's *first* draw instead; if it consumed two, it would match
            // fresh's *third*.
            var r = new Rng(42);
            Assert.Equal(0, r.NextInt(1));

            var fresh = new Rng(42);
            fresh.NextULong(); // skip the one draw NextInt(1) must have consumed
            Assert.Equal(fresh.NextULong(), r.NextULong());
        }

        [Fact]
        public void KnownSeed_ProducesKnownFirstDraw()
        {
            // Pins the algorithm, not just "seed 1 is reproducible" -- the literal
            // below is xorshift128+'s actual first draw for seed 1 under this
            // type's seed-splitting scheme (two chained SplitMix64 applications;
            // see the constructor's doc comment on Rng), taken verbatim from a
            // real run, never computed by hand. If this changes -- the shift
            // triple, the operation order, or the seed-splitting scheme -- every
            // stored replay is invalid and sim_version must change with it.
            var r = new Rng(1);
            ulong first = r.NextULong();
            Assert.Equal(10993463216891074725UL, first);
            var again = new Rng(1);
            Assert.Equal(first, again.NextULong());
        }
    }
}
