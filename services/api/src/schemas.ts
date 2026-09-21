import { extendZodWithOpenApi } from '@asteasolutions/zod-to-openapi'
import { z } from 'zod'

extendZodWithOpenApi(z)

/**
 * TypeScript is the source of truth for the Unity -> api direction, per
 * solo_execution section 6. These schemas are what the OpenAPI document is
 * generated FROM, and NSwag generates the C# client from that document.
 *
 * Nothing in client/Assets/Generated is ever hand-edited, and CI fails on a
 * non-empty regeneration diff.
 */
export const CreateAccountRequest = z.object({
  birthdateBand: z.string().openapi({ example: 'adult' }),
  storefrontRegion: z.string().openapi({ example: 'us-central1' }),
}).openapi('CreateAccountRequest')

export const CreateAccountResponse = z.object({
  accountId: z.string().uuid(),
  playerId: z.string().uuid(),
  serverId: z.number().int(),
  accessToken: z.string(),
  refreshToken: z.string(),
  balances: z.record(z.string(), z.number().int()),
}).openapi('CreateAccountResponse')

export const DeleteAccountResponse = z.object({
  deleted: z.boolean(),
}).openapi('DeleteAccountResponse')

export const SyncResponse = z.object({
  player: z.object({
    playerId: z.string().uuid(),
    serverId: z.number().int(),
  }),
  balances: z.record(z.string(), z.number().int()),
  campaign: z.object({
    highestWaveCleared: z.number().int(),
    milestonesClaimed: z.number().int(),
  }),
  timers: z.array(z.object({
    kind: z.string(),
    completesAt: z.string(),
  })),
  // tabs, waves and traits joined bundleVersion/minimumClientVersion in
  // Phase 7, Task 5 (design §4/§5 beat 1) - additive, so the Phase 6 client
  // keeps parsing this object.
  config: z.object({
    bundleVersion: z.string(),
    minimumClientVersion: z.string(),
    // client_architecture §9's reveal bar - a tab name to the progress value
    // that unlocks it.
    tabs: z.record(z.string(), z.number().int()),
    waves: z.array(z.object({
      id: z.number().int(),
      reward: z.object({
        currency: z.string(),
        amount: z.number().int(),
      }).nullable(),
    })),
    traits: z.array(z.object({
      id: z.string(),
      species: z.string(),
      counters: z.string().nullable(),
    })),
  }),
  // The FTUE facts the client derives its current tutorial beat from.
  // Nothing about FTUE progress is stored on the client - this, and this
  // alone, is the source of truth it polls on every cold start.
  ftue: z.object({
    founderNamed: z.boolean(),
    tutorialStockGranted: z.boolean(),
    splices: z.number().int(),
  }),
}).openapi('SyncResponse')

// Phase 5, Task 5, registered into openapi.ts's registry by Task 6 - see
// that file's header comment for why both wave routes were still absent
// from the generated contract until now.
// Phase 6, Task 8. design §6.1: the request carries an ID and a POCKET and
// nothing else, and that is the contract stating the mechanism rather than
// merely permitting it. A body that could name a species or a tier would be
// a body a modified client could deploy a creature it does not own with -
// the hole Phase 5 shipped knowingly and this closes. The specs are resolved
// server-side from the rows these ids name.
export const DeployedCreature = z.object({
  creatureId: z.string().uuid(),
  // Which pocket of the lane. Bounded only below (and at int32) by the API -
  // how many pockets a lane has is content, and `sim` is the authority; see
  // routes/wave.ts's MAX_POCKET.
  pocket: z.number().int(),
}).openapi('DeployedCreature')

export const WaveStartRequest = z.object({
  waveId: z.number().int(),
  // REQUIRED, AND NON-EMPTY. `DEPLOYMENT_FLOOR` (1) to `Stats.DeploymentCap`
  // (5) entries, naming distinct creatures. An ABSENT field is a body written
  // against the pre-Task-8 contract; an EMPTY array is a wave fought with
  // nothing, which `wave/submit` would pay for the day content authors a wave
  // the Ark survives undefended (Task 10's fix round - see DEPLOYMENT_FLOOR).
  //
  // NEITHER BOUND IS IN THE SCHEMA, and that is the same ruling the cap has
  // always had here: `routes/wave.ts`'s parse layer owns the bounds and this
  // file owns the SHAPE. `DEPLOYMENT_CAP` is a balance constant - issuance.ts
  // refuses to freeze it into a migration for exactly this reason, and a
  // generated contract is harder to change than a migration, not easier. The
  // floor is not a balance constant, but splitting the two across two
  // enforcement layers would be worse than keeping them together.
  deployment: z.array(DeployedCreature),
}).openapi('WaveStartRequest')

// A creature as deployed - engine `CreatureSpec`, and the mirror of
// services/api/src/db/schema.ts's CreatureSpec. No creature id: design §2.1,
// identity is a storage concern and the engine has no business holding one.
export const CreatureSpecDto = z.object({
  species: z.string(),
  trait1: z.string(),
  // NULLABLE for CreatureDto.tier1's reason: null is an Aberrant, which has
  // no coverage, and data_model §2 refuses to conflate that with zero.
  tier1: z.number().int().nullable(),
  trait2: z.string(),
  tier2: z.number().int().nullable(),
  instinct: z.string(),
  pocket: z.number().int(),
}).openapi('CreatureSpecDto')

export const WaveStartResponse = z.object({
  issuanceId: z.string().uuid(),
  // A decimal string, not a number - the seed is a ulong and a JSON number
  // loses precision above 2^53. See services/api/src/db/schema.ts's
  // int8String customType.
  seed: z.string(),
  waveId: z.number().int(),
  expiresAt: z.string(),
  // What was ACTUALLY issued, which need not be what the request asked for:
  // design §2.1 returns a live issuance rather than replacing it, so a second
  // start gets the first one's deployment back.
  //
  // NULLABLE, and honestly so rather than by oversight. The column is the
  // expand step of drizzle/0006_issuance_deployment.sql, so for the two hours
  // of ISSUANCE_TTL_MS after this handler ships a player can still hold a
  // live issuance minted by the previous build, which has no deployment and
  // never will. A contract that promised an array there would be lying for
  // exactly as long as it mattered.
  deployment: z.array(CreatureSpecDto).nullable(),
}).openapi('WaveStartResponse')

// Phase 5, Task 6.
export const WaveSubmitRequest = z.object({
  issuanceId: z.string().uuid(),
  // Base64, the serialized Replay bytes verbatim - api never parses a
  // replay, never learns what a trait does, never reimplements a rule
  // (design §3.2). This is a plain string rather than z.string().base64()
  // deliberately: the latter constrains ALPHABET, not that the decoded
  // bytes form a valid Replay, and the only real validator for that is
  // sim's own Replay.Deserialize on the other side of this boundary.
  replay: z.string(),
}).openapi('WaveSubmitRequest')

const BreachDto = z.object({
  tick: z.number().int(),
  raider: z.number().int(),
  lane: z.number().int(),
  type: z.string(),
  // combat_engine §7's three-boolean diagnosis, forwarded for the Wave
  // Defeat screen. api stores none of them and interprets none of them.
  access: z.boolean(),
  coverage: z.boolean(),
  placement: z.boolean(),
}).openapi('BreachDto')

// Phase 6, Task 5. The map's two routes - design §4.3.
//
// Registered into openapi.ts's registry in the SAME task that adds the
// routes, not a later one. Phase 5 shipped /v1/wave/start in code and left
// it out of the registry, so it was absent from the generated client
// entirely until the next task found it - see openapi.ts's own note on that
// path. A route the Unity client must call and has no generated method for
// is the same gap wearing a new name.
//
// DECLARED HERE, AHEAD OF `WaveSubmitResponse` - a Phase 7, Task 8 move.
// `const` bindings are not hoisted the way `function` ones are, so a schema
// declared below its first reference would throw at module load rather than
// at review time. `WaveSubmitResponse` is the earlier (Phase 5) schema and
// the newer consumer; this one moved rather than that one, since every
// other consumer (RegionStateResponse and friends, below) was already
// content to read it from here.
export const CreatureDto = z.object({
  creatureId: z.string().uuid(),
  species: z.string(),
  generation: z.number().int(),
  trait1: z.string(),
  // NULLABLE, and it is not an oversight to "fix": null is an Aberrant,
  // which has no coverage, and data_model §2 refuses to conflate that with
  // zero because zero would sort and display as "less than tier I".
  // 0005_loop.sql's coverage_tier_N_not_zero is the same rule in storage.
  tier1: z.number().int().nullable(),
  trait2: z.string(),
  tier2: z.number().int().nullable(),
  instinct: z.string(),
  // Founders only - 0005's only_founders_named.
  name: z.string().nullable(),
  isFounder: z.boolean(),
  // The issuance this creature is out fighting for, or null - design §2.5.
  // Its first writer is Task 8; it is on the DTO from the first response
  // that carries a creature because the roster screen renders it.
  committedTo: z.string().uuid().nullable(),
}).openapi('CreatureDto')

export const WaveSubmitResponse = z.object({
  result: z.string(),
  integrityRemaining: z.number().int(),
  breaches: z.array(BreachDto),
  // Absent rather than null on a loss, so a client cannot render a zero -
  // design §4.2's response shape, `{ result, integrityRemaining, breaches[],
  // reward? }`.
  reward: z.object({
    currency: z.string(),
    amount: z.number().int(),
  }).optional(),
  // ADDITIVE - Phase 7, Task 8. Every creature THIS settlement minted (base
  // stock, the Founder, the wave-6 Pale), so Post-Wave and Wave Defeat can
  // render what arrived without diffing the roster. Optional and absent
  // (never `[]`) when nothing was granted, so the Phase 6 client - which has
  // never heard of this field - keeps parsing every response exactly as it
  // always has.
  granted: z.array(CreatureDto).optional(),
}).openapi('WaveSubmitResponse')

export const WaveAbandonResponse = z.object({
  settled: z.boolean(),
}).openapi('WaveAbandonResponse')

export const ErrorResponse = z.object({
  code: z.string(),
  message: z.string(),
  details: z.unknown().optional(),
}).openapi('ErrorResponse')

// Phase 5, Task 9. POST /v1/account has returned a refreshToken since Phase
// 4 with no route able to redeem it - this is the route.
export const RefreshRequest = z.object({
  refreshToken: z.string(),
}).openapi('RefreshRequest')

export const RefreshResponse = z.object({
  accessToken: z.string(),
  // Rotation is deferred to the first non-TestFlight players (solo_execution
  // 6.4) - this is the SAME token the caller sent, not a freshly minted one.
  refreshToken: z.string(),
}).openapi('RefreshResponse')

export const RegionStateResponse = z.object({
  regionId: z.string(),
  // A plain integer, not a string: the epoch is a small counter derived
  // from the server's tick fields, not a seed, so it carries none of the
  // precision risk that makes WaveStartResponse.seed a decimal string. Same
  // judgement schema.ts's `bigint mode: 'number'` columns already make.
  epoch: z.number().int(),
  nodes: z.array(z.object({
    slot: z.number().int(),
    type: z.string(),
    // What a claim WOULD pay right now. Reading this settles nothing -
    // design §4.1 - so two reads in a row report the same number.
    accrued: z.number().int(),
    // null for a node that never depletes. The Common Vein is the livable
    // floor and can never be taken from anyone (bible §5.3), which is a
    // different statement from "its remaining yield is very large".
    remaining: z.number().int().nullable(),
    // Creatures a claim would grant right now. With `roster` below, this is
    // what lets a client predict the roster_full 409 instead of meeting it:
    // design §4.3 refuses the WHOLE claim, so the shards go unpaid too, and
    // a refusal a client could not see coming is one a player reads as the
    // button being broken.
    grants: z.number().int(),
  })),
  roster: z.object({
    count: z.number().int(),
    cap: z.number().int(),
  }),
}).openapi('RegionStateResponse')

// Phase 6, Task 12. `GET /v1/roster` - the listing no earlier task was
// assigned and which the phase's done-when needs: "harvest -> splice ->
// fight -> reward closes without leaving the app" requires a player to SEE
// and CHOOSE their creatures, and until this route existed the only creature
// lists a client ever held were the ones `node/claim` and `splice/commit`
// happened to return. A relaunch lost every id.
//
// A ROUTE OF ITS OWN RATHER THAN A FIELD ON `region/state`, for three
// reasons. `region/state` is the MAP: it is derived per request against one
// clock read (design 4.1), and the roster is not clock-dependent, so folding
// the list into it would make every map poll re-read and re-serialise the
// whole roster to answer a question about accrual. Its `roster: {count, cap}`
// is a HEADLINE deliberately - it exists so a client can predict the
// roster_full 409 - and the Splice Chamber and deployment screens need the
// members, not the count, at moments when they are not looking at the map.
// And the two have different cache lives: accrual moves every shard tick,
// a roster only when the player changes it.
//
// NO `count` FIELD. It is `creatures.length` by construction - both sides of
// that would come from the same `liveCreature()` read - and a second copy of
// a number is a second thing to disagree. `cap` IS here because it is not
// derivable from the list and the roster screen needs it to say "18 of 20".
export const RosterResponse = z.object({
  creatures: z.array(CreatureDto),
  // bible 7.2's Hatchery capacity for this player's Ark tier.
  cap: z.number().int(),
}).openapi('RosterResponse')

export const NodeClaimRequest = z.object({
  slot: z.number().int(),
}).openapi('NodeClaimRequest')

export const NodeClaimResponse = z.object({
  slot: z.number().int(),
  // Zero is a legitimate answer, not an error: a second claim moments after
  // the first has genuinely accrued nothing - design §4.3.
  shards: z.number().int(),
  creatures: z.array(CreatureDto),
  balance: z.number().int(),
}).openapi('NodeClaimResponse')

// Phase 6, Task 6. design §5.1's forecast - the value `POST
// /v1/splice/preview` returns and `POST /v1/splice/commit` (Task 7) samples.
//
// Registered into openapi.ts's registry in the SAME task that adds the route,
// for the reason the map's two routes state above: Phase 5 shipped
// /v1/wave/start in code and left it out of the registry, so it was absent
// from the generated client entirely until the next task found it.
export const SpliceLock = z.object({
  // Which of the parents' four combat traits the player locked - BY POSITION,
  // not by trait id. Two parents frequently share a trait (every base-stock
  // species carries Carapace in slot 2), so a lock naming only a trait would
  // be ambiguous about whose coverage carries.
  from: z.enum(['a', 'b']),
  slot: z.enum(['trait_1', 'trait_2']),
}).openapi('SpliceLock')

export const SplicePreviewRequest = z.object({
  parentA: z.string().uuid(),
  parentB: z.string().uuid(),
  locked: SpliceLock,
}).openapi('SplicePreviewRequest')

// ONE entry per distinct OUTCOME, not one per parent copy - see
// splice/distribution.ts. Two parents carrying Carapace publish it once, at
// the summed probability, because that is the odds rather than the pool.
const CombatOutcomeDto = z.object({
  trait: z.string(),
  // NULLABLE for the same reason CreatureDto.tier1 is: null is an Aberrant,
  // which has no coverage, and design §5.3's recessive downtier floors at
  // Tier I precisely so an ordinary trait can never arrive here as null.
  tier: z.number().int().nullable(),
  p: z.number(),
}).openapi('CombatOutcomeDto')

export const SpliceForecast = z.object({
  combat2: z.array(CombatOutcomeDto),
  instinct: z.array(z.object({
    instinct: z.string(),
    p: z.number(),
  })),
  // Per splice - `sample_economy` §9.
  mutation: z.number(),
  // OF mutations, not absolute. `splice_confirm_spec` §2 shows the two
  // separately so the player understands they are not the same event.
  aberrant: z.number(),
}).openapi('SpliceForecast')

export const SplicePreviewResponse = z.object({
  forecast: SpliceForecast,
  // `sample_economy` §7's screen requirement, and only what is certain: the
  // coverage that cannot carry under ANY outcome above. Empty is the normal
  // answer for four distinct traits - see splice/distribution.ts.
  coverageLost: z.array(z.object({
    trait: z.string(),
    tier: z.number().int().nullable(),
  })),
}).openapi('SplicePreviewResponse')

// Phase 6, Task 7. design §5.1's paid half - the route that samples the
// forecast above and destroys both parents.
//
// Registered into openapi.ts's registry in the SAME task that adds the route,
// the discipline the map's routes and the preview route both state.
export const SpliceCommitRequest = z.object({
  parentA: z.string().uuid(),
  parentB: z.string().uuid(),
  locked: SpliceLock,
  // design §5.2's body is a FREE choice between the two parents' species -
  // "free" meaning unrandomised, NOT unconstrained. A body that is neither
  // parent's species would let a client mint any species it liked out of two
  // it owns, so the server refuses it; this is a string rather than an enum
  // because species are engine content and a list retyped here would refuse
  // a species a later bundle adds.
  bodyFrom: z.string(),
}).openapi('SpliceCommitRequest')

export const SpliceCommitResponse = z.object({
  child: CreatureDto,
  spliceId: z.string().uuid(),
  // A DECIMAL STRING, exactly as WaveStartResponse.seed is one. The seed is
  // stored so a paid randomised action with published odds can be
  // re-derived after the fact (design §5.1), so it must be exact - and a JS
  // number loses precision above 2^53 while a BigInt is what
  // JSON.stringify throws on.
  seed: z.string(),
  // NO `mutated` / `aberrant`, and their absence is a decision rather than an
  // oversight. Both are rolled on every splice and stored on the `splices`
  // row - the seed makes the roll re-derivable either way - but design §10
  // defers Aberrant traits out of this phase, so there is nothing for a
  // mutation to produce and the child is identical whichever way the roll
  // lands. A client told `mutated: true` would show the player a mutation
  // that did not happen, which is worse than not telling them: withholding
  // is the safe direction, and `splice_confirm_spec` §2's separate display
  // of the two rates belongs with the content that makes them mean
  // something. They join this response when that content does.
  // Splice Charges left after the debit. `broodline_screen_inventory_v2.md`
  // §2 keeps them in the persistent top bar, so a client that had to call
  // /v1/sync to refresh them after every splice would be refetching the
  // world for one integer. NodeClaimResponse carries its balance for the
  // same reason.
  balance: z.number().int(),
}).openapi('SpliceCommitResponse')

// Phase 7, Task 6. design §5 beat 4 - naming the Founder. The response is
// CreatureDto itself, not a new shape: the only thing this route changes is
// `name`, and the client already knows how to render a CreatureDto.
//
// Registered into openapi.ts's registry in the SAME task that adds the
// route, the discipline every route above states.
export const CreatureNameRequest = z.object({
  creatureId: z.string().uuid(),
  // Trimmed and length-checked server-side (routes/creature.ts); z.string()
  // rather than a stricter schema because the 400 has to name the WHOLE rule
  // (1-16 printable characters after trimming), not just "must be a string".
  name: z.string(),
}).openapi('CreatureNameRequest')

// Phase 7, Task 7. design §5 beats 6-7 - the guided splice's provided pair.
// NO REQUEST SCHEMA: the route is body-less, exactly as routes/creature.ts's
// discipline note describes for a mutation with nothing for the caller to
// supply. Registered into openapi.ts's registry in the SAME task that adds
// the route, the discipline every route above states.
export const FtueStockResponse = z.object({
  creatures: z.array(CreatureDto),
}).openapi('FtueStockResponse')

// Phase 7, Task 9. `GET /v1/lineage` - design §5 beat 8, `splice_confirm_spec`
// §5: the Lineage View shows a splice's two consumed parents still whole, so
// this is the one response shape in the contract that covers all three of
// 0005_loop.sql's creature states (live, consumed, pruned) rather than only
// the live ones `CreatureDto` is built for.
//
// NOT `CreatureDto`, deliberately, and the difference is exactly the fields
// that state forces open. `trait1`/`tier1`/`trait2`/`tier2` are NULLABLE
// here - a pruned tombstone has none of them (0005's
// `pruned_creatures_are_stripped`) - where `CreatureDto`'s are not, because
// every route that returns a `CreatureDto` reads through `liveCreature()`
// first and never carries a pruned row at all. `instinct` and `committedTo`
// are absent: a tombstone has neither, and the tree this screen draws needs
// neither from a live node either.
export const LineageNode = z.object({
  creatureId: z.string().uuid(),
  // Kept on every row, pruned or not - 0005's tombstone is stripped down TO
  // exactly `{species, generation, is_founder}` plus its identity and parent
  // pointers, not past it.
  species: z.string(),
  generation: z.number().int(),
  isFounder: z.boolean(),
  // Founders only, and nulled by the prune same as `CreatureDto`'s - but a
  // pruned NON-Founder was never named to begin with, so this is null there
  // for a second, independent reason.
  name: z.string().nullable(),
  // The pointers the tree resolves through. NOT stripped by the prune -
  // 0005's header is explicit that the whole point of pruning as an UPDATE
  // rather than a DELETE is that a descendant's pointer keeps resolving to a
  // row that really exists.
  parentA: z.string().uuid().nullable(),
  parentB: z.string().uuid().nullable(),
  // NULL while live; the moment a splice consumed this creature otherwise.
  // NOT nulled by the prune - it is the liveness marker, and a tombstone is
  // the most dead a row gets (0005_loop.sql's column comment on `pruned`).
  consumedAt: z.string().nullable(),
  pruned: z.boolean(),
  // From `splices.mutated`, joined on this creature as the splice's CHILD -
  // false for a creature that is not one (base stock, the Founder, the
  // tutorial pair), never null. Never carried on `CreatureDto`'s own
  // response: `SpliceCommitResponse`'s comment records why the mutation flag
  // is withheld from the route that just rolled it, so this join is the
  // only place in the contract a player ever learns whether a splice
  // mutated.
  mutated: z.boolean(),
  // NULLABLE, unlike `CreatureDto`'s - a pruned tombstone has none of these,
  // and this route's whole job is returning the tombstone rather than
  // refusing to represent it.
  trait1: z.string().nullable(),
  tier1: z.number().int().nullable(),
  trait2: z.string().nullable(),
  tier2: z.number().int().nullable(),
}).openapi('LineageNode')

export const LineageResponse = z.object({
  nodes: z.array(LineageNode),
}).openapi('LineageResponse')
