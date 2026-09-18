using System;
using System.Collections.Generic;
using Broodline.Api;
using Broodline.Model;
using Broodline.UI;
using Broodline.UI.Screens;
using UnityEngine;
using UnityEngine.UIElements;

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
        "RegionView",
        "RosterView",
        "SpliceChamberView",
        "SpliceRevealView",
        "WaveDefeatView",
        "WaveHudView",
        // NOT A SCREEN, and last for that reason - see Primitives() below.
        "Primitives",
    };

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
            case "RegionView": return Region();
            case "RosterView": return Roster();
            case "SpliceChamberView": return SpliceChamber();
            case "SpliceRevealView": return SpliceReveal();
            case "WaveDefeatView": return WaveDefeat();
            case "WaveHudView": return WaveHud();
            case "Primitives": return Primitives();
            default: throw new ArgumentException("ScreenFixtures has no fixture named '" + name + "'", nameof(name));
        }
    }

    // ---------------------------------------------------------------
    // Shared builders - same shape as ScreenBindingTests.cs /
    // FirstHourScreensTests.cs / WaveScreensTests.cs in Broodline.UI.Tests.
    // ---------------------------------------------------------------

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
            new TraitSummary { Id = "Taunt", Species = "Vetch Crawler", Counters = "Lash" },
            // bible 1.2: four traits counter nothing by design - Carapace is
            // one, and a null Counters is the fixture for that real state.
            new TraitSummary { Id = "Carapace", Species = "Vetch Crawler", Counters = null },
        }, onDismiss: () => { });
        return view;
    }

    static VisualElement Deploy()
    {
        var roster = new RosterScreen();
        var response = new RosterResponse { Cap = 20 };
        var selected = new List<Guid>();
        for (var i = 0; i < 3; i++)
        {
            var creature = Creature("Vetch Crawler", 1, name: null, founder: false);
            response.Creatures.Add(creature);
            selected.Add(creature.CreatureId);
        }
        roster.ApplyRoster(response);

        var model = DeployScreen.Build(waveId: 6, roster: roster, selected: selected);
        var view = new DeployView();
        view.Bind(model, onStart: () => { });
        return view;
    }

    static VisualElement FounderNaming()
    {
        var founder = Creature("Vetch Crawler", 1, name: null, founder: true, id: FounderId);
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
                Node("Vetch Crawler", FounderId, 1, founder: true, name: "Ash"),
                Node("Ember Skitter", ParentAId, 1, founder: false, consumedAt: "2026-09-16T00:00:00Z"),
                Node("Pale", ParentBId, 1, founder: false, consumedAt: "2026-09-16T00:00:00Z"),
                Node("Vetch Crawler", ChildId, 2, founder: false, mutated: true,
                    parentA: ParentAId, parentB: ParentBId),
            },
        }, highlight: FounderId, next: () => { });
        return view;
    }

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
        view.Bind(response, granted, next: () => { });
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
                Creature("Vetch Crawler", 4, name: "Ash", founder: true),
                Creature("Ember Skitter", 6, name: null, founder: false),
            },
        });

        var view = new RosterView();
        view.Bind(roster, onSelect: _ => { });
        return view;
    }

    static VisualElement SpliceChamber()
    {
        var a = Creature("Vetch Crawler", 4, name: "Ash", founder: true, id: ParentAId);
        var b = Creature("Ember Skitter", 6, name: null, founder: false, id: ParentBId);
        var model = SpliceScreen.Build(a, b, Preview());

        var view = new SpliceChamberView();
        view.Bind(model, lockedOut: new HashSet<Guid>(), onSplice: () => { });
        return view;
    }

    static VisualElement SpliceReveal()
    {
        var a = Creature("Vetch Crawler", 4, name: "Ash", founder: true, id: ParentAId);
        var b = Creature("Ember Skitter", 6, name: null, founder: false, id: ParentBId);
        var child = Creature("Vetch Crawler", 5, name: null, founder: false, id: ChildId);
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
        view.Bind(report, granted, retry: () => { });
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
        view.Bind(() => snapshot);
        return view;
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
}
