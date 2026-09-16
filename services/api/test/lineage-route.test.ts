import { randomUUID } from 'node:crypto'
import { mkdtemp, rm } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { afterAll, beforeAll, beforeEach, describe, expect, it } from 'vitest'
import { createApp, type Deps } from '../src/app.ts'
import { clearBundleCache } from '../src/config/bundle.ts'
import { publishBundle } from '../src/config/publish.ts'
import { LocalBundleStore } from '../src/config/store.ts'
import { withServer } from '../src/db/client.ts'
import { creatures, servers } from '../src/db/schema.ts'
import { LocalReplayStore } from '../src/replays/store.ts'
import { creatureHp } from '../src/roster/creatures.ts'
import { SimClient } from '../src/sim/client.ts'
import { issueAccessToken } from '../src/identity/jwt.ts'
import { startTestDb, type TestDb } from './harness.ts'
import { clearWave, setupPlayer } from './wave-helpers.ts'

/**
 * `GET /v1/lineage` - design 5 beat 8, `splice_confirm_spec` 5.
 *
 * WHAT THIS ROUTE EXISTS TO PASS. Every other roster reader in this service
 * is right to refuse a dead row - `GET /v1/roster` filters both kinds out,
 * and `toCreatureDto` throws on a pruned one outright. This is the one
 * screen where that is backwards: `splice_confirm_spec` 5 makes showing a
 * splice's two consumed parents, still whole, immediately after the splice
 * the FTUE's entire lesson - so these tests are guarding the OPPOSITE
 * failure from roster.test.ts's. A route that filtered dead rows the way
 * every sibling route correctly does would pass every other suite in this
 * package and fail the one thing this route is for.
 */

const SERVER_ID = 1
const TICK = { tickDayOfWeek: 0, tickMinuteOfDay: 1200 }

// fileURLToPath, not .pathname - this repo lives under a directory
// containing a space, which .pathname percent-encodes.
const REPO = fileURLToPath(new URL('../../../', import.meta.url))
const SEED = join(REPO, 'config/bundles/0.1.2')

/** Any past instant. What matters is that it is not NULL. */
const DEAD_AT = new Date('2026-09-01T00:00:00Z')

let t: TestDb
let deps: Deps
let app: ReturnType<typeof createApp>
let bundleRoot: string
let playerId: string
let token: string

beforeAll(async () => {
  t = await startTestDb()
  await t.ownerDb.insert(servers).values({
    serverId: SERVER_ID, region: 'us-central1', state: 'open', ...TICK,
  })

  bundleRoot = await mkdtemp(join(tmpdir(), 'broodline-lineage-'))
  const store = new LocalBundleStore(bundleRoot)
  await publishBundle(store, SEED, '0.1.2')
  await store.setPointer('0.1.2')
  clearBundleCache()

  // No route under test calls sim - node-claim.test.ts's and
  // ftue-stock.test.ts's idiom: the fixture below only needs
  // /v1/ftue/splice-stock and /v1/splice/commit, and neither calls sim.
  deps = {
    db: t.db,
    bundleStore: store,
    simClient: new SimClient('http://127.0.0.1:1', SimClient.noAuth('a deliberately dead address - no route here calls sim')),
    replayStore: new LocalReplayStore(bundleRoot),
  }
  app = createApp(deps)
}, 240_000)

afterAll(async () => {
  await t?.stop()
  if (bundleRoot) await rm(bundleRoot, { recursive: true, force: true })
})

beforeEach(async () => {
  const p = await setupPlayer(deps)
  playerId = p.playerId
  token = p.token
  app = createApp(deps)
})

// --- Route driver.

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
  trait1: string | null
  tier1: number | null
  trait2: string | null
  tier2: number | null
}

interface LineageBody { nodes: LineageNode[] }

async function lineageRes(auth = () => token): Promise<Response> {
  return app.request('/v1/lineage', { headers: { authorization: `Bearer ${auth()}` } })
}

async function lineage(auth = () => token): Promise<LineageBody> {
  const res = await lineageRes(auth)
  expect(res.status).toBe(200)
  return res.json() as Promise<LineageBody>
}

// --- Fixtures.

/**
 * A live Founder, minted directly through the owner connection.
 *
 * SEEDED, NOT EARNED - and that is a real gap this test documents rather than
 * papers over. The Founder is only ever granted by `grantWaveBaseStock` on a
 * REAL wave-1 completion (Task 6), which needs a real sim host on its own
 * port (founder.test.ts's and loop.test.ts's `startSim()`). Standing up a
 * fifth sim-hosting file for this route's own test would need a new entry in
 * `preflight.ts`'s `SIM_PORTS` and a matching change to that list's pinned
 * assertion in preflight.test.ts - infrastructure this task's brief does not
 * ask for and this route's own logic does not need exercised: what this test
 * is verifying is the PROJECTION (does `isFounder` pass through the join
 * correctly), not the GRANT, which founder.test.ts already covers end to
 * end. `roster.test.ts` makes the identical call for the identical reason.
 */
async function giveFounder(): Promise<string> {
  const [row] = await withServer(deps.db, SERVER_ID, (tx) => tx.insert(creatures).values({
    serverId: SERVER_ID, playerId, species: 'Hollow', generation: 1,
    trait1: 'None', tier1: null, trait2: 'None', tier2: null, instinct: 'Vanguard',
    hpCurrent: creatureHp('Hollow'), isFounder: true, name: 'Ash',
  }).returning())
  return row!.creatureId
}

/**
 * A born-pruned tombstone, minted directly - 0005_loop.sql's own comment
 * names this the one way to manufacture one without first building and
 * pruning a six-deep line: "not reachable from any request path... and is
 * affirmatively WANTED" for exactly this purpose. `roster.test.ts` and
 * `splice-commit.test.ts` mint the identical row the identical way.
 *
 * EARNING ONE IS NOT POSSIBLE ON THIS BUILD AT ALL, not merely inconvenient:
 * `roster/lineage.ts`'s `RETAINED_DEPTH` only prunes an ancestor SIX
 * generations back, and `splice/commit.ts`'s `maxGeneration` caps every
 * splice at Gen-4 for the Splicing Chamber's default (and, this phase, only)
 * tier. A six-deep line needs a chamber upgrade path Phase 6 does not ship
 * ("Phase 6 pins every Ark at tier 1 with no upgrade path" - 0005's own
 * header), so a real prune cannot be produced through any route that exists
 * today. Seeding is not a shortcut here; it is the only way to exercise this
 * branch before that content lands.
 */
async function givePruned(): Promise<string> {
  const [row] = await withServer(deps.db, SERVER_ID, (tx) => tx.insert(creatures).values({
    serverId: SERVER_ID, playerId, species: 'Vetch', generation: 1,
    pruned: true, consumedAt: DEAD_AT,
  }).returning())
  return row!.creatureId
}

/** `POST /v1/ftue/splice-stock`, once wave 2 is cleared - ftue-stock.test.ts's idiom. */
async function stock(): Promise<{ vetch: string; ember: string }> {
  await clearWave(2)
  const res = await app.request('/v1/ftue/splice-stock', {
    method: 'POST',
    headers: { authorization: `Bearer ${token}`, 'idempotency-key': randomUUID() },
  })
  expect(res.status).toBe(200)
  const body = await res.json() as { creatures: Array<{ creatureId: string; species: string }> }
  const vetch = body.creatures.find((c) => c.species === 'Vetch')!.creatureId
  const ember = body.creatures.find((c) => c.species === 'Ember')!.creatureId
  return { vetch, ember }
}

/**
 * Splices the tutorial pair - this player's FIRST splice, so Task 7's
 * `guaranteedMutation` makes the roll deterministic rather than a 9%
 * coin-flip. Returns the child's id.
 */
async function spliceTutorialPair(vetch: string, ember: string): Promise<string> {
  const res = await app.request('/v1/splice/commit', {
    method: 'POST',
    headers: {
      'content-type': 'application/json', authorization: `Bearer ${token}`,
      'idempotency-key': randomUUID(),
    },
    body: JSON.stringify({
      parentA: vetch, parentB: ember, locked: { from: 'a', slot: 'trait_1' }, bodyFrom: 'Vetch',
    }),
  })
  expect(res.status).toBe(200)
  const body = await res.json() as { child: { creatureId: string } }
  return body.child.creatureId
}

describe('GET /v1/lineage', () => {
  it('returns live, consumed and pruned rows for the player, with parents and the mutation flag', async () => {
    await giveFounder()
    const { vetch, ember } = await stock()
    await spliceTutorialPair(vetch, ember)

    const body = await lineage()

    const child = body.nodes.find((n) => n.generation === 2)!
    expect(child, `expected a Gen-2 child among ${JSON.stringify(body.nodes)}`).toBeDefined()
    expect(child.mutated).toBe(true)

    const parents = body.nodes.filter((n) => n.creatureId === child.parentA || n.creatureId === child.parentB)
    expect(parents.length).toBe(2)
    // Dead but WHOLE - splice_confirm_spec 5's lesson, and the property
    // toCreatureDto would refuse to represent: a consumed row still carries
    // its traits.
    expect(parents.every((p) => p.consumedAt !== null && p.trait1 !== null)).toBe(true)
    expect(parents.map((p) => p.species).sort()).toEqual(['Ember', 'Vetch'])

    expect(body.nodes.filter((n) => n.isFounder).length).toBe(1)

    // Every row the fixture minted, and nothing else: the Founder (live),
    // the two consumed parents, the Gen-2 child. Not asserted by the brief's
    // own snippet, but it is what turns "the parents are among the nodes"
    // into "the nodes are exactly what this player's tree really holds".
    expect(body.nodes.length).toBe(4)
  })

  it('returns a pruned tombstone stripped rather than throwing or filtering it away', async () => {
    // THE TRAP THIS ROUTE EXISTS AROUND, verified directly: `toCreatureDto`
    // throws on exactly this row, and this route must not reach for it.
    const tombstone = await givePruned()

    const body = await lineage()
    const found = body.nodes.find((n) => n.creatureId === tombstone)

    expect(found, 'a pruned row must round-trip, not be filtered out').toBeDefined()
    expect(found).toMatchObject({
      species: 'Vetch', generation: 1, isFounder: false, pruned: true, mutated: false,
    })
    expect(found!.trait1).toBeNull()
    expect(found!.tier1).toBeNull()
    expect(found!.trait2).toBeNull()
    expect(found!.tier2).toBeNull()
    expect(found!.name).toBeNull()
    expect(found!.consumedAt).not.toBeNull()
  })

  it('answers the same tree on a second call - derived and writes nothing', async () => {
    await giveFounder()
    await givePruned()

    const first = await lineage()
    const second = await lineage()
    expect(second).toEqual(first)
  })

  it('never returns another player\'s rows', async () => {
    await giveFounder()
    const mine = await lineage()
    expect(mine.nodes.length).toBe(1)
    const mineIds = new Set(mine.nodes.map((n) => n.creatureId))

    // A second player on the same server, with their own tree.
    const other = await setupPlayer(deps)
    app = createApp(deps)
    playerId = other.playerId
    await giveFounder()
    await givePruned()

    const theirs = await lineage(() => other.token)
    expect(theirs.nodes.length).toBe(2)
    for (const n of theirs.nodes) expect(mineIds.has(n.creatureId)).toBe(false)
  })

  it('refuses an unauthenticated read', async () => {
    const res = await app.request('/v1/lineage')
    expect(res.status).toBe(401)
  })

  it('404s an account with no player on this server', async () => {
    // Minted rather than manufactured by deleting a real player row -
    // roster.test.ts's reasoning: `players` is pointed at by other tables,
    // so deleting one to reach this branch would test the FK graph instead
    // of the route.
    const orphan = await issueAccessToken({ accountId: randomUUID(), serverId: SERVER_ID })

    const res = await lineageRes(() => orphan)
    expect(res.status).toBe(404)
    expect((await res.json() as { code: string }).code).toBe('not_found')
  })
})
