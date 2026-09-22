using System;
using System.Collections.Generic;
using Broodline.Api;
using Broodline.Model;
using Broodline.UI.Components;
using Broodline.UI.Screens;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Broodline.UI.Tests
{
    /// The three wave screens: the live HUD, Post-Wave and Wave Defeat.
    ///
    /// The property this file stands for is the one the Task 15 review found
    /// missing: **the view renders the model's sentence, and the sentence is
    /// not in the view.** So most assertions below come in pairs - one that a
    /// specific fact reaches the screen (which is what the bible asks for),
    /// and one that a DIFFERENT fact produces a DIFFERENT screen. A view
    /// holding a literal passes the first and fails the second, and that is
    /// the only difference a test can actually see.
    ///
    /// NOTE ON ASSEMBLIES. `WaveReport`, `BreachSummary` and `HudSnapshot`
    /// are `Broodline.Model` types, so this assembly gained a reference to
    /// `Broodline.Model` (it had `Broodline.UI` and `Generated.Api` only).
    /// It did NOT gain `Broodline.Sim` and must never: the whole point of the
    /// report being plain data is that no view and no view test needs an
    /// engine to render a defeat.
    ///
    /// The constraint `ScreenBindingTests` documents applies here too: a bare
    /// `new SomeView()` has no attached `Panel`, so no `ClickEvent` can be
    /// dispatched and no scheduled item runs. Click wiring is trusted by
    /// inspection; what is proven is the structural precondition - the right
    /// button, in the right enabled state - and, for the HUD, that a redraw
    /// is reachable without a panel at all.
    public class WaveScreensTests
    {
        // ---------------------------------------------------------------
        // Fixtures
        // ---------------------------------------------------------------

        static CreatureDto Creature(string species, string name = null, bool founder = false,
            string trait1 = "Chill", int? tier1 = 1, string trait2 = "Guard", int? tier2 = 1)
        {
            return new CreatureDto
            {
                CreatureId = Guid.NewGuid(),
                Species = species,
                Generation = 1,
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

        static BreachSummary Breach(string raider, string counter,
            bool access = false, bool coverage = false, bool placement = false)
        {
            return new BreachSummary
            {
                RaiderType = raider,
                Counter = counter,
                Access = access,
                Coverage = coverage,
                Placement = placement,
            };
        }

        static WaveReport LossWith(params BreachSummary[] breaches)
        {
            return new WaveReport
            {
                Result = "Loss",
                Ticks = 540,
                IntegrityRemaining = 0,
                ReplayBytes = new byte[] { 1, 2, 3 },
                Breaches = breaches,
            };
        }

        static WaveDefeatView BoundDefeat(WaveReport report, params CreatureDto[] granted)
        {
            var view = new WaveDefeatView();
            view.Bind(report, granted, retry: () => { });
            return view;
        }

        static PostWaveView BoundPostWave(WaveSubmitResponse response, params CreatureDto[] granted)
        {
            var view = new PostWaveView();
            view.Bind(response, granted, next: () => { });
            return view;
        }

        static WaveSubmitResponse Submitted(string result, string currency, int amount)
        {
            return new WaveSubmitResponse
            {
                Result = result,
                IntegrityRemaining = 1,
                Reward = new Reward { Currency = currency, Amount = amount },
            };
        }

        // ---------------------------------------------------------------
        // Wave Defeat - screen_inventory_v2 10: "the single most important
        // teaching screen in the game."
        // ---------------------------------------------------------------

        [Test]
        public void WaveDefeat_NamesTheRaiderAndTheTrait_AndShowsTheResupply()
        {
            // bible 4.11: "Wave Defeat names the raider that broke through
            // and the trait that would have answered it, then offers a free
            // retry." waves_01_12 section 3 makes wave 6's resupply a Pale.
            var view = BoundDefeat(LossWith(Breach("Courser", "Chill")), Creature("Pale"));

            StringAssert.Contains("Courser", view.Q<Label>("headline").text);
            StringAssert.Contains("Chill", view.Q<Label>("headline").text);
            StringAssert.Contains("Pale", view.Q<Label>("granted").text);
            Assert.IsTrue(view.Q<Button>("retry").enabledSelf, "bible 4.11: free retry, no paywall on failure");
        }

        [Test]
        public void WaveDefeat_ADifferentRaiderAndTrait_ProducesADifferentHeadline()
        {
            // WITHOUT THIS, the test above is satisfied by a headline
            // hardcoded to wave 6's sentence - which is exactly the failure
            // the Task 15 review found three times in one file.
            var view = BoundDefeat(LossWith(Breach("Lash", "Taunt")), Creature("Vetch Crawler"));
            var headline = view.Q<Label>("headline").text;

            StringAssert.Contains("Lash", headline);
            StringAssert.Contains("Taunt", headline);
            StringAssert.DoesNotContain("Courser", headline);
            StringAssert.DoesNotContain("Chill", headline);
        }

        [Test]
        public void WaveDefeat_TheHeadlineAndResupplyAreTheModelsSentencesVerbatim()
        {
            // The positive form of the same constraint: not merely that the
            // right words appear, but that the STRING is the one
            // `WaveDefeatScreen` authors - so a view that composed its own
            // sentence out of the same nouns still fails.
            var breach = Breach("Courser", "Chill");
            var pale = Creature("Pale");
            var view = BoundDefeat(LossWith(breach), pale);

            Assert.AreEqual(WaveDefeatScreen.Headline(breach), view.Q<Label>("headline").text);
            Assert.AreEqual(WaveDefeatScreen.Diagnosis(breach), view.Q<Label>("diagnosis").text);
            Assert.AreEqual(WaveDefeatScreen.Resupply(new[] { pale }), view.Q<Label>("granted").text);
            Assert.AreEqual(WaveDefeatScreen.RetryLabel, view.Q<Button>("retry").text);
        }

        [Test]
        public void WaveDefeat_ABreachTheBundleAnswersWithNothing_NamesTheRaiderAndNoTrait()
        {
            // `WaveReportBuilder` writes an empty counter when the bundle's
            // trait table names no answer. The screen must not invent one -
            // a wrong trait name on the teaching screen teaches the wrong
            // rule.
            var view = BoundDefeat(LossWith(Breach("Courser", string.Empty)));

            StringAssert.Contains("Courser", view.Q<Label>("headline").text);
            Assert.AreEqual(string.Empty, view.Q<Label>("diagnosis").text);
        }

        [Test]
        public void WaveDefeat_TheDiagnosisFollowsWhichOfTheThreeBooleansFailed()
        {
            // combat_engine section 7's three messages: the trait was absent,
            // present at insufficient coverage, or misplaced. Three inputs,
            // three DISTINCT outputs - asserted pairwise, because a view (or
            // a formatter) that returned one sentence for all three would
            // otherwise pass every individual check.
            var absent = BoundDefeat(LossWith(Breach("Courser", "Chill")))
                .Q<Label>("diagnosis").text;
            var thin = BoundDefeat(LossWith(Breach("Courser", "Chill", access: true)))
                .Q<Label>("diagnosis").text;
            var misplaced = BoundDefeat(LossWith(Breach("Courser", "Chill", access: true, coverage: true)))
                .Q<Label>("diagnosis").text;

            Assert.AreNotEqual(absent, thin);
            Assert.AreNotEqual(thin, misplaced);
            Assert.AreNotEqual(absent, misplaced);
            foreach (var line in new[] { absent, thin, misplaced })
                StringAssert.Contains("Chill", line);
        }

        [Test]
        public void WaveDefeat_ShowsOneCardPerGrantedCreature_NamedAfterIt()
        {
            // `RosterView`'s rule, for the same reason: the right NUMBER of
            // arrivals with the wrong creatures is bible 9.3's resupply
            // failing silently.
            var pale = Creature("Pale");
            var ember = Creature("Ember Skitter");
            var view = BoundDefeat(LossWith(Breach("Courser", "Chill")), pale, ember);

            Assert.AreEqual(2, view.Query<CreatureCard>().ToList().Count);
            Assert.IsNotNull(view.Q<CreatureCard>(pale.CreatureId.ToString()));
            Assert.IsNotNull(view.Q<CreatureCard>(ember.CreatureId.ToString()));
        }

        [Test]
        public void WaveDefeat_NoResupply_ShowsNoCardsAndNoResupplyLine()
        {
            // The complement: a screen that always drew a card, or always
            // claimed something arrived, would pass every test above.
            var view = BoundDefeat(LossWith(Breach("Courser", "Chill")));

            Assert.AreEqual(0, view.Query<CreatureCard>().ToList().Count);
            Assert.AreEqual(string.Empty, view.Q<Label>("granted").text);
        }

        [Test]
        public void WaveDefeat_RetryIsEnabledEvenWithNoResupplyAndNoDiagnosis()
        {
            // bible 4.11: "Free retry. No paywall on failure, ever." There is
            // no state that disables it, so the worst state is the one worth
            // asserting.
            var view = BoundDefeat(new WaveReport
            {
                Result = "Loss",
                Breaches = new BreachSummary[0],
            });

            Assert.IsTrue(view.Q<Button>("retry").enabledSelf);
            Assert.AreEqual(string.Empty, view.Q<Label>("headline").text);
        }

        [Test]
        public void WaveDefeat_ANullBreachListIsSurvived_NotAssumedAway()
        {
            // `WaveReport.Breaches` is plain data with a public field, so
            // "never null" is a convention `WaveReportBuilder` keeps rather
            // than an invariant the type enforces - and this assembly can
            // construct a counterexample in one line, which is precisely why
            // the view null-checks. This is that counterexample.
            var view = BoundDefeat(new WaveReport { Result = "Loss", Breaches = null });

            Assert.AreEqual(string.Empty, view.Q<Label>("headline").text);
            Assert.AreEqual(string.Empty, view.Q<Label>("diagnosis").text);
            Assert.IsTrue(view.Q<Button>("retry").enabledSelf);
        }

        /// THE FIFTH BLANK TINTED PANEL THIS PHASE HAS FOUND, and the fourth
        /// task to find one. `Diagnosis` returns the empty string whenever the
        /// bundle names no counter for the raider, and the surface around it
        /// existed unconditionally - so the defeat with nothing to teach still
        /// drew a padded, bordered white card with nothing inside it. Both
        /// directions, because a view that hid the card unconditionally would
        /// pass the first half by dropping the sentence.
        [Test]
        public void WaveDefeat_WithNoDiagnosis_CollapsesTheCardRatherThanDrawingItEmpty()
        {
            var silent = BoundDefeat(LossWith(Breach("Courser", string.Empty)));
            Assert.AreEqual(string.Empty, silent.Q<Label>("diagnosis").text);
            Assert.AreEqual(DisplayStyle.None,
                silent.Q<SectionCard>("diagnosis-card").resolvedStyle.display,
                "a defeat the bundle has no counter for drew an empty card");

            var teaching = BoundDefeat(LossWith(Breach("Courser", "Chill")));
            Assert.IsNotEmpty(teaching.Q<Label>("diagnosis").text);
            Assert.AreEqual(DisplayStyle.Flex,
                teaching.Q<SectionCard>("diagnosis-card").resolvedStyle.display,
                "the screen's whole reason for existing was hidden");
        }

        /// The same shape one element out: the resupply SENTENCE and the cards
        /// under it are one announcement, so they appear and disappear
        /// together. `WaveDefeatScreen.Resupply` is why there is no EmptyState
        /// here - "an empty grant says nothing rather than promising a
        /// resupply that is not there" - and a block that stayed on screen
        /// saying nothing is that promise made in --green-tint.
        [Test]
        public void WaveDefeat_WithNoResupply_CollapsesTheWholeBlock()
        {
            var bare = BoundDefeat(LossWith(Breach("Courser", "Chill")));
            Assert.AreEqual(DisplayStyle.None, bare.Q<VisualElement>("resupply").resolvedStyle.display);

            var resupplied = BoundDefeat(LossWith(Breach("Courser", "Chill")), Creature("Pale"));
            Assert.AreEqual(DisplayStyle.Flex, resupplied.Q<VisualElement>("resupply").resolvedStyle.display);
        }

        /// THE SECONDARY CTA IS DRAWN ONLY WHEN A CALLER HAS SOMEWHERE FOR IT
        /// TO GO, which is `ScreenScaffold.OnBack`'s rule applied to a button:
        /// a player taps a control that does nothing, gets no error and no
        /// transition, and reads the app as broken rather than as busy.
        /// Nothing passes `roster` today - `FtueDirector` binds `retry` alone -
        /// so the screen renders one CTA, which is what it rendered before
        /// Task 11 added the second.
        [Test]
        public void WaveDefeat_TheRosterCtaAppearsOnlyWhenItHasSomewhereToGo()
        {
            var alone = BoundDefeat(LossWith(Breach("Courser", "Chill")));
            Assert.AreEqual(DisplayStyle.None, alone.Q<Button>("roster").resolvedStyle.display);

            var offered = new WaveDefeatView();
            offered.Bind(LossWith(Breach("Courser", "Chill")), new CreatureDto[0],
                retry: () => { }, roster: () => { });

            var roster = offered.Q<Button>("roster");
            Assert.AreEqual(DisplayStyle.Flex, roster.resolvedStyle.display);
            Assert.AreEqual(WaveDefeatScreen.RosterLabel, roster.text);
        }

        /// The header names the SCREEN and the headline says what happened -
        /// `FounderNamingScreen.Title`'s rule, which is why the title is not
        /// "Wave lost". Asserted against the model rather than a literal, so
        /// moving the string back into the view still fails.
        /// THIS USED TO ASSERT THE HEADER SAID "Wave Defeat" AND NOW ASSERTS
        /// THERE IS NO HEADER, and the handoff is why rather than taste:
        /// `Wave Defeat.dc.html` runs status bar (`:26`) straight into the
        /// band (`:31`) with no header row between them. `SpliceRevealView`
        /// made the same move in Task 16b on the same evidence, and
        /// `ScaffoldTests.EveryTitledScreenStillDrawsItsPageHeader` is where
        /// this screen's name moved from one list to the other.
        ///
        /// THE KICKER HAD TO GO SOMEWHERE, which is the half that is easy to
        /// lose: `#header` is also where the eyebrow lives, so hiding it takes
        /// the kicker with it. It is in the band now, which is where `:40`
        /// draws it, and that is asserted here rather than left to the
        /// capture - an eyebrow that silently stopped rendering would look
        /// exactly like a screen that never had one.
        [Test]
        public void WaveDefeat_DrawsNoPageHeader_AndCarriesItsKickerInTheBand()
        {
            var view = new WaveDefeatView();
            view.Bind(LossWith(Breach("Courser", "Chill")), null, retry: () => { }, wave: 6);

            Assert.IsNotNull(view.Q(className: ScreenScaffold.UssClassName), "no scaffold");
            Assert.AreEqual(DisplayStyle.None, view.Q<VisualElement>("header").style.display.value,
                "the handoff draws no page header on this screen");

            // NAMED `kicker`, NOT `eyebrow`: the scaffold owns a Label called
            // `eyebrow` inside the header it just hid, and a hidden element is
            // still an element `Q` walks - earlier in the traversal than this
            // one. The UXML's own note has it.
            var kicker = view.Q<Label>("kicker");
            Assert.AreEqual(WaveDefeatScreen.Eyebrow(6), kicker.text);
            Assert.AreSame(view.Q<HeroBand>().Subject, kicker.parent,
                "the kicker is not in the band, so hiding the header took it with it");
            Assert.AreNotEqual(kicker, view.Q<Label>("eyebrow"),
                "the band's kicker answers to `eyebrow`, which is the scaffold's own name for "
                + "the Label inside the header this screen hides");

            // A DIFFERENT WAVE PRODUCES A DIFFERENT KICKER, which is what
            // separates a screen reading its argument from one holding wave 6.
            var other = new WaveDefeatView();
            other.Bind(LossWith(Breach("Courser", "Chill")), null, retry: () => { }, wave: 9);
            Assert.AreNotEqual(kicker.text, other.Q<Label>("kicker").text);

            // And no wave at all collapses it rather than printing a hole.
            Assert.AreEqual(DisplayStyle.None,
                BoundDefeat(LossWith(Breach("Courser", "Chill"))).Q<Label>("kicker").style.display.value);
        }

        /// `Wave Defeat.dc.html:31` IS A HERO BAND AND THIS IS THE ASSERTION
        /// THAT SAYS SO. Its ramp, its `border-radius: 26px` and its
        /// `box-shadow: 0 4px 16px` are `HeroBand`'s three defining values in a
        /// tint the component could not draw until Phase 9 Task 18.
        ///
        /// THE TINT IS THE COMPONENT'S AND NOT THIS SCREEN'S, which is the
        /// thing actually worth pinning. The forbidden fix was a rule in
        /// `WaveDefeatView.uss` reaching into `.hero-band__surface` to
        /// recolour it, and that fix would leave every assertion about
        /// structure green while the next screen that wants a tint
        /// rediscovers the whole problem. `ComponentTests
        /// .TheCoralTintSwapsBothTheRampAndTheFillItDegradesTo` reads the rule
        /// itself; this reads that the screen asked the component for it.
        [Test]
        public void WaveDefeat_ComposesTheHandoffsCoralHeroBand_TintedByTheComponent()
        {
            var view = BoundDefeat(LossWith(Breach("Courser", "Chill")));

            var band = view.Q<HeroBand>();
            Assert.IsNotNull(band, "the defeat screen draws no HeroBand");
            Assert.AreEqual(HeroBand.Tint.Coral, band.Tinted,
                "the band is violet; `Wave Defeat.dc.html:31` is #fbeee9 -> #f6e2e4");
            Assert.IsTrue(band.ClassListContains(HeroBand.ElevationUssClassName),
                "the band lost its elevation; the handoff draws `0 4px 16px`, the .elev-2 half of "
                + "the 2:1 pair, where the card under it is `0 2px 8px`");

            // RINGLESS. There is no dashed ring anywhere on this screen, and a
            // ring-bearing band would also halo its subject - which is a
            // 264px ornament behind four lines of text.
            Assert.IsNull(band.Q<VisualElement>("ring"),
                "the coral band drew the dashed ring; the handoff has none");

            // THE SCREEN DOES NOT RECOLOUR THE SURFACE ITSELF, asserted from
            // the stylesheet because an inline-style read cannot see a rule.
            var path = System.IO.Path.GetFullPath(System.IO.Path.Combine(
                UnityEngine.Application.dataPath, "UI/Screens/Resources/WaveDefeatView.uss"));
            var sheet = System.IO.File.ReadAllText(path);
            StringAssert.DoesNotContain("hero-band__surface", sheet,
                "this screen reaches into the band's surface from its own sheet, which is the one "
                + "fix HeroBand's tint hook exists to make unnecessary");
        }

        /// `Wave Defeat.dc.html:44-55` - three cells inside the band.
        [Test]
        public void WaveDefeat_StatesTheThreeCellsTheHandoffDraws()
        {
            var view = new WaveDefeatView();
            view.Bind(LossWith(Breach("Courser", "Chill"), Breach("Lash", "Taunt")),
                      null, retry: () => { }, wave: 6, deployed: 5);

            Assert.AreEqual(3, view.Query<StatCell>().ToList().Count);
            Assert.AreEqual("2", ValueOf(view.Q<StatCell>("leaked")),
                "LEAKED is the breach list's own length");
            Assert.AreEqual("0", ValueOf(view.Q<StatCell>("integrity")));
            Assert.AreEqual("5", ValueOf(view.Q<StatCell>("kept")));

            // THE CELLS ARE IN THE BAND, not under it. `:43` puts them inside
            // the coral panel; a row that fell out of it would still show the
            // right three numbers.
            Assert.AreSame(view.Q<HeroBand>().Subject,
                view.Q<VisualElement>("stats").parent,
                "the stat row is not inside the band");

            // A CALLER THAT DOES NOT KNOW THE DEPLOYMENT SAYS NOTHING, rather
            // than claiming nothing came home on the screen whose own sentence
            // is that the player's hybrids are intact.
            Assert.AreEqual(string.Empty,
                ValueOf(BoundDefeat(LossWith(Breach("Courser", "Chill"))).Q<StatCell>("kept")));
        }

        /// bible 9.3's teaching, as an element rather than as a word in a
        /// sentence: the trait that answers the raider, tinted by the species
        /// that just joined the roster and tiered at the coverage it brings.
        [Test]
        public void WaveDefeat_NamesTheCounterInAChipTintedByTheCreatureThatBringsIt()
        {
            var pale = Creature("Pale", trait1: "Chill", tier1: 2, trait2: "Guard", tier2: 1);
            var view = BoundDefeat(LossWith(Breach("Courser", "Chill")), pale);

            var chip = view.Q<TraitChip>("counter");
            Assert.IsNotNull(chip, "the defeat screen names no counter chip");
            Assert.AreEqual(CreatureLabel.TraitWithTier("Chill", 2), chip.Q<Label>("trait").text,
                "the chip did not take the tier off the creature that carries the trait");
            Assert.IsTrue(chip.ClassListContains(TraitChip.UssClassName + "--pale"),
                "the chip is not tinted by the species that brings it");
            Assert.IsFalse(chip.ClassListContains(TraitChip.AberrantUssClassName),
                "a tiered counter drew the Aberrant outline");

            // A DIFFERENT BREACH NAMES A DIFFERENT TRAIT, which is what
            // separates a chip reading the bundle from one holding "Chill".
            var taunt = BoundDefeat(LossWith(Breach("Lash", "Taunt")), pale);
            StringAssert.Contains("Taunt", taunt.Q<TraitChip>("counter").Q<Label>("trait").text);
            Assert.IsTrue(taunt.Q<TraitChip>("counter").ClassListContains(
                    TraitChip.AberrantUssClassName),
                "nothing granted carries Taunt, so the chip has no tier to show - which is the "
                + "fallback WaveDefeatView.Bind records as the one thing it overstates");

            // AND A BREACH THE BUNDLE ANSWERS WITH NOTHING DRAWS NO CHIP AT
            // ALL, rather than an empty one. `WaveDefeatScreen.Headline` makes
            // the same call on the same case for the sentence.
            Assert.IsNull(BoundDefeat(LossWith(Breach("Courser", null)), pale)
                .Q<TraitChip>("counter"));
        }

        /// The verdict is a second Label, not the headline renamed.
        [Test]
        public void WaveDefeat_DrawsTheVerdictAboveTheSentenceAndTheyAreNotTheSameLine()
        {
            var view = BoundDefeat(LossWith(Breach("Courser", "Chill")));

            var verdict = view.Q<Label>("verdict");
            Assert.AreEqual(WaveDefeatScreen.Verdict, verdict.text);
            Assert.IsTrue(verdict.ClassListContains("t-hero"), "the verdict is not the hero line");
            Assert.IsTrue(verdict.ClassListContains("t-danger"), "the verdict is not coral");

            // THE SENTENCE SURVIVED, which is the half a rename would have
            // eaten: bible 4.11 makes "names the raider that broke through and
            // the trait that would have answered it" the reason this screen
            // exists, and it is still its own Label with its own text.
            Assert.AreNotEqual(verdict.text, view.Q<Label>("headline").text);
            StringAssert.Contains("Courser", view.Q<Label>("headline").text);
        }

        // ---------------------------------------------------------------
        // Post-Wave
        // ---------------------------------------------------------------

        [Test]
        public void PostWave_ShowsTheRewardAndEveryCreatureThatArrived()
        {
            var hollow = Creature("Hollow", founder: true);
            var view = BoundPostWave(Submitted("Win", "shards", 150), hollow);

            StringAssert.Contains("150", RewardOf(view));
            Assert.AreEqual(1, view.Query<CreatureCard>().ToList().Count);
        }

        [Test]
        public void PostWave_ADifferentReward_ProducesADifferentLine()
        {
            // Kills a hardcoded "150 shards", which the test above cannot.
            var reward = RewardOf(BoundPostWave(Submitted("Win", "husks", 40)));

            StringAssert.Contains("40", reward);
            StringAssert.Contains("husks", reward);
            StringAssert.DoesNotContain("150", reward);
            StringAssert.DoesNotContain("shards", reward);
        }

        [Test]
        public void PostWave_TheRewardLineAndCtaAreTheModelsVerbatim()
        {
            var response = Submitted("Win", "shards", 150);
            var view = BoundPostWave(response);

            Assert.AreEqual(PostWaveScreen.RewardLine(response.Reward), RewardOf(view));
            Assert.AreEqual(PostWaveScreen.NextLabel, view.Q<Button>("next").text);
            Assert.AreEqual(PostWaveScreen.Headline(response.Result), view.Q<Label>("headline").text);
        }

        [Test]
        public void PostWave_ThreeArrivals_RenderThreeCardsNamedAfterThem()
        {
            // The single-card assertion above is satisfied by a view that
            // always renders exactly one card. This is not.
            var a = Creature("Vetch Crawler");
            var b = Creature("Ember Skitter");
            var c = Creature("Pale");
            var view = BoundPostWave(Submitted("Win", "shards", 150), a, b, c);

            Assert.AreEqual(3, view.Query<CreatureCard>().ToList().Count);
            foreach (var creature in new[] { a, b, c })
                Assert.IsNotNull(view.Q<CreatureCard>(creature.CreatureId.ToString()));
        }

        [Test]
        public void PostWave_NoRewardAndNoArrivals_ShowsNeither()
        {
            // `reward` is optional on the wire (`NullValueHandling.Ignore`),
            // and a wave can pay nothing. An empty line, never "0 ".
            var view = BoundPostWave(new WaveSubmitResponse { Result = "Win", IntegrityRemaining = 2 });

            Assert.AreEqual(string.Empty, RewardOf(view));
            Assert.AreEqual(0, view.Query<CreatureCard>().ToList().Count);
        }

        [Test]
        public void PostWave_TheHeadlineFollowsTheServersVerdictAndNotTheClients()
        {
            // `client_architecture` section 7: the client is never a source
            // of truth. Two verdicts, two headlines, and the only input that
            // differs is the server's string.
            var won = BoundPostWave(Submitted("Win", "shards", 150)).Q<Label>("headline").text;
            var lost = BoundPostWave(Submitted("Loss", "shards", 150)).Q<Label>("headline").text;

            Assert.AreNotEqual(won, lost);
            Assert.IsNotEmpty(won);
        }

        /// THE REWARD IS A StatCell AS OF PHASE 8 TASK 11, and this is the
        /// half `RewardOf` cannot see: the value Label carries `t-num`. bible
        /// 10.6's two hard requirements - tabular figures and an 11px floor -
        /// are both enforced through that marker (TypographyTests reads the
        /// face off the FontAsset, verify-uss-tokens.sh check 2 reads the
        /// size), and neither can fire on a numeral that never wears it.
        [Test]
        public void PostWave_TheRewardAndIntegrityValuesCarryTheNumeralMarker()
        {
            var view = BoundPostWave(Submitted("Win", "shards", 150));

            foreach (var name in new[] { "reward", "integrity" })
                Assert.IsTrue(view.Q<StatCell>(name).Q<Label>("value").ClassListContains("t-num"),
                    name + "'s value is a number a decision depends on and is not marked as one");
        }

        /// INTEGRITY WAS ON THIS SCREEN'S `Bind` AND ON NO SCREEN AT ALL until
        /// Task 11. It is `Required.Always` on the wire and combat_engine
        /// section 8 makes it the loss condition, so a player who cleared at 1
        /// and a player who cleared untouched were reading the identical
        /// screen. Both directions, because a cell hardcoded to the fixture's
        /// number passes the first.
        [Test]
        public void PostWave_StatesTheIntegrityTheServerReturned()
        {
            var scraped = new WaveSubmitResponse { Result = "Win", IntegrityRemaining = 1 };
            var untouched = new WaveSubmitResponse { Result = "Win", IntegrityRemaining = 8 };

            Assert.AreEqual(PostWaveScreen.IntegrityStatValue(1),
                ValueOf(BoundPostWave(scraped).Q<StatCell>("integrity")));
            Assert.AreEqual(PostWaveScreen.IntegrityStatValue(8),
                ValueOf(BoundPostWave(untouched).Q<StatCell>("integrity")));
        }

        /// The grant row carried --green-tint and --space-3 of padding at
        /// width 100%, so a wave that granted nothing drew a blank green block
        /// between the reward and the CTA. The tint is gone from the
        /// stylesheet; the row collapses. Both directions, so a view that hid
        /// the row unconditionally cannot pass by dropping the cards.
        [Test]
        public void PostWave_WithNoArrivals_CollapsesTheGrantRowRatherThanDrawingItEmpty()
        {
            var bare = BoundPostWave(new WaveSubmitResponse { Result = "Win", IntegrityRemaining = 2 });
            Assert.AreEqual(DisplayStyle.None, bare.Q<VisualElement>("granted").resolvedStyle.display);

            var dropped = BoundPostWave(Submitted("Win", "shards", 150), Creature("Pale"));
            Assert.AreEqual(DisplayStyle.Flex, dropped.Q<VisualElement>("granted").resolvedStyle.display);
        }

        /// THE HEADER MUST NOT ASSERT THE VERDICT, which is the whole reason
        /// the title is not "Wave cleared": it is set at construction and the
        /// verdict arrives at `Bind`, from the server. A constant header over
        /// a server headline reading "Wave not cleared." is the client
        /// contradicting the server in its own chrome.
        [Test]
        public void PostWave_ComposesTheScaffoldAndItsHeaderDoesNotStateTheVerdict()
        {
            var lost = BoundPostWave(Submitted("Loss", "shards", 150));

            Assert.IsNotNull(lost.Q(className: ScreenScaffold.UssClassName), "no scaffold");
            Assert.AreEqual(PostWaveScreen.Title, lost.Q<Label>("title").text);
            StringAssert.DoesNotContain("cleared", lost.Q<Label>("title").text);
        }

        /// The three-cell row, and the kicker that says which wave it is
        /// about. Post-Wave keeps its page header (`PostWaveScreen.Title`'s
        /// note), so its kicker goes in the scaffold's own eyebrow slot rather
        /// than into a band the way the defeat screen's does.
        [Test]
        public void PostWave_StatesThreeCellsAndItsKickerNamesTheWave()
        {
            var view = new PostWaveView();
            view.Bind(Submitted("Win", "shards", 150), null, next: () => { },
                      wave: 7, deployed: 5);

            Assert.AreEqual(3, view.Query<StatCell>().ToList().Count);
            Assert.AreEqual("5", ValueOf(view.Q<StatCell>("kept")));

            var eyebrow = view.Q<Label>("eyebrow");
            Assert.AreEqual(PostWaveScreen.Eyebrow(7), eyebrow.text);
            Assert.AreEqual(DisplayStyle.Flex, eyebrow.style.display.value);

            // THE TWO SCREENS OF THIS BEAT SAY IT THE SAME WAY, which is what
            // `WaveCampaign.Eyebrow` exists for - one sentence, not two that
            // happen to agree today.
            Assert.AreEqual(WaveDefeatScreen.Eyebrow(7), eyebrow.text);

            // A caller that does not know the wave hides the row rather than
            // printing "WAVE  OF 12".
            Assert.AreEqual(DisplayStyle.None,
                BoundPostWave(Submitted("Win", "shards", 150)).Q<Label>("eyebrow")
                    .style.display.value);
        }

        // ---------------------------------------------------------------
        // Wave Defense HUD
        // ---------------------------------------------------------------

        static BodyBar Raider(float x, int hp, int max, BodyState state = BodyState.Normal)
        {
            return new BodyBar
            {
                Kind = BodyKind.Raider,
                World = new UnityEngine.Vector3(x, 0f, 0f),
                Hp = hp,
                MaxHp = max,
                State = state,
            };
        }

        static HudSnapshot Hud(int integrity, int tick, params BodyBar[] bodies)
        {
            return new HudSnapshot { Integrity = integrity, Tick = tick, Bodies = bodies };
        }

        static IList<VisualElement> BarsOf(WaveHudView view)
        {
            return view.Query<VisualElement>(className: WaveHudView.BarUssClassName).ToList();
        }

        [Test]
        public void WaveHud_DrawsOneBarPerVisibleBody_AndRedrawsWhenTheSnapshotChanges()
        {
            // The HUD PULLS its snapshot once a frame. A view that read the
            // Func once at Bind - or that built a fixed set of bars - passes
            // the first count and fails the second.
            var snapshot = Hud(2, 10, Raider(1f, 100, 100), Raider(2f, 50, 100));
            var view = new WaveHudView();
            view.Bind(() => snapshot);

            Assert.AreEqual(2, BarsOf(view).Count);

            snapshot = Hud(1, 200, Raider(3f, 10, 100));
            view.Refresh();
            Assert.AreEqual(1, BarsOf(view).Count);

            snapshot = Hud(1, 260);
            view.Refresh();
            Assert.AreEqual(0, BarsOf(view).Count,
                "a wave with nothing on the field must draw no bars, not stale ones");
        }

        [Test]
        public void WaveHud_ShowsTheIntegrityAndTickLineVerbatim_AndItMoves()
        {
            var first = Hud(2, 10, Raider(1f, 100, 100));
            var snapshot = first;
            var view = new WaveHudView();
            view.Bind(() => snapshot);

            Assert.AreEqual(WaveHudScreen.Integrity(first), view.Q<Label>("integrity").text);

            // The tracked-capture procedure reads the tick off this line, so
            // a line that freezes at its first value is a capture aimed with
            // a stopped clock.
            var second = Hud(1, 200, Raider(1f, 100, 100));
            snapshot = second;
            view.Refresh();

            Assert.AreEqual(WaveHudScreen.Integrity(second), view.Q<Label>("integrity").text);
            Assert.AreNotEqual(WaveHudScreen.Integrity(first), WaveHudScreen.Integrity(second),
                "sanity: the two lines must differ, or the check above proves nothing");
        }

        [Test]
        public void WaveHud_AcceptsNoPointerEventAnywhere()
        {
            // RALLY IS THE PLAYER'S ONLY INPUT DURING A WAVE (combat_engine
            // section 8) and it is a tap anywhere on the screen. A
            // full-screen UI Toolkit panel that picked pointer events would
            // swallow every one of them - a HUD that silently disables the
            // game, with nothing on screen to show for it. IMGUI had no
            // equivalent hazard, so this is new with the rebuild.
            var view = new WaveHudView();
            view.Bind(() => Hud(2, 10, Raider(1f, 100, 100), Raider(2f, 60, 100)));

            Assert.AreEqual(PickingMode.Ignore, view.pickingMode);

            var elements = view.Query<VisualElement>().ToList();

            // NAMED BEFORE THEY ARE CHECKED. A `foreach` over an empty query
            // asserts nothing and reports success - so the elements that
            // matter are identified first, and the loop below is then known
            // to have seen them. Counting would do as well and would break on
            // any change to the bar's internals; this does not.
            var bars = BarsOf(view);
            Assert.AreEqual(2, bars.Count);
            Assert.Contains(view.Q<Label>("integrity"), elements);
            foreach (var bar in bars)
            {
                Assert.Contains(bar, elements);
                Assert.Contains(bar.Q<VisualElement>("fill"), elements);
                Assert.Contains(bar.Q<Label>("tag"), elements);
            }

            foreach (var element in elements)
                Assert.AreEqual(PickingMode.Ignore, element.pickingMode,
                    "pickable element in the HUD: " + (element.name ?? element.GetType().Name));
        }

        [Test]
        public void WaveHud_TagsABodyByItsState_AndABreachOutranksAChill()
        {
            // `WaveHud`'s rule, which lived only in prose: "Chill is the
            // thing wave 6 exists to teach ... a breach outranks it: that
            // frame is the verdict."
            Assert.AreEqual(BodyState.Breaching, BodyBars.RaiderState(breaching: true, chilled: true));
            Assert.AreEqual(BodyState.Chilled, BodyBars.RaiderState(breaching: false, chilled: true));
            Assert.AreEqual(BodyState.Normal, BodyBars.RaiderState(breaching: false, chilled: false));

            var view = new WaveHudView();
            view.Bind(() => Hud(2, 10,
                Raider(1f, 100, 100, BodyState.Breaching),
                Raider(2f, 100, 100, BodyState.Chilled),
                Raider(3f, 100, 100)));

            var bars = BarsOf(view);
            Assert.AreEqual("BREACH", bars[0].Q<Label>("tag").text);
            Assert.AreEqual("CHILLED", bars[1].Q<Label>("tag").text);
            Assert.AreEqual(string.Empty, bars[2].Q<Label>("tag").text);
            Assert.IsTrue(bars[0].ClassListContains(WaveHudView.BreachingUssClassName));
            Assert.IsFalse(bars[2].ClassListContains(WaveHudView.BreachingUssClassName));
        }

        [Test]
        public void WaveHud_ARalliedCreatureShowsWhicheverRemainderTheSnapshotCarries()
        {
            // NAMED FOR WHAT IT CHECKS. An earlier name said "from the engine",
            // which this cannot see: the snapshot below is hand-built, so what
            // is proven is that the VIEW renders the number it is given and
            // does not compute one. That the number originates at
            // `SimRunner.CreatureRallyRemaining` and is never subtracted
            // client-side lives in `WaveRunner.Snapshot`, and is covered where
            // a real runner exists - `WaveCapturePlayTests`.
            //
            // `WaveRunner`: "a tap with no visible consequence is
            // indistinguishable from a tap that was dropped." So two different
            // remainders must produce two different tags.
            Assert.AreEqual(BodyState.Rallied, BodyBars.CreatureState(87));
            Assert.AreEqual(BodyState.Normal, BodyBars.CreatureState(0));

            var body = new BodyBar
            {
                Kind = BodyKind.Creature,
                Hp = 100,
                MaxHp = 100,
                State = BodyState.Rallied,
                RallyRemaining = 87,
            };
            var snapshot = Hud(2, 10, body);
            var view = new WaveHudView();
            view.Bind(() => snapshot);
            Assert.AreEqual("RALLY 87", BarsOf(view)[0].Q<Label>("tag").text);

            body.RallyRemaining = 12;
            snapshot = Hud(2, 20, body);
            view.Refresh();
            Assert.AreEqual("RALLY 12", BarsOf(view)[0].Q<Label>("tag").text);
        }

        [Test]
        public void WaveHud_FillsEachBarInProportionToItsHp()
        {
            var view = new WaveHudView();
            view.Bind(() => Hud(2, 10,
                Raider(1f, 100, 100),
                Raider(2f, 25, 100),
                Raider(3f, 0, 0)));

            var bars = BarsOf(view);
            Assert.AreEqual(100f, bars[0].Q<VisualElement>("fill").style.width.value.value, 0.001f);
            Assert.AreEqual(25f, bars[1].Q<VisualElement>("fill").style.width.value.value, 0.001f);
            // A body with no max HP would divide by zero. Empty, not full.
            Assert.AreEqual(0f, bars[2].Q<VisualElement>("fill").style.width.value.value, 0.001f);
        }

        [Test]
        public void WaveHud_ASecondBind_ReadsTheNewSnapshotAndNotTheOld()
        {
            // A HUD is bound once per wave, and the shell recycles screens.
            // A `Bind` that appended a reader rather than replacing one would
            // leave the previous wave's numbers on screen - and would stack a
            // second per-frame redraw behind the first.
            var view = new WaveHudView();
            view.Bind(() => Hud(2, 10, Raider(1f, 100, 100)));
            Assert.AreEqual(1, BarsOf(view).Count);

            view.Bind(() => Hud(9, 99, Raider(1f, 100, 100), Raider(2f, 100, 100)));

            Assert.AreEqual(2, BarsOf(view).Count);
            Assert.AreEqual(WaveHudScreen.Integrity(Hud(9, 99)), view.Q<Label>("integrity").text);
        }

        [Test]
        public void WaveHud_ABarIsClampedIntoTheFrameAndBelowTheIntegrityReadout()
        {
            // `WaveHud.ClampIntoSafeArea`'s job, which the rebuild dropped and
            // this restores. The header reservation is the half that matters:
            // a clamped bar landing ON the integrity line would cover the one
            // element the safe-area work exists to keep readable - and
            // `DeviceReplayTests` tells the capturer to read the live tick off
            // that line to time a tap.
            var frame = new UnityEngine.Rect(0f, 0f, 390f, 844f);
            var bar = new UnityEngine.Vector2(52f, 20f);
            const float header = 30f;

            // Inside: untouched. Without this the three clamps below are
            // satisfied by a function that pins every bar to one corner.
            Assert.AreEqual(new UnityEngine.Vector2(100f, 400f),
                WaveHudView.ClampIntoFrame(new UnityEngine.Vector2(100f, 400f), frame, header, bar));

            // Off each edge, one at a time.
            Assert.AreEqual(0f, WaveHudView.ClampIntoFrame(new UnityEngine.Vector2(-40f, 400f), frame, header, bar).x);
            Assert.AreEqual(338f, WaveHudView.ClampIntoFrame(new UnityEngine.Vector2(900f, 400f), frame, header, bar).x);
            Assert.AreEqual(824f, WaveHudView.ClampIntoFrame(new UnityEngine.Vector2(100f, 2000f), frame, header, bar).y);

            // ABOVE THE HEADER, which is the reservation itself: clamped to
            // the readout's bottom, NOT to zero.
            Assert.AreEqual(header,
                WaveHudView.ClampIntoFrame(new UnityEngine.Vector2(100f, -50f), frame, header, bar).y);
            Assert.AreEqual(header,
                WaveHudView.ClampIntoFrame(new UnityEngine.Vector2(100f, 5f), frame, header, bar).y);
        }

        [Test]
        public void WaveHud_WithNoResolvedLayout_LeavesAPositionAloneRatherThanClampingToNothing()
        {
            // The first frame, and the state every test in this file sees. A
            // clamp against a zero or NaN frame would stack every bar in one
            // corner, which is worse than a bar briefly off-screen.
            var desired = new UnityEngine.Vector2(100f, 400f);
            var bar = new UnityEngine.Vector2(52f, 20f);

            Assert.AreEqual(desired, WaveHudView.ClampIntoFrame(desired, new UnityEngine.Rect(), 0f, bar));
            Assert.AreEqual(desired, WaveHudView.ClampIntoFrame(
                desired, new UnityEngine.Rect(0f, 0f, float.NaN, float.NaN), 0f, bar));
        }

        [Test]
        public void WaveHud_ReusesItsBarElementsRatherThanRebuildingThemEveryFrame()
        {
            // This view redraws every frame of the one scene with a defended
            // per-frame budget. A `new VisualElement()` per body per frame is
            // the same garbage `WaveRunner._onTick` was hoisted out of Update
            // to avoid, and nothing else in the suite would notice it.
            var snapshot = Hud(2, 10, Raider(1f, 100, 100));
            var view = new WaveHudView();
            view.Bind(() => snapshot);
            var first = BarsOf(view)[0];

            snapshot = Hud(2, 11, Raider(1.5f, 90, 100));
            view.Refresh();

            Assert.AreSame(first, BarsOf(view)[0]);
        }

        /// 3b49931 CACHED `fill` AND `tag` AT CONSTRUCTION and Task 11 put an
        /// elevation wrapper between the bar and its track. The cache is a
        /// pair of references held in the pool, so a wrapper cannot invalidate
        /// it - but the QUERIES the rest of this file uses walk the tree, and
        /// this is the assertion that says they still arrive. It is the
        /// structural half of `ReusesItsBarElements...`, which proves the
        /// elements are reused and says nothing about where they are.
        [Test]
        public void WaveHud_TheTrackSitsInsideAnElevationWrapperAndTheCachedChildrenStillResolve()
        {
            var view = new WaveHudView();
            view.Bind(() => Hud(2, 10, Raider(1f, 60, 100)));

            var bar = BarsOf(view)[0];
            var elev = bar.Q<VisualElement>("elev");
            Assert.IsNotNull(elev, "the bar has no elevation wrapper");
            Assert.IsTrue(elev.ClassListContains(SectionCard.ElevationUssClassName),
                "the wrapper carries no elevation, so it is padding with nothing in it");

            var track = bar.Q<VisualElement>("track");
            Assert.AreSame(elev, track.parent, "the track is not the wrapper's surface");
            Assert.IsNotNull(bar.Q<VisualElement>("fill"));
            Assert.IsNotNull(bar.Q<Label>("tag"));

            // The cached references are what `Draw` writes, so the proof they
            // still point at the elements in the tree is that a redraw reaches
            // them - not that they are non-null.
            Assert.AreEqual(60f, bar.Q<VisualElement>("fill").style.width.value.value, 0.001f);
        }

        /// bible 10.6's marker on both numerals the HUD prints. The readout is
        /// integrity and the live tick; the tag's is Rally's countdown, which
        /// combat_engine section 8 makes the player's only feedback that their
        /// only input was taken.
        [Test]
        public void WaveHud_BothNumeralsCarryTheTabularMarker()
        {
            var view = new WaveHudView();
            view.Bind(() => Hud(2, 10,
                new BodyBar
                {
                    Kind = BodyKind.Creature,
                    World = new UnityEngine.Vector3(1f, 0f, 0f),
                    Hp = 60, MaxHp = 100,
                    State = BodyState.Rallied, RallyRemaining = 42,
                }));

            Assert.IsTrue(view.Q<Label>("integrity").ClassListContains("t-num"));
            Assert.IsTrue(BarsOf(view)[0].Q<Label>("tag").ClassListContains("t-num"));
        }

        /// THE HUD TAKES NO SCAFFOLD, ASSERTED FROM THIS SIDE TOO.
        /// `ScaffoldTests` exempts it by name, which records that the sweep
        /// must not FAIL on it; this records that it must not GAIN one. The
        /// two together are what stop a later task satisfying the sweep by
        /// wrapping an overlay in a header, a CTA row and a ScrollView it can
        /// never use - `WaveRunner` adds this straight to the wave scene's own
        /// `document.rootVisualElement`, so there is no host to pop back to.
        [Test]
        public void WaveHud_ComposesNoScaffold_BecauseItOverlaysTheSceneRatherThanBeingAScreen()
        {
            var view = new WaveHudView();
            view.Bind(() => Hud(2, 10, Raider(1f, 100, 100)));

            Assert.IsNull(view.Q(className: ScreenScaffold.UssClassName),
                "the HUD grew a scaffold; it is an overlay and ScaffoldTests exempts it by name");
        }

        /// The reward line, read back through the StatCell that now states it.
        ///
        /// IT WAS A Label NAMED `reward` UNTIL PHASE 8 TASK 11 AND THE FACT
        /// ASSERTED IS UNCHANGED - this reaches the same sentence through the
        /// same handle, because the cell keeps the name the Label had.
        /// `StatCell`'s whole reason for existing is that its value Label
        /// carries `t-num` without the screen having to remember to ask for
        /// it ("nothing makes a screen remember to put the class on"), and
        /// the reward is a number a decision depends on, which is bible
        /// 10.6's own test. `PostWave_TheRewardStatCarriesTheNumeralMarker`
        /// pins the marker itself so this helper does not have to.
        static string ValueOf(VisualElement cell) => cell.Q<Label>("value").text;

        static string RewardOf(PostWaveView view) => ValueOf(view.Q<StatCell>("reward"));

        /// The handoff's top band - `Wave Defense.dc.html:39-40`, the kicker
        /// and the wave counter over the scrim.
        ///
        /// `HudSnapshot` GAINED NOTHING, which is asserted from the other
        /// side: the counter is set through a property and is therefore
        /// unaffected by a redraw, so a `Refresh` between the two reads below
        /// must not disturb it. A wave id smuggled into the snapshot would
        /// pass the first read and fail this one the moment a frame arrived
        /// without it.
        [Test]
        public void WaveHud_TheTopBandNamesTheWaveAndItsKicker()
        {
            var view = new WaveHudView { Wave = 6 };
            view.Bind(() => Hud(2, 10, Raider(1f, 100, 100)));

            Assert.AreEqual(WaveHudScreen.Eyebrow, view.Q<Label>("eyebrow").text);
            Assert.AreEqual(WaveHudScreen.WaveWord, view.Q<Label>("wave-word").text);
            Assert.AreEqual(WaveHudScreen.WaveOf(6), view.Q<Label>("wave-number").text);

            view.Refresh();
            Assert.AreEqual(WaveHudScreen.WaveOf(6), view.Q<Label>("wave-number").text,
                "a redraw cleared the wave counter - it is not per-frame data");

            // A DIFFERENT WAVE PRODUCES A DIFFERENT COUNTER. Without this the
            // assertion above is satisfied by a Label holding "6 / 12".
            var other = new WaveHudView { Wave = 9 };
            Assert.AreNotEqual(view.Q<Label>("wave-number").text,
                other.Q<Label>("wave-number").text);

            // BOTH NUMERALS CARRY THE MARKER. bible 10.6, and the wave a
            // player is on is a number the next decision depends on.
            Assert.IsTrue(view.Q<Label>("wave-number").ClassListContains("t-num"));
        }

        /// A HUD with no wave draws no counter rather than "Wave  / 12" or
        /// "Wave 0 / 12" - `DeployScreen.FoesStatValue`'s rule, which this
        /// project applies to every readout a caller may not know.
        [Test]
        public void WaveHud_WithNoWave_CollapsesTheCounterRatherThanClaimingWaveZero()
        {
            var view = new WaveHudView();
            view.Bind(() => Hud(2, 10));

            Assert.AreEqual(string.Empty, view.Q<Label>("wave-number").text);
            Assert.AreEqual(DisplayStyle.None,
                view.Q<VisualElement>("wave-line").style.display.value);

            // The kicker names the PLACE and not the wave, so it stays.
            Assert.AreEqual(WaveHudScreen.Eyebrow, view.Q<Label>("eyebrow").text);

            // And setting one later brings the row back - `Wave` is settable
            // after construction for the same reason `Camera` is.
            view.Wave = 6;
            Assert.AreEqual(DisplayStyle.Flex,
                view.Q<VisualElement>("wave-line").style.display.value);
        }

        /// THE READOUT IS THE VALUE OF A PILL AND `#chrome` IS THE DIRECT
        /// CHILD, which is the structural precondition `Place` depends on and
        /// no test could otherwise see.
        ///
        /// `WaveHudView.Place` reserves a band at the top of the frame so a
        /// clamped bar never lands on the chrome, and it MEASURES that band
        /// with `layout`, which is relative to the PARENT. Task 11's own note
        /// there named this trap in advance - "a wrapper would have moved the
        /// measurement one level down, silently, while still returning a
        /// plausible number" - and Task 18 is the change that moved it. The
        /// bug it would produce is invisible in the Editor and in every
        /// capture, because `Place` returns early with no camera.
        [Test]
        public void WaveHud_TheClampMeasuresTheWholeChrome_WhichIsStillADirectChild()
        {
            var view = new WaveHudView { Wave = 6 };
            view.Bind(() => Hud(2, 10, Raider(1f, 100, 100)));

            var chrome = view.Q<VisualElement>("chrome");
            Assert.IsNotNull(chrome, "the HUD has no chrome element for Place to measure");
            Assert.AreSame(view, chrome.parent,
                "#chrome is not a direct child of the root, so its `layout` is in the wrong space");

            // AND THE READOUT IS NOT ONE ANY MORE, which is the fact that made
            // the change necessary. If this ever becomes true again, `Place`
            // should go back to measuring it.
            var integrity = view.Q<Label>("integrity");
            Assert.AreNotSame(view, integrity.parent,
                "the readout is a direct child again; Place measures #chrome and would now "
                + "reserve a band that does not contain it");
            Assert.IsTrue(integrity.ClassListContains("t-num"), "the readout lost the marker");
        }

        /// A FRAME WHOSE NUMBERS HAVE NOT MOVED WRITES NO STRING.
        ///
        /// Not "the HUD allocates nothing per tick", which overstates it and
        /// which an earlier version of this comment said: the readout carries
        /// the live tick, so it IS rebuilt every time the simulation ticks -
        /// 30 times a second during combat, by design, because
        /// `WaveCapturePlayTests` and the device re-capture procedure both
        /// read that number off the screen. What the guards remove is the
        /// REPEAT: the renderer is not the tick clock, so at 60fps at least
        /// half the frames were rebuilding a string identical to the one
        /// already on screen, on the one view with a per-frame budget worth
        /// defending. Two places did it before Phase 9 Task 18 -
        /// `WaveHudScreen.Integrity` (two `ToString`s and a concat) and
        /// `BarTag`'s "RALLY 42".
        ///
        /// MEASURED IN GEN-0 COLLECTIONS AND NOT IN BYTES, and the first
        /// version of this test is why. It read
        /// `GC.GetAllocatedBytesForCurrentThread()`, which on this Editor's
        /// Mono returns the same figure however much is allocated - so the
        /// frozen loop and the control loop both measured zero and the "it
        /// allocated nothing" assertion passed while proving nothing at all.
        /// That is the shape of the two assertions this project recently found
        /// that could not fail, and it is recorded here so nobody reaches for
        /// the same API again.
        ///
        /// THE CONTROL ARM IS THE REGRESSION ITSELF, AT ITS OWN SIZE, which is
        /// fix round 1's finding and is the part that makes the frozen arm
        /// mean something. A gen-0 counter only says "more than the trigger T
        /// was allocated", so a control arm that allocates 2.5x what the
        /// regression would allocate bounds T ABOVE the regression and leaves
        /// a window in which this test stays green on exactly the defect it
        /// was written for. The control loop below therefore calls the two
        /// model functions the guards skip, `frames` times each and nothing
        /// else - so it allocates precisely what removing both guards would,
        /// and T is bounded at or below that. A green frozen arm and a red
        /// control arm cannot both hold with a guard missing.
        ///
        /// AND `frames` IS 200,000 BECAUSE 20,000 WAS MEASURED TO BE TOO FEW -
        /// which is the window fix round 1 predicted, observed. At 20,000 the
        /// control arm reports ZERO collections (~2 MB of strings), while the
        /// earlier version's arm, which allocated a snapshot per iteration as
        /// well, reported several (~5 MB). So T sits in (2, 5] MB on this
        /// Editor and the first version of this test WOULD have stayed green
        /// with the readout guard removed. `run-unity-tests.sh` was run with
        /// that guard commented out to see it: the control arm reddened with
        /// `unguarded=0`, which is the sanity arm refusing to let the frozen
        /// assertion mean anything. 200,000 puts the control at ~20 MB, an
        /// order of magnitude over the observed floor, and the frozen arm
        /// carries the same count - so the two remain matched, which is the
        /// property that makes this a proof rather than a ratio.
        ///
        /// `GC.CollectionCount(0)` IS PROCESS-WIDE AND THIS RUNS INSIDE AN
        /// EDITOR, so another thread allocating during the frozen loop can
        /// redden it. That is a false positive rather than a false negative -
        /// it cannot hide a regression - and it has not been observed across
        /// the runs in this task. If it ever flakes, the answer is not a
        /// tolerance: it is a per-thread counter, which this runtime does not
        /// have.
        [Test]
        public void WaveHud_RedrawingAFrameWhoseNumbersHaveNotMoved_WritesNoString()
        {
            const int frames = 200000;

            var snapshot = Hud(2, 10, Rallied(1f, 42));
            var view = new WaveHudView { Wave = 6 };
            view.Bind(() => snapshot);

            // Warm: the first writes build the class lists and the inline
            // style store, which are one-off allocations by construction.
            for (var i = 0; i < 8; i++) view.Refresh();

            GC.Collect();
            var mark = GC.CollectionCount(0);
            for (var i = 0; i < frames; i++) view.Refresh();
            var frozen = GC.CollectionCount(0) - mark;

            // THE CONTROL: exactly the two strings the guards skip, and
            // nothing else. `sink` is accumulated rather than discarded so no
            // optimiser can decide the calls are dead.
            var rallied = Rallied(1f, 42);
            var sink = 0;
            GC.Collect();
            mark = GC.CollectionCount(0);
            for (var i = 0; i < frames; i++)
            {
                sink += WaveHudScreen.Integrity(snapshot).Length;
                sink += WaveHudScreen.BarTag(rallied).Length;
            }
            var unguarded = GC.CollectionCount(0) - mark;

            Assert.Greater(sink, 0, "sanity: the control loop's work was optimised away");
            Assert.Greater(unguarded, 0,
                "writing both readouts " + frames + " times unguarded triggered no gen-0 "
                + "collection, so either this runtime is not counting them or the trigger is "
                + "above the regression's own allocation - and the assertion below would then "
                + "prove nothing (frozen=" + frozen + ", unguarded=" + unguarded + ")");
            Assert.AreEqual(0, frozen,
                "the HUD triggered " + frozen + " gen-0 collections redrawing " + frames
                + " frames whose numbers had not moved, against " + unguarded
                + " for the same number of unguarded writes");
        }

        static BodyBar Rallied(float x, int remaining)
        {
            return new BodyBar
            {
                Kind = BodyKind.Creature,
                World = new UnityEngine.Vector3(x, 0f, 0f),
                Hp = 60, MaxHp = 100,
                State = BodyState.Rallied,
                RallyRemaining = remaining,
            };
        }

        // ---------------------------------------------------------------
        // DeployView - Phase 9 Task 17, `Wave Defense.dc.html` in
        // `phase: 'placing'`.
        //
        // WHERE THE OTHER DEPLOY TESTS ARE, AND WHY THESE ARE HERE INSTEAD.
        // `ScreenBindingTests` owns the MODEL-TO-VIEW property for this
        // screen - Start follows `CanDeploy` and never a count, the blocker
        // is the model's sentence verbatim, the CTA is the model's label -
        // and those are untouched by this task. What is here is the
        // HANDOFF's half: the four things the deploy screen states about a
        // wave, and the list a player acts on. Same file as the other two
        // wave screens because it is the third face of the same beat.
        // ---------------------------------------------------------------

        /// A roster of `count` creatures with the first `selected` of them
        /// deployed, and the model built from exactly that.
        static (DeployScreenModel model, RosterScreen roster) Deployment(int count, int selected)
        {
            var roster = new RosterScreen();
            var response = new RosterResponse { Cap = 20 };
            var ids = new List<Guid>();
            for (var i = 0; i < count; i++)
            {
                var creature = Creature("Species" + i);
                response.Creatures.Add(creature);
                if (i < selected) ids.Add(creature.CreatureId);
            }
            roster.ApplyRoster(response);
            return (DeployScreen.Build(waveId: 7, roster: roster, selected: ids), roster);
        }

        static DeployView BoundDeploy(
            int count, int selected, Texture lane = null,
            Action<Guid> onToggle = null, DeployWaveFacts facts = null)
        {
            var (model, roster) = Deployment(count, selected);
            var view = new DeployView();
            view.Bind(model, onStart: () => { }, lane: lane,
                roster: roster.Known, onToggle: onToggle, facts: facts);
            return view;
        }

        [Test]
        public void Deploy_TheLaneCardShowsTheTextureItWasHandedAndNothingElse()
        {
            // THE INLINE ACCESSOR, NOT `resolvedStyle`. In a suite with no
            // live panel a stylesheet rule cannot be read back, and
            // `LanePreviewCard.SetTexture` writes `style.backgroundImage`
            // inline - which is exactly what makes it readable here.
            var texture = new Texture2D(2, 2);
            try
            {
                var lane = BoundDeploy(3, 3, lane: texture).Q<LanePreviewCard>("lane-card");
                Assert.IsNotNull(lane, "the deploy screen draws no lane card at all");
                Assert.AreEqual(texture, PictureOf(lane).style.backgroundImage.value.texture,
                    "the card is showing something other than the stage's texture");

                // THE COMPLEMENT, and it is the half that matters: a card
                // that ignored its argument and drew a fixed image would
                // pass the assertion above only by accident and this one
                // never. Null is the ordinary state - every capture and
                // every test binds without a stage.
                var blank = BoundDeploy(3, 3).Q<LanePreviewCard>("lane-card");
                Assert.IsNull(PictureOf(blank).style.backgroundImage.value.texture,
                    "a screen with no stage still drew a picture");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        [Test]
        public void Deploy_TheLaneCardTagsOnePocketPerDeployableSlot_FilledUpToTheDeployment()
        {
            // `DeployScreen.Cap` is the server's (wave/issuance.ts), not the
            // handoff's four, and the tags are what say so on the picture.
            var tags = BoundDeploy(6, 2).Q<LanePreviewCard>("lane-card")
                .Query<Label>(className: LanePreviewCard.SlotUssClassName).ToList();

            Assert.AreEqual(DeployScreen.Cap, tags.Count, "one tag per deployable pocket");
            for (var i = 0; i < tags.Count; i++)
            {
                Assert.AreEqual(DeployScreen.PocketTag(i), tags[i].text);
                Assert.AreEqual(i < 2, tags[i].ClassListContains(LanePreviewCard.SlotFilledUssClassName),
                    "pocket " + i + " is drawn as the wrong occupancy for a deployment of 2");
            }
        }

        [Test]
        public void Deploy_TheFieldListHoldsEveryRosterCreature_DeployedOnesFirst_BadgedWithTheirPocket()
        {
            // THE ORDER IS WHAT MAKES THE BADGE HONEST. Row i is pocket i for
            // every deployed creature only because the deployed ones are
            // listed first - so this asserts the ordering and the letters
            // together, which is the invariant rather than two facts.
            var rows = BoundDeploy(5, 2).Query<FieldSlotRow>().ToList();

            Assert.AreEqual(5, rows.Count, "one row per roster creature");
            Assert.AreEqual(DeployScreen.PocketTag(0), rows[0].Q<Label>("letter").text);
            Assert.AreEqual(DeployScreen.PocketTag(1), rows[1].Q<Label>("letter").text);
            Assert.IsTrue(rows[0].ClassListContains(FieldSlotRow.FilledUssClassName));
            Assert.IsTrue(rows[1].ClassListContains(FieldSlotRow.FilledUssClassName));

            for (var i = 2; i < rows.Count; i++)
            {
                Assert.IsFalse(rows[i].ClassListContains(FieldSlotRow.FilledUssClassName),
                    "row " + i + " is drawn as occupied and nothing is standing in it");
                Assert.AreEqual(DeployScreen.EmptyPocketTag, rows[i].Q<Label>("letter").text,
                    "an undeployed creature was badged with a pocket it does not hold");
            }
        }

        [Test]
        public void Deploy_ADifferentDeployment_MovesTheBadges()
        {
            // WITHOUT THIS, the test above is satisfied by a list that
            // letters its rows A, B, C... by position and calls them pockets.
            // Four of five deployed puts a pocket letter on row 3, where two
            // of five puts the empty mark.
            var two = BoundDeploy(5, 2).Query<FieldSlotRow>().ToList();
            var four = BoundDeploy(5, 4).Query<FieldSlotRow>().ToList();

            Assert.AreEqual(DeployScreen.EmptyPocketTag, two[3].Q<Label>("letter").text);
            Assert.AreEqual(DeployScreen.PocketTag(3), four[3].Q<Label>("letter").text);
        }

        [Test]
        public void Deploy_AtTheCap_AnUndeployedRowIsVisiblyOutOfReachRatherThanSilentlyInert()
        {
            // `DeployScreen.Cap` is 5 and the model refuses a sixth, so a tap
            // on row 6 cannot do anything - and a control that answers a tap
            // with nothing reads as broken. Both halves, because either one
            // alone is a state the other can drift out of.
            var rows = BoundDeploy(6, DeployScreen.Cap, onToggle: _ => { }).Query<FieldSlotRow>().ToList();
            var last = rows[rows.Count - 1];

            Assert.IsTrue(last.ClassListContains(DeployView.RowFullUssClassName));
            Assert.IsFalse(last.enabledSelf);

            // And below the cap nothing is dimmed, which is the complement a
            // view that always dimmed its last row would fail.
            var room = BoundDeploy(6, 2, onToggle: _ => { }).Query<FieldSlotRow>().ToList();
            foreach (var row in room)
            {
                Assert.IsFalse(row.ClassListContains(DeployView.RowFullUssClassName));
                Assert.IsTrue(row.enabledSelf);
            }
        }

        [Test]
        public void Deploy_EveryFieldRowAnswersToItsCreaturesId()
        {
            // The handle `RosterView` and `SpliceChamberView` give a
            // creature's element, and the one `onToggle` reports back - a
            // caller reaching one row must not have to count children.
            var (model, roster) = Deployment(4, 2);
            var view = new DeployView();
            view.Bind(model, onStart: () => { }, roster: roster.Known, onToggle: _ => { });

            foreach (var creature in roster.Known)
            {
                Assert.IsNotNull(view.Q<FieldSlotRow>(creature.CreatureId.ToString()),
                    "no field row answers to " + creature.CreatureId);
            }
        }

        [Test]
        public void Deploy_TheIncomingCardStatesTheFoesTheCountAndTheReward()
        {
            var cells = BoundDeploy(6, 3, facts: new DeployWaveFacts
            {
                Foes = 5,
                RewardCurrency = "shards",
                RewardAmount = 120,
            }).Query<StatCell>().ToList();

            Assert.AreEqual(3, cells.Count, "the handoff's incoming-wave card is three cells");
            Assert.AreEqual(DeployScreen.FoesStatValue(5), ValueOf(cells[0]));
            Assert.AreEqual(DeployScreen.DeployedStatValue(3), ValueOf(cells[1]));
            Assert.AreEqual(DeployScreen.RewardValue("shards", 120), ValueOf(cells[2]));

            // Every one of the three is a number a decision depends on, so
            // every one wears bible 10.6's marker - which is `StatCell`'s
            // whole reason for existing.
            foreach (var cell in cells) Assert.IsTrue(cell.Q<Label>("value").ClassListContains("t-num"));
        }

        [Test]
        public void Deploy_WithNoWaveFacts_StatesNothingRatherThanZero()
        {
            // A readout that prints 0 for a number it was never told is a
            // screen that lies about the player's wallet and about the wave.
            // `WaveDefeatScreen.Resupply` makes the same call on an empty
            // grant: "says nothing rather than promising a resupply that is
            // not there."
            var view = BoundDeploy(3, 2);
            var cells = view.Query<StatCell>().ToList();

            Assert.AreEqual(string.Empty, ValueOf(cells[0]), "FOES claimed a number it was not given");
            Assert.AreEqual(string.Empty, ValueOf(cells[2]), "REWARD claimed a number it was not given");
            Assert.AreEqual(string.Empty, EnergyValueOf(view),
                "GENE ENERGY claimed a balance it was not given");

            // The one readout that does NOT vary, and it is a fact rather
            // than a blank: nothing has broken through before the wave.
            Assert.AreEqual(DeployScreen.IntegrityFull, IntegrityValueOf(view));
        }

        [Test]
        public void Deploy_TheHeaderIsTheHandoffsKickerAndAWaveNumberInTheNumeralFace()
        {
            var view = BoundDeploy(3, 2);

            Assert.AreEqual(DeployScreen.Eyebrow, view.Q<Label>("eyebrow").text);
            Assert.AreEqual(DeployScreen.Title, view.Q<Label>("title").text);
            Assert.AreEqual(DeployScreen.WaveStatValue(7), view.Q<Label>("wave-number").text);

            // THE TWO LABELS ARE THE HANDOFF'S ONE LINE. A screen that put
            // the number into the title Label would pass neither of the two
            // assertions above, and one that dropped it would pass both of
            // the first two - this is what pins the pair.
            Assert.AreEqual(DeployScreen.WaveTitle(7),
                view.Q<Label>("title").text + " " + view.Q<Label>("wave-number").text);

            // The numeral is in the tabular face and the word is not, which
            // is the reason there are two Labels at all: bible 10.6 keeps
            // tabular figures for a number a decision depends on, and this
            // project never renders a numeral in the display face.
            Assert.IsTrue(view.Q<Label>("wave-number").ClassListContains("t-num"));
            Assert.IsFalse(view.Q<Label>("title").ClassListContains("t-num"));
        }

        [Test]
        public void Deploy_ADifferentWave_MovesEveryPlaceTheWaveIsNamed()
        {
            // The header and the incoming card both state the wave, and a
            // view holding either as a literal passes a single-wave test.
            var (model, roster) = Deployment(3, 2);
            var one = new DeployView();
            one.Bind(model, onStart: () => { }, roster: roster.Known);

            var otherRoster = new RosterScreen();
            var creature = Creature("Vetch");
            otherRoster.ApplyRoster(new RosterResponse { Creatures = { creature }, Cap = 20 });
            var two = new DeployView();
            two.Bind(
                DeployScreen.Build(waveId: 2, roster: otherRoster, selected: new List<Guid> { creature.CreatureId }),
                onStart: () => { }, roster: otherRoster.Known);

            Assert.AreEqual(DeployScreen.WaveStatValue(7), one.Q<Label>("wave-number").text);
            Assert.AreEqual(DeployScreen.WaveStatValue(2), two.Q<Label>("wave-number").text);
            Assert.AreEqual(DeployScreen.IncomingHeading(7), one.Q<Label>("incoming-heading").text);
            Assert.AreEqual(DeployScreen.IncomingHeading(2), two.Q<Label>("incoming-heading").text);
            Assert.AreNotEqual(
                one.Q<Label>("incoming-heading").text, two.Q<Label>("incoming-heading").text);
        }

        [Test]
        public void Deploy_TheWaveSentenceIsTheFactsWhenThereIsOne_AndTheDefaultWhenThereIsNot()
        {
            Assert.AreEqual(DeployScreen.DefaultBrief, BoundDeploy(3, 2).Q<Label>("brief").text);
            Assert.AreEqual("Two armoured brutes lead this one.",
                BoundDeploy(3, 2, facts: new DeployWaveFacts { Brief = "Two armoured brutes lead this one." })
                    .Q<Label>("brief").text);
        }

        /// THE UPPERCASE IS IN THE CONSTANT AND NOWHERE ELSE, WHICH IS WHAT
        /// THIS ASSERTS. USS has no `text-transform`, so the handoff's `.lbl`
        /// casing has to be baked into the string - and the user's ruling at
        /// the Task 13/14 boundary is that it lives in the NAMED CONSTANT and
        /// is never scattered as a literal in markup or in a test. So this
        /// test names no uppercase string of its own: it reads the constants
        /// and checks they ARE uppercase, which is the property, and reads
        /// the view against the constants, which is the wiring.
        [Test]
        public void Deploy_EveryLblShipsUppercaseInItsOwnConstant()
        {
            foreach (var eyebrow in new[]
                     {
                         DeployScreen.Eyebrow, DeployScreen.StandardTag, DeployScreen.FieldHeading,
                         DeployScreen.FoesStatLabel, DeployScreen.DeployedStatLabel,
                         DeployScreen.RewardStatLabel, DeployScreen.EnergyLabel,
                         DeployScreen.IntegrityLabel, DeployScreen.IncomingHeading(7),
                     })
            {
                Assert.AreEqual(eyebrow.ToUpperInvariant(), eyebrow,
                    "a `.lbl` string is not uppercase in its constant, and USS cannot make it so");
            }

            var view = BoundDeploy(3, 2);
            Assert.AreEqual(DeployScreen.StandardTag, view.Q<Label>("wave-tag").text);
            Assert.AreEqual(DeployScreen.FieldHeading, view.Q<Label>("field-heading").text);
            Assert.AreEqual(DeployScreen.FieldHint, view.Q<Label>("field-hint").text);

            // AND THE HINT IS THE ONE THAT MUST NOT BE. The handoff draws it
            // lowercase and it is not a `.lbl` - the same distinction
            // `SpliceScreen.OddsNote` records.
            Assert.AreNotEqual(DeployScreen.FieldHint.ToUpperInvariant(), DeployScreen.FieldHint);
        }

        [Test]
        public void Deploy_ALegalDeploymentDrawsNoRefusalRow_AndStartStillCarriesTheModelsLabel()
        {
            // Both halves of Phase 8 Task 10's blocker rule, re-asserted
            // through the rebuilt screen: the row COLLAPSES rather than
            // drawing a blank coral strip, and the CTA is the model's
            // sentence rather than a literal this view holds.
            var legal = BoundDeploy(3, 2);
            Assert.AreEqual(string.Empty, legal.Q<Label>("blocker").text);
            Assert.AreEqual(DisplayStyle.None, legal.Q<Label>("blocker").resolvedStyle.display);
            Assert.AreEqual(DeployScreen.Cta, legal.Q<Button>("start").text);
            Assert.IsTrue(legal.Q<Button>("start").enabledSelf);

            var blocked = BoundDeploy(3, 0);
            Assert.IsNotEmpty(blocked.Q<Label>("blocker").text);
            Assert.AreEqual(DisplayStyle.Flex, blocked.Q<Label>("blocker").resolvedStyle.display);
            Assert.IsFalse(blocked.Q<Button>("start").enabledSelf);
        }

        /// BY CLASS AND NOT BY NAME. `LanePreviewCard` names its picture
        /// element "lane" and `DeployView` names the card "lane-card", so
        /// this is unambiguous from either end - and it is the accessor the
        /// component's own `ComponentTests` case uses one level down.
        static VisualElement PictureOf(LanePreviewCard card) =>
            card.Q<VisualElement>(className: "lane-preview-card__lane");

        static string EnergyValueOf(DeployView view) =>
            view.Q<SectionCard>("energy").Q<Label>(className: "deploy-view__pill-value").text;

        static string IntegrityValueOf(DeployView view) =>
            view.Q<SectionCard>("integrity").Q<Label>(className: "deploy-view__pill-value").text;
    }
}
