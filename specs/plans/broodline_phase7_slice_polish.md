---
status: current
folder: 03-technical
note: >
  Phase 7 design. Slice polish: the navigation shell, the first hour, the
  outbox, and the first deployed stack this project has ever had. Precedes the
  implementation plan. Records two scope rulings — placeholder art, and
  internal TestFlight — and the sequencing rule that keeps the device proof lit.
---

# Broodline — Phase 7 Design: Slice Polish

*The phase where it stops being a test harness and becomes an app somebody else can hold*

> **CURRENT — design, not a plan.** This settles what Phase 7 builds and how it
> is structured. The task-by-task plan is a separate document under
> `implementation/`. The scene graph, the tab model and the outbox live in
> `broodline_client_architecture.md`; the screen set and its build priority live
> in `broodline_screen_inventory_v2.md`; the first-hour beats live in
> `broodline_ftue.md` as amended by `broodline_supersession_map.md`; the wave
> content lives in `broodline_waves_01_12.md`; deployables and retention live in
> `broodline_solo_execution.md`. This document owns none of them and cites all
> five.

---

## 1. Scope

`broodline_solo_execution.md` §8.2 scopes Phase 7 as *"One species of real art,
FTUE beats from `broodline_build_order.md` Phase 2, `minimumClientVersion`,
offline retry queue,"* done when *"TestFlight build in someone else's hands."*

Three of those four need restating before anything is built.

**`minimumClientVersion` is already discharged.** `a8e5785` moved
`config/bundles/0.1.2/manifest.json` to `0.2.0` and
`client/ProjectSettings/ProjectSettings.asset` to `bundleVersion: 0.2.0`
together. What Phase 7 inherits is not the field but the **coupling guard** that
discharge did not build — §10.1.

**Real art is out, by ruling.** §8.3 says shipping on placeholder art saves
roughly two months and *"proves the spine just as well,"* and the rig proof
commission has never been briefed: procurement item 3 at
`broodline_whats_left.md` §3 is unstarted, and it carries external lead time
measured in weeks. Phase 7 therefore ships **placeholder art**, and the
commission runs as a parallel art track so Vetch and Pale land *during* this
phase for a later one to build on. The obligations that ride on the commission
are unchanged and unwaived: `broodline_rig_proof.md` §8.3's real-mesh re-run of
the entity-count harness still gates production species, and
`broodline_client_architecture.md` §12's *"does 150 MB survive real art?"* still
has its first honest measurement there. **Neither is Phase 7's to discharge, and
neither is weakened by this ruling.**

**TestFlight means internal, by consequence.** External TestFlight goes through
Beta App Review. Founder naming — FTUE beat 4 — is a permanent public UGC
surface, and `broodline_build_order.md` §2 states plainly that App Review rejects
an app carrying UGC without filtering. The English moderation filter is
procurement item 1, it is unstarted, and `broodline_whats_left.md` §7 says to
start it *this week*. **Internal is not a compromise here; it is the only
unblocked path**, and it is what makes this phase schedulable at all. The four
submission-gating screens defer with a named trigger — §12.

**Done when:** a TestFlight build, on the deployed stack, in the hands of
someone who is not the developer, who can complete the first hour without being
told what to tap.

---

## 2. Six findings the design has to absorb

Each was found by reading the tree rather than the documents, and each changes
what the phase is.

### 2.1 There is no shippable UI, and less than the record implies

`client/Assets/UI/` contains **no MonoBehaviour**. `RegionScreen`,
`RosterScreen`, `SpliceScreen` and `DeployScreen` are plain C# classes holding
screen state, verified headless by `LoopGuardTests` and `SpliceConfirmTests`,
and **nothing outside `UI/` instantiates any of them**. Phase 6's *"four screens,
at placeholder fidelity"* delivered the state and the logic. It did not deliver
presentation, and no part of this design should read as though it did.

What *is* rendered is the combat scene — `WaveView`, `WaveRunner` and `WaveHud`
are MonoBehaviours wired into `Wave.unity`, and the wave is genuinely playable.
But `WaveHud` draws with **IMGUI** (`GUI.Label`, `GUI.DrawTexture`, `GUI.skin`),
which is Unity's editor-facing immediate-mode GUI. It is the right tool for
proving a renderer and the wrong one for a build somebody else holds.

**So Phase 7's client work is building a presentation layer from zero, not
styling an existing one.** That is a larger phase than §8.2's sentence implies,
and pretending otherwise would put the discovery in week three.

### 2.2 Authored content and the engine disagree from wave 1 onward

`broodline_waves_01_12.md` wave 1 specifies *"Defile layout, 6 pockets."*
`Lane.Defile()` is hardcoded to five — `new[] { 6, 10, 13, 17, 20 }` — because
it was written for wave 6, which is the only wave that had ever run on it. Wave
4 wants a **Basin**; `Terrain` has exactly one value, `Defile = 0`. Waves 1–12
alternate the two families.

Phase 7 authors waves 1 and 2, both Defile, so Basin is not this phase's
problem. **Wave 1's sixth pocket is**, and it is an engine change, which leads
directly to the next finding.

### 2.3 An engine change supersedes the device capture, so ordering is load-bearing

Phase 6 moved `SimVersion` 0.2.0 → 0.3.0 in Task 1 and darkened the renderer
round-trip proof for the rest of the phase, leaving
`TheTrackedCapturesAreCurrent` deliberately red and blocking the phase's close.
A phase that re-captures early and *then* touches the engine repeats that
exactly.

**The rule this phase adopts: every engine change lands before the capture, and
the capture is the last engine-affecting act of the phase.** Concretely, the
pocket-count decision at §6.2 is resolved and merged *before* Task 0's capture
is taken, not after. If a later task is found to need an engine change, the
capture is retaken — it is cheap now that hardware is available, and a stale
capture is not.

### 2.4 The outbox was booked to Phase 6 and never built

`client/Assets/Net/BroodlineClient.cs` still says the outbox *"arrives with the
first queueable mutation, in Phase 6."* Phase 6 shipped four queueable mutations
— claim, splice commit, wave start, wave submit — and no outbox. The comment is
stale in the same way §6 of the Phase 6 followups records the
`minimumClientVersion` row being stale: a record that went on asserting a plan
after the plan had moved.

It is fully specified at `broodline_client_architecture.md` §8, so this is a
bounded deliverable rather than an open design question — §8 below.

### 2.5 The FTUE is what finally joins the supply line

Phase 6's followups §12 item 9 is the sharpest thing it left: ten of the
thirteen creatures in the closing roster were inserted directly, so the
harvest→splice leg and the wave→payout leg *"are two half-loops that meet on
paper."* It asks for either content a player can harvest into a wave-capable
deployment, or an explicit ruling that they cannot.

**The first hour is that content.** Beats 1–6 walk a player from nothing to a
splice: two creatures are granted, wave 1 is winnable with them, a third drops,
wave 2 is winnable with three, and the guided splice consumes two provided
parents. No step is seeded by a direct insert. Closing this is therefore not
extra work bolted onto Phase 7 — it is what Phase 7 already builds, asserted.

### 2.6 There is no navigation, and the shell is a prerequisite rather than a feature

Four scenes exist — `SampleScene`, `Benchmark`, `Wave`, `CorpusHarness` — and
none of them is an app. `broodline_client_architecture.md` §9 specifies one
persistent root scene with everything else additive, a persistent top bar, and
five tabs revealed progressively. Nothing of it exists. Every screen this phase
builds needs somewhere to live, so the shell is the first client task and not a
polish item.

---

## 3. Task 0 — close Phase 6 before building on it

`TheTrackedCapturesAreCurrent` is red, `phase6-test-baseline.txt`'s
`dotnet.failed` row is 1 on purpose, and `implementation/results/` still holds
the 2026-09-14 captures taken under engine `0.2.0`. **Phase 6 does not close
until this lands, and Phase 7 does not build on a red baseline.**

Discharge: play `client/Assets/Scenes/Wave.unity` in the Editor and on device,
then commit the four files in `implementation/results/`. **On device the tap
window is ticks 184–207** — the overlap of creature 0 holding a target through
184–240 and the Courser remaining below the first defender, which ends at 207.
Expect roughly ten ticks of reaction lag, so aim near 190. The Editor's window
is looser: Rally lasts 120 ticks, so any tap overlapping 184–240 is valid. **Do
not re-baseline the test green. Re-capture it.**

Per §2.3 this runs *after* §6.2's engine decision is merged, and a new
`phase7-test-baseline.txt` is taken from the resulting fully-green tip. That
baseline supersedes any count written into prose anywhere, including this
document.

---

## 4. The navigation shell

`broodline_client_architecture.md` §9, implemented as specified and cited rather
than restated:

- **One persistent root scene** holding the composition root, the network layer,
  the tab bar and the persistent top bar. Wave Defense loads **additively** and
  unloads on exit — it is the only scene with a 3D battlefield and the only one
  with a per-frame budget worth defending.
- **Five tabs — Map · Ark · Splice · Lab · Allies.** No Store tab.
- **Progressive reveal stores nothing.** The bar is a pure function of campaign
  progress and Geneticist Tier, both already returned by `/v1/sync`. No new
  server field, no local state to lose on reinstall.
- **Gate thresholds live in the config bundle**, so retuning which wave reveals
  the Lab is a bundle publish rather than an App Review cycle. This matters more
  in this phase than in any other: `broodline_build_order.md` §5 makes the FTUE
  the thing playtest has to answer.
- **Bottom sheets are an overlay layer, not navigation destinations.** The Codex
  sheet opens from every trait pip in the app; making it a destination would put
  it in the back stack of every screen.
- **Layout is safe-area driven with no fixed pixel positions**, per §10 of that
  document. It is cheap as a rule from the first screen and expensive as a
  retrofit across fifty-nine.

**The GUI technology is UI Toolkit (UXML/USS), decided here.** The reasons are
the screen set rather than taste: thirty-four screens of mostly data-bound
lists, a required size- and aspect-tolerant layout, and a shared component set
(`screen_inventory_v2` §11) whose whole value is being authored once. IMGUI is
editor tooling and is not a candidate. **This decision is reversible until the
first screen ships and effectively frozen after**, which is why it is taken in
the design rather than discovered in the plan.

`WaveHud` is rebuilt in UI Toolkit as part of this. It is the one piece of
existing presentation that a tester sees, and it is the piece most obviously
drawn by a debug tool.

---

## 5. The first hour

`broodline_build_order.md` Phase 2 names the deliverables; `broodline_ftue.md`
§2 carries the beat structure. **That file is marked `status: superseded` and
lives in `99-archive/`**, and the README's rule is that archive is never built
from — so this section restates the beats it takes, with the supersession map's
amendments applied, and **`broodline_ftue.md` is not cited as an authority
anywhere in the implementation plan.**

`broodline_supersession_map.md` marks beats 1–4 and 6–8 ✅, and amends two:

| Beat | What ships | Screen |
|---|---|---|
| 1 | **Cold open.** A wave is already incoming. Two creatures — the tutorial Vetch and Ember — handed over with one instruction: place them | Wave Defense |
| 2 | **Wave 1 resolves.** Creatures act on their own via Instinct; the player watches and wins | Wave Defense, Post-Wave |
| 3 | **Creature drop.** A third creature is awarded | Post-Wave |
| 4 | **Name it.** Founder naming, one creature, **with the skip path and a good default** — bible §3.3 | Founder Naming |
| 5 | **Wave 2.** Three creatures, and the Lash arrives. **Amended (map 1.1): this beat teaches a counter trait, not an armour type.** The Vetch holds the Lash because Taunt is on it, and the player experiences that as the big one standing in front. Bible §9.7 keeps the word "counter" out of session one entirely | Wave Defense |
| 6 | **First splice**, guided, using two **provided** creatures. The named Founder is **visibly locked out** of the parent slots | Splice Chamber, Splice Confirm |
| 7 | **Mutation fires**, scripted and guaranteed. **Amended (map 3.1): at 9% base this sets a far less unrealistic expectation than at 3%** | Splice Reveal |
| 8 | **Lineage View.** Two generations, the named Founder at the root, **both consumed parents still present in the tree** | Lineage View |

**Beat 8 is the argument for the whole session** — a player closing the app
after five minutes should be carrying one image: a family tree with a creature
they named at the bottom of it.

**The consumption lesson is the hardest thing in the game to teach**, and §3 of
the FTUE spec survives the supersession intact: the parents are framed as sample
stock rather than as the player's creatures, destruction is stated plainly before
the confirm **in the same language the live game uses**, and the Lineage View
immediately after shows both consumed parents still in the tree. *Individuals
are consumed; the record survives.* Teaching it with no stakes attached means
the player already understands the trade the first time it costs them something.
**Do not soften it.**

Phase 6 shipped the confirmation **copy and interrupt** — the destruction notice
naming the actual creatures, the CTA labelled with its cost, the Founder dialog
with no destructive default, the coverage warning and the forecast. Phase 7 ships
what Phase 6 explicitly deferred: **styling, the lineage reveal animation, and
beat 8.**

### 5.1 Screens this phase builds

Against `broodline_screen_inventory_v2.md` §10, and against the existing visual
designs in `specs/Designs/` rather than inventing layouts:

**New:** Founder Naming · Splice Reveal · Lineage View · Post-Wave · Wave Defeat
· Campaign Select · Trait Codex index (as the bottom sheet, opened from any trait
pip)

**Presentation built over Phase 6's headless state:** Region · Roster · Splice
Chamber · Splice Confirm · Deploy

**Rebuilt:** Wave Defense HUD, from IMGUI to UI Toolkit

**Shared components authored once** — `screen_inventory_v2` §11: the creature
card (it appears on at least nine screens and is the atom of the interface), the
trait pip, the currency header, the timer chip, and the two-level confirmation
dialog.

**Wave 6 and the Wave Defeat screen belong here and not later.** Build order is
explicit: it is the beat that teaches the counter system and the first place the
design can be wrong in a way playtest will show. A first hour that ends before
the designed loss tests the tutorial but not the game. Wave 6 already exists as
content and is already winnable-or-losable; what is missing is the screen that
explains the loss.

### 5.2 Where this deviates from `build_order` Phase 2, and why

Three deviations, named rather than absorbed.

**Two screens added.** `build_order` Phase 2 lists neither **Post-Wave** nor
**Campaign Select**, and this phase builds both. Post-Wave is where beat 3's
creature drop happens, so the beat set requires a surface that list does not
name. Campaign Select is how a player reaches wave 6 after wave 2 — without it
the first hour ends at beat 8 with no route to the designed loss, which the same
list places in this phase.

**The age gate is dropped.** That list pairs *"account creation and the age
gate,"* and account creation already exists — Sign in with Apple and
`POST /v1/account` shipped in Phase 4. The age gate defers with the other three
submission-gating screens at §12, because it is a store-submission requirement
rather than a feature and internal TestFlight does not reach review. **This is a
divergence from a current document, not an oversight**, and it reverses the
moment an external build is contemplated.

**The threat board is dropped, and for a specific reason.** The list pairs
*"Trait Codex index and the threat board."* The index ships; the board does not.
The threat board is where a pre-wave check surfaces, and
`Diagnosis.PreWaveCheck` **can never clear wave 7** — it counts all six
Skirmishers as simultaneous while Splash III caps at 5, and the breach path sees
two or three. It has no production caller today, which is the only reason it is
harmless. **Shipping the board without fixing it would give the first screen
that consumes it a defect the last phase already found and recorded.** Either
the diagnosis is fixed and the board ships, or neither does; this phase chooses
neither, and §13 carries the decision.

---

## 6. Wave 1 and wave 2 as authored content

### 6.1 What they are

From `broodline_waves_01_12.md`, unchanged:

| | Wave 1 | Wave 2 |
|---|---|---|
| Composition | 6 Skirmishers | 8 Skirmishers · 1 Lash |
| Timeline | one every 2.0s from t=3 | Skirmishers from t=3 at 1.5s; Lash at t=12 |
| Integrity | 2 | 2 |
| Expected roster | 2 | 3 |
| First clear | 150 shards · 2 tier-I samples | 165 shards · 2 tier-I samples |
| Teaches | placement produces a result | something shoots back, and it reaches past the front line |

Both compositions are **inside the existing enums** — `RaiderType` already
carries Courser, Lash and Skirmisher, and Taunt is already a `Trait`. Authoring
them is a bundle publish, not an engine change, and they ship as
`config/bundles/0.1.3` alongside the tab-reveal thresholds §4 requires.

Wave 1's six-at-two-seconds is deliberately slower than the Skirmisher's
designed 1.5s pattern, and six is fewer than the designed eight. **It is not
testing the player; it is showing them that placement produces a result.** Wave
2 is authored over budget at 135% on purpose. Neither number is a mistake to be
tidied.

### 6.2 The sixth pocket — the one engine decision

Wave 1 says six pockets; `Lane.Defile()` gives five. Two ways to close it:

**Take the engine change.** `Lane.Defile()` stops being a fixed layout and the
pocket set comes from the wave definition, which is also what Basin needs at wave
4 and what every terrain after it needs. This is the change the content has been
waiting for since it was authored.

**Or amend the document** to run wave 1 on the five-pocket Defile, on the
grounds that wave 1's own text says the lane is *"six pockets for two creatures —
so no placement is wrong,"* and that five pockets for two creatures satisfies
that reasoning exactly.

**This design takes the engine change**, for one reason: the divergence is not
wave 1's, it is the terrain model's, and every wave from 4 onward needs the same
fix. Deferring it means Phase 7 ships content that quietly disagrees with the
document it was authored from, which is the defect class the last two phases
spent most of their review budget on.

**It moves `SimVersion` to `0.4.0`**, and therefore it lands **before** Task 0's
capture, per §2.3. That ordering is the entire reason this decision appears in
the design rather than in the plan.

---

## 7. The deployed stack

**Every deployed-stack clause in Phases 5 and 6 is believed, not demonstrated.**
`gcloud run services list` and `gcloud sql instances list` are both empty, and
`infra/terraform/terraform.tfstate` holds three networking resources and no
`api`, no `sim`, no database. The debt is Phase 5 Task 11's, inherited twice.

A TestFlight build cannot point at localhost, so this phase discharges it.

**Minimum billable, by ruling:** a smallest-tier Cloud SQL instance, and `api`
and `sim` on Cloud Run at **min-instances 0**, accepting cold starts. Cloud SQL
HA stays deferred — `broodline_solo_execution.md` §10 triggers it on *"first
non-TestFlight players,"* and internal TestFlight sits exactly one step below
that line. Everything is stood up **through Terraform**, not by hand, so the
stack is reproducible and destroyable; a stack that can only be rebuilt by
remembering what was clicked is a second undocumented environment.

**The loop is then driven against it**, as `services/api/test/loop.test.ts`
drives it locally: sign in → claim → splice → start → submit, asserting the
shard credit, the single ledger row, both parents consumed, the child on the
roster and `committed_to` cleared. That discharges followups §12 item 2, and
together with §2.5 it closes item 9.

**Guardrails that do not relax because the stack is small:** Row-Level Security
stays on and transaction-scoped; a schema migration and the code requiring it
never deploy together; a config bundle failing publish-time validation is not
published.

---

## 8. The outbox

`broodline_client_architecture.md` §8, implemented as specified:

- **An idempotency key generated when the action is taken**, not when it is
  sent. A retry after a kill, a crash or three days offline sends the identical
  key and cannot double-grant. The server side has existed since Phase 4.
- **Ordered per player, drained oldest-first.** A splice depending on a wave
  reward must not overtake it.
- **Flushed on foreground and on connectivity**, with exponential backoff.
- **The server's response is truth.** A queued submission that fails validation
  loses its reward, and the client must be able to show a reward being withdrawn
  without pretending it never appeared.
- **Entries expire** past the server's 24-hour idempotency window and are
  dropped with a mail explaining what did not happen, rather than replayed into
  a double grant.

**Playable offline:** campaign waves — the engine is local, so the wave
simulates and the submission queues — plus the roster, the Codex and the lineage
view. **Unavailable offline**, shown as unavailable rather than failing on
submit: splice, harvest claim, the store.

`BroodlineClient.cs`'s stale comment is corrected in the same change.

---

## 9. The determinism gate

`determinism.yml` declares `runs-on: [self-hosted, macOS]` against a runner
nobody has registered. Every trigger has queued until cancelled — 24h, 8h34m,
5h34m, one still queued at 10h42m — and **a queued run is not a failing run, so
nothing has ever gone red.** `Tests` on `ubuntu-latest` has run since 2026-09-12
and is not the gap; the **IL2CPP half of the cross-runtime diff** is.

Phase 7 registers this Mac as the self-hosted runner. The gate then runs against
a phase that changes `SimVersion`, adds two authored waves and alters the lane
model — which is precisely the class of change it exists to catch, and which
Phase 6 shipped with the same gate unrun and every check performed by hand on
one machine.

---

## 10. The inherited smalls

### 10.1 The coupling guard

Nothing asserts that `bundleVersion` and a bundle's `minimumClientVersion` move
together, which is exactly how they came to disagree. `verify-unity-settings.sh`
is the home — it already pins `ProjectSettings.asset` keys by grep. The failure
it prevents is invisible in every other check: a player told they are current
while every wave they start is refused.

Phase 7 publishes `0.1.3` and ships a client, so both halves move this phase and
the guard is written against a real movement rather than a hypothetical one.

### 10.2 The abandonment sweep

An issuance refused at checks 1–3 strands its creatures `committed_to` a dead
issuance, and every splice of them is refused indefinitely. The check ordering
is correct and is not what changes — roster checks run *after* check 4 because in
front of it an abandoned issuance locks a player out of their own roster
permanently. What is missing is the sweeper that releases them.

`weakenings.md` row 7's retention split waits on the same sweep, so one piece of
work has two claimants. **The FTUE is what makes this urgent**: a first-hour
player whose guided splice is refused because a creature is stranded has no
recovery path and no way to understand what happened.

### 10.3 The document edits the code has outrun

Four, all owed, none large, each recorded because the count staying honest is
the point:

| Edit | Owner |
|---|---|
| Node rates and the Rich Deposit's six-day depletion budget — `0.1.2` authors 60/hr and `totalYield` 8640, and 8640/60 = 144 hours exactly | `broodline_region_roster.md`, bible §5.3 |
| The downtier's floor at Tier I — implemented and tested, while the document still says "one tier lower" and stops | `broodline_sample_economy.md` §7 |
| The degradation row, contradicted by shipped code since Phase 5 | `broodline_solo_execution.md` §3.1 |
| `ref readonly SimState`, which specifies a shape that guarantees nothing. **Owed since Phase 3 and five times deferred** | `broodline_client_architecture.md` §2 |

The last one is not this phase's to design, and it is recorded here so that
a sixth deferral is a visible act rather than a silent one.

---

## 11. Testing

The gates that would fail against a broken implementation, rather than the ones
that pass against anything:

**The first hour, driven end to end.** The existing `loop.test.ts` pattern
extended to the FTUE path: a brand-new account walks beats 1–8 and reaches the
Lineage View, **with every creature earned through an HTTP response and none
inserted directly.** The test asserts the earned-versus-seeded split rather than
printing it — the distinction whose absence let a false claim stand for a full
review cycle in Phase 6. This is §2.5's discharge, and it must fail if anything
re-introduces a seeded creature into the path.

**The same drive against the deployed stack**, run once per release rather than
per commit, because it costs a cold start and real rows.

**The outbox, adversarially.** A mutation taken offline and flushed twice grants
once. Two queued mutations drain in order. An entry older than 24 hours is
dropped rather than replayed. A queued submission that the server rejects
withdraws its optimistic reward visibly.

**The coupling guard fails when either half moves alone** — verified by moving
each half alone and seeing it go red for its own reason, not by observing it
green.

**The sweep releases a stranded creature and nothing else.** Construct the
abandoned issuance, sweep, assert the creature is spliceable and that a live
issuance's creatures are untouched.

**The capture marker discriminates.** `TheTrackedCapturesAreCurrent` goes green
when Task 0 lands and red again if `SimVersion` moves after it, verified by
moving it — the check Phase 6 performed and recorded, and the reason its red was
trustworthy rather than merely present.

**Determinism, finally across runtimes.** The IL2CPP half of the diff runs on
the registered runner, against the phase that moved `SimVersion`.

---

## 12. What this design deliberately does not do

- **No real art.** §1. Placeholder throughout; the rig proof runs as a parallel
  track and keeps its full force over the twenty-four-asset budget, every
  production species, and the real-mesh re-run
- **No external TestFlight, and no submission screens.** Age Gate, Report/Block,
  the Settings support contact and the Founder Naming **filter path** defer
  together. **Trigger: the first build intended for external testers** — and
  they cannot ship before procurement item 1 does, because founder naming is the
  UGC surface that makes the filter mandatory
- **No waves 8–10, and no Brood, Drift, Bulwark or Delver.** These are new
  systems rather than modifiers over the existing loop, and they are week-one
  content rather than first-hour content. **Trigger: `broodline_build_order.md`
  Phase 3**
- **No Basin terrain.** §6.2's change makes the lane model take a layout; wave 4
  is the first content that needs a second family, and it is not in this phase
- **No Gene Lab, world map, relocation, Sample Store or fusing.** All
  `build_order` Phase 3
- **No Codex content beyond the index sheet**, and **no threat board** — §5.2.
  The eleven behaviour previews are procurement item 5. **Trigger for the board:
  `Diagnosis.PreWaveCheck` being fixed, and not before**
- **No new deployable and no scheduler.** The triggers at
  `broodline_solo_execution.md` §10 remain unmet, with one exception now in
  sight: Cloud SQL HA fires on the first non-TestFlight player
- **No replay viewer.** Tier 4, flagged launch-critical, and not first-hour

---

## 13. Decisions owed

*Five carried in. Four taken at design time (§2). Two closed during execution. The measured record after execution will live
in `implementation/`; this table is the index into it.*

| Decision | Why it matters | State |
|---|---|---|
| **The device capture** | Blocks Phase 6's close, not just Phase 7's start | **Taken: Task 0, after §6.2's engine change.** Hardware is available |
| **The self-hosted macOS runner** | The IL2CPP gate has never run, across three phases that each changed the engine | **Taken: registered this phase.** §9 |
| **Wave 1's sixth pocket** | Authored content and the engine disagree from wave 1 onward | **Taken: the engine changes**, the lane model takes a layout, `SimVersion` → `0.4.0`, and it lands before the capture. §6.2 |
| **The GUI technology** | Reversible until the first screen ships, effectively frozen after | **Taken: UI Toolkit.** §4 |
| **`ref readonly SimState`** | Specifies a shape that guarantees nothing | **Deferred Phase 7: six times deferred, by count.** Not edited. Recorded so the deferral is an act. |
| **`solo_execution` §3.1's degradation row** | Contradicted by shipped code since Phase 5 | **Taken: narrowed to wave submission error handling.** §10.3 |
| **Node rates and the downtier floor, back into their documents** | The code has outrun both | **Taken: added to region_roster, bible §5.3, and sample_economy.** §10.3 |
| **DOM/REC for the remaining traits** | `0.1.2` authors four; `combat_numbers` §4.2–4.3 has twelve traits and six species unauthored | **Owed, and provisional for the four that exist.** Not this phase's, and it blocks any wave needing a fifth trait |
| **`Diagnosis.PreWaveCheck` cannot clear wave 7** | It counts all six Skirmishers as simultaneous while Splash III caps at 5 | **Owed, and this phase is the first that could have shipped a caller.** Resolved by not shipping one: the threat board is dropped with it — §5.2. **Trigger: the two move together or neither moves**, because the board is the surface that would make the defect visible to a player |

---

*Owns: the Phase 7 slice boundary, the placeholder-art and internal-TestFlight
rulings, the navigation shell's adoption of `client_architecture` §9, the
first-hour beat set as amended, the engine-change-before-capture ordering rule,
and the minimum deployed stack's shape. Does not own: the scene graph, the tab
model or the outbox's contract (`broodline_client_architecture.md`); the screen
set or its build priority (`broodline_screen_inventory_v2.md`); the wave content
(`broodline_waves_01_12.md`); determinism or the replay format
(`broodline_combat_engine.md`); deployables, retention or the deferral triggers
(`broodline_solo_execution.md`); the rig proof's gates
(`broodline_rig_proof.md`).*
