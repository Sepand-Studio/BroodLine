import { type ChildProcess, execFileSync, spawn } from 'node:child_process'
import { randomUUID } from 'node:crypto'
import { mkdtemp, rm } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { afterAll, beforeAll, describe, expect, it } from 'vitest'
import { createApp, type Deps } from '../src/app.ts'
import { clearBundleCache } from '../src/config/bundle.ts'
import { publishBundle } from '../src/config/publish.ts'
import { LocalBundleStore } from '../src/config/store.ts'
import { servers } from '../src/db/schema.ts'
import { LocalReplayStore } from '../src/replays/store.ts'
import { SimClient } from '../src/sim/client.ts'
import { reapOnExit } from './child-reaper.ts'
import { startTestDb, type TestDb } from './harness.ts'
import {
  coldOpenDeployment, CREATURE_HP, type ReplayCreature, SPECIES, TRAIT, wave2TrioDeployment,
} from './replay-format.ts'
import { RosterLedger } from './roster-ledger.ts'
import {
  balance, buildReplayOf, type Deployed, ledgerRowCount, setupPlayer, startWave, submit,
  withDotnetBuildLock,
} from './wave-helpers.ts'

/**
 * THE FIRST HOUR, DRIVEN END TO END - design §5's beats as a gate rather than
 * as a claim.
 *
 * It closes Phase 6 `followups` §12 item 9 **for the first hour, and for
 * nothing else**. That item asks for a drive whose supply line is unbroken;
 * this is one, from account creation to a won wave 6. It says nothing about
 * wave 7, where `loop.test.ts` still seeds ten creatures and still has a test
 * saying so. Two files, two readings, both true - and the overclaim ("item 9
 * closed", full stop) is the exact shape of the sentence this phase exists to
 * stop writing.
 *
 * ---------------------------------------------------------------------------
 * WHAT THIS FILE IS FOR, and the sentence it exists to make writable.
 *
 * `loop.test.ts` drives design §1's loop and books TEN of its fifteen
 * creatures as SEEDED - inserted straight into `creatures` by `giveRoster`,
 * because earning five Taunt/Splash-at-tier-III creatures through the splice
 * was not something Phase 6 established was reachable at all. Its last test
 * says so as an executable fact: `does NOT yet join its two legs`. Phase 6's
 * record nevertheless wrote "the loop closes", and what closed was two half
 * loops that met on paper.
 *
 * This file is the other half of that sentence, and it is the half that can
 * be earned today. Every creature here arrives through a RESPONSE:
 *
 *   `POST /v1/account`      the cold-open pair (starter.json)
 *   wave 1, won             the Founder, instead of a roll
 *   wave 2, won             one rolled base-stock creature
 *   `POST /v1/ftue/splice-stock`  the tutorial pair
 *   `POST /v1/splice/commit`      -2 parents, +1 child
 *   wave 6, LOST            the Pale, carrying Chill
 *   wave 6, WON with it     one more rolled base-stock creature
 *
 * `seeded === 0` IS THE ASSERTION OF THIS FILE. A drive that walked the same
 * eight screens with `giveRoster` behind it would print the same beats and
 * prove nothing about whether a player could reach them, which is precisely
 * the mistake `roster-ledger.ts`'s header records. The ledger is not
 * instrumentation here: the split is asserted, and one half of it is zero.
 *
 * ---------------------------------------------------------------------------
 * WHAT IS REAL HERE, AND WHAT IS NOT. Stated because the record this file
 * feeds got exactly this wrong once.
 *
 * REAL: Postgres (Testcontainers, migrated by the repo's own migrator); the
 * `sim` service, built and run as a child process, not a stub, so every
 * Win and Loss below is one the ENGINE returned; the Hono app, with every
 * request going through its real route handlers, auth, idempotency and
 * transactions.
 *
 * NOT REAL, and asserted as such:
 *   (a) The CLIENT. This walks the eight beats over the api's routes in the
 *       order `FtueDirector` walks them; it does not press a button. Nothing
 *       on this branch has ever executed a director-to-view join - that is
 *       owed to an eyes-on Editor session, and this file does not close it.
 *   (b) A DEPLOYED STACK. There is none. `smoke-loop.sh` against Cloud Run is
 *       the gate that would make these same routes true over real
 *       infrastructure, and it has never been run because nothing is
 *       deployed. This file proves the line exists; it does not prove anyone
 *       can reach it over the internet.
 */

// fileURLToPath, not .pathname - this repo lives under a directory containing
// a space, which .pathname percent-encodes (wave-submit.test.ts's note).
const REPO = fileURLToPath(new URL('../../../', import.meta.url))
/**
 * 0.1.3, and it is the ONLY bundle that can carry this walk: it is the one
 * that authors starter.json's cold-open pair AND waves 1, 2, 6 and 7. 0.1.1
 * and 0.1.2 author neither wave 1 nor wave 2 and grant no starter creatures,
 * so beats 1-5 are unreachable under them.
 */
const SEED = join(REPO, 'config/bundles/0.1.3')
const SERVER_ID = 1
// Distinct from every other sim-hosting file's port - test/preflight.ts's
// SIM_PORTS, whose exact list preflight.test.ts pins.
const SIM_PORT = 5899
const SIM_URL = `http://127.0.0.1:${SIM_PORT}`

// config/bundles/0.1.3/waves.json. Written out rather than read off the
// bundle, so a content change that moves a payout reddens this file instead
// of being agreed with by it.
const STARTER_SHARDS = 250      // starter.json
const STARTER_CHARGES = 3       // starter.json
const WAVE_1_REWARD = 150
const WAVE_2_REWARD = 165
const WAVE_6_REWARD = 40

/** The name the player gives the Founder at beat 4. Deliberately not a species. */
const FOUNDER_NAME = 'Ash'

/**
 * The expected split, written out rather than derived - a derived expectation
 * would move with whatever the drive happens to do, which is the one thing it
 * must not do.
 */
// +2 account, +1 Founder (wave 1), +1 roll (wave 2), +2 tutorial pair,
// -1 splice (two parents in, one child out), +1 Pale (wave 6 lost),
// +1 roll (wave 6 won).
const EXPECTED_EARNED = 7
const EXPECTED_SEEDED = 0

/**
 * The wave-6 Pale AS THE REPLAY CARRIES IT.
 *
 * Hand-written, and it must agree field-for-field with what
 * `src/ftue/pale.ts`'s `WAVE6_PALE` actually mints - `Chill`/1 in slot one
 * and `Carapace`/1 in slot two, because it IS a base-stock Pale handed over
 * by name rather than by roll. `winningDeployment()`'s Pale is NOT this
 * creature: its second slot is empty, so a replay built from it would be
 * refused `deployment_mismatch` against an issuance resolved from the row the
 * server actually granted. That refusal is the good failure mode - it is
 * loud, it names the disagreement, and it cannot pass by accident.
 */
function grantedPale(pocket: number): ReplayCreature {
  return {
    species: SPECIES.Pale,
    trait1: TRAIT.Chill, tier1: 1,
    trait2: TRAIT.Carapace, tier2: 1,
    instinct: 1, // engine/Runtime/Combat/Ids.cs Instinct.Vanguard
    pocket,
    hp: CREATURE_HP[SPECIES.Pale]!,
  }
}

/** Starts the REAL sim service for this file - wave-submit.test.ts's dance, same reasoning. */
async function startSim(): Promise<{ stop: () => Promise<void> }> {
  const work = await mkdtemp(join(tmpdir(), 'broodline-ftue-sim-'))
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
// A local app for the routes wave-helpers does not drive - GET /v1/roster,
// POST /v1/creature/name, POST /v1/ftue/splice-stock, the two splice routes
// and GET /v1/lineage. wave-helpers' own startWave/submit act against the
// SAME account, set by this file's one setupPlayer() call (founder.test.ts's
// idiom, same reasoning).
let app: ReturnType<typeof createApp>
let auth: Record<string, string>

beforeAll(async () => {
  [t, sim] = await Promise.all([startTestDb(), startSim()])
  await t.ownerDb.insert(servers).values({
    serverId: SERVER_ID, region: 'us-central1', state: 'open', tickDayOfWeek: 0, tickMinuteOfDay: 1200,
  })
  bundleRoot = await mkdtemp(join(tmpdir(), 'broodline-ftue-bundle-'))
  const store = new LocalBundleStore(bundleRoot)
  await publishBundle(store, SEED, '0.1.3')
  await store.setPointer('0.1.3')
  clearBundleCache()
  deps = {
    db: t.db,
    bundleStore: store,
    simClient: new SimClient(SIM_URL, SimClient.noAuth('local sim host on 127.0.0.1: no Cloud Run in front of it')),
    replayStore: new LocalReplayStore(bundleRoot),
  }
  app = createApp(deps)
}, 240_000)

afterAll(async () => {
  await t?.stop()
  await sim?.stop()
  // Guarded, like every other file's: bundleRoot is assigned partway through
  // beforeAll, so an aborted beforeAll would otherwise throw a path TypeError
  // on top of the real error - see masked-teardown.test.ts.
  if (bundleRoot) await rm(bundleRoot, { recursive: true, force: true })
})

// --- Route drivers beyond wave-helpers'.

interface RosterCreature {
  creatureId: string
  species: string
  generation: number
  name: string | null
  isFounder: boolean
  trait1: string
  tier1: number | null
  trait2: string
  tier2: number | null
}

async function roster(): Promise<RosterCreature[]> {
  const res = await app.request('/v1/roster', { headers: auth })
  expect(res.status).toBe(200)
  return ((await res.json()) as { creatures: RosterCreature[] }).creatures
}

interface LineageNode {
  creatureId: string
  species: string
  generation: number
  isFounder: boolean
  name: string | null
  parentA: string | null
  parentB: string | null
  consumedAt: string | null
  pruned: boolean
  mutated: boolean
}

async function lineage(): Promise<LineageNode[]> {
  const res = await app.request('/v1/lineage', { headers: auth })
  expect(res.status).toBe(200)
  return ((await res.json()) as { nodes: LineageNode[] }).nodes
}

function nodeFor(nodes: readonly LineageNode[], creatureId: string, what: string): LineageNode {
  const node = nodes.find((n) => n.creatureId === creatureId)
  if (node === undefined) {
    throw new Error(`the lineage tree does not carry ${what} (${creatureId}): ${JSON.stringify(nodes)}`)
  }
  return node
}

interface SubmitBody {
  result: string
  reward?: { currency: string; amount: number }
  granted?: RosterCreature[]
}

/**
 * One wave, played: start with a deployment of OWNED creatures, submit a
 * replay of exactly those creatures, and hand back what the server said.
 *
 * `deployment` and `claimed` are two descriptions of one thing and they must
 * agree - `wave/submit` compares the deployment `sim` echoed against the one
 * the issuance froze, INDEXED, so an honest submission is the only kind this
 * helper can make. Taking both as parameters rather than deriving one from
 * the other is deliberate: deriving would make every submission honest by
 * construction and delete the ability to write a dishonest one, which is what
 * `wave-submit.test.ts` needs and is not this file's job to foreclose.
 */
async function playWave(
  waveId: number, deployment: Deployed[], claimed: readonly ReplayCreature[],
): Promise<Response> {
  const start = await startWave(waveId, deployment)
  expect(start.status, `wave ${waveId} must be startable at this point in the walk`).toBe(200)
  const { issuanceId, seed } = await start.json() as { issuanceId: string; seed: string }
  return submit(issuanceId, buildReplayOf(waveId, BigInt(seed), claimed), randomUUID())
}

const ledger = new RosterLedger()

// State the walk captures for the assertions that follow it.
let founderId: string | undefined
let childId: string | undefined
let tutorialPair: [string, string] | undefined
let paleId: string | undefined
let closingRosterSize = -1

describe('the first hour', () => {
  it(
    'is one line a player can walk: account, wave 1, the Founder, wave 2, the guided splice, '
    + 'wave 6 lost, the Pale, wave 6 won - with every creature EARNED',
    async () => {
      // ------------------------------------------------- beat 1: the account
      const { token } = await setupPlayer(deps)
      auth = { authorization: `Bearer ${token}` }
      const opening = await ledger.open('POST /v1/account granted starter.json\'s cold-open pair')
      expect(opening, 'starter.json authors a Vetch and an Ember').toBe(2)
      await ledger.reconcile('sign in')
      expect(await balance('shards')).toBe(STARTER_SHARDS)
      expect(await balance('splice_charges')).toBe(STARTER_CHARGES)

      // Read off the roster rather than assumed: account creation guarantees
      // only that a Vetch and an Ember exist, not the order `POST /v1/account`
      // inserted them in nor the order `GET /v1/roster` (by creatureId)
      // returns them in. Captured HERE, before anything else can add a second
      // Vetch or Ember - the tutorial pair below is exactly that.
      const opened = await roster()
      // ASSERTED ON THE ROSTER, not with `!` and a `toBeDefined()` after it.
      // That shape - `find(...)!.creatureId` followed by
      // `expect(id).toBeDefined()` - reads as a guard and is not one: a
      // missing starter throws a TypeError on the line ABOVE and the
      // assertion never runs, so it can only pass. It was written that way
      // here and caught in review, which makes it the eleventh instance of
      // this branch's dominant defect and the first one inside the file
      // written to close it.
      expect(opened.map((c) => c.species).sort(),
        'starter.json authors exactly a Vetch and an Ember').toEqual(['Ember', 'Vetch'])
      const vetchId = opened.find((c) => c.species === 'Vetch')!.creatureId
      const emberId = opened.find((c) => c.species === 'Ember')!.creatureId
      expect(opened.every((c) => !c.isFounder && c.generation === 1 && c.name === null)).toBe(true)

      // The pair AS THE REPLAY WILL CLAIM IT. Pinned against the roster rows
      // the server actually minted, because every wave below submits
      // `coldOpenDeployment()` and a disagreement would surface as a
      // `deployment_mismatch` several beats later, pointing at the wrong
      // thing.
      const authoredPair = coldOpenDeployment()
      const asMinted = [vetchId, emberId].map((id) => {
        const c = opened.find((r) => r.creatureId === id)!
        return {
          species: c.species, trait1: c.trait1, tier1: c.tier1, trait2: c.trait2, tier2: c.tier2,
        }
      })
      expect(asMinted, 'the rows the server minted must be the pair coldOpenDeployment() claims').toEqual([
        { species: 'Vetch', trait1: 'Taunt', tier1: 1, trait2: 'Carapace', tier2: 1 },
        { species: 'Ember', trait1: 'Splash', tier1: 1, trait2: 'Carapace', tier2: 1 },
      ])
      expect(authoredPair.map((c) => c.species), 'and in that order').toEqual([SPECIES.Vetch, SPECIES.Ember])

      // -------------------------------------- beats 2-3: wave 1, and the Founder
      // Pockets 0 and 2 on the six-pocket Defile lane. task-1-report.md
      // confirms the cold-open pair wins wave 1 at EVERY ordered pocket pair,
      // which is what makes this placement a choice rather than a dependency.
      const pairDeployed: Deployed[] = [
        { creatureId: vetchId, pocket: 0 }, { creatureId: emberId, pocket: 2 },
      ]
      const { result: won1 } = await ledger.earn(
        'wave 1 won: the FIRST completion grants the Founder instead of a roll',
        () => playWave(1, pairDeployed, authoredPair))
      expect(won1.status).toBe(200)
      const w1 = await won1.json() as SubmitBody
      expect(w1.result, 'the cold-open pair really beats wave 1 - the ENGINE said so').toBe('Win')
      expect(w1.reward).toEqual({ currency: 'shards', amount: WAVE_1_REWARD })
      await ledger.reconcile('wave 1')

      // THE COUNT ALONE CANNOT SEE THIS, and the blindness is the one
      // `loop.test.ts` was bitten by: `WAVE_BASE_STOCK` is 1 whether the grant
      // is a roll or the Founder, so a +1 says nothing about WHICH. Read back
      // the way a client would.
      expect(w1.granted?.map((c) => c.species), 'the grant is reported to the player').toEqual(['Hollow'])
      const afterWave1 = await roster()
      const founders = afterWave1.filter((c) => c.isFounder)
      expect(founders, 'exactly one Founder, and wave 1\'s grant IS it').toHaveLength(1)
      expect(founders[0]?.species, 'design §5 beat 3: the Founder is a Hollow').toBe('Hollow')
      expect(founders[0]?.name, 'granted unnamed - beat 4 is where the player names it').toBeNull()
      founderId = founders[0]!.creatureId

      // ------------------------------------------- beat 4: the player names it
      const named = await app.request('/v1/creature/name', {
        method: 'POST',
        headers: { ...auth, 'content-type': 'application/json', 'idempotency-key': randomUUID() },
        body: JSON.stringify({ creatureId: founderId, name: FOUNDER_NAME }),
      })
      expect(named.status).toBe(200)
      expect(((await named.json()) as { name: string }).name).toBe(FOUNDER_NAME)
      // Naming is not a grant. Reconciled rather than assumed, because
      // `creature/name` is a write on the `creatures` table and this ledger's
      // whole job is that no write on that table goes unbooked.
      await ledger.reconcile('naming the Founder')

      // -------------------------------- beat 5: wave 2, with the Founder in it
      const trioDeployed: Deployed[] = [...pairDeployed, { creatureId: founderId, pocket: 4 }]
      const { result: won2 } = await ledger.earn(
        'wave 2 won: the SECOND completion grants rolled base stock, never a second Founder',
        () => playWave(2, trioDeployed, wave2TrioDeployment()))
      expect(won2.status).toBe(200)
      const w2 = await won2.json() as SubmitBody
      expect(w2.result, 'the trio beats wave 2 - the Vetch holds the Lash because Taunt is on it').toBe('Win')
      expect(w2.reward).toEqual({ currency: 'shards', amount: WAVE_2_REWARD })
      expect(w2.granted, 'one creature, rolled').toHaveLength(1)
      expect(w2.granted?.[0]?.isFounder, 'a SECOND Founder would be the bug beat 3 exists to avoid').toBe(false)
      expect(w2.granted?.[0]?.species, 'and Pale is withheld from base stock until wave 6 grants one')
        .not.toBe('Pale')
      await ledger.reconcile('wave 2')
      expect((await roster()).filter((c) => c.isFounder), 'still exactly one Founder').toHaveLength(1)

      // -------------------------- beats 6-7: the guided splice's provided pair
      // `grantTutorialStock`'s three gates are all now true and none of them
      // is anything a client could fabricate: wave 2 cleared by PLAYING it,
      // this player's first splice not yet spent, the write-once marker unset.
      const { result: stockRes, delta: stockDelta } = await ledger.earn(
        'POST /v1/ftue/splice-stock granted the tutorial pair',
        () => app.request('/v1/ftue/splice-stock', {
          method: 'POST', headers: { ...auth, 'idempotency-key': randomUUID() },
        }))
      expect(stockRes.status).toBe(200)
      const stock = (await stockRes.json() as { creatures: RosterCreature[] }).creatures
      expect(stockDelta, 'the tutorial pair is TWO creatures').toBe(2)
      expect(stock.map((c) => c.species).sort()).toEqual(['Ember', 'Vetch'])
      expect(stock.every((c) => !c.isFounder && c.generation === 1),
        'sample stock, never the Founder the player just named').toBe(true)
      // BY ID, NOT BY SPECIES, for the rest of the walk: the roster now holds
      // two Vetch and two Ember, and picking a parent by species would be free
      // to splice away a starter creature and still pass.
      const [parentA, parentB] = [stock[0]!, stock[1]!]
      expect(parentA.creatureId).not.toBe(vetchId)
      expect(parentA.creatureId).not.toBe(emberId)
      expect(parentB.creatureId).not.toBe(vetchId)
      expect(parentB.creatureId).not.toBe(emberId)
      tutorialPair = [parentA.creatureId, parentB.creatureId]
      await ledger.reconcile('the tutorial pair')

      // The forecast the confirmation screen publishes BEFORE the charge is
      // spent. `mutation: 1` is the whole lesson of the beat - the guaranteed
      // mutation is what makes the player's first splice show them that a
      // splice changes something.
      const locked = { from: 'a' as const, slot: 'trait_1' as const }
      const previewRes = await app.request('/v1/splice/preview', {
        method: 'POST',
        headers: { ...auth, 'content-type': 'application/json' },
        body: JSON.stringify({ parentA: parentA.creatureId, parentB: parentB.creatureId, locked }),
      })
      expect(previewRes.status).toBe(200)
      const preview = await previewRes.json() as
        { forecast: { mutation: number }; coverageLost: unknown }
      expect(preview.forecast.mutation, 'this player\'s FIRST splice is a guaranteed mutation').toBe(1)
      expect(preview.coverageLost, 'and the screen is told what the two unlocked slots cost').toBeDefined()

      const chargesBefore = await balance('splice_charges')
      const { result: commitRes, delta: spliceDelta } = await ledger.earn(
        'POST /v1/splice/commit consumed both tutorial parents and produced the child',
        () => app.request('/v1/splice/commit', {
          method: 'POST',
          headers: { ...auth, 'content-type': 'application/json', 'idempotency-key': randomUUID() },
          // `bodyFrom` must be one of the two parents' species, read off the
          // parent rather than named - the tutorial sends parent A's, which is
          // what `FtueDirector` does for want of a body picker.
          body: JSON.stringify({
            parentA: parentA.creatureId, parentB: parentB.creatureId,
            locked, bodyFrom: parentA.species,
          }),
        }))
      expect(commitRes.status).toBe(200)
      const commit = await commitRes.json() as { child: RosterCreature }
      childId = commit.child.creatureId
      expect(commit.child.generation, 'two gen-1 parents make a gen-2 child').toBe(2)
      expect(spliceDelta, 'two parents in, one child out').toBe(-1)
      expect(await balance('splice_charges'), 'design §5.2: one splice, one charge')
        .toBe(chargesBefore - 1)
      await ledger.reconcile('the guided splice')

      // ------------------------------------------- beat 8: the Lineage View
      const tree = await lineage()
      const child = nodeFor(tree, childId, 'the child')
      // THE MUTATION, ASSERTED WHERE IT IS ACTUALLY OBSERVABLE.
      // `SpliceCommitResponse` deliberately carries no `mutated` (design §10
      // defers Aberrant traits and the child's slots read identically either
      // way, so routes/splice.ts withholds it); the lineage tree is where the
      // client reads it, and this is the beat that reads it.
      expect(child.mutated, 'the forecast promised mutation 1 and the commit must have delivered it')
        .toBe(true)
      expect(child.generation).toBe(2)
      expect([child.parentA, child.parentB].sort(),
        'the child names the two creatures it came from').toEqual([...tutorialPair].sort())
      expect(child.consumedAt, 'the child is alive').toBeNull()
      expect(child.pruned).toBe(false)

      // BOTH CONSUMED PARENTS, STILL WHOLE. `splice_confirm_spec` §5 makes
      // this the FTUE's entire lesson, and it is the one screen in the
      // service where filtering dead rows - which every sibling roster reader
      // is right to do - would be the bug.
      for (const id of tutorialPair) {
        const parent = nodeFor(tree, id, 'a consumed tutorial parent')
        expect(parent.consumedAt, 'a spliced parent is gone from the roster and PRESENT here')
          .not.toBeNull()
        expect(parent.pruned, 'consumed is not pruned - a tombstone would have no traits to show')
          .toBe(false)
        expect(parent.generation).toBe(1)
      }

      // AND THE NAMED FOUNDER, in the same tree - beat 4's name carried
      // through to beat 8's screen.
      const founderNode = nodeFor(tree, founderId, 'the Founder')
      expect(founderNode.isFounder).toBe(true)
      expect(founderNode.name, 'the name the player gave it at beat 4').toBe(FOUNDER_NAME)
      expect(founderNode.consumedAt).toBeNull()

      // Neither parent is on the roster any more, which is the other half of
      // the same lesson.
      const afterSplice = (await roster()).map((c) => c.creatureId)
      expect(afterSplice, 'the child is on the roster').toContain(childId)
      expect(afterSplice).not.toContain(tutorialPair[0])
      expect(afterSplice).not.toContain(tutorialPair[1])

      // --------------------------------------- wave 6, LOST, and the Pale
      // THE DESIGNED LOSS. waves_01_12 wave 6 is one Courser down an empty
      // lane and the only answer to a Courser is Chill - which this player has
      // no path to: Pale is withheld from every base-stock roll until this
      // grant fires, and `bodyFrom` makes the splice child one of its parents'
      // species. So the pair that has won every wave so far loses this one.
      //
      // THE PAIR, NOT THE WHOLE ROSTER, and the reason is honesty rather than
      // convenience: the wave-2 roll and the splice child are ROLLED, so a
      // deployment containing them would make this wave's outcome depend on a
      // die - a Hollow rolled into the back pocket out-damages a Vetch by
      // four to one. A test that sometimes wins the wave it claims to lose is
      // worth less than a smaller claim that always holds.
      const { result: lost6 } = await ledger.earn(
        'wave 6 LOST: the Wave Defeat screen grants a Pale carrying Chill',
        () => playWave(6, pairDeployed, authoredPair))
      expect(lost6.status).toBe(200)
      const l6 = await lost6.json() as SubmitBody
      expect(l6.result, 'the pair cannot answer a Courser').toBe('Loss')
      expect(l6.reward, 'a lost wave pays nothing').toBeUndefined()
      expect(l6.granted?.map((c) => c.species), 'the resupply, and only it').toEqual(['Pale'])
      expect(l6.granted?.[0]?.trait1, 'Stats.CounterFor(Courser) is Chill and nothing else').toBe('Chill')
      paleId = l6.granted![0]!.creatureId
      await ledger.reconcile('wave 6, lost')

      // The Pale the server minted, read back and pinned field-for-field
      // against what the replay below is about to claim. A disagreement here
      // is the reason `grantedPale()` is hand-written, and catching it at this
      // line beats catching it as a `deployment_mismatch` two requests later.
      const pale = (await roster()).find((c) => c.creatureId === paleId)!
      expect({
        species: pale.species, trait1: pale.trait1, tier1: pale.tier1,
        trait2: pale.trait2, tier2: pale.tier2,
      }).toEqual({ species: 'Pale', trait1: 'Chill', tier1: 1, trait2: 'Carapace', tier2: 1 })

      // ------------------------------------- wave 6, WON, with the Pale in it
      // THE SAME TWO CREATURES, THE SAME WAVE, THE SAME SEEDLESS LANE - plus
      // the Pale. That contrast is the assertion: the difference between the
      // Loss above and the Win below is one creature the player was given
      // BECAUSE they lost, which is what makes wave 6 a lesson rather than a
      // wall.
      const shardsBefore = await balance('shards')
      const ledgerRowsBefore = await ledgerRowCount()
      const { result: won6 } = await ledger.earn(
        'wave 6 WON with the Pale: the Pale grant is once-per-player, so this one is a roll',
        () => playWave(
          6,
          [...pairDeployed, { creatureId: paleId!, pocket: 4 }],
          [...authoredPair, grantedPale(4)]))
      expect(won6.status).toBe(200)
      const w6 = await won6.json() as SubmitBody
      expect(w6.result, 'Chill answers the Courser').toBe('Win')
      expect(w6.reward).toEqual({ currency: 'shards', amount: WAVE_6_REWARD })
      expect(w6.granted?.map((c) => c.species),
        'ONE creature: the Pale grant is write-once, so this win grants a roll and not a second Pale')
        .toHaveLength(1)
      expect(w6.granted?.[0]?.creatureId).not.toBe(paleId)
      await ledger.reconcile('wave 6, won')

      expect(await balance('shards') - shardsBefore, `${WAVE_6_REWARD} shards, credited`)
        .toBe(WAVE_6_REWARD)
      expect(await ledgerRowCount() - ledgerRowsBefore, 'exactly ONE ledger row').toBe(1)

      // The arithmetic of the whole walk, in one number. Every shard came from
      // a wave this player played.
      expect(await balance('shards'),
        'starter + wave 1 + wave 2 + wave 6; the lost wave 6 paid nothing')
        .toBe(STARTER_SHARDS + WAVE_1_REWARD + WAVE_2_REWARD + WAVE_6_REWARD)

      const closing = await roster()
      closingRosterSize = closing.length
      expect(closing.map((c) => c.creatureId), 'the child survived the campaign').toContain(childId)
      expect(closing.find((c) => c.creatureId === founderId)?.name).toBe(FOUNDER_NAME)
    },
    300_000,
  )

  /**
   * THE POINT OF THE FILE, as an assertion.
   *
   * Separate from the walk rather than folded into its tail, and for the same
   * reason `loop.test.ts` separates its own: this is not a step of the first
   * hour, it is the claim ABOUT the first hour. It should fail under its own
   * name, so a report says "a creature was seeded" rather than "the FTUE
   * broke".
   */
  it('accounts every creature as EARNED or SEEDED, and NONE of the seven was SEEDED', () => {
    expect(ledger.seeded,
      `SEEDED must be zero: every creature in the first hour arrived through a response. `
      + `A non-zero count means this file has started proving something weaker than it claims.`
      + ledger.detail).toBe(EXPECTED_SEEDED)
    expect(ledger.earned, `EARNED count moved.${ledger.detail}`).toBe(EXPECTED_EARNED)

    // The sum LAST, and never instead of the two above: booking everything to
    // one bucket satisfies the sum and is precisely the conflation the ledger
    // exists to prevent.
    expect(ledger.total).toBe(EXPECTED_EARNED + EXPECTED_SEEDED)
    expect(closingRosterSize, 'and GET /v1/roster agrees with the books').toBe(ledger.total)
  })

  /**
   * WHAT THIS DOES **NOT** CLOSE, pinned so it cannot be closed in prose while
   * the code stands still.
   *
   * `loop.test.ts`'s own last test states the unjoined half: the wave-6 and
   * wave-7 deployments there are seeded because earning five tier-III
   * counter-carriers is not reachable. This file joins the FIRST hour end to
   * end and says nothing about that. Both facts are true at once, and a
   * reader of either file should be able to find the other.
   */
  it('is the first hour, and not the whole supply line', () => {
    // READ OFF THE LEDGER, not off the constant. An earlier version of this
    // test closed with `expect(EXPECTED_EARNED).toBe(7)`, which compares a
    // literal to itself and cannot fail - in the very test whose job is to
    // stop a claim being closed in prose while the code stands still.
    // `ledger.earned` is a value the drive produced at runtime, so a content
    // change that grows the first hour reddens HERE and someone has to decide
    // whether the sentence below is still true.
    expect(ledger.earned,
      'seven creatures is the first hour, not the campaign - if this moved, the scope of the '
      + 'claim this file makes moved with it').toBe(7)

    // The three ids exist only if the drive above reached the beats that
    // produce them, so these are tripwires against a future edit that
    // REMOVES a beat while leaving the counts arithmetically satisfiable -
    // not independent evidence. Stated so nobody reads them as more.
    expect(childId, 'the guided splice IS fully earned, end to end').toBeDefined()
    expect(tutorialPair, 'its two parents came out of a route, not an insert').toBeDefined()
    expect(paleId, 'as did the Pale that turns wave 6 from a wall into a lesson').toBeDefined()
  })
})
