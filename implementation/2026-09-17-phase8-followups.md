# Phase 8 — What Execution Left Behind

*The durable record of the look-and-ship phase: what it built, what it proved,
what it left owed, and — because this phase's own Task 0 existed to repair the
last one — what it found wrong in its own plan and its own design*

> **Why this file exists.** The working ledger that recorded these findings
> lives in git-ignored scratch and will not survive a `git clean`. This is the
> durable copy, and for most of these items it is the only one. It is written
> to the same contract as `2026-09-15-phase7-followups.md`, which it should be
> read beside.
>
> **State at the time of writing:** every implementation task — **0 through 15**
> — is through its review gate on `phase_8`, plus this one. **Tasks 16, 17 and
> 18 are not done, and none of them is an engineering task that was skipped.**
> Each is a human at a keyboard: eyes on sixteen screenshots, a `terraform
> apply` with a billing decision behind it, and an App Store Connect account
> with a tester attached. They are §1.
>
> **There is no commit count in this paragraph, and that is deliberate.** The
> Phase 7 record wrote one, watched it go stale inside two days, wrote a
> parenthetical explaining why it was stale, and watched that go stale too. The
> command is the whole answer — `git rev-list --count develop..HEAD` — and the
> task list above is what actually identifies the work.
>
> **Counts are not quoted in this prose.** They live in
> `implementation/results/phase8-test-baseline.txt`, which supersedes any number
> written into a sentence anywhere, including this one.
>
> **The branch has never been pushed.** There is no `origin/phase_8`, so **no
> CI has run on any of this.** Every gate in the baseline was measured on one
> developer machine. That is the largest single caveat on this phase.
>
> **This record is deliberately not a success report.** §5 enumerates ten
> defects this phase found in its own plan, §6 the two it found in its own
> design, and §7 the four defects nobody planned for that were found only by
> measurement. §8 records three things that contradict what is written down
> elsewhere in this repo, including one in this file's own predecessor. A record
> that read as uniformly successful would be false, and the last one that did is
> the reason Task 0 existed.

---

## 1. The three gates this phase cannot close

**The Definition of Done names eleven clauses. Eight are green. Three are not,
and none of the three is blocked on anything an agent could write.**

| Gate | Blocked on | State today |
|---|---|---|
| **`phase8-visual-review.md`, written by a human, no blank sections** | **Task 16** — a person looking at sixteen captures, and walking beats 1–8 in the running app | Not started. The file does not exist. **One defect is already known and measured** — see §2.1 — and the motion from Task 5 has never been looked at by anyone, because a screenshot of a 220ms transition is pixel-identical to no transition at all |
| **`smoke-loop.sh` PASS against Cloud Run** | **Task 17** — `terraform apply`, a migration, a seeded `servers` row, a published bundle, two deploys, and a human who has decided the Cloud SQL instance stays up and billing | No deployed stack. `implementation/scripts/smoke-loop.sh` **does not exist**; the directory holds `smoke-wave.sh` only. **Unchanged from Phase 7, where it was Task 19** — this is the second phase it has carried |
| **A TestFlight build installed by someone who is not the developer, and their report in this file verbatim** | **Task 18** | No build, no upload, no tester. **The section of this file that would carry that report is absent rather than empty**, which is the same choice Phase 7 made and for the same reason: a heading with nothing under it reads like a thing that was tried |

**Task 18 is not only a ship gate.** It is the first time a colour-blind person
could look at this palette, and the first time anyone at all holds the app at
arm's length on a real phone. Both §2.3 and §2.6 are waiting on it.

---

## 2. The punch list — what is owed, with its evidence

Seven items. Each carries the measurement it rests on, so a later session can
act without re-deriving it.

### 2.1 `OptionRow`'s boundary fails WCAG, and it shipped

**The one thing in this phase that is objectively out of spec and shipped
anyway.** Measured in Task 10.

| Surface | Against | Contrast | Required |
|---|---|---|---|
| `OptionRow` fill `--surface-sunk` `#f8f6fc` | `--paper` `#f7f4fb` | **1.0151 : 1** | 3:1, WCAG 1.4.11 |
| The whole row boundary, a 1px `--hairline` | `--paper` | **1.1131 : 1** | 3:1 |
| The same fill inside a `SectionCard` | white | 1.0726 : 1 | 3:1 |

Bible §10.4 is arguably breached too, since a row's extent is carried by a faint
border and nothing else.

**It renders legibly in `DeployView.png` and `CampaignSelectView.png`** — the
rounded 1px outline does the work. That is exactly the problem: a screenshot is
not a phone in daylight, and no measurement here can settle it. **Booked to Task
16 with three costed options**, cheapest first:

1. Strengthen `--hairline` for this one use.
2. **Drop the fill**, which currently does nothing, and let the row read as an
   outline deliberately.
3. Compose `OptionRow`s inside a `SectionCard` so the fill sits on white — which
   may be the handoff's actual intent for "inset rows, unselected options".

### 2.2 A 2px `Label` margin nobody has zeroed, found three times

**Unity's runtime theme gives `Label` a 2px margin that `Theme.uss`'s `Label`
rule does not zero.** Found in Task 10, re-reported in Task 11, found again in
Task 12 — three tasks, three independent sightings, deliberately not fixed each
time.

The evidence is dimensional, off the captures:

- `SpliceChamberView`'s destruction notice starts at **x=14** where its own
  `SectionCard` starts at x=12 and its CTA at x=12.
- `WaveDefeatView`'s resupply caption starts at **x=15** where the
  `CreatureCard` under it starts at x=12.
- `RegionView`'s spent-node coral panel runs **x=30..397** inside a card body of
  x=28..401.

**It is pre-existing** — the captures these commits replaced show the same
offsets. **The fix belongs in `Theme.uss`, and from there it would move all
sixteen captures rather than any one task's three.** That is why no task took
it: a two-line fix with a sixteen-capture blast radius is a task of its own, and
it wants doing next to Task 16's eyes-on pass rather than blind.

It is the same shape as the elevation finding that *was* fixed in `615229c`, one
level smaller.

### 2.3 Pale cannot be drawn as one flat colour

`#c6cede` on the creature card's `#f8f6fc` silhouette slot is **1.47 : 1**. On
the capture, the entire Pale creature moves no channel by more than 50 of 255.

**This is a constraint on the Phase 9 art brief, not a defect in the interim
proxies.** It is now recorded in `bible §10.4`, `accessibility.md §4.3` and the
design handoff README, because it is the kind of thing that is cheap to plan for
now and expensive after six species are modelled.

**Task 6's lightening of Pale improved the colour-blindness numbers and worsened
this, and both were correct against the measurements available at the time.**
Contrast-against-a-card was not measurable in Task 6 — no species colour had
landed on a surface yet. Two good measurements can pull in opposite directions,
and this pair does.

### 2.4 `RegionView` is deferred to Phase 10, and the deferral note is wrong

The map tab ships as a **node list** with the visual foundation applied, and its
stylesheet says so at the top rather than letting a later reader assume it is
finished. That is the right call: `/v1/region/state` returns claimable node
slots and `nodes.json` is two node types with hourly rates, so a polygon
viewport over that data would be a mock, not a screen.

**But the note says "there is no geometry anywhere", and that is false.**
`specs/broodline_region_graph.md` authors **thirty regions across three rings,
with full adjacency and forty-three edges**. What does not exist is
**coordinate** geometry — no region has an x/y or a polygon. The distinction
matters, because "no geometry" invites the next person to author a graph that
already exists.

**And there is a second mismatch under it.** The handoff's `Splice World Map v2`
draws **eight** irregular polygon regions, `R-01…R-08`. The roster authors
**thirty**. Phase 10 owes a reconciliation, not just a renderer. Corrected in
the design document's §11.

### 2.5 Three `.ConfigureAwait(false)` sites keep one EditMode test red

`client/Assets/Game/Tests/SessionTests.cs`, lines **140, 155 and 179**.
`MainThreadAffinityTests.NoConfigureAwaitFalse_InCodeThatTouchesTheUi` fails on
them and `run-unity-tests.sh EditMode` exits 2.

**This is correct, not tolerated.** The three sites are inside a test's fake HTTP
handler and touch no UI, so the code is fine — and the gate is also right, because
it is text analysis over source and cannot know which `await` reaches a
`VisualElement`. The suite ships red rather than exempting the file, because the
exemption is the thing that would let a real fourth site through.

**Do not make it disappear.** If it ever reports a count other than three, or a
file other than `SessionTests.cs`, that is a real finding. The baseline file says
this at length for the same reason.

### 2.6 The palette's binding constraint rests on the weakest of three matrices

Machado's protan and deutan matrices at severity 1.0 are true rank-2 projections
— determinant **0.0000**, the mathematical signature of a dichromat. **The tritan
matrix is not: determinant 0.236.** It is an extrapolation.

**The palette's worst pair is now Vetch/Loam at ΔE 10.5, and that is a tritan
number.** The colour the phase spent was spent correctly — Vetch/Pale went from
ΔE 7.2 to 17.3 — but what it bought is a ceiling measured by the least
trustworthy simulation of the three. Roughly ΔE 10–12 is the floor for six
saturated hues in this family however they are arranged, so there is no cheap
move left, and the number that says so is the soft one.

### 2.7 Nobody who is colour-blind has seen this palette

Every number in `palette-cvd-baseline.txt` and in `accessibility.md §4` comes
from a model of an average dichromat. **The models disagree with each other and
with real viewers, and none of them has an opinion about a creature card at
arm's length on a phone.**

**Task 18's playtest is the first real check.** Until it happens, the whole
palette document is a well-measured prediction rather than a finding, and it
should be cited that way.

### 2.8 One smaller thing, still true

**Two screens print a count against a cap two different ways.**
`RegionScreen.RosterHeadline` writes `"5/20"`; `DeployScreen.DeployedStatValue`
writes `"3 / 5"`. The first is pinned by a Phase 6 assertion. Unifying them is a
copy decision rather than a layout one, so it was recorded rather than taken.

---

## 3. What this phase actually closed

Stated so the punch list above is not mistaken for the whole story.

- **A visual foundation that did not previously exist**: two type faces with
  tabular figures asserted rather than assumed, elevation and the CTA gradient
  as generated textures, thirteen glyphs, a motion vocabulary with every
  duration read from a token, and a token layer that — for the first time in
  seven phases — actually compiles (§7.1).
- **A layout frame every screen composes.** `EveryScreenComposesTheScaffold`
  went from deliberately red, naming its own checklist, to green.
- **Five components built once and used everywhere**, replacing bespoke rows.
- **The species palette measured rather than asserted**, all 45
  pair/deficiency combinations tracked as a baseline that fails when any number
  gets worse.
- **Two errata written back into the documents that were wrong**, rather than
  quietly reimplemented elsewhere.
- **Phase 7's two remaining closable gates** — its Tasks 2 and 12 — were already
  closed and its record did not say so. **Task 0 fixed that**, which is the
  whole reason this phase opened with a record-keeping task.

---

## 4. The thing worth naming: the silent-failure family

**This phase's dominant defect shape is a change that compiles, passes every
gate, and renders wrong.** Not a crash, not a red test — a thing that looks
exactly like success from every automated vantage point the project has.

Five instances, all found this phase:

1. **A USS parse error that produced zero rules** rather than an error (§7.1).
2. **A `calc()` that does not exist on this Editor** — written, accepted,
   silently inert.
3. **A rule in a stylesheet that cannot reach the container it styles.** A sheet
   attached through `<ui:Style>` in a component's UXML reaches that component and
   its *descendants*. The gap *between* two `OptionRow`s belongs to the parent, so
   a rule for it written in the component's own file compiles, passes
   `check-stylesheets.sh` and `verify-uss-tokens.sh`, and styles nothing.
4. **A black-rasterised glyph multiplied by a tint**, which is black times
   anything.
5. **A declaration dropped with only a warning** — `-unity-slice-scale: 0.5`
   emitted *"Expected (&lt;length&gt;) but found '0.5'"* and was discarded. The
   value needed a unit. Nothing failed.

**Every one of these was caught by the screenshot corpus, and nothing else could
have caught them.** That is the argument for keeping the corpus, and it is also
the argument for Task 16: the corpus catches what renders wrong, and only a human
catches what renders *badly*.

**The counter-lesson, from the same phase.** Two gates had to learn the
difference between code and commentary — `verify-uss-tokens.sh` strips comments
before its hex check, and `MainThreadAffinityTests` now blanks comments and
string literals before matching (§7.4). A gate that reads source as text will
eventually punish someone for documenting it.

---

## 5. Defects in this phase's own plan

**The plan misstated something in every single task.** This is recorded as a
pattern rather than as a complaint, because the pattern has a lesson in it and
the lesson is not "the plan was bad."

| Where | The plan said | Measured |
|---|---|---|
| **Seven** places in Task 1a | the EditMode baseline is **264**, exit 0 | **268 / 267 / 1, exit 2** at the time. The amendment at the top of the file is correct; **the seven stale lines are left as written on purpose**, and that is the right call — this plan's own Task 0 is about a cached number outliving its measurement, so the fix is to say the regeneration command is the authority, not to re-cache it in seven more places |
| Task 2's heading | **"nineteen raw hexes"** | **seventeen** offenders across four files. The other two are inside `Theme.uss`'s header comment — documentation of the two primitives USS cannot express, which the gate is right not to flag |
| Task 4 Step 1 | **"the eight Lucide glyphs"** | the loop fetches **twelve**, plus `splice` drawn by hand: **thirteen**, which is what the commit message says |
| Task 4 Steps 5–6 | iterate a **`Tab` enum** | **there is no `Tab` enum anywhere in `client/Assets`.** The test as written does not compile |
| Three places in Task 7 | **"eleven screens"** | `ScaffoldTests` asserts **twelve** types in `Broodline.UI.Screens` and **ten** carrying the frame, two exempt by name |
| Task 3's Interfaces line | put `.elev-1` **"on every card surface"** | following it literally draws a **grey smudge inside the card**. UI Toolkit clips `background-image` to the element's own box, so a drop shadow must live on something *larger* than the thing it lifts. The tell was that `--elev-1-spread` and `--elev-2-spread` were referenced by nothing — they are the wrapper's padding |
| Task 3's stylesheet block | `-unity-slice-scale: 0.5` | **geometrically impossible as one value.** Nine-slice maps texture offset `p` to `p × scale` px in from the edge, so `scale = padding / C`; one scale cannot serve both elevations. Shipped as `0.6px` and `1.2px`. And `0.5` is unitless, so it was **dropped with a warning** |
| Task 5's token block | `--motion-press` is **"already used by the CTA press"** | it was not. `Theme.uss` carried a raw `60ms` and nothing referenced the token |
| Task 12 | **"There is no geometry anywhere"** | `broodline_region_graph.md` authors **thirty regions with full adjacency across three rings**. What is missing is **coordinate** geometry. §2.4 |
| Task 13's Modify list | `client/Assets/View/WaveView.cs` | **nothing to do in it.** The task's own text says the primitive markers stay; the commit does not touch the file |

**Several were corrected in the plan during execution, in four `docs(plan)`
commits** — `460e716` (the EditMode baseline and the dead token layer),
`ccf0378` (the Interfaces line and the nine-slice arithmetic), `37d187a` (the
bash 3.2 gate hazard and the corpus's blind spot) and `5560b78` (Task 16's
`OptionRow` measurement). That habit is why they are recoverable here.

**`460e716` made one judgement worth preserving.** It corrected the EditMode
baseline **at the top of the file** and deliberately left the seven stale lines
below it as written, on the grounds that *"the fix for a cached number outliving
its measurement is to say the regeneration command is the authority, not to
re-cache it."* That is the right instinct, and it is the same one this phase's
baseline file is built on.

**A caveat on the scope of this table.** Ten defects are listed and every one was
verified against the tree before being written down. The claim this table was
commissioned under — that the plan misstated something in *every* task — was
**not** exhaustively checked, and is not asserted here. Ten is what was measured.

> ### The lesson, which is not "read more carefully"
>
> **A plan's prose and its code drift apart, and the assertions were right
> nearly every time the prose was wrong.** In almost every row above, the plan's
> *executable* content — the test it told you to write, the script it told you
> to run, the number a gate would print — was correct or self-correcting, and the
> sentence *describing* that content was the part that was stale.
>
> That is not a coincidence. An assertion is re-evaluated every time it runs; a
> sentence is evaluated once, by its author, before the thing it describes
> exists. **The next plan-writer should assume every count in their prose is
> already wrong, and put the load on things that re-measure themselves.**
>
> Task 19 is downstream of the same lesson. It is why this phase has a baseline
> file whose header says *"nothing is carried"* and means it.

---

## 6. Defects in this phase's own design

Two, both recorded in `specs/plans/broodline_phase8_look_and_ship.md` §11 rather
than quietly reimplemented.

**§4.6's palette gate was not implementable as specified, and would have passed
while the palette got worse.** It asks for "a minimum separation" to be
asserted. There is no minimum available: 45 constraints against 6 free colours,
and the worst pair is one that deleting Pale outright would not improve. Any
threshold low enough to pass today is low enough to pass a materially worse
palette. Replaced with a tracked baseline over all 45 measurements, which fails
when any number gets *worse* — the question a change can actually answer.

**§7.1's "resolved font-size ≥ 11px" gate was not implementable in
`Broodline.UI.Tests`**, which has no attached `Panel` — nothing resolves, so
there is no resolved font-size to read. Implemented in `verify-uss-tokens.sh` as
text analysis against the `.t-num` marker, which catches the violation in the
stylesheet rather than in one instantiated tree.

**And the design inherited `accessibility.md` §4's wrong pairs.** Both documents
ranked the palette by lightness (ΔL\*), which does not rank confusability — it
inverts it. §4 scored **one for three**, and the design's own nominated worst
pair, Skitter/Pale, measured ΔE 71.4. The real worst pair, Vetch/Pale at ΔE 7.2,
was named by neither. Corrected in `accessibility.md` §4.1.

---

## 7. Four fixes nobody planned, all found by measurement

None of these was in the plan. Each was found because something was measured
rather than assumed.

### 7.1 `Tokens.uss` compiled to zero rules, for seven phases

**The most consequential defect in the project's history so far**, and it
produced no error of any kind.

The file's header comment cited a path containing a glob — a directory wildcard
immediately followed by a slash — and that two-character sequence **closed the
block comment eighteen lines early.** The following lines were then parsed as USS
source, where apostrophes in ordinary English words opened unterminated strings,
and **the file compiled to zero rules.**

**Every `var(--token)` in the project therefore resolved to nothing** — not to a
fallback, not to magenta, not to an exception. Colour, padding, radius and
font-size all went at once, in the Editor, in the screenshot harness, and in the
built player. **All twelve captures in the corpus changed when it was fixed.**

Fixed in `b374bcd`. Two wrong theories were committed to comments before the
right one, and both are recorded there.

> **The standing rule this produces:** never write a comment-closing sequence
> inside a USS comment — including inside a path, a glob or an example. There is
> no diagnostic for it.

### 7.2 `--text-tab` was 12px where the handoff gives 11.5px

Found by sweeping the type scale against the handoff's table after Task 5
reported the token as referenced by nothing. **Every other size token sits inside
its handoff range; this one is the only exact value in that table and the only
one that missed it.**

Fixed in `7649b01`, while nothing consumed it and the fix therefore cost nothing.
The handoff distinguishes two roles that are easy to conflate — "Tab label" at
11.5px and "Nav label" at 10px — and the bottom nav correctly uses the latter.

### 7.3 `--radius-pill: 999px` drew every short element as a lens

**CSS and UI Toolkit clamp an over-large corner radius differently, and the
difference is visible.** CSS scales an over-large radius down by **one common
factor** until the corners fit. **UI Toolkit clamps each axis separately**, to
half the width and half the height, and draws the resulting ellipse quadrant.

Measured on a rendered pixel: the 99×27 currency chip reached full height **32px
in from its end — 65% of the chip was taper.** Established by probe rather than
by inference: 50 bars through the capture path at five heights and six widths.
At exactly half the height the cap is a true semicircle at every size tested;
above it the taper grows with the radius, bounded only by half the element's
**width**, so a wider pill is a worse pill. A square element is immune.

**The token is retired.** `999px` was the one row of the handoff's radius table
that does not survive translation to USS. Fixed in `dfbd6a2`.

### 7.4 The affinity gate counted its own documentation as a violation

`MainThreadAffinityTests` greps source text for the pattern. It already excluded
its own file, because it knew it would match itself — **but not any other file
that names the pattern in prose.** So when `24b08dc` added a doc comment
explaining that this very gate is a known, correct failure against three
deliberate sites, **the sentence naming them became a fourth offender. The gate
over-reported, and the thing it punished was someone documenting it.**

Fixed in `d0267f2` by blanking comments and string literals before matching,
preserving offsets so reported line numbers still point at the real line.

**This is the second time in one phase a gate had to learn the difference
between code and commentary** — `verify-uss-tokens.sh` strips comments before its
hex check for exactly the same reason, because `Theme.uss` documents the two
primitives USS cannot express, hexes and all.

---

## 8. What measuring the gates found, beyond the numbers

Three things, all from Task 19's own measurement pass. They are here because
each one contradicts something written down elsewhere in the repo.

**PlayMode does not deadlock.** `run-unity-tests.sh`'s own comment says it is
"EXPECTED TO DEADLOCK on this Editor", and Phase 7's baseline carried the
PlayMode row as *"not attempted"* on the strength of that. It was attempted here
as a bounded probe expecting to be killed, and **completed in 29 seconds, 4/4
green.** One run does not prove the deadlock gone; it proves the comment is not
reliably true, which is enough to stop it being a reason not to measure. The
comment is deliberately left in place — rewriting a script on one green run is
the same over-confidence in the other direction.

**Phase 7's settings-drift finding did not reproduce.** Phase 7 recorded that
the Unity runs put `SENTIS_ANALYTICS_ENABLED` back on iPhone and rewrote two URP
assets. Measured across **three** separate Unity batchmode invocations at this
tree — EditMode, the IL2CPP player build, and PlayMode — `git status
--porcelain` was empty after each, and the scripting defines matched across both
platforms. **This does not mean Phase 7 was wrong.** It means the drift is not
unconditional, so a clean tree after a Unity run has not proven the guard
unnecessary. The ordering discipline is kept, and the settings gate was still
taken *after* all three invocations.

**The self-hosted runner is registered, and the determinism gate is red on
`develop` anyway.** Two claims are corrected here at once. The runner exists —
`gh api repos/:owner/:repo/actions/runners` returns `sd-sassadi-m1`, macOS/ARM64,
online. And `determinism.yml` run `35252288421`, triggered by the merge of PR #7
into `develop`, **failed after 4h45m56s**. Its only green execution ever remains
one manual `workflow_dispatch`. The same gate is **green locally at this tree** —
`PASS: 500 scenarios agree`, on a cold IL2CPP player build — so this presents as
a CI-execution problem rather than a determinism problem. **Nobody owns that gap
and no task number is assigned to it.** `tests.yml`, by contrast, is green, and
Phase 7's record saying its last runs were red is now stale.

---

## 9. What the next session inherits

In priority order.

1. **Task 16**, and with it §2.1's `OptionRow` decision and §2.2's `Label`
   margin. Both want a human looking at a device, and §2.2's fix wants doing in
   the same pass because it moves all sixteen captures.
2. **Task 17**, then **Task 18** promptly after it — billing starts at
   `terraform apply`. Task 18 is also the first colour-blind check of the
   palette (§2.7) and the first time anyone holds the app.
3. **Push the branch.** Nothing here has been through CI (§8), and the
   determinism gate's CI failure will not diagnose itself.
4. **Phase 9** inherits §2.3's constraint on Pale, and the whole of
   `bible §10.4`'s new subsection, before it draws anything.
5. **Phase 10** inherits `RegionView` and §2.4's thirty-versus-eight
   reconciliation.

---

*Owns: the Phase 8 record — what closed, what is owed, what this phase found in
its own plan and design, and what measuring the gates found. Does not own: the
numbers, which are `implementation/results/phase8-test-baseline.txt`'s; the
palette reasoning, which is `implementation/results/palette-decision.md`'s and
`species-collision.md`'s; or the tester's report, which does not exist yet.*
