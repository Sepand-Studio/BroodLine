using System;
using System.Collections.Generic;
using Broodline.Api;
using Broodline.Model;
using Broodline.UI.Components;
using Broodline.UI.Screens;
using NUnit.Framework;
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
        [Test]
        public void WaveDefeat_ComposesTheScaffoldAndItsHeaderIsNotTheHeadline()
        {
            var view = BoundDefeat(LossWith(Breach("Courser", "Chill")));

            Assert.IsNotNull(view.Q(className: ScreenScaffold.UssClassName), "no scaffold");
            Assert.AreEqual(WaveDefeatScreen.Title, view.Q<Label>("title").text);
            Assert.AreNotEqual(view.Q<Label>("headline").text, view.Q<Label>("title").text);
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
    }
}
