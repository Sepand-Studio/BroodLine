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
import { commitSplice, isFirstSplice } from '../src/splice/commit.ts'
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
      spliceId: string; seed: string; balance: number
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
    })
    // The roll is RECORDED even though it is not reported - see the next test.
    expect(typeof rows[0]?.mutated).toBe('boolean')
    expect(typeof rows[0]?.aberrant).toBe('boolean')
  })

  it('mutates with certainty on this player\'s first splice - Task 7\'s guaranteed beat', async () => {
    // 100% guaranteed rather than merely likely: `mutation: 1` makes
    // `uMutation < d.mutation` true for EVERY seed (uMutation is drawn in
    // [0, 1)), so this is not a flake despite reading a column that is
    // ordinarily a 9% roll. `isFirstSplice` reads zero rows in `splices` for
    // this fresh player, exactly as splice-preview.test.ts's forecast test
    // reads for the same fact on the other route.
    expect(await withServer(deps.db, SERVER_ID, (tx) => isFirstSplice(tx, SERVER_ID, playerId)))
      .toBe(true)

    const { a, b } = await pair()
    const res = await commit({ parentA: a, parentB: b, locked: LOCK_A1, bodyFrom: 'Vetch' })
    expect(res.status).toBe(200)

    const [row] = await spliceRows()
    expect(row?.mutated).toBe(true)

    // And the SECOND splice for this player is no longer the first one.
    expect(await withServer(deps.db, SERVER_ID, (tx) => isFirstSplice(tx, SERVER_ID, playerId)))
      .toBe(false)
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
      seed: string
    }

    const [row] = await spliceRows()
    expect(row?.seed).toBe(body.seed)

    // { guaranteedMutation: true }: this is this fresh player's first splice
    // (beforeEach mints a brand-new one per test), so Task 7's parameter is
    // set the same way commit's own internal recomputation sets it - not a
    // fact this test may drop, or the re-derivation below answers a
    // different distribution than the one that actually rolled.
    const bundle = await loadBundle(deps.bundleStore)
    const forecast = spliceDistribution(
      { trait1: 'Taunt', tier1: 2, trait2: 'Carapace', tier2: 1, instinct: 'Vanguard' },
      { trait1: 'Chill', tier1: 3, trait2: 'Carapace', tier2: 2, instinct: 'Vanguard' },
      LOCK_A1, bundle, { guaranteedMutation: true })
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
    // THE DOUBLE-SPEND UNDER CONCURRENCY, which `withIdempotency` does not
    // close: it makes ONE key run once, and this uses two. The guard is the
    // FOR UPDATE that `lockParents` takes on both parent rows.
    //
    // THE INTERLEAVING IS CONSTRUCTED, not hoped for, and the first version
    // of this test did not construct it - it raced two HTTP requests through
    // `Promise.all` and stayed GREEN against a `lockParents` with its
    // FOR UPDATE removed. Measured, then fixed the way node-claim.test.ts
    // fixed the identical problem: splice A is paused by a hook in its own
    // read-to-write window, so splice B is guaranteed to arrive after A has
    // read the parents and before A has written anything, which is the only
    // ordering the race exists in.
    //
    // WHAT THE LOCK CHANGES, precisely: with it, B blocks on the parent row
    // and - once A commits - re-reads a consumed parent and is REFUSED.
    // Without it, B reads straight past A, decides the splice is legal, and
    // is stopped only by `consume`'s own row count, which throws and aborts
    // the transaction. A refusal and a thrown error are both "no second
    // child", but only one of them is a route answering a player.
    const { a, b } = await pair()
    const c = await give(PALE)
    const bundle = await loadBundle(deps.bundleStore)
    const req = (second: string) => ({
      parentA: a, parentB: second, locked: LOCK_A1, bodyFrom: 'Vetch',
    })

    let release!: () => void
    const paused = new Promise<void>((r) => { release = r })
    let aReachedTheWindow!: () => void
    const aIsInTheWindow = new Promise<void>((r) => { aReachedTheWindow = r })

    const txA = withServer(deps.db, SERVER_ID, async (tx) => {
      const r = await commitSplice(
        tx, SERVER_ID, playerId, bundle, req(b), new Date(), randomUUID(), {
          afterParentsLocked: async () => { aReachedTheWindow(); await paused },
        })
      if (r.kind !== 'ok') throw new Error(`first splice refused: ${r.kind}`)
      return r
    })

    // A holds both parent locks and has written nothing. No sleep: waiting
    // on the hook itself is what makes this deterministic.
    await aIsInTheWindow

    let bSettled = false
    const txB = withServer(deps.db, SERVER_ID, async (tx) => {
      const r = await commitSplice(
        tx, SERVER_ID, playerId, bundle, req(c), new Date(), randomUUID())
      bSettled = true
      return r
    })

    await new Promise((r) => setTimeout(r, 400))
    // B IS BLOCKED, and here that means something specific: A has taken the
    // two parent locks and made no other write, so there is nothing else in
    // flight for B to be stuck on. Without the lock, B reads straight past A.
    expect(bSettled).toBe(false)

    release()
    await txA
    const outcome = await txB

    // B is REFUSED, not thrown at, and it is refused for the right reason -
    // parent A is no longer a live creature.
    expect(outcome.kind).toBe('not_owned')
    expect(await isLive(c)).toBe(true)
    expect(await spliceRows()).toHaveLength(1)
    expect(await balance('splice_charges')).toBe(STARTING_CHARGES - 1)
    expect(await liveCount()).toBe(2) // the child, and the untouched c
  })

  it('does not deadlock when two splices name the same pair in opposite orders', async () => {
    // THE LOCK ORDER, and it is a property rather than a comment only
    // because there is a seam to stand in. `lockParents` takes its two
    // FOR UPDATE locks in SORTED id order; taking them in REQUEST order
    // instead lets (a, b) hold a while (b, a) holds b, and Postgres breaks
    // the cycle by aborting one transaction with 40P01 - a 500 on a route
    // that is destroying player property, where a clean refusal was
    // available.
    //
    // The window is between the two lock statements and is microseconds
    // wide, so `betweenParentLocks` is what makes the interleaving real
    // instead of hoped for: A pauses holding exactly one lock, and B is
    // given its chance to take the other.
    const { a, b } = await pair()
    const bundle = await loadBundle(deps.bundleStore)

    let release!: () => void
    const paused = new Promise<void>((r) => { release = r })
    let bTookOne!: () => void
    const bTookItsFirstLock = new Promise<void>((r) => { bTookOne = r })

    const txA = withServer(deps.db, SERVER_ID, (tx) => commitSplice(
      tx, SERVER_ID, playerId, bundle,
      { parentA: a, parentB: b, locked: LOCK_A1, bodyFrom: 'Vetch' },
      new Date(), randomUUID(), {
        betweenParentLocks: async () => {
          // Hold one lock and wait - either for B to take the other (which
          // is what an UNSORTED order would let it do, and the deadlock),
          // or for the timeout, which is B being correctly blocked.
          await Promise.race([bTookItsFirstLock, paused])
        },
      }))

    // B names the SAME pair in the opposite order.
    const txB = withServer(deps.db, SERVER_ID, (tx) => commitSplice(
      tx, SERVER_ID, playerId, bundle,
      { parentA: b, parentB: a, locked: LOCK_A1, bodyFrom: 'Vetch' },
      new Date(), randomUUID(), {
        betweenParentLocks: async () => { bTookOne() },
      }))

    setTimeout(release, 400)
    const outcomes = await Promise.all([txA, txB])

    // Exactly one splice happened, and NEITHER transaction was aborted by
    // the deadlock detector - which is what `await` above would have
    // surfaced as a rejection.
    expect(outcomes.filter((o) => o.kind === 'ok')).toHaveLength(1)
    expect(outcomes.filter((o) => o.kind === 'not_owned')).toHaveLength(1)
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

  it('stores the mutation roll but does NOT report it', async () => {
    // design §10 defers Aberrant traits out of this phase, so a mutation has
    // nothing to produce and the child is identical whichever way the roll
    // lands. Reporting `mutated: true` for an event with no effect shows the
    // player a mutation that did not happen - so the flags are withheld
    // until the content lands, and the seed on the row is what keeps the
    // roll re-derivable in the meantime.
    //
    // ONE SPLICE IS NOT ENOUGH to pin this: at a 9% rate a single roll is
    // almost always `false`, so `mutated: false` in a response would look
    // identical to the field being absent. The assertion is on the KEYS.
    const { a, b } = await pair()
    const res = await commit({ parentA: a, parentB: b, locked: LOCK_A1, bodyFrom: 'Vetch' })
    const body = await res.json() as Record<string, unknown>

    expect(Object.keys(body).sort()).toEqual(['balance', 'child', 'seed', 'spliceId'])
    expect(body).not.toHaveProperty('mutated')
    expect(body).not.toHaveProperty('aberrant')

    // And the row DOES carry them, so this is a withheld field rather than an
    // unrolled one.
    const [row] = await spliceRows()
    expect(row).toHaveProperty('mutated')
    expect(row).toHaveProperty('aberrant')
  })

  it('refuses a creature spliced with itself under a DIFFERENT CASE', async () => {
    // THE SAME BUG CLASS AS 7bbb76f, one route over. `http/ids.ts`'s regex
    // accepts upper case (RFC 4122 hex is case-insensitive and clients send
    // both) and Postgres `uuid` equality is case-insensitive - but JS `===`
    // is not. So this body used to pass `parentA === parentB`, resolve BOTH
    // parent locks to the same row, and run the splice PAST THE DEBIT before
    // `consume` found one parent where it expected two: HTTP 500 on the only
    // route in the game that destroys player property.
    //
    // 400, and the SAME 400 the lower-case body gets - which is the other
    // half of the point: `/v1/splice/preview` refuses the identical body, and
    // two splice routes disagreeing about whether a body is legal is exactly
    // what sharing `parseParents` exists to prevent.
    const { a, b } = await pair()

    const res = await commit({ parentA: a, parentB: a.toUpperCase(), locked: LOCK_A1, bodyFrom: 'Vetch' })
    expect(res.status).toBe(400)
    expect(await res.json()).toMatchObject({ code: 'invalid_request' })
    await nothingConsumed(a, b)
  })

  it('accepts an UPPER-CASE parent id and resolves it to the same creature', async () => {
    // The positive control, and without it the refusal above is satisfied by
    // a parse layer that rejects every upper-case id - which would break
    // every client that sends canonical upper-case uuids, and would be a
    // worse bug than the one being fixed.
    const { a, b } = await pair()

    const res = await commit({
      parentA: a.toUpperCase(), parentB: b, locked: LOCK_A1, bodyFrom: 'Vetch',
    })
    expect(res.status).toBe(200)
    const { child } = await res.json() as { child: { creatureId: string } }
    expect((await read(child.creatureId))?.parentA).toBe(a)
    expect(await isLive(a)).toBe(false)
  })

  it('replays a retried key whose id casing changed', async () => {
    // The quieter half of the same defect. Both splice routes hash the
    // PARSED body for the idempotency key, so an un-normalised id makes the
    // same logical retry a DIFFERENT request - 422 for something the caller
    // sent twice on purpose, on a route where the first attempt already
    // destroyed two creatures.
    const { a, b } = await pair()
    const key = randomUUID()

    const first = await commit({ parentA: a, parentB: b, locked: LOCK_A1, bodyFrom: 'Vetch' }, { key })
    const second = await commit(
      { parentA: a.toUpperCase(), parentB: b, locked: LOCK_A1, bodyFrom: 'Vetch' }, { key })

    expect(second.status).toBe(200)
    expect((await second.json() as { spliceId: string }).spliceId)
      .toBe((await first.json() as { spliceId: string }).spliceId)
    expect(await spliceRows()).toHaveLength(1)
    expect(await balance('splice_charges')).toBe(STARTING_CHARGES - 1)
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
