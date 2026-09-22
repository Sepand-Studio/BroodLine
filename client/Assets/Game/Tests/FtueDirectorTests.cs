using System;
using System.Collections.Generic;
using System.Globalization;
using Broodline.Api;
using Broodline.Model;
using Broodline.Net;
using Broodline.Sim.Combat;
using Broodline.UI;
using NUnit.Framework;
using Instinct = Broodline.Sim.Combat.Instinct;   // Broodline.Api has an `Instinct` class too - the generated
                                                  // /v1/sync forecast row. This file means the engine's enum.

namespace Broodline.Game.Tests
{
    /// The director's decisions, each of which is a place the first hour can
    /// be wrong without anything crashing.
    ///
    /// WHAT IS NOT HERE, STATED RATHER THAN LEFT TO BE NOTICED: there is no
    /// end-to-end walk of `RunAsync` in this suite. The walk advances when a
    /// screen answers, and every Task 15/16 screen answers through
    /// `Button.clicked` - raised by a `Clickable` manipulator handling a
    /// dispatched `ClickEvent`, which needs an attached `Panel`. A bare
    /// `new DeployView()` has none (`ComponentTests`'s class comment records
    /// the dead ends: `Clickable.SimulateSingleClick` is `internal`,
    /// `VisualElement.GetCallbackCount&lt;T&gt;()` does not exist), so a test
    /// here could start the walk and never get past its first screen.
    /// Adding a test-only "advance" hook to `ScreenFlow` would be a seam
    /// that exists only for tests to reach into, which this codebase has
    /// already refused once (`OutboxClient`'s connectivity predicate is a
    /// real constructor parameter for exactly that reason).
    ///
    /// So the walk itself is covered by `ScreenFlowTests` (the sequencing
    /// mechanism, in full) plus everything below (every decision it makes
    /// between screens), and the join between them - "this view's callback
    /// is the one that resumes this turn" - is read, not executed. A PlayMode
    /// test with a live `UIDocument` is where that join can actually be
    /// proven, and it is owed.
    public class FtueDirectorTests
    {
        // ---------------------------------------------------------------
        // Fixtures
        // ---------------------------------------------------------------

        static CreatureDto Creature(string species, bool founder = false, Guid? committedTo = null, Guid? id = null)
        {
            return new CreatureDto
            {
                CreatureId = id ?? Guid.NewGuid(),
                Species = species,
                Generation = 1,
                Trait1 = "Taunt", Tier1 = 1,
                Trait2 = "Carapace", Tier2 = 1,
                Instinct = "Vanguard",
                IsFounder = founder,
                CommittedTo = committedTo,
            };
        }

        static CreatureSpecDto Spec(string species, string trait1, int? tier1, string trait2, int? tier2,
            string instinct, int pocket)
        {
            return new CreatureSpecDto
            {
                Species = species, Trait1 = trait1, Tier1 = tier1,
                Trait2 = trait2, Tier2 = tier2, Instinct = instinct, Pocket = pocket,
            };
        }

        // ---------------------------------------------------------------
        // How many creatures a wave is fought with
        // ---------------------------------------------------------------

        [Test]
        public void WantedFor_IsTheAuthoredRosterForTheTutorialWaves_AndTheCapAfter()
        {
            // design 6.1's table: wave 1 expects 2, wave 2 expects 3.
            Assert.AreEqual(2, FtueDirector.WantedFor(1));
            Assert.AreEqual(3, FtueDirector.WantedFor(2));

            // Nothing authors an expected roster past wave 2, and wave 6 is
            // the one designed to be LOST (bible 9.3) - sending fewer than
            // the player owns would make that lesson land for the wrong
            // reason.
            Assert.AreEqual(DeployScreen.Cap, FtueDirector.WantedFor(6));
            Assert.AreEqual(DeployScreen.Cap, FtueDirector.WantedFor(7));
            Assert.AreNotEqual(FtueDirector.WantedFor(2), FtueDirector.WantedFor(6));
        }

        // ---------------------------------------------------------------
        // What a tap on a field row does to the selection
        //
        // THE RULE IS ASYMMETRIC, WHICH IS WHY IT IS TESTED AT ALL. A removal
        // is always allowed and an add past the cap is refused, and those two
        // halves have different justifications - so "it toggles" is not a
        // description a reader can check the code against. `DeployView` draws
        // the second half (every undeployed row dimmed and disabled at the
        // cap); this is the half that decides it.
        //
        // The loop AROUND this - the closure in `FightAsync` that rebuilds
        // the model and re-binds without ending the turn - is still read
        // rather than executed, for the reason this file's class comment
        // gives: it answers a `ClickEvent` that needs an attached Panel.
        // Named in the task report as owed to the PlayMode pass.
        // ---------------------------------------------------------------

        [Test]
        public void Toggle_AddsACreatureThatIsNotSelected_AtTheEnd()
        {
            // ORDER IS PART OF THE REQUEST. "Index == deployment order", and
            // `deploymentMatches` compares the replay's deployment against
            // the stored one IN ORDER - so an add that inserted anywhere but
            // the end would silently renumber pockets the player had already
            // filled.
            var a = Guid.NewGuid();
            var b = Guid.NewGuid();
            var selected = new List<Guid> { a };

            Assert.IsTrue(FtueDirector.Toggle(selected, b), "an add must report that something changed");
            CollectionAssert.AreEqual(new[] { a, b }, selected);
        }

        [Test]
        public void Toggle_RemovesASelectedCreature_EvenWhenItIsTheLastOne()
        {
            // THE FLOOR IS THE MODEL'S TO STATE, NOT THIS METHOD'S.
            // `DeployScreen.Build` answers an empty selection with "Send at
            // least one creature..." and `DeployView` renders it in coral
            // above a greyed Start; any tap undoes it. Refusing the tap here
            // would make the screen silent about a rule it can state, and
            // `WantedFor`'s own comment already says its number is "A FLOOR,
            // NOT A PROMISE" that sizes the OPENING selection and nothing
            // else.
            var a = Guid.NewGuid();
            var selected = new List<Guid> { a };

            Assert.IsTrue(FtueDirector.Toggle(selected, a));
            CollectionAssert.IsEmpty(selected);

            // And the model really does refuse it rather than merely
            // disliking it - which is what makes the removal safe.
            var roster = new RosterScreen();
            roster.ApplyRoster(new RosterResponse { Creatures = { Creature("Vetch", id: a) }, Cap = 20 });
            var model = DeployScreen.Build(1, roster, selected);
            Assert.IsFalse(model.CanDeploy);
            Assert.IsNotEmpty(model.Blocker);
        }

        [Test]
        public void Toggle_RefusesAnAddAtTheCap_ButStillLetsOneGo()
        {
            // THE ASYMMETRY, ASSERTED AS AN ASYMMETRY. Over the cap the
            // model's sentence is "A deployment is at most 5 creatures." and,
            // unlike the floor, no single tap undoes it - the player would
            // have to work out which of six to remove. So the add is refused
            // and the removal beside it is not, in the same state.
            var selected = new List<Guid>();
            for (var i = 0; i < DeployScreen.Cap; i++) selected.Add(Guid.NewGuid());
            var atTheCap = new List<Guid>(selected);

            Assert.IsFalse(FtueDirector.Toggle(selected, Guid.NewGuid()),
                "a sixth creature was added past DeployScreen.Cap");
            CollectionAssert.AreEqual(atTheCap, selected, "a refused add must not disturb the selection");

            Assert.IsTrue(FtueDirector.Toggle(selected, atTheCap[0]),
                "at the cap a REMOVAL must still work, or the screen is stuck");
            Assert.AreEqual(DeployScreen.Cap - 1, selected.Count);
        }

        [Test]
        public void Toggle_RoundTripsToWhereItStarted()
        {
            // The property the two cases above do not state between them: a
            // tap and a second tap on the same row leave the deployment - and
            // therefore every pocket in it - exactly as they were.
            var ids = new List<Guid> { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
            var selected = new List<Guid>(ids);

            Assert.IsTrue(FtueDirector.Toggle(selected, ids[0]));
            Assert.IsTrue(FtueDirector.Toggle(selected, ids[0]));

            // NOT the same ORDER - the re-added creature goes to the end,
            // which is the pocket it now holds and what the lane picture and
            // the row badges redraw. Asserted as a set plus a count so the
            // test says what it means rather than pinning the wrong half.
            CollectionAssert.AreEquivalent(ids, selected);
            Assert.AreEqual(ids[0], selected[selected.Count - 1],
                "a re-added creature takes the last pocket, not the one it had");
        }

        // ---------------------------------------------------------------
        // The issuance's deployment, as engine structs
        // ---------------------------------------------------------------

        [Test]
        public void SpecsFor_ParsesTheIssuanceInOrder_AndKeepsEachPocket()
        {
            // The order is the deployment, not a set: `deploymentMatches`
            // compares the replay's deployment against the stored one IN
            // ORDER, so a pass that sorted or grouped would produce a replay
            // the server rejects as a mismatch.
            var specs = FtueDirector.SpecsFor(new[]
            {
                Spec("Ember", "Splash", 1, "Carapace", 1, "Vanguard", pocket: 2),
                Spec("Vetch", "Taunt", 2, "Chill", 3, "Overwatch", pocket: 0),
            });

            Assert.AreEqual(2, specs.Length);

            Assert.AreEqual(Species.Ember, specs[0].Species);
            Assert.AreEqual(Trait.Splash, specs[0].Trait1);
            Assert.AreEqual(Instinct.Vanguard, specs[0].Instinct);
            Assert.AreEqual(2, specs[0].Pocket);

            Assert.AreEqual(Species.Vetch, specs[1].Species);
            Assert.AreEqual(Trait.Taunt, specs[1].Trait1);
            Assert.AreEqual(2, specs[1].Tier1);
            Assert.AreEqual(Trait.Chill, specs[1].Trait2);
            Assert.AreEqual(3, specs[1].Tier2);
            Assert.AreEqual(Instinct.Overwatch, specs[1].Instinct);
            Assert.AreEqual(0, specs[1].Pocket);
        }

        [Test]
        public void SpecsFor_TakesTheServersCasingEitherWay()
        {
            // `ftue/stock.ts` sends PascalCase today. The parse is
            // case-insensitive so a casing change on the wire is not a
            // client build.
            var specs = FtueDirector.SpecsFor(new[] { Spec("vetch", "taunt", 1, "chill", 1, "vanguard", 0) });
            Assert.AreEqual(Species.Vetch, specs[0].Species);
            Assert.AreEqual(Trait.Taunt, specs[0].Trait1);
            Assert.AreEqual(Instinct.Vanguard, specs[0].Instinct);
        }

        [Test]
        public void SpecsFor_RefusesAThingThisEngineDoesNotCarry()
        {
            // The bundle and the client build disagreeing is a real state -
            // a bundle publish can name content a shipped client has no enum
            // for. Simulating it as whatever `default` happens to be would
            // fight a wave with a creature the server did not issue.
            Assert.Throws<ArgumentException>(() =>
                FtueDirector.SpecsFor(new[] { Spec("Warden", "Taunt", 1, "Chill", 1, "Vanguard", 0) }));
            Assert.Throws<ArgumentException>(() =>
                FtueDirector.SpecsFor(new[] { Spec("Vetch", "Pierce", 1, "Chill", 1, "Vanguard", 0) }));
            Assert.Throws<ArgumentException>(() =>
                FtueDirector.SpecsFor(new[] { Spec("Vetch", "Taunt", 1, "Chill", 1, "Ambush", 0) }));
        }

        [Test]
        public void SpecsFor_RefusesANullTier_RatherThanSimulatingTierZero()
        {
            // data_model 2: a null tier is an Aberrant, and `PlayerSnapshot`
            // spells out why the two must not be conflated - "zero would sort
            // and display as 'less than tier I'". Design 10 defers Aberrant
            // content, so this is an impossible state made loud rather than
            // fought as a weaker creature.
            Assert.Throws<ArgumentException>(() =>
                FtueDirector.SpecsFor(new[] { Spec("Vetch", "Taunt", null, "Chill", 1, "Vanguard", 0) }));
            Assert.Throws<ArgumentException>(() =>
                FtueDirector.SpecsFor(new[] { Spec("Vetch", "Taunt", 1, "Chill", null, "Vanguard", 0) }));
        }

        [Test]
        public void SpecsFor_RefusesANullSlotAndANullDeployment()
        {
            Assert.Throws<ArgumentNullException>(() => FtueDirector.SpecsFor(null));
            Assert.Throws<ArgumentException>(() => FtueDirector.SpecsFor(new CreatureSpecDto[] { null }));
        }

        [Test]
        public void SpecsFor_AcceptsAnEmptySecondCombatSlot_AsTraitNoneAtTierZero()
        {
            // A creature with one combat trait arrives as trait2 "None" with a null
            // tier, because an absent trait has no coverage. That is an ABSENCE, not
            // an Aberrant, and it is what stopped the first hour at a fight.
            var specs = FtueDirector.SpecsFor(new[]
            {
                Spec("Vetch", "Taunt", 1, "None", null, "Vanguard", 0),
            });

            Assert.AreEqual(1, specs.Length);
            Assert.AreEqual(Trait.Taunt, specs[0].Trait1);
            Assert.AreEqual(1, specs[0].Tier1);
            Assert.AreEqual(Trait.None, specs[0].Trait2);
            Assert.AreEqual(0, specs[0].Tier2, "an empty combat slot is tier zero, not a throw");
        }

        [Test]
        public void SpecsFor_StillRefusesARealTraitWithNoCoverageTier()
        {
            // The guard's original purpose, kept: a null tier on a REAL trait is an
            // Aberrant (data_model 2), which this phase's bundle does not author and
            // the engine cannot represent.
            var ex = Assert.Throws<ArgumentException>(() => FtueDirector.SpecsFor(new[]
            {
                Spec("Vetch", "Taunt", 1, "Carapace", null, "Vanguard", 0),
            }));
            StringAssert.Contains("Aberrant", ex.Message);
        }

        [Test]
        public void SpecsFor_AcceptsTraitNoneWithAnExplicitTier()
        {
            // Don't start rejecting a shape that works today.
            var specs = FtueDirector.SpecsFor(new[]
            {
                Spec("Vetch", "Taunt", 1, "None", 0, "Vanguard", 0),
            });
            Assert.AreEqual(Trait.None, specs[0].Trait2);
            Assert.AreEqual(0, specs[0].Tier2);
        }

        // ---------------------------------------------------------------
        // The seed
        // ---------------------------------------------------------------

        [Test]
        public void SeedOf_KeepsEveryBitOfA63BitSeed()
        {
            // `issueWave` masks its CSPRNG draw to 63 bits and sends it as a
            // STRING, because JSON numbers are doubles. This is the test
            // that says why the string matters: the same value through a
            // double loses its low bits and simulates a different wave than
            // the server re-simulates.
            const string wire = "9223372036854775783";      // the largest prime below 2^63
            var parsed = FtueDirector.SeedOf(wire);

            Assert.AreEqual(9223372036854775783UL, parsed);
            Assert.AreNotEqual(parsed, (ulong)double.Parse(wire, CultureInfo.InvariantCulture),
                "a double round-trip was lossless, so this test proves nothing");
        }

        [Test]
        public void SeedOf_RefusesSomethingThatIsNotASeed()
        {
            Assert.Throws<ArgumentException>(() => FtueDirector.SeedOf("-1"));
            Assert.Throws<ArgumentException>(() => FtueDirector.SeedOf("12.5"));
            Assert.Throws<ArgumentException>(() => FtueDirector.SeedOf(null));
            Assert.Throws<ArgumentException>(() => FtueDirector.SeedOf(string.Empty));
        }

        // ---------------------------------------------------------------
        // Beat 6's two provided creatures
        // ---------------------------------------------------------------

        [Test]
        public void TutorialPair_ComesFromTheStockGrantsOwnResponse()
        {
            // task-17-brief.md reaches for `c.AcquiredAfter(stockGrant)`,
            // which is not on `CreatureDto` and could not be - nothing on
            // the wire carries an acquisition time. The grant hands back
            // exactly what it made, so no inference is needed.
            var stockA = Creature("Vetch");
            var stockB = Creature("Ember");
            var older = Creature("Skitter");

            var pair = FtueDirector.TutorialPair(
                new FtueStockResponse { Creatures = new[] { stockA, stockB } },
                new[] { older, stockA, stockB });

            Assert.AreSame(stockA, pair[0]);
            Assert.AreSame(stockB, pair[1]);
            // And NOT the creature that was already there - which is what a
            // roster-order fallback would have picked first.
            Assert.AreNotSame(older, pair[0]);
            Assert.AreNotSame(older, pair[1]);
        }

        [Test]
        public void TutorialPair_FallsBackToTheRosterOnAResumedSession()
        {
            // The grant landed on a previous run, so `tutorialStockGranted`
            // is true and there is no response in hand.
            var a = Creature("Vetch");
            var b = Creature("Ember");
            var pair = FtueDirector.TutorialPair(null, new[] { a, b });

            Assert.AreSame(a, pair[0]);
            Assert.AreSame(b, pair[1]);
        }

        [Test]
        public void TutorialPair_NeverReturnsAFounder_FromEitherBranch()
        {
            // design 5 beat 6 and splice_confirm_spec 6: "two PROVIDED
            // creatures - never the named one."
            var founder = Creature("Hollow", founder: true);
            var a = Creature("Vetch");
            var b = Creature("Ember");

            var fromRoster = FtueDirector.TutorialPair(null, new[] { founder, a, b });
            Assert.AreSame(a, fromRoster[0]);
            Assert.AreSame(b, fromRoster[1]);

            // Even a grant that somehow named a Founder cannot put one in the
            // slots - the filter is on the returned pair, not on the source.
            var fromStock = FtueDirector.TutorialPair(
                new FtueStockResponse { Creatures = new[] { founder, a, b } }, new[] { founder });
            Assert.AreSame(a, fromStock[0]);
            Assert.AreSame(b, fromStock[1]);
        }

        [Test]
        public void TutorialPair_SkipsACreatureThatIsOutFighting()
        {
            // `creature_committed` is a refusal the splice routes make, and a
            // chamber built on a committed parent can only be refused.
            var committed = Creature("Vetch", committedTo: Guid.NewGuid());
            var a = Creature("Ember");
            var b = Creature("Skitter");

            var pair = FtueDirector.TutorialPair(null, new[] { committed, a, b });
            Assert.AreSame(a, pair[0]);
            Assert.AreSame(b, pair[1]);
        }

        [Test]
        public void TutorialPair_IsNullWhenThereAreNotTwo()
        {
            Assert.IsNull(FtueDirector.TutorialPair(null, new[] { Creature("Vetch") }));
            Assert.IsNull(FtueDirector.TutorialPair(null, new CreatureDto[0]));
            Assert.IsNull(FtueDirector.TutorialPair(null, null));
            Assert.IsNull(FtueDirector.TutorialPair(null,
                new[] { Creature("Hollow", founder: true), Creature("Loam", founder: true) }));
        }

        [Test]
        public void LockedOut_IsEveryFounder_NotJustTheNamedOne()
        {
            // bible 3.3 flags the first five as Founders; the tutorial has no
            // business consuming any of them.
            var one = Creature("Hollow", founder: true);
            var two = Creature("Loam", founder: true);
            var ordinary = Creature("Vetch");

            var locked = FtueDirector.LockedOut(new[] { one, ordinary, two });

            Assert.AreEqual(2, locked.Count);
            Assert.IsTrue(locked.Contains(one.CreatureId));
            Assert.IsTrue(locked.Contains(two.CreatureId));
            Assert.IsFalse(locked.Contains(ordinary.CreatureId));
        }

        // ---------------------------------------------------------------
        // Beat 7's flag
        // ---------------------------------------------------------------

        static LineageNode Node(Guid id, bool mutated)
        {
            return new LineageNode
            {
                CreatureId = id, Species = "Hollow", Generation = 2,
                IsFounder = false, Pruned = false, Mutated = mutated,
            };
        }

        [Test]
        public void MutatedIn_ReadsTheServersFlagForTheChildAndNobodyElse()
        {
            // `SpliceCommitResponse` deliberately carries no `mutated`
            // (routes/splice.ts withholds it while design 10 defers Aberrant
            // content). `GET /v1/lineage` does, and this is where the reveal
            // gets it from - so the flag still comes off a server response.
            var child = Guid.NewGuid();
            var sibling = Guid.NewGuid();
            var tree = new LineageResponse { Nodes = new[] { Node(sibling, mutated: true), Node(child, mutated: false) } };

            Assert.IsFalse(FtueDirector.MutatedIn(tree, child),
                "the flag was read off the wrong node");
            Assert.IsTrue(FtueDirector.MutatedIn(tree, sibling));
        }

        [Test]
        public void MutatedIn_AnUnreadableOrSilentTreeIsNotAMutation()
        {
            // Celebrating a mutation that did not happen is exactly the
            // failure the server withholds the commit flag to avoid, so an
            // absent fact reads as "no", never as "probably".
            var child = Guid.NewGuid();
            Assert.IsFalse(FtueDirector.MutatedIn(null, child));
            Assert.IsFalse(FtueDirector.MutatedIn(new LineageResponse { Nodes = new LineageNode[0] }, child));
            Assert.IsFalse(FtueDirector.MutatedIn(new LineageResponse { Nodes = new[] { Node(child, true) } }, Guid.Empty));
        }

        // ---------------------------------------------------------------
        // The campaign's route out
        // ---------------------------------------------------------------

        [Test]
        public void WaveIdsIn_KeepsTheBundlesOwnOrder()
        {
            var snapshot = new PlayerSnapshot
            {
                Waves = new[]
                {
                    new WaveSummary { Id = 1 }, new WaveSummary { Id = 2 },
                    new WaveSummary { Id = 6 }, new WaveSummary { Id = 7 },
                },
            };

            CollectionAssert.AreEqual(new[] { 1, 2, 6, 7 }, FtueDirector.WaveIdsIn(snapshot));
        }

        [Test]
        public void WaveIdsIn_ASnapshotWithNoWavesIsEmptyRatherThanNull()
        {
            Assert.AreEqual(0, FtueDirector.WaveIdsIn(new PlayerSnapshot()).Count);
            Assert.AreEqual(0, FtueDirector.WaveIdsIn(null).Count);
        }

        [Test]
        public void FounderIn_FindsTheFirstOne_AndNothingWhenThereIsNone()
        {
            var founder = Creature("Hollow", founder: true);
            Assert.AreSame(founder, FtueDirector.FounderIn(new[] { Creature("Vetch"), founder }));
            Assert.IsNull(FtueDirector.FounderIn(new[] { Creature("Vetch") }));
            Assert.IsNull(FtueDirector.FounderIn(null));
        }

        // ---------------------------------------------------------------
        // Whether the roster is locked to a wave nobody will submit
        // ---------------------------------------------------------------

        [Test]
        public void NeedsForfeit_IsTrueWhenAnyKnownCreatureIsCommitted()
        {
            var live = Guid.NewGuid();
            Assert.IsFalse(FtueDirector.NeedsForfeit(new[] { Creature("Vetch"), Creature("Ember") }));
            Assert.IsTrue(FtueDirector.NeedsForfeit(new[] { Creature("Vetch"), Creature("Ember", committedTo: live) }));
            Assert.IsFalse(FtueDirector.NeedsForfeit(new CreatureDto[0]), "an empty roster has nothing to forfeit");
            Assert.IsFalse(FtueDirector.NeedsForfeit(new CreatureDto[] { null }), "a null slot is skipped, as FightAsync skips it");
            Assert.IsFalse(FtueDirector.NeedsForfeit(null), "no roster loaded yet is not treated as locked");
        }

        // ---------------------------------------------------------------
        // What a blocked beat says
        // ---------------------------------------------------------------

        [Test]
        public void FtueNotice_SaysADifferentThingForEachOutcome()
        {
            var sentences = new List<string>
            {
                FtueNotice.For(OutboxOutcome.Queued, FtueNotice.SubmitWave),
                FtueNotice.For(OutboxOutcome.Unavailable, FtueNotice.SubmitWave),
                FtueNotice.For(OutboxOutcome.Rejected, FtueNotice.SubmitWave),
            };

            Assert.AreEqual(string.Empty, FtueNotice.For(OutboxOutcome.Sent, FtueNotice.SubmitWave),
                "success has nothing to say");
            CollectionAssert.AllItemsAreNotNull(sentences);
            CollectionAssert.AllItemsAreUnique(sentences);
            foreach (var sentence in sentences) Assert.IsNotEmpty(sentence);
        }

        [Test]
        public void FtueNotice_PromisesARetryOnlyWhereTheOutboxActuallyQueues()
        {
            // The asymmetry is `OutboxClient`'s: wave/submit and
            // ftue/splice-stock queue while offline; splice/commit,
            // node/claim and creature/name do not, because the server is
            // authoritative for all three. A notice that promised "it will
            // be sent" for a name that was never persisted would be a lie
            // the player has no way to detect.
            StringAssert.Contains("will be sent",
                FtueNotice.For(OutboxOutcome.Queued, FtueNotice.SubmitWave));
            StringAssert.DoesNotContain("will be sent",
                FtueNotice.For(OutboxOutcome.Unavailable, FtueNotice.NameFounder));
        }

        [Test]
        public void FtueNotice_CarriesTheTwoSentencesTheSpliceBeatCanStopOn()
        {
            // Both were literals inside `FtueDirector`, which contradicts
            // this class's own reason for existing - sentences live here "so
            // they can be tested as sentences". Neither was tested.
            Assert.IsNotEmpty(FtueNotice.NoTutorialPair);
            Assert.IsNotEmpty(FtueNotice.FounderInTutorialPair);
            Assert.AreNotEqual(FtueNotice.NoTutorialPair, FtueNotice.FounderInTutorialPair,
                "two different reasons the splice cannot proceed must not read the same");

            // splice_confirm_spec 6 makes the Founder lockout the point of
            // the second one, so it says Founder.
            StringAssert.Contains("Founder", FtueNotice.FounderInTutorialPair);

            // And the first tells the player what to DO, which is the
            // difference between a notice and an apology.
            StringAssert.Contains("Refresh", FtueNotice.NoTutorialPair);
        }

        [Test]
        public void FtueNotice_NamesTheActionThatWasBlocked()
        {
            // "Something went wrong" is the sentence this exists to avoid.
            StringAssert.Contains(FtueNotice.SpliceCommit,
                FtueNotice.For(OutboxOutcome.Unavailable, FtueNotice.SpliceCommit));
            Assert.AreNotEqual(
                FtueNotice.For(OutboxOutcome.Unavailable, FtueNotice.SpliceCommit),
                FtueNotice.For(OutboxOutcome.Unavailable, FtueNotice.NameFounder));
        }
    }
}
