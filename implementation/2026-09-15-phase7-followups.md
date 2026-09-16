# Phase 7 — What Execution Left Behind

*The durable record of the slice-polish phase: what it built, what it proved,
what it left owed, and — first, because it is the thing most easily lost — the
four gates it could not close and the sentence it therefore does not get to
write*

> **Why this file exists.** The working ledger that recorded these findings
> lives in git-ignored scratch and will not survive a `git clean`. This is the
> durable copy, and for most of these items it is the only one. It is written
> to the same contract as `2026-09-15-phase6-followups.md`, which it should be
> read beside.
>
> **State at the time of writing:** seventeen implementation tasks are through
> their review gates on `phase_7` — **1, 3, 4, 5, 6, 7, 8, 9, 10, 11, 13, 14,
> 15, 16, 17, 18, 21** — plus this one, **35 commits** from the branch point at
> `92b7a70` (36 counting the commit that carries this file, which did not exist
> when the number was written; `git rev-list --count 92b7a70..HEAD` is the
> authority, and the parenthetical is here because a record whose thesis is
> that prose counts go stale silently should not leave one ambiguous). **Tasks 2, 12, 19 and 20 have not been started**, and each of them
> is a human at a keyboard with hardware, a runner registration, a billing
> decision or an App Store Connect account — not an engineering task that was
> skipped. They are §1.
>
> **The branch has never been pushed.** There is no `origin/phase_7`, so no CI
> has run on any of this — not `determinism.yml`, which cannot run at all (§1),
> and not `tests.yml`, which can. The last `tests.yml` runs, on `phase_6` and
> `develop`, are **red**, and they are red for the reason in §3.
>
> Counts are **not** quoted in this prose — they live in
> `implementation/results/phase7-test-baseline.txt`, which supersedes any
> number written into a sentence anywhere, including this one.
>
> **This record is deliberately not a success report.** Section 8 enumerates
> roughly fifteen defects this phase found in its own plan — one of which was
> retracted, and the retraction is recorded beside it — and section 9
> enumerates three errors the controller made. A record that read as uniformly
> successful would be false.

---

## 1. The four gates this phase cannot close

The phase's Definition of Done names eleven clauses. **Four are not green, and
none of the four is blocked on anything this session could write.** The other
seven are, and §1.1 names the evidence for each rather than asserting it - an
earlier draft of this sentence said they were "recorded below" and they were
not, which is this record's own thesis happening to this record.

| Gate | Blocked on | Verified today |
|---|---|---|
| **`TheTrackedCapturesAreCurrent` green under `0.4.0`**, and with it `TheDeviceRunsRallyActuallyChangedTheSimulation` | **Task 2** — a capture taken on physical iOS hardware, after the engine change | `dotnet test` reports it red. §3 |
| **The determinism gate run in CI**, on the self-hosted runner, on this branch's tip | **Task 12** — a self-hosted macOS runner, registered | `gh api repos/:owner/:repo/actions/runners` → `{"total_count":0,"runners":[]}`. `gh run list --workflow determinism.yml`: the newest run has been **queued for 9h5m** and the four before it were all **cancelled** after 8h34m–24h. A queued run is not a failing run, so nothing ever goes red |
| **`smoke-loop.sh` PASS against Cloud Run** — the loop on a deployed stack | **Task 19** — `terraform apply`, a migration, a seeded `servers` row, a published bundle, two deploys, and a human who has decided the Cloud SQL instance stays up and billing | `gcloud run services list --project broodline-508416` → `Listed 0 items.` `gcloud sql instances list` → `Listed 0 items.` `implementation/scripts/smoke-loop.sh` **does not exist**; the directory holds `smoke-wave.sh` only |
| **A TestFlight build installed by someone who is not the developer**, and their report in this file | **Task 20** | Not started. There is no build, no upload, and no tester's report — and the section of this file that would carry it verbatim is therefore absent rather than empty |

### 1.1 The seven that ARE green, and what evidences each

Named so a later reader can confirm any one of them without re-deriving it.
Counts are the baseline file's, not these sentences'.

| Clause | Evidenced by |
|---|---|
| **Wave 6's 500 corpus hashes byte-identical across the lane refactor**, and the cross-runtime diff green **before** the bump | `tests/engine/CorpusBaselineTests.cs`, which pins the tracked `tests/engine/corpus-baseline.txt` against `SimVersion` on every `dotnet test`. Task 1's diff across the bump is **header-only** - `0.3.0` → `0.4.0`, all 500 scenario hashes byte-identical - and `cross-runtime-diff.sh` was confirmed run with `SimVersion` still reading `0.3.0` immediately before it, `PASS: 500 scenarios agree`. Re-run at this tree: PASS again |
| **`WaveContentTests` green** - the cold-open pair wins wave 1 in every pocket pair, the trio wins wave 2, Taunt changes wave 2's outcome | `tests/engine/Combat/WaveContentTests.cs`, three tests: `Wave1_IsWonByTheColdOpenPair_InAnyTwoPockets`, `Wave2_IsWonByTheTrio_WithTauntOnTheVetch`, `Wave2_WithoutTaunt_TheLashReachesPastTheFrontLine`. The third is the contrastive half - without it the first two could hold for a reason that has nothing to do with Taunt |
| **`ftue.test.ts` green with `seeded === 0`** | `services/api/test/ftue.test.ts`. §2 |
| **`preview` publishes `mutation: 1` and `commit` mutates**, on the first splice, from **one** row count | `splice-preview.test.ts`'s *"forecasts a guaranteed mutation before this player's first splice, and the base rate after"*; the commit side in `splice-commit.test.ts`; and both together in `ftue.test.ts`, which reads `forecast.mutation === 1` from the route and then `mutated === true` from `GET /v1/lineage`. The **one row count** is `isFirstSplice`'s `count(*)` on `splices`, which both routes read - Task 7's Step 5 weakening is the evidence that they read the SAME one: it left preview 14/14 green and reddened commit on two tests, which is the asymmetry a single shared distribution predicts |
| **A lost wave 6 grants a Pale once**, and base stock never mints one before it | `wave6-pale.test.ts` (loss grants one, a second loss grants nothing, a win grants it too, and the marker is what gates base stock afterwards) and `founder.test.ts`'s *"base stock never mints a Pale before the wave-6 grant has fired"*, which draws 200 seeds either side of the marker |
| **`weakenings.md` row 7 a test**, and the 24h weakening seen to redden it | `services/api/test/sweep.test.ts`, and row 7 is **booked CLOSED** in `services/api/test/weakenings.md` with both runs tabulated. **With a correction the record should carry:** the brief predicted the 24h weakening would redden the 23:50/00:10 pair and **it does not** - that pair is twenty minutes wide and nowhere near either threshold, so it stays green under 24h and 48h alike. What reddens is a third, permanently shipped test - *"a consumed row strictly between 24h and 48h old survives the 48h window"* - built for the purpose. The clause is satisfied; the brief's prediction about **which** test satisfies it was wrong, and that is the row 7 pattern repeating on itself |
| **The coupling guard reddening** when either half of the client floor moves alone | `implementation/scripts/verify-unity-settings.sh`'s client-floor check, run in CI by `tests.yml`'s `client-settings` job. Both halves were weakened separately and the real FAIL output captured. **This is the one of the seven whose gate is a shell script rather than a test, so its evidence is a transcript rather than a suite - and the transcript is therefore reproduced in §1.2 below**, not merely cited |

### 1.2 The coupling guard's evidence, copied here so the record stands alone

**Why this is transcribed rather than cited.** Every other clause in §1.1
points at a test in a suite, which any later reader can re-run. This one points
at a weakening run by hand, and the only place it was written down was
`task-10-report.md` — which lives under `.superpowers/sdd/`, **a directory this
file's own preamble says will not survive a `git clean`.** A record whose
single non-suite citation evaporates with the scratch directory is a record
that will one day assert this clause with nothing behind it. The report is
still worth reading for the full context and the `sort -V` analysis; what
follows is the part the claim actually rests on.

The guard is `verify-unity-settings.sh`'s client-floor check: the highest
bundle's `minimumClientVersion` against `ProjectSettings.asset`'s
`bundleVersion`. Baseline, with the two equal at `0.3.0`, is `ok` and exit 0.

**Half 1 — the client drops below the floor.**

```
$ sed -i '' 's/bundleVersion: 0.3.0/bundleVersion: 0.2.0/' client/ProjectSettings/ProjectSettings.asset
$ bash implementation/scripts/verify-unity-settings.sh
  FAIL  client bundleVersion '0.2.0' is below bundle 0.1.3's minimumClientVersion '0.3.0' (or one is unreadable)
EXIT CODE: 1
```

**Half 2 — the floor rises above the client.**

```
$ sed -i '' 's/"0.3.0"/"9.9.9"/' config/bundles/0.1.3/manifest.json
$ bash implementation/scripts/verify-unity-settings.sh
  FAIL  client bundleVersion '0.3.0' is below bundle 0.1.3's minimumClientVersion '9.9.9' (or one is unreadable)
EXIT CODE: 1
```

That is the clause as written — **either half moving alone reddens it** — and
the two probes are not the same probe twice: one moves the client, one moves
the bundle, and they fail with different values in the message.

**And it FAILS CLOSED**, which matters more than either half above, because a
guard that answers `ok` when it cannot read its inputs is worse than no guard.
Two of the five paths, probed directly:

```
# A — the manifest key renamed, so the version cannot be parsed
$ sed -i '' 's/minimumClientVersion/minClientVersion/' config/bundles/0.1.3/manifest.json
  FAIL  client bundleVersion '0.3.0' is below bundle 0.1.3's minimumClientVersion '' (or one is unreadable)
EXIT CODE: 1

# B — the bundle directory listing comes back empty
$ mv config/bundles config/bundles_hidden
  FAIL  client bundleVersion '0.3.0' is below bundle 's minimumClientVersion '' (or one is unreadable)
EXIT CODE: 1
```

In both, the unreadable value arrives **empty** and is visible as `''` in the
message, and the script routes it to `bad` rather than to a skip. The other
three traced paths — a missing manifest, a malformed version string, and a
missing client version — reach the same branch. Every edit above was reverted
and `git status --porcelain` confirmed clean after each.

**What this evidence does NOT cover, stated so nobody upgrades it later.** It
is a transcript of four probes run once by a person, not a gate that re-runs.
Deleting the client-floor check from the script tomorrow reddens nothing
anywhere; only `tests.yml`'s `client-settings` job running the script at all is
automatic. Closing that needs a test harness for a shell script, which is
**owed and out of scope for this phase** — and which is a different problem
from the one this section fixes, which was simply that the evidence lived
somewhere non-durable.

**A fifth thing is owed and is not a Definition-of-Done clause**, so it is
recorded here rather than in the table: **Task 17 Step 6, the eyes-on play
through of beats 1–8 against a local `api`**, was not run. The implementer
reported it honestly rather than claiming it, and the reviewer noted it was
"accurately disclosed and blocked on infrastructure, not concealed" — the
Docker daemon was down in that session. **Docker is running on this machine
now** (server 29.8.0), so the blocker that stopped it has lifted; what remains
is a person opening the Editor. It matters more than its size suggests: see
§5, because it is still true that **nothing on this branch has ever executed a
single director-to-view join**.

### The sentence this phase does not get to write

The plan closes with one:

> *"With `ftue.test.ts` asserting `seeded === 0` from account creation to the
> Lineage View, and `smoke-loop.sh` doing the same over HTTP against real
> infrastructure, **the loop closes, on a deployed stack, along a line a player
> can walk.**"*

**Half of that is now true and half of it is not, so the sentence is not
written here.** `ftue.test.ts` exists and asserts `seeded === 0` (§2).
`smoke-loop.sh` does not exist and there is no stack to run it against. There
is no TestFlight build and no player who is not the developer.

This is exactly the error Phase 6's record made and this phase exists partly to
stop repeating: **Phase 6 wrote "the loop closes" about two half-loops that met
on paper.** Writing the stronger sentence today, with two of its three clauses
unevidenced, would be the same mistake with a longer sentence.

**The sentence is owed to the session that completes Tasks 2, 12, 19 and 20.**
When that session runs `smoke-loop.sh` green against a deployed stack and puts
a build in someone else's hands, it should write the sentence, and it should
write that it took until then.

---

## 2. The first hour IS one line a player can walk — SERVER-SIDE

This is the claim this phase *did* earn, and it should be recorded as **newly
earned** rather than assumed. The qualifier in the heading is not decoration:
what was demonstrated is that **the server serves that line**, in the order
`FtueDirector` walks it, with every creature earned. **No player-facing code
executed.** That belongs in the heading because this is the section that will
be quoted later as what Phase 7 earned, and a heading is what gets quoted.

`services/api/test/ftue.test.ts` drives design §5's beats over the real routes,
against real Postgres and the real `sim` service built and run as a child
process, and **asserts that its roster ledger's `seeded` count is zero**. Every
creature in the walk arrived through a response:

```
+2  EARNED  POST /v1/account granted starter.json's cold-open pair
+1  EARNED  wave 1 won: the FIRST completion grants the Founder instead of a roll
+1  EARNED  wave 2 won: the SECOND completion grants rolled base stock
+2  EARNED  POST /v1/ftue/splice-stock granted the tutorial pair
-1  EARNED  POST /v1/splice/commit consumed both parents and produced the child
+1  EARNED  wave 6 LOST: the Wave Defeat screen grants a Pale carrying Chill
+1  EARNED  wave 6 WON with the Pale: the Pale grant is once-per-player, so this one is a roll
```

`earned === 7`, `seeded === 0`, and `GET /v1/roster` agrees with the books. The
ledger reconciles after **every** step, so a creature entering the roster
unbooked reddens at the next one.

**The wave-6 pair is the sharp part.** The same two starter creatures, in the
same two pockets, against the same wave: **they lose**, and then they win once
the Pale the defeat screen handed over is deployed beside them. The contrast is
inside one test, so the Win is not a fixture that happens to win — it is the
Loss plus one creature the player was given *because* they lost. Both verdicts
came from the engine.

### What it closes, and what it does not

Phase 6's §12 item 9 asked for "a drive whose supply line is unbroken", and
recorded that until one existed, *"the loop closes" is two half-loops that meet
on paper*.

- **For the first hour, that is now closed.** Account creation to the Lineage
  View to a won wave 6, with nothing inserted.
- **For the wave-7 leg, it is not, and `loop.test.ts` still says so in code.**
  That file books ten of its fifteen creatures as SEEDED, and its third test —
  `does NOT yet join its two legs` — is still present and still passing.
  Earning five Taunt/Splash-at-tier-III creatures through the splice is not
  something this phase established is reachable, and `ftue.test.ts` says
  nothing about it.
- **For the CLIENT, it is not closed at all, and this is the gap most likely
  to be read past.** `ftue.test.ts` walks the api's routes in the order
  `FtueDirector` walks them; it does not press a button, bind a view, or
  resume a turn. **Nothing on this branch has ever executed a single
  director-to-view join** (§5), and the eyes-on walk of beats 1–8 that would
  is owed (§1). So "one line a player can walk" is, precisely, *one line the
  server will serve to a client that asks for it in that order* — which is
  worth a great deal and is not the same sentence.

  Stated here rather than only in §5 because **this is the same shape as the
  error this phase exists to correct, one notch smaller**: a true claim about
  one half, written where a reader will take it for the whole. `ftue.test.ts`'s
  own header leads with the disclaimer; this section now does too.

**All three facts are true at once.** A reader of either test file can find the
other: each file's header names the other by path and says which reading it
takes.

**And neither file has been run against a deployed stack.** §1.

### One structural change this made

`RosterLedger` moved out of `loop.test.ts` into
`services/api/test/roster-ledger.ts`, because the two drives need the same
instrument for opposite readings. A second copy of a sixty-line class whose
entire purpose is to prevent one class of dishonesty is the shape
`wave-helpers.ts`'s own header refuses, and the drift would be silent: a copy
that stopped measuring, or stopped reconciling, still reads as a ledger and
still prints a plausible total.

---

## 3. The committed round-trip proof is dark, and one red test is all that says so

`dotnet test Broodline.sln` reports **exactly one failure**, and it is meant to:

```
Broodline.Sim.Tests.Combat.ReplayArtifactPresenceTests.TheTrackedCapturesAreCurrent
```

Task 1 moved `SimVersion` from `0.3.0` to `0.4.0` — waves 1 and 2 needed a
six-pocket lane and `WaveDef.ForId` now answers four wave ids where it answered
two. Under `solo_execution` §9.4 that supersedes the tracked device and editor
captures, which were taken under `0.2.0`. **No iOS hardware was available.**

**This is the same deliberate red Phase 6 recorded, one engine version further
on, and it is fixed by Task 2's recapture and by nothing else.** Do not
re-baseline it, do not skip it, and do not "fix" it. `dotnet.skipped` is 0 and
must stay 0: there is no `Skip` anywhere in the solution, so no count will
report this for you.

**The part that is easy to lose, and which this phase verified directly:**
while that test is red, **`EditorReplayTests` and `DeviceReplayTests` bail
before their assertions and report PASSED**. Every re-simulating test in those
files opens with `if (ReplayArtifact.Superseded(record, _out)) return;`. So the
committed round-trip proof — Phase 3's done-when, the thing the artifacts exist
for — **is not being proven at all right now**, and the suite says "passed" for
both files while it is not. `TheTrackedCapturesAreCurrent` exists to be the
guard on that guard, and it is the only thing in the solution that announces
it.

**Carried to whoever runs Task 2**, both items verified during Task 16:

1. **Do not read a green suite as evidence the round trip holds.** Task 16's
   new PlayMode capture test is currently the only live proof that the Editor
   capture path still works at all.
2. **Do not trust the HUD tick readout blindly** when timing the tap at ticks
   184–207. `WaveRunner.Snapshot()`'s `Integrity`/`Tick` wiring was unasserted
   for most of this phase — Task 22 has since added the race-free assertion
   (§4, instance nine) — but eyeball that the readout advances sensibly before
   relying on it.

The device tap window is **ticks 184–207**; the Editor's is the looser
184–240. They are different windows and the Phase 5 record conflated them.

---

## 4. The dominant defect of this branch: ELEVEN instances of one shape

**A test that reads as covering a property, passes, and never touches it.**

It appeared **eleven** times across eighteen tasks, in code written by five
different implementers and caught by six different reviewers — and the
eleventh is in the file Task 22 wrote to close the pattern, found by the review
of that task. That is not an embarrassment to bury in a footnote; it is the
most useful datum in this section, because it says the shape survives being
named, written down, and read about immediately beforehand. The best
formulation
anyone produced is Task 16's implementer's, and it is the sentence to carry
into any later phase's plan:

> **A fixture whose expected value is a type's default is not a fixture.**

Because when the expected value is `0`, `null`, `false` or `""`, the assertion
passes against a correct implementation, against an omitted assignment, and
against a hardcoded constant — three hypotheses, one green.

The eleven:

1. **Task 6** — `loop.test.ts` absorbed a real behaviour change. Its player's
   first-ever completion became wave 6, so the grant silently changed from a
   roll to a Hollow Founder; `RosterLedger.earn()` measures a roster-count
   delta, which is 1 either way. The file that exists *because* a transcript
   conflated earned with seeded had begun conflating Founder with rolled.
2. **Task 8** — the same file proved the new `+1` was the Pale **by exclusion
   in a comment**, not by an assertion.
3. **Task 9** — `GET /v1/lineage`'s ordering test could not see the hazard it
   named: `grantTutorialStock` inserts both creatures in one statement in one
   transaction, so both carry an identical transaction-stable `acquired_at`,
   and the test's fixtures were in separate transactions with distinct
   timestamps. It passed while the gap stood.
4. **Task 13** — `Session.RefreshAsync`, the 401 retry that keeps a returning
   player signed in, had zero coverage.
5. **Task 14** — `StandardConfirm_IsPrimaryAndDismissibleByTheScrim` asserted
   `pickingMode == Position` and **not the dismiss behaviour**. Delete
   `ConfirmDialog.cs:118`'s `RegisterCallback<ClickEvent>(_ => cancel?.Invoke())`
   and `pickingMode` still reads `Position`, the test still passes, and "tap
   outside a Standard dialog cancels" is silently lost for every screen reusing
   the component.
6. **Task 16** — `From_CarriesTheTicksAndIntegrityTheEngineReported`: wave 6's
   integrity 2 minus the Courser's cost 2 is **0**, which is `default(int)`, so
   a builder that hardcoded `IntegrityRemaining = 0` or omitted the assignment
   passed. Ticks had been guarded against exactly this with a second
   contrastive fixture; integrity had not, one line away.
7. **Task 16** — `WaveScreensTests.cs:444`'s `...FromTheEngine` never touches
   the engine.
8. **Task 16** — `WaveRunner.cs:1379`'s `inputEnabled: true` is load-bearing
   and was untested: a capture with no Rally in it is an invalid artifact, and
   flipping that literal leaves every test green and fails on hardware with a
   person waiting.
9. **Task 16, fix round** — a racy PlayMode assertion was deleted, and it took
   a property with it: nothing asserted that `WaveRunner.Snapshot()` fills
   `Integrity` and `Tick` **from the live runner**. Transpose those two
   assignments and every EditMode test stays green (their snapshots are
   hand-built) and every PlayMode test stays green (only "non-empty" and
   "changed" were asserted). **Closed in Task 22**: integrity holds at 2 for
   ~500 of wave 6's 540 ticks, so a `StringAssert.StartsWith("Integrity " +
   runner.Runner.Integrity, ...)` needs no tolerance — and it is paired with an
   `AreNotEqual(integrity, tick)` so the prefix check cannot go vacuous if the
   block is ever moved earlier.
   **Shown to discriminate, not asserted to:** the transposition was applied to
   committed source (`WaveRunner.cs:313-314`), PlayMode re-run headlessly, and
   the result was `total=3 passed=2 failed=1` — failing by the new assertion's
   own message, *"the HUD's integrity readout must come from the live runner"*.
   Reverted, and the tree confirmed clean afterwards.
10. **Task 17** — and this one is inside the test for the property its author
    had written a comment to explain. `ScreenFlowTests`'
    `ASheetOverlaysAndThenDismissesItself` asserted `sheetLayer.display ==
    None` **after** `resume(true)` returned, which passes whether `HideSheet`
    runs before or after `TrySetResult` — so it read as covering the ordering
    race at `ScreenFlow.cs:133-136` and did not touch it.

11. **Task 22**, in `ftue.test.ts` — the file whose entire subject is claims
    that assert more than they prove. It read:

    ```ts
    const vetchId = opened.find((c) => c.species === 'Vetch')!.creatureId
    expect(vetchId, 'the starter Vetch').toBeDefined()
    ```

    The `toBeDefined()` **cannot fail**. A missing starter throws a
    `TypeError` on the line above and the assertion never executes; a present
    one makes it trivially true. It reads as a guard on the fixture and is
    decoration. Replaced by an assertion on the roster itself —
    `expect(opened.map(c => c.species).sort()).toEqual(['Ember', 'Vetch'])` —
    which fails on the thing it claims to check. A second instance in the same
    file's third test closed the same way: `expect(EXPECTED_EARNED).toBe(7)`
    compared a literal to itself and is now `expect(ledger.earned).toBe(7)`, a
    runtime value.

    **The variant worth naming separately**, because it is not the
    type-default shape and the one-line rule does not catch it: *an assertion
    placed after a non-null assertion that would already have thrown*. Same
    outcome — a green that carries no information — reached by a different
    route.

**Applied with judgment, not as a ban.** Task 18's re-reviewer flagged
`queuedWave.Attempts == 0` as a type default and then correctly concluded it is
**deliberately contrastive** against the paired 5xx and transport tests
asserting `Attempts == 1`. Asserting a default is fine when something in the
same test makes it mean something. The rule is a prompt to check, not a
prohibition.

**It was twice caught proactively rather than in review**, which is the only
sign on this branch that the lesson is transferring: Task 18's implementer
strengthened a persistence assertion unprompted on exactly these grounds, and
Task 17's implementer mutation-checked their own fix for instance 10 — moved
`after` past `TrySetResult`, watched the new test fail, reverted — with the
reasoning *"given this test had already read as covering the ordering and
didn't, an assertion that it's real now seemed worth more than my word for
it."*

---

## 5. Two components shipped with no production caller

Both were **built, reviewed, tested and merged** while nothing in the app
constructed them, and in both cases **no test could have noticed**, because
every test constructs the thing itself.

- **`WaveHost` (Task 16).** The whole hosted-wave mechanism — additive scene
  load, the `Hosted` latch, `RunAsync`, the report builder — had no caller
  until Task 17's `FtueDirector`. Its only exercise was the new PlayMode test.
  Flagged at the time so the final review would not read it as wired.
- **`OutboxPump` (Task 18).** Worse, because a docstring already claimed
  otherwise: `BootController.cs`'s own header said it "starts the OutboxPump",
  and **nothing had ever constructed one**. Neither the foreground trigger nor
  the reachability trigger had run in a build. Task 17 found it and added
  `gameObject.AddComponent<OutboxPump>()` at `BootController.cs:96`.

**The generalisation worth carrying:** a component's test suite proves the
component works; it says nothing about whether the app reaches it. The two
questions need different instruments, and this branch had only the first.
Task 17's own self-disclosed residual risk is the same shape a level up —
`FtueDirector.RunAsync` has no end-to-end test, because screens resume through
`Button.clicked` and a bare view has no attached Panel, so *"the JOIN — this
view's callback resumes this turn — is READ, NOT EXECUTED"*. Concretely
undetected today, per the reviewer: swapping `onName`/`onSkip` at
`FtueDirector.cs:313-314`, omitting `next:` at `:517` (which compiles and
yields a permanent hang), and the whole `CampaignSelectView` row-click path.
**A PlayMode test is owed, and so is the eyes-on walk in §1.**

---

## 6. Verify committed content with `git show <sha>:<path>`, never by reading the file

This is the single most transferable operational lesson of the branch, and it
cost a full extra review round in Task 21.

A documentation fix was written correctly, the commit was amended, and the
controller verified the content with `sed -n` **on the working tree** while
checking the commit message with `git log`. Both checks passed. The report said
the fix had landed. **It had not**: the amend updated the message and the
trailer but never `git add`ed the file, and `git show 2af51fa:specs/broodline_bible.md`
still carried all three original defects verbatim. The reviewer caught it by
reading the diff, which is the actual deliverable.

> **The rule.** A dirty tree makes the file on disk and the file in the commit
> disagree silently, and **the file always looks right, because it *is* right —
> just uncommitted.** There is no symptom. Verify with `git show <sha>:<path>`,
> and return `git show --stat` and `git status --short` alongside any claim
> that something landed.

The same root cause produced all three of this session's controller errors
(§9): **verification through a channel that was not the deliverable** — a
signature regex that enumerated modifiers and silently hid every `async`
member; a `grep | head -5` that truncated away the line being looked for; and
this one.

---

## 7. Two reproduced deadlocks, and reasoning got it wrong twice

Both were **advisory-vs-row or row-vs-row lock ordering**, both were found only
by **constructing the interleaving**, and in both cases the prior conclusion
had been reached by reading the code and was wrong.

- **Task 7 — an ABBA cycle introduced by a fix, on controller guidance.** The
  controller told the implementer "advisory lock first, it cannot deadlock
  against `lockParents`", having reasoned only about the splice path. It
  inverts against wave submit, where `settle()` → `releaseCreatures` takes
  **row** locks and `grantWaveBaseStock` takes the **advisory** lock after
  them. Client-reachable by one player: splice a creature that is
  `committed_to` an in-flight issuance while submitting that wave. Reproduced
  as a genuine Postgres "deadlock detected" at ~1s. Closed by taking
  `lockRoster` as the first lock-relevant statement in the wave-submit
  callback.
- **Task 11 — a pure row-lock inversion, no advisory lock involved.**
  `wave/start` began taking creature row locks in **two** statements
  (`settleExpiredForPlayer` → `releaseCreatures`, then `resolveDeployment` →
  `loadOwnedCreatures`), each sorted, whose **union is not globally
  ascending**. Reproduced (40P01). Closed by taking `lockRoster` before
  `issueWave`.

**The invariant that came out of it**, proven by exhaustive repo-wide grep of
`pg_advisory` / `FOR UPDATE` and stated so a later author does not re-derive
it: *every transaction taking `lockRoster` takes it before any lock on that
player's `creatures`/`wallets` rows, and every transaction locking `creatures`
`FOR UPDATE` does so in ascending case-normalised `creature_id` order.*
`normalizeUuid` lower-cases ids before either sorter sees them, so JS `.sort()`
agrees with Postgres's `uuid` btree order and with `releaseCreatures`' SQL
`ORDER BY`. The enumeration found a **fourth** `FOR UPDATE` path
(`routes/creature.ts`) that neither the implementer nor the controller had
listed, verified safe for a different and stronger reason — it locks exactly
one row.

**Still owed:** nothing pins `commitSplice` against `wave/start`'s
`loadOwnedCreatures`, or against `creature/name`'s row lock. Given that two
"reasoned it through" conclusions on this exact subject were wrong, a
constructed test for one of those pairings is worth adding.

**Also owed, and flagged as a deferred Important**: the lock-rule comment at
`wave.ts:386-397` **prescribes correctly and justifies badly**. "One sorted
statement is safe, there is only one statement to order against itself" is not
sufficient — a single sorted statement *can* deadlock against a two-phase
transaction, which is precisely what Task 11 reproduced. `consumeAndRefuse` is
safe only because of the `wave_issuances_one_live` domain invariant, which the
comment does not cite. And its "paid for twice" narrative retrofits three
distinct hazards — roster-cap TOCTOU, advisory-vs-row order, and genuine
two-phase row reordering — into one story, when only the third is what the rule
is about.

---

## 8. Defects found in the plan and the design, not the implementations

Each of these was a defect in the **instructions**, caught by an implementer or
a reviewer running something. They are listed in task order.

**Task 1 — a target file that already existed.** The plan listed
`tests/engine/Combat/WaveDefTests.cs` as "(new)"; it existed with six tests.
The implementer's first pass clobbered them and self-caught it.

**Task 1 — the verification steps omitted the api suite**, and that omission
had a consequence: the `SimVersion` bump to `0.4.0` left
`services/api/test/replay-format.ts`'s hand-maintained `DEFAULT_ENGINE_VERSION
= '0.3.0'` behind, `sim` correctly refused every replay with `engine_too_old`,
and **41 api tests failed for a reason that had nothing to do with what they
were testing**. Neither the implementer nor the reviewer had cause to run the
suite that would have shown it. Fixed by *deriving* the version from
`SimVersion.cs` rather than retyping it, following the in-repo precedent
(`ReplayArtifact.CapturedUnder` had been converted from a hand-maintained
constant for this exact reason).

**Task 5 — the pseudocode introduced a redundant query** on the one route
carrying an SLO. `/v1/sync` already selects the full `campaign_progress` row,
and the plan's snippet then called `readMarkers()` to re-query the same row for
one of its columns. Ruled by a human: split the accessor — a pure
`markersFrom(row)` beside the querying `readMarkers` — so one module still owns
the column names and the round trip goes away.

**Task 7 — two.** Step 6's `git add` line omitted
`client/Assets/UI/ServerError.cs`, which the repo's three-file error-code rule
requires. And the preview snippet referenced `playerId` before it was resolved.

**Task 13 — the brief contradicted itself** on `Session`'s constructor:
the pseudocode took `baseUrl`, the test snippet did not.

**Task 14 — two phantom Unity APIs.** The brief's test used
`VisualElement.GetCallbackCount<T>()`, which does not exist in Unity 6000.6
(confirmed by reflection across every managed DLL), and its object-initializer
for `SpliceDialog` does not compile because the setters are `internal`.

**Task 16 — an assembly cycle the brief could not have compiled.** It put
`WaveReport` in `Broodline.Game` and gave `WaveDefeatView.Bind(WaveReport)` to
a view in `Broodline.UI`. The asmdef graph is `Broodline.Game → Broodline.UI`,
so UI cannot reference Game; Unity refuses it. Corrected before dispatch: the
plain-data types go in `Broodline.Model` (which both reference), and the code
that reads the engine `Outcome` stays in `Broodline.Game` as a separate static
factory — so `WaveReport.From` could not be a static on the type as the brief
wrote it. The brief also never defined `HudSnapshot` at all.

**Task 18 — a test in the brief that cannot run.** `BackoffIsExponentialAndCapped`
called `box.Next(T0.AddHours(1))` with the same `now` every iteration, so once
the first `Fail` set `NotBefore = now + 2s` the next `Next()` returned null —
which the brief's *other* test explicitly requires — and iteration 2
dereferenced it. The property was right and the loop was broken.

**Task 18 — a citation that pointed at the wrong file.** Step 4 cited
`adversarial.test.ts` for the same-key idempotency property. It is at
`wave-submit.test.ts:174`. The implementer checked a citation rather than
trusting it, which is the second time in that round that paid.

**Task 17 — nine, and it is the worst brief on the branch.** One is
**structural rather than API drift**: `ScreenHost.Show(VisualElement)` returns
**void** — its entire public surface does — and every view's `Bind` returns
void too. So the brief's `await _host.Show(new DeployView().Bind(...))`, which
is the director's core mechanism in every beat, does not compile, **and the
"show a screen, wait until the player is done, continue" model is unspecified
anywhere in the repo.** It had to be designed: `ScreenFlow`, which turns a
screen's own completion callback into a `Task` via a `TaskCompletionSource`,
with the caller naming which callback ends the turn. No reviewed Task 15/16
signature was changed to accommodate it.
The other API-level ones: `DeployScreen.Build`'s real signature; `SpliceScreen.Build`
taking three parameters with no charges and no four-argument overload;
`CreatureDto.AcquiredAfter` (zero hits); `ScreenHost.ShowTabs` (does not
exist); `Button.SendClick()` — **the fourth phantom Unity API in these
briefs**; and `OutboxClient` returning `OutboxResult<T>` rather than bare
responses, which a director assuming `Sent` would null-reference on the first
offline player.

**Task 17 — and a real hole in the brief's derivation table**, which the
controller had verified and called correct. `Ftue.Derive` answered `Lineage` on
**every** launch with `cleared ∈ [2,5]` and `splices ≥ 1`, so a terminal beat 8
parks a *returning* player at the lineage tree forever and **the campaign
becomes unfinishable**. Fixed by giving `LineageView.Bind` a `next` that
continues to Campaign Select.

**Task 21 — the file list undercounted.** The "Files:" line names four files
and omits `specs/plans/broodline_phase7_slice_polish.md`, even though its own
Step 4 requires editing that file's §13. The implementer was right to touch
five.

**Task 22 (this task) — the Definition of Done cannot be satisfied today.**
Four of its eleven clauses depend on Tasks 2, 12, 19 and 20, which are
deliberately batched for a human session. §1.

### The one that was RETRACTED

**`SpliceScreen.PreviewAsync` does exist**, at `SpliceScreen.cs:104`, and the
brief was right to cite it. It was listed as a phantom because the controller's
pre-dispatch grep enumerated the modifiers `public|internal|sealed|static|readonly`
and **not `async`**, so `public static async Task<SplicePreviewResponse> PreviewAsync(...)`
never matched and its absence was asserted confidently.

> **The lesson is worth more than the correction.** A verification regex that
> enumerates modifiers will silently hide any member whose modifier list it did
> not anticipate. **Prefer a plain name grep over a clever signature regex** —
> and see §6, because this is the same failure shape as the uncommitted file.

---

## 9. Controller errors

Recorded because a record of an execution that lists only the implementers'
mistakes is not a record.

1. **Task 7 — guidance that caused a Critical.** "Advisory first cannot
   deadlock against `lockParents`", asserted after checking only the splice
   path. It inverted against wave submit and produced a client-reachable ABBA
   cycle that did not exist before the fix round. §7.
2. **Task 17 — a retracted plan defect.** §8's retraction.
3. **Task 17 — an option offered that cannot be built.** For the
   `LineageView.next` finding, the controller offered "(b) a debug-build assert
   in `ScreenFlow` that a presented turn's resume was wired." The implementer
   showed it **cannot honestly be written**: `ScreenFlow` hands the resume to
   `bind` and has no way to observe whether the *view* kept it — all it could
   check is that `bind` ran, which is unconditionally true. Independently
   confirmed by the reviewer.
   > *"A guard that cannot fire on the failure it names is worse than none."*
   > The controller had proposed a guard whose own failure mode is the pattern
   > this branch hit ten times (§4).
4. **Task 21 — the verification channel.** §6. The worst of the three, because
   it was reported to the user as "the fix landed correctly" when it had not.

---

## 10. What execution added to the design

`specs/plans/broodline_phase7_slice_polish.md` §13 has been updated by this
task. Promoted into it:

- **The five items from the plan's own "What this plan found the design
  missed"** — the wave-6 Pale grant; base stock pre-empting the designed loss
  by minting a Pale; no `servers` row existing outside tests; the supply-line
  gap between wave 2 and the guided splice; and the refresh token having
  nowhere safe to live.
- **The `BundleWaves` → `WaveDef.ForId` equality check**, owed to
  `tools/config-validate`. The JSON and the engine's `Wave1()`/`Wave2()` are
  transcribed by hand and **nothing diffs them**.
- **The Keychain plugin.** Unity has no Keychain API without a native plugin;
  tokens live under `Application.persistentDataPath` with the no-backup flag.
  Owed before any external build.
- **The sweep's scheduler trigger.** `sweep-cli.ts` is owner-run;
  `solo_execution` §10 has no deployment trigger met.
- **`DefileSix`'s ratification.** `Lane.DefileSix()` places six pockets beside
  tiles `{6, 9, 12, 14, 17, 20}`. `combat_numbers` §2 gives the family 4–6
  pockets beside tiles 6–20 and `waves_01_12` wave 1 says six; **no document
  places them.** The layout is provisional, spread so no two are adjacent, and
  owed to `region_roster` §3.
- **`client_architecture` §2's `ref readonly SimState`** is now **six times
  deferred, by count** — recorded by Task 21 so the deferral stays an act
  rather than an omission.

---

## 11. Smaller things, still true

These were graded Minor and deliberately not run into a fix round. They are
recorded because the alternative is rediscovering them.

**Test coverage and fixtures**

- `founder.test.ts`'s non-founder case asserts status 409 only, not
  `code === 'not_a_founder'`. No test covers the missing-`Idempotency-Key` 400
  branch on `POST /v1/creature/name`.
- The wave-6 Pale's once-per-player gate is exercised only Loss→Loss; there is
  no Win→Win or cross-path repeat.
- No test asserts that a brand-new player with zero creatures gets
  `{ nodes: [] }` at 200 from `GET /v1/lineage`.
- `markers` write-once is tested **sequentially**, not under a real
  two-transaction race. Correct on Postgres semantics, but a regression that
  only shows under true overlap would not be caught.
- The `lineage` plan-forcing test's untied half compares an **unforced**
  default-plan result against a forced sequential scan, so it holds only while
  the planner keeps choosing an index scan for this tiny table. Test-infra
  flake risk, not a production bug.
- `CreatureCard`'s `committed` class and silhouette tooltip are untested;
  `WaveHudView.Place()` is uncovered (it returns early when `panel == null`,
  which is every EditMode test), so the `camera.transform.up` lesson is
  protected only by a comment.
- `FtueNotice.For(Rejected, ...)` is never called with a real
  `BroodlineApiException`, so the `ServerError.From` interpolation at
  `FtueDirector.cs:46-49` never executes in the suite. `FtueTests` has no row
  for `splices == 0 && cleared >= 6`.

**Comments and documents that are now false or overclaim**

- `WaveHudView.cs:255-258`'s "should never fire" clamp comment is **false on
  notched devices**: `WaveSceneBuilder`'s ~3.7% framing headroom (~31 of 844
  units) is smaller than `SafeAreaBinder`'s ~59 top / ~34 bottom insets, so
  bars **will** clamp in routine play and detach from their body by up to ~55
  units near the Ark. Invisible in the Editor. The clamp does what it promises;
  the comment is what is wrong. Root cause if ever worth fixing: the safe-area
  padding insets the world-tracking `_bars` layer, which overlays a full-bleed
  camera and does not want to be inset — only the chrome does.
- `WaveHudView.uss:3-4` overclaims: `SafeAreaBinder` sets only
  `paddingTop`/`paddingBottom`, never left/right, so `margin-left: 12px` still
  measures from the display edge. Latent while the project stays
  portrait-locked.
- `grantTutorialStock`'s doc comment credits `lockRoster` with the once-only
  protection that `setMarker`'s `UPDATE ... WHERE marker IS NULL` already
  provides on its own.
- `Replay.cs`'s `BuildLane` error message prints `(int)Terrain` where its two
  sibling cross-check messages print the enum name.

**Structural**

- `pruneLineage` is **unreachable in play this phase**: chamber tier defaults
  to 3, `maxGeneration(3) = 4`, `RETAINED_DEPTH = 5`, and there are no facility
  upgrades. Recorded as a known-dead path rather than rediscovered.
- `mutated`'s no-duplication guarantee rests on an **unenforced app-level
  invariant**: `0005_loop.sql`'s `splices` table has no `UNIQUE` on `child_id`,
  only the PK on `(server_id, splice_id)`. The left join cannot duplicate today
  only because a splice always creates its child atomically — and that file's
  own philosophy prefers a constraint to a convention.
- `OutboxClient` is five related types in one 333-line file. Cohesive now,
  worth splitting as more mutations are added.
- Backoff can stall if the device stays continuously online through repeated
  5xx: the pump only triggers on foreground and on a `NotReachable → reachable`
  transition, which is exactly what the plan specifies. Plan-mandated, and the
  reviewer explicitly declined to call it a defect.
- `sweepRetention` does a single settle/`releaseCreatures` phase per player
  without `lockRoster` and is absent from the lock rule's enumeration. Safe by
  the `wave_issuances_one_live` argument, but it is a hand-run CLI that can run
  against live traffic.
- `CodexSheet` has **no production caller** — trait-pip deep links are unbuilt,
  and the implementer declined to invent a beat for it. There is still no
  notice surface.
- There is no `bodyFrom` picker: the tutorial splice sends parent A's species.
- Three of `ScreenFlow`'s six entry points are test-only.
- `Session`, `AuthStore` and `SnapshotStore` do synchronous file I/O on the
  main thread. Negligible today.
- **One line in the client remains genuinely unverified**: `BootController.cs:49`'s
  `root.RegisterCallback<GeometryChangedEvent>(...)`. Nothing drives
  `BootController.Start()` headlessly, and `FindOrCreateRuntimePanel`'s
  delegate type is inaccessible outside the UIElements module (confirmed by
  disassembly). Disclosed in the test file.

**Closed by Task 22**, both routed here from earlier reviews:

- `WaveRunner.Snapshot()`'s integrity wiring now has the race-free assertion
  described in §4, instance 9.
- `OutboxClientTests` leaked one `outbox-client-tests-*.bin` into the OS temp
  directory **per test**: `_tempPath` was created in `[SetUp]` and deleted in
  `[TearDown]`, but `NewClient()` built its `OutboxStore` on an independent
  freshly-`Guid`'d path, so the whole SetUp/TearDown pair was dead code.
  Nothing failed, because every assertion in that file reads the in-memory
  `Outbox` rather than the persisted bytes — which is exactly why it survived a
  review. `NewClient` is now an instance method routed through `_tempPath`.

---

## 12. Method: things this phase learned about its own testing

The transferable ones. Each cost real time.

**A component's tests say nothing about whether the app reaches it.** §5. Two
components shipped unwired; neither had a test that could have noticed, because
every test constructs the component itself.

**Verify the deliverable, through the deliverable's own channel.** §6. Three
distinct controller errors, one root cause.

**Construct the interleaving; do not reason about it.** §7. Two reproduced
deadlocks, two prior conclusions reached by reading code, both wrong.

**A test whose expected value is a type's default is not a test.** §4. Ten
instances — and a companion rule from Task 18's re-review: asserting a default
**is** legitimate when something in the same test makes it contrastive.

**Task ordering can be a correctness property, not a preference.** The plan
numbered the FTUE director 17 and the outbox 18, but every mutation the
director makes goes through `OutboxClient`, which is a Task 18 deliverable,
while Task 18 depends on nothing in 17. Running them in plan order would have
meant stubbing an interface that the next task then defines for real. Swapped,
with the implementer told to state the final public signatures prominently so
they could be handed over.

**A long, silent command will be killed by a stream watchdog, and the kill
looks like a model failure.** Task 18's first dispatch died after 600s with no
output and nothing written. The cause was `run-unity-tests.sh`, which blocks for
minutes and prints nothing. Every later dispatch was told to redirect it to a
log, background it, and poll with short commands. **Applies to the api suite
too**, which now runs for well over a minute.

**Unity substitutions have to be judged, not accepted.** Four distinct phantom
Unity APIs appeared in these briefs (`GetCallbackCount`, `SimulateSingleClick`
being internal, `SpliceDialog`'s internal setters, `Button.SendClick`), and in
each case the substitution the implementer chose had to be shown at least as
strong as what it replaced. `PickingMode.Ignore` in place of a callback-count
assertion is genuinely **stronger** — it forecloses even an accidental
copy-paste of `Standard`'s click registration into `Named`.

**A verbatim `AreEqual(model constant, view text)` test has a known ceiling,
and it is accepted rather than defective.** It cannot structurally distinguish
"reads from the model" from "still hardcoded but coincidentally identical". A
coincidental hardcode sits inert until the wording changes, and that desync is
exactly what a mutation run proved this shape does catch. Adjudicated twice
(Tasks 15 and 17), consistently. **Do not re-litigate it.**

### And one the fix round found by auditing itself: the repo cites its own scratch

Fix round 2 caught §1.1 citing `task-10-report.md` — which lives under
`.superpowers/sdd/`, **gitignored**, and which this file's own preamble says
will not survive a `git clean` — as the sole evidence for a Definition-of-Done
clause. Fixed by transcribing the evidence into §1.2.

**Auditing for others found it is not one instance, it is a house habit.**
Across the tracked tree at this commit: **38 citations of `task-N-brief.md` or
`task-N-report.md` in 22 files**, of which **19 cite a *report*** — a
measurement or a transcript — rather than a brief. They span Phases 5, 6 and 7.
Most are provenance ("the brief said X and was wrong"), where the claim stands
on its own and losing the pointer costs only context. **Some are not**, and
read exactly like the one just fixed:

- `services/api/test/weakenings.md:202` — *"task-11-report.md carries both runs
  verbatim."*
- `services/api/test/sweep.test.ts:316` — *"VERIFIED BOTH WAYS - task-11-report.md
  has the transcripts."*
- `services/api/test/adversarial.test.ts:607` — *"Measured, not assumed:
  task-11-report.md records the ..."*

In each, the durable file asserts a measurement and delegates the evidence to a
file that is one `git clean` from gone.

**Not fixed here**, and deliberately: it is nineteen transcripts across two
phases, in files this task has no mandate over, and the fix round it surfaced
in was scoped to documentation and comments. **Recorded as inherited** (§13),
with the distinction that matters for whoever takes it: *a pointer to context
is fine; a pointer that IS the evidence has to be transcribed.* The cheap first
pass is the grep that found it —
`git grep -nE "task-[0-9]+-(report|brief)\.md" -- . | grep -v superpowers`.

**The generalisation, which is §6 again in a third costume:** durable claims
must rest on durable evidence. §6 was about verifying through the deliverable's
own channel; this is about *citing* through it.

### A new one, from Task 22: running the Unity gates reintroduces the drift the settings gate exists to catch

Phase 6 recorded that `SENTIS_ANALYTICS_ENABLED` "came back: a package write,
then a `git add -A` in a commit about something else", and Task 13 recorded
reverting drift "the Editor reintroduced" before committing. **This session
watched the mechanism happen**, which is worth more than either note.

`verify-unity-settings.sh` was run **before** any Unity gate: green, 12 ok, 0
FAIL, including its own *"Scripting defines match across Standalone and
iPhone"* check. After `run-unity-tests.sh EditMode` and `PlayMode`,
`ProjectSettings.asset` read:

```
    Standalone: APP_UI_EDITOR_ONLY
    iPhone:     APP_UI_EDITOR_ONLY;SENTIS_ANALYTICS_ENABLED
```

— which is exactly the mismatch that guard exists for, and its own comment says
why it matters: *a define on one platform and not the other makes
`cross-runtime-diff.sh` compare two different compilations and report
agreement.* `PC_RPAsset.asset` and
`UniversalRenderPipelineGlobalSettings.asset` had been rewritten by the URP
package in the same runs, and two untracked Performance Testing JSON files had
appeared under `client/Assets/Resources/`.

**All of it was cleaned up — and only by luck of ordering.**
`cross-runtime-diff.sh` ran afterwards, and its `restore_known_churn` reverts
exactly those three paths on exit. **A session that runs the Unity suite and
not the cross-runtime gate leaves the drift sitting in the tree**, where the
next `git add -A` commits it. That is the route Phase 6 described; this is the
mechanism it travels.

> **The rule:** run `verify-unity-settings.sh` **after** the Unity gates, never
> only before. A green run taken before the thing that dirties the tree is a
> green run about a different tree. It was run twice here for that reason, and
> both runs are recorded in the baseline file.

This is §6's lesson in a second costume: **verify the artefact you are about to
ship, at the moment you are about to ship it.**

### And a second, from the same task: the build lock is a finite resource

`services/api/test/` now has **eight** files that build
`services/sim/Broodline.Sim.Service.csproj` for their own sim host, all
serialised through one cross-process lock because MSBuild shares
`services/sim/obj/` regardless of `-o`. `ftue.test.ts` is the eighth.

On the **first** full-suite run after it landed, **`loop.test.ts` failed at
exactly 60037ms with `timed out waiting for the dotnet build lock`** and its
three tests reported as *skipped*. The acquire timeout was 60s, a build
measures 2.5–12s here, and eight contenders no longer fit. The same run's
`contract.test.ts > frees port 5199 when interrupted` also failed, returning
exit 0 where 130 was expected — the shape of the script having finished before
the SIGINT landed, under load.

**The second run of the identical tree was fully green**, 451/451 across 43
files (that tree — the fix round later added three tests, and the baseline
carries the shipped count), which is what makes this a scheduling race rather
than a wall — and what
makes it worth writing down rather than fixing quietly, because a gate that
passes one run in two is not a gate.

**Raised to 180s on both sides** — `LOCK_ACQUIRE_TIMEOUT_MS` in
`wave-helpers.ts` and `BUILD_LOCK_TIMEOUT_TENTHS` in `generate-contract.sh`.
The third run, with the raise, is the one the baseline file records.

**The arithmetic, which the fix round supplied and the fix did not have.** Each
sim-hosting file takes the lock **once**, in its `beforeAll`, around the build
alone — so a waiter's worst case is `(N−1) × build`, not `N ×` anything
ongoing. At N=8 and a 12s build that is **84s**, which overruns 60s and is an
exact account of the 60037ms failure rather than a plausible one. 180s holds to
roughly N=15, so a ninth file does not reintroduce it. The comment saying "a
ninth should not raise this again" is therefore a **scope judgement** — past
this point the answer is one shared build, not a longer queue — and not a
necessity, and it now says which it is.

**Two justifications in that fix were wrong and are corrected**, which is the
same defect §7 criticises in `wave.ts:386-397`: prescribing correctly and
justifying badly, two files over.

- *"A path or timeout edit applied to only one side yields two locks, no
  exclusion"* runs two different failures together. It is **true of the path**
  — which cannot drift, because both sides derive it by hashing the repo root —
  and **false of the timeout**: two waiters with different give-up times still
  exclude correctly, they just fail at different moments, so a contended suite
  goes red in one place and green in another for no visible reason. A
  legibility failure, worth syncing for exactly that reason rather than by
  overstating it.
- *"It stays well under `LOCK_STALE_MS` (300s) so a genuinely abandoned lock is
  still reclaimed"* inverts the mechanism. A **dead** owner is reclaimed
  immediately on `ESRCH`, whatever this timeout is; `LOCK_STALE_MS` governs
  only the liveness-**unconfirmable** case, which a 180s waiter now gives up
  *before* reaching. The number is safe — for the opposite reason to the one
  written.

**And nothing pinned the two constants.** Two comments were the whole
mechanism, in a package that pins `SIM_PORTS` with a test for precisely this.
Closed in the fix round: `dotnet-build-lock.test.ts` now asserts all three
pairs agree, with the TS side exported and the **shell side parsed rather than
retyped**. Shown to discriminate — desyncing the timeout reddens one assertion
(`expected 60000 to be 180000`), and renaming the shell variable reddens the
same one through the parser's own throw rather than passing on a default.

**Honest about what this does and does not establish.** Two green runs after a
red one do not prove a race is closed; they prove it is not deterministic. The
raise removes the arithmetic reason eight contenders could exceed the bound —
it does not make the queue shorter.

> **The thing to notice is not the number, it is the shape.** Adding a test
> file made an *unrelated* test file fail, and the failure reported as
> `3 skipped` — which reads in a summary as a deliberate skip rather than as a
> suite that did not run. A ninth sim-hosting file will need this looked at
> again, and the right answer then is probably a shared build rather than a
> larger timeout.

---

## 13. What the next session inherits

0. **Run `verify-unity-settings.sh` after the Unity gates, not before.** §12.
   It is the cheapest item here and the easiest to get wrong — and the
   regenerate recipe in `phase7-test-baseline.txt` will redden its own last
   line if you run it in the order it lists, which that file now says above the
   recipe rather than leaving as a trap.
1. **The device capture.** §1, §3. It has blocked Phase 6's close since
   2026-09-15 and now blocks Phase 7's.
2. **A self-hosted macOS runner**, and with it the determinism gate — which has
   still, across four phases that each changed the engine, **never run**. §1.
3. **A deployed stack, `smoke-loop.sh` written, and the loop driven on it.**
   §1. Inherited unchanged from Phase 5's Task 11 and Phase 6's §12 item 2.
4. **A TestFlight build in someone else's hands**, and their report added to
   this file verbatim. §1.
5. **The eyes-on walk of beats 1–8** against a local `api` (Task 17 Step 6),
   and a PlayMode test for at least one director-to-view join. §5.
6. ~~**A drive whose supply line is unbroken.**~~ **Closed for the first hour**
   by `ftue.test.ts`. **Not closed for the wave-7 leg**, where `loop.test.ts`
   still seeds ten creatures and its third test still says so. §2.
7. **`BundleWaves` → `WaveDef.ForId` equality in `tools/config-validate`.**
   Nothing diffs the authored JSON against the engine's own wave definitions.
8. **The Keychain plugin**, before any external build.
9. **A scheduler trigger for the sweep.**
10. **`region_roster` §3's ratification of `DefileSix`'s six pocket
    positions**, which are provisional and placed by no document.
11. **A constructed test for one more lock pairing** (`commitSplice` vs
    `wave/start`'s `loadOwnedCreatures`, or vs `creature/name`), and a rewrite
    of `wave.ts:386-397`'s justification. §7.
12. **`ref readonly SimState`** — six times deferred.
13. **Nineteen load-bearing-ish citations from tracked files into gitignored
    scratch.** §12. A durable claim resting on a `task-N-report.md` that one
    `git clean` removes. Triage with
    `git grep -nE "task-[0-9]+-(report|brief)\.md" -- . | grep -v superpowers`,
    and transcribe the ones where the citation IS the evidence rather than
    context. §1.2 is the worked example.
14. **Waves 8–10 and the raiders they require**; Brood, Drift, Bulwark and
    Delver. Explicitly out of this phase's scope.
15. **`Diagnosis.PreWaveCheck` cannot clear wave 7** — it counts all six
    Skirmishers as simultaneous while Splash III caps at five. Resolved this
    phase by not shipping a caller: the threat board is dropped with it. **The
    two move together or neither moves**, because the board is the surface that
    would make the defect visible to a player.
