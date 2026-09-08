# Broodline — The Data Model

*Technical spec, what is stored and where*

> **CURRENT — technical spec.** Entities, relationships, authority split and
> retention. Values live in the design companions; this owns their shape.

**Two findings.** Lineage records grow without bound unless retention is designed, and the design already contains its own answer — §4. And the authority split is not "server owns everything": three categories behave differently and conflating them costs either responsiveness or integrity — §8.

---

## 1. Entities

Twelve, and the relationships between them are most of the model.

| Entity | Scope | Lifetime |
|---|---|---|
| **Player** | Global | Permanent |
| **Ark** | Player | Permanent. One per player |
| **Facility** | Ark | Permanent. Six per Ark |
| **Creature** | Player | Until spliced, retired, or pruned — §4 |
| **TraitInstance** | Creature | With its creature |
| **SampleStack** | Player | Until fused or discarded |
| **Region** | **Server** | Permanent. Thirty per server |
| **Node** | Region | Until depleted or rotated |
| **Convoy** | Player | Until delivered, intercepted or recalled |
| **Alliance** | Server | Until disbanded |
| **Stake** | Alliance × Region | Until withdrawn or lost |
| **Replay** | Player | 30 days, or permanent if pinned — §5 |

**Region and Alliance are server-scoped, everything else is player-scoped.** That is the single most important line in the model: `broodline_region_graph.md` gives every server its own copy of the same thirty regions with independent node rotation, territory and tick timing, so region state is shared mutable state and creature state is not.

---

## 2. Creature

The central entity, and the one with the most constraints on it.

| Field | |
|---|---|
| `id` | Permanent. Never reused, survives the creature — §4 |
| `species` | One of six |
| `generation` | 1 for base stock; parent max + 1 |
| `combat_1` | TraitInstance. The locked slot |
| `combat_2` | TraitInstance. The rolled slot |
| `instinct` | One of six, or an Aberrant Instinct |
| `name` | Player-set, or null. **Founders only** |
| `is_founder` | Bool. Set at creation, never changed |
| `parent_a`, `parent_b` | Creature ids, or null for base stock |
| `hp_current` | For regeneration state |
| `regen_until` | Tick timestamp, or null |
| `committed_to` | Garrison, escort, convoy, or null |
| `acquired_at` | For Codex discovery timestamps |

**A TraitInstance is `(trait, coverage_tier)`.** Coverage is per-creature, not per-player — `broodline_sample_economy.md` §7 makes coverage travel with the trait through a splice, and two creatures carrying Chill can carry it at different tiers.

**Aberrants occupy either a combat slot or the Instinct slot** depending on kind, per `broodline_combat_numbers.md` §4.4. They have no coverage tier, so a TraitInstance holding an Aberrant has `coverage_tier = null` rather than 0. Null and zero must not be conflated; zero would sort and display as "less than tier I."

**`committed_to` is why the splice screen can warn.** `broodline_sample_economy.md` §7 requires the confirmation to name coverage that will not carry, and `broodline_waves_45_52.md` raises the case of splicing away a creature that carries a counter held nowhere else. Both are queries against the player's live roster at confirmation time.

---

## 3. What a creature is *not*

**No level, no XP, no stars, no rarity, no power score.** A creature is species, two traits with coverage, one Instinct, and a generation. Everything about its strength derives from those.

**No stat overrides.** Stats come from species via `broodline_combat_numbers.md` §3. Storing per-creature stats would let them drift from the table and would make a balance patch a data migration.

**Growth is not stored.** Bible §10.2 rule 3 makes runt-to-apex a proportion curve over one rig, driven by generation. It is a render-time function of `generation`, not a field.

---

## 4. Lineage retention

**The problem.** Bible §3.5 gives every creature a five-generation ancestry view, and Founder names propagate into every descendant's tree. A splice consumes both parents — so **the tree is made of dead creatures**, and every one of them has to still exist to be displayed.

At six splices a day, a core player destroys twelve creatures daily. **Over two years that is roughly nine thousand dead creature records per player**, none of which the player can see or act on, all of which are retained solely so a lineage view can render.

**The design already contains the answer.**

> **Retain ancestors five generations deep. Retain all Founders permanently. Prune everything else.**

Bible §2.1 caps lineage depth at five generations, so an ancestor six generations back is never displayed and never needs to exist. Founders are the exception — bible §3.3 makes their names the emotional anchor and they appear in every descendant's tree regardless of depth.

**A pruned record leaves a tombstone**, not a hole: `{id, species, generation, was_founder: false}`. Enough to render "an unnamed Gen-4 Vetch" if a tree ever reaches past the retained depth, and small enough to keep indefinitely.

**Practical retention per player**, steady state:

| | Records |
|---|---|
| Live creatures | ≤ 60, capped by the Hatchery |
| Retained ancestors | ~5 × 60 = 300 |
| Founders | 5 |
| Tombstones | Growing, but ~40 bytes each |

**Three hundred records instead of nine thousand**, and the constraint that produces it was already in the design for narrative reasons rather than storage ones.

**Creature ids are never reused**, including for pruned records. A reused id would attach a dead creature's lineage to a living one, which is a bug that would be reported as a ghost.

---

## 5. Replay retention

`broodline_combat_engine.md` §3 makes a replay a few hundred bytes of inputs. At two raids a day plus defences, a player accumulates roughly 1,500 a year.

> **Retain 30 days rolling. A player may pin up to 20 permanently.**

Thirty days covers the revenge-token window, the weekly tick and a season. Pinning covers the case a replay exists for at all — `broodline_collectors_raiding.md` calls it the thing that makes losing survivable, and a player who wants to keep the raid where their Cinderplate held the line should be able to.

**Engine version is stored per replay** and a superseded replay is marked rather than deleted, per the engine spec.

---

## 6. Samples and the Gene Vault

**A SampleStack is `(trait, tier, count)`.** Twelve traits × three tiers = **36 possible stacks**, and a player holds some subset.

**Capacity counts individual samples, not stacks**, per `broodline_sample_economy.md` §2 — thirty Chill samples occupy thirty of a player's capacity, not one. That is the whole reason the valve bites, and it is easy to implement backwards.

**Fusing is a transaction**: three of tier N removed, one of tier N+1 added, atomic. A partial failure that consumed three and granted nothing is the worst bug available in this system.

**Applying a sample to a creature is also a transaction**, and it mutates a TraitInstance's `coverage_tier` on one creature.

---

## 7. Region and node state

**Server-scoped, shared, mutable.** Thirty Region rows per server, each with:

| Field | |
|---|---|
| `terrain_family` | Static, from `broodline_region_roster.md` §5 |
| `species_weighted` | Static, two species |
| `band`, `adjacency` | Static, from `broodline_region_graph.md` |
| `nodes` | Live. Type, remaining yield, depletion timestamp |
| `controller` | Alliance id or null. **Null forever for the eight Inner regions** |
| `arks_present` | Player ids currently parked |

**Static fields are content, not data.** Terrain family, adjacency and species weighting ship with the build and are identical on every server. Only nodes, controller and presence are stored per server, which keeps a server's region state to a few kilobytes.

**Node rotation and control both resolve on the weekly tick**, per bible §6.7, at a per-server hour set at creation and never changed.

---

## 8. Authority

Not "the server owns everything." **Three categories, and conflating them costs either responsiveness or integrity.**

### Client-simulated, server-verified

Campaign waves and region defence. **The client simulates and submits the input log; the server re-runs it and compares.** Every submission, never sampled — the same treatment raids get, and the same code path.

**This was previously client-authoritative with a plausibility check and re-simulation only on anomaly.** It changed on cost grounds rather than strictness. A plausibility model has to know what outcomes are possible for each of sixty authored waves, plus an anomaly heuristic, plus the re-simulation path anyway as the escalation — strictly more code than always re-simulating, and a second model of the game that must stay in sync with the first. At roughly 20 ms per wave it is under two CPU-hours daily at 50k DAU.

The old reasoning — no adversary, exposure bounded by the wave definition — argued that sampling would be *acceptable*, not that it was cheaper. And the bound is per wave: a modified client can claim all sixty immediately and collect all twelve campaign milestones, whose currency enters the ledger.

Submissions made offline queue and validate on reconnect, granting optimistically. An honest client always validates, so a clawback only ever touches a modified one. Decision and reasoning at `broodline_solo_execution.md` §9.3.

### Server-verified

**Raids.** The defender is not present, so a modified client wins every raid. Per `broodline_combat_engine.md` §2.1 the client simulates live for responsiveness and submits seed, placement and Rally timestamp; the server re-runs and compares.

**Take the client-simulates-and-verify option rather than server-simulates-and-playback.** The raid alert window is ninety seconds and the defender may be joining live; a round trip per wave against that is the wrong trade, and determinism makes verification a single re-run either way.

### Server-authoritative, always

Everything with a shared or economic surface:

- Node yield, depletion and rotation
- Territory, Stakes, Hold accrual and the weekly tick
- Convoy position, interception rolls, cargo
- All currency and sample grants
- Splice outcomes — **including the mutation roll**
- Base stock drops
- Matchmaking and the Transit Board

**The splice roll is server-side and non-negotiable.** It is a paid randomised action with published odds, and a client-side roll is both cheatable and unverifiable against the disclosure bible §2.6 commits to.

---

## 9. Codex state

Per player, and smaller than it looks.

| Field | |
|---|---|
| `discovered_traits` | 12 bits |
| `discovered_instincts` | 6 bits |
| `discovered_aberrants` | 8 bits |
| `encountered_raiders` | 8 bits |
| `discovered_at` | Timestamp per entry, for the Codex's discovery dates |

**Discovery registers on acquisition, never on retention**, per `broodline_trait_codex.md` §3. Once set, a bit is never cleared — a player who briefly owned a Screen carrier keeps the entry after splicing it away. That is deliberate and it is what stops hoarding pressure.

---

## 10. Guardrails

- Creature ids are permanent and never reused, including for pruned records
- Coverage is per-creature, never per-player
- Aberrant coverage is null, never zero
- Sample capacity counts individuals, not stacks
- Fusing and sample application are atomic transactions
- Static region content ships with the build; only node, controller and presence state is stored per server
- The Inner Reach's eight regions have a permanently null controller, enforced in schema rather than in logic
- Splice rolls, mutation rolls and all grants are server-authoritative
- Lineage retains five generations plus all Founders; everything else becomes a tombstone

---

## 11. Open questions

1. **Does a tombstone need a name field?** A pruned Founder is impossible by §4, but a pruned creature the player renamed is not — except renaming is Founders-only, so it may genuinely never occur. Worth confirming rather than assuming.
2. ~~**Thirty-day replay retention against the revenge token's 24 hours** is generous.~~ **Resolved — thirty stands.** Storage is not the constraint at any realistic scale: thirty days plus twenty pinned is roughly 290 KB per player, about 14 GB at 50k DAU. The CI replay corpus is a separate collection with its own retention and is not governed by this — `broodline_solo_execution.md` §9.5.
3. **Node state on a dead server.** Rotation, depletion and the weekly tick all assume the tick runs. A server with no active players still accrues state, and whether that matters depends on server lifecycle decisions nobody has made.
4. **`committed_to` as a single field assumes a creature has one commitment.** A creature cannot garrison and escort simultaneously, so that holds — but it should be confirmed against the alliance and raiding specs rather than inferred.
5. **Discovery timestamps are stored per entry and shown in the Codex.** That is 34 timestamps per player for a feature nobody has asked for. It is cheap, and it may be worth cutting anyway.

---

*Owns: entity shapes, relationships, retention and the authority split. Does not own: any value, wave contents, or the simulation itself (`broodline_combat_engine.md`).*
