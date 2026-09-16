using Xunit;
using Broodline.Sim.Combat;

namespace Broodline.Sim.Tests.Combat
{
    public class AttackTests
    {
        private static SimState OneRaider(Instinct instinct, Species species, int pocket)
        {
            var d = new[]
            {
                new CreatureSpec { Species = species, Pocket = pocket, Instinct = instinct }
            };
            var s = new SimState(WaveDef.Wave6(), Lane.Defile(), d);
            s.Tick = 90;
            Phases.Spawn(s);
            s.RaiderProgress[0] = Fix64.FromInt(6);   // beside pocket 0
            return s;
        }

        /// Phase 5's splash buffer. The engine's copy lives in SimRunner and is
        /// sized from the ladder; a test that reached phase 5 directly has to
        /// supply its own, and sizing it the same way means a retune of tier
        /// III cannot leave these one entry short.
        private static int[] SplashScratch() => new int[Stats.MaxSplashTargets];

        [Fact]
        public void Attack_DealsSpeciesDamageAndSetsTheNextInterval()
        {
            var s = OneRaider(Instinct.Vanguard, Species.Hollow, 0);
            Phases.Targeting(s);
            Phases.Attack(s, SplashScratch());

            Assert.Equal(220 - 55, s.RaiderHp[0]);                 // Hollow: 55
            Assert.Equal(90 + 75, s.CreatureNextAttackAt[0]);      // 2.5s = 75 ticks
        }

        [Fact]
        public void Attack_DoesNothingBeforeTheIntervalElapses()
        {
            var s = OneRaider(Instinct.Vanguard, Species.Hollow, 0);
            Phases.Targeting(s);
            Phases.Attack(s, SplashScratch());
            int hp = s.RaiderHp[0];

            s.Tick = 91;
            Phases.Attack(s, SplashScratch());
            Assert.Equal(hp, s.RaiderHp[0]);
        }

        [Fact]
        public void Overwatch_TradesTwentyPercentAttackSpeedForRange()
        {
            var s = OneRaider(Instinct.Overwatch, Species.Hollow, 0);
            // -20% attack speed: 75 ticks becomes 75 * 5 / 4.
            Assert.Equal(93, Attacks.IntervalTicks(s, 0));
        }

        [Fact]
        public void LastStand_SpeedsUpOnlyBelowQuarterHealth()
        {
            var s = OneRaider(Instinct.LastStand, Species.Hollow, 0);
            Assert.Equal(75, Attacks.IntervalTicks(s, 0));

            s.CreatureHp[0] = 14;                 // Hollow's 60 HP, under 25%
            // +50% attack speed: 75 * 2 / 3.
            Assert.Equal(50, Attacks.IntervalTicks(s, 0));
        }

        [Fact]
        public void PackSense_AddsFifteenPercentWithAnAdjacentSameSpeciesAlly()
        {
            var d = new[]
            {
                new CreatureSpec { Species = Species.Hollow, Pocket = 0, Instinct = Instinct.PackSense },
                new CreatureSpec { Species = Species.Hollow, Pocket = 1, Instinct = Instinct.Vanguard }
            };
            var s = new SimState(WaveDef.Wave6(), Lane.Defile(), d);

            // 55 * 115 / 100 = 63.
            Assert.Equal(63, Attacks.Damage(s, 0));

            // A different species in the adjacent pocket does not qualify.
            s.CreatureSpecies[1] = Species.Vetch;
            Assert.Equal(55, Attacks.Damage(s, 0));
        }

        [Fact]
        public void Death_ClearsTheRaiderAtZeroHp()
        {
            var s = OneRaider(Instinct.Vanguard, Species.Hollow, 0);
            s.RaiderHp[0] = 0;
            Phases.Death(s);
            Assert.False(s.RaiderAlive[0]);
        }

        [Fact]
        public void Skittish_RepositionsOnceBelowFortyPercentAndCannotActWhileMoving()
        {
            var s = OneRaider(Instinct.Skittish, Species.Hollow, 1);
            s.CreatureHp[0] = 20;                 // Hollow 60 HP, under 40%

            Phases.Skittish(s);
            Assert.False(s.CreatureCanAct(0));
            Assert.Equal(90 + 60, s.CreatureBusyUntil[0]);   // 2s at 30Hz

            // It repositions away from the threat, and only ever once.
            int moved = s.CreaturePocket[0];
            Assert.NotEqual(1, moved);

            s.Tick = 200;
            Phases.Skittish(s);
            Assert.Equal(moved, s.CreaturePocket[0]);
            Assert.True(s.CreatureCanAct(0));
        }
    }
}
