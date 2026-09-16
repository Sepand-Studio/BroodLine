import { createHash, randomUUID } from 'node:crypto'
import { mkdir, readFile, rm, stat, writeFile } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { and, eq, isNull } from 'drizzle-orm'
import type { Deps } from '../src/app.ts'
import { createApp } from '../src/app.ts'
import { withServer } from '../src/db/client.ts'
import { campaignProgress, creatures, ledger, waveIssuances, wallets } from '../src/db/schema.ts'
import type { Currency } from '../src/money/ledger.ts'
import { liveCreature } from '../src/roster/creatures.ts'
import { settle } from '../src/wave/issuance.ts'
import {
  asRosterSpecs, losingDeployment, type RosterSpec, winningDeployment,
} from './replay-format.ts'

/**
 * Tasks 5, 6, 8 and 10 all drive the same two routes (POST /v1/wave/start,
 * POST /v1/wave/submit). A helper redefined in four test files drifts in
 * four directions - this is the one definition.
 */

// --- The dotnet-build lock. wave-submit.test.ts, replays.test.ts and
// adversarial.test.ts each build services/sim/Broodline.Sim.Service.csproj
// for their own sim instance, and implementation/scripts/generate-contract.sh's
// Direction 2 builds the SAME project independently for its own - four
// contending call sites, three of them in this package. MSBuild's -o/--output
// overrides OutputPath but never BaseIntermediateOutputPath, so all four
// share services/sim/obj/ regardless of where -o points, and two
// concurrent builds racing there intermittently fail with "the process
// cannot access the file ...rjsmrazor.dswa.cache.json". This is the ONE
// definition for the three TS callers, for the same reason the rest of this
// file is one definition; generate-contract.sh cannot import it and keeps
// its own bash implementation, hand-kept in sync (see that script's
// comment, which names this file back) - a path or timeout edit applied to
// only one side yields two locks, no exclusion, and a flake indistinguishable
// from the one this exists to close.

const LOCK_STALE_MS = 5 * 60 * 1000 // the critical section is ~1-2s; minutes is a generous margin
// A SEPARATE, much longer ceiling from LOCK_STALE_MS above - deliberately
// not folded into one OR, and deliberately not a heartbeat (disproportionate
// here). LOCK_STALE_MS governs the "liveness unconfirmable" case, where a
// short margin is safe because the critical section is short. This ceiling
// governs the "liveness CONFIRMED alive" case instead, which the short
// margin cannot: if the OS recycles a SIGKILLed owner's pid onto some
// unrelated, still-running process, isStale()'s liveness check reports
// "alive" forever, and a stuck lock is not bounded by a short critical
// section - it is abandoned, and sits for as long as nobody clears it. Set
// far above any plausible legitimate build so it can never steal from one.
const LOCK_ABSOLUTE_CEILING_MS = 30 * 60 * 1000
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
 * wave-submit.test.ts / replays.test.ts / adversarial.test.ts run might be
 * doing against the production path at the same time).
 *
 * Ownership is an owner file written INSIDE the lock dir immediately after
 * mkdir claims it. There is a small window between mkdir succeeding and
 * that write landing where a waiter can see an empty lock dir with no
 * owner file yet - that is deliberately NOT treated as stale: a
 * missing/unreadable owner file falls through to the lock dir's own mtime
 * (isStale below), and that window is microseconds against the staleness
 * threshold, so a waiter who hits it just keeps polling rather than
 * misreading "not written yet" as "abandoned."
 *
 * The owner VALUE is `${pid}:${nonce}`, not a bare pid, and the nonce is
 * fresh per acquisition. Two reasons, and the second is the load-bearing
 * one:
 *
 *  - A bare pid distinguishes lock GENERATIONS only as well as pids do.
 *    reclaim()'s re-verify and releaseOnce's ownership check both ask "is
 *    the thing at this path still the same generation I decided about?",
 *    and a value that repeats across generations answers that wrongly.
 *    This closes the generation question only where a value EXISTS to
 *    compare; reclaim()'s second accepted residual is the one case where
 *    none does, and no nonce reaches it.
 *  - *** A bare pid would make this design silently depend on vitest's
 *    pool. *** The three TS callers are distinct processes only because
 *    vitest 2.1's default pool is `forks` and vitest.config.ts sets no
 *    `pool`. Setting `pool: 'threads'` there - an edit in an unrelated
 *    config file, for unrelated reasons - would give every caller ONE
 *    process.pid, at which point releaseOnce's ownership check and
 *    isStale's liveness check both degenerate to "yes, that's me" and the
 *    lock stops excluding anything. Every test would stay green. A nonce
 *    removes the dependency outright: two lock instances in the same
 *    process have different owner values, so the protocol holds under any
 *    pool. dotnet-build-lock.test.ts's "a holder whose lock was reclaimed
 *    out from under it does not delete the new owner's lock" test is the
 *    one that pins this, and it runs both instances in THIS process on
 *    purpose.
 *
 * The pid stays the FIRST colon-delimited field because liveness still
 * needs it (process.kill(pid, 0)); everything else compares the full
 * string. generate-contract.sh writes the same shape (`$$:$RANDOM...`) and
 * parses it the same way - the two sides only ever compare owner values
 * for equality, so all they must agree on is "pid first, then a colon."
 */
function createDotnetBuildLock(dir: string, opts: {
  staleMs?: number
  absoluteCeilingMs?: number
  timeoutMs?: number
  pollMs?: number
  /**
   * TEST-ONLY. Awaited between isStale() deciding a lock is reclaimable
   * and the destructive step that acts on that decision - the exact window
   * round 3's review demonstrated exploitably: two reclaimers can both
   * decide "stale" against the SAME lock instance before either has
   * removed anything. A no-op (the default, and the only behavior any
   * real caller sees) has no effect on the code path; only
   * dotnet-build-lock.test.ts's ABA regression test sets it, to a hook
   * that waits for a SIGNAL (another instance's critical section actually
   * starting) rather than a guessed delay - so the reproduction is
   * deterministic, not "probably wide enough on this machine today."
   */
  testBeforeReclaim?: () => void | Promise<void>
} = {}): DotnetBuildLock {
  const staleMs = opts.staleMs ?? LOCK_STALE_MS
  const absoluteCeilingMs = opts.absoluteCeilingMs ?? LOCK_ABSOLUTE_CEILING_MS
  const timeoutMs = opts.timeoutMs ?? LOCK_ACQUIRE_TIMEOUT_MS
  const pollMs = opts.pollMs ?? LOCK_POLL_MS
  const testBeforeReclaim = opts.testBeforeReclaim
  const ownerFile = join(dir, 'owner.pid')

  async function isStale(raw: string | undefined): Promise<boolean> {
    // The owner value is `${pid}:${nonce}`; liveness needs only the pid, so
    // take the first colon-delimited field. A value with no colon at all
    // (a bare pid, which is what a lock left behind by an older build of
    // this file or of generate-contract.sh contains) parses identically,
    // so an in-flight upgrade cannot produce an unreclaimable lock.
    const pid = raw === undefined ? NaN : Number(raw.split(':')[0])

    const st = await stat(dir).catch(() => undefined)
    if (st === undefined) return false // already gone; the next mkdir attempt will just succeed
    const age = Date.now() - st.mtimeMs

    if (Number.isInteger(pid)) {
      try {
        // Signal 0 sends nothing; it only probes whether the pid exists
        // and is signalable, which is exactly "is the owner still
        // running."
        process.kill(pid, 0)
        // Confirmed alive per the OS - but NOT unconditionally trusted:
        // if this pid was recycled onto an unrelated process after a
        // SIGKILLed owner died, this is a false positive that would
        // otherwise pin the lock stuck forever, since nothing else in
        // this check would ever revisit it. absoluteCeilingMs is the
        // guaranteed eventual recovery for exactly that case - see this
        // function's outer comment for why it is a separate, much larger
        // threshold rather than folded into staleMs.
        return age > absoluteCeilingMs
      } catch (err) {
        if ((err as NodeJS.ErrnoException).code === 'ESRCH') return true // confirmed gone
        // EPERM (alive, owned by someone else) or anything else: liveness
        // is unconfirmable, not disproven - fall through to the shorter
        // age check below rather than guess either way.
      }
    }

    return age > staleMs
  }

  /**
   * Re-verify, then destroy. ONE layer, deliberately - an earlier version
   * had a second, "detach atomically via rename(2), then rm the detached
   * copy", justified by a comment claiming neither layer sufficed alone.
   * Round 4's review DISPROVED that claim by direct experiment: at the
   * one interleaving the comment said rename earned its keep on (three
   * contenders; B re-verifies OK and stalls; A destroys the dead lock; C
   * acquires and enters; B's destructive step then runs) blind rm and
   * atomic rename produced byte-identical outcomes. Rename detaches
   * whatever generation is at the path just as blindly as rm removes it;
   * the only case it genuinely wins is two reclaimers destroying the same
   * STILL-DEAD lock, which is harmless either way (one wins, the other
   * no-ops on an already-gone path).
   *
   * And it had acquired a cost: the detached `${dir}.reclaim-<pid>-<uuid>`
   * directory is garbage that nothing ever matches or cleans, so a hard
   * kill landing between the rename and the rm left it in TMPDIR forever
   * - and hard kills mid-build are precisely the scenario this lock
   * exists for. Zero demonstrated benefit plus a real leak means the
   * layer is gone, on this side and in generate-contract.sh's copy.
   *
   * What does the work is the re-read below. reclaim is handed the EXACT
   * owner value isStale() judged (raw, not the whole boolean verdict), and
   * before touching anything it re-reads the CURRENT owner value and
   * compares. A mismatch - even to a value this function cannot itself
   * interpret - means some OTHER, legitimate acquirer has claimed this
   * path since the decision was made, and reclaim aborts rather than
   * destroying work that is not its to destroy.
   *
   * THE WINDOW IS NARROWED, NOT CLOSED. Be precise about what survives:
   * this is a check-then-act, and between the check and the act there is
   * still a gap. A re-reads owner=X, re-verifies OK, and then its `rm` is
   * queued behind a busy libuv threadpool (bash's `rm` behind a
   * fork/exec); B reclaims, mkdirs a fresh lock and writes its own owner;
   * A's queued rm lands on B's LIVE lock. The stall required is no longer
   * "however long the caller happened to be paused" - it is now "longer
   * than the other side's reclaim + mkdir + owner-write", which round 3
   * measured at roughly 20ms for the bash side. That is ordinary jitter,
   * not an exotic pause, so this is an ACCEPTED RESIDUAL, not a closed
   * hole.
   *
   * It is explicitly NOT the same kind of thing as mkdir's EEXIST or
   * rename's ENOENT, which an earlier comment here claimed. Those are
   * single atomic syscalls with NO window at all. Closing a check-then-act
   * properly needs a compare-and-swap primitive that a lock directory
   * simply does not offer (it would mean a real lock file with
   * O_EXCL+fcntl, or a lock server) - disproportionate for test
   * infrastructure whose worst case is one flaky `dotnet build`, which is
   * the failure this lock already reduced from routine to rare. So: known,
   * bounded, accepted, and written down rather than papered over.
   *
   * A SECOND residual sits alongside it, and - unlike the generation
   * confusion the nonce above really does close - the nonce CANNOT close
   * this one, because it acts on a value that does not exist yet. During
   * the mkdir -> owner-write window there is no owner file at all, so
   * `observedRaw` and `stillThere` are both undefined, the re-verify
   * compares EQUAL, and reclaim proceeds. Round 4's review demonstrated it
   * against this code path: an ownerless lock dir aged past staleMs (a
   * crash inside that window) -> A reads owner-absent and judges stale ->
   * C reclaims, mkdirs a fresh lock, has not yet written its owner -> A's
   * re-verify reads owner-absent too, matches its own, and destroys C's
   * LIVE lock. The bash side is structurally identical, with the empty
   * string in place of undefined. Pre-existing and Minor - it needs a
   * crash in a microsecond window plus five minutes of nobody touching the
   * path - and accepted for exactly the reason above: distinguishing
   * "absent because not written yet" from "absent because it was never
   * written" needs the same compare-and-swap primitive this design already
   * rules disproportionate.
   */
  async function reclaim(observedRaw: string | undefined): Promise<void> {
    if (testBeforeReclaim) await testBeforeReclaim()

    const stillThere = await readFile(ownerFile, 'utf8').catch(() => undefined)
    if (stillThere !== observedRaw) return // changed since the decision - not ours to reclaim

    await rm(dir, { recursive: true, force: true })
  }

  /** The owner value written on success, or undefined if the lock was not won. */
  async function acquireOnce(): Promise<string | undefined> {
    try {
      await mkdir(dir)
    } catch (err) {
      if ((err as NodeJS.ErrnoException).code !== 'EEXIST') throw err
      // Read ONCE, feed the same observed value to both isStale()'s
      // verdict and reclaim()'s later re-check - reading twice here would
      // just move the ABA window rather than close it.
      const observedRaw = await readFile(ownerFile, 'utf8').catch(() => undefined)
      if (await isStale(observedRaw)) await reclaim(observedRaw)
      return undefined
    }
    // Fresh nonce per acquisition, so no two generations of this lock ever
    // carry the same owner value - not even two generations produced by
    // this same instance in this same process. See the factory's comment
    // for why a bare pid would make the whole protocol depend on vitest's
    // pool setting.
    const ownerValue = `${process.pid}:${randomUUID()}`
    await writeFile(ownerFile, ownerValue, 'utf8')
    return ownerValue
  }

  async function releaseOnce(ownerValue: string): Promise<void> {
    // Only remove the lock if it is still OURS - never a blind rm. The
    // staleness threshold is minutes against a ~1-2s critical section, so
    // this should never fire in practice, but if a build somehow ran long
    // enough to be reclaimed out from under it, blind-removing here would
    // delete whoever holds it NOW and reopen the exact race this file
    // exists to close. Compared as the FULL owner string, not by pid: a
    // pid comparison is satisfied by any generation this process wrote,
    // including one a reclaimer handed to a different lock instance in
    // this same process, which is exactly the case
    // dotnet-build-lock.test.ts's "a holder whose lock was reclaimed out
    // from under it" test drives.
    const raw = await readFile(ownerFile, 'utf8').catch(() => undefined)
    if (raw === ownerValue) {
      await rm(dir, { recursive: true, force: true }).catch(() => {})
    }
  }

  return {
    async withLock<T>(fn: () => T | Promise<T>): Promise<T> {
      const deadline = Date.now() + timeoutMs
      let ownerValue: string | undefined
      for (;;) {
        ownerValue = await acquireOnce()
        if (ownerValue !== undefined) break
        if (Date.now() > deadline) {
          throw new Error(`timed out waiting for the dotnet build lock at ${dir}`)
        }
        await new Promise((r) => setTimeout(r, pollMs))
      }
      try {
        // Awaited INSIDE the try block (`return await fn()`, not
        // `return fn()`) specifically so that if a future caller passes an
        // async fn (today's three callers pass synchronous execFileSync),
        // the finally block's release still runs AFTER fn's promise
        // settles rather than the instant it is created - the reverse
        // ordering would silently drop all mutual exclusion the moment
        // either caller moved to execFile/spawn.
        return await fn()
      } finally {
        // The owner value this acquisition actually wrote, carried in a
        // local rather than instance state: one instance can have two
        // concurrent withLock() calls, and instance-level state would let
        // the second acquisition's value be the one the first release
        // checks against.
        await releaseOnce(ownerValue)
      }
    },
  }
}

/**
 * Cross-process mutex around the `dotnet build` step in startSim() (see
 * wave-submit.test.ts, replays.test.ts and adversarial.test.ts), scoped to
 * ONLY that step, not the whole file or the whole suite - the critical
 * section is ~1-2s, so worst-case four-way contention (those three plus
 * generate-contract.sh) adds a few seconds, nowhere near what
 * `--no-file-parallelism` would cost serializing all 19 test files for a
 * problem confined to 4.
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
// execution. The three real callers above must always go through
// withDotnetBuildLock, never this, so every acquirer converges on the one
// shared production path.
export const __createDotnetBuildLockForTest = createDotnetBuildLock

// The replay byte format moved to ./replay-format.ts, so that building a
// replay no longer drags the Hono app, the Drizzle schema and SimClient in
// with it - see that file for why. Re-exported here so every existing
// importer of wave-helpers keeps working, with one definition of the format.
export {
  asRosterSpecs, buildLosingReplay, buildReplayOf, buildWinningReplay,
  losingDeployment, winningDeployment,
} from './replay-format.ts'
export type { ReplayCreature, ReplayOpts, RosterSpec } from './replay-format.ts'

// --- Route drivers. `app`, `token`, `playerId`, `serverId` and `deps` are
// module-level, set by setupPlayer() - every driver and state reader below
// reads them at CALL time, not at import time, so re-running setupPlayer()
// (a second player in the same file) is what a later test uses on purpose.

let deps: Deps
let app: ReturnType<typeof createApp>
let token: string
let playerId: string
let serverId: number
let winners: Deployed[] | undefined
let losers: Deployed[] | undefined

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
  // The new player's roster is empty and the two cached ones belong to the
  // PREVIOUS player - deploying those would be `creature_not_owned`, which is
  // a confusing way to discover that a cache was not cleared.
  winners = undefined
  losers = undefined
  return { playerId, token }
}

/** design §6.1's body: an id and a pocket, and nothing else a client controls. */
export interface Deployed { creatureId: string; pocket: number }

/**
 * Sends the body VERBATIM, so a test can put fields in it that `parseStart`
 * has no business honouring.
 *
 * That is the whole point of having it: design §6.1's mechanism is that
 * there is no path from a client-supplied value to a stored spec, and the
 * only way to show that is to supply one. `startWave` below cannot - its
 * signature admits an id and a pocket, which is exactly the shape under
 * test.
 */
export async function startWaveRaw(body: unknown): Promise<Response> {
  return app.request('/v1/wave/start', {
    method: 'POST',
    headers: { 'content-type': 'application/json', authorization: `Bearer ${token}` },
    body: JSON.stringify(body),
  })
}

/**
 * DEFAULTS TO AN EMPTY DEPLOYMENT, which is a real deployment and not a
 * stand-in for "the field is optional": `deployment` is REQUIRED by
 * routes/wave.ts's parseStart (design §6.1 grows the body), and an empty
 * array is the one value every pre-Task-8 caller in this package can be
 * given without asserting anything new. Those callers - wave-submit,
 * replays and adversarial - are about the SUBMIT path and say nothing about
 * what was deployed; Task 9 is where the echo starts being compared against
 * the issuance, and that is the task that has to give them real rosters.
 */
export async function startWave(waveId: number, deployment: Deployed[] = []): Promise<Response> {
  return startWaveRaw({ waveId, deployment })
}

/**
 * THE ROSTER A REPLAY CLAIMS, MINTED.
 *
 * Task 10 made `wave/submit` compare the deployment `sim` echoed against the
 * one the issuance froze, so a wave started with the empty deployment above
 * and submitted with a five-creature replay is now a `deployment_mismatch` -
 * correctly. The three submit-path files were all written before that
 * comparison existed and all start with the default; the note above
 * `startWave` said this task is the one that "has to give them real rosters",
 * and these are them.
 *
 * MINTED THROUGH THE APP ROLE inside `withServer`, one row at a time. Two
 * reasons for the one-at-a-time: a multi-row `INSERT ... RETURNING` has no
 * ORDER guarantee in the standard, and the four Vetch of a frontline are
 * field-for-field identical, so a returned row cannot be matched back to the
 * spec it came from by anything except position. Pockets come off the spec
 * rather than the loop index, because two creatures may legitimately share
 * one (task-9-report §5).
 *
 * `trait 'None'` IS NOT A PLACEHOLDER FOR A MISSING TRAIT. It is what the
 * engine's `Trait.None` echoes as, and the frontline these helpers mirror
 * (tests/engine/Combat/GoldenTests.cs) really does carry empty combat slots.
 * A fixture roster carrying `Taunt`/`Carapace` instead would be more like a
 * granted creature and LESS like the thing under test: the replay bytes would
 * have to change with it, and changing them changes what the engine
 * simulates - wave 6 is authored at integrity 2, so a Carapace wall could
 * turn `buildLosingReplay` into a win and quietly delete the loss cases.
 */
export async function giveRoster(specs: readonly RosterSpec[]): Promise<Deployed[]> {
  const deployed: Deployed[] = []
  for (const s of specs) {
    const [row] = await withServer(deps.db, serverId, (tx) => tx.insert(creatures).values({
      serverId,
      playerId,
      species: s.species,
      generation: 1,
      trait1: s.trait1, tier1: s.tier1,
      trait2: s.trait2, tier2: s.tier2,
      instinct: s.instinct,
      hpCurrent: s.hp,
      isFounder: false,
    }).returning())
    deployed.push({ creatureId: row!.creatureId, pocket: s.pocket })
  }
  return deployed
}

/**
 * The five creatures `buildWinningReplay` claims, minted ONCE per player and
 * reused.
 *
 * Once rather than per call, for two reasons that both bite: the Hatchery cap
 * is 20 (bible §7.2) and a file that mints five per wave would reach it
 * inside one test, and `settle` clears `committed_to` on both terminal states
 * so the same five really are redeployable - which is the behaviour a replay
 * loop should be exercising anyway.
 */
export async function winningRoster(): Promise<Deployed[]> {
  winners ??= await giveRoster(asRosterSpecs(winningDeployment()))
  return winners
}

/** The five `buildLosingReplay` claims - a frontline with no Chill behind it. */
export async function losingRoster(): Promise<Deployed[]> {
  losers ??= await giveRoster(asRosterSpecs(losingDeployment()))
  return losers
}

/** `startWave` with the deployment `buildWinningReplay` will claim. */
export async function startWinning(waveId: number): Promise<Response> {
  return startWave(waveId, await winningRoster())
}

/** `startWave` with the deployment `buildLosingReplay` will claim. */
export async function startLosing(waveId: number): Promise<Response> {
  return startWave(waveId, await losingRoster())
}

/** Live creatures this player holds - design §2.4's supply line, counted. */
export async function rosterCount(): Promise<number> {
  const rows = await withServer(deps.db, serverId, (tx) => tx.select().from(creatures)
    .where(and(eq(creatures.playerId, playerId), liveCreature())))
  return rows.length
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
