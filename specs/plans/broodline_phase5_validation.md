---
status: current
folder: 03-technical
note: >
  Phase 5 design. Validation: the sim service, server-issued seeds,
  submit-and-verify, replays to GCS, and the session refresh route the spine
  left unredeemable. Precedes the implementation plan.
---

# Broodline — Phase 5 Design: Validation

*The second deployable, and the five gaps between "re-simulate everything" and a server that can*

> **CURRENT — design, not a plan.** This settles what Phase 5 builds and how it
> is structured. The task-by-task plan is a separate document under
> `implementation/`. Deployables, the language boundary, retention and the
> degradation policy live in `broodline_solo_execution.md`; determinism, the
> replay format and breach diagnosis live in `broodline_combat_engine.md`; the
> authority split lives in `broodline_data_model.md`. This document owns none of
> them and cites all three.

---

## 1. Scope

`broodline_solo_execution.md` §8.2 scopes Phase 5 as *"`sim` on Cloud Run,
server-issued seeds, submit-and-verify, replays to GCS,"* done when *"a tampered
submission earns nothing; an honest one pays exactly once under retry."*

**Three amendments.** The done-when acquires an explicit entitlement boundary it
is not allowed to cross (§2.2), the session refresh route joins the deliverables
(§6), and Phase 3's device round-trip proof becomes this phase's gate rather than
continuing as standing debt (§2.5).

| | In scope | Deferred |
|---|---|---|
| **Repo** | `services/sim` — the last directory in §7's layout that does not exist | `tools/batch` stays where Phase 2 put it. No new client assemblies |
| **Infra** | A second Cloud Run service, internal ingress only, and the replay bucket with its lifecycle rule | HA, PgBouncer, Redis, a second region — their §10 triggers |
| **Contract** | The `api` → `sim` direction: ASP.NET OpenAPI → TS generator, committed, CI-diffed — §6 rules 1–3 | Nothing new on the Unity → `api` direction beyond the routes below |
| **Protocol** | `POST /v1/wave/start` and `POST /v1/wave/submit`, and `POST /internal/simulate` behind them | Every other intent endpoint at §6.2 |
| **Schema** | One table, `wave_issuances`, and the campaign-progress write the submit path earns | Creatures, lineage, nodes, regions — Phase 6 |
| **Money** | The campaign reward credit, through `credit()` and `withIdempotency()` as built | Commerce, receipts, entitlements. **No clawback** — §9 |
| **Identity** | `POST /v1/session/refresh`, plus the `kid`-aware verify and the missing `iss` claim | The Sign in with Apple HTTP surface — §10 |
| **Storage** | Player-visible replays to GCS, 30 days rolling | Pinning, the viewer, the CI corpus move |
| **Engine** | No behaviour change. One raider, one trait, wave 6 | The remaining seven raiders and eleven traits — §9 |

**This is the first phase whose done-when is adversarial.** Every gate before it
asked whether the code does what it says. This one asks whether a hostile client
can make it pay twice, and that is a different kind of test — one that passes
against a correct implementation and against three broken ones, unless it is
written to fail.

---

## 2. Five findings the design has to absorb

None appear in any document. All five come from reading §8.2's one line against
`solo_execution` §§3, 6 and 9, `combat_engine` §§2.1 and 3, and against what the
repository actually contains.

### 2.1 "Server-issued seeds" is a protocol and a table, not a field

`Replay.Seed` already exists — [`engine/Runtime/Combat/Replay.cs`][replay] stores
it, `Sim.Replay` feeds it to `SimRunner`, and the client sets it today. Making
the server the issuer changes nothing about the engine and everything about the
request flow.

**A seed the server hands out but does not remember is not server-issued.** If
`submit` trusts the seed inside the replay, a client mints its own and the word
"issued" describes a courtesy. The server must persist what it issued and
compare on submission, which makes this a two-call protocol with state between
the calls.

**And a seed the server remembers but re-issues freely is barely better.** A
client that can call `wave/start` a hundred times holds a hundred valid seeds,
simulates all of them locally, and submits the one that wins. Determinism, which
is what makes verification cheap at §2.1 of `combat_engine`, is exactly what
makes seed-shopping cheap for the attacker — the same property, pointed the
other way.

> **Resolved: one live issuance per player, consumed on submission.**

`wave/start` returns the existing live issuance rather than minting a second.
Consumption is the ledger's guard, not the idempotency key's — §4.2.

### 2.2 Re-simulation proves arithmetic, not entitlement

The done-when says *"a tampered submission earns nothing."* Read against what
this phase contains, that sentence is a claim the phase cannot make.

`Replay.Validate()` checks the record against **this engine's** rules: the
authored wave, the lane geometry the terrain family produces, `Stats.CreatureHp`
per species, `Deployments.Problem`, the rally bounds. It has no opinion about
whether the player owns the five creatures in `Deployment`, because **there is no
creature table until Phase 6** and the deployment is unowned by construction.

So a client that submits a deployment carrying Chill III when it holds none
produces a replay that validates, re-simulates identically on the server, and
earns the wave's reward. Nothing in this phase detects it.

> **Resolved: the reward is a function of the issuance, never of the submission.**

`wave/start` fixes the wave id against `campaign_progress.highest_wave_cleared`,
and the reward is looked up from the config bundle by that wave id. The
submission decides **whether** the reward is paid — `Result.Win` or not — and
never **what** is paid.

That leaves one exploit open and closes the rest: a modified client can win a
wave it would otherwise lose. It cannot skip ahead, cannot claim a wave twice,
cannot inflate a reward, and cannot claim all sixty milestones at once — which is
the failure §9.3 of `solo_execution` names when it rejects sampling. The
remaining hole closes in Phase 6, when the roster exists and the deployment can
be checked against it.

**This must be written into the phase's done-when rather than left implied**, or
the suite gets written against the stronger sentence and passes by testing
something else.

### 2.3 A superseded submission is rejected; only a viewer renders one

`solo_execution` §9.4 is unambiguous about replays recorded under an earlier
engine: *"renders its stored outcome with a notice and is not re-simulated."*
`Replay.Validate` enforces it, throwing with that instruction in the message, and
`IsFromThisEngine` exists so a caller can branch instead of catching.

**That rule is for the viewer. Applied to the payout path it is a hole.** A
submission the server cannot re-simulate is a submission the server cannot
verify, and rendering its *claimed* outcome with a notice would pay out an
unverified number — precisely the thing every wave being re-simulated exists to
prevent.

> **Resolved: two rules behind one version check.**

| Caller | Version mismatch means |
|---|---|
| Replay viewer | Show the stored outcome with a notice. `IsFromThisEngine` is false; never call `Sim.Replay` |
| `POST /v1/wave/submit` | **Reject.** `engine_too_old`, the issuance stays live, and the client is told to update |

`minimumClientVersion` is the mechanism that should stop such a submission
arriving at all. Phase 4 §3.2 calls it *"the mechanism, not yet the
constraint"* — it ships and is never raised. Phase 5 is the first phase with a
reason to raise it, and the submit path is the first place where failing to is
player-visible rather than theoretical.

### 2.4 The second contract direction doubles a toolchain whose pin is already fragile

§6 requires OpenAPI documents generated rather than written, generated clients
committed, and CI failing on a non-empty diff. Phase 4 built one direction —
Zod → OpenAPI 3.1 → NSwag → C#, driven by `implementation/scripts/generate-contract.sh`.
Phase 5 builds the opposite: ASP.NET's OpenAPI generation emits `sim`'s document,
and a TypeScript generator consumes it into `services/api/src/generated/`.

Two things make this more than a second invocation.

**The existing pin is machine-specific.** `nswag.json` pins `runtime: Net90` and
the script exports `DOTNET_ROLL_FORWARD=LatestMajor`, both tuned to a machine
carrying only .NET 10 — recorded in
[`implementation/2026-09-11-phase4-followups.md`][followups] §2. Adding a second
generator to the same script without resolving that pin doubles the surface of a
known-fragile step.

**And the diff gate must cover both directions or it covers neither.** One script
regenerating two documents and two clients, one `git diff --quiet` over all four
paths. Two scripts invite the second one being forgotten in CI, which is the
failure mode rule 3 exists to prevent.

### 2.5 The gate that would prove this phase is dark

Phase 3's done-when — *"a wave played on device replays bit-identically in
xUnit"* — is not running. Bumping `SimVersion` to `0.2.0` moved the engine past
the tracked device-replay artifact, so `ReplayArtifact.Superseded` short-circuits
both round-trip tests before their assertions. `dotnet test` reports 174 passing
with 0 skipped and does not cover it.

The tests now assert that a `ReplayFormatException` is thrown. **That is a
strictly weaker proof than comparing hashes**, and it is weaker in exactly the
direction Phase 5 depends on: this phase's entire claim is that the server's
re-simulation reproduces the run the client played.

`cross-runtime-diff.sh` is not a substitute. It proves CoreCLR and IL2CPP agree
on generated scenarios run headlessly — arithmetic portability. The device proof
covers a wave run through a renderer, at a variable frame rate, with a human tap
in it. Phase 3 §7 calls these different proofs and neither implies the other.

> **Resolved: the re-capture is a task in this phase, not standing debt.**

Wave 6 replayed on physical hardware under `0.2.0`, the artifact committed, and
both round-trip tests returned to **comparing `Outcome.Hash`** rather than
asserting a throw. Phase 5 does not close while its own premise is unproven.

**GitHub Actions is a separate blocker and is not one this phase can clear.** It
is disabled at the `Sepand-Studio` organisation level; the session token carries
`read:org`, not `admin:org`. Every gate below verifies locally, as Phase 4's did,
so the work proceeds — but `determinism.yml` stays manual-only until a human
enables it.

---

## 3. `sim`, the second deployable

`solo_execution` §3: two deployables, and `sim` is separate because it is a
different language and a different scaling curve. This is where the second one
arrives.

### 3.1 It holds no database, no secrets and no public ingress

**`sim` is a pure function behind HTTP.** It takes bytes and returns a verdict.
It has no Cloud SQL client, no GCS client, no `JWT_SECRET`, no knowledge of
players, wallets or servers. `api` writes every row and every object.

That is not tidiness. Three consequences fall out of it:

| | |
|---|---|
| **Internal ingress only** | No public route, so `sim` needs no authentication of its own and no rate limit of its own. Cloud Run ingress `internal`, invoked by `api`'s service account |
| **Scales to zero, restarts freely** | Nothing to drain, no connection pool against the Postgres cap — which `solo_execution` §5.7 names as the thing that runs out before CPU does |
| **Its failure is one degraded route** | `sim` down means `wave/submit` fails retryably. Nothing else in the product notices — §8 |

**It reaches the engine by `ProjectReference`, not a package.** §9.2: one source
tree, two manifests. `services/sim/Broodline.Sim.Service.csproj` references
`engine/Broodline.Sim.csproj` and joins the root `Broodline.sln`, so a change to
the engine cannot be stale on the server.

### 3.2 `POST /internal/simulate` — §6.1's shape, made exact

`solo_execution` §6.1 gives the shape:

```
POST /internal/simulate
  → { engineVersion, seed, waveId, terrain, deployment, rally }
  ← { outcome, integrityRemaining, breachDiagnoses[], outcomeHash }
```

One correction the engine forces. **The request carries the serialized replay,
not its fields spread out.** `Replay.Serialize()` is fixed-layout little-endian
and `Deserialize` validates inside itself — Phase 3 made "an unvalidated Replay
object" impossible to hold. Re-describing the six fields in JSON at this boundary
builds a second parser for a format that already has a canonical one, and the two
can disagree. The body is the replay bytes, base64 in a JSON envelope; the named
fields above are what `sim` **echoes back** so `api` can key its ledger row
without parsing anything.

The response maps `Outcome` — `Result`, `IntegrityRemaining`, `Hash` as a
decimal string because `Hash` is a `ulong` and JSON numbers are not — plus the
`Breach` array, whose three booleans are `combat_engine` §7's diagnosis recorded
at tick phase 7. `api` forwards those to the client for the Wave Defeat screen
and stores none of them.

**`api` never parses a replay, never learns what a trait does, never reimplements
a rule.** That is §6.1's whole purpose and it is the one boundary in this phase
that must not soften.

**Rejections are results, not errors.** A `ReplayFormatException` — forged bytes,
a superseded engine version, a deployment the rules forbid — returns `200` with a
`rejected` verdict and the reason, because it is a fact about the submission
rather than a fault in the service. A `5xx` from `sim` means `sim` is broken, and
`api` must be able to tell those apart to decide whether the issuance survives.

### 3.3 The SLO, and why batching is not built

`solo_execution` §3.1 budgets **wave submission end to end at p99 under 500 ms,
including re-simulation.** One 90-second wave is roughly 20 ms of CPU. The budget
is dominated by two network hops and a transaction, not by the simulation.

`combat_engine` §2.1 notes verification *"can be batched."* It is not batched
here: batching is a throughput tool and this phase has one player. Building it
now would add a queue, a flush policy and a partial-failure path to defend a
number nothing is measuring.

---

## 4. The wave protocol

### 4.1 `POST /v1/wave/start` — issuance is single-use

```
POST /v1/wave/start          { waveId }
  → 200                      { issuanceId, seed, waveId, expiresAt }
```

Five checks, in this order, all inside `withServer()`:

1. **The wave is the next one.** `waveId` must equal
   `campaign_progress.highest_wave_cleared + 1`, or a wave already cleared —
   replay is allowed and rewarded, subject to check 2. Any other value is
   `wave_locked`.
2. **The replay cap holds.** `broodline_campaign_structure.md` allows **three
   replays per wave per day, then nothing until tomorrow** — the cap is what
   stops replay competing with harvesting. A fourth issuance for an
   already-cleared wave is `replay_cap_reached`. **Without this check the
   phase's own reward claim is false**: wave 1 is farmable indefinitely, and no
   amount of re-simulation notices, because every one of those runs is honest.
3. **The wave is authored in the bundle.** The bundle is the content source, per
   §5.2's content-versus-data split. A wave id absent from `waves.json` is
   `wave_locked`, not a 500.
4. **A live issuance is returned, not replaced.** One row per player with
   `settled_at IS NULL` and `expires_at` in the future. This is §2.1's seed-shop
   defence and it is the load-bearing half of the endpoint. A row that is
   `settled_at IS NULL` but **past** its expiry is the abandoned-wave path: settle
   it `'expired'` and insert, in the same transaction — §4.3.
5. **The seed comes from the server's CSPRNG**, and the row is written before the
   response is formed.

`expiresAt` is **two hours**. Long enough that a player who backgrounds the app
mid-wave loses nothing; short enough that a stockpile is not a strategy. It is a
starting value and belongs on the playtest list, not in this document.

### 4.2 `POST /v1/wave/submit` — one verification, one credit

```
POST /v1/wave/submit         { issuanceId, replay }     Idempotency-Key: <key>
  → 200                      { result, integrityRemaining, breaches[], reward? }
```

The sequence, and the order matters:

| | Step | Fails as |
|---|---|---|
| 1 | `requireSession` — `serverId` and `accountId` from the **verified claim**, never from the body | `401` |
| 2 | Load the issuance for this player. Absent, expired or already consumed | `issuance_invalid` |
| 3 | Call `sim`. A `5xx` here leaves the issuance live and returns retryably | `sim_unavailable`, 503 |
| 4 | `sim` returns `rejected` — forged, superseded, rule-violating | `submission_rejected` / `engine_too_old` |
| 5 | The replay's `seed` and `waveId` disagree with the issuance | `submission_rejected` |
| 6 | Inside one transaction: consume the issuance, look up the reward, advance campaign progress, `credit()` it | `wave_locked` |

**Steps 5 and 2 are two different checks and both are needed.** The issuance
proves *this player was given a wave*; the seed comparison proves *this replay is
of that wave*. Dropping the second lets a player start wave 7, simulate wave 3
locally against an old seed, and submit it against the wave 7 issuance.

> **Amended — step 2 runs before step 3, and step 2 does not answer.** The
> handler shipped with steps 2 and 3 transposed: `sim` was called first, so a
> fabricated or already-settled `issuanceId` bought a full re-simulation before
> being refused. The transposition was honouring a real constraint — *do not
> hold a Postgres connection across the `sim` call* (`solo_execution` §5.7) —
> but collapsed it into the stronger *do not touch the database before `sim`*.
> Step 2 now runs in its own transaction, which commits and releases its
> connection before `simulate` is reached.
>
> **The order is not protecting an enumeration oracle.** `loadLiveIssuance` is
> scoped by `server_id` **and** `player_id` **and** `issuance_id`, and the first
> two come from the verified claim, so the only rows it can ever answer about
> are the caller's own. §6's deliberately indistinguishable 401 is the
> *contrasting* case: there the caller is unauthenticated and the token is the
> credential being guessed. Nor is the distinction new — `consumeAndRefuse` has
> always answered `submission_rejected` for a live issuance and
> `issuance_invalid` for a dead one in a single request.
>
> **What step 2 gates is the `sim` call, not the response.** Refusing directly
> from step 2 breaks guard one below: a client retrying across a network failure
> resends the same key *after* the issuance it paid for is settled, so a refusal
> taken at step 2 answers a retrying client `409` for a wave it was in fact paid
> for. A dead issuance therefore skips `sim` and still falls through to
> `withIdempotency`, which replays the stored response if there is one and
> produces `issuance_invalid` if there is not.
>
> **Two dead-path answers change, and only for a dead issuance.** A too-old
> engine and a `sim` outage are both now answered `issuance_invalid` (409)
> rather than `engine_too_old` (426) / `sim_unavailable` (503), because neither
> is knowable without the call this reorder exists to skip. §2.3's invariant is
> untouched — `engine_too_old` must *leave the issuance live*, and nothing on
> this path settles anything — and a superseded client still learns so on its
> next submission against a live issuance, the only one that could have
> succeeded. The outage case is strictly more honest: a retryable 503 for an
> issuance that can never succeed invites a retry loop that cannot terminate.
> For a live issuance every answer is unchanged.

> **Amended — step 6 can refuse, and the reward lookup sits before the
> advance.** The row read "consume the issuance, advance campaign progress,
> `credit()` the reward" with nothing in the Fails-as column. Both halves were
> stale. The reward is looked up from the bundle *between* the consume and the
> advance, and it can come back `null` — see §5.2's amendment for when — so
> step 6 refuses `wave_locked`, 409. The order is deliberate and predates this
> amendment: advancing `campaign_progress` and only *then* refusing would
> commit the clear under an error response, and the player's next
> `wave/start` would treat a wave they were never told they cleared as
> already cleared. The issuance settles `'consumed'` either way — they did win
> it; this is a spent attempt, the same as a `Loss`.

**"Pays exactly once under retry" has two independent guards, and they fail
differently.**

| Guard | Protects | Fails as |
|---|---|---|
| `withIdempotency()` on the `Idempotency-Key` | The **response**. A resend returns the stored body verbatim | Stored response replayed |
| `settled_at`/`settlement = 'consumed'` on the issuance, set inside the mutation's transaction | The **ledger**. A second submission with a *different* key finds no live issuance | `issuance_invalid` |

Idempotency alone is insufficient here, and the reason is worth stating: §6.3's
key is client-supplied. A modified client simply sends a new key. The issuance is
server-supplied and consumed under the same transaction as the credit, so the
second attempt has nothing to consume. **The suite must prove the second guard
independently of the first** — Phase 4's concurrency gate is the precedent, and
its lesson was that a single-path test passes while the invariant is broken.

### 4.3 `wave_issuances`

Migration `0003_wave_issuances.sql`, following `0001`'s conventions exactly:
`server_id` leading the primary key and every index, `FORCE ROW LEVEL SECURITY`
on, a composite foreign key to `players`.

```
wave_issuances (server_id, issuance_id, player_id, wave_id,
                seed, issued_at, expires_at, settled_at, settlement)
                PRIMARY KEY (server_id, issuance_id)
```

> **Amended after review.** This section first specified a `consumed_at`
> column with the one-live index keyed `WHERE consumed_at IS NULL`. **That
> locked a player out of the game.** An abandoned wave leaves a row that is
> expired but unconsumed, so it still sits in the index; `wave/start` excludes
> it as expired, inserts, and collides — `23505`, for at least an hour, every
> time anyone backgrounds the app mid-wave.
>
> The trap is that **liveness had two definitions**: the index's
> (`consumed_at IS NULL`) and the handler's (`consumed_at IS NULL AND
> expires_at > now()`). They agree everywhere except the abandoned window.
> Time cannot reconcile them — Postgres requires index predicates to be
> `IMMUTABLE` and `now()` is `STABLE`, so `expires_at > now()` is not merely
> unwise in a predicate, it is rejected outright. A row cannot un-index itself
> as the clock advances.
>
> **The fix is one terminal state, reached only by a write**, so the index
> predicate and the handler's liveness test are the same expression and the
> handler cannot express a notion of live that the index does not share.

**A settled issuance is settled by a write, never by the clock.**

| Column | |
|---|---|
| `settled_at timestamptz` | NULL while live. Set once, to the moment it stopped being live |
| `settlement text` | `CHECK (settlement IN ('consumed','expired'))`, NULL iff `settled_at` is NULL |

Three constraints do real work:

- **A partial unique index** on `(server_id, player_id) WHERE settled_at IS NULL`.
  §4.1's one-live-issuance rule enforced by Postgres rather than by a handler
  remembering to check — the same instinct as `0002`'s RLS gate preferring
  `pg_class` to an enumeration.
- **The settlement is write-once.** A trigger, in the style of
  `accounts_server_id_immutable`, and written with `DROP TRIGGER IF EXISTS`
  first — the one statement in `0002` that is not re-runnable is exactly this
  pattern without the guard. It permits NULL → set, and rejects every rewrite
  and every reversion to NULL.
- **`CHECK (seed >= 0)`.** Postgres `bigint` is signed and the engine's seed is
  a `ulong`. A seed at or above 2^63 either errors on insert or, if someone
  "fixes" that with a cast, sign-flips into a *different wave* — presenting as
  a hash mismatch on an honest submission, which is the single most misleading
  failure this phase could ship. §4.1 draws the seed from `[0, 2^63-1]`; this
  makes the contract loud at the boundary rather than implicit at four call
  sites.

**`wave/start` settles the stale row and inserts the new one in one
transaction.** Finding an expired live row is not an error — it is the ordinary
abandoned-wave path, and settling it `'expired'` is what makes room. Settling it
`'consumed'` instead would silently charge the player a replay they never took.

**The replay cap counts off this table**, not off a second counter. Issuances
for `(player_id, wave_id)` with `settlement = 'consumed'` since the day boundary
are the count §4.1 check 2 reads — **`'expired'` rows are not counted**, which
is the whole reason the two settlements are distinguishable rather than a single
boolean. Retention splits in two:

| Row | Aged out | Because |
|---|---|---|
| `settlement = 'expired'`, or still live and long past expiry | One hour past `expires_at` — three hours after issuance | It is dead weight, and nothing counts it |
| **`settlement = 'consumed'`** | **48 hours after `issued_at`** | It *is* the replay counter. Swept on the other schedule, a player's three replays vanish three hours later and the cap never binds |

48 rather than 24 because the count is against a **day boundary**, not a rolling
window: a row written at 23:50 must still be countable at 00:10, and a 24-hour
sweep deletes it at 23:50 the next day — inside the window it is still needed
for. This is the kind of off-by-a-boundary that a test written at midday passes.

**The sweep is garbage collection, not correctness.** Nothing above depends on
it having run: settlement is a write on the request path, so a player is never
waiting on a cron to be allowed to play. That is the property the first version
of this section did not have.

### 4.4 What Phase 5's "tampered" means

Stated plainly, because §2.2 makes the unqualified sentence false:

| Attack | Outcome |
|---|---|
| Forged outcome — claim a win that did not happen | **Caught.** `sim` re-simulates; `api` pays what `sim` returns, never what the client claims |
| Forged replay bytes | **Caught.** `Deserialize` rejects magic, format version, truncation and trailing data |
| Replay a winning submission twice | **Caught.** The issuance is consumed |
| Submit against a self-chosen seed | **Caught.** Seed compared to the issuance |
| Skip to wave 60 | **Caught.** `wave/start` fixes the wave against campaign progress |
| Inflate the reward | **Caught.** Reward is a function of the issuance's wave id |
| Farm a cleared wave for its reward | **Caught.** Three replays per wave per day, counted off consumed issuances |
| Seed-shop for a favourable run | **Caught.** One live issuance |
| **Deploy creatures the player does not own** | **Not caught. Phase 6.** No creature table exists to check against |

---

## 5. Replays to GCS

### 5.1 Two collections, two policies

`solo_execution` §9.5 separates them by owner, and the separation is what makes
both retention rules simple:

| Collection | Policy | Owner |
|---|---|---|
| Player-visible replays | 30 days rolling, up to 20 pinned per player | Product |
| The CI corpus | Its own retention, seeded with generated waves | Engineering |

**Phase 5 builds the first and leaves the second where it is.** The corpus lives
in the repo as a committed seed today and `cross-runtime-diff.sh` reads it there.
Moving it to GCS is what §7.3 asks for *when the full 5,000-wave corpus exists*,
and it does not.

**Pinning is deferred and the lifecycle rule is not.** A GCS lifecycle rule
deleting at 30 days is one line of Terraform and cannot be retrofitted onto
objects already deleted; pinning needs a `pinned_replays` table and a UI that
does not exist. The rule ships now with the 20-pin exemption unimplemented,
because at milestone-1 scale nothing is 30 days old yet.

### 5.2 Written on the verified path only

The object is written **after** `sim` verifies and **inside** the same handler,
keyed `replays/{server_id}/{player_id}/{issuance_id}.bin`. The bytes are the
submitted replay verbatim — a few hundred bytes, per §3's constraint.

Three things follow from writing it only on the verified path:

- **A rejected submission leaves no object.** Storage is not an attacker's write
  primitive.
- **The outcome is not stored beside it.** `Outcome` is derivable from the
  record by anything that can run the engine, and §9.4's superseded-replay path
  needs the *recorded* outcome — which is Phase 6's problem, when a replay row
  exists to hold it. This phase stores the inputs, which is what a replay is.
- **The write is not in the money transaction.** A GCS failure after a
  successful credit must not roll back a payment. It is logged and the object is
  lost; a missing replay is a degraded viewer, not a ledger defect.

> **Amended — the replay follows *verification*, not the response status.**
> This section said "the verified path" and the handler read it as "the
> successful path". Those differ in exactly one place, and the design did not
> address it.
>
> **The reachability condition.** The bundle can be republished — or, more
> realistically, the pointer *rolled back* — during an issuance's two-hour TTL,
> dropping the reward for a wave that carried one when the issuance was
> granted. `config/bundles/0.1.0` is the live example: its wave 6 carries no
> `reward` field, because the field did not exist when it was published, and
> `setPointer` does not re-validate the bundle it names (only `publishBundle`
> validates, and Task 7's reward check would refuse 0.1.0 today).
>
> **Three things must coincide; the rollback alone is not enough.**
> `loadBundle` caches the bundle *per process* and nothing in production ever
> clears it — `clearBundleCache` is tests-only — so an instance that was already
> running keeps serving 0.1.1 until it dies. The condition is: a pointer
> rollback, **and** an instance that started or restarted after it, **and** an
> issuance granted before it that is still inside its two-hour TTL. Ordinary
> rather than exotic on Cloud Run — `config/bundle.ts`'s own comment gives
> "instances are short-lived" as the reason the cache is acceptable at all, and
> a rollback is usually accompanied by a deploy — but it is three conditions,
> not one.
>
> When they coincide, a player holding a live wave-6 issuance submits a
> **winning** replay; `sim` verifies it, it proves to be of the issued wave, the
> issuance settles `'consumed'` — and then the reward lookup returns `null` and
> they are answered `wave_locked`, 409. The attempt was real and is now spent.
> *(The campaign is **not** advanced: §4.2 step 6 checks the reward before
> `advanceCampaign` precisely so a 409 cannot coincide with a silent clear.)*
>
> **The ruling: the replay is written.** §5's principle is that every spent,
> verified attempt is reconstructible, and a refusal the player cannot appeal is
> exactly when the stored replay matters most.
>
> **"Verified" is a three-part conjunction, and all three parts are required:**
> `sim` accepted the bytes, **and** they proved to be of the *issued* wave
> (§4.2 step 5), **and** this call actually consumed the issuance. That covers
> `200` and `wave_locked`. It deliberately does **not** cover the two other
> refusals that also settle the issuance — a `sim` rejection (nothing was
> verified) and a seed/wave mismatch (something was verified, but not the issued
> wave, so the bytes are attacker-chosen). Both exclusions are what keep the
> first bullet above true: **storage is not an attacker's write primitive.**
>
> **The write must not be able to fail the request.** It was already outside the
> money transaction; it is now also on a path that has no payment to protect but
> still has a response, so the swallow additionally stops a storage error
> turning a `409` into a `500`.

---

## 6. The session refresh route

`POST /v1/account` returns a 90-day refresh token that **no endpoint can
redeem** — `redeemRefreshToken` has no caller outside its own tests. A session
therefore ends hard at the 15-minute access-token TTL with no recovery path.

This is plan-consistent — Phase 4's route table only ever named `account.ts` and
`sync.ts` — and it collides with this phase's done-when. *"Pays exactly once
under retry"* describes a client resending across a network failure, and the
retry window that matters most is the one that spans a token expiry. A phase
whose central claim is about retry cannot leave the session unable to survive
fifteen minutes.

```
POST /v1/session/refresh     { refreshToken }
  → 200                      { accessToken, refreshToken }
```

Three defects from [`followups`][followups] §2 are fixed in the same change,
because this is the first route that makes any of them observable:

| | |
|---|---|
| **`kid`-aware verify** | Session tokens carry no key id, so rotating `JWT_SECRET` silently invalidates every outstanding 90-day refresh token. Two-key verify is cheap now and a migration later |
| **The missing `iss` claim** | Absent from `SessionClaims`. Free to add while the verify path is open |
| **`deleted_at` on redemption** | `requireSession` deliberately does no deletion lookup — a read per authenticated request is what the design avoids. Refresh is not per-request, and it is the right place to pay for it |

**Rotation is deferred deliberately.** `solo_execution` §6.4 defers refresh-token
rotation to the first non-TestFlight players. Redemption returns the same token
until then.

---

## 7. Testing

Four layers. Two extend Phase 4's structure, one is Phase 3's returned to
strength, and one is new because nothing existing proves it.

| Layer | Covers | Runs |
|---|---|---|
| **xUnit, headless** | `sim`'s handler over the wire: a round-tripped replay yields the same `Outcome.Hash` as `Sim.Replay` in-process. Rejection paths return `200 rejected`, not `5xx` | `dotnet test` |
| **Device round-trip, restored** | §2.5. Wave 6 re-captured on hardware under `0.2.0`; both tests back to **comparing hashes**, not asserting a throw | Manual, per release. **Phase gate** |
| **`api` integration, real Postgres** | The six-step submit sequence, each failure in isolation. Contract diff over **both** directions | `pnpm --filter @broodline/api test` |
| **The adversarial suite** | §4.4's table, one test per row, each proven to fail when its guard is weakened | `pnpm --filter @broodline/api test` |

**The adversarial suite is this phase's gate, and every test in it must be shown
to fail.** Phase 4's lesson, recorded in its own review notes, is that a suite
can pass while proving nothing — auth tests that passed against a broken guard,
a concurrency gate that only proved anything once the insert was deliberately
weakened to `onConflictDoNothing`. Three weakenings are named in advance:

| Weaken | Must break |
|---|---|
| Drop the seed comparison at step 5 | The wrong-wave submission test |
| Drop the partial unique index on live issuances | The seed-shopping test |
| Age issuance rows out at consumption rather than at expiry | The replay-cap test |
| Settle the issuance outside the credit's transaction | The double-submit-with-a-new-key test |

**And one vacuity guard.** The double-submit test must assert a *balance*, not an
error code — a test that checks for `issuance_invalid` passes identically against
a server that rejects everything.

---

## 8. What this design deliberately does not do

- **No engine content.** One raider, one trait, one authored wave. The validation
  machinery is content-agnostic: Courser and Chill prove it exactly as well as
  eight raiders would, and the fill is its own work with its own gates. Named
  here because Phase 6's loop is the first thing that genuinely needs it.
- **No optimistic grant and no clawback**, and this **narrows `solo_execution`
  §3.1** — §9. `sim` down returns `503` and the issuance survives.
- **No raids, no auto-resolve, no region defence.** `POST /internal/simulate` is
  shaped to serve auto-resolve — the same call with no rally input, per
  `combat_engine` §2.1 — but nothing calls it that way. Raids need Collectors,
  routes and the Transit Board; none exists.
- **No replay viewer.** The record is stored; drawing it is a client phase's work,
  and `IsFromThisEngine` already carries the branch it will need.
- **No pinning.** §5.1 — the lifecycle rule ships, the exemption does not.
- **No nodes, regions, harvest, relocation or splice.** Phase 6.
- **No creature ownership check.** §2.2, and it is the one hole this phase leaves
  open knowingly.
- **No batching, no Redis, no PgBouncer, no HA.** Named triggers at §10; none is
  measured.
- **No refresh-token rotation.** §6.4 defers it to the first non-TestFlight
  players.
- **No `Broodline.UI` and no Codex sheet.** Still a client phase's work.

---

## 9. Decisions owed

Six. Three carry forward, three are this phase's.

| Decision | Why it matters | State |
|---|---|---|
| **Narrow `solo_execution` §3.1's degradation row for `sim`** | It promises campaign submissions *"queue and reward optimistically, reconciled on recovery."* That needs an outbox Phase 4 deferred and a clawback — a ledger **debit** against a balance the player may already have spent. At one server with no players, a retryable `503` is honest and costs nothing. The row is right for a live game and wrong for this phase | **New.** Owner: `solo_execution` §3.1. Re-widen at the first non-TestFlight players |
| **Reward amounts enter the config bundle** | §2.2 makes the reward a function of the wave id. `waves.json` carries `{id, integrity, laneCount, spawns}` and no reward. This is content, not data — §5.2 — so the bundle is its home, and `Broodline.Config.Validate` gains a rule | **New.** Owner: `broodline_campaign_structure.md` for the values, this phase for the shape |
| **Raise `minimumClientVersion` for the first time** | §2.3 makes a superseded submission a rejection the player sees. The mechanism has shipped since Phase 4 and has never been used; the first use should be deliberate rather than urgent | **New.** Owner: the release process, `solo_execution` §7.0 |
| **Correct `client_architecture` §2's `ref readonly SimState`** | Still present at line 52, still specifying a shape that guarantees nothing. Phase 3 resolved it in code and booked the edit; Phase 4 booked it again | **Owed since Phase 3.** Twice deferred |
| **Re-run `broodline_supersession_map.md`** | It accounts for none of `plans/`, and now misses three phase designs rather than two. "Not in the map" is weak evidence a document is unratified | **Owed since `solo_execution`.** Growing by one document per phase |
| **Enable GitHub Actions at `Sepand-Studio`** | Needs `admin:org`; no session can do it. `tests.yml` and `determinism.yml` are committed and have never run. Every gate here verifies locally, so it blocks no work — but it blocks the determinism gate `solo_execution` §7.3 designed | **Owed since Phase 4.** Human-only |

---

[replay]: ../../engine/Runtime/Combat/Replay.cs
[followups]: ../../implementation/2026-09-11-phase4-followups.md

---

*Owns: the Phase 5 slice boundary, the wave issuance protocol and its two
independent once-only guards, the entitlement boundary this phase cannot cross,
the split between a rejected submission and a rendered superseded replay, the
`api` → `sim` contract direction, and the adversarial suite as the phase's gate.
Does not own: deployables, the language boundary, retention or the degradation
policy (`broodline_solo_execution.md`), determinism, the replay format or breach
diagnosis (`broodline_combat_engine.md`), the authority split
(`broodline_data_model.md`), reward values
(`broodline_campaign_structure.md`), or phase sequencing
(`broodline_solo_execution.md` §8.2).*
