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
as it ships (**14 tests**), on branch `phase_5`, with file parallelism on.
After each run the weakening was reverted (`git checkout`) and the suite
re-run to green.

---

## The table

| # | Weakened | Where | Result | Test(s) that went red |
|---|---|---|---|---|
| 1 | The seed comparison at submit step 5 deleted | `routes/wave.ts` `matchesIssuance` | **RED — discriminating (1/14)** | `cannot submit against a self-chosen seed` |
| 2 | `wave_issuances_one_live` dropped | `drizzle/0003_wave_issuances.sql` | **RED — not discriminating (11/14)** | named test red, but so is nearly the whole file — see below |
| 3 | `settle()` moved outside the credit's transaction and the credit un-gated on it | `routes/wave.ts` submit handler | **RED — discriminating (1/14)**, *after a new test was written* | `cannot replay a winning submission twice under a genuinely concurrent second attempt` |
| 4 | The settlement write-once trigger not created | `drizzle/0003_wave_issuances.sql` | **GREEN — 14/14. FINDING.** | none — see below |
| 4b | The abandoned row settled `'consumed'` rather than `'expired'` | `wave/issuance.ts` `issueWave` check 4 | **RED — discriminating (1/14)**, *after a new test was written* | `cannot spend a replay it never took by abandoning a wave` |
| 5 | ~~Read the reward from `verdict.echo.waveId`~~ | — | **STRUCK. NOT CLOSED.** | see "Row 5" below |
| 6 | Issuance check 2 (the replay cap) deleted | `wave/issuance.ts` `issueWave` | **RED (2/14)** | `cannot farm a cleared wave past the daily cap`, `still counts a consumed issuance from earlier today against the cap` |
| 7 | `'consumed'` issuances aged out at 3 hours rather than the UTC day boundary | `wave/issuance.ts` check 2's count window | **RED — discriminating (1/14)**, *after a new test was written* | `still counts a consumed issuance from earlier today against the cap` |
| 8 | `sim`'s rejection returned as a `5xx` instead of a `200` verdict | `services/sim/Program.cs` | **RED — discriminating (1/14)** | `cannot submit forged bytes` |

`services/sim/` is not `engine/`; row 8 touches the service host, and no row
here touches `engine/`.

---

## Row 2 — the index cannot be weakened in isolation

Dropping `wave_issuances_one_live` does redden the named test, but it reddens
**eleven of fourteen**, and for a reason that has nothing to do with replaying
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

Dropping the write-once trigger leaves all fourteen tests green, and **no
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

## The three tests that exist because a weakening stayed green

| Row | What stayed green | Test written |
|---|---|---|
| 3 | `cannot replay a winning submission twice` — the sequential double-submit finishes request one entirely before request two starts, so the guard could live anywhere in the handler | `cannot replay a winning submission twice under a genuinely concurrent second attempt` — an external transaction takes the row lock, so the handler's `settle()` blocks on a **real** Postgres lock instead of on timing that does not cooperate |
| 4b | `cannot farm a cleared wave past the daily cap` — it consumes every issuance it starts, so `wave/start`'s abandoned-wave path is never reached | `cannot spend a replay it never took by abandoning a wave` — back-dates `expires_at`, then takes all three replays and asserts the abandoned row settled `'expired'` |
| 7 | **The entire api suite: 137/137 green.** Every row the cap test creates is seconds old, so a count looking back only three hours satisfies it identically | `still counts a consumed issuance from earlier today against the cap` — back-dates `issued_at` to `greatest(day start, now − 3h)`, so the row is old enough for a three-hour window to drop it and still inside today |

Row 7 is the one the brief predicted, and it was the worst: **nothing anywhere
in the repository failed when the retention/day-boundary split stopped being
honoured.** Design §4.3 splits retention (`'expired'` rows out at three hours,
`'consumed'` rows kept 48 hours *because they are the replay counter*) and
until this test nothing read it.

The back-date is `greatest(date_trunc('day', now() AT TIME ZONE 'UTC'), now()
- interval '3 hours')` and never a blind three hours: before 03:00 UTC a blind
back-date lands in *yesterday*, where the row correctly stops counting and the
test would fail for a reason unrelated to its subject. When the day is younger
than three hours the row stays put and the test degrades to a weaker but still
correct assertion rather than a false alarm.

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
pnpm --filter @broodline/api test adversarial   # back to 14 passed
```

Do **not** add `--no-file-parallelism` or set `fileParallelism` to run these.
Each sim-hosting test file binds its own port (`5199` generate-contract.sh,
`5299` wave-submit, `5399` replays, `5499` adversarial) and shares the
`dotnet build` step through `wave-helpers.ts`'s `withDotnetBuildLock`
precisely so parallelism can stay on.
