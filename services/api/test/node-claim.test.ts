import { randomUUID } from 'node:crypto'
import { readFileSync } from 'node:fs'
import { mkdtemp, rm } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { and, eq, sql } from 'drizzle-orm'
import { afterAll, beforeAll, beforeEach, describe, expect, it } from 'vitest'
import { createApp, type Deps } from '../src/app.ts'
import { type Bundle, clearBundleCache, loadBundle } from '../src/config/bundle.ts'
import { publishBundle } from '../src/config/publish.ts'
import { LocalBundleStore } from '../src/config/store.ts'
import { withServer } from '../src/db/client.ts'
import {
  creatures, harvestPositions, ledger, nodeDepletion, servers, wallets,
} from '../src/db/schema.ts'
import { claimNode, THE_REGION } from '../src/map/claim.ts'
import { epochFor } from '../src/map/rotation.ts'
import { LocalReplayStore } from '../src/replays/store.ts'
import { SimClient } from '../src/sim/client.ts'
import { startTestDb, type TestDb } from './harness.ts'
import { baseStockSpecies } from '../src/roster/creatures.ts'
import { setupPlayer } from './wave-helpers.ts'

const SERVER_ID = 1
const TICK = { tickDayOfWeek: 0, tickMinuteOfDay: 1200 }

const HOUR = 3_600_000

// fileURLToPath, not .pathname - this repo lives under a directory
// containing a space, which .pathname percent-encodes.
const REPO = fileURLToPath(new URL('../../../', import.meta.url))
// 0.1.2, not 0.1.1: it is the first bundle carrying nodes.json, and
// `nodesFor` has nothing to build a region out of without it.
const SEED = join(REPO, 'config/bundles/0.1.2')

/**
 * The real authored node table, not numbers retyped here - the same thing
 * rotation.test.ts does, and for the same reason: a test that hardcodes the
 * rate cannot tell a code regression from a content change.
 */
const AUTHORED = JSON.parse(readFileSync(join(SEED, 'nodes.json'), 'utf8')) as
  Array<{ id: string; ratePerHour: number; totalYield: number | null }>
const COMMON = AUTHORED.find((n) => n.id === 'common_vein')!
const RICH = AUTHORED.find((n) => n.id === 'rich_deposit')!
const RICH_TOTAL = RICH.totalYield!

/**
 * What `POST /v1/account` has already paid this player before any of these
 * tests runs - `config/bundles/0.1.2/starter.json`, credited by
 * routes/account.ts through credit(), one ledger row each.
 *
 * Pinned as absolute values and then ADDED to, rather than each test
 * measuring a delta from whatever it happened to find: a uniform offset -
 * every claim paying one shard too many, say - is exactly what a
 * delta-only assertion cannot see, and the starter grant is the offset most
 * likely to drift underneath these tests.
 */
const STARTER_SHARDS = 250
const STARTER_LEDGER_ROWS = 2

/**
 * One Common Vein shard, in milliseconds: 3,600,000 / 20. Also exactly
 * three Rich Deposit shards, so aligning to it aligns to both.
 *
 * `accrue` is a difference of floors against ABSOLUTE timestamps (see that
 * function), so a claim `n` hours after a position was settled pays a fixed
 * number of shards only when the two timestamps sit the same distance into
 * their respective shard ticks. Align `lastSettledAt` to a tick boundary
 * and the count is exact for any request that completes inside the tick;
 * do not, and a claim expecting 120 gets 121 whenever the request happens
 * to cross a boundary - a flake at roughly one run in a thousand that would
 * read as a rounding bug rather than as a test that pinned a clock.
 */
const SHARD_TICK_MS = HOUR / COMMON.ratePerHour

let t: TestDb
let deps: Deps
let app: ReturnType<typeof createApp>
let bundle: Bundle
let bundleRoot: string
let playerId: string
let token: string

beforeAll(async () => {
  t = await startTestDb()
  await t.ownerDb.insert(servers).values({
    serverId: SERVER_ID, region: 'us-central1', state: 'open', ...TICK,
  })

  bundleRoot = await mkdtemp(join(tmpdir(), 'broodline-node-claim-'))
  const store = new LocalBundleStore(bundleRoot)
  await publishBundle(store, SEED, '0.1.2')
  await store.setPointer('0.1.2')
  clearBundleCache()

  // No route under test calls sim, and none submits a replay - so the sim
  // address points nowhere reachable rather than standing up a real host,
  // and the replay store is only ever asked to exist. Same idiom as
  // wave-start.test.ts.
  deps = {
    db: t.db,
    bundleStore: store,
    simClient: new SimClient('http://127.0.0.1:1', SimClient.noAuth('a deliberately dead address - no route here calls sim')),
    replayStore: new LocalReplayStore(bundleRoot),
  }
  app = createApp(deps)
  bundle = await loadBundle(store)
}, 240_000)

afterAll(async () => {
  await t?.stop()
  // Guarded: bundleRoot is assigned partway through beforeAll, so an
  // aborted beforeAll would otherwise throw ERR_INVALID_ARG_TYPE on top of
  // the real error and bury it - see masked-teardown.test.ts.
  if (bundleRoot) await rm(bundleRoot, { recursive: true, force: true })
})

/**
 * A FRESH PLAYER PER TEST, on purpose. Several of these assert an absolute
 * balance or an absolute ledger row count, and a player carried between
 * tests would make every one of those depend on the order the file happens
 * to run in - which is how an assertion quietly stops meaning what it says.
 */
beforeEach(async () => {
  ({ playerId, token } = await setupPlayer(deps))
})

// --- Clock helpers.

/** The epoch the route will compute for this server, right now. */
function epochNow(): number {
  return Number(epochFor(TICK, new Date()))
}

/**
 * `h` hours before now, snapped DOWN to a shard-tick boundary - see
 * SHARD_TICK_MS for why the snapping is load-bearing rather than tidy.
 *
 * At h >= 12 the twelve-hour cap makes the window a fixed width measured
 * back from the REQUEST's clock, so the result is exact with or without
 * this; below twelve hours it is the difference between an exact assertion
 * and an occasional one.
 */
function hoursAgo(h: number): Date {
  return new Date(Math.floor(Date.now() / SHARD_TICK_MS) * SHARD_TICK_MS - h * HOUR)
}

// --- Route drivers.

async function regionState(): Promise<Response> {
  return app.request('/v1/region/state', { headers: { authorization: `Bearer ${token}` } })
}

async function claimWithKey(slot: number, key: string): Promise<Response> {
  return app.request('/v1/node/claim', {
    method: 'POST',
    headers: {
      'content-type': 'application/json',
      authorization: `Bearer ${token}`,
      'idempotency-key': key,
    },
    body: JSON.stringify({ slot }),
  })
}

const claim = (slot: number) => claimWithKey(slot, randomUUID())

// --- State readers and fixtures.

async function balance(who = () => playerId): Promise<number> {
  const rows = await withServer(deps.db, SERVER_ID, (tx) => tx.select().from(wallets)
    .where(and(eq(wallets.playerId, who()), eq(wallets.currency, 'shards'))))
  return rows[0]?.balance ?? 0
}

async function ledgerRowCount(who = () => playerId): Promise<number> {
  const rows = await withServer(deps.db, SERVER_ID, (tx) => tx.select().from(ledger)
    .where(eq(ledger.playerId, who())))
  return rows.length
}

async function rosterCount(who = () => playerId): Promise<number> {
  const rows = await withServer(deps.db, SERVER_ID, (tx) => tx.select().from(creatures)
    .where(and(eq(creatures.playerId, who()), sql`NOT ${creatures.pruned}`)))
  return rows.length
}

async function positionRow(slot: number, who = () => playerId) {
  const [row] = await withServer(deps.db, SERVER_ID, (tx) => tx.select().from(harvestPositions)
    .where(and(
      eq(harvestPositions.playerId, who()),
      eq(harvestPositions.nodeSlot, slot),
      eq(harvestPositions.epoch, epochNow()),
    )))
  return row
}

async function setLastSettled(slot: number, at: Date, who = () => playerId): Promise<void> {
  await withServer(deps.db, SERVER_ID, (tx) => tx.insert(harvestPositions)
    .values({
      serverId: SERVER_ID, playerId: who(), regionId: THE_REGION,
      nodeSlot: slot, epoch: epochNow(), lastSettledAt: at,
    })
    .onConflictDoUpdate({
      target: [harvestPositions.serverId, harvestPositions.playerId, harvestPositions.regionId,
        harvestPositions.nodeSlot, harvestPositions.epoch],
      set: { lastSettledAt: at },
    }))
}

async function depletionRow(slot: number) {
  const [row] = await withServer(deps.db, SERVER_ID, (tx) => tx.select().from(nodeDepletion)
    .where(and(eq(nodeDepletion.nodeSlot, slot), eq(nodeDepletion.epoch, epochNow()))))
  return row
}

/** Takes `RICH_TOTAL - leave` out of a node, so `leave` units are left in it. */
async function harvestDownTo(slot: number, leave: number): Promise<void> {
  const harvested = RICH_TOTAL - leave
  await withServer(deps.db, SERVER_ID, (tx) => tx.insert(nodeDepletion)
    .values({
      serverId: SERVER_ID, regionId: THE_REGION, nodeSlot: slot,
      epoch: epochNow(), harvestedUnits: harvested,
    })
    .onConflictDoUpdate({
      target: [nodeDepletion.serverId, nodeDepletion.regionId, nodeDepletion.nodeSlot, nodeDepletion.epoch],
      set: { harvestedUnits: harvested },
    }))
}

const LIVE_CREATURE = {
  species: 'Vetch', generation: 1,
  trait1: 'Taunt', tier1: 1, trait2: 'Carapace', tier2: 1,
  instinct: 'Vanguard', hpCurrent: 260,
}

async function addCreatures(
  n: number, o: { pruned?: boolean; committed?: boolean } = {}, who = () => playerId,
): Promise<void> {
  if (n === 0) return
  await withServer(deps.db, SERVER_ID, (tx) => tx.insert(creatures).values(
    Array.from({ length: n }, () => (o.pruned === true
      // A born-pruned row - 0005_loop.sql allows it explicitly, and it is
      // the only way to manufacture a dead ancestor without first building
      // a lineage to prune.
      ? { serverId: SERVER_ID, playerId: who(), species: 'Vetch', generation: 1, pruned: true }
      : {
        serverId: SERVER_ID, playerId: who(), ...LIVE_CREATURE,
        committedTo: o.committed === true ? randomUUID() : null,
      })),
  ))
}

/**
 * Removes every `node_depletion` row for the current epoch, through the
 * OWNER connection.
 *
 * `node_depletion` is SERVER-scoped, not player-scoped, so `beforeEach`'s
 * fresh player does not reset it - rows written by an earlier test in this
 * file are still there. A test that asserts a row is ABSENT has to say so
 * itself rather than depend on running first.
 */
async function clearDepletion(): Promise<void> {
  await t.ownerDb.delete(nodeDepletion).where(eq(nodeDepletion.epoch, epochNow()))
}

/**
 * Deletes every creature this player holds, through the OWNER connection.
 *
 * The app role has no DELETE on `creatures` at all - 0005_loop.sql withdrew
 * it deliberately, because the prune is an UPDATE and a handler that could
 * delete a creature could break a living descendant's lineage. This is a
 * fixture reset, not a thing any handler does.
 */
async function clearRoster(): Promise<void> {
  await t.ownerDb.delete(creatures).where(eq(creatures.playerId, playerId))
}

describe('GET /v1/region/state', () => {
  it('reports accrual without writing anything', async () => {
    // The read path is derived - design 4.1. Reading it must not settle the
    // position, or a player who opened the region screen twice would find
    // the second visit showing zero, having been paid nothing for what the
    // first visit consumed.
    await clearDepletion()
    await setLastSettled(0, hoursAgo(6))
    const before = await positionRow(0)

    const res = await regionState()
    expect(res.status).toBe(200)
    const body = await res.json() as {
      regionId: string; epoch: number
      roster: { count: number; cap: number }
      nodes: Array<{
        slot: number; type: string; accrued: number
        remaining: number | null; grants: number
      }>
    }

    const common = body.nodes.find((n) => n.slot === 0)!
    expect(common.type).toBe('common_vein')
    expect(common.accrued).toBe(6 * COMMON.ratePerHour)   // 120
    // bible 5.3: the Common Vein never depletes, which is a different
    // statement from "it has a very large amount left".
    expect(common.remaining).toBeNull()
    expect(body.nodes.find((n) => n.slot === 1)!.remaining).toBe(RICH_TOTAL)
    expect(body.regionId).toBe(THE_REGION)

    // What the client needs to predict a roster_full 409 rather than meet
    // one. bible 7.2's floor of 20, against a roster this player has not
    // been granted anything into yet.
    expect(body.roster).toEqual({ count: 0, cap: 20 })
    // Six hours of Common Vein is 120 units against 480 to the creature, so
    // this particular read owes nothing yet - the assertion that the field
    // MOVES is on the claim path, where a grant actually happens.
    expect(common.grants).toBe(0)

    expect((await positionRow(0))!.lastSettledAt).toEqual(before!.lastSettledAt)
    expect(await ledgerRowCount()).toBe(STARTER_LEDGER_ROWS)
    expect(await balance()).toBe(STARTER_SHARDS)

    // AND NO node_depletion ROW, for either node. `readDepletion` and
    // `lockDepletion` have identical signatures in the same file, so
    // swapping one word for the other here is a one-character-class typo
    // that would make opening the region screen INSERT a row and take the
    // hottest shared lock on the server - every screen-open, for every
    // player. Asserting the position and the ledger cannot see that; only
    // this can.
    expect(await depletionRow(0)).toBeUndefined()
    expect(await depletionRow(1)).toBeUndefined()
  })

  it('reports the same accrual twice in a row', async () => {
    // The consequence of the above, asserted as the player experiences it
    // rather than as a row comparison. A read that settled would make the
    // second of these zero.
    await setLastSettled(0, hoursAgo(6))
    const first = await (await regionState()).json() as { nodes: Array<{ slot: number; accrued: number }> }
    const second = await (await regionState()).json() as { nodes: Array<{ slot: number; accrued: number }> }

    expect(first.nodes.find((n) => n.slot === 0)!.accrued).toBe(120)
    expect(second.nodes.find((n) => n.slot === 0)!.accrued).toBe(120)
  })
})

describe('POST /v1/node/claim', () => {
  it('credits shards and writes exactly one ledger row', async () => {
    const settledAt = hoursAgo(6)
    await setLastSettled(0, settledAt)
    const res = await claim(0)

    expect(res.status).toBe(200)
    const body = await res.json() as { slot: number; shards: number; balance: number }
    expect(body.shards).toBe(120)                                   // 6h x 20
    expect(body.balance).toBe(STARTER_SHARDS + 120)
    expect(await balance()).toBe(STARTER_SHARDS + 120)
    expect(await ledgerRowCount()).toBe(STARTER_LEDGER_ROWS + 1)

    // design 4.3's third and fourth writes, in the same transaction.
    expect((await positionRow(0))!.lastSettledAt.getTime()).toBeGreaterThan(settledAt.getTime())
    expect((await depletionRow(0))!.harvestedUnits).toBe(120)
  })

  it('pays nothing on an immediate second claim', async () => {
    await setLastSettled(0, hoursAgo(6))
    await claim(0)
    const second = await claim(0)

    // NOT an error - accrual since the first claim is genuinely zero. The
    // assertion is on the BALANCE, because a route that rejected everything
    // would pass an error-code assertion while being completely wrong.
    expect(second.status).toBe(200)
    expect((await second.json() as { shards: number }).shards).toBe(0)
    expect(await balance()).toBe(STARTER_SHARDS + 120)
    // And no SECOND ledger row: the ledger records mutations, and a claim
    // that paid nothing is not one.
    expect(await ledgerRowCount()).toBe(STARTER_LEDGER_ROWS + 1)
  })

  it('is idempotent under a retried key', async () => {
    await setLastSettled(0, hoursAgo(6))
    const key = randomUUID()
    const first = await claimWithKey(0, key)
    const second = await claimWithKey(0, key)

    expect(first.status).toBe(200)
    expect(second.status).toBe(200)
    // The SAME response, not merely a second successful one - that is what
    // the stored body is for.
    expect(await second.json()).toEqual(await first.json())
    expect(await balance()).toBe(STARTER_SHARDS + 120)
    expect(await ledgerRowCount()).toBe(STARTER_LEDGER_ROWS + 1)
  })

  it('refuses a grant that would exceed the Hatchery cap', async () => {
    // design 4.3: refused before anything in the transaction is written,
    // never truncated inside it. A partial grant that silently drops
    // creatures is a loss the player reports as theft.
    await addCreatures(20)
    const settledAt = hoursAgo(12)
    await setLastSettled(1, settledAt)

    const res = await claim(1)
    expect(res.status).toBe(409)
    expect(await res.json()).toMatchObject({ code: 'roster_full' })

    // NOTHING credited either, and nothing settled - so the accrual is
    // still there when a slot is freed.
    expect(await balance()).toBe(STARTER_SHARDS)
    expect(await ledgerRowCount()).toBe(STARTER_LEDGER_ROWS)
    expect(await rosterCount()).toBe(20)
    expect((await positionRow(1))!.lastSettledAt).toEqual(settledAt)
  })

  it('does not count pruned ancestors against the Hatchery cap', async () => {
    // THE TRAP 0005_loop.sql warns about, as a behaviour rather than as a
    // comment. A pruned creature keeps its row and has `committed_to`
    // NULLED, so `committed_to IS NULL` is TRUE of every dead ancestor: a
    // cap query written as "available means uncommitted" counts all twenty
    // rows below and refuses a player holding five live creatures.
    await addCreatures(15, { pruned: true })
    await addCreatures(5)
    expect(await rosterCount()).toBe(5)

    await setLastSettled(1, hoursAgo(12))
    const res = await claim(1)

    expect(res.status).toBe(200)
    const body = await res.json() as { shards: number; creatures: unknown[] }
    expect(body.shards).toBe(12 * RICH.ratePerHour)     // 720
    expect(body.creatures.length).toBeGreaterThan(0)
    expect(await rosterCount()).toBe(5 + body.creatures.length)
  })

  it('counts a committed creature against the Hatchery cap', async () => {
    // The other half, and the one that looks like the bug. A creature out
    // fighting still occupies a Hatchery slot - it comes back - so
    // excluding it would let a player hold more than the cap simply by
    // deploying some of them.
    await addCreatures(20, { committed: true })
    await setLastSettled(1, hoursAgo(12))

    const res = await claim(1)
    expect(res.status).toBe(409)
    expect(await res.json()).toMatchObject({ code: 'roster_full' })
    expect(await balance()).toBe(STARTER_SHARDS)
  })

  it('grants Gen-1 base stock whose whole shape the schema accepts', async () => {
    await setLastSettled(1, hoursAgo(12))
    const body = await (await claim(1)).json() as {
      creatures: Array<{
        species: string; generation: number; trait1: string; tier1: number | null
        trait2: string; tier2: number | null; instinct: string; isFounder: boolean
        name: string | null; committedTo: string | null
      }>
    }

    expect(body.creatures.length).toBeGreaterThan(0)
    const c = body.creatures[0]!
    expect(c).toMatchObject({
      generation: 1, tier1: 1, tier2: 1,
      instinct: 'Vanguard', isFounder: false, name: null, committedTo: null,
    })
    // The species and its traits must agree - a grant assembled from one
    // species' body and another's traits is a creature no bundle authors.
    expect(baseStockSpecies.map((v) => v.species)).toContain(c.species)
    const template = baseStockSpecies.find((v) => v.species === c.species)!
    expect(c.trait1).toBe(template.trait1)
    expect(c.trait2).toBe(template.trait2)
  })

  it('grants more than one species across a roster', async () => {
    // design 5.2 lets a splice take the child's body from EITHER parent's
    // species. That choice has no reachable input if every creature a
    // player can obtain is the same species - and this function is the only
    // insert(creatures) in src/, with starter.json granting currency only.
    // So this is not a test about variety for its own sake; it is what
    // makes Task 7's body choice reachable at all.
    // THE NODE MUST START FULL. Slot 1 enters this test carrying whatever
    // earlier tests harvested (measured: 1,440 of 8,640), and each iteration
    // takes 720 - so without this only ten of the twelve iterations below
    // have any yield to grant from, and a run of identical rolls would fail
    // as `expected 1 to be >= 2`, blaming the species roll for depletion.
    // That is a ~1-in-20,000 flake that points at the wrong cause, which is
    // worse than a louder one: eight more tasks run this suite.
    await clearDepletion()

    const seen = new Set<string>()
    for (let i = 0; i < 12 && seen.size < 2; i++) {
      await clearRoster()
      await setLastSettled(1, hoursAgo(12))
      const body = await (await claim(1)).json() as { creatures: Array<{ species: string }> }
      for (const c of body.creatures) seen.add(c.species)
    }
    // TWO, not three: the roll is uniform over three species, so demanding
    // all three would be a test whose pass depends on how many iterations
    // were budgeted. Two is the property design 5.2 actually needs, and
    // twelve claims of at least one creature each make missing it a
    // ~1-in-88,000 event. The exact distribution is pinned deterministically
    // in test/base-stock.test.ts, where it costs no container.
    expect(seen.size).toBeGreaterThanOrEqual(2)
  })

  it('never depletes the Common Vein', async () => {
    // bible 5.3, and it is what stops the loop seizing: a player who
    // exhausts the Rich Deposit and cannot relocate still has a floor.
    // Harvest far past any plausible total and slot 0 still pays.
    for (let i = 0; i < 40; i++) {
      // The roster is emptied between claims because the HATCHERY cap would
      // otherwise start refusing them around the twentieth creature, and a
      // refused claim harvests nothing - the loop would stop accumulating
      // depletion and this test would pass while measuring nothing.
      await clearRoster()
      await setLastSettled(0, hoursAgo(12))
      expect((await claim(0)).status).toBe(200)
    }

    // Past the Rich Deposit's entire authored yield, on a node that has none.
    expect((await depletionRow(0))!.harvestedUnits).toBeGreaterThan(RICH_TOTAL)
    expect((await depletionRow(0))!.depletedAt).toBeNull()

    await clearRoster()
    await setLastSettled(0, hoursAgo(12))
    const body = await (await claim(0)).json() as { shards: number }
    expect(body.shards).toBe(12 * COMMON.ratePerHour)   // 240, the twelve-hour cap
  })

  it('stops paying a Rich Deposit once its yield is exhausted', async () => {
    await harvestDownTo(1, 0)
    await setLastSettled(1, hoursAgo(12))

    const res = await claim(1)
    expect(res.status).toBe(200)
    const body = await res.json() as { shards: number; creatures: unknown[] }
    expect(body.shards).toBe(0)
    // And no base stock either: it is earned on shards actually harvested.
    expect(body.creatures).toEqual([])
    expect(await balance()).toBe(STARTER_SHARDS)
    expect(await ledgerRowCount()).toBe(STARTER_LEDGER_ROWS)
  })

  it('pays out the last of a node exactly, and marks it depleted', async () => {
    await harvestDownTo(1, 100)
    await setLastSettled(1, hoursAgo(12))       // would otherwise be 720

    const body = await (await claim(1)).json() as { shards: number }
    expect(body.shards).toBe(100)
    const row = await depletionRow(1)
    expect(row!.harvestedUnits).toBe(RICH_TOTAL)
    expect(row!.depletedAt).not.toBeNull()
  })

  it('refuses a slot that is not in this epoch\'s node set', async () => {
    // Paired with a claim on a slot that DOES exist, because Hono's own
    // notFound handler answers `not_found` 404 as well - so without this
    // line the assertion below is satisfied by the route not being
    // registered at all. Measured, not supposed: it was the one test in
    // this file that stayed green when registerRegionRoutes was removed.
    expect((await claim(0)).status).toBe(200)

    const res = await claim(7)
    expect(res.status).toBe(404)
    expect(await res.json()).toMatchObject({ code: 'not_found' })
    expect(await balance()).toBe(STARTER_SHARDS)
  })

  it('refuses a slot no schema could store', async () => {
    // The parse layer's bound, not a statement about which nodes exist -
    // node_slot is a smallint. 400 rather than 404, because the request is
    // malformed rather than merely unsatisfiable.
    const res = await claimWithKey(2 ** 40, randomUUID())
    expect(res.status).toBe(400)
    expect(await res.json()).toMatchObject({ code: 'invalid_request' })
  })
})

describe('two claims racing for the last of a node', () => {
  it('cannot together take more than the node holds', async () => {
    // THE OVERSHOOT GUARD. `node_depletion.harvested_units` has no CHECK
    // against a node's total yield and cannot have one - `total_yield` is
    // bundle config, not a column - so the bound lives on the write path or
    // nowhere. Under READ COMMITTED, two claims that both read
    // `harvested_units` before either commits both compute the same
    // `remaining` and both credit it.
    //
    // THE INTERLEAVING IS CONSTRUCTED, not hoped for, and the first version
    // of this test did not construct it. Claim A is paused by a hook in its
    // OWN read-to-write window (see ClaimHooks), so claim B is guaranteed to
    // arrive after A has read and before A has written - which is the only
    // ordering the race exists in. Racing two HTTP requests instead produces
    // whatever ordering the machine happens to give, and an unguarded
    // implementation survives almost all of them: this suite stayed green
    // against `lockDepletion` rewritten as `ON CONFLICT DO NOTHING` plus a
    // plain SELECT until the hook existed.
    //
    // WHAT `ON CONFLICT DO NOTHING` ACTUALLY DOES, since it looks like a
    // cheaper lock and is not one: it waits only while a conflicting tuple
    // is uncommitted, and the depletion row here is committed long before
    // either claim starts. It never waits during the read-to-write window,
    // which is exactly where the race is. It is not a weaker guard; it is
    // not a guard.
    const A = playerId
    const B = (await setupPlayer(deps)).playerId

    await harvestDownTo(1, 100)
    const settledAt = hoursAgo(12)
    await setLastSettled(1, settledAt, () => A)
    await setLastSettled(1, settledAt, () => B)

    let release!: () => void
    const paused = new Promise<void>((r) => { release = r })
    let aReachedTheWindow!: () => void
    const aIsInTheWindow = new Promise<void>((r) => { aReachedTheWindow = r })

    let firstShards = -1
    const txA = withServer(deps.db, SERVER_ID, async (tx) => {
      const r = await claimNode(tx, SERVER_ID, A, bundle, 1, new Date(), randomUUID(), {
        afterDepletionRead: async () => {
          aReachedTheWindow()
          await paused
        },
      })
      if (r.kind !== 'ok') throw new Error(`first claim refused: ${r.kind}`)
      firstShards = r.shards
    })

    // A has read the node and written nothing. No sleep: waiting on the
    // hook itself is what makes this deterministic rather than timing-based.
    await aIsInTheWindow

    let bSettled = false
    const txB = withServer(deps.db, SERVER_ID, async (tx) => {
      const r = await claimNode(tx, SERVER_ID, B, bundle, 1, new Date(), randomUUID())
      bSettled = true
      return r
    })

    await new Promise((r) => setTimeout(r, 400))
    // B IS BLOCKED, and here that means something it did not mean before:
    // A has taken the depletion lock and made NO other write, so there is
    // nothing else in flight for B to be stuck on. Without the lock, B reads
    // straight past A and finishes.
    expect(bSettled).toBe(false)

    release()
    await txA
    const outcome = await txB

    expect(firstShards).toBe(100)   // A took everything the node had left
    expect(outcome.kind).toBe('ok')
    expect(outcome.kind === 'ok' ? outcome.shards : -1).toBe(0)

    // The invariant the whole guard exists for: 8,640 in the node, 8,640
    // out of it. An unguarded claim reaches 8,740.
    expect((await depletionRow(1))!.harvestedUnits).toBe(RICH_TOTAL)
    expect((await balance(() => A)) + (await balance(() => B)))
      .toBe(2 * STARTER_SHARDS + 100)
  })
})
