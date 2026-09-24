# Phase 10 handoff — Living Frontier visual redesign

Updated 2026-09-24. This is the working handoff for continuing on another machine. Read [the Phase 10 plan](../specs/plans/broodline_phase10_living_frontier.md), [the visual upgrade plan](../specs/plans/broodline_visual_upgrade_plan.md), and [the game bible](../specs/broodline_bible.md) for intent. The plan is an execution map, **not a claim that every numbered item is finished**. Use the implementation records linked below and current source as the record of what shipped.

## Start here on the Unity machine

1. Work on `phase_10`: `git checkout phase_10 && git pull --ff-only origin phase_10`. Run `git lfs install && git lfs pull`; the PNGs are LFS assets. Check `git status -sb` before editing. Do not overwrite local changes from another machine.
2. Open **`client/` as the Unity project**, with **Unity 6000.6.0f1** (`client/ProjectSettings/ProjectVersion.txt`). The game entry scene is `client/Assets/Scenes/Boot.unity`. The standalone art proof is built from Unity's **Broodline → Frontier Proof → Open** menu, or `implementation/scripts/build-frontier-proof.sh` with the Editor closed. Its scene is generated under `client/Assets/Frontier/Generated/` and is intentionally ignored by Git.
3. Let imports and compilation finish before deciding anything is broken. If batch mode reports compiler errors, inspect the first `error CS...` lines in its log; an unrelated Unity AI Toolkit cloud-account timeout is only a warning. Use the exact executable through `UNITY_EDITOR` for scripts that support it. Several older scripts hard-code the standard macOS Unity Hub path; adjust locally if yours differs.
4. The user's current direction is **finish UI and visual design first, then do one final Unity/device validation pass**. Source checks and browser previews are evidence of source/geometry, not proof of Unity rendering or acceptance. The user has accepted the stronger companion face direction, but has not approved every final screen on device.

## Product and code boundaries

- Make Broodline feel as polished as Kingshot in hierarchy, staging, feedback and presentation while keeping Broodline's own fiction. The visual language is warm ivory, violet/slate, brass and species colours; no medieval humans, castles, rarity frames or invented commerce. `client/Assets/UI/Shell/Tokens.uss` owns USS colours. Generate art and icons from their source scripts when changing tokens.
- Do not change `engine/`, simulation/replay/outcome hashes, save or server contracts to accomplish visual work. `Broodline.Frontier.Art` is dependency-free and holds procedural art; `View` and `Game` consume it; `Broodline.Frontier` is the proof. `Broodline.UI` references only Model, Net and Generated.Api, never Art/Game/View. Keep the shared trait geometry and both combat sockets, with slot 1 dorsal and slot 2 flank.
- Real server behavior is limited to the APIs that exist. The current region's state and claim nodes use `RegionStateAsync()`/`ClaimNodeAsync()`. Store, Lab, Allies and relocation are local previews using `Model/Stub/StubLedger.cs`, visibly labelled `(preview)`, with `[stub]` logging. No IAP or new offers. Do not portray a locally completed relocation as a server move.
- The region graph in `specs/broodline_region_graph.md` and `client/Assets/Model/Catalogs/RegionCatalog.cs` has 30 nodes, 43 borders and 8 gates. It is authored topology, not live world state. The current server emits the deliberately unauthored placeholder id `verdant-shelf`; live harvest detail now names that neutral server region without borrowing Holdfast geography. The authored map uses Holdfast only as a visibly labelled preview origin (`P`) until the API returns a catalog slug. Do not present remote controller, richness, Collector routes or alerts as live data.

## What has been built

| Area | Source state and useful entry points | Evidence / limit |
| --- | --- | --- |
| Whole-app skin and shell | Token palette, type scale, nine-slice chrome, `ResourceBar`, dark tab bar, five destinations, FTUE handoff, Ark home stage, pause/speed, 26-screen corpus. `client/Assets/Game/Shell/HubRouter.cs`, `HomeStage.cs`, `client/Assets/UI/Shell/`, `client/Assets/UI/Components/`. | [Batch 1 record](2026-09-23-phase10-skin-pass.md) contains an earlier Unity simulator walk and its fixes. It is not a current post-redesign pass. |
| Six companions and shared art | Vetch, Ember, Pale, Skitter, Hollow, Loam builders, explicit rigs/sockets, pose controls, face details and proof Studio in `client/Assets/Frontier/Art/`. Production `PortraitStudio`/`LaneStage` use Frontier art; `FrontierBaker` can write legacy card layers. | [Companion record](2026-09-23-phase10-batch2-companions.md), [production adoption](2026-09-23-phase10-production-companions.md), `implementation/scripts/preview-frontier-companions.py` → `implementation/results/frontier/companions.html`. The user's face/detail feedback was addressed in the latest builders; final Unity motion/socket review is due. Production baseline exports for Skitter/Hollow/Loam are absent in this checkout; do not label an invented before image as production. |
| Raiders | Nine art definitions across Runner/Lifter/Hauler/Segment/Sunder families. The live engine currently uses Skirmisher, Courser and Lash; the remaining definitions are art previews. `FrontierRaiders.cs`; the old `client/Assets/Creatures/` runtime has been retired. | [Raider record](2026-09-24-phase10-raider-art.md); `preview-frontier-raiders.py` creates `implementation/reviews/phase10-raider-cast.png`. Combat telegraphs and device readability still need Unity review. |
| Battlefield | Shared `FrontierTerrain` environment in deploy and live wave, painted backdrop, angled composition, live pooled battle VFX/cues, HUD motion toggle and reduced-motion support. `client/Assets/View/`, `client/Assets/Game/Art/`. | [Feedback](2026-09-24-phase10-battle-feedback.md), [presentation](2026-09-24-phase10-battlefield-presentation.md), [terrain source](2026-09-23-phase10-battle-map-source.md). Offline combat/hash checks passed; the historical “empty lane” report still needs a real build reproduction/check. |
| Ark and Lab | Painted valley backdrop behind the procedural base, six distinct plot structures, corresponding facility glyphs. Lab uses the same `HomeStage` texture/plot anchors as Ark, with a tappable facility sheet, real displayed tier/cost/Core cap and persisted preview timer. `HomeBaseView`, `LabView`, `FacilitySheet`, `ArkStageProjection`, `HubRouter`. | [Home backdrop](2026-09-24-phase10-home-backdrop.md), [Lab stage](2026-09-24-phase10-lab-stage.md), `implementation/reviews/phase10-facility-icons.png`. Unity crop, plot taps, lighting and countdown behavior remain unchecked for these recent edits. |
| Map and region detail | Painted pannable atlas, 44px zoom controls, graph borders/gates, selectable shortest route and alternate region list. Region Detail handles local server-backed claims and remote route/relocation preview. Unmapped server ids stay neutral; Holdfast is labelled as a preview origin, never the live Ark position. `WorldMapView`, `RegionView`, `RegionPreviewScreen`, `RegionCatalog`. | [Map record](2026-09-24-phase10-map-topology.md), `implementation/reviews/phase10-map-graph.html`, `implementation/reviews/phase10-region-detail.html`. Remote world-status richness is not available from today's API. |
| Store and Allies | Store has a daily gift, five mixed-content bundles, two separate direct-shard products, seven distinct product marks, a three-of-six Custom Chest with Small/Standard/Large controls and exact tier-aware contents/prices, Season Pass and Double Regen. It makes no savings claim because separate-item reference prices do not exist. Allies has a large generated pennant, empty state and planned tools; creation is preview-only. `StoreView`, `AlliesView`, `StoreCatalog`, `generate-store-icons.py`, `generate-alliance-pennant.py`. | [UI record](2026-09-23-phase10-ui-design-pass.md), `implementation/reviews/phase10-store-pack-icons.png`. Fresh Unity fixtures now cover every Store tab/tier and Allies. Thirteen facility/Store PNGs were repaired from cubemap imports to 2D sprites; the importer regression test and a warning-free 34-screen capture pin that fix. Real Store interactions and pennant acceptance still need the Editor walk. |
| Portraits | `Game/Shell/PortraitCache.cs` queues one 256px offscreen render per frame and bounds the LRU at 64 textures, keyed by species/ordered traits/growth/art revision. `CreatureCard` and `HeroSlot` use it with baked-layer fallback. `FrontierBaker` now crops all eleven layers for a species through one padded square, preserving alignment while making the fallback readable; Splash uses Ember's deep coral value so it separates from Ember's hide. | [UI record](2026-09-23-phase10-ui-design-pass.md). The six bodies pass the 40px silhouette gates and all 60 current part/socket layers pass coverage, edge and colour-separation gates. `CreatureSprites` and the baked resources still remain as fallback; retire them only after cache, eviction/rebind behavior and memory have been measured in Unity. |

Recent source commits cover battlefield ground/motion, Ark backdrop, map topology, Lab sheet and the truthful Store economy tiers. `ScreenFixtures` has deterministic states for the selected Holdfast→Sheerdown route, remote Sheerdown detail, Store Packs, Store Pass and Small/Standard/Large chests with charges, shards and XP selected. Unity licensing was repaired by terminating one orphaned `Unity.Licensing.Client` that owned the named-pipe mutex without serving it. The current verification produced 34/34 fresh fixtures, four 34-screen frame probes (430×932, 390×844, 360×640 and 402×874), and an 8-subject safe-area shell probe with all controls reachable. The current EditMode result is 596 total / 595 passing; its sole red is the documented `MainThreadAffinityTests` baseline naming exactly `SessionTests.cs:223`, `:238` and `:262`. Verify the current head with `git log -5 --oneline` rather than assuming a hash in this document.

## Work remaining, in practical order

1. **Finish the real Editor walk of the newest UI batches.** Static fixture capture and size/safe-area probes are complete. Open `Boot.unity` in Play mode and inspect the actual `HomeStage` on Ark and Lab (their fixtures deliberately call `SetStage(null)`), plot taps and facility countdown, map/detail navigation, retained chest picks while changing tiers, deploy and a live wave. Check +40% text and status colours. Show the user the final comparison from the real runtime views rather than treating the blank fixture stages as a product defect.
2. **Resolve known design gaps without inventing server data.** Remote map richness/controller/Collector/alerts still need an API/content decision; the current unauthored server-region boundary is now explicit in the client. The physical 40px silhouettes, face/readability, full trait combinations and motion/attachment behavior of the cast need final Unity eyes-on review. Examine whether Loam's current four body segments plus head satisfy the original five-connected-form brief; do not relabel it without viewing the model. Confirm raider telegraphs for only implemented enemy types before broadening combat scope.
3. **Complete the interactive Unity verification.** Fresh compile/import, EditMode, USS/token/silent-drop/combat gates, screen capture and frame/label probes are current. PlayMode tests still need the Editor Test Runner because the headless PlayMode script can deadlock. Run the proof capture, repeated navigation/species switching, pause/reduced motion and asset cleanup in the Editor. Export production baselines through `ProductionBaselineExport.Export` before making before/after claims. Reproduce or close the old “empty lane” concern on deploy and live wave.
4. **Only after those checks**, decide if old baked card layers and `CreatureSprites` can be removed while preserving silhouette fallback and test coverage. Then perform the Phase 10 device gate: 104 entities with terrain/HUD/VFX, A14 targets of 60 fps and 600 MB with a 30 fps degradation path, 44pt targets, narrow screens and +40% text. Record actual device/model/build/frame/memory evidence in a new `implementation/*-device-validation.md`. TestFlight follows physical-device evidence, not just source checks.

## Commands and result locations

Run source checks from the repository root after a source edit:

```sh
bash implementation/scripts/verify-uss-tokens.sh
bash implementation/scripts/check-silent-drops.sh
python3 implementation/scripts/check-frontier-combat.py
git diff --check
```

For offline visual reviews, the scripts read the current builders/catalog and write their own outputs:

```sh
python3 implementation/scripts/preview-frontier-companions.py
python3 implementation/scripts/preview-frontier-raiders.py
python3 implementation/scripts/preview-frontier-terrain.py
python3 implementation/scripts/preview-world-map.py
```

The pennant generator needs Pillow: `python3 client/Assets/UI/Art/generate-alliance-pennant.py` in an environment with that package. The generated `alliance-pennant.png` and its `.meta` are tracked; preserve the source script when revising it. Binary art is Git LFS, so check `git lfs status` after changing PNGs.

`check-stylesheets.sh` invokes Unity, so run it with the Unity checks rather than interpreting its missing-Editor exit as a style failure. With the Unity Editor closed, on a machine with a graphics device:

```sh
bash implementation/scripts/bake-frontier.sh
bash implementation/scripts/check-stylesheets.sh
bash implementation/scripts/run-unity-tests.sh EditMode
bash implementation/scripts/capture-screens.sh
bash implementation/scripts/probe-frame.sh 430x932,390x844,360x640,402x874
```

These write logs/results under `implementation/results/`; `capture-screens.sh` deletes the previous `results/screens/` before capture and verifies the fixture count. Save comparison copies before rerunning it. `run-unity-tests.sh PlayMode` explicitly warns about a known headless deadlock; use the Editor Test Runner for PlayMode. For the standalone character proof, choose **Broodline → Frontier Proof → Open**, enter Play, use Companion Studio, then **Broodline → Frontier Proof → Capture Game View**. The proof menu capture lands under `implementation/results/frontier/`.

## How the next AI should proceed

Start with `git status -sb`, `git log -5 --oneline`, the current plan and the linked implementation records. Treat this document as a handoff snapshot and inspect source when a claim conflicts with it. Choose one unfinished visual slice, implement it in source, run appropriate source checks, and record what a browser/offline preview proves separately from what Unity proves. Do not call a batch accepted based on a source check. Keep preview actions honest and the UI dependency boundary intact. When Unity is available, use captured images and observed interactions to drive the last revisions, record defects and evidence, then ask the user to review the actual result.
