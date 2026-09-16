using System;
using System.Collections.Generic;
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
                RegionId = "region-1",
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
            // "Splice — consumes both parents."
            Assert.AreEqual(m.CtaLabel, v.Q<Button>("cta").text);
            Assert.AreEqual(m.CoverageWarning ?? string.Empty, v.Q<Label>("coverage-warning").text);
            Assert.AreEqual(SpliceScreen.MutationLine(m.Forecast), v.Q<Label>("mutation").text);

            // The forecast table renders the server's numbers and computes
            // none - client_architecture 9.1. Row count, not just presence:
            // a view that rendered a single static row would pass a
            // "not empty" check and fail this one.
            Assert.AreEqual(
                m.Forecast.Combat2.Count,
                v.Query(className: SpliceChamberView.ForecastRowUssClassName).ToList().Count);
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

            var card = v.Q<CreatureCard>(founder.CreatureId.ToString());
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

            var cardA = v.Q<CreatureCard>(a.CreatureId.ToString());
            var cardB = v.Q<CreatureCard>(b.CreatureId.ToString());
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

            Assert.AreEqual(string.Empty, v.Q<Label>("blocker").text);
        }

        // ---------------------------------------------------------------
        // RosterView
        // ---------------------------------------------------------------

        [Test]
        public void RosterView_ANeverLoadedRoster_ShowsTheIncompleteNoticeNotAnEmptyList()
        {
            // Phase 6's fix 7c95cd9: a roster that never loaded must not
            // claim to be complete.
            var r = new RosterScreen();
            var v = new RosterView();
            v.Bind(r, _ => { });

            StringAssert.Contains(r.IncompleteNotice, v.Q<Label>("notice").text);
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

            Assert.AreEqual(string.Empty, v.Q<Label>("notice").text);
            Assert.AreEqual(2, v.Query<CreatureCard>().ToList().Count);
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
            Assert.AreEqual(string.Empty, row.Q<Label>("blocker").text);
            Assert.IsTrue(row.Q<Button>("claim").enabledSelf);
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

            Assert.AreEqual(string.Empty, v.Q<Label>("roster").text);
        }
    }
}
