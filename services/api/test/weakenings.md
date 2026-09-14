# Weakenings — the evidence behind `adversarial.test.ts`

Design §7, Task 10 Step 3. **A suite whose tests have never been seen to fail
is a suite that has not been shown to test anything.** That is Phase 4's
recorded lesson twice over, and Phase 5 has since found *six* assertions that
were green while proving nothing — every one caught by weakening the guard and
watching the test stay green, never by reading the test.

So each row below was **actually applied to the real source**, the suite
actually run, and the failing test's name actually recorded. Nothing here is
predicted. Where a weakening left the suite green that is written down as a
finding, not smoothed over — three rows did, and three new tests exist because
of it.

Every run is `pnpm --filter @broodline/api test adversarial` against the file
as it ships (**15 tests**), on branch `phase_5`, with file parallelism on.
After each run the weakening was reverted (`git checkout`) and the suite
re-run to green.

---

## The table

| # | Weakened | Where | Result | Test(s) that went red |
|---|---|---|---|---|
| 1 | The seed comparison at submit step 5 deleted | `routes/wave.ts` `matchesIssuance` | **RED — discriminating (1/15)** | `cannot submit against a self-chosen seed` |
| 2 | `wave_issuances_one_live` dropped | `drizzle/0003_wave_issuances.sql` | **RED — not discriminating (12/15)** | named test red, but so is nearly the whole file — see below |
| 3 | `settle()` moved outside the credit's transaction and the credit un-gated on it | `routes/wave.ts` submit handler | **RED — discriminating (1/15)**, *after a new test was written* | `cannot replay a winning submission twice under a genuinely concurrent second attempt` |
| 4 | The settlement write-once trigger not created | `drizzle/0003_wave_issuances.sql` | **GREEN — 15/15. FINDING.** | none — see below |
| 4b | The abandoned row settled `'consumed'` rather than `'expired'` | `wave/issuance.ts` `issueWave` check 4 | **RED — discriminating (1/15)**, *after a new test was written* | `cannot spend a replay it never took by abandoning a wave` |
| 5 | ~~Read the reward from `verdict.echo.waveId`~~ | — | **STRUCK. NOT CLOSED.** | see "Row 5" below |
| 6 | Issuance check 2 (the replay cap) deleted | `wave/issuance.ts` `issueWave` | **RED (3/15)** | `cannot farm a cleared wave past the daily cap`, `counts a consumed issuance against the UTC day boundary, to the second`, `counts against midnight UTC even when the session TimeZone is not UTC` |
| 7 | `'consumed'` issuances aged out at 3 hours rather than the UTC day boundary | `wave/issuance.ts` check 2's count window | **RED (2/15)**, *after a new test was written, then rewritten* — **but the row is NOT closed: see the OWED ruling below** | `counts a consumed issuance against the UTC day boundary, to the second`, `counts against midnight UTC even when the session TimeZone is not UTC` |
| 8 | `sim`'s rejection returned as a `5xx` instead of a `200` verdict | `services/sim/Program.cs` | **RED — discriminating (1/15)** | `cannot submit forged bytes` |
| 9 | The trailing `AT TIME ZONE 'UTC'` dropped from check 2's day boundary | `wave/issuance.ts` check 2 | **RED — discriminating (1/15)** | `counts against midnight UTC even when the session TimeZone is not UTC` |

`services/sim/` is not `engine/`; row 8 touches the service host, and no row
here touches `engine/`.

---

## Row 2 — the index cannot be weakened in isolation

Dropping `wave_issuances_one_live` does redden the named test, but it reddens
**eleven of fifteen**, and for a reason that has nothing to do with replaying
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

Dropping the write-once trigger leaves all fifteen tests green, and **no
adversarial test can reach it**. `settle()` carries `AND settled_at IS NULL`
in its `WHERE`, so a second settlement of the same row matches nothing and the
`BEFORE UPDATE` trigger never fires. There is no sequence of HTTP requests that
causes a settlement *rewrite*, which is the only thing the trigger rejects — it
is defence in depth against a future writer that is not the request path, and
it belongs to a schema test rather than to a behavioural one.

It is not untested. Under this weakening the full suite reddens exactly one
test:

```
FAIL test/issuance-schema.test.ts > wave_issuances > refuses to rewrite settlement once set
  → promise resolved "Result{ command: 'UPDATE', …}" instead of rejecting
```

**No new test was written for this row**, deliberately: an adversarial test
that could only reach the trigger by reaching around the API into raw SQL
would be `issuance-schema.test.ts` with extra steps. Recorded as "the gate row
is mis-specified", not as "the guard is unproven".

## Row 5 — struck, and **not closed**

"Read the reward from `verdict.echo.waveId`" cannot break anything, and the
brief's three-layer argument was re-derived against the code rather than taken
on trust:

1. **Step 5 subsumes it.** `matchesIssuance` rejects an echo/issuance wave-id
   mismatch at `routes/wave.ts:223`, *before* the reward is computed at
   `:257` — both line numbers verified in the shipped file. By the time the
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
inflation is impossible. **Row 5 is not closed.** The real proof is **owed
against the engine content fill**, when a second authored wave with a
different reward exists — at which point the weakening becomes constructible
and must be run. Do not let this row be quietly dropped at Phase 6.

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

---

## Coverage note: design §4.4 has nine rows, the brief specifies eight tests

The row with no test in the brief's eight is **"Seed-shop for a favourable
run — Caught. One live issuance."** `cannot seed-shop for a favourable run`
was added for it. Its guard is row 2's index, which cannot be weakened in
isolation (above), so its discriminating gate remains
`wave-start.test.ts:203`, which drives `claimIssuance` directly against a real
conflict. Recorded so a reader does not have to notice the missing row for
themselves.

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
