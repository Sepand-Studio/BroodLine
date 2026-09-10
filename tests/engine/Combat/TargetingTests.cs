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

            // Pocket 0 sits at tile 6. Raider 2 sits BEHIND the pocket (tile
            // 2, not yet reached it) rather than beside it, so "greatest
            // progress along the lane" (0>1>2: 11>8>2) and "greatest distance
            // from the pocket" (0>2>1: DistSq 26>17>5) rank raiders 1 and 2 in
            // opposite order - which is what makes Vanguard and Overwatch
            // distinguishable here instead of two names for the same rule.
            s.RaiderProgress[0] = Fix64.FromInt(11);
            s.RaiderProgress[1] = Fix64.FromInt(8);
            s.RaiderProgress[2] = Fix64.FromInt(2);
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
        public void Vanguard_TieBreaksOnSpawnIndexAscending()
        {
            // Only the nearest-predicate path (LastStand/Skittish/PackSense)
            // had a tie test; Bloodscent, Vanguard and Overwatch did not. This
            // covers Vanguard directly, and - because a tie on progress does
            // NOT imply a tie on distance from the pocket once a raider sits
            // behind it - also catches Vanguard and Overwatch's Prefers case
            // bodies being swapped: under the swap, raider 2's greater
            // distance from the pocket would beat the tied pair below.
            var s = ThreeRaiders(Instinct.Vanguard, Species.Hollow, pocket: 0);
            s.RaiderProgress[0] = Fix64.FromInt(8);   // tied with raider 1
            s.RaiderProgress[1] = Fix64.FromInt(8);
            s.RaiderProgress[2] = Fix64.FromInt(2);   // out of the way

            Assert.Equal(0, Targeting.Select(s, 0));   // lower spawn index wins
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
            // ThreeRaiders' default puts raider 2 behind the pocket at tile 2
            // (out of this narrow range), so it is moved back to tile 6 here
            // to keep this test's own premise - a single reachable raider.
            var s = ThreeRaiders(Instinct.Vanguard, Species.Vetch, pocket: 0);
            s.RaiderProgress[2] = Fix64.FromInt(6);
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
