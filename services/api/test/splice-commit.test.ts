import { randomUUID } from 'node:crypto'
import { mkdtemp, rm } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { and, eq } from 'drizzle-orm'
import { afterAll, beforeAll, beforeEach, describe, expect, it } from 'vitest'
import { createApp, type Deps } from '../src/app.ts'
import { clearBundleCache, loadBundle } from '../src/config/bundle.ts'
import { publishBundle } from '../src/config/publish.ts'
import { LocalBundleStore } from '../src/config/store.ts'
import { withServer } from '../src/db/client.ts'
import { accounts, creatures, ledger, players, servers, splices, wallets } from '../src/db/schema.ts'
import { LocalReplayStore } from '../src/replays/store.ts'
import { creatureHp, rosterCount } from '../src/roster/creatures.ts'
import { SimClient } from '../src/sim/client.ts'
import { sampleSplice, spliceDistribution, type TraitRef } from '../src/splice/distribution.ts'
import { startTestDb, type TestDb } from './harness.ts'
import { balance, setupPlayer } from './wave-helpers.ts'

/**
 * `POST /v1/splice/commit` - design §5.1, and the first route in this game
 * that destroys player property.
 *
 * EVERY REFUSAL HERE ASSERTS ON STATE, not only on a status code. A route
 * that refused everything would pass a status-code suite perfectly, and the
 * thing actually worth guaranteeing is the sentence design §5.2 writes into
 * the refusal message: *nothing has been consumed*. So each refusal checks
 * that both parents are still live AND that the charge is unspent.
 */

const SERVER_ID = 1
const TICK = { tickDayOfWeek: 0, tickMinuteOfDay: 1200 }
/** config/bundles/0.1.2/starter.json. The only source of charges this phase. */
const STARTING_CHARGES = 3

// fileURLToPath, not .pathname - this repo lives under a directory
// containing a space, which .pathname percent-encodes.
const REPO = fileURLToPath(new URL('../../../', import.meta.url))
const SEED = join(REPO, 'config/bundles/0.1.2')

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

  bundleRoot = await mkdtemp(join(tmpdir(), 'broodline-splice-commit-'))
  const store = new LocalBundleStore(bundleRoot)
  await publishBundle(store, SEED, '0.1.2')
  await store.setPointer('0.1.2')
  clearBundleCache()

  // No route under test calls sim - the same idiom node-claim.test.ts and
  // splice-preview.test.ts use, with the address pointing nowhere reachable.
  deps = {
    db: t.db,
    bundleStore: store,
    simClient: new SimClient('http://127.0.0.1:1', SimClient.noAuth('a deliberately dead address - no route here calls sim')),
    replayStore: new LocalReplayStore(bundleRoot),
  }
  app = createApp(deps)
  await loadBundle(store)
}, 240_000)

afterAll(async () => {
  await t?.stop()
  if (bundleRoot) await rm(bundleRoot, { recursive: true, force: true })
})

beforeEach(async () => {
  ({ playerId, token } = await setupPlayer(deps))
})

interface NewCreature {
  species?: string
  trait1?: string
  tier1?: number | null
  trait2?: string
  tier2?: number | null
  instinct?: string
  generation?: number
  parentA?: string | null
  owner?: string
  committedTo?: string | null
  pruned?: boolean
  consumed?: boolean
}

/**
 * Fixtures through the OWNER connection - splice-preview.test.ts's idiom and
 * for the same reason: `grantBaseStock` is the only `insert(creatures)` on a
 * grant path and it mints Gen-1 Tier-I base stock with no way to ask for a
 * generation, a tier or a parent.
 */
async function give(c: NewCreature = {}): Promise<string> {
  const species = c.species ?? 'Vetch'
  const [row] = await t.ownerDb.insert(creatures).values({
    serverId: SERVER_ID,
    playerId: c.owner ?? playerId,
    species,
    generation: c.generation ?? 1,
    trait1: c.trait1 ?? 'Taunt', tier1: c.tier1 === undefined ? 1 : c.tier1,
    trait2: c.trait2 ?? 'Carapace', tier2: c.tier2 === undefined ? 1 : c.tier2,
    instinct: c.instinct ?? 'Vanguard',
    hpCurrent: creatureHp(species),
    isFounder: false,
    parentA: c.parentA ?? null,
    committedTo: c.committedTo ?? null,
    consumedAt: c.consumed === true || c.pruned === true ? new Date('2026-09-01T00:00:00Z') : null,
  }).returning()
  const id = row!.creatureId
  if (c.pruned === true) {
    // Stripped the way design §3.2's prune strips it, so the row is a real
    // tombstone rather than a live creature wearing a flag - 0005's
    // pruned_creatures_are_stripped refuses the latter outright.
    await t.ownerDb.update(creatures)
      .set({
        pruned: true, trait1: null, tier1: null, trait2: null, tier2: null,
        instinct: null, name: null, hpCurrent: null, regenUntil: null, committedTo: null,
      })
      .where(and(eq(creatures.serverId, SERVER_ID), eq(creatures.creatureId, id)))
  }
  return id
}

const LOCK_A1: TraitRef = { slot: 'trait_1', from: 'a' }

interface CommitBody {
  parentA: string
  parentB: string
  locked?: TraitRef
  bodyFrom?: string
}

async function commit(
  body: CommitBody | unknown, o: { key?: string; auth?: string; noKey?: boolean } = {},
): Promise<Response> {
  const headers: Record<string, string> = {
    'content-type': 'application/json',
    authorization: o.auth ?? `Bearer ${token}`,
  }
  if (o.noKey !== true) headers['idempotency-key'] = o.key ?? randomUUID()
  return app.request('/v1/splice/commit', {
    method: 'POST', headers, body: JSON.stringify(body),
  })
}

type Row = typeof creatures.$inferSelect

async function read(id: string): Promise<Row | undefined> {
  const [row] = await t.ownerDb.select().from(creatures)
    .where(and(eq(creatures.serverId, SERVER_ID), eq(creatures.creatureId, id)))
  return row
}

/**
 * On the roster, in the one sense that matters: not pruned and not consumed.
 *
 * `roster/creatures.ts`'s `liveCreature()` is the shipped predicate; this is
 * deliberately written out by hand instead of importing it, so a weakening
 * that drops a half from the predicate cannot also silently move what these
 * assertions mean.
 */
async function isLive(id: string): Promise<boolean> {
  const row = await read(id)
  return row !== undefined && !row.pruned && row.consumedAt === null
}

async function liveCount(): Promise<number> {
  return withServer(deps.db, SERVER_ID, (tx) => rosterCount(tx, SERVER_ID, playerId))
}

async function spliceRows(): Promise<Array<typeof splices.$inferSelect>> {
  return t.ownerDb.select().from(splices)
    .where(and(eq(splices.serverId, SERVER_ID), eq(splices.playerId, playerId)))
}

async function chargeLedger(): Promise<Array<typeof ledger.$inferSelect>> {
  const rows = await t.ownerDb.select().from(ledger)
    .where(and(eq(ledger.serverId, SERVER_ID), eq(ledger.playerId, playerId)))
  return rows.filter((r) => r.currency === 'splice_charges' && r.delta < 0)
}

/** Two ordinary base-stock-shaped parents of different species. */
const VETCH = { species: 'Vetch', trait1: 'Taunt', trait2: 'Carapace' }
const PALE = { species: 'Pale', trait1: 'Chill', trait2: 'Carapace' }

async function pair(): Promise<{ a: string; b: string }> {
  return { a: await give(VETCH), b: await give(PALE) }
}

/** A second player on this server, without touching wave-helpers' module state. */
async function strangerPlayer(): Promise<string> {
  const [acc] = await t.ownerDb.insert(accounts)
    .values({ birthdateBand: 'adult', homeRegion: 'us-central1', serverId: SERVER_ID }).returning()
  const [p] = await t.ownerDb.insert(players)
    .values({ serverId: SERVER_ID, accountId: acc!.accountId }).returning()
  return p!.playerId
}

/**
 * The state every refusal must leave behind: both parents alive, the charge
 * unspent, no child, no splice record.
 *
 * design §5.2: "a splice that would exceed it is blocked" - and a blocked
 * splice is not a partial one. A partial splice costs a player two creatures
 * for no child, which is the loss the whole confirmation spec exists to
 * prevent and which no retry can repair.
 */
async function nothingConsumed(a: string, b: string): Promise<void> {
  expect(await isLive(a), 'parent A still live').toBe(true)
  expect(await isLive(b), 'parent B still live').toBe(true)
  expect(await balance('splice_charges'), 'charge unspent').toBe(STARTING_CHARGES)
  expect(await spliceRows(), 'no splice record').toHaveLength(0)
  expect(await chargeLedger(), 'no charge ledger row').toHaveLength(0)
  expect(await liveCount(), 'roster unchanged').toBe(2)
}

describe('POST /v1/splice/commit', () => {
  it('consumes both parents and one charge in the same transaction as the child', async () => {
    const { a, b } = await pair()
    expect(await balance('splice_charges')).toBe(STARTING_CHARGES)

    const res = await commit({ parentA: a, parentB: b, locked: LOCK_A1, bodyFrom: 'Vetch' })
    expect(res.status).toBe(200)
    const body = await res.json() as {
      child: { creatureId: string; generation: number; species: string; isFounder: boolean; name: string | null }
      spliceId: string; seed: string; mutated: boolean; aberrant: boolean; balance: number
    }

    // The parents are gone FROM THE ROSTER.
    expect(await isLive(a)).toBe(false)
    expect(await isLive(b)).toBe(false)
    expect(await liveCount()).toBe(1)

    // The charge is spent, once, with a ledger row naming the splice.
    expect(await balance('splice_charges')).toBe(STARTING_CHARGES - 1)
    expect(body.balance).toBe(STARTING_CHARGES - 1)
    const entries = await chargeLedger()
    expect(entries).toHaveLength(1)
    expect(entries[0]).toMatchObject({ delta: -1, reasonCode: 'splice', refType: 'splice' })
    expect(entries[0]?.refId).toBe(body.spliceId)

    // The child exists and is the player's.
    const child = await read(body.child.creatureId)
    expect(child).toMatchObject({
      playerId, generation: 2, species: 'Vetch',
      parentA: a, parentB: b, pruned: false, consumedAt: null,
    })
    expect(body.child.generation).toBe(2)

    // And the record of the roll.
    const rows = await spliceRows()
    expect(rows).toHaveLength(1)
    expect(rows[0]).toMatchObject({
      spliceId: body.spliceId, parentA: a, parentB: b, childId: body.child.creatureId,
      mutated: body.mutated, aberrant: body.aberrant,
    })
  })

  it('leaves the consumed parents WHOLE, so the lineage can still render them', async () => {
    // design §3.2 retains ancestors five generations deep, and
    // `splice_confirm_spec` §5 makes the consumed parents appearing in the
    // tree immediately afterwards the lesson of the whole mechanic - "their
    // traits live on in the pedigree". A prune at consumption would strip
    // them here, at depth one, and there would be nothing left to retain at
    // any depth.
    const { a, b } = await pair()
    await commit({ parentA: a, parentB: b, locked: LOCK_A1, bodyFrom: 'Vetch' })

    for (const id of [a, b]) {
      const row = await read(id)
      expect(row?.pruned, 'consumed is not pruned').toBe(false)
      expect(row?.consumedAt).not.toBeNull()
      expect(row?.trait1).not.toBeNull()
      expect(row?.instinct).not.toBeNull()
      expect(row?.hpCurrent).not.toBeNull()
    }
  })

  it('carries the locked trait at FULL coverage and rolls only the second slot', async () => {
    // design §5.3: the locked combat trait carries at its parent's full
    // coverage - no dominance roll and no downtier, which is the rolled
    // slot's rule alone. Taunt is RECESSIVE in bundle 0.1.2, so a handler
    // that downtiered both slots would drop this Tier II to Tier I and no
    // other test in this file would notice.
    const a = await give({ ...VETCH, tier1: 2, tier2: 2 })
    const b = await give(PALE)

    const res = await commit({ parentA: a, parentB: b, locked: LOCK_A1, bodyFrom: 'Vetch' })
    const { child } = await res.json() as { child: { trait1: string; tier1: number; trait2: string } }

    expect(child.trait1).toBe('Taunt')
    expect(child.tier1).toBe(2)
    // And the locked TRAIT is not what rolled into slot 2 - Task 6's rule.
    expect(child.trait2).not.toBe('Taunt')
  })

  it('stores a seed the roll can be re-derived from', async () => {
    // design §5.1: a paid randomised action with published odds whose roll
    // cannot be re-derived afterwards has no evidence on either side of a
    // dispute. Re-derived here from the STORED seed against the SAME
    // distribution preview would have published for these parents.
    const a = await give({ ...VETCH, tier1: 2, tier2: 1 })
    const b = await give({ ...PALE, tier1: 3, tier2: 2 })

    const res = await commit({ parentA: a, parentB: b, locked: LOCK_A1, bodyFrom: 'Pale' })
    const body = await res.json() as {
      child: { trait2: string; tier2: number | null; instinct: string }
      seed: string; mutated: boolean; aberrant: boolean
    }

    const [row] = await spliceRows()
    expect(row?.seed).toBe(body.seed)

    const bundle = await loadBundle(deps.bundleStore)
    const forecast = spliceDistribution(
      { trait1: 'Taunt', tier1: 2, trait2: 'Carapace', tier2: 1, instinct: 'Vanguard' },
      { trait1: 'Chill', tier1: 3, trait2: 'Carapace', tier2: 2, instinct: 'Vanguard' },
      LOCK_A1, bundle)
    const rederived = sampleSplice(forecast, BigInt(row!.seed))

    expect(rederived.combat2.trait).toBe(body.child.trait2)
    expect(rederived.combat2.tier).toBe(body.child.tier2)
    expect(rederived.instinct).toBe(body.child.instinct)
    expect(rederived.mutated).toBe(row!.mutated)
    expect(rederived.aberrant).toBe(row!.aberrant)
  })

  it('refuses to splice a creature that is out fighting', async () => {
    // design §2.5. `committed_to` gets its first writer in Task 8; this is
    // the race it closes - the deployment stored on an issuance must not
    // outlive the roster rows it was resolved from.
    const { a, b } = await pair()
    await t.ownerDb.update(creatures).set({ committedTo: randomUUID() })
      .where(and(eq(creatures.serverId, SERVER_ID), eq(creatures.creatureId, a)))

    const res = await commit({ parentA: a, parentB: b, locked: LOCK_A1, bodyFrom: 'Vetch' })
    expect(res.status).toBe(409)
    expect(await res.json()).toMatchObject({ code: 'creature_committed' })

    expect(await isLive(a)).toBe(true)
    expect(await isLive(b)).toBe(true)
    expect(await balance('splice_charges')).toBe(STARTING_CHARGES)
    expect(await spliceRows()).toHaveLength(0)
  })

  it('refuses the SECOND parent being committed too, not only the first', async () => {
    // One half proven and its twin unproven is how a handler that checks
    // `a.committedTo` alone ships.
    const { a, b } = await pair()
    await t.ownerDb.update(creatures).set({ committedTo: randomUUID() })
      .where(and(eq(creatures.serverId, SERVER_ID), eq(creatures.creatureId, b)))

    const res = await commit({ parentA: a, parentB: b, locked: LOCK_A1, bodyFrom: 'Vetch' })
    expect(res.status).toBe(409)
    await nothingConsumed(a, b)
  })

  it('refuses a child that would exceed the generation ceiling, and consumes nothing', async () => {
    // combat_numbers §7: a tier-3 Splicing Chamber caps at G4, so two G4
    // parents would make a G5. design §5.2 wants the Chamber upgrade
    // surfaced rather than a bare error, so the ceiling travels with the
    // refusal.
    const a = await give({ ...VETCH, generation: 4 })
    const b = await give({ ...PALE, generation: 4 })

    const res = await commit({ parentA: a, parentB: b, locked: LOCK_A1, bodyFrom: 'Vetch' })
    expect(res.status).toBe(409)
    expect(await res.json()).toMatchObject({
      code: 'generation_ceiling', details: { ceiling: 4, would: 5 },
    })
    await nothingConsumed(a, b)
  })

  it('allows the deepest splice the ceiling does permit', async () => {
    // The positive control. Without it the test above is satisfied by a
    // handler that refuses every splice above generation 1, and "the ceiling
    // is G4" would be indistinguishable from "the ceiling is G2".
    const a = await give({ ...VETCH, generation: 3 })
    const b = await give({ ...PALE, generation: 2 })

    const res = await commit({ parentA: a, parentB: b, locked: LOCK_A1, bodyFrom: 'Vetch' })
    expect(res.status).toBe(200)
    expect((await res.json() as { child: { generation: number } }).child.generation).toBe(4)
  })

  it('refuses a body that is neither parent\'s species', async () => {
    // design §5.2's "free choice" is UNRANDOMISED, not unconstrained.
    // Without this a client mints any species it likes out of two it owns,
    // which is the splice's own version of the ownership hole design §6
    // closes on the wave path.
    const { a, b } = await pair()

    const res = await commit({ parentA: a, parentB: b, locked: LOCK_A1, bodyFrom: 'Hollow' })
    expect(res.status).toBe(400)
    expect(await res.json()).toMatchObject({ code: 'invalid_request' })
    await nothingConsumed(a, b)
  })

  it('takes either parent\'s species, and the child\'s HP with it', async () => {
    // The positive control for the refusal above, and it has to cover BOTH
    // parents: a handler that only ever accepted parent A's species would
    // pass the refusal test identically. The HP is the engine's
    // Stats.CreatureHp for the chosen body, not for parent A's.
    for (const [bodyFrom, hp] of [['Vetch', 260], ['Pale', 120]] as const) {
      const { a, b } = await pair()
      const res = await commit({ parentA: a, parentB: b, locked: LOCK_A1, bodyFrom })
      expect(res.status, bodyFrom).toBe(200)

      const { child } = await res.json() as { child: { creatureId: string; species: string } }
      expect(child.species).toBe(bodyFrom)
      expect((await read(child.creatureId))?.hpCurrent, bodyFrom).toBe(hp)
    }
  })

  it('refuses a parent the player does not own', async () => {
    // A REAL second player, not an invented uuid: `creatures` carries a
    // composite FK to (server_id, player_id), so a fabricated owner is
    // refused by the schema before the route is reached.
    //
    // Seeded through ownerDb rather than through `setupPlayer`, which
    // rebinds wave-helpers' module-level player and would silently move what
    // `balance` and `liveCount` below are talking about.
    const { a, b } = await pair()
    const theirs = await give({ ...PALE, owner: await strangerPlayer() })

    const res = await commit({ parentA: theirs, parentB: b, locked: LOCK_A1, bodyFrom: 'Pale' })
    expect(res.status).toBe(404)

    expect(await isLive(theirs)).toBe(true)
    await nothingConsumed(a, b)
  })

  it('refuses a parent an earlier splice already consumed', async () => {
    // THE DOUBLE-SPEND, sequentially. A consumed parent is NOT pruned - it
    // keeps every trait it had - so a liveness predicate written as `NOT
    // pruned` alone would splice it a second time and mint a second child
    // out of one creature.
    const { a, b } = await pair()
    const c = await give(PALE)

    expect((await commit({ parentA: a, parentB: b, locked: LOCK_A1, bodyFrom: 'Vetch' })).status).toBe(200)

    const res = await commit({ parentA: a, parentB: c, locked: LOCK_A1, bodyFrom: 'Vetch' })
    expect(res.status).toBe(404)
    expect(await isLive(c)).toBe(true)
    expect(await balance('splice_charges')).toBe(STARTING_CHARGES - 1)
    expect(await spliceRows()).toHaveLength(1)
  })

  it('refuses a PRUNED ancestor as a parent', async () => {
    // A tombstone has no traits at all, so splicing against one would roll
    // over `null`. The predicate that catches it is easy to leave out
    // because the prune NULLS committed_to, which makes every "uncommitted"
    // filter true of a dead ancestor.
    const a = await give(VETCH)
    const ancestor = await give({ ...PALE, pruned: true })

    const res = await commit({ parentA: a, parentB: ancestor, locked: LOCK_A1, bodyFrom: 'Vetch' })
    expect(res.status).toBe(404)
    expect(await isLive(a)).toBe(true)
    expect(await balance('splice_charges')).toBe(STARTING_CHARGES)
  })

  it('refuses a splice with no Splice Charge to spend, and consumes nothing', async () => {
    const { a, b } = await pair()
    await t.ownerDb.update(wallets).set({ balance: 0 })
      .where(and(
        eq(wallets.serverId, SERVER_ID), eq(wallets.playerId, playerId),
        eq(wallets.currency, 'splice_charges')))

    const res = await commit({ parentA: a, parentB: b, locked: LOCK_A1, bodyFrom: 'Vetch' })
    expect(res.status).toBe(409)
    expect(await res.json()).toMatchObject({ code: 'insufficient_charges' })

    expect(await isLive(a)).toBe(true)
    expect(await isLive(b)).toBe(true)
    expect(await balance('splice_charges')).toBe(0)
    expect(await spliceRows()).toHaveLength(0)
    expect(await liveCount()).toBe(2)
  })

  it('is idempotent under a retried key', async () => {
    const { a, b } = await pair()
    const key = randomUUID()
    const body = { parentA: a, parentB: b, locked: LOCK_A1, bodyFrom: 'Vetch' }

    const first = await (await commit(body, { key })).json() as { child: { creatureId: string }; spliceId: string }
    const second = await (await commit(body, { key })).json() as { child: { creatureId: string }; spliceId: string }

    expect(second.child.creatureId).toBe(first.child.creatureId)
    expect(second.spliceId).toBe(first.spliceId)
    expect(await balance('splice_charges')).toBe(STARTING_CHARGES - 1)
    expect(await spliceRows()).toHaveLength(1)
    expect(await chargeLedger()).toHaveLength(1)
    // One child, not two.
    expect(await liveCount()).toBe(1)
  })

  it('refuses a DIFFERENT splice under a key already used', async () => {
    // 422, not a replay: returning the first splice's child for a request
    // naming two other creatures would answer a question the caller did not
    // ask, and on this route the answer names creatures that were destroyed.
    const { a, b } = await pair()
    const c = await give(VETCH)
    const key = randomUUID()

    await commit({ parentA: a, parentB: b, locked: LOCK_A1, bodyFrom: 'Vetch' }, { key })
    const res = await commit({ parentA: a, parentB: c, locked: LOCK_A1, bodyFrom: 'Vetch' }, { key })

    expect(res.status).toBe(422)
    expect(await res.json()).toMatchObject({ code: 'idempotency_key_reused' })
    expect(await isLive(c)).toBe(true)
  })

  it('prunes the lineage on the splice path', async () => {
    // design §3.2: the prune runs on the write that CHANGES THE DEPTH rather
    // than as a sweep. Five ancestors above parent A means the deepest sits
    // at depth six from the child the moment it is written.
    //
    // Generations are all 1 so the Chamber's G4 ceiling does not bind - this
    // is about depth, which the schema does not tie to generation.
    let ancestor: string | null = null
    const line: string[] = []
    for (let i = 0; i < 5; i++) {
      ancestor = await give({ ...VETCH, parentA: ancestor, consumed: true })
      line.unshift(ancestor)
    }
    const a = await give({ ...VETCH, parentA: ancestor })
    const b = await give(PALE)

    const res = await commit({ parentA: a, parentB: b, locked: LOCK_A1, bodyFrom: 'Vetch' })
    expect(res.status).toBe(200)

    // line[0] is the newest ancestor (depth 2 from the child); line[4] is the
    // oldest, at depth 6.
    expect((await read(line[4]!))?.pruned, 'depth 6').toBe(true)
    expect((await read(line[3]!))?.pruned, 'depth 5').toBe(false)
    expect((await read(a))?.pruned, 'the parent itself').toBe(false)
  })

  it('refuses two concurrent splices that share a parent', async () => {
    // The double-spend under CONCURRENCY, which `withIdempotency` does not
    // close: it makes ONE key run once, and this uses two. The guard is the
    // FOR UPDATE on both parent rows in commitSplice.
    //
    // HONEST ABOUT WHAT THIS PROVES: with the lock the outcome is
    // deterministic (the loser blocks, then reads the consumed parent and is
    // refused). Without it the two transactions have to actually interleave
    // to produce two children, so a green here is not by itself proof the
    // lock exists - see the task report's weakening row.
    const { a, b } = await pair()
    const c = await give(PALE)
    const body = (second: string) => ({
      parentA: a, parentB: second, locked: LOCK_A1, bodyFrom: 'Vetch',
    })

    const [one, two] = await Promise.all([
      commit(body(b), { key: randomUUID() }),
      commit(body(c), { key: randomUUID() }),
    ])

    const statuses = [one.status, two.status].sort()
    expect(statuses).toEqual([200, 404])
    expect(await spliceRows()).toHaveLength(1)
    expect(await balance('splice_charges')).toBe(STARTING_CHARGES - 1)
  })

  it('requires an Idempotency-Key', async () => {
    const { a, b } = await pair()
    const res = await commit(
      { parentA: a, parentB: b, locked: LOCK_A1, bodyFrom: 'Vetch' }, { noKey: true })

    expect(res.status).toBe(400)
    await nothingConsumed(a, b)
  })

  it('refuses a malformed body as a bad request, not as a server error', async () => {
    const { a, b } = await pair()
    for (const body of [
      { parentA: 'not-a-uuid', parentB: b, locked: LOCK_A1, bodyFrom: 'Vetch' },
      { parentA: a, parentB: a, locked: LOCK_A1, bodyFrom: 'Vetch' },
      { parentA: a, parentB: b, locked: { slot: 'trait_3', from: 'a' }, bodyFrom: 'Vetch' },
      { parentA: a, parentB: b, locked: LOCK_A1 },
      { parentA: a, parentB: b, locked: LOCK_A1, bodyFrom: '' },
      { parentA: a, parentB: b, locked: LOCK_A1, bodyFrom: 7 },
    ]) {
      const res = await commit(body)
      expect(res.status, JSON.stringify(body)).toBe(400)
    }
    await nothingConsumed(a, b)
  })

  it('requires a session', async () => {
    const { a, b } = await pair()
    const res = await commit(
      { parentA: a, parentB: b, locked: LOCK_A1, bodyFrom: 'Vetch' }, { auth: 'Bearer nonsense' })

    expect(res.status).toBe(401)
    await nothingConsumed(a, b)
  })
})
