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
import { SimClient } from '../src/sim/client.ts'
import { issueAccessToken } from '../src/identity/jwt.ts'
import { startTestDb, type TestDb } from './harness.ts'
import { setupPlayer } from './wave-helpers.ts'

/**
 * `GET /v1/roster` - Phase 6, Task 12, fix round 1.
 *
 * WHAT THIS ROUTE IS FOR. The phase's done-when is that harvest -> splice ->
 * fight -> reward closes without leaving the app. Every beat of that needs the
 * player to SEE and CHOOSE creatures, and before this route the only creature
 * lists a client ever held were incidental: what `node/claim` had just granted
 * and the child `splice/commit` had just made. A relaunch lost every id, so
 * the loop closed only inside a single session.
 *
 * WHAT THESE TESTS ARE ACTUALLY GUARDING. Not "the route returns rows" - the
 * LIVENESS PREDICATE. `creatures` carries two kinds of dead row and this is
 * the route where confusing either with a live one is most expensive:
 *
 *  - A CONSUMED parent is not pruned and is WHOLE, so it renders perfectly.
 *    Listing one offers the player a creature that no longer exists as a
 *    splice parent or a deployment slot.
 *  - A PRUNED tombstone has `committed_to` NULLED by the prune, so the
 *    tempting "available means uncommitted" predicate matches every one of
 *    them - and `toCreatureDto` THROWS on a pruned row, so that mistake is a
 *    500 on exactly the players whose lines are deepest.
 *
 * Both are asserted against a roster holding one of each, so a predicate that
 * drops either half reddens rather than merely getting lucky.
 */

const SERVER_ID = 1
const TICK = { tickDayOfWeek: 0, tickMinuteOfDay: 1200 }

const REPO = fileURLToPath(new URL('../../../', import.meta.url))
const SEED = join(REPO, 'config/bundles/0.1.2')

/** bible 7.2's floor, for a tier-1 Hatchery - roster/creatures.ts's table. */
const CAP = 20

/** Any past instant. What matters is that it is not NULL. */
const DEAD_AT = new Date('2026-09-01T00:00:00Z')

/**
 * A whole, renderable creature. Every field `live_creatures_are_whole`
 * requires, so the only thing that varies between the fixtures below is which
 * kind of dead a row is.
 */
const LIVE_CREATURE = {
  species: 'Vetch Crawler',
  generation: 1,
  trait1: 'Chill', tier1: 2,
  trait2: 'Carapace', tier2: 1,
  instinct: 'Forage',
  hpCurrent: 100,
  isFounder: false,
}

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

  bundleRoot = await mkdtemp(join(tmpdir(), 'broodline-roster-'))
  const store = new LocalBundleStore(bundleRoot)
  await publishBundle(store, SEED, '0.1.2')
  await store.setPointer('0.1.2')
  clearBundleCache()

  // No route under test calls sim or submits a replay, so the sim address
  // points nowhere reachable and the replay store is only asked to exist -
  // node-claim.test.ts's idiom.
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
  // Guarded: bundleRoot is assigned partway through beforeAll, so an aborted
  // beforeAll would otherwise throw on top of the real error and bury it -
  // see masked-teardown.test.ts.
  if (bundleRoot) await rm(bundleRoot, { recursive: true, force: true })
})

beforeEach(async () => {
  const p = await setupPlayer(deps)
  playerId = p.playerId
  token = p.token
  app = createApp(deps)
})

// --- Route driver.

async function roster(auth = () => token): Promise<Response> {
  return app.request('/v1/roster', { headers: { authorization: `Bearer ${auth()}` } })
}

interface RosterBody {
  cap: number
  creatures: Array<{
    creatureId: string; species: string; generation: number
    trait1: string; tier1: number | null
    trait2: string; tier2: number | null
    instinct: string; name: string | null
    isFounder: boolean; committedTo: string | null
  }>
}

// --- Fixtures.

/**
 * One creature, in whichever state the options name.
 *
 * A born-pruned row is what 0005_loop.sql allows explicitly and is the only
 * way to manufacture a dead ancestor without first building a lineage to
 * prune - node-claim.test.ts makes the same one the same way.
 * `pruned_creatures_are_consumed` refuses a tombstone that claims never to
 * have died, so `consumedAt` is part of the skeleton.
 */
async function give(o: {
  pruned?: boolean; consumed?: boolean; committed?: boolean
  isFounder?: boolean; name?: string; tier1?: number | null
} = {}): Promise<string> {
  const [row] = await withServer(deps.db, SERVER_ID, (tx) => tx.insert(creatures).values(
    o.pruned === true
      ? {
        serverId: SERVER_ID, playerId, species: 'Vetch Crawler', generation: 1,
        pruned: true, consumedAt: DEAD_AT,
      }
      : {
        serverId: SERVER_ID, playerId, ...LIVE_CREATURE,
        tier1: o.tier1 === undefined ? LIVE_CREATURE.tier1 : o.tier1,
        consumedAt: o.consumed === true ? DEAD_AT : null,
        committedTo: o.committed === true ? randomUUID() : null,
        isFounder: o.isFounder === true,
        name: o.name ?? null,
      },
  ).returning())
  return row!.creatureId
}

describe('GET /v1/roster', () => {
  it('answers a fresh player with an empty roster and a cap, not a 404', async () => {
    // The one launch where the player most needs to be told to go and
    // harvest something is the one where a 404 would make the roster screen
    // indistinguishable from a broken session.
    const res = await roster()
    expect(res.status).toBe(200)

    const body = await res.json() as RosterBody
    expect(body.creatures).toEqual([])
    expect(body.cap).toBe(CAP)
  })

  it('returns the live creatures this player holds, whole', async () => {
    const id = await give({ isFounder: true, name: 'Ash' })

    const res = await roster()
    expect(res.status).toBe(200)

    const body = await res.json() as RosterBody
    expect(body.creatures).toHaveLength(1)
    // Every field the three screens need: the Splice Chamber's names and
    // tiers, the deploy screen's committedTo, the Founder interrupt's
    // isFounder and name.
    expect(body.creatures[0]).toEqual({
      creatureId: id,
      species: 'Vetch Crawler',
      generation: 1,
      trait1: 'Chill', tier1: 2,
      trait2: 'Carapace', tier2: 1,
      instinct: 'Forage',
      name: 'Ash',
      isFounder: true,
      committedTo: null,
    })
  })

  it('omits a pruned row and a consumed row, and keeps the live one', async () => {
    // THE TEST THIS ROUTE EXISTS TO PASS. Both kinds of dead row are present
    // at once, so a predicate that keeps either half is red rather than
    // lucky - and a hand-rolled `committed_to IS NULL` would return all
    // three, with the pruned one throwing inside toCreatureDto.
    const live = await give()
    await give({ pruned: true })
    await give({ consumed: true })

    const res = await roster()
    expect(res.status).toBe(200)

    const body = await res.json() as RosterBody
    expect(body.creatures.map((c) => c.creatureId)).toEqual([live])
  })

  it('lists a committed creature, and marks it', async () => {
    // A creature out fighting is LIVE - it occupies a Hatchery slot and it
    // comes back (roster/creatures.ts). Excluding it would let a player hold
    // more than the cap by deploying some of them. It is listed and MARKED so
    // the Splice Chamber can warn and the deployment screen can grey it out.
    const committed = await give({ committed: true })

    const body = await (await roster()).json() as RosterBody
    const found = body.creatures.find((c) => c.creatureId === committed)

    expect(found).toBeDefined()
    expect(found!.committedTo).not.toBeNull()
  })

  it('carries a null tier through as null, never as zero', async () => {
    // data_model 2: null is an ABERRANT, which has no coverage, and zero
    // would sort and display as "less than tier I". 0005's
    // coverage_tier_N_not_zero is the same rule in storage; this is the rule
    // surviving the wire.
    await give({ tier1: null })

    const body = await (await roster()).json() as RosterBody
    expect(body.creatures[0]!.tier1).toBeNull()
    expect(body.creatures[0]!.tier1).not.toBe(0)
  })

  it('returns the same list, in the same order, on a second call', async () => {
    // Derived and writes nothing, like GET /v1/region/state. The ORDER is
    // part of that: a client diffing against its cache should see a creature
    // appear or disappear, never the same set rearranged.
    await give()
    await give()
    await give()

    const first = await (await roster()).json() as RosterBody
    const second = await (await roster()).json() as RosterBody

    expect(second.creatures).toEqual(first.creatures)
    expect(first.creatures).toHaveLength(3)
  })

  it('never shows one player another player\'s roster', async () => {
    await give()
    const mine = await (await roster()).json() as RosterBody
    expect(mine.creatures).toHaveLength(1)

    // A second player on the same server, with their own creature.
    const other = await setupPlayer(deps)
    app = createApp(deps)
    playerId = other.playerId
    await give()

    const theirs = await (await roster(() => other.token)).json() as RosterBody
    expect(theirs.creatures).toHaveLength(1)
    expect(theirs.creatures[0]!.creatureId).not.toBe(mine.creatures[0]!.creatureId)
  })

  it('refuses an unauthenticated read', async () => {
    const res = await app.request('/v1/roster')
    expect(res.status).toBe(401)
  })

  it('404s an account with no player on this server', async () => {
    // A VALID SESSION FOR AN ACCOUNT THAT HAS NO PLAYER ROW, minted rather
    // than manufactured by deleting one: `players` is pointed at by
    // `wallets`, `arks` and `harvest_positions`, so deleting a player to
    // reach this branch tests the FK graph instead of the route. The same
    // sentence region.ts and splice.ts use for the same condition.
    const orphan = await issueAccessToken({ accountId: randomUUID(), serverId: SERVER_ID })

    const res = await roster(() => orphan)
    expect(res.status).toBe(404)
    expect((await res.json() as { code: string }).code).toBe('not_found')
  })
})
