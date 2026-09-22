using System;
using System.Collections.Generic;
using System.Linq;
using Broodline.Api;
using Broodline.Model;
using Broodline.UI.Components;
using Broodline.UI.Screens;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Broodline.UI.Tests
{
    /// The five screens the first hour adds: Founder Naming, Splice Reveal,
    /// Lineage, Campaign Select and the Trait Codex sheet.
    ///
    /// SAME TWO CONSTRAINTS AS EVERY SCREEN SUITE ON THIS BRANCH, restated
    /// because they shape what is asserted:
    ///
    /// 1. A bare `new SomeView()` has no attached `Panel`, so no `ClickEvent`
    ///    can be dispatched, `Button.clicked` cannot be raised, and a
    ///    `TextField`'s `ChangeEvent` never fires. `ComponentTests`'s class
    ///    comment records the dead ends (`Clickable.SimulateSingleClick` is
    ///    internal; `VisualElement.GetCallbackCount<T>()` does not exist).
    ///    So `FounderNamingView` exposes `Confirm()` and `Skip()` as public
    ///    methods and the buttons call them: the RULES are tested for real
    ///    and only the click DISPATCH is trusted by inspection, which is a
    ///    much smaller trusted surface than a rule living inside a closure
    ///    no test can reach. `CampaignSelectView`'s row-level `onPick` has
    ///    no such method and is trusted the same way `RosterView`'s
    ///    per-card `onSelect` is; what IS proven there is the enabled
    ///    state, which is the thing that actually gates the tap.
    ///
    /// 2. **The view renders the model's sentence, and the sentence is not
    ///    in the view.** `WaveScreensTests` states the reasoning; the
    ///    consequence here is that assertions come in pairs - one that a
    ///    fact reaches the screen, one that a DIFFERENT fact produces a
    ///    DIFFERENT screen. A view holding a literal passes the first and
    ///    fails the second.
    public class FirstHourScreensTests
    {
        // ---------------------------------------------------------------
        // Fixtures
        // ---------------------------------------------------------------

        static readonly Guid F = Guid.Parse("11111111-1111-1111-1111-111111111111");
        static readonly Guid A = Guid.Parse("22222222-2222-2222-2222-222222222222");
        static readonly Guid B = Guid.Parse("33333333-3333-3333-3333-333333333333");
        static readonly Guid C = Guid.Parse("44444444-4444-4444-4444-444444444444");

        static CreatureDto Creature(string species, string name = null, bool founder = false,
            int generation = 1, Guid? id = null)
        {
            return new CreatureDto
            {
                CreatureId = id ?? Guid.NewGuid(),
                Species = species,
                Generation = generation,
                Trait1 = "Chill", Tier1 = 1,
                Trait2 = "Taunt", Tier2 = 2,
                Instinct = "Forage",
                Name = name,
                IsFounder = founder,
                CommittedTo = null,
            };
        }

        static LineageNode Node(string species, Guid id, int generation = 1,
            bool founder = false, string consumedAt = null, bool mutated = false,
            Guid? parentA = null, Guid? parentB = null, string name = null)
        {
            return new LineageNode
            {
                CreatureId = id,
                Species = species,
                Generation = generation,
                IsFounder = founder,
                Name = name,
                ParentA = parentA,
                ParentB = parentB,
                ConsumedAt = consumedAt,
                Pruned = false,
                Mutated = mutated,
                Trait1 = "Chill", Tier1 = 1,
                Trait2 = "Taunt", Tier2 = 2,
            };
        }

        static TraitSummary Trait(string id, string species, string counters)
        {
            return new TraitSummary { Id = id, Species = species, Counters = counters };
        }

        static SpliceCommitResponse CommitOf(CreatureDto child)
        {
            return new SpliceCommitResponse
            {
                Child = child,
                SpliceId = Guid.NewGuid(),
                Seed = "7",
                Balance = 2,
            };
        }

        // ---------------------------------------------------------------
        // Founder Naming - beat 4, bible 3.3
        // ---------------------------------------------------------------

        static FounderNamingView BoundNaming(CreatureDto founder, Action<string> onName,
            Action onSkip, string defaultName = null)
        {
            var view = new FounderNamingView();
            view.Bind(founder, defaultName ?? FounderNamingScreen.DefaultFor(founder), onName, onSkip);
            return view;
        }

        [Test]
        public void FounderNaming_OffersTheSpeciesAsItsDefault_AndSubmitsTrimmed()
        {
            // bible 3.3: "a sensible default". The species is the only name
            // the client has that is about THIS creature.
            string named = null;
            var view = BoundNaming(Creature("Hollow", founder: true), n => named = n, () => { });

            Assert.AreEqual("Hollow", view.Q<TextField>("name").value);

            // The contrast. A view that wrote "Hollow" into the field itself
            // passes the line above and fails this one.
            var other = BoundNaming(Creature("Loam", founder: true), _ => { }, () => { });
            Assert.AreEqual("Loam", other.Q<TextField>("name").value);

            view.Q<TextField>("name").value = "  Ash ";
            view.Confirm();
            Assert.AreEqual("Ash", named);
        }

        [Test]
        public void FounderNaming_RefusesABlankName_AndSaysSoRatherThanSubmittingIt()
        {
            // `CreatureNameRequest.name` is required server-side, so a blank
            // confirm is a round trip that can only be refused. The screen
            // refuses it first and names the other button.
            string named = null;
            var view = BoundNaming(Creature("Hollow", founder: true), n => named = n, () => { });

            view.Q<TextField>("name").value = "   ";
            view.Confirm();

            Assert.IsNull(named, "a blank name must not reach the outbox");
            Assert.AreEqual(FounderNamingScreen.EmptyNameBlocker, view.Q<Label>("blocker").text);
            StringAssert.Contains(FounderNamingScreen.SkipLabel, view.Q<Label>("blocker").text);

            // And it clears once a real name is given - a blocker that stuck
            // would read as a refusal of the name that actually worked.
            view.Q<TextField>("name").value = "Ash";
            view.Confirm();
            Assert.AreEqual("Ash", named);
            Assert.AreEqual(string.Empty, view.Q<Label>("blocker").text);
        }

        [Test]
        public void FounderNaming_SkipIsARealAnswerAndLeavesTheCreatureUnnamed()
        {
            // bible 3.3: the prompt is optional and "all five are renameable
            // at any time from the Roster". Skipping must reach the caller,
            // and must NOT submit the default as though it were chosen.
            string named = null;
            var skipped = false;
            var view = BoundNaming(Creature("Hollow", founder: true), n => named = n, () => skipped = true);

            view.Skip();

            Assert.IsTrue(skipped);
            Assert.IsNull(named, "skipping must not submit the default as a name");
        }

        [Test]
        public void FounderNaming_TakesEverySentenceFromTheModel()
        {
            var hollow = Creature("Hollow", founder: true);
            var view = BoundNaming(hollow, _ => { }, () => { });

            var prompt = view.Q<Label>("prompt").text;
            Assert.AreEqual(FounderNamingScreen.Prompt(hollow), prompt);
            Assert.IsNotEmpty(prompt);
            Assert.AreEqual(FounderNamingScreen.ConfirmLabel, view.Q<Button>("confirm").text);
            Assert.AreEqual(FounderNamingScreen.SkipLabel, view.Q<Button>("skip").text);

            // A different creature must produce a different prompt, or the
            // equality above proves only that two constants match.
            var other = BoundNaming(Creature("Loam", founder: true), _ => { }, () => { });
            Assert.AreNotEqual(prompt, other.Q<Label>("prompt").text);
        }

        [Test]
        public void FounderNaming_WithNoDefaultSupplied_StillOffersOne()
        {
            // bible 3.3 asks for a default; a caller that forgets to compute
            // one must not be able to ship an empty prompt.
            var view = new FounderNamingView();
            view.Bind(Creature("Hollow", founder: true), defaultName: null, onName: _ => { }, onSkip: () => { });

            Assert.AreEqual("Hollow", view.Q<TextField>("name").value);
        }

        [Test]
        public void FounderNaming_WearsTheOnboardingStepFrame()
        {
            // `Onboarding.dc.html` step 1: five progress marks with the first
            // lit, and a counter beside them. Lines 34-40 draw them as a
            // full-width row above the hero, which is where they are now -
            // a direct child of the scaffold's CONTENT region. Task 14 had
            // them in the header slot because a title column was growing
            // beside them; there is no header on this screen any more.
            var view = BoundNaming(Creature("Hollow", founder: true), _ => { }, () => { });
            var progress = view.Q<VisualElement>("progress");
            Assert.IsNotNull(progress, "the step frame is not on the screen at all");

            var pips = progress.Query<VisualElement>(className: "progress-pip").ToList();
            Assert.AreEqual(5, pips.Count, "onboarding is five steps");

            var lit = pips.Where(p => p.ClassListContains("progress-pip--current")).ToList();
            Assert.AreEqual(1, lit.Count, "beat 4 is step 1 of 5, so exactly one pip is lit");
            Assert.AreSame(pips[0], lit[0], "the lit pip is the first one");

            Assert.AreEqual(FounderNamingScreen.Step, progress.Q<Label>("step").text);

            // THE RE-PARENT ITSELF, which nothing else would notice if the
            // row were left behind in the screen's own tree or stranded in a
            // header that no longer renders. Asked of each container rather
            // than of `progress.parent`, because `Content` is a ScrollView
            // and the row lands two levels down inside its viewport and
            // content container rather than directly under it.
            var scaffold = view.Q<VisualElement>(className: ScreenScaffold.UssClassName);
            Assert.IsNull(view.Q<VisualElement>("header-slot").Q<VisualElement>("progress"),
                "the pips are still in the header slot, on a screen whose header is hidden - "
                + "so the step frame does not render at all");
            Assert.IsNotNull(scaffold.Q<ScrollView>("content").Q<VisualElement>("progress"),
                "the pips are not inside the scaffold's content region");

            // The caveat that makes `SkipLabel` a real answer. It was the
            // scaffold's footer note until Task 14 and is now the violet tip
            // panel inside the card, which is where the handoff draws its
            // own - so it is still ON the screen and still the model's
            // sentence rather than a literal in the markup.
            Assert.AreEqual(FounderNamingScreen.Note, view.Q<Label>("note-text").text);
            Assert.IsNotEmpty(FounderNamingScreen.Note);
        }

        /// THE COMPOSITION TASK 14b CORRECTED, pinned so the four screen
        /// tasks after it copy the right one.
        ///
        /// `Onboarding.dc.html` has no page header: line 33 starts the column
        /// with the progress row, and lines 158-169 put the kicker, the 26px
        /// title, the body and the note in ONE white card. Task 14 split that
        /// across two containers - kicker and title in the scaffold's header,
        /// body in a card - which is what made a 21px page title and a 19px
        /// card line read as two competing display lines.
        ///
        /// THE HEADER ASSERTION READS THE INLINE STYLE, NOT `resolvedStyle`.
        /// These trees have no panel, and `Flex` is also the default computed
        /// value - so a `resolvedStyle` read would pass on a scaffold that had
        /// never set anything, which is the whole regression. `style.display
        /// .value` is the property `ScreenScaffold`'s constructor actually
        /// writes.
        [Test]
        public void FounderNaming_HasNoPageHeader_AndCarriesItsKickerAndTitleInTheCard()
        {
            var view = BoundNaming(Creature("Hollow", founder: true), _ => { }, () => { });

            Assert.AreEqual(DisplayStyle.None, view.Q<VisualElement>("header").style.display.value,
                "the founder screen drew a page header; the handoff's onboarding has none, and a "
                + "21px page title beside a 26px card title is the type ladder this task removed");
            Assert.AreEqual(string.Empty, view.Q<Label>("title").text,
                "the scaffold was given a page title as well as a card title");

            var kicker = view.Q<Label>("kicker");
            var heading = view.Q<Label>("card-title");
            Assert.AreEqual(FounderNamingScreen.Eyebrow, kicker.text);
            Assert.AreEqual(FounderNamingScreen.Title, heading.text);
            Assert.IsTrue(kicker.ClassListContains("t-micro"),
                "the kicker lost the 10px uppercase treatment the handoff's `.lbl` is");

            // ONE CARD, AND THAT IS THE STRUCTURAL CLAIM. Four elements, one
            // `SectionCard` body, in the handoff's order.
            var body = heading.parent;
            Assert.IsTrue(body.ClassListContains("section-card__body"),
                "the card's type ladder is not inside a SectionCard body");
            foreach (var named in new[] { "kicker", "card-title", "prompt", "name", "blocker", "note" })
            {
                Assert.AreSame(body, view.Q<VisualElement>(named).parent,
                    "`" + named + "` is not in the same card as the rest of the step; the handoff "
                    + "draws all of it in one white surface");
            }
            Assert.AreEqual(0, body.IndexOf(kicker), "the kicker is not the card's first line");
            Assert.AreEqual(1, body.IndexOf(heading), "the title does not follow the kicker");
            Assert.AreEqual(2, body.IndexOf(view.Q<Label>("prompt")), "the body copy does not follow the title");

            // AND THE HERO IS STILL A CARD OF ITS OWN, above that one. The
            // handoff's hero is a separate surface at `flex: 1`.
            Assert.AreNotSame(body, view.Q<VisualElement>("founder").parent,
                "the founder was folded into the words card; the handoff draws it in a band of its own");
        }

        [Test]
        public void FounderNaming_TurnsTheFounderWhenItIsGivenOne_AndShowsTheSpriteStackWhenItIsNot()
        {
            // `HeroSlot` has two forms and the live one REMOVES its three
            // sprite layers rather than leaving them empty behind the stage -
            // that component's own contract, and `Q("body")` is the
            // discriminator it names. A slot answering to both names is a
            // slot no test can tell apart, so both halves are asserted.
            var founder = Creature("Vetch", founder: true);

            var baked = new FounderNamingView();
            baked.Bind(founder, "Ash", _ => { }, () => { });
            var bakedSlot = baked.Q<HeroSlot>();
            Assert.IsNotNull(bakedSlot, "the founder is not in a hero slot at all");
            Assert.IsNotNull(bakedSlot.Q<VisualElement>("body"),
                "a slot with no portrait must be the sprite form, which has a body layer");
            Assert.IsNull(baked.Q<CreatureStage>(),
                "nothing was passed to turn, so there must be no stage");

            // The contrast. A view that ignored `portrait` entirely would
            // pass every line above and fail every line below.
            var live = new FounderNamingView();
            live.Bind(founder, "Ash", _ => { }, () => { }, new UnityEngine.Texture2D(2, 2));
            var liveSlot = live.Q<HeroSlot>();
            Assert.IsNotNull(live.Q<CreatureStage>(),
                "a portrait was passed and nothing is showing it");
            Assert.IsNull(liveSlot.Q<VisualElement>("body"),
                "the live slot kept its sprite layers, so no test can tell the two forms apart");
        }

        // ---------------------------------------------------------------
        // The handoff's eyebrow, on both of Task 14's screens
        // ---------------------------------------------------------------

        [Test]
        public void TheFirstTwoScreens_SayWhereInTheAppTheyAre_InCasingUssCannotApply()
        {
            // The handoff renders every kicker through `.lbl {
            // text-transform: uppercase }`. UI Toolkit has no
            // `text-transform` at all, so the casing has to be in the string,
            // and these two constants are the only place it is written.
            // Asserted against the CONSTANT and never against a repeated
            // literal - a test holding "YOUR GENE ARK" would still pass if
            // the screen stopped reading the model.
            // TWO SCREENS, TWO DIFFERENT KICKERS, AND THEY ARE NOT IN THE
            // SAME PLACE ANY MORE. Campaign select has a page header and its
            // kicker is the scaffold's eyebrow row; founder naming has no
            // header at all and carries its kicker inside the card, which is
            // what `Onboarding.dc.html` does. The constants are shared; the
            // containers are not.
            var naming = BoundNaming(Creature("Hollow", founder: true), _ => { }, () => { });
            Assert.AreEqual(FounderNamingScreen.Eyebrow, naming.Q<Label>("kicker").text);

            var campaign = BoundCampaign(highestWaveCleared: 2);
            var eyebrow = campaign.Q<Label>("eyebrow");
            Assert.AreEqual(CampaignSelectScreen.Eyebrow, eyebrow.text);

            // `style.display.value`, NOT `resolvedStyle.display`, AND THE
            // DIFFERENCE IS WHETHER THIS LINE MEANS ANYTHING. These trees
            // have no panel, and `Flex` is ALSO the default computed value -
            // so the resolvedStyle form this test used to carry would have
            // passed on a scaffold whose `Eyebrow` setter never ran, which is
            // exactly the regression the message claims to catch. The inline
            // style is the property that setter writes, and
            // `ScaffoldTests.AScaffoldWithNoEyebrowReservesNoRowForOne` reads
            // it the same way for the same reason.
            Assert.AreEqual(DisplayStyle.Flex, eyebrow.style.display.value,
                "the eyebrow row is hidden, so the screen renders with no kicker at all");

            // And the casing is a property of the constants rather than
            // something that happened to be typed once. `ToUpperInvariant`
            // rather than a spelled-out literal, for the reason above.
            Assert.AreEqual(FounderNamingScreen.Eyebrow.ToUpperInvariant(),
                FounderNamingScreen.Eyebrow, "the eyebrow must ship uppercase; USS cannot transform it");
            Assert.AreEqual(CampaignSelectScreen.Eyebrow.ToUpperInvariant(),
                CampaignSelectScreen.Eyebrow, "the eyebrow must ship uppercase; USS cannot transform it");

            // Two screens, two different places in the app. Equal eyebrows
            // would mean the header stopped saying anything.
            Assert.AreNotEqual(FounderNamingScreen.Eyebrow, CampaignSelectScreen.Eyebrow);
        }

        // ---------------------------------------------------------------
        // Splice Reveal - beat 7, splice_confirm_spec 5
        // ---------------------------------------------------------------

        static SpliceRevealView BoundReveal(bool mutated, CreatureDto parentA, CreatureDto parentB)
        {
            var view = new SpliceRevealView();
            view.Bind(CommitOf(Creature("Hollow", generation: 2, id: C)),
                parentA, parentB, mutated, next: () => { });
            return view;
        }

        [Test]
        public void SpliceReveal_CelebratesTheMutationAndKeepsTheConsumptionLine()
        {
            // splice_confirm_spec 5: "parents are consumed and their traits
            // live on in the pedigree" - a confirmation now, not a disclosure.
            var ash = Creature("Vetch", name: "Ash", founder: true, id: A);
            var ember = Creature("Ember", id: B);
            var view = BoundReveal(mutated: true, ash, ember);

            Assert.IsTrue(view.ClassListContains(SpliceRevealView.MutatedUssClassName));
            StringAssert.Contains("live on", view.Q<Label>("consumption").text);

            // Both parents named in the line, by the SAME vocabulary the
            // confirm dialog used - splice_confirm_spec 3: "three different
            // phrasings of the same fact reads as evasion."
            Assert.AreEqual(SpliceRevealScreen.Consumption(ash, ember),
                view.Q<Label>("consumption").text);
            StringAssert.Contains("Ash", view.Q<Label>("consumption").text);
            StringAssert.Contains("Ember", view.Q<Label>("consumption").text);
        }

        [Test]
        public void SpliceReveal_WithoutAMutationSaysSoWithoutApologising()
        {
            // splice_confirm_spec 7: "No 'SPLICE FAILED' state ... there is
            // no failure outcome." And the contrast that makes the `mutated`
            // class mean something: a view that always added it would pass
            // the test above.
            var plain = BoundReveal(mutated: false, Creature("Vetch", id: A), Creature("Ember", id: B));

            Assert.IsFalse(plain.ClassListContains(SpliceRevealView.MutatedUssClassName));
            Assert.AreEqual(SpliceRevealScreen.Headline(false), plain.Q<Label>("headline").text);
            Assert.AreNotEqual(SpliceRevealScreen.Headline(true), plain.Q<Label>("headline").text);
        }

        [Test]
        public void SpliceReveal_KeepsBothParentsOnTheScreenThatSaysTheyAreGone()
        {
            // The reveal is where the lesson is confirmed, so the parents are
            // still drawn. A screen that removed them would teach the
            // opposite of what the tree shows one tap later.
            var view = BoundReveal(mutated: true, Creature("Vetch", id: A), Creature("Ember", id: B));

            Assert.IsNotNull(view.Q(A.ToString()), "parent A is missing from the reveal");
            Assert.IsNotNull(view.Q(B.ToString()), "parent B is missing from the reveal");
            Assert.IsNotNull(view.Q(C.ToString()), "the child is missing from the reveal");
        }

        /// A REVEAL HANDED NEITHER PARENT SAYS SO. `AddParent` skips a null,
        /// so before Phase 8 Task 10 two nulls rendered as the same screen
        /// minus its whole lesson - the parents simply absent, with no
        /// sentence saying whether they were consumed or never arrived.
        /// bible 2.1 makes that row the FTUE's teaching moment.
        [Test]
        public void SpliceReveal_WithNoParents_SaysSoInsteadOfDroppingTheLesson()
        {
            var view = BoundReveal(mutated: false, null, null);

            var empty = view.Q<EmptyState>();
            Assert.IsNotNull(empty, "a reveal with no parents rendered the row as nothing at all");
            Assert.AreEqual(SpliceRevealScreen.NoParentsMessage, empty.Q<Label>("message").text);
        }

        /// THE HERO CARD IS THE ONE PLACE IN THE APP THAT EARNS `.elev-2`,
        /// and the one consumer `reveal-flare` was written for. A transition
        /// class goes stale silently in both directions: nothing renders
        /// differently when it stops being applied, and a screenshot of a
        /// 220ms transition is one frame either way. This assertion caught
        /// the second direction - the class was applied and pinned here while
        /// Motion.uss carried no state change to run it against, so the flare
        /// existed on paper and never played. `Bind` now toggles
        /// `reveal-flare--hidden`, which is what actually drives it.
        [Test]
        public void SpliceReveal_TheHeroCardWearsTheRaisedElevationAndTheRevealTransition()
        {
            var view = BoundReveal(mutated: true, Creature("Vetch", id: A), Creature("Ember", id: B));
            var hero = view.Q<VisualElement>("child");

            Assert.IsNotNull(hero);
            Assert.IsTrue(hero.ClassListContains("elev-2"),
                "the reveal's hero card sits at the same elevation as an ordinary section card");
            Assert.IsTrue(hero.ClassListContains("reveal-flare"),
                "Motion.uss's reveal transition is applied to nothing, so the payoff snaps in");
        }

        // ---------------------------------------------------------------
        // Lineage - beat 8
        // ---------------------------------------------------------------

        /// The DIRECTED binding - the one the director uses at beat 8 - so
        /// the tree tests below all exercise that path. `BindStandalone` has
        /// its own test; it is the named opt-out, not the default.
        static LineageView BoundLineage(Guid highlight, params LineageNode[] nodes)
        {
            var view = new LineageView();
            view.Bind(new LineageResponse { Nodes = nodes }, highlight, next: () => { });
            return view;
        }

        [Test]
        public void Lineage_ShowsConsumedParentsUnderTheChild_AndMarksTheFounder()
        {
            var view = BoundLineage(F,
                Node("Hollow", F, founder: true, name: "Ash"),
                Node("Vetch", A, consumedAt: "2026-09-16T00:00:00Z"),
                Node("Ember", B, consumedAt: "2026-09-16T00:00:00Z"),
                Node("Hollow", C, generation: 2, mutated: true, parentA: A, parentB: B));

            // Nobody is missing - individuals are consumed, the record survives.
            Assert.AreEqual(4, view.Query(className: LineageView.NodeUssClassName).ToList().Count);

            Assert.IsTrue(view.Q(A.ToString()).ClassListContains(LineageView.ConsumedUssClassName));
            Assert.IsTrue(view.Q(F.ToString()).ClassListContains(LineageView.FounderUssClassName));
            Assert.IsTrue(view.Q(C.ToString()).ClassListContains(LineageView.MutatedUssClassName));

            // Two generations, and the child is in the SECOND row - the
            // ordering is `LineageScreen.Generations`' ascending rule, which
            // is what puts the Founder roots first.
            var rows = view.Query(className: LineageView.GenerationRowUssClassName).ToList();
            Assert.AreEqual(2, rows.Count);
            Assert.IsNotNull(rows[1].Q(C.ToString()));
            Assert.IsNotNull(rows[0].Q(F.ToString()));

            // The contrast for each marker: the child is not consumed, and
            // the consumed parents are not Founders and did not mutate. A
            // view that stamped every class on every node passes everything
            // above.
            Assert.IsFalse(view.Q(C.ToString()).ClassListContains(LineageView.ConsumedUssClassName));
            Assert.IsFalse(view.Q(A.ToString()).ClassListContains(LineageView.FounderUssClassName));
            Assert.IsFalse(view.Q(A.ToString()).ClassListContains(LineageView.MutatedUssClassName));
        }

        /// bible 10.4: "Colour never carries information alone."
        ///
        /// THIS SCREEN WAS THE ONE PLACE IN THE APP THAT BROKE IT, and it
        /// broke it silently: a lineage node draws no creature, so Founder
        /// and Mutated were each carried by a 3px coloured border and by
        /// nothing at all besides. Every assertion in
        /// `Lineage_ShowsConsumedParentsUnderTheChild_AndMarksTheFounder`
        /// passes on that version, because a USS class IS the colour - the
        /// class list cannot tell you whether a second channel exists.
        ///
        /// It is also what closed Task 6's deferred palette collision. Amber
        /// IS Skitter and violet IS Hollow at dE 0.0, so while colour was the
        /// only channel here the rails read as species marks; no colour
        /// moved, the words were added instead. If this test is ever deleted
        /// to make a redesign pass, that decision comes back open - see
        /// implementation/results/species-collision.md.
        [Test]
        public void Lineage_StatesEveryFactInWordsAndNotOnlyInColour()
        {
            var view = BoundLineage(F,
                Node("Hollow", F, founder: true, name: "Ash"),
                Node("Vetch", A, consumedAt: "2026-09-16T00:00:00Z"),
                Node("Ember", B),
                Node("Hollow", C, generation: 2, mutated: true, parentA: A, parentB: B));

            // Each coloured class has a word beside it. Read off the TEXT,
            // not off a class name, because a class name is the colour.
            AssertMarked(view, F, LineageScreen.FounderLabel);
            AssertMarked(view, C, LineageScreen.MutatedLabel);
            AssertMarked(view, A, LineageScreen.ConsumedLabel);

            // The contrast: a node with nothing true about it says nothing.
            // B is an ordinary living non-founder, and it gets NO marks row
            // at all rather than an empty one - the blank-banner shape this
            // phase found eight times, once on this very screen.
            var plain = view.Q(B.ToString());
            Assert.IsNull(plain.Q<VisualElement>(className: LineageView.MarksUssClassName),
                "a node with nothing to say built a marks row anyway - that is the ninth blank banner");
            Assert.AreEqual(0, plain.Query<Label>(className: LineageView.MarkUssClassName).ToList().Count);

            // And a marker is not stamped on everybody: the founder did not
            // mutate and was not consumed.
            var founderWords = Words(view, F);
            CollectionAssert.DoesNotContain(founderWords, LineageScreen.MutatedLabel);
            CollectionAssert.DoesNotContain(founderWords, LineageScreen.ConsumedLabel);
        }

        static List<string> Words(LineageView view, Guid id)
        {
            return view.Q(id.ToString())
                       .Query<Label>(className: LineageView.MarkUssClassName)
                       .ToList()
                       .ConvertAll(l => l.text);
        }

        static void AssertMarked(LineageView view, Guid id, string word)
        {
            CollectionAssert.Contains(Words(view, id), word,
                $"{word} is carried by the rail's colour and by nothing else - bible 10.4");
        }

        [Test]
        public void Lineage_OffersAWayOnwardOnlyWhenThereIsSomewhereToGo()
        {
            // Beat 8 ends session one, but `Ftue.Derive` answers `Lineage` on
            // every launch until wave 6 is cleared - so a tree with no way
            // forward parks a returning player on it and never lets them
            // reach the designed first loss.
            var directed = new LineageView();
            directed.Bind(Tree(), F, next: () => { });
            Assert.AreNotEqual(DisplayStyle.None, directed.Q<Button>("next").style.display.value);
            Assert.AreEqual(LineageScreen.NextLabel, directed.Q<Button>("next").text);

            // ...and a plain tab destination with nothing waiting on it gets
            // a hidden button rather than a dead one.
            var standalone = new LineageView();
            standalone.BindStandalone(Tree(), F);
            Assert.AreEqual(DisplayStyle.None, standalone.Q<Button>("next").style.display.value);
        }

        [Test]
        public void Lineage_ADirectedTurnWithNowhereToGoIsRefused()
        {
            // `next` is REQUIRED on `Bind`, so omitting it does not compile -
            // which is the actual guard. This covers the remaining hole: a
            // caller that passes a null it computed. Hanging the walk on a
            // hidden button is the failure; throwing names it.
            var view = new LineageView();
            Assert.Throws<ArgumentNullException>(() => view.Bind(Tree(), F, next: null));

            // And the named opt-out is the ONLY way to the hidden button.
            Assert.DoesNotThrow(() => view.BindStandalone(Tree(), F));
        }

        static LineageResponse Tree()
        {
            return new LineageResponse { Nodes = new[] { Node("Hollow", F, founder: true) } };
        }

        [Test]
        public void Lineage_HighlightsOnlyTheCreatureTheSessionIsAbout()
        {
            var view = BoundLineage(F,
                Node("Hollow", F, founder: true, name: "Ash"),
                Node("Vetch", A),
                Node("Hollow", C, generation: 2, parentA: F, parentB: A));

            Assert.IsTrue(view.Q(F.ToString()).ClassListContains(LineageView.HighlightUssClassName));
            Assert.IsFalse(view.Q(A.ToString()).ClassListContains(LineageView.HighlightUssClassName));
            Assert.IsFalse(view.Q(C.ToString()).ClassListContains(LineageView.HighlightUssClassName));
        }

        [Test]
        public void Lineage_HighlightingNobodyHighlightsNobody()
        {
            // `Guid.Empty` is "no highlight", and it must not match a node.
            var view = BoundLineage(Guid.Empty, Node("Hollow", F, founder: true));

            Assert.IsFalse(view.Q(F.ToString()).ClassListContains(LineageView.HighlightUssClassName));
        }

        [Test]
        public void Lineage_AnEmptyTreeSaysSoRatherThanRenderingBlank()
        {
            // An `EmptyState` as of Phase 8 Task 9, where it was a `notice`
            // Label before. The assertion is STRONGER for it: the old notice
            // was an amber-tinted, padded banner that existed on every tree
            // and merely emptied its text, so it drew a blank amber strip
            // under the title of every populated capture. Absence is now the
            // absence of an element.
            var empty = BoundLineage(Guid.Empty);
            var state = empty.Q<VisualElement>(className: EmptyState.UssClassName);
            Assert.IsNotNull(state, "an empty tree rendered blank");
            Assert.AreEqual(LineageScreen.EmptyNotice, state.Q<Label>("message").text);
            Assert.AreEqual(0, empty.Query(className: LineageView.GenerationRowUssClassName).ToList().Count);

            // And the empty state is GONE once there is a tree - a permanent
            // "no lineage yet" beside four nodes is the same defect pointed
            // the other way.
            var full = BoundLineage(Guid.Empty, Node("Hollow", F, founder: true));
            Assert.IsNull(full.Q<VisualElement>(className: EmptyState.UssClassName));
        }

        [Test]
        public void Lineage_NamesEachNodeFromTheModel_NameOverSpecies()
        {
            var named = Node("Hollow", F, founder: true, name: "Ash");
            var unnamed = Node("Vetch", A, generation: 2);
            var view = BoundLineage(Guid.Empty, named, unnamed);

            Assert.AreEqual(LineageScreen.NodeLabel(named), view.Q(F.ToString()).Q<Label>("label").text);
            StringAssert.Contains("Ash", view.Q(F.ToString()).Q<Label>("label").text);
            // The species fallback, and the generation badge that tells two
            // creatures of one species apart.
            StringAssert.Contains("Vetch", view.Q(A.ToString()).Q<Label>("label").text);
            StringAssert.Contains("G2", view.Q(A.ToString()).Q<Label>("label").text);
        }

        // ---------------------------------------------------------------
        // Campaign Select - design 5.2
        // ---------------------------------------------------------------

        static readonly int[] Authored = { 1, 2, 6, 7 };

        static CampaignSelectView BoundCampaign(int highestWaveCleared)
        {
            var view = new CampaignSelectView();
            view.Bind(Authored, highestWaveCleared, onPick: _ => { });
            return view;
        }

        [Test]
        public void CampaignSelect_LocksEverythingPastTheNextWave()
        {
            var view = BoundCampaign(highestWaveCleared: 2);

            // issueWave's forward branch: only the next AUTHORED wave.
            Assert.IsTrue(view.Q("wave-6").enabledSelf);
            Assert.IsFalse(view.Q("wave-7").enabledSelf);
        }

        [Test]
        public void CampaignSelect_TheNextWaveIsTheNextAuthoredId_NotClearedPlusOne()
        {
            // The whole reason this rule is mirrored rather than computed:
            // this bundle authors 1, 2, 6 and 7. `cleared + 1` is 3 for a
            // player who has cleared 2, and 3 is not a wave - the arithmetic
            // version locks the only wave they can play. `issueWave`'s own
            // comment records the same correction on the server.
            Assert.AreEqual(6, CampaignSelectScreen.NextWave(Authored, 2));
            Assert.AreEqual(1, CampaignSelectScreen.NextWave(Authored, 0));
            Assert.AreEqual(7, CampaignSelectScreen.NextWave(Authored, 6));
            Assert.IsNull(CampaignSelectScreen.NextWave(Authored, 7));
        }

        [Test]
        public void CampaignSelect_ClearedWavesStayPlayable_AndTheFreshPlayerGetsOnlyWaveOne()
        {
            var cleared2 = BoundCampaign(highestWaveCleared: 2);
            Assert.IsTrue(cleared2.Q("wave-1").enabledSelf, "a cleared wave is replayable");
            Assert.IsTrue(cleared2.Q("wave-2").enabledSelf, "a cleared wave is replayable");

            var fresh = BoundCampaign(highestWaveCleared: 0);
            Assert.IsTrue(fresh.Q("wave-1").enabledSelf);
            Assert.IsFalse(fresh.Q("wave-2").enabledSelf);
            Assert.IsFalse(fresh.Q("wave-6").enabledSelf);
        }

        [Test]
        public void CampaignSelect_ThreeStatesReadAsThreeDifferentThings()
        {
            // "Cleared", "Next" and "Locked" must never read the same - the
            // rule `RosterScreen.IncompleteNotice` is written to.
            // `title` and `detail`, not this screen's old `label` and
            // `state`: Phase 8 Task 9 made a wave row an `OptionRow`, and
            // those are that component's two label names. The row's own
            // element name is still CampaignSelectScreen.RowName(id), which
            // is what every assertion below reaches it by, and every fact
            // asserted here is the same fact.
            var view = BoundCampaign(highestWaveCleared: 2);
            var states = new[] { "wave-1", "wave-2", "wave-6", "wave-7" }
                .Select(n => view.Q(n).Q<Label>("detail").text)
                .ToList();

            Assert.AreEqual(CampaignSelectScreen.StateLabel(1, Authored, 2), states[0]);
            Assert.AreEqual(CampaignSelectScreen.StateLabel(6, Authored, 2), states[2]);
            Assert.AreEqual(3, states.Distinct().Count(), "cleared / next / locked collapsed into fewer words");
            Assert.AreEqual(CampaignSelectScreen.RowLabel(6), view.Q("wave-6").Q<Label>("title").text);

            // A locked wave carries the padlock and a playable one does not -
            // the glyph is the state at a glance, beside the word.
            Assert.IsNotNull(view.Q("wave-7").Q<VisualElement>(className: "icon--lock"),
                "a locked wave has no padlock");
            Assert.IsNull(view.Q("wave-6").Q<VisualElement>(className: "icon--lock"),
                "the next playable wave is wearing a padlock");
        }

        [Test]
        public void CampaignSelect_PutsItsRowsOnAWhiteCard_AndMarksTheClearedOnes()
        {
            // `phase8-visual-review.md`'s one objectively out-of-spec
            // finding: an `OptionRow`'s --surface-sunk fill on --paper is
            // 1.0151:1, under WCAG 1.4.11's 3:1 for a non-text boundary. Of
            // the three fixes it offered, design section 4.1 took the third -
            // compose the rows inside a `SectionCard` so the fill sits on
            // white. This is that arrangement, pinned: a later refactor that
            // lifted the rows back out would restore the defect silently,
            // because nothing else in this suite can see a fill.
            var view = BoundCampaign(highestWaveCleared: 2);

            var card = view.Q<SectionCard>();
            Assert.IsNotNull(card, "the wave list is not on a card at all");
            Assert.IsNotNull(card.Body.Q(CampaignSelectScreen.RowName(6)),
                "the rows are somewhere other than inside the card's body");

            // The cleared mark, and the locked one's opposite number: a
            // beaten wave wears a green chip the way a locked one wears a
            // padlock. Both are redundant with the detail line by design -
            // the mark is what makes a row readable at a glance.
            var cleared = view.Q(CampaignSelectScreen.RowName(1)).Q<Label>("cleared");
            Assert.IsNotNull(cleared, "a cleared wave carries no chip");
            Assert.AreEqual(CampaignSelectScreen.ClearedLabel, cleared.text);
            Assert.AreEqual(CampaignSelectScreen.StateLabel(1, Authored, 2), cleared.text,
                "the chip and the detail line must be the same word, from the same constant");

            // The contrast. A view that chipped every row would pass every
            // line above.
            Assert.IsNull(view.Q(CampaignSelectScreen.RowName(6)).Q<Label>("cleared"),
                "the next unplayed wave is wearing a Cleared chip");
            Assert.IsNull(view.Q(CampaignSelectScreen.RowName(7)).Q<Label>("cleared"),
                "a locked wave is wearing a Cleared chip");
        }

        // ---------------------------------------------------------------
        // Trait Codex - screen_inventory_v2 4
        // ---------------------------------------------------------------

        [Test]
        public void CodexSheet_ListsEveryTraitWithWhatItCounters()
        {
            var view = new CodexSheet();
            view.Bind(new[] { Trait("Chill", "Pale", "Courser"), Trait("Carapace", "Vetch", null) });

            StringAssert.Contains("Courser", view.Q("Chill").Q<Label>("counters").text);

            // bible 1.2: four traits "counter nothing by design". Answering
            // nothing is an answer, and that is legal.
            StringAssert.Contains("nothing", view.Q("Carapace").Q<Label>("counters").text.ToLower());

            // The contrast: a sheet that printed "counters nothing" for
            // everything satisfies the line above.
            StringAssert.DoesNotContain("nothing", view.Q("Chill").Q<Label>("counters").text.ToLower());
        }

        [Test]
        public void CodexSheet_TakesItsSentencesFromTheModel_AndNamesWhereATraitIsFound()
        {
            var chill = Trait("Chill", "Pale", "Courser");
            var view = new CodexSheet();
            view.Bind(new[] { chill });

            Assert.AreEqual(TraitCodexScreen.Counters(chill), view.Q("Chill").Q<Label>("counters").text);
            Assert.AreEqual(TraitCodexScreen.FoundOn(chill), view.Q("Chill").Q<Label>("found-on").text);
            StringAssert.Contains("Pale", view.Q("Chill").Q<Label>("found-on").text);

            // A trait the bundle names no species for gets no line rather
            // than an invented one.
            var anon = Trait("Screen", null, null);
            var anonView = new CodexSheet();
            anonView.Bind(new[] { anon });
            Assert.AreEqual(string.Empty, anonView.Q("Screen").Q<Label>("found-on").text);
        }

        [Test]
        public void CodexSheet_RendersOneEntryPerTrait_NamedAfterTheTrait()
        {
            // Named after the trait id so a deep link from a trait pip can
            // reach its own entry - the same convention every other screen on
            // this branch uses for creature cards.
            var view = new CodexSheet();
            view.Bind(new[]
            {
                Trait("Chill", "Pale", "Courser"),
                Trait("Taunt", "Vetch", "Lash"),
                Trait("Carapace", "Vetch", null),
            });

            Assert.AreEqual(3, view.Query(className: CodexSheet.EntryUssClassName).ToList().Count);
            Assert.IsNotNull(view.Q("Taunt"));
            StringAssert.Contains("Lash", view.Q("Taunt").Q<Label>("counters").text);
        }

        [Test]
        public void CodexSheet_DismissesRatherThanGoingBack_AndTakesThatWordFromTheModel()
        {
            // client_architecture 9: a sheet "overlays, it dismisses, it does
            // not push", so it carries a close and not a back chevron. This
            // was the one player-facing literal left in the five new screens
            // and nothing asserted it - which is how the Task 15 review's
            // three strings walked past a 382-line suite.
            var view = new CodexSheet();
            view.Bind(new[] { Trait("Chill", "Pale", "Courser") });

            Assert.AreEqual(TraitCodexScreen.DismissLabel, view.Q<Button>("dismiss").text);
            Assert.IsNotEmpty(TraitCodexScreen.DismissLabel);
        }
    }
}
