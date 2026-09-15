using System;
using Broodline.Api;
using NUnit.Framework;

namespace Broodline.UI.Tests
{
    /// THE ASSERTIONS IN THIS FILE ARE NOT PLACEHOLDER.
    ///
    /// Layout, styling and art on the four screens are placeholder and a later
    /// phase owns them. The confirmation copy is not. broodline_splice_confirm
    /// _spec exists because the designed Splice Chamber ended on a Cost row and
    /// a "Begin Splice" CTA, with the player learning both parents were gone on
    /// the NEXT screen - named in that spec as the most likely source of refund
    /// requests and one-star reviews. Every assertion below is a sentence from
    /// splice_confirm_spec sections 3 and 4, or from sample_economy section 7's
    /// screen requirement, pinned as behaviour.
    ///
    /// Weakening any of these is weakening the product requirement, not the
    /// test - which is why each one names the spec line it stands for.
    public class SpliceConfirmTests
    {
        // ---------------------------------------------------------------
        // Fixtures
        // ---------------------------------------------------------------

        static CreatureDto Creature(
            string species, int generation, string name, bool founder,
            string trait1 = "Chill", int? tier1 = 1,
            string trait2 = "Guard", int? tier2 = 1)
        {
            return new CreatureDto
            {
                CreatureId = Guid.NewGuid(),
                Species = species,
                Generation = generation,
                Trait1 = trait1,
                Tier1 = tier1,
                Trait2 = trait2,
                Tier2 = tier2,
                Instinct = "Forage",
                Name = name,
                IsFounder = founder,
                CommittedTo = null,
            };
        }

        /// A NAMED creature is a Founder, and that is the schema talking, not
        /// this fixture: drizzle 0005's `only_founders_named` check refuses a
        /// named non-Founder outright (see services/api/src/roster/creatures.ts,
        /// "Founders are granted deliberately and named; base stock is
        /// neither"). So "the player's chosen name, where one exists" is today
        /// exactly the Founder set. The screen code still resolves name-then-
        /// species generically, so relaxing that constraint later needs no UI
        /// change - but a fixture that invented a named non-Founder would be
        /// testing a row the server cannot produce.
        static CreatureDto Named(string name) => Creature("Vetch Crawler", 4, name, founder: true);

        static CreatureDto Founder(string name) => Named(name);

        static CreatureDto Unnamed(string species, int generation)
            => Creature(species, generation, name: null, founder: false);

        /// The forecast and the coverage warning are the SERVER's, fetched from
        /// POST /v1/splice/preview. The client does not re-derive either: the
        /// route itself says so at services/api/src/routes/splice.ts - "the
        /// client must not be the thing that works out which coverage is at
        /// stake" - and the server's distribution is the only published one.
        static SplicePreviewResponse Preview()
        {
            var r = new SplicePreviewResponse();
            r.Forecast = new SpliceForecast { Mutation = 0.09, Aberrant = 0.01 };
            r.Forecast.Combat2.Add(new CombatOutcomeDto { Trait = "Guard", Tier = 1, P = 0.55 });
            r.Forecast.Combat2.Add(new CombatOutcomeDto { Trait = "Ward", Tier = 2, P = 0.45 });
            r.Forecast.Instinct.Add(new Instinct { Instinct1 = "Forage", P = 1.0 });
            return r;
        }

        /// A preview whose `coverageLost` names one trait. The server returns
        /// this list; the screen renders it.
        static SplicePreviewResponse PreviewLosing(string trait, int? tier)
        {
            var r = Preview();
            r.CoverageLost.Add(new CoverageLost { Trait = trait, Tier = tier });
            return r;
        }

        static CreatureDto A() => Unnamed("Vetch Crawler", 4);
        static CreatureDto B() => Unnamed("Ember Skitter", 6);

        // ---------------------------------------------------------------
        // splice_confirm_spec section 3 - the destruction notice
        // ---------------------------------------------------------------

        [Test]
        public void DestructionNotice_NamesTheCreaturesAndSaysTheRecordSurvives()
        {
            var vm = SpliceScreen.Build(Named("Ash"), Unnamed("Ember Skitter", 6), Preview());

            // Section 3: names the ACTUAL creatures, using the player's name
            // where one exists - not "the selected parents".
            StringAssert.Contains("Ash", vm.DestructionNotice);
            StringAssert.Contains("Ember", vm.DestructionNotice);

            // The second line matters as much as the first: it is the
            // reassurance that makes the loss survivable, and it is true.
            StringAssert.Contains("lineage", vm.DestructionNotice);

            // And it must not fall back to the phrasing the spec rejects.
            StringAssert.DoesNotContain("the selected parents", vm.DestructionNotice);
        }

        [Test]
        public void DestructionNotice_UsesTheSpeciesWhenNoNameWasChosen()
        {
            // The name half of the rule is only meaningful if the fallback is
            // the species rather than a placeholder.
            var vm = SpliceScreen.Build(A(), B(), Preview());

            StringAssert.Contains("Vetch Crawler", vm.DestructionNotice);
            StringAssert.Contains("Ember Skitter", vm.DestructionNotice);
        }

        [Test]
        public void Cta_StatesItsCost()
        {
            // Section 3: "a player who taps past everything else still reads
            // the thing they tap." Not "Begin Splice".
            StringAssert.Contains("consumes both parents", SpliceScreen.Build(A(), B(), Preview()).CtaLabel);
        }

        [Test]
        public void Cta_IsNotTheLabelTheSpecExistsToReplace()
        {
            // The CTA the shipped design ended on, named in section 1 as the
            // gap. Pinned separately so reverting the label reddens a test
            // whose name says what was lost.
            Assert.AreNotEqual("Begin Splice", SpliceScreen.Build(A(), B(), Preview()).CtaLabel);
        }

        [Test]
        public void StandardDialog_NamesBothCreaturesAndIsNotSuppressible()
        {
            // Section 4, Standard. Same language as the notice: "three
            // different phrasings of the same fact reads as evasion."
            var vm = SpliceScreen.Build(Named("Ash"), B(), Preview());

            StringAssert.Contains("Ash", vm.StandardDialog.Body);
            StringAssert.Contains("Ember Skitter", vm.StandardDialog.Body);
            Assert.IsFalse(vm.StandardDialog.Suppressible);
        }

        // ---------------------------------------------------------------
        // splice_confirm_spec section 4 - the Founder interrupt
        // ---------------------------------------------------------------

        [Test]
        public void FounderConsumption_RaisesASecondDialogNamingTheFounder()
        {
            var vm = SpliceScreen.Build(Founder("Ash"), B(), Preview());

            Assert.IsTrue(vm.RequiresFounderConfirm);

            // Section 4: the NAME, not the species. "Consume Ash" stops a
            // player; "Consume Vetch Crawler" does not.
            StringAssert.Contains("Ash", vm.FounderDialog.Title);
            Assert.AreEqual("Consume Ash", vm.FounderDialog.ConfirmLabel);
            Assert.AreEqual("Keep Ash", vm.FounderDialog.CancelLabel);

            // No destructive default, and never suppressible.
            Assert.IsFalse(vm.FounderDialog.ConfirmIsPrimary);
            Assert.IsFalse(vm.FounderDialog.Suppressible);
        }

        [Test]
        public void FounderDialog_ComesSecond_AfterTheStandardOne()
        {
            // Section 4: "Second dialog, AFTER the standard one." The order is
            // the requirement - a Founder dialog shown first would be the
            // interrupt arriving before the thing it interrupts.
            var vm = SpliceScreen.Build(Founder("Ash"), B(), Preview());

            Assert.AreEqual(2, vm.Dialogs.Count);
            Assert.AreSame(vm.StandardDialog, vm.Dialogs[0]);
            Assert.AreSame(vm.FounderDialog, vm.Dialogs[1]);
        }

        [Test]
        public void NoFounder_RaisesOnlyTheStandardDialog()
        {
            var vm = SpliceScreen.Build(A(), B(), Preview());

            Assert.IsFalse(vm.RequiresFounderConfirm);
            Assert.IsNull(vm.FounderDialog);
            Assert.AreEqual(1, vm.Dialogs.Count);
        }

        [Test]
        public void TwoFounders_RaiseADialogEach_NeitherCollapsed()
        {
            // Both parents being Founders is reachable, and collapsing the two
            // into one dialog would drop a name the spec makes load-bearing.
            var vm = SpliceScreen.Build(Founder("Ash"), Founder("Bramble"), Preview());

            Assert.AreEqual(2, vm.FounderDialogs.Count);
            Assert.AreEqual("Consume Ash", vm.FounderDialogs[0].ConfirmLabel);
            Assert.AreEqual("Consume Bramble", vm.FounderDialogs[1].ConfirmLabel);
            Assert.AreEqual(3, vm.Dialogs.Count);
        }

        [Test]
        public void AFounderWithNoName_StillRaisesTheInterrupt()
        {
            // `isFounder` and `name` are separate fields on the wire, so an
            // unnamed Founder is representable even though today's grant path
            // names every one it creates. The weak version of this dialog is
            // "Consume Vetch Crawler", which section 4 says does not stop a
            // player - but SKIPPING it entirely is the dangerous failure, not
            // the weak wording. So it is raised either way.
            var unnamedFounder = Creature("Vetch Crawler", 4, name: null, founder: true);
            var vm = SpliceScreen.Build(unnamedFounder, B(), Preview());

            Assert.IsTrue(vm.RequiresFounderConfirm);
            Assert.AreEqual("Consume Vetch Crawler", vm.FounderDialog.ConfirmLabel);
            Assert.IsFalse(vm.FounderDialog.Suppressible);
        }

        // ---------------------------------------------------------------
        // sample_economy section 7 - the coverage that will not carry
        // ---------------------------------------------------------------

        [Test]
        public void CoverageThatWillNotCarry_IsNamedByTraitAndTier()
        {
            // sample_economy section 7's screen requirement. A player who
            // discovers after the fact that they destroyed nine tier-I
            // equivalents reads it as the game hiding a cost.
            var vm = SpliceScreen.Build(A(), B(), PreviewLosing("Chill", 3));

            StringAssert.Contains("Chill III will not carry", vm.CoverageWarning);
        }

        [Test]
        public void CoverageWarning_IsSilentWhenTheServerNamesNothingLost()
        {
            // The server names only what cannot carry under ANY outcome it
            // forecast (splice/distribution.ts's `coverageLost`), which is
            // empty for four distinct traits. A warning shown then would be
            // false, and a warning that is false half the time trains
            // dismissal - section 7's "no 'are you sure?' without content".
            var vm = SpliceScreen.Build(A(), B(), Preview());

            Assert.IsEmpty(vm.CoverageWarning);
        }

        [Test]
        public void CoverageWarning_NamesEveryTraitTheServerListed()
        {
            var preview = PreviewLosing("Chill", 3);
            preview.CoverageLost.Add(new CoverageLost { Trait = "Guard", Tier = 1 });

            var vm = SpliceScreen.Build(A(), B(), preview);

            StringAssert.Contains("Chill III", vm.CoverageWarning);
            StringAssert.Contains("Guard I", vm.CoverageWarning);
            StringAssert.Contains("will not carry", vm.CoverageWarning);
        }

        // ---------------------------------------------------------------
        // splice_confirm_spec section 2 - odds before the charge
        // ---------------------------------------------------------------

        [Test]
        public void Forecast_IsShownBeforeTheChargeIsSpent()
        {
            var vm = SpliceScreen.Build(A(), B(), Preview());

            Assert.IsNotEmpty(vm.Forecast.Combat2);
            Assert.IsFalse(vm.ChargeSpent);
        }

        [Test]
        public void ChargeSpent_BecomesTrueOnlyAfterCommit()
        {
            // Without this, `IsFalse(vm.ChargeSpent)` above passes against a
            // field that is false by construction and asserts nothing. The
            // preview route writes nothing and the charge is spent at commit -
            // routes/splice.ts, "neither costs the player anything".
            var vm = SpliceScreen.Build(A(), B(), Preview());
            Assert.IsFalse(vm.ChargeSpent);

            var after = SpliceScreen.AfterCommit(vm, new SpliceCommitResponse
            {
                Child = Unnamed("Vetch Crawler", 5),
                SpliceId = Guid.NewGuid(),
                Seed = "0",
                Balance = 3,
            });

            Assert.IsTrue(after.ChargeSpent);
            Assert.AreEqual(3, after.ChargesRemaining);
        }

        [Test]
        public void TheForecastIsTheServersOwn_NotRebuiltByTheScreen()
        {
            // The server's distribution is the only published source of these
            // odds, so the screen hands back the response object itself rather
            // than a copy it could drift from. Broodline.UI does not reference
            // Broodline.Sim for the same reason.
            var preview = Preview();
            var vm = SpliceScreen.Build(A(), B(), preview);

            Assert.AreSame(preview.Forecast, vm.Forecast);
            Assert.AreEqual(0.09, vm.Forecast.Mutation, 1e-9);
            Assert.AreEqual(0.01, vm.Forecast.Aberrant, 1e-9);
        }

        // ---------------------------------------------------------------
        // The screen cannot be built out of a state the server refuses
        // ---------------------------------------------------------------

        [Test]
        public void TheSameCreatureTwice_IsNotASplice()
        {
            // /v1/splice/preview refuses `parentA === parentB` as
            // invalid_request. A screen that can express it sends a request
            // that can only be refused.
            var a = A();
            Assert.Throws<ArgumentException>(() => SpliceScreen.Build(a, a, Preview()));
        }

        [Test]
        public void ABuildWithNoPreview_IsRefused()
        {
            // No preview means no published odds, and section 2 makes showing
            // them before the charge non-negotiable.
            Assert.Throws<ArgumentNullException>(() => SpliceScreen.Build(A(), B(), null));
        }
    }
}
