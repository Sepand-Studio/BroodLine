---
status: current
folder: 03-technical
note: >
  Phase 10 design and plan. The full-app redesign toward Kingshot's structure
  and polish while keeping Broodline's fiction: the whole-app skin pass first,
  then the five companions, the raiders, the battlefield, the world map, the
  store/lab/allies screens, live portraits, and device validation as the
  TestFlight gate. Records the four rulings the user took on 2026-09-23 and
  the two structural moves (a dependency-free Broodline.Frontier.Art assembly;
  retiring client/Assets/Creatures/). Supersedes nothing in
  broodline_visual_upgrade_plan.md; it is that plan's execution order.
---

# Broodline — Phase 10: the Living Frontier, whole app

## Context

The user approved "The Living Frontier" direction on 2026-09-23 after reviewing Kingshot, and a previous agent built a standalone Unity art proof (`client/Assets/Frontier/`) plus the pasted "remaining five companions" plan. Vetch and Cinderplate are the accepted quality bar (revision 02); Ember, Pale, Skitter, Hollow and Loam have first-pass builders that nobody has rendered in Unity yet. The production game is still at Phase 9 fidelity: old handoff palette, dead tab bar, no Store, Lab or Allies screens, a text-list Map, a flat lane with sphere trees, and enemies that are low-poly spheres. The user wants one plan that takes the *whole* app to a Kingshot-like standard: screens, home base, resource bar, enemies, battlefield, map, store.

**Decisions taken with the user (final):**
1. Structure and polish, keep the fiction. No medieval humans, castles or rarity frames. Ivory/violet Living Frontier palette stays. New: an **isometric Ark base is the home screen** under the Ark tab; tabs stay Map · Ark · Splice · Lab · Allies.
2. **Real where backend exists, stubbed where not.** Map/Region Detail use `GET /v1/region/state` and `POST /v1/node/claim`. Store, Lab, Allies are full-fidelity screens on local catalogs with clearly labelled "(preview)" stub actions. No IAP, no new endpoints.
3. **First review batch is a whole-app skin pass**, then deepen creatures → enemies → battlefield → map → store/lab.
4. **Art = procedural 3D (Frontier pipeline) + user-approved generated 2D** for backdrops, map terrain, chapter art, icons. Nine-slice panel/button sprites are script-generated from tokens.

**Hard constraints carried forward:** no `engine/` change; sim/replay/outcome hashes untouched; `Broodline.UI` references only Model/Net/Generated.Api; every USS colour lives in `Tokens.uss`; ≤10,000 triangles per assembled creature; Unity batch tasks serialize (single Editor lock on `client/`); the user reviews by seeing, not by test output.

## Where the pasted companions plan lands

The pasted plan is adopted as **Batch 2** below, unchanged in its three review batches (Ember/Pale → Skitter/Hollow → Loam), its Companion Studio and browser comparison tool, and its acceptance checks. It runs *after* the skin pass because the user chose to see the whole app change first, and it runs *inside* the assembly split from Batch 1 so its output feeds production directly instead of staying a proof.

## Architecture decisions

- **Assembly split first.** `Broodline.Frontier` today references Sim/View/Game, so production cannot reference it. Create `client/Assets/Frontier/Art/Broodline.Frontier.Art.asmdef` (no references) holding the art core: `FrontierArt`, `FrontierMesh`, `FrontierSurface.shader`, `FrontierRigDefinition`, `FrontierPose`, `FrontierCreature`, `FrontierFace`, `FrontierParts`, the six species builders. `Broodline.View` and `Broodline.Game` gain a reference to Art. The proof app (`FrontierProof`, `FrontierFormation`, `FrontierBattleFeedback`, cues, badges, icons, `FrontierProof.uss`) stays in `Broodline.Frontier` and references Art + Sim + View + Game. Graph: `Frontier.Art ← View ← Game ← Frontier(proof)`. UI never references Art.
- **Frontier pipeline replaces `client/Assets/Creatures/`** in three steps (Batch 2 portraits/lane stage, Batch 3 `WaveView` and raiders, Batch 7 card portraits). The approved plan's `CreatureVisualDefinition` becomes `FrontierVisualDefinition` (plain C#: id, kind, rig, builder, portrait framing, growth range, art revision); `FrontierLook` replaces `CreatureLook`.
- **Chrome = nine-slice sprites generated from tokens.** Extend `client/Assets/UI/Art/generate-textures.py` to read hex values from `Tokens.uss` and emit `panel-ivory`, `panel-deep` (HUD frame with brass edge), `btn-primary`/`-pressed`, `btn-secondary`, `btn-reward`, `tab-frame`. Ramp PNGs stay only for gradient fills. `verify-uss-tokens.sh` already ignores `url(...)`, so the gate keeps meaning because the script is the sole source of sprite colour.
- **Home base renders through a stage RenderTexture** (the `LaneStage`/`PortraitStudio` pattern in `Game/Shell/`), live camera only while the Ark tab is visible, UI Toolkit hotspots placed from normalized viewport points. Facility plots are shared with the Lab screen. The stage must hide while `WaveHost` hosts a wave so two cameras never render.
- **Presentation events without touching the engine.** `FrontierBattleFeedback.Observe` already derives Attack/Damage/Defeated/Chilled/Breach/Rally from the sim's read-only spans and has a hash-neutrality test. It moves into `Broodline.View` as the production cue source in Batch 4.
- **Stub policy.** Catalogs in `Model/Catalogs/`, stub state in one PlayerPrefs-backed `Model/Stub/StubLedger.cs` that logs every write as `[stub]`; every stub CTA is suffixed "(preview)" and answers with `NoticeToast`. No offers, no popups.
- **Token migration in two moves.** Re-point existing token *values* first so all ~36 stylesheets restyle in one edit, add semantic names alongside, migrate references screen by screen, delete old names at the end of Batch 1 (`verify-uss-tokens.sh` check 3 catches stragglers).

Task tags: `[U]` needs the Unity Editor or batch mode (serialize, close the Editor for scripts, always pass a real `-logFile`); `[S]` is shell-only and can run in parallel with other `[S]` tasks.

---

## Batch 1 — Whole-app skin pass (user reviews after 1.8)

### 1.0 `[U]` Records, assembly split, hygiene
- Write the phase record pair the repo uses: `specs/plans/broodline_phase10_living_frontier.md` (this plan, decisions, bible §10 amendment: faces allowed, new palette tokens, "gold for rewards" kept) and `implementation/2026-09-23-phase10-skin-pass.md` (running record). Amend `specs/broodline_bible.md` §10.1/§10.6 so `Tokens.uss` stops citing the old handoff as "authoritative".
- Create `Frontier/Art/Broodline.Frontier.Art.asmdef`; move the art-core files listed above into `Frontier/Art/`; add Art to `View/Broodline.View.asmdef`, `Game/Broodline.Game.asmdef`, `Frontier/Broodline.Frontier.asmdef`. Split `Frontier/Tests/FrontierProofTests.cs`: geometry/budget/socket tests → `Frontier/Art/Tests/` (new test asmdef); formation/hash tests stay.
- `View/RuntimeShaders.cs`: add `Frontier = "Broodline/FrontierSurface"` to `All`; run `Editor/Phase0Setup.Apply` so it lands on the Always Included list (`View/Tests/RuntimeShaderInclusionTests.cs` gates it).
- `.gitignore`: add `client/Assets/Frontier/Generated/` and `Generated.meta`; commit the eight untracked `Frontier/*.cs.meta` files.
- Gates: `implementation/scripts/run-unity-tests.sh EditMode` against `implementation/results/phase9-test-baseline.txt` (MainThreadAffinity stays the one known red); `Broodline → Frontier Proof → Open` still plays.

### 1.1 `[S]→[U]` Tokens, type scale, sprite kit, palette re-baseline
- `UI/Shell/Tokens.uss`: re-point `--paper→#F5EBD9`, `--surface→#FFF8EB`, `--ink→#302D40`, `--ink-deep→#263345`, `--violet→#6B4EC2`, `--violet-shadow→#49348D`; add semantic tokens `--surface-paper/-raised/-deep`, `--text-primary/-secondary`, `--action/-shadow/-text`, `--selection`, `--reward #E9B44C`, `--success #286846`, `--danger #B53E3E`, `--brass`, `--hud-frame/-edge`, `--species-{vetch,ember,skitter,hollow,loam,pale}` (same values as today's species swatches). Type scale: hero 30, screen title 24, section 18, card title 16, body 14, secondary 12, micro 11 (eyebrow only), CTA 17, tab 12; retire `--text-nav`. Build `Nunito-Regular SDF` via `Editor/FontAssetBuilder.cs`.
- `UI/Art/generate-textures.py`: parse `Tokens.uss`, emit the nine-slice set, retint `shadow-card.png` warm, retire `cta-ramp.png`.
- `UI/Shell/Theme.uss`: `.card`/`.row` on `panel-ivory` with `-unity-slice-*`; `.btn-primary` on the sprite keeping the 2px press translate and `:active` swap; `.elev-1/.elev-2` mechanism unchanged; `.t-*` on the new scale.
- PaletteContrast re-baseline: run `UI/Tests/PaletteContrastTests`, review each regressed pair, then `Editor/PaletteBaselineWriter.Write` and commit `implementation/results/palette-cvd-baseline.txt` in the same commit as `Tokens.uss`. Never hand-edit the baseline.
- Gates: `verify-uss-tokens.sh`, `check-stylesheets.sh`, PaletteContrast + Typography tests, `capture-screens.sh`. Review: `implementation/results/screens/{Primitives,Vocabulary,Components}.png`.

### 1.2 `[S]→[U]` Shared components and shell chrome
- New `UI/Components/ResourceBar.cs` + `Resources/ResourceBar.{uxml,uss}` replacing the unused `CurrencyHeader.cs`: Splice Charges with regen `TimerChip`, Gene Shards with "+" raising `OnOpenStore`, Geneticist XP/Tier, Marks (no "+", never purchasable).
- `UI/Shell/TabBar.cs`/`Shell.uss`: dark `tab-frame` nine-slice, lifted active tab, 12px labels. New generated nav icons `UI/Art/icons/nav-{map,ark,splice,lab,allies}.png` mapped in `icons.uss` under the existing `.icon--*` names so `TabBar` is untouched; add `xp`, `mark` and twelve trait glyphs under `UI/Art/icons/traits/` (user approves the generated sheet first).
- Restyle `ScreenScaffold` (keep `SetResourcePill` for `ScaffoldTests`), `SectionCard`, `OptionRow`, `TraitChip` (glyph + label, raises `OnTap`), `GenChip`, `ProgressBar`, `TimerChip`, `ConfirmDialog`, `NoticeToast`, `EmptyState`, `CostCtaRow`, `HeroBand` (add a deep-backdrop tint), `CreatureCard` (larger portrait slot; sprites unchanged this batch).
- `Shell.uss`: `.shell-root` on `--surface-paper`; new `.screen-host--stage` modifier makes `#screen-host` transparent for the home base. `Shell.uxml` unchanged (sheet-layer order is load-bearing).
- Gates: token gate, `ComponentTests`, `ScaffoldTests`, `TabBarTests`, `capture-screens.sh`, `probe-shell.sh`.

### 1.3 `[U]` Shell wiring: resource bar, tab router, Codex reach
- New `Game/Shell/HubRouter.cs`: `Show(tab)` with a real destination per `Model/Progression.Order` entry — Map → `RegionView` bound from the generated client's `RegionStateAsync()`; Ark → `HomeBaseView` (1.4); Splice → `RosterView` in pick-parents mode → guided splice extracted from `FtueDirector` into `Game/Ftue/SpliceFlow.cs` (the FTUE beat calls the same class); Lab → `LabView`; Allies → `AlliesView` (1.5). Uses `ScreenHost.Show/Push/Pop/ShowSheet` and `ScreenFlow`.
- `Game/Shell/BootController.cs`: `OnTabSelected` → router; `OnSnapshot` also binds the `ResourceBar` in `#top-bar` from `PlayerSnapshot`. `FtueDirector` exposes `Busy`; while a beat before `Beat.Done` owns the host, a tab tap toasts the staged-disclosure line instead of routing. `Beat.Done` lands on the Ark tab; `CampaignSelectView` becomes the home base's "Defend" entry.
- Codex reachable: `TraitChip.OnTap` → `ScreenHost.ShowSheet(CodexSheet)`; home base carries a Codex button.
- New `Game/Tests/HubRouterTests.cs`. Gates: tests, `probe-shell.sh`. Review: Play in `Scenes/Boot.unity`, tap all five tabs.

### 1.4 `[U]` Home = isometric Ark base
- New `Frontier/Art/FrontierBase.cs` (`FrontierArt.Base(parent)`): isometric platform, the existing Ark builder, six plot markers `plot_{splicing,hatchery,vault,harvest,drive,core}` (bible §7.2), foliage, backdrop quad for `Game/Art/home-backdrop.png` (generated, user-approved). Budget ≤30k triangles for the whole base.
- New `Game/Shell/HomeStage.cs` modeled on `LaneStage.Create/Show/Clear`: 720×1280 RT, `PlotAnchors()` → normalized viewport points; `Hide()` called from `WaveHost` when the shell hides.
- New `UI/Screens/HomeBaseView.cs` + resources, `UI/HomeScreen.cs`: full-bleed stage (reuse `CreatureStage.SetTexture`), hotspot buttons placed by percent, Ark name/tier card, CTAs Defend / Roster / Codex / Store.
- Add a `HomeBaseView` fixture to `Editor/ScreenFixtures.cs` (static placeholder texture). New `Game/Tests/HomeStageTests.cs`. Gates: tests, `capture-screens.sh`, `probe-frame.sh`.

### 1.5 `[S]→[U]` Map / Store / Lab / Allies at skin fidelity
- `Model/Catalogs/StoreCatalog.cs` (daily gift, five packs from the economy ladder, Custom Chest 3-of-6 at three prices, Season Pass, Double Regen — bible §8.3), `FacilityCatalog.cs` (six facilities, tiers, Core-cap rule), `RegionCatalog.cs` (30 regions, 3 rings, 43 edges, 8 gates from `specs/broodline_region_graph.md`), `Model/Stub/StubLedger.cs`, with Model tests asserting counts against the specs.
- New `UI/Screens/{WorldMapView,StoreView,LabView,AlliesView}.cs` + resources and `UI/{Map,Store,Lab,Allies}Screen.cs` text. Batch-1 fidelity: WorldMap = ring-grouped region cards with the live region real; Store = tabs with pack cards "Buy (preview)"; Lab = facility list with tier/cost/timer and "Upgrade (preview)"; Allies = pennant + `EmptyState` + "Create alliance (preview)".
- Add four fixtures to `ScreenFixtures.Names`. Gates: token gate, tests, `capture-screens.sh`.

### 1.6 `[S]→[U]` Restyle all 14 existing screens
- Every `UI/Screens/Resources/*.{uss,uxml}`: FounderNaming and SpliceReveal → hero layout (creature ~52% of height on a deep backdrop, per the approved mockups); Roster → two-column card grid; CampaignSelect → illustrated chapter header (`UI/Art/chapters/chapter-1.png`); Deploy/LanePreview framed; WaveHud → `panel-deep` frame with brass; PostWave/WaveDefeat outcome headline with reward gold; Lineage nodes; Codex/Interrupted/AbandonedWave sheets. Then migrate all `var(--old)` references to semantic names and delete the old token names.
- Gates: all four stylesheet gates + full `capture-screens.sh`; diff against the pre-batch corpus. Review: the full fixture corpus.

### 1.7 `[U]` Functional gaps folded in
- Pause/speed: `View/WaveClock.cs` gains `Paused`/`Scale`; `WaveHudView` raises `OnPause/OnSpeed`; `Game/WaveRunner.cs` wires them. HUD energy pill shows balance (reward stays on PostWave). Deploy and HUD read one campaign index ("Wave N of M"). `FtueDirector` clears the portrait studio only after the next screen has presented. Campaign end state: a "Frontier held" row when the last bundled wave is cleared.
- Gates: `WaveScreensTests`, `FtueDirectorTests`, `ScreenBindingTests`, `check-silent-drops.sh`.

### 1.8 `[U]` Batch-1 review package
- Full gate run, `capture-screens.sh`, simulator build via `Editor/SimulatorBuilder.cs`, record in the implementation doc. User reviews: corpus PNGs, a Play walk of the five tabs and home base, the phone build. Direction is accepted or corrected here before any deepening.

---

## Batch 2 — Companions (the pasted plan) + production adoption
- **2.1 `[S]`** `Frontier/Art/FrontierVisualDefinition.cs` + `FrontierVisuals.For(id)`; `FrontierLook`; `FrontierArt.Creature` resolves through it.
- **2.2 Ember + Pale → 2.3 Skitter + Hollow → 2.4 Loam** `[S]→[U]`: refine each `Frontier/Art/Frontier<Species>.cs` to the Vetch standard per the pasted character direction (silhouette, face, materials, species motion in `FrontierPose`). Before each Unity pass run `python3 implementation/scripts/preview-frontier-companions.py` (browser page: batches, 40px silhouettes, 36-combination attachment sheet, 10k limit); export production baselines for Skitter/Hollow/Loam from Unity into `implementation/results/frontier/production-baselines/` so "before" is real. Companion Studio in `FrontierProof` shows the pair; user captures via `Broodline → Frontier Proof → Capture Game View` at 430×932, 390×844, 360×640.
- Fix the two stale tests in `Frontier/Art/Tests`: budget loop over `FrontierRigDefinition.Companions` plus raider ids; replace `weight0 == 1` with weights summing to ~1 and indices in range (Pale's blended membrane).
- **2.5 `[U]` Production adoption**: `Game/Shell/PortraitStudio.cs` and `LaneStage.cs` build with `FrontierArt` (`Show(species,t1,t2,growth01)` unchanged; `LaneStage.Show` takes `FrontierLook`); `FtueDirector` uses `FrontierLook`. New `Editor/FrontierBaker.cs` writes the same `UI/Resources/Art/creatures/{bodies,parts}` paths with a deterministic pose so `CreatureCard`, `CreatureSprites` and `SilhouetteTests` keep working; `generate-creatures.sh` → `bake-frontier.sh`. Gates: `SilhouetteTests`, `LaneStageTests`, `CreatureColourTests`, capture.
- Acceptance per the pasted plan: finite geometry, outward winding, valid bind poses; ≤10k triangles with two traits; all five traits in both slots and all ordered pairs at min/max growth; Unity checks for startup, blinking, transitions, pause continuity, reduced motion, species switching, asset cleanup; readable faces at portrait size and distinct 40px silhouettes.

## Batch 3 — Enemies
- **3.1 `[S]`** Raider language in `Frontier/Art/FrontierPalette.cs` (dark `#414556` family); `Kit` socket (`sk_kit`) added to `FrontierRigDefinition`; rule: angular, dark, visibly unfinished structure, never six-limbed insectoid (bible §10.5/§10.7), silhouette distinct from all six companions. Confirm sim raider type ids in `engine/` before naming builders (read only).
- **3.2 `[S]→[U]`** `Frontier/Art/FrontierRaiders.cs`: Runner body (Skirmisher 0.6×, Courser 1.2× charge) and Lifter body (Lash furled / Drift spread) — the three implemented raiders first. **3.3** Hauler (Breaker fused plate, Bulwark shield on `sk_kit`), Segment (Delver, Brood at reduced scales), Sunder = Hauler 1.5× with both kits. Browser page gains a raider sheet; `SilhouetteTests` extended to companions-vs-raiders.
- **3.4 `[U]`** `View/WaveView.cs` builds every entity through `FrontierArt` and drives `FrontierCreature` from `WavePair`. Retire `client/Assets/Creatures/` entirely (recipes, SDF mesher, assembler, library, motion, shader, generated prefabs, its tests); drop `Broodline.Creatures` from View and Game asmdefs. Gates: `WaveViewTests`, `RuntimeShaderInclusionTests`, full EditMode. Review: proof Battle page + a `Wave.unity` capture.

## Batch 4 — Battlefield
- **4.1 `[U]`** `Editor/WaveSceneBuilder.cs`: perspective camera; lane stays straight in world and is composed diagonally by camera yaw; `WaveView` fits the camera to lane+pocket bounds minus HUD-safe insets.
- **4.2 `[S]→[U]`** `Frontier/Art/FrontierTerrain.cs` (cliff modules, trees, shrubs, path edging, platform stones, backdrop quad `Game/Art/battle-backdrop.png`); one kit for `LaneStage` preview and `WaveView`; delete `View/LaneDressing.cs`.
- **4.3 `[U]`** Move `FrontierBattleFeedback` → `View/PresentationCues.cs`; `WaveView.Render` observes after every sim step including catch-up; drives `FrontierCreature.Attack/Hit`, pooled VFX in `View/BattleVfx.cs`, dedupe by tick/entity, reset on restart; port `FrontierFloatingCues` into `UI/Components/FloatingCue.cs` driven by `WaveHudView`. Move the hash-neutrality test to `View/Tests` and assert equal hashes with cues on and off.
- **4.4 `[S]→[U]`** HUD dark frame, Rally readiness, reduced-motion setting. **4.5 `[U]`** Reproduce the Phase 9 "lane is empty" concern in deploy preview and live wave on one build; record the outcome either way.
- Gates: `WaveClockTests`, `WaveViewTests`, `WaveSnapshotTests`, determinism harness, capture, `smoke-wave.sh`.

## Batch 5 — World Map, Region Detail, Relocate Ark
- **5.1 `[S]`** `Model/Map/RegionGraph.cs` (nodes, edges, gates, hop minutes, shortest path) + tests vs spec.
- **5.2 `[S]→[U]`** `Game/Art/map/terrain.png` (generated, user-approved), `WorldMapView` pannable via a `PointerManipulator` with zoom buttons, markers with a state glyph + text (rich/common/drained/contested/Apex), Ark marker, Collector routes from the catalog; live region from `RegionStateAsync()`.
- **5.3** Region Detail = `RegionView` restyled with richness, controller, lanes/raider pool, neighbours, travel, alerts; claim via `OutboxClient.ClaimNodeAsync(slot)`. **5.4** Relocate Ark: graph path + local timer in `StubLedger`, labelled "(preview)". Review at 430×932 and 390×844.

## Batch 6 — Store, Lab, Allies deepened
- Store: Packs tab opens on the daily gift (once/day local), pack cards, Custom Chest picker with live value strip, Season Pass, Double Regen; stub purchase only, no offers surface. Lab: reuse `HomeStage` plots, facility sheet with tier/cost/timer and Core-cap enforcement, stub upgrade with a real countdown persisted in `StubLedger`. Allies: pennant art, empty state, stub create. Gates: Model tests, capture.

## Batch 7 — Live portraits with a bounded cache
- New `Game/Shell/PortraitCache.cs`: one offscreen camera, 256px, 64-entry LRU keyed `species|t1|t2|growthStage|artRev`, deterministic pose. `CreatureCard` gains a static `PortraitSource` hook set by `BootController` (UI stays free of Art); silhouette fallback while queued. Delete `UI/Resources/Art/creatures` and `CreatureSprites.cs`; keep only the 40px silhouette bake. Tests: eviction, invalidation on art revision, measured memory recorded.

## Batch 8 — Device validation and TestFlight gate
- `Benchmark/` harness on Frontier bodies (104 entities, HUD, terrain, VFX); A14 targets 60 fps / 600 MB / 30 fps degradation path; captures at 402×874, 430×932, 360×640; +40% text expansion; 44pt hit targets; PlayMode through the Editor Test Runner only. Record `implementation/…-device-validation.md`. TestFlight only after physical-device evidence.

---

## Risks
- **Assembly boundary**: add a `Game/Tests` assertion that `Broodline.UI.asmdef` references stay `{Model, Net, Generated.Api}`; textures and normalized anchors cross into UI, code does not.
- **Frontier circularity**: solved by 1.0; Game must never reference `Broodline.Frontier` (the proof).
- **Shader Always-Included**: `FrontierSurface` must be registered before any production `Shader.Find`, or the simulator build crashes silently.
- **Wave additive load**: `HomeStage` must hide when `WaveHost` hides the shell.
- **Budgets**: 10k triangles per creature is a ceiling, not an allowance; Brood ×13 and the 30k base scene are measured in the benchmark.
- **Palette baseline** only via `PaletteBaselineWriter`; **sprite colour drift** only defended by regenerating textures on every token change.
- **Unity lock**: `[U]` tasks serialize; check for orphaned Unity processes before diagnosing a hang.
- **Generated 2D art** needs the user's approval before import; label anything I produce as a stand-in.

## Verification (every batch)
1. `implementation/scripts/verify-uss-tokens.sh`, `check-stylesheets.sh`, `check-silent-drops.sh`.
2. `run-unity-tests.sh EditMode` vs the phase-9 baseline (one known red, by identity).
3. `capture-screens.sh` → `implementation/results/screens/`, diffed against the previous corpus; `probe-frame.sh`, `probe-shell.sh`.
4. Batches 2–4: `preview-frontier-companions.py` browser page, then `Frontier Proof → Capture Game View` at the three sizes; `check-frontier-combat.py`.
5. Batch 8: physical iPhone run and benchmark record.
6. A user eyes-on review closes each batch; nothing is called accepted from a passing test.
