using Xunit;
using Broodline.Sim.Combat;

namespace Broodline.Sim.Tests.Combat
{
    public class TerminationTests
    {
        [Fact]
        public void Breach_DeductsIntegrityByRaiderTypeAndRemovesTheRaider()
        {
            var s = new SimState(WaveDef.Wave6(), Lane.Defile(),
                                 SimStateTests.FiveWithoutChill());
            s.Tick = 90;
            Phases.Spawn(s);
            s.RaiderProgress[0] = Fix64.FromInt(Stats.LaneTiles);   // at the Ark

            var log = new Breach[1];
            int count = 0;
            Phases.Breach(s, log, ref count);

            Assert.Equal(0, s.Integrity);          // 2 - Courser's 2
            Assert.False(s.RaiderAlive[0]);
            Assert.Equal(1, count);
            Assert.Equal(RaiderType.Courser, log[0].Type);
        }

        [Fact]
        public void Resolve_LosesTheTickIntegrityReachesZero()
        {
            var s = new SimState(WaveDef.Wave6(), Lane.Defile(),
                                 SimStateTests.FiveWithoutChill());
            s.Integrity = 0;
            Assert.Equal(Result.Loss, Phases.Resolve(s));
        }

        [Fact]
        public void Resolve_WinsWhenNothingRemainsAndNothingIsPending()
        {
            var s = new SimState(WaveDef.Wave6(), Lane.Defile(),
                                 SimStateTests.FiveWithoutChill());
            s.Tick = 90;
            Phases.Spawn(s);
            Assert.Equal(Result.Running, Phases.Resolve(s));

            s.RaiderAlive[0] = false;
            Assert.Equal(Result.Win, Phases.Resolve(s));
        }

        [Fact]
        public void Resolve_IsStillRunningWhileASpawnIsPending()
        {
            var s = new SimState(WaveDef.Wave6(), Lane.Defile(),
                                 SimStateTests.FiveWithoutChill());
            // Nothing spawned yet, but the table is not exhausted.
            Assert.Equal(Result.Running, Phases.Resolve(s));
        }

        [Fact]
        public void Resolve_PrefersLossWhenTheWaveEndsAndIntegrityEmptiesTogether()
        {
            // The race the loss-before-win ordering exists for, and the only
            // state in which the ordering is observable: the spawn table is
            // exhausted, no raider is left alive, AND integrity has just reached
            // zero. Both branches are live, so only their ORDER decides the
            // answer. Every other test in this file leaves one branch
            // structurally unreachable, so a refactor that moved the integrity
            // check below the win checks would pass all of them and fail only
            // this one.
            var s = new SimState(WaveDef.Wave6(), Lane.Defile(),
                                 SimStateTests.FiveWithoutChill());
            s.Tick = 90;
            Phases.Spawn(s);                       // spawn table now exhausted
            s.RaiderProgress[0] = Fix64.FromInt(Stats.LaneTiles);

            var log = new Breach[1];
            int count = 0;
            Phases.Breach(s, log, ref count);      // integrity 2 -> 0, raider removed

            Assert.Equal(0, s.Integrity);
            Assert.False(s.RaiderAlive[0]);
            Assert.Equal(s.Wave.Spawns.Length, s.RaiderCount);   // nothing pending

            Assert.Equal(Result.Loss, Phases.Resolve(s));
        }
    }
}
