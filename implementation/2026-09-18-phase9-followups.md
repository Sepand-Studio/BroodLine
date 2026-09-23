# Phase 9 — What Execution Left Behind

*The durable record of the look phase: what it proved and on what, what it left
owed and with what weight, and — because this phase corrected eight false
comments, three of them inside the commits fixing other ones — what it found
wrong in its own plan, its own design, and its own prose*

> **Why this file exists.** The working ledger that recorded these findings —
> `.superpowers/sdd/2026-09-18-phase9-the-look/progress.md` — lives in
> git-ignored scratch and will not survive a `git clean`. This is the durable
> copy, and for most of these items it is the only one. Written to the same
> contract as `2026-09-17-phase8-followups.md`, which it should be read beside.
>
> **State at the time of writing:** the work is on `phase_9` at `68bad4b`, and
> the exit gate (§8 of the design) **ran and returned NO** with one named gap.
> That verdict is §1.4 and it is the state this file describes. **There is no task
> count in this paragraph and no blanket claim that every task closed**,
> deliberately: the phase ran 0 through 21i with several multi-round fix cycles,
> two developer checkpoints that were not engineering tasks (Task 12, the Vetch
> stop; Task 21, the walk), and one task — 3, the deployed smoke loop — that was
> blocked on a credential, deferred to run with Task 20, and finally passed with
> a caveat that is recorded in that task's report and not repeated here.
> `git log --oneline develop..phase_9` is the whole answer and
> `.superpowers/sdd/2026-09-18-phase9-the-look/` holds the per-task reports.
>
> **Counts are not quoted in this prose.** The EditMode baseline lives in
> `implementation/results/phase9-test-baseline.txt`, which supersedes any number
> written into a sentence anywhere, including this one. §7 lists the counts this
> record could *not* reproduce, which is a different thing and belongs in prose.
>
> **The single most important structural fact in this file is §1.** Phase 8's
> record could say "a human walked it". Phase 9's cannot say "a human held it".
> Everything the gate established, it established by driving the **packaged app
> in the iPhone 17 Simulator at 402×874 points** — the frame the game ships at,
> which is not the 430 the capture corpus renders. **No build of this app has
> run on physical hardware.** The phase's working ledger has one heading reading
> "CONFIRMED ON HARDWARE" directly above a line reading "Controller drove the
> **simulator** directly", and the controller's own notes for this task repeated
> the wrong half. It is corrected here, and §1.3 says what that costs.
>
> **This record is deliberately not a success report.** §4 enumerates
> twenty-seven premises this phase's own plan, briefs and controller asserted and
> that an implementer or reviewer refuted by measuring. §5 records eleven method
> findings that outlive the phase. §7 records three counts in the controller's
> own notes that this task could not reproduce. A record that read as uniformly
> successful would be false, and the reason it would be false is §4's lesson.

---

## 1. What is known, and on what

This section exists because the phase's most valuable lesson is about the
difference between three kinds of "it works", and a follow-ups document that
flattened them would destroy the finding.

### 1.1 Known from driving the built app at the shipping frame

**Four walks of the packaged app, iPhone 17 Simulator, 402×874 points, the last
on `0267682`.** Each of the recovery walks used one control that makes the result
trustworthy: **the app was suspended and resumed under the same PID** (10473
before and after on walk 3; 11713 before and after on walk 4), so what was
exercised is a resumed process and not a relaunch.

These are established:

- **The walk recovers from a wave abandoned by backgrounding.** The full
  sequence ran: Interrupted → Try again → roster reload → abandon sheet →
  Forfeit → a playable deploy screen with a freed roster.
- **The player is told an authored sentence, not a developer's.** `InterruptedView`
  carries `ServerError`'s `"The game hit an unexpected problem. Try again."`
  (`client/Assets/UI/ServerError.cs:395`), not the `TimeoutException`'s text.
- **The notice toast clears the Dynamic Island.**
- **The dead band is fixed, proven positively rather than by absence.** A tap at
  `(310, 790)` — *inside* the measured dead band 782.1 → 798.6, a coordinate
  class provably dead on two earlier walks — completed the forfeit and returned
  a freed roster in seconds. The Forfeit button's bottom corners became visible
  for the first time, which is the same change seen from the other side.
- **The lane draws, and its creature count matches the deploy screen.** Two
  bodies for `DEPLOYED 2/5` on walk 3; three for `3/5` on walk 4, including the
  newly named founder. Task 21f's suspected "one slot drawing two bodies" does
  not reproduce.
- **Nothing regressed under the sheet-layer reorder.** On walk 4,
  `FounderNamingView`'s "Not now" at `(201, 775)` — the lowest interactive
  element in the app — advanced the walk; `DeployView`'s Start at `(201, 764)`
  started wave 2; `InterruptedView`'s "Try again" worked.
- **Post-Wave reads "Wave held"** (`client/Assets/UI/WaveScreens.cs:375`), not
  the screen's own name; no chip reads "None"; the founder card renders whole.
- **Unity's Development Console still paints in a non-development simulator
  player, and three rounds of setting `developerConsoleEnabled`/`Visible` did not
  suppress it.** Walk 4 was the first *valid* test of this — see §5.2.

### 1.2 Known only in a suite, an instrument or a render

Everything else this phase claims. The EditMode suite, the PlayMode suite, seven
companion script gates, the 21-fixture capture corpus, the 48-tile contact sheet
and the Cinderplate render. All of it is real evidence and none of it is the
above: **four of this gate's defects were invisible to 525 EditMode tests and the
whole capture corpus** (§5.1).

One distinction inside this tier is worth keeping. **PlayMode is seven tests and
they run only from the Editor's Test Runner** — `run-unity-tests.sh PlayMode`
deadlocks on this Editor and the script says so in its own comment rather than
routing somewhere that would report a false green. They were unrun for most of
the phase; the developer ran them at the exit gate and reported 7 of 7, one
reddening first on a stale assertion rather than a regression. **That figure is
the developer's report, not a re-run artifact**: `test-results-PlayMode.xml` in
`implementation/results/` is from 2026-09-17, i.e. Phase 8.

### 1.3 Not known at all, and what that costs

**No build of this app has run on physical hardware.** The consequences, each
already load-bearing somewhere in the phase:

- **The console ruling rests on `strings`, not on a device.** `Diagnostics.cs`
  says so in its own header and calls it "evidence and not proof". The finding
  (§5.2) is strong and it is still an absence found in a binary.
- **"Device-only defect" in this record means "found only by driving the built
  app", not "found on a phone".** Both readings imply the same gap in the gates;
  only one of them is true.
- **TestFlight, the phase's own exit gate, is not met.** §1.5.

### 1.4 The exit gate ran and returned NO

`implementation/results/phase9-visual-review.md` is the walk record, written
against the deployed stack with a human doing the walk. Four of the design's five
checklist items are green. **The verdict is NO on one named gap: "the lane is
empty".**

The gap is unclassified *deliberately*, and that judgement should be respected
rather than tidied: the same six words describe two unrelated failures in
unrelated files, and the walk did not record which screen they were said about.
Both readings were investigated and neither was ruled out; two controller
hypotheses were tested and discarded on the way, and are recorded in that file so
nobody re-runs them. **What settles it is one look, and the only question is
which screen.** The fix-up cannot be written until the screen is named.

**This item is the only thing in this document that blocks the phase's own
exit.** See §2.1.

### 1.5 There is no tester's report, and this section is where it would be

The design's DoD item 10 — Phase 8's item 10, carried in Phase 8's words — asks
for an archive uploaded, a tester who has not seen the app, and their report
here, verbatim. **No archive was built and no tester was recruited.** `BootBuilder`
and the release Xcode project exist (`build/ios-release/`); the release
frameworks were built and read with `strings` for §5.2, and that is the whole of
what the release path has produced.

**There is no heading below this paragraph waiting to be filled.** That is the
same choice Phase 7 and Phase 8 made and it is made for the same reason: a
heading with nothing under it reads like a thing that was tried. It is also this
phase's own §5.2 rule — *record an unrun test as unrun, never as a pass* — applied
to the record itself.

---

## 2. The punch list — what is owed, by weight

**Not a flat list.** The items below differ by an order of magnitude in what they
oblige the next session to do, and the previous phase's record is where that
distinction was lost. Task 20's triage is the standing ruling on which of the
pre-gate items block merge; the gate's items are weighted here.

### 2.1 Blocks the phase's own exit gate — one item

**"The lane is empty", unclassified.** §1.4. It needs one look at a running app,
and the look must record *which screen*. Two readings, each with its own file:

| If it was… | Then it is | Evidence for | Evidence against |
|---|---|---|---|
| the **deploy card**'s lane | Task 17's unclosed `LaneStage`→`LanePreviewCard` join | Task 17's own report says the join is "proven in the Editor's batchmode render path only" and "nobody has yet looked at the lane picture"; the card falls back to a flat `--green-tint` fill, which looks exactly like empty | The wiring is present — `BootController` creates the stage with `LaneStage.Create` and passes it into the director, which hands `ShowLane(waveId, deployment)` to the view as `lane:` — and `LaneStagePlayTests` passed on the developer's run. (The visual review cites `BootController.cs:129` and `FtueDirector.cs:353`; both have since moved to `:238` and `:672`, so search the call, not the line) |
| **during the wave** | `LaneDressing` not built or not seen by the camera | — | The dressing is not subtle: ΔE against the field `#e8f5ec` is 9.29 (trees), 13.14 (path), 8.02 (dashes) and **72.39** (the Ark cylinder). A violet cylinder cannot hide on pale green |

**Walk 3 and walk 4 both drew the deploy lane correctly** (§1.1), which is the
strongest single piece of evidence in either column and arrives from a later walk
than the verdict. It is not decisive — the walks and the verdict ran different
builds — but the next look should start by asking whether the deploy card is
still the suspect at all.

### 2.2 Unowned defects — nobody has taken these

| Item | Where | Why it is unowned |
|---|---|---|
| **`MainThreadAffinityTests`' deliberate red fires against a test file.** The permanent failure names `SessionTests.cs:223`, `:238` and `:262`. **The rule's point is `FtueDirector.cs`** | `client/Assets/Game/Tests/MainThreadAffinityTests.cs`; the baseline file | A permanent red aimed at the wrong target trains its reader to stop looking. Inherited from Phase 8 §2.5, which argued it correctly and still left it pointed here |
| **`WaveHost.CompletionTimeoutSeconds` counts wall clock**, so a backgrounded app is treated as a hung wave | `client/Assets/Game/Shell/WaveHost.cs:48`, `:213` | Survivable — the recovery path now works and is walked — and still wrong. It is the mechanism that *produced* the walk this phase spent four device walks on |
| **`CampaignAsync` has no clean terminal.** A content-exhausted player — a legitimate end — is shown the *error* screen and a "Try again" that re-shows it | `client/Assets/Game/Ftue/FtueDirector.cs`, `CampaignAsync` | Human-gated, so it is a mislabel rather than a spin. The real terminal is still missing. §4.2 has the measurement that found this |
| **`ScreenFlow` hides no view when a turn resolves**, so the portrait studio clears while the screen presenting it is still up | `client/Assets/Game/Shell/ScreenFlow.cs`, `TurnAsync` | Task 14b shortened the window (`6bd1b3a`) and could not close it. The fix is a change to a class every presenting beat shares — **eleven `_flow.Show*`/`ShowSheetAsync` call sites in `FtueDirector` today**; Task 14b counted six beats at the time. Worth ruling on before a fourth hero moment is wired to a live studio; the design says there are three |
| **`CodexSheet` is unreachable at runtime and is in the capture corpus.** The corpus has been certifying a screen no player can reach | `client/Assets/Editor/ScreenFixtures.cs:81` | **Not new**, and the controller's framing of it as a fresh find was wrong: already recorded at `implementation/2026-09-15-phase7-followups.md:910` — "`CodexSheet` has **no production caller**" — as one of five unwired components |
| **`DeployView` reads `Wave 7` where `WaveHudView` reads `Wave 6 / 12`** | `DeployScreen.WaveStatValue`; `WaveHudScreen.WaveOf` | Confirmed real by Task 20 and **the only one of its triage items visible on the user's walk**, so it was escalated for a ruling rather than fixed. It changes a rendered string after the baseline capture, `WaveStatValue` has three callers (one of which, `IncomingHeading`, must **not** gain "/ 12"), and two tests pin it. It wants its own capture |
| **`BootController.OnTabSelected` is empty** | `client/Assets/Game/Shell/BootController.cs:335` | Inherited from Phase 7 and deliberately documented there as "the largest limitation of this build". It is now load-bearing in a new way: it is what makes a near-miss on a CTA feel broken, and it is why the sheet-layer scrim change is unobservable today. **A trap for whoever implements tab navigation** — see §2.4's band-pick entry |
| **Seven runtime error logs outside `FtueDirector`/`BootController` are unconverted to `Diagnostics.Defect`** | see §7.3 | A project-wide severity sweep is a diagnostics-policy decision, not a fix. **The count of seven is one this record could not reproduce; §7.3 has what was measured** |

### 2.3 Design calls already ruled on — do not re-open as defects

| Item | The ruling |
|---|---|
| **The 4.1-point CTA clearance on ten screens.** `.screen-scaffold__cta-row` has no bottom padding, so the only gap between a primary CTA and the tab bar is a `Button`'s own `margin-bottom: var(--space-1)` | **Not a defect.** It cannot become an overlap — the slot is in-flow — and a 55-point button clears the 44-point minimum. Re-tuning it is a design call. It does mean a thumb aimed at the bottom edge of any screen CTA is 4 points from chrome that does nothing, which is §2.2's `OnTabSelected` entry from the other side |
| **Skitter's 3.08 dorsal/flank curvature ratio**, the worst of the six bodies | **A recorded trade, not a defect.** Moving its flank socket costs a re-bake and a re-measure of a silhouette gate whose closest pair has under two points of margin |
| **Loam's "segmented" does not read at 40px.** The committed mesh keeps all four waists at 3–9% where the recipe's field has them at 15–21% | **Accepted and recorded in the recipe.** Resolving them costs grid 22, which meshes to 3296 triangles against a 2500 budget. `Loam_IsSegmented_MeasuredOnTheRecipesOwnSurface` therefore measures the **field**, and the segmentation is honestly a read on the card render rather than in the silhouette. `client/Assets/Creatures/Recipes/SpeciesRecipes.cs:320-331` |
| **Two sheets' bottom inset** | **They agree** — both `--space-6` top/bottom and `--space-4` sides (`CodexSheet.uss:75-78`, `AbandonedWaveSheet.uss:18-21`). The controller's "they disagree" was refuted; the 12-point difference is `.elev-2`'s padding, which `CodexSheet` deliberately omits and already records. Nothing asserts they should agree, which is the only live part of this |

### 2.4 Follow-ups with a named blocker

| Item | Blocker |
|---|---|
| **`probe-shell.sh` is unwired.** Nothing forces the shell probe to run | It has a named home — `determinism.yml`'s self-hosted macOS job, which already runs the EditMode suite and the stylesheet check (`.github/workflows/determinism.yml:163`) — and a named blocker: **that workflow is red on `develop` and unowned.** Confirmed: `probe-shell.sh` appears in neither workflow |
| **The band pick asserts sheet-ownership, not that the element under the band is wired to dismiss** | Shrink `ConfirmDialog.Standard`'s scrim to its card and the pick lands on the handler-less dialog root, stays green, and spec §4's tap-to-cancel is dead in the band again. **Not closable with UI Toolkit's API** — there is no way to ask an element whether a handler would fire. Recorded as a known weakness of a gate that is otherwise sound and falsifiable |
| **A count gate** asserting that a comment's stated number of `PlayerMessage` readers matches the real one | Not blocked — costed at ~10 lines against `MainThreadAffinityTests.StripCommentsAndStrings` (`MainThreadAffinityTests.cs:67`), which already scans `Game/` and `UI/` (`UiFacingRoots`, `:42`). **The count was wrong four times across three rounds**, which is the argument for it |
| **Task 21i's three deferred minors** — the 259/238 clause in `Shell.uss:74`; `ShellProbe.cs:57-59`'s "covers what exists today" framing against `FounderNamingView.uxml:15`'s `TextField`; the only shell-probe log on disk being the negative-control run | All three were folded into this task rather than a fourth fix round. **All three are still open**: this task is documentation and none of them is a documentation change. §6.2 explains why the first one matters more than its size suggests |

### 2.5 Carried from the design, unchanged

The design's §7 declined these deliberately and nothing this phase learned
changes any of them: resuming an abandoned wave rather than forfeiting (Phase 10,
on playtest evidence); the eight-raider roster beyond the three the engine
carries; Instinct cues beyond `sk_crown` being authored empty on every body; an
artist commission's entry point (the pipeline is built so a commission replaces
meshes and keeps recipes, sockets, shader, bake and studio); the Region map's
polygon viewport.

Two are worth restating because they are *inert surfaces a tester will touch*:

- **The pause and speed pills do not work.** The runner has no pause and the plan
  did not add one. The design's own §4.2 lists "pause, speed pill" among
  `WaveHudView`'s handoff fidelity items, so they are drawn.
- **The HUD's energy pill shows reward rather than balance.** `Gene energy` is not
  on the runner's path.

### 2.6 Task 19's five-item residual, still in the ledger

Carried in `progress.md` and therefore at risk of a `git clean`, which is why it
is copied here: the sheet pair's scrim/elevation gap; neither sheet surface
scrolling; `.node__mark`'s bold-on-a-`Label`; `.codex-sheet__entries`' dead
`flex-direction`; and `TraitPip`'s 34px box. Task 20's triage ruled on all five —
items 5 and 6 there. **`TraitPip`'s fix is now proven** (zeroing the label's
chrome gives exactly `TraitChip`'s 20px) and it is post-phase anyway because the
component has no screen consumer left.

---

## 3. What this phase actually closed

Stated so §2 is not mistaken for the whole story.

- **Both Phase 8 blockers.** The abandoned-wave lockout is closed on the server
  (`POST /v1/wave/abandon`, a healing roster read) and in the client, and the
  recovery is **walked end to end on a built app**. The shell no longer renders
  over the battlefield, with the hide after the additive load and the restore as
  the `finally`'s guarded first statement.
- **A notice surface.** `NoticeToast` in the shell's own layer, a sibling of
  `#shell-root` at the document root, so a blocked beat's sentence reaches a
  player. This is what turns "it went blank" into "it said X", and §1.1 is the
  first time that was observed rather than argued.
- **A creature pipeline that produces committed assets from text.** Recipes, an
  SDF mesher, sockets, a hand-written URP shader, a sprite bake, a portrait
  studio, a lane stage. Nine bodies, twelve parts, the 48-tile contact sheet and
  Cinderplate — and a drift gate that regenerates and compares hashes, which now
  **holds from a clean tree** (§5.3).
- **Twenty-one screens in a capture corpus**, including the one screen a
  play-through is not supposed to reach. `InterruptedView` is captured *because*
  it goes up only when the walk stops — a corpus that skipped it would have left
  the failure path as the only unlooked-at surface in a phase about the look.
- **A shell probe that measures reachability at the shipping frame** and prints
  what it asserted on, which is the fix for an instrument that could be vacuous
  (§5.5).
- **Eight false comments corrected**, in code and in stylesheets, three of them
  inside the round fixing a previous one. That is a finding, not a tally — §5.8.

---

## 4. The dominant fact about how this phase ran

**Every correction below was found by an implementer or reviewer measuring rather
than complying.** Not one was noticed by luck, and not one was caught by reading
the plan more carefully. That is the load-bearing fact about this phase, and a
record that listed the corrections without saying how they were found would
mislead the next plan-writer about what to do differently.

The lesson is Phase 8's, sharpened: **a plan's prose and its executable content
drift apart, and the prose is what goes stale.** Phase 9 adds a second half —
**the controller's diagnoses went stale the same way**, and at a comparable rate
(§4.2).

### 4.1 Plan and brief premises refuted by measurement

**Before the exit gate — sixteen tasks, sixteen refutations.** Each is indexed
here; the derivation is in the named task's report.

| Task | The premise | What was measured |
|---|---|---|
| 1/2 | import paths `config/bundle.ts`, `replay/store.ts`; `start(1, …)` | `config/store.ts` + `publish.ts`, `replays/store.ts`; bundle 0.1.1 authors only wave 6 |
| 5 | `elev-2` on the element carrying `--surface` | Against `Theme.uss`'s own documented wrapper convention — a drop shadow must be drawn by something larger than the thing it lifts |
| 6 | the plan's test proves the shell ordering | A flat `List<bool>` lets the reverted fix's exact defect pass. Replaced with `(bool Visible, bool SceneLoaded)` tuples, which is what discriminates "hidden after the load" from "hidden before it" |
| 7 | `sk_flank` at `Euler(90,0,0)`; "a body stands on y=0"; the drift gate covers the recipe | The Euler mounted parts **into** the body; Vetch sat at y = −0.09985; the gate hashed only vertices and triangles, so bone, socket and colour edits passed green; an unknown bone name bound silently to root |
| 8 | the test reads `Renderer.material.GetFloat` | Which **can never observe a `MaterialPropertyBlock`** |
| 12b | the blend radius is the defect | The dome's underside sat exactly at y=0, so **the body rested on the ground between its own legs**. A blend-zero sweep still found five islands |
| 13 | `--lane-card-height: 220` is 4:3; the back control is a pill; `align-items: baseline` | 220 is 5:3; a rounded square; **`baseline` does not exist in UI Toolkit** and is silently dropped. And `.btn-primary--hybrid` named a button nothing composes |
| 14 | the type scale is wrong | The premise compared a **card heading to a page title**. And "the stylesheet shrinks" is a bad check, replaced by "does any declaration restate a component or a Theme class" |
| 14b | `_studio?.Clear()` is safe | **`?.` bypasses Unity's overloaded `==`** and calls into a destroyed object. `FtueDirector.cs:1142` now tests `!= null`, and says why |
| 14c | the band and the ring are on the same six screens | They overlap in four, so the ring had to be optional |
| 15 | the brief's Ember, roster coverage, rotated boxes, Loam's numbers, the carapace `Under` fix, §10.2 as the hook | Ember carried Task 12b's own defect (torso underside at y = −0.03); `RaiderRecipes.All` was empty, so "the raiders were never checked" was moot; **`Sdf.Box` is axis-aligned and `Primitive` carries no orientation**, so rotated boxes were inexpressible; the brief's Loam gave **four waists at 0.0%**; the `Under` fix cannot work because the shader gates `Under` on a **world-space** normal; and **§10.4 binds, not §10.2** |
| 16 | `SpliceForecast` carries traits and stats; `BeginLabel`; the joiner | No `Trait1`/`Trait2` and no stat block — the brief specified data that does not exist; `BeginLabel` would have **reverted** the gap `broodline_splice_confirm_spec.md:83` exists to close; the joiner is 40px and its mark is a **multiplication cross, not a flask**; `.t-card-title` was wrong on both axes |
| 16b | the line box is 2.5× and `FontAssetBuilder` is the lever | **A `Label`'s line box is 1.36em**, and the defect is a **nested** `Label` measuring two boxes. Wrong lever, wrong reason |
| 17 | `Wave Defense.dc.html:69` is a band; the cited colours; the camera numbers | `:69` is the **lane viewport** — the next line is `<svg viewBox="0 0 406 300">` carrying the lane's trees; the colours were `Collector Intercept.dc.html:46`'s; the camera numbers put the last pocket 0.25 units inside the frame |
| 18 | `HudSnapshot.WaveId`; no loss fixture; `Gene energy`; a pause icon; a `won` phase; `Kept` from breaches | No `WaveId`; the loss fixture already existed; no `Gene energy` on the runner's path; **a pause glyph would be a control whose tap Rallies**; `Wave Defense.dc.html` has **no `won` phase**, so post-wave has no handoff file; and `Kept` cannot come from breaches, because a breach is a raider and the bible says no creature is lost involuntarily |
| 19 | the `ScaffoldTests` sweep numbers; a codex `TraitChip`; the species tint; `CreatureCard`'s claim | The sweep numbers were stale (2 / **3** / **7**); a `TraitChip` with a null tier marks **every** trait Aberrant; the tint as a background ties with `.node.highlight` at (0,2,0); and "lineage is where a counter is known" was false |

**At the exit gate — eleven more, and these were the controller's own
diagnoses.** This is the half that matters most, because by this point the plan
was no longer the thing being followed.

| The premise | What was measured |
|---|---|
| "`:302`/`:306` are the legitimate terminal returns" | `CampaignAsync` is a `while (true)` a player never leaves by playing — it returns only when it has given up. **There was no clean end to the walk at all.** Counting returns that can end the walk: nine in `WalkAsync`, five in `CampaignAsync`, four in `FightAsync`, seven across the splice beat's two halves — **twenty-five, plus the throw**, not "fourteen-odd plus two good ones" |
| "`FtueDirectorTests` already builds a director" | It builds none; every case drives a `public static` helper. `BootController` held the **only** `new FtueDirector` in the repository, and `FtueDirector`'s own `_stage` comment made the same false claim. The fix added the second, in `WalkRecoveryTests` |
| "`FtueNotice.LoadRoster` is also said by `LoadRosterAsync`" | That line says `error.PlayerMessage` — the server's own sentence. The constant had exactly **one** production call site: the one being fixed. **Which is what made *replacement* right rather than addition** — a fourth constant would have left an unvalidated sentence behind for the next person to reach for |
| "There is no safe-area handling anywhere in `client/Assets`" | `SafeAreaBinder` had existed since 2026-09-16 (`4659218`), with its own EditMode suite and two production call sites (`WaveRunner.cs:241`, `BootController.cs:107`). The real cause was that the inset goes on the panel root and **Yoga does not pass padding to absolutely positioned children** |
| "C2 was fixed in name only and lay dormant until Task 21h woke it" | `BootController`'s cold-start catch has logged an error on **every failed cold start all along** — on exactly the timed-out-first-launch path. C2 was live the whole phase; 21h was the first task that looked |
| "Cloud Run's cold start explains the 90-second waits" | **5.680 s and 5.904 s** on two independent idle gaps, against 0.147–0.188 s warm — about 6%. And the mechanism was wrong anyway: `Retry.TransientAsync` wraps **only** `SyncAsync` (`BroodlineClient.cs:76`), so neither `/v1/roster` nor `/v1/wave/abandon` is retried at all, and both operations *succeeded* |
| "`PostWaveView`'s Continue is a second dead zone" | Latency, not geometry. `next` measures identically to `DeployView`'s `start` — 721.1 → 778.0 — and was pickable at every point before the fix |
| "The two sheets disagree on their bottom inset" | They agree. §2.3 |
| "`CodexSheet` having no production caller is a new discovery" | Already tracked at `implementation/2026-09-15-phase7-followups.md:910`, since Phase 7 |
| "The console is a TestFlight blocker" | §5.2. It cannot exist on the platform TestFlight ships to |
| "D3: every step of the recovery takes 90–120 seconds" | §4.2. It was the controller's own taps |

### 4.2 The controller's own measurement errors, for the same record

Recorded here, at the same weight as the rest, because a record that only
catalogued the *plan's* errors would teach the wrong lesson.

**The largest single error of the gate.** "Every step of the recovery takes 90–120
seconds with no feedback" was reported to the user as a major finding. It was
**the controller's own taps landing in the dead band**: `(201, 782)` on Forfeit
was inside the tab bar's strip and was swallowed; `(201, 770)` completed the same
action in 8–16 seconds. Reproduced twice each way. **The API measurements were
right the whole time; the device timing was wrong.** What survives of D3 is one
unreproduced ~90 s retry on walk 1. Not a pattern.

**The same defect was first filed as "D4, cosmetic clipping".** It was a dead zone
on a primary action. **Grading a visual symptom without testing the interaction
underneath it is how a blocker gets filed as polish** — and it is the reason the
fix took four walks instead of one.

**And from before the gate**, three of the same shape:

- The subject-scale target of 91% came from the **Ark-prism icon**, a flat
  geometric shape, not a creature.
- The Pale flank "occlusion" read off a contact sheet was wrong: the card stacks
  flat sprites, so occlusion is structurally impossible.
- **"One species in six" undercounted a visibility defect by measuring exact
  equality.** The right question is distance against a nameable threshold, and
  this project now has one — a `SectionCard`'s own drop shadow measures ΔE 1.73.

---

## 5. Method findings that outlive the phase

Eleven. These are the part of this record most likely to be worth reading in six
months, and several of them are cheap rules that would have saved this phase
whole days.

### 5.1 Anything depending on real layout, real input or a real player binary is invisible to this project's gates

**Four defects at this gate were found only by driving the built app**, and were
invisible to 525 EditMode tests and a 21-fixture capture corpus: a developer log
line shown as the player's explanation; a toast under the Dynamic Island; the
dead zone on Forfeit; and the dead screen itself. Two more from earlier in the
phase belong to the same family: the corpus renders at **430** and the game ships
at **402**, and that 16-point gap hid a third defect by itself.

The counter-half, also true: **the capture corpus caught every defect the tests
could not** — an elevation shadow drawn around 160px of bare paper, a hero disc
at 1.02:1 against its own page, ten of twelve trait parts coloured identically to
the body they mount on, a chip at 2.7× its height. **A component no fixture
instances is a component nobody has looked at.**

### 5.2 A walk that does not provoke the failure proves nothing

The console test "passed" once on a clean screen because the wave *won* instead of
timing out — no error was ever logged, so nothing could have painted. **That
specific false pass was written into the plan beforehand as the thing to avoid,
and it happened anyway.** Walk 4 fixed it by backgrounding nine seconds into the
wave so the 120-second clock expired on a genuinely live wave.

**Record an unrun test as unrun, never as a pass.** This file's §1.5 is that rule
applied to itself.

**And the platform question was settled without hardware.** `strings` over a
controlled pair of release frameworks — both `BuildOptions.None`, both
`UNITY_DEVELOPER_BUILD 0`, both with no `player-connection` line, differing only
in SDK — found `Development Console` **once in the simulator framework and zero
times in the device one**, with the scripting API surviving at 6 versus 4 and the
device framework 10 MB smaller. A binary cannot draw a window whose title it does
not contain. **Three rounds had been spent fixing it blind.** It is evidence, not
proof — an absence in a binary — and `Diagnostics.cs` says so.

> **A defect in this chain, found while verifying it.** `Diagnostics.cs`'s own
> header states the measurement with the two platforms **transposed**: "A release
> DEVICE framework and a release SIMULATOR framework … contain the string
> `Development Console` **once** and **zero** times respectively". Its own next
> two sentences contradict that ordering, and so does the observed symptom — the
> console painted on the *simulator*. Task 21i's report has it right. **Not fixed
> here: this task changes no production code.** It is §5.8's pattern for the
> fourth time, and it is inside the file the console ruling rests on.

### 5.3 Non-determinism can hide under a gate that has no margin

**The bake was non-deterministic for most of this phase.** `CreatureMotion.Awake`
seeds its breath phase from `Random.value`, and `Tick(0, 0)` reads
`sin((time + phase) × …)`, so at time zero the root still carried a random breath
of up to ±3.5%. Measured: two bakes of an *unchanged* Vetch produced a 100px
sprite and then a 101px one, Ember 137px then 131px, and every body PNG turned up
dirty in git after a generate that changed nothing.

**It mattered because of what sat on top of it.** `SilhouetteTests` reads those
PNGs at 40px against an 8% floor, and the closest species pair measures 9.6%. A
bake that wobbles is a gate that wobbles.

**The fix is in the baker, not in the creature.** `CreatureBaker.RestPose`
flattens the breath back out of the root after the tick, so the sprite is the
creature at its authored size. `CreatureMotion` is untouched and still seeds from
`Random.value` — *a wave is not a chorus line* — which is the right place for the
asymmetry. Task 20 re-ran the whole bake from a clean tree: 150 sprites,
byte-identical.

### 5.4 Three silent-drop mechanisms, none of which fails anything

1. **A `--` inside a `.uxml` comment silently drops elements.** Unity's importer
   **recovers partially**: no error, no warning, no gate. The only surface is an
   NRE from a `Q<>` that returned null.
2. **A stray `*/` mid-comment drops every rule below it in a stylesheet**, and
   `check-stylesheets.sh` only finds it via a full Editor run. **Task 19's own fix
   round caught a real instance in its own edit.**
3. **A `.uxml` root with no children parses cleanly**, so an XML-validity check
   sees nothing wrong with it.

All three are now gated by `implementation/scripts/check-silent-drops.sh`, and
**each arm was reddened by a mutation constructed against real project files
before the gate was claimed to work** — including a false-positive guard for
`*/` inside `url("a/*b*/c.png")`, which stayed green.

### 5.5 An instrument can be vacuous too, and the green must carry its own count

`ShellProbe` skipped zero-size rects before asserting, so a panel that resolved
nothing printed *"OK: every asserted control is reachable"* — **the same green
whether it measured six controls or none, inside the tool built to catch exactly
that.** The fix: the summary line now prints `asserted N control sweep(s), M
sheet band pick(s)` (`ShellProbe.cs:220`).

And the baseline file promptly restated that figure wrong — "10 controls" against
an instrument printing 15 — on the one count the round had added in order to
defeat vacuity. **Read a count off the instrument's own output, never off a
sentence about it.**

### 5.6 A fixture that sanitises the string it tests is worse than no test

`WalkRecoveryTests` stubbed the hung-wave message **without its `[WaveHost] `
prefix** — and the defect lived in the prefix. **The durable fix is structural,
not careful:** the message is named where it is thrown
(`WaveHost.CompletionTimedOut`, which builds the prefix from `nameof(WaveHost)`)
and the test reads that name (`WalkRecoveryTests.cs:363`, `:522`, `:578`). Drift
is impossible by construction rather than by review.

### 5.7 Build tools mutate project state and do not restore it

Three instances this phase, each a near-miss:

- **`PlayerSettings.iOS.sdkVersion` left at `SimulatorSDK`.** A device build would
  then have produced a simulator binary that uploads and *then* fails App Store
  Connect processing with a message about nothing.
- **`BootBuilder` rewriting product name, bundle id and build number** — and
  overwriting a tracked config file, which its header records as intended.
- **Two builders writing the same config with different indentation.**

**Restore before you build, not after.** `SimulatorBuilder` now captures
`PlayerSettings.iOS.sdkVersion` into a local *before* the `try` and restores it in
a `finally` — *"whether the build succeeds, fails, or throws"*. A `finally`
faithfully restores whatever it found; a line at the end of the happy path
restores nothing when it matters.

### 5.8 Fixing a false comment is where false comments are born

**Three instances in one task, each inside the round fixing the previous one** —
including a measured figure (259 points) attached to a subject wider than the
measurement, and a corrected derivation that named `CodexSheet` as a caller of a
method nothing calls. **A fourth is still standing and is recorded in §5.2.**

Beside it, Phase 8's rule, which this phase proved again: **a comment claiming a
gate that does not exist is worse than no comment**, because the next reader stops
looking. It shipped twice and was caught by a grep.

The practical consequence: **a round whose whole job is to fix a false statement
must have its every changed assertion re-checked against the tree.** That is what
Task 21i's scoped re-review was pointed at, and it is what caught the third.

### 5.9 Two tests that could not fail, and a third whose sensitivity was asserted

Two tests were found that could not fail — one printed *"nothing was painted"* in
the exact case it could not detect — and a third's sensitivity was asserted until
someone ran the mutation and found the window was real. **The practice that caught
all three: construct the failure mode and confirm it reddens.**

`LogAssert` is no help here: it **is unusable in this project's EditMode suites**,
because `EditModeRunner` drives NUnit by plain reflection and leaves no log scope,
so a `Debug.LogError` reddens nothing. A comment claiming otherwise had been
copied in from PlayMode.

### 5.10 Two offsetting test-count changes can hide a dropped test

A suite that reads the same total after a `+1` and a `−1` must be verified **from
the results XML**, not from the report. Task 21i landed on 525 twice and the
reviewer counted 525 `<test-case>` entries, found the added one present and
passing, and found the removed one gone with its class reduced to its survivors.

The baseline file is built on the same principle and says it at length: **the
count is not the check.** Read the failure's *message* and confirm it still names
exactly `SessionTests.cs:223`, `:238` and `:262`. A red that changes identity
while the count holds has cost this phase real time.

### 5.11 A live button that looks dead is a dead button — and UI Toolkit has no z-index

**Sibling order in a `.uxml` is both paint order AND hit-test order.**
`#sheet-layer` declared above `#tab-bar` meant a modal's own buttons lost their
lower band to a bar that painted over them. One line of declaration order; three
sheets affected, one of them — `CodexSheet`'s dismiss — **28 of 50 points dead and
never reported.** `Shell.uxml` now declares `#sheet-layer` last and carries the
mechanism in a comment, because the element is absolutely positioned and its order
changes no layout at all, so the next tidy-up would move it back.

And the human half: **ninety seconds of unchanged screen after a tap is
indistinguishable from a broken control.** The controller, who wrote the fix's own
brief, concluded twice that the button was broken. An empty `OnTabSelected` (§2.2)
is the same failure mode waiting for its first tap.

### 5.12 Unity's `Debug` output is not a reporting channel a device walk can read

It does not reach the macOS unified log, and a `--console-pty` capture stops
receiving it after startup. **A readback meant for a walk must go where a walk can
reach: the app's `Documents/` persists.** `IosFileSharingPostProcess` writes
`UIFileSharingEnabled` and `LSSupportsOpeningDocumentsInPlace` so that directory
can be pulled off a device — **and it now returns early unless
`EditorUserBuildSettings.development`**, because a shipped app that exposes its
Documents directory is handing the player its own save data. This build is the one
that made the distinction necessary.

---

## 6. Defects in this phase's own plan and design

### 6.1 The design

Four, recorded in `specs/plans/broodline_phase9_the_look.md` §11 rather than
quietly reimplemented elsewhere: §3.4's triangle budgets, §2.1's placement of the
forfeit sheet, §2.2's proof and how it runs, and the `Studio` layer the design
describes without naming. Each carries the code that refuted it. Two further
documents were corrected with them: `broodline_bible.md` §10.3, which described a
pipeline that did not exist and now exists, and `broodline_rig_proof.md` §9, where
item 10's open question has an answer — with a caveat that matters more than the
answer (§6.3).

### 6.2 The plan

**§4.1 is the plan's own defect list**, and there is no point restating sixteen
rows here. Two observations about the *shape* of that table are the part worth
keeping:

- **The plan's executable content was right nearly every time its prose was
  wrong.** In almost every row, the test it told you to write, the script it told
  you to run or the number a gate would print was correct or self-correcting, and
  the sentence *describing* that content was the stale part. An assertion is
  re-evaluated every time it runs; a sentence is evaluated once, by its author,
  before the thing it describes exists.
- **The exit gate inverted that.** By Task 21b the plan was no longer the thing
  being followed, and the eleven refutations in §4.1's second table are all
  *diagnoses* — a controller's model of a defect, held between a symptom and a
  fix, with nothing that re-measures it. That is the riskiest kind of prose this
  project produces, and it went wrong at roughly the same rate as the plan's.

The three deferred minors in §2.4 are downstream of the same thing: a measured
figure attached to a subject wider than the measurement, in the artifact a reader
meets first. **`Shell.uss` and `phase9-test-baseline.txt` are read before any
report is**, which is why a one-clause error in either is worth a round.

### 6.3 One correction that is a rule without a rig

`broodline_rig_proof.md` §4 item 10 asks what a socket does when the surface under
it moves, and §5 says the deliverable is *"a stated rule, not a beautiful
result"*. **Loam states the rule — the part rides one segment — and nothing
implements it.** `CreatureGenerator` parents every socket to the creature root
(`CreatureGenerator.cs:122-128`) and `CreatureAssembler.Mount` finds it there
(`creature.transform.Find(socketName)`), so no socket follows a bone on any body.
`sk_dorsal` sits geometrically over Loam's middle segment and would not move with
it.

**The rule is recorded; the rig is not built.** §9 now says both, because "item 10
answered" without that sentence is exactly the kind of claim this phase spent
itself correcting.

---

## 7. Counts this record could not reproduce

Three, listed because the alternative was writing them down in a document's voice
and letting them harden. Each is from the controller's own notes for this task.

### 7.1 The capture corpus is 21 fixtures; the baseline says 20

`ScreenFixtures.Names` holds **21** entries and `implementation/results/screens/`
holds **21** PNGs. `phase9-test-baseline.txt:49` reads `capture-screens.sh 20 of
20 screens`.

**The baseline is not lying** — that line sits under a heading saying those gates
were "last verified at Task 20 and NOT re-run since", and `InterruptedView`
arrived in Task 21g, after. **But the committed record understates by one on
exactly the count that answers "which screens has anyone looked at",** which is
the same shape as the "10 controls" error the phase already caught in the same
file. The screens directory is git-ignored, so the baseline's figure is the only
committed answer.

### 7.2 `SafeAreaBinderTests` has seven cases, not five

Seven `[Test]` methods in `client/Assets/Game/Tests/SafeAreaBinderTests.cs`
today. Two production call sites, as claimed. The "five" may have been the count
before Task 21e added to it; I did not go back through the history to find out,
because the figure that matters is the one a reader will check.

### 7.3 "Seven runtime error logs, two player-reachable" does not reproduce at any boundary I can construct

Measured, excluding test and Editor assemblies and `Diagnostics` itself, there
are **ten** `Debug.LogError` sites. Five of them are in instruments rather than
the app — `Benchmark/SweepRunner.cs` (three) and
`Determinism/CorpusPlayerHarness.cs` (two). That leaves **five in the shipped
app's assemblies**:

| Site | Reachable by a player? |
|---|---|
| `client/Assets/Net/OutboxClient.cs:383` | yes — the network path |
| `client/Assets/Game/WaveRunner.cs:217` | yes, on a build defect (no `UIDocument` in the wave scene) |
| `client/Assets/Game/Shell/WaveHost.cs:183` | yes — the shell-restore callback throwing |
| `client/Assets/Game/Shell/WaveHost.cs:245` | yes — an unload that failed |
| `client/Assets/Creatures/CreatureAssembler.cs:82` | yes, if the roster names a species this build does not carry |

So: **not seven, and not two.** Five in the app, of which at least three sit on
paths a player can drive without a build defect. **The item in §2.2 stands on its
substance** — a project-wide severity sweep is owed and is a policy decision — and
its numbers are the ones above. This is the fourth time in three rounds that a
count of log sites came out wrong, which is the whole argument for the count gate
in §2.4.

---

## 8. What the next session inherits

1. **One named gap and one look.** §1.4 and §2.1. Name the screen, then write the
   fix-up; the walk repeats after it.
2. **A phase that cannot be certified on hardware.** §1.3. TestFlight is the exit
   gate and nothing has run on a phone. Everything §1.1 establishes, it
   establishes on a simulator at the shipping frame.
3. **Seven unowned items** (§2.2), of which two would change a player's
   experience today: the wall-clock timeout and `CampaignAsync`'s missing
   terminal.
4. **A red test aimed at the wrong file**, permanently, and the baseline file's
   standing instruction not to let its identity drift.
5. **Four design calls already ruled on** (§2.3). Re-litigating them costs a
   re-bake or a re-capture and buys nothing that was not already measured.
6. **Eleven method findings** (§5). The two cheapest: construct the failure mode
   and confirm it reddens; and make every green carry its own count.
7. **One false comment left standing on purpose**, in `Diagnostics.cs` (§5.2),
   because this task changes no production code. It is a one-clause fix and it is
   in the file the console ruling rests on.

---

*Owns: Phase 9's residue — what is known and on what evidence, what is owed and
with what weight, and what the phase found wrong in its own documents. Does not
own: the design's rulings (`specs/plans/broodline_phase9_the_look.md`), the test
baseline (`implementation/results/phase9-test-baseline.txt`, which supersedes any
count here), or the walk record
(`implementation/results/phase9-visual-review.md`, which is the human's and is
quoted, not summarised).*
