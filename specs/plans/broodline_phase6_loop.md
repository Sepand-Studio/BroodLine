---
status: current
folder: 03-technical
note: >
  Phase 6 design. The loop: the roster, region node state, lazy accrual and
  claim, the server-rolled splice, and the entitlement boundary Phase 5 left
  open knowingly. Precedes the implementation plan.
---

# Broodline — Phase 6 Design: The Loop

*The roster arrives, and with it the one check Phase 5 was not able to make*

> **CURRENT — design, not a plan.** This settles what Phase 6 builds and how it
> is structured. The task-by-task plan is a separate document under
> `implementation/`. Deployables, the language boundary, lazy accrual and
> retention live in `broodline_solo_execution.md`; determinism and the replay
> format live in `broodline_combat_engine.md`; the authority split and lineage
> retention live in `broodline_data_model.md`; coverage and the splice's cost
> live in `broodline_sample_economy.md`. This document owns none of them and
> cites all four.

---

## 1. Scope

`broodline_solo_execution.md` §8.2 scopes Phase 6 as *"Region, nodes, lazy
accrual, claim, server-rolled splice, splice confirmation per
`broodline_splice_confirm_spec.md`,"* done when *"Harvest → splice → fight →
reward closes without leaving the app."*

**Two amendments.** The done-when acquires the entitlement boundary Phase 5
could not cross — §6, and it is the sentence that phase was forbidden from
claiming. And the engine content fill stops being deferrable, because with one
authored trait a splice returns a copy by construction and the middle verb of
the done-when is unreachable — §2.2.

| | In scope | Deferred |
|---|---|---|
| **Schema** | `creatures`, `creature_tombstones`, `arks`, `node_depletion`, `harvest_positions`, `splices` | Convoys, alliances, stakes, sample stacks |
| **Routes** | A deployment on `wave/start`; `region/state`, `node/claim`, `splice/preview`, `splice/commit` | Relocation, retirement, fusing, the Transit Board |
| **Contract** | `SimulateEcho` gains the deployment. Additive, `api` → `sim` direction only | Nothing on the Unity → `api` direction beyond the routes below |
| **Engine** | Traits up from one, the raiders their counters answer, a second authored wave | The remaining raiders, Aberrant traits, region defence |
| **Map** | One region, two node types, derived rotation | Relocation, adjacency, the Drive, Apex Veins, the other twenty-nine regions |
| **Money** | Shard credit on claim, the Splice Charge debit | Commerce, receipts, Splice Roulette |
| **Client** | Minimal `Broodline.UI` — region, roster, splice, deployment select | Art, FTUE beats, the Codex sheet, the replay viewer |
| **Infra** | None. No new deployable, no new bucket, no scheduler | Everything at `solo_execution` §10, all still on their triggers |

**This is the first phase that can lose a player something.** Every phase before
it added state. This one consumes it: a splice destroys two creatures
permanently, and the confirmation that precedes it is a design deliverable with
its own spec rather than a screen detail — §5.5.

---

## 2. Five findings the design has to absorb

None appear in any document. All five come from reading §8.2's one line against
`solo_execution` §§5.5 and 5.6, `data_model` §§2 and 4, `sample_economy` §§7
and 8, and against what the repository actually contains.

### 2.1 The replay records what was deployed, not which creatures

This is the finding that shapes the phase, and it is invisible until you open
the struct.

`CreatureSpec` — [`SimState.cs`][simstate] — is `Species`, `Trait1`, `Tier1`,
`Trait2`, `Tier2`, `Instinct`, `Pocket`. **There is no creature id in it**, and
there should not be: `data_model` §3 is explicit that a creature *is* species,
two traits with coverage, one Instinct and a generation. Identity is a storage
concern, and the engine has no business holding one.

So "check the deployment against the roster" is not the id lookup Phase 5's
§2.2 makes it sound like. Read literally against what the replay contains, it
is a **shape match** — does this player own five creatures whose seven fields
match these five specs? — and a shape match is the wrong check. Two identical
creatures are indistinguishable under it, and a player who once owned a
matching creature can deploy its ghost forever.

> **Resolved: the deployment is fixed at issuance, not checked at submit.**

`wave/start` takes creature ids, checks them against the roster, and resolves
them to specs **from that roster**. The issuance stores the resolved specs. At
submit, `sim` echoes the deployment and `api` compares it against what it
stored — the comparison Phase 5 already runs on `seed` and `waveId`, extended
by one field.

The hole then closes by construction rather than by check: a deployment the
player does not own **cannot be expressed**, because the only path to a spec is
through a row they own. §6 states the mechanism exactly.

Two consequences worth naming here rather than discovering in implementation.
The replay format does not change, so `SimVersion` does not bump and the
determinism corpus stays valid. And ownership is refused on the **cheap** path,
before a simulation is paid for, which the submit-side alternative cannot do.

### 2.2 One authored trait means every splice returns a copy

`solo_execution` §8 scopes the slice's content as *"one species, one region, two
node types, five waves, and enough traits that splicing has a real outcome space
rather than returning a copy."* That last clause is usually read as a content
nicety. It is not.

The engine today declares `Trait { None = 0, Chill = 1 }` — [`Ids.cs`][ids].
**One real combat trait.** A splice rolls its second combat slot from the
parents' remaining pool, and with one trait in existence that pool holds one
value. Every splice returns a copy of its parents, the forecast is a certainty,
and the mutation roll has nothing to mutate *into*.

So the trait fill is not content polish that can follow the machinery. It is
**load-bearing for the done-when**, and a phase that shipped the splice against
`Trait { None, Chill }` would have shipped a function that provably cannot do
the thing its screen promises.

**The wave fill is separable and the trait fill is not.** Waves reach two here —
the minimum that makes `weakenings.md` row 5 constructible, §9 — and waves three
through five fall to Phase 7. Traits go up far enough that the roll has an
outcome space. §7 states what that costs.

### 2.3 Rotation is server-scoped, so `solo_execution` §5.5 does not settle it

`solo_execution` §5.5 is unambiguous about accrual: **never tick players.**
Fifty thousand sleeping players cost zero background CPU and no job fleet is
ever required.

`data_model` §7 then says *"node rotation and control both resolve on the weekly
tick,"* and it is tempting to read §5.5 as having already settled how. It has
not. §5.5's argument is about **per-player** work and its force comes entirely
from the player count; rotation is **server-scoped** — thirty regions on one
server — so the cost argument that rules out ticking players says nothing at all
about ticking a server. A weekly job over thirty rows is genuinely cheap.

The decision is made on different ground, and it needs stating because the
obvious reading is wrong:

> **Rotation is derived, not scheduled. The node set for a region is a pure
> function of `(server_id, region_id, epoch, seed)`.**

Three reasons, none of them cost. A scheduled job needs infrastructure this
phase otherwise does not need at all — a scheduler, an internal endpoint, an
auth surface on it. It must be idempotent against double-fire and must catch up
after downtime, which is two failure modes that a pure function does not have.
And it makes the weekly tick **untestable without a clock**, where a pure
function over an epoch is a unit test that runs in a millisecond.

`servers` already carries `tick_day_of_week` and `tick_minute_of_day`, so the
epoch is computable from a row that exists. Only **depletion** writes, and it
writes keyed by epoch, so last epoch's rows are dead by construction rather than
by a sweep.

### 2.4 Charges are not the binding constraint on splicing — base stock is

`sample_economy` §8 corrects four documents that assumed a player's splice
cadence follows from their Splice Charge income. It does not, because a splice
consumes two creatures: fifteen splices a day requires thirty base-stock
creatures a day against a roster floor of twenty, and no supply model in the set
produces that.

This matters to Phase 6 specifically because it decides **whether the loop
closes at all.** A splice is net −1 creature. If nothing replenishes the roster,
the loop runs for as many splices as the player has fodder and then stops, and
the phase's done-when is a sentence about a machine that seizes.

`base_stock` §3 gives the supply lines, and one of them is a floor:

> **Guardrail: wave-completion base stock never scales with any facility,
> purchase, tier or event.**

That guardrail is the reason the loop is closeable. Phase 6 therefore grants
base stock on the **verified** submit path — the same transaction that credits
the wave reward — and again on claim, region-weighted, scaled by the Harvest
Array. §4.3 and §7 give the shapes; the rates are content and are owed, §11.

### 2.5 `committed_to` has had no writer since it was designed

`data_model` §2 lists `committed_to` on the creature and explains its purpose:
*"`committed_to` is why the splice screen can warn"* — the confirmation has to
be able to say that a parent is garrisoned, escorting or deployed.

Nothing has ever set it, because nothing could: there were no creatures. It
arrives in this phase with a field that is already specified and still unwritten,
and the deployment-at-issuance decision gives it its **first writer**: set to the
issuance id at `wave/start`, cleared at settle.

That is not a convenience. It is what stops a player splicing away a creature
that is currently out fighting — a race that is otherwise wide open between
`wave/start` and `wave/submit`, and which would let the deployment stored on the
issuance outlive the roster rows it was resolved from.

---

## 3. The roster

### 3.1 `creatures`

Server-scoped like every player-owned row, per `solo_execution` §12's first
guardrail: `server_id` leads the primary key.

The columns follow `data_model` §2 exactly and add nothing. Species, generation,
the two combat TraitInstances as `(trait, coverage_tier)` pairs, the Instinct,
the nullable player-set name, `is_founder`, `parent_a` / `parent_b`,
`hp_current`, `regen_until`, `committed_to`, `acquired_at`.

**Two constraints the SQL carries and the typed surface cannot.** A
TraitInstance holding an Aberrant has `coverage_tier = null` rather than `0`,
per §2 — null and zero must not be conflated, because zero would sort and
display as "less than tier I", so the CHECK permits null and forbids zero.
And `generation >= 1`, with base stock at 1 and a child at `max(parents) + 1`.

**No level, no XP, no power score, no stored stats** — §3 of the data model, and
the reason is that stats come from `Stats.CreatureHp` and its siblings in the
engine. Storing them per creature would let them drift from the table and make a
balance patch a data migration.

### 3.2 Lineage retention ships in the first migration

`solo_execution` §5.6 names this as one of two rules that are **storage-shaped**
and must exist from the first migration, because retrofitting means reprocessing
every row.

> Retain ancestors five generations deep. Retain all Founders permanently.
> Prune everything else to a tombstone `{id, species, generation, was_founder}`.

The arithmetic `data_model` §4 runs is the justification: at six splices a day a
core player destroys twelve creatures daily, which over two years is roughly
**nine thousand** dead records per player, against **~300** under the rule. The
constraint that produces the saving was already in the design for narrative
reasons — bible §2.1 caps the lineage view at five generations, so an ancestor
six deep is never displayed and never needs to exist.

**Creature ids are never reused, including for pruned records.** A reused id
attaches a dead creature's lineage to a living one, and that is a bug that gets
reported as a ghost. The id space is therefore allocated, never recycled, and
`creature_tombstones` holds the pruned rows at about forty bytes each.

Pruning runs on the splice path, where the depth changes, rather than as a
sweep — the same reasoning as §2.3, applied to a different problem: work that
can happen on a write that is already occurring should not become a job.

### 3.3 `arks`, and the facilities that are not a table yet

One row per player. It carries the region the Ark is parked in — a constant this
phase, because there is one region — and three facility tiers: Harvest Array,
Hatchery, Splicing Chamber. **Pinned, with no upgrade path.**

**Harvest Array and Hatchery pin at tier 1. The Splicing Chamber pins at tier 3,
and that is not an arbitrary difference.** `combat_numbers` §7 ties the Chamber's
tier to a maximum generation and through it to a coverage ceiling: tiers 1–2 cap
a creature at **G2**, which caps coverage at **Tier I**.

A G2 cap does not break the loop — base stock is G1, so G1 × G1 → G2 keeps
working indefinitely and the loop closes as often as supply allows. What it
breaks is §5.3: a **Tier I** recessive trait cannot carry "one tier lower," so
the phase's most interesting coverage rule would be unreachable in the only
configuration the phase ships. Tier 3 caps at G4 and Tier II, which makes the
downtier a thing that actually happens to a player.

The cost of the choice is one integer, and it buys the difference between
shipping a rule and shipping a rule nobody can observe.

The tiers are columns rather than rows in a `facilities` table, and that is a
deliberate trade worth stating plainly because it costs a migration later.
`data_model` §1 gives an Ark six facilities. Phase 6 reads three of them and
upgrades none. Six rows per player where three columns are read is structure
built for a system that does not exist, and the accrual formula needs only that
the multiplier come from **somewhere real** rather than a hardcoded `1.0`, so
that the shape is honest and the later change is an `UPDATE` rather than a
rewrite.

**The `facilities` table is owed, and its trigger is the first upgrade path** —
§11. What this costs is one migration at that point, and what it buys is not
shipping five-sixths of a table that nothing reads.

The Hatchery's **floor of 20** (bible §7.2) is the roster cap, enforced on every
grant path. `sample_economy` §8 is worth reading alongside it: at twenty slots a
core player holds under two days of splice fodder, and the cap is supposed to
bind slightly. The same section sets the bound on that bound — *"roster capacity
never binds below what the counter system requires"* — because a cap that forces
a player to drop a counter is a soft lockout rather than pressure.

---

## 4. The map

### 4.1 The node set is derived, never stored

```
nodesFor(serverId, regionId, epoch, seed) -> NodeSet
```

Pure, total, and the only authority on which nodes exist. `epoch` comes from the
server's tick fields; `seed` is the server's. Two node types this phase, per
bible §5.3:

> **"Rotation" means something narrower here than bible §5.4 means by it**, and
> an implementer who reads the two as the same thing will look for a mechanism
> that cannot exist. §5.4 rotates Rich Deposits *between* regions — *"shifts a
> portion to new regions every seven days."* With one region there is nowhere to
> shift to, so what the epoch boundary does in this phase is **respawn the Rich
> Deposit in place at full yield**, discarding last epoch's depletion row. The
> cross-region shuffle arrives with the map, and `nodesFor` already takes the
> `regionId` it will need.

| Tier | Yield | Lifespan |
|---|---|---|
| **Common Vein** | 1× | Never fully depletes. The livable floor, and it can never be taken from anyone |
| **Rich Deposit** | 3× | Depletes in ~6 days of active harvesting, timed to run dry about a day before the refresh |

**Apex Veins are out**, and they take the catalyst with them: `sample_economy`
§9 makes *"one per successful Apex Vein extraction — nothing else"* the
catalyst's only supply line, so a phase without Apex Veins has no catalyst by
construction rather than by exclusion. The Aberrant sub-roll therefore ships at
its flat base rate, §5.4.

The Common Vein never depleting is what stops §2.4's seizure from being possible
at all: a player who exhausts the Rich Deposit and cannot relocate still has a
floor to draw on. With one region and no relocation — the map scope this phase
takes — that floor is the whole reason the loop keeps running.

### 4.2 Accrual is a pure function with a twelve-hour cap

```
accrue(lastSettledAt, now, rate, arrayTier, remaining) -> units
```

Also pure, and it **takes a timestamp rather than reading a clock**, which is
what makes every boundary in it testable without waiting.

`solo_execution` §5.5 gives the four inputs: `last_settled_at`, the node's
current rate, the Harvest Array multiplier and the twelve-hour offline cap.
Order matters — clamp elapsed time to the cap **first**, then bound by the
node's remaining yield, or a long absence against a nearly-dead node reports
units the node cannot supply.

**Integer arithmetic throughout, with the rate as a rational.** This feeds a
ledger credit. A float that drifts by a thousandth per claim is a currency bug
that compounds silently and is unfalsifiable after the fact, and the engine's
no-floating-point discipline exists for a related reason — the discipline is
worth keeping on this side of the language boundary too, where it is a
correctness argument rather than a determinism one.

`harvest_positions` carries `last_settled_at` per player per node per epoch.
`node_depletion` carries `harvested_units` and `depleted_at`, keyed by epoch.
Nothing else about a node is stored.

### 4.3 Claim

`POST /v1/node/claim` — one transaction, under `withIdempotency` as built in
Phase 4, writing:

1. The shard credit through `credit()`, with its ledger row in the same
   transaction — `solo_execution` §12
2. Base-stock grants, region-weighted, scaled by the Harvest Array at **half
   the rate shards scale** (`base_stock` §3.1 — shards are throughput and can
   spread widely; base stock is species, and species are counters)
3. `last_settled_at = now`
4. `harvested_units += units`, and `depleted_at` when the node reaches zero

A grant that would exceed the Hatchery cap is refused before the transaction
opens, not truncated inside it — a partial grant that silently drops creatures
is the kind of loss a player reports as theft.

---

## 5. The splice

### 5.1 One distribution, two callers

This is the load-bearing decision of §5 and it is a structural one, not a
stylistic one.

```
spliceDistribution(parentA, parentB, lockedSlot, bundle) -> Distribution
```

`POST /v1/splice/preview` returns it. `POST /v1/splice/commit` **samples** it
with a server-generated seed. One function, two callers.

The alternative — a forecast function beside a roll function — is the normal way
to build this and it is wrong here. Bible §2.6 commits to publishing odds, and
`genetics_system` §4 calls showing them before the charge is spent
non-negotiable *"the mechanic regulators are currently most interested in."*
Two functions make the published odds a **claim about code**. One function makes
them a **property of it**, and the difference is a test that can exist: sample
the distribution and assert convergence to what preview returned.

The seed is stored on the `splices` row for the same reason wave seeds are
stored — a paid randomised action with published odds has to be reproducible
after the fact, or a dispute has no evidence on either side.

**Server-authoritative, and `data_model` §8 says so in the strongest terms it
uses anywhere:** *"The splice roll is server-side and non-negotiable."* Including
the mutation roll.

### 5.2 The three slots

Per `broodline_splice_confirm_spec.md` §2, which supersedes the four-slot model
in the archived `broodline_genetics_system.md`:

| Slot | Fills from | Resolution |
|---|---|---|
| Combat 1 | Any of the parents' four combat traits | **Player locks it.** Guaranteed to carry |
| Combat 2 | The remaining three | Rolls, with displayed odds |
| Instinct | The parents' two Instincts | Rolls, modified by affinity and DOM/REC |

**Body is free choice** between the two parents' species — no randomness, and
the player's only controlled lever against the roll. Generation is
`max(parents) + 1`, bounded by the Splicing Chamber's ceiling; a splice that
would exceed it is blocked with the Chamber upgrade surfaced in the message
rather than a bare error.

> **The archived genetics document is not the spec of record here**, and the
> difference is not cosmetic: it gives four slots and a 3% mutation rate against
> the confirm spec's three slots and 9%. It is marked `status: superseded` and
> an implementer reading it would build a different game. Named because it is
> the most plausible wrong turn in this section.

### 5.3 Coverage, and what a splice costs

`sample_economy` §7 calls this *"the largest sink in this document"* and notes it
was unstated anywhere before that document wrote it down:

- The **locked** combat trait carries at its parent's full coverage
- The **rolled** combat trait carries at full coverage if **dominant**, and
  **one tier lower if recessive**
- The two traits that do not carry **return nothing**

The recessive downtier is recoverable by re-fusing. The two non-carrying traits
are not, and that asymmetry is why retirement exists as a system — which this
phase does not build, §10.

> **The downtier needs a floor, and no document states one** — but the design
> set determines it, so this is a derivation rather than a ruling.
> `sample_economy` §7 says a recessive trait carries "one tier lower" without
> saying what happens at Tier I, where there is no lower tier. Bible §2.2
> settles it: a downtier *"costs coverage and never access — the trait still
> works, it covers less."* A Tier I trait that downtiered out of existence would
> cost access, which that sentence forbids, and `combat_numbers` §4.1 says the
> same thing from the other side — *"tier decides how much a trait covers, never
> whether it works."*
>
> **Therefore: the downtier floors at Tier I.** Coverage never reaches zero or
> null through this path. Null stays reserved for Aberrants (§3.1); conflating
> the two would render a downtiered trait as an Aberrant. Owed back to
> `sample_economy` §7 as an edit, §11.

DOM/REC is per-trait content and lives in the bundle. It is owed, §11.

### 5.4 Mutation

`sample_economy` §9, at its base rates, with no catalyst and no Surge:

| | Value |
|---|---|
| Base mutation rate | **9%** per splice |
| Aberrant sub-roll | **5%** of mutations |

Mutation is the only entry point for Apex traits into the economy, and it is
important that it is the **free** mechanic — every player has equal access to
it, which is what keeps the game's most distinctive object unbuyable.

**There is no failure state.** `genetics_system` §4 is right about this and it
survives that document's supersession as a UX rule: the worst outcome is rolling
traits you already had, and there is never a "SPLICE FAILED" screen. Losing two
parents and a charge for a lateral result is disappointment enough.

### 5.5 The confirmation is a design deliverable, not a screen detail

§8.2 names `broodline_splice_confirm_spec.md` in Phase 6's deliverable line, and
that is unusual enough to be deliberate — no other phase row cites a screen
spec. The reason is in that document's §1: the designed Splice Chamber ends on a
Cost row and a **Begin Splice** CTA, the player learns both parents are gone on
the *next* screen, and the genetics spec names accidental consumption as the
most likely source of refund requests and one-star reviews.

What this phase must ship, minimally:

- **The destruction notice** directly above the CTA, naming the actual
  creatures and their player-set names, with the second line — that the record
  survives in the lineage — because it is the reassurance that makes the loss
  survivable, and it is true
- **The CTA labelled with its cost**, not "Begin Splice". A player who taps past
  everything else still reads the thing they tap
- **The standard confirm dialog**, dismissible
- **The Founder dialog**, second, never suppressible, with buttons that state
  the outcome rather than Yes/No, and **no destructive default**
- **The coverage warning** — "Chill III will not carry" — per §5.3 above and
  `sample_economy` §7's screen requirement
- **The forecast**, odds visible before the charge is spent

Styling, the lineage reveal animation and FTUE beat 8 are Phase 7's. The copy
and the interrupt are not, because they are what the spec exists to guarantee.

---

## 6. The entitlement boundary closes

### 6.1 The deployment is fixed at issuance

`POST /v1/wave/start` grows a body:

```
{ waveId, deployment: [ { creatureId, pocket } ] }   // at most Stats.DeploymentCap
```

`api` then, in one transaction:

1. Refuses a deployment longer than `Stats.DeploymentCap` (5) — the parse layer
   bound, matching how `waveId` is bounded today
2. Refuses any id the player does not own → **409**
3. Refuses any creature with `committed_to` not null → **409**
4. Resolves each id to a `CreatureSpec` **from the owned row**
5. Stores the resolved specs on the issuance
6. Sets `committed_to = issuanceId` on each creature

**Step 4 is the whole mechanism.** There is no path from a client-supplied value
to a spec; the only source of a spec is a row the player owns. A deployment they
do not own is not rejected — it is **inexpressible**.

### 6.2 The contract change

`SimulateEcho` gains the deployment it simulated. That is the phase's only
contract change and it is additive, on the `api` → `sim` direction Phase 5
already built, generated and CI-diffs.

The echo exists for exactly this: its comment says it is *"what sim echoes back
so api can key a ledger row WITHOUT PARSING ANYTHING."* Extending it keeps that
property — `api` compares specs it already holds against specs sim reports, and
still never learns what a trait does, which is `solo_execution` §6.1's line.

At submit, a deployment in the echo that does not match the issuance is a
**breach**, handled on the path Phase 5 built for a seed or wave-id mismatch.

### 6.3 The marker test flips

Phase 5 left one hole open knowingly and marked it with a test that asserts the
hole rather than a comment that describes it —
[`adversarial.test.ts:656`][adversarial]:

```
it('CAN still deploy creatures the player does not own — Phase 6', ...)
```

**That test flipping from passing to expecting 409 is this phase's proof that
the boundary closed.** It is not a test to delete and replace; it is a marker
whose whole design was to fail the day the roster landed, and rewriting it in
place is what "Phase 6 has landed the roster check" looks like in the suite.

Phase 5's §2.2 also wrote a sentence into its own done-when to stop the suite
being written against the stronger claim. **That narrowing is now lifted**, and
the stronger sentence — *a tampered submission earns nothing* — becomes true for
the first time. It should be stated as newly earned rather than quietly adopted.

---

## 7. Engine content

`Trait` goes from `{ None, Chill }` to a set large enough that §2.2's outcome
space exists. **The count is content and is owed — but the floor is not.** A
splice rolls combat 2 from the three traits the locked slot leaves behind, so
the roll is degenerate unless two arbitrary parents can differ in their combat
pool. One trait is the degenerate case §2.2 names; two makes the roll
non-trivial only for the one pairing that happens to differ. The fill has to
clear that floor by enough that a roll between two roster creatures is usually
uncertain, and **whoever sets the number owes that property, not a count.**

`RaiderType` goes from `{ Courser }` to the raiders those new
traits answer, because `combat_engine` §5.4's invariant is that **no two raiders
in a wave share a counter** — traits and raiders scale together, and adding a
trait without its raider adds an answer to no question.

**`RaiderTypeCounts.RaiderTypeCount` must move with the enum.** Its comment says
so and states the consequence: `WaveDef`'s composition scans are bounded on that
constant, and a stale value stops enforcing the invariants silently instead of
throwing.

`WaveDef.ForId` gains a second authored wave. Today it returns wave 6 and throws
`WaveCompositionException` for every other id — which is correct behaviour and
is why the second wave is a content change rather than a code change.

**The second wave must carry a different reward from wave 6.** That is the
precise thing `weakenings.md` row 5 needs, §9, and a bundle fixture does not
substitute for it — Phase 5 checked, and booked the gate as the engine.

Values are content and are owed, §11. This document fixes the **shape** of the
fill and not its numbers.

---

## 8. The client

`Broodline.UI` is created here. It does not exist today — the client carries
`Model`, `View`, `Net` and `Game` assemblies and Phase 5 deferred this one
explicitly.

Four screens, at placeholder fidelity:

| Screen | Minimum |
|---|---|
| **Region** | The region's nodes, accrual visibly advancing, a claim action |
| **Roster** | The player's creatures, their traits and coverage tiers, `committed_to` state |
| **Splice Chamber** | Parent select, body choice, the locked slot, the forecast, and everything §5.5 requires |
| **Deploy** | Creature select before `wave/start`, bounded at five |

**Placeholder art, placeholder layout.** `solo_execution` §8.2 gives Phase 7
*"one species of real art"* and the FTUE beats, and §8.3 names art as the
variable that makes Phase 7 the least predictable phase. Pulling styling forward
into a three-week phase moves that unpredictability without removing it.

What is **not** placeholder is the confirmation copy — §5.5. The interrupt is
the deliverable.

---

## 9. Testing

The gates that would fail against a broken implementation, rather than the ones
that pass against any:

**Pure functions, at their boundaries.** `accrue` across the twelve-hour cap on
both sides; `accrue` against a nearly-depleted node, asserting the clamp order
of §4.2; `accrue` summed over many small intervals against one large interval,
asserting no integer drift. `nodesFor` across an epoch boundary, and asserting
the same epoch is stable under repeated calls.

**The splice's disclosure.** Sample `spliceDistribution` and assert convergence
to what `preview` returned. This is the test that only exists because §5.1 made
them one function, and it is the phase's answer to bible §2.6.

**Coverage carry.** The recessive downtier, the dominant full carry, and that
the two non-carrying traits return nothing — §5.3, each as a named case, because
the asymmetry is the part a reasonable implementer would smooth over.

**Ownership, adversarially.** The flipped marker — §6.3 — plus: deploy an
unowned id → 409; deploy a `committed_to` creature → 409; deploy six → 400;
swap the deployment between start and submit → breach; splice away a creature
that is currently deployed → refused.

**Lineage.** Prune at depth five, Founders survive unpruned, the tombstone's
shape, and that a pruned id is never reissued.

**`weakenings.md` row 5 becomes constructible and must actually be run.** Phase
5 booked it *"owed against the Phase 6 engine content fill"* and noted the
shipped coverage proves the wiring rather than the property. The second authored
wave is what makes the property testable, and booking it a second time instead
of running it would be the phase quietly inheriting what it was meant to close.

---

## 10. What this design deliberately does not do

- **No relocation, no Drive, no adjacency.** One region, so there is nowhere to
  move. Bible §5.2's system, with its transit times, harvest-forfeit-on-packing
  and stowed defenders, is its own work
- **No Apex Veins**, and therefore **no catalyst** — §4.1. The Aberrant sub-roll
  ships flat
- **No samples, fusing, Gene Vault or Sample Store.** Coverage-carry rules only,
  §5.3. Coverage exists as creature data; the economy that raises it does not
- **No facility upgrades.** Tiers pinned at 1, and the `facilities` table is
  owed rather than built — §3.3
- **No retirement.** It is the recovery path for §5.3's unrecoverable loss and
  it needs the sample economy to have anywhere to bank into
- **No convoys, Collectors, raids, alliances or territory.** None has an entity
  in this phase and raids need the Transit Board
- **No Codex, no FTUE beats, no replay viewer.** Phase 7 and a client phase
- **No Splice Roulette, no pity counter, no monetization surface**
- **No new infrastructure.** No scheduler — §2.3 — no new deployable, no new
  bucket. The triggers at `solo_execution` §10 are all still unmet

---

## 11. Decisions owed

Five carried from Phase 5 and still open, and five this design creates.

| Decision | Why it matters | State |
|---|---|---|
| **Enable GitHub Actions at `Sepand-Studio`** | Needs `admin:org`; no session can do it. `tests.yml` and `determinism.yml` are committed and have never run | **Owed since Phase 4.** This phase adds a trait/raider enum change, which is exactly the class of change the determinism gate exists to catch across runtimes. It now blocks more than it did again |
| **Correct `client_architecture` §2's `ref readonly SimState`** | Still specifies a shape that guarantees nothing. Phase 3 resolved it in code and booked the edit; Phases 4 and 5 booked it again | **Owed since Phase 3.** Four times deferred. Not this phase's to fix, and recorded so the count stays honest |
| **Raise `minimumClientVersion` for the first time** | The mechanism has shipped since Phase 4 and has never been used | **Unchanged.** This phase gives it its first real trigger: `wave/start`'s body grows, so an old client's call is malformed rather than merely stale |
| **Narrow `solo_execution` §3.1's degradation row** | Contradicted by shipped code since Phase 5 | **The edit is still owed.** Owner: `solo_execution` §3.1 |
| **Guard §4.3's retention split** | `weakenings.md` row 7. The sweep does not exist, so neither does its test | **Owed against the retention sweep.** This phase adds no sweep — §2.3's reasoning removes the need for one on nodes, but it does not discharge the issuance sweep |
| **~~Reward for the second authored wave~~** *(new)* | §7. Row 5's gate is two waves with **different** rewards; two waves with the same reward closes nothing | **Discharged — it was already authored.** `broodline_waves_01_12.md` gives wave 7 as *1 Lash · 6 Skirmishers*, integrity 3, first clear **230 shards**, against wave 6's 40 in `config/bundles/0.1.1`. Different by 190, so row 5's gate is constructible from content that exists. The samples in wave 7's reward line are dropped — no sample economy this phase, §10 |
| **DOM/REC per trait** *(new)* | §5.3. Dominance decides the recessive downtier, and it is the one input to the splice that no document supplies | **Genuinely owed, and narrower than it first looked.** The traits themselves are **authored** — `broodline_combat_numbers.md` §4.2 gives eight counters with species, raider and three tiers each, §4.3 gives four that counter nothing, and §6 gives eight raider profiles. Those are a **port**, not a decision. Dominance is adopted as a mechanic (bible §2.2, `reconciliation` 3.5) but **assigned to no trait anywhere in the set**, and the splice cannot roll without it |
| **Node rates and the Rich Deposit's depletion budget** *(new)* | §4.1. "~6 days of active harvesting" is a duration; `accrue` needs a rate and a total in units | **Owed to `broodline_region_roster.md` / bible §5.3.** Must be expressed so the six days falls out of the arithmetic rather than being asserted beside it |
| **~~The Splicing Chamber's tier-1 generation ceiling~~** *(new)* | §5.2 | **Discharged, and it changed a decision.** `broodline_combat_numbers.md` §7 already carries the table: Chamber tiers 1–2 cap a creature at **G2** and coverage at **Tier I**. That is why §3.3 pins the Chamber at **tier 3** rather than tier 1 — at Tier I the recessive downtier has nothing to drop to and §5.3's rule ships unobservable |
| **The downtier's floor, back into `sample_economy` §7** *(new)* | §5.3. That document says "one tier lower" and stops, leaving Tier I undefined | **Derived here, and the edit is owed there.** Bible §2.2's *"costs coverage and never access"* forces the floor to be Tier I. The derivation belongs in the document that owns the rule, not only in a phase design |
| **The `facilities` table** *(new)* | §3.3. Three tiers ship as columns on `arks`; six facilities and an upgrade path need rows | **Trigger: the first facility upgrade.** Costs one migration at that point, and the alternative is shipping five-sixths of a table nothing reads |

> **One boundary restated, because this phase is where it moves.**
> Phase 5's §2.2 required its done-when to be written *narrower* than "a
> tampered submission earns nothing," since a deployment the player did not own
> still won. §6 closes that. The stronger sentence becomes true here — and it
> should be claimed **as newly earned in this phase**, not quoted as though it
> were always the case, or the record loses the fact that it was open for a
> phase and why.

---

[simstate]: ../../engine/Runtime/Combat/SimState.cs
[ids]: ../../engine/Runtime/Combat/Ids.cs
[adversarial]: ../../services/api/test/adversarial.test.ts
[weakenings]: ../../services/api/test/weakenings.md

---

*Owns: the Phase 6 slice boundary, the roster's shape, the deployment-at-issuance
mechanism that closes Phase 5's entitlement hole, derived node rotation, the
accrual and claim paths, and the one-distribution-two-callers rule that makes
published splice odds a property rather than a claim. Does not own: deployables,
the language boundary, lazy accrual's policy or retention
(`broodline_solo_execution.md`), determinism or the replay format
(`broodline_combat_engine.md`), the authority split, the creature's fields or
lineage retention (`broodline_data_model.md`), coverage and the splice's cost
(`broodline_sample_economy.md`), the confirmation's copy
(`broodline_splice_confirm_spec.md`), reward values
(`broodline_campaign_structure.md`), trait and raider values
(`broodline_combat_numbers.md`), or phase sequencing
(`broodline_solo_execution.md` §8.2).*
