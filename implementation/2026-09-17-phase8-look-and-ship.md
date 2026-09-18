# Phase 8 — The Look and the Ship Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give the shipped slice a face — fonts, elevation, icons, motion, eleven screens to handoff specification — and put a TestFlight build in the hands of someone who has not seen the app.

**Architecture:** A token layer already exists and is faithful; what it lacks is a typeface, textures for the two primitives USS cannot express, and a layout frame. Phase 8 adds `ScreenScaffold` plus a component vocabulary, then every screen becomes composition rather than bespoke chrome. Creature art stays interim — proxies in slots that already exist — and the proxies double as a pre-test of bible §10.2 rule 1 before Phase 9 generates anything.

**Tech Stack:** Unity 6000.6.0f1, UI Toolkit (UXML/USS), C# EditMode tests under NUnit, bash gates in `tests.yml`, Terraform + Cloud Run + Cloud SQL, Xcode / App Store Connect.

**Design:** `specs/plans/broodline_phase8_look_and_ship.md`. Read it first. Where this plan and that design disagree, §"What this plan found the design missed" below says so explicitly and the plan wins — the design gets an erratum in Task 19.

---

## Global Constraints

**No task in this plan modifies `engine/`.** Phase 8 is client, config and infrastructure. If a task appears to need an engine change, stop: the device capture at `implementation/results/device-replay.bin` is current under `SimVersion` `0.4.0` and an engine change supersedes it, which reopens a gate this phase inherits closed. That is a phase-scope decision, not a task-level one.

**The engine constraints from Phases 1–3 still bind `engine/`** — no floating point, no `System.Math`, no `System.Linq`, no `Dictionary`, no `HashSet`, no `System.IO`, zero project references, the Cecil float scan, `BannedSymbols.txt`. `EnforcementTests` scans for them. They are stated here only so that the paragraph above is understood as scope, not as permission.

From `specs/plans/broodline_phase8_look_and_ship.md`, and normative here:

- **`Broodline.UI` does not reference `Broodline.Sim`.** Phase 6's rule, kept. Anything reading an engine `Outcome` lives in `Broodline.Game` and hands `Broodline.UI` plain data. `Broodline.UI.Tests` references `Broodline.UI`, `Broodline.Model`, `Generated.Api` and the test runners — nothing else, and `overrideReferences` is `true`.
- **Every colour, size, radius and spacing value lives in `Tokens.uss`.** After Task 2 this is enforced. A screen stylesheet that needs a value the token layer does not have adds the token; it does not inline a hex.
- **Screens are `[UxmlElement] public partial class XView : VisualElement`**, constructed with `AddToClassList(UssClassName)` then `Resources.Load<VisualTreeAsset>("XView").CloneTree(this)` then `Q<>` lookups, and bound through a `Bind(model, callbacks)` method. `RosterView.cs` is the reference shape. Do not invent a second pattern.
- **UI tests construct plain `VisualElement` trees with no scene and no live `Panel`.** `ComponentTests.cs` records why: a dispatched `ClickEvent` needs an attached panel, and `GetCallbackCount<T>()` does not exist in this Editor version. Assert structure, not dispatch. Anything needing a real panel is a PlayMode test in `Broodline.Game.PlayTests`.
- **Interim creature art only.** Production assets are Phase 9's, against `specs/broodline_rig_proof.md` unchanged. Proxies go in the `silhouette` slot `CreatureCard.uxml` already has.
- **Internal TestFlight only.** No Beta App Review, so no Age Gate, Report/Block, support contact or name filter. Carried unchanged from Phase 7.
- **`RegionView` is deferred, not descoped.** It gets the foundation and a designed node list. The polygon map is Phase 10 and no task here approximates it.
- **A release build never points at `localhost`.** `BootBuilder` throws when `BROODLINE_API_URL` is unset. Task 17 runs before Task 18 for this reason and the order is not negotiable.

### Toolchain — read before running any `pnpm` command

Carried verbatim from Phases 6 and 7, because it has cost time twice: **the default `pnpm` on `PATH` is 3.7.5 under node v10** and fails in ways that read as broken tests (`test` exits 9 into usage text; `typecheck` prints nothing). In every shell:

```bash
export PATH="$HOME/.nvm/versions/node/v22.22.2/bin:$PATH"   # node 22.22.2, pnpm 9.12.0
```

**Also:** `implementation/results/*` is gitignored except `*.csv` and `*-test-baseline.txt`. `git add` on any other results file silently stages nothing; use `git add -f`.

**Also:** `run-unity-tests.sh` and `cross-runtime-diff.sh` refuse to run while the Editor has the project open. Close it first. `run-unity-tests.sh EditMode` writes `implementation/results/test-results-EditMode.xml` and exits 0 on all-pass, 2 on failure, 127 if Unity is missing.

**Also:** `.superpowers/` is gitignored as of `8050998`. Nothing under it is a deliverable.

### Values, copied verbatim

| | |
|---|---|
| Unity | `6000.6.0f1` at `/Applications/Unity/Hub/Editor/6000.6.0f1/Unity.app/Contents/MacOS/Unity` |
| Branch | `phase_8`, off `develop` at `aa36c31` |
| Display face | **Baloo 2**, SIL OFL 1.1. Weights 600–800. Titles, all large numbers, CTA labels |
| Body face | **Nunito**, SIL OFL 1.1. Weights 400–800. Everything else |
| Type scale | Already in `Tokens.uss` and unchanged: screen-title 21, hero 28, section 19, card-title 13, body 12, secondary 11, micro 10, cta 17, tab 12, nav 10 |
| Numeral floor | **11px** on any number a decision depends on (bible §10.6). `--text-micro` at 10px is therefore forbidden on numerals |
| Card shadow | `0 2px 8px rgba(63,58,82,.06)`; raised `0 4px 16px rgba(63,58,82,.09)` — reproduced as a nine-slice, not a `box-shadow` |
| Primary CTA fill | `linear-gradient(180deg, #8878cf, #6f5fbb)` — a 2×64 ramp texture |
| CTA press | Already built in `Theme.uss` and **not re-implemented**: 3px bottom border in `--violet-shadow` collapsing to 1px under `translate: 0 2px`, 60ms |
| Selected option | `inset 0 0 0 2px #8878cf` + `--surface` fill → a 2px border in `--ring` |
| Unselected option | `inset 0 0 0 1px #ece7f6` + `#f7f5fb` → a 1px border in `--hairline` over `--surface-sunk` |
| Nav icons | 19px, stroke width 1.9, no fill. Active `--violet-tint` pill, `--violet-text` label at weight 800; inactive `--mute-soft` icon, `--mute` label at 700 |
| Icon sheet | Rasterised at **3×**, so a 19px glyph is authored at 57px |
| Species colours | **Unchanged this phase.** Task 6 measures and decides; see that task for why the design's §4.6 could not be implemented as written |
| Config bundle | `0.1.3`, `minimumClientVersion` `0.3.0` — unchanged. No task here publishes a new bundle |
| Client version | `bundleVersion` `0.3.0` stays; iOS `buildNumber` moves **0 → 1** in Task 18 only |
| Cloud SQL | `db-f1-micro`, `ENTERPRISE`, `deletion_protection = true`, private IP. `db_public_ip = true` **only** during migrate/seed, `db_authorized_networks` scoped to one `/32`, then reverted |
| Cloud Run | `api` and `sim`, `min_instance_count = 0`, `max_instance_count = 10` — already in `main.tf` |
| Internal tester account | `POST /v1/account` with `birthdateBand: "adult"`, `storefrontRegion: "us-central1"` |
| CVD matrices | Machado, Oliveira & Fernandes 2009, severity 1.0, sRGB. Reproduced verbatim in Task 6 |

---

## The three things this plan cannot do for you

1. **Task 16's eyes-on pass.** A human looks at twelve screenshots and writes down what is wrong. No agent substitutes for this, and Phase 7's equivalent step was not run.
2. **Task 18's TestFlight upload.** An Apple ID, a signing identity, App Store Connect, and a first-time archive that will want attention.
3. **Task 18 Step 5 — someone who is not the developer plays the first hour.** This is the deliverable. Everything before it is preparation.

---

## What this plan found the design missed

Four things. The first changes a task substantially; the design gets an erratum in Task 19.

**1. The design's §4.6 palette gate cannot be implemented as written, and would have passed while making the palette worse.** §4.6 says to assert a minimum CVD separation "for the two named pairs." Measured against the Machado matrices and CIE L\*:

- `broodline_accessibility.md` §4 names **Vetch/Hollow** as collapsing. It does not: ΔL\* is **13.3** under deuteranopia and **16.6** under protanopia — the best-separated of the pairs it discusses.
- The palette's genuinely worst pair is **Skitter/Pale at ΔL\* 0.2** under protanopia, which §4 never mentions.
- **Ember/Loam** is real: **4.9** under deuteranopia.
- Applying §4's prescription — widen lightness on the named pairs — to reach ΔL\* ≥ 12 requires moving Skitter to `#cc901a` and Loam to `#9cd2ac`, and **that drops Ember/Skitter to 0.1**, worse than the problem it fixes.
- Six hues carry 15 pairs × 3 deficiencies = **45 constraints against 6 free lightness values.** The global optimum reachable by lightness alone is **ΔL\* 9.7**, and it costs the palette its character entirely: Loam to `#337045`, Hollow to `#443681`, Pale to `#e1e3ea`. §4's own promise — "that preserves the palette's character" — is not available.

So Task 6 builds the measurement, commits it as a tracked baseline, and puts a costed decision in front of a human. It does not repaint the palette on a plan's authority. This follows `broodline_rig_proof.md` §6's rule for exactly this situation: a finding of this kind "goes back to the bible rather than being absorbed by the art team."

**2. The design's §7.1 gate "resolved font-size ≥ 11px on numeric label classes" is not implementable in `Broodline.UI.Tests`.** Resolving a USS custom property needs an attached `Panel`, which `ComponentTests.cs` documents as unavailable in that assembly. Task 2 implements it as text analysis in `verify-uss-tokens.sh` against a `.t-num` marker class instead, which is stronger: it catches the violation in the stylesheet rather than in one instantiated tree.

**3. `Tokens.uss` cannot hold a font asset reference usefully.** Task 1 binds `-unity-font-definition` directly in `Theme.uss` and documents why, in the idiom `Theme.uss` already uses for the three primitives it could not express.

**4. `broodline_accessibility.md` §4's final sentence is now false.** It says the fix "must happen before the Character Bible is finalised." Task 6 establishes that the fix as specified does not exist. The sentence needs replacing with the decision Task 6 produces, and Task 19 does that.

---

## File structure

### New — client, foundation

| Path | Responsibility |
|---|---|
| `client/Assets/UI/Fonts/Baloo2-Bold.ttf` + `.asset` | Display face and its generated `FontAsset` |
| `client/Assets/UI/Fonts/Nunito-Bold.ttf` + `.asset` | Body face and its generated `FontAsset` |
| `client/Assets/UI/Shell/Motion.uss` | Transition vocabulary — push, sheet, ring, tick, flare |
| `client/Assets/UI/Art/shadow-card.png` | One soft-shadow nine-slice |
| `client/Assets/UI/Art/cta-ramp.png` | 2×64 violet ramp |
| `client/Assets/UI/Art/icons.png` + `icons.uss` | 3× glyph sheet and its sprite rules |
| `client/Assets/UI/Art/proxies/*.png` | Six species silhouette proxies |
| `client/Assets/Editor/FontAssetBuilder.cs` | Batch creation of the two `FontAsset`s from TTFs |

### New — client, structure

| Path | Responsibility |
|---|---|
| `client/Assets/UI/Components/ScreenScaffold.cs` + `Resources/ScreenScaffold.uxml/.uss` | The handoff's layout frame. Header, content, CTA row, footer note |
| `client/Assets/UI/Components/OptionRow.cs` + resources | Selected/unselected option treatment |
| `client/Assets/UI/Components/SectionCard.cs` + resources | Card surface with radius, padding, elevation |
| `client/Assets/UI/Components/StatCell.cs` + resources | The three-cell stat row |
| `client/Assets/UI/Components/ProgressBar.cs` + resources | Richness, growth, integrity |
| `client/Assets/UI/Components/EmptyState.cs` + resources | Every list can be empty; none currently says so |
| `client/Assets/UI/Components/SpeciesProxy.cs` | Maps a species name to its proxy sprite and its 40px silhouette |

### New — tooling and gates

| Path | Responsibility |
|---|---|
| `implementation/scripts/verify-uss-tokens.sh` | No raw hex outside `Tokens.uss`; no `--text-micro` on `.t-num` |
| `client/Assets/UI/Tests/TypographyTests.cs` | Tabular figures, from `FontAsset` glyph metrics |
| `client/Assets/UI/Diagnostics/PaletteContrast.cs` | The CVD maths, shared by the gate and the emitter |
| `client/Assets/UI/Tests/PaletteContrastTests.cs` | 45 CVD separations against a tracked baseline |
| `client/Assets/Editor/PaletteBaselineWriter.cs` | Emits the baseline from that same code |
| `client/Assets/UI/Tests/ScaffoldTests.cs` | Every screen composes `ScreenScaffold`, by reflection |
| `client/Assets/UI/Tests/SilhouetteTests.cs` | Six proxies pairwise distinct at 40px |
| `client/Assets/Editor/ScreenHarness.cs` | Editor window rendering any screen against fixtures |
| `client/Assets/Editor/ScreenshotCapture.cs` | Batch capture of all twelve screens to PNG |
| `implementation/results/palette-cvd-baseline.txt` | Tracked. The 45 measurements |
| `implementation/scripts/smoke-loop.sh` | Harvest → splice → wave → paid, against a deployed stack |
| `client/Assets/Editor/BootBuilder.cs` | Release iOS Xcode project |
| `client/Assets/Editor/ExportOptions.plist` | `app-store-connect`, symbols on |

### Modified

| Path | Change |
|---|---|
| `client/Assets/UI/Shell/Tokens.uss` | Font tokens, elevation tokens, motion durations |
| `client/Assets/UI/Shell/Theme.uss` | `-unity-font-definition`; `.t-num`; `.elev-1`/`.elev-2`; CTA `background-image`; the three-primitives header rewritten to say which are now solved |
| `client/Assets/UI/Shell/Shell.uxml` | `Motion.uss` and `icons.uss` added at the panel root |
| `client/Assets/UI/Shell/TabBar.cs` | Icons per tab |
| Eleven screen `.uss`/`.uxml`/`.cs` | Scaffold composition and handoff layout |
| `client/Assets/UI/Screens/Resources/WaveHudView.uss` | Five raw hexes → tokens |
| `client/Assets/UI/Components/Resources/CreatureCard.uss` | `silhouette` slot renders a proxy |
| `.github/workflows/tests.yml` | `verify-uss-tokens.sh` in `client-settings` |
| `client/ProjectSettings/ProjectSettings.asset` | `productName`, iOS `buildNumber` (Task 18 only) |
| `client/Assets/Editor/BuildSteps/IosFileSharingPostProcess.cs` | Early-return unless `EditorUserBuildSettings.development` |
| `implementation/results/phase7-test-baseline.txt` | Re-measured (Task 0) |
| `implementation/2026-09-15-phase7-followups.md` | §1's table corrected (Task 0) |
| `specs/broodline_accessibility.md` | §4 replaced with Task 6's decision (Task 19) |
| `specs/plans/broodline_phase8_look_and_ship.md` | Erratum for §4.6 and §7.1 (Task 19) |

---

---

## RE-GATED 2026-09-17 — the visual work does not wait on the test runner

**Why this section exists.** As first written, all thirteen visual tasks gated on
`./implementation/scripts/run-unity-tests.sh EditMode`. That runner turned out to be broken
(Task 1a), so a tooling defect blocked the entire deliverable. **That coupling was a planning
error, not a fact about the project.** Whether a game looks good is judged by looking at it;
the unit suite is a safety net, not a prerequisite for editing a stylesheet.

**The order changes. Build the harness first.** Tasks 14 and 15 — the Editor screen harness and
the screenshot corpus — move to the FRONT, before any visual change. Doing visual work without
being able to see the result is how this plan ended up gated on a test runner in the first
place. The harness needs the Editor, not the test runner, and `-executeMethod` works.

**New order:** 14, 15 (see the "before"), then 1, 2, 3, 4, 5, 6, then 7, 8, 9–12, 13.

**Gates, revised:**

| Task | Was | Now |
|---|---|---|
| 1 fonts | NUnit tabular-figures test | A `-executeMethod` check that prints the ten digit advances and exits non-zero if they differ. Same assertion, no test framework. Works today |
| 2 token lint | shell + EditMode suite | **Shell only.** It never needed Unity |
| 3 elevation, 4 icons, 5 motion | EditMode suite | Screenshot corpus + the eyes-on pass |
| 6 palette CVD | NUnit in `Broodline.UI.Tests` | **Move to `dotnet test`.** The maths is pure C# with no Unity types — it belongs beside the engine suite, which runs green today. `PaletteContrast.cs` moves out of `client/` into the tools/engine test project |
| 7 scaffold sweep | NUnit reflection sweep | Keep the test, but it is **not blocking**. The screenshot corpus shows whether a screen has a header and a CTA row |
| 13 silhouettes | NUnit 40px test | A standalone script over the six PNGs. No Unity needed to downsample an image and diff two masks |

**UPDATE — the EditMode suite LANDED.** Task 1a completed and was verified before this
re-gating was written: commit `014b025`, a reflection runner invoked through `-executeMethod`,
**268 total / 267 passed / 1 failed**, proven by two real runs plus a weakening probe that
detected a deliberate failure and then cleared.

**The gate's healthy state is exit code 2, not 0.** The single failure is
`MainThreadAffinityTests.NoConfigureAwaitFalse_InCodeThatTouchesTheUi`, a genuine pre-existing
defect flagging three real `.ConfigureAwait(false)` sites in `SessionTests.cs`. It is
deliberately not fixed. **Do not read exit 2 as a broken runner, and do not fix that test to
get a green** — it is a real finding about the codebase and it belongs to whoever owns that
code, not to a visual task passing through.

So both forms of verification now exist, and the ordering above still stands: **the screenshot
corpus is primary for visual work and the suite is the safety net**, not the other way round.
Run the suite; expect 267/268; treat any *other* failure as yours.

**PlayMode is still broken** and still uses `-runTests`. No Phase 8 task needs it.

---

## Task 0: The record — re-measure, and correct what is stale

Design §2.1. **Runs first, before any other task**, because every later task's "did I break something" comparison is against this baseline, and it currently asserts a failure that does not exist.

**Files:**
- Modify: `implementation/results/phase7-test-baseline.txt`, `implementation/2026-09-15-phase7-followups.md`

**Interfaces:**
- Produces: a `phase7-test-baseline.txt` whose `gate.*` rows match the tree, which every later task diffs against.

- [ ] **Step 1: Branch**

```bash
git checkout develop && git pull --ff-only
git checkout -b phase_8
```

Expected: `develop` at `aa36c31` or later, and a clean `git status --porcelain`.

- [ ] **Step 2: Measure every gate at this tree**

```bash
export PATH="$HOME/.nvm/versions/node/v22.22.2/bin:$PATH"
dotnet test Broodline.sln --nologo 2>&1 | tail -5
./implementation/scripts/cross-runtime-diff.sh 2>&1 | tail -3
pnpm --filter @broodline/api test 2>&1 | tail -5
pnpm --filter @broodline/api typecheck 2>&1 | tail -3
bash implementation/scripts/verify-unity-settings.sh
./implementation/scripts/generate-contract.sh >/dev/null && git status --porcelain openapi/ client/Assets/Generated/
```

Expected: `dotnet` **0 failed, 231 passed, 0 skipped**; cross-runtime `PASS: 500 scenarios agree`; api **0 failed**; typecheck silent; settings `Task 2 settings verified.`; contract diff empty.

**If `dotnet` reports a failure, stop and diagnose before editing anything.** The whole point of this task is that the recorded number and the measured number disagree; discovering a third number means something else moved.

- [ ] **Step 3: Rewrite the baseline's gate rows from that output**

Edit `implementation/results/phase7-test-baseline.txt`. The header contract is unchanged. The `gate.dotnet.*` block becomes:

```
gate.dotnet.total	231
gate.dotnet.passed	231
gate.dotnet.failed	0
gate.dotnet.skipped	0
gate.dotnet.engine.total	214
gate.dotnet.engine.passed	214
gate.dotnet.engine.failed	0
gate.dotnet.sim-service.total	17
gate.dotnet.sim-service.passed	17
```

And the header's explanatory paragraph — the one beginning `# gate.dotnet.failed is 1, not 0` — is replaced with:

```
# gate.dotnet.failed WAS 1 and is now 0. The failure was
#   Broodline.Sim.Tests.Combat.ReplayArtifactPresenceTests.TheTrackedCapturesAreCurrent
# and it was deliberate: the capture was owed under SimVersion 0.4.0.
#
# It was taken in e217bea, which landed AFTER ec06d40 wrote this file. Nothing
# re-measured, so this file spent the rest of Phase 7 asserting a deliberate red
# against a tree that was green. That is this file's own thesis happening to
# this file, for the second time in the project.
#
# The lesson is not "re-read the record." It is that a baseline is only worth
# what its last measurement was worth, so the regeneration commands above are
# the authority and these rows are a cache of them.
```

- [ ] **Step 4: Correct `phase7-followups` §1**

In `implementation/2026-09-15-phase7-followups.md`, §1's table: the first two rows' "Verified today" cells are replaced, and the section gains a dated note directly under its heading.

Row 1 becomes: `**CLOSED 2026-09-17.** Taken in e217bea. dotnet test: 0 failed, 0 skipped. Phase 6 is closed.`

Row 2 becomes: `**CLOSED 2026-09-17.** Runner sd-sassadi-m1 registered and online; run 35238465972 green in 7m58s cold, 500 scenarios byte-identical.`

The note under the heading:

```markdown
> **Amended 2026-09-17.** Two of these four closed after this file was written,
> and this file did not say so for two days. The preamble above still says
> "Tasks 2, 12, 19 and 20 have not been started"; **2 and 12 have.** The
> remaining two are 19 and 20 and they are Phase 8's Tasks 17 and 18.
>
> This is the third instance of the pattern `2026-09-11-phase4-followups.md`
> named — a durable record going stale against the work that followed it — and
> it is recorded here rather than quietly fixed, because the instance count is
> the evidence that the habit, not the document, is the problem.
```

- [ ] **Step 5: Verify the edit did not break the baseline's own consumers**

```bash
grep -c '^gate\.' implementation/results/phase7-test-baseline.txt
grep -n 'gate.dotnet.failed' implementation/results/phase7-test-baseline.txt
```

Expected: the `gate.` row count is unchanged from before the edit, and `gate.dotnet.failed` reads `0` with no trailing annotation.

- [ ] **Step 6: Commit**

```bash
git add implementation/results/phase7-test-baseline.txt implementation/2026-09-15-phase7-followups.md
git commit -m "docs(record): the baseline asserted a deliberate red against a green tree

e217bea took the device capture and landed after ec06d40 wrote the
baseline. Nothing re-measured, so phase7-test-baseline.txt spent the rest
of the phase recording gate.dotnet.failed 1 with an annotation explaining
why that was correct. Measured at this tree: 0 failed, 231 passed, 0
skipped.

phase7-followups section 1 is amended rather than rewritten: two of its
four open gates are closed, and the amendment says so where the stale
claim is, because the count of these instances is the evidence that the
habit is the problem.

Phase 6 is closed.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 1a: A test runner that is not `-runTests` — the phase's gate

**Everything from Task 1 to Task 13 gates on running the EditMode suite. `-runTests` cannot run it on this machine, so this task builds the gate before anything needs it.**

### Why, in one paragraph

`Unity.PerformanceTesting.Editor.TestRunBuilder.Setup()` — an `IPrebuildSetup` the test framework discovers from the loaded domain — deadlocks on a `Monitor.Wait` on the main thread inside `EditorApplication:Internal_CallUpdateFunctions`. Confirmed by two `sample` captures of the hung process. It is reached ~60s into every `-runTests` invocation and never leaves.

**Eliminated, with evidence — do not re-test any of these:** orphaned Unity/relay processes and a stale `Temp/UnityLockfile`; Editor lock contention (none was open); the package removals in `ad7ff3c`/`1e6c77f` (the lock has zero unresolved dependencies); `com.unity.ai.assistant 2.19.0-pre.2`; licensing (`verify-prereqs.sh` passes all five, `license.unity3d.com` reachable); network and UPM (`packages.unity.com` 200 in 0.19s; `upm.log` shows `project:list-packages --> 200 (94 ms)` seconds before a freeze); a corrupt `Library` (deleted all 6.8G, cold reimport, identical deadlock); and `-assemblyNames` filtering.

**Routes closed:** `com.unity.test-framework.performance` is `source: builtin` — it ships with Unity 6000.6 and cannot be removed or downgraded. `com.unity.collections` pulls it via `render-pipelines.core` ← URP, which is the renderer. Only `6000.6.0f1` is installed.

**What still works, and is the whole basis of this task:** `dotnet test` passes 231. Unity `-batchmode -executeMethod` drives this same Editor headlessly — the determinism gate ran green in 7m58s today. It is `-runTests`'s prebuild pipeline specifically that is broken.

**And the EditMode suites are plain NUnit.** Measured: **264 `[Test]` methods** across six Editor-only assemblies, and **zero** `[UnityTest]` or `IEnumerator` tests once the nested `PlayMode/` folders are excluded. So running NUnit directly loses nothing — it is not a degraded fallback, it is an equivalent path that never enters `TestJobRunner`.

**Files:**
- Create: `client/Assets/Editor/TestHarness/Broodline.TestHarness.asmdef`, `client/Assets/Editor/TestHarness/EditModeRunner.cs`
- Modify: `implementation/scripts/run-unity-tests.sh`

**Interfaces:**
- Produces: `-executeMethod Broodline.TestHarness.EditModeRunner.Run`, writing NUnit3-format XML to the path in `-testResults`, exiting **0** when all pass and **2** when any fail. Every later task's gate is `./implementation/scripts/run-unity-tests.sh EditMode`, unchanged in how it is called.

- [ ] **Step 1: The assembly definition**

`client/Assets/Editor/TestHarness/Broodline.TestHarness.asmdef`. It must reference the six EditMode test assemblies so their types are loadable, and carry the same define constraint they do, so it compiles only when tests are included:

```json
{
  "name": "Broodline.TestHarness",
  "rootNamespace": "Broodline.TestHarness",
  "references": [
    "Broodline.UI.Tests", "Broodline.Game.Tests", "Broodline.View.Tests",
    "Broodline.Net.Tests", "Broodline.Benchmark.Tests", "Broodline.EditorBuild.Tests"
  ],
  "includePlatforms": ["Editor"],
  "excludePlatforms": [],
  "overrideReferences": true,
  "precompiledReferences": ["nunit.framework.dll"],
  "autoReferenced": false,
  "defineConstraints": ["UNITY_INCLUDE_TESTS"],
  "versionDefines": [],
  "noEngineReferences": false
}
```

**If a referenced assembly name is wrong the whole harness silently does not compile**, and `-executeMethod` then fails with "method not found" rather than a compile error. Confirm all six names against their `.asmdef` files before moving on.

- [ ] **Step 2: The runner**

`client/Assets/Editor/TestHarness/EditModeRunner.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework.Api;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;
using UnityEditor;
using UnityEngine;

namespace Broodline.TestHarness
{
    /// Runs the EditMode suites through NUnit directly, bypassing Unity's
    /// TestJobRunner.
    ///
    /// WHY THIS EXISTS. `-runTests` deadlocks on this Editor:
    /// Unity.PerformanceTesting.Editor.TestRunBuilder.Setup(), an IPrebuildSetup
    /// the framework discovers from the loaded domain, blocks on a Monitor.Wait
    /// on the main thread inside EditorApplication:Internal_CallUpdateFunctions
    /// and never returns. The package is `source: builtin` so it cannot be
    /// removed, and com.unity.collections pulls it via URP. See the task text
    /// for the eight hypotheses already eliminated.
    ///
    /// THIS IS NOT A DEGRADED PATH. All 264 EditMode tests are plain NUnit
    /// [Test]; there is not one [UnityTest] or IEnumerator test outside the
    /// nested PlayMode/ folders, which this runner does not claim to run and
    /// does not touch. What is given up is Unity's test *pipeline*, which is
    /// exactly the broken part.
    ///
    /// PLAYMODE IS NOT COVERED. run-unity-tests.sh PlayMode still uses
    /// -runTests and is expected to deadlock the same way. No task in Phase 8
    /// needs it; whoever needs it next owns that problem.
    public static class EditModeRunner
    {
        static readonly string[] Assemblies =
        {
            "Broodline.UI.Tests", "Broodline.Game.Tests", "Broodline.View.Tests",
            "Broodline.Net.Tests", "Broodline.Benchmark.Tests", "Broodline.EditorBuild.Tests",
        };

        public static void Run()
        {
            string outPath = ArgAfter("-testResults")
                             ?? Path.Combine(Directory.GetCurrentDirectory(), "test-results-EditMode.xml");

            int total = 0, passed = 0, failed = 0, skipped = 0;
            var results = new List<ITestResult>();
            var problems = new List<string>();

            foreach (var name in Assemblies)
            {
                var asm = AppDomain.CurrentDomain.GetAssemblies()
                                   .FirstOrDefault(a => a.GetName().Name == name);
                if (asm == null)
                {
                    // Loud, not silent: a missing assembly is indistinguishable
                    // from a passing one in the counts otherwise, which is the
                    // failure mode this whole phase keeps finding in records.
                    problems.Add($"assembly not loaded: {name}");
                    continue;
                }

                var runner = new NUnitTestAssemblyRunner(new DefaultTestAssemblyBuilder());
                runner.Load(asm, new Dictionary<string, object>());
                var r = runner.Run(TestListener.NULL, NUnit.Framework.Internal.TestFilter.Empty);

                results.Add(r);
                total += r.PassCount + r.FailCount + r.SkipCount + r.InconclusiveCount;
                passed += r.PassCount;
                failed += r.FailCount;
                skipped += r.SkipCount + r.InconclusiveCount;

                foreach (var leaf in Leaves(r).Where(x => x.ResultState.Status == TestStatus.Failed))
                    problems.Add($"FAILED {leaf.FullName}: {leaf.Message}");
            }

            WriteXml(outPath, results, total, passed, failed, skipped);

            Debug.Log($"[EditModeRunner] total={total} passed={passed} failed={failed} skipped={skipped}");
            foreach (var p in problems) Debug.Log("[EditModeRunner] " + p);

            bool broken = failed > 0 || problems.Any(p => p.StartsWith("assembly not loaded"));
            EditorApplication.Exit(broken ? 2 : 0);
        }

        static IEnumerable<ITestResult> Leaves(ITestResult r)
        {
            if (!r.HasChildren) { yield return r; yield break; }
            foreach (var c in r.Children) foreach (var l in Leaves(c)) yield return l;
        }

        static string ArgAfter(string flag)
        {
            var a = Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++) if (a[i] == flag) return a[i + 1];
            return null;
        }

        /// NUnit3 format, because run-unity-tests.sh's python parser reads
        /// `total`/`passed`/`failed`/`skipped` off the root element.
        static void WriteXml(string path, List<ITestResult> results,
                             int total, int passed, int failed, int skipped)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using var w = new StreamWriter(path);
            w.WriteLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
            w.WriteLine($"<test-run id=\"1\" testcasecount=\"{total}\" result=\"{(failed > 0 ? "Failed" : "Passed")}\" " +
                        $"total=\"{total}\" passed=\"{passed}\" failed=\"{failed}\" " +
                        $"inconclusive=\"0\" skipped=\"{skipped}\" asserts=\"0\">");
            foreach (var r in results) w.WriteLine(r.ToXml(true).OuterXml);
            w.WriteLine("</test-run>");
        }
    }
}
```

- [ ] **Step 3: Point the script at it, and make it observable**

Rewrite `run-unity-tests.sh`'s invocation. Two changes in one edit: `-executeMethod` instead of `-runTests`, and a real log file instead of `-logFile -` piped through `tail -40`.

**The log change is not cosmetic.** The current form emits nothing until a run ends, so a slow run and a deadlocked run are indistinguishable. That is why the first three attempts at this blocker produced "it hangs" with no detail — three people looked at an empty screen. Diagnosis only became possible when the log went to a file.

```bash
LOG="$(pwd)/implementation/results/unity-$PLATFORM.log"
rm -f "$LOG" "$RESULTS"

if [ "$PLATFORM" = "EditMode" ]; then
  "$UNITY" -batchmode -quit \
    -projectPath "$(pwd)/client" \
    -executeMethod Broodline.TestHarness.EditModeRunner.Run \
    -testResults "$RESULTS" \
    -logFile "$LOG"
  code=$?
else
  # PlayMode still uses -runTests and is EXPECTED TO DEADLOCK on this Editor.
  # Not fixed here: no Phase 8 task needs PlayMode. Left honest rather than
  # silently routed somewhere that would report a false green.
  "$UNITY" -batchmode -runTests \
    -projectPath "$(pwd)/client" \
    -testPlatform "$PLATFORM" \
    -testResults "$RESULTS" \
    -logFile "$LOG"
  code=$?
fi

echo "--- unity log: $LOG ---"
tail -40 "$LOG"
```

`implementation/results/*.log` is already gitignored.

- [ ] **Step 4: Run it, and check the count against a known number**

```bash
./implementation/scripts/run-unity-tests.sh EditMode
```

**Expected: exit 0, and a total of 264.** That number is measured from source — `[Test]` attributes across the six assemblies: UI 141, Game 77, Benchmark 14, View 14, Net 11, EditorBuild 7.

**A total materially below 264 means an assembly did not load, not that tests passed.** The runner reports `assembly not loaded` and exits 2 for exactly this reason, but check the number yourself as well: the entire subject of Task 0 was a record that reported success while proving nothing.

If some tests fail, that is a real result — record which, and do not "fix" them as part of this task without saying so.

- [ ] **Step 5: Prove the runner discriminates**

A runner that cannot report failure is worse than none.

```bash
# Weaken: make one existing test fail, confirm the gate goes red and exits 2.
cat >> client/Assets/UI/Tests/ComponentTests.cs.probe <<'PROBE'
PROBE
# Add a deliberately failing [Test] to Broodline.UI.Tests, e.g.
#   [Test] public void ProbeThatMustFail() => Assert.Fail("probe");
# then:
./implementation/scripts/run-unity-tests.sh EditMode; echo "EXIT: $?"
#   expect: failed=1, "FAILED ...ProbeThatMustFail: probe" in the output, EXIT 2
# Remove the probe, re-run, expect 264 passed and EXIT 0.
rm -f client/Assets/UI/Tests/ComponentTests.cs.probe
git status --porcelain    # expect empty
```

Record both transcripts in the commit message. Also confirm, while a run is in progress, that `implementation/results/unity-EditMode.log` exists and is growing — that observability is the second half of this task's deliverable and it has no test.

- [ ] **Step 6: Commit**

```bash
git add client/Assets/Editor/TestHarness implementation/scripts/run-unity-tests.sh
git commit -m "test(harness): run EditMode through NUnit, because -runTests deadlocks

Unity.PerformanceTesting.Editor.TestRunBuilder.Setup() blocks on a
Monitor.Wait on the main thread inside the editor update loop and never
returns. Two sample captures of the hung process agree. The package is
source: builtin so it cannot be removed, and com.unity.collections pulls
it via URP. Eight other hypotheses were eliminated first and are listed
in the task text so nobody re-tests them.

This is not a degraded path. All 264 EditMode tests are plain NUnit
[Test] - there is not one [UnityTest] outside the nested PlayMode
folders - so running NUnit directly gives up only Unity's test pipeline,
which is the broken part. PlayMode still uses -runTests and is expected
to deadlock; no Phase 8 task needs it and that is said out loud rather
than routed somewhere that would report a false green.

The script also stops piping -logFile - through tail -40. That form emits
nothing until a run ends, so a slow run and a dead run look identical -
which is why this blocker cost three attempts before anyone could see
where it hung.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 1: Fonts — and bible §10.6's tabular figures, asserted for the first time

Design §4.1. **First among the foundation, because its gate can fail and the fallback changes the work.**

**Files:**
- Create: `client/Assets/UI/Fonts/Baloo2-Bold.ttf`, `Nunito-Bold.ttf`, `Nunito-Regular.ttf`, their `.asset` FontAssets, `client/Assets/UI/Fonts/OFL.txt`
- Create: `client/Assets/Editor/FontAssetBuilder.cs`
- Create: `client/Assets/UI/Tests/TypographyTests.cs`
- Modify: `client/Assets/UI/Shell/Theme.uss`

**Interfaces:**
- Produces: `Broodline.UI.Theme.FontPaths.Display` and `.Body` — the two `AssetDatabase` paths Task 15's capture and this task's test both load. Exact strings in Step 3.

**The test runner is Task 1a's, not this task's.** Your gate is `./implementation/scripts/run-unity-tests.sh EditMode`, which Task 1a rebuilt on `-executeMethod` because `-runTests` deadlocks on this Editor. Expect a baseline of **264 passing** before your changes.

- [ ] **Step 1: Fetch both faces and their licence**

```bash
mkdir -p client/Assets/UI/Fonts && cd client/Assets/UI/Fonts
curl -sL -o baloo.zip "https://fonts.google.com/download?family=Baloo%202"
curl -sL -o nunito.zip "https://fonts.google.com/download?family=Nunito"
unzip -jo baloo.zip '*Baloo2-Bold.ttf' '*OFL.txt' -d .
unzip -jo nunito.zip '*Nunito-Bold.ttf' '*Nunito-Regular.ttf' -d .
rm -f baloo.zip nunito.zip && ls
```

Expected: `Baloo2-Bold.ttf`, `Nunito-Bold.ttf`, `Nunito-Regular.ttf`, `OFL.txt`.

If the Google Fonts download endpoint has changed shape, the faces are also at `github.com/google/fonts/tree/main/ofl/baloo2` and `/ofl/nunito`. Both are SIL OFL 1.1; `OFL.txt` must be committed beside them either way.

- [ ] **Step 2: The FontAsset builder**

Create `client/Assets/Editor/FontAssetBuilder.cs`:

```csharp
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.Text;

// NO NAMESPACE, deliberately. Broodline.EditorBuild.asmdef lives in
// Assets/Editor/BuildSteps/, so a file directly under Assets/Editor/ is in
// Assembly-CSharp-Editor - which is where WaveBuilder and BenchmarkBuilder
// already are, both global. -executeMethod takes the type name as it is.

    /// Generates the two UI Toolkit FontAssets from the committed TTFs.
    ///
    /// Batch rather than by hand because a FontAsset created through the
    /// editor UI records the atlas settings whoever created it happened to
    /// have, and those settings are what decides whether a numeral renders
    /// crisply at 11px. One entry point, one set of values, reproducible.
    public static class FontAssetBuilder
    {
        const int AtlasWidth = 1024, AtlasHeight = 1024, SamplingPointSize = 90, Padding = 9;

        [MenuItem("Broodline/Rebuild Font Assets")]
        public static void Rebuild()
        {
            Build("Assets/UI/Fonts/Baloo2-Bold.ttf",  "Assets/UI/Fonts/Baloo2-Bold SDF.asset");
            Build("Assets/UI/Fonts/Nunito-Bold.ttf",  "Assets/UI/Fonts/Nunito-Bold SDF.asset");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        static void Build(string ttf, string outPath)
        {
            var font = AssetDatabase.LoadAssetAtPath<Font>(ttf);
            if (font == null) throw new System.IO.FileNotFoundException($"no TTF at {ttf}");

            var asset = FontAsset.CreateFontAsset(
                font, SamplingPointSize, Padding, GlyphRenderMode.SDFAA,
                AtlasWidth, AtlasHeight, AtlasPopulationMode.Dynamic, enableMultiAtlasSupport: true);
            asset.name = System.IO.Path.GetFileNameWithoutExtension(outPath);

            AssetDatabase.DeleteAsset(outPath);
            AssetDatabase.CreateAsset(asset, outPath);
            // The atlas texture and material are sub-assets, or the .asset
            // references objects that do not survive a reimport.
            AssetDatabase.AddObjectToAsset(asset.atlasTextures[0], asset);
            AssetDatabase.AddObjectToAsset(asset.material, asset);
        }
    }
```

- [ ] **Step 3: Write the failing test**

Create `client/Assets/UI/Tests/TypographyTests.cs`:

```csharp
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.TextCore.Text;

namespace Broodline.UI.Tests
{
    /// bible 10.6, asserted rather than assumed: "Numbers must not jitter as
    /// they tick, and 1/l/I and 0/O must be unambiguous - misread stats erode
    /// trust."
    ///
    /// Tabular figures means every digit advances the pen by the same amount.
    /// That is a property of the FONT, readable from its glyph metrics, and
    /// it is the one half of 10.6 that no amount of USS can satisfy. The
    /// other half - the 11px floor - is a SIZE property and lives in
    /// verify-uss-tokens.sh.
    ///
    /// If this test fails on Baloo 2, 10.6 already prescribes the remedy and
    /// it is not a judgement call: "keep it for titles and CTAs and pair a
    /// tabular face for numerals only." Record the measurement, do that, and
    /// point DisplayFace below at the paired face.
    public class TypographyTests
    {
        const string DisplayFace = "Assets/UI/Fonts/Baloo2-Bold SDF.asset";
        const string BodyFace    = "Assets/UI/Fonts/Nunito-Bold SDF.asset";

        static float[] DigitAdvances(string path)
        {
            var font = AssetDatabase.LoadAssetAtPath<FontAsset>(path);
            Assert.IsNotNull(font, $"no FontAsset at {path} - run Broodline/Rebuild Font Assets");
            font.TryAddCharacters("0123456789");

            return "0123456789".Select(c =>
            {
                Assert.IsTrue(font.characterLookupTable.TryGetValue(c, out var ch),
                              $"{font.name} has no glyph for '{c}'");
                return ch.glyph.metrics.horizontalAdvance;
            }).ToArray();
        }

        [Test]
        public void TheDisplayFacesDigitsAllAdvanceTheSameWidth()
        {
            var a = DigitAdvances(DisplayFace);
            Assert.That(a.Distinct().Count(), Is.EqualTo(1),
                "display face numerals are proportional, so every ticking number will jitter. " +
                "Advances 0-9: " + string.Join(", ", a) + ". bible 10.6 says what to do.");
        }

        [Test]
        public void TheBodyFacesDigitsAllAdvanceTheSameWidth()
        {
            var a = DigitAdvances(BodyFace);
            Assert.That(a.Distinct().Count(), Is.EqualTo(1),
                "body face numerals are proportional. Advances 0-9: " + string.Join(", ", a));
        }

        [Test]
        public void TheDisplayFaceDistinguishesOneFromEllAndZeroFromOh()
        {
            var font = AssetDatabase.LoadAssetAtPath<FontAsset>(DisplayFace);
            font.TryAddCharacters("1lI0O");
            foreach (var pair in new[] { ('1', 'l'), ('1', 'I'), ('0', 'O') })
            {
                Assert.IsTrue(font.characterLookupTable.TryGetValue(pair.Item1, out var a));
                Assert.IsTrue(font.characterLookupTable.TryGetValue(pair.Item2, out var b));
                Assert.That(a.glyph.glyphIndex, Is.Not.EqualTo(b.glyph.glyphIndex),
                    $"'{pair.Item1}' and '{pair.Item2}' share a glyph - 10.6's second requirement fails");
            }
        }
    }
}
```

- [ ] **Step 4: Run it and watch it fail for the right reason**

```bash
./implementation/scripts/run-unity-tests.sh EditMode 2>&1 | tail -20
```

Expected: `TypographyTests` fails with *"no FontAsset at Assets/UI/Fonts/Baloo2-Bold SDF.asset"* — not with a compile error. A compile error means the asmdef needs `UnityEditor` access; `includePlatforms: ["Editor"]` already grants it and no change should be needed.

- [ ] **Step 5: Generate the assets and re-run**

```bash
"/Applications/Unity/Hub/Editor/6000.6.0f1/Unity.app/Contents/MacOS/Unity" -batchmode -quit \
  -projectPath "$(pwd)/client" -executeMethod FontAssetBuilder.Rebuild -logFile - | tail -5
./implementation/scripts/run-unity-tests.sh EditMode 2>&1 | tail -20
```

**Two outcomes, and both are acceptable results of this step:**

- All three pass → continue to Step 6 unchanged.
- `TheDisplayFacesDigitsAllAdvanceTheSameWidth` fails → **this is the expected-plausible outcome for a rounded display face.** Record the printed advances in the commit message, then: set `DisplayFace` to `"Assets/UI/Fonts/Nunito-Bold SDF.asset"`, and in Step 6's `Theme.uss` apply Baloo 2 only to `.t-screen-title`, `.t-section` and `.btn-primary`, with Nunito on `.t-hero` and every `.t-num`. Bible §10.6 authorises exactly this and no further decision is needed.

- [ ] **Step 6: Bind the faces in `Theme.uss`**

Replace `Theme.uss`'s `FONTS ARE NOT SET HERE` header block with:

```
/* FONTS. Baloo 2 (display/numerals) and Nunito (body), both SIL OFL 1.1,
 * under client/Assets/UI/Fonts with their licence.
 *
 * BOUND DIRECTLY, NOT THROUGH A TOKEN, and that is deliberate. Tokens.uss
 * holds values; a font is an asset reference, and threading one through a
 * custom property makes the indirection load-bearing for something that has
 * exactly two values and changes roughly never. The type SCALE is tokenised
 * because it is arithmetic; the FACE is not because it is an asset.
 *
 * bible 10.6's two hard requirements are now both gated: tabular figures by
 * TypographyTests, and the 11px numeral floor by verify-uss-tokens.sh via the
 * .t-num marker below. Neither was checkable before this phase.
 */
```

and add, at the element defaults:

```css
Label, Button, TextField {
    -unity-font-definition: url("project:///Assets/UI/Fonts/Nunito-Bold%20SDF.asset");
}

.t-screen-title, .t-section, .t-hero, .btn-primary > Label, .btn-primary {
    -unity-font-definition: url("project:///Assets/UI/Fonts/Baloo2-Bold%20SDF.asset");
}

/* The marker verify-uss-tokens.sh enforces the 11px floor against. Any label
 * showing a number a decision depends on carries it. */
.t-num {
    -unity-font-definition: url("project:///Assets/UI/Fonts/Baloo2-Bold%20SDF.asset");
    font-size: var(--text-secondary);
}
```

If Step 5 took the fallback branch, the second selector loses `.t-hero` and `.t-num` uses the Nunito url instead. Nothing else changes.

- [ ] **Step 7: Re-run and confirm nothing else moved**

```bash
./implementation/scripts/run-unity-tests.sh EditMode 2>&1 | tail -8
dotnet test Broodline.sln --nologo 2>&1 | tail -3
```

Expected: EditMode all pass; `dotnet` still 0 failed, 231 passed.

- [ ] **Step 8: Commit**

```bash
git add client/Assets/UI/Fonts client/Assets/Editor/FontAssetBuilder.cs \
        client/Assets/UI/Tests/TypographyTests.cs client/Assets/UI/Shell/Theme.uss
git commit -m "feat(ui): the two faces, and bible 10.6's tabular figures as a test

There was no .ttf anywhere under client/Assets, so every number in the
game rendered in Unity's default sans and 10.6's tabular-figures
requirement had never been checked - not failed, never checked.

TypographyTests reads digit horizontalAdvance straight off the FontAsset
and asserts all ten are equal. That is the half of 10.6 no stylesheet can
satisfy. The other half, the 11px floor, is a size property and belongs
to verify-uss-tokens.sh via the .t-num marker this commit introduces.

The faces are bound directly rather than through a token: Tokens.uss
holds values, and a font is an asset.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 2: The token lint — and the nineteen raw hexes it finds

Design §4, §7.1, and the amendment in "What this plan found the design missed" item 2.

**Files:**
- Create: `implementation/scripts/verify-uss-tokens.sh`
- Modify: `client/Assets/UI/Screens/Resources/WaveHudView.uss`, `client/Assets/UI/Shell/Tokens.uss`, `.github/workflows/tests.yml`

**Interfaces:**
- Consumes: `.t-num` from Task 1.
- Produces: a gate every later task's stylesheet must satisfy. Adding a colour after this task means adding a token.

- [ ] **Step 1: Write the gate**

Create `implementation/scripts/verify-uss-tokens.sh`, following `verify-unity-settings.sh`'s idiom exactly — same `ok`/`bad` helpers, same fail-closed posture:

```bash
#!/usr/bin/env bash
# Asserts the token layer is actually the token layer.
#
# Tokens.uss is faithful to the handoff and was landed in 6b76a0f, the last
# commit of Phase 7 - AFTER most screens were written. WaveHudView.uss
# therefore carried five raw hex values, four of them verbatim copies of token
# values and one (#ff6b5c) a sixth colour that is in no design document at all.
# That is what this gate exists to stop recurring.
#
# FAIL CLOSED: if the stylesheet list comes back empty, that is a FAIL and not
# a pass - a lint that finds nothing to lint has not verified anything.
set -uo pipefail
cd "$(dirname "$0")/../.."
fail=0
ok ()  { printf '  ok    %s\n' "$1"; }
bad () { printf '  FAIL  %s\n  ->  %s\n' "$1" "$2"; fail=1; }

TOKENS=client/Assets/UI/Shell/Tokens.uss
[ -f "$TOKENS" ] || { echo "FAIL: no $TOKENS"; exit 1; }

sheets=$(find client/Assets/UI -name '*.uss' ! -path "*/Shell/Tokens.uss" | sort)
if [ -z "$sheets" ]; then
  bad "no .uss files found under client/Assets/UI" "the lint found nothing to lint, which is not a pass"
  echo; echo "Fix what is marked FAIL, then re-run."; exit 1
fi
ok "$(printf '%s\n' "$sheets" | wc -l | tr -d ' ') stylesheets to check"

# --- 1. No raw hex outside Tokens.uss. -------------------------------------
# Two exclusions, both load-bearing:
#   url(...)  - asset references, no colour.
#   comments  - Theme.uss DOCUMENTS the handoff's `linear-gradient(180deg,
#               #8878cf, #6f5fbb)` and `box-shadow: 0 3px 0 #5b4d9e` in its
#               header, explaining which primitives USS cannot express. A lint
#               that reddens on that turns the file's honesty into a failure,
#               which is the opposite of the point.
# The awk strips /* ... */ (including multi-line) before the grep sees it.
offenders=$(printf '%s\n' "$sheets" | while read -r f; do
  awk '
    { line = $0
      while (1) {
        if (inc) { i = index(line, "*/"); if (!i) { line = ""; break }
                   inc = 0; line = substr(line, i + 2); continue }
        i = index(line, "/*"); if (!i) break
        rest = substr(line, i + 2); j = index(rest, "*/")
        if (j) { line = substr(line, 1, i - 1) substr(rest, j + 2); continue }
        line = substr(line, 1, i - 1); inc = 1; break
      }
      printf "%d:%s\n", NR, line }
  ' "$f" | grep -E ':.*#[0-9a-fA-F]{3,8}\b' | grep -v 'url(' | sed "s#^#$f:#"
done)
if [ -z "$offenders" ]; then
  ok "no raw hex outside Tokens.uss"
else
  bad "raw hex outside Tokens.uss:"$'\n'"$offenders" \
      "add a token to Tokens.uss and reference it with var(); a colour that is not in the token layer is not in the design system"
fi

# --- 2. The 11px numeral floor. bible 10.6. --------------------------------
# --text-micro is 10px. Any rule that sets .t-num must not use it.
micro=$(printf '%s\n' "$sheets" | while read -r f; do
  awk -v F="$f" '
    /\.t-num/            { inrule=1 }
    inrule && /--text-micro/ { printf "%s:%d: %s\n", F, NR, $0 }
    /}/                  { inrule=0 }
  ' "$f"
done)
if [ -z "$micro" ]; then
  ok "no .t-num rule uses --text-micro (the 10px token)"
else
  bad "a numeral class is set below the 11px floor:"$'\n'"$micro" \
      "bible 10.6: minimum 11pt for any number a decision depends on"
fi

# --- 3. Every var() resolves to a token that exists. -----------------------
# RUNTIME-INJECTED properties are legitimate and are not in Tokens.uss:
# SafeAreaBinder sets --safe-top/--safe-bottom on the panel root at runtime
# from Screen.safeArea. They cannot be static values and Shell.uss is right to
# use them. Anything else added here needs a comment saying who sets it.
RUNTIME_SET="--safe-top --safe-bottom"
missing=$(printf '%s\n' "$sheets" | while read -r f; do
  grep -oE 'var\(--[a-z0-9-]+' "$f" | sed 's/var(//' | sort -u | while read -r t; do
    case " $RUNTIME_SET " in *" $t "*) continue ;; esac
    grep -q -- "^\s*$t:" "$TOKENS" || echo "$f: $t"
  done
done)
if [ -z "$missing" ]; then
  ok "every var() resolves to a token defined in Tokens.uss"
else
  bad "var() references a token that does not exist:"$'\n'"$missing" \
      "USS resolves an unknown custom property to nothing and renders the element untinted; it does not warn"
fi

echo
[ $fail -eq 0 ] && echo "Token layer verified." || echo "Fix what is marked FAIL, then re-run."
exit $fail
```

```bash
chmod +x implementation/scripts/verify-uss-tokens.sh
```

- [ ] **Step 2: Run it and watch it fail on real offenders**

```bash
bash implementation/scripts/verify-uss-tokens.sh
```

Expected: FAIL on check 1, exit code 1, naming **seventeen** offenders across four files — and **not** `Theme.uss` lines 12 and 23, which are hexes inside its header comment and are legitimate documentation of the two primitives USS cannot express.

The full expected offender list, measured at this tree:

| File | Lines |
|---|---|
| `Components/Resources/ConfirmDialog.uss` | 59 `#ddd3f0`, 76 `#ffffff` |
| `Components/Resources/CreatureCard.uss` | 85 `#ffffff`, 86 `#fdfcff`, 90 `#f3eefc` |
| `Components/Resources/TraitPip.uss` | 42 `#ffffff`, 44 `#efe9fb`, 48 `#d9cdf5` |
| `Screens/Resources/WaveHudView.uss` | 63, 67, 72, 75, 79 |
| `Shell/Theme.uss` | 135 `#ffffff`, 173 `#ddd3f0`, 175 `#e7e0f6`, 239 `#f7f5fb` |

**If the run flags `Theme.uss:12` or `:23`, the comment-stripping is wrong — fix the lint, not the comment.**

- [ ] **Step 3: Add the genuinely missing tokens**

Most offenders are token values already spelled out longhand. Three are not, and each is a real
value the design system should own. Add to `Tokens.uss`:

```css
    --coral-alert: #ff6b5c;       /* breach state - hotter than --coral, HUD only */
    --surface-raised: #fdfcff;    /* CreatureCard's selected fill, above --surface */
    --violet-pressed: #e7e0f6;    /* secondary button :active, under --violet-tint */
```

`#ddd3f0` (ConfirmDialog 59, Theme 173) and `#efe9fb` (TraitPip 44) are both one step off
`--hairline` (`#ece7f6`); `#f3eefc` (CreatureCard 90) and `#f7f5fb` (Theme 239) are both one
step off `--violet-tint` (`#f1ecfa`) and `--surface-sunk` (`#f8f6fc`). **Map each to the nearest
existing token rather than minting four more** — they are almost certainly drift, not intent, and
the handoff defines no such values. Record in the commit which four you collapsed and to what, so
a later reader can object if one was deliberate.

`#ffffff` is `--surface` everywhere it appears.

- [ ] **Step 4: Replace every offender, starting with `WaveHudView.uss`**

| Line | Was | Becomes |
|---|---|---|
| 63 | `background-color: #e5867a;` | `background-color: var(--coral);` |
| 67 | `... .creature .. { background-color: #7cc492; }` | `var(--green)` |
| 72 | `... .chilled .. { background-color: #6ba7c0; }` | `var(--teal)` |
| 75 | `... .rallied .. { background-color: #ffb703; }` | `var(--amber-bright)` |
| 79 | `... .breaching .. { background-color: #ff6b5c; }` | `var(--coral-alert)` |

- [ ] **Step 5: Run the gate and the suite**

```bash
bash implementation/scripts/verify-uss-tokens.sh
./implementation/scripts/run-unity-tests.sh EditMode 2>&1 | tail -6
```

Expected: `Token layer verified.`, exit 0; EditMode all pass.

- [ ] **Step 6: Prove each of the three checks discriminates**

Three weakenings, reverted after each. A gate nobody has seen fail is a gate nobody should trust.

```bash
# Check 1 - a raw hex returns.
sed -i '' 's/var(--coral-alert)/#ff6b5c/' client/Assets/UI/Screens/Resources/WaveHudView.uss
bash implementation/scripts/verify-uss-tokens.sh; echo "EXIT: $?"   # expect FAIL on check 1, exit 1
git checkout client/Assets/UI/Screens/Resources/WaveHudView.uss

# Check 2 - a numeral drops below the floor.
printf '\n.t-num { font-size: var(--text-micro); }\n' >> client/Assets/UI/Shell/Theme.uss
bash implementation/scripts/verify-uss-tokens.sh; echo "EXIT: $?"   # expect FAIL on check 2, exit 1
git checkout client/Assets/UI/Shell/Theme.uss

# Check 3 - a var() points at nothing.
sed -i '' 's/var(--coral-alert)/var(--coral-alarm)/' client/Assets/UI/Screens/Resources/WaveHudView.uss
bash implementation/scripts/verify-uss-tokens.sh; echo "EXIT: $?"   # expect FAIL on check 3, exit 1
git checkout client/Assets/UI/Screens/Resources/WaveHudView.uss

# Fail-closed - nothing to lint.
mv client/Assets/UI/Screens/Resources /tmp/uss-hidden
bash implementation/scripts/verify-uss-tokens.sh; echo "EXIT: $?"   # still exit 1: some sheets remain, none offend
mv /tmp/uss-hidden client/Assets/UI/Screens/Resources
git status --porcelain     # expect empty
```

Record the four exit codes in the commit message. The fourth probe is the one that matters least and is checked anyway, because `verify-unity-settings.sh`'s own header makes fail-closed a house requirement.

- [ ] **Step 7: Wire it into CI**

In `.github/workflows/tests.yml`, the `client-settings` job gains a step directly after `verify-unity-settings.sh`:

```yaml
      - name: verify-uss-tokens.sh
        run: bash implementation/scripts/verify-uss-tokens.sh
```

- [ ] **Step 8: Commit**

```bash
git add implementation/scripts/verify-uss-tokens.sh client/Assets/UI/Shell/Tokens.uss \
        client/Assets/UI/Screens/Resources/WaveHudView.uss .github/workflows/tests.yml
git commit -m "test(ui): the token layer becomes a gate, and the HUD stops opting out

WaveHudView.uss carried five raw hexes - four verbatim copies of token
values and one, #ff6b5c, a sixth colour in no design document. It was
written in Task 16, before Tokens.uss existed in the branch, and nothing
would ever have caught it.

Three checks, each seen to fail and then restored: a raw hex outside
Tokens.uss, a .t-num rule using the 10px token (bible 10.6's floor), and
a var() naming a token that does not exist - which USS resolves to
nothing and renders untinted, silently. The empty-input case is a FAIL,
not a pass.

#ff6b5c becomes --coral-alert rather than being flattened into --coral:
it is a real sixth value and the design system should own it.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 3: Elevation and the CTA gradient — the two primitives USS cannot express

Design §4.2, §4.3, §2.3.

**Files:**
- Create: `client/Assets/UI/Art/shadow-card.png`, `client/Assets/UI/Art/cta-ramp.png`
- Modify: `client/Assets/UI/Shell/Tokens.uss`, `client/Assets/UI/Shell/Theme.uss`

**Interfaces:**
- Produces: `.elev-1` and `.elev-2` — the classes Tasks 8–12 put on every card surface.

- [ ] **Step 1: Generate both textures**

Neither is art; both are arithmetic, so they are generated rather than drawn and the script is committed with them.

```bash
mkdir -p client/Assets/UI/Art
python3 - <<'PY'
from PIL import Image, ImageFilter, ImageDraw
# shadow-card.png - a 64x64 nine-slice. 20px slice borders, 24px rounded box,
# blurred 8px, in the handoff's rgba(63,58,82,.06)->(.09) ink at full alpha so
# the USS tint controls the strength.
img = Image.new("RGBA", (64, 64), (0, 0, 0, 0))
d = ImageDraw.Draw(img)
d.rounded_rectangle([14, 12, 50, 50], radius=10, fill=(63, 58, 82, 255))
img = img.filter(ImageFilter.GaussianBlur(6))
img.save("client/Assets/UI/Art/shadow-card.png")

# cta-ramp.png - 2x64, #8878cf -> #6f5fbb top to bottom. Two px wide because a
# 1px texture invites the importer to treat it as degenerate.
a, b = (0x88, 0x78, 0xcf), (0x6f, 0x5f, 0xbb)
ramp = Image.new("RGB", (2, 64))
for y in range(64):
    t = y / 63
    c = tuple(round(a[i] + (b[i] - a[i]) * t) for i in range(3))
    for x in range(2):
        ramp.putpixel((x, y), c)
ramp.save("client/Assets/UI/Art/cta-ramp.png")
print("wrote both")
PY
```

If Pillow is absent: `python3 -m pip install --user Pillow`.

- [ ] **Step 2: Import settings**

Both need `Sprite (2D and UI)` with the nine-slice border on the shadow. Create `client/Assets/Editor/ArtImportSettings.cs`:

```csharp
using UnityEditor;
using UnityEngine;

// No namespace: Assembly-CSharp-Editor, as above.

    /// Import settings for the two generated UI textures.
    ///
    /// In an AssetPostprocessor rather than committed .meta edits: a .meta is
    /// regenerated on a fresh clone if the importer version moves, and the
    /// nine-slice border silently reverting to zero turns every card shadow
    /// into a stretched blur. This re-asserts it on every import.
    public class ArtImportSettings : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/UI/Art/")) return;
            var t = (TextureImporter)assetImporter;
            t.textureType = TextureImporterType.Sprite;
            t.spriteImportMode = SpriteImportMode.Single;
            t.mipmapEnabled = false;
            t.filterMode = FilterMode.Bilinear;
            t.alphaIsTransparency = true;

            if (assetPath.EndsWith("shadow-card.png"))
                t.spriteBorder = new Vector4(20, 20, 20, 20);   // L, B, R, T
        }
    }
```

- [ ] **Step 3: Tokens for elevation**

Add to `Tokens.uss` after the radius block:

```css
    /* ---- Elevation. USS has no box-shadow; these tint a nine-slice. ---- */
    --elev-1-tint: rgba(63, 58, 82, 0.06);   /* handoff: 0 2px 8px  */
    --elev-2-tint: rgba(63, 58, 82, 0.09);   /* handoff: 0 4px 16px */
    --elev-1-spread: 6px;
    --elev-2-spread: 12px;
```

- [ ] **Step 4: The classes, and the header that stops lying**

In `Theme.uss`, replace bullets 1 and 2 of the `THREE THINGS THE HANDOFF ASKS FOR` header with:

```
 * 1. `linear-gradient(180deg, #8878cf, #6f5fbb)` on the primary CTA. SOLVED
 *    in Phase 8 with a 2x64 ramp texture at Assets/UI/Art/cta-ramp.png. The
 *    Phase 7 note said a gradient "would mean shipping a 1xN texture per CTA
 *    colour, which is real art for a difference of six hex steps" - true when
 *    it would have been the only texture in the build, and false once the
 *    icon sheet ships. One ramp, one CTA colour, no art.
 *
 * 2. `box-shadow`. USS still has none. SOLVED as a nine-sliced sprite behind
 *    the card rather than on it: Assets/UI/Art/shadow-card.png, tinted by
 *    --elev-1-tint / --elev-2-tint to the handoff's two elevations. It is a
 *    sibling element, not a style, so a card that wants elevation carries
 *    .elev-1 and the rule draws the sprite as its own background with a
 *    negative margin. On a light theme with white on near-white, this is most
 *    of what separates a card from a rectangle.
```

and add:

```css
.elev-1, .elev-2 {
    background-image: url("project:///Assets/UI/Art/shadow-card.png");
    -unity-slice-left: 20; -unity-slice-right: 20;
    -unity-slice-top: 20; -unity-slice-bottom: 20;
    -unity-slice-scale: 0.5;
}
.elev-1 { -unity-background-image-tint-color: var(--elev-1-tint); }
.elev-2 { -unity-background-image-tint-color: var(--elev-2-tint); }

.btn-primary {
    background-image: url("project:///Assets/UI/Art/cta-ramp.png");
    -unity-background-scale-mode: stretch-to-fill;
    background-color: rgba(0, 0, 0, 0);
}
```

`background-color` goes transparent because a colour under a `background-image` is what the image is composited over, and leaving `--violet` there tints the ramp.

- [ ] **Step 5: Verify**

```bash
bash implementation/scripts/verify-uss-tokens.sh
./implementation/scripts/run-unity-tests.sh EditMode 2>&1 | tail -6
```

Expected: both green. The lint must pass — the `rgba()` values are in `Tokens.uss`, and `url(` lines are excluded from the hex check by design.

- [ ] **Step 6: Commit**

```bash
git add client/Assets/UI/Art client/Assets/Editor/ArtImportSettings.cs \
        client/Assets/UI/Shell/Tokens.uss client/Assets/UI/Shell/Theme.uss
git commit -m "feat(ui): elevation and the CTA gradient, as textures

Theme.uss recorded both as things USS cannot express and left them
unsolved. The reasoning held only while they would have been the sole
textures in the build; the icon sheet lands this phase, so the marginal
cost is a nine-slice and a 2x64 ramp.

Elevation is a tinted nine-slice behind the card rather than a style on
it, because USS has no box-shadow at all. On a light theme with pure
white surfaces on near-white paper, that contrast IS the elevation, and
it was the single largest reason the app read flat.

Both textures are generated by a committed script - they are arithmetic,
not art - and the nine-slice border is re-asserted by an
AssetPostprocessor rather than trusted to a .meta.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 4: Icons

Design §4.4.

**Files:**
- Create: `client/Assets/UI/Art/icons/*.png` (3×), `client/Assets/UI/Shell/icons.uss`, `client/Assets/UI/Art/icons/LICENSE-lucide.txt`
- Modify: `client/Assets/UI/Shell/Shell.uxml`, `client/Assets/UI/Shell/TabBar.cs`, `client/Assets/UI/Tests/TabBarTests.cs`

**Interfaces:**
- Produces: `.icon` plus `.icon--<name>` for each glyph. Tasks 7–12 reference these names and no others: `map`, `ark`, `splice`, `lab`, `allies`, `back`, `charge`, `shard`, `tier`, `timer`, `lock`, `check`, `warning`.

- [ ] **Step 1: Fetch the eight Lucide glyphs**

Lucide is MIT and stroke-based; its 24px/2.0 default rescales to the handoff's 19px/1.9 without redrawing.

```bash
mkdir -p client/Assets/UI/Art/icons && cd client/Assets/UI/Art/icons
curl -sL -o LICENSE-lucide.txt https://raw.githubusercontent.com/lucide-icons/lucide/main/LICENSE
for pair in "map:map" "home:ark" "users:allies" "flask-conical:lab" \
            "chevron-left:back" "timer:timer" "lock:lock" "check:check" \
            "triangle-alert:warning" "zap:charge" "gem:shard" "award:tier"; do
  src="${pair%%:*}"; dst="${pair##*:}"
  curl -sfL -o "$dst.svg" "https://raw.githubusercontent.com/lucide-icons/lucide/main/icons/$src.svg" \
    && echo "  $dst" || echo "  MISSING $src"
done
ls *.svg | wc -l
```

Expected: 12 SVGs, no `MISSING`. If a name has moved, find the current one at `lucide.dev/icons` — the mapping is intent, not a fixed string.

- [ ] **Step 2: Draw the thirteenth**

`splice` has no equivalent in any general set: two strands crossing, per bible §2.1's splice imagery. Write `splice.svg` by hand in the Lucide idiom — 24×24 viewBox, `stroke="currentColor"`, `stroke-width="2"`, `fill="none"`, round caps:

```bash
cat > splice.svg <<'SVG'
<svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24"
     fill="none" stroke="currentColor" stroke-width="2"
     stroke-linecap="round" stroke-linejoin="round">
  <path d="M7 3c0 4.5 10 6 10 10.5"/>
  <path d="M17 3c0 4.5-10 6-10 10.5"/>
  <path d="M7 21c0-3 10-3 10 0"/>
  <path d="M8 8h8"/>
</svg>
SVG
```

- [ ] **Step 3: Rasterise at 3×**

```bash
cd client/Assets/UI/Art/icons
for f in *.svg; do
  n="${f%.svg}"
  # 19px logical at 3x = 57px. rsvg-convert if present, else sips via a PDF.
  if command -v rsvg-convert >/dev/null; then
    rsvg-convert -w 57 -h 57 -o "$n.png" "$f"
  else
    qlmanage -t -s 57 -o . "$f" >/dev/null 2>&1 && mv "$f.png" "$n.png"
  fi
done
rm -f *.svg && ls *.png | wc -l
```

Expected: 13 PNGs at 57×57. The SVGs are removed because they are not shipped — only the raster is. Keep them in the commit message's record if a redraw is ever needed; `lucide.dev` is the durable source.

**If neither tool is available**, `npm exec -y svgexport -- <in.svg> <out.png> 57:57` works and needs no install.

- [ ] **Step 4: The stylesheet**

Create `client/Assets/UI/Shell/icons.uss`:

```css
/* Glyphs. Lucide (MIT, see LICENSE-lucide.txt) at 24/2.0, rescaled to the
 * handoff's 19px / 1.9 stroke / no fill; `splice` is drawn to match because no
 * general set carries it.
 *
 * RASTER, NOT VECTOR. UI Toolkit reads SVG only through the Vector Graphics
 * package, and ad7ff3c removed two unused packages from the client build this
 * phase last. A dependency for glyphs that never exceed 24px is not a trade
 * worth making, so these are 3x PNGs: 57px authored, 19px drawn.
 */
.icon {
    width: 19px;
    height: 19px;
    -unity-background-scale-mode: scale-to-fit;
    -unity-background-image-tint-color: var(--mute-soft);
}
.icon--map     { background-image: url("project:///Assets/UI/Art/icons/map.png"); }
.icon--ark     { background-image: url("project:///Assets/UI/Art/icons/ark.png"); }
.icon--splice  { background-image: url("project:///Assets/UI/Art/icons/splice.png"); }
.icon--lab     { background-image: url("project:///Assets/UI/Art/icons/lab.png"); }
.icon--allies  { background-image: url("project:///Assets/UI/Art/icons/allies.png"); }
.icon--back    { background-image: url("project:///Assets/UI/Art/icons/back.png"); }
.icon--charge  { background-image: url("project:///Assets/UI/Art/icons/charge.png"); }
.icon--shard   { background-image: url("project:///Assets/UI/Art/icons/shard.png"); }
.icon--tier    { background-image: url("project:///Assets/UI/Art/icons/tier.png"); }
.icon--timer   { background-image: url("project:///Assets/UI/Art/icons/timer.png"); }
.icon--lock    { background-image: url("project:///Assets/UI/Art/icons/lock.png"); }
.icon--check   { background-image: url("project:///Assets/UI/Art/icons/check.png"); }
.icon--warning { background-image: url("project:///Assets/UI/Art/icons/warning.png"); }

/* The tab bar's two states, per the handoff's nav model. */
.tab-bar__tab .icon            { -unity-background-image-tint-color: var(--mute-soft); }
.tab-bar__tab--active .icon    { -unity-background-image-tint-color: var(--violet-text); }
```

Add it to `Shell.uxml` after `Theme.uss`:

```xml
    <ui:Style src="icons.uss" />
```

- [ ] **Step 5: Give each tab its glyph**

In `TabBar.cs`, each tab button gains an icon child before its label. The tabs and their glyph names are `Map→map`, `Ark→ark`, `Splice→splice`, `Lab→lab`, `Allies→allies`. Add to the per-tab construction:

```csharp
var icon = new VisualElement();
icon.AddToClassList("icon");
icon.AddToClassList("icon--" + tab.ToString().ToLowerInvariant());
icon.pickingMode = PickingMode.Ignore;   // the button takes the click, not the glyph
button.Insert(0, icon);
```

- [ ] **Step 6: Extend `TabBarTests`**

Add to `client/Assets/UI/Tests/TabBarTests.cs`, in the assembly's established no-panel style:

```csharp
[Test]
public void EveryTabCarriesItsOwnGlyph()
{
    var bar = new TabBar();
    foreach (var tab in System.Enum.GetValues(typeof(Tab)).Cast<Tab>())
    {
        var button = bar.Q<Button>(tab.ToString());
        Assert.IsNotNull(button, $"no button for {tab}");
        var icon = button.Q<VisualElement>(className: "icon");
        Assert.IsNotNull(icon, $"{tab} has no .icon child - a nav bar of bare words");
        Assert.IsTrue(icon.ClassListContains("icon--" + tab.ToString().ToLowerInvariant()),
                      $"{tab}'s glyph is not its own");
    }
}
```

- [ ] **Step 7: Run, then weaken**

```bash
./implementation/scripts/run-unity-tests.sh EditMode 2>&1 | tail -6
```

Expected: pass. Then prove it discriminates — delete the `icon--` class line in `TabBar.cs`, re-run, expect `Splice's glyph is not its own` (or whichever tab is enumerated first), restore, re-run.

- [ ] **Step 8: Commit**

```bash
git add client/Assets/UI/Art/icons client/Assets/UI/Shell/icons.uss \
        client/Assets/UI/Shell/Shell.uxml client/Assets/UI/Shell/TabBar.cs \
        client/Assets/UI/Tests/TabBarTests.cs
git commit -m "feat(ui): thirteen glyphs, and a nav bar that is not bare words

Lucide (MIT) at 24/2.0 rescaled to the handoff's 19px / 1.9 / no fill,
plus a splice mark drawn to match because no general set carries one.

Raster rather than vector, deliberately: UI Toolkit reads SVG only
through the Vector Graphics package and ad7ff3c removed two unused
packages from this build. A dependency for glyphs that never exceed 24px
is not worth it, so the sheet is authored at 3x.

TabBarTests now asserts each tab carries its OWN glyph, not merely some
glyph - seen to fail by dropping the modifier class.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 5: Motion

Design §4.5. Small, self-contained, and it has no test — stated plainly rather than papered over.

**Files:**
- Create: `client/Assets/UI/Shell/Motion.uss`
- Modify: `client/Assets/UI/Shell/Tokens.uss`, `client/Assets/UI/Shell/Shell.uxml`

- [ ] **Step 1: Duration tokens**

Add to `Tokens.uss`:

```css
    /* ---- Motion. The handoff's named animation set. ---- */
    --motion-press: 60ms;      /* already used by the CTA press */
    --motion-quick: 120ms;     /* selection, tint, tick */
    --motion-screen: 220ms;    /* push, pop, sheet */
```

- [ ] **Step 2: The vocabulary**

Create `client/Assets/UI/Shell/Motion.uss`:

```css
/* The handoff's animation set as a class vocabulary. bible 10.6 makes the
 * handoff's named animations authoritative alongside colour and spacing.
 *
 * THE CTA PRESS IS NOT HERE. It is in Theme.uss, it is correct, and bible 10.6
 * names it specifically. Re-implementing it would be a second source.
 *
 * THIS FILE HAS NO TEST, and that is stated rather than hidden. USS transitions
 * are not observable from Broodline.UI.Tests - there is no panel, so nothing
 * ticks. What IS gated is that the durations are tokens (verify-uss-tokens.sh)
 * and that the screens look right (Task 16's eyes-on pass over Task 15's
 * screenshots). Motion is the one part of this phase where the screenshot
 * corpus genuinely cannot substitute for looking at the running app.
 */
.motion-screen-in {
    transition-property: opacity, translate;
    transition-duration: var(--motion-screen);
    transition-timing-function: ease-out;
}
.motion-screen-in--enter { opacity: 0; translate: 12px 0; }

.sheet-layer > * {
    transition-property: translate;
    transition-duration: var(--motion-screen);
    transition-timing-function: ease-out;
}
.sheet-layer--hidden > * { translate: 0 100%; }

.option-row {
    transition-property: border-color, background-color;
    transition-duration: var(--motion-quick);
}

.t-num {
    transition-property: color;
    transition-duration: var(--motion-quick);
}

.reveal-flare {
    transition-property: opacity, scale;
    transition-duration: var(--motion-screen);
    transition-timing-function: ease-out;
}
.reveal-flare--hidden { opacity: 0; scale: 0.9 0.9; }
```

Add to `Shell.uxml` after `icons.uss`:

```xml
    <ui:Style src="Motion.uss" />
```

- [ ] **Step 3: Verify and commit**

```bash
bash implementation/scripts/verify-uss-tokens.sh && ./implementation/scripts/run-unity-tests.sh EditMode 2>&1 | tail -4
git add client/Assets/UI/Shell/Motion.uss client/Assets/UI/Shell/Tokens.uss client/Assets/UI/Shell/Shell.uxml
git commit -m "feat(ui): the motion vocabulary, and an honest note that it has no test

Five transitions as classes - screen enter, sheet slide, option row,
numeral tick, reveal flare - with durations as tokens.

The CTA press stays in Theme.uss. It is correct and bible 10.6 names it;
a second copy would be a second source of truth.

The file says outright that nothing here is gated by a test, because a
USS transition needs a panel to tick and Broodline.UI.Tests has none.
Motion is the one part of this phase the screenshot corpus cannot check,
and Task 16 is where it gets looked at.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 6: The species palette — measure it, baseline it, and hand back a decision

Design §4.6, **as amended**. Read "What this plan found the design missed" item 1 before starting: the design asks for an assertion that cannot be satisfied and would have passed while the palette got worse.

**This task does not change a single species colour.** It builds the measurement that makes the change decidable, and Task 19 records whatever is decided.

**Files:**
- Create: `client/Assets/UI/Diagnostics/PaletteContrast.cs` (the maths), `client/Assets/UI/Tests/PaletteContrastTests.cs` (the gate), `client/Assets/Editor/PaletteBaselineWriter.cs` (the emitter), `implementation/results/palette-cvd-baseline.txt`

**Why the maths is in `Broodline.UI` and not in the test assembly.** `Broodline.UI.Tests.asmdef` is `autoReferenced: false` and carries `defineConstraints: ["UNITY_INCLUDE_TESTS"]`, so nothing outside it can call into it — the emitter in `Assembly-CSharp-Editor` could not reach a `Measure()` that lived there. Putting it in `Broodline.UI` (which *is* `autoReferenced`) gives the test and the emitter one shared implementation, which is the property that matters: a baseline written by different code than the code that checks it is not a baseline.

**Interfaces:**
- Produces: `palette-cvd-baseline.txt`, a tracked artifact in the shape of `corpus-baseline.txt` — pinned measurements that a test re-derives and compares.

- [ ] **Step 1: Write the measurement as a test**

Create `client/Assets/UI/Diagnostics/PaletteContrast.cs` — the measurement, shared by the gate and the emitter:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace Broodline.UI.Diagnostics
{
    /// The species palette's separation under colour-vision deficiency.
    ///
    /// IN Broodline.UI RATHER THAN THE TEST ASSEMBLY, because the baseline
    /// emitter lives in Assembly-CSharp-Editor and Broodline.UI.Tests is
    /// autoReferenced:false behind UNITY_INCLUDE_TESTS - nothing outside it
    /// can call in. A baseline written by different code than the code that
    /// checks it is not a baseline, so both read this.
    ///
    /// Matrices: Machado, Oliveira and Fernandes (2009), severity 1.0, sRGB.
    public static class PaletteContrast
    {
        public static readonly (string Name, string Hex)[] Species =
        {
            ("Vetch",  "#6ba7c0"), ("Ember", "#e5867a"), ("Skitter", "#e8b34a"),
            ("Hollow", "#7a6ac0"), ("Loam",  "#7cc492"), ("Pale",    "#a9b0c4"),
        };

        static readonly Dictionary<string, double[][]> Cvd = new()
        {
            ["deutan"] = new[] { new[]{ 0.367322, 0.860646,-0.227968},
                                 new[]{ 0.280085, 0.672501, 0.047413},
                                 new[]{-0.011820, 0.042940, 0.968881} },
            ["protan"] = new[] { new[]{ 0.152286, 1.052583,-0.204868},
                                 new[]{ 0.114503, 0.786281, 0.099216},
                                 new[]{-0.003882,-0.048116, 1.051998} },
            ["tritan"] = new[] { new[]{ 1.255528,-0.076749,-0.178779},
                                 new[]{-0.078411, 0.930809, 0.147602},
                                 new[]{ 0.004733, 0.691367, 0.303900} },
        };

        static double[] Rgb(string hex) => new[]
        {
            Convert.ToInt32(hex.Substring(1, 2), 16) / 255.0,
            Convert.ToInt32(hex.Substring(3, 2), 16) / 255.0,
            Convert.ToInt32(hex.Substring(5, 2), 16) / 255.0,
        };

        static double Linear(double c) => c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);

        static double Lstar(double[] rgb)
        {
            var l = rgb.Select(c => Linear(Math.Clamp(c, 0, 1))).ToArray();
            var y = 0.2126 * l[0] + 0.7152 * l[1] + 0.0722 * l[2];
            return y > 0.008856 ? 116.0 * Math.Cbrt(y) - 16.0 : 903.3 * y;
        }

        static double[] Simulate(double[] rgb, string k)
        {
            var m = Cvd[k];
            return Enumerable.Range(0, 3)
                             .Select(i => m[i][0] * rgb[0] + m[i][1] * rgb[1] + m[i][2] * rgb[2])
                             .ToArray();
        }

        /// Every unordered pair under every deficiency, ordinal-sorted so the
        /// emitted file is stable and a diff is readable. Keys are
        /// "A/B\tdeficiency"; values are dL* to one decimal.
        public static SortedDictionary<string, double> Measure()
        {
            var result = new SortedDictionary<string, double>(StringComparer.Ordinal);
            for (int i = 0; i < Species.Length; i++)
                for (int j = i + 1; j < Species.Length; j++)
                    foreach (var k in Cvd.Keys.OrderBy(x => x, StringComparer.Ordinal))
                    {
                        var a = Lstar(Simulate(Rgb(Species[i].Hex), k));
                        var b = Lstar(Simulate(Rgb(Species[j].Hex), k));
                        result[$"{Species[i].Name}/{Species[j].Name}\t{k}"] = Math.Round(Math.Abs(a - b), 1);
                    }
            return result;
        }
    }
}
```

Then create `client/Assets/UI/Tests/PaletteContrastTests.cs` — the gate:

```csharp
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Broodline.UI.Diagnostics;
using NUnit.Framework;

namespace Broodline.UI.Tests
{
    /// WHY THIS IS A BASELINE AND NOT A THRESHOLD.
    /// broodline_accessibility.md section 4 asks to "widen the lightness
    /// separation between the two collapsing pairs". Measured, that
    /// prescription does not survive:
    ///
    ///   - Vetch/Hollow, which section 4 names, does NOT collapse: dL* 13.3
    ///     under deuteranopia, 16.6 under protanopia. It is the BEST of the
    ///     pairs that section discusses.
    ///   - The palette's genuinely worst pair is Skitter/Pale at dL* 0.2 under
    ///     protanopia, which section 4 never mentions.
    ///   - Fixing only the named pairs to dL* >= 12 (Skitter -> #cc901a, Loam
    ///     -> #9cd2ac) drops Ember/Skitter to 0.1. Whack-a-mole.
    ///   - Six hues give 15 pairs x 3 deficiencies = 45 constraints against 6
    ///     free lightness values. The best reachable global minimum is dL* 9.7
    ///     and it costs the palette its character entirely (Loam #337045,
    ///     Hollow #443681, Pale #e1e3ea) - the one thing section 4 promises
    ///     the fix would preserve.
    ///
    /// So this asserts no threshold. It pins all 45 measurements and fails
    /// when any of them gets WORSE, which makes the palette's CVD behaviour a
    /// tracked artifact in the shape of corpus-baseline.txt and leaves the
    /// real decision - repaint, or rely on silhouette - a human one with
    /// numbers attached. bible 10.4 already leans: "Colour never carries
    /// information alone."
    public class PaletteContrastTests
    {
        static readonly string BaselinePath = Path.GetFullPath(Path.Combine(
            UnityEngine.Application.dataPath, "../../implementation/results/palette-cvd-baseline.txt"));

        static SortedDictionary<string, double> ReadBaseline()
        {
            Assert.IsTrue(File.Exists(BaselinePath), $"no baseline at {BaselinePath}");
            var d = new SortedDictionary<string, double>(StringComparer.Ordinal);
            foreach (var line in File.ReadAllLines(BaselinePath))
            {
                if (line.StartsWith("#") || line.Trim().Length == 0) continue;
                var p = line.Split('\t');
                Assert.AreEqual(3, p.Length, $"malformed baseline row: {line}");
                d[$"{p[0]}\t{p[1]}"] = double.Parse(p[2], CultureInfo.InvariantCulture);
            }
            return d;
        }

        [Test]
        public void EveryPairIsStillMeasured()
        {
            var now = PaletteContrast.Measure();
            Assert.AreEqual(45, now.Count, "six species give fifteen pairs across three deficiencies");
            CollectionAssert.AreEquivalent(ReadBaseline().Keys, now.Keys,
                "the set of measured pairs changed - a species was added, removed or renamed");
        }

        [Test]
        public void NoPairSeparatesLessWellThanItDidWhenPinned()
        {
            var now = PaletteContrast.Measure();
            var pinned = ReadBaseline();
            var worse = now.Where(kv => kv.Value < pinned[kv.Key] - 0.05)
                           .Select(kv => $"  {kv.Key.Replace('\t', ' ')}: {pinned[kv.Key]:F1} -> {kv.Value:F1}")
                           .ToList();
            Assert.IsEmpty(worse,
                "a species colour changed and made CVD separation WORSE:\n" + string.Join("\n", worse) +
                "\n\nIf this is intentional, re-pin palette-cvd-baseline.txt in the same commit " +
                "and say in the message which pair you traded away and for what.");
        }
    }
}
```

- [ ] **Step 2: Run it and watch it fail on the missing baseline**

```bash
./implementation/scripts/run-unity-tests.sh EditMode 2>&1 | tail -12
```

Expected: both `PaletteContrastTests` fail with `no baseline at .../palette-cvd-baseline.txt`.

- [ ] **Step 3: Emit the baseline from the code that reads it**

Create `client/Assets/Editor/PaletteBaselineWriter.cs`:

```csharp
using System.IO;
using System.Linq;
using Broodline.UI.Diagnostics;
using UnityEditor;

// No namespace: Assembly-CSharp-Editor, as with WaveBuilder.
//
/// Writes palette-cvd-baseline.txt from the SAME PaletteContrast.Measure()
/// the test reads, so the file can never disagree with the code that checks
/// it. The emit-corpus-baseline.sh pattern, in C#.
    public static class PaletteBaselineWriter
    {
        [MenuItem("Broodline/Write Palette CVD Baseline")]
        public static void Write()
        {
            var path = Path.GetFullPath(Path.Combine(
                UnityEngine.Application.dataPath, "../../implementation/results/palette-cvd-baseline.txt"));
            var rows = PaletteContrast.Measure().Select(kv => $"{kv.Key}\t{kv.Value:F1}");
            File.WriteAllText(path, Header + string.Join("\n", rows) + "\n");
            UnityEngine.Debug.Log($"wrote {path}");
        }

        const string Header = @"# Species palette under colour-vision deficiency — DATA, NOT PROSE.
#
# dL* (CIE L*) between each species pair, under each deficiency, at severity
# 1.0 (Machado, Oliveira & Fernandes 2009). Lower is worse; 0 means the two are
# indistinguishable to that viewer by lightness.
#
# THIS FILE IS A BASELINE, NOT A TARGET. See PaletteContrastTests for why a
# threshold is not available: 45 constraints, 6 free values, and the best
# reachable global minimum is 9.7 at the cost of the palette's whole character.
# The test fails when a number here gets WORSE, which is the question a change
# can actually answer.
#
# Regenerate: Unity -> Broodline/Write Palette CVD Baseline, or
#   Unity -executeMethod PaletteBaselineWriter.Write
#
# WHAT THIS FILE SAYS ABOUT broodline_accessibility.md section 4, which named
# the wrong pairs:
#   Vetch/Hollow    deutan  13.3  <- section 4 says this collapses. It does not.
#   Ember/Loam      deutan   4.9  <- section 4 is right about this one.
#   Skitter/Pale    protan   0.2  <- the worst pair in the palette. Unmentioned.
#
";
    }
```

```bash
"/Applications/Unity/Hub/Editor/6000.6.0f1/Unity.app/Contents/MacOS/Unity" -batchmode -quit \
  -projectPath "$(pwd)/client" -executeMethod PaletteBaselineWriter.Write -logFile - | tail -3
head -25 implementation/results/palette-cvd-baseline.txt
sort -t$'\t' -k3 -n implementation/results/palette-cvd-baseline.txt | grep -v '^#' | head -6
```

Expected: 45 data rows; the six worst are `Skitter/Pale protan 0.2`, `Loam/Pale deutan 0.7`, `Ember/Vetch tritan 1.2`, `Loam/Pale tritan 1.3`, `Loam/Skitter tritan 1.7`, `Loam/Skitter protan 1.9`.

- [ ] **Step 4: Confirm it is tracked**

`implementation/results/*` is gitignored except `*.csv` and `*-test-baseline.txt`. This file is neither.

```bash
git check-ignore -v implementation/results/palette-cvd-baseline.txt
```

If it reports a match, **use `git add -f`** in Step 7 exactly as every prior phase does for the capture files, and say so in the commit message. Do not widen the ignore rule: the rule is deliberately narrow and a fifth exception dilutes it.

- [ ] **Step 5: Run the tests green, then weaken**

```bash
./implementation/scripts/run-unity-tests.sh EditMode 2>&1 | tail -6

# Weaken: apply section 4's own prescription and watch it register as a regression.
sed -i '' 's/("Skitter", "#e8b34a")/("Skitter", "#cc901a")/; s/("Loam",  "#7cc492")/("Loam",  "#9cd2ac")/' \
  client/Assets/UI/Diagnostics/PaletteContrast.cs
./implementation/scripts/run-unity-tests.sh EditMode 2>&1 | grep -A8 "made CVD separation WORSE"
git checkout client/Assets/UI/Diagnostics/PaletteContrast.cs
./implementation/scripts/run-unity-tests.sh EditMode 2>&1 | tail -4
```

Expected on the weakening: a failure naming `Ember/Skitter protan: 15.2 -> 0.1` among others. **That transcript is the evidence for this task and goes in the commit message**, because it is the proof that §4's prescription is net-negative rather than an assertion that it is.

- [ ] **Step 6: Write the decision memo**

Create `implementation/results/palette-decision.md` — not a spec edit, because the decision is not taken yet:

```markdown
# The species palette — three options, costed

Measured by `PaletteContrastTests`; numbers from `palette-cvd-baseline.txt`.
`broodline_accessibility.md` §4's prescription is not among the options because
it was measured and is net-negative: see §4 of that file and this task's commit.

**A. Leave it. Rely on silhouette.** Zero cost. Worst pair stays Skitter/Pale
at ΔL\* 0.2 under protanopia. Bible §10.4 already carries this: *"Colour never
carries information alone"*, and §10.2 rule 1 makes all six distinguishable as
flat black shapes at 40px — which Task 13 turns into an assertion. This is the
option the design documents already argue for.

**B. Targeted repaint.** Move Skitter and Loam only. Raises Ember/Loam 4.9 →
12.1 and Skitter/Loam 1.7 → 14.9, and **drops Ember/Skitter 15.2 → 0.1.** Net
negative. Recorded so nobody re-derives it.

**C. Global repaint.** All six lightness values re-solved. Best achievable worst
pair ΔL\* 9.7. Costs: Loam `#7cc492`→`#337045`, Hollow `#7a6ac0`→`#443681`,
Pale `#a9b0c4`→`#e1e3ea`, Vetch `#6ba7c0`→`#a7cad9`, Skitter `#e8b34a`→`#b27e17`.
The palette's character does not survive, and §4's promise that the fix
"preserves the palette's character" is what fails.

**The decision gates Phase 9**, because species colour is on the body, the card,
the map marker and the store icon. It does not gate the rest of Phase 8.

**Recommendation: A**, with the baseline and Task 13's 40px assertion as the
standing evidence that the shortcut's absence is survivable.
```

- [ ] **Step 7: Commit**

```bash
git add client/Assets/UI/Diagnostics/PaletteContrast.cs \
        client/Assets/UI/Tests/PaletteContrastTests.cs client/Assets/Editor/PaletteBaselineWriter.cs
git add -f implementation/results/palette-cvd-baseline.txt implementation/results/palette-decision.md
git commit -m "test(ui): the palette's CVD behaviour becomes a tracked artifact

accessibility.md section 4 asks for a fix that measurement does not
support. It names Vetch/Hollow as collapsing - dL* is 13.3 under
deuteranopia, the best of the pairs it discusses - and never mentions
Skitter/Pale at 0.2 under protanopia, which is the worst in the palette.

Applying section 4's prescription was run and recorded: Skitter #cc901a
and Loam #9cd2ac raise Ember/Loam 4.9 -> 12.1 and Skitter/Loam 1.7 ->
14.9, and drop Ember/Skitter 15.2 -> 0.1. Net negative, and the test
caught it, which is the evidence that this is a baseline and not a
threshold.

Six hues give 45 constraints against 6 free lightness values. The best
reachable global minimum is 9.7 and it costs the palette its character -
the one thing section 4 promises the fix would preserve.

So nothing is repainted here. The 45 measurements are pinned in the shape
of corpus-baseline.txt, the test fails when any of them worsens, and
palette-decision.md puts three costed options in front of a human.
bible 10.4 already leans: colour never carries information alone.

Tracked with -f: implementation/results/* is ignored except *.csv and
*-test-baseline.txt, and the narrowness of that rule is worth keeping.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 7: `ScreenScaffold`, and the gate that stops the thirteenth screen skipping it

Design §5.1.

**Files:**
- Create: `client/Assets/UI/Components/ScreenScaffold.cs`, `Resources/ScreenScaffold.uxml`, `Resources/ScreenScaffold.uss`
- Create: `client/Assets/UI/Tests/ScaffoldTests.cs`

**Interfaces:**
- Produces, and every screen task from 9 onward consumes exactly this:

```csharp
public sealed class ScreenScaffold : VisualElement
{
    public const string UssClassName = "screen-scaffold";
    public ScreenScaffold(string title, bool pushed = false, Action onBack = null);
    public VisualElement Content    { get; }  // add screen body here
    public VisualElement CtaRow     { get; }  // add primary/secondary buttons here
    public VisualElement HeaderSlot { get; }  // currency header or avatar, per screen
    public string FooterNote { set; }         // null or "" hides the row
}
```

- [ ] **Step 1: Write the failing test**

Create `client/Assets/UI/Tests/ScaffoldTests.cs`:

```csharp
using System;
using System.Linq;
using System.Reflection;
using Broodline.UI.Components;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Broodline.UI.Tests
{
    /// The handoff's layout frame, and the rule that every screen uses it.
    ///
    /// The frame is: status bar -> header -> [tab bar] -> content (flex 1,
    /// scrolls) -> CTA row -> footer note -> bottom nav. The shell owns the
    /// bars; this owns everything between them.
    ///
    /// EveryScreenComposesTheScaffold is the load-bearing one. Phase 7 built
    /// twelve screens as bare VisualElements with no shared chrome, which is
    /// why nothing lined up. A reflection sweep means a thirteenth cannot
    /// quietly do the same.
    ///
    /// NAVIGATION IS NOT HERE. ScreenHost already enforces that a pushed
    /// sub-screen hides the tab bar (client_architecture section 9). The
    /// scaffold renders a back affordance when told to and calls back; it does
    /// not decide depth. Two owners of that rule would be one too many.
    public class ScaffoldTests
    {
        [Test]
        public void TheScaffoldCarriesHeaderContentAndCtaRow()
        {
            var s = new ScreenScaffold("Gene Ark");
            Assert.IsNotNull(s.Q<VisualElement>("header"), "no header");
            Assert.IsNotNull(s.Content, "no content region");
            Assert.IsNotNull(s.CtaRow, "no CTA row");
            Assert.AreEqual("Gene Ark", s.Q<Label>("title").text);
        }

        [Test]
        public void ATopLevelScreenHasNoBackChevron()
        {
            var s = new ScreenScaffold("Roster");
            Assert.IsNull(s.Q<Button>("back"),
                "a top-level screen showed a back chevron; the handoff gives one only to pushed sub-screens");
        }

        [Test]
        public void APushedScreenHasABackChevronThatCallsBack()
        {
            var called = 0;
            var s = new ScreenScaffold("Splice Reveal", pushed: true, onBack: () => called++);
            var back = s.Q<Button>("back");
            Assert.IsNotNull(back, "a pushed sub-screen has no back chevron");
            Assert.IsNotNull(back.Q<VisualElement>(className: "icon--back"), "the chevron has no glyph");

            // No panel in this assembly, so the callback is invoked directly -
            // the constraint ComponentTests documents. What is proven is that
            // the button is wired to the caller's action, not that a click
            // dispatches.
            back.clicked?.Invoke();
            Assert.AreEqual(1, called, "the back button is not wired to onBack");
        }

        [Test]
        public void AnEmptyFooterNoteHidesItsRow()
        {
            var s = new ScreenScaffold("Roster");
            Assert.AreEqual(DisplayStyle.None, s.Q<Label>("footer-note").resolvedStyle.display,
                "an unset footer note still occupies a row");
            s.FooterNote = "Consumes both parents.";
            Assert.AreEqual(DisplayStyle.Flex, s.Q<Label>("footer-note").resolvedStyle.display);
        }

        [Test]
        public void EveryScreenComposesTheScaffold()
        {
            // The two OVERLAYS in this namespace are not screens and take no
            // scaffold. Exempted BY NAME, not by a namespace accident:
            //   CodexSheet  - a bottom sheet. ScreenHost.ShowSheet overlays it,
            //                 never pushes it, and it never touches the back
            //                 stack (client_architecture section 9).
            //   WaveHudView - a HUD drawn over the wave scene, not a screen
            //                 the host swaps in.
            // The list is asserted to be exactly these two, so a third screen
            // cannot be quietly excused by adding a name here.
            var exempt = new[] { "CodexSheet", "WaveHudView" };

            var all = typeof(Broodline.UI.Screens.RosterView).Assembly
                .GetTypes()
                .Where(t => t.Namespace == "Broodline.UI.Screens"
                            && typeof(VisualElement).IsAssignableFrom(t)
                            && !t.IsAbstract
                            && t.GetConstructor(Type.EmptyTypes) != null)
                .ToList();

            Assert.That(all.Count, Is.EqualTo(12),
                "the namespace holds 12 constructible VisualElements; if this moved, the " +
                "sweep's exemption list below needs re-deciding rather than silently widening");

            var screens = all.Where(t => !exempt.Contains(t.Name)).ToList();
            Assert.That(screens.Count, Is.EqualTo(10), "10 screens must carry the frame");

            var bare = screens
                .Where(t => ((VisualElement)Activator.CreateInstance(t))
                            .Q<VisualElement>(className: ScreenScaffold.UssClassName) == null)
                .Select(t => t.Name)
                .ToList();

            Assert.IsEmpty(bare,
                "these screens do not compose ScreenScaffold, so they carry no header, no CTA row " +
                "and no scrolling content region:\n  " + string.Join("\n  ", bare));
        }
    }
}
```

- [ ] **Step 2: Run it and confirm it fails on the right thing**

```bash
./implementation/scripts/run-unity-tests.sh EditMode 2>&1 | tail -20
```

Expected: every `ScaffoldTests` case fails to compile or fails on a missing `ScreenScaffold`. That is the point — `EveryScreenComposesTheScaffold` will list all eleven screens once the type exists, and Tasks 9–12 empty that list.

- [ ] **Step 3: The markup**

`client/Assets/UI/Components/Resources/ScreenScaffold.uxml`:

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements" editor-extension-mode="False">
    <ui:Style src="ScreenScaffold.uss" />
    <ui:VisualElement name="header" class="screen-scaffold__header">
        <ui:Button name="back" class="screen-scaffold__back" />
        <ui:Label name="title" class="screen-scaffold__title t-screen-title" />
        <ui:VisualElement name="header-slot" class="screen-scaffold__header-slot" />
    </ui:VisualElement>
    <ui:ScrollView name="content" class="screen-scaffold__content" />
    <ui:VisualElement name="cta-row" class="screen-scaffold__cta-row" />
    <ui:Label name="footer-note" class="screen-scaffold__footer-note t-secondary" />
</ui:UXML>
```

`ScreenScaffold.uss`:

```css
/* The handoff's fixed-height column: header fixed, content the only flexible
 * region, CTA row and footer note pinned under it. Screen gutter is 12px and
 * header padding is 8/14/10, both straight from the handoff. */
.screen-scaffold { flex-grow: 1; flex-direction: column; }

.screen-scaffold__header {
    flex-direction: row;
    align-items: center;
    padding-top: var(--space-2);
    padding-bottom: 10px;
    padding-left: 14px;
    padding-right: 14px;
}

.screen-scaffold__back {
    width: 34px; height: 34px;
    margin-right: var(--space-2);
    border-radius: var(--radius-tile);
    border-width: 0;
    background-color: var(--surface-sunk);
    align-items: center;
    justify-content: center;
}

.screen-scaffold__title { flex-grow: 1; }
.screen-scaffold__header-slot { flex-direction: row; align-items: center; }

.screen-scaffold__content {
    flex-grow: 1;
    padding-left: var(--gutter);
    padding-right: var(--gutter);
}

.screen-scaffold__cta-row {
    flex-direction: column;
    padding-left: var(--gutter);
    padding-right: var(--gutter);
    padding-top: var(--space-2);
}

.screen-scaffold__footer-note {
    -unity-text-align: middle-center;
    color: var(--mute);
    padding-top: var(--space-2);
    padding-bottom: var(--space-2);
}
```

- [ ] **Step 4: The class**

`client/Assets/UI/Components/ScreenScaffold.cs`:

```csharp
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Broodline.UI.Components
{
    /// The handoff's layout frame, built once.
    ///
    /// Phase 7 shipped twelve screens as bare VisualElements: no header, no
    /// CTA row, no scrolling content region, each stylesheet re-deriving its
    /// own padding. This is that chrome, and ScaffoldTests asserts every
    /// screen uses it.
    ///
    /// It does NOT own navigation. ScreenHost decides depth and hides the tab
    /// bar (client_architecture section 9); this renders a chevron when told
    /// to and calls back.
    public sealed class ScreenScaffold : VisualElement
    {
        public const string UssClassName = "screen-scaffold";

        readonly Label _footer;

        public VisualElement Content { get; }
        public VisualElement CtaRow { get; }
        /// For a currency header or an avatar, per screen.
        public VisualElement HeaderSlot { get; }

        public string FooterNote
        {
            set
            {
                _footer.text = value ?? string.Empty;
                _footer.style.display = string.IsNullOrEmpty(value)
                    ? DisplayStyle.None : DisplayStyle.Flex;
            }
        }

        public ScreenScaffold(string title, bool pushed = false, Action onBack = null)
        {
            AddToClassList(UssClassName);
            Resources.Load<VisualTreeAsset>("ScreenScaffold").CloneTree(this);

            this.Q<Label>("title").text = title ?? string.Empty;
            Content = this.Q<ScrollView>("content");
            CtaRow = this.Q<VisualElement>("cta-row");
            HeaderSlot = this.Q<VisualElement>("header-slot");
            _footer = this.Q<Label>("footer-note");
            FooterNote = null;

            var back = this.Q<Button>("back");
            if (!pushed)
            {
                // Removed, not hidden: ATopLevelScreenHasNoBackChevron asserts
                // absence, and a hidden button is still focusable by a screen
                // reader. accessibility section 3 wants menus done properly.
                back.RemoveFromHierarchy();
            }
            else
            {
                var glyph = new VisualElement();
                glyph.AddToClassList("icon");
                glyph.AddToClassList("icon--back");
                glyph.pickingMode = PickingMode.Ignore;
                back.Add(glyph);
                if (onBack != null) back.clicked += onBack;
            }
        }
    }
}
```

- [ ] **Step 5: Run — four green, one listing eleven failures**

```bash
./implementation/scripts/run-unity-tests.sh EditMode 2>&1 | tail -20
```

Expected: the four structural tests pass; `EveryScreenComposesTheScaffold` fails and names all eleven screens. **Record that list** — it is Tasks 9–12's checklist, and the test going green is how those tasks know they are done.

- [ ] **Step 6: Commit, with the failure deliberately red**

The house rule from Phase 6 applies: a red that means "work is owed" is better than a skip.

```bash
git add client/Assets/UI/Components/ScreenScaffold.cs \
        client/Assets/UI/Components/Resources/ScreenScaffold.uxml \
        client/Assets/UI/Components/Resources/ScreenScaffold.uss \
        client/Assets/UI/Tests/ScaffoldTests.cs
git commit -m "feat(ui): the layout frame every screen was missing

The handoff specifies one column - header, scrolling content, CTA row,
footer note - and nothing in the codebase provided it. Twelve screens
were bare VisualElements each re-deriving its own padding, which is why
nothing lined up.

EveryScreenComposesTheScaffold is DELIBERATELY RED at this commit and
names all eleven screens. It is Tasks 9-12's checklist, and it goes green
when they are done. A skip here would be indistinguishable from success;
a failure is visible.

The scaffold does not own navigation - ScreenHost already decides depth
and hides the tab bar. It renders a chevron when told to and calls back.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 8: The shared component layer

Design §5.2. Five components, each built once, each with a structural test in `ComponentTests`' established style.

**Files:**
- Create, each with `Resources/<Name>.uxml` and `.uss`: `client/Assets/UI/Components/OptionRow.cs`, `SectionCard.cs`, `StatCell.cs`, `ProgressBar.cs`, `EmptyState.cs`
- Modify: `client/Assets/UI/Tests/ComponentTests.cs`

**Interfaces:**
- Produces, consumed verbatim by Tasks 9–12:

```csharp
public sealed class OptionRow : VisualElement   // Ctor(string title, string detail, Action onSelect)
{ public const string UssClassName = "option-row";
  public bool Selected { get; set; } }          // toggles --selected

public sealed class SectionCard : VisualElement // Ctor(string heading = null)
{ public const string UssClassName = "section-card";
  public VisualElement Body { get; } }

public sealed class StatCell : VisualElement    // Ctor(string label, string value)
{ public const string UssClassName = "stat-cell";
  public string Value { set; } }                // the Label carries .t-num

public sealed class ProgressBar : VisualElement // Ctor(float fill01, string modifier = null)
{ public const string UssClassName = "progress-bar";
  public float Fill { set; } }                  // clamped 0..1

public sealed class EmptyState : VisualElement  // Ctor(string message, string glyph = null)
{ public const string UssClassName = "empty-state"; }
```

- [ ] **Step 1: Write the failing tests**

Append to `client/Assets/UI/Tests/ComponentTests.cs`. These follow the file's own no-panel rule; `OptionRow`'s selection is a class-list assertion, not a dispatched click.

```csharp
[Test]
public void AnUnselectedOptionRowCarriesNeitherTheRingNorTheFill()
{
    var row = new OptionRow("Coast road", "4h · low risk", () => { });
    Assert.IsFalse(row.ClassListContains("option-row--selected"));
    Assert.AreEqual("Coast road", row.Q<Label>("title").text);
    Assert.AreEqual("4h · low risk", row.Q<Label>("detail").text);
}

[Test]
public void SelectingAnOptionRowIsAClassChangeAndNothingElse()
{
    var row = new OptionRow("Coast road", "4h · low risk", () => { });
    row.Selected = true;
    Assert.IsTrue(row.ClassListContains("option-row--selected"),
        "the handoff's selected treatment is a 2px ring and a white fill; both hang off this class");
    row.Selected = false;
    Assert.IsFalse(row.ClassListContains("option-row--selected"));
}

[Test]
public void AStatCellsValueIsANumeralAndCarriesTheNumeralClass()
{
    var cell = new StatCell("Travel", "4h 20m");
    Assert.AreEqual("Travel", cell.Q<Label>("label").text);
    var value = cell.Q<Label>("value");
    Assert.AreEqual("4h 20m", value.text);
    Assert.IsTrue(value.ClassListContains("t-num"),
        "a stat value without .t-num escapes bible 10.6's 11px floor and the tabular face");
}

[Test]
public void AProgressBarClampsRatherThanOverflowing()
{
    foreach (var (input, expected) in new[] { (0.5f, 50f), (-1f, 0f), (3f, 100f) })
    {
        var bar = new ProgressBar(input);
        Assert.AreEqual(expected, bar.Q<VisualElement>("fill").style.width.value.value, 0.01f,
            $"fill {input} should clamp to {expected}%");
    }
}

[Test]
public void ASectionCardWithNoHeadingDoesNotReserveOne()
{
    Assert.IsNull(new SectionCard().Q<Label>("heading"),
        "an unheaded card reserved a heading row");
    Assert.AreEqual("Lineage", new SectionCard("Lineage").Q<Label>("heading").text);
}

[Test]
public void AnEmptyStateSaysSomethingRatherThanRenderingNothing()
{
    var e = new EmptyState("No creatures yet.");
    Assert.AreEqual("No creatures yet.", e.Q<Label>("message").text);
}
```

- [ ] **Step 2: Run to see them fail**

```bash
./implementation/scripts/run-unity-tests.sh EditMode 2>&1 | tail -12
```

Expected: compile failure — none of the five types exist.

- [ ] **Step 3: Implement the five**

Each follows the `CreatureCard` shape exactly: `AddToClassList(UssClassName)`, `Resources.Load<VisualTreeAsset>(name).CloneTree(this)`, `Q<>` lookups.

**The element names are fixed by the tests above and are not free choices.** Each `.uxml` contains exactly these, in order:

| Component | Elements in its `.uxml` (`name` → type, class) |
|---|---|
| `OptionRow` | `title` → `Label.option-row__title.t-card-title`; `detail` → `Label.option-row__detail.t-secondary` |
| `SectionCard` | `heading` → `Label.section-card__heading.t-section` (**removed from the hierarchy when the ctor's `heading` is null**, per `ASectionCardWithNoHeadingDoesNotReserveOne`); `body` → `VisualElement.section-card__body`. The root carries `elev-1` |
| `StatCell` | `label` → `Label.stat-cell__label.t-micro`; `value` → `Label.stat-cell__value.t-num` |
| `ProgressBar` | `track` → `VisualElement.progress-bar__track`; `fill` → `VisualElement.progress-bar__fill`, a child of `track` |
| `EmptyState` | `glyph` → `VisualElement.icon` (removed when the ctor's `glyph` is null); `message` → `Label.empty-state__message.t-secondary` |

`OptionRow.uss` carries the handoff's two treatments verbatim:

```css
.option-row {
    flex-direction: column;
    background-color: var(--surface-sunk);
    border-width: 1px;
    border-color: var(--hairline);
    border-radius: var(--radius-row);
    padding: var(--space-3);
    margin-bottom: var(--space-2);
}
.option-row--selected {
    background-color: var(--surface);
    border-width: 2px;
    border-color: var(--ring);
}
.option-row__title  { font-size: var(--text-card-title); color: var(--ink); }
.option-row__detail { font-size: var(--text-secondary); color: var(--mute); }
```

`ProgressBar`'s fill is set as a percentage width, which is what the clamp test reads:

```csharp
public float Fill
{
    set => _fill.style.width = Length.Percent(Mathf.Clamp01(value) * 100f);
}
```

`SectionCard` carries `.elev-1` from Task 3. `StatCell`'s value label carries `t-num`. `EmptyState`'s glyph, when given, is `icon icon--<glyph>`.

- [ ] **Step 4: Run green, then weaken two**

```bash
./implementation/scripts/run-unity-tests.sh EditMode 2>&1 | tail -6

# Weaken 1: drop .t-num from StatCell's value -> the numeral test reddens.
# Weaken 2: replace Mathf.Clamp01 with the raw value -> the clamp test reddens
#           on both -1 and 3.
# Restore each, re-run, confirm green.
```

- [ ] **Step 5: Commit**

```bash
git add client/Assets/UI/Components client/Assets/UI/Tests/ComponentTests.cs
git commit -m "feat(ui): the five components the handoff repeats on every screen

OptionRow, SectionCard, StatCell, ProgressBar, EmptyState - built once,
in the shape CreatureCard and the other four already established.

OptionRow carries the handoff's two treatments verbatim: a 2px ring in
--ring over --surface when selected, a 1px --hairline over --surface-sunk
when not. Every route list, splice parent picker and chapter list in
Phases 10-13 is this component.

StatCell's value carries .t-num, so it cannot escape bible 10.6's 11px
floor or the tabular face - seen to redden by dropping the class.
ProgressBar clamps rather than overflowing, seen to redden on -1 and 3.

EmptyState exists because every list in the game can be empty and none of
them currently says so.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Tasks 9–12 — the screens (10 swept, 2 overlays exempt)

Four tasks covering twelve types: **10 screens that must carry the scaffold**, plus `CodexSheet` and `WaveHudView`, which are overlays and are exempt by name in Task 7. Each task ends with `EveryScreenComposesTheScaffold` naming fewer; Task 12 is where it goes green. Task 9 greens 3, Task 10 greens 4, Task 11 greens 2, Task 12 greens the last 1. Each task below repeats the shared steps in full — do not treat them as a cross-reference.

---

## Task 9: The first-hour screens

**Files:** `FounderNamingView`, `CampaignSelectView`, `CodexSheet`, `LineageView` — `.cs`, `.uxml`, `.uss` each, under `client/Assets/UI/Screens/`.

`CodexSheet` **is** in `Broodline.UI.Screens` — verified, not assumed — so it IS reached by the sweep, and Task 7 exempts it **by name** because it is a bottom sheet rather than a screen: `ScreenHost.ShowSheet` overlays it, never pushes it, and it never touches the back stack. It takes no scaffold. Do not give it one to satisfy the sweep, and do not move the type to dodge the sweep.

`LineageView` is a pushed sub-screen (`pushed: true`, `onBack` → `ScreenHost.Pop`). `FounderNamingView` and `CampaignSelectView` are top-level (`pushed: false`).

**This task greens 3 of the sweep's 10**: FounderNaming, CampaignSelect, Lineage.

**The five steps, in order.** Repeated in each of Tasks 9–12 rather than cross-referenced, because a brief is extracted per task and an implementer may never see its neighbours.

1. Wrap the existing tree in a `ScreenScaffold` — title from the handoff's copy, `pushed: true` for sub-screens per the handoff's push table, `onBack` calling `ScreenHost.Pop`.
2. Move the screen's body into `scaffold.Content`; move its buttons into `scaffold.CtaRow`.
3. Replace bespoke rows with `SectionCard` / `OptionRow` / `StatCell` / `ProgressBar`; give every list an `EmptyState`.
4. Put `.t-num` on every numeral and `.elev-1` on every card.
5. Delete from the screen's own `.uss` every rule the scaffold or a component now provides. **The stylesheet should shrink.** If it grows, the layout is being re-derived rather than composed.

**Gate for this task:** `./implementation/scripts/run-unity-tests.sh EditMode` green, `bash implementation/scripts/verify-uss-tokens.sh` green, and `EveryScreenComposesTheScaffold`'s failure list shorter by exactly the screens this task names. That test is deliberately red until Task 12; a shorter list is this task's success signal, not a failure.

**Component interfaces** (from Task 8, used verbatim): `OptionRow(string title, string detail, Action onSelect)` with `.Selected`; `SectionCard(string heading = null)` with `.Body`; `StatCell(string label, string value)` with `.Value`; `ProgressBar(float fill01, string modifier = null)` with `.Fill`; `EmptyState(string message, string glyph = null)`. Scaffold: `ScreenScaffold(string title, bool pushed = false, Action onBack = null)` with `.Content`, `.CtaRow`, `.HeaderSlot`, `.FooterNote`.

- [ ] **Step 1:** Apply the five steps to `FounderNamingView`. Title *"Name your Founder"*; the name field is the content; the CTA row carries the primary *"Confirm"*; footer note *"Founders keep their names for life."*
- [ ] **Step 2:** `CampaignSelectView` — each chapter becomes an `OptionRow`; locked chapters carry `icon--lock` and are unselectable.
- [ ] **Step 3:** `CodexSheet` — `SectionCard` per trait, `TraitPip` row unchanged, `EmptyState` when the codex is empty.
- [ ] **Step 4:** `LineageView` — generation rows as `SectionCard`s, `StatCell` for generation and coverage, `EmptyState` reading *"No lineage yet. Your first splice starts it."*
- [ ] **Step 5:** `./implementation/scripts/run-unity-tests.sh EditMode` and `bash implementation/scripts/verify-uss-tokens.sh`. Expected: the sweep now names seven screens, not eleven.
- [ ] **Step 6: Commit**

```bash
git add client/Assets/UI/Screens
git commit -m "feat(ui): the first-hour screens compose the frame

Founder naming, campaign select, the codex sheet and the lineage view.
Four of the eleven the scaffold sweep was listing; seven remain.

Every list gained an EmptyState. The lineage view had none, so a player
before their first splice saw a screen with nothing on it and no sentence
explaining why.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

## Task 10: The loop screens

**Files:** `RosterView`, `SpliceChamberView`, `SpliceRevealView`, `DeployView`.

**The five steps, in order.** Repeated in each of Tasks 9–12 rather than cross-referenced, because a brief is extracted per task and an implementer may never see its neighbours.

1. Wrap the existing tree in a `ScreenScaffold` — title from the handoff's copy, `pushed: true` for sub-screens per the handoff's push table, `onBack` calling `ScreenHost.Pop`.
2. Move the screen's body into `scaffold.Content`; move its buttons into `scaffold.CtaRow`.
3. Replace bespoke rows with `SectionCard` / `OptionRow` / `StatCell` / `ProgressBar`; give every list an `EmptyState`.
4. Put `.t-num` on every numeral and `.elev-1` on every card.
5. Delete from the screen's own `.uss` every rule the scaffold or a component now provides. **The stylesheet should shrink.** If it grows, the layout is being re-derived rather than composed.

**Gate for this task:** `./implementation/scripts/run-unity-tests.sh EditMode` green, `bash implementation/scripts/verify-uss-tokens.sh` green, and `EveryScreenComposesTheScaffold`'s failure list shorter by exactly the screens this task names. That test is deliberately red until Task 12; a shorter list is this task's success signal, not a failure.

**Component interfaces** (from Task 8, used verbatim): `OptionRow(string title, string detail, Action onSelect)` with `.Selected`; `SectionCard(string heading = null)` with `.Body`; `StatCell(string label, string value)` with `.Value`; `ProgressBar(float fill01, string modifier = null)` with `.Fill`; `EmptyState(string message, string glyph = null)`. Scaffold: `ScreenScaffold(string title, bool pushed = false, Action onBack = null)` with `.Content`, `.CtaRow`, `.HeaderSlot`, `.FooterNote`.

- [ ] **Step 1:** `RosterView` — `CreatureCard` grid inside `scaffold.Content`; the `IncompleteNotice` becomes the scaffold's footer note rather than a floating label; `EmptyState` for an empty roster.
- [ ] **Step 2:** `SpliceChamberView` — parent pickers become `OptionRow`s; the forecast becomes a `SectionCard` of `StatCell`s; the mutation percentage carries `.t-num`. The existing confirm flow and `SpliceConfirmTests` are untouched.
- [ ] **Step 3:** `SpliceRevealView` — the hero card gets `.elev-2` and `reveal-flare` from Task 5; the new creature's `silhouette` slot is filled by Task 13's proxy.
- [ ] **Step 4:** `DeployView` — pocket list as `OptionRow`s; `StatCell` for the deployment count.
- [ ] **Step 5:** Tests and lint. Expected: the sweep names three screens.
- [ ] **Step 6: Commit**

```bash
git add client/Assets/UI/Screens
git commit -m "feat(ui): the loop screens compose the frame

Roster, splice chamber, splice reveal, deploy. Three remain, all of them
combat.

The roster's IncompleteNotice becomes the scaffold's footer note rather
than a floating label - it is a caveat about the list, which is what that
row is for.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

## Task 11: The wave screens

**Files:** `WaveHudView`, `PostWaveView`, `WaveDefeatView`.

**This is the largest of the four.** `WaveHudView` was written before the token layer existed and carries the most bespoke styling; Task 2 already replaced its five hexes, and this task replaces its layout.

**The five steps, in order.** Repeated in each of Tasks 9–12 rather than cross-referenced, because a brief is extracted per task and an implementer may never see its neighbours.

1. Wrap the existing tree in a `ScreenScaffold` — title from the handoff's copy, `pushed: true` for sub-screens per the handoff's push table, `onBack` calling `ScreenHost.Pop`.
2. Move the screen's body into `scaffold.Content`; move its buttons into `scaffold.CtaRow`.
3. Replace bespoke rows with `SectionCard` / `OptionRow` / `StatCell` / `ProgressBar`; give every list an `EmptyState`.
4. Put `.t-num` on every numeral and `.elev-1` on every card.
5. Delete from the screen's own `.uss` every rule the scaffold or a component now provides. **The stylesheet should shrink.** If it grows, the layout is being re-derived rather than composed.

**Gate for this task:** `./implementation/scripts/run-unity-tests.sh EditMode` green, `bash implementation/scripts/verify-uss-tokens.sh` green, and `EveryScreenComposesTheScaffold`'s failure list shorter by exactly the screens this task names. That test is deliberately red until Task 12; a shorter list is this task's success signal, not a failure.

**Component interfaces** (from Task 8, used verbatim): `OptionRow(string title, string detail, Action onSelect)` with `.Selected`; `SectionCard(string heading = null)` with `.Body`; `StatCell(string label, string value)` with `.Value`; `ProgressBar(float fill01, string modifier = null)` with `.Fill`; `EmptyState(string message, string glyph = null)`. Scaffold: `ScreenScaffold(string title, bool pushed = false, Action onBack = null)` with `.Content`, `.CtaRow`, `.HeaderSlot`, `.FooterNote`.

- [ ] **Step 1:** `WaveHudView` — the HUD is an overlay on the wave scene, so it takes **no scaffold**. It **is** in `Broodline.UI.Screens` and therefore IS reached by the sweep; Task 7 exempts it by name for that reason. Do not add a scaffold to satisfy the sweep and do not move the type. Instead it adopts `SectionCard`-consistent surfaces, `.t-num` on integrity, tick and wave number, and `.elev-1` on the bar container. Confirm `WaveScreensTests` and the cached bar children from `3b49931` still pass — that commit is a performance fix and this task must not undo it.
- [ ] **Step 2:** `PostWaveView` — scaffold titled *"Wave cleared"*; rewards as `StatCell`s; primary CTA *"Continue"*.
- [ ] **Step 3:** `WaveDefeatView` — scaffold titled *"Wave lost"*; the breach diagnosis as a `SectionCard`; **the diagnosis still comes from `sim`'s echo through `WaveSubmitResponse.breaches` and the client's local outcome. `api` never parses a replay.** Primary CTA *"Try again"*, secondary *"Roster"*.
- [ ] **Step 4:** Tests and lint. Expected: the sweep now names **only `RegionView`**, which Task 12 finishes. `WaveHudView` must not appear — it is exempt by name in Task 7.
- [ ] **Step 5: Commit**

```bash
git add client/Assets/UI/Screens
git commit -m "feat(ui): the wave screens, and the HUD stops being bespoke

Post-wave and wave-defeat take the scaffold. The HUD does not - it
overlays the wave scene rather than being a screen - but it adopts the
same surfaces, .t-num on every number a player reads mid-wave, and
.elev-1 on the bar container.

3b49931's cached bar children are preserved and WaveScreensTests still
passes: that commit is a performance fix and this one must not undo it.

The defeat diagnosis still comes from sim's echo through
WaveSubmitResponse.breaches and the client's own outcome. api still never
parses a replay.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

## Task 12: `RegionView` — the interim, recorded as interim

Design §5.3. **The one screen that does not reach handoff specification, and the commit says so.**

**The five steps, in order.** Repeated in each of Tasks 9–12 rather than cross-referenced, because a brief is extracted per task and an implementer may never see its neighbours.

1. Wrap the existing tree in a `ScreenScaffold` — title from the handoff's copy, `pushed: true` for sub-screens per the handoff's push table, `onBack` calling `ScreenHost.Pop`.
2. Move the screen's body into `scaffold.Content`; move its buttons into `scaffold.CtaRow`.
3. Replace bespoke rows with `SectionCard` / `OptionRow` / `StatCell` / `ProgressBar`; give every list an `EmptyState`.
4. Put `.t-num` on every numeral and `.elev-1` on every card.
5. Delete from the screen's own `.uss` every rule the scaffold or a component now provides. **The stylesheet should shrink.** If it grows, the layout is being re-derived rather than composed.

**Gate for this task:** `./implementation/scripts/run-unity-tests.sh EditMode` green, `bash implementation/scripts/verify-uss-tokens.sh` green, and `EveryScreenComposesTheScaffold`'s failure list shorter by exactly the screens this task names. That test is deliberately red until Task 12; a shorter list is this task's success signal, not a failure.

**Component interfaces** (from Task 8, used verbatim): `OptionRow(string title, string detail, Action onSelect)` with `.Selected`; `SectionCard(string heading = null)` with `.Body`; `StatCell(string label, string value)` with `.Value`; `ProgressBar(float fill01, string modifier = null)` with `.Fill`; `EmptyState(string message, string glyph = null)`. Scaffold: `ScreenScaffold(string title, bool pushed = false, Action onBack = null)` with `.Content`, `.CtaRow`, `.HeaderSlot`, `.FooterNote`.

- [ ] **Step 1:** Scaffold titled *"Region"*, `CurrencyHeader` in the header slot.
- [ ] **Step 2:** Each node becomes a `SectionCard` with a `ProgressBar` for richness and `StatCell`s for rate and yield. `EmptyState` when no nodes are claimable.
- [ ] **Step 3:** Replace the stylesheet's existing comment with one that states the deferral rather than implying the current state is finished:

```css
/* The map tab, INTERIM. The handoff's Splice World Map v2 needs eight polygon
 * regions with five yield states, Ark pins, ally and hostile markers, animated
 * collector routes and an apex vein pulse. None of the systems behind those
 * exist: /v1/region/state returns claimable node slots, and
 * config/bundles/0.1.3/nodes.json is two node types with hourly rates. There
 * is no geometry anywhere and no relocation or collector system to drive an
 * overlay.
 *
 * So this is a node LIST with the foundation applied, and it is deferred to
 * Phase 10 rather than finished here. Phase 8 design section 5.3. Do not
 * approximate the map against two node types; a polygon viewport over this
 * data would be a mock, not a screen.
 */
```

- [ ] **Step 4:** Tests and lint. **This task greens the last of the 10**, so `EveryScreenComposesTheScaffold` passes. If it still names anything, that screen was missed — finish it before committing. If it names `CodexSheet` or `WaveHudView`, Task 7's exemption list was broken; restore it rather than scaffolding an overlay.
- [ ] **Step 5: Commit**

```bash
git add client/Assets/UI/Screens
git commit -m "feat(ui): the map tab's interim, and the sweep goes green

A node list with the foundation applied - cards, progress bars, stat
cells, an empty state. Not the handoff's map, and the stylesheet says why
at the top rather than letting a later reader assume this is finished.

The handoff's map needs region geometry, five yield states, Ark pins and
collector routes. /v1/region/state returns claimable node slots and
nodes.json is two node types with rates. Phase 10 owns it.

EveryScreenComposesTheScaffold is green: all eleven screens carry the
frame. It was deliberately red from the commit that introduced it, and
the eleven names it printed were this group of tasks' checklist.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 13: Interim creature proxies — and bible §10.2 rule 1, asserted early

Design §5.4. The proxies are placeholder art **and** a pre-test of the art direction, which is why this is a task rather than a step.

**Files:**
- Create: `client/Assets/UI/Art/proxies/{vetch,ember,skitter,hollow,loam,pale}.png`, `client/Assets/UI/Components/SpeciesProxy.cs`, `client/Assets/UI/Tests/SilhouetteTests.cs`
- Modify: `client/Assets/UI/Components/Resources/CreatureCard.uss`, `client/Assets/View/WaveView.cs`

**Interfaces:**
- Produces: `SpeciesProxy.UssClassFor(string species)` → `"proxy proxy--vetch"`, consumed by `CreatureCard` and Task 15's capture.

- [ ] **Step 1: Draw the six from bible §1.2's silhouette column**

Flat black on transparent, 192×192 (3× of the 64px card slot), each a literal reading of its row:

| Species | Silhouette, verbatim from bible §1.2 |
|---|---|
| Vetch | Low dome, four stubby legs, no neck |
| Ember | Tall narrow torso, head crest, two legs |
| Skitter | Small body, six long thin legs, tiny head |
| Hollow | Tiny body, stilt legs, long forward neck |
| Loam | Segmented ground-hugger, blunt snout, no legs |
| Pale | Broad wing arc, small hanging body |

Author them as SVG for editability, rasterise as in Task 4 Step 3, commit only the PNGs. **Black on transparent, not species-coloured** — the card tints them via `-unity-background-image-tint-color`, so one asset serves the roster, the reveal and the 40px test, and the palette decision from Task 6 cannot strand them.

- [ ] **Step 2: Write the failing test**

Create `client/Assets/UI/Tests/SilhouetteTests.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Broodline.UI.Tests
{
    /// bible 10.2 rule 1, asserted before Phase 9 generates anything:
    ///
    ///   "All six species must be distinguishable as flat black shapes at
    ///    40px; if two are confusable, one is wrong."
    ///
    /// These are interim proxies, not production art - but they are drawn from
    /// bible 1.2's silhouette column, so if two of THEM collide, the collision
    /// is in the DESIGN and not in the drawing. rig_proof.md section 6 routes
    /// that finding back to the bible rather than to the art team, and section
    /// 5 says item 7 is "the one that decides the art direction". Finding it
    /// here costs six PNGs; finding it after Phase 9 costs the asset budget.
    ///
    /// The threshold is deliberately low. This is a collision detector, not a
    /// quality bar: two shapes differing on fewer than 8% of a 40x40 field are
    /// the same shape at a glance.
    public class SilhouetteTests
    {
        const int Size = 40;
        const double MinDifferingFraction = 0.08;

        static readonly string[] Species = { "vetch", "ember", "skitter", "hollow", "loam", "pale" };

        /// 40x40 coverage mask: true where the silhouette is opaque.
        static bool[] Mask(string species)
        {
            var path = $"Assets/UI/Art/proxies/{species}.png";
            var src = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            Assert.IsNotNull(src, $"no proxy at {path}");

            var rt = RenderTexture.GetTemporary(Size, Size, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(src, rt);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var small = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            small.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
            small.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);

            var mask = small.GetPixels().Select(p => p.a > 0.5f).ToArray();
            Object.DestroyImmediate(small);
            return mask;
        }

        [Test]
        public void EverySpeciesHasAProxyThatIsNotBlank()
        {
            foreach (var s in Species)
            {
                var filled = Mask(s).Count(b => b);
                Assert.That(filled, Is.GreaterThan(Size * Size / 20),
                    $"{s}'s proxy is blank or nearly so - a blank mask would pass the pairwise test trivially");
            }
        }

        [Test]
        public void NoTwoSpeciesAreConfusableAsFlatBlackShapesAt40px()
        {
            var masks = Species.ToDictionary(s => s, Mask);
            var collisions = new List<string>();

            for (int i = 0; i < Species.Length; i++)
                for (int j = i + 1; j < Species.Length; j++)
                {
                    var a = masks[Species[i]];
                    var b = masks[Species[j]];
                    var differing = a.Where((v, idx) => v != b[idx]).Count() / (double)a.Length;
                    if (differing < MinDifferingFraction)
                        collisions.Add($"  {Species[i]}/{Species[j]}: {differing:P1} of the field differs");
                }

            Assert.IsEmpty(collisions,
                "bible 10.2 rule 1 fails on these pairs - one of each is wrong, and the fix is a " +
                "DESIGN change to bible 1.2's silhouette column, not a redraw:\n" +
                string.Join("\n", collisions));
        }
    }
}
```

- [ ] **Step 3: Run**

```bash
./implementation/scripts/run-unity-tests.sh EditMode 2>&1 | tail -12
```

Expected: pass. **If `NoTwoSpecies...` fails, stop and escalate rather than nudging the drawing.** Bible §10.2 rule 1 says one of the pair is wrong, and `rig_proof.md` §6 says that goes back to the bible. Record the failing pair and its percentage in `implementation/results/palette-decision.md`'s sibling — a new `silhouette-finding.md` — and raise it. Redrawing a proxy to pass a test that exists to detect a design collision destroys the test's only purpose.

- [ ] **Step 4: Make the textures readable**

`ReadPixels` after a `Blit` does not need `isReadable`, but the source import must not be crunched. Extend `ArtImportSettings.OnPreprocessTexture`:

```csharp
if (assetPath.StartsWith("Assets/UI/Art/proxies/"))
{
    t.crunchedCompression = false;
    t.textureCompression = TextureImporterCompression.Uncompressed;
}
```

- [ ] **Step 5: Wire the proxies into the card and the wave**

`SpeciesProxy.cs` maps species → class; `CreatureCard.uss` gives `.creature-card__silhouette` the proxy background and tints it with the species colour. In `WaveView.Build()`, the primitive markers stay — this task does **not** replace `SyntheticCreature`, which is the render-budget harness's instrument and belongs to Phase 9.

- [ ] **Step 6: Weaken, to prove the detector detects**

```bash
cp client/Assets/UI/Art/proxies/vetch.png /tmp/vetch-real.png
cp client/Assets/UI/Art/proxies/loam.png client/Assets/UI/Art/proxies/vetch.png
./implementation/scripts/run-unity-tests.sh EditMode 2>&1 | grep -A6 "rule 1 fails"
cp /tmp/vetch-real.png client/Assets/UI/Art/proxies/vetch.png
./implementation/scripts/run-unity-tests.sh EditMode 2>&1 | tail -4
```

Expected: `vetch/loam: 0.0% of the field differs`, then green again. Put that line in the commit message.

- [ ] **Step 7: Commit**

```bash
git add client/Assets/UI/Art/proxies client/Assets/UI/Components/SpeciesProxy.cs \
        client/Assets/UI/Tests/SilhouetteTests.cs client/Assets/Editor/ArtImportSettings.cs \
        client/Assets/UI/Components/Resources/CreatureCard.uss
git commit -m "feat(ui): six proxies, and bible 10.2 rule 1 as a test

Drawn from bible 1.2's silhouette column - low dome on four stubby legs,
stilt legs with a forward neck, and so on - into the silhouette slot
CreatureCard.uxml has carried empty since Phase 7.

They are interim art AND a pre-test. rig_proof.md section 5 calls item 7
'the one that decides the art direction' and section 6 routes a failure
back to the bible rather than to the art team. If two proxies drawn from
the bible's own descriptions collide at 40px, the collision is in the
DESIGN, and finding that out now costs six PNGs instead of the Phase 9
asset budget.

Black on transparent, tinted by the card, so one asset serves the roster,
the reveal and the test - and Task 6's unresolved palette decision cannot
strand them.

Seen to detect: copying loam.png over vetch.png gives 'vetch/loam: 0.0%
of the field differs'.

SyntheticCreature is untouched. It is the render-budget harness's
instrument and it belongs to Phase 9.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 14: The Editor harness

Design §5.5. Small, and everything after it depends on it.

**Files:**
- Create: `client/Assets/Editor/ScreenHarness.cs`, `client/Assets/Editor/ScreenFixtures.cs`

- [ ] **Step 1: Fixtures**

`ScreenFixtures.cs` returns one populated model per screen, with no server and no session — the same DTO shapes `ScreenBindingTests` already builds. Reuse those builders rather than writing a second set; if they are private to the test assembly, lift them into `ScreenFixtures` and have the tests call it, so there is one fixture source.

- [ ] **Step 2: The window**

```csharp
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

// No namespace: Assembly-CSharp-Editor, as with WaveBuilder.

    /// Renders any one screen against fixture data, with no server, no
    /// session and no beat machine.
    ///
    /// Phase 7 Task 17 Step 6 - "press Play in Boot.unity and walk beats 1-8"
    /// against a local Postgres, a published bundle and a running api - was
    /// not run, and the length of that sentence is why. This is the same look
    /// at the same screens for the cost of one menu item.
    public class ScreenHarness : EditorWindow
    {
        [MenuItem("Broodline/Screen Harness")]
        static void Open() => GetWindow<ScreenHarness>("Screens").minSize = new Vector2(460, 960);

        int _index;

        void CreateGUI()
        {
            var names = ScreenFixtures.Names;
            var picker = new PopupField<string>("Screen", new System.Collections.Generic.List<string>(names), 0);
            var host = new VisualElement { style = { flexGrow = 1, width = 430, height = 932 } };
            host.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/UI/Shell/Tokens.uss"));
            host.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/UI/Shell/Theme.uss"));
            host.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/UI/Shell/icons.uss"));
            host.styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/UI/Shell/Motion.uss"));
            host.AddToClassList("shell-root");

            void Show()
            {
                host.Clear();
                host.Add(ScreenFixtures.Build(names[_index]));
            }
            picker.RegisterValueChangedCallback(e => { _index = names.IndexOf(e.newValue); Show(); });

            rootVisualElement.Add(picker);
            rootVisualElement.Add(host);
            Show();
        }
    }
```

The 430×932 host is the handoff's reference frame, so what the harness shows is what the design specifies.

- [ ] **Step 3: Verify by eye, once**

Open Unity, `Broodline → Screen Harness`, step through all twelve. Every one must render without a console exception. Fix any that throw before committing — a harness that only works for some screens is worse than none, because Task 15 will silently capture blanks.

- [ ] **Step 4: Commit**

```bash
git add client/Assets/Editor/ScreenHarness.cs client/Assets/Editor/ScreenFixtures.cs
git commit -m "feat(editor): see a screen without running the stack

Phase 7's Task 17 Step 6 asked for a local Postgres, a published bundle,
a running api, Play in Boot.unity and a walk through eight beats, in
order to look at a screen. It was not run, and the length of that
sentence is the reason.

One menu item, fixture data, the handoff's 430x932 frame. Fixtures come
from the same builders ScreenBindingTests uses, so there is one source.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 15: The screenshot corpus

Design §7.2.

**Files:**
- Create: `client/Assets/Editor/ScreenshotCapture.cs`, `implementation/scripts/capture-screens.sh`

- [ ] **Step 1: Batch capture**

`ScreenshotCapture.cs` renders each fixture into a `RenderTexture` via a `Panel`, reads it back, writes `implementation/results/screens/<name>.png` at 430×932.

```csharp
[MenuItem("Broodline/Capture All Screens")]
public static void CaptureAll()
{
    var dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../implementation/results/screens"));
    Directory.CreateDirectory(dir);
    foreach (var name in ScreenFixtures.Names)
        File.WriteAllBytes(Path.Combine(dir, name + ".png"), Capture(ScreenFixtures.Build(name)));
    Debug.Log($"captured {ScreenFixtures.Names.Count} screens to {dir}");
}
```

`Capture` builds a `PanelSettings` with `targetTexture` set, attaches a `UIDocument`, forces one `panel.UpdateAndRepaint()`, then `ReadPixels`.

- [ ] **Step 2: The script**

```bash
cat > implementation/scripts/capture-screens.sh <<'SH'
#!/usr/bin/env bash
# Renders every screen to implementation/results/screens/*.png at 430x932.
#
# NEEDS A GRAPHICS DEVICE: -batchmode WITHOUT -nographics. On a headless box
# this produces blank or black images rather than failing, so the size check
# below is not decoration - it is the only thing between a silent blank and a
# reviewer looking at nothing.
set -euo pipefail
cd "$(dirname "$0")/../.."
UNITY="/Applications/Unity/Hub/Editor/6000.6.0f1/Unity.app/Contents/MacOS/Unity"
rm -rf implementation/results/screens
"$UNITY" -batchmode -quit -projectPath "$(pwd)/client" \
  -executeMethod ScreenshotCapture.CaptureAll -logFile - | tail -5

n=$(ls implementation/results/screens/*.png 2>/dev/null | wc -l | tr -d ' ')
[ "$n" -ge 12 ] || { echo "FAIL: expected >=12 screens, got $n"; exit 1; }
small=$(find implementation/results/screens -name '*.png' -size -8k)
[ -z "$small" ] || { echo "FAIL: these captures are suspiciously small (blank?):"; echo "$small"; exit 1; }
echo "OK: $n screens captured."
SH
chmod +x implementation/scripts/capture-screens.sh
```

- [ ] **Step 2b: Run it**

```bash
./implementation/scripts/capture-screens.sh
```

Expected: `OK: 12 screens captured.`

**If capture produces blanks and cannot be made to work**, the design's §7.2 fallback applies and is not a failure of this task: the harness stays manual, screenshots are taken by hand, and **they become tracked** — because `results/`'s own rule tracks what a human has to regenerate. Say which branch was taken in the commit message.

- [ ] **Step 3: Decide tracking by the existing rule**

Automated → **not tracked**; add `implementation/results/screens/` to `.gitignore` and note the regeneration command in the commit. Manual → **tracked**, with `git add -f`.

- [ ] **Step 4: Commit**

```bash
git add client/Assets/Editor/ScreenshotCapture.cs implementation/scripts/capture-screens.sh .gitignore
git commit -m "feat(editor): the screenshot corpus

Twelve screens to PNG at the handoff's 430x932. This is the artifact
Task 16 looks at, and the thing a reviewer is actually sent.

Not tracked, by results/'s own rule: the tracked files there are the ones
that need a device, a human or half an hour, and this needs one command.
Regenerate with implementation/scripts/capture-screens.sh.

The script checks count and file size because headless capture without a
graphics device produces blanks rather than errors, and a reviewer
looking at twelve blank frames would have no way to tell.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 16: The eyes-on pass

Design §7.3. **A human does this. There is no automated substitute and no step here that an agent completes.**

**Files:**
- Create: `implementation/results/phase8-visual-review.md`

- [ ] **Step 1: Capture and look**

```bash
./implementation/scripts/capture-screens.sh
open implementation/results/screens
```

- [ ] **Step 2: Run the app and watch the motion**

The screenshot corpus cannot check Task 5's transitions — `Motion.uss` says so in its own header. Start the local stack, press Play in `Boot.unity`, and walk beats 1–8. **This is Phase 7 Task 17 Step 6, which was not run.** It is run here.

- [ ] **Step 3: Write down what is wrong**

Create `implementation/results/phase8-visual-review.md` with one line per defect: screen, what is wrong, and whether it is fixed in this phase or booked. Blank sections are not permitted; *"nothing wrong with this screen"* is a finding and is written as one.

- [ ] **Step 4: Fix what this phase fixes**

Apply the fixes, re-capture, re-read. Iterate until the list is either empty or every remaining item is explicitly booked to a later phase with a reason.

- [ ] **Step 5: Commit**

```bash
git add -f implementation/results/phase8-visual-review.md
git add client/Assets/UI
git commit -m "fix(ui): the eyes-on pass, and what it found

Phase 7's equivalent step was not run and the implementer said so rather
than claiming it. In a phase whose deliverable is how the game looks, an
unrun eyes-on step is not a minor omission, so this one has an artifact:
phase8-visual-review.md, one line per defect, with 'nothing wrong here'
written down as a finding rather than left as silence.

Motion is the part of this the screenshots could not check, so beats 1-8
were walked in the running app as well.

Tracked with -f: a human at a keyboard produced it, which is exactly the
criterion the results/ table uses.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 17: The deployed stack, and `smoke-loop.sh`

Design §6.1. **Runs before Task 18.** Phase 7 Task 19, unchanged in substance and unstarted.

**Billing begins at `terraform apply`.** Do not start this task until Task 18 can follow it promptly.

**Files:**
- Create: `implementation/scripts/smoke-loop.sh`
- Modify: `infra/terraform/terraform.tfvars` (temporarily, then reverted)

- [ ] **Step 1: Apply**

```bash
cd infra/terraform && terraform init && terraform apply
```

Expected: `api` and `sim` services, the Cloud SQL instance, both buckets, the DNS zone. Record `terraform output -raw api_url`.

- [ ] **Step 2: Migrate and seed, through a temporarily reachable database**

```bash
cd infra/terraform
terraform apply -var db_public_ip=true -var "db_authorized_networks=[\"$(curl -s ifconfig.me)/32\"]"
cd ../..
export PATH="$HOME/.nvm/versions/node/v22.22.2/bin:$PATH"
pnpm --filter @broodline/api migrate
bash implementation/scripts/seed-server.sh      # the one servers row nothing creates
cd infra/terraform && terraform apply            # revert both vars to defaults
```

**Revert in the same session.** A public Cloud SQL instance left authorized to a laptop's IP is the kind of thing that survives a phase.

- [ ] **Step 3: Publish and deploy**

```bash
bash implementation/scripts/publish-bundle.sh 0.1.3
bash implementation/scripts/deploy.sh api
bash implementation/scripts/deploy.sh sim
gcloud run services list --project broodline-508416
```

Expected: both services listed with a URL.

- [ ] **Step 4: Write `smoke-loop.sh`**

`smoke-wave.sh` drives a wave through `smoke-wave.ts` and checks the replay object landed. The loop is larger: **harvest a node, splice two creatures into one, fight a wave, get paid.** Model `smoke-loop.sh` on `smoke-wave.sh` exactly — resolve the URL from `terraform output`, drive `smoke-loop.ts`, then assert the side effects separately, for the same reason `smoke-wave.sh`'s header gives:

```bash
# THE LEDGER CHECK IS NOT DECORATION. Every step of the loop can return a clean
# 200 while the ledger row that makes it reconstructible was never written -
# the verdict comes from sim and the credit from Postgres, and a missing ledger
# row is invisible to both. broodline_solo_execution.md calls the ledger "the
# highest-value small piece of code in the backend" and says games that add it
# after launch never fully recover the first six months. So it is asserted
# separately, and after.
```

Steps: create an account → sync → claim a node → wait out or fast-forward the harvest → `POST /v1/ftue/splice-stock` → preview → commit the splice → start wave → submit → assert shards credited **and** a ledger row per mutation **and** the replay object in GCS.

- [ ] **Step 5: Run it**

```bash
bash implementation/scripts/smoke-loop.sh
```

Expected: `PASS`. This is Definition-of-Done clause 7.

- [ ] **Step 6: Commit**

```bash
git add implementation/scripts/smoke-loop.sh
git commit -m "feat(infra): the deployed stack, and the loop driven against it

Phase 7 Task 19, unstarted since. terraform apply, migrate, the one
servers row nothing creates, bundle 0.1.3 published, api and sim
deployed. The public-IP window for the migration was opened scoped to one
/32 and reverted in the same session.

smoke-loop.sh did not exist - the directory had smoke-wave.sh, which
drives a wave and not the loop. Harvest, splice, fight, get paid, and
then the ledger row and the replay object asserted separately and after,
for the reason smoke-wave.sh's own header gives: every step can return a
clean 200 with the thing that makes it reconstructible never written.

Billing starts here.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 18: TestFlight, and the first playtest this project has ever had

Design §6.2. Phase 7 Task 20, unchanged in substance. **Requires Task 17.**

**Files:**
- Create: `client/Assets/Editor/BootBuilder.cs`, `client/Assets/Editor/ExportOptions.plist`
- Modify: `client/Assets/Editor/BuildSteps/IosFileSharingPostProcess.cs`, `client/ProjectSettings/ProjectSettings.asset`

- [ ] **Step 1: The release builder**

```csharp
// Editor/BootBuilder.cs - the WaveBuilder shape, release.
//   [MenuItem("Broodline/Build Release iOS Xcode Project")] BuildIOS():
//   - reads BROODLINE_API_URL and THROWS if unset or if it contains
//     "localhost" - a release pointing at a laptop is the failure this
//     prevents, and an empty string is not the only way to cause it
//   - writes Assets/Resources/BroodlineConfig.json from it
//   - BenchmarkBuilder.ConfigureSigning(); PlayerSettings.productName = "Broodline"
//   - scenes = { "Assets/Scenes/Boot.unity", "Assets/Scenes/Wave.unity" }
//   - options = BuildOptions.None  (NOT Development), to build/ios-release
```

- [ ] **Step 2: Release builds get no file-sharing keys**

In `IosFileSharingPostProcess.OnPostProcessBuild`, first line:

```csharp
if (!EditorUserBuildSettings.development) return;
```

Its own header already said the two plist keys should be reconsidered before anything ships to a real player. This is that reconsideration.

- [ ] **Step 3: Settings**

`ProjectSettings.asset`: `productName: Broodline`, iOS `buildNumber: 1`. **`bundleVersion` stays `0.3.0`** — moving it without moving bundle `0.1.3`'s `minimumClientVersion` reddens `verify-unity-settings.sh`'s coupling guard, which is what that guard is for.

- [ ] **Step 4: Build, archive, export, upload**

```bash
export BROODLINE_API_URL="$(terraform -chdir=infra/terraform output -raw api_url)"
"/Applications/Unity/Hub/Editor/6000.6.0f1/Unity.app/Contents/MacOS/Unity" -batchmode -quit \
  -projectPath "$(pwd)/client" -executeMethod BootBuilder.BuildIOS -logFile - | tail -5
cd build/ios-release
xcodebuild -project Unity-iPhone.xcodeproj -scheme Unity-iPhone -configuration Release \
  -archivePath ./Broodline.xcarchive archive -allowProvisioningUpdates
xcodebuild -exportArchive -archivePath ./Broodline.xcarchive -exportPath ./export \
  -exportOptionsPlist ../../client/Assets/Editor/ExportOptions.plist -allowProvisioningUpdates
xcrun altool --upload-app -f export/Broodline.ipa -t ios \
  --apiKey "$ASC_KEY_ID" --apiIssuer "$ASC_ISSUER_ID"
```

Then App Store Connect → TestFlight → the build → an **internal** group. No Beta App Review.

- [ ] **Step 5: Someone who is not the developer plays the first hour**

Hand it to one person who has not seen the app. They report: **did they reach the Lineage View; where they were confused; how long it took.**

**That report goes into the Phase 8 record verbatim.** It is the first playtest signal this project has had, and `broodline_whats_left.md` §7 says the next document worth writing is the one that records what the first playtest found.

- [ ] **Step 6: Commit**

```bash
git add client/Assets/Editor/BootBuilder.cs client/Assets/Editor/ExportOptions.plist \
        client/Assets/Editor/BuildSteps/IosFileSharingPostProcess.cs \
        client/ProjectSettings/ProjectSettings.asset
git commit -m "build(ios): the release builder - Boot + Wave, non-development, api url baked

The file-sharing plist keys are skipped on release builds, as their own
header asked and no build had yet honoured. BROODLINE_API_URL is required
and a value containing 'localhost' throws as loudly as an empty one - a
release pointing at a laptop is the failure that check exists for, and an
unset variable is not the only way to get there.

bundleVersion stays 0.3.0. Moving it without moving bundle 0.1.3's
minimumClientVersion reddens the coupling guard, which is the guard
working.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 19: The baseline, the errata, and the record

**Files:**
- Create: `implementation/results/phase8-test-baseline.txt`, `implementation/2026-XX-XX-phase8-followups.md` (dated the day it is written)
- Modify: `specs/broodline_accessibility.md` §4, `specs/plans/broodline_phase8_look_and_ship.md`

- [ ] **Step 1: Measure every gate**

```bash
export PATH="$HOME/.nvm/versions/node/v22.22.2/bin:$PATH"
dotnet test Broodline.sln --nologo 2>&1 | tail -4
./implementation/scripts/run-unity-tests.sh EditMode 2>&1 | tail -4
./implementation/scripts/cross-runtime-diff.sh 2>&1 | tail -2
pnpm --filter @broodline/api test 2>&1 | tail -4
bash implementation/scripts/verify-unity-settings.sh | tail -2
bash implementation/scripts/verify-uss-tokens.sh | tail -2
```

- [ ] **Step 2: Write `phase8-test-baseline.txt`** in the Phase 7 file's contract, with rows for the new gates: `gate.unity.editmode.*`, `gate.uss-tokens`, `gate.palette.pairs`, `gate.silhouette.collisions`.

- [ ] **Step 3: The accessibility erratum**

Replace `broodline_accessibility.md` §4's recommendation block with Task 6's finding and whichever option was chosen, and **delete the sentence** *"It is a small change and it must happen before the Character Bible is finalised"* — it is false as written, because the small change does not exist. Cite `palette-cvd-baseline.txt` as the measurement.

- [ ] **Step 4: The design erratum**

Append to `specs/plans/broodline_phase8_look_and_ship.md`:

```markdown
## 11. Errata, found in execution

**§4.6's palette gate was not implementable as specified** and would have
passed while the palette got worse. Task 6 replaced it with a tracked baseline
over all 45 pair/deficiency measurements. See that task and
`implementation/results/palette-decision.md`.

**§7.1's "resolved font-size ≥ 11px" gate was not implementable in
`Broodline.UI.Tests`**, which has no attached `Panel`. Implemented in
`verify-uss-tokens.sh` as text analysis against the `.t-num` marker, which
catches the violation in the stylesheet rather than in one instantiated tree.
```

- [ ] **Step 5: Write the Phase 8 record**

`implementation/2026-XX-XX-phase8-followups.md`, to the contract of `2026-09-15-phase7-followups.md`: what closed, what is owed, what was found in this phase's own plan and design, **and Task 18 Step 5's tester report verbatim.**

It must not read as a uniform success. Task 6 found a design document wrong; Task 0 found a baseline wrong; this plan found four things the design missed. Record them.

- [ ] **Step 6: Commit and open the PR**

```bash
git add -f implementation/results/phase8-test-baseline.txt
git add implementation/ specs/
git commit -m "docs: the Phase 8 record, the baseline, and two errata

The tester's report is here verbatim - the first playtest signal this
project has had.

Two errata against this phase's own design: section 4.6's palette gate
would have passed while the palette got worse, and section 7.1's font
size gate needed a Panel that Broodline.UI.Tests does not have. Both are
recorded where they were written rather than quietly reimplemented.

accessibility.md section 4 loses the sentence saying the palette fix
'must happen before the Character Bible is finalised'. Measurement says
the fix as described does not exist; palette-decision.md has the three
options that do.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
git push -u origin phase_8
gh pr create --base develop --title "Phase 8: the look, and a build in someone else's hands" --body "$(cat <<'BODY'
The visual foundation, eleven screens to handoff spec, and the first
TestFlight build.

Closes Phase 7's remaining two gates (19 and 20). Tasks 2 and 12 were
already closed and the record did not say so — Task 0 fixes that.

Two errata against the phase's own design are recorded in §11 rather than
silently reimplemented.

🤖 Generated with [Claude Code](https://claude.com/claude-code)
BODY
)"
```

---

## Definition of done

1. `dotnet test Broodline.sln` — 0 failed, 0 skipped.
2. `run-unity-tests.sh EditMode` — 0 failed, 0 skipped, including `TypographyTests`, `PaletteContrastTests`, `ScaffoldTests`, `SilhouetteTests`.
3. `verify-uss-tokens.sh` — green, and in `tests.yml`.
4. `verify-unity-settings.sh` — green.
5. `cross-runtime-diff.sh` — `PASS: 500 scenarios agree`.
6. `EveryScreenComposesTheScaffold` — green.
7. `capture-screens.sh` — twelve screens, none blank.
8. `phase8-visual-review.md` — written by a human, no blank sections.
9. `smoke-loop.sh` — PASS against Cloud Run.
10. A TestFlight build installed by someone who is not the developer, **and their report in the Phase 8 record verbatim.**
11. `phase8-test-baseline.txt` written; `phase7-test-baseline.txt` corrected; both errata recorded.

---

## What this plan deliberately does not do

**It does not repaint the species palette.** Task 6 measures and hands back a costed decision. Repainting six species colours on a plan's authority, when the prescription in the design document is measurably net-negative, would be the opposite of what `rig_proof.md` §6 asks for.

**It does not build the world map.** Task 12 ships a node list and says so in the stylesheet.

**It does not touch `engine/`.** The device capture is current under `0.4.0` and an engine change supersedes it.

**It does not replace `SyntheticCreature`.** That is the render-budget harness's instrument and it belongs to Phase 9.

**It does not schedule audio**, and the Phase 8 record repeats that no phase owns it.

**It does not start the moderation-filter procurement**, which feeds Phase 12 and wants starting independently of this plan.
