import { type ChildProcess, execFileSync, spawn } from 'node:child_process'
import { randomUUID } from 'node:crypto'
import { readFileSync } from 'node:fs'
import { mkdtemp, rm } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { and, eq, sql } from 'drizzle-orm'
import { afterAll, beforeAll, describe, expect, it } from 'vitest'
import { createApp, type Deps } from '../src/app.ts'
import { clearBundleCache } from '../src/config/bundle.ts'
import { publishBundle } from '../src/config/publish.ts'
import { LocalBundleStore } from '../src/config/store.ts'
import { withServer } from '../src/db/client.ts'
import { creatures, harvestPositions, servers, waveIssuances } from '../src/db/schema.ts'
import { THE_REGION } from '../src/map/claim.ts'
import { epochFor } from '../src/map/rotation.ts'
import { LocalReplayStore } from '../src/replays/store.ts'
import { SimClient } from '../src/sim/client.ts'
import { reapOnExit } from './child-reaper.ts'
import { startTestDb, type TestDb } from './harness.ts'
import { CREATURE_HP, SPECIES } from './replay-format.ts'
import { RosterLedger } from './roster-ledger.ts'
import {
  asRosterSpecs, balance, buildReplayOf, buildWinningReplay, giveRoster, ledgerRowCount,
  type RosterSpec, setupPlayer, submit, winningDeployment, withDotnetBuildLock,
} from './wave-helpers.ts'

/**
 * THE LOOP, DRIVEN END TO END - design §1's done-when, as a gate rather than
 * as a claim.
 *
 * WHY THIS FILE EXISTS, and it is worth being blunt about it. Phase 6's
 * central demonstration was originally a TRANSCRIPT: a driver run once by
 * hand, its output pasted into a report, the driver deleted. That transcript
 * was internally impossible and nobody noticed for a full review cycle. It
 * reported thirteen creatures on the closing roster against only two node
 * claims, and asserted alongside them that "every mutation came from an HTTP
 * request". Both could not be true - ten of the thirteen had been inserted
 * directly by `giveRoster`.
 *
 * What caught it was not a test. It was a reviewer multiplying shard deltas by
 * hand. **A done-when that can only be checked by a human doing arithmetic
 * over a transcript is not a gate, it is a claim**, and the specific thing it
 * failed to notice - SEEDED creatures being described as EARNED - is free to
 * recur invisibly as long as nothing asserts the distinction.
 *
 * So the ledger below is the point of this file, at least as much as the
 * payout assertions are. It is not instrumentation and it is not logging: the
 * split is ASSERTED.
 *
 * The ledger itself is `./roster-ledger.ts` - extracted there by Task 22
 * because `ftue.test.ts` drives the same instrument to the opposite reading.
 * Its header carries the three properties that make it honest BY
 * CONSTRUCTION rather than by discipline (every booking is of a MEASURED
 * delta; `reconcile()` runs after every step; the closing assertion is on the
 * SPLIT, never the sum) and they are not restated here, so there is one copy
 * of them to keep true.
 *
 * THE EXACT NUMBERS ARE DELIBERATE AND THIS FILE IS MEANT TO BE BRITTLE ABOUT
 * THEM. If content or code changes the balance - a node that grants two, a
 * wave that grants none - this goes red. That is correct: the supply line
 * changed, and someone should look at it rather than have the total absorb it.
 *
 * ---------------------------------------------------------------------------
 * WHAT IS REAL HERE, AND WHAT IS NOT. Stated because the original transcript
 * got exactly this wrong.
 *
 * REAL: Postgres (Testcontainers, migrated by the repo's own migrator); the
 * `sim` service, built and run as a child process, not a stub; the Hono app,
 * with every request going through its real route handlers, auth, idempotency
 * and transactions.
 *
 * NOT REAL, and asserted as such:
 *   (a) The passage of time. Harvest accrual is measured from
 *       `harvest_positions.last_settled_at`, so a node on a minutes-old player
 *       has accrued nothing. That row is backdated twelve hours rather than
 *       waiting twelve hours.
 *   (b) The ten creatures deployed against waves 6 and 7. Earning five
 *       specific Taunt/Splash-at-tier-III creatures through the splice would
 *       take far more than the two harvest windows this drive opens, and
 *       whether it is reachable at all is NOT something Phase 6 established.
 *       So the harvest->splice leg and the wave->payout leg are each driven
 *       for real, and are NOT joined by a supply line a player could walk.
 *       That gap is Phase 7's inherited item 9; this file's job is to keep it
 *       VISIBLE rather than to pretend it is closed.
 *
 * NOT a deployed stack. There is none - `gcloud run services list` and
 * `gcloud sql instances list` are both empty, and the Terraform state holds
 * three networking resources and no `api`. Standing one up is billable. Every
 * deployed-stack clause in the phase's done-when is therefore believed, not
 * demonstrated, and this file does not change that.
 */

// fileURLToPath, not .pathname - this repo lives under a directory containing
// a space, which .pathname percent-encodes (wave-submit.test.ts's note).
const REPO = fileURLToPath(new URL('../../../', import.meta.url))
// 0.1.2, not 0.1.1: it is the bundle that authors wave 7 AND carries
// nodes.json, and this drive needs both.
const SEED = join(REPO, 'config/bundles/0.1.2')
const SERVER_ID = 1
const TICK = { tickDayOfWeek: 0, tickMinuteOfDay: 1200 }
const HOUR = 3_600_000
// Distinct from generate-contract.sh's 5199, wave-submit's 5299, replays'
// 5399 and adversarial's 5499 - file parallelism is ON and must stay on, so
// every sim-hosting file needs its own port. Registered in preflight.ts's
// SIM_PORTS, whose list preflight.test.ts pins.
const SIM_PORT = 5599
const SIM_URL = `http://127.0.0.1:${SIM_PORT}`

const AUTHORED = JSON.parse(readFileSync(join(SEED, 'nodes.json'), 'utf8')) as
  Array<{ id: string; ratePerHour: number; totalYield: number | null }>
const COMMON = AUTHORED.find((n) => n.id === 'common_vein')!
// Snapping a window back to a shard-tick boundary; at >= 12h the twelve-hour
// cap makes the window a fixed width anyway, but the snap keeps it exact.
const SHARD_TICK_MS = HOUR / COMMON.ratePerHour

const WAVE_7_REWARD = 230 // config/bundles/0.1.2/waves.json

/**
 * The expected split. Written out rather than derived, because a derived
 * expectation would move with whatever the drive happens to do - which is the
 * one thing this must not do.
 */
// +1 then +2 from the two claims (see the claim block: 720 units is 1.5
// creatures, and 0007 carries the half rather than letting the phase of an
// absolute grid decide where it lands), -2 consumed, +1 child, +1 wave 6
// (the Founder), +1 wave 6 AGAIN (Task 8's Pale - waves_01_12's own text:
// the grant fires on a win too, and this player's first-ever completion IS
// wave 6, so both land on the SAME submit), +1 wave 7.
const EXPECTED_EARNED = 5
const EXPECTED_SEEDED = 10  // two giveRoster() deployments of five

/** The composition that actually beats wave 7, verified against the engine in adversarial.test.ts. */
function wave7Deployment() {
  const TAUNT = 2, SPLASH = 3, VANGUARD = 1
  return [
    { species: SPECIES.Vetch, trait1: TAUNT, tier1: 3, trait2: 0, tier2: 0, instinct: VANGUARD, pocket: 0, hp: CREATURE_HP[SPECIES.Vetch]! },
    { species: SPECIES.Ember, trait1: SPLASH, tier1: 3, trait2: 0, tier2: 0, instinct: VANGUARD, pocket: 1, hp: CREATURE_HP[SPECIES.Ember]! },
    { species: SPECIES.Ember, trait1: SPLASH, tier1: 3, trait2: 0, tier2: 0, instinct: VANGUARD, pocket: 2, hp: CREATURE_HP[SPECIES.Ember]! },
    { species: SPECIES.Hollow, trait1: 0, tier1: 0, trait2: 0, tier2: 0, instinct: VANGUARD, pocket: 3, hp: CREATURE_HP[SPECIES.Hollow]! },
    { species: SPECIES.Hollow, trait1: 0, tier1: 0, trait2: 0, tier2: 0, instinct: VANGUARD, pocket: 4, hp: CREATURE_HP[SPECIES.Hollow]! },
  ]
}

/** The same five in the shape `creatures` stores them - see adversarial.test.ts on why this is by hand. */
function wave7RosterSpecs(): RosterSpec[] {
  return [
    { species: 'Vetch', trait1: 'Taunt', tier1: 3, trait2: 'None', tier2: null, instinct: 'Vanguard', pocket: 0, hp: CREATURE_HP[SPECIES.Vetch]! },
    { species: 'Ember', trait1: 'Splash', tier1: 3, trait2: 'None', tier2: null, instinct: 'Vanguard', pocket: 1, hp: CREATURE_HP[SPECIES.Ember]! },
    { species: 'Ember', trait1: 'Splash', tier1: 3, trait2: 'None', tier2: null, instinct: 'Vanguard', pocket: 2, hp: CREATURE_HP[SPECIES.Ember]! },
    { species: 'Hollow', trait1: 'None', tier1: null, trait2: 'None', tier2: null, instinct: 'Vanguard', pocket: 3, hp: CREATURE_HP[SPECIES.Hollow]! },
    { species: 'Hollow', trait1: 'None', tier1: null, trait2: 'None', tier2: null, instinct: 'Vanguard', pocket: 4, hp: CREATURE_HP[SPECIES.Hollow]! },
  ]
}

/** Starts the REAL sim service for this file - wave-submit.test.ts's dance, same reasoning. */
async function startSim(): Promise<{ stop: () => Promise<void> }> {
  const work = await mkdtemp(join(tmpdir(), 'broodline-loop-sim-'))
  await withDotnetBuildLock(() => execFileSync('dotnet', [
    'build', 'services/sim/Broodline.Sim.Service.csproj', '-c', 'Debug', '-o', work, '--nologo',
  ], { cwd: REPO, stdio: 'inherit' }))
  const proc: ChildProcess = spawn('dotnet', [join(work, 'Broodline.Sim.Service.dll'), '--urls', SIM_URL],
    { cwd: REPO, stdio: 'ignore' })
  const unreap = reapOnExit(proc)

  let ready = false
  for (let i = 0; i < 80; i++) {
    try { if ((await fetch(`${SIM_URL}/healthz`)).ok) { ready = true; break } } catch { /* not up yet */ }
    await new Promise((r) => setTimeout(r, 250))
  }
  if (!ready) {
    proc.kill(); unreap()
    await rm(work, { recursive: true, force: true })
    throw new Error(`sim host never became ready on ${SIM_URL}`)
  }
  return { stop: async () => { proc.kill(); unreap(); await rm(work, { recursive: true, force: true }) } }
}

let t: TestDb
let deps: Deps
let sim: { stop: () => Promise<void> }
let bundleRoot: string

beforeAll(async () => {
  [t, sim] = await Promise.all([startTestDb(), startSim()])
  await t.ownerDb.insert(servers).values({
    serverId: SERVER_ID, region: 'us-central1', state: 'open', tickDayOfWeek: 0, tickMinuteOfDay: 1200,
  })
  bundleRoot = await mkdtemp(join(tmpdir(), 'broodline-loop-bundle-'))
  const store = new LocalBundleStore(bundleRoot)
  await publishBundle(store, SEED, '0.1.2')
  await store.setPointer('0.1.2')
  clearBundleCache()
  deps = {
    db: t.db,
    bundleStore: store,
    simClient: new SimClient(SIM_URL, SimClient.noAuth('local sim host on 127.0.0.1: no Cloud Run in front of it')),
    replayStore: new LocalReplayStore(bundleRoot),
  }
}, 240_000)

afterAll(async () => {
  await t?.stop()
  await sim?.stop()
  // Guarded, like wave-submit.test.ts's: bundleRoot is assigned partway
  // through beforeAll, so an aborted beforeAll would otherwise throw a
  // path TypeError on top of the real error. masked-teardown.test.ts pins why.
  if (bundleRoot) await rm(bundleRoot, { recursive: true, force: true })
})

/**
 * The roster ledger. See `roster-ledger.ts` for why the SPLIT is the
 * assertion, and why the class lives there rather than here: Task 22's
 * `ftue.test.ts` drives the same instrument to the opposite reading
 * (`seeded === 0`), and two copies of it would be free to drift apart
 * silently - a copy that stopped measuring still reads as a ledger.
 */
const roster = new RosterLedger()

// State the drive captures for the assertions that follow it.
let childId: string | undefined
let parentIds: [string, string] | undefined
let closingRosterSize = -1

describe('the loop', () => {
  it(
    'closes end to end: sign in, claim, splice, wave 7, payout - reconciling the roster at every step',
    async () => {
      const { playerId, token } = await setupPlayer(deps)
      const app = createApp(deps)
      const auth = { authorization: `Bearer ${token}` }
      await roster.reconcile('sign in')
      expect(await balance('shards')).toBe(250) // starter.json

      // ---------------------------------------------------------- claim a node
      const epochNow = Number(epochFor(TICK, new Date()))
      const openAWindow = async (slot: number): Promise<void> => {
        // The only direct write outside `seed`, and it writes a CLOCK, not an
        // outcome - it cannot create a creature, so it is outside the ledger.
        const at = new Date(Math.floor(Date.now() / SHARD_TICK_MS) * SHARD_TICK_MS - 12 * HOUR)
        await withServer(deps.db, SERVER_ID, (tx) => tx.insert(harvestPositions).values({
          serverId: SERVER_ID, playerId, regionId: THE_REGION, nodeSlot: slot, epoch: epochNow,
          lastSettledAt: at,
        }).onConflictDoUpdate({
          target: [harvestPositions.serverId, harvestPositions.playerId, harvestPositions.regionId,
            harvestPositions.nodeSlot, harvestPositions.epoch],
          set: { lastSettledAt: at },
        }))
      }

      const state = await (await app.request('/v1/region/state', { headers: auth })).json() as
        { nodes: Array<{ slot: number; type: string }> }
      /**
       * THE RICH DEPOSIT, not the common vein, and the arithmetic is worth
       * keeping: base stock is one gen-1 creature per 480 units
       * (accrual.ts UNITS_PER_CREATURE), so the common vein at 20/hr yields
       * 240 in a full twelve-hour window - the cap - and grants ZERO. The
       * cheap node cannot supply a splice at all. The rich deposit at 60/hr
       * clears 720 and grants one.
       */
      const rich = state.nodes.find((n) => n.type === 'rich_deposit')!
      expect(rich, 'the active bundle must author a rich_deposit for this drive').toBeDefined()

      const claim = async () => {
        await openAWindow(rich.slot)
        return app.request('/v1/node/claim', {
          method: 'POST',
          headers: { ...auth, 'content-type': 'application/json', 'idempotency-key': randomUUID() },
          body: JSON.stringify({ slot: rich.slot }),
        })
      }

      const pool: Array<{ creatureId: string; species: string }> = []
      const granted: number[] = []
      for (let i = 0; i < 2; i++) {
        const { result, delta } = await roster.earn(
          `node/claim on slot ${rich.slot} granted base stock`, claim)
        const body = await result.json() as {
          shards: number; creatures: Array<{ creatureId: string; species: string }>
        }
        expect(result.status).toBe(200)
        expect(body.shards, 'a twelve-hour window on the rich deposit pays 60/hr x 12').toBe(720)
        // 720 units is ONE AND A HALF creatures at 480 each, so the two claims
        // pay 1 then 2 rather than 1 each: 0007 carries the half in a column
        // instead of recovering it from the phase of an absolute floor grid,
        // and the total is the authored rate exactly - 24h x 60/hr is 1,440
        // units, which is the 3 creatures a day `perDay(60, 1)` pins. Before
        // 0007 this assertion read `toBe(1)` and passed or failed depending on
        // what time of day the suite ran.
        granted.push(delta)
        pool.push(...body.creatures)
        await roster.reconcile(`claim ${i + 1}`)
      }
      expect(granted, 'lumpy per claim, exact in total').toEqual([1, 2])
      expect(pool, 'two claims, three creatures - the splice takes the first two').toHaveLength(3)

      // ------------------------------------------------------------- the splice
      const [a, b] = [pool[0]!, pool[1]!]
      parentIds = [a.creatureId, b.creatureId]

      const preview = await app.request('/v1/splice/preview', {
        method: 'POST',
        headers: { ...auth, 'content-type': 'application/json' },
        body: JSON.stringify({ parentA: a.creatureId, parentB: b.creatureId, locked: { from: 'a', slot: 'trait_1' } }),
      })
      expect(preview.status).toBe(200)
      // The forecast's CONTENT is splice-distribution.test.ts's subject; what
      // matters here is only that the confirmation screen has something to
      // publish before the charge is spent.
      expect(await preview.json()).toHaveProperty('forecast')

      const { result: commitRes, delta: spliceDelta } = await roster.earn(
        'splice consumed both parents and produced the child',
        () => app.request('/v1/splice/commit', {
          method: 'POST',
          headers: { ...auth, 'content-type': 'application/json', 'idempotency-key': randomUUID() },
          // bodyFrom must be one of the two parents' species, and the base-stock
          // roll is random, so it is read off the parent rather than named.
          body: JSON.stringify({
            parentA: a.creatureId, parentB: b.creatureId,
            locked: { from: 'a', slot: 'trait_1' }, bodyFrom: a.species,
          }),
        }))
      expect(commitRes.status).toBe(200)
      const commit = await commitRes.json() as { child: { creatureId: string; generation: number } }
      childId = commit.child.creatureId
      expect(commit.child.generation, 'two gen-1 parents make a gen-2 child').toBe(2)
      expect(spliceDelta, 'two parents in, one child out').toBe(-1)

      const parentRows = await withServer(deps.db, SERVER_ID, (tx) => tx.select().from(creatures)
        .where(and(eq(creatures.playerId, playerId),
          sql`${creatures.creatureId} IN (${sql.raw(`'${a.creatureId}','${b.creatureId}'`)})`)))
      expect(parentRows).toHaveLength(2)
      expect(parentRows.every((r) => r.consumedAt !== null), 'BOTH PARENTS GONE').toBe(true)

      const afterSplice = await (await app.request('/v1/roster', { headers: auth })).json() as
        { creatures: Array<{ creatureId: string }> }
      const listed = afterSplice.creatures.map((c) => c.creatureId)
      expect(listed, 'the child is on the roster').toContain(childId)
      expect(listed, 'and neither parent is').not.toContain(a.creatureId)
      expect(listed).not.toContain(b.creatureId)
      await roster.reconcile('the splice')

      // ------------------------------------- wave 6, cleared by PLAYING it
      // Not by writing campaign_progress: wave 7 is gated on wave 6, and a
      // drive that wrote the gate open would not be driving the loop.
      const w6 = await roster.seed('giveRoster minted the wave-6 deployment',
        () => giveRoster(asRosterSpecs(winningDeployment())))
      expect(w6.delta, 'five inserted directly - NOT earned').toBe(5)
      await roster.reconcile('seeding the wave-6 roster')

      const start6 = await app.request('/v1/wave/start', {
        method: 'POST', headers: { ...auth, 'content-type': 'application/json' },
        body: JSON.stringify({ waveId: 6, deployment: w6.result }),
      })
      expect(start6.status).toBe(200)
      const s6 = await start6.json() as { issuanceId: string; seed: string }
      const { result: sub6 } = await roster.earn(
        'wave-6 win granted the Founder AND the Task 8 Pale - this player\'s first-ever completion - on the verified submit path',
        () => submit(s6.issuanceId, buildWinningReplay(6, BigInt(s6.seed)), randomUUID()))
      expect(sub6.status).toBe(200)
      expect(await sub6.json()).toMatchObject({ result: 'Win' })
      await roster.reconcile('the wave-6 win')

      // THE COUNT ALONE CANNOT SEE THIS, and that is exactly the blindness
      // this file's own header opens by warning about - just for
      // Founder-vs-rolled rather than earned-vs-seeded. `roster.earn()`
      // above measured a roster-count delta of +2: `WAVE_BASE_STOCK` is 1
      // whether that grant is an ordinary roll or Task 6's Founder, and
      // Task 8's wave-6 Pale is a SEPARATE, independent +1 on top of it -
      // waves_01_12's own text is that the Pale grant fires on a win too,
      // not only on the designed loss. Nothing before this player's wave-6
      // submit ever advanced `campaign_progress` (no wave was cleared by
      // playing it, and `clearWave`/`clearThrough` are never called in this
      // drive), so wave 6 genuinely IS this player's first-ever completion -
      // Task 6's Founder branch fires HERE, not at wave 7's. Pinned by
      // reading the roster the way a client would (GET /v1/roster), not by
      // trusting the count `roster.reconcile` just vouched for: a count
      // that agrees is not evidence about WHAT was granted, only how much.
      const afterWave6 = await (await app.request('/v1/roster', { headers: auth })).json() as
        { creatures: Array<{ species: string; isFounder: boolean }> }
      const wave6Founders = afterWave6.creatures.filter((c) => c.isFounder)
      expect(wave6Founders, 'wave 6\'s grant IS the Founder, not a roll').toHaveLength(1)
      expect(wave6Founders[0]?.species).toBe('Hollow')

      // --------------------------------- wave 7, with five owned creatures
      const w7 = await roster.seed('giveRoster minted the wave-7 deployment',
        () => giveRoster(wave7RosterSpecs()))
      expect(w7.delta, 'five more inserted directly - NOT earned').toBe(5)
      await roster.reconcile('seeding the wave-7 roster')

      const start7 = await app.request('/v1/wave/start', {
        method: 'POST', headers: { ...auth, 'content-type': 'application/json' },
        body: JSON.stringify({ waveId: 7, deployment: w7.result }),
      })
      expect(start7.status, 'wave 7 unlocked because wave 6 was really cleared').toBe(200)
      const s7 = await start7.json() as { issuanceId: string; seed: string }

      const committed = await withServer(deps.db, SERVER_ID, (tx) => tx.select().from(creatures)
        .where(and(eq(creatures.playerId, playerId), sql`${creatures.committedTo} IS NOT NULL`)))
      expect(committed, 'the five are committed_to the live issuance').toHaveLength(5)

      // ----------------------------------------------- submit, and the payout
      const shardsBefore = await balance('shards')
      const ledgerBefore = await ledgerRowCount()

      const { result: sub7 } = await roster.earn('wave-7 win granted base stock on the verified submit path',
        () => submit(s7.issuanceId, buildReplayOf(7, BigInt(s7.seed), wave7Deployment()), randomUUID()))
      expect(sub7.status).toBe(200)
      expect(await sub7.json()).toMatchObject({
        result: 'Win',
        reward: { currency: 'shards', amount: WAVE_7_REWARD },
      })

      expect(await balance('shards') - shardsBefore, `${WAVE_7_REWARD} shards, credited`).toBe(WAVE_7_REWARD)
      expect(await ledgerRowCount() - ledgerBefore, 'exactly ONE ledger row').toBe(1)

      const stillCommitted = await withServer(deps.db, SERVER_ID, (tx) => tx.select().from(creatures)
        .where(and(eq(creatures.playerId, playerId), sql`${creatures.committedTo} IS NOT NULL`)))
      expect(stillCommitted, 'committed_to CLEARED on every creature').toHaveLength(0)

      const [issuance] = await withServer(deps.db, SERVER_ID, (tx) => tx.select().from(waveIssuances)
        .where(eq(waveIssuances.issuanceId, s7.issuanceId)))
      expect(issuance?.settledAt, 'the issuance is settled, not left live').not.toBeNull()

      const finalParents = await withServer(deps.db, SERVER_ID, (tx) => tx.select().from(creatures)
        .where(sql`${creatures.creatureId} IN (${sql.raw(`'${a.creatureId}','${b.creatureId}'`)})`))
      expect(finalParents.every((r) => r.consumedAt !== null), 'both parents STILL gone').toBe(true)

      const closing = await (await app.request('/v1/roster', { headers: auth })).json() as
        { creatures: Array<{ creatureId: string }> }
      closingRosterSize = closing.creatures.length
      expect(closing.creatures.map((c) => c.creatureId), 'the child survived the wave').toContain(childId)

      await roster.reconcile('the wave-7 win')
    },
    300_000,
  )

  /**
   * THE FINDING, as an assertion.
   *
   * This is separate from the drive above rather than folded into its tail,
   * because it is not a step of the loop - it is the claim ABOUT the loop that
   * the original transcript got wrong, and it should fail under its own name
   * so the report says "the split moved" rather than "the loop broke".
   */
  it('accounts every creature as EARNED or SEEDED, and ten of the fifteen were SEEDED', () => {
    // Printed on failure only - a reader diagnosing a moved split needs the
    // itemisation, and a reader of a green run does not.
    const detail = `\n${roster.entries.map((e) => `  ${e}`).join('\n')}\n`

    expect(roster.seeded, `SEEDED count moved.${detail}`).toBe(EXPECTED_SEEDED)
    expect(roster.earned, `EARNED count moved.${detail}`).toBe(EXPECTED_EARNED)

    // The sum LAST, and never instead of the two above: booking all thirteen
    // to one bucket satisfies the sum and is precisely the conflation this
    // file exists to prevent.
    expect(roster.total).toBe(EXPECTED_EARNED + EXPECTED_SEEDED)
    expect(closingRosterSize, 'and GET /v1/roster agrees with the books').toBe(roster.total)
  })

  /**
   * The gap, pinned so it cannot be quietly closed in prose without the code
   * changing. Phase 7's inherited item 9.
   *
   * If a future change genuinely joins the two legs - content a player can
   * harvest into a wave-7-capable deployment - this test is what goes red, and
   * the right response is to update EXPECTED_SEEDED and delete this test with
   * the record saying so. Until then it states, as an executable fact, that the
   * loop is two legs that do not meet.
   */
  it('does NOT yet join its two legs: the wave deployments were never earned', () => {
    expect(roster.seeded, 'ten seeded creatures is the unjoined supply line').toBeGreaterThan(0)
    expect(childId, 'the harvest->splice leg IS fully earned, end to end').toBeDefined()
    expect(parentIds, 'and its two parents came out of node/claim, not an insert').toBeDefined()
  })
})
