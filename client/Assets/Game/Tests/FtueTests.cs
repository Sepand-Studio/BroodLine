using System;
using System.Collections.Generic;
using Broodline.Api;
using Broodline.Model;
using NUnit.Framework;

namespace Broodline.Game.Tests
{
    /// The beat table of bible 9.2, as a pure function of the two responses
    /// a cold start already makes: `/v1/sync`'s snapshot and `/v1/roster`'s
    /// creatures.
    ///
    /// EVERY ROW HERE CONTRASTS. A table test whose every expected value is
    /// reachable by one constant is not a table - `Beat.ColdOpen` is the
    /// enum's zero, so `FreshAccount_IsTheColdOpen` alone would still pass
    /// against `Derive => default`. So the fresh-account row asserts the
    /// answer AND that clearing wave 1 moves off it, and each later row is
    /// written beside the input that produces the neighbouring beat.
    public class FtueTests
    {
        // ---------------------------------------------------------------
        // Fixtures
        // ---------------------------------------------------------------

        static PlayerSnapshot S(int cleared, bool founderNamed = false, bool stock = false, int splices = 0)
        {
            return new PlayerSnapshot
            {
                PlayerId = "p",
                ServerId = 1,
                HighestWaveCleared = cleared,
                Balances = new Dictionary<string, int>(),
                Tabs = new Dictionary<string, int>(),
                Ftue = new FtueFacts
                {
                    FounderNamed = founderNamed,
                    TutorialStockGranted = stock,
                    Splices = splices,
                },
            };
        }

        static CreatureDto C(string species)
        {
            return new CreatureDto
            {
                CreatureId = Guid.NewGuid(),
                Species = species,
                Generation = 1,
                Trait1 = "Chill", Tier1 = 1,
                Trait2 = "Taunt", Tier2 = 1,
                Instinct = "Forage",
                IsFounder = false,
            };
        }

        /// A Founder, named or not. `named` writes the NAME - the server's
        /// `ftue.founderNamed` marker is a separate fact and the tests that
        /// care set it on the snapshot, because the two disagreeing is a real
        /// state (naming is queued, the snapshot is stale) and `Derive` reads
        /// the snapshot's marker, not the creature's name.
        static CreatureDto Founder(string species, bool named = false)
        {
            var creature = C(species);
            creature.IsFounder = true;
            creature.Name = named ? species + " the named" : null;
            return creature;
        }

        static IReadOnlyList<CreatureDto> Roster(params CreatureDto[] creatures)
        {
            return creatures;
        }

        /// A roster whose CONTENTS cannot change the answer. Used only on
        /// rows past wave 2, where `Derive` reads the snapshot alone - and it
        /// deliberately carries an UNNAMED Founder, so a derivation that
        /// started consulting the roster again on those rows would answer
        /// `NameFounder` and fail rather than pass by luck on an empty list.
        static IReadOnlyList<CreatureDto> Any()
        {
            return Roster(C("Vetch"), C("Ember"), Founder("Hollow"));
        }

        // ---------------------------------------------------------------
        // The table - bible 9.2
        // ---------------------------------------------------------------

        [Test]
        public void FreshAccount_IsTheColdOpen_AndClearingWaveOneLeavesIt()
        {
            var roster = Roster(C("Vetch"), C("Ember"));

            Assert.AreEqual(Beat.ColdOpen, Ftue.Derive(S(0), roster));

            // The contrast that makes the line above mean something:
            // `ColdOpen` is `default(Beat)`, so without this a derivation
            // that answered one constant would satisfy the assertion.
            Assert.AreNotEqual(Beat.ColdOpen, Ftue.Derive(S(1), roster));
        }

        [Test]
        public void Wave1Cleared_UnnamedFounder_NamesIt()
        {
            Assert.AreEqual(Beat.NameFounder,
                Ftue.Derive(S(1), Roster(C("Vetch"), C("Ember"), Founder("Hollow", named: false))));
        }

        [Test]
        public void Wave1Cleared_NamedFounder_SecondWave()
        {
            Assert.AreEqual(Beat.SecondWave,
                Ftue.Derive(S(1, founderNamed: true),
                    Roster(C("Vetch"), C("Ember"), Founder("Hollow", named: true))));
        }

        [Test]
        public void Wave1Cleared_WithNoFounderInTheRosterYet_GoesToTheSecondWave()
        {
            // Beat 3 awards the creature beat 4 names. A wave-1 clear whose
            // grant has not reached this roster - a submission still in the
            // outbox, or a roster load that failed - has nobody to name, and
            // `FounderNamed` is false in that state exactly as it is in the
            // row above. The roster is what tells the two apart.
            Assert.AreEqual(Beat.SecondWave,
                Ftue.Derive(S(1), Roster(C("Vetch"), C("Ember"))));
        }

        [Test]
        public void Wave2Cleared_NoSplice_GuidedSplice()
        {
            Assert.AreEqual(Beat.GuidedSplice, Ftue.Derive(S(2, founderNamed: true), Any()));
        }

        [Test]
        public void Wave2Cleared_OneSplice_Lineage()
        {
            Assert.AreEqual(Beat.Lineage,
                Ftue.Derive(S(2, founderNamed: true, stock: true, splices: 1), Any()));
        }

        [Test]
        public void Wave6Cleared_Done()
        {
            Assert.AreEqual(Beat.Done,
                Ftue.Derive(S(6, founderNamed: true, stock: true, splices: 1), Any()));
        }

        [Test]
        public void TheFirstHourEndsAtWave6_NotBefore()
        {
            // The boundary itself, from both sides. `SessionTwoWave` is the
            // one number in this file that is a choice rather than a fact of
            // the table, and an off-by-one in it would leave every row above
            // green.
            Assert.AreEqual(Beat.Lineage,
                Ftue.Derive(S(Ftue.SessionTwoWave - 1, true, true, 1), Any()));
            Assert.AreEqual(Beat.Done,
                Ftue.Derive(S(Ftue.SessionTwoWave, true, true, 1), Any()));
        }

        [Test]
        public void SkippedNaming_IsStillNameFounderUntilWave2_ThenNeverAgain()
        {
            // bible 3.3: the skip path with a good default. Skipping does NOT
            // name the creature server-side, so `FounderNamed` stays false in
            // both halves below - what closes the beat is the wave, not the
            // name. The Roster is where renaming lives after.
            var unnamed = Roster(C("Vetch"), C("Ember"), Founder("Hollow", named: false));

            Assert.AreEqual(Beat.NameFounder, Ftue.Derive(S(1), unnamed));
            Assert.AreEqual(Beat.GuidedSplice, Ftue.Derive(S(2), unnamed));
        }

        // ---------------------------------------------------------------
        // Properties of the derivation itself
        // ---------------------------------------------------------------

        [Test]
        public void RevealIsNeverDerived_ItIsShownOffACommit()
        {
            // The class comment's central claim: nothing in a snapshot tells
            // "spliced a moment ago" from "spliced last week", so `Reveal`
            // has no derivable input and the director shows it directly.
            // Swept across every wave and splice count the table covers.
            for (var cleared = 0; cleared <= 8; cleared++)
            {
                foreach (var splices in new[] { 0, 1, 7 })
                {
                    foreach (var named in new[] { false, true })
                    {
                        Assert.AreNotEqual(Beat.Reveal,
                            Ftue.Derive(S(cleared, named, stock: true, splices: splices), Any()),
                            "cleared=" + cleared + " splices=" + splices + " named=" + named);
                    }
                }
            }
        }

        [Test]
        public void ASnapshotWithNoFtueBlock_ReadsAsFresh_RatherThanThrowing()
        {
            // `SnapshotStore`'s cache can hold a snapshot an older build
            // wrote. A cold start that throws here shows no screen at all,
            // so the missing block reads as the fresh account's block - which
            // means the guided splice is re-offered, and the server's
            // one-time stock grant is what refuses a second one.
            var stale = S(3, founderNamed: true, stock: true, splices: 2);
            stale.Ftue = null;

            Assert.AreEqual(Beat.GuidedSplice, Ftue.Derive(stale, Any()));
        }

        [Test]
        public void NullSnapshotOrRoster_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => Ftue.Derive(null, Any()));
            Assert.Throws<ArgumentNullException>(() => Ftue.Derive(S(0), null));
        }

        [Test]
        public void ANullEntryInTheRosterIsNotAFounder()
        {
            // Newtonsoft will deserialise a JSON `null` inside `creatures`
            // into a null element. Beat 4 must not be skipped or crashed by
            // one: the real Founder behind it still decides.
            Assert.AreEqual(Beat.NameFounder,
                Ftue.Derive(S(1), Roster(null, Founder("Hollow"))));
            Assert.AreEqual(Beat.SecondWave,
                Ftue.Derive(S(1), Roster(null, C("Vetch"))));
        }
    }
}
