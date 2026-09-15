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
  config: z.object({
    bundleVersion: z.string(),
    minimumClientVersion: z.string(),
  }),
}).openapi('SyncResponse')

// Phase 5, Task 5, registered into openapi.ts's registry by Task 6 - see
// that file's header comment for why both wave routes were still absent
// from the generated contract until now.
export const WaveStartRequest = z.object({
  waveId: z.number().int(),
}).openapi('WaveStartRequest')

export const WaveStartResponse = z.object({
  issuanceId: z.string().uuid(),
  // A decimal string, not a number - the seed is a ulong and a JSON number
  // loses precision above 2^53. See services/api/src/db/schema.ts's
  // int8String customType.
  seed: z.string(),
  waveId: z.number().int(),
  expiresAt: z.string(),
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
}).openapi('WaveSubmitResponse')

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

// Phase 6, Task 5. The map's two routes - design §4.3.
//
// Registered into openapi.ts's registry in the SAME task that adds the
// routes, not a later one. Phase 5 shipped /v1/wave/start in code and left
// it out of the registry, so it was absent from the generated client
// entirely until the next task found it - see openapi.ts's own note on that
// path. A route the Unity client must call and has no generated method for
// is the same gap wearing a new name.
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
