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
