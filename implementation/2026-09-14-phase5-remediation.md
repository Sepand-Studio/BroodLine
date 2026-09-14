# Phase 5 Remediation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close every open item Phase 5 leaves behind — the adversarial gate, all fifteen deferred minors, and the final whole-branch review — so the phase ends with nothing carried silently into Phase 6.

**Architecture:** Continues the Phase 5 branch (`phase_5`). No new services and no new deployables. Work is grouped so each task owns one surface — the sim service, the contract/test harness, the database schema, the submission handler, the fixtures — and can be reviewed and rejected independently.

**Tech Stack:** TypeScript (Hono, Drizzle, Postgres 16), C# (.NET minimal API, xUnit), bash, vitest.

## Global Constraints

- **Branch:** `phase_5`. Do not branch or merge; Task 12 of the parent plan owns the close-out.
- **Bundle:** `config/bundles/0.1.1` is the working bundle. `0.1.0` stays byte-identical to what is live in GCS — never edit it.
- **No GCP.** Nothing in this plan provisions, applies, or bills cloud infrastructure. Task 11 of the parent plan remains held by human ruling.
- **No engine content.** No task modifies `engine/`. One raider, one trait, wave 6.
- **Every guard gets a weakening.** Any test added here that protects a behaviour must be shown to fail when its guard is removed. A test that stays green under the weakening it names is a finding, not a pass.
- **Mutate the specific branch.** When proving a test covers a branch, mutate *that branch*, not the enclosing function. Phase 5 shipped six assertions that were green while proving nothing; five of them survived a wholesale mutation and died only under a targeted one.
- **Exact counts.** Every task reports the api test count and the .NET test count. Baseline: **142 api tests across 19 files, typecheck 0 errors, .NET 179 / 0 skipped** (was 127/18 when this plan was written; Task 1 added the gate). A count that *falls* is a deletion nobody noticed.

---

## Ordering and dependencies

Task 1 is the phase gate and must land first — it is what makes every other guarantee in Phase 5 real rather than asserted. Tasks 2–6 are independent of each other and may run in any order, but all must precede Task 7.

```
Task 1 (adversarial gate)  ──┐
Task 2 (sim service)         │
Task 3 (harness + contract)  ├──► Task 7 (final whole-branch review)
Task 4 (schema + input)      │
Task 5 (submission order)    │
Task 6 (fixtures + docs)   ──┘

Held by ruling, planned not dispatched:  parent Task 1 (device), 11 (GCP), 12 (close-out)
```

---

## Task 1: The adversarial suite — the phase gate ✅ COMPLETE

**Done — commits `c01f39a`..`297b67d`, review clean after two fix rounds.** All nine of design §4.4's rows covered (the brief said eight; the omitted "Seed-shop for a favourable run" row was a defect in the brief, found by the implementer). Eight weakenings applied to real source and recorded in `weakenings.md` with the test each one reddens.

Three weakenings left the brief's cases green, so three tests exist that otherwise would not — including one that confirmed a **real double-pay** (`[200, 200]`) when `settle()` moves outside the credit's transaction, which the parent plan had recorded as breaking no test.

**Left honestly open:** row 5 (end-to-end reward inflation) is owed against the engine content fill; row 7 (retention) is owed against Task 12, since the sweep does not exist yet — its test guards the UTC day boundary and only that.

**It found a product bug**, which is what a gate is for: see "Found since this plan was written" below.

<details><summary>Original task text</summary>

Already briefed and pre-flighted. The brief at `.superpowers/sdd/2026-09-13-phase5-validation/task-10-brief.md` is current and is the requirements; it reflects two plan corrections made after it was first generated (the `startWave` return type and the `BigInt(seed)` conversion) and the struck weakening row 5.

**Files:**
- Create: `services/api/test/adversarial.test.ts`
- Create: `services/api/test/weakenings.md`

**Interfaces:**
- Consumes: every route and guard from the parent plan's Tasks 4–9, plus a real `sim`.
- Produces: nothing importable. This task produces the phase's gate.

- [x] **Step 1:** Dispatch from the existing brief. Do not regenerate it — it carries corrections the plan text alone does not.
- [x] **Step 2:** Nine cases (see above); the eighth asserts the *hole* (unowned deployments still succeed) so Phase 6 inherits a failing-by-design marker.
- [x] **Step 3:** Each of the remaining weakenings applied, observed to break the suite, and recorded in `weakenings.md` with the failing test's name.
- [x] **Step 4:** Row 5 stays struck. The report must state plainly that the unit-level coverage proves the *wiring*, not the end-to-end property, and that the real proof is owed against the engine content fill.

</details>

---

## Found since this plan was written

Two items that are not among the original fifteen.

### 16. The replay cap's day boundary was wrong under any non-UTC session ✅ FIXED

`issuance.ts` compared the `timestamptz` column against `date_trunc('day', now() AT TIME ZONE 'UTC')`, which returns `timestamp` **without** time zone — so the comparison silently re-converted through the session's `TimeZone` GUC, restoring the exact dependence the line's own comment claimed to have removed. Verified against a real `postgres:16-alpine` under `America/New_York`: the boundary landed on midnight New York, 04:00 UTC. Players' replay caps would have reset four hours late.

Fixed in `297b67d` with the trailing conversion, guarded by a test that drives the **real** `issueWave` under `set_config('TimeZone', 'America/New_York', true)` — not a copy of its SQL, which could drift from what ships.

**The defect class is bounded and closed.** An audit for the same `timestamp`-vs-`timestamptz` shape found `AT TIME ZONE`/`date_trunc` at that one line and nowhere else in `services/api/src`, zero occurrences in `drizzle/`, and every expiry comparison done in JavaScript against `Date` objects on a `withTimezone` column — where node-postgres yields a correct absolute instant and there is no implicit cast to get wrong.

### 17. An intermittent file-level test failure 🔍 UNDER INVESTIGATION

~1 run in 8–10, a `beforeAll` failure rather than an assertion, moving between files (`issuance-schema`, `wave-start`, `isolation`, `ledger`). Attributed to testcontainers startup contention — but that attribution rests on a control that was **invalid**: it ran a commit that still contained both lock commits, so it established only "not caused by the most recent diff", not "unrelated to the lock".

Being re-investigated now with the correct control (`0de9f7c`, before any lock work) and with the actual error text captured — every report so far carried only the `FAIL` line, which makes the diagnosis a hypothesis.

This matters because `pnpm --filter @broodline/api test` is a Definition-of-Done command. A gate that goes red for unrelated reasons is one people learn to re-run.

---

## Task 2: The sim service tells the truth about malformed input ✅ COMPLETE

**Done — commit `be0bf9c`.** .NET 186 / 0 skipped (sim 5 → 12), api 166/23 held, `openapi/sim.json` byte-identical after regeneration (MD5 unchanged) with `SimulateRequest`/`SimulateResponse` both still present.

> **Three defects in the task text below, all caught by the implementer rather than forced.**
>
> 1. **`{}` does not fail binding.** `System.Text.Json` fills the record's constructor parameter with `null`, so a body missing `replay` reached the handler and was *already* 200. Step 5's "confirm both new tests go red" is therefore false — that test is green with the middleware deleted. Kept, relabelled in-code as a handler regression guard, and a case that genuinely fails binding (`replay` present but a number/object/array) added to cover the half this text intended.
> 2. **The prescribed `when (ctx.Request.Path == "/internal/simulate")` leaves a hole.** Routing matches `/internal/simulate/` to the same endpoint — verified, a good body there returns the 200 verdict — but `PathString` equality forgives case and *not* a trailing slash, so that spelling would still have returned a bare 400 from the same endpoint. Scoped on the matched endpoint's name metadata instead, sharing one const with `.WithName(...)`. Adds nothing to the OpenAPI document, which is why byte-identity held.
> 3. **The test file path is wrong.** `SimulateEndpointTests.cs` does not exist; the real file is `tests/sim/SimulateTests.cs`.
>
> **On the apphost (Steps 7–8): it reproduces as a failure, but not the recorded one, and was not root-caused.** The apphost exits **137 (SIGKILL) instantly with zero output**; `dotnet run` is gone in ~2 s. It does **not** hang — that reading was an artifact of the readiness loop having no liveness check. The recorded "apphost stub is the common thread" hypothesis is **disproved**: `tools/config-validate` uses the same 124712-byte apphost template (50 bytes differ) and both its apphost and `dotnet run` exit 0 here. Sandbox, invalid signature (`codesign -v` passes) and output buffering all ruled out by experiment. **The workaround stays**, and the comment now records the reproduction, the disproof, and what to check under CI. A duplicate false claim in `wave-submit.test.ts` was corrected.
>
> **Filed, deliberately not fixed inline:** the readiness loop in `generate-contract.sh` cannot distinguish a dead host from a slow one — the direct cause of the original misdiagnosis. It is a control-flow change to a five-times-hardened script and needs its own test.

<details><summary>Original task text</summary>

Two ledger items, both in `services/sim`.

**Files:**
- Modify: `services/sim/Program.cs`
- Test: `services/sim/…/SimulateEndpointTests.cs` (the existing xUnit file)

**Interfaces:**
- Consumes: `SimulateRequest`, `SimulateResponse` from `Contracts.cs` — unchanged.
- Produces: no signature change. The OpenAPI document must be byte-identical afterwards.

### The always-200 gap

A body that is invalid JSON, or omits `replay`, fails minimal-API model binding and returns ASP.NET's **400** — not the service's own 200-with-`replay_malformed` verdict. Unreachable today because `api` is the only caller, but the always-200 principle is load-bearing: `api`'s `SimVerdict` is a three-way union that fails closed on anything unexpected, and a bare 400 is exactly the "unexpected" it cannot interpret.

- [ ] **Step 1: Write the failing test**

Two cases: a body that is syntactically invalid JSON, and a well-formed body missing `replay`. Both must assert **200** and `rejected` / `replay_malformed`.

- [ ] **Step 2: Run it, confirm 400**

Expected: FAIL, received 400.

- [ ] **Step 3: Implement**

Do **not** change the handler's parameter to `HttpRequest` or `JsonElement` — that would erase `SimulateRequest` from the OpenAPI document and silently gut the drift gate, which is the same defect class as the `Results.Ok()` erasure Task 3 of the parent plan already fixed.

Minimal APIs raise `BadHttpRequestException` on a binding failure, *before* any endpoint filter runs. Catch it in middleware scoped to this path and return the service's own rejected verdict:

```csharp
app.Use(async (ctx, next) =>
{
    try { await next(); }
    catch (BadHttpRequestException) when (ctx.Request.Path == "/internal/simulate")
    {
        ctx.Response.StatusCode = 200;
        await ctx.Response.WriteAsJsonAsync(SimulateResponse.Rejected("replay_malformed"));
    }
});
```

Use the real constructor/factory `SimulateEndpoint` already uses for a `replay_malformed` rejection — do not hand-build the JSON, or the two shapes drift.

- [ ] **Step 4:** Tests pass. **Regenerate the contract and confirm `openapi/sim.json` is unchanged** — `git diff --quiet -- openapi/sim.json`. A diff here means the response type was erased.
- [ ] **Step 5: Weaken it.** Remove the `when` clause so the middleware swallows every path's binding failure; confirm a test notices. Then remove the middleware entirely; confirm both new tests go red.
- [ ] **Step 6: Commit**

### The never-root-caused apphost hang

`dotnet run --project` hung; the workaround is build-then-run-the-dll. Never root-caused.

- [ ] **Step 7:** Time-box a root-cause attempt to **30 minutes**. Try `dotnet run` directly and capture what happens.
- [ ] **Step 8:** If it reproduces, fix it and remove the workaround. If it does **not** reproduce, say so plainly — do not invent a cause. Replace the vague comment with the exact reproduction attempt, the environment it was tried on, and what to check when CI exists. A comment that records a failed reproduction is worth more than one that implies an unexamined mystery.

</details>

---

## Task 3: The test harness and the contract stop lying

Four ledger items. All are about the harness being the least-finished surface in the phase.

**Files:**
- Modify: `implementation/scripts/generate-contract.sh`
- Modify: `openapi/sim.json` (regenerated, not hand-edited)
- Modify: `services/api/test/wave-submit.test.ts`, `services/api/test/replays.test.ts` (signal handling)
- Modify: `services/api/package.json` (preflight)
- Modify: `services/api/README.md` (create if absent)
- Modify: `services/api/test/wave-submit.test.ts` (the `pool.ts` comment)

### 3a. The trap leaks port 5199 on Ctrl-C ⚠️ PREMISE WAS WRONG — see correction

> **CORRECTION (measured, after this plan was written).** The premise below is **false**, and the fix it prescribes would have been a **regression**. `trap cleanup EXIT` *already* fires on group SIGINT, on vitest's SIGTERM and on SIGHUP — bash runs the EXIT trap before re-raising. Measured with a control (removing only `kill "$SIM_PID"` leaves 5199 held under the same signal, proving `cleanup` was doing the reaping):
>
> | form | signal | rc | port 5199 |
> |---|---|---|---|
> | `trap cleanup EXIT` | SIGTERM → pid | 143 | free |
> | `trap cleanup EXIT` | SIGINT → group (Ctrl-C) | 130 | free |
> | `trap cleanup EXIT` | **SIGINT → pid alone** | **0, ran to completion** | free |
> | **prescribed `EXIT INT TERM`** | SIGTERM → pid | **1** | free |
>
> The prescribed form exits 1 and **resumes the script** after cleanup has already `rm -rf`'d `$WORK` — the log ends on a `curl >` into a deleted directory, presenting an interrupted run as an ordinary generator failure.
>
> The **genuine** hole is a SIGINT delivered to the script's pid alone, which bash swallows entirely. Shipped fix: explicit `on_signal` handlers that disarm, clean up, and exit 128+n. After: 130/143/130/129, port free, no stray `dotnet`.
>
> **Measurement trap, documented because it invalidated two probes:** a process started with `&` by a non-interactive shell inherits **SIGINT as ignored**, and that disposition survives `exec` **down a chain of shells**. A hand probe run that way reads as a false pass.
>
> **Narrower than first stated** (corrected after the implementer checked it rather than repeating it): this does *not* apply to a child spawned by node — libuv resets every signal to `SIG_DFL` before `exec`, verified directly. So a plain `spawn` from a vitest test needs no trampoline; only shell-launched probes do.

<details><summary>Original (incorrect) premise</summary>

`trap cleanup EXIT` covers a clean exit but not INT or TERM. Ctrl-C, or vitest's 300 s SIGTERM, leaks the `dotnet` host holding 5199 — producing exactly the misleading "address already in use" bind error the trap exists to prevent. This is the same class of defect as the lock's Ctrl-C hole that cost three fix rounds; it is the last one of its shape left.

</details>

- [ ] **Step 1:** Change to `trap cleanup EXIT INT TERM`. Verify the handler is idempotent — on INT, bash runs the trap *and then* EXIT, so `cleanup` must tolerate running twice. `release_build_lock` is already ownership-checked; confirm `kill "$SIM_PID"` and `rm -rf "$WORK"` are too.
- [ ] **Step 2: Prove it.** Start the script, send SIGINT mid-run, then assert nothing holds 5199 (`lsof -i :5199` empty) and no stray `dotnet` remains. Do the same for SIGTERM.
- [ ] **Step 3:** Give the vitest-side sim children the same treatment — a child spawned in `beforeAll` must be killed when vitest itself is signalled, not only on clean teardown.

### 3b. A loopback URL in a Cloud Run contract

`openapi/sim.json` carries `"servers": [{"url": "http://127.0.0.1:5199/"}]` — the local harness port, committed into the contract for a service that will run on Cloud Run. Any generated client that honours `servers` points at localhost.

- [ ] **Step 4:** Strip `servers` during Direction 2's normalisation, before the document is moved into place. The document is already key-sorted; keep it so.
- [ ] **Step 5:** Regenerate; confirm the only diff is the removed `servers` block, and that the contract gate still passes.
- [ ] **Step 6: Weaken it.** Re-add a `servers` block by hand and confirm the gate fails — the strip must be enforced, not merely performed once.

### 3c. The api package no longer runs on a Node-only machine

`pnpm --filter @broodline/api test` now needs a .NET SDK, a network-capable `dotnet tool restore`, and a free port 5199.

**This is not removable.** The contract drift gate and the wave-submit tests exercise a *real* sim; faking it would delete the guarantee. The honest fix is to make the failure legible instead of confusing.

- [ ] **Step 7:** Add a preflight that runs before the suite and fails with a plain message naming what is missing and how to install it — `dotnet` absent, `dotnet tool restore` unreachable, or 5199 occupied. Each gets its own message; a single generic one is not the fix.
- [ ] **Step 8:** Document the requirement in `services/api/README.md`, including which tests need it and why faking the sim is not on the table.
- [ ] **Step 9:** Prove each of the three preflight branches fires, by simulating each condition.

### 3d. The misnamed comment

- [ ] **Step 10:** `wave-submit.test.ts` cites "pool.ts's `lock_timeout=5000`"; the setting lives in `services/api/src/db/client.ts`. Correct it.
- [ ] **Step 11: Commit** all of Task 3 together.

---

## Task 4: Schema and input hardening

Two ledger items. Both need a migration or a parse change plus a test.

**Files:**
- Create: `services/api/drizzle/0004_trigger_scope_and_bounds.sql`
- Modify: `services/api/src/routes/wave.ts:21-26` (`parseStart`)
- Test: `services/api/test/wave-start.test.ts`, and the existing issuance schema test

### 4a. The write-once trigger fires on every UPDATE

`0003_wave_issuances.sql:89` declares `BEFORE UPDATE ON wave_issuances` with no column list, so it enters plpgsql on every update of any column.

- [ ] **Step 1:** New migration narrowing it to `BEFORE UPDATE OF settled_at, settlement ON wave_issuances`. `DROP TRIGGER IF EXISTS` first, matching `0003`'s own idiom.
- [ ] **Step 2:** The existing trigger tests must still pass **unchanged** — all four transitions including set-to-same-value. If narrowing the trigger changes any of those outcomes, the narrowing is wrong, not the test.
- [ ] **Step 3: Weaken it.** Confirm an UPDATE that rewrites `settlement` is still rejected, and that an UPDATE touching only an unrelated column now skips the trigger (assert on behaviour, not on a plan node).

### 4b. `parseStart` accepts an unbounded `waveId`

Line 24 checks `typeof` and `Number.isInteger` but no range. A caller can send `waveId: 2**53` and reach `issueWave`, which refuses it — but only after a progress lookup and a bundle scan.

- [ ] **Step 4: Write the failing test.** `waveId: Number.MAX_SAFE_INTEGER` and `waveId: -1` must both return `invalid_request` (400) from the parse layer, not `wave_locked` (409) from `issueWave`. The distinction is the point: 400 says "your request was malformed", 409 says "your request was understood and refused", and a client cannot tell them apart today.
- [ ] **Step 5:** Bound it. Upper bound belongs to the parse layer as a sanity limit, not a content decision — the authored-wave check in `issueWave` stays the authority on which waves exist. State the chosen bound and why in a comment.
- [ ] **Step 6:** Confirm the existing `waveId: 0` / `waveId: -1` behaviour in `issueWave` (`waveId < 1` → `wave_locked`) is unaffected for values that still reach it.
- [ ] **Step 7: Weaken it.** Remove the bound; confirm the new tests go red.
- [ ] **Step 8: Commit**

---

## Task 5: Submission ordering and the replay lifecycle

Two ledger items, both in `services/api/src/routes/wave.ts`, both requiring a design amendment recorded in `specs/plans/broodline_phase5_validation.md`.

**Files:**
- Modify: `services/api/src/routes/wave.ts`
- Modify: `specs/plans/broodline_phase5_validation.md` (§4, §5)
- Test: `services/api/test/wave-submit.test.ts`, `services/api/test/replays.test.ts`

### 5a. A dead issuance id costs a full re-simulation

Step 2 (load the live issuance) runs *after* step 3 (call `sim`). A fabricated or already-settled `issuanceId` therefore pays for a complete re-simulation before being refused. An abuse surface and a cost question, not a correctness one.

- [ ] **Step 1: Establish why the order is what it is** before changing it. Read the surrounding comments and the parent plan's Task 6 notes. If the current order is load-bearing for some invariant — for instance, if refusing early would leak whether an issuance id exists — **stop and report that**; do not reorder over it.
- [ ] **Step 2: Write the failing test.** A submission against a fabricated `issuanceId` must not reach `sim`. Assert on a **sim call counter**, not on latency — a timing assertion is a flake, and this suite already has one flake too many.
- [ ] **Step 3:** Reorder so liveness is checked first. Preserve the existing behaviour exactly for the *live* path; only the dead path changes.
- [ ] **Step 4:** Confirm the refusal code and message for a dead issuance are **unchanged** from today. A client must not be able to distinguish the reorder.
- [ ] **Step 5: Weaken it.** Restore the original order; confirm the new test goes red.

### 5b. A `wave_locked` refusal is a verified, settled Win that writes no replay

Reachable when the bundle is republished during an issuance's 2 h TTL: `sim` verifies the replay, `settle` and `advanceCampaign` commit, the player is told 409, and **no replay is stored**. The design does not address it, so this needs a ruling before a fix.

**Recommended ruling — implement this unless overruled:** the replay follows *verification*, not the response status. Design §5's principle is that every spent, verified attempt is reconstructible; a refusal the player cannot appeal is precisely when the stored replay matters most. Write the replay whenever `sim` returned a verdict and the issuance was settled, regardless of what the response says.

- [ ] **Step 6:** Record the ruling in design §5 as an amendment, with the reachability condition stated.
- [ ] **Step 7: Write the failing test.** Force the republish-during-TTL condition, assert 409 *and* that the replay object exists.
- [ ] **Step 8:** Implement. The replay write must not be able to fail the request — a storage error on a path that is already refusing must not turn a 409 into a 500.
- [ ] **Step 9: Weaken it.** Move the write back under the success path; confirm the new test goes red.
- [ ] **Step 10: Commit**

---

## Task 6: Fixtures, recorded baselines, and stale documentation

Five ledger items, all cheap. Batched because each is a one-line change that still deserves one review.

**Files:**
- Modify: `services/api/test/fixtures/*/manifest.json` (the two Task 7 fixtures)
- Modify: `services/api/src/replays/gcs-store.ts` (class doc)
- Create: `implementation/results/phase5-test-baseline.txt`
- Regenerate: `.superpowers/sdd/2026-09-13-phase5-validation/task-4-brief.md`

- [ ] **Step 1:** The two bundle fixtures hardcode `minimumClientVersion "0.1.0"`. Derive it from the same constant the real manifests use, or state in a comment why a fixture pins it deliberately. Cosmetic today because `validateBundle` only takes a directory — the note matters if manifest checks ever validate that field against retired versions.
- [ ] **Step 2:** `GcsReplayStore`'s class doc still says it "only ever writes and lists, never deletes". `list()` was removed from that class by the parent plan's Task 8 fix. Correct it.
- [ ] **Step 3:** Record the test baseline as **data, not prose**. Phase 4 recorded "71" in a sentence and it went stale unnoticed; the real count was 74. Write the current counts to `implementation/results/phase5-test-baseline.txt` in a form Task 12 can diff mechanically, and have Task 12 compare against the file rather than a number someone typed into a paragraph.
- [ ] **Step 4:** Regenerate `task-4-brief.md`, whose Produces line still names `consumedAt` from before the `settled_at`/`settlement` amendment. Note in the ledger that this file is git-ignored scratch and will be deleted at phase close — it is regenerated for consistency while the workspace is live, not preserved.
- [ ] **Step 5: Commit**

---

## Task 7: Final whole-branch review

- [ ] **Step 1:** Dispatch the whole-branch reviewer on the **most capable available model**, over the full `phase_5` range.
- [ ] **Step 2:** Hand it the deferred-minor list from the ledger explicitly, so it can confirm each is closed or consciously ruled.
- [ ] **Step 3:** Hand it `weakenings.md` and ask it to check that each recorded weakening names a test that plausibly *could* fail that way — a weakenings file nobody audits is the same failure mode as a green test that proves nothing.
- [ ] **Step 4:** One fix dispatch, one scoped re-review, adjudicate residuals into the ledger.
- [ ] **Step 5:** Delete this plan's SDD workspace once clean.

---

## Held by ruling — planned, not dispatched

### Parent Task 1: device round-trip re-capture

**Yours.** Needs a physical iPhone and a human tap. Re-capture the device replay artifacts under engine `0.2.0` so the round-trip tests compare `Outcome.Hash` instead of asserting a throw, and `dotnet test` reports 0 skipped.

Command and expected artifacts are in the parent plan's Task 1. Nothing here blocks on it except that one Definition-of-Done clause.

### Parent Task 11: Terraform, the sim service, the replay bucket

**Held — billable GCP.** Plan stands as written in the parent document. One item from this plan folds into it:

- The `terraform output` bug (reports "No outputs found" despite `outputs.tf` defining `api_url`; the tracked tfstate carries an empty outputs map) must be fixed **as the first step** of Task 11, not discovered mid-apply. The likely cause is outputs added after the last apply, so the state predates them — but that is a hypothesis, and it must be confirmed against the real state before anything is applied.

### Parent Task 12: close-out

**Held — depends on 11.** Runs the six Definition-of-Done commands, promotes the ledger to a tracked followups file, updates design §9's decisions owed, and adds the phase to `implementation/README.md`. Task 6 Step 3 above changes its baseline check from a prose number to a diffable file.

---

## What this plan deliberately does not do

- **No engine content.** The real end-to-end proof of weakening 5 needs a second authored wave with a different reward. That is Phase 6's content fill, booked as owed, not smuggled in here.
- **No creature ownership check.** Design §2.2's knowingly-open hole. Task 1's eighth case marks it with a test that flips when Phase 6 closes it.
- **No cloud spend.** See above.
- **No refresh-token rotation, no raids, no nodes.** Unchanged from the parent plan's exclusions.

---

## Definition of done

```bash
dotnet test Broodline.sln --nologo
./implementation/scripts/cross-runtime-diff.sh
pnpm --filter @broodline/api test
pnpm --filter @broodline/api typecheck
./implementation/scripts/generate-contract.sh && \
  git diff --quiet -- openapi/ client/Assets/Generated/ services/api/src/generated/
./implementation/scripts/run-unity-tests.sh EditMode
```

All six green, with:

- **All fifteen ledger minors closed or explicitly ruled**, each with its resolution recorded — no silent carries.
- **Task 1's seven live weakenings observed to break the suite**, each named in `weakenings.md` with its failing test; row 5 struck with its reasoning inline and booked as owed.
- **`openapi/sim.json` free of a `servers` block**, and the strip enforced by the gate rather than performed once.
- **SIGINT and SIGTERM proven not to leak port 5199**, by signal, not by inspection.
- **A malformed body to `sim` answered with 200 and `replay_malformed`**, with the OpenAPI document byte-identical.
- **A dead issuance refused without reaching `sim`**, asserted on a call counter.
- **A `wave_locked` refusal writing its replay**, asserted on the stored object.
- **The test baseline recorded as a diffable file**, not a number in a sentence.

Two clauses stay open by ruling and must be named as such at close: the **device round-trip** (needs parent Task 1) and the **deployed end-to-end wave** (needs parent Task 11).
