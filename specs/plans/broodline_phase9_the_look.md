---
status: current
folder: 03-technical
note: >
  Phase 9 design. The Look: the two defects the eyes-on pass found, the nine
  tester-path screens brought from the handoff's skeleton to the handoff's
  fidelity, creatures in 3D from a recipe pipeline that honours the rig
  proof's socket standard, and TestFlight as the exit gate in Phase 8's own
  words. Precedes the implementation plan. Records the tone ruling
  (stylised creature, not toy cartoon and not natural-history), the
  production ruling (generated in-project, socketed, an artist commission left
  as a clean upgrade), and the presentation ruling (the deploy card shows the
  real lane; the fight takes the screen).
---

# Broodline — Phase 9 Design: The Look

*The phase that makes the game worth a stranger's hour*

> **Read `implementation/2026-09-17-phase8-followups.md` §1c–§1e and
> `implementation/results/phase8-visual-review.md` before re-deciding anything
> they name.** Phase 8 closed on a human verdict, quoted there verbatim: the
> screens are too simple, the creatures should be 3D like cartoon characters,
> the shapes are right, the design is poor, and nothing this ugly and this
> broken goes to a tester. This document is the answer to that verdict. The
> TestFlight gate (Phase 8 DoD item 10) moved here unchanged and is §8.
>
> **Counts are not quoted in this prose.** The EditMode baseline lives in
> `implementation/results/phase8-test-baseline.txt` and moves only when a task
> moves it.

---

## 1. Scope

Phase 9 fixes the two defects nothing can be judged through, brings the
screens a tester meets in the first hour up to the design handoff's fidelity,
puts 3D creatures on the lane and on the cards, and ships a TestFlight build
when — and only when — a checklist is green and the developer's own walk says
yes.

**In scope.** The abandoned-wave lockout and the shell-over-battlefield
layering (§2). A creature pipeline: recipes, an Editor mesher, sockets, a
shader, a sprite bake, a portrait studio, and the lane dressed (§3). Nine
screens to handoff fidelity and two to consistency (§4). The gates that prove
each of those (§6). TestFlight, on the exit gate (§8).

**Out of scope, each a later phase rather than a deferral.** Resuming an
abandoned wave rather than forfeiting it (Phase 10, if playtest asks). The
Region map's polygon viewport (Phase 10, deferred by Phase 8 §5.3 and not
reopened). An artist commission against `broodline_rig_proof.md` — the
pipeline here is built so that commission replaces meshes and keeps
everything else. Instinct cues beyond the crown socket being authored on every
body (bible §10.3's open question, still a playtest question). Raider bodies
beyond the three the engine carries. Audio.

### 1.1 Definition of done

1. `POST /v1/wave/abandon` deployed, the roster read healing past-expiry
   issuances, and the client's cold-start sheet forfeiting a left-behind wave.
   Proven by the server suite and by a "start, never submit, abandon" step in
   `smoke-loop.sh` against the deployed stack.
2. The shell hidden while a wave is resident and restored on both the
   completion and the exception path, proven by a PlayMode test and then seen
   in a running wave.
3. A notice surface in the shell, so a blocked beat's sentence reaches the
   player and not a console.
4. Six species bodies, twelve trait parts and three raider bodies generated
   from recipes into committed assets, with the drift gate green.
5. Every creature card showing its body and both combat parts as layered
   baked sprites, and the 40px silhouette test green against the baked bodies.
6. The lane drawing creatures and raiders from the pipeline, dressed, with
   the capture tests proving the sim untouched.
7. The portrait studio live on founder naming, the splice chamber's predicted
   hybrid, and the splice reveal.
8. Nine tester-path screens at handoff fidelity; Codex and Lineage consistent
   with them. `verify-uss-tokens.sh` and the stylesheet gate green.
9. The forty-eight-combination contact sheet and Cinderplate rendered by the
   bake, in `implementation/results/`, for the eyes-on pass.
10. The exit gate of §8 met: the checklist green, the developer's walk of the
    first hour recorded, and — on a yes — the archive uploaded, a tester who
    has not seen the app installed, and their report in the Phase 9 record
    verbatim. This is Phase 8's item 10 in Phase 8's words.

---

## 2. The two blockers

Both were found by a human in the first minutes of the first walk, after a
green suite and sixteen screenshots had said nothing was wrong. Both land
before anything visual, because neither the screens nor the creatures can be
judged through them.

### 2.1 An abandoned wave locks the roster

**What the record says, and the correction.** `phase8-followups.md` §1c calls
the lockout unrecoverable. On the server it is not: `wave_issuances` carries a
two-hour expiry, and `issueWave` runs `settleExpiredForPlayer` before its own
checks, releasing the creatures. The lockout is a client fact. `FtueDirector`
refuses to open the deploy screen while any creature is committed, and the
deploy screen is the only route to the call that would release them. Inside
the two-hour window the lock is real; after it, the client is blocking itself.

**The fix, three parts, none of them UI polish.**

1. **Forfeit.** `POST /v1/wave/abandon` settles the caller's live issuance as
   `'expired'` through `settle()`, which already releases the roster. No new
   state, no reward, no loss, no replay-cap cost — the same settlement the
   expiry path applies, applied now. It is added to `openapi/broodline.json`
   and the C# client is regenerated through the existing `nswag.json`.
2. **A healing read.** The roster read runs the same expiry settle
   `wave/start` runs, so a player who returns after the window sees a free
   roster with no dialog at all.
3. **The sheet.** `BootController`, after cold start and before the director
   runs, checks the snapshot. If any creature is committed, the shell shows
   an `AbandonedWaveSheet`: "A wave was left unfinished. Your creatures are
   still out there." and one button, **Forfeit**. Forfeit calls abandon,
   cold-starts again, and the director proceeds. The director's own skip of
   committed creatures is unchanged; the sheet guarantees it never sees an
   all-committed roster.

**The notice surface.** §1c's second item is "somewhere to see it".
`BootController.OnNotice` logs and stops, and `OutboxPump.Notices` holds
sentences nothing renders. The shell gets a `NoticeToast`: one element in the
sheet layer, tokens only, showing a sentence for a few seconds and stacking
if a second arrives. It is what turns a tester's "it went blank" into "it
said X".

**Why resume is not here.** Resume needs the server to hand back the live
issuance's wave, seed and deployment — a second route — and the client to
rebuild deploy state from it. A wave is about a minute long. The player loses
little by forfeiting, and the phase is about the look. Booked to Phase 10 on
playtest evidence.

### 2.2 The shell renders over the battlefield

**Diagnosis, unchanged from §1d.** `Wave.unity` loads additively; its
`UIDocument` shares the shell's `PanelSettings`, so the HUD is a sibling root
in the shared panel and nothing hides `#shell-root`. The shell painted
transparent for seven phases because `Tokens.uss` compiled to zero rules;
fixing the tokens made it opaque.

**The fix.** `WaveHost` takes a `setShellVisible` callback from
`BootController`. It hides the shell **after** `LoadSceneAsync` completes and
the runner is found — the wave scene's document is attached by then — and
restores it in the `finally` block, before `UnloadAsync`, so the completion
path and the exception path both restore. The reverted attempt hid the shell
before the load and had no exception-path restore; the white screen it
produced is consistent with the restore never running.

**The proof, this time.** A PlayMode test in `Broodline.Game.PlayTests` drives
`WaveHost.RunAsync` with a recording callback through a completed wave and
through the forced-throw path `WaveCapturePlayTests` already exercises,
asserting hidden while resident and visible afterwards in both. The EditMode
suite runs green before the commit. Then the fix is confirmed in a running
wave by eye. The HUD stays a sibling root; nothing about its attachment
changes.

---

## 3. The creature pipeline

### 3.1 The rulings

**Tone: stylised creature.** Cartoon proportions and cartoon rendering —
chunky forms, soft shading, thick readable shapes — on animal anatomy and
animal behaviour. Personality lives in posture, gait and the trait parts, not
in a face. This is the reading of "3D like cartoon characters" that keeps
bible §10.7 intact: strange is allowed, damage stays posture, appeal beats
horror, and raiders only need to be angular and dark rather than a different
art style. References: Pikmin's creatures, Kena's, Creatures of Ava. Not: big
eyes, plush forms, faces as the identity.

**Production: generated in-project, socketed from day one.** No Blender on
the machine and no mesh libraries; nothing in this pipeline waits on a
download. Meshes are generated inside Unity by an Editor script from recipes,
committed as assets, and rebuilt on the CI runner. The rig proof's socket
standard is applied to the first body, so an artist commission later replaces
meshes and keeps recipes, sockets, shader, bake and studio.

**Where 3D appears.** Baked sprites on every card and list. A live turntable
at three hero moments: founder naming, the splice chamber's predicted hybrid,
the splice reveal. Full 3D on the lane.

**Shading: soft matte.** A lighting ramp into a warm key and a cool fill, a
rim light, a two-tone body with a darker underside, no outline. The rim and
the underside are the value structure bible §10.4 measured Pale as needing on
a near-white card.

### 3.2 The assembly boundary

A new runtime assembly, **`Broodline.Creatures`**, references nothing
project-side. `Broodline.View` references it to build the lane.
`Broodline.Game` references it for the portrait studio and the lane stage.
**`Broodline.UI` never references it, nor `Broodline.View`**: cards receive
PNG sprites from `Resources` and the stage element receives a `Texture`, both
plain data. `Broodline.UI.Tests` keeps constructing trees with no panel.

### 3.3 Recipes

A species is a C# recipe, not a model:

| Field | Meaning |
|---|---|
| Primitives | Spheres and capsules (bodies) or boxes and wedges (raiders): position, size, a blend radius, a bone name, a growth weight |
| Sockets | `sk_dorsal`, `sk_flank`, `sk_crown` as named transforms — position, orientation, uniform scale — per rig proof §3 |
| Colour | Base and underside. The base hex is mirrored from `PaletteContrast.Species`, because `Broodline.Creatures` references nothing and cannot read it; an EditMode test in `Broodline.Game.Tests`, which sees both, asserts the mirror matches |
| Bones | A short list with parents; eight or fewer |

A trait part and a raider kit are the same shape with one mount point in a
canonical orientation. Twelve parts are authored even though the engine
carries four traits (Chill, Taunt, Splash, Carapace), because a part is a few
lines and the forty-eight-combination sheet cannot be rendered without them.
The four engine traits come first. Raiders use a near-zero blend so they come
out angular; three bodies, for Courser, Lash and Skirmisher, from
`Character Bible.dc.html`'s silhouettes.

Vetch is authored first and shown on a card and on the lane before any other
body exists. That is the stop where "no, warmer" costs one recipe rather than
six.

### 3.4 The mesher

An Editor generator evaluates a recipe as a signed distance field with smooth
union, runs marching cubes on a fixed grid, smooths, and assigns bone weights
from each primitive's contribution. Output: a `Mesh` asset and a prefab per
body, part and kit, under a generated folder that is committed. Budgets:
under 1500 triangles per body, under 300 per part, eight bones or fewer —
well inside the 10000-triangle bound `rig_proof.md` §8 measured, with the
margin that document says not to spend.

**The drift gate.** An EditMode test regenerates every recipe into a temp
path and compares hashes with the committed assets. A recipe edited without a
regenerate fails the suite. The generator is also callable in batchmode for
CI, the same way the wave scene builder is.

### 3.5 Growth, motion, assembly

**Growth** is per-bone scale driven by the growth weights: runt to apex on one
rig, mass moved outward, same profile — bible §10.2 rule 3, and never
separate meshes.

**Motion** is procedural and body-level, per §10.3: breathe at idle, bob when
moving, a flinch on hit, and damage as posture — a droop and a desaturation
value on the material, never injury (§10.7). No animation clips; the
`Animator` cost the rig proof never measured is not incurred.

**Assembly** mounts by slot index — combat one to `sk_dorsal`, combat two to
`sk_flank` — per rig proof §3.1, whatever the traits. The crown socket is
authored on every body and carries nothing yet. **Cinderplate** is the Vetch
body with the Cinder part at the dorsal socket, produced by the assembler and
by nothing else (bible §10.9).

### 3.6 The shader

One hand-written URP shader, `Creature.shader`: half-Lambert remapped through
a two-stop ramp (warm key, cool fill), a rim term, base and underside colours
blended on the normal's downward component, a desaturation float, and a
tint. Hand-written rather than Shader Graph so it is text under review. Used
by bodies, parts and kits alike; raiders get their own dark ramp.

### 3.7 The bake

An Editor bake renders, from one fixed camera at 3× the card slot, one PNG
per body and one per part in each socket on each body — six bodies, twelve
parts, two sockets — all aligned. The card's silhouette slot becomes three
stacked images: body, dorsal part, flank part. Both combat traits are visible
on every card at zero runtime cost, which is §10.4's "single most important
functional requirement" met in 2D. `SilhouetteTests` runs against the baked
bodies. The same bake writes the forty-eight-combination contact sheet and a
Cinderplate render to `implementation/results/` for the eyes-on pass, which
is rig proof §4 items 2, 3, 4 and 7 rendered rather than imagined.

### 3.8 The portrait studio and the lane stage

**Portrait studio**, in `Broodline.Game`: a hidden layer, one camera, one
render texture, one assembled creature turning slowly. The UI's
`CreatureStage` element shows a texture and does nothing else. Three screens
use it, one at a time.

**Lane stage**, in `Broodline.Game`: the deploy card's picture. A lightweight
ground strip, pockets, the Ark, and the selected creatures standing in their
pockets, rendered by its own camera to a render texture the deploy view
shows. It reads pocket positions from the same lane layout the wave view
uses, so the deploy picture and the fought lane agree. It does not load
`Wave.unity` and does not touch the sim.

### 3.9 The lane

`WaveView.Build` constructs creatures and raiders through the assembler
instead of `SyntheticCreature`. The lane stays straight, because
`Lane`'s distance table is straight; the handoff's winding path is dressing
and is not copied. Dressing lives in the wave scene: a pastel ground, a path
strip, soft blob trees and an Ark prism in the handoff's palette. **The wave
view may change what it draws and never what it reads**: everything still
comes off the retained snapshot pair, and `WaveCapturePlayTests` and the
determinism gate keep proving the sim untouched. `SimVersion` stays 0.4.0 and
the device capture stays valid.

---

## 4. The screens

### 4.1 Shared first

Every screen is built from the same parts, so the parts come first.

- **Scaffold header**: the handoff's eyebrow label above the title, the back
  control as a white pill, a resource pill on the right through the existing
  header slot.
- **New components**, one element and one purpose each: `GenChip`,
  species-tinted `TraitChip`, `HeroSlot` (dashed ring around the layered
  sprite), `InheritanceBar` with an odds tag, `MutationBanner` (amber
  gradient), `LineageStrip`, `CostCtaRow`, `FieldSlotRow`, `CreatureStage`.
  `StatCell` gains a trend arrow.
- **Gradients** become ramp textures, as the CTA already is. Every new value
  goes into `Tokens.uss`; `verify-uss-tokens.sh` enforces it.
- **`OptionRow`** follows the handoff's own numbers and is composed inside a
  `SectionCard` where the handoff composes it, so its fill sits on white. The
  WCAG note in `phase8-visual-review.md` closes by following the design, not
  exceeding it.

### 4.2 Tester order, each against its handoff file

| Screen | Against | To fidelity means |
|---|---|---|
| `FounderNamingView` | `Onboarding.dc.html` | Step frame, progress pips, note tone, the founder's live turntable above the name field |
| `CampaignSelectView` | none | Eyebrow header, option rows in a section card, the locked row with `icon--lock` |
| `RosterView` | `Creature Roster.dc.html` | Card grid with layered sprites, generation chip, trait chips, instinct line |
| `SpliceChamberView` | `Splice Chamber.dc.html` | Parent pair in hero slots with chips, the join control, predicted-hybrid panel with turntable, name, lineage line, stat cells with arrows; inheritance card with bars and odds tags; mutation banner; lineage strip; cost-plus-CTA row. Banner and forecast still collapse when the server names nothing |
| `SpliceRevealView` | `Splice Reveal.dc.html` | Hero card carries the turntable; the flare stays and is judged in motion |
| `DeployView` | `Wave Defense.dc.html`, `placing` | Eyebrow, wave title, energy and integrity pills, the lane card from the lane stage, the incoming-wave card with three cells, field slot rows, Start Wave |
| `WaveHudView` | `Wave Defense.dc.html`, `running` | Shell hidden; eyebrow, wave counter, pause, speed pill, the two stat pills; lane and creatures from §3 |
| `PostWaveView` | `Wave Defense.dc.html`, `won` | The result card in the handoff's vocabulary |
| `WaveDefeatView` | `Wave Defeat.dc.html` | The breach and the answering trait in the handoff's card; the free retry; a loss fixture, since none exists |

**Deploy keeps its rules.** Tapping a field row selects or deselects a
creature. Pockets stay assigned by selection order, which `DeployScreenModel`
owns and `deploymentMatches` depends on. The picture changes; the protocol
does not.

**The fight's shape.** Deploy shows the lane in a card. Start Wave hides the
shell and the wave takes the screen. On completion the shell returns and the
post-wave or defeat screen is pushed. Two presentations of one lane, kept in
step by one layout.

### 4.3 Consistency pass

`CodexSheet` and `LineageView` get the eyebrow header, chips, cards and
spacing. No new layout. Neither was reached in the Phase 8 walk and neither
has a handoff screen.

---

## 5. Sequencing

Serial, because the Editor holds a single-instance lock on `client/` and
parallel agents on Unity buy nothing.

1. **Blockers** (§2), each with its gate, each seen in a running wave.
2. **Vetch end to end**: assembly, recipe, mesher, shader, bake, one card,
   one lane creature, the portrait studio. **Stop for the developer's eye.**
3. **Shared components and the scaffold header** (§4.1).
4. **Screens in tester order** (§4.2), with Ember landing before the splice
   chamber so the tutorial parents and Cinderplate are real, and the other
   four species and the three raiders landing beside the roster and deploy
   screens.
5. **Consistency pass** (§4.3).
6. **The exit gate** (§8).

---

## 6. Testing

### 6.1 What binds

Carried from Phase 8 and extended:

- **No change under `engine/`.** `SimVersion` stays 0.4.0.
- **`Broodline.UI` references neither `Broodline.Sim`, `Broodline.View` nor
  `Broodline.Creatures`.** Sprites and textures cross the boundary; code does
  not.
- **Every value lives in `Tokens.uss`.** Screens compose `ScreenScaffold`.
- **Nothing on the startup path merges without the EditMode suite green.**
  This is how the reverted fix happened.
- **The wave view changes what it draws, never what it reads.**

### 6.2 What gets a gate

| Gate | Where |
|---|---|
| Abandon releases and settles `'expired'`; no live issuance is a no-op; a second abandon does not double-settle; the roster read heals past expiry | `services/api` vitest |
| Start, never submit, abandon, roster free | `smoke-loop.sh` against the deployed stack |
| Shell hidden while resident, restored on both paths | `Broodline.Game.PlayTests` |
| Recipe-to-asset drift; triangle and bone budgets; three sockets on every body; dorsal and flank parts never overlap in bounds on any body | EditMode, `Broodline.Creatures.Tests` |
| Six baked bodies distinct at 40px | `SilhouetteTests`, re-pointed at the bake |
| Portrait studio produces a non-blank texture | PlayMode |
| Structure of every new component and screen | `Broodline.UI.Tests` |
| Tokens and stylesheets | `verify-uss-tokens.sh`, `check-stylesheets` |
| The sim untouched | `WaveCapturePlayTests`, the determinism gate |

### 6.3 What cannot be gated

Motion, the HUD in a running wave, whether the ramp is warm enough, whether
Pale reads on a card, whether a stranger says "oh". These are the eyes-on
walk (§8), and the contact sheet, the Cinderplate render and the screen
captures exist so that walk has something to point at.

---

## 7. What this design deliberately does not do

- It does not commission an artist. It builds the pipeline that commission
  would plug into, and records in §3.1 that the ceiling here is charming
  low-poly rather than sculpted.
- It does not copy the handoff's winding lane, its raster art, or its eight
  raiders. The sim's lane is straight, the handoff has no raster art, and the
  engine carries three raiders.
- It does not reopen the Region map, the palette, or the Instinct cue count.
- It does not resume an abandoned wave.
- It does not make the deploy card interactive on the lane itself. Selection
  happens in the field rows; the lane shows the result.

---

## 8. The exit gate

Written down before the work starts, so the phase cannot drift and a "no"
comes with a named gap rather than a mood.

**The checklist.**

1. Both blockers fixed, gated, and verified in a running wave.
2. Nine screens at handoff fidelity; Codex and Lineage consistent.
3. Six species and three raiders on the lane; layered sprites on every card;
   Cinderplate from the pipeline; the forty-eight-combination sheet rendered.
4. Smoke loop green against the deployed stack, abandoned-wave step included.
5. EditMode baseline held or moved by a task that says so.

**Then the walk.** The developer walks the first hour on the phone-sized
viewport against the deployed stack, the same walk as Phase 8, and records
the verdict in `implementation/results/phase9-visual-review.md` in their own
words. Motion and the HUD in a running wave are judged here.

**Then, on a yes, TestFlight.** The archive, the upload, a tester who has not
seen the app, and their report on where they were confused, in the Phase 9
record verbatim. Phase 8's DoD item 10, in Phase 8's words. On a no, the
named gap becomes a task and the walk repeats.

---

## 9. Decisions taken

| Decision | Ruling | Reversible? |
|---|---|---|
| Creature tone | Stylised creature | Yes, at the Vetch stop; expensive after six bodies |
| Production | Generated in-project, socketed | Yes; an artist replaces meshes and keeps the rest |
| Where 3D appears | Sprites on cards, turntable at three moments, lane in 3D | Yes |
| Abandoned wave | Forfeit plus healing read; resume later | Yes |
| Battlefield | Lane in the deploy card; fight full screen | Yes, but the lane stage is sunk cost |
| Screen scope | Nine to fidelity, two to consistency, Region deferred | Yes |
| Shading | Soft matte, ramp, rim, underside, no outline | Yes, one shader |
| Exit gate | Checklist plus the developer's walk | No |

---

## 10. Errata, found in execution

*Recorded 2026-09-22 by Phase 9 Task 22, against the tree at the end of the
phase. These are defects in **this design document**, found by implementing it.
They are written here, where they were written, rather than quietly
reimplemented somewhere else. **Numbered §10 and not §11**: the document has
nine numbered sections, and a §11 after a §9 would be its own erratum.*

**§3.4's triangle budgets are wrong, both of them: 1500 → 2500 per body, and
300 → 400 per part.** Measured rather than negotiated. Naive surface nets emit
one vertex per surface cell and very nearly one quad per vertex, so triangles
≈ 2 × surface cells and the count scales with **grid squared**. At a grid that
reads smooth — 24 cells across a body, which is what the Task 7 brief
estimated at "about 2000–2500" — Vetch meshes to **3336**. The shipped budgets
are `MesherTests.BodyTriangleBudget = 2500` and `PartTriangleBudget = 400`,
still **a quarter of the 10000 per body `broodline_rig_proof.md` §8.3
measured**, with the ~25% of frame that document says not to spend still
unspent.

**Two consequences of the same arithmetic, both worth keeping.** First, the
grids in the recipes are lower than any brief's: Vetch ships at grid **20**
(2352 triangles), because 22 measures 2748 and 21 measures 2524 — both still
over. The lever for more detail is a **smaller padding**, not a bigger grid.
Second, parts run high for a structural reason: `grid` is a cell **count** per
axis, not a cell **size**, so a part whose bounding box is far from cubic gets
anisotropic cells and its short axis is oversampled. The cinder crest's box is
0.96 × 0.90 × 0.46 and meshed to 884 at grid 16 against a 400 budget; it ships
at grid 10. `SurfaceNets.Build` was deliberately **not** changed to derive
per-axis counts from the longest axis, because that would change what `grid`
means for every future recipe.

**§2.1 part 3 puts the abandoned-wave check in the wrong place.** It says
*"`BootController`, after cold start and before the director runs, checks the
snapshot."* It is **inside the director, after the roster loads**:
`FtueDirector.WalkAsync` calls `LoadRosterAsync()` and then
`ForfeitIfLockedAsync()` (`client/Assets/Game/Ftue/FtueDirector.cs:485-486`),
both before `Ftue.Derive`. It had to move, and the reason is not organisational:
**the snapshot does not say whether a creature is committed** — `NeedsForfeit`
reads `_roster.Known`, which does not exist until the roster load has
happened. The design's actual guarantee is unchanged and is now stated at the
site: the sheet is shown before the beat is derived, so `FightAsync` never meets
an all-committed roster. One thing the design did not anticipate and the code
now carries: the forfeit can *fail*, so `ForfeitIfLockedAsync` re-reads the
roster afterwards and stops on a toast rather than a blank screen if the lock
survives.

**§2.2's proof is not a new test and it cannot run headlessly.** Three
corrections. (1) The assertions live in the **existing**
`client/Assets/Game/Tests/PlayMode/WaveCapturePlayTests.cs`, on both the
completion path and the forced-throw path, rather than in a test of their own —
those two tests already drive `WaveHost.RunAsync` through exactly the two paths
this proof needs. (2) **A `bool` alone cannot prove the ordering this fix is
about.** The reverted attempt hid the shell *before* the additive load and would
record the same `false`, so the recording callback records
`(Visible, SceneLoaded)` pairs and the assertion is
`{ (false, true), (true, true) }` — hidden while the scene is already resident,
restored while it still is. (3) **The proof runs from the Unity Editor's Test
Runner, by a human.** `implementation/scripts/run-unity-tests.sh PlayMode` still
uses `-runTests` and is expected to deadlock on this Editor; the script says so
in its own comment rather than routing somewhere that would report a false
green. So "the EditMode suite runs green before the commit" is a gate an agent
can meet and **the PlayMode proof is not** — it was unrun for most of the phase
and was run at the exit gate. Anywhere this design says "proven by a PlayMode
test", read: proven by a person opening the Test Runner.

**§3.7 and §3.8 describe "a hidden layer" without naming it, and it is
project-wide state.** It is **layer 6, named `Studio`**, authored in
`client/ProjectSettings/TagManager.asset` and referenced everywhere as
`CreatureAssembler.StudioLayer`. Four consumers rely on it and a fifth on its
absence: `PortraitStudio` and `LaneStage` each put their rig, camera and light
on it and set `cullingMask` to `1 << StudioLayer`; `CreatureBaker` does the same
for the bake rig; and `WaveSceneBuilder` excludes it from the wave camera
(`"Everything but Studio (layer 6)"`) so an off-screen rig cannot appear on the
battlefield. Worth naming in a design rather than leaving to four call sites,
because a layer index is the kind of shared state a later scene, camera or
physics setting collides with silently — and because the layer's whole job is
that nothing else ever draws it.

---

*Owns: Phase 9's scope, rulings, sequencing, gates and exit. Does not own: the
art direction (bible §10), the socket standard (`broodline_rig_proof.md`), the
handoff's tokens (`specs/Designs/design_handoff_broodline/README.md`), or the
implementation plan, which follows this document.*
