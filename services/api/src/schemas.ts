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
