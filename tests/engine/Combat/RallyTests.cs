using Xunit;
using Broodline.Sim.Combat;

namespace Broodline.Sim.Tests.Combat
{
    public class RallyTests
    {
        private static SimRunner Fresh() =>
            new SimRunner(WaveDef.Wave6(), Lane.Defile(),
                          GoldenTests.DeploymentWithoutChill(), GoldenTests.Seed);

        [Fact]
        public void RallyHalvesTheIntervalAfterInstinctModifiers()
        {
            // The reason this is a rule and not a style: integer division does
            // not commute. Vetch is 45 ticks and is the STARTER species.
            //   Rally last : 45 * 5 / 4 = 56, then / 2 = 28
            //   Rally first: 45 / 2     = 22, then * 5 / 4 = 27
            // combat_numbers section 3 gives Vetch 1.5s = 45 ticks exactly, so
            // this is live for the most common creature in the game.
            var deployment = GoldenTests.DeploymentWithoutChill();
            deployment[0] = new CreatureSpec
            {
                Species = Species.Vetch, Pocket = 0, Instinct = Instinct.Overwatch
            };

            var plain = new SimState(WaveDef.Wave6(), Lane.Defile(), deployment);
            Assert.Equal(56, Attacks.IntervalTicks(plain, 0));      // Overwatch only

            plain.CreatureRallyUntil[0] = 1;                        // tick 0 < 1, so rallied
            Assert.Equal(28, Attacks.IntervalTicks(plain, 0));      // NOT 27
        }

        [Fact]
        public void RallyHalvesTheRemainingCooldown()
        {
            // NextAttackAt is an absolute tick already computed against the
            // un-halved interval. Left alone, Rally on a Hollow - 75 ticks -
            // does visibly nothing for up to 2.5s of its 4s window.
            var deployment = GoldenTests.DeploymentWithoutChill();
            deployment[0] = new CreatureSpec
            {
                Species = Species.Hollow, Pocket = 0, Instinct = Instinct.Vanguard
            };
            var r = new SimRunner(WaveDef.Wave6(), Lane.Defile(), deployment, GoldenTests.Seed);

            // Step until the Hollow has actually fired once, so there IS a
            // cooldown to halve. Stepping to a fixed tick is not safe: a
            // creature whose NextAttackAt is still 0 has a negative "remaining"
            // and the assertion below would be checking nothing.
            while (r.Step() && r.CreatureNextAttackAt[0] <= r.Tick) { }
            Assert.True(r.CreatureNextAttackAt[0] > r.Tick,
                "the Hollow never attacked, so there is no cooldown to halve");

            int remaining = r.CreatureNextAttackAt[0] - r.Tick;
            Assert.True(r.TryRally(0));
            Assert.Equal(r.Tick + remaining / 2, r.CreatureNextAttackAt[0]);
        }

        [Fact]
        public void RallyLastsExactlyOneHundredAndTwentyTicks()
        {
            var r = Fresh();
            r.Step();                                     // Tick is now 1
            Assert.True(r.TryRally(0));
            Assert.Equal(r.Tick + 120, r.CreatureRallyUntil[0]);

            // Active on the tick it was granted and on the 120th, gone after.
            var plain = new SimState(WaveDef.Wave6(), Lane.Defile(),
                                     GoldenTests.DeploymentWithoutChill());
            plain.CreatureRallyUntil[0] = 121;
            plain.Tick = 120;
            Assert.Equal(22, Attacks.IntervalTicks(plain, 0));   // Vetch 45 -> 22, rallied
            plain.Tick = 121;
            Assert.Equal(45, Attacks.IntervalTicks(plain, 0));   // expired
        }

        [Fact]
        public void RallyIsOneUsePerWave()
        {
            var r = Fresh();
            Assert.True(r.TryRally(0));
            Assert.True(r.RallyUsed);
            Assert.False(r.TryRally(1));
            Assert.False(r.TryRally(0));
        }

        [Fact]
        public void InvalidRallyIsANoOpAndNotAnError()
        {
            // The replay must record only what the simulation CONSUMED. An
            // input that was rejected but still written is exactly the shape of
            // a replay that does not reproduce.
            var r = Fresh();
            Assert.False(r.TryRally(-1));
            Assert.False(r.TryRally(99));
            Assert.False(r.RallyUsed);

            // A terminated run cannot be rallied, and the attempt is not spent.
            var r2 = Fresh();
            while (r2.Step()) { }
            Assert.False(r2.TryRally(0));
        }

        [Fact]
        public void RallyChangesTheOutcomeHash()
        {
            // If it did not, Rally would be invisible to the determinism gate
            // and to server verification - an input the server could not check.
            var a = Fresh();
            while (a.Step()) { }

            var b = Fresh();
            b.Step();
            b.TryRally(0);
            while (b.Step()) { }

            Assert.NotEqual(a.Outcome.Hash, b.Outcome.Hash);
        }
    }
}
