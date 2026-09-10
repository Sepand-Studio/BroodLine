using Xunit;
using Broodline.Sim.Combat;

namespace Broodline.Sim.Tests.Combat
{
    public class TargetingTests
    {
        /// Three Coursers abreast, so the predicates select different raiders.
        private static SimState ThreeRaiders(Instinct instinct, Species species, int pocket)
        {
            var wave = new WaveDef(1, 9, 1, new[]
            {
                new SpawnEntry { Tick = 0, Type = RaiderType.Courser },
                new SpawnEntry { Tick = 0, Type = RaiderType.Courser },
                new SpawnEntry { Tick = 0, Type = RaiderType.Courser }
            });
            var d = new[]
            {
                new CreatureSpec { Species = species, Pocket = pocket, Instinct = instinct }
            };
            var s = new SimState(wave, Lane.Defile(), d);
            s.Tick = 0;
            Phases.Spawn(s);

            // Pocket 0 sits at tile 6. Raider 0 furthest along, raider 2 nearest.
            s.RaiderProgress[0] = Fix64.FromInt(11);
            s.RaiderProgress[1] = Fix64.FromInt(8);
            s.RaiderProgress[2] = Fix64.FromInt(6);
            return s;
        }

        [Fact]
        public void Vanguard_TargetsTheRaiderClosestToTheArk()
        {
            var s = ThreeRaiders(Instinct.Vanguard, Species.Hollow, pocket: 0);
            Assert.Equal(0, Targeting.Select(s, 0));   // furthest along = closest to Ark
        }

        [Fact]
        public void Bloodscent_TargetsLowestCurrentHp()
        {
            var s = ThreeRaiders(Instinct.Bloodscent, Species.Hollow, pocket: 0);
            s.RaiderHp[1] = 40;
            Assert.Equal(1, Targeting.Select(s, 0));
        }

        [Fact]
        public void Overwatch_TargetsFurthestInRangeAndWidensRange()
        {
            var s = ThreeRaiders(Instinct.Overwatch, Species.Hollow, pocket: 0);

            // Range 7 becomes 8: +25%, truncated toward zero.
            Assert.Equal(8, Targeting.EffectiveRange(s, 0));

            // "Furthest in range" is by distance from the creature, so the
            // raider at tile 11 - five tiles away - wins over the one at 6.
            Assert.Equal(0, Targeting.Select(s, 0));
        }

        [Fact]
        public void NearestPredicates_TieBreakOnSpawnIndexAscending()
        {
            var s = ThreeRaiders(Instinct.LastStand, Species.Hollow, pocket: 0);
            // Raiders 1 and 2 equidistant from tile 6: tiles 8 and 4.
            s.RaiderProgress[1] = Fix64.FromInt(8);
            s.RaiderProgress[2] = Fix64.FromInt(4);
            s.RaiderProgress[0] = Fix64.FromInt(20);   // out of the way

            Assert.Equal(1, Targeting.Select(s, 0));   // lower spawn index wins
        }

        [Fact]
        public void Select_IgnoresDeadAndOutOfRangeRaiders()
        {
            // Vetch range 2 from pocket 0 (tile 6) reaches tiles 5-7 only.
            var s = ThreeRaiders(Instinct.Vanguard, Species.Vetch, pocket: 0);
            Assert.Equal(2, Targeting.Select(s, 0));   // only the tile-6 raider

            s.RaiderAlive[2] = false;
            Assert.Equal(-1, Targeting.Select(s, 0));
        }

        [Fact]
        public void Targeting_LockoutDelaysAcquisitionByTwelveTicks()
        {
            var s = ThreeRaiders(Instinct.Vanguard, Species.Hollow, pocket: 0);
            Phases.Targeting(s);
            Assert.Equal(0, s.CreatureTarget[0]);

            // The target dies. The creature drops it immediately but may not
            // acquire again until the lockout expires.
            s.RaiderAlive[0] = false;
            s.Tick = 100;
            Phases.Targeting(s);
            Assert.Equal(-1, s.CreatureTarget[0]);
            Assert.Equal(100 + Stats.RetargetLockoutTicks, s.CreatureAcquireAt[0]);

            s.Tick = 111;
            Phases.Targeting(s);
            Assert.Equal(-1, s.CreatureTarget[0]);   // still locked out

            s.Tick = 112;
            Phases.Targeting(s);
            Assert.Equal(1, s.CreatureTarget[0]);    // acquires
        }
    }
}
