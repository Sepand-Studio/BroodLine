using Xunit;
using Broodline.Sim.Combat;

namespace Broodline.Sim.Tests.Combat
{
    /// The traits and the raider mechanic this slice gave a body to: Lash's
    /// attack, Taunt forcing it, and Carapace blunting whatever lands.
    ///
    /// Splash is declared and answers Skirmisher but has no effect yet -
    /// Counters.cs still lists it as deferred - so the only thing asserted
    /// about it here is that CounterFor names it. A test that pretended
    /// otherwise would be the more expensive kind of wrong.
    public class TraitEffectTests
    {
        /// One Defile lane, some Lashes standing on tile 6.
        ///
        /// Tile 6 is where pocket 0 sits, and pocket 1 sits at tile 10. A Lash
        /// there is DistSq 1 from pocket 0 and 17 from pocket 1 - both inside
        /// its range of 5, since InRange compares against 25 - so the two
        /// pockets are a NEAR body and a FAR body and "furthest defender in
        /// range" has something to choose between. Every test below is built on
        /// that one arrangement.
        private static SimState LaneWith(CreatureSpec[] deployment, int lashes, int tile = 6)
        {
            var spawns = new SpawnEntry[lashes];
            for (int i = 0; i < lashes; i++)
                spawns[i] = new SpawnEntry { Tick = 0, Type = RaiderType.Lash };

            var s = new SimState(
                new WaveDef(id: 999, integrity: 9, laneCount: 1, spawns: spawns),
                Lane.Defile(), deployment);

            s.Tick = 0;
            Phases.Spawn(s);
            for (int i = 0; i < lashes; i++) s.RaiderProgress[i] = Fix64.FromInt(tile);
            return s;
        }

        private static CreatureSpec Spec(Species species, int pocket,
                                         Trait trait = Trait.None, int tier = 0) =>
            new CreatureSpec
            {
                Species = species,
                Pocket = pocket,
                Instinct = Instinct.Vanguard,
                Trait1 = trait,
                Tier1 = tier
            };

        // ------------------------------------------------------------------
        // The stat lines, which are transcription and nothing else
        // ------------------------------------------------------------------

        [Fact]
        public void TheThreeRaiderProfilesMatchCombatNumbersSectionSix()
        {
            Assert.Equal(220, Stats.RaiderHp(RaiderType.Courser));
            Assert.Equal(180, Stats.RaiderHp(RaiderType.Lash));
            Assert.Equal(40, Stats.RaiderHp(RaiderType.Skirmisher));

            Assert.Equal(2, Stats.RaiderIntegrityCost(RaiderType.Courser));
            Assert.Equal(2, Stats.RaiderIntegrityCost(RaiderType.Lash));
            Assert.Equal(1, Stats.RaiderIntegrityCost(RaiderType.Skirmisher));

            // Thousandths of a tile per second: 1.6, 0.5 and 0.9.
            Assert.Equal(1600, Stats.RaiderMilliTilesPerSec(RaiderType.Courser));
            Assert.Equal(500, Stats.RaiderMilliTilesPerSec(RaiderType.Lash));
            Assert.Equal(900, Stats.RaiderMilliTilesPerSec(RaiderType.Skirmisher));
        }

        [Fact]
        public void CounterForIsTotalOverTheThreeRaiders()
        {
            // combat_numbers 4.2. Total is the property WaveDef's shared-counter
            // invariant needs: it compares CounterFor(t) against CounterFor(u)
            // for every pair, and two raiders both falling through to
            // Trait.None would read as sharing an answer and reject a legal
            // wave.
            Assert.Equal(Trait.Chill, Stats.CounterFor(RaiderType.Courser));
            Assert.Equal(Trait.Taunt, Stats.CounterFor(RaiderType.Lash));
            Assert.Equal(Trait.Splash, Stats.CounterFor(RaiderType.Skirmisher));
        }

        [Fact]
        public void TauntLaddersLikeChillBecauseBothScaleOnSimultaneity()
        {
            // combat_numbers 4.1 puts both on the simultaneity axis and 4.2
            // gives them the same 1/2/4 rungs. Asserted side by side because
            // the shared ladder is the claim, not a coincidence of two tables.
            Assert.Equal(1, Stats.TauntCapacity(1));
            Assert.Equal(2, Stats.TauntCapacity(2));
            Assert.Equal(4, Stats.TauntCapacity(3));

            Assert.Equal(Stats.ChillCapacity(1), Stats.TauntCapacity(1));
            Assert.Equal(Stats.ChillCapacity(2), Stats.TauntCapacity(2));
            Assert.Equal(Stats.ChillCapacity(3), Stats.TauntCapacity(3));
        }

        // ------------------------------------------------------------------
        // Lash - the only raider in section 6 that attacks
        // ------------------------------------------------------------------

        [Fact]
        public void Lash_HitsTheFurthestDefenderForThirtyEveryTwoSeconds()
        {
            // "Attacks the furthest defender in range 5 for 30 every 2s,
            // reaching past the front line into support pockets." Two Vetch so
            // neither dies and the interval can be watched across three ticks.
            var s = LaneWith(new[]
            {
                Spec(Species.Vetch, pocket: 0),
                Spec(Species.Vetch, pocket: 1)
            }, lashes: 1);

            Phases.Targeting(s);
            Phases.Attack(s);

            Assert.Equal(1, s.RaiderTargetCreature[0]);      // the far pocket
            Assert.Equal(260 - 30, s.CreatureHp[1]);
            Assert.Equal(260, s.CreatureHp[0]);              // the near body is skipped
            Assert.Equal(2 * Stats.TicksPerSecond, s.RaiderNextAttackAt[0]);

            // Not again until the interval elapses.
            s.Tick = 59;
            Phases.Targeting(s);
            Phases.Attack(s);
            Assert.Equal(260 - 30, s.CreatureHp[1]);

            s.Tick = 60;
            Phases.Targeting(s);
            Phases.Attack(s);
            Assert.Equal(260 - 60, s.CreatureHp[1]);
        }

        [Fact]
        public void Lash_TieBreaksTheFurthestDefenderOnCreatureIndexAscending()
        {
            // Pockets 0 and 1 sit at tiles 6 and 10, so a Lash on tile 8 is
            // DistSq 5 from BOTH. Lane 2.2 says outright that exact integer
            // distances make ties common rather than rare, and a comparator
            // that stopped at "furthest" would not be a total order - two
            // runtimes could rank the tied pair differently, which is the class
            // of bug that only shows up under IL2CPP.
            //
            // Run twice with the deployment order reversed. Both must answer 0,
            // which is what distinguishes "lowest creature index" from "the
            // lower pocket" or "whichever was written first".
            var byPocket = LaneWith(new[]
            {
                Spec(Species.Vetch, pocket: 0),
                Spec(Species.Vetch, pocket: 1)
            }, lashes: 1, tile: 8);

            Phases.Targeting(byPocket);
            Assert.Equal(0, byPocket.RaiderTargetCreature[0]);

            var reversed = LaneWith(new[]
            {
                Spec(Species.Vetch, pocket: 1),
                Spec(Species.Vetch, pocket: 0)
            }, lashes: 1, tile: 8);

            Phases.Targeting(reversed);
            Assert.Equal(0, reversed.RaiderTargetCreature[0]);
        }

        [Fact]
        public void Skirmisher_WalksAndCostsIntegrityAndNeverSwings()
        {
            // combat_numbers section 6 says "No attack" in as many words, so
            // Skirmisher adds no code to Attacks.cs - it is a body that walks.
            // Asserted at both ends: the stat table returns zero, AND the
            // raider selects no defender, so phase 5's interval guard is not
            // the only thing standing between it and a swing.
            Assert.Equal(0, Stats.RaiderDamage(RaiderType.Skirmisher));
            Assert.Equal(0, Stats.RaiderIntervalTicks(RaiderType.Skirmisher));
            Assert.Equal(0, Stats.RaiderRange(RaiderType.Skirmisher));

            // The Courser has no attack either - it is a rule about time.
            Assert.Equal(0, Stats.RaiderDamage(RaiderType.Courser));

            var s = new SimState(
                new WaveDef(id: 999, integrity: 9, laneCount: 1, spawns: new[]
                {
                    new SpawnEntry { Tick = 0, Type = RaiderType.Skirmisher }
                }),
                Lane.Defile(),
                new[] { Spec(Species.Hollow, pocket: 0) });

            s.Tick = 0;
            Phases.Spawn(s);
            s.RaiderProgress[0] = Fix64.FromInt(6);

            Phases.Targeting(s);
            Phases.Attack(s);

            Assert.Equal(-1, s.RaiderTargetCreature[0]);
            Assert.Equal(60, s.CreatureHp[0]);              // Hollow, untouched
        }

        // ------------------------------------------------------------------
        // Taunt
        // ------------------------------------------------------------------

        [Fact]
        public void Taunt_ForcesLashToTargetTheCarrier()
        {
            // combat_numbers 4.2: Taunt I forces 1 Lash to target this
            // creature. The assertion is on WHO the Lash attacks, not on
            // damage - "no counter has a damage component" is the rule that
            // keeps counters off the power ladder.
            var s = LaneWith(new[]
            {
                Spec(Species.Vetch, pocket: 0, trait: Trait.Taunt, tier: 1),
                Spec(Species.Hollow, pocket: 1)
            }, lashes: 1);

            Phases.Targeting(s);

            Assert.Equal(0, s.RaiderTargetCreature[0]);   // the Vetch, not the further Hollow
        }

        [Fact]
        public void WithoutTaunt_TheSameBoardIsTargetedTheOtherWay()
        {
            // The control the test above needs. Without it, "the Lash targets
            // creature 0" is also what a broken selector that always returns
            // the first creature would produce, and the Taunt assertion would
            // be reading nothing.
            var s = LaneWith(new[]
            {
                Spec(Species.Vetch, pocket: 0),
                Spec(Species.Hollow, pocket: 1)
            }, lashes: 1);

            Phases.Targeting(s);

            Assert.Equal(1, s.RaiderTargetCreature[0]);
        }

        [Fact]
        public void TauntI_HoldsOneLashAndTheSecondStillReachesPast()
        {
            // Tier decides how much a trait covers, never whether it works
            // (bible 1.3). At tier I the second Lash is not held at ALL - it is
            // not half-held or held late - and it goes back to preferring the
            // far pocket.
            var s = LaneWith(new[]
            {
                Spec(Species.Vetch, pocket: 0, trait: Trait.Taunt, tier: 1),
                Spec(Species.Hollow, pocket: 1)
            }, lashes: 2);

            Phases.Targeting(s);

            Assert.Equal(0, s.RaiderTargetCreature[0]);   // held; spawn index 0 wins the tie
            Assert.Equal(1, s.RaiderTargetCreature[1]);   // free, and reaches past
        }

        [Fact]
        public void TauntII_HoldsBoth()
        {
            var s = LaneWith(new[]
            {
                Spec(Species.Vetch, pocket: 0, trait: Trait.Taunt, tier: 2),
                Spec(Species.Hollow, pocket: 1)
            }, lashes: 2);

            Phases.Targeting(s);

            Assert.Equal(0, s.RaiderTargetCreature[0]);
            Assert.Equal(0, s.RaiderTargetCreature[1]);
        }

        [Fact]
        public void Taunt_ReleasesTheLashWhenItsCarrierDies()
        {
            // Capacity is recomputed from scratch every tick from the LIVE
            // creature set (5.3), never accumulated. A carrier that falls
            // releases what it was holding on the same tick, and the Lash goes
            // straight back to the furthest body.
            var s = LaneWith(new[]
            {
                Spec(Species.Vetch, pocket: 0, trait: Trait.Taunt, tier: 1),
                Spec(Species.Hollow, pocket: 1)
            }, lashes: 1);

            Phases.Targeting(s);
            Assert.Equal(0, s.RaiderTargetCreature[0]);

            s.CreatureHp[0] = 0;
            Phases.Targeting(s);
            Assert.Equal(1, s.RaiderTargetCreature[0]);
        }

        [Fact]
        public void Taunt_DoesNotReachARaiderThatCannotReachTheCarrier()
        {
            // Gated on the RAIDER's range, not the carrier's - the opposite of
            // Chill's gate, and deliberately. A taunt thrown from outside the
            // Lash's own reach would pin it to a target it can never hit, which
            // is a stun, and 4.2 gives Taunt no such effect. Ungated, one Vetch
            // in a back pocket would freeze every Lash on the board.
            //
            // Pocket 4 sits at tile 20 and the Lash is on tile 6: DistSq 197,
            // far outside its range of 5.
            var s = LaneWith(new[]
            {
                Spec(Species.Vetch, pocket: 4, trait: Trait.Taunt, tier: 1),
                Spec(Species.Hollow, pocket: 1)
            }, lashes: 1);

            Phases.Targeting(s);

            Assert.Equal(1, s.RaiderTargetCreature[0]);   // the Hollow, which it CAN hit
        }

        // ------------------------------------------------------------------
        // Carapace
        // ------------------------------------------------------------------

        [Fact]
        public void Carapace_ReducesIncomingDamageAndCountersNothing()
        {
            // 4.3: -25% at tier I. And Carapace answers no raider - CounterFor
            // never returns it, which is what makes it legal in a wave
            // alongside any other trait.
            foreach (RaiderType r in new[] { RaiderType.Courser, RaiderType.Lash, RaiderType.Skirmisher })
                Assert.NotEqual(Trait.Carapace, Stats.CounterFor(r));

            var s = LaneWith(new[]
            {
                Spec(Species.Vetch, pocket: 0, trait: Trait.Carapace, tier: 1),
                Spec(Species.Vetch, pocket: 1)
            }, lashes: 1);

            int with = Attacks.DamageTaken(s, 0, 100);
            int without = Attacks.DamageTaken(s, 1, 100);

            Assert.Equal(75, with);
            Assert.Equal(100, without);
        }

        [Fact]
        public void Carapace_LaddersWithTierAndTruncatesTowardZero()
        {
            Assert.Equal(25, Stats.CarapacePercent(1));
            Assert.Equal(40, Stats.CarapacePercent(2));
            Assert.Equal(55, Stats.CarapacePercent(3));

            // Against the Lash's real swing rather than a round 100: 30 at
            // -25% is 22, not 22.5. Integer and truncating, like every other
            // modifier in Attacks, and it is the defender that pays the
            // fraction.
            var s = LaneWith(new[]
            {
                Spec(Species.Vetch, pocket: 0, trait: Trait.Carapace, tier: 1),
                Spec(Species.Vetch, pocket: 1, trait: Trait.Carapace, tier: 3)
            }, lashes: 1);

            Assert.Equal(22, Attacks.DamageTaken(s, 0, 30));
            Assert.Equal(13, Attacks.DamageTaken(s, 1, 30));   // 30 * 45 / 100
        }

        [Fact]
        public void Carapace_IsAppliedToWhatTheLashActuallyLands()
        {
            // The two halves wired together. DamageTaken being correct in
            // isolation says nothing about phase 5 calling it, and a raider
            // pass that subtracted the raw 30 would pass every assertion above.
            var s = LaneWith(new[]
            {
                Spec(Species.Vetch, pocket: 0),
                Spec(Species.Vetch, pocket: 1, trait: Trait.Carapace, tier: 1)
            }, lashes: 1);

            Phases.Targeting(s);
            Phases.Attack(s);

            Assert.Equal(1, s.RaiderTargetCreature[0]);
            Assert.Equal(260 - 22, s.CreatureHp[1]);
        }

        // ------------------------------------------------------------------
        // Wave 7, end to end
        // ------------------------------------------------------------------

        [Fact]
        public void Wave7_SimulatesToADecisionAndIsRecordable()
        {
            // The authored content actually running, which none of the unit
            // assertions above establishes: a wave whose raiders never reach
            // the Ark and never die would Stall, and a wave the engine cannot
            // record is one whose replay describes a different game.
            var deployment = new[]
            {
                Spec(Species.Vetch, pocket: 0, trait: Trait.Taunt, tier: 1),
                Spec(Species.Ember, pocket: 1),
                Spec(Species.Hollow, pocket: 2)
            };

            var runner = new SimRunner(WaveDef.Wave7(), Lane.Defile(), deployment, seed: 7);
            Assert.Null(runner.Unrecordable);

            while (runner.Step()) { }

            Assert.True(runner.Done);
            Assert.NotEqual(Result.Running, runner.Result);
            Assert.NotEqual(Result.Stalled, runner.Result);
        }
    }
}
