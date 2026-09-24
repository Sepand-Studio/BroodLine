using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Broodline.Api;
using Broodline.UI.Components;
using Broodline.UI.Screens;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Broodline.UI.Tests
{
    /// The four Phase 6 screen models, given a view - `RegionView`,
    /// `RosterView`, `SpliceChamberView`, `DeployView`. The property every
    /// test in this file stands for is the same one: **the view shows the
    /// model's sentences verbatim, and reads the model's verdicts rather
    /// than re-deriving them.**
    ///
    /// Two constraints already established on this branch apply here too,
    /// rather than being re-discovered:
    ///
    /// 1. There is no way, from this assembly, to prove a registered
    ///    `ClickEvent`/`Button.clicked` callback actually FIRES - only that
    ///    one could. Dispatching a real event needs an attached `Panel`,
    ///    and a bare `new SomeView()` has none (`TabBarTests`'s class
    ///    comment, `ComponentTests`'s point 3). So button/card click wiring
    ///    below is trusted by inspection, the same way `TabBar`'s per-button
    ///    closures and `ConfirmDialog`'s scrim wiring are - what IS proven
    ///    is the structural precondition the wiring depends on: the right
    ///    button, named after the right id, in the right enabled state.
    ///
    /// 2. Every `*ScreenModel` (and `RosterScreen`, which is itself the
    ///    model) has `internal set` properties. Every fixture below builds
    ///    one through its real factory - `SpliceScreen.Build`,
    ///    `DeployScreen.Build`, `RegionScreen.Build`, `RosterScreen`'s own
    ///    methods - never an object initializer, which does not compile
    ///    from this assembly (`SpliceConfirmTests.cs` established this
    ///    first).
    public class ScreenBindingTests
    {
        // ---------------------------------------------------------------
        // Fixtures - same shape as SpliceConfirmTests.cs/ComponentTests.cs.
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

        static CreatureDto Named(string name) => Creature("Vetch Crawler", 4, name, founder: true);
        static CreatureDto Unnamed(string species, int generation) => Creature(species, generation, name: null, founder: false);
        static CreatureDto A() => Unnamed("Vetch Crawler", 4);
        static CreatureDto B() => Unnamed("Ember Skitter", 6);

        static SplicePreviewResponse Preview()
        {
            var r = new SplicePreviewResponse();
            r.Forecast = new SpliceForecast { Mutation = 0.09, Aberrant = 0.01 };
            r.Forecast.Combat2.Add(new CombatOutcomeDto { Trait = "Guard", Tier = 1, P = 0.55 });
            r.Forecast.Combat2.Add(new CombatOutcomeDto { Trait = "Ward", Tier = 2, P = 0.45 });
            r.Forecast.Instinct.Add(new Instinct { Instinct1 = "Forage", P = 1.0 });
            return r;
        }

        static RegionStateResponse RegionState(Roster roster, params Nodes[] nodes)
        {
            var state = new RegionStateResponse
            {
                RegionId = "verdant-shelf",
                Epoch = 1,
                Roster = roster,
            };
            foreach (var node in nodes) state.Nodes.Add(node);
            return state;
        }

        // ---------------------------------------------------------------
        // SpliceChamberView
        // ---------------------------------------------------------------

        [Test]
        public void SpliceChamberView_ShowsTheModelsDestructionNoticeAndCtaVerbatim()
        {
            var m = SpliceScreen.Build(Named("Ash"), Unnamed("Ember Skitter", 1), Preview());
            var v = new SpliceChamberView();
            v.Bind(m, lockedOut: new HashSet<Guid>(), onSplice: () => { });

            Assert.AreEqual(m.DestructionNotice, v.Q<Label>("destruction-notice").text);
            Assert.AreEqual(m.CoverageWarning ?? string.Empty, v.Q<Label>("coverage-warning").text);

            // THE CTA IS `CostCtaRow`'s BUTTON AS OF PHASE 9 TASK 16, AND ITS
            // LABEL IS STILL THE MODEL'S. The handoff pairs the button with a
            // cost block ("COST / 1") and calls the button "Begin Splice";
            // `broodline_splice_confirm_spec.md:83` replaces exactly that
            // string with the model's own, and section 1 names the handoff's
            // row as the gap it was written to close. The row is adopted, the
            // label is not - so this assertion is unchanged from Phase 8 and
            // now also proves the button inside the new row is the one bound.
            Assert.AreEqual(m.CtaLabel, v.Q<Button>("cta").text);

            // THE MUTATION PAIR IS `MutationBanner`'s NOW - the handoff's
            // amber strip at `Splice Chamber.dc.html:175` - and its sentence
            // is still `SpliceScreen.MutationLine` verbatim.
            var banner = v.Q<MutationBanner>("mutation");
            Assert.IsNotNull(banner, "the mutation pair is not rendered at all");
            var mutationText = banner.Q<Label>("text");
            Assert.AreEqual(SpliceScreen.MutationLine(m.Forecast), mutationText.text);

            // Phase 8 Task 10: the mutation line carries `t-num`, which is
            // the marker bible 10.6's tabular figures and 11px floor are both
            // enforced through - and this label states two percentages. The
            // marker moved onto `MutationBanner`'s own UXML in Task 16 so
            // that it survives the component swap.
            Assert.IsTrue(mutationText.ClassListContains("t-num"),
                "the mutation percentages are not marked as numerals, so nothing holds them "
                + "to the tabular face or to the 11px floor");

            // The forecast table renders the server's numbers and computes
            // none - client_architecture 9.1. Row count, not just presence:
            // a view that rendered a single static row would pass a
            // "not empty" check and fail this one.
            //
            // THE ROWS ARE `InheritanceBar`s AS OF TASK 16 - the handoff's
            // dot / trait / odds / bar / tag row at `:139-173` - where Phase
            // 8 used a `StatCell` to get `t-num` onto the percentage. The
            // class the count is keyed off is unchanged, which is why it is
            // a `public const` on the view.
            var rows = v.Query<InheritanceBar>(className: SpliceChamberView.ForecastRowUssClassName)
                .ToList();
            Assert.AreEqual(m.Forecast.Combat2.Count, rows.Count);

            // The trait is the bar's own label, by the shared formatter the
            // confirm dialog and the reveal use - splice_confirm_spec 3's
            // "three different phrasings of the same fact reads as evasion"
            // applies to "Guard I" as much as to a destruction notice.
            Assert.AreEqual(
                CreatureLabel.TraitWithTier(
                    m.Forecast.Combat2.First().Trait, m.Forecast.Combat2.First().Tier),
                rows[0].Q<Label>("trait").text);

            // AND THE ODDS ARE A WHOLE PERCENT, WHICH IS A CHANGE FROM
            // `SpliceScreen.Percent` AND IS THE COMPONENT'S OWN CONTRACT.
            // `InheritanceBar` writes `RoundToInt(p * 100) + "%"` because the
            // same value drives the bar beside it and the two must not
            // disagree about which trait is likeliest; `Percent` keeps one
            // decimal and still formats the mutation pair above, which is
            // splice_confirm_spec 2's "stated as two numbers". The two agree
            // on every whole percent and differ only where the server sends
            // fractions of one - pinned here rather than left to look
            // equivalent, which is the mistake the Phase 8 version of this
            // assertion was written to catch in the other direction.
            Assert.AreEqual("55%", rows[0].Q<Label>("odds").text);

            // The percentage wears `t-num`. It comes from InheritanceBar's
            // own UXML, which is the point of routing the forecast through
            // the component.
            Assert.IsTrue(rows[0].Q<Label>("odds").ClassListContains("t-num"));
        }

        /// THE TAG AND THE COLOUR ARE READINGS OF THE SERVER'S NUMBER, AND
        /// NEITHER IS COMPUTED HERE. The bar takes both already decided;
        /// this is the assertion that the screen decided them from the data
        /// it holds rather than from a rule of its own.
        [Test]
        public void SpliceChamberView_TagsEachForecastRowFromTheServersOwnOdds()
        {
            var a = A();
            var b = B();
            var preview = Preview();
            // 0.55 is above the handoff's 50% line and 0.45 is below it, so a
            // view that hardcoded either tag fails on one of the two rows.
            var m = SpliceScreen.Build(a, b, preview);

            var v = new SpliceChamberView();
            v.Bind(m, lockedOut: new HashSet<Guid>(), onSplice: () => { });

            var rows = v.Query<InheritanceBar>(className: SpliceChamberView.ForecastRowUssClassName)
                .ToList();
            Assert.AreEqual("DOM", rows[0].Q<Label>("tag").text);
            Assert.AreEqual("REC", rows[1].Q<Label>("tag").text);

            Assert.AreEqual(SpliceScreen.InheritanceTag(0.55), rows[0].Q<Label>("tag").text,
                "the view and the model disagree about where the DOM/REC line is");
        }

        /// THE BANNER COLLAPSES WHEN THE SERVER NAMED NO MUTATION WINDOW.
        /// `MutationLine` returns empty for a null forecast, and the banner
        /// is an amber gradient strip with an inset ring and a sparkle in it
        /// - so an empty one is pure decoration under a card that is saying
        /// the server named no outcomes at all.
        ///
        /// A NULL FORECAST REACHES THIS VIEW THROUGH `AfterCommit`'s SHAPE
        /// RATHER THAN `Build`'s: `Build` requires a preview, so the model it
        /// makes always HOLDS a forecast object. The state is reachable and
        /// `Bind` already treats it as such two blocks down, where it renders
        /// `ForecastEmptyMessage`; this is the same state seen from the
        /// banner's side, built the only way this assembly can build one -
        /// through the real factory, then read for the empty sentence.
        [Test]
        public void SpliceChamberView_AnEmptyMutationLine_CollapsesTheBannerRatherThanDrawingIt()
        {
            Assert.AreEqual(string.Empty, SpliceScreen.MutationLine(null),
                "sanity: a null forecast produces no mutation sentence");

            var banner = new MutationBanner();
            banner.Text = SpliceScreen.MutationLine(null);
            Assert.AreEqual(DisplayStyle.None, banner.style.display.value,
                "an empty mutation sentence still draws an amber strip");

            // And the live screen's banner IS shown when there is a sentence,
            // which is what makes the collapse mean something.
            var m = SpliceScreen.Build(A(), B(), Preview());
            var v = new SpliceChamberView();
            v.Bind(m, lockedOut: new HashSet<Guid>(), onSplice: () => { });
            Assert.AreEqual(DisplayStyle.Flex,
                v.Q<MutationBanner>("mutation").style.display.value);
        }

        /// TWO CREATURES ARE LOOKED AT ON THIS SCREEN AND BOTH ARE FRAMED.
        /// `Splice Chamber.dc.html` draws a ringed disc on each parent
        /// (`:56`, `:84`) and a third on the predicted hybrid (`:107`); a
        /// screen that drew the parents as bare sprites would pass every
        /// other assertion in this file.
        [Test]
        public void SpliceChamberView_FramesBothParentsAndThePredictionInHeroSlots()
        {
            var m = SpliceScreen.Build(A(), B(), Preview());
            var v = new SpliceChamberView();
            v.Bind(m, lockedOut: new HashSet<Guid>(), onSplice: () => { });

            Assert.AreEqual(3, v.Query<HeroSlot>().ToList().Count,
                "the two parents and the predicted hybrid are three framed creatures");

            // AND THE PREDICTION IS A SPRITE STACK WITHOUT A PORTRAIT.
            // `Q("body")` is the discriminator `HeroSlot` documents: a live
            // slot REMOVES its three layers rather than leaving them empty
            // behind the stage.
            var predicted = v.Q<HeroSlot>("predicted-creature");
            Assert.IsNotNull(predicted, "the predicted hybrid has no slot at all");
            Assert.IsNotNull(predicted.Q<VisualElement>("body"),
                "a portrait-less chamber should fall back to the baked sprite stack");
            Assert.IsNull(predicted.Q<CreatureStage>(),
                "a chamber given no portrait is rendering a live stage");
        }

        /// THE PREDICTED GENERATION IS THE SERVER'S ARITHMETIC, NOT A GUESS.
        /// `splice/commit.ts:292` is `max(a, b) + 1`; a panel that printed
        /// either parent's own generation would be telling the player the
        /// splice produces a creature no deeper than what it consumes, on
        /// the screen whose whole subject is depth.
        [Test]
        public void SpliceChamberView_StatesThePredictedGenerationAndTheLineageItComesFrom()
        {
            var a = Unnamed("Vetch", 4);
            var b = Unnamed("Skitter", 6);
            var m = SpliceScreen.Build(a, b, Preview());

            var v = new SpliceChamberView();
            v.Bind(m, lockedOut: new HashSet<Guid>(), onSplice: () => { });

            Assert.AreEqual(7, SpliceScreen.PredictedGeneration(a, b));
            Assert.AreEqual(CreatureLabel.Generation(7),
                v.Q<VisualElement>("predicted-gen").Q<Label>("gen").text);

            Assert.AreEqual(SpliceScreen.PredictedName, v.Q<Label>("predicted-name").text);
            Assert.AreEqual("Vetch × Skitter lineage", v.Q<Label>("predicted-lineage").text);

            // The strip draws the founder root, both parents and the child -
            // four stops for a G4/G6 pair. `SpliceScreen.LineageChain` has
            // why it is four and not the handoff's five.
            var stops = v.Query(className: LineageStrip.NodeUssClassName).ToList();
            Assert.AreEqual(4, stops.Count);
        }

        [Test]
        public void SpliceChamberView_AnEmptyCoverageWarning_HidesItsRowRatherThanDrawingABlankPanel()
        {
            // The ordinary case: the server named no coverage loss, so
            // `CoverageWarningFor` returns empty. The label sits in a
            // --amber-tint panel with --space-3 of padding, so before Phase 8
            // Task 10 this drew a blank amber strip across the screen on
            // every splice where nothing was being lost - visible in every
            // capture of this screen up to that commit.
            var m = SpliceScreen.Build(Named("Ash"), B(), Preview());
            Assert.AreEqual(string.Empty, m.CoverageWarning,
                "sanity: this fixture's preview names no coverage loss");

            var v = new SpliceChamberView();
            v.Bind(m, lockedOut: new HashSet<Guid>(), onSplice: () => { });

            var warning = v.Q<Label>("coverage-warning");
            Assert.AreEqual(string.Empty, warning.text);
            Assert.AreEqual(DisplayStyle.None, warning.resolvedStyle.display,
                "an empty coverage warning still occupies a tinted row");
        }

        [Test]
        public void SpliceChamberView_ACoverageWarning_ShowsItsRow()
        {
            // The complement: without it, a view that hid the panel
            // unconditionally would pass the test above and silently drop
            // sample_economy 7's whole screen requirement.
            var lost = new List<CoverageLost> { new CoverageLost { Trait = "Chill", Tier = 2 } };
            var warning = SpliceScreen.CoverageWarningFor(lost);
            Assert.IsNotEmpty(warning, "sanity: a named coverage loss produces a sentence");

            var preview = Preview();
            preview.CoverageLost = lost;
            var m = SpliceScreen.Build(Named("Ash"), B(), preview);

            var v = new SpliceChamberView();
            v.Bind(m, lockedOut: new HashSet<Guid>(), onSplice: () => { });

            var label = v.Q<Label>("coverage-warning");
            Assert.AreEqual(warning, label.text);
            Assert.AreEqual(DisplayStyle.Flex, label.resolvedStyle.display);
        }

        [Test]
        public void SpliceChamberView_LockedOutParents_AreVisiblyDisabled()
        {
            // splice_confirm_spec 6: "the named Founder is visibly locked out".
            var founder = Named("Ash");
            var other = B();
            var m = SpliceScreen.Build(founder, other, Preview());
            var v = new SpliceChamberView();
            v.Bind(m, lockedOut: new HashSet<Guid> { founder.CreatureId }, onSplice: () => { });

            // A `SectionCard` AS OF PHASE 9 TASK 16, WHICH CHANGES NOTHING
            // THIS TEST IS ABOUT. The tile the handoff draws is a card with
            // an eyebrow, a generation chip, a ringed creature, a name, a
            // role line and two trait chips - not a roster tile - so it is
            // composed rather than a `CreatureCard`. The NAME and the
            // LOCKED-OUT class were always this view's doing and neither has
            // moved; see `SpliceChamberView`'s class comment.
            var card = v.Q<SectionCard>(founder.CreatureId.ToString());
            Assert.IsNotNull(card, "the locked-out parent has no tile at all");
            Assert.IsFalse(card.enabledSelf);
            Assert.IsTrue(card.ClassListContains(SpliceChamberView.LockedOutUssClassName));
        }

        [Test]
        public void SpliceChamberView_ParentsNotLockedOut_AreEnabledAndUnmarked()
        {
            // The complement of the test above - without it, a view that
            // always disables both cards (regardless of `lockedOut`) would
            // still pass the locked-out test.
            var a = A();
            var b = B();
            var m = SpliceScreen.Build(a, b, Preview());
            var v = new SpliceChamberView();
            v.Bind(m, lockedOut: new HashSet<Guid>(), onSplice: () => { });

            var cardA = v.Q<SectionCard>(a.CreatureId.ToString());
            var cardB = v.Q<SectionCard>(b.CreatureId.ToString());
            Assert.IsTrue(cardA.enabledSelf);
            Assert.IsTrue(cardB.enabledSelf);
            Assert.IsFalse(cardA.ClassListContains(SpliceChamberView.LockedOutUssClassName));
            Assert.IsFalse(cardB.ClassListContains(SpliceChamberView.LockedOutUssClassName));
        }

        // ---------------------------------------------------------------
        // DeployView
        // ---------------------------------------------------------------

        /// Builds a real `DeployScreenModel` from `slots` distinct creatures,
        /// all selected, in a roster that holds exactly them - so `Floor`(1)
        /// and `Cap`(5) are the only things that can make 0/1/6 disagree.
        static DeployScreenModel DeployModelWith(int slots)
        {
            var roster = new RosterScreen();
            var response = new RosterResponse { Cap = 20 };
            var ids = new List<Guid>();
            for (var i = 0; i < slots; i++)
            {
                var creature = Unnamed("Species" + i, 1);
                response.Creatures.Add(creature);
                ids.Add(creature.CreatureId);
            }
            roster.ApplyRoster(response);

            return DeployScreen.Build(waveId: 1, roster: roster, selected: ids);
        }

        static DeployView BindWith(int slots)
        {
            var v = new DeployView();
            v.Bind(DeployModelWith(slots), onStart: () => { });
            return v;
        }

        [Test]
        public void DeployView_RefusesToEnableStartBelowTheFloorOrAboveTheCap()
        {
            // DeployScreen.Floor (1) and .Cap (5) - services/api/src/wave/
            // issuance.ts, mirrored there, read here.
            Assert.IsFalse(BindWith(slots: 0).Q<Button>("start").enabledSelf);
            Assert.IsTrue(BindWith(slots: 1).Q<Button>("start").enabledSelf);
            Assert.IsFalse(BindWith(slots: 6).Q<Button>("start").enabledSelf);
        }

        [Test]
        public void DeployView_ADuplicateSelection_DisablesStartForAReasonOtherThanCount()
        {
            // The point of this test: 2 is inside [Floor, Cap], so a view
            // that re-derived the rule from `m.Slots.Count` would enable
            // Start here and pass the three counts above anyway. Only
            // reading `m.CanDeploy` catches it.
            var roster = new RosterScreen();
            var creature = Unnamed("Vetch Crawler", 4);
            roster.ApplyRoster(new RosterResponse { Creatures = { creature }, Cap = 20 });

            var model = DeployScreen.Build(1, roster, new List<Guid> { creature.CreatureId, creature.CreatureId });
            Assert.IsFalse(model.CanDeploy, "sanity: a duplicate id must not build a legal deployment");

            var v = new DeployView();
            v.Bind(model, () => { });

            Assert.IsFalse(v.Q<Button>("start").enabledSelf);
        }

        [Test]
        public void DeployView_ACommittedCreature_DisablesStartForAReasonOtherThanCount()
        {
            // Sharper than the duplicate case: this uses the EXACT count
            // (1) that reads as enabled above. A view keyed off count alone
            // cannot distinguish this from that passing case.
            var committed = Unnamed("Vetch Crawler", 4);
            committed.CommittedTo = Guid.NewGuid();
            var roster = new RosterScreen();
            roster.ApplyRoster(new RosterResponse { Creatures = { committed }, Cap = 20 });

            var model = DeployScreen.Build(1, roster, new List<Guid> { committed.CreatureId });
            Assert.IsFalse(model.CanDeploy, "sanity: an already-committed creature must not build a legal deployment");

            var v = new DeployView();
            v.Bind(model, () => { });

            Assert.IsFalse(v.Q<Button>("start").enabledSelf);
        }

        [Test]
        public void DeployView_ShowsTheModelsBlockerVerbatim()
        {
            var model = DeployModelWith(0);
            Assert.IsNotEmpty(model.Blocker);

            var v = new DeployView();
            v.Bind(model, () => { });

            Assert.AreEqual(model.Blocker, v.Q<Label>("blocker").text);
        }

        [Test]
        public void DeployView_ALegalDeployment_ShowsNoBlockerText()
        {
            var model = DeployModelWith(1);
            Assert.AreEqual(string.Empty, model.Blocker);

            var v = new DeployView();
            v.Bind(model, () => { });

            var blocker = v.Q<Label>("blocker");
            Assert.AreEqual(string.Empty, blocker.text);

            // AND THE ROW GOES WITH THE TEXT, as of Phase 8 Task 10. The
            // label sits in a --coral-tint panel with --space-3 of padding,
            // so an empty one is a blank coral strip between the pocket list
            // and the Start button - drawn on the ORDINARY case, the one a
            // player sees every wave. It is in every capture of this screen
            // before that commit. Same rule as FounderNamingView.Blocker and
            // ScreenScaffold.FooterNote.
            Assert.AreEqual(DisplayStyle.None, blocker.resolvedStyle.display,
                "a legal deployment still drew the refusal row, blank");
        }

        [Test]
        public void DeployView_ABlockedDeployment_ShowsItsRow()
        {
            // The complement: a view that hid the panel unconditionally would
            // pass the test above and silently drop the one sentence that
            // says why Start is off.
            var model = DeployModelWith(0);
            Assert.IsNotEmpty(model.Blocker);

            var v = new DeployView();
            v.Bind(model, () => { });

            Assert.AreEqual(DisplayStyle.Flex, v.Q<Label>("blocker").resolvedStyle.display);
        }

        /// POCKET ORDER IS PROTOCOL, AND THE SCREEN NOW SAYS IT.
        /// `DeployScreen.Build` assigns `Pocket = i` because "Index ==
        /// deployment order" and `deploymentMatches` compares the replay's
        /// deployment against the stored one IN ORDER - so two deployments
        /// that agree as multisets and differ in order are different
        /// deployments. Before Phase 8 Task 10 this screen drew a grid of
        /// identical `CreatureCard`s and printed the pocket nowhere at all.
        ///
        /// THE ROW IS A `FieldSlotRow` AND THE POCKET IS ITS BADGE, as of
        /// Phase 9 Task 17 - it used to be an `OptionRow` whose detail line
        /// read `DeployScreen.PocketLabel(i)`. The FACT asserted is unchanged
        /// and the alphabet is the only thing that moved.
        [Test]
        public void DeployView_NamesEachSlotsPocketInOrder()
        {
            var v = BindWith(slots: 3);

            var rows = v.Query<FieldSlotRow>().ToList();
            Assert.AreEqual(3, rows.Count, "one row per deployed creature");
            for (var i = 0; i < rows.Count; i++)
            {
                Assert.AreEqual(DeployScreen.PocketTag(i), rows[i].Q<Label>("letter").text,
                    "slot " + i + " is not badged with its own pocket, in order");
            }

            // A-FIRST FOR THE PLAYER, 0-INDEXED ON THE WIRE. Asserted
            // explicitly because the two schemes are a real trap: a test that
            // only compared the view against PocketTag would pass if both
            // sides quietly agreed that pocket 0 is "B".
            Assert.AreEqual("A", rows[0].Q<Label>("letter").text);
        }

        [Test]
        public void DeployView_StatesTheDeploymentCountAgainstTheCap()
        {
            // A count with no ceiling is not a decision: "3" does not say
            // whether there is room for a fourth. Both numbers are the
            // model's - Cap is mirrored from wave/issuance.ts.
            var v = BindWith(slots: 3);

            var cells = v.Query<StatCell>().ToList();
            Assert.AreEqual(3, cells.Count, "the incoming-wave card states the foes, the count and the reward");
            Assert.AreEqual(DeployScreen.DeployedStatValue(3), cells[1].Q<Label>("value").text);
            StringAssert.Contains(
                DeployScreen.Cap.ToString(CultureInfo.InvariantCulture),
                cells[1].Q<Label>("value").text);

            // And the numbers wear `t-num` - StatCell's own UXML, which is
            // why routing them through the component is worth doing.
            Assert.IsTrue(cells[1].Q<Label>("value").ClassListContains("t-num"));
        }

        [Test]
        public void DeployView_AnEmptyDeployment_SaysSoInsteadOfRenderingBlankPaper()
        {
            var v = BindWith(slots: 0);

            var empty = v.Q<EmptyState>();
            Assert.IsNotNull(empty, "an empty deployment rendered as a screen with nothing on it");
            Assert.AreEqual(DeployScreen.EmptyMessage, empty.Q<Label>("message").text);

            // TWO SENTENCES, NOT ONE, and they must not be the same one -
            // splice_confirm_spec 3's "three different phrasings of the same
            // fact reads as evasion". The empty state names the list's state;
            // the blocker says what to do about it.
            Assert.AreNotEqual(DeployScreen.EmptyMessage, v.Q<Label>("blocker").text);
        }

        [Test]
        public void DeployView_ShowsTheModelsStartLabelVerbatim()
        {
            // Fix round 1: "Start" used to be a literal in DeployView.cs.
            // It is now DeployScreen.Cta, mirrored onto the model as
            // DeployScreenModel.CtaLabel - this pins the view against
            // whatever the model says, not against a fixed string.
            var model = DeployModelWith(1);
            var v = new DeployView();
            v.Bind(model, () => { });

            Assert.AreEqual(model.CtaLabel, v.Q<Button>("start").text);
        }

        // ---------------------------------------------------------------
        // RosterView
        // ---------------------------------------------------------------

        /// THE NOTICE IS THE SCAFFOLD'S FOOTER NOTE AS OF PHASE 8 TASK 10,
        /// not this screen's own `notice` Label, so these two read it by the
        /// element name the row now carries. The FACT asserted is unchanged:
        /// the model's sentence reaches the screen, and a complete roster
        /// shows none of it. The element gained one property in the move -
        /// an empty footer note collapses its row
        /// (`ScaffoldTests.AnEmptyFooterNoteHidesItsRow`), where the old
        /// Label drew a blank strip above the grid on every complete roster.
        [Test]
        public void RosterView_ANeverLoadedRoster_ShowsTheIncompleteNoticeNotAnEmptyList()
        {
            // Phase 6's fix 7c95cd9: a roster that never loaded must not
            // claim to be complete.
            var r = new RosterScreen();
            var v = new RosterView();
            v.Bind(r, _ => { });

            StringAssert.Contains(r.IncompleteNotice, v.Q<Label>("footer-note").text);
        }

        [Test]
        public void RosterView_ACompleteRoster_ShowsNoNoticeAndOneCardPerCreature()
        {
            // The complement of the test above: without it, a view that
            // always showed a hardcoded notice would still pass it.
            var r = new RosterScreen();
            var vetch = Unnamed("Vetch Crawler", 4);
            var ember = Unnamed("Ember Skitter", 6);
            r.ApplyRoster(new RosterResponse { Creatures = { vetch, ember }, Cap = 20 });

            var v = new RosterView();
            v.Bind(r, _ => { });

            var note = v.Q<Label>("footer-note");
            Assert.AreEqual(string.Empty, note.text);
            Assert.AreEqual(DisplayStyle.None, note.resolvedStyle.display,
                "a complete roster still drew the caveat row, blank");
            Assert.AreEqual(2, v.Query<CreatureCard>().ToList().Count);
        }

        /// "YOU OWN NOTHING" AND "WE COULD NOT FIND OUT WHAT YOU OWN" MUST
        /// NEVER READ THE SAME - `RosterScreen.IncompleteNotice`'s own rule,
        /// asserted from the view's side. An empty grid gets the first
        /// sentence only when the cache is entitled to make that claim.
        [Test]
        public void RosterView_AnEmptyButCompleteRoster_SaysSoInsteadOfRenderingBlankPaper()
        {
            var r = new RosterScreen();
            r.ApplyRoster(new RosterResponse { Cap = 20 });
            Assert.IsTrue(r.IsComplete, "sanity: a successful load of nothing is a complete roster");

            var v = new RosterView();
            v.Bind(r, _ => { });

            var empty = v.Q<EmptyState>();
            Assert.IsNotNull(empty, "an empty roster rendered as a screen with nothing on it");
            Assert.AreEqual(RosterScreen.EmptyMessage, empty.Q<Label>("message").text);
        }

        [Test]
        public void RosterView_AnEmptyRosterThatNeverLoaded_DoesNotClaimThePlayerOwnsNothing()
        {
            // The sharp case, and the reason the empty state is conditional:
            // this cache holds no creatures for the same reason it holds no
            // knowledge. The footer note says which; the grid must not say
            // the other thing.
            var r = new RosterScreen();
            Assert.IsFalse(r.IsComplete, "sanity: a cache nobody wrote to is not complete");

            var v = new RosterView();
            v.Bind(r, _ => { });

            Assert.IsNull(v.Q<EmptyState>(),
                "a never-loaded roster told the player they own nothing, which is the exact "
                + "false claim RosterLoadState exists to prevent");
            StringAssert.Contains(r.IncompleteNotice, v.Q<Label>("footer-note").text);
        }

        [Test]
        public void RosterView_NamesEachCardAfterItsCreatureId()
        {
            // The structural precondition for `onSelect` wiring - see the
            // class comment's point 1.
            var r = new RosterScreen();
            var vetch = Unnamed("Vetch Crawler", 4);
            r.ApplyRoster(new RosterResponse { Creatures = { vetch }, Cap = 20 });

            var v = new RosterView();
            v.Bind(r, _ => { });

            Assert.IsNotNull(v.Q<CreatureCard>(vetch.CreatureId.ToString()));
        }

        // ---------------------------------------------------------------
        // RegionView
        // ---------------------------------------------------------------

        [Test]
        public void RegionView_LeavesUnmappedServerGeographyUninvented()
        {
            var state = new RegionStateResponse
            {
                RegionId = "verdant-shelf",
                Epoch = 1,
                Roster = new Roster { Count = 1, Cap = 20 },
            };
            var view = new RegionView();
            view.Bind(RegionScreen.Build(state), _ => { });

            Assert.AreEqual("SERVER REGION", view.Q<Label>("region-band").text);
            Assert.AreEqual("Verdant Shelf", view.Q<Label>("region-name").text);
            Assert.AreEqual("Live harvest data · Authored atlas link pending",
                view.Q<Label>("region-detail").text);
        }

        [Test]
        public void RegionView_ASpentNode_ShowsItsBlockerVerbatimAndDisablesClaim()
        {
            var node = new Nodes { Slot = 2, Type = "Shard", Accrued = 40, Remaining = 0, Grants = 0 };
            var m = RegionScreen.Build(RegionState(new Roster { Count = 5, Cap = 20 }, node));
            Assert.IsFalse(m.Nodes[0].CanClaim, "sanity: a spent node must not be claimable");
            Assert.IsNotEmpty(m.Nodes[0].Blocker);

            var v = new RegionView();
            v.Bind(m, _ => { });

            var row = v.Query(className: RegionView.NodeRowUssClassName).ToList()[0];
            Assert.AreEqual(m.Nodes[0].Blocker, row.Q<Label>("blocker").text);
            Assert.IsFalse(row.Q<Button>("claim").enabledSelf);

            // AND THE ROW IS DRAWN, as of Phase 8 Task 12. Paired with the
            // collapse asserted below so a view that hid the panel
            // unconditionally cannot pass by dropping the sentence.
            Assert.AreEqual(DisplayStyle.Flex, row.Q<Label>("blocker").resolvedStyle.display);
        }

        [Test]
        public void RegionView_AClaimableNode_HasNoBlockerTextAndAnEnabledClaimButton()
        {
            // The complement of the test above: without it, a view that
            // always disables every claim button would still pass it.
            var node = new Nodes { Slot = 3, Type = "Shard", Accrued = 12, Remaining = null, Grants = 0 };
            var m = RegionScreen.Build(RegionState(new Roster { Count = 5, Cap = 20 }, node));
            Assert.IsTrue(m.Nodes[0].CanClaim, "sanity: an un-depleted, non-overflowing node is claimable");

            var v = new RegionView();
            v.Bind(m, _ => { });

            var row = v.Query(className: RegionView.NodeRowUssClassName).ToList()[0];
            var blocker = row.Q<Label>("blocker");
            Assert.AreEqual(string.Empty, blocker.text);
            Assert.IsTrue(row.Q<Button>("claim").enabledSelf);

            // AND THE ROW GOES WITH THE TEXT, as of Phase 8 Task 12. The
            // label sits in a --coral-tint panel with --space-3 of padding,
            // so an empty one is a blank coral strip inside the card of every
            // node a player CAN claim - the ordinary case, and both of the
            // two nodes the server actually serves. Same rule as
            // DeployView.Blocker and ScreenScaffold.FooterNote.
            Assert.AreEqual(DisplayStyle.None, blocker.resolvedStyle.display,
                "a claimable node still drew the refusal row, blank");
        }

        [Test]
        public void RegionView_ShowsANodesRemainingAndClaimLabelsVerbatim()
        {
            // Fix round 1: "Claim" and the "unlimited"/numeric remaining
            // text used to be literals in RegionView.cs. They are now
            // NodeRow.ClaimLabel/.RemainingLabel, populated in
            // RegionScreen.Build exactly as Blocker is.
            //
            // THE REMAINING COUNT IS A `StatCell` AS OF PHASE 8 TASK 12, so
            // it is read through that component's element names rather than
            // through a `remaining` Label of this screen's own - the same
            // rename the splice forecast's `probability` took when it became
            // a cell in Task 10, and RosterView's `notice` took when it
            // became the scaffold's footer note. The FACT asserted is
            // unchanged: the model's string reaches the screen, and the view
            // does not re-derive it. What it gains is a LABEL: the old row
            // printed this number bare, beside another bare number, with
            // nothing saying which was which.
            var node = new Nodes { Slot = 4, Type = "Shard", Accrued = 8, Remaining = 3, Grants = 0 };
            var m = RegionScreen.Build(RegionState(new Roster { Count = 5, Cap = 20 }, node));

            var v = new RegionView();
            v.Bind(m, _ => { });

            var row = v.Query(className: RegionView.NodeRowUssClassName).ToList()[0];
            Assert.AreEqual(m.Nodes[0].RemainingLabel, Remaining(row).Q<Label>("value").text);
            Assert.AreEqual(RegionScreen.RemainingStatLabel, Remaining(row).Q<Label>("label").text);
            Assert.AreEqual(m.Nodes[0].ClaimLabel, row.Q<Button>("claim").text);

            // The accrual is the first cell, and it is the other half of the
            // pair the old row left unlabelled.
            var cells = row.Query<StatCell>().ToList();
            Assert.AreEqual(RegionScreen.AccruedStatLabel, cells[0].Q<Label>("label").text);
            Assert.AreEqual(RegionScreen.StatValue(8), cells[0].Q<Label>("value").text);

            // Both values wear `t-num`, which is StatCell's own UXML and the
            // point of routing these numbers through the component: it is the
            // marker bible 10.6's tabular figures and 11px floor are enforced
            // through, and the old `.node-row > Label` rule set neither.
            Assert.IsTrue(cells[0].Q<Label>("value").ClassListContains("t-num"));
            Assert.IsTrue(Remaining(row).Q<Label>("value").ClassListContains("t-num"));
        }

        /// A node card's second stat cell - harvests left. Named here rather
        /// than indexed at four call sites, because the THIRD cell is
        /// conditional (see RegionView_AGrantingNode_...) and an index is
        /// the thing that would quietly start reading the wrong one.
        static StatCell Remaining(VisualElement row)
        {
            return row.Query<StatCell>().ToList()[1];
        }

        /// THE GRANT WAS ON NO SCREEN AT ALL BEFORE PHASE 8 TASK 12.
        /// `NodeRow.Grants` is "creatures this claim would grant", and it is
        /// the entire subject of the roster refusal - "Your roster has no
        /// room for 1 more" - yet a granting node rendered identically to a
        /// shard node until that refusal fired. The screen was withholding
        /// its own premise.
        [Test]
        public void RegionView_AGrantingNode_StatesHowManyCreaturesTheClaimWouldGrant()
        {
            var node = new Nodes { Slot = 2, Type = "Grant", Accrued = 0, Remaining = null, Grants = 2 };
            var m = RegionScreen.Build(RegionState(new Roster { Count = 5, Cap = 20 }, node));
            Assert.IsTrue(m.Nodes[0].CanClaim, "sanity: 5 + 2 fits in a cap of 20");

            var v = new RegionView();
            v.Bind(m, _ => { });

            var cells = v.Query(className: RegionView.NodeRowUssClassName).ToList()[0]
                .Query<StatCell>().ToList();
            Assert.AreEqual(3, cells.Count, "a granting node states accrual, depletion and the grant");
            Assert.AreEqual(RegionScreen.GrantsStatLabel, cells[2].Q<Label>("label").text);
            Assert.AreEqual(RegionScreen.StatValue(2), cells[2].Q<Label>("value").text);
        }

        [Test]
        public void RegionView_APureShardNode_StatesNoGrantRatherThanZero()
        {
            // The complement: `Grants` is 0 on every pure shard node, and a
            // cell reading "0" on all of them would be noise around the one
            // that matters. Two cells, not three.
            var node = new Nodes { Slot = 1, Type = "Shard", Accrued = 12, Remaining = null, Grants = 0 };
            var m = RegionScreen.Build(RegionState(new Roster { Count = 5, Cap = 20 }, node));

            var v = new RegionView();
            v.Bind(m, _ => { });

            var cells = v.Query(className: RegionView.NodeRowUssClassName).ToList()[0]
                .Query<StatCell>().ToList();
            Assert.AreEqual(2, cells.Count, "a shard node drew an empty grant cell");
        }

        [Test]
        public void RegionView_ARegionWithNoNodes_SaysSoInsteadOfRenderingBlankPaper()
        {
            // Reachable: `RegionStateResponse` default-initialises `Nodes` to
            // an empty list, so a response constructed rather than
            // deserialised builds a model with none. Before Phase 8 Task 12
            // that rendered as a screen with nothing on it, which reads as a
            // failed load rather than as a state the game meant.
            var m = RegionScreen.Build(RegionState(new Roster { Count = 5, Cap = 20 }));
            Assert.IsEmpty(m.Nodes, "sanity: this fixture carries no nodes");

            var v = new RegionView();
            v.Bind(m, _ => { });

            var empty = v.Q<EmptyState>();
            Assert.IsNotNull(empty, "an empty node list rendered as a screen with nothing on it");
            Assert.AreEqual(RegionScreen.EmptyMessage, empty.Q<Label>("message").text);
        }

        /// THE EMPTY STATE IS FOR AN EMPTY LIST, NOT AN UNCLAIMABLE ONE, and
        /// Task 12's own step 2 asks for the other thing - "EmptyState when
        /// no nodes are claimable". That screen would be worse: a spent node
        /// carries the sentence saying why its Claim is off, and replacing
        /// the rows with one centred message deletes the explanation.
        [Test]
        public void RegionView_ARegionWhoseNodesAreAllSpent_KeepsTheRowsAndTheirReasons()
        {
            var a = new Nodes { Slot = 0, Type = "Shard", Accrued = 40, Remaining = 0, Grants = 0 };
            var b = new Nodes { Slot = 1, Type = "Shard", Accrued = 9, Remaining = 0, Grants = 0 };
            var m = RegionScreen.Build(RegionState(new Roster { Count = 5, Cap = 20 }, a, b));
            Assert.IsFalse(m.Nodes[0].CanClaim);
            Assert.IsFalse(m.Nodes[1].CanClaim);

            var v = new RegionView();
            v.Bind(m, _ => { });

            Assert.IsNull(v.Q<EmptyState>(),
                "a region whose nodes are all spent was emptied of the rows that say why");
            var rows = v.Query(className: RegionView.NodeRowUssClassName).ToList();
            Assert.AreEqual(2, rows.Count);
            Assert.IsNotEmpty(rows[0].Q<Label>("blocker").text);
            Assert.IsNotEmpty(rows[1].Q<Label>("blocker").text);
        }

        [Test]
        public void RegionView_ANodeThatNeverDepletes_ShowsTheModelsUnlimitedLabelVerbatim()
        {
            // The complement of the test above: a null `Remaining` takes the
            // other branch of `RemainingLabel`, and this proves the view
            // still just mirrors it rather than falling back to a literal
            // of its own for this case.
            var node = new Nodes { Slot = 5, Type = "Shard", Accrued = 8, Remaining = null, Grants = 0 };
            var m = RegionScreen.Build(RegionState(new Roster { Count = 5, Cap = 20 }, node));
            Assert.AreEqual(RegionScreen.UnlimitedLabel, m.Nodes[0].RemainingLabel);

            var v = new RegionView();
            v.Bind(m, _ => { });

            var row = v.Query(className: RegionView.NodeRowUssClassName).ToList()[0];
            Assert.AreEqual(m.Nodes[0].RemainingLabel, Remaining(row).Q<Label>("value").text);
        }

        [Test]
        public void RegionView_RendersExactlyOneRowPerNode()
        {
            var a = new Nodes { Slot = 1, Type = "Shard", Accrued = 5, Remaining = null, Grants = 0 };
            var b = new Nodes { Slot = 2, Type = "Grant", Accrued = 0, Remaining = 3, Grants = 1 };
            var m = RegionScreen.Build(RegionState(new Roster { Count = 5, Cap = 20 }, a, b));

            var v = new RegionView();
            v.Bind(m, _ => { });

            Assert.AreEqual(2, v.Query(className: RegionView.NodeRowUssClassName).ToList().Count);
        }

        [Test]
        public void RegionView_ShowsRosterCountAndCapWhenKnown()
        {
            var m = RegionScreen.Build(RegionState(new Roster { Count = 5, Cap = 20 }));
            Assert.IsTrue(m.HasRosterCounts);

            var v = new RegionView();
            v.Bind(m, _ => { });

            Assert.AreEqual("5/20", v.Q<Label>("roster").text);

            // The model's own formatter now writes it - the string moved out
            // of the view in Phase 8 Task 12, on the convention
            // UnlimitedLabel and ClaimCta already follow. The literal above
            // stays as the stronger assertion: a test that only compared the
            // view against the formatter would pass if both sides quietly
            // agreed on the wrong shape.
            Assert.AreEqual(RegionScreen.RosterHeadline(m), v.Q<Label>("roster").text);

            // And it is drawn. Paired with the collapse below.
            Assert.AreEqual(DisplayStyle.Flex, v.Q<Label>("roster").resolvedStyle.display);

            // `t-num`: a count against a cap is a number a decision depends
            // on - it is what the roster refusal on a granting node is about -
            // so bible 10.6's tabular face and 11px floor apply to it.
            Assert.IsTrue(v.Q<Label>("roster").ClassListContains("t-num"));
        }

        [Test]
        public void RegionView_HidesRosterCountWhenUnset()
        {
            // RegionScreenModel.HasRosterCounts is gated on Cap > 0 -
            // a response constructed rather than deserialised defaults
            // Roster to Count 0 / Cap 0, which is UNSET, not "empty".
            var m = RegionScreen.Build(RegionState(new Roster()));
            Assert.IsFalse(m.HasRosterCounts);

            var v = new RegionView();
            v.Bind(m, _ => { });

            var roster = v.Q<Label>("roster");
            Assert.AreEqual(string.Empty, roster.text);

            // AND THE LINE GOES WITH THE TEXT, as of Phase 8 Task 12. The
            // Label was added unconditionally and carried
            // `margin-bottom: var(--space-3)`, so an unset roster headline
            // drew 12px of empty strip above the node list - the eighth
            // instance of the shape this phase has been removing, and the
            // first that is a bare header line rather than a tinted panel.
            Assert.AreEqual(DisplayStyle.None, roster.resolvedStyle.display,
                "an unknown roster headline still occupied a header line");
        }
    }
}
