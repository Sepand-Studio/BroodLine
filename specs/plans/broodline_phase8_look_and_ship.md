---
status: current
folder: 03-technical
note: >
  Phase 8 design. The Look and the Ship: the visual foundation the token layer
  has no face for, eleven screens to handoff spec, and the two Phase 7 gates
  that are still genuinely open. Precedes the implementation plan. Records the
  render-pipeline ruling (3D, generated rather than commissioned), the art
  ruling (interim proxies ship, production assets do not), and the finding that
  two of the four gates the Phase 7 record calls open are already closed.
---

# Broodline — Phase 8 Design: The Look and the Ship

*The first phase whose deliverable is how the game looks, and the last one
before someone who is not the developer installs it*

> **Read `implementation/2026-09-15-phase7-followups.md` before re-deciding
> anything it names — and read §2.1 of this document first, because that record
> is stale in a way that would cost this phase real work.** It lists four
> Definition-of-Done gates as open. Two of them closed after it was written.
> This is the third time the project has hit that pattern; `phase4-followups`
> warned about it in exactly these words, and Phase 5 nearly re-fixed three
> defects because of it.
>
> **Counts are not quoted in this prose.** They live in
> `implementation/results/phase7-test-baseline.txt` — which is itself stale as
> of this writing, and §2.1 is the correction.

---

## 1. Scope

Phase 8 makes the shipped slice look like a product and puts it in someone
else's hands.

**In scope.** The visual foundation the token layer currently has no face for —
fonts, elevation, the CTA gradient, icons, a motion vocabulary, and the species
palette fix. Eleven of the twelve existing screens brought to the design
handoff's specification. Interim creature proxies. An Editor harness so screens
can be iterated without a running server. The deployed stack. A TestFlight
build, installed by someone who has not seen the app, whose report lands in the
Phase 8 record verbatim.

**Out of scope, and each is a later phase rather than a deferral.** Every
handoff screen whose system does not exist yet (Phases 10–13). Production creature
art (Phase 9, running in parallel). The world map's polygon viewport (Phase 10,
and §5.3 says why it cannot be pulled forward). Audio, which no phase currently
owns and which §9 records as unscheduled rather than silently omitted.

### 1.1 Definition of done

1. Both faces imported, and the tabular-figures gate green — or, if Baloo 2's
   numerals fail it, the paired-face fallback bible §10.6 already prescribes,
   implemented, with the failing measurement recorded.
2. `verify-uss-tokens.sh` in `tests.yml`, and green, with the five raw hexes in
   `WaveHudView.uss` gone.
3. The species palette fix applied to `Tokens.uss`, `broodline_accessibility.md`
   §4 and `broodline_bible.md` §1.2 **in one commit**, with the CVD separation
   test green.
4. Every screen view composes `ScreenScaffold`, asserted by reflection.
5. The six proxy silhouettes pairwise-distinct at 40px, asserted.
6. The screenshot corpus captured for all twelve screens, and looked at by a
   human who writes down what is wrong with it.
7. `smoke-loop.sh` PASS against Cloud Run.
8. A TestFlight build installed by someone who is not the developer, and their
   report carried verbatim in the Phase 8 record.
9. `phase8-test-baseline.txt` written, and `phase7-test-baseline.txt`
   corrected — see §2.1.

---

## 2. What this design found that the record does not say

### 2.1 Two of Phase 7's four open gates are already closed

Measured at `develop` = `aa36c31`, not read from a document.

| Gate | `phase7-followups` §1 | Measured |
|---|---|---|
| `TheTrackedCapturesAreCurrent` under `0.4.0` | blocked on Task 2 | **green.** `dotnet test Broodline.sln` reports 0 failed, 0 skipped. Commit `e217bea` took the capture |
| The determinism gate run in CI | blocked on Task 12 | **run.** Runner `sd-sassadi-m1` is registered and online; run `35238465972` was green in 7m58s cold, 500 corpus scenarios byte-identical across CoreCLR and IL2CPP. The first execution of the IL2CPP half of that diff in CI, ever |
| `smoke-loop.sh` PASS against Cloud Run | blocked on Task 19 | **still open.** `gcloud run services list` → `Listed 0 items.` The script does not exist |
| A TestFlight build in someone else's hands | blocked on Task 20 | **still open.** No `BootBuilder.cs`, no `ExportOptions.plist` |

**Phase 6 is therefore closed**, and this design is the first document in the
project entitled to say so.

**`phase7-test-baseline.txt` is stale against its own tree.** It records
`gate.dotnet.failed 1` with `TheTrackedCapturesAreCurrent` annotated
*DELIBERATE*, and the tree measures zero failures. The cause is ordinary and
worth naming: the baseline was last written by `ec06d40`, and `e217bea` — the
capture — landed **after** it. The file whose stated purpose is to supersede any
count written into a sentence has become a count written into a file that
nothing re-measured.

**This is not a documentation chore and it must not be booked as one.** A
baseline that says a gate is deliberately red, when it is green, teaches the next
reader to expect a red board and to ignore it. That is how a real failure gets
through. Correcting it is Task 0.

### 2.2 The token layer has no face, and that is most of "rough"

`Tokens.uss` landed in `6b76a0f`, the final commit of Phase 7, and it is
faithful — every hex is copied from the handoff. What it has no answer for is
that **no font exists in the project.** There is no `.ttf` anywhere under
`client/Assets`. `Theme.uss` says so in its own header, and says it plainly:
sizes and weights follow the handoff's type table "so the scale is right the
moment a face is imported; until then everything renders in Unity's default
sans."

Two consequences follow, and the second is the one that matters.

The register is wrong — the handoff pairs a rounded display face with a humanist
sans, and Unity's default is neither. And **bible §10.6's tabular-figures
requirement is unmet**, which is not an aesthetic complaint: the interface is
dense with odds, timers, yields and countdowns, and §10.6 says misread stats
erode trust. A proportional numeral set makes every ticking number jitter.

### 2.3 Three handoff primitives USS cannot express, and only one was solved

`Theme.uss` documents all three honestly. The physical CTA press was
reproduced with a bottom border that collapses under a 2px translate — same
silhouette, same motion. The other two were not solved, only recorded:

- **`box-shadow` does not exist in USS at all.** Card elevation is currently
  carried by surface/paper contrast and radius alone.
- **`linear-gradient` is not available to `background-color`.** The primary CTA
  is a solid `--violet` sitting between the handoff's two stops.

Both are solvable the same way — with a texture — and the reason they were not
solved in Phase 7 is that shipping a texture for a six-hex-step difference is
poor value **when it is the only texture in the build.** Phase 8 ships an icon
sheet regardless, so the marginal cost of a shadow nine-slice and a 2×64 ramp
is close to zero. The trade that was correct in Phase 7 is no longer correct.

### 2.4 The art slots already exist

`CreatureCard.uxml` carries an empty `silhouette` element. That is exactly the
contract the handoff bundle states: *"every slot is sized and positioned for
final art and can be swapped without layout changes."* Interim proxies drop into
a hole that is already the right shape, and Phase 9's production assets replace
them without touching layout.

### 2.5 The least tokenised file in the project is the combat HUD

`WaveHudView.uss` carries five raw hex values. Four are verbatim copies of token
values — `#e5867a` is `--coral`, `#7cc492` is `--green`, `#6ba7c0` is `--teal`,
`#ffb703` is `--amber-bright` — and the fifth, `#ff6b5c`, is a colour that is not
in the system at all. It was authored in Phase 7 Task 16, before `Tokens.uss`
existed in the branch.

This is the evidence that the token lint in §7 is a real gate rather than a
hypothetical one: it has something to catch on the day it is written.

---

## 3. The render pipeline ruling

**3D throughout, generated rather than commissioned.** Taken 2026-09-17.

The alternative considered and rejected was 2.5D — AI-generated flat art on
billboarded quads. The comparison is worth recording because the reasoning is
not obvious and the decision is reversible under a condition `rig_proof.md`
already names.

**What the alternative would have cost, measured rather than estimated.**
`SyntheticCreature.cs`, `BoneAnimator.cs`, `WaveView.Build()` and the
`client/Assets/Benchmark/` harness — under three hundred lines. `WaveView.Render()`,
`WaveSnapshot.cs` and `WaveClock.cs` are snapshot interpolation and touch no
mesh. The engine, the corpus baseline, the determinism gate, all of
`services/api` and every UI Toolkit screen are untouched in either direction.

**What it would have retired.** The ten-thousand-triangle budget and the owed
real-mesh re-run of the entity harness; `rig_proof.md` wholesale; and the
render-side justification for the A14/4 GB device floor — which would not have
reversed that decision, only removed the reason it was taken.

**What decided it.** Not visual quality. The wave camera is orthographic and
locked, and entities only translate along a lane, so a mesh and a billboarded
sprite are indistinguishable in a still frame. The difference is the pipeline —
eighteen rigged meshes with sockets that align across every body/part pair,
versus eighteen PNGs with anchor points — and the Splice Reveal, which can turn
the creature under one and cannot under the other.

**The consequence for Phase 9 is that `rig_proof.md` needs no edits.** It
specifies ten demonstrations, forty-eight renders and pass/fail acceptance
criteria, and it never says who makes the assets. It works unchanged as a brief
for a generative pipeline.

**And the ruling has a written escape hatch.** `rig_proof.md` §6: *"Fail item 6
— growth needs separate assets rather than a proportion curve ... If growth
cannot be a curve, re-open the 2D question, because the reasoning at bible §10.3
no longer holds."* That condition is the one to watch, and Phase 9 owns it.

---

## 4. The visual foundation

Six units. Each has one purpose, its own gate, and can be built and reviewed
without the others.

### 4.1 Fonts

Baloo 2 for display and numerals, Nunito for body and UI. Both SIL OFL.
Imported as Unity FontAssets, bound through a `--font-display` / `--font-body`
token pair and applied on `Theme.uss`'s existing class vocabulary, whose sizes
and weights already follow the handoff's type table.

**This is first, because its gate can fail and the fallback changes the work.**
The gate reads digit glyph metrics from the FontAsset and asserts
`horizontalAdvance` is equal across `0` through `9`. That is bible §10.6's
tabular-figures requirement expressed as an assertion for the first time.

If Baloo 2 fails it — plausible for a rounded display face — §10.6 already
prescribes the remedy and it is not a judgement call: *"keep it for titles and
CTAs and pair a tabular face for numerals only."* The failing measurement is
recorded rather than discarded, because the next person to consider changing the
display face needs to know this was tested.

The second half of §10.6 — an 11px floor on any number a decision depends on —
gets its own assertion over resolved font sizes on numeric label classes.
`Theme.uss` already honours it; nothing currently stops it being broken.

### 4.2 Elevation

One soft-shadow PNG, nine-sliced, behind cards, exposed as `.elev-1` and
`.elev-2`. This closes the first of §2.3's two unsolved primitives. It is the
single largest visual change in the phase: on a light theme with pure-white
surfaces on near-white paper, elevation is most of what separates a card from a
rectangle.

### 4.3 The CTA gradient

A 2×64 violet ramp as `background-image` on the primary CTA, restoring
`linear-gradient(180deg, #8878cf, #6f5fbb)`. The press state stays exactly as
`Theme.uss` built it — it is correct and bible §10.6 names it specifically.

### 4.4 Icons

Five navigation glyphs at 19px, 1.9 stroke width, no fill — the handoff
specifies those numbers exactly — plus the glyphs the screens need: back
chevron, three currency marks, timer, lock, check, warning.

Lucide (MIT, stroke-based, geometrically close to the handoff's spec) as the
base set. Splice helix, gene shard and ark hex drawn to match, because no
general icon set carries them.

**Rasterised to a 3× sprite sheet rather than imported as vectors.** UI Toolkit
can consume SVG through the Vector Graphics package, and Phase 7 deliberately
removed two unused packages from the client build in `ad7ff3c`. Adding one back
for glyphs that never scale past 24px is not a trade worth making.

### 4.5 Motion

`Motion.uss` — screen push and pop, sheet slide, selection ring, number tick,
reveal flare — as a class vocabulary over USS transitions. The handoff names an
animation set and bible §10.6 makes it authoritative. The CTA press is already
built and is not re-implemented.

### 4.6 The species palette fix

`broodline_accessibility.md` §4: under deuteranopia and protanopia Ember and
Loam converge, and Vetch and Hollow converge; under tritanopia Skitter and Loam
move closer. The remedy is stated there — widen the lightness separation so the
pairs differ in value as well as hue.

**This is in Phase 8 and not Phase 9 because §4 says when it has to happen:**
*"it must happen before the Character Bible is finalised, because species colour
is on the body, the card, the map marker and the store icon."* Phase 9 generates
bodies. After that, the fix is a repaint of every asset.

The gate runs each pair through deuteranope, protanope and tritanope simulation,
computes CIE L\*, and asserts a minimum separation. It exists so that the next
person who adjusts a species colour for aesthetic reasons finds out immediately.

---

## 5. The screens

### 5.1 Composition, not twelve rewrites

The handoff specifies one layout frame that every screen shares:

```
status bar → header → [tab bar] → content (flex: 1, scrolls) → CTA row → footer note → bottom nav
```

Nothing in the codebase provides it. Each screen is a bare `VisualElement`;
`Shell.uxml` supplies only the top bar, the host, the sheet layer and the tab
bar slot. So the first unit is **`ScreenScaffold`** — header with optional back
chevron and avatar, scrolling content region, CTA row, footer note — and every
screen composes it. Eleven screens become structurally correct in one change,
and the twelfth (§5.3) becomes correct chrome around interim content.

`ScreenScaffold` does **not** own navigation. `ScreenHost` already enforces the
rule that pushed sub-screens hide the tab bar, and `client_architecture` §9 is
the source of it. The scaffold renders a back affordance when it is told to and
asks `ScreenHost` to pop; it does not decide depth.

### 5.2 The shared component layer

Built once, used everywhere, in the pattern `client/Assets/UI/Components/`
already establishes:

- **`OptionRow`** — the handoff's selected and unselected treatments, specified
  to the pixel: `inset 0 0 0 2px #8878cf` with a white fill against
  `inset 0 0 0 1px #ece7f6` with `#f7f5fb`. Used by splice parent selection,
  campaign chapters, and every route and option list in Phases 10–13.
- **`SectionCard`** — the card surface with its radius, padding and elevation.
- **`StatCell`** — the three-cell row the handoff uses on almost every detail card.
- **`ProgressBar`** — richness, growth, integrity.
- **`EmptyState`** — currently absent, and every list in the game can be empty.

The existing five — `ConfirmDialog`, `CreatureCard`, `CurrencyHeader`,
`TimerChip`, `TraitPip` — gain the foundation rather than being rebuilt.

With the scaffold and these in place, per-screen work is layout and copy.

### 5.3 The eleven, and the one

Brought to handoff specification: `FounderNamingView`, `CampaignSelectView`,
`RosterView`, `SpliceChamberView`, `SpliceRevealView`, `LineageView`,
`CodexSheet`, `DeployView`, `WaveHudView`, `PostWaveView`, `WaveDefeatView`.

`WaveHudView` takes the most work, for the reason §2.5 gives.

**`RegionView` is the exception and it is recorded as deferred, not shipped.**
Its own stylesheet already says what it is: *"this phase draws rows rather than
polygons."* The handoff's map needs region geometry, five yield states, Ark pins,
ally and hostile markers, animated collector routes and an apex vein pulse.
`/v1/region/state` returns claimable node slots; `config/bundles/0.1.3/nodes.json`
is two node types with hourly rates; there is no geometry anywhere and no
relocation or collector system to drive the overlays.

Phase 8 gives it a designed node list with the foundation applied. The polygon
viewport is Phase 10, with the systems that populate it.

### 5.4 Interim creatures

Six species proxies built from bible §1.2's silhouette column — Vetch a low dome
on four stubby legs, Hollow a tiny body on stilt legs with a forward neck, and so
on for Ember, Skitter, Loam and Pale. Flat sprites in the `silhouette` slot on
cards; primitive-assembled in the wave scene.

**They are not throwaway and they are not production art.** The handoff's own
placeholder policy is the model: geometric silhouettes in sized slots, swapped
without layout changes.

**And they double as a pre-test of the art direction.** Bible §10.2 rule 1: all
six species must be distinguishable as flat black shapes at 40px, and if two are
confusable one of them is wrong. Rendering the proxies and asserting pairwise
distinctness answers that question **before Phase 9 generates a single asset** —
which is the same argument `rig_proof.md` §5 makes for putting item 7 in the
proof at all. If it fails, the finding is a design finding and goes back to the
bible, exactly as `rig_proof.md` §6 routes it.

### 5.5 The Editor harness

A window that renders any screen against fixture data, with no server and no
session. Two reasons, and the second is the larger one.

Iteration: at present, seeing a screen means running a local Postgres, publishing
a bundle, starting the `api`, pressing Play in `Boot.unity` and walking the beat
machine to the screen in question. That is the loop Phase 7 Task 17 Step 6
describes, and it is why that step was not run.

And it is what makes §7's screenshot corpus possible at all.

---

## 6. The ship track

Ordering is forced: §6.1 before §6.2, because the release build bakes the API
URL at build time and a release pointing at `localhost` is the failure
`BootBuilder` is specified to prevent.

### 6.1 The deployed stack

`terraform apply`, the migration, the one `servers` row that a deployed database
needs and nothing creates, bundle `0.1.3` published, `api` and `sim` deployed.

Then the loop driven against it end to end. **`implementation/scripts/smoke-loop.sh`
does not exist** — the directory holds `smoke-wave.sh`, which drives a wave and
not the loop — so writing it is part of this work, not a precondition of it.

The billing ruling is taken: the Terraform is deployed as written, at the
`$25–50/month` `broodline_solo_execution.md` §3 already costed, with
`db-f1-micro` as the only always-on component. **Billing begins at `apply`**, so
this is sequenced as late as the ordering constraint allows.

### 6.2 TestFlight

`BootBuilder.cs` in the `WaveBuilder` shape but release: `BuildOptions.None`,
`productName` set, `BROODLINE_API_URL` written into the config and **throwing if
unset**. `ExportOptions.plist` for `app-store-connect` with symbol upload.
`IosFileSharingPostProcess` returns early unless `EditorUserBuildSettings.development`,
as its own header asked and no release build has yet honoured.

Then archive, export, upload, and an **internal** group — no Beta App Review.

**And then someone who has not seen the app installs it and plays the first
hour.** They report where they got stuck, whether they reached the Lineage View,
and how long it took. **That report goes into the Phase 8 record verbatim.** It
is the first playtest signal this project has ever had, and
`broodline_whats_left.md` §7 says the next document worth writing is the one
that records what the first playtest found.

---

## 7. Testing

### 7.1 What gets a gate

| Gate | What it catches |
|---|---|
| Digit `horizontalAdvance` equality across `0`–`9` | Bible §10.6's tabular figures, asserted rather than assumed |
| Resolved font-size ≥ 11px on numeric label classes | §10.6's decision-bearing-number floor |
| `verify-uss-tokens.sh` — no raw hex outside `Tokens.uss` | §2.5's five hexes, and the next one typed |
| CVD simulation + CIE L\* pairwise separation | Ember/Loam and Vetch/Hollow re-collapsing |
| Reflection over the UI assembly for `ScreenScaffold` composition | A later screen skipping the layout frame |
| Pairwise difference of six proxies rendered 40×40 black-on-white | Bible §10.2 rule 1, ahead of Phase 9 |
| The existing suites, unchanged | Regression across all of the above |

`verify-uss-tokens.sh` joins `verify-unity-settings.sh` in `tests.yml`'s
`client-settings` job. It inherits that script's known limitation, stated here so
nobody upgrades it later: a shell gate that is deleted reddens nothing. Only the
job running it at all is automatic.

### 7.2 What cannot be gated

**Whether it looks good.** Elevation weight, spacing rhythm, motion feel, icon
quality. None of it has a test, and a phase that claimed otherwise would be
repeating the failure this project's records exist to prevent.

The substitute is **a screenshot corpus**: the §5.5 harness renders each screen
against fixture data and a batch method captures all twelve to PNG. It is the
eyes-on artifact, a diffable record across commits, and the thing a reviewer is
actually sent.

Two things are stated rather than assumed. Headless UI Toolkit capture needs a
graphics device; it is workable on a real Mac and it is engineering with a
failure mode, not a freebie, and the fallback is that the harness stays a manual
tool. And tracking follows the `results/` table's existing rule rather than a new
one: **if capture is automated the PNGs are not tracked**, because they are cheap
to regenerate; **if it stays manual they are**, because a human at a keyboard is
precisely that table's criterion.

### 7.3 The eyes-on pass is a task, not a checkbox

Phase 7's single eyes-on step — Task 17 Step 6, walking beats 1–8 against a local
`api` — was not run. The implementer reported that honestly rather than claiming
it.

In a phase whose entire deliverable is how the game looks, an unrun eyes-on step
is not a minor omission. It is numbered, it names its artifact, and the artifact
is a written list of what is wrong with the screenshots.

---

## 8. Sequencing

1. **Task 0 — the record.** Re-measure `phase7-test-baseline.txt`; correct
   `phase7-followups` §1's four-open-gates table. §2.1.
2. **Fonts**, first among the foundation, because the gate can fail and the
   fallback changes the work. §4.1.
3. The rest of the foundation — elevation, gradient, icons, motion. §4.2–4.5.
4. **The palette fix.** Before anything generates an asset. §4.6.
5. `ScreenScaffold` and the component layer. §5.1–5.2.
6. The eleven screens; `RegionView`'s interim. §5.3.
7. Proxies, and the 40px assertion. §5.4.
8. The harness and the screenshot corpus. §5.5, §7.2.
9. **The eyes-on pass**, and the fixes it produces. §7.3.
10. The deployed stack. §6.1.
11. TestFlight, and the tester's report. §6.2.
12. `phase8-test-baseline.txt`, and the Phase 8 record.

The palette fix at step 4 is the only ordering constraint inside the foundation
that is not about convenience: it gates the art track, which runs in parallel
from the start of this phase.

---

## 9. What this design deliberately does not do

**It does not build the world map.** §5.3. The systems that populate it do not
exist, and a polygon viewport over two node types would be a mock, not a screen.

**It does not produce creature art.** Phase 9 owns that, against `rig_proof.md`
unchanged. Phase 8 ships proxies and the pre-test that de-risks them.

**It does not schedule audio.** `broodline_audio.md` exists and no phase in the
roadmap owns it. Recorded here as unscheduled so that it is a decision rather
than an omission.

**It does not start the moderation filter procurement**, which
`broodline_whats_left.md` §3 lists as item 1 and says should already be in
motion. That is a vendor lead time feeding Phase 12, and it wants starting
independently of this phase rather than inside it.

**It does not narrow the determinism gate's triggers.** They are already
path-filtered to `engine/**`, `tests/engine/**` and the gate's own machinery.
Phase 8's work under `client/Assets/UI/**` will not trigger it, and the one path
it does touch — `ProjectSettings.asset`, for `productName` and `buildNumber` —
should trigger it.

**It does not re-open the A14 device floor.** The 3D ruling in §3 leaves the
render-side justification intact.

---

## 10. Decisions taken, and the one that is reversible

| Decision | Taken |
|---|---|
| **3D throughout**, assets generated rather than commissioned | 2026-09-17. §3. **Reversible** under `rig_proof.md` §6's item-6 condition, and only that one |
| **The full twenty-screen game is the destination**, built out over Phases 10–13 with their backends | 2026-09-17 |
| **Ship a build now, then ship every phase** | 2026-09-17. Milestone one is this phase |
| **Interim creatures in build one**; production art lands in build two | 2026-09-17 |
| **Deploy the Terraform as written**, at the cost `solo_execution` §3 costed | 2026-09-17. §6.1 |
| **`RegionView` is deferred, not descoped** | §5.3 |
| The screenshot corpus is tracked only if capture stays manual | §7.2 |

---

## 11. Errata, found in execution

*Recorded 2026-09-17 by Phase 8 Task 19, against the tree at the end of the
phase. These are defects in **this design document**, found by implementing it.
They are written here, where they were written, rather than quietly
reimplemented somewhere else.*

**§4.6's palette gate was not implementable as specified** and would have
passed while the palette got worse. It asks the gate to "compute CIE L\*, and
assert a minimum separation". There is no minimum available to assert: 45
pair/deficiency constraints against 6 free colours, and the worst pair —
Vetch/Loam at ΔE 10.5 under tritanopia — is one that deleting Pale outright
would not improve, because Pale is not in it. Roughly ΔE 10–12 is the floor for
six saturated hues in this family however they are arranged, so any threshold
low enough to pass today is low enough to pass a materially worse palette.
Task 6 replaced it with a **tracked baseline over all 45 measurements**, which
fails when any number gets worse on either channel — the question a change can
actually answer. See that task and `implementation/results/palette-decision.md`.

**§4.6 also ranked the palette by the wrong quantity, and so did
`accessibility.md` §4.** Both ranked by lightness (ΔL\*), which does not rank
confusability — here it inverts it. §4 scored **one for three**: Ember/Loam is
real (ΔE 11.0 deutan), Vetch/Hollow does not collapse (26.9), and Skitter/Loam
under tritanopia is 63.0, among the best-separated pairs there is. This design's
own nominated worst pair, Skitter/Pale, was ΔE 71.4. **The real worst pair was
Vetch/Pale at ΔE 7.2** — unnamed by both documents, and the closest pair at
normal vision too. `accessibility.md` §4 is corrected; §4.1 there carries the
scorecard.

**Two corrections to Task 6's drafted maths, both measured.** The Machado
matrices are applied to **linear** RGB, not gamma-encoded sRGB — the draft did
the latter, which is a known, named defect with a history (fixed in R
`colorspace` 2.1-0 after Matthew Petroff reported it). And the draft's
"Ember/Skitter 15.2" was 12.8; its other option-B numbers reproduce exactly.

**Pale moved: `#a9b0c4` → `#c6cede`.** The only species colour this phase
changed. `bible §1.2`, `accessibility.md §4` and the design handoff README were
all stale on that one row and are corrected as of this task, along with §4's
final paragraph, whose prescription was costed as option B and retired.

**The palette decision §4.6 deferred is now closed, and no colour moved.**
Hollow is exactly `--violet` (every CTA) and Skitter exactly `--amber` (every
warning and Founder marker), at ΔE 0.0 for every viewer. No simulation can see
it, because it is not a confusion between two species. Task 13 decided it
against a rendered card rather than a hex table, and the render changed the
answer: the defect was `LineageView` carrying **Founder** and **Mutated** on
colour and nothing else — a bible §10.4 violation that existed before any
species had a colour. That screen now says both in words. See
`implementation/results/species-collision.md`.

**§7.1's "resolved font-size ≥ 11px" gate was not implementable in
`Broodline.UI.Tests`**, which has no attached `Panel` — nothing resolves, so
there is no resolved font-size to read. Implemented in `verify-uss-tokens.sh` as
text analysis against the `.t-num` marker, which catches the violation in the
stylesheet rather than in one instantiated tree.

**§5.3's map deferral note says "there is no geometry anywhere", and that is
wrong.** `specs/broodline_region_graph.md` authors **thirty regions across three
rings with full adjacency** and forty-three edges. What does not exist is
**coordinate** geometry — no region has an x/y or a polygon, which is what a
drawn map needs. Worth stating precisely, because "no geometry" invites someone
to author the graph that already exists. A second mismatch sits beside it: the
handoff's Splice World Map v2 draws **eight** polygon regions where the roster
authors **thirty**, so Phase 10 owes a reconciliation, not just a renderer.
