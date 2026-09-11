---
status: current
folder: 03-technical
note: >
  Phase 4 design. The backend spine: infrastructure, the schema and its
  isolation, identity, the ledger, the config bundle pipeline, and the three
  version axes that arrive together. Precedes the implementation plan.
---

# Broodline — Phase 4 Design: The Backend Spine

*The first server, and the five gaps between what this phase builds and what it proves*

> **CURRENT — design, not a plan.** This settles what Phase 4 builds and how it
> is structured. The task-by-task plan is a separate document under
> `implementation/`. Deployables, storage mechanics, the language boundary and
> account lifecycle live in `broodline_solo_execution.md`; entity shapes and the
> authority split live in `broodline_data_model.md`; population and lifecycle
> live in `broodline_server_topology.md`. This document owns none of them and
> cites all three.

---

## 1. Scope

`broodline_solo_execution.md` §8.2 scopes Phase 4 as *"Terraform, Cloud Run,
Cloud SQL, RLS, ledger, wallets, idempotency, Sign in with Apple, `/v1/sync`,
generated client,"* done when *"cold start fetches a real player from a real
server in one call."*

**Three amendments, each argued at §2.** The done-when grows a mutating path,
the config bundle pipeline joins the deliverables, and the three version axes
that arrive in this phase are designed together rather than separately.

| | In scope | Deferred |
|---|---|---|
| **Repo** | `services/api`, `config/`, `infra/terraform/`, `pnpm-workspace.yaml` — the whole TypeScript half of §7's layout, none of which exists | `services/sim` — Phase 5. `tools/batch` stays where Phase 2 put it |
| **Infra** | Terraform → Cloud Run, Cloud SQL Postgres, GCS buckets, one region | HA, PgBouncer, Redis, provisioning automation, merge tooling |
| **Schema** | Drizzle. Seven tables. `server_id` leading every server-scoped key and index, RLS on, identity and registry outside it | Creatures, lineage, nodes, regions, territory — Phases 6 and later |
| **Money** | Ledger, wallets with `version`, idempotency keys, the invariant job | Commerce, receipts, entitlements — their §10 triggers |
| **Identity** | Sign in with Apple, guest and upgrade, JWT plus refresh, immutable server assignment, the deletion path | Recovery — Apple's problem, deliberately |
| **API** | `GET /v1/sync`, `POST /v1/account`, the error envelope, `minimumClientVersion` | Every other intent endpoint |
| **Config** | Author → validate → publish immutably → roll back, seeded with what exists | The LiveOps console |
| **Contract** | Zod → OpenAPI 3.1 → NSwag → committed C# client, and the three client assemblies that hold and call it — §7 | The `api` → `sim` direction — Phase 5. `Broodline.UI`, and the outbox |
| **Versioning** | All three axes, with a bump policy and a test that enforces it — §3 | — |

**This is the first phase that costs money.** Nothing before it required GCP to
exist. It is also the first phase in the developer's strongest language rather
than their weakest, which inverts where the risk sits: the unfamiliar surfaces
here are Postgres, RLS, Terraform and Apple's identity flow, not the code.

---

## 2. Five findings the design has to absorb

None appear in any document. All five come from reading `solo_execution` §§4–6
against §8.2's phase list and against what the repository actually contains.

### 2.1 The done-when proves a read while three deliverables are writes

§8.2 names **ledger, wallets and idempotency** among Phase 4's deliverables and
then sets the done-when at *"cold start fetches a real player from a real server
in one call."* That is a `GET`. It exercises Terraform, Cloud Run, Cloud SQL,
RLS, identity and the generated client, and it exercises **none of the three**.

Built to that criterion they are dormant code — written to spec, unit-tested at
best, and first executed for real in Phase 5 against a grant path that is itself
new. §5.3 calls the ledger *"the highest-value small piece of code in the
backend"* and notes that games adding it after launch never fully recover the
first six months. Shipping it untested is a weaker version of the same mistake.

The phase needs one mutating endpoint. The obvious candidate is
`POST /v1/harvest/claim` — §5.5's lazy accrual is pure server arithmetic and
needs no simulation — but it requires a `nodes` row, an Ark-presence association
and `last_settled_at` to read from. **That schema belongs to Phase 6**, which
owns relocation and rotation and would likely reshape it.

> **Resolved: the starter grant at account creation is Phase 4's mutating path.**
> Account creation must write the player's first `wallets` row regardless, and
> §5.3 requires every currency mutation to write a ledger row in the same
> transaction. No new schema, no encroachment on Phase 6, and the idempotency key
> lands on the single most-retried request in the application — first launch, cold
> network, a player who force-quits the spinner and taps again.

**The done-when becomes:** a cold start fetches a real player from a real server
in one call, **and** a repeated account creation carrying the same idempotency
key produces exactly one account, one set of wallet rows and one set of ledger
rows.

### 2.2 `/v1/sync` names a config bundle that nothing produces

§6.2 makes the config bundle version part of sync's response. §8.2 does not list
the publish pipeline among Phase 4's deliverables. As scoped, Phase 4 ships an
endpoint that names a bundle version for a bundle no pipeline creates.

The gap has a second edge. `broodline_phase3_unity_client.md` §1 **cut the Codex
bottom sheet** from Phase 3 on the explicit grounds that *"the config bundle does
not exist until Phase 4's backend, so building it now means hand-rolling a local
config source and throwing it away."* Phase 3 deferred work **into** this phase's
bundle, and this phase's list does not contain it.

> **Resolved: the full four-step pipeline lands in Phase 4, seeded minimally.**
> Author in-repo as JSON, validate at publish time, publish immutably under a
> version, roll back by naming the previous version — §5.2, all four. The bundle
> is seeded with only what exists today: wave definitions and the trait table.

**The validator is the deliverable, not the content.** §5.2's reasoning is that a
bad bundle *"is shipped to every player at once and cannot be recalled by an app
update."* That argues the validator must exist before the first real bundle does,
not before the bundle is large. Content grows into it in later phases at no
additional cost.

The Codex sheet stays deferred — it needs `Broodline.UI` and `Broodline.Model`,
and this phase is otherwise entirely backend — but it is no longer deferred
against something that does not exist.

### 2.3 The config validator would fork game rules into TypeScript

This is the finding that most changes the shape of the work.

§5.2 requires publish-time validation to run *"the invariants the design
documents already state"*, and names three:

| Invariant | Source | Where the rule already lives |
|---|---|---|
| The monotonic pack ladder | `broodline_monetization.md` | Nowhere in code. Pack content does not exist yet |
| The two wave-composition rules | `broodline_combat_engine.md` | **In the engine.** Asserted at wave load and thrown on |
| Every localised key present in every launch locale | `broodline_localization.md` | Nowhere in code. A pure data check |

The publish pipeline is TypeScript. The wave-composition rules — never two
raiders answered by the same trait, never more than four raider types — are C#,
live in the engine, and are already enforced at wave load. Writing them again in
the validator puts a game rule in two languages, which is precisely what §6.1
exists to prevent: *"`api` never parses a replay, never knows what a trait does,
never reimplements a game rule. This is what prevents game logic from quietly
existing in two languages."*

The rule was written about the request path. It applies with equal force to the
publish path, and nothing in the set says so.

> **Resolved: bundle validation shells out to a thin C# CLI over the engine, and
> the pipeline owns only the data checks.** A `Broodline.Config.Validate`
> executable loads every wave definition in a candidate bundle through the
> engine's existing load path and reports what it throws. The TypeScript
> validator owns the pack ladder and the locale-key check, which are data
> properties with no game semantics, and owns the orchestration.

This costs one small project and adds **zero** new game logic. The rules stay in
one place, the same binary the tests already exercise is the one the publish path
trusts, and a future rule added to the engine is enforced at publish without
anyone remembering to mirror it.

It also means Phase 4 is not quite backend-only, and the plan should say so
rather than discovering a C# task in week three.

### 2.4 Three version axes arrive in one phase and nothing relates them

Phase 4 introduces `minimumClientVersion` (§6.2) and the config bundle version
(§5.2). It also inherits the one Phase 3 left owed: **`SimVersion` has no bump
policy**, still reads `0.1.0` after Phase 3's Task 4 moved every hash in the
project, and is read by nothing except the replay's own `EngineVersion` field.

That last point is not cosmetic. `solo_execution` §9.4 states that *"a replay
recorded under a superseded engine version renders its stored outcome and is not
re-simulated."* With the constant never bumped, **every replay claims the current
version**, so the rule is inert: a pre-Rally replay would be silently
re-simulated into a different outcome, which is exactly the failure the rule
exists to prevent. The subsystem §9.4 congratulates itself on deleting was never
actually armed.

Phase 3 booked this correctly — the fix *"needs a decision rather than an edit,"*
and *"deciding what forces a bump and what enforces it belongs with whoever owns
release cadence."* §7.0 puts release cadence in `solo_execution`, and Phase 4 is
where two of the three axes are built.

> **Resolved: all three axes are designed together in this phase, and each gets a
> bump trigger and a mechanical enforcer. §3 is that design.**

Designing them apart is how they drift, and they are genuinely coupled: a bundle
can require a client, a client carries an engine, and a replay carries an engine
version that outlives both.

### 2.5 The gate cannot run

`gh api repos/Sepand-Studio/BroodLine/actions/permissions` returns
`{"enabled": false}`. **GitHub Actions is disabled at the repository level**, so
no workflow has ever executed on this repository — `determinism.yml` and
`tests.yml` are both committed and both have never run. Phase 3 found this and
corrected the workflow header that misdiagnosed it as a missing runner, but the
setting itself was never flipped.

Phase 4 roughly doubles the CI surface: a TypeScript test suite, a Postgres
service container, migration checks, and the §6 contract diff that is supposed to
*fail the build* on a non-empty regeneration diff. Every gate in §8 is
decorative until the setting changes.

> **Resolved: enabling Actions is Task 0 of the implementation plan, before any
> code.** It is a repository setting rather than work, it unblocks the two
> workflows already written, and a gate that cannot execute protects nothing.

Registering the self-hosted macOS runner is *not* pulled in here. It gates the
IL2CPP comparison and the Unity EditMode suite, which Phase 4 does not touch, and
it is real setup work on the developer's machine rather than a toggle.

---

## 3. The three version axes

Each axis answers a different question, and each needs three things stated: what
forces a bump, what enforces it, and what happens on mismatch.

| Axis | Question it answers | Compared by |
|---|---|---|
| **`SimVersion`** | May this replay be re-simulated, or only shown? | Equality |
| **`minimumClientVersion`** | May this app build talk to this server? | Ordering |
| **Bundle version** | Which config is this client rendering against? | Equality, with a fetch on mismatch |

### 3.1 `SimVersion` — bumped with the corpus baseline, enforced by a test

**What forces a bump:** any change that alters engine output for any input.

That set is not a matter of judgment, because Phase 3 already built its detector.
The 500-scenario corpus baseline at `tests/engine/corpus-baseline.txt` is
reproduced byte-for-byte by every run, and a behaviour change is exactly a change
that fails to reproduce it. **A change that requires re-baselining is a change
that requires a bump, and the two sets are identical by construction.**

> **Resolved: the corpus baseline gains a header carrying the `SimVersion` it was
> generated under, and `emit-corpus-baseline.sh` — not a test — refuses to
> re-baseline when the hashes moved and the version did not.** The emitter
> regenerates the whole file from the current engine, so the header it writes is
> always the *current* `SimVersion`; a test comparing the two can never observe
> the failure case, because the file always agrees with itself. The deliberate
> act is running the emitter, so the guard has to live there, comparing the
> committed baseline (`git show HEAD:`) against the freshly emitted one. A test
> keeps the narrower, and checkable, job of catching a bump that forgot to run
> the emitter afterwards. Bumping without re-baselining is harmless and allowed —
> a deliberate bump for a non-behavioural release is legitimate, and the emitter
> reports the baseline as unchanged.

`tests/engine/corpus-baseline.txt` is currently bare `index hash` lines with no
header at all, so this touches three things: `emit-corpus-baseline.sh` writes the
version, `CorpusBaselineTests` reads and asserts it, and the existing 500 lines
are regenerated once under `0.1.0` to acquire the header without changing a
single hash. **That regeneration is the one safe re-baseline** — it must be
proven to alter only the header, and the plan should make that a separate,
reviewable step rather than folding it into a behavioural change.

This is the project's existing idiom rather than a new mechanism: a rule that
depends on discipline fails when the only reviewer is the author at 1am, so it is
compiled in. §7.1 makes the same argument for the banned-API analyzer and the
Cecil scan.

**On mismatch:** a replay whose `EngineVersion` differs from `SimVersion.Value`
renders its stored outcome with a notice and is never re-simulated — §9.4,
finally armed. Phase 3's `DeviceReplayTests` already asserts the equal case, so
the unequal branch is the only new behaviour.

**What this does not do:** it does not version the engine semantically. The only
consumer compares for equality, so the string's shape is a readability choice and
nothing reads its parts.

### 3.2 `minimumClientVersion` — the mechanism, not yet the constraint

**What forces a bump:** a non-additive API change, or a bundle that requires a
client capability older builds lack.

**What enforces it:** nothing mechanical, and that is correct. It is a judgment
call made at release, and the guard against needing it is §7.0's rule that API
and bundle changes are additive.

**On mismatch:** the client blocks with an update prompt. §6.2 is emphatic that
it ships from day one, because *"added after players exist, the players who most
need to upgrade are running the build that cannot be told to."*

**In Phase 4 there is one client build and the constraint is always satisfiable.**
The deliverable is the mechanism and the client's handling of it, proven by a
test that sets the floor above the running build and asserts the block.

### 3.3 Bundle version — immutable, enforced by the publish path

**What forces a bump:** every publish. Bundles are never overwritten.

**What enforces it:** the publish step refuses to write to a version that already
exists in GCS. Rollback is naming the previous version in the server's config, a
change that touches no deploy — §5.2.

**On mismatch:** the client fetches the named version before rendering anything
that depends on it. `client_architecture` §7 requires this specifically, because
*"a stale trait table renders wrong pips, and the pip is the most-repeated
element in the app."*

### 3.4 How they relate

The client holds all three at runtime and they fail independently, which is the
argument for not collapsing them into one number:

- A **bundle** change is live in minutes and needs no App Review.
- A **client** change waits on App Review and phased rollout, which is why
  several client versions are always live at once.
- An **engine** change ships inside a client, so replays outlive the engine that
  made them by exactly as long as retention allows — 30 days, per §9.5.

---

## 4. Schema and isolation

`broodline_data_model.md` owns entity shapes. This section owns only which tables
exist at Phase 4 and how isolation is enforced.

**Seven tables.** Two are global; five are server-scoped and under Row-Level
Security.

| Table | Scope | Notes |
|---|---|---|
| `accounts` | Global | Account id, Apple `sub` (null for a guest), birthdate band, `home_region`, server pointer, `deleted_at`. §5.1 keeps it small because it is the only component that region-partitions later |
| `servers` | Global | Registry. Region, state, and the tick slot **at minute granularity** — §9.7 |
| `players` | `server_id` | The account's presence on its server |
| `wallets` | `server_id` | Balance per currency, with `version` for optimistic concurrency |
| `ledger` | `server_id` | Append-only. Every currency mutation, in the same transaction as the balance write |
| `idempotency_keys` | `server_id` | Key, request hash, stored response. Aged out at 24 hours |
| `campaign_progress` | `server_id` | What `/v1/sync` reports. Minimal — Phase 6 and later own the rest |

**Isolation is RLS with a transaction-scoped setting**, per §5.1. Every request
opens a transaction and calls `set_config('app.server_id', $1, true)`. **The
third argument is the load-bearing one**: it scopes the setting to the
transaction, so it cannot leak across a pooled connection the way `SET
search_path` does. A query missing its `WHERE server_id` returns zero rows rather
than another server's data.

**Drizzle for schema and migrations**, defined in TypeScript with types flowing
into handlers. The C# side never touches Postgres — the config validator at §2.3
reads a candidate bundle from the filesystem, not the database.

**Expand, deploy, migrate, contract.** A schema migration and the code that
requires it never deploy together — §7.0, and the rule that makes rollback
possible.

---

## 5. The starter grant

Phase 4's mutating path, and the shape every later grant follows.

Account creation is one transaction, and the statement order is normative:

1. **Insert the idempotency key first.** On unique violation, return the stored
   response and do nothing else. If the key matches but `request_hash` differs,
   return `422` — that is a client bug, and returning another request's response
   would be worse than failing (§6.3).
2. Insert the account. Assign the server from storefront region, immutably (§4).
3. Insert the player row on that server.
4. Insert the wallet rows.
5. Insert the ledger rows, `reason_code` `STARTER_GRANT`, with `balance_after`
   matching the wallet write.
6. Commit.

**The key is generated when the action is taken, not when it is sent** —
`client_architecture` §8. A retry after a kill, a crash or three days offline
sends the identical key and cannot double-grant.

**The granted amounts come from the config bundle, not from constants.** This is
deliberate coupling: it means the done-when exercises the §2.2 pipeline as well
as the §2.1 ledger, and it means the starter package is tunable without an app
update, which is the first thing live-ops will want to move.

---

## 6. `/v1/sync`

The one cold-start call, per §6.2. It returns settled balances, charges and their
regen timers, active timers, campaign progress, the config bundle version and
`minimumClientVersion`.

**Balances are read from `wallets`, never summed from the ledger** — §5.3, and
the reason the invariant job exists to check them against each other.

**Sync computes lazily and writes nothing.** §5.5 puts settlement writes on
claim, on depletion, or on a raid resolving. In Phase 4 there are no nodes, so
nothing accrues and the lazy-read path has nothing to compute — the shape is
established and Phase 6 fills it.

**The error envelope is `{ code, message, details? }` and the client switches on
`code`, never on message text.** Cheap now, and the alternative is discovered
when the first message is reworded.

**`?since=` deltas are deferred.** §6.2 specifies them for roster and world
reads, and Phase 4 has neither.

---

## 7. The contract

Two contracts generated in opposite directions, per §6. **Phase 4 builds one of
them**; the `api` → `sim` direction arrives with Phase 5.

Zod schemas alongside the handlers, `@asteasolutions/zod-to-openapi` emitting
OpenAPI 3.1, NSwag generating C# DTOs and an `HttpClient` client into
`client/Assets/Generated/Api/`.

Three rules, all from §6, all mechanical:

1. OpenAPI documents are **generated, never hand-written**.
2. Generated clients are **committed**, so a contract change is a reviewable diff
   and Unity builds need no Node.
3. **CI regenerates and fails on a non-empty diff.**

**Three client assemblies are created here, not one.** `client_architecture` §1's
six-assembly layering makes the generated code its own assembly, with two others
above it:

```
Generated.Api     NSwag output — committed, never hand-edited
      ▲
Broodline.Model   player + world state, cached from /v1/sync
      ▲
Broodline.Net     HTTP, auth, the outbox — references Model, Generated.Api
```

Phase 3 deliberately created none of them. The done-when requires the *client* to
make the cold-start call, so all three land — `Model` minimally, holding the sync
snapshot `client_architecture` §7 requires the client to cache, and nothing more.
`Broodline.UI` stays absent, which is what keeps the phase backend-shaped.

**The outbox is `Net`'s, and it is not built here.** `client_architecture` §8
scopes it to durable queued *mutations*, and Phase 4's only mutation is account
creation, which cannot be queued — a player with no account has nothing to queue
against. The assembly exists; the outbox arrives with the first queueable
mutation in Phase 6.

---

## 8. Testing

Five layers. The two adversarial suites are the gate, and they are the phase's
equivalent of Phase 2's corpus baseline: generated cases, binary pass/fail,
failing loudly and automatically.

| Layer | Covers | Runs |
|---|---|---|
| **TS unit** | The validator's data rules — pack-ladder monotonicity, locale-key completeness. Pure functions | Every push |
| **Isolation suite** | **Gate.** Two `server_id`s seeded across every server-scoped table; every query asserts zero cross-server rows. Separately, that `set_config`'s transaction scope holds — the setting must not survive a pooled checkout | Every push |
| **Idempotency suite** | **Gate.** N parallel requests carrying one key produce exactly one account, one wallet set, one ledger set. The mismatched-`request_hash` case returns `422`. The §5.3 invariant job, run as a test, finds zero drift | Every push |
| **Contract diff** | Regenerate the OpenAPI document and the NSwag client; fail on a non-empty diff | Every push |
| **Cold-start round-trip** | **The done-when.** A deployed Cloud Run revision, a real Cloud SQL instance, one call returning a real player — and a repeated creation granting exactly once | Manual, per deploy |

**Every database test runs against real Postgres — never a mock, never SQLite.**
RLS, `set_config` scoping, unique-violation semantics and transactional behaviour
are the properties under test, and all four are precisely what a substitute
fakes. Testcontainers locally, a service container in CI.

**Why these two suites and not others.** §5.1 says cross-server leakage is
*"nearly invisible in testing with one server and catastrophic with fifty"* — it
cannot be caught by observation, only by a test that deliberately creates the
second server. And idempotency's failure mode is a duplication exploit, which
§3.1 names as one of the two failures that get worse the longer they run. Both
are properties that single-path testing passes while broken.

---

## 9. What this design deliberately does not do

- **No `sim` service and no submit-and-verify.** `Sim.Replay` has the shape it
  will take, from Phase 3. Wiring it, server-issued seeds and replays to GCS are
  Phase 5's, per §8.2.
- **No nodes, regions, harvest or splice.** Phase 6. §2.1 declines to pull the
  node schema forward for the sake of a test, and uses the starter grant instead.
- **No second deployable.** `commerce`, `social`, `stream` and `notify` are
  modules in `api` until their §10 triggers fire, and none fires in this phase.
- **No Redis, no PgBouncer, no HA.** All three have named triggers at §10 and
  none is measured yet. The cheap version of the connection problem is a hard
  `--max-instances` cap plus a small per-instance pool, which costs one flag.
- **No `Broodline.UI`, and no Codex sheet.** `Generated.Api`, `Model` and `Net`
  land because the done-when needs the client to make the call — §7 — but the
  screen layer does not. The bundle the sheet was deferred against now exists,
  which is what §2.2 closes; building the sheet is a client phase's work.
- **No outbox.** Phase 4's only mutation cannot be queued — §7.
- **No push, no APNs.** §10's trigger is raid alerts, which are Phase 4 of the
  *build order*, not of this milestone.
- **No region partitioning.** `home_region` and the routing rule exist from this
  phase, per §10; the partition itself waits on a committed non-US market.
- **No LiveOps console.** Config is authored in-repo as JSON and reviewed as a
  diff. The console replaces that step later and publishes through the same path.

---

## 10. Decisions owed

Four. Two are Phase 3's debts, two are this phase's. None blocks the work.

| Decision | Why it matters | State |
|---|---|---|
| **Correct `client_architecture` §2's `ref readonly SimState`** | Still present at line 52, still specifying a shape that guarantees nothing — `SimState` is a sealed class whose readonly arrays hold mutable contents. Phase 3 resolved it in code and booked the document edit | **Owed since Phase 3.** Cheap, and this phase touches `client_architecture` §§7–8 anyway |
| **Re-run `broodline_supersession_map.md`** | It accounts for none of `plans/` — not `solo_execution`, not `client_architecture`, not the two phase designs. §9.9 asked for this and it has not happened. "Not in the map" is weak evidence a document is unratified, and that ambiguity is how two documents end up owning one rule | **Owed since `solo_execution`.** Growing by one document per phase |
| **Record §2.3's publish-path rule into `solo_execution` §5.2** | §6.1 forbids game logic in two languages on the request path. §5.2's validation list silently requires it on the publish path. The rule generalises and the document that owns both should say so | **New.** Owner: `solo_execution` |
| **Record §3.1's `SimVersion` policy into `solo_execution` §9.4** | §9.4 states the superseded-replay rule and nothing states what makes a replay superseded. The policy is this document's; the rule's home is there | **New.** Owner: `solo_execution` |

---

*Owns: the Phase 4 slice boundary, the starter grant as the phase's mutating
path, the config validator's language split, the three version axes and their
bump policies, the Phase 4 table set, and the two adversarial suites as the
phase's gate. Does not own: deployables, storage mechanics, the language boundary
or account lifecycle (`broodline_solo_execution.md`), entity shapes or the
authority split (`broodline_data_model.md`), population, assignment or the tick
(`broodline_server_topology.md`), anything inside the app
(`broodline_client_architecture.md`), or phase sequencing
(`broodline_solo_execution.md` §8.2).*
