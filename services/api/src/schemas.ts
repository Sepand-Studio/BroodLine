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

// Phase 5, Task 5. Not yet wired into openapi.ts's registry - that
// generates openapi/broodline.json, which is checked byte-for-byte by
// test/contract.test.ts against a regeneration requiring the NSwag/.NET
// toolchain this task was told not to invoke. Registering the path is left
// to whichever task next touches the generated contract (POST
// /v1/wave/submit does not exist yet either - Task 6).
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

export const ErrorResponse = z.object({
  code: z.string(),
  message: z.string(),
  details: z.unknown().optional(),
}).openapi('ErrorResponse')
