using System;
using Xunit;
using Broodline.Sim.Combat;

namespace Broodline.Sim.Tests.Combat
{
    public class WaveDefTests
    {
        [Fact]
        public void Wave6_MatchesTheAuthoredDefinition()
        {
            var w = WaveDef.Wave6();
            Assert.Equal(6, w.Id);
            Assert.Equal(2, w.Integrity);
            Assert.Equal(1, w.LaneCount);
            // Spawns is a ReadOnlySpan now - a span cannot be enumerated by
            // Assert.Single, and that is the trade: it also cannot be written
            // through, which is what stops a caller editing an authored wave
            // out from under the Validate that already passed on it.
            Assert.Equal(1, w.Spawns.Length);
            Assert.Equal(RaiderType.Courser, w.Spawns[0].Type);
            Assert.Equal(90, w.Spawns[0].Tick);   // t=3s at 30Hz
        }

        [Fact]
        public void Wave7_IsAuthoredAndValid()
        {
            var w = WaveDef.ForId(7);

            Assert.Equal(7, w.Id);
            Assert.Equal(3, w.Integrity);
            Assert.Equal(1, w.LaneCount);

            // 1 Lash + 6 Skirmishers = 7 spawns. waves_01_12 says "6 Skirmishers",
            // NOT the raider roster's default arrival of eight - the wave table is
            // the authority on composition and the roster on the pattern.
            Assert.Equal(7, w.Spawns.Length);
            Assert.Equal(2, w.DistinctTypeCount());

            // Lash at t=4, Skirmishers from t=6 at 1.5s. TicksPerSecond is 30, so
            // 1.5s is 45 ticks. Spelled as arithmetic rather than as literals so a
            // tick-rate change moves them instead of silently desynchronising.
            Assert.Equal(4 * Stats.TicksPerSecond, w.Spawns[0].Tick);
            Assert.Equal(RaiderType.Lash, w.Spawns[0].Type);
            for (int i = 0; i < 6; i++)
            {
                Assert.Equal(6 * Stats.TicksPerSecond + i * 45, w.Spawns[i + 1].Tick);
                Assert.Equal(RaiderType.Skirmisher, w.Spawns[i + 1].Type);
            }

            // The composition invariants must hold on authored content, not just
            // be available to call. Lash answers Taunt, Skirmisher answers Splash -
            // distinct, so AssertNoSharedCounter passes.
            w.Validate();
        }

        [Fact]
        public void ForId_StillThrowsForAnUnauthoredWave()
        {
            // The throw is the point: a replay naming a wave this engine does not
            // have must fail at load rather than re-simulate something else.
            Assert.Throws<WaveCompositionException>(() => WaveDef.ForId(8));
        }

        [Fact]
        public void Validate_AllowsTwoSpawnsOfTheSameRaiderType()
        {
            var w = new WaveDef(
                id: 999, integrity: 3, laneCount: 1,
                spawns: new[]
                {
                    new SpawnEntry { Tick = 0,  Type = RaiderType.Courser },
                    new SpawnEntry { Tick = 30, Type = RaiderType.Courser }
                });

            // Two spawns of the SAME type is legal - that is volume, not a
            // shared counter. This must NOT throw.
            w.Validate();
        }

        [Fact]
        public void Validate_ThrowsWhenMoreThanFourRaiderTypes()
        {
            // With one raider type in the slice this cannot be constructed from
            // real data, so the invariant is exercised through the type-count
            // helper directly.
            Assert.Throws<WaveCompositionException>(
                () => WaveDef.AssertTypeCount(5));
        }

        [Fact]
        public void Stats_ChillCapacityIsPerTraitNotAGlobalLadder()
        {
            // combat_numbers section 134: I=1, II=2, III=4.
            // NOT combat_engine section 5.3's superseded 1/3/5.
            Assert.Equal(1, Stats.ChillCapacity(1));
            Assert.Equal(2, Stats.ChillCapacity(2));
            Assert.Equal(4, Stats.ChillCapacity(3));
        }
    }
}
