using Xunit;
using Broodline.Sim.Combat;

namespace Broodline.Sim.Tests.Combat
{
    public class SimStateTests
    {
        public static CreatureSpec[] FiveWithoutChill() => new[]
        {
            new CreatureSpec { Species = Species.Vetch,   Pocket = 0, Instinct = Instinct.Vanguard },
            new CreatureSpec { Species = Species.Hollow,  Pocket = 1, Instinct = Instinct.Overwatch },
            new CreatureSpec { Species = Species.Skitter, Pocket = 2, Instinct = Instinct.Bloodscent },
            new CreatureSpec { Species = Species.Loam,    Pocket = 3, Instinct = Instinct.LastStand },
            new CreatureSpec { Species = Species.Ember,   Pocket = 4, Instinct = Instinct.PackSense }
        };

        [Fact]
        public void Construction_SizesForEverySpawnAndSeedsIntegrity()
        {
            var wave = WaveDef.Wave6();
            var state = new SimState(wave, Lane.Defile(), FiveWithoutChill());

            Assert.Equal(2, state.Integrity);
            Assert.Equal(0, state.Tick);
            Assert.Equal(0, state.RaiderCount);          // none spawned yet
            Assert.Equal(1, state.RaiderHp.Length);      // but capacity for one
            Assert.Equal(5, state.CreatureCount);
        }

        [Fact]
        public void Creatures_TakeTheirStatsFromTheSpeciesTable()
        {
            var state = new SimState(WaveDef.Wave6(), Lane.Defile(), FiveWithoutChill());
            Assert.Equal(260, state.CreatureHp[0]);   // Vetch
            Assert.Equal(60,  state.CreatureHp[1]);   // Hollow
        }

        [Fact]
        public void CreatureCarries_FindsATraitInEitherSlot()
        {
            var deployment = FiveWithoutChill();
            deployment[4] = new CreatureSpec
            {
                Species = Species.Pale, Pocket = 4,
                Instinct = Instinct.Vanguard,
                Trait2 = Trait.Chill, Tier2 = 1
            };
            var state = new SimState(WaveDef.Wave6(), Lane.Defile(), deployment);

            Assert.True(state.CreatureCarries(4, Trait.Chill, out int tier));
            Assert.Equal(1, tier);
            Assert.False(state.CreatureCarries(0, Trait.Chill, out _));
        }
    }
}
