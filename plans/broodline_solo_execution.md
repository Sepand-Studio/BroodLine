# Broodline — Solo Execution Plan

*Technical spec, how one person builds this*

> **PROPOSED — not yet in the supersession map.** Owns the execution shape:
> deployables, repository layout, storage mechanics, the language boundary, and
> milestone scoping. Does not own entity shapes (`broodline_data_model.md`),
> the simulation (`broodline_combat_engine.md`), the client
> (`broodline_client_architecture.md`), server population
> (`broodline_server_topology.md`), or the production plan
> (`broodline_build_order.md`). Where this document appears to contradict any
> of those, they win and this is a bug — except at §9, which records eight
> decisions taken deliberately against them, and the edits those decisions owe
> to other documents.

**Two findings.** The technical architecture the project has been carrying assumes a studio, and nine of its decisions invert for a single developer — §3. And the supersession map is fifteen documents behind, which left two unmapped technical specs contradicting mapped ones on load-bearing rules — including whether every wave is validated. All eight conflicts are resolved at §9; five of them require edits to documents this one does not own, listed at §9.9.

---

## 1. Why this document exists

`broodline_build_order.md` is the production plan and it is correct. It assumes a team: someone rigging while someone builds the combat engine while someone builds the Codex sheet. Its Phase 1 has three parallel tracks.

There is one developer, working with Claude, with no prior C#, Unity or .NET experience and fluency in TypeScript.

That does not change the design and it does not change the build order's dependency graph. It changes **how many things can be in flight, how many deployables are worth operating, and which infrastructure is worth building before it is needed.**

The test applied throughout: *would getting this wrong now cost a rewrite later?* If yes it is decided and built now, even for a TestFlight with six players. If no it is deferred against a named trigger — §8.

### Constraints

| | |
|---|---|
| Team | One developer + Claude |
| Existing fluency | TypeScript / Node. No C#, Unity or .NET |
| Platform | iOS and iPadOS, portrait-locked |
| Cloud | Vercel and GCP |
| Launch | US, English. Multi-region and multi-language later per `broodline_localization.md` |
| Design | Fixed. This document does not re-open it |
| First milestone | Server-authoritative vertical slice on TestFlight — §7 |

---

## 2. Engine and language

**Unity 6, C#, decided.** It follows from `broodline_rig_proof.md`: modular attachment points across dissimilar bodies is a rigging problem with a paved road in Unity and a hand-built one everywhere else.

The cost is real and worth stating. Learning C#, the Unity editor and .NET simultaneously puts the hardest problem in the project — bit-identical determinism — in the least familiar language. Two things mitigate it:

- **The simulation core is pure C# with no editor**, so it is the on-ramp. `broodline_build_order.md` Phase 1 already puts the combat engine first for dependency reasons; it happens also to be the best place to learn the language.
- **A fraction of Unity work happens in an editor Claude cannot operate** — prefab wiring, import settings, animator state machines. That tax is real. The larger GUI cost is art, and art is engine-independent.

**The backend is TypeScript.** C# is confined to the Unity client, the simulation core, and the thin service that hosts it. This keeps the largest surface in the language the developer already has, at the cost of a generated contract across the boundary — §6.

---

## 3. Deployables

The retired `broodline_technical_architecture.md` proposed five services — §9.1. **Two.**

| | |
|---|---|
| **`api`** | TypeScript. Player, world, raid, social, commerce, notify and stream as modules in one container |
| **`sim`** | C#. Hosts the combat engine headless. Internal ingress only |

Each deployable costs a build pipeline, a secret set, a log stream and a dashboard, and one person pays that cost *n* times. `sim` stays separate because it is a different language and a different scaling curve.

**The module seams are real and split on triggers, not on principle:**

| Module leaves `api` when | |
|---|---|
| `commerce` | The first line of receipt verification is written — that is when Apple and Stripe secrets enter the system, and secret isolation is the actual argument. Ruleset in `broodline_store_iap.md` |
| `social` | Player-generated text ships. `broodline_moderation_ugc.md` owns why |
| `stream` | Alliance chat or live territory state ships |
| `notify` | Raid alerts ship — the ninety-second window in `broodline_collectors_raiding.md` is meaningless without push |

Until then commerce reaches the rest of the system through exactly two things — `grantEntitlement()` and the ledger — and nothing imports its internals. If that holds, splitting is `git mv` plus a Dockerfile.

### Runtime

**Cloud Run, not Kubernetes.** The workload is stateless request/response plus scheduled jobs. Revisit only if synchronous PvP ships or a shard needs an in-memory world that cannot be reconstructed per request.

```
   iOS / iPadOS ──► Cloud Load Balancing ──► ┌──────────────┐
      (Unity 6)                              │     api      │  TypeScript
                                             └──┬───────┬───┘
                                    ┌───────────▼──┐  ┌─▼──────────┐
                                    │  Cloud SQL   │  │    sim     │  C# / .NET
                                    │  Postgres    │  │ Cloud Run  │  internal only
                                    │ (one server) │  └─────┬──────┘
                                    └──────┬───────┘        │
                                    ┌──────▼────────────────▼───┐
                                    │ GCS — replays · config    │──► BigQuery (later)
                                    │       bundles · telemetry │
                                    └───────────────────────────┘

   Vercel: marketing and support. LiveOps console and web shop later.
           No gameplay traffic, ever.
```

**No gameplay traffic touches Vercel.** Not a proxy, not an edge cache, not one endpoint. The moment a Vercel function reads authoritative game state there are two backends with two deployment stories and a split-brain nobody remembers to test.

Estimated cost before players: **$25–50/month.** Cloud Run scales to zero; the Postgres instance is the only always-on component.

### 3.1 Reliability and observability

`broodline_telemetry.md` is **design** telemetry — its own scope line is "the seven playtest questions as events, metrics and thresholds." Nothing in the set owns operational monitoring, so it is owned here.

**Service level objectives.** Three, and only three, because a solo developer cannot act on more:

| | |
|---|---|
| `GET /v1/sync` | p99 under 300 ms — it is on every cold start |
| Wave submission end to end | p99 under 500 ms, including re-simulation |
| Raid alert delivered | within 5 s at p95 |

The third is a **gameplay requirement, not an ops preference.** `broodline_collectors_raiding.md` gives the defender a ninety-second window from the push; delivery that eats twenty of them changes the game.

**Graceful degradation, decided in advance rather than at 2am:**

| If this is down | Then |
|---|---|
| `sim` | Campaign submissions queue and reward optimistically, reconciled on recovery. Bounded exposure, and the player feels nothing |
| Push delivery | Raids auto-resolve without an alert; the defender gets the replay and a mail apology |
| Live state / chat | Degrades to polling |
| `api` | Nothing about this is graceful. It is the one service with a real SLO and the one that must not be down |

**What is watched.** Cloud Run request latency and error rate per route, Cloud SQL connection count against the cap, the ledger-versus-wallet invariant job (§5.3), simulation hash-mismatch rate, and the nightly determinism diff. **Alert on the invariant job and the mismatch rate**; graph everything else. A duplication exploit and a determinism drift are the two failures that get worse the longer they run.

**Deploy safety.** With one server there is no canary population, so the substitutes are: deploy behind a health check, keep the previous Cloud Run revision one command away, and never deploy a schema migration and the code that depends on it in the same step. Once a second server exists, canary-by-server is available for free and is the strongest release tool the architecture offers.

---

## 4. Servers, and how many to run

`broodline_server_topology.md` owns population, lifecycle, assignment and the tick. This document owns only how much of it is *built* at milestone 1.

**Run one server. Shape the schema for many.**

| Built now | Deferred |
|---|---|
| `server_id` leading every primary key and index | Provisioning automation and a warm pool — trigger: second server |
| Server registry table with tick slot | Population-trigger opening — trigger: second server |
| No globally meaningful IDs, so a merge is a re-keying exercise | Merge tooling — trigger: first server decaying below its band |
| Assignment by storefront region at signup, immutable | — |

The schema shape is the expensive part to retrofit and it costs nothing now. Provisioning automation is a launch-spike problem, and TestFlight has no launch spike.

**Assignment and transfers follow `broodline_server_topology.md` §5 exactly: assignment by storefront region at signup, no transfers, ever.** A player wanting to play elsewhere creates a second unlinked account. This also closes the guest-account question the architecture document left open — assignment is not something a client can influence, so a guest cannot reroll onto a low-population server to farm its Apex Veins.

**Tick slot is one of the three fixed slots in `broodline_server_topology.md` §5**, chosen at creation and never changed.

**Multi-region is nearly free, provided one decision is made now: servers are region-pinned, and account data is region-partitioned from day one even while only the US exists.**

| Layer | US launch | EU | APAC |
|---|---|---|---|
| Servers and their data | `us-central1` | `europe-west1` | `asia-northeast1` |
| Account / identity | `us-central1` | `europe-west1`, EU residency | home region + replica |
| Commerce | `us-central1` | routes by account region | routes by account region |
| Telemetry | pseudonymised, single region | EU raw retained in EU | same |
| Static content and CDN | global | global | global |

**Data residency is the reason to build the partition before it is needed.** Once EU players exist, moving their data into the EU is a migration under legal pressure. A `home_region` column and a routing rule written now cost nothing — and `broodline_localization.md` §9 already fixes per-jurisdiction age thresholds, which is the same partition seen from the policy side.

The account record is the only genuinely global component, which is why §5.1 keeps it small.

**Store it at minute granularity, not hour.** Every server in a region ticking at the same minute makes the weekly tick simultaneously the heaviest scheduled job and the largest push fan-out in the game. Spreading servers across the first half hour of their slot flattens that spike and is invisible to players, because nothing in the game is cross-server. Storing an hour and needing minutes later is a migration; storing minutes and only ever using `:00` costs nothing — §9.7.

---

## 5. Storage

`broodline_data_model.md` owns entity shapes, relationships, retention and the authority split. Everything below is storage mechanics only.

### 5.1 Isolation

**One Postgres schema. `server_id` leads every primary key and index. Isolation is enforced by Row-Level Security.**

```sql
ALTER TABLE creatures ENABLE ROW LEVEL SECURITY;
CREATE POLICY server_isolation ON creatures
  USING (server_id = current_setting('app.server_id')::int);
```

Every request opens a transaction and calls `set_config('app.server_id', $1, true)`. **The third argument scopes the setting to the transaction**, so it cannot leak across a pooled connection the way `SET search_path` does.

A query missing its `WHERE server_id` returns zero rows rather than another server's data. Cross-server leakage is nearly invisible in testing with one server and catastrophic with fifty.

The alternative — a schema per server — is what a fleet wants and is a live footgun for one developer, because every pooled connection needs its `search_path` set and reset correctly on every checkout.

**Identity is the one table outside this.** Per `broodline_data_model.md` §1 the Player is global; the account record — account id, credential binding, birthdate band, home region and its server pointer — carries no `server_id` and is not under the policy. Keep it small: it is the only component that becomes region-partitioned when the first non-US market lands.

**Forward path:** nothing joins across servers, so a second database is created and whole servers move by copying rows where `server_id = X`. If physical isolation is later wanted, `PARTITION BY LIST (server_id)` is available and every unique index already leads with `server_id`.

### 5.2 What is content and what is data

Per `broodline_data_model.md` §7 and `broodline_server_topology.md` §7, **most of the map is content.** Region definitions, terrain family, adjacency, gate pairings, lane counts, species weighting and band membership ship with the build and are identical on every server. Only node state, controller and Ark presence are stored.

**A server's entire map state is a few kilobytes.** Storing region definitions per server would be a data migration every time a region is tuned.

This extends to the rest of the config surface. Event definitions, pack contents, trait tables, wave definitions and localised strings are a **versioned JSON bundle on GCS behind CDN**, not an API. `/v1/sync` returns the required bundle version; the client fetches and caches it. Live-ops changes stay off the API path and are CDN-cheap at any scale.

**The publish pipeline, which the bundle format implies and nothing else owns:**

1. **Authored in-repo as JSON**, reviewed as a diff. The LiveOps console replaces this step later — §10 — and publishes through the same path
2. **Validated at publish time**, not at read time. The client must never receive a bundle it cannot parse, because a bad bundle is shipped to every player at once and cannot be recalled by an app update
3. **Published immutably under a version**, never overwritten. `/v1/sync` names the version; rollback is naming the previous one
4. **Rollback is a config change**, not a deploy

**Validation runs the invariants the design documents already state**, as tests over the bundle:

- The **monotonic pack ladder** from `broodline_monetization.md` — value per dollar must never decrease as pack size rises. This was broken once by hand; the machine should enforce it
- `broodline_combat_engine.md`'s **two wave-composition rules** — never two raiders answered by the same trait, never more than four raider types. The engine asserts these at wave load, so catching them at publish turns a runtime throw into a failed publish
- Every localised string key present in every launch locale, per `broodline_localization.md`

A bundle that fails validation is not published. That is the entire safety model, and it is worth more than a console.

### 5.3 The ledger

**Every currency mutation writes an append-only ledger row in the same transaction as the balance update.** Shards, Splice Charges, Marks, premium currency, all of it.

```
ledger (server_id, entry_id, player_id, currency, delta, balance_after,
        reason_code, ref_type, ref_id, idempotency_key, created_at)

wallets (server_id, player_id, currency, balance, version)
```

Balances live in `wallets` with a `version` column for optimistic concurrency, written in the same transaction as the ledger insert. **Balances are never derived by summing the ledger at read time.**

**The invariant job:** a scheduled task sums `ledger.delta` per `(player_id, currency)` and compares to `wallets.balance`. Drift is either a bug or a duplication exploit and warrants same-day attention. Roughly twenty lines.

This is the highest-value small piece of code in the backend. It answers "where did my shards go", feeds `broodline_economy_model.md`'s faucet and sink tuning without separate instrumentation, and makes refunds reconstructible rather than guessed. Games that add it after launch never fully recover the first six months.

### 5.4 Concurrency

Player-owned state is single-writer by nature. Optimistic concurrency with a `version` column, compare-and-set, retry once, surface a conflict on the second failure. No locks.

**Node depletion is the one genuinely contended counter**, and `broodline_server_topology.md` §2 requires it to track total extraction scaled by harvester count, bounded by a 24-hour floor and the weekly tick. Atomic in one statement:

```sql
UPDATE nodes
   SET remaining_yield = GREATEST(remaining_yield - $extraction, 0)
 WHERE server_id = $s AND node_id = $n
RETURNING remaining_yield, depletes_at;
```

Correct under any concurrency, with less throughput than a Redis counter and no other difference. **Memorystore is deferred** until contention is measured; it slots in behind the same function with a flush to Postgres on an interval and on depletion crossing.

`broodline_server_topology.md` §9.2 leaves open what counts as a harvester — a parked Ark or an actively accruing one. That is a design question and it changes this statement's input, not its shape.

### 5.5 Accrual is lazy

**Never tick players.** Harvest is computed on read from `last_settled_at`, the node's current rate, the Harvest Array multiplier and the 12-hour offline cap. Settlement writes happen on claim, on depletion, or when a raid resolves against the convoy.

Fifty thousand sleeping players cost zero background CPU, and no background job fleet is ever required.

### 5.6 Retention

Two retention rules from `broodline_data_model.md` are storage-shaped and must exist in the schema from the first migration, because retrofitting either means reprocessing every row:

- **Lineage:** retain ancestors five generations deep, retain all Founders permanently, prune everything else to a tombstone `{id, species, generation, was_founder}`. Creature ids are never reused, including for pruned records. This is the difference between ~300 and ~9,000 records per player.
- **Replays:** 30 days rolling, up to 20 pinned per player. **The CI corpus is a separate collection** with engineering-owned retention, seeded with generated waves rather than harvested from players — §9.5. Two collections over one file format, two policies.

### 5.7 Migrations and backups

**Drizzle** for schema and migrations — defined in TypeScript, types flowing into `api` handlers, migrations generated and committed. The C# side never touches Postgres.

**Cloud SQL automated backups and point-in-time recovery from day one.** Cheap at this size, and with the ledger intact economy state is reconstructible even from an imperfect restore.

**PgBouncer is deferred.** Cloud Run scales to hundreds of instances and each opens a pool; Postgres runs out of connections long before CPU. The cheap version of that fix is a hard `--max-instances` cap plus a small per-instance pool, which costs one flag. PgBouncer arrives when the cap throttles real traffic.

---

## 6. The language boundary

Two contracts, generated in opposite directions, because a hand-maintained cross-language contract rots when one person maintains both sides.

**Unity → `api`.** TypeScript is the source of truth. Zod schemas defined alongside handlers; `@asteasolutions/zod-to-openapi` emits OpenAPI 3.1; NSwag generates C# DTOs and an `HttpClient` client into `client/Assets/Generated/Api/`.

**`api` → `sim`.** C# is the source of truth. ASP.NET's OpenAPI generation emits the document; a TS generator consumes it. Small, because the boundary is deliberately narrow.

Three rules:

1. OpenAPI documents are **generated, never hand-written**
2. Generated clients are **committed**, so a contract change is a reviewable diff and Unity builds need no Node
3. **CI regenerates and fails on a non-empty diff**

### 6.1 `api` does not understand simulations

```
POST /internal/simulate
  → { engineVersion, seed, waveId, terrain, deployment, rally }
  ← { outcome, integrityRemaining, breachDiagnoses[], outcomeHash }
```

The request shape follows `broodline_combat_engine.md` §3. `api` maps the result to ledger entries and never parses a replay, never knows what a trait does, never reimplements a game rule. **This is what prevents game logic from quietly existing in two languages.**

Raid auto-resolve is the same endpoint with no rally input, per `broodline_combat_engine.md` §2.1.

**Every submission is re-simulated — campaign, region defence and raid alike. Nothing is sampled.** One validation path rather than two, and less code than a plausibility model plus an anomaly heuristic plus a re-simulation escalation. At roughly 20 ms per wave this is under two CPU-hours a day at 50k DAU, and at milestone-1 scale it is free. This supersedes `broodline_data_model.md` §8's client-authoritative treatment of campaign waves — §9.3.

Submissions made offline queue and validate on reconnect, granting optimistically. An honest client always validates, so a clawback only ever touches a modified one.

### 6.2 API shape

- **Intent endpoints, not CRUD:** `POST /v1/splice`, `POST /v1/wave/start`, `POST /v1/wave/submit`, `POST /v1/harvest/claim`, `POST /v1/ark/relocate`
- **`GET /v1/sync`** — the one cold-start call. Settled balances, charges and regen timers, active timers, campaign progress, config bundle version, and `minimumClientVersion`
- **`?since=<cursor>`** deltas on roster and world reads
- **Error envelope** `{ code, message, details? }`. The client switches on `code`, never on message text

`minimumClientVersion` ships from day one. Added after players exist, the players who most need to upgrade are running the build that cannot be told to.

### 6.3 Idempotency

```
idempotency_keys (server_id, key, request_hash, status,
                  response_body, created_at)
                  PRIMARY KEY (server_id, key)
```

The key is inserted as the first statement inside the same transaction as the mutation. On unique violation the stored response is returned. If the key matches but `request_hash` differs, return `422` — that is a client bug, and returning another request's response would be worse than failing. Rows age out after 24 hours.

Retry therefore means *resend the identical request with the identical key*, with no client-side reasoning about whether the first attempt landed. Mobile networks are always flaky.

### 6.4 Identity

**Sign in with Apple** as primary, guest path that upgrades, per `broodline_build_order.md` Phase 2's account creation and age gate. `api` issues a short-lived access JWT and a refresh token held in the iOS Keychain. Guests are real accounts with no credential bound; upgrading binds the Apple `sub` to the existing account, so nothing migrates.

Server assignment happens once at account creation from storefront region and is immutable — §4.

**Account lifecycle, which nothing else in the set owns:**

- **Deletion is a product requirement, not a nicety.** The App Store requires an in-app path to account deletion for any app that supports account creation. It is a soft delete plus a scheduled purge: the account is disabled immediately, player-visible data is removed on a timer, and the **ledger is retained in pseudonymised form** because it is a financial record and because `broodline_store_iap.md`'s refund path depends on it. Creature tombstones (§5.6) survive too — a deleted player's descendants still render in other players' lineage views, which is exactly why tombstones are minimal
- **Recovery is Apple's problem, deliberately.** Sign in with Apple means there is no password to reset and no recovery flow to build or abuse. A guest account that was never bound is **unrecoverable**, and the FTUE must say so before it matters
- **Device migration is sign-in, nothing more.** No state lives on the device that is not reconstructible from `/v1/sync` — see `broodline_client_architecture.md` for what the client is permitted to cache
- **Guest upgrade never migrates data.** Binding the Apple `sub` to the existing account id means there is no merge, and therefore no merge conflict. A player who signs in with an Apple ID already bound to another account is offered that account, not a merge

---

## 7. Repository layout

Monorepo. One person, tightly coupled contracts, generated code crossing language boundaries.

```
broodline/
├─ client/                        Unity 6 project
│  ├─ Assets/
│  │  ├─ Game/                    gameplay, view layer, UI
│  │  ├─ Generated/Api/           NSwag output — committed, never hand-edited
│  │  ├─ Art/                     models, textures, animations   (Git LFS)
│  │  └─ Tests/                   Unity Test Framework — view layer only
│  ├─ Packages/manifest.json      → "file:../../engine"
│  └─ ProjectSettings/
│
├─ engine/                        the combat engine — one source of truth  (§9.2)
│  ├─ package.json                UPM manifest — how Unity sees it
│  ├─ Broodline.Sim.csproj        netstandard2.1, zero dependencies
│  └─ Runtime/
│     ├─ Broodline.Sim.asmdef     zero references → UnityEngine unavailable
│     └─ Fix64 · Rng · World · Tick · Replay · Hash
│
├─ services/
│  ├─ api/                        TypeScript — Hono on Cloud Run
│  └─ sim/                        C# — ASP.NET minimal API → ProjectReference engine
│
├─ tests/engine/                  xUnit: golden · fuzz · corpus · IL scan
├─ tools/batch/                   headless batch runner  (§7.2)
├─ config/                        versioned JSON bundles → GCS
├─ infra/terraform/
├─ Broodline.sln                  engine + sim service + tests + batch runner
└─ pnpm-workspace.yaml
```

**Shared source, not a compiled DLL.** `engine/` carries two manifests over one source tree: Unity consumes it as a local UPM package, .NET consumes it through `Broodline.Sim.csproj`. No copying, no rebuild-and-copy step, no stale-artifact bug class, and full source stepping in both debuggers. The guarantee that the engine cannot reach `UnityEngine` comes from the empty `.asmdef`, which holds either way — and the DLL's apparent benefit of a single shared artifact does not survive IL2CPP, which transpiles the IL to native C++ on device regardless. Determinism comes from the rules and the CI diff, not from artifact sharing — §9.2.

**Two solutions, deliberately.** Unity generates and overwrites `client/*.sln` and `client/*.csproj`; those are gitignored. The root `Broodline.sln` contains only the engine, the sim service, the tests and the batch runner.

**Git LFS from the first commit.** Retrofitting LFS means rewriting history, and `broodline_build_order.md` puts thirty-six character assets and twenty animation clips in the plan.

- LFS: `*.fbx *.png *.tga *.psd *.jpg *.wav *.mp3 *.ogg`
- Not LFS: `*.prefab *.asset *.anim *.controller *.unity` — YAML, and they must stay diffable

Requires **Asset Serialization: Force Text** and **Version Control: Visible Meta Files** from the start. `.meta` files are committed.

**CI paths.** Workflows filter by path. Touching `services/api/` does not rebuild Unity. Touching `engine/` triggers everything.

### 7.0 Release

Three artifacts ship on three different cadences, and they must be able to move independently or the slowest one gates the others.

| Artifact | Cadence | Gated by |
|---|---|---|
| Config bundle | Any time | Publish-time validation — §5.2 |
| `api` and `sim` | Any time | Tests, then a health-checked Cloud Run revision |
| The client | App Review | TestFlight, then phased release |

**The client is the slow one, and everything else is designed around that.** Config is a bundle so live-ops does not wait on App Review. `minimumClientVersion` in `/v1/sync` — §6.2 — is what lets a server change outrun a client that cannot be updated in time.

**The server must tolerate old clients**, because App Review plus phased rollout means several client versions are live simultaneously. That is what versions the API (`/v1`) and what makes additive-only bundle changes a rule rather than a preference.

**Never deploy a schema migration and the code that requires it together.** Expand, deploy, migrate, contract — the same discipline that makes rollback possible.

### 7.1 Determinism enforced by tooling

`broodline_combat_engine.md` §2 states the determinism rules. Rules that depend on discipline fail when the only reviewer is the author at 1am, so all of them are compiled in:

- **`BannedApiAnalyzers`** with a `BannedSymbols.txt` covering `System.Random`, `DateTime.Now`/`UtcNow`, `Guid.NewGuid`, `Environment.TickCount`, all of `System.Math`, all of `System.Linq`, `Dictionary`, `HashSet`, and `string.GetHashCode`. Build errors, not review comments.
- **A Mono.Cecil test** that opens the compiled assembly and fails if any `float32` or `float64` appears in any signature, field, local or instruction.
- **Zero project references** on `Broodline.Sim.csproj`, so `UnityEngine` is unavailable by construction.

`string.GetHashCode` warrants naming: it is randomized per process on CoreCLR and **not** randomized on Unity's Mono/IL2CPP, so it is guaranteed to disagree between the two runtimes, and it looks completely innocent.

**Every comparator must produce a total order.** `Array.Sort` is unstable and its introsort is not guaranteed identical across runtimes, so equal keys can order differently on CoreCLR and IL2CPP. `broodline_combat_engine.md` §6 already sets the rule — **ties break on spawn index, ascending** — and it applies everywhere, not only in targeting. String comparison is ordinal-only.

### 7.2 The batch runner is part of the engine

The strongest argument in the retired `broodline_sim_core.md`, and one of the five sections merged into `broodline_combat_engine.md` per §9.1: **trait utility balance is not answerable by argument.** Carapace, Litter, Regrow and Screen counter nothing, and "what is Regrow worth" becomes a measurement the moment the engine runs headless over a fixed wave set.

It is also how sixty authored waves get tuned without playing them sixty times, and how `broodline_playtest_tuning_sheet.md` gets its inputs. Build it with the engine, not as a later tool.

### 7.3 The determinism gate, scaled to one person

The retired `broodline_sim_core.md` §9 called for a nightly corpus replay against an **iOS device-farm build**. A device farm is not fundable or maintainable by one developer.

**Replace it with a self-hosted runner on the developer's own Mac**, diffing the corpus against a CoreCLR build and a **macOS ARM64 IL2CPP standalone player**. Same IL2CPP compiler, same ARM64 target, different OS and ABI — it catches IL2CPP codegen drift, which is the class of bug that matters.

It is a proxy, not proof. **Run the corpus on a physical iPhone by hand before each release** to close the gap.

Hosted macOS CI runners bill at a 10× minute multiplier; a self-hosted Mac is free and, usefully, already carries an activated Unity licence — license activation on ephemeral runners is a known source of flaky builds.

**Corpus storage:** a small seed corpus (~50 replays) is committed so `dotnet test` is fast and offline. The full corpus — 5,000 generated waves spanning every archetype, Instinct, coverage tier and legal composition — lives in GCS and is pulled by the nightly job. Replays that ever caught a drift are promoted into the committed set by hand.

**Intermediate hashes matter more than terminal ones.** A divergence that self-corrects before the end still means the player watched a different fight than the server recorded.

---

## 8. Milestone 1 — the vertical slice

`broodline_build_order.md` owns the production plan. This section states only what the first milestone is and where it deviates.

**Sign in → land on a server → harvest a node → splice a creature → fight a wave the server validates → get paid through the ledger.**

**Content:** one species, one region, two node types, five waves, and enough traits that splicing has a real outcome space rather than returning a copy.

### 8.1 Where this deviates from the build order, and why

`broodline_build_order.md` Phase 1 is three parallel tracks — rig two species, build the combat engine, build the Codex sheet — and no backend appears until later. That is right for a team.

Two deliberate deviations:

**The backend arrives earlier.** The milestone is explicitly *server-authoritative*, because server authority and the ledger are the two things in `broodline_data_model.md` §8 that cannot be retrofitted. Building the loop client-only and adding authority later means rewriting every grant path.

**Two proofs run first, not in parallel with anything.** `broodline_build_order.md` calls it a gate rather than a milestone: if two dissimilar bodies cannot carry the same twelve trait parts in either socket at acceptable quality, the twenty-four-asset budget is wrong and the art plan changes before money is spent. One developer cannot run it in parallel with engine work, and a failed gate discovered late is more expensive than a serialised one.

### 8.2 Order

| Phase | What lands | Done when |
|---|---|---|
| **0. Two proofs** | `broodline_rig_proof.md` — Vetch and Pale, twelve parts, both sockets. **And the entity-count proof** — wave 44's ~100 entities at target frame rate on the oldest supported device, per `broodline_client_architecture.md` §4 | Both pass/fail recorded against their documents' criteria. Either failure changes the art budget or the renderer, and both are cheaper to find now |
| **1. Foundations** | Repo, LFS, Unity settings, `engine` project, banned-API analyzer, Cecil scan, xUnit harness, self-hosted CI | A toy sim passes golden and fuzz on CoreCLR and IL2CPP |
| **2. Combat engine** | One lane, one raider, five pockets, the counter check, tick order, termination, replay format, batch runner | A 90-second wave simulates identically twice; corpus test runs |
| **3. Unity client** | Renderer for `SimState`, interpolation, input capture, placeholder art, Codex bottom sheet | A wave played on device replays bit-identically in xUnit |
| **4. Backend spine** | Terraform, Cloud Run, Cloud SQL, RLS, ledger, wallets, idempotency, Sign in with Apple, `/v1/sync`, generated client | Cold start fetches a real player from a real server in one call |
| **5. Validation** | `sim` on Cloud Run, server-issued seeds, submit-and-verify, replays to GCS | A tampered submission earns nothing; an honest one pays exactly once under retry |
| **6. The loop** | Region, nodes, lazy accrual, claim, server-rolled splice, splice confirmation per `broodline_splice_confirm_spec.md` | Harvest → splice → fight → reward closes without leaving the app |
| **7. Slice polish** | One species of real art, FTUE beats from `broodline_build_order.md` Phase 2, `minimumClientVersion`, offline retry queue | TestFlight build in someone else's hands |

Nothing before Phase 4 requires GCP to exist. The cloud bill is zero for the first half of the project.

### 8.3 Timing

| Phase | Full-time | Nights & weekends |
|---|---|---|
| 0. Two proofs | 3–4 weeks | 8 weeks |
| 1. Foundations | 2 weeks | 5 weeks |
| 2. Combat engine | 6–8 weeks | 4–5 months |
| 3. Unity client | 6–8 weeks | 4–5 months |
| 4. Backend spine | 3–4 weeks | 2 months |
| 5. Validation | 2 weeks | 5 weeks |
| 6. The loop | 3 weeks | 7 weeks |
| 7. Slice polish | 4–6 weeks | 3 months |
| **Total** | **6.5–8.5 months** | **19–21 months** |

Phase 2 is far wider than the "one engineer for about a week" the retired `broodline_sim_core.md` §12 budgeted for its first two steps, because that estimate assumes an engineer who already writes C#. Phase 3 carries the Unity editor learning curve.

**Phase 7 is the least predictable and the variable is art, not code.** Shipping to friends on placeholder art saves roughly two months and proves the spine just as well.

### 8.4 What the slice does not prove

**It does not prove the game is fun.** The raid loop — which `broodline_collectors_raiding.md` makes the thing that builds or bleeds a community — is not in it. Technical viability and design viability are separate risks and this milestone retires only the first.

---

## 9. Decisions taken against the design set

**`broodline_supersession_map.md` is behind the document set.** It accounts for fifty-three files and all fifty-three exist, but there are now seventy — thirteen unmapped documents in `specs/`, plus four in `plans/`: `broodline_technical_architecture.md`, `broodline_sim_core.md`, this document and `broodline_client_architecture.md`.

That mattered for how these were decided. "Not in the map" is evidence a document has not been ratified; it is **not** evidence it was rejected, because the map has simply not been run since these were written. So each of the following was decided on merit, with the mapped document as the default where merit was close.

**Five of them require edits to documents this one does not own**, listed at §9.9.

### 9.1 The two unmapped documents are retired

`broodline_sim_core.md` and `broodline_combat_engine.md` both specified tick order, determinism rules and the replay format, and disagreed on all three. The set's convention is one owner per area.

> **`broodline_sim_core.md` → ❌ superseded.** Five sections merge into `broodline_combat_engine.md` first, because they exist nowhere else:
>
> - **§3, geometry without vectors** — lane position as a scalar, range as a precomputed distance-table lookup. No sqrt, no trig, no vector maths in the hot loop. This is why a wave costs ~20 ms, and it removes the largest class of cross-platform divergence before it can exist
> - **§5, the capacity model** — tiers as a resource (I=1, II=3, III=5), four families, capacity summing across carriers, recomputed each tick from the live creature set and never accumulated
> - **§5, the two wave-composition rules as core invariants** — validated at wave load and thrown on, rather than left to a content author's memory
> - **§7, termination and the stall detector** — a 5,400-tick cap plus a 300-tick no-progress detector. Three raiders had soft-lock versions during design; this is what stops the fourth shipping
> - **§11, the batch-runner argument** — trait utility balance is a measurement, not an argument
>
> Its tick order, its unified counter phase and its assignment rule (§9.6) do **not** merge. `broodline_combat_engine.md` §4 and §5 stand.

> **`broodline_technical_architecture.md` → ❌ superseded**, but only after its unique material is rescued, because it was the sole owner of several topics. It is written for a studio, and it describes ten chassis — `broodline_chassis_roster.md` is ❌ superseded for exactly that premise.

| Rescued to | What |
|---|---|
| §3.1 here | SLOs, graceful degradation, what is watched and alerted |
| §4 here | The multi-region layer table and the data-residency argument |
| §5.2 here | The config publish pipeline and its validation invariants |
| §6.4 here | Account deletion, recovery and device migration |
| §7.0 here | The three release cadences and old-client tolerance |
| `broodline_client_architecture.md` | Addressables and remote content, install size as a conversion metric, the iPad orientation caveat |
| §10 here | The deferred-triggers table, which records *when* each remaining piece arrives |

An earlier revision of this document retired it into the triggers table alone. That was wrong: the table records when things arrive, not what they are, so four topics — observability, residency, the config pipeline and account lifecycle — briefly had no owner at all.

### 9.2 The engine is shared source, not a DLL

`broodline_sim_core.md` §2 required a compiled DLL. Retiring it made this a free choice, and the merits point the other way.

Its stated rationale — that a Unity assembly definition cannot reach `UnityEngine` from a DLL — is satisfied by the **`.asmdef`**, not by the packaging. And the DLL's real benefit, one artifact in both places, **does not survive IL2CPP**, which transpiles the IL to native C++ on device regardless. Determinism comes from the rules and the CI diff.

> **`engine/` is a local UPM package with a `package.json` and a `.csproj` over one source tree.** §7 reflects it.

What the DLL would have cost daily through Phases 2–3: rebuild-and-copy before any engine change is visible in Unity, worse stepping through IL2CPP, and a stale-artifact bug class.

### 9.3 Every wave is re-simulated

| | |
|---|---|
| `broodline_data_model.md` §8 — mapped | Campaign and region defence are **client-authoritative**; the server checks the outcome is *possible* and re-simulates only "if a player's results look anomalous" |
| Retired documents | "Validate every wave. Do not sample." |

> **Re-simulate everything. Nothing is sampled.**

The decisive argument is code volume, not strictness. A plausibility check needs a model of what outcomes are possible for each of sixty authored waves, plus an anomaly heuristic, plus the re-simulation path anyway as the escalation — strictly more code than always re-simulating, and a second model of the game that has to stay in sync with the first. It also unifies with raids, which `broodline_combat_engine.md` §2.1 already verifies exhaustively.

`data_model`'s reasoning — no adversary, bounded exposure — argues that sampling would be *acceptable*, not that it is *cheaper*. And "bounded by the wave definition" is bounded per wave: a modified client can claim all sixty immediately and collect all twelve campaign milestones, whose currency enters the ledger.

Offline play resolves either way: queue, grant optimistically, reconcile on reconnect.

Asymmetry worth recording: downgrading to sampling later is trivial; discovering sampling was wrong wastes the plausibility model.

### 9.4 Superseded replays show their recorded outcome

> **Follow `broodline_combat_engine.md` §3.** A replay recorded under an earlier engine version renders its stored outcome with a notice and is not re-simulated. No version routing.

This deletes a subsystem: no version→URL config map, no permanently pinned Cloud Run revisions, no routing logic in `api`, and no "which version was this?" failure mode.

**It costs nothing in CI**, contrary to an earlier draft of this document. The corpus exists to diff CoreCLR against IL2CPP *at the same version*, and it is generated inputs rather than harvested player replays. Deliberate engine changes re-baseline the golden hashes, which is normal.

It is also a smaller surface than it looks: with 30-day replay retention (§9.5) and balance patches well short of monthly, few replays ever outlive their engine version.

### 9.5 Two collections, two retention policies

Not a conflict once separated by owner.

> - **Player-visible replays** — 30 days rolling, up to 20 pinned, per `broodline_data_model.md` §5
> - **The CI corpus** — an engineering artifact in GCS with its own retention, seeded with generated waves

Storage constrains neither: 30 days plus pinning is roughly 290 KB per player, about 14 GB at 50k DAU.

**Closes `broodline_data_model.md` §11.2**, which questioned its own thirty-day figure against a seven-day alternative. Thirty stands — storage is not the constraint, and it covers a full season plus the pinning case the number was chosen for.

### 9.6 Coverage assignment is nearest-first, ties on spawn index

The one real merge conflict inside §9.1, since the capacity model is one of the five merged sections.

| | |
|---|---|
| `broodline_combat_engine.md` §5.1 — mapped | Carrier's current target, then **nearest-first**; Chill, Taunt and Burrow are pure nearest-first within range each tick, because their carriers may not be attacking the raider at all |
| `broodline_sim_core.md` §5 — retired | **Lowest raider ID**, rejecting nearest-first as a determinism hazard |

> **`broodline_combat_engine.md` §5.1 stands, with the tie-break made explicit: `(distance, spawnIndex)`.**

The determinism objection is real but already solved in the same document. `broodline_combat_engine.md` §6 sets the project-wide rule — *ties break on spawn index, ascending* — and a comparator returning `(distance, spawnIndex)` is a **total order**, so any correct sort produces identical output regardless of stability. That is the `Array.Sort` hazard §7.1 guards against globally.

The tie-break is load-bearing rather than decorative: with the merged geometry, distance is an exact integer from a precomputed table, so raiders at equal progress in different lanes tie constantly. Spawn index is unique and covers Brood children, which `broodline_combat_engine.md` §5.2 already orders that way for Cinder.

On design merit the mapped rule also wins — it exists to serve breach diagnosis, and *a player who cannot see which Breaker their Pierce is suppressing cannot tell whether coverage or placement failed*. Lowest-ID reads as "whoever spawned first," which in a three-lane wave may be nowhere near the carrier.

### 9.7 Three fixed tick slots, jittered within the slot

> **`broodline_server_topology.md` §5 stands** — APAC, EMEA and Americas prime evening, fixed at server creation. **Store the tick time at minute granularity** so servers can be spread across the first half hour of their slot.

The design reason outranks the operational one: `broodline_server_topology.md` §6 makes the tick the one moment a week the whole server watches together, an announced five-minute window. Within-slot jitter keeps that intact — nothing in the game is cross-server, so the offset is invisible — while flattening what is otherwise the heaviest scheduled job and the largest push fan-out in the game firing at once across a region.

Nothing to build at milestone 1. The only thing that must be right now is the column.

### 9.8 Six species

`broodline_data_model.md` §2 and `broodline_build_order.md` Phase 1 both say six; `build_order` names Vetch and Pale as the rig-proof pair. The only document saying ten chassis is retired by §9.1, and `broodline_chassis_roster.md` was already ❌ for it. Closed.

### 9.9 What this requires elsewhere, and what is still open

**Edits owed to other documents**, none applied by this one:

| Document | Change |
|---|---|
| `broodline_combat_engine.md` | Merge the five sections from §9.1. Make the `(distance, spawnIndex)` tie-break explicit at §5.1 |
| `broodline_data_model.md` | Rewrite §8 to make campaign waves server-validated — §9.3. Close §11.2 — §9.5 |
| `broodline_sim_core.md` | Add a ❌ superseded header once the merge lands |
| `broodline_technical_architecture.md` | Add a ❌ superseded header |
| `broodline_supersession_map.md` | Re-run it. Seventeen documents are unaccounted for, including this one and `broodline_client_architecture.md` |

**A second gap this document created and has now closed:** retiring `broodline_technical_architecture.md` left the client with no owner at all — no document covered Addressables, install size, memory, the render budget, local persistence or offline behaviour, and `broodline_combat_engine.md` §9 explicitly hands rendering to a document that did not exist. `broodline_client_architecture.md` is that document.

**Still open, and not resolvable without reading them** — two possible duplicate owners among the unmapped thirteen:

- **`broodline_species_stats.md`** against `broodline_combat_numbers.md`, which the map already credits with owning species stats
- **`broodline_trait_utility.md`** against `broodline_utility_traits.md`

The unmapped thirteen in full: `broodline_bastion_economy_check.md`, `broodline_cosmetic_economy.md`, `broodline_enemy_archetypes.md`, `broodline_errata_pass.md`, `broodline_four_decisions.md`, `broodline_gap_register.md`, `broodline_playtest_tuning_sheet.md`, `broodline_species_stats.md`, `broodline_terminal_sink.md`, `broodline_trait_utility.md`, `broodline_utility_traits.md`, `broodline_wave_scaling_addenda.md`, `broodline_wave_scaling_audit.md`.

---

## 10. Deferred work and its triggers

Nothing speculative. Each has a named condition.

| Deferred | Trigger |
|---|---|
| `commerce` as its own service | The first line of receipt verification |
| `social` as its own service | Player-generated text ships |
| `stream` and Redis pub/sub | Alliance chat or live territory state |
| `notify` and APNs | Raid alerts |
| Redis counters | Measured contention on node depletion |
| PgBouncer | The Cloud Run instance cap throttles real traffic |
| Cloud SQL HA | First non-TestFlight players |
| Server provisioning automation | Second server |
| Merge tooling | First server decaying below its population band |
| LiveOps console | Hand-editing config JSON becomes the bottleneck |
| Web shop | Post-launch, and only after verifying current App Review guidance on external purchase links |
| Region partitioning | First committed non-US market. `home_region` and the routing rule exist from Phase 4 |
| BigQuery pipeline | Enough players for `broodline_telemetry.md`'s questions to have signal |
| Moderation filter | `broodline_build_order.md` §2 says procure now — external lead time, and App Review rejects UGC without it |

---

## 11. Risks

| Risk | | Mitigation |
|---|---|---|
| **Determinism drift** | Trust failure, not load failure. Surfaces as players believing the game cheats | Three tooling-enforced rules, four test layers, nightly cross-runtime diff, manual device runs before release |
| **Either Phase 0 proof fails** | The rig proof changes the asset budget; the entity-count proof changes the renderer | Both are Phase 0 gates, run before anything is built on them. `broodline_rig_proof.md` and `broodline_client_architecture.md` §4 state what passing looks like |
| **Unity editor learning curve** | Schedule risk concentrated in Phase 3 | Phases 1–2 teach C# outside the editor first |
| **Art acquisition** | Gates Phase 7, least predictable line item | Ship on placeholder art if needed; art is a parallel track, not a blocker |
| **Two build systems over one source tree** | Low, but unusual enough to confuse tooling | Unity compiles `engine/Runtime` via the asmdef; .NET compiles the same files via the csproj. CI builds both on every `engine/` change |
| **The game may not be fun** | Not addressed by this milestone | Named in §8.4 so it is not mistaken for retired |

---

## 12. Guardrails

- No gameplay traffic touches Vercel
- `server_id` leads every primary key and index; no globally meaningful IDs
- Row-Level Security is on, and the setting is transaction-scoped
- Every currency mutation writes a ledger row in the same transaction as the balance update
- Every mutating request carries an idempotency key
- Splice rolls, mutation rolls and all grants are server-authoritative
- The engine has zero project references, no floating point, and no unordered iteration
- Every comparator produces a total order, tie-breaking on spawn index
- Static region content ships with the build; only node, controller and presence state is stored
- Git LFS and Force Text serialization are set before the first art commit
- Generated API clients are committed and never hand-edited
- A config bundle that fails publish-time validation is not published
- A schema migration and the code requiring it never deploy together
- The server tolerates old clients; API and bundle changes are additive

---

*Owns: deployables, runtime, reliability and observability, repository layout, release, storage mechanics, the config publish pipeline, the language boundary, account lifecycle, multi-region posture and milestone scoping. Defers to: `broodline_client_architecture.md` for everything inside the app, `broodline_bible.md` for the design, `broodline_combat_engine.md` for the simulation, `broodline_data_model.md` for entity shapes and authority, `broodline_server_topology.md` for population and lifecycle, `broodline_build_order.md` for the production plan.*
