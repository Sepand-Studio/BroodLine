import { randomUUID } from 'node:crypto'
import { and, eq, isNull } from 'drizzle-orm'
import type { Deps } from '../src/app.ts'
import { createApp } from '../src/app.ts'
import { withServer } from '../src/db/client.ts'
import { campaignProgress, ledger, waveIssuances, wallets } from '../src/db/schema.ts'
import type { Currency } from '../src/money/ledger.ts'
import { settle } from '../src/wave/issuance.ts'

/**
 * Tasks 5, 6, 8 and 10 all drive the same two routes (POST /v1/wave/start,
 * POST /v1/wave/submit). A helper redefined in four test files drifts in
 * four directions - this is the one definition.
 */

export interface ReplayOpts { engineVersion?: string; trait?: string; tier?: number }

// --- Replay byte layout, mirroring engine/Runtime/Combat/Replay.cs's
// Serialize() field-for-field. Magic, format version and the field order
// below are NOT guessed - they are transcribed from that method, including
// its trailing-data rule (Deserialize rejects a record with even one extra
// byte, which is why nothing here is allowed to round up or pad).

const MAGIC = 0x50524c42 // "BLRP" little-endian, engine/Runtime/Combat/Replay.cs
const FORMAT_VERSION = 1
const DEFAULT_ENGINE_VERSION = '0.2.0' // engine/Runtime/SimVersion.cs SimVersion.Value

// engine/Runtime/Combat/Ids.cs
const SPECIES = { Vetch: 0, Ember: 1, Skitter: 2, Hollow: 3, Loam: 4, Pale: 5 } as const
const TRAIT = { None: 0, Chill: 1 } as const
const INSTINCT = { Vanguard: 1 } as const
const TERRAIN_DEFILE = 0 // engine/Runtime/Combat/Ids.cs Terrain.Defile

// engine/Runtime/Combat/Stats.cs CreatureHp
const CREATURE_HP: Record<number, number> = {
  [SPECIES.Vetch]: 260, [SPECIES.Ember]: 130, [SPECIES.Skitter]: 80,
  [SPECIES.Hollow]: 60, [SPECIES.Loam]: 190, [SPECIES.Pale]: 120,
}

// engine/Runtime/Combat/Lane.cs Lane.Defile() - the only authored terrain
// family, so these are constants rather than per-wave data.
const LANE_TILES = 24
const POCKET_COUNT = 5

// engine/Runtime/Combat/WaveDef.cs Wave6(): laneCount 1. The only wave this
// bundle authors today (config/bundles/0.1.1/waves.json), so this is a
// one-entry table rather than a general lookup - it grows with content.
const WAVE_LANE_COUNT: Record<number, number> = { 6: 1 }

interface Creature {
  species: number; trait1: number; tier1: number; trait2: number; tier2: number
  instinct: number; pocket: number; hp: number
}

/**
 * Four Vetch holding the first four pockets, mirroring
 * tests/engine/Combat/GoldenTests.cs's DeploymentWithoutChill/WithChill - the
 * engine's own pinned fixture for wave 6, not a fixture reinvented here.
 */
function frontline(): Creature[] {
  return [0, 1, 2, 3].map((pocket) => ({
    species: SPECIES.Vetch, trait1: TRAIT.None, tier1: 0, trait2: TRAIT.None, tier2: 0,
    instinct: INSTINCT.Vanguard, pocket, hp: CREATURE_HP[SPECIES.Vetch]!,
  }))
}

function buildReplay(waveId: number, seed: bigint, last: Creature, o: ReplayOpts): string {
  const laneCount = WAVE_LANE_COUNT[waveId]
  if (laneCount === undefined) {
    throw new Error(`wave-helpers: no known lane count for wave ${waveId} - add it to WAVE_LANE_COUNT`)
  }
  const deployment = [...frontline(), last]
  const engineVersion = o.engineVersion ?? DEFAULT_ENGINE_VERSION
  const versionBytes = Buffer.from(engineVersion, 'utf8')

  const size = 4 + 2 + 1 + versionBytes.length
    + 4 + 8 + 4 + 4 + 4 + 4
    + 4 + deployment.length * 8 * 4
    + 4 + 4
  const buf = Buffer.alloc(size)
  let i = 0

  buf.writeUInt32LE(MAGIC, i); i += 4
  buf.writeUInt16LE(FORMAT_VERSION, i); i += 2
  buf.writeUInt8(versionBytes.length, i); i += 1
  versionBytes.copy(buf, i); i += versionBytes.length

  buf.writeInt32LE(waveId, i); i += 4
  buf.writeBigUInt64LE(seed, i); i += 8
  buf.writeInt32LE(TERRAIN_DEFILE, i); i += 4
  buf.writeInt32LE(laneCount, i); i += 4
  buf.writeInt32LE(POCKET_COUNT, i); i += 4
  buf.writeInt32LE(LANE_TILES, i); i += 4

  buf.writeInt32LE(deployment.length, i); i += 4
  for (const c of deployment) {
    buf.writeInt32LE(c.species, i); i += 4
    buf.writeInt32LE(c.trait1, i); i += 4
    buf.writeInt32LE(c.tier1, i); i += 4
    buf.writeInt32LE(c.trait2, i); i += 4
    buf.writeInt32LE(c.tier2, i); i += 4
    buf.writeInt32LE(c.instinct, i); i += 4
    buf.writeInt32LE(c.pocket, i); i += 4
    buf.writeInt32LE(c.hp, i); i += 4
  }

  buf.writeInt32LE(-1, i); i += 4 // RallyTick, absent
  buf.writeInt32LE(-1, i); i += 4 // RallyCreature, absent

  return buf.toString('base64')
}

/** The fifth creature carries Chill, which answers wave 6's lone Courser. */
export function buildWinningReplay(waveId: number, seed: bigint, o: ReplayOpts = {}): string {
  const trait = o.trait !== undefined ? TRAIT[o.trait as keyof typeof TRAIT] : TRAIT.Chill
  return buildReplay(waveId, seed, {
    species: SPECIES.Pale, trait1: trait, tier1: o.tier ?? 1, trait2: TRAIT.None, tier2: 0,
    instinct: INSTINCT.Vanguard, pocket: 4, hp: CREATURE_HP[SPECIES.Pale]!,
  }, o)
}

/**
 * Differs from the winning replay in one field: the fifth creature carries
 * Trait.None rather than Trait.Chill, so the Courser is unanswered and
 * breaches - Stats.CounterFor(Courser) is Chill and nothing else covers it.
 */
export function buildLosingReplay(waveId: number, seed: bigint, o: ReplayOpts = {}): string {
  return buildReplay(waveId, seed, {
    species: SPECIES.Loam, trait1: TRAIT.None, tier1: 0, trait2: TRAIT.None, tier2: 0,
    instinct: INSTINCT.Vanguard, pocket: 4, hp: CREATURE_HP[SPECIES.Loam]!,
  }, o)
}

// --- Route drivers. `app`, `token`, `playerId`, `serverId` and `deps` are
// module-level, set by setupPlayer() - every driver and state reader below
// reads them at CALL time, not at import time, so re-running setupPlayer()
// (a second player in the same file) is what a later test uses on purpose.

let deps: Deps
let app: ReturnType<typeof createApp>
let token: string
let playerId: string
let serverId: number

export async function setupPlayer(d: Deps): Promise<{ playerId: string; token: string }> {
  deps = d
  app = createApp(d)

  const res = await app.request('/v1/account', {
    method: 'POST',
    headers: { 'content-type': 'application/json', 'idempotency-key': randomUUID() },
    body: JSON.stringify({ birthdateBand: 'adult', storefrontRegion: 'us-central1' }),
  })
  const body = await res.json() as { playerId: string; serverId: number; accessToken: string }
  playerId = body.playerId
  serverId = body.serverId
  token = body.accessToken
  return { playerId, token }
}

export async function startWave(waveId: number): Promise<Response> {
  return app.request('/v1/wave/start', {
    method: 'POST',
    headers: { 'content-type': 'application/json', authorization: `Bearer ${token}` },
    body: JSON.stringify({ waveId }),
  })
}

export function submitInit(issuanceId: string, replay: string, key: string): RequestInit {
  return {
    method: 'POST',
    headers: {
      'content-type': 'application/json',
      authorization: `Bearer ${token}`,
      'idempotency-key': key,
    },
    body: JSON.stringify({ issuanceId, replay }),
  }
}

// /v1/wave/submit does not exist yet - Task 6. This calls it the same way
// startWave calls /v1/wave/start, so Task 6 need only implement the route;
// nothing here changes.
export async function submit(issuanceId: string, replay: string, key: string): Promise<Response> {
  return app.request('/v1/wave/submit', submitInit(issuanceId, replay, key))
}

// --- State readers. design 7: a test asserting a BALANCE (or a row, or a
// live/settled issuance) is a real gate; a test asserting only an error code
// would pass against a server that rejects everything.

export async function balance(currency: Currency): Promise<number> {
  const rows = await withServer(deps.db, serverId, (tx) => tx.select().from(wallets)
    .where(and(eq(wallets.playerId, playerId), eq(wallets.currency, currency))))
  return rows[0]?.balance ?? 0
}

export async function ledgerRowCount(): Promise<number> {
  const rows = await withServer(deps.db, serverId, (tx) => tx.select().from(ledger)
    .where(eq(ledger.playerId, playerId)))
  return rows.length
}

export async function liveIssuance(): Promise<typeof waveIssuances.$inferSelect | undefined> {
  const [row] = await withServer(deps.db, serverId, (tx) => tx.select().from(waveIssuances)
    .where(and(eq(waveIssuances.playerId, playerId), isNull(waveIssuances.settledAt))))
  return row
}

export async function consumeLiveIssuance(): Promise<void> {
  const live = await liveIssuance()
  if (live === undefined) throw new Error('consumeLiveIssuance: no live issuance to consume')
  await withServer(deps.db, serverId, (tx) => settle(tx, live, 'consumed'))
}

/**
 * Test-only shortcut for "this player has already cleared wave N": settles
 * any currently-live issuance as 'expired' (a no-op if there is none) and
 * sets campaign_progress.highest_wave_cleared directly.
 *
 * 'expired', not 'consumed' - a live issuance this helper displaces was
 * never actually submitted (this helper exists precisely to skip playing
 * the wave), so it is an abandoned issuance in exactly the sense design 4.3
 * uses the word, not a played one. Settling it 'consumed' instead would
 * silently spend one of the caller's three daily replays (design 4.1 check
 * 2 counts only 'consumed' rows) before the caller has taken any of them -
 * confirmed by a real failure here: the first version of this helper used
 * 'consumed' and it made 'refuses a fourth replay...' fail on its THIRD
 * start() rather than a fourth that was never reached, because clearWave(6)
 * had already spent one slot settling the still-live issuance test 1/2 left
 * behind. Also: no wave_issuances row is minted here for the "advance
 * progress" half - a manufactured row would count against the cap the same
 * way.
 */
export async function clearWave(waveId: number): Promise<void> {
  const live = await liveIssuance()
  if (live !== undefined) {
    await withServer(deps.db, serverId, (tx) => settle(tx, live, 'expired'))
  }

  await withServer(deps.db, serverId, async (tx) => {
    const [progress] = await tx.select().from(campaignProgress)
      .where(and(eq(campaignProgress.serverId, serverId), eq(campaignProgress.playerId, playerId)))
    const highestWaveCleared = Math.max(progress?.highestWaveCleared ?? 0, waveId)

    if (progress === undefined) {
      await tx.insert(campaignProgress).values({ serverId, playerId, highestWaveCleared })
    } else {
      await tx.update(campaignProgress).set({ highestWaveCleared, updatedAt: new Date() })
        .where(and(eq(campaignProgress.serverId, serverId), eq(campaignProgress.playerId, playerId)))
    }
  })
}

/**
 * Today this IS clearWave: campaign progress is a single high-water mark, so
 * "cleared through wave N" and "cleared wave N" set the same column to the
 * same value. Named separately because a future multi-wave bundle's "clear
 * every wave up to N" is a different operation from "clear wave N itself",
 * and callers should not have to change which helper they use when that
 * lands.
 */
export const clearThrough = clearWave
