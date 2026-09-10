using Xunit;
using Broodline.Sim.Combat;

namespace Broodline.Sim.Tests.Combat
{
    public class CapacityTests
    {
        private static CreatureSpec[] WithChill(int tier, int copies)
        {
            var d = SimStateTests.FiveWithoutChill();
            for (int i = 0; i < copies; i++)
                d[i] = new CreatureSpec
                {
                    Species = Species.Pale, Pocket = i,
                    Instinct = Instinct.Vanguard,
                    Trait1 = Trait.Chill, Tier1 = tier
                };
            return d;
        }

        [Fact]
        public void Capacity_SumsAcrossCarriers()
        {
            // combat_engine 5.3: "Capacity from multiple creatures carrying the
            // same trait sums." Two Chill II give 4, not 2.
            var s = new SimState(WaveDef.Wave6(), Lane.Defile(), WithChill(tier: 2, copies: 2));
            Assert.Equal(4, Capacity.TotalChillCapacity(s));
        }

        [Fact]
        public void Capacity_IsZeroWhenNoCarrierIsAlive()
        {
            var s = new SimState(WaveDef.Wave6(), Lane.Defile(), WithChill(tier: 3, copies: 1));
            Assert.Equal(4, Capacity.TotalChillCapacity(s));

            s.CreatureHp[0] = 0;   // the carrier dies

            // 5.3: "its capacity frees immediately, because there is nothing to
            // free." Recomputed from the live set, never accumulated.
            Assert.Equal(0, Capacity.TotalChillCapacity(s));
        }

        [Fact]
        public void Compare_TieBreaksOnSpawnIndexAscending()
        {
            var s = new SimState(
                new WaveDef(1, 5, 1, new[]
                {
                    new SpawnEntry { Tick = 0, Type = RaiderType.Courser },
                    new SpawnEntry { Tick = 0, Type = RaiderType.Courser }
                }),
                Lane.Defile(), SimStateTests.FiveWithoutChill());

            s.RaiderCount = 2;
            s.RaiderAlive[0] = s.RaiderAlive[1] = true;
            // Identical progress: distance ties exactly, which 2.2 says is
            // common rather than rare.
            s.RaiderProgress[0] = s.RaiderProgress[1] = Fix64.FromInt(10);

            Assert.True(Capacity.Compare(s, 0, 1, pocket: 0) < 0);
            Assert.True(Capacity.Compare(s, 1, 0, pocket: 0) > 0);
            Assert.Equal(0, Capacity.Compare(s, 0, 0, pocket: 0));
        }

        [Fact]
        public void AssignChill_SlowsExactlyCapacityManyNearestFirst()
        {
            var wave = new WaveDef(1, 9, 1, new[]
            {
                new SpawnEntry { Tick = 0, Type = RaiderType.Courser },
                new SpawnEntry { Tick = 0, Type = RaiderType.Courser },
                new SpawnEntry { Tick = 0, Type = RaiderType.Courser }
            });
            var s = new SimState(wave, Lane.Defile(), WithChill(tier: 1, copies: 1));
            s.RaiderCount = 3;
            for (int r = 0; r < 3; r++) s.RaiderAlive[r] = true;

            // Raider 2 is nearest the carrier's pocket (tile 6).
            s.RaiderProgress[0] = Fix64.FromInt(20);
            s.RaiderProgress[1] = Fix64.FromInt(15);
            s.RaiderProgress[2] = Fix64.FromInt(6);

            Capacity.AssignChill(s, new int[3]);

            // Chill I is capacity 1 - one raider, the nearest.
            Assert.False(s.RaiderChilled[0]);
            Assert.False(s.RaiderChilled[1]);
            Assert.True(s.RaiderChilled[2]);
        }
    }
}
