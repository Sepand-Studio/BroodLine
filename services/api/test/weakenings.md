# Weakenings — the evidence behind `adversarial.test.ts`

Design §7, Task 10 Step 3. **A suite whose tests have never been seen to fail
is a suite that has not been shown to test anything.** That is Phase 4's
recorded lesson twice over, and Phase 5's ledger reached **ten or eleven**
assertions that were green while proving nothing — caught by weakening the
guard and watching the test stay green, never by reading the test. One was
caught in this very file's own newest test (row 2 below); one at remediation
Task 3, where a test passed on its first run having exercised nothing at all,
because the unexecutable stub it was meant to exercise was skipped by the PATH
search rather than run.

**Ten or eleven, not a single number, and the ambiguity is deliberate.** The
final pair were one house pattern found together in two distinct tests, so the
count depends on where you draw that boundary. An earlier draft of this
paragraph said *nine*, which was simply an undercount nobody had reconstructed
— and in a file whose whole subject is claims that assert more than they prove,
an unverifiable statistic is the wrong thing to carry. The full enumeration is
in `implementation/2026-09-13-phase5-followups.md`; take the list over the
number.

So each row below was **actually applied to the real source**, the suite
actually run, and the failing test's name actually recorded. Nothing here is
predicted. **Row 5 was the one standing exception to that sentence for the
whole of Phase 5** — it was struck rather than run, because there was no
second authored wave to run it against — and Task 11 is what closes that
gap, in two stages: a single-edit run that stayed masked, and (fix round 1,
re-reading Phase 5's own register rather than stopping at the first result)
the COMBINED weakening Phase 5 actually named, which does not stay masked.
See "Row 5" below for both runs.

Where a weakening left the suite green that is written down as a finding,
not smoothed over — **six rows did**, one of them (row 5) only in its
single-edit form. Row 5's COMBINED form — the one Phase 5's own register
names — is discriminating instead, and belongs with rows 1, 3, 6, 7, 8 and 9,
which reddened a named test. Three of the six that stayed green produced a
new test (rows 3, 4b and 7); three did not, and each says why in its own
section: row 4 is a guard no
sequence of HTTP requests can reach, row 5's SINGLE EDIT is a guard of the
same shape (today — see "Row 5" for why that is not the same claim as "the
row is safe"), and row 10 is a genuine hole in this gate that a controller
ruling deliberately leaves open, because closing it here would put a
non-adversarial property in the adversarial suite.

Every run of the original ten rows (1 through 10, including 4b) is
`pnpm --filter @broodline/api test adversarial` against the file as it
shipped THEN (**15 tests**), on branch `phase_5`, with file parallelism on.
Row 5's two Task 11 entries run against a later `phase_6` file instead
(**18 tests** as of fix round 1) and say so precisely in their own section
below, not here — this sentence describes the original ten and nothing
after them, so it is not updated to chase a count it was never claiming.
After each run the weakening was reverted (`git checkout`) and the suite
re-run to green.

---

## The table

| # | Weakened | Where | Result | Test(s) that went red |
|---|---|---|---|---|
| 1 | The seed comparison at submit step 5 deleted | `routes/wave.ts` `matchesIssuance` | **RED — discriminating (1/15)** | `cannot submit against a self-chosen seed` |
| 2 | `wave_issuances_one_live` dropped | `drizzle/0003_wave_issuances.sql` | **RED — not discriminating (12/15)** | named test red, but so is nearly the whole file — see below |
| 3 | `settle()` moved outside the credit's transaction and the credit un-gated on it | `routes/wave.ts` submit handler | **RED — discriminating (1/15)**, *after a new test was written* | `cannot replay a winning submission twice under a genuinely concurrent second attempt` |
| 4 | The settlement write-once trigger not created | `drizzle/0004_trigger_scope_and_bounds.sql` — **not `0003`**, see below | **GREEN — 15/15. FINDING.** | none in this file — see below |
| 4b | The abandoned row settled `'consumed'` rather than `'expired'` | `wave/issuance.ts` `issueWave` check 4 | **RED — discriminating (1/15)**, *after a new test was written* | `cannot spend a replay it never took by abandoning a wave` |
| 5 | Read the reward from `verdict.echo.waveId` ALONE, `matchesIssuance` left intact | `routes/wave.ts` submit step 6, `rewardForWave`'s argument | **GREEN — 17/17 (`adversarial`), 49/49 (`wave-submit adversarial replays`). NOT the row's closing result — masked because `matchesIssuance` still stands; see 5b, and "Row 5" below for why nothing, including the synthetic-bundle unit test, discriminates this in isolation.** | none |
| 5b | THE COMBINED WEAKENING, AND THE ROW'S CLOSING RESULT: 5, AND `matchesIssuance`'s `waveId` comparison deleted (seed comparison kept) | `routes/wave.ts` `matchesIssuance` + submit step 6 | **RED — discriminating in `adversarial` (1/18); ALSO reddens a second, unrelated test (1/7 in `sim-client`)** | `cannot claim wave 7's reward against a wave 6 issuance`; separately, `sim-client.test.ts`'s `matchesIssuance > refuses a genuine mismatch regardless of which branch the type took` |
| 6 | Issuance check 2 (the replay cap) deleted | `wave/issuance.ts` `issueWave` | **RED (3/15)** | `cannot farm a cleared wave past the daily cap`, `counts a consumed issuance against the UTC day boundary, to the second`, `counts against midnight UTC even when the session TimeZone is not UTC` |
| 7 | `'consumed'` issuances aged out at 3 hours rather than the UTC day boundary | `wave/issuance.ts` check 2's count window | **RED (2/15)**, *after a new test was written, then rewritten* — **but the row is NOT closed: see the OWED ruling below** | `counts a consumed issuance against the UTC day boundary, to the second`, `counts against midnight UTC even when the session TimeZone is not UTC` |
| 8 | `sim`'s rejection returned as a `5xx` instead of a `200` verdict | `services/sim/Program.cs` | **RED — discriminating (1/15)** | `cannot submit forged bytes` |
| 9 | The trailing `AT TIME ZONE 'UTC'` dropped from check 2's day boundary | `wave/issuance.ts` check 2 | **RED — discriminating (1/15)** | `counts against midnight UTC even when the session TimeZone is not UTC` |
| 10 | Submit's step-2 liveness check made to refuse directly, instead of only gating the `sim` call | `routes/wave.ts` submit handler | **GREEN — 15/15. FINDING.** | none in this file — see below |

`services/sim/` is not `engine/`; row 8 touches the service host, and no row
here touches `engine/`.

---

## Row 2 — the index cannot be weakened in isolation

Dropping `wave_issuances_one_live` does redden the named test, but it reddens
**twelve of fifteen** (the table row above is the measured figure; the
"eleven" this sentence used to carry was taken before the fifteenth test
landed and was never updated), and for a reason that has nothing to do with
replaying
a submission. `claimIssuance` infers its `ON CONFLICT (server_id, player_id)
WHERE settled_at IS NULL ... DO NOTHING` against exactly that index, so with
the index gone Postgres raises

```
42P10: there is no unique or exclusion constraint matching the ON CONFLICT specification
```

on **every** `wave/start`, which surfaces as a 500 and takes every test that
starts a wave with it. (Observed directly in the run's stderr, not inferred.)

So the index is proven load-bearing — for *liveness*. It is **not** the guard
`cannot replay a winning submission twice` depends on; that is `settle()`'s
return value gating `credit()`, which row 3 is the real weakening for. Two
narrower mutations were considered and rejected: making the index non-unique,
or widening its key, both of which break `ON CONFLICT` inference the same way
and produce the same wholesale failure.

**A vacuity this run caught in its own newest test.** Under this weakening an
earlier draft of `cannot seed-shop for a favourable run` *passed*: both
`wave/start` calls 500, both bodies are error envelopes, both `issuanceId`s
read `undefined`, and `undefined === undefined` is a green assertion. The test
now asserts `status === 200` and the field types before comparing, and the
re-run moved it from the passing column to the failing one (10 → 11 red). Left
alone it would have been the seventh assertion this phase that was green while
proving nothing — in the file written to stop that happening.

## Row 4 — GREEN, and the row is mis-specified rather than the guard missing

> **RE-MEASURED at the final-review fix round, and the `Where` moved.** This
> row was first recorded against `drizzle/0003_wave_issuances.sql`, and
> **applying it there today weakens nothing at all**: remediation Task 4 added
> `drizzle/0004_trigger_scope_and_bounds.sql`, which `DROP`s the trigger and
> re-creates it narrowed to `BEFORE UPDATE OF settled_at, settlement`. 0004
> runs after 0003, so commenting 0003's `CREATE TRIGGER` out leaves the guard
> fully in place — measured: **176/176 green, `issuance-schema.test.ts` 9/9**.
> Anyone reproducing this row from the old `Where` would get a green suite and
> conclude the row was a false record. **0004 is the live definition**; to
> weaken the trigger you must remove *its* `CREATE TRIGGER`.

Dropping the write-once trigger leaves all fifteen adversarial tests green,
and **no adversarial test can reach it**. `settle()` carries `AND settled_at
IS NULL` in its `WHERE`, so a second settlement of the same row matches
nothing and the `BEFORE UPDATE OF settled_at, settlement` trigger never fires.
There is no sequence of HTTP requests that causes a settlement *rewrite*,
which is the only thing the trigger rejects — it is defence in depth against a
future writer that is not the request path, and it belongs to a schema test
rather than to a behavioural one.

It is not untested, and **the row's substance is stronger than first
recorded**, not weaker. The original note said the full suite "reddens exactly
one test". Measured again with the trigger created nowhere (both `CREATE
TRIGGER` statements removed), the full suite reports

```
Test Files  1 failed | 22 passed (23)
     Tests  1 failed | 172 passed | 3 skipped (176)

FAIL test/issuance-schema.test.ts > wave_issuances > refuses to rewrite settlement once set
  → promise resolved "Result{ command: 'UPDATE', …(9) }" instead of rejecting

FAIL test/issuance-schema.test.ts > wave_issuances > the write-once trigger, narrowed to the settlement columns
```

— one failing assertion **plus a whole `describe` block aborted, taking 3
tests with it as skipped**. That block is remediation Task 4's
`the write-once trigger, narrowed to the settlement columns`, which did not
exist when this row was first measured.

**The abort is a cascade, and the cascade is the interesting part.** The block
does not fail for want of a trigger; it fails in its own `beforeAll` with

```
error: duplicate key value violates unique constraint "wave_issuances_one_live"
  ❱ test/issuance-schema.test.ts:143  await issue(t.db, 6, SETTLED)
```

because the assertion that failed just above it *succeeded at the database*.
`UPDATE wave_issuances SET settled_at = NULL, settlement = NULL` is exactly
what the trigger exists to refuse; with the trigger gone it commits, which
puts issuance `…005` **back into the one-live partial index**, and the next
block's attempt to issue a fresh wave for that player then collides. So the
weakening does not merely redden an assertion — it reverts a settled issuance
to live and corrupts the table underneath everything after it. That is a
stronger demonstration of what the guard is for than "reddens exactly one
test" conveyed.

`adversarial.test.ts` stayed **15/15** throughout, so the row's actual finding
— that this gate cannot see the guard — is unchanged.

**No new test was written for this row**, deliberately: an adversarial test
that could only reach the trigger by reaching around the API into raw SQL
would be `issuance-schema.test.ts` with extra steps. Recorded as "the gate row
is mis-specified", not as "the guard is unproven".

## Row 5 — struck through Phase 5, **run in two stages at Task 11**

> **CLOSED AT TASK 11 FIX ROUND 1, Phase 6 — and the first stage's "closed"
> was premature.** Struck for the whole of Phase 5 because `WaveDef.ForId`
> authored exactly one wave, so there was no second reward to inflate toward.
> The three-point argument immediately below is preserved exactly as Phase 5
> wrote it — re-derivation, not memory, is what this file runs on.
>
> **Point 2 of that argument is the one that matters, and the first pass
> through this row under-weighted it.** Phase 5's own decision register
> calls out **the combined weakening** by name as the one that "needs two
> authored waves with different rewards" — not the single-line reward-source
> edit in isolation. Task 11's first pass ran only the single edit, found it
> masked, and called the row closed. **That was the wrong half of the
> argument to treat as the whole row.** Fix round 1 ran the combined
> weakening Phase 5 actually named — `matchesIssuance`'s `waveId` comparison
> deleted AS WELL AS the reward source — and it is **not** masked:
> **RED. `expected 200 to be 409`, balance inflated by exactly 230.** See
> "Task 11, fix round 1" below for the attack, the run, and the new
> permanent test; "Task 11, single edit" for the first pass's result, kept
> because it is also true and says something the combined run does not.
> task-11-report.md carries both runs verbatim.

"Read the reward from `verdict.echo.waveId`" cannot break anything, and the
brief's three-layer argument was re-derived against the code rather than taken
on trust:

1. **Step 5 subsumes it.** `matchesIssuance` rejects an echo/issuance wave-id
   mismatch at its call site in submit step 5 — `routes/wave.ts:372` — *before*
   `rewardForWave` is reached in step 6, at `:417`. **The function names are
   the anchors; the line numbers are not.** The `:223`/`:257` this sentence
   used to cite rotted when Task 5 reordered the handler, and a reader who
   checked them landed in the middle of the enumeration-oracle comment.
   `:372`/`:417` were re-read out of the shipped file at the final-review fix
   round. By the time the
   lookup runs `echo.waveId === issuance.waveId` is guaranteed, so reading
   either source is behaviourally identical.
2. **A combined weakening needs a second authored wave** with a *different*
   reward. Remove step 5 as well and wave B's reward could be paid against a
   wave A issuance — but only if two such waves exist.
3. **The engine authors exactly one.** `WaveDef.ForId` returns wave 6 and
   throws `WaveCompositionException` for every other id (verified at
   `engine/Runtime/Combat/WaveDef.cs:62-66`), which `SimulateEndpoint.Handle`
   maps to `rejected: rules_violated`. A wave-7 replay is refused by `sim`
   before the handler ever sees it. **A test-only bundle fixture does not
   help** — the gate is the engine, not the bundle, and no task in this phase
   modifies `engine/`.

**What was done instead.** `adversarial.test.ts`'s second `describe` block,
`the reward's source of truth (design §2.2)`, drives `rewardForWave` against a
synthetic two-wave bundle (6 → 40 shards, 7 → 9,999) and asserts the amount is
a function of the wave id it is handed, plus that an unauthored id returns
`null` rather than defaulting.

**That proves the wiring, not the property.** It shows the handler's choice of
source is a choice with consequences; it does **not** prove an end-to-end
inflation is impossible. ~~Row 5 is not closed. The real proof is owed
against the engine content fill, when a second authored wave with a
different reward exists — at which point the weakening becomes constructible
and must be run. Do not let this row be quietly dropped at Phase 6.~~ **Task
11 is that content fill; see below for what running it found.**

### Task 11, single edit — measured, not argued: masked, but not the whole row

Phase 6 authored wave 7 (`engine/Runtime/Combat/WaveDef.cs Wave7()`: 1 Lash,
6 Skirmishers, integrity 3; `config/bundles/0.1.2/waves.json`: 230 shards
against wave 6's 40). That makes points 2 and 3 above checkable rather than
hypothetical for the first time, so the weakening was applied to real source
— `routes/wave.ts`'s `rewardForWave(bundle, issuance.waveId)` changed to
`rewardForWave(bundle, toInt(verdict.echo.waveId))`, confirmed present with
`git diff` before trusting any result — and run, rather than argued about a
second time.

**`adversarial.test.ts`'s new test, `pays the ISSUED wave reward, never the
submitted one`, drives an HONEST wave-7 win end to end**: a real roster
minted to a deliberately-built wave-7-winning composition (Taunt/Vetch and
Splash/Ember at tier III per `config/bundles/0.1.2/traits.json`'s own
pairings, plus two Hollow for range/margin — verified against the real
engine directly, at `/internal/simulate`, before being written into the
test; see the test file's own comment for the four-seed, zero-breach
measurement), issued through `wave/start`, submitted, and the balance
asserted — not the status code alone, per this file's own house rule.

**Measured result: GREEN.**

```
pnpm --filter @broodline/api test adversarial
 ✓ test/adversarial.test.ts (17 tests) 6199ms
 Test Files  1 passed (1)
      Tests  17 passed (17)

pnpm --filter @broodline/api test wave-submit adversarial replays
 ✓ test/adversarial.test.ts (17 tests)
 ✓ test/wave-submit.test.ts (25 tests)
 ✓ test/replays.test.ts (7 tests)
 Test Files  3 passed (3)
      Tests  49 passed (49)
```

Reverted with `git checkout -- services/api/src/routes/wave.ts`, confirmed
clean with `git diff --quiet`, and the full suite re-run to **383/383**
green on the restored source.

**Why it stays green, confirmed rather than assumed.** Point 1 above is not
merely still true, it is now checked against a case where it MATTERS: submit
step 5 (`routes/wave.ts:598` today), `matchesIssuance`, refuses the whole
request whenever `toInt(echo.waveId) !== issuance.waveId`, and step 6's
`rewardForWave` call (`:663`) is only ever reached afterward. For an honest
wave-7 submission `echo.waveId` and `issuance.waveId` are both 7 regardless
of which the weakened line reads, so this specific test cannot discriminate
the mutation — nothing can, over HTTP, because no request reaches line 663
with the two fields disagreeing. **That is a stronger property than "no test
happened to catch it"**: the substitution is unreachable by construction,
not merely unexercised, and an honest end-to-end test is what makes that
checkable rather than assumed. **CORRECTED AT FIX ROUND 2 — nothing
discriminates the single edit, and that includes `adversarial.test.ts`'s
pre-existing `describe("the reward's source of truth (design §2.2)")`
block.** An earlier draft of this paragraph called that block "the row's
only discriminating gate," which is false, and false in a way a test run
could never catch: the block calls `rewardForWave(twoWaves, 6)` and
`rewardForWave(twoWaves, 7)` with its OWN LITERAL ids, never through
`routes/wave.ts`'s call site at all, so it cannot observe — by
construction, not merely in practice — which of `issuance.waveId` or
`verdict.echo.waveId` that call site passes. It is unfalsifiable against
this weakening: green whichever argument the handler reads, because it
never reads the handler's argument. It proves `rewardForWave` uses whatever
id it is given; it says nothing about which id the call site chooses to
give it, which is the entire content of row 5. (Fix round 1, next, finds a
genuine discriminating gate — but only for the COMBINED weakening; it does
not catch the single edit alone, because `matchesIssuance`'s waveId half is
still standing in that scenario and refuses the request before the reward
line runs, exactly as this section found.)

**This result stands, and it is real — it is just not the row.** The
single-edit weakening leaves this file green and says why, which is worth
recording precisely the way rows 4 and 10 are: `matchesIssuance`'s waveId
half is redundant, TODAY, with the reward line's own argument, so cutting
only the reward line changes nothing observable. What it does not show is
what happens if that redundancy is ever the ONLY protection left — which is
exactly the question fix round 1 asked next.

### Task 11, fix round 1 — the combined weakening, and this is the one Phase 5 meant

**The attack Phase 5's point 2 describes, built for real.** A player who has
cleared NOTHING issues wave 6 (reward 40) with a roster that happens to equal
a deliberately-built wave-7-winning composition (Taunt/Vetch and Splash/Ember
at tier III, two Hollow — the same one `pays the ISSUED wave reward...`
verified against the real engine at four seeds). They then submit a
GENUINELY WINNING WAVE-7 REPLAY at that issuance's own real seed, with the
SAME five creatures. Every field matches between the issuance and the echo
except one: 6 was issued, 7 was simulated.

**Both halves of the combined weakening, applied to real source together,
confirmed present with `git diff` before anything was trusted:**

```diff
 export function matchesIssuance(echo: SimulateEcho, issuance: Pick<Issuance, 'seed' | 'waveId'>): boolean {
-  return echo.seed === issuance.seed && toInt(echo.waveId) === issuance.waveId
+  return echo.seed === issuance.seed
 }
```
```diff
-          const reward = rewardForWave(bundle, issuance.waveId)
+          const reward = rewardForWave(bundle, toInt(verdict.echo.waveId))
```

**Measured result: RED — the attack succeeds.**

```
PROBE result {"status":200,"body":{"result":"Win","integrityRemaining":3,
"breaches":[],"reward":{"currency":"shards","amount":230}},
"before":250,"after":480}
```

`before + 230 = after`, exactly. A player who never played wave 7 at all —
who was ISSUED wave 6 — was paid wave 7's reward, in full, end to end,
through the real handler. **This is the hole weakenings.md row 5 was always
about**, and it is now observed rather than argued.

**A second, unrelated test also reddens — and it is worth naming precisely
why that is not the same finding.** `sim-client.test.ts`'s `matchesIssuance
> refuses a genuine mismatch regardless of which branch the type took`
directly unit-tests `matchesIssuance(<waveId: 7>, <issuance waveId: 6>)`
and expects `false`; deleting the waveId comparison makes it `true`, so that
test fails independent of anything about money. **That test proves
`matchesIssuance` still obeys ITS OWN contract. It does not prove the
reward is safe** — it is testing a different function's correctness, not
the property row 5 is about, and a regression shaped differently enough to
satisfy that specific assertion (or a future refactor that quietly drops
the test as "redundant with the seed check") would sail through it while
still paying the wrong reward. The end-to-end test below is what closes
that gap; `matchesIssuance`'s own unit coverage is a good, real, but
INDEPENDENT protection, not a substitute.

**The permanent test:** `cannot claim wave 7's reward against a wave 6
issuance` (`adversarial.test.ts`). On real source it asserts the refusal
that actually happens — `409`, `submission_rejected` (from
`matchesIssuance`'s waveId half specifically; the seed matches, so that is
the only guard left that can fire) — and the balance unchanged. Run against
the combined weakening:

```
pnpm --filter @broodline/api test sim-client wave-submit adversarial replays
 × adversarial: what a modified client cannot do > cannot claim wave 7's
   reward against a wave 6 issuance (weakenings.md row 5, THE COMBINED WEAKENING)
   → expected 200 to be 409
 × matchesIssuance > refuses a genuine mismatch regardless of which branch
   the type took
   → expected true to be false
 Test Files  2 failed | 2 passed (4)
      Tests  2 failed | 55 passed (57)
```

**Reverted (`git checkout -- services/api/src/routes/wave.ts`), confirmed
clean (`git diff --quiet`), and re-run to green:**

```
pnpm --filter @broodline/api test sim-client wave-submit adversarial replays
 Test Files  4 passed (4)
      Tests  57 passed (57)

pnpm --filter @broodline/api test          # full suite
 Test Files  34 passed (34)
      Tests  384 passed (384)
```

**Closed, and this time by a discriminating gate, not by an unreachability
argument.** Row 5 moves to the same bucket as rows 1, 3, 6, 7, 8 and 9: a
named test goes red under the weakening this row describes. The single-edit
result above stays on the record too, because it answers a real and
different question — "does the reward line's own argument matter, given
`matchesIssuance` today" — and the answer to that one is still no, today,
by itself. The combined result answers the question that was actually
asked: **is the reward a function of the issuance and never of the
submission, PROVABLY, rather than merely as an accident of where two
independent guards currently happen to sit.** Full detail, both verbatim
runs, and the reachability argument are in task-11-report.md.

---

## Row 9 — a weakening that found a REAL PRODUCT BUG, not a test gap

Row 9 is not from the brief. It exists because rewriting row 7's gate to pin
the UTC day boundary exposed a live defect in **Task 5's** shipped code — this
is the case where the gate did the thing gates are for.

`issuance.ts` check 2 compared the `timestamptz` `issued_at` column against

```sql
date_trunc('day', now() AT TIME ZONE 'UTC')        -- timestamp WITHOUT time zone
```

That expression's type is `timestamp without time zone`, so the comparison
**silently re-converts it through the session's `TimeZone` GUC** — putting back
the exact dependence the inner `AT TIME ZONE 'UTC'` was written to remove, and
which the line's own comment claimed to have removed. Verified against a real
`postgres:16-alpine` under `SET TimeZone='America/New_York'`:

| Expression | Value |
|---|---|
| shipped | `2026-09-14 00:00:00` (a bare timestamp, read as midnight **New York** = 04:00 UTC) |
| fixed | `2026-09-13 20:00:00-04` (= midnight **UTC**) |

**Player impact:** the replay cap would reset on the session's local midnight
rather than the UTC one — four hours late in that zone. It was correct in
production only because the GUC defaults to UTC on both `postgres:16-alpine`
and Cloud SQL, which is precisely the default the comment said it had stopped
relying on. No existing test could see it, because every test runs on that
default.

**Fix:** append the trailing `AT TIME ZONE 'UTC'`, converting back to
`timestamptz` so both sides are absolute instants.

**Guard:** `counts against midnight UTC even when the session TimeZone is not
UTC` drives the **real** `issueWave` — not a copy of its SQL, which could drift
from the shipping query — inside a transaction that has done
`set_config('TimeZone', 'America/New_York', true)`. It places all three
consumed rows exactly on midnight UTC (which the fixed query counts and the
broken one does not, at every hour of the day, since the broken boundary is
always displaced by the zone's offset) and then re-places them one second
earlier as a wrong-reason check, so a query that counted nothing — or
everything — under a non-UTC session could not pass.

Dropping the trailing conversion reddens exactly that test, 1/15, with
`expected { serverId: 1, … } to deeply equal { refused: 'replay_cap_reached' }`
— the cap failing to bind because the boundary moved.

---

## Row 10 — GREEN, and it is a REAL HOLE IN THIS GATE, deliberately left open

Row 10 is not from any brief. It comes from the Phase 5 remediation task that
reordered submit's step 2 ahead of step 3 (`routes/wave.ts`; design §4.2's
amendment). The reorder's first, obvious shape refused straight from the
step-2 read:

```ts
if (!issuanceIsLive) return fail('issuance_invalid', refusalMessage('issuance_invalid'))
```

**That breaks design §4.2's guard one** — the idempotency key protects the
*response*. A client retrying across a network failure resends the **same**
key, and by then the issuance it was paid for is settled, so it reads "dead"
exactly like a fabricated id. The refusal is taken before `withIdempotency` is
ever reached, and the retrying client is answered **409 for a wave it was in
fact paid for**: "pays exactly once under retry" — this phase's central claim,
and this file's own subject — failing in the direction the player notices,
since they see a refusal and conclude they were not paid.

**This suite does not notice. 15/15 green.** Run directly against the
weakening, not predicted.

**Why it cannot see it.** Every double-submit test here uses a **different**
idempotency key, and deliberately so — `cannot replay a winning submission
twice` says it outright: "§6.3's key is client-supplied and a modified client
simply mints a new one, so the idempotency layer is not the guard under test
here. The issuance is." The same-key path is therefore never exercised in this
file at all, and guard one has **no adversarial coverage**.

**What does catch it**, 2/12 in its own file:

```
FAIL test/wave-submit.test.ts > returns the stored response on a resend with the SAME key
  → expected 409 to be 200
FAIL test/wave-submit.test.ts > refuses a dead issuance without paying for a
     re-simulation, indistinguishably from before
  → expected 409 to be 200
```

The first is Task 6's; the second was written by the remediation task
specifically so the property is pinned by a test that sits *next to* the
reorder a future change would be editing.

### CONTROLLER RULING — the hole stays open, and that is a decision

**Do not close this row by adding a same-key test to `adversarial.test.ts`.**
The two replay shapes are different properties and belong in different files:

| | |
|---|---|
| **Different-key** replay | The **attack**. A modified client mints a new key; idempotency cannot save you, and the issuance settlement must. This file's subject. |
| **Same-key** replay | The **honest retry**. A correctness property about not lying to a client that did nothing wrong. `wave-submit.test.ts`'s subject. |

The property *is* covered, in the file where it belongs, by two tests that are
shown red above. Duplicating it here would paper over the gate hole rather than
record it, and would put a non-adversarial property in the adversarial suite.

**Recorded, not fixed, on purpose** — the same disposition as row 4, and for
the same reason: the gate row is mis-specified, not the guard unproven. A later
reader who notices this file has no same-key coverage should read this section
before "fixing" it.

---

## The three tests that exist because a weakening stayed green

| Row | What stayed green | Test written |
|---|---|---|
| 3 | `cannot replay a winning submission twice` — the sequential double-submit finishes request one entirely before request two starts, so the guard could live anywhere in the handler | `cannot replay a winning submission twice under a genuinely concurrent second attempt` — an external transaction takes the row lock, so the handler's `settle()` blocks on a **real** Postgres lock instead of on timing that does not cooperate |
| 4b | `cannot farm a cleared wave past the daily cap` — it consumes every issuance it starts, so `wave/start`'s abandoned-wave path is never reached | `cannot spend a replay it never took by abandoning a wave` — back-dates `expires_at`, then takes all three replays and asserts the abandoned row settled `'expired'` |
| 7 | **The entire api suite: 137/137 green.** Every row the cap test creates is seconds old, so a count looking back only three hours satisfies it identically | `counts a consumed issuance against the UTC day boundary, to the second` — pins the BOUNDARY: one consumed row exactly at `date_trunc('day', now() AT TIME ZONE 'UTC')` that must count, one a second earlier that must not |

### The row-3 test had to be stopped from becoming the test it replaced

Round 1 review, and the same vacuity shape again. The poll loop that waits for
the handler's backend to block on the row lock fell out of its deadline branch
**without asserting a waiter was ever seen**. If the sync point were missed — a
`sim` call slower than the 3s deadline, or a future handler that settles
earlier — `T_ext` would commit before the handler's transaction opened,
`loadLiveIssuance` would return `undefined`, and the test would observe `409 +
issuance_invalid` and **pass anyway**, having silently collapsed back into the
sequential case it exists because that case proves too little.

Demonstrated rather than argued, by forcing the deadline path (deadline set to
`Date.now() + 0`) under otherwise identical conditions:

| Loop | Forced deadline path |
|---|---|
| As first written (fall-through, no assertion) | **GREEN 14/14** — the vacuity |
| As shipped (`expect(sawWaiter).toBe(true)`) | **RED** — `expected false to be true` |

The same review also found the test **leaked an open transaction on failure**:
every assertion sits between that connection's `BEGIN` and `COMMIT`, `t.pool`
is the *app* pool the handlers themselves draw from, and `pg-pool` issues no
`ROLLBACK` of its own on release. A throwing assertion hands back a connection
still inside a transaction holding a row lock on `wave_issuances`. Now
`await client.query('ROLLBACK').catch(() => {})` precedes `release()`.

**The blast radius, corrected to what was measured.** This document first
claimed the leak would cause "a cascade of 60s timeouts". Round 2's reviewer
tried to produce that cascade and **could not**: with the `ROLLBACK` deleted
and the deadline forced, the run was **1 failed / 13 passed in 4.8s**.
`createPool` sets `max: 5` and `lock_timeout=5000`, and later tests use fresh
players and different rows, so the actual cost is two consumed pool slots and
an idle-in-transaction backend for the rest of the file. The fix is kept
because it is correct and free — but the claim is now the observed one.
Overstating a hazard in this file costs exactly as much credibility as
understating one, and credibility is the only thing this file has.

Row 7 is the one the brief predicted, and it was the worst: **nothing anywhere
in the repository failed when design §4.3's retention/day-boundary split
stopped being honoured.**

### The first fix pinned a distance, and that was not enough

Round 1 review caught the replacement test being **inert for three hours a
day, and blind to the likelier regression.** It back-dated to
`greatest(day_start, now() - 3h)`, which only discriminates against windows
*narrower than the distance it happened to travel*. Two demonstrations, both
re-run here:

| Mutation | Against the first fix | Against the shipped test |
|---|---|---|
| Day boundary → **rolling 24-hour window** (`now() - interval '24 hours'`) | **GREEN, full suite 141/141** | **RED** (`expected 429 to be 200`) |
| 3-hour window, at a **simulated 00:30 UTC** run | **GREEN 14/14** | **RED** (`expected 429 to be 200`) |

The rolling-24h case is a real, player-visible behaviour change — "three per
UTC day, resets at midnight" silently becoming "three per rolling 24h", so a
player who takes three replays at 23:50 UTC is locked out until 23:50 the
*next* day — and nothing in the repository noticed. The file's own comment
described this as degrading to "a weaker but still-correct assertion", which
turned a gate hole into a non-issue; that framing is gone.

**The shipped test pins the boundary, not a distance.** One consumed row
exactly at `date_trunc('day', now() AT TIME ZONE 'UTC')` must count; one row
**one second earlier** must not. That pair is independent of both the time of
day and the width of any replacement window: a rolling window of any width
that reaches back past midnight fails the second half, and a window too narrow
to reach midnight fails the first. The boundary is captured once, as epoch
milliseconds, so both halves pin the same instant.

### CONTROLLER RULING — row 7 is OWED against Task 12, not covered

**Do not read the improved test as closing row 7.** It guards the **UTC day
boundary the replay cap counts against**, to the second, and that is all it
guards.

**Design §4.3's 3h/48h retention split still has no test and is owed.** The
retention sweep does not exist yet — `0003_wave_issuances.sql:95` records that
Task 12 owns it — so there is no code to weaken: `'expired'` rows aged out one
hour past `expires_at`, and `'consumed'` rows retained 48 hours *because they
are the replay counter*, are both unguarded. Row 7's weakening was therefore
applied to the nearest shipped equivalent, check 2's count **window**, which
is the split's consumer rather than the split itself. When the sweep lands it
needs its own direct test, including the 48-vs-24 hour argument (a row written
at 23:50 must still be countable at 00:10, which a 24-hour sweep deletes while
it is still needed).

**Booked as owed against Task 12.**

### UPDATE (Task 11) — the sweep now exists, and row 7 is CLOSED

The ruling above is left unedited for the record; the sweep it describes as
absent now exists (`services/api/src/wave/sweep.ts`, `services/api/src/sweep-cli.ts`,
`services/api/test/sweep.test.ts`), so the "there is no code to weaken" premise
no longer holds. This section records what actually closes it, run the same
way as every other row in this file: applied to the real source, the suite
actually run, the failing test's name actually recorded.

**Two tests were added for the RED/GREEN cycle**, both actually run RED
before the fix and GREEN after:

1. `sweep.test.ts`'s *"a wave_locked refusal no longer strands creatures
   committed to an expired issuance"* — the OTHER claimant Task 11 closes,
   not a retention test: `issueWave`'s checks 1-3 used to be able to refuse
   a start without ever reaching the settle-expired branch that used to
   live only at check 4, so a bundle rollback un-authoring a player's wave
   left their deployment `committed_to` a row nothing would ever settle.
   **RED**, against `issueWave` reverted to its pre-Task-11 shape
   (`git stash push -- services/api/src/wave/issuance.ts`, `sweep.ts`
   otherwise unchanged):
   ```
   AssertionError: expected false to be true
     at test/sweep.test.ts:173 — (await myRoster()).every(c => c.committedTo === null)
   ```
   (five creatures stayed committed after the 409, exactly the strand this
   task exists to close). **GREEN** after restoring the fix.
2. `sweep.test.ts`'s *"retention: expired rows go one hour past expires_at;
   consumed rows go 48h past issued_at"* — design §4.3's split, against
   `sweepRetention` directly. **RED** simply because `sweep.ts` did not
   exist before this task (`Cannot find module '../src/wave/sweep.ts'`).
   **GREEN**, asserting `{ expiredDeleted: 2, consumedDeleted: 1 }` over
   four rows (a keepable consumed row, a >48h consumed row, a >1h-past-expiry
   expired row, and a live-past-expiry row that must be SETTLED before it is
   deleted or its creatures never release).

Full suite after: `pnpm --filter @broodline/api test` → **Test Files 42
passed (42) / Tests 445 passed (445)**. No other file moved — for once, a
brief prediction about test movement (this branch is at six wrong so far)
was not tested here because none was made; this task is the first to leave
every pre-existing file exactly as green as it started.

**The weakening run, exactly as this row's own standard demands — and the
brief's specific prediction about it was wrong.** Changing
`CONSUMED_RETENTION_MS` in `sweep.ts` from `172_800_000` (48h) to
`86_400_000` (24h) and re-running the two tests above **verbatim** stays
**GREEN, 2/2**. The brief's claim was that this reddens "the 23:50/00:10
assertion" (test 2's `replayCapCountFor` check); it does not, because that
pair is only **twenty minutes** apart (`2026-09-14T23:50:00Z` to
`2026-09-15T00:10:00Z`) — nowhere near either a 24h or a 48h threshold, so
both windows keep the row and `replayCapCountFor` returns 1 either way. This
is the row 7 pattern repeating on itself: a boundary pair chosen for one
purpose (here, illustrating "issued right before a day boundary") does not
automatically discriminate an unrelated numeric threshold, the same lesson
"The first fix pinned a distance, and that was not enough" already drew
above.

A pair that DOES discriminate needs a row strictly between the two windows.
`sweep.test.ts`'s third test — *"a consumed row strictly between 24h and 48h
old survives the 48h window"*, a single consumed row issued 30 hours before
`now` — is that gate, and it is a real, permanently shipped test rather than
a discarded probe, on the same reasoning row 5's COMBINED weakening was kept
over its single-edit predecessor: a row 7 fix that stops at "the brief's
literal pair happens to stay green" repeats exactly the defect this row
exists to record. Both runs below are against the identical fixture and
`now`, nothing else changed:

| `CONSUMED_RETENTION_MS` | 30h-old consumed row | 23:50/00:10 pair (test 2) |
|---|---|---|
| `172_800_000` (48h, shipped) | **kept** — `{ expiredDeleted: 0, consumedDeleted: 0 }` | green |
| `86_400_000` (24h, weakened) | **deleted** — `{ expiredDeleted: 0, consumedDeleted: 1 }`, test **RED** | green (does not discriminate) |

The 24h value was reverted immediately after each run; `sweep.ts` ships at
`172_800_000`, confirmed by re-running the full file GREEN 3/3 afterward.

**Booked as CLOSED.** Design §4.3's retention split has a direct,
discriminating test on both halves (the day-boundary-adjacent pair and the
strictly-between-windows pair), and the stranding bug the sweep also fixes
has its own RED/GREEN pair above it. Full detail — including the
lock-ordering question Task 11 raised while moving the settle call ahead of
`issueWave`'s check 1 — is in `task-11-report.md`.

---

## Coverage note: design §4.4 has nine rows, the brief specifies eight tests

The row with no test in the brief's eight is **"Seed-shop for a favourable
run — Caught. One live issuance."** `cannot seed-shop for a favourable run`
was added for it. Its guard is row 2's index, which cannot be weakened in
isolation (above), so its discriminating gate remains
`wave-start.test.ts`'s **`a conflicting insert on wave_issuances_one_live
resolves with the existing row rather than aborting the transaction`** — at
`:207` today, but the test NAME is the anchor; the `:203` this sentence used
to cite rotted as that file grew. It drives `claimIssuance` directly against a
real conflict. Recorded so a reader does not have to notice the missing row for
themselves.

---

## Phase 6 — the deployment comparison and the supply floor

Design §6.2 and §2.4, Phase 6 Task 10. Same discipline as the table above and
the same method: each row was **applied to the real source** at this task’s
own shipped state, the suite actually run, the failing test names actually recorded,
then reverted with `git checkout -- services/api/src` and `git diff --quiet`
confirmed before the next row. Nothing here is predicted.

Runs are `pnpm --filter @broodline/api test wave-submit adversarial replays`
(**48 tests**) unless the row says otherwise — the comparison is exercised from
all three files, and a row scoped to `adversarial` alone would miss two thirds
of its own evidence.

| # | Weakened | Where | Result | Test(s) that went red |
|---|---|---|---|---|
| P6-1 | The whole `deploymentMatches` call deleted from the submit handler | `routes/wave.ts` submit step 5 | **RED (7/48)** | every deployment test and nothing else: the three mismatch cases, the null-column case, `cannot submit a replay claiming a deployment it was not issued`, `grants NOTHING on a submission the deployment comparison refuses`, `writes nothing for a submission the deployment comparison refuses` |
| P6-2 | Compared as a **multiset** — the same seven fields, both sides sorted, order ignored | `routes/wave.ts` `deploymentMatches` | **RED — discriminating (1/48)** | `rejects a submission that deploys the SAME creatures in a different order` |
| P6-3 | The length check dropped, leaving `stored.every(...)` alone | `routes/wave.ts` `deploymentMatches` | **RED — discriminating (1/48)** | `rejects a submission that deploys MORE creatures than it was issued` |
| P6-4 | A null stored deployment SKIPS the check (`return true`) | `routes/wave.ts` `deploymentMatches` | **RED — discriminating (1/48)** | `rejects a submission against an issuance minted before the column had a writer` |
| P6-5 | `toTier(null)` returns `0` instead of `null` | `sim/client.ts` | **RED (26/380, full suite)** | every honest submission in all three files — see below |
| P6-6 | The grant multiplied by the Harvest Array tier | `wave/base-stock.ts` | **RED — discriminating (1/48)** | `does not scale wave base stock with the Harvest Array` |
| P6-7 | `grantWaveBaseStock` hoisted out of the Win branch to the verification point, so it runs on every verified submission | `routes/wave.ts` submit handler | **RED — discriminating (1/48)** | `grants NOTHING on a losing submission` |
| P6-8 | The Hatchery cap made a REFUSAL instead of a skip | `wave/base-stock.ts` | **RED — discriminating (1/48)** | `skips the grant at the Hatchery cap and still pays the reward` |
| P6-9 | `deploymentMatches` returns `false` unconditionally — the **vacuity control** | `routes/wave.ts` | **RED (26/48)** | every honest-path test in all three files; no deployment-mismatch test moved |

### P6-3 and P6-4 are the two that were named in advance

Both were carried forward from Task 9's report as hazards, and both are the
shape where a wrong answer passes silently rather than failing:

- **P6-3 is the vacuity hazard.** Written as a loop over the stored
  deployment, the comparison runs **zero iterations** against an empty one and
  agrees with every echo there is. `claimIssuance`'s `deployment:
  CreatureSpec[] = []` default made that state reachable from production code;
  the default is gone as of this task, and the length check is what makes its
  return harmless rather than fatal. Note what P6-3 did *not* redden: the two
  field-level mismatch tests stay green under it, because their deployments are
  the same length. A suite without the MORE-creatures case would have shipped
  this.
- **P6-4 is the nullable-column ruling.** `wave_issuances.deployment` is
  nullable because `drizzle/0006` is the expand step, so for up to
  `ISSUANCE_TTL_MS` after this handler deploys a player can hold a live
  issuance the previous build minted with no deployment. "Skip the check when
  null" reopens the hole for two hours, for everyone — and it reddens exactly
  one test, which is to say that without that one test the entire repository
  would have been green while the boundary was open. The ruling is to REFUSE:
  an honest player mid-deploy loses one attempt, which is a real cost and is
  the smaller one.

### P6-5 is the case for `toTier`, and it is not a style point

`toInt` does not accept `null`, and the generated `tier1`/`tier2` are
`null | number | string` — so this **failed at compile** rather than silently,
which is the good direction. The three one-keystroke "fixes" (`v ?? 0`,
`Number(v)`, `toInt(v as number)`) are all the same wrong answer: they turn
"no coverage" into "tier zero". `drizzle/0005_loop.sql`'s
`coverage_tier_N_not_zero` makes `0` unstorable on the api side, so an echoed
`0` equals nothing api holds and **every honest submission carrying an empty
combat slot is refused**. That is what the 26 red tests are: not a subtle
regression, a route that has stopped working — which is why the row is
recorded as non-discriminating rather than as a good gate.

### P6-9 is the control the rest of the table needs

Eight of the nine rows above are refusals going missing. A comparison that
refused EVERYTHING would pass all eight of those tests perfectly, so the table
would be evidence of nothing without a row that moves in the other direction.
P6-9 reddens 26 tests and moves none of the mismatch cases — the two halves are
independent, and the suite pins both.

### Fix round — the two guards review added

`DEPLOYMENT_FLOOR` and `commitCreatures`' full WHERE, applied to the fix-round commit's own shipped state.
Runs are `pnpm --filter @broodline/api test wave-start wave-submit adversarial
replays` (**80 tests**) — `wave-start` joins because both guards live on the
issuance side.

| # | Weakened | Where | Result | Test(s) that went red |
|---|---|---|---|---|
| P6-10 | The floor removed — an empty deployment issuable again | `routes/wave.ts` `parseDeployment` | **RED — discriminating (1/80)** | `refuses an EMPTY deployment, as malformed rather than as a wave fought with nothing` |
| P6-11 | The floor raised to `DEPLOYMENT_CAP` — a required size of five rather than a minimum of one | `routes/wave.ts` `parseDeployment` | **RED (11/80)** | the floor test's own positive control, plus nine roster tests and `rejects a submission that deploys MORE creatures than it was issued` |
| P6-12 | The row-count assertion deleted **and** the update narrowed to the first id | `wave/issuance.ts` `commitCreatures` | **RED (5/80)** | the direct test plus all four `committed_to` route tests |
| P6-13 | The row-count assertion deleted, predicates intact | `wave/issuance.ts` `commitCreatures` | **RED — discriminating (1/80)** | `commitCreatures refuses a row that is not this player's, live and uncommitted` |
| P6-14 | The `player_id` predicate dropped | `wave/issuance.ts` `commitCreatures` | **RED — discriminating (1/80)** | same |
| P6-15 | The `committed_to IS NULL` predicate dropped | `wave/issuance.ts` `commitCreatures` | **RED — discriminating (1/80)** | same |

**P6-11 is the floor's positive control**, and it is the row that stops the
floor from being "refuse anything shorter than a full deployment". A check that
demanded five would pass P6-10's test perfectly; it reddens eleven, including
the `expect((await start(6, deploymentOf(mine.slice(0, 1)))).status).toBe(200)`
inside the floor test itself. One creature is a legal, if doomed, deployment —
how a wave *goes* is the engine's business.

**P6-13, P6-14 and P6-15 were GREEN at first, and that is recorded rather than
quietly fixed.** Run against the fix commit BEFORE its direct test existed,
each of the three left the suite **79/79 green**. That is not an
accident of coverage, it is structural: `resolveDeployment` refuses an unowned,
dead or committed creature long before `commitCreatures` runs, so **no HTTP
request can present that statement with a row it should decline.** Which is
precisely the argument that made the loose `WHERE` "correct today" — and it
cannot also be the reason not to test it.

So `commitCreatures` is exported and driven **directly, without the FOR UPDATE
in front of it** (`wave-start.test.ts`'s `commitCreatures refuses a row that is
not this player's, live and uncommitted`), which is the only place the
predicates can be shown to do anything. The same precedent `claimIssuance` set
one test up: when a guard is unreachable through the route, drive the function.
With that test in place all three rows became discriminating at 1/80. The
sequence — green, recorded, test written, red — is the point; a reader should
be able to see that these three predicates had no coverage at all until
something went looking.

---

## Reproducing any row

Apply the change named in the table, then:

```bash
pnpm --filter @broodline/api test adversarial   # record the failing test name
git checkout -- services/api/src services/api/drizzle services/sim
pnpm --filter @broodline/api test adversarial   # back to 15 passed
```

Do **not** add `--no-file-parallelism` or set `fileParallelism` to run these.
Each sim-hosting test file binds its own port (`5199` generate-contract.sh,
`5299` wave-submit, `5399` replays, `5499` adversarial) and shares the
`dotnet build` step through `wave-helpers.ts`'s `withDotnetBuildLock`
precisely so parallelism can stay on.
