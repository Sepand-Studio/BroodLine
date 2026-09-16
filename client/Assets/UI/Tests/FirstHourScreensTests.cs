using System;
using System.Collections.Generic;
using System.Linq;
using Broodline.Api;
using Broodline.Model;
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

        // ---------------------------------------------------------------
        // Lineage - beat 8
        // ---------------------------------------------------------------

        static LineageView BoundLineage(Guid highlight, params LineageNode[] nodes)
        {
            var view = new LineageView();
            view.Bind(new LineageResponse { Nodes = nodes }, highlight);
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
            var empty = BoundLineage(Guid.Empty);
            Assert.AreEqual(LineageScreen.EmptyNotice, empty.Q<Label>("notice").text);
            Assert.AreEqual(0, empty.Query(className: LineageView.GenerationRowUssClassName).ToList().Count);

            // And the notice is GONE once there is a tree - a permanent
            // "no lineage yet" beside four nodes is the same defect pointed
            // the other way.
            var full = BoundLineage(Guid.Empty, Node("Hollow", F, founder: true));
            Assert.AreEqual(string.Empty, full.Q<Label>("notice").text);
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
            var view = BoundCampaign(highestWaveCleared: 2);
            var states = new[] { "wave-1", "wave-2", "wave-6", "wave-7" }
                .Select(n => view.Q(n).Q<Label>("state").text)
                .ToList();

            Assert.AreEqual(CampaignSelectScreen.StateLabel(1, Authored, 2), states[0]);
            Assert.AreEqual(CampaignSelectScreen.StateLabel(6, Authored, 2), states[2]);
            Assert.AreEqual(3, states.Distinct().Count(), "cleared / next / locked collapsed into fewer words");
            Assert.AreEqual(CampaignSelectScreen.RowLabel(6), view.Q("wave-6").Q<Label>("label").text);
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
    }
}
