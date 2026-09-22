using System;
using System.Collections.Generic;
using Broodline.Api;
using Broodline.Model;
using Broodline.UI;
using Broodline.UI.Components;
using Broodline.UI.Screens;
using Broodline.UI.Shell;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

// UnityEngine.UIElements HAS ITS OWN `ProgressBar`, so the bare name in a
// file importing both namespaces binds to Unity's type and the compiler
// reports it as a missing constructor rather than as an ambiguity. See
// ProgressBar.cs's class comment - every consumer needs this line.
using ProgressBar = Broodline.UI.Components.ProgressBar;

// No namespace: Assembly-CSharp-Editor, as with WaveBuilder and ScreenHarness.

/// One populated model per screen, with no server and no session.
///
/// SAME SHAPES AS `ScreenBindingTests`/`FirstHourScreensTests`/
/// `WaveScreensTests`, NOT THE SAME SOURCE. The brief for this task asked to
/// lift those builders out of `Broodline.UI.Tests` so there is one fixture
/// source the tests and this harness both call. That assembly is
/// `autoReferenced: false` with `defineConstraints: ["UNITY_INCLUDE_TESTS"]`
/// (`Broodline.UI.Tests.asmdef`), and `Assembly-CSharp-Editor` - where this
/// file lives, with no asmdef of its own - cannot reference a
/// non-auto-referenced assembly it was never given a reference to. Lifting
/// the builders the other way (out of the test assembly, into here, with the
/// tests calling this) was also rejected: that would make three test files
/// that currently need nothing but `Broodline.UI`/`Broodline.Model`/
/// `Generated.Api` newly depend on the Editor assembly, for a phase whose
/// task list does not touch those tests. So this is a second, standalone
/// copy of the same DTO shapes, built the same way, and the two are expected
/// to drift rather than call each other - see task-14-15-report.md for the
/// concrete asmdef reasoning.
///
/// ================================================================
/// NOTHING IN A FIXTURE MAY SHRINK, AND THIS IS THE CONSTRAINT MOST
/// LIKELY TO BITE THE NEXT PERSON WHO ADDS ONE.
///
/// The capture target is a fixed 430x932. A flex column whose children
/// overflow it does not clip them - it SHRINKS every one of them that will
/// shrink, silently and proportionally, because flex-shrink defaults to 1.
/// Measured on the first `Components` capture: a 6px ProgressBar track went
/// to zero height and vanished from the picture outright, and three stat
/// cells collapsed and spilled their values through the bottom of the card
/// they were in. Neither rendered as an error. Both rendered as a component
/// that does not work - which is exactly the wrong thing for a corpus whose
/// whole job is to be the place a broken component is visible.
///
/// So: set `flexShrink = 0` on everything a fixture stacks, and let content
/// that does not fit run off the bottom of the frame, where it can be seen
/// and trimmed. A too-tall fixture is a layout problem with an obvious
/// symptom; a shrunk one is a lie about five components at once.
///
/// This applies to a SCREEN fixture too, not only to the component ones -
/// a screen whose content is taller than 932 will shrink its own furniture
/// before it scrolls, because ScreenScaffold's content region is the only
/// part of the column that grows.
/// ================================================================
public static class ScreenFixtures
{
    // Fixed ids rather than Guid.NewGuid() wherever a screen's own binding
    // logic keys off identity (Lineage's highlight, Splice's parents/child) -
    // deterministic fixtures are easier to reason about from a screenshot.
    static readonly Guid FounderId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    static readonly Guid ParentAId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    static readonly Guid ParentBId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    static readonly Guid ChildId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    /// Every screen this harness knows how to build, in the order the picker
    /// and the capture both walk. `List<string>`, not `IReadOnlyList<string>`:
    /// `ScreenHarness.CreateGUI` calls `names.IndexOf(...)`, which
    /// `IReadOnlyList<T>` does not declare.
    public static readonly List<string> Names = new List<string>
    {
        "CampaignSelectView",
        "CodexSheet",
        "DeployView",
        "FounderNamingView",
        "LineageView",
        "PostWaveView",
        // THE OTHER ARM OF THE ONE SCREEN THAT RENDERS BOTH VERDICTS.
        // `PostWaveView.Bind` toggles `t-success`/`t-danger` off the SERVER's
        // result and `PostWaveScreen.Headline` has two sentences; a
        // non-win response reaching this screen is a documented path
        // (`WinResult`'s own note) and until Phase 9 Task 18 no capture in the
        // corpus showed it. A branch nobody has looked at is a branch that has
        // shipped wrong before in this project - the green blank strip and the
        // shadow round bare paper were both this shape.
        "PostWaveLoss",
        "RegionView",
        "RosterView",
        "SpliceChamberView",
        "SpliceRevealView",
        "WaveDefeatView",
        "WaveHudView",
        // NOT SCREENS, and last for that reason - see Primitives(),
        // Icons(), Scaffold() and Components() below. Each exists because
        // the twelve screens above it cannot show the thing it shows.
        "Primitives",
        "Icons",
        "Scaffold",
        "Components",
        "Vocabulary",
        "Lane",
        "Band",
    };

    /// Whether the shell holds this fixture in `#screen-host`, which is the
    /// slot `ScreenHost.Show` and `ScreenHost.Push` add a screen to.
    ///
    /// BY NAME, AND FOR THE SAME REASON `ScaffoldTests`' exemption list is by
    /// name: a fixture that is not a pushed-or-shown screen must not be
    /// rendered inside a slot the shell would never put it in, and an
    /// accident of ordering in `Names` is not a reason. The three that answer
    /// false:
    ///   CodexSheet  - ScreenHost.ShowSheet puts it in `#sheet-layer`, which
    ///                 is `position: absolute` and `display: none` until that
    ///                 method sets it Flex inline. This harness does not
    ///                 reproduce the overlay layer; the sheet POSITIONS
    ///                 ITSELF absolutely against whatever holds it (Phase 9
    ///                 Task 19 made it the bottom sheet five specs call it),
    ///                 so it fills this frame the way it fills that layer.
    ///                 The old reason given here - "it carries no padding" -
    ///                 stopped being true in that task: the padding moved one
    ///                 level in, to `#surface`, and the root positions.
    ///   WaveHudView - never reaches ScreenHost at all. `WaveRunner` adds it
    ///                 straight to the wave scene's own panel root.
    ///   the seven catalogues - Primitives, Icons, Scaffold, Components,
    ///                 Vocabulary, Lane and Band are not screens and have no
    ///                 place in the shell.
    public static bool GoesInTheScreenHost(string name)
    {
        switch (name)
        {
            case "CodexSheet":
            case "WaveHudView":
            case "Primitives":
            case "Icons":
            case "Scaffold":
            case "Components":
            case "Vocabulary":
            case "Lane":
            case "Band":
                return false;
            default:
                return true;
        }
    }

    public static VisualElement Build(string name)
    {
        switch (name)
        {
            case "CampaignSelectView": return CampaignSelect();
            case "CodexSheet": return Codex();
            case "DeployView": return Deploy();
            case "FounderNamingView": return FounderNaming();
            case "LineageView": return Lineage();
            case "PostWaveView": return PostWave();
            case "PostWaveLoss": return PostWaveLost();
            case "RegionView": return Region();
            case "RosterView": return Roster();
            case "SpliceChamberView": return SpliceChamber();
            case "SpliceRevealView": return SpliceReveal();
            case "WaveDefeatView": return WaveDefeat();
            case "WaveHudView": return WaveHud();
            case "Primitives": return Primitives();
            case "Icons": return Icons();
            case "Scaffold": return Scaffold();
            case "Components": return Components();
            case "Vocabulary": return Vocabulary();
            case "Lane": return Lane();
            case "Band": return Band();
            default: throw new ArgumentException("ScreenFixtures has no fixture named '" + name + "'", nameof(name));
        }
    }

    // ---------------------------------------------------------------
    // Shared builders - same shape as ScreenBindingTests.cs /
    // FirstHourScreensTests.cs / WaveScreensTests.cs in Broodline.UI.Tests.
    // ---------------------------------------------------------------

    /// `species` IS ONE OF BIBLE 1.2's SIX AND NOT A DISPLAY NAME. These
    /// fixtures carried "Vetch Crawler" and "Ember Skitter" until Phase 8
    /// Task 13; `broodline_data_model.md` section 2 says the field is "one of
    /// six", and neither of those is. "Ember Skitter" was the worse of the
    /// two - it contains the names of TWO species, so nothing can read a
    /// species out of it without guessing which. (The plan read it as a
    /// Skitter; it could as easily have been read as an Ember.)
    ///
    /// It was invisible while nothing consumed the field. `SpeciesProxy`
    /// (Phase 8 Task 13, retired by Phase 9 Task 9's `CreatureSprites`) was
    /// the first consumer, it matched the six exactly and refused to guess,
    /// and with the old strings in place fourteen of the sixteen captures
    /// would have shown an empty silhouette slot and looked like a wiring
    /// bug. The same strings are still in `Broodline.UI.Tests`, where they
    /// are harmless: no test there reads the field AS a species - two assert
    /// it appears verbatim in the destruction notice, which is a test about
    /// CreatureLabel and passes whatever the string is. See this file's
    /// class comment on why the two copies are expected to drift.
    static CreatureDto Creature(
        string species, int generation, string name, bool founder, Guid? id = null,
        string trait1 = "Chill", int? tier1 = 1, string trait2 = "Guard", int? tier2 = 1)
    {
        return new CreatureDto
        {
            CreatureId = id ?? Guid.NewGuid(),
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

    static LineageNode Node(
        string species, Guid id, int generation, bool founder,
        string consumedAt = null, bool mutated = false,
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
            Trait1 = "Chill",
            Tier1 = 1,
            Trait2 = "Taunt",
            Tier2 = 2,
        };
    }

    static SplicePreviewResponse Preview()
    {
        var r = new SplicePreviewResponse();
        r.Forecast = new SpliceForecast { Mutation = 0.09, Aberrant = 0.01 };
        r.Forecast.Combat2.Add(new CombatOutcomeDto { Trait = "Guard", Tier = 1, P = 0.55 });
        r.Forecast.Combat2.Add(new CombatOutcomeDto { Trait = "Ward", Tier = 2, P = 0.45 });
        r.Forecast.Instinct.Add(new Instinct { Instinct1 = "Forage", P = 1.0 });
        return r;
    }

    /// The chamber's forecast, drawn from the two parents' OWN traits.
    ///
    /// THREE ROWS AND NOT TWO, WHICH IS THE COUNT THE CARD IS SIZED FOR.
    /// `combatPool` is the two parents' four combat slots and `merged()`
    /// collapses duplicate (trait, tier) pairs, so three distinct outcomes is
    /// the ceiling and is what the handoff draws (`Splice Chamber.dc.html
    /// :139-173`). A two-row fixture could not show what three bars look like
    /// stacked, which is the whole point of the component.
    ///
    /// AND THE ODDS STRADDLE THE DOM/REC LINE - 0.78, 0.54 and 0.31 are the
    /// handoff's own three numbers, so the capture shows two `DOM` tags and
    /// one `REC` rather than three of a kind.
    static SplicePreviewResponse Preview(CreatureDto a, CreatureDto b)
    {
        var r = new SplicePreviewResponse();
        r.Forecast = new SpliceForecast { Mutation = 0.09, Aberrant = 0.01 };
        r.Forecast.Combat2.Add(new CombatOutcomeDto { Trait = a.Trait1, Tier = a.Tier1, P = 0.78 });
        r.Forecast.Combat2.Add(new CombatOutcomeDto { Trait = b.Trait1, Tier = b.Tier1, P = 0.54 });
        r.Forecast.Combat2.Add(new CombatOutcomeDto { Trait = b.Trait2, Tier = b.Tier2, P = 0.31 });
        r.Forecast.Instinct.Add(new Instinct { Instinct1 = "Forage", P = 1.0 });
        return r;
    }

    // ---------------------------------------------------------------
    // Screens
    // ---------------------------------------------------------------

    static VisualElement CampaignSelect()
    {
        var view = new CampaignSelectView();
        // 1, 2, 6 and 7 are the bundle's actual authored ids (FirstHourScreensTests'
        // own fixture) - 2 cleared puts the row set through Cleared/Next/Locked.
        view.Bind(new[] { 1, 2, 6, 7 }, highestWaveCleared: 2, onPick: _ => { });
        return view;
    }

    static VisualElement Codex()
    {
        var view = new CodexSheet();
        view.Bind(new[]
        {
            new TraitSummary { Id = "Chill", Species = "Pale", Counters = "Courser" },
            new TraitSummary { Id = "Taunt", Species = "Vetch", Counters = "Lash" },
            // bible 1.2: four traits counter nothing by design - Carapace is
            // one, and a null Counters is the fixture for that real state.
            new TraitSummary { Id = "Carapace", Species = "Vetch", Counters = null },
        }, onDismiss: () => { });
        return view;
    }

    /// Wave 7, WHICH IS THE HANDOFF'S OWN WAVE, so this capture and
    /// `specs/Designs/shots/wave-defense.png` can be laid side by side
    /// without first translating one into the other. Its numbers are the
    /// handoff's too - 145 energy, five foes, 120 shards - except the two
    /// this project states differently and says why: `DEPLOYED 2 / 5`
    /// against the handoff's `2 / 4`, because `DeployScreen.Cap` is the
    /// server's (services/api/src/wave/issuance.ts) and its 4 is a drawing;
    /// and `ARK INTEGRITY 100%` against its 82%, because this screen is
    /// shown BEFORE the wave (`DeployScreen.IntegrityFull`).
    ///
    /// FOUR CREATURES FOR TWO POCKETS, so the capture shows BOTH row states -
    /// two filled rows lettered A and B, and two undeployed ones carrying
    /// `DeployScreen.EmptyPocketTag`. A fixture that selected every creature
    /// it owned would render one of the two and leave the other as a code
    /// path nobody has looked at, which is exactly how Pale's hero disc
    /// shipped invisible through two components (see `Band()`).
    ///
    /// FOUR AND NOT SIX, AND THE FIRST CAPTURE DECIDED IT. Six creatures is
    /// three pairs, and three pairs ran the content column past the CTA row:
    /// the last pair was clipped at y=860 in a 932 frame. Nothing is wrong
    /// with that - the scaffold's content region is a ScrollView and a real
    /// roster of twenty scrolls - but a corpus image that cannot show its own
    /// last row cannot be compared against anything. Four is also the
    /// handoff's own count (`Wave Defense.dc.html:246-260` draws exactly four
    /// field rows), which is what makes the two pictures comparable at all.
    ///
    /// NO LANE TEXTURE, AND THAT IS THE DESIGNED STATE HERE RATHER THAN A
    /// GAP. `LaneStage` is a camera and a render texture in `Broodline.Game`;
    /// no fixture in this file drives one, including `FounderNaming()`, which
    /// shows the portrait studio's fallback for the same reason. What the
    /// capture therefore checks is the card's own geometry and its pocket
    /// strip - `.lane-preview-card`'s flat --green-tint fill is described in
    /// its own sheet as being "for the capture corpus, where no stage runs at
    /// all". The picture itself is proven by `LaneStagePlayTests` and by a
    /// human running `Boot.unity`.
    static VisualElement Deploy()
    {
        var roster = new RosterScreen();
        var response = new RosterResponse { Cap = 20 };
        // BIBLE 1.2's SIX, not the handoff's display names. Its lane says
        // "Vetch Wall R2" and "Cinderplate R1"; `broodline_data_model.md`
        // section 2 makes `species` one of six ids, and "Cinderplate" is not
        // one of them. The note at the head of `Creature()` records what the
        // wrong strings cost the last time this file carried them.
        var species = new[] { "Vetch", "Ember", "Loam", "Pale" };
        var selected = new List<Guid>();
        for (var i = 0; i < species.Length; i++)
        {
            var creature = Creature(species[i], i < 2 ? 2 : 1, name: null, founder: false);
            response.Creatures.Add(creature);
            if (i < 2) selected.Add(creature.CreatureId);
        }
        roster.ApplyRoster(response);

        var model = DeployScreen.Build(waveId: 7, roster: roster, selected: selected);
        var view = new DeployView();
        view.Bind(
            model,
            onStart: () => { },
            roster: roster.Known,
            onToggle: _ => { },
            facts: new DeployWaveFacts
            {
                Energy = 145,
                Foes = 5,
                RewardCurrency = "shards",
                RewardAmount = 120,
            });
        return view;
    }

    static VisualElement FounderNaming()
    {
        var founder = Creature("Vetch", 1, name: null, founder: true, id: FounderId);
        var view = new FounderNamingView();
        view.Bind(founder, FounderNamingScreen.DefaultFor(founder), onName: _ => { }, onSkip: () => { });
        return view;
    }

    static VisualElement Lineage()
    {
        var view = new LineageView();
        view.Bind(new LineageResponse
        {
            Nodes = new List<LineageNode>
            {
                Node("Vetch", FounderId, 1, founder: true, name: "Ash"),
                Node("Skitter", ParentAId, 1, founder: false, consumedAt: "2026-09-16T00:00:00Z"),
                Node("Pale", ParentBId, 1, founder: false, consumedAt: "2026-09-16T00:00:00Z"),
                Node("Hollow", ChildId, 2, founder: false, mutated: true,
                    parentA: ParentAId, parentB: ParentBId),
            },
        }, highlight: FounderId, next: () => { });
        return view;
    }

    /// THE WIN. `WaveDefeat()` below is wave 6, the designed loss, so this is
    /// the wave after it - which is also the wave `Wave Defense.dc.html` is
    /// drawn at (`:303`, `wave: 7`).
    static VisualElement PostWave()
    {
        var response = new WaveSubmitResponse
        {
            Result = "Win",
            IntegrityRemaining = 1,
            Reward = new Reward { Currency = "shards", Amount = 150 },
        };
        var granted = new List<CreatureDto> { Creature("Pale", 1, name: null, founder: false) };

        var view = new PostWaveView();
        // waves_01_12 section 3's "expected roster 5" is the deployment both
        // verdict screens state as KEPT.
        view.Bind(response, granted, next: () => { }, wave: 7, deployed: 5);
        return view;
    }

    /// THE SAME SCREEN ON THE OTHER VERDICT, and the only frame in the corpus
    /// that shows it. Everything differs from `PostWave()` that the branch
    /// touches and nothing that it does not: a non-win result, no reward, no
    /// arrivals and no integrity left - so the headline's coral, the collapsed
    /// grant row and an empty reward cell are all readable in one picture.
    static VisualElement PostWaveLost()
    {
        var response = new WaveSubmitResponse
        {
            Result = "Loss",
            IntegrityRemaining = 0,
            Reward = new Reward { Currency = "shards", Amount = 0 },
        };

        var view = new PostWaveView();
        view.Bind(response, new List<CreatureDto>(), next: () => { }, wave: 6, deployed: 5);
        return view;
    }

    static VisualElement Region()
    {
        var state = new RegionStateResponse
        {
            RegionId = "region-1",
            Epoch = 1,
            Roster = new Roster { Count = 5, Cap = 20 },
        };
        // One of each shape RegionScreen.Build branches on: an ordinary
        // accrual, a granting node, and a spent (Remaining == 0) node.
        state.Nodes.Add(new Nodes { Slot = 1, Type = "Shard", Accrued = 12, Remaining = 3, Grants = 0 });
        state.Nodes.Add(new Nodes { Slot = 2, Type = "Grant", Accrued = 0, Remaining = null, Grants = 1 });
        state.Nodes.Add(new Nodes { Slot = 3, Type = "Shard", Accrued = 40, Remaining = 0, Grants = 0 });

        var model = RegionScreen.Build(state);
        var view = new RegionView();
        view.Bind(model, onClaim: _ => { });
        return view;
    }

    static VisualElement Roster()
    {
        var roster = new RosterScreen();
        roster.ApplyRoster(new RosterResponse
        {
            Cap = 20,
            Creatures =
            {
                // ONE OF EACH OF BIBLE 1.2's SIX, and the roster is the right
                // screen to carry them: it is the only one whose job is
                // several creatures at once, so it is where the species
                // palette is actually comparable. Two Vetch-shaped cards told
                // a reviewer nothing about five sixths of the palette, and
                // Phase 8 Task 13 needed a frame in which a Skitter's amber
                // body sits beside the Founder's amber border and a Hollow's
                // violet body sits above the violet CTA. It does; see
                // implementation/results/species-collision.md.
                // AND EACH CARRIES ITS OWN SPECIES' TRAITS AS OF PHASE 9
                // TASK 16, which the default "Chill"/"Guard" pair did not.
                // Two things depended on it and neither was visible before:
                // `CreatureSprites.Part` returns null for a trait the bake
                // has no part for, so five of the six cards were drawing a
                // bare body; and the card's marks are `TraitChip`s now, which
                // are tinted by the species that carries the trait - so a
                // roster of six identical "Chill I / Guard I" pairs would
                // have rendered the one thing the chip exists to show as six
                // copies of the same thing. The pairs are bible 1.2's own
                // table (`broodline_bible.md:42-48`).
                Creature("Vetch", 4, name: "Ash", founder: true,
                    trait1: "Carapace", tier1: 3, trait2: "Taunt", tier2: 1),
                Creature("Skitter", 6, name: null, founder: false,
                    trait1: "Sprint", tier1: 2, trait2: "Litter", tier2: 1),
                Creature("Hollow", 5, name: null, founder: false,
                    trait1: "Reach", tier1: 2, trait2: "Pierce", tier2: 3),
                Creature("Ember", 3, name: null, founder: false,
                    trait1: "Cinder", tier1: 1, trait2: "Splash", tier2: 2),
                Creature("Loam", 2, name: null, founder: false,
                    trait1: "Regrow", tier1: 1, trait2: "Burrow", tier2: 2),
                // AN ABERRANT, WHICH NO FIXTURE HAD. data_model 2 makes a
                // null coverage tier exactly an Aberrant, and it is the
                // state `CreatureCard.aberrant` and `TraitChip.aberrant`
                // both draw - so until this the two treatments existed in
                // the Vocabulary catalogue and in no screen.
                Creature("Pale", 1, name: null, founder: false,
                    trait1: "Screen", tier1: null, trait2: "Chill", tier2: 2),
            },
        });

        var view = new RosterView();
        view.Bind(roster, onSelect: _ => { });
        return view;
    }

    static VisualElement SpliceChamber()
    {
        // THE HANDOFF'S OWN PAIR, IN SPECIES AND IN GENERATION: `Splice
        // Chamber.dc.html` splices a G4 Vetch carrying Carapace III and Root
        // Anchor against a G6 Ember carrying Cinder Spit and Sprint II. Ours
        // is the Vetch/Skitter pair the rest of this harness uses (so the
        // roster and the reveal show the same animals), with each parent's
        // traits its own species' - which is what makes the two tiles' chips
        // tint differently and the inheritance bars' dots disagree.
        var a = Creature("Vetch", 4, name: "Ash", founder: true, id: ParentAId,
            trait1: "Carapace", tier1: 3, trait2: "Taunt", tier2: 1);
        var b = Creature("Skitter", 6, name: null, founder: false, id: ParentBId,
            trait1: "Sprint", tier1: 2, trait2: "Litter", tier2: 1);
        var model = SpliceScreen.Build(a, b, Preview(a, b));

        var view = new SpliceChamberView();
        view.Bind(model, lockedOut: new HashSet<Guid>(), onSplice: () => { });
        return view;
    }

    static VisualElement SpliceReveal()
    {
        // THE SAME PAIR THE CHAMBER SPLICED, AND A CHILD THAT ACTUALLY
        // MUTATED. One trait carries from parent A (Carapace III) and the
        // other is in NEITHER parent (Cinder I), which is what a mutation is
        // - `SpliceRevealScreen.MutatedTrait` is a set difference over these
        // three creatures, so a fixture whose child only held its parents'
        // traits would render the `mutated: true` pill with no trait to name
        // and both rows reading "From ...". The generation is the server's
        // own `max(4, 6) + 1` (`splice/commit.ts:292`), which the chamber's
        // predicted panel states one screen earlier.
        var a = Creature("Vetch", 4, name: "Ash", founder: true, id: ParentAId,
            trait1: "Carapace", tier1: 3, trait2: "Taunt", tier2: 1);
        var b = Creature("Skitter", 6, name: null, founder: false, id: ParentBId,
            trait1: "Sprint", tier1: 2, trait2: "Litter", tier2: 1);
        var child = Creature("Vetch", 7, name: null, founder: false, id: ChildId,
            trait1: "Carapace", tier1: 3, trait2: "Cinder", tier2: 1);
        var committed = new SpliceCommitResponse
        {
            Child = child,
            SpliceId = Guid.NewGuid(),
            Seed = "7",
            Balance = 2,
        };

        var view = new SpliceRevealView();
        view.Bind(committed, a, b, mutated: true, next: () => { });
        return view;
    }

    static VisualElement WaveDefeat()
    {
        var report = new WaveReport
        {
            Result = "Loss",
            Ticks = 540,
            IntegrityRemaining = 0,
            ReplayBytes = new byte[] { 1, 2, 3 },
            Breaches = new List<BreachSummary>
            {
                // bible 4.11/9.3's actual wave 6 case: a Courser broke
                // through and Chill would have answered it, but nothing
                // deployed carried Chill at all - the teaching moment this
                // screen exists for, not an edge case of it.
                new BreachSummary
                {
                    RaiderType = "Courser",
                    Counter = "Chill",
                    Access = false,
                    Coverage = false,
                    Placement = false,
                },
            },
        };
        var granted = new List<CreatureDto> { Creature("Pale", 1, name: null, founder: false) };

        var view = new WaveDefeatView();
        // wave 6 and waves_01_12 section 3's "expected roster 5, none carrying
        // Chill" - the deployment the KEPT cell states. The granted Pale
        // carries Chill I (the `Creature` helper's own default), which is what
        // lets the counter chip take a real tier and a real species tint
        // rather than the tier-less fallback `WaveDefeatView.Bind` describes.
        view.Bind(report, granted, retry: () => { }, wave: 6, deployed: 5);
        return view;
    }

    static VisualElement WaveHud()
    {
        var snapshot = new HudSnapshot
        {
            Integrity = 2,
            Tick = 340,
            Bodies = new List<BodyBar>
            {
                new BodyBar { Kind = BodyKind.Raider, World = new Vector3(1f, 0f, 0f), Hp = 100, MaxHp = 100, State = BodyState.Normal },
                new BodyBar { Kind = BodyKind.Raider, World = new Vector3(2f, 0f, 0f), Hp = 40, MaxHp = 100, State = BodyState.Breaching },
                new BodyBar { Kind = BodyKind.Raider, World = new Vector3(3f, 0f, 0f), Hp = 80, MaxHp = 100, State = BodyState.Chilled },
                new BodyBar { Kind = BodyKind.Creature, World = new Vector3(4f, 0f, 0f), Hp = 60, MaxHp = 100, State = BodyState.Rallied, RallyRemaining = 42 },
            },
        };

        // No Camera assigned - WaveHudView.Place returns early without one
        // (its own doc: "silently skipped with no panel or no camera"), so
        // every bar stacks at its unpositioned default rather than throwing.
        // That is a real, reachable state (a HUD bound before its camera is
        // wired) and not a fixture defect to paper over.
        var view = new WaveHudView();
        // WAVE 6, WHICH IS THE ONE THIS HUD IS EVER CAPTURED ON.
        // `WaveRunner.CaptureWaveId` is 6 and waves_01_12 section 3 designs it
        // as the loss; the snapshot's integrity of 2 above is that wave's
        // authored pool. Set BEFORE `Bind` for no reason other than reading
        // order - `Wave` writes its own Label and never touches the snapshot.
        view.Wave = 6;
        view.Bind(() => snapshot);

        // CAPTIONED, BECAUSE THE CORPUS CANNOT TELL THIS STATE FROM A DEFECT.
        // Fix round 1's own minor: with no camera every bar stacks at (0,0)
        // and draws BREACH/RALLY across the chrome, and a reader of
        // `WaveHudView.png` cannot distinguish that from the clamp failing -
        // which is the one risk on this screen that no test reaches, because
        // `Place` returns early without a panel and a camera. The caption is
        // ABSOLUTE so it changes none of the HUD's own layout, and the HUD
        // keeps `flex-grow` so the frame it is measured in is unchanged.
        var captioned = new VisualElement { name = "wave-hud-fixture" };
        captioned.style.flexGrow = 1;
        captioned.Add(view);

        var caption = ComponentCaption(
            "WaveHudView  -  no camera in this fixture, so every bar stacks at (0,0). "
            + "Bar POSITIONS here are not evidence of anything.");
        caption.style.position = Position.Absolute;
        caption.style.left = 12;
        caption.style.right = 12;
        caption.style.bottom = 12;
        caption.style.whiteSpace = WhiteSpace.Normal;
        captioned.Add(caption);
        return captioned;
    }

    // ---------------------------------------------------------------
    // Components
    // ---------------------------------------------------------------

    /// NOT A SCREEN. The class vocabulary Theme.uss defines but no screen
    /// carries yet.
    ///
    /// WHY IT EXISTS. `.elev-1`, `.elev-2` and `.btn-primary` are produced by
    /// Phase 8 Task 3 and consumed by Tasks 8-12; measured at Task 3, they
    /// have zero usages anywhere in `client/` (real CTAs are per-screen BEM
    /// classes - the Splice CTA is `splice-chamber-view__cta`). So the twelve
    /// screen captures come back byte-identical however those three rules
    /// render, or fail to. That is the plan's own amendment finding 4 - a
    /// byte-identical corpus "looks like no regression and is actually not
    /// looked at" - and this fixture is the one picture that tells the
    /// difference. It is deliberately the only thing Task 3 adds to the
    /// corpus: putting `.elev-1` on a real card is Tasks 8-12's work.
    ///
    /// GENEROUS SPACING IS THE POINT, not layout taste. A shadow clipped by
    /// its own container proves nothing, so every card here has room around
    /// it - and the fourth card carries no elevation class at all, because
    /// "the shadow rendered" is only readable against a card that has none.
    ///
    /// THE ELEVATION CLASSES GO ON A WRAPPER, and this fixture is the
    /// reference for that arrangement. The first version of this fixture put
    /// .elev-1 on the card itself, as Theme.uss then invited, and the capture
    /// is what caught it: card centre 248/245 against an unelevated 255,
    /// densest in the middle, with nothing at all outside the card. UI Toolkit
    /// clips background-image to the element's own box, so the shadow has to
    /// be drawn by something larger than the thing casting it.
    static VisualElement Primitives()
    {
        // .shell-root is --paper, so the fixture gets the real screen
        // background from the token layer rather than a hardcoded colour.
        var root = new VisualElement();
        root.AddToClassList("shell-root");
        root.style.flexGrow = 1;
        root.style.paddingLeft = 40;
        root.style.paddingRight = 40;
        root.style.paddingTop = 48;
        root.style.paddingBottom = 48;

        var title = new Label("Theme primitives");
        title.AddToClassList("t-screen-title");
        title.style.marginBottom = 32;
        root.Add(title);

        root.Add(PrimitiveCard("elev-1", ".elev-1 wrapper  -  handoff 0 2px 8px"));
        root.Add(PrimitiveCard("elev-2", ".elev-2 wrapper  -  handoff 0 4px 16px"));
        root.Add(PrimitiveCard(null, "no wrapper  -  the control"));

        var cta = new Button { text = "Splice" };
        cta.AddToClassList("btn-primary");
        cta.style.marginTop = 40;
        root.Add(cta);

        var note = new Label("btn-primary carries the 2x64 ramp: #8878cf at the top, #6f5fbb at the bottom.");
        note.AddToClassList("t-secondary");
        note.style.whiteSpace = WhiteSpace.Normal;
        note.style.marginTop = 16;
        root.Add(note);

        return root;
    }

    /// One `.card` (`--surface` fill, `--radius-card`), wrapped in an
    /// elevation class when it wants one, with 40px of clear paper under it so
    /// the shadow has somewhere to fall.
    ///
    /// The control returns the bare card with no wrapper, so "no elevation"
    /// means no wrapper at all rather than a wrapper that draws nothing.
    static VisualElement PrimitiveCard(string elevationClass, string caption)
    {
        var card = new VisualElement();
        card.AddToClassList("card");
        card.style.height = 116;
        card.style.marginBottom = 0;

        var label = new Label(caption);
        label.AddToClassList("t-card-title");
        label.style.whiteSpace = WhiteSpace.Normal;
        card.Add(label);

        if (elevationClass == null)
        {
            card.style.marginBottom = 40;
            return card;
        }

        var wrapper = new VisualElement();
        wrapper.AddToClassList(elevationClass);
        wrapper.style.marginBottom = 40;
        wrapper.Add(card);
        return wrapper;
    }

    /// The thirteen glyphs, each at the 19px it is really drawn at, beside the
    /// `.icon--` name that maps to it - and under them a live `TabBar`
    /// rendering the five nav tabs through the same classes the runtime uses.
    ///
    /// WHY THIS EXISTS AT ALL. `Shell.uxml` is the only thing that mounts a
    /// `TabBar`, and no screen fixture mounts a shell - so without this the
    /// icon task lands as thirteen PNGs, one stylesheet, and twelve
    /// byte-identical screens. That is the plan's amendment finding 4 again: a
    /// corpus that cannot see a change reads as "no regression" when what it
    /// actually means is "not looked at". Same reason as `Primitives`, one
    /// task later.
    ///
    /// THE NAME BESIDE THE MARK IS THE POINT, not decoration. Nothing else in
    /// this task can tell `gem.svg` from `award.svg`: TabBarTests asserts a
    /// tab carries the class `icon--shard`, check-stylesheets asserts the rule
    /// compiles, and both stay green if `shard.png` is a padlock. A wrong
    /// mapping is invisible to every automated check here and obvious on this
    /// grid, which is the only place it can be caught.
    ///
    /// THE BAR RENDERS WITH `Splice` ACTIVE so both tint states are in the
    /// picture - four glyphs at `--mute-soft`, one at `--violet-text`. That
    /// pair is also the check on how the rasters were authored: Lucide's
    /// `stroke="currentColor"` rasterises to BLACK, and
    /// `-unity-background-image-tint-color` MULTIPLIES, so black glyphs would
    /// ignore both tints and render five identical black marks. They are
    /// authored white for exactly this reason, and this is where you see it.
    static VisualElement Icons()
    {
        // .shell-root is --paper, as in Primitives - the real screen
        // background out of the token layer rather than a hardcoded colour.
        var root = new VisualElement();
        root.AddToClassList("shell-root");
        root.style.flexGrow = 1;
        root.style.paddingLeft = 32;
        root.style.paddingRight = 32;
        root.style.paddingTop = 48;

        var title = new Label("Icons");
        title.AddToClassList("t-screen-title");
        title.style.marginBottom = 20;
        root.Add(title);

        // The order `icons.uss` declares them in, which is also the order the
        // plan's Interfaces line names them.
        foreach (var name in new[]
                 {
                     "map", "ark", "splice", "lab", "allies", "back", "charge",
                     "shard", "tier", "timer", "lock", "check", "warning",
                 })
        {
            root.Add(GlyphRow(name));
        }

        var caption = new Label("TabBar, live - Splice active");
        caption.AddToClassList("t-secondary");
        caption.style.marginTop = 24;
        caption.style.marginBottom = 8;
        root.Add(caption);

        var bar = new TabBar();
        bar.Render(new List<string> { "Map", "Ark", "Splice", "Lab", "Allies" }, "Splice", _ => { });

        // SHELL.USS, ON THE BAR'S SUBTREE ONLY, AND IT IS NOT OPTIONAL HERE.
        // `ScreenHarness.AddShellStyles` loads Tokens, Theme, icons and
        // Motion - not Shell.uss - so every `.tab-bar*` rule is absent from
        // this capture: the row direction, the --surface fill, the divider,
        // and the block that undoes `Button`'s primary-CTA fill for nav items.
        // Without it a real TabBar draws as five violet CTA buttons in a
        // COLUMN, which is not what ships and would make this fixture lie
        // about the thing it exists to show. Scoped to `bar` rather than added
        // to AddShellStyles deliberately: Shell.uss also carries `.shell-root`
        // safe-area padding off `--safe-top`/`--safe-bottom`, which only
        // SafeAreaBinder sets, so putting it on the panel root would re-lay
        // out all thirteen other fixtures for no gain.
        var shell = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/UI/Shell/Shell.uss");
        if (shell != null) bar.styleSheets.Add(shell);

        // Out to the fixture's edges, as the tab-bar slot sits at runtime -
        // the bar draws its own --surface fill and top divider, and inset by
        // 32px it would read as a floating card instead of shell chrome.
        bar.style.marginLeft = -32;
        bar.style.marginRight = -32;
        root.Add(bar);

        return root;
    }

    /// One glyph at its real 19px with the name that maps to it beside it.
    /// No scaling up: a mark that is unreadable at the size it ships at is a
    /// finding, not something for this fixture to flatter away.
    static VisualElement GlyphRow(string name)
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.marginBottom = 9;

        var glyph = new VisualElement();
        glyph.AddToClassList("icon");
        glyph.AddToClassList("icon--" + name);
        row.Add(glyph);

        var label = new Label(name);
        label.AddToClassList("t-card-title");
        label.style.marginLeft = 14;
        row.Add(label);

        return row;
    }

    /// NOT A SCREEN. `ScreenScaffold` on its own, in both of the states the
    /// handoff gives it.
    ///
    /// WHY IT EXISTS. Nothing consumes the scaffold until Task 9, so at the
    /// commit that adds it all twelve screen captures come back
    /// byte-identical and its only other artifact is a test that is
    /// DELIBERATELY RED. Without this fixture the task lands as three files
    /// nobody can look at - the plan's amendment finding 4 for the third
    /// time, and the same reason `Primitives` and `Icons` exist one and two
    /// tasks back.
    ///
    /// TWO FRAMES, STACKED, AND THE COST IS WORTH NAMING. The capture target
    /// is a single 430x932, so two frames in it are each shorter than a real
    /// screen: the one thing this picture CANNOT judge is the vertical
    /// proportion of a full-height scaffold. That is the trade for what it
    /// can show - the two frames differ only in `pushed` and in whether
    /// `FooterNote` is set, so the chevron's presence and the note's row
    /// collapsing both read as a difference between two pictures rather than
    /// as a claim in prose.
    ///
    /// THE CONTENT IS REAL `CreatureCard`s, not grey blocks. The question a
    /// reviewer is actually asking here is whether the 12px gutter, the
    /// header's 8-14-10 padding and the CTA row hold a real screen's
    /// furniture at the sizes the handoff gives them, and a placeholder
    /// rectangle cannot answer it.
    static VisualElement Scaffold()
    {
        // .shell-root is --paper, as in Primitives and Icons - the real
        // screen background out of the token layer.
        var root = new VisualElement();
        root.AddToClassList("shell-root");
        root.style.flexGrow = 1;

        root.Add(ScaffoldCaption("pushed: true   -   chevron, FooterNote set"));

        // No flexGrow set here: .screen-scaffold already carries flex-grow 1,
        // so this frame takes whatever the fixed one below it leaves.
        // EYEBROW AND RESOURCE PILL ON THE PUSHED FRAME ONLY, so one capture
        // holds both header forms: the handoff's full header above, and the
        // bare title-only header ten screens still build below. That pair is
        // the only check in existence on whether the eyebrow actually hides
        // when nothing sets it.
        var pushed = new ScreenScaffold("Splicing Chamber", pushed: true, onBack: () => { },
            eyebrow: "Gene Lab");
        FillScaffold(pushed);

        // THE CURRENCY HEADER GOES SO THE PILL CAN BE SEEN. Both live in
        // `HeaderSlot` and both answer "what do I have"; no screen in the
        // handoff shows two of them, and side by side in a 390 frame neither
        // reads. The unpushed frame below keeps the currency header, so the
        // capture still holds one of each.
        pushed.HeaderSlot.Clear();
        pushed.SetResourcePill("icon--charge", "4", "/5");
        pushed.FooterNote = "Consumes both parents.";
        root.Add(pushed);

        var divider = new VisualElement();
        divider.AddToClassList("divider");
        root.Add(divider);

        root.Add(ScaffoldCaption("pushed: false   -   no chevron, FooterNote null"));

        // Fixed and non-growing, against .screen-scaffold's own flex-grow 1 -
        // otherwise the two frames split the free space evenly and neither is
        // tall enough to hold its content.
        var top = new ScreenScaffold("Gene Ark");
        FillScaffold(top);
        top.style.flexGrow = 0;
        top.style.flexShrink = 0;
        top.style.height = 300;
        root.Add(top);

        return root;
    }

    /// The same furniture in both frames, so the only differences between the
    /// two are the two the fixture is about. Deliberately more content than
    /// the short frame can hold: the content region is the only scrolling one
    /// in the handoff's column, and a frame whose body is clipped at the CTA
    /// row is what that looks like when it is working.
    static void FillScaffold(ScreenScaffold scaffold)
    {
        var currency = new CurrencyHeader();
        currency.Bind(new Dictionary<string, int> { { "shards", 1240 } });
        scaffold.HeaderSlot.Add(currency);

        // A WRAPPING ROW, NOT A COLUMN, and the first capture of this fixture
        // is what caught it: `.creature-card` is a fixed `width: 160px` tile
        // authored for the two-column grid `RosterView.uss` lays out
        // (`flex-direction: row; flex-wrap: wrap; justify-content:
        // space-between`). Stacked in a column each card kept its 160px and
        // the content region read as two-fifths full, which says nothing true
        // about whether the 12px gutter holds a real screen's furniture - the
        // one question this fixture exists to answer.
        var cards = new VisualElement();
        cards.style.flexDirection = FlexDirection.Row;
        cards.style.flexWrap = Wrap.Wrap;
        cards.style.justifyContent = Justify.SpaceBetween;

        var a = new CreatureCard();
        a.Bind(Creature("Vetch", 4, name: "Ash", founder: true, id: ParentAId), null);
        cards.Add(a);

        var b = new CreatureCard();
        b.Bind(Creature("Skitter", 6, name: null, founder: false, id: ParentBId), null);
        cards.Add(b);

        scaffold.Content.Add(cards);

        var forecast = new VisualElement();
        forecast.AddToClassList("panel-violet");
        var line = new Label("Mutation 9%   -   Aberrant 1%");
        line.AddToClassList("t-secondary");
        forecast.Add(line);
        scaffold.Content.Add(forecast);

        var primary = new Button { text = "Splice" };
        primary.AddToClassList("btn-primary");
        scaffold.CtaRow.Add(primary);

        var secondary = new Button { text = "Back to the Ark" };
        secondary.AddToClassList("btn-secondary");
        scaffold.CtaRow.Add(secondary);
    }

    /// Fixture chrome, not scaffold chrome - it says which of the two frames
    /// below it is which, and nothing in a real screen looks like this.
    static Label ScaffoldCaption(string text)
    {
        var label = new Label(text);
        label.AddToClassList("t-micro");
        label.style.paddingLeft = 14;
        label.style.paddingTop = 10;
        label.style.paddingBottom = 4;
        return label;
    }

    /// NOT A SCREEN. The five shared components of Task 8, each in the states
    /// that actually differ.
    ///
    /// WHY IT EXISTS - the fourth time in this phase, and the same reason
    /// each time. Nothing composes these five until Task 9, so at the commit
    /// that adds them all fifteen existing captures come back byte-identical
    /// and the only other artifact is a green test file. The plan's
    /// amendment finding 4 names that state exactly: a corpus that cannot
    /// see a change reads as "no regression" when what it means is "not
    /// looked at". Primitives, Icons and Scaffold each exist for this, one,
    /// two and three tasks back.
    ///
    /// TWO THINGS HERE ARE WORTH A REVIEWER'S EYE AND NOTHING ELSE CAN
    /// ANSWER THEM:
    ///
    /// 1. WHETHER SectionCard's ELEVATION READS AS LIFTED RATHER THAN DIRTY.
    ///    `.elev-1` is a nine-sliced sprite, and putting it on the surface
    ///    instead of on a wrapper stretches the sprite's centre region across
    ///    the card's interior - a grey smudge, not a shadow. That shipped
    ///    once this phase and only the Primitives capture caught it. The two
    ///    cards below sit on clear --paper with un-elevated surfaces in the
    ///    same frame to be read against - the two option rows above them and
    ///    the two empty states below. Measured on this capture: the card
    ///    interior is pure --surface or pure --surface-sunk at every sample,
    ///    with no gradient, and the falloff is outside the card, reaching 4
    ///    of 255 directly under its bottom edge and fading over about 3px.
    ///    That is faint - it is --elev-1-tint's own 6% - and it is a lift
    ///    rather than a stain.
    ///
    /// 2. WHETHER ProgressBar AT 0 AND AT 1 LOOK DELIBERATE. Both are the
    ///    shapes that break: a 0 fill is a track with nothing in it, which
    ///    must still read as an empty measure rather than as a stray rule,
    ///    and a 1 fill must take the track's rounded ends rather than
    ///    squaring them off - the fill carries no radius of its own and
    ///    depends entirely on the track's `overflow: hidden` clipping to the
    ///    border radius. A test can read the width back; only a picture can
    ///    say whether the corner got clipped. They are rendered adjacent,
    ///    both in the default violet, so the comparison is geometry and not
    ///    colour, with a near-empty 0.08 under them as the third hard case.
    static VisualElement Components()
    {
        // .shell-root is --paper, as in the other three - the real screen
        // background out of the token layer.
        var root = new VisualElement();
        root.AddToClassList("shell-root");
        root.style.flexGrow = 1;
        root.style.paddingLeft = 32;
        root.style.paddingRight = 32;
        root.style.paddingTop = 20;

        // NOTHING IN THIS COLUMN MAY SHRINK - this file's class comment has
        // the measurement and why it applies to every fixture, not just
        // this one. The short version: a 430x932 frame this full overflows,
        // and flexbox answers an overflow by shrinking rather than by
        // clipping, so the 6px ProgressBar tracks went to zero height and
        // vanished from the first capture of this fixture altogether.
        void Stack(VisualElement child)
        {
            child.style.flexShrink = 0;
            root.Add(child);
        }

        var title = new Label("Components");
        title.AddToClassList("t-screen-title");
        title.style.marginBottom = 8;
        Stack(title);

        Stack(ComponentCaption("OptionRow  -  selected, then not"));
        var picked = new OptionRow("Coast road", "4h  -  low risk", () => { });
        picked.Selected = true;
        Stack(picked);
        Stack(new OptionRow("Night corridor", "6h  -  raider sightings", () => { }));

        Stack(ComponentCaption("SectionCard  -  with a heading, then without"));

        // A heading plus the handoff's three-cell stat row, which is what
        // StatCell is for - "Control / Travel / Arks" under the region detail
        // card. Three cells, so the flex-basis 0 that makes them equal width
        // is visible rather than asserted.
        //
        // THE ROW DIRECTION IS SET HERE, ON THE CONTAINER, and that is not
        // laziness: StatCell.uss cannot reach its own parent, so a row of
        // cells is the caller's to arrange. Its closing note has the full
        // account. The -4px margins cancel the 4px each cell carries, so the
        // row sits flush with the card's padding.
        var stats = new SectionCard("Region R-04");
        var statRow = new VisualElement();
        statRow.style.flexDirection = FlexDirection.Row;
        statRow.style.marginLeft = -4;
        statRow.style.marginRight = -4;
        // ONE OF EACH TREND AND ONE WITHOUT, in a row, because an arrow is
        // only legible against the other two: "is that green triangle big
        // enough" is unanswerable next to nothing, and the middle cell is
        // the proof that a cell with no trend reserves no space for one.
        statRow.Add(new StatCell("Control", "62%", StatCell.Trend.Up));
        statRow.Add(new StatCell("Travel", "4h 20m"));
        statRow.Add(new StatCell("Arks", "2", StatCell.Trend.Down));
        stats.Body.Add(statRow);
        Stack(stats);

        // No heading - the card must not reserve a row for one. The bars are
        // its body, so the unheaded card is also where ProgressBar gets
        // looked at. 0 and 1 are adjacent and both in the default violet, so
        // the comparison between them is geometry rather than colour.
        var bars = new SectionCard();
        bars.Body.Add(BarRow("fill 0.00  -  empty", 0f, null));
        bars.Body.Add(BarRow("fill 1.00  -  full", 1f, null));
        bars.Body.Add(BarRow("fill 0.08  -  nearly empty", 0.08f, null));
        bars.Body.Add(BarRow("fill 0.50  -  teal", 0.5f, "teal"));
        bars.Body.Add(BarRow("fill 0.78  -  green", 0.78f, "green"));
        Stack(bars);

        Stack(ComponentCaption("EmptyState  -  with a glyph, then without"));

        // SIDE BY SIDE, AND ON BARE PAPER. Stacked and carded, the two of
        // them plus their wrappers ran past the bottom of the 932px frame
        // and the second one was cut off entirely - measured on the second
        // capture of this fixture. A row costs the height of one instead of
        // two, and paper rather than --surface means the two elevated cards
        // above have an un-elevated surface in the same frame to be read
        // against, which is the only way "lifted" is legible at all.
        //
        // `ark` is one of icons.uss's thirteen. A name outside that set
        // renders as an empty 19px box with no error anywhere, which is
        // precisely the kind of thing only a capture catches.
        var empties = new VisualElement();
        empties.style.flexDirection = FlexDirection.Row;
        var withGlyph = new EmptyState("Nothing in the Ark yet.", "ark");
        withGlyph.style.flexGrow = 1;
        withGlyph.style.flexBasis = 0;
        var withoutGlyph = new EmptyState("No creatures yet.");
        withoutGlyph.style.flexGrow = 1;
        withoutGlyph.style.flexBasis = 0;
        empties.Add(withGlyph);
        empties.Add(withoutGlyph);
        Stack(empties);

        return root;
    }

    /// Seven of the nine parts Phase 9 Task 13 added: everything the splice
    /// chamber is built from, stacked in the order that screen stacks it.
    ///
    /// TWO NEW CATALOGUES, AND THE FRAME IS WHY. `Components` was already
    /// using all 932 of the capture's pixels; these nine parts add roughly
    /// 900 more, and this file's class comment is explicit about what a flex
    /// column does with an overflow - it SHRINKS every child that will
    /// shrink, silently, which once took a 6px ProgressBar track to zero
    /// height and out of the picture altogether. MEASURED RATHER THAN
    /// ESTIMATED: the first capture of this fixture held all nine and lost
    /// the field rows and the cost row off the bottom of the frame
    /// completely, with the 274px lane card taking a third of the height on
    /// its own.
    ///
    /// SPLIT BY SCREEN, NOT BY SIZE. The two that moved to `Lane` are the
    /// two the wave screen uses and this one does not, so each frame is a
    /// screen's vocabulary rather than an arbitrary half of a list - which
    /// is also what makes each one worth putting beside the handoff capture
    /// it corresponds to.
    static VisualElement Vocabulary()
    {
        var root = CatalogueRoot();

        void Stack(VisualElement child)
        {
            child.style.flexShrink = 0;
            root.Add(child);
        }

        var title = new Label("Vocabulary");
        title.AddToClassList("t-screen-title");
        Stack(title);

        Stack(ComponentCaption("GenChip  ·  TraitChip  -  the six species tints, and an aberrant"));

        // WRAPPING, and the first capture is why: five chips at the handoff's
        // own sizes are wider than the 366px content width, and the fifth ran
        // off the right edge. A row of chips is the caller's to arrange -
        // StatCell.uss's closing note - so the wrap is set here.
        var chips = new VisualElement();
        chips.style.flexDirection = FlexDirection.Row;
        chips.style.flexWrap = Wrap.Wrap;
        chips.style.alignItems = Align.Center;
        chips.style.marginBottom = 8;
        chips.Add(new GenChip(4));
        chips.Add(new TraitChip("Carapace", 3, "Vetch"));
        chips.Add(new TraitChip("Cinder", 2, "Ember"));
        chips.Add(new TraitChip("Sprint", null, "Skitter"));
        chips.Add(new TraitChip("Reach", 2, "Hollow"));
        chips.Add(new TraitChip("Regrow", 1, "Loam"));
        chips.Add(new TraitChip("Screen", 1, "Pale"));
        Stack(chips);

        Stack(ComponentCaption("HeroSlot  -  all six species tints, bound-but-unbaked, and live"));

        // ALL SIX, NOT THREE. Fix round 1, Minor 4: the first capture showed
        // Vetch, Ember and the live form and left four of the six
        // `.hero-slot--*` ring and disc rules - Skitter, Hollow, Loam, Pale -
        // never rendered anywhere in the corpus. Vetch is the one species
        // baked before Task 15, so it is the only slot that shows real
        // sprites; the other five are bound for their tint with no art
        // behind them, which is the honest picture until Task 15.
        //
        // WRAPPED, LIKE THE CHIP ROW ABOVE, for the same reason: seven 96px
        // slots are 672px and the content width is 366-406px. Margins are
        // set here rather than in HeroSlot.uss because a row's layout is the
        // caller's to arrange - StatCell.uss's closing note.
        var slots = new VisualElement();
        slots.style.flexDirection = FlexDirection.Row;
        slots.style.flexWrap = Wrap.Wrap;
        slots.style.alignItems = Align.Center;

        void AddSlot(HeroSlot slot)
        {
            slot.style.marginRight = 8;
            slot.style.marginBottom = 8;
            slots.Add(slot);
        }

        var vetch = new HeroSlot();
        vetch.Bind(Creature("Vetch", 4, name: "Ash", founder: true,
            trait1: "Carapace", tier1: 1, trait2: "Taunt", tier2: 1));
        AddSlot(vetch);

        var ember = new HeroSlot();
        ember.Bind(Creature("Ember", 6, name: null, founder: false));
        AddSlot(ember);

        var skitter = new HeroSlot();
        skitter.Bind(Creature("Skitter", 3, name: null, founder: false));
        AddSlot(skitter);

        var hollow = new HeroSlot();
        hollow.Bind(Creature("Hollow", 5, name: null, founder: false));
        AddSlot(hollow);

        var loam = new HeroSlot();
        loam.Bind(Creature("Loam", 2, name: null, founder: false));
        AddSlot(loam);

        var pale = new HeroSlot();
        pale.Bind(Creature("Pale", 1, name: null, founder: false));
        AddSlot(pale);

        // The live form, with nothing in it: no camera runs in a headless
        // capture, so what this shows is the frame around a portrait - the
        // violet ring and the transparent stage - which is exactly the part
        // of it this project owns.
        AddSlot(new HeroSlot(new CreatureStage()));
        Stack(slots);

        Stack(ComponentCaption("InheritanceBar  ·  MutationBanner"));
        Stack(new InheritanceBar("Carapace III", 0.78f, "DOM", "teal"));
        Stack(new InheritanceBar("Cinder Spit", 0.54f, "DOM", "coral"));
        Stack(new InheritanceBar("Sprint II", 0.31f, "REC", "mute"));

        var banner = new MutationBanner();
        banner.Text = "Mutation window open  -  1 in 9 chance of an unlisted trait";
        Stack(banner);

        Stack(ComponentCaption("LineageStrip  ·  CostCtaRow"));
        var lineage = new LineageStrip();
        lineage.Bind(
            new[] { (1, "vetch", false), (3, "vetch", false), (4, "vetch", false),
                    (6, "ember", false), (7, "hollow", true) },
            "Unbroken Vetch line since G1  -  pedigree bonus +12% trait fidelity");
        Stack(lineage);

        var cost = new CostCtaRow("icon--charge", "2", "Begin Splice", () => { });
        cost.style.marginTop = 12;
        Stack(cost);

        return root;
    }

    /// The other two: what the wave screen puts above and below its lane.
    ///
    /// THE LANE CARD IS EMPTY HERE AND THAT IS THE HONEST PICTURE. `LaneStage`
    /// (Task 17, `Broodline.Game`) is what renders into it, and nothing in a
    /// headless `-executeMethod` drives a camera - so what this frame checks
    /// is everything the card owns on its own: the 4:3 height, the radius,
    /// the fill it shows before the first frame arrives, and the pocket tags.
    static VisualElement Lane()
    {
        var root = CatalogueRoot();

        void Stack(VisualElement child)
        {
            child.style.flexShrink = 0;
            root.Add(child);
        }

        var title = new Label("Lane");
        title.AddToClassList("t-screen-title");
        Stack(title);

        Stack(ComponentCaption("LanePreviewCard  -  4:3, pockets named along the bottom edge"));
        var lane = new LanePreviewCard();
        lane.SetSlots(new[] { ("A", true), ("B", false), ("C", false), ("D", true) });
        Stack(lane);

        Stack(ComponentCaption("FieldSlotRow  -  filled, empty, selected"));

        // IN A CARD, BECAUSE THAT IS WHERE THEY LIVE AND BECAUSE OF WHAT THE
        // FIRST CAPTURE SHOWED. The handoff puts this list inside its "On the
        // field" card, and a row's --surface-sunk fill is 4 channel steps from
        // --paper - so on bare paper these rows read as four labels floating
        // in space, and the one thing the fixture is for (is a sunk row
        // visible? is the selected ring stronger than the fill?) cannot be
        // answered. On the white surface they belong on, both are.
        var field = new SectionCard("On the field");

        // TWO TO A ROW, which is the handoff's own `grid-template-columns:
        // 1fr 1fr` for this list. A row of these is the caller's to arrange -
        // StatCell.uss's closing note: a component's stylesheet reaches its
        // descendants and never its parent - so the direction is set here.
        field.Body.Add(FieldPair(
            new FieldSlotRow("A", "Vetch Wall R2", filled: true, onTap: () => { }),
            new FieldSlotRow("B", "Empty", filled: false, onTap: () => { })));

        var selected = new FieldSlotRow("C", "Empty", filled: false, onTap: () => { });
        selected.Selected = true;
        field.Body.Add(FieldPair(selected,
            new FieldSlotRow("D", "Cinderplate R1", filled: true, onTap: () => { })));
        Stack(field);

        return root;
    }

    /// The tenth part of the vocabulary and the only one Task 13 did not
    /// build - Phase 9 Task 14c's `HeroBand`, in the three forms the
    /// handoff's six instances come in.
    ///
    /// A FIXTURE OF ITS OWN, BECAUSE `Vocabulary` HAS NO ROOM AND THE FRAME
    /// DOES NOT SAY SO. That fixture's own note records what a flex column
    /// does with an overflow - it SHRINKS every child that will shrink,
    /// silently, which once took a 6px ProgressBar track to zero height and
    /// out of the picture altogether. One band is 300px on its own; three
    /// would take every pixel `Vocabulary` has and then some.
    ///
    /// AND A FIXTURE IS THE POINT RATHER THAN THE PAPERWORK. `FounderNaming
    /// View` instances exactly ONE of the three forms - filled, with a ring -
    /// so on that capture alone the ringless band and the fixed band are two
    /// code paths nobody has looked at. That is precisely how Pale's hero
    /// disc shipped invisible through two components and two fix rounds: a
    /// defect this project had already found and already fixed sat unrendered
    /// in a second place for as long as no fixture instanced it.
    ///
    /// THE MIDDLE BAND IS DELIBERATELY EMPTY. `Gene Ark` and `Gene Lab` draw
    /// the ringless band around a scene, and what this frame is for is
    /// everything the band owns on its own - the ramp, the radius-26 corner
    /// and the elevation - with nothing in front of them. An empty band is
    /// also the state `AHeroBandWithNoSubjectDoesNotCrash` asserts, rendered.
    static VisualElement Band()
    {
        var root = CatalogueRoot();

        void Stack(VisualElement child)
        {
            child.style.flexShrink = 0;
            root.Add(child);
        }

        var title = new Label("Band");
        title.AddToClassList("t-screen-title");
        Stack(title);

        Stack(ComponentCaption("HeroBand  -  fixed, with the ring and a founder in it"));

        // FIXED RATHER THAN FILLED, AT THE HANDOFF'S OWN 300. `Fill` needs a
        // column with slack to take and this catalogue's root is a plain
        // stack, so a filled band here would size to its floor and show
        // nothing a fixed one does not - and the floor is the number worth
        // looking at. It is also the smallest band that does NOT clip the
        // 264px ring, which is what makes this the frame where the ring can
        // be counted; the two below it are deliberately shorter than that.
        var withRing = new HeroBand();
        withRing.Fix(300f);
        var founder = new HeroSlot();
        founder.Bind(Creature("Vetch", 4, name: "Ash", founder: true,
            trait1: "Carapace", tier1: 1, trait2: "Taunt", tier2: 1));
        withRing.Subject.Add(founder);
        Stack(withRing);

        Stack(ComponentCaption("HeroBand(ring: false)  -  the ramp, the corner and the elevation alone"));
        var ringless = new HeroBand(ring: false);
        ringless.Fix(120f);
        Stack(ringless);

        // THE TINT HOOK, IN THE ONE PLACE THE TWO RAMPS CAN BE COMPARED.
        // Phase 9 Task 18 gave `HeroBand` a second ramp for `Wave Defeat
        // .dc.html:31`, and a tint that renders is the only evidence the swap
        // worked - a modifier class that matched no rule would draw the violet
        // band and nothing would say so. Directly under the ringless violet
        // one above, at the same 120, so the two are the same picture in two
        // colours and the difference is the whole of what is being shown.
        //
        // AND BEFORE THE 200px BAND RATHER THAN AFTER IT, WHICH IS FIX ROUND
        // 1's OWN MINOR. Appended last it started at y=908 in a 932 frame and
        // 24 of its 120px fitted - so the frame whose stated purpose is that
        // the swap is visible showed the ramp's pale end and none of its deep
        // one. This catalogue has no scroll; anything past ~900 is not in the
        // picture, and the picture is the point.
        Stack(ComponentCaption("HeroBand(ring: false, tint: Coral)  -  Wave Defeat's own ramp"));
        var coral = new HeroBand(ring: false, tint: HeroBand.Tint.Coral);
        coral.Fix(120f);
        Stack(coral);

        // THE RING WITH NOTHING INSIDE IT, which is what three of the six
        // handoff screens would show before their content arrives.
        //
        // AND IT IS NOW THE CROPPED ONE. Moving the coral band above it - fix
        // round 1's own minor - did not remove the crop, it MOVED it: this
        // band starts near y=830 and about half of its 200px fits. The claim
        // this comment used to carry, that it is the only frame where the
        // pool's fill reads against both ends of the ramp at once, is no
        // longer true of the picture. The arithmetic it was the picture of
        // (--violet-pressed 64 from the white the band starts at, 26 from the
        // --violet-tint it ends on) is in HeroBand.uss's note, which is where
        // it is checkable; this frame shows the top half.
        //
        // The trade was deliberate - a tint nobody can see is worth less than
        // a pool nobody can see twice - but the catalogue has outgrown one
        // 932px frame and the next component added here will crop something
        // else. A second fixture is the fix, as the Vocabulary/Lane split
        // already was in Task 13.
        Stack(ComponentCaption("HeroBand  -  the ring and the pool, with no subject"));
        var empty = new HeroBand();
        empty.Fix(200f);
        Stack(empty);

        return root;
    }

    /// Two field rows side by side, each taking half the width whatever its
    /// text says - `flex-basis: 0` rather than `auto`, so "Empty" and
    /// "Cinderplate R1" produce two equal columns instead of one narrow one.
    static VisualElement FieldPair(VisualElement left, VisualElement right)
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;

        left.style.flexGrow = 1;
        left.style.flexBasis = 0;
        left.style.marginRight = 7;
        row.Add(left);

        right.style.flexGrow = 1;
        right.style.flexBasis = 0;
        row.Add(right);

        return row;
    }

    /// The frame both new catalogues share.
    ///
    /// THE GUTTER IS THE REAL ONE, 12px, not the 32 the other catalogues use.
    /// Half of these parts are full-width - the lane card, the cost row, the
    /// field rows - and a component measured at the handoff's own 366px
    /// content width is the only one whose proportions can be checked against
    /// the handoff's own captures.
    static VisualElement CatalogueRoot()
    {
        var root = new VisualElement();
        root.AddToClassList("shell-root");
        root.style.flexGrow = 1;
        root.style.paddingLeft = 12;
        root.style.paddingRight = 12;
        root.style.paddingTop = 20;
        return root;
    }

    /// One bar with its fill stated beside it. The number is in the caption
    /// because a bar cannot say what it is showing, and "does 0 look
    /// deliberate" is unanswerable without knowing that the bar above it is
    /// at 1.
    static VisualElement BarRow(string caption, float fill, string modifier)
    {
        var row = new VisualElement();
        row.style.flexShrink = 0;
        row.style.marginBottom = 8;

        var label = new Label(caption);
        label.AddToClassList("t-secondary");
        label.style.flexShrink = 0;
        row.Add(label);

        var bar = new ProgressBar(fill, modifier);
        bar.style.flexShrink = 0;
        bar.style.marginTop = 4;
        row.Add(bar);

        return row;
    }

    /// Fixture chrome, not component chrome - it names the component below
    /// it. Nothing in a real screen looks like this.
    static Label ComponentCaption(string text)
    {
        var label = new Label(text);
        label.AddToClassList("t-micro");
        label.style.paddingTop = 10;
        label.style.paddingBottom = 4;
        return label;
    }
}
