# Phase 10 — Batch 1, the whole-app skin pass

Running record for [the Phase 10 plan](../specs/plans/broodline_phase10_living_frontier.md).
Batch 1 ends with the user's eyes-on review (Task 1.8); nothing below is
accepted because a test passed.

## Task 1.0 — records, assembly split, hygiene (2026-09-23)

**Done in source:**

- `client/Assets/Frontier/Art/` now holds the art core — `FrontierArt`,
  `FrontierMesh`, `FrontierSurface.shader`, `FrontierRigDefinition`,
  `FrontierPose`, `FrontierCreature`, `FrontierFace`, `FrontierParts` and the
  six species builders — under `Broodline.Frontier.Art.asmdef`, an assembly
  with **no references**. `Broodline.View`, `Broodline.Game` and the proof's
  `Broodline.Frontier` reference it. Dependency graph:
  `Frontier.Art ← View ← Game ← Frontier (proof)`. `Broodline.UI` does not
  reference it and must not.
- The proof's tests split: `Art/Tests/FrontierArtTests.cs` (7 tests:
  initialization, mesh primitives, budget/bindings, sockets, blink, pause,
  reduced motion) and `Tests/FrontierProofTests.cs` (4: outcome hash,
  formation ×2, breach feedback). Test bodies are unchanged; the known
  weaknesses (budget loop hard-codes six ids; `weight0 == 1` will reject
  Pale's blended membrane) are Batch 2's to fix, deliberately not folded in
  here so this task stays a pure move.
- `View/RuntimeShaders.All` gains `"Broodline/FrontierSurface"`.
  `RuntimeShaderInclusionTests` will be RED until `Phase0Setup.Apply` runs in
  the Editor and `ProjectSettings/GraphicsSettings.asset` is committed with it.
- `.gitignore` excludes `client/Assets/Frontier/Generated/` (the proof scene
  `FrontierProofBuilder.Build` regenerates). The eight `Frontier/*.cs.meta`
  files the previous commits left untracked are now tracked under `Art/`.
- Bible §10.1 and §10.6 amended: faces are allowed (the Phase 9 "not in a
  face" ruling is superseded by the approved Living Frontier direction), and
  `client/Assets/UI/Shell/Tokens.uss` is the token authority, seeded from the
  visual upgrade plan's palette rather than the 2026-09 handoff README.

**Checks run here:** Roslyn syntax parse of the two test files and
`RuntimeShaders.cs` (no syntax errors; not a Unity compile). `git status`
shows renames, not delete+add, for every tracked file.

**Unity checks, once the Editor was closed later the same morning:** the
open Editor recompiled the split with no errors; `Phase0Setup.Apply` ran in
batch mode and `GraphicsSettings.asset` now lists `Broodline/FrontierSurface`;
the full EditMode suite ran green apart from the baselined MainThreadAffinity
red, with both Frontier assemblies' tests included for the first time. Still
owed: `Broodline → Frontier Proof → Open` in the Editor (not exercised by any
batch gate).

## Task 1.1 — tokens, type scale, sprite kit (commit 1650e97)

Every legacy token re-pointed to the Living Frontier palette; the type scale
raised (body 12→14, micro 10→11, tab 10→12, screen title 21→24, hero 28→30);
semantic and species tokens added. `generate-textures.py` reads `Tokens.uss`
and emits the nine-slice primary/reward buttons, the brass-edged slate panel
and the tab frame; `ArtImportSettings.cs` re-asserts their slice borders by
file name. The primary CTA and the tab bar moved onto the sprites; `.card`
gained a hairline. The two species/role collisions palette-decision.md
recorded are resolved and pinned in the other direction.

**Found:** `ComponentTests.TheChipPinsItsOwnHeightInTheStylesheetItself`
pinned the chip height to the 10px type; the pin moved to 22 with its
arithmetic. Species swatches are unchanged, so the CVD baseline did not move.

## Task 1.2 — shared components and shell chrome (commit 1650e97)

`ResourceBar` replaces the never-placed `CurrencyHeader`: one pill per server
balance, a "+" only on shards, never on Marks. Tab bar on the dark frame with
brass; section cards and confirm dialogs on the new tokens; the founder card
border is the reward gold. Nav and trait icons are still the Lucide set: the
generated icon sheet the plan calls for waits on the user's approval of the
images, so the tabs carry a stand-in.

## Tasks 1.3–1.5 — router, home base, four destinations (commit 0f4e9c4)

`HubRouter` routes every tab; `FtueDirector` hands the host to it after its
last beat (`Busy` gates a tab tap before that; `DefendOnceAsync` and
`SpliceFromRosterAsync` are the hub's ways into the fight and the chamber).
`HomeStage` paints `FrontierArt.Base` (platform, Ark, six plots, rim trees;
budget ≤30k triangles, pinned) into a 720×1280 texture on demand and projects
the plots into `HomeBaseView`'s markers. Map is the thirty-region catalog with
the live region from `region/state`; Store, Lab and Allies read a
PlayerPrefs-backed `StubLedger` whose every write is logged `[stub]` and whose
every CTA says "(preview)". The Codex opens from any trait chip.

**Narrowed:** region detail is the existing top-level `RegionView` shown in
place (it has no back chevron), so the Map tab is the way back. The drawn map
is Batch 5.

## Task 1.6 — the migration and the hero band (commit cdf4c2f)

All forty stylesheets read the semantic names; the seven legacy names are
deleted and the token gate proves it. Founder naming and the splice reveal put
the creature on a deep slate band with a brass ring (`HeroBand.Tint.Deep`);
the lane card and the HUD wear the brass frame.

**Not done:** the illustrated chapter header on Campaign select waits on
approved generated art; Roster already draws two columns.

## Task 1.7 — the inherited gaps (commit cdf4c2f)

Pause and speed work and are the HUD's only pickable elements; `WaveRunner`
asks `PicksControlAt` before a tap becomes a Rally, so "tap anywhere" still
holds everywhere else. Deploy and the HUD share `WaveHudScreen.WaveOf`. The
portrait studio clears on `ScreenHost.ScreenChanging`. A cleared campaign
says "Frontier held". Not done: the HUD's energy-vs-reward note from Phase 9
- the runner's path carries no balance, and the deploy card already shows the
balance; recorded rather than invented.

## Task 1.8 — the review package

Baseline: `implementation/results/phase10-test-baseline.txt` (546/545/1).
Corpus: `implementation/results/screens/` at 430×932, 26 fixtures.

**Simulator walk, 2026-09-23, iPhone 17 simulator (iOS 27), development
build** (`SimulatorBuilder.BuildIOSSimulatorDevelopment` against the deployed
API, then `xcodebuild -target Unity-iPhone -sdk iphonesimulator`; log at
`implementation/results/phase10-sim-console.log`). The account was at the
founder-naming beat. Walked: naming (live Hollow portrait on the deep band)
→ tab tap toasts "Finish this step first" → Deploy "Wave 2 / 12" with the
brass-framed lane card → the wave with the brass HUD; **Pause held the tick
at 265 and Resume continued it; 2× ran the clock twice as fast** → "Wave
held" → guided splice (the Splice tab unlocked and the shard pill re-bound
to 565 from the snapshot) → confirm dialog in the danger red → reveal on the
deep band → lineage → **the hub**: the Ark base painted live with its six
markers → Map list with Holdfast marked → Splice roster with a chevron →
Store from the "+" → Lab from a base marker, "Upgrade (preview)" toasting
and counting down 01:59 → the Codex from a trait chip.

**Found on the walk, fixed in source, awaiting the gate run:**

1. **The first pass through the first hour never reached the hub.** The
   reveal path (`CommitAndRevealAsync` → lineage) called `CampaignAsync`
   directly, a third road the Task 1.3 hand-over missed, so the walk showed
   Campaign with the tabs still gated. On relaunch (Beat.Lineage) the walk
   took the `WalkAsync` road and the hub took over. Routed through
   `HubOrCampaignAsync`.
2. **The Store could not be left.** Pushed screens hide the tab bar and the
   scaffold draws its chevron only for `pushed: true`; `StoreView` said
   neither. Now pushed.
3. **The reveal's "mutation rolled" pill read paper-on-amber** under the
   deep band's label rule. The band now leaves `.t-warn` and `.panel-amber`
   labels their own ink.
4. **The roster drew one column** at the phone's 402pt: two 198px cards fit
   the 430 capture frame and not the device. Cards are 48% wide.

**Seen, not changed:** the black band above the shell is the Boot camera's
clear colour under the safe-area inset (unchanged from Phase 9); the
battlefield and every creature are still the Phase 9 procedural set (Batches
2–4); the resource pills follow the server's balance order; the reveal's
creature sits high in its disc (portrait framing, Batch 7).
