# Broodline — Technical Architecture

*System design spec. The runtime underneath every design document in the set.*

> **❌ SUPERSEDED by `broodline_solo_execution.md` and
> `broodline_client_architecture.md`.** It assumes a studio, and it describes
> ten chassis — `broodline_chassis_roster.md` is superseded for exactly that
> premise; the design is six species. Its unique material was rescued before
> retirement: SLOs and degradation, the multi-region layer table, the config
> publish pipeline, account lifecycle and the release cadences to
> `broodline_solo_execution.md` §3.1, §4, §5.2, §6.4 and §7.0; Addressables,
> install size and the iPad caveat to `broodline_client_architecture.md`.
> Retained for history. Do not build from this file.

---

## 1. Why This Document Exists

Twenty-seven design documents describe what the game does. None of them say what runs it.

That gap has already started producing decisions by default. "Auto-resolve is a full simulation of the same engine" is a sentence in three documents; it is also the single hardest engineering constraint in the project, and nobody has written down what makes it true. "Depletion must track total extraction, not wall-clock time" implies a contended shared counter across every player harvesting a node. "Matchmaking must be enforced server-side, not as a UI filter" implies an authoritative server that the client cannot lie to. "Each server instance runs its own copy of the same thirty regions" is, quietly, the most important scalability decision in the entire design — and it was made by a designer for gameplay reasons, not by an engineer for capacity reasons.

This document names the runtime, fixes the service boundaries, and states what breaks first.

**Constraints given:** iOS and iPadOS only. Vercel and GCP. US launch, multi-region and multi-language later. Must survive going big faster than planned.

---

## 2. The Shard Is the Architecture

The region roster already commits to it: *each server instance runs its own copy of the same thirty regions, with independent node rotation, territory, and weekly tick timing.* The economy model derives a healthy population of **500–1,500 DAU per shard** from node depletion maths.

Take that seriously and most of the hard problems disappear.

- A shard is a **closed world**. Nothing crosses shard boundaries: no cross-shard raids, no cross-shard alliances, no cross-shard leaderboards except a global cosmetic one computed offline.
- A shard's entire live state fits comfortably in one Postgres schema and one Redis keyspace.
- **Capacity is measured in shards, not in servers.** Growth is answered by creating shards, which is a bounded, automatable, testable operation — not by re-architecting a global world under load.
- Blast radius is one shard. A bad deploy, a corrupted tick, a hot alliance row: 1,500 players, not everyone.

The cost is real and should be stated plainly: **friends who sign up separately will land on different shards and cannot play together.** Mitigation is an invite code that pins the invitee to the inviter's shard, plus a paid or milestone-gated shard transfer later. Do not build transfer at launch. Do build the invite code at launch — it is cheap and it is the thing players will actually ask for.

Two other consequences worth naming now:

**Shard lifecycle is a product feature, not an ops chore.** New shards open as older ones fill; old shards decay as players churn. Merging shards is eventually mandatory and it is genuinely difficult (alliance name collisions, territory reconciliation, duplicate Stakes). Design the schema so every row carries `shard_id` and no primary key is globally meaningful, so that a merge is a re-keying exercise rather than a rewrite.

**Tick times stagger by shard.** The alliance spec already floated per-server tick times to solve a timezone fairness problem. That same decision solves a capacity problem for free: if all shards ticked at once, the weekly tick would be a synchronised load spike across the entire fleet and the largest push-notification fan-out in the game. Set tick hour at shard creation, spread across the hour wheel.

---

## 3. Platform Split: Vercel and GCP

The split is not a preference. It follows from what each platform is actually good at.

| Vercel | GCP |
|---|---|
| Marketing site, press kit | Everything authoritative |
| Web shop (direct purchase) | Game API, simulation, world tick |
| LiveOps console (internal admin) | Player and world data |
| Support portal, privacy policy, account-deletion request page | IAP verification and entitlement grants |
| Localised static content and CDN | Push notifications, telemetry |

**No gameplay traffic touches Vercel.** Not a proxy, not an edge cache of player state, not a "just this one endpoint." The moment a Vercel function reads or writes authoritative game state, you own two backends with two deployment stories and a split-brain failure mode nobody will remember to test.

**The web shop is the strongest reason Vercel is in this design.** A Next.js storefront on Vercel with Stripe checkout, linked from the app, sells the same packs at the same face value with materially better margin than an in-app purchase. It also gives you a place to run promotions that App Review has no opinion about. Two rules:

1. **Stripe's webhook points at GCP, not at Vercel.** Vercel renders checkout and nothing else. Entitlement granting is a server-to-server call from Stripe into the GCP commerce service, signed and idempotent on the Stripe event ID. If Vercel is down, purchases already in flight still land.
2. **The web shop must never sell anything the app doesn't sell**, or you create a two-tier economy and an App Review conversation you don't want.

The LiveOps console is the second real earner. It is an internal Next.js app behind Google Workspace SSO where designers author events, offers, pack contents, Roulette pools and localised strings, and publish a **versioned JSON bundle to GCS** which the client fetches through CDN. Designers ship live-ops without an app update and without an engineer. Publishing writes to GCS through a signed admin endpoint on GCP — Vercel holds no GCP write credentials that can touch player data.

---

## 4. Runtime: Cloud Run, Not Kubernetes

**Recommendation: no Kubernetes at launch.** Everything runs on Cloud Run.

The instinct with a "must scale" mandate is GKE, and it is wrong here. Broodline's server workload is stateless request/response plus a handful of scheduled jobs. There is no persistent authoritative simulation room, no real-time multiplayer session, no player-to-player packet path. Combat runs on the client for live play and headless on the server for validation and auto-resolve — both are short, self-contained, CPU-bound function calls. That is precisely the shape Cloud Run is built for, and Cloud Run costs roughly one platform engineer less per year than GKE.

Revisit if and only if one of these becomes true: synchronous PvP ships, a shard needs an in-memory authoritative world that cannot be reconstructed per-request, or sustained load makes committed-use GKE nodes cheaper than Cloud Run's per-request pricing.

### GCP component map

```
                    ┌─────────────────────────────────────┐
   iOS / iPadOS ────┤ Cloud Load Balancing + Cloud Armor   │
      (Unity)       └───────────────┬─────────────────────┘
                                    │
              ┌─────────────────────┼─────────────────────┐
              │                     │                     │
        ┌─────▼─────┐        ┌──────▼──────┐       ┌──────▼──────┐
        │   game    │        │  commerce   │       │   stream    │
        │ Cloud Run │        │  Cloud Run  │       │  Cloud Run  │
        └─────┬─────┘        └──────┬──────┘       └──────┬──────┘
              │                     │                     │
              │  ┌──────────────────┘                     │
              │  │                                        │
        ┌─────▼──▼───┐   ┌──────────┐   ┌────────────┐   │
        │  Cloud SQL │   │Memorystore│  │   Pub/Sub  │◄──┘
        │  Postgres  │   │   Redis   │  │            │
        │  (sharded) │   │ (per shard)│ └─────┬──────┘
        └─────┬──────┘   └──────────┘         │
              │                          ┌────▼─────┐  ┌──────────┐
              │                          │  notify  │  │   sim    │
              │                          │Cloud Run │  │Cloud Run │
              │                          └────┬─────┘  └────┬─────┘
              │                               │ APNs        │
        ┌─────▼──────────────┐                ▼             │
        │ GCS (replays,      │◄────────────────────────────┘
        │ config bundles,    │
        │ telemetry raw)     │──► BigQuery ──► Looker Studio
        └────────────────────┘

   Cloud Scheduler ──► Pub/Sub ──► world-tick jobs (per shard, staggered)

   Vercel: marketing · web shop · LiveOps console · support
           (writes config to GCS via signed admin endpoint; no player data)
```

### Deployables

Start with **three**, split later along the seams marked below.

**`game`** — the player-facing API. Contains four eventual services that share a database and have no reason to be separate on day one:

- *player*: roster, splicing, Gene Lab modules, campaign progress, daily claims, Geneticist Tier
- *world*: regions, nodes, harvest accrual, depletion, Ark relocation, region defence
- *raid*: collectors, convoys, route plotting, matchmaking, cooldowns, immunity, Marks
- *social*: alliances, Stakes, Hold, chat persistence, recipe share, reports

Split `social` out first — it is the only one with a moderation and legal surface, and the only one likely to need a different on-call posture.

**`sim`** — the headless deterministic simulator. Internal-only, no public ingress. Validates client-submitted wave results, runs raid auto-resolve, produces replays. Separate from `game` from day one because its scaling curve, CPU profile and deploy cadence are all different, and because it must be independently version-pinned (§6).

**`commerce`** — IAP receipt verification, App Store Server Notifications, Stripe webhooks, entitlement grants, Lab Pass subscription state. Separate from day one because money.

Plus two thin ones: **`notify`** (APNs fan-out, scheduled notifications) and **`stream`** (SSE fan-out for chat and alerts). Both are small enough to fold into `game` initially if you prefer three deployables total; `stream` in particular holds long-lived connections and will eventually want its own concurrency settings.

---

## 5. Data

### Storage choices

| Store | Holds | Why |
|---|---|---|
| **Cloud SQL Postgres** | All authoritative player and world state | Relational, transactional, cheap, universally understood. Genetics is a graph of small strongly-related records; this is exactly what Postgres is for. |
| **Memorystore Redis** | Hot counters, node extraction totals, rate limits, chat pub/sub, presence | Contended atomic counters and fan-out. One instance per shard-group. |
| **GCS** | Replays, config bundles, raw telemetry, creature thumbnails | Cheap, durable, CDN-fronted |
| **BigQuery** | All telemetry and economy analytics | The economy model's tuning loop lives here |

**Not Spanner at launch.** Spanner is the correct answer to a global-writes problem you do not have, at roughly ten times the cost. The shard model means writes are naturally partitioned. Design so a migration stays mechanical: `shard_id` is the leading column of every partition key, no cross-shard foreign keys, no global sequences.

**Sharding layout:** one Postgres instance per **shard-group** of roughly ten game shards, each shard in its own schema. Ten shards × 1,500 DAU = 15,000 DAU per database instance, which a single `db-custom-4-16` handles without noticing. Adding capacity is adding an instance. A hot shard can be moved to its own instance without touching anything else because nothing joins across schemas.

### Core tables

Sketch, not a migration. Every table carries `shard_id`.

```
players            (shard_id, player_id, account_id, display_name, birthdate_band,
                    core_tier, created_at, immunity_until, locale)
creatures          (shard_id, creature_id, player_id, chassis_id, generation,
                    slot_a_trait, slot_b_trait, instinct_trait, aberrant_flags,
                    parent_a, parent_b, name, state, regen_until)
samples            (shard_id, player_id, trait_id, count)
lab_modules        (shard_id, player_id, module, tier, upgrade_completes_at)
campaign_progress  (shard_id, player_id, wave_id, first_clear_at, best_result)

regions            (shard_id, region_id, band, terrain_family, lane_count,
                    controller_alliance, richness)
nodes              (shard_id, node_id, region_id, node_type, spawned_at,
                    expires_at, total_extracted, capacity)
harvest_claims     (shard_id, player_id, node_id, rate, last_settled_at)
arks               (shard_id, player_id, region_id, arrives_at)

collectors         (shard_id, collector_id, player_id, class, state)
convoys            (shard_id, convoy_id, collector_id, route, cargo_value,
                    departs_at, arrives_at, exposure_window)
raids              (shard_id, raid_id, attacker_id, defender_id, convoy_id,
                    alert_sent_at, resolves_at, outcome, replay_uri)

alliances          (shard_id, alliance_id, name, tag, banner, treasury)
alliance_members   (shard_id, alliance_id, player_id, rank, contribution)
stakes             (shard_id, stake_id, alliance_id, region_id, hold, planted_at)

ledger             (shard_id, entry_id, player_id, currency, delta, balance_after,
                    reason_code, ref_type, ref_id, idempotency_key, created_at)
```

### The ledger is not optional

**Every currency mutation writes an append-only ledger row in the same transaction as the balance update.** Gene Shards, Splice Charges, Raid Marks, Defense Marks, premium currency, all of it.

This is the highest-value 200 lines of code in the backend. It gives you: exact answers to "where did my shards go" support tickets, a duplication-exploit tripwire (sum the ledger, compare to balances, alert on drift), the raw input for the economy model's faucet/sink tuning without instrumenting anything separately, and a refund path that isn't guesswork. Games that add this after launch never fully reconstruct the first six months.

`idempotency_key` is unique per shard. Every mutating request carries a client-generated key; a replay of the same key returns the original result rather than double-granting. This is what makes retries safe over flaky mobile networks, and mobile networks are always flaky.

### Concurrency

Player-owned state is single-writer by nature — only you splice your creatures. Use optimistic concurrency: a `version` column, compare-and-set, retry once, surface a conflict on the second failure. No locks.

Contended state is narrow and named: **node extraction totals**, **alliance treasury**, **Stake Hold**. Node extraction is the one that matters, because the design explicitly requires depletion to track *total extraction across all harvesters* rather than wall-clock time. Keep the running total in a Redis atomic counter keyed `shard:{id}:node:{id}:extracted`, flush to Postgres every 30 seconds and on depletion crossing. Postgres is the durable record; Redis is the arbiter of the race. On Redis loss, rebuild from Postgres and accept up to 30 seconds of under-counted extraction in the players' favour.

### Harvest accrual is lazy

Never tick players. Harvest is computed on read from `last_settled_at`, the node's current rate, the Harvest Array multiplier, and the 12-hour offline cap. Settlement writes happen when the player claims, when the node depletes, or when a raid resolves against the convoy. Fifty thousand players cost zero background CPU while asleep.

---

## 6. The Simulation Core

This is the highest-risk item in the project and it deserves its own section.

Three documents commit to auto-resolve being *the identical engine, a full simulation, never a stat roll*. That commitment is correct — it is what makes the replay viewer meaningful and it is stated as the difference between PvP that builds a community and PvP that bleeds one. It also means **the same simulation must produce bit-identical results on an iPhone and in a Linux container.**

### The rule

**One core, compiled twice.** The simulation is a pure library with no engine dependency, no rendering, no allocation in the hot loop. The Unity client links it for live play; the `sim` service runs it headless on .NET for validation and auto-resolve.

**Engine decision: Unity. Locked.** The deciding factor is the art direction's modular attachment-point rig — creatures are chassis plus swappable trait attachments, addressable and combinatorial, and Unity's rigging, Addressables and animation-retargeting tooling is materially ahead of the alternatives for exactly that. Godot 4 has the better licensing story and loses on 3D rigging; native Swift plus Metal costs a year for aesthetic control this project doesn't need.

With Unity fixed, the core is **plain C#** targeting `netstandard2.1`, referenced by both the Unity client and the `sim` service. The alternative — a Rust core with a Swift/C# FFI shim — gives stronger determinism guarantees and costs more velocity than this team should spend. Take C# and buy the determinism back with discipline and a CI gate.

### Determinism discipline

Non-negotiable, and cheap only if adopted before the first line of gameplay code:

- **No floating point anywhere in the core.** Fixed-point `long` at Q32.32. Trig and square roots from lookup tables, not `Math`.
- **Fixed timestep**, 30 Hz. Rendering interpolates; simulation never sees a variable delta.
- **Seeded PRNG** — xorshift128+, one stream per simulation, seed supplied by the server. Never `System.Random`, never `UnityEngine.Random`.
- **Deterministic iteration order.** No `Dictionary` or `HashSet` traversal in the core; sorted arrays and stable entity IDs only. This is the failure that always ships, because it is invisible on one machine.
- **No wall-clock reads.** Time is tick count.

### Replays are inputs, not frames

A replay is `{ sim_version, seed, terrain_id, both rosters, ordered input log }`. Roughly 2 KB. The viewer re-simulates. This makes replays free to store, free to transmit, and — usefully — makes every replay a regression test.

**Corollary: `sim_version` is immutable per replay.** When the simulation changes, old replays must still play back. Keep every released `sim` version deployed and route replay requests to the version they were recorded under. This is the reason `sim` is a separate deployable from day one; it is the only service in the system that cannot roll forward.

### Anti-cheat by server authority

The economy is server-authoritative without exception:

- **Splice outcomes are rolled server-side.** The client sends "splice these two parents with this chassis choice"; the server rolls, writes the child and the ledger entry, and returns a result the client animates. Same for Splice Roulette — this is also a regulatory requirement, since published odds you cannot demonstrate are a problem.
- **Wave results are validated by re-simulation.** The client submits its input log; `sim` replays it and compares the outcome hash. Mismatch means no reward and a telemetry event. At 90 seconds, ~30 Hz, five creatures and a few dozen raiders, one wave is roughly 20 ms of server CPU — validate every wave, always. Do not sample.
- The client is never trusted with currency deltas, node yields, cooldowns, matchmaking bands, or immunity state.

Jailbreak detection is worth a token effort and no more. Server authority is the actual defence.

### CI gate

A nightly job replays a corpus of a few thousand stored replays on both an iOS device farm build and the server build, diffing outcome hashes. Any drift fails the build. Without this the two builds diverge silently within a month and every player who watches a replay that doesn't match what they saw loses trust in the game's fairness.

---

## 7. API Design

REST/JSON over HTTP/2. Not GraphQL — the client's query shapes are known and fixed, and GraphQL's flexibility buys nothing while costing a caching story. Not gRPC — Unity's HTTP stack is the path of least resistance and the request volume doesn't justify the friction.

### Shape

- **Intent endpoints, not CRUD.** `POST /v1/splice`, `POST /v1/ark/relocate`, `POST /v1/convoy/dispatch`, `POST /v1/raid/launch`. The server owns what a splice means.
- **Every mutating request carries `Idempotency-Key`.**
- **`GET /v1/sync`** returns everything the Ark Home screen needs in one call: settled shard balance, charge count and regen timer, active timers, convoy states, campaign progress, unread mail count, active event, and a config bundle version. One call per app foreground. The five-tab structure means the client would otherwise make a dozen calls on cold start.
- **Deltas via `?since=<cursor>`** on roster and world reads, so a warm client fetches almost nothing.
- **Config is not an API.** Event definitions, pack contents, trait tables, wave definitions and localised strings are a versioned JSON bundle on CDN. `/v1/sync` returns the required bundle version; the client fetches and caches it. This keeps live-ops changes off the API path entirely and makes them CDN-cheap at any scale.

### Realtime

Three things need push: raid alerts, alliance chat, and convoy or territory state changes.

**Raid alerts go over APNs.** The 90-second countdown starts from the push. Direct APNs with token-based auth from the `notify` service — since the platform is iOS-only, Firebase adds a dependency and buys nothing. Alliance rally broadcast is a fan-out over at most 40 device tokens, which is not a scaling concern.

**Chat and live state go over SSE**, one long-lived `GET /v1/stream` per foregrounded client, served by `stream` on Cloud Run with Redis pub/sub behind it. SSE rather than WebSockets because the traffic is server-to-client only, it survives proxies and mobile NAT better, and it reconnects itself. Cloud Run's streaming support and long request timeouts cover this without introducing Kubernetes.

Chat volume is genuinely small — a 40-member alliance producing a few hundred messages a day means the entire fleet's chat traffic is a rounding error. Do not over-engineer it. Persist to Postgres for moderation history, fan out through Redis.

---

## 8. Commerce

**App Store Server API v2** for verification and **App Store Server Notifications v2** for the async stream. Both land on `commerce` in GCP.

- Verify the signed JWS transaction against Apple's root certificates. Never trust a client-supplied receipt blob.
- **Grant idempotently on `transactionId`.** Apple will deliver the same notification more than once; this is expected behaviour, not an error.
- Lab Pass is a subscription: handle `DID_RENEW`, `EXPIRED`, `GRACE_PERIOD`, `REFUND`, `REVOKE`. A refund must claw back the entitlement and write a compensating ledger entry rather than deleting the original.
- Stripe (web shop) follows the identical path keyed on the Stripe event ID, granting through the same internal entitlement function. **One grant path, two front doors.** If web and IAP have separate grant code, they will diverge and one of them will be wrong.
- The monetization spec's monotonic pack ladder invariant should be a **unit test over the published pack config**, run at publish time in the LiveOps console. A designer editing a pack in the console must not be able to break value-per-dollar monotonicity. This was already broken once by hand; make the machine enforce it.

---

## 9. Multi-Region and Multi-Language

### Region

The shard model makes this nearly free, provided one decision is made now: **shards are region-pinned, and account data is region-partitioned from day one even while only US exists.**

| Layer | US launch | EU expansion | APAC expansion |
|---|---|---|---|
| Shards + shard databases | `us-central1` | `europe-west1` | `asia-northeast1` |
| Account / identity | `us-central1` | `europe-west1` (EU residency) | home region + replica |
| Commerce | `us-central1` | routes by account region | routes by account region |
| Telemetry | pseudonymised, single BigQuery region | same, with EU raw retained in EU | same |
| Static content and CDN | global | global | global |

The account service is the only genuinely global component, and it needs to be small: account ID, credential binding (Sign in with Apple), birthdate band, home region, and a shard pointer. Everything else is shard-local. Keep it small and region-partitioning it later is a weekend rather than a quarter.

**Data residency is the reason to build the partition before you need it.** Once EU players exist, moving their data into the EU is a migration under legal pressure. A `home_region` column and a routing rule written now cost nothing.

### Language

No server work if the split is right.

- **UI strings** ship in the client bundle, one file per locale, keyed. Standard Unity localisation.
- **Live-ops content** — event names, offer copy, pack descriptions, patch notes — is authored in the LiveOps console with a locale column and published in the same versioned JSON bundle the client already fetches. Adding a language is adding a column, not a deploy.
- **Player-generated text** is the only hard part: chat, alliance names, creature names. The moderation spec's decision to make Recipe Share structured-only already removed the largest surface. What remains needs a commercial filter with per-locale models — not one English blocklist applied everywhere, which fails immediately and embarrassingly in the first non-English market.
- **The one thing that is not free:** the Trait Codex and Wave Defeat screens carry the counter system's entire teaching load. Their copy is load-bearing gameplay, not decoration, and needs a translator who has played the game rather than a string-table pass.

---

## 10. Scale and Reliability

### Load estimate

Work it from 50,000 DAU, roughly 35–100 shards.

| Quantity | Estimate |
|---|---|
| API requests | ~40 calls/session × 3 sessions × 50k = 6M/day → **~70 rps mean, ~300 rps peak** |
| Wave validations | ~6 waves/player/day = 300k sims/day at ~20 ms → **~1.7 CPU-hours/day** |
| Raid auto-resolves | 2/player/day cap, ~20% participation → **~20k/day** |
| Replay storage | 20k × 2 KB → **~40 MB/day**, ~15 GB/year |
| Player data | 50k × ~50 KB → **~2.5 GB** |
| Push notifications | raid alerts + tick + events → **~200k/day**, staggered |

The honest conclusion: **this is not a large system.** At 50k DAU, a handful of Cloud Run instances and five Postgres instances carry it, at something in the order of $2–4k/month excluding asset CDN egress — which will likely exceed all compute combined, and is the one line item worth optimising before launch by keeping the install and patch payloads small.

The scaling risk in Broodline is not computational. It is operational.

### What breaks first

In the order it will actually happen:

1. **Shard provisioning is manual.** A launch spike arrives, shards fill, and someone is hand-running Terraform at 2am while sign-ups queue. **Fix before launch:** shard creation is one automated operation — Terraform module, schema migration, thirty seeded regions, registry row, health check — and the system keeps **three empty shards warm at all times**, provisioning a replacement whenever one is claimed.

2. **Cloud SQL connection exhaustion.** Cloud Run scales to hundreds of instances; each opens a pool; Postgres runs out of connections long before it runs out of CPU. This is the classic serverless-plus-relational failure and it arrives exactly when traffic is good news. **Fix:** PgBouncer in transaction-pooling mode in front of every instance, and a hard cap on per-instance pool size.

3. **Synchronised weekly tick.** Every shard rotating deposits, resolving territory, and pushing notifications in the same minute. **Fix:** tick hour assigned at shard creation, spread across 24 hours — already the right call for timezone fairness, and it doubles as load-spreading.

4. **Redis hot keys on contested nodes.** An Apex Vein with fifty players harvesting is one counter taking every write. Survivable at this scale; watch it, and shard the counter by player-hash into sixteen sub-counters if it isn't.

5. **Simulation drift.** Not a load failure, a trust failure, and the worst one on the list because it surfaces as players believing the game cheats. The nightly cross-platform replay diff (§6) is the entire mitigation.

### Reliability posture

- **Cloud SQL HA** with a failover replica and point-in-time recovery. The ledger makes economy state reconstructible even from a bad restore.
- **Canary by shard.** Deploy to one shard, watch for an hour, roll the fleet. The shard model gives this for free and almost nothing else in the design does.
- **Graceful degradation targets:** if `sim` is down, campaign waves queue for validation and reward optimistically with a reconciliation pass (bounded exposure, good feel). If `stream` is down, chat degrades to polling. If `notify` is down, raids auto-resolve without an alert and the defender gets the replay plus a mail apology. Nothing about `game` being down is graceful, which is why it is the one service with a real SLO.
- **SLOs:** `/v1/sync` p99 under 300 ms. Wave validation p99 under 500 ms. Raid alert push delivered within 5 s p95 — this one is a gameplay requirement, because the 90-second countdown is meaningless if delivery eats twenty of them.
- **Telemetry** streams to GCS via Pub/Sub, loads to BigQuery, surfaces in Looker Studio. The economy model's faucet and sink tables are dashboards, not spreadsheets — the whole point of fixing 40 shards/hour as the anchor was to have something to check reality against.

---

## 11. Client

**Unity 6, portrait-locked, Metal, universal iPhone and iPad binary.** Decided, per §6.

Unity-specific constraints that follow:

- **IL2CPP, ARM64 only.** Mono is not an option for App Store submission and IL2CPP is also what makes the sim core's fixed-point maths perform acceptably on device.
- **The sim core lives outside the Unity assembly graph** — a separate `netstandard2.1` project referenced as a compiled DLL, not as source in `Assets/`. This is what physically prevents someone reaching for `UnityEngine.Random` or `Time.deltaTime` inside it. Enforce with an assembly definition that has no `UnityEngine` reference.
- **Unity's own systems are banned from the simulation.** No physics, no coroutines, no `Update()` driving game logic. Unity renders the sim's output; it does not participate in it. Getting this wrong is how the headless build stops matching the client.
- **Licensing:** Unity Pro is required above the Personal revenue threshold, and the runtime-fee terms have changed more than once. Confirm current seat and revenue terms before the first paid seat, and budget for the studio tier at launch rather than discovering it during soft launch.

- The art direction's modular attachment-point rig means creature assets are addressable and combinatorial. Use **Addressables with remote content**, so new chassis and trait attachments ship without an app update where possible.
- **Install size is a conversion metric.** Portrait mobile, cellular install, thirty regions of environment art and ten chassis — keep the initial download lean and stream the rest. This is also the CDN egress line item from §10.
- **iPad:** portrait-locked universal binary is the cheapest correct answer given a portrait-designed game, but Apple's orientation and multitasking expectations for iPad builds shift between guideline revisions. Verify against current App Review guidelines before committing the layout work, rather than assuming what was true last year still is.
- **Sign in with Apple** as the primary identity, with a guest path that upgrades. Guest-first reduces FTUE friction, which the FTUE spec cares about more than anything else; account binding can be prompted at the first purchase or the first alliance join.

---

## 12. Trade-Offs Made Explicit

| Decision | Chosen | Alternative | Why, and what it costs |
|---|---|---|---|
| World model | Shards | Single global world | Shards bound every scaling problem and give per-shard blast radius. Costs cross-shard play and an eventual merge project. |
| Runtime | Cloud Run | GKE | An order of magnitude less ops for a workload that is request/response. Costs the ability to hold long-lived in-memory world state. |
| Database | Cloud SQL Postgres | Spanner | Roughly a tenth the cost for a workload that partitions naturally. Costs a migration later if global writes ever appear. |
| Sim core | Shared C# library | Rust core with FFI | Team velocity, one language, one debugger. Costs determinism guarantees that must be bought back with discipline and a CI gate. |
| Realtime | SSE + Redis pub/sub | WebSockets, or Firestore | One stack, proxy-friendly, self-reconnecting. Costs bidirectional streaming, which nothing needs. |
| Push | Direct APNs | Firebase Cloud Messaging | No extra dependency on an iOS-only platform. Costs a rewrite if Android ever ships. |
| Client engine | **Unity 6** (decided) | Godot 4, or native Swift + Metal | Modular attachment-point rig and Addressables. Costs install size, licensing fees, and some aesthetic control. |
| Vercel scope | Web only | Vercel edge functions in the game path | One authoritative backend. Costs nothing real. |
| API | REST/JSON | GraphQL, gRPC | Fixed query shapes, simple caching, easiest Unity integration. Costs client flexibility nobody asked for. |

---

## 13. Build Order

1. **Sim core and determinism harness.** Before any server, before any content pipeline. It gates combat, replays, raids and anti-cheat, and retrofitting determinism is a rewrite.
2. **Account service, shard registry, automated shard provisioning.** Prove a shard can be created by one command on day one, not on launch day.
3. **`game` service, Postgres schema, ledger, `/v1/sync`.** The vertical slice: sign up, land on a shard, harvest, splice, fight a wave that validates server-side.
4. **`commerce`.** Early, because IAP edge cases take longer than anyone budgets and soft launch needs real purchases.
5. **`notify` and `stream`.** Raids and alliances are unplayable without them, and they unblock the day-14 retention mechanics.
6. **LiveOps console and config pipeline.** The moment designers can ship an event without an engineer, throughput changes.
7. **Web shop.** Post-launch. Margin, not launch-critical.
8. **Region partitioning activation.** Only when the first non-US market is committed — but the `home_region` column and routing rule land in step 2.

---

## 14. Open Questions

1. **Shard population target of 500–1,500 DAU** comes from node depletion maths. It is also the alliance-density number and the Gene Lab community-goal number. Confirm the three agree, because they will be tuned by different people.
2. **Shard merge policy.** Not launch-critical, schema-critical. Confirm no globally-meaningful IDs before the first migration ships.
3. **Guest accounts and shard assignment.** If a guest can be created freely, shard assignment can be gamed to farm a low-population shard's Apex Veins. Needs a rule.
4. **Replay retention.** Every replay is 2 KB and every replay is also a regression test. Keep all of them, or age out to Coldline after 90 days? Leaning keep-all; the corpus is worth more than the storage.
5. **Does the web shop create an App Review problem?** The rules on external purchase links have moved repeatedly. Verify current guidance before building step 7 rather than inheriting an assumption.

---

*Companion documents: the combat system spec defines what the simulator computes; the economy model defines what the ledger records; the moderation spec defines what the filter enforces. This document defines what runs.*
