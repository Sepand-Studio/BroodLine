# Phase 6 — What Execution Left Behind

*The durable record of the loop phase: what it built, what it proved, what it
left owed, and which claims are believed rather than demonstrated*

> **Why this file exists.** The working ledger that recorded these findings
> lives in git-ignored scratch and will not survive a `git clean`. This is the
> durable copy, and for most of these items it is the only one. It is written
> to the same contract as `2026-09-13-phase5-followups.md`, which it should be
> read beside.
>
> **State at the time of writing:** Tasks 0–13 complete on `phase_6`, **49
> commits** from the branch point at `944d412`. All six gates run green at
> `832bfb6` except the one that is deliberately red (§1); `dotnet test` therefore
> exits 1 by design, and anything treating that as a broken gate must special-case
> the test by name rather than make it green. Counts are **not**
> quoted in this prose — they live in
> `implementation/results/phase6-test-baseline.txt`, which supersedes any number
> written into a sentence anywhere, including this one.
>
> **This phase found a large number of defects, and several of them were in the
> plan and the design rather than in the implementations.** A handful were in
> already-merged code from Phases 4 and 5. A record that read as uniformly
> successful would be false, so section 8 enumerates them rather than
> summarising them away.

---

## 1. The one thing that is deliberately red, and blocks the phase

`dotnet test Broodline.sln` reports **exactly one failure**, and it is meant to:

```
Broodline.Sim.Tests.Combat.ReplayArtifactPresenceTests.TheTrackedCapturesAreCurrent
```

Task 1 moved `SimVersion` from `0.2.0` to `0.3.0` to widen the trait and raider
enums. Under `solo_execution` §9.4 that supersedes the tracked device and editor
captures in `implementation/results/`, so the renderer round-trip is **not being
proven right now**. No physical iOS device was available to re-capture.

**A human ruled on 2026-09-14 that execution continue with the test FAILING
rather than skipping.** The rule the plan states — never proceed on a dark proof
— guards against a *skip*, which is indistinguishable from success in every
count anybody reads. A *failure* is visible, names itself, and blocks the
Definition of Done on its own. So Task 1 Step 9 is **parked, not skipped**, and
`dotnet.skipped` is 0 and must stay 0: there is no `Skip` anywhere in the
solution, so no count will ever report this for you.

**Phase 6 cannot close until the device capture happens.** That is the plain
statement and it is not softened anywhere else in this file.

To discharge it: play `client/Assets/Scenes/Wave.unity` in the Editor and on
device, then commit the four files in `implementation/results/`. **On device aim
for a tap at ticks 184–207** — the overlap of two independent requirements
(creature 0 holds a target during 184–240, and the Courser must still be below
the first defender, which ends at 207). A tap outside that window can round-trip
perfectly while proving nothing. Expect roughly ten ticks of reaction lag, so
aim near 190. **In the Editor it is looser**: Rally lasts 120 ticks, so any tap
whose window overlaps 184–240 is valid.

> The window `184..207` is itself a Phase 6 correction. The Phase 5 record gave
> it as `184..240`, which is the *Editor* window; `DeviceReplayTests.cs:50` is
> the authority. An inherited error, caught in Task 1.

---

## 2. Phase 5's entitlement hole is closed, and it was EARNED here

This is the one claim in this phase that should be recorded as **newly earned**
rather than quoted as though it had always held.

Phase 5's §2.2 required its done-when to be written *narrower* than "a tampered
submission earns nothing", because **a deployment the player did not own still
won**. That was a knowingly-open hole for the whole of Phase 5, and the register
would lose that fact if the stronger sentence were simply asserted here.

What closed it, in order:

- **Task 8** fixed the deployment at issuance, resolved from owned rows only.
  The mechanism has two independent halves — `parseDeployment` (strips all but
  `creatureId`/`pocket`) and `resolveDeployment` (reads the spec off the owned
  row) — and with **either half intact, inverting the other leaves every
  HTTP-driven test green**. The brief's headline test ("send trait fields in the
  body") passed against a fully inverted resolution, because it cannot see past
  the parse-layer strip. Both tests are needed; neither alone is sufficient.
- **Task 9** made `SimulateEcho` carry the deployment it simulated, with the
  trait translation living in `sim` and running **ordinal → name only**. The
  reverse direction was rejected because an unknown name must map somewhere, and
  defaulting `"Bogus"` to `None`/0 would make a forged deployment compare equal
  to a simulated `Trait.None` slot.
- **Task 10** flipped the marker.

### The marker was rewritten in place, not deleted

`adversarial.test.ts`'s Phase 5 marker asserted the hole. It now reads **"CANNOT
deploy creatures the player does not own"** and expects `409
creature_not_owned` plus three state assertions. It passed on its first run —
Task 8 built the mechanism and Task 10 flipped the assertion.

**The original attack is preserved verbatim as a second adversarial test**,
because the flip moved the boundary from the submit side to the issuance side
and the old attack still has to be shown to fail there. The suite ships **18
tests and none of them asserts a hole**.

The marker was shown to discriminate rather than assumed to:

- Removing the issuance ownership check reddens **five** tests, including the
  flipped marker.
- Vacuity control: forcing `deploymentMatches` to always return false reddens
  26 tests and moves **no** mismatch test.
- A reviewer could construct **no payout path**: `resolveDeployment` builds
  every spec from `loadOwnedCreatures` (playerId + `liveCreature()`, `FOR
  UPDATE`), so unowned specs are inexpressible; `deploymentMatches` is
  null-first, length-second, then seven fields positionally, all before
  settle/credit/grant.
- **A null stored deployment is a MISMATCH**, not a skip. "Skip when null"
  reopens this exact hole for the full 2h issuance TTL for everyone on every
  deploy, and it reddens exactly one test — so without that one test the repo is
  green while the boundary is open.

---

## 3. The loop closed — locally, and only locally

The phase's done-when asks for the loop driven **against a deployed stack**.
**It was not, and no part of this record should be read as saying it was.**

There is no deployed stack to drive it against:

| Checked | Result |
|---|---|
| `gcloud run services list --project broodline-508416` | empty |
| `gcloud sql instances list --project broodline-508416` | `Listed 0 items.` |
| `infra/terraform/terraform.tfstate` | 3 resources, all networking (`google_compute_network`, a global address, a service-networking connection). No `api`, no `sim`, no Cloud SQL. `outputs: []` |

Standing one up means a Cloud SQL instance and two Cloud Run services — real,
billed, ongoing spend — so it was not done. **Every deployed-stack clause in
this phase's done-when is therefore believed, not demonstrated.** That is the
same debt Phase 5's Task 11 left, now inherited by Phase 7.

**What WAS driven**, end to end, with all twenty observations holding: real
Testcontainers Postgres migrated by the repo's own migrator, the real `sim`
service built and run as a child process (not a stub), and every request through
the real Hono handlers, auth, idempotency and transactions.

```
sign in       -> POST /v1/account
claim a node  -> POST /v1/node/claim  (rich_deposit, +720 shards, 1 gen-1 creature)
                 x2, to harvest two parents rather than mint them
splice        -> POST /v1/splice/preview, then /v1/splice/commit
                 Pale x Ember -> Gen-2 Pale, Chill I / Carapace I
start wave 7  -> POST /v1/wave/start with five owned creatures
submit        -> POST /v1/wave/submit -> 200 Win, integrityRemaining 3
```

Observed, each asserted rather than eyeballed: **230 shards** named in the
response *and* credited to the balance (1730 → 1960); **exactly one ledger row**
(6 → 7); **both parents gone** (`consumed_at` set, absent from `GET /v1/roster`);
**the child on the roster**; **`committed_to` cleared** on every creature; and
the issuance settled rather than left live.

**Two things were simulated, and the first draft of this section named only
one of them.** What it said is kept struck through, because the correction is
the point:

> ~~The one thing simulated was the passage of time. … Nothing else was written
> directly — every other mutation above is the result of an HTTP request.~~

**That last clause was FALSE**, caught in review by arithmetic this file's own
transcript could not support: the run reports thirteen creatures on the closing
roster and only two node claims, and no supply line reaches thirteen from two.
The counts were right; the sentence was wrong. Corrected, and re-measured by
re-running the driver with a per-step roster ledger rather than by
reconstruction:

| | |
|---|---|
| **EARNED** via HTTP responses | **3** |
| **SEEDED** via a direct `tx.insert(creatures)` | **10** |
| **Total booked** | **13** |
| `GET /v1/roster` reports | **13** |

```
  +1  EARNED  node/claim on slot 1 granted base stock
  +1  EARNED  node/claim on slot 1 granted base stock
  -2  EARNED  splice consumed both parents
  +1  EARNED  splice produced the child
  +5  SEEDED  giveRoster minted the wave-6 deployment
  +1  EARNED  wave-6 win granted base stock on the verified submit path
  +5  SEEDED  giveRoster minted the wave-7 deployment
  +1  EARNED  wave-7 win granted base stock on the verified submit path
```

So the two simulations are:

1. **The passage of time.** Harvest accrual is measured from
   `harvest_positions.last_settled_at`, so a node on a minutes-old player has
   accrued nothing. That row was backdated twelve hours rather than waiting
   twelve hours.
2. **The ten creatures deployed against waves 6 and 7.** `giveRoster`
   (`test/wave-helpers.ts`) does a direct `tx.insert(creatures)`. Earning five
   specific Taunt/Splash-at-tier-III creatures through the splice would take
   far more than the two windows this drive opens, and is not what the wave
   legs demonstrate — but it means **the harvest→splice leg and the
   wave→payout leg were each driven for real and were NOT joined by a supply
   line the player could actually walk.**

**What that does and does not weaken.** The claim-splice leg is fully earned:
both parents came out of `node/claim` grants, and the child is the splice's own
output. The wave leg's payout assertions — 230 shards, one ledger row,
`committed_to` cleared, the issuance settled — are untouched by how the five
deployed creatures got there, because the deployment is resolved from owned
rows either way (§2) and the rows are real rows. **What is NOT demonstrated is
that a player can harvest their way to a wave-7 deployment**, and nothing in
this phase shows they can.

**~~The driver was a throwaway and is not committed … that is owed.~~
DISCHARGED in fix round 2** — and it was the false claim above that made the
case. What caught that claim was not a test but a reviewer multiplying shard
deltas by hand, and a done-when checkable only that way is a claim rather than a
gate. The drive is now **`services/api/test/loop.test.ts`**, three tests inside
the ordinary suite, so it runs on every `pnpm --filter @broodline/api test`.

It carries the earned-vs-seeded ledger as **assertions, not output**: every
booking is of a delta measured against `rosterCount()` rather than an argument's
length; a reconciliation after every step catches any creature that enters
unbooked; and the closing assertion is on the **split** (`seeded === 10`,
`earned === 3`) rather than the sum, because booking all thirteen to one bucket
satisfies a sum and is exactly the conflation that went unnoticed here. Both
guards were weakened against committed state and each was seen to fail for its
own reason — booking the seeded deployment as earned reddens the split test
alone while the sum stays 13, and an unbooked `giveRoster` reddens the
reconciliation at the very next step.

A third test pins the unjoined supply line below, so it cannot be closed in
prose while the code still seeds ten.

> Writing the driver surfaced a fact worth keeping: a **twelve-hour window on
> the `common_vein` grants zero creatures**. Base stock is one gen-1 creature per
> 480 units (`accrual.ts UNITS_PER_CREATURE`) and the common vein runs at 20/hr,
> so a full window — the cap — is 240 units. That is the authored content
> behaving correctly, not a defect: the `rich_deposit` at 60/hr clears 720 in the
> same window and grants one. But it means **the cheap node cannot supply a
> splice at all**, and nothing in the design says so.

---

## 4. The toolchain footgun, closed

`implementation/scripts/generate-contract.sh` called `pnpm openapi`. The pnpm
that resolves first on a default PATH on this machine is **3.7.5** (under an nvm
node 10), and pnpm 3 has no bare-script form: given `pnpm openapi` it **prints
its usage text and exits 0**. `set -e` saw success, the script carried on
believing `openapi/broodline.json` had been generated, and the run died ~200
lines later inside `dotnet nswag` with a `FileNotFoundException` naming a temp
file — an error pointing nowhere near the cause. It cost several agents time
during this phase.

Closed in Task 13 (commit `832bfb6`) with **two guards, deliberately not
merged**:

- **A toolchain check**, first thing, before the build lock and the traps. It
  compares pnpm's major against `package.json`'s own `packageManager` pin, so it
  cannot drift from the repo, and reports found vs required, the offending
  binary's path, why exit 0 is the trap, and the `PATH` export that fixes it.
- **An assertion** that the document was actually produced and parses as JSON. A
  version check can only catch failures someone has already met; renaming the
  `openapi` script, a generator that swallows its own write error, or the script
  and `src/openapi.ts` disagreeing about `OPENAPI_OUTPUT_PATH` all land in the
  same place. The first guard is the diagnosis; this one is the observation.

**A node check would not have caught it.** The node resolving first here is v26
and satisfies `engines.node` (>=22) perfectly, while the pnpm beside it is
3.7.5 — they come from different bin directories.

Three branches, each fired in `preflight.test.ts` by a stub pnpm on PATH. The
stub for the version check reproduces 3.7.5's real behaviour (usage text, exit
0) rather than failing honestly, since a stub that exited non-zero would be
caught by `set -e` and prove nothing. Each guard was then **weakened against
committed state** and the right tests reddened: disabling the version comparison
reddens exactly one test (and the fall-through is caught by the second guard —
defence in depth, observed); disabling the output assertion reddens exactly the
other two. The two tests are each other's vacuity control.

---

## 5. Still owed, carried from earlier phases

| Item | State |
|---|---|
| **Enable GitHub Actions at `Sepand-Studio`** | **Owed since Phase 4, and it got worse.** This phase widened enums, added a second authored wave and a second bundle — the class of change the determinism gate exists to catch across runtimes — and all six gates were run by hand on one machine. `contract.test.ts` running the contract gate with the suite is the only automated part |
| **`client_architecture` §2's `ref readonly SimState`** | **Owed since Phase 3. FIVE times deferred.** Recorded so the count stays honest |
| **~~Raise `minimumClientVersion`~~** | **Discharged in `a8e5785`**, after this table had already recorded it as owed — see §6 |
| **Narrow `solo_execution` §3.1's degradation row** | Still owed. Untouched this phase |
| **Guard §4.3's retention split** | Owed against a retention sweep that does not exist, and it acquired a second claimant this phase — see §7 |

---

## 6. The one that turned from "unused" into "wrong", and was then paid

Design §11 predicted this phase would give `minimumClientVersion` its first real
trigger, because `wave/start`'s body grows. **It did, and the mechanism was not
reached for** — until the final fix wave.

`parseStart` (`services/api/src/routes/wave.ts`) **requires** `deployment`:
`parseDeployment(b.deployment)` returning null fails the whole parse. A
pre-Phase-6 client posting `{ waveId }` alone gets `400 invalid_request`.

**The rest of this section as first written is kept struck through, because this
file went on asserting the debt after it had been paid** — the same class of
defect §3 corrects in itself, and the reason §5's row and §12's item 4 both had
to move with this one:

> ~~`config/bundles/0.1.2/manifest.json` still carries `"minimumClientVersion":
> "0.1.0"`. So that client is **told it is current while every wave it starts is
> refused**. The mechanism has shipped since Phase 4, has never been used, and
> now has a concrete reason to be. It is one line in a manifest.~~

**Discharged in `a8e5785`.** The row is a COUPLING, so both halves moved:
`config/bundles/0.1.2/manifest.json` to `"minimumClientVersion": "0.2.0"`, and
`client/ProjectSettings/ProjectSettings.asset` to `bundleVersion: 0.2.0`.
`isBelow` now serves the shipped client (`0.2.0` is not below `0.2.0`) and
answers the Phase 5 one with `426 client_too_old`, which is the outcome the
field exists for. `0.1.0`'s and `0.1.1`'s manifests are deliberately untouched:
they are historical, and `0.1.0` is byte-identical to what is live in GCS, so
rewriting the floor a past bundle shipped under would falsify a record rather
than fix a bug.

**What this does NOT discharge**, and what §12 item 4 now carries instead:
nothing asserts that the two halves keep moving together, which is exactly how
they came to disagree. `verify-unity-settings.sh` is the recommended home — it
already pins `ProjectSettings.asset` keys by grep, and a bundle floor that
outruns the client it ships to is invisible until a player is told they are
current while every wave they start is refused.

---

## 7. Owed against work that does not exist yet

**A sweeper for abandoned issuances.** Task 8 established that an issuance
refused at checks 1–3 strands its creatures `committed_to` a dead issuance — a
bundle rollback that un-authors a wave leaves five creatures committed and every
splice of them refused indefinitely. The check ordering is right (roster checks
run *after* check 4, because in front of it an abandoned issuance locks a player
out of their own roster permanently — the call that would settle it is the one
being refused). This is the residual case, and it joins `weakenings.md` row 7's
retention split waiting on the same sweep.

**A writer for `arks`.** Nothing in `src/` creates an `arks` row; `loadArk`
returns a documented default. Safe today because no FK targets `arks` — but no
facility can be upgraded until something writes one.

**`validateNodeRates` is inert.** It skips a bundle with no `nodes.json`, which
was true of every fixture when it was written. It must become mandatory when
`loadBundle` starts reading `nodes.json`, or the rule proves nothing.

**`Diagnosis.PreWaveCheck` can never clear wave 7.** It counts all six
Skirmishers as simultaneous while Splash III caps at 5, and the breach path sees
2–3. Pre-existing, first reachable now, no production caller — it will mislead
the pre-wave panel when that screen is built.

---

## 8. Defects found in the plan and the design, not the implementations

This is the section that stops the record reading as uniformly successful. Each
of these was a defect in the *instructions*, caught by an implementer or a
reviewer running something.

**Task 0 — the plan would have destroyed its own proof.** `CorpusBaselineTests`
asserts the baseline header equals `SimVersion.Value`, so Task 1's bump reddens
it — and regenerating the baseline to fix that rewrites all 500 hashes and
passes *by definition*. Reordered to force corpus-green **before** the bump, then
a header-only re-baseline verified by an empty diff below line 1. The proof then
held: the corpus diff across Task 1 is exactly one line, `0.2.0` → `0.3.0`, with
all 500 scenario hashes byte-identical.

**Task 1 — a declared trait with no mechanic.** Splash was in the Values table
with tiers and no implementation step and no test, while wave 7 is 1 Lash + 6
Skirmishers and Skirmisher's counter *is* Splash. Half that wave had no working
answer. Escalated; ruled IMPLEMENT NOW.

**Task 3 — the schema as designed was mutually exclusive with itself.** §3.2
asked for tombstones in a second table **and** composite parent FKs from
creatures back onto creatures. Every ancestor is referenced by its own child and
the keys are `NO ACTION`, so `DELETE` on any prunable row violates its child's
key: **the prune could delete nothing**, and `data_model` §4's
9,000-rows-per-player problem would have gone unsolved while appearing solved.
Ruled: no second table; a pruned creature keeps its row, nulled to `{species,
generation, is_founder}` plus a `pruned` flag. Design amended. The brief also
shipped RLS with `USING` and no `WITH CHECK`, `current_setting()` without
`missing_ok`, no `GRANT`s at all, a constraint name that never matches its own
regex, and three vacuity traps in its test snippets.

**Task 4 — the plan's accrual formula paid frequent players less.**
`floor(elapsedMs*rate*mult/denom)` floors away every partial unit per call: at
20/hr one minute is `floor(0.333)` = 0, so 720 one-minute claims accrue nothing
while one 12-hour claim accrues 240. **A player who opens the app often would be
paid strictly less than one who leaves it closed, with nothing wrong in the
ledger.** Replaced with a telescoping difference of two absolute floors in
BigInt; verified by property over 3,000 random partitions across six rates
including windows straddling 1970, zero mismatches.

**Task 6 — three brief departures, each provably necessary.** Merging by pool
draw rather than outcome publishes "Carapace 33%, Carapace 33%" and measures 2/3
against a published 1/3 — it cannot pass its own convergence test. A positionally
locked trait makes the brief's own first test false for same-species pairs. And
`coverageLost` as sketched is **provably empty**.

**Task 8 — the brief mispaired pockets and tested the wrong refusal.**
`owned.map((c,i) => deployment[i].pocket)` is never request order, because
`loadOwnedCreatures` locks in ascending sorted id order. And the second test used
a wave the player had not cleared, so it would have passed for the wrong reason
(`wave_locked`).

**Task 9 — the plan's step order was impossible.** `contract.test.ts` asserts
`git status --porcelain` is empty over the generated paths, so a
correct-but-uncommitted regeneration *fails* the api suite. Real order:
regenerate → commit → run suite.

**Task 12 — no endpoint listed owned creatures.** `region/state` returned
`roster: {count, cap}` only; creatures arrived solely inside `node/claim` and
`splice/commit` responses. **A relaunch knew the roster's size and none of its
members**, so the loop closed only within a single session — which fails the
phase's own done-when. No task had ever been assigned that endpoint. `GET
/v1/roster` was added in a fix round. The implementer surfaced the gap via
`RosterScreen.IsComplete` rather than hiding it, which is why it was visible at
all.

### And defects in already-merged code from Phases 4 and 5

- **`credit()` could not debit** (Phase 4). Postgres evaluates the `CHECK`
  against the proposed insert tuple, never the updated one, so `delta: -1`
  reported `InsufficientFunds` against **any** balance. Undetected because all
  three callers were grants and the one negative-delta test used −1000 against a
  wallet of 15, where both hypotheses give the same error: green and
  non-discriminating. `debit()` added with the positive control that test lacked.
- **A malformed `issuanceId` answered 500, not 400** (Phase 5). `routes/wave.ts`
  checked only `length === 0` and the value reached a `uuid` column comparison,
  so any authenticated player sending a non-uuid got Postgres 22P02 as a 500.
  Fixed in its own bisectable commit.
- **A mixed-case uuid spliced a creature with itself and answered 500**, past the
  debit. The implementer then found a second direction the finding missed: a
  legitimate upper-case parent id got 404 from preview and 200 from commit,
  because preview keyed on the row's canonical id and commit on the raw request
  string. `isUuid` was **replaced** by `normalizeUuid`, which returns the string
  so a caller cannot validate and still hold the raw value.
- **The generated client default-initialises response objects.**
  `BroodlineApiClient.cs:1848` — `public Roster Roster {get;set;} = new Roster();`
  — so `Required.Always` does **not** mean "non-null implies the server sent it"
  on a constructed instance. Gating on `state.Roster != null` is always true. The
  working discriminator is `Cap > 0`. **This will catch someone else; it looks
  like a correct null check.** The sharper half: with an unset cap of 0,
  `count + grants > cap` is true for every granting node, so a response lacking a
  roster headline would have **greyed out every claim button on the map** and
  told a player with an empty roster it had no room.

---

## 9. Method: things this phase learned about its own testing

These are the transferable ones. They cost real time to find and each one
invalidates a class of assertion.

**A weakening that reports GREEN is untrustworthy unless the edit is confirmed
to have landed.** A Python quoting error meant an edit never reached the file and
the suite reported green against an *unmodified* migration — indistinguishable
from a weakening that legitimately stayed green. Weakenings that report RED are
self-proving; reddening shows the edit landed. **Confirm the diff before
believing any green weakening.**

**Weaken only against committed state.** A weakening helper reverting with `git
checkout -- services/api/src` destroyed an uncommitted fix mid-run. A grep caught
it, not a test. If a weakening run can revert the very change it is testing,
every subsequent green is a green for code that is no longer there.

**Relative-only assertions are blind to a uniform offset.** A Thursday-tick test
stayed green with its branch disabled, because a wrong reference tick shifts
every output by the same constant — before/after/one-week-later all still hold.
Closed with an **absolute** anchor (`epochFor` at the Unix epoch must be `0n`).

**One function with two callers guarantees agreement, not correctness.** Dropping
a parent's trait from the splice pool left the forecast and the sampler in
perfect agreement over a pool missing a parent's trait. Deleting only the two
*absolute* pins turns the convergence test green. Absolute anchors are what catch
it.

**A defence-in-depth guard that can only be exercised by first breaking
production is untestable by definition.** Three `commitCreatures` predicate
weakenings were green at first, because `resolveDeployment` refuses unowned,
dead and committed rows before that statement runs. That is exactly the argument
that made the loose `WHERE` "correct today" — and therefore cannot also be the
reason not to test it. Fixed by exporting the function and driving it directly.

**A test whose power is borrowed from a sibling is not pinned.** Task 11's new
security test never asserted that the wave-7 replay actually *wins*; if the
composition stopped winning, the attack becomes a Loss paying nothing and the
test stays green while proving nothing. Closed with a self-contained control on a
separate player.

**Row 5 of `weakenings.md`, and a false claim corrected in four places.** Row 5
was the one row Phase 5 booked forward rather than ran. Task 11 ran it as
tabulated: **green, nothing reddened**. That was not the end — Phase 5's register
had always predicted the single edit would be masked, which is *why* the row was
struck. What the second authored wave unlocks is the **combined** weakening,
never run anywhere: remove `matchesIssuance`'s waveId comparison *and* read the
reward from `echo.waveId`. Run: **status 200, Win, balance 250 → 480, +230 —
wave 7's reward paid against a wave 6 issuance, and nothing in the suite caught
it.** The new test reddens under it and is pinned to the guard rather than
incidentally to the reward source. A subsequent round found the *record* then
contained a false and unfalsifiable claim — prose saying a block "remains the
only thing that discriminates this weakening" when it calls `rewardForWave` with
its own literal ids and can never observe which argument the handler passes.
Corrected in all four places, **with the original text struck through rather
than deleted: the history of the row having been wrong is itself part of the
record.**

---

## 10. Undiagnosed

**One unexplained full-suite exit 1** immediately after commit `7bbb76f`, with no
failing assertion captured. Not reproduced in five runs by the implementer nor
five by the reviewer. Plausibly `harness.ts`'s documented container-teardown race
(`drainPool`, historically ~1/8, container-count sensitive) now that a 32nd
parallel container exists. **Attribution plausible, not confirmed** — no failing
output was ever captured. If a future run shows a file-level error with every
test green, start there.

---

## 11. Smaller things, still true

- Two constraint/function couplings are enforced only by cross-referencing
  **comments**: `CHECK (hatchery_tier = 1)` vs `rosterCap`'s table, and `CHECK
  (harvest_array_tier IN (1,4,8,12))` vs `shardMultiplierHundredths`. A reviewer
  prototyped and verified the fix — a `pg_get_constraintdef` test that goes red
  when either side is widened alone. The introspection idiom already exists in
  `ledger.ts` and `idempotency.ts`.
- `rosterCap` throws for any Hatchery tier but 1 and is **reachable from the paid
  submit path** since base stock landed there. A throw would 500 a winning
  submission and roll back its credit. Unreachable while the `CHECK` stands, but
  it is a throw where every other cap interaction is a skip.
- `NearestTaunter`'s carrier tie-break is unpinned: `<` → `<=` leaves every
  engine test green. Two equidistant Taunt carriers — which holds the Lash is
  untested.
- **The corpus can never show that a new trait WORKS.** All 500 scenarios are
  wave 6 with one Courser, so no Splash ever gets a second target. The corpus
  shows a new trait did not *break* old behaviour; the unit test is the only
  guard that it works, not a redundant second one. This applies to every trait
  this phase added.
- `validate.ts`'s `validateWaveRewardDistinctness` uses the unfiltered
  `waves.length > 1` while its `Set` uses the reward-filtered list, so a two-wave
  bundle where one wave lacks a reward emits a confusing second violation. The
  bundle is still correctly rejected; the extra message misdirects.
- `routes/splice.ts:107`'s comment still names `isUuid`, which no longer exists.
- `openapi.ts:138` declares `/v1/roster` has "No 500" while the route calls
  `rosterCap()`, which throws for an unauthored tier — exactly like
  `region/state`'s identical call, which *does* declare 500. Two routes making
  opposite claims about the same call.
- `weakenings.md:378` states the waveId-guard attribution as a flat parenthetical,
  missing the elimination hedge added elsewhere in the same commit. Not false,
  just less rigorous than its neighbours. The single-edit row's "17/17" is the
  pre-fix-round count and is stale against the file's current 18 tests.
- Duplicate pockets are accepted on both sides (duplicate ids are not). Inert
  today since pockets are not sent to `sim`, and now pinned on both sides.
- The harness security classifier **blocks deleting a validator check line** — it
  reads as validator-removal. Weakening-style verification of validators must
  drive the exported function against a constructed fixture instead.

---

## 12. What Phase 7 inherits

1. **The device capture.** §1. It blocks this phase's close, not just Phase 7's
   start.
2. **A deployed stack, and the loop driven on it.** §3. Inherited from Phase 5's
   Task 11, unchanged.
3. **GitHub Actions**, and with it the determinism gate that has still never run.
4. ~~**One line in a manifest** — `minimumClientVersion`.~~ **Discharged in
   `a8e5785`.** What Phase 7 inherits is the **coupling guard** that discharge
   did not build: nothing asserts that `bundleVersion` and a bundle's
   `minimumClientVersion` move together. §6.
5. **A retention/abandonment sweep**, now with two claimants. §7.
6. **Waves 8–10 and the raiders they require**; Brood, Drift, Bulwark and Delver,
   which are new systems rather than modifiers over the existing loop.
7. **The document edits this phase's code has outrun**: `region_roster`/bible
   §5.3 on node rates, `sample_economy` §7 on the downtier floor,
   `solo_execution` §3.1's degradation row, `client_architecture` §2.
8. ~~**An end-to-end test for the loop**, so that "the loop closes" stops being
   something checked by hand.~~ **Discharged in fix round 2** —
   `services/api/test/loop.test.ts`. §3. What is *not* discharged is the same
   drive against a **deployed** stack, which is item 2.
9. **A drive whose supply line is unbroken.** §3's reconciliation: ten of the
   thirteen creatures in the closing roster were inserted directly, so the
   harvest→splice leg and the wave→payout leg have never been joined by
   creatures a player actually earned. Closing that needs either content a
   player can harvest into a wave-7-capable deployment, or an explicit ruling
   that they cannot and that the two legs are separately sufficient. **Until
   one of those exists, "the loop closes" is two half-loops that meet on
   paper.**
