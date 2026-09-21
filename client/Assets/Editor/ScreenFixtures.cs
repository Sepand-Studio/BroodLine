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
    ///                 method sets it Flex inline. It carries no padding, so
    ///                 the sheet renders the same either way and this harness
    ///                 does not reproduce the overlay layer.
    ///   WaveHudView - never reaches ScreenHost at all. `WaveRunner` adds it
    ///                 straight to the wave scene's own panel root.
    ///   the four catalogues - Primitives, Icons, Scaffold and Components are
    ///                 not screens and have no place in the shell.
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

    static VisualElement Deploy()
    {
        var roster = new RosterScreen();
        var response = new RosterResponse { Cap = 20 };
        var selected = new List<Guid>();
        for (var i = 0; i < 3; i++)
        {
            var creature = Creature("Vetch", 1, name: null, founder: false);
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
                // ONE OF EACH OF BIBLE 1.2's SIX, and the roster is the right
                // screen to carry them: it is the only one whose job is
                // several creatures at once, so it is where the species
                // palette is actually comparable. Two Vetch-shaped cards told
                // a reviewer nothing about five sixths of the palette, and
                // Phase 8 Task 13 needed a frame in which a Skitter's amber
                // body sits beside the Founder's amber border and a Hollow's
                // violet body sits above the violet CTA. It does; see
                // implementation/results/species-collision.md.
                Creature("Vetch", 4, name: "Ash", founder: true),
                Creature("Skitter", 6, name: null, founder: false),
                Creature("Hollow", 5, name: null, founder: false),
                Creature("Ember", 3, name: null, founder: false),
                Creature("Loam", 2, name: null, founder: false),
                Creature("Pale", 1, name: null, founder: false),
            },
        });

        var view = new RosterView();
        view.Bind(roster, onSelect: _ => { });
        return view;
    }

    static VisualElement SpliceChamber()
    {
        var a = Creature("Vetch", 4, name: "Ash", founder: true, id: ParentAId);
        var b = Creature("Skitter", 6, name: null, founder: false, id: ParentBId);
        var model = SpliceScreen.Build(a, b, Preview());

        var view = new SpliceChamberView();
        view.Bind(model, lockedOut: new HashSet<Guid>(), onSplice: () => { });
        return view;
    }

    static VisualElement SpliceReveal()
    {
        var a = Creature("Vetch", 4, name: "Ash", founder: true, id: ParentAId);
        var b = Creature("Skitter", 6, name: null, founder: false, id: ParentBId);
        var child = Creature("Vetch", 5, name: null, founder: false, id: ChildId);
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
        var pushed = new ScreenScaffold("Splice Reveal", pushed: true, onBack: () => { });
        FillScaffold(pushed);
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
        statRow.Add(new StatCell("Control", "62%"));
        statRow.Add(new StatCell("Travel", "4h 20m"));
        statRow.Add(new StatCell("Arks", "2"));
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
