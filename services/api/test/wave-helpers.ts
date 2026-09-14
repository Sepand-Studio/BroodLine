import { createHash, randomUUID } from 'node:crypto'
import { mkdir, readFile, rm, stat, writeFile } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
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

// --- The dotnet-build lock. wave-submit.test.ts and replays.test.ts both
// build services/sim/Broodline.Sim.Service.csproj for their own sim
// instance, and implementation/scripts/generate-contract.sh's Direction 2
// builds the SAME project independently for its own. MSBuild's -o/--output
// overrides OutputPath but never BaseIntermediateOutputPath, so all three
// share services/sim/obj/ regardless of where -o points, and two
// concurrent builds racing there intermittently fail with "the process
// cannot access the file ...rjsmrazor.dswa.cache.json". This is the ONE
// definition for the two TS callers, for the same reason the rest of this
// file is one definition; generate-contract.sh cannot import it and keeps
// its own bash implementation, hand-kept in sync (see that script's
// comment, which names this file back) - a path or timeout edit applied to
// only one side yields two locks, no exclusion, and a flake indistinguishable
// from the one this exists to close.

const LOCK_STALE_MS = 5 * 60 * 1000 // the critical section is ~1-2s; minutes is a generous margin
const LOCK_ACQUIRE_TIMEOUT_MS = 60_000
const LOCK_POLL_MS = 100

/**
 * The repo root, trailing slash stripped so this hashes to the exact same
 * bytes generate-contract.sh's `pwd` (after its own `cd .../../..`)
 * produces - a mismatch here would silently give the two sides different
 * lock paths.
 */
function repoRoot(): string {
  return fileURLToPath(new URL('../../../', import.meta.url)).replace(/\/+$/, '')
}

/**
 * Namespaced by OS user (so two developers or two CI identities sharing one
 * /tmp never contend or block each other - load-bearing, since /tmp's
 * sticky bit means one user literally cannot remove another user's stale
 * lock directory) and by a hash of the repo root (so two clones of this
 * repo do not share a lock either - lower stakes, since sharing one there
 * would only over-serialize two otherwise-independent clones, not produce
 * a correctness bug).
 */
function productionLockDir(): string {
  const uid = typeof process.getuid === 'function' ? process.getuid() : 0
  const hash = createHash('sha256').update(repoRoot()).digest('hex').slice(0, 10)
  return join(tmpdir(), `broodline-sim-dotnet-build-${uid}-${hash}.lock`)
}

interface DotnetBuildLock {
  withLock<T>(fn: () => T | Promise<T>): Promise<T>
}

/**
 * Builds one instance of the mkdir-based mutex protocol, parameterized by
 * directory and thresholds so the SAME logic backs both the real,
 * process-wide `withDotnetBuildLock` below (bound to the shared production
 * path every real caller must use) and dotnet-build-lock.test.ts's
 * isolated instances (each pointed at its own throwaway temp dir, so
 * testing reclaim/contention can never race the real thing a concurrent
 * wave-submit.test.ts/replays.test.ts run might be doing against the
 * production path at the same time).
 *
 * Ownership is a pid file written INSIDE the lock dir immediately after
 * mkdir claims it. There is a small window between mkdir succeeding and
 * that write landing where a waiter can see an empty lock dir with no
 * owner file yet - that is deliberately NOT treated as stale: a
 * missing/unreadable owner file falls through to the lock dir's own mtime
 * (isStale below), and that window is microseconds against the staleness
 * threshold, so a waiter who hits it just keeps polling rather than
 * misreading "not written yet" as "abandoned."
 */
function createDotnetBuildLock(dir: string, opts: {
  staleMs?: number
  timeoutMs?: number
  pollMs?: number
} = {}): DotnetBuildLock {
  const staleMs = opts.staleMs ?? LOCK_STALE_MS
  const timeoutMs = opts.timeoutMs ?? LOCK_ACQUIRE_TIMEOUT_MS
  const pollMs = opts.pollMs ?? LOCK_POLL_MS
  const ownerFile = join(dir, 'owner.pid')

  async function isStale(): Promise<boolean> {
    const raw = await readFile(ownerFile, 'utf8').catch(() => undefined)
    const pid = raw === undefined ? NaN : Number(raw)

    if (Number.isInteger(pid)) {
      try {
        // Signal 0 sends nothing; it only probes whether the pid exists
        // and is signalable, which is exactly "is the owner still
        // running."
        process.kill(pid, 0)
        return false // owner is alive - never stale, regardless of age
      } catch (err) {
        if ((err as NodeJS.ErrnoException).code === 'ESRCH') return true // confirmed gone
        // EPERM (alive, owned by someone else) or anything else: liveness
        // is unconfirmable, not disproven - fall through to the age check
        // below rather than guess either way.
      }
    }

    const st = await stat(dir).catch(() => undefined)
    if (st === undefined) return false // already gone; the next mkdir attempt will just succeed
    return Date.now() - st.mtimeMs > staleMs
  }

  async function acquireOnce(): Promise<boolean> {
    try {
      await mkdir(dir)
    } catch (err) {
      if ((err as NodeJS.ErrnoException).code !== 'EEXIST') throw err
      if (await isStale()) {
        // Reclaiming is just "remove it and let the NEXT mkdir decide who
        // actually gets it" - the removal itself grants no ownership, so
        // two waiters independently reaching this branch at the same
        // moment cannot both end up believing they hold the lock: at most
        // one subsequent mkdir call (the caller's next loop iteration)
        // wins; the other sees EEXIST again and re-evaluates staleness
        // against whatever is there now.
        await rm(dir, { recursive: true, force: true }).catch(() => {})
      }
      return false
    }
    await writeFile(ownerFile, String(process.pid), 'utf8')
    return true
  }

  async function releaseOnce(): Promise<void> {
    // Only remove the lock if it is still OURS - never a blind rm. The
    // staleness threshold is minutes against a ~1-2s critical section, so
    // this should never fire in practice, but if a build somehow ran long
    // enough to be reclaimed out from under it, blind-removing here would
    // delete whoever holds it NOW and reopen the exact race this file
    // exists to close.
    const raw = await readFile(ownerFile, 'utf8').catch(() => undefined)
    if (raw !== undefined && Number(raw) === process.pid) {
      await rm(dir, { recursive: true, force: true }).catch(() => {})
    }
  }

  return {
    async withLock<T>(fn: () => T | Promise<T>): Promise<T> {
      const deadline = Date.now() + timeoutMs
      for (;;) {
        if (await acquireOnce()) break
        if (Date.now() > deadline) {
          throw new Error(`timed out waiting for the dotnet build lock at ${dir}`)
        }
        await new Promise((r) => setTimeout(r, pollMs))
      }
      try {
        // Awaited INSIDE the try block (`return await fn()`, not
        // `return fn()`) specifically so that if a future caller passes an
        // async fn (today's two callers pass synchronous execFileSync),
        // the finally block's release still runs AFTER fn's promise
        // settles rather than the instant it is created - the reverse
        // ordering would silently drop all mutual exclusion the moment
        // either caller moved to execFile/spawn.
        return await fn()
      } finally {
        await releaseOnce()
      }
    },
  }
}

/**
 * Cross-process mutex around the `dotnet build` step in startSim() (see
 * wave-submit.test.ts and replays.test.ts), scoped to ONLY that step, not
 * the whole file or the whole suite - the critical section is ~1-2s, so
 * worst-case three-way contention adds a few seconds, nowhere near what
 * `--no-file-parallelism` would cost serializing all 17 test files for a
 * problem confined to 3.
 *
 * `flock` isn't installed on macOS by default; mkdir's atomicity (EEXIST if
 * the directory already exists) is the portable substitute. A hard kill
 * (SIGKILL, or Ctrl-C during execFileSync, which terminates the process
 * under SIGINT's default disposition before any try/finally can run) does
 * NOT run this function's own cleanup - that is exactly why the lock
 * carries its owner's pid and reclaims an abandoned one (see
 * createDotnetBuildLock's isStale/acquireOnce above) instead of assuming a
 * held lock is always a live one. An earlier version of this comment
 * claimed a crash "never leaves the lock stuck," which was false; a stale
 * lock is now recoverable rather than prevented - see
 * dotnet-build-lock.test.ts for the regression tests that prove reclaim
 * actually fires (and that a live lock is never stolen).
 */
export const withDotnetBuildLock: DotnetBuildLock['withLock'] =
  createDotnetBuildLock(productionLockDir()).withLock

// Exported ONLY for dotnet-build-lock.test.ts, which needs an isolated lock
// instance (its own throwaway directory) to test reclaim/contention without
// racing whatever wave-submit.test.ts/replays.test.ts may be doing against
// the real production lock at the same moment under vitest's parallel file
// execution. The two real callers above must always go through
// withDotnetBuildLock, never this, so every acquirer converges on the one
// shared production path.
export const __createDotnetBuildLockForTest = createDotnetBuildLock

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
