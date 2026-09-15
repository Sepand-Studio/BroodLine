using Xunit;
using Broodline.Sim.Combat;

namespace Broodline.Sim.Tests.Combat
{
    /// The traits and the raider mechanic this slice gave a body to: Lash's
    /// attack, Taunt forcing it, Splash answering the stream of Skirmishers,
    /// and Carapace blunting whatever lands.
    ///
    /// Splash arrived a round late. It shipped declared-but-inert - named by
    /// CounterFor, listed among Counters.cs's deferred, and reported as zero
    /// coverage by Diagnosis - while wave 7 is 1 Lash and 6 Skirmishers, whose
    /// answer IS Splash. Six of the wave's seven spawns had no working counter.
    /// Pierce, Sprint, Cinder, Reach and Burrow are still deferred and are
    /// asserted about nowhere here, which is the state Splash should have been
    /// caught in.
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

        /// Phase 5's splash buffer. The engine's copy lives in SimRunner and is
        /// sized from the ladder; a test that reached phase 5 directly has to
        /// supply its own, and sizing it the same way means a retune of tier
        /// III cannot leave these one entry short.
        private static int[] SplashScratch() => new int[Stats.MaxSplashTargets];

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
            Phases.Attack(s, SplashScratch());

            Assert.Equal(1, s.RaiderTargetCreature[0]);      // the far pocket
            Assert.Equal(260 - 30, s.CreatureHp[1]);
            Assert.Equal(260, s.CreatureHp[0]);              // the near body is skipped
            Assert.Equal(2 * Stats.TicksPerSecond, s.RaiderNextAttackAt[0]);

            // Not again until the interval elapses.
            s.Tick = 59;
            Phases.Targeting(s);
            Phases.Attack(s, SplashScratch());
            Assert.Equal(260 - 30, s.CreatureHp[1]);

            s.Tick = 60;
            Phases.Targeting(s);
            Phases.Attack(s, SplashScratch());
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
            Phases.Attack(s, SplashScratch());

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
            Phases.Attack(s, SplashScratch());

            Assert.Equal(1, s.RaiderTargetCreature[0]);
            Assert.Equal(260 - 22, s.CreatureHp[1]);
        }

        // ------------------------------------------------------------------
        // Splash - the answer to the six bodies wave 7 is mostly made of
        // ------------------------------------------------------------------

        /// One Defile lane with a Skirmisher on each tile named.
        ///
        /// Splash is measured between RAIDERS rather than from the creature, so
        /// their tiles are the variable here and the carrier's pocket is not.
        private static SimState SkirmisherLaneWith(CreatureSpec[] deployment, params int[] tiles)
        {
            var spawns = new SpawnEntry[tiles.Length];
            for (int i = 0; i < tiles.Length; i++)
                spawns[i] = new SpawnEntry { Tick = 0, Type = RaiderType.Skirmisher };

            var s = new SimState(
                new WaveDef(id: 998, integrity: 9, laneCount: 1, spawns: spawns),
                Lane.Defile(), deployment);

            s.Tick = 0;
            Phases.Spawn(s);
            for (int i = 0; i < tiles.Length; i++) s.RaiderProgress[i] = Fix64.FromInt(tiles[i]);
            return s;
        }

        /// The raiders one swing lands on, as an array, for assertions that are
        /// about WHO rather than about how much.
        private static int[] Hit(SimState s, int creature, int primary)
        {
            var buffer = SplashScratch();
            int count = Counters.ApplySplash(s, creature, primary, buffer);

            var hits = new int[count];
            for (int i = 0; i < count; i++) hits[i] = buffer[i];
            return hits;
        }

        [Fact]
        public void Splash_LaddersWithTierAndCountsTheCarriersOwnTargetAmongThem()
        {
            // combat_numbers 4.2: 2 targets within 1 tile, 3, then 5 at radius
            // 2. The count is TOTAL - the creature's own target is one of the
            // two, not one plus two - which is what makes tier I a real but
            // small answer to a stream of bodies rather than a tripling.
            Assert.Equal(2, Stats.SplashTargets(1));
            Assert.Equal(3, Stats.SplashTargets(2));
            Assert.Equal(5, Stats.SplashTargets(3));
            Assert.Equal(0, Stats.SplashTargets(0));   // the no-trait gate

            Assert.Equal(1, Stats.SplashRadius(1));
            Assert.Equal(1, Stats.SplashRadius(2));
            Assert.Equal(2, Stats.SplashRadius(3));

            Assert.Equal(5, Stats.MaxSplashTargets);
        }

        [Fact]
        public void Splash_HitsItsTierCountAndNoMore()
        {
            // Tier I answers 2 of the 3 Skirmishers in radius. Not 3, not all.
            // All three stand on the same tile, so nothing is excluded by
            // distance and the CAP is the only thing doing the work.
            var s = SkirmisherLaneWith(new[]
            {
                Spec(Species.Ember, pocket: 0, trait: Trait.Splash, tier: 1)
            }, 6, 6, 6);

            var hit = Hit(s, creature: 0, primary: 0);

            Assert.Equal(2, hit.Length);
            // Total order, tie-broken on spawn index - the same two every run,
            // on CoreCLR and on IL2CPP alike.
            Assert.Equal(new[] { 0, 1 }, hit);
        }

        [Fact]
        public void Splash_TieBreaksOnSpawnIndexAscendingAndAlwaysKeepsTheCarriersOwnTarget()
        {
            // Four bodies on one tile and room for three. Every candidate is at
            // distance 0, so this is a pure tie and the spawn-index tie-break
            // is the ONLY thing choosing - which is exactly the comparator
            // solo_execution 12 is about, and the one that would diverge
            // between runtimes if it were left to a sort over equal keys.
            var s = SkirmisherLaneWith(new[]
            {
                Spec(Species.Ember, pocket: 0, trait: Trait.Splash, tier: 2)
            }, 6, 6, 6, 6);

            Assert.Equal(new[] { 0, 1, 2 }, Hit(s, creature: 0, primary: 0));

            // The primary is hits[0] whatever its index, and the spill is still
            // spawn-ordered behind it. Without that, a carrier whose target tied
            // with a lower-indexed raider could be ordered out of its own swing.
            Assert.Equal(new[] { 2, 0, 1 }, Hit(s, creature: 0, primary: 2));
        }

        [Fact]
        public void Splash_DoesNotReachBeyondItsRadius()
        {
            // Tier II has room for THREE, so a third hit would be legal on
            // capacity - which isolates the radius from the cap.
            //
            // The excluded body sits at EXACTLY radius + 1. An earlier version
            // put it on tile 9, three tiles out, and a mutation widening the
            // radius by one went undetected: a boundary test has to stand on
            // the boundary or it only proves the radius is finite.
            var s = SkirmisherLaneWith(new[]
            {
                Spec(Species.Ember, pocket: 0, trait: Trait.Splash, tier: 2)
            }, 6, 7, 8);

            Assert.Equal(new[] { 0, 1 }, Hit(s, creature: 0, primary: 0));
        }

        [Fact]
        public void SplashIII_WidensTheRadiusRatherThanOnlyTheCount()
        {
            // 4.2 gives III "5 targets, radius 2" - two changes on one rung.
            // The same board at II and at III, so the extra body at tile 8 is
            // attributable to the radius and not to the larger count.
            var atThree = SkirmisherLaneWith(new[]
            {
                Spec(Species.Ember, pocket: 0, trait: Trait.Splash, tier: 3)
            }, 6, 7, 8, 9);

            Assert.Equal(new[] { 0, 1, 2 }, Hit(atThree, creature: 0, primary: 0));

            var atTwo = SkirmisherLaneWith(new[]
            {
                Spec(Species.Ember, pocket: 0, trait: Trait.Splash, tier: 2)
            }, 6, 7, 8, 9);

            Assert.Equal(new[] { 0, 1 }, Hit(atTwo, creature: 0, primary: 0));
        }

        [Fact]
        public void Splash_SkipsDeadRaiders()
        {
            var s = SkirmisherLaneWith(new[]
            {
                Spec(Species.Ember, pocket: 0, trait: Trait.Splash, tier: 2)
            }, 6, 6, 6);

            s.RaiderAlive[1] = false;

            Assert.Equal(new[] { 0, 2 }, Hit(s, creature: 0, primary: 0));
        }

        [Fact]
        public void WithoutSplash_OnlyTheCarriersOwnTargetIsHit()
        {
            // The property that makes this whole mechanic inert for the corpus.
            // All 500 scenarios predate Splash and none deploys a carrier, so
            // phase 5 must take a path that is byte-identical to the one it
            // took before ApplySplash existed - one raider, one subtraction.
            // If this ever goes red, every corpus hash has moved.
            var s = SkirmisherLaneWith(new[]
            {
                Spec(Species.Ember, pocket: 0)
            }, 6, 6, 6);

            Assert.Equal(new[] { 0 }, Hit(s, creature: 0, primary: 0));

            // Carrying a DIFFERENT trait is the same path - CreatureCarries
            // matches on the trait, and a Chill carrier splashes nothing.
            var chiller = SkirmisherLaneWith(new[]
            {
                Spec(Species.Pale, pocket: 0, trait: Trait.Chill, tier: 3)
            }, 6, 6, 6);

            Assert.Equal(new[] { 0 }, Hit(chiller, creature: 0, primary: 0));
        }

        [Fact]
        public void Splash_ReachesPhaseFive()
        {
            // The selector being right in isolation says nothing about phase 5
            // calling it. Asserted on WHO lost HP rather than on how much:
            // 4.2 is explicit that no counter has a damage component, and the
            // number below is Ember's ordinary 36 landing on a second body
            // rather than a bigger number landing on one.
            //
            // Ember in pocket 0 (tile 6) has range 3, covering tiles 4..8, so
            // the body on tile 9 is outside its reach as well as outside the
            // splash - and Vanguard's tie on tiles 6 and 6 resolves to spawn
            // index 0, which fixes the impact point.
            var s = SkirmisherLaneWith(new[]
            {
                Spec(Species.Ember, pocket: 0, trait: Trait.Splash, tier: 1)
            }, 6, 6, 9);

            Phases.Targeting(s);
            Assert.Equal(0, s.CreatureTarget[0]);

            Phases.Attack(s, SplashScratch());

            Assert.Equal(40 - 36, s.RaiderHp[0]);   // struck
            Assert.Equal(40 - 36, s.RaiderHp[1]);   // splashed, same swing
            Assert.Equal(40, s.RaiderHp[2]);        // out of radius, untouched
        }

        [Fact]
        public void Splash_CapacityReachesDiagnosis()
        {
            // The wiring that makes a Skirmisher breach report the right reason.
            var s = SkirmisherLaneWith(new[]
            {
                Spec(Species.Ember, pocket: 0, trait: Trait.Splash, tier: 3)
            }, 6);

            Assert.True(Diagnosis.PreWaveCheck(s, RaiderType.Skirmisher).Coverage);
        }

        [Fact]
        public void ASkirmisherBreachNoLongerBlamesCoverageWhenTheAnswerIsDeployed()
        {
            // The regression the missing Diagnosis arm caused, pinned. With
            // CapacityFor returning 0 for Splash, Evaluate returned on its
            // SECOND boolean and EVERY Skirmisher breach read as "coverage" -
            // the loss screen telling a player their tier was too low while
            // they were holding the answer. Wave 7 is six Skirmishers, so that
            // was the wrong sentence for six of its seven spawns.
            var s = SkirmisherLaneWith(new[]
            {
                Spec(Species.Ember, pocket: 0, trait: Trait.Splash, tier: 1)
            }, 6, 6);

            var verdict = Diagnosis.Evaluate(
                s, RaiderType.Skirmisher,
                Diagnosis.SimultaneousCount(s, RaiderType.Skirmisher),
                tile: 6);

            Assert.True(verdict.Access);
            Assert.True(verdict.Coverage);     // two at once, and Splash I answers two
            Assert.True(verdict.Placement);
            Assert.True(verdict.Answered);
        }

        [Fact]
        public void SplashCoverageIsAboutSimultaneityAndTierDecidesHowMuch()
        {
            // bible 1.3: tier decides how much a trait covers, never whether it
            // works. A third body at the same moment is more than Splash I
            // answers, and the diagnosis must say COVERAGE for that - which is
            // only distinguishable from the bug above because the capacity
            // number is genuinely being read.
            var s = SkirmisherLaneWith(new[]
            {
                Spec(Species.Ember, pocket: 0, trait: Trait.Splash, tier: 1)
            }, 6, 6, 6);

            var verdict = Diagnosis.Evaluate(
                s, RaiderType.Skirmisher,
                Diagnosis.SimultaneousCount(s, RaiderType.Skirmisher),
                tile: 6);

            Assert.True(verdict.Access);
            Assert.False(verdict.Coverage);    // three at once, Splash I answers two
        }

        [Fact]
        public void WithNoSplashCarrierTheDiagnosisIsAccessRatherThanCoverage()
        {
            // The first false is the diagnosis (combat_engine 7), so a player
            // holding nothing must be told they hold nothing - not that their
            // tier is short.
            var s = SkirmisherLaneWith(new[]
            {
                Spec(Species.Ember, pocket: 0)
            }, 6, 6);

            var verdict = Diagnosis.Evaluate(
                s, RaiderType.Skirmisher,
                Diagnosis.SimultaneousCount(s, RaiderType.Skirmisher),
                tile: 6);

            Assert.False(verdict.Access);
            Assert.False(verdict.Coverage);    // never reached, and correctly false
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
