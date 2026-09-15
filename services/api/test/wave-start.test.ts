import { randomUUID } from 'node:crypto'
import { mkdtemp, rm } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { and, eq, inArray, sql } from 'drizzle-orm'
import { afterAll, beforeAll, beforeEach, describe, expect, it } from 'vitest'
import { createApp, type Deps } from '../src/app.ts'
import { clearBundleCache, loadBundle } from '../src/config/bundle.ts'
import { publishBundle } from '../src/config/publish.ts'
import { LocalBundleStore } from '../src/config/store.ts'
import { withServer } from '../src/db/client.ts'
import { creatures, servers, waveIssuances } from '../src/db/schema.ts'
import { LocalReplayStore } from '../src/replays/store.ts'
import { creatureHp } from '../src/roster/creatures.ts'
import { SimClient } from '../src/sim/client.ts'
import {
  claimIssuance, type CreatureSpec, DEPLOYMENT_CAP, type DeployedCreature,
  type Issuance, issueWave, settle,
} from '../src/wave/issuance.ts'
import { startTestDb, type TestDb } from './harness.ts'
import {
  clearWave, consumeLiveIssuance, type Deployed, liveIssuance, setupPlayer,
  startWave as start, startWaveRaw,
} from './wave-helpers.ts'

// serverId is always 1 in this file - the one server beforeAll creates.
const SERVER_ID = 1

// fileURLToPath, not .pathname - a path containing a space would arrive
// percent-encoded and every join below would miss.
const REPO = fileURLToPath(new URL('../../../', import.meta.url))
// 0.1.1, not 0.1.0: wave 6 carries no reward in 0.1.0, and rewardForWave has
// nothing to read there - see config/bundles/0.1.1 and the human ruling in
// task-5-brief.md Step 3 (0.1.0 is already published to GCS and must stay
// byte-identical to what shipped in Phase 4).
const SEED = join(REPO, 'config/bundles/0.1.1')

let t: TestDb
let deps: Deps
let app: ReturnType<typeof createApp>
let bundleRoot: string

beforeAll(async () => {
  t = await startTestDb()
  await t.ownerDb.insert(servers).values({
    serverId: 1, region: 'us-central1', state: 'open', tickDayOfWeek: 0, tickMinuteOfDay: 1200,
  })

  bundleRoot = await mkdtemp(join(tmpdir(), 'broodline-wave-start-'))
  const store = new LocalBundleStore(bundleRoot)
  await publishBundle(store, SEED, '0.1.1')
  await store.setPointer('0.1.1')
  clearBundleCache()

  // wave/start never calls sim - Task 5's route makes no use of simClient -
  // so this points nowhere reachable rather than standing up a real host.
  // Likewise never submits a wave, so the replay store is only ever asked
  // to exist.
  deps = {
    db: t.db, bundleStore: store, simClient: new SimClient('http://127.0.0.1:1', SimClient.noAuth('a deliberately dead address - no route under test here calls sim')),
    replayStore: new LocalReplayStore(bundleRoot),
  }
  app = createApp(deps)

  await setupPlayer(deps)
}, 240_000)

afterAll(async () => {
  await t?.stop()
  // Guarded: bundleRoot is assigned partway through beforeAll, so an
  // aborted beforeAll left this throwing ERR_INVALID_ARG_TYPE on top of the
  // real error and burying it. See wave-submit.test.ts's afterAll for the
  // full account, and masked-teardown.test.ts for the test.
  if (bundleRoot) await rm(bundleRoot, { recursive: true, force: true })
})

describe('POST /v1/wave/start', () => {
  it('issues a seed for the next uncleared wave', async () => {
    const res = await start(6)
    expect(res.status).toBe(200)
    const body = await res.json() as { issuanceId: string; seed: string; expiresAt: string }

    expect(body.issuanceId).toMatch(/^[0-9a-f-]{36}$/)
    // A STRING. The seed is a ulong; a JSON number loses the top bits and
    // the client would re-simulate against a different seed than the server
    // stored - which presents as a hash mismatch on an honest submission,
    // the single most misleading failure this phase could ship.
    expect(typeof body.seed).toBe('string')
    // Check 5's whole point, pinned directly rather than only implied by
    // `typeof === 'string'`: masked to 63 bits, so it is representable in
    // Postgres's SIGNED bigint (CHECK (seed >= 0)) without either erroring
    // on insert or sign-flipping into a different wave.
    const seed = BigInt(body.seed)
    expect(seed >= 0n && seed < 2n ** 63n).toBe(true)
    expect(new Date(body.expiresAt).getTime() - Date.now()).toBeGreaterThan(7_100_000)
  })

  it('returns the SAME issuance rather than minting a second', async () => {
    const first = await (await start(6)).json() as { issuanceId: string; seed: string }
    const second = await (await start(6)).json() as { issuanceId: string; seed: string }

    // design 2.1. Determinism is what makes verification cheap; it is also
    // what makes seed-shopping cheap, and this is the defence.
    expect(second.issuanceId).toBe(first.issuanceId)
    expect(second.seed).toBe(first.seed)
  })

  it('refuses a wave beyond the next one', async () => {
    const res = await start(20)
    expect(res.status).toBe(409)
    expect(await res.json()).toMatchObject({ code: 'wave_locked' })
  })

  it('refuses a wave absent from the bundle', async () => {
    // The bundle carries wave 6 only. A wave id the content does not define
    // is wave_locked, not a 500 - the bundle is the content source, per
    // solo_execution 5.2's content-versus-data split.
    const res = await start(1)
    expect([409]).toContain(res.status)
    expect(await res.json()).toMatchObject({ code: 'wave_locked' })
  })

  it('refuses a fourth replay of a cleared wave in one day', async () => {
    await clearWave(6) // helper: consume any live issuance and advance progress

    for (let i = 0; i < 3; i++) {
      expect((await start(6)).status).toBe(200)
      await consumeLiveIssuance()
    }

    // broodline_campaign_structure.md: three replays per wave per day, then
    // nothing until tomorrow. WITHOUT THIS CHECK wave 1 is farmable
    // indefinitely and re-simulation never notices, because every one of
    // those runs is honest - design 4.1 check 2.
    const res = await start(6)
    expect(res.status).toBe(429)
    expect(await res.json()).toMatchObject({ code: 'replay_cap_reached' })
  })

  it('refuses without a session', async () => {
    const res = await app.request('/v1/wave/start', {
      method: 'POST',
      headers: { 'content-type': 'application/json' },
      body: JSON.stringify({ waveId: 6 }),
    })
    expect(res.status).toBe(401)
  })

  it('refuses an already-passed wave id that the bundle does not author (check 3, replay branch)', async () => {
    // Reviewer minor finding: check 3's guard on the REPLAY branch
    // (issuance.ts's `if (!bundle.waves.some(...)) return wave_locked` at
    // the `waveId <= cleared` branch) was unreached by every other test -
    // 'refuses a wave absent from the bundle' exercises the FORWARD branch
    // instead (cleared is 0 there, so waveId 1 fails via "not next", never
    // via this line). Deleting that guard turns nothing red without this
    // test.
    //
    // A fresh player, cleared to 6 (the only authored wave) via clearWave -
    // waveId 5 is then <= cleared (the replay branch) but was never
    // authored by any bundle, so it must still be refused.
    await setupPlayer(deps)
    await clearWave(6)

    const res = await start(5)
    expect(res.status).toBe(409)
    expect(await res.json()).toMatchObject({ code: 'wave_locked' })
  })

  it('settles an abandoned (expired but never consumed) issuance and issues a fresh one', async () => {
    // design 4.3's amendment box, verbatim: a row that is settled_at IS
    // NULL but past its expires_at is the ABANDONED-wave path, not an
    // error. wave/start must settle it 'expired' (never 'consumed', which
    // would charge a replay never taken) and insert a new row in the same
    // transaction, or the one-live index locks the player out entirely -
    // the exact defect Task 4's C1 review finding was about. Nothing
    // exercised this path directly before now; it was only a load-bearing
    // side effect of clearWave()'s own cleanup elsewhere in this file.
    const { playerId: freshPlayerId } = await setupPlayer(deps)

    const firstBody = await (await start(6)).json() as { issuanceId: string }

    // Force the abandoned state directly - expired, but never settled -
    // via the owner connection (bypasses RLS; this is a fixture write a
    // handler would never itself perform, standing in for "two hours
    // passed").
    await t.ownerDb.execute(sql`
      UPDATE wave_issuances SET expires_at = now() - interval '1 second'
      WHERE issuance_id = ${firstBody.issuanceId}`)

    const res = await start(6)
    expect(res.status).toBe(200)
    const secondBody = await res.json() as { issuanceId: string }
    expect(secondBody.issuanceId).not.toBe(firstBody.issuanceId)

    const [old] = await t.ownerDb.select().from(waveIssuances)
      .where(eq(waveIssuances.issuanceId, firstBody.issuanceId))
    expect(old?.settlement).toBe('expired')
    expect(old?.settledAt).not.toBeNull()

    // design 4.3: only 'consumed' rows count toward the replay cap - the
    // abandoned row above must not have spent any of it. Asserted directly
    // rather than only inferred from clearWave() working elsewhere.
    const [row] = await t.ownerDb.select({ n: sql<number>`count(*)::int` })
      .from(waveIssuances)
      .where(and(
        eq(waveIssuances.playerId, freshPlayerId),
        eq(waveIssuances.waveId, 6),
        eq(waveIssuances.settlement, 'consumed')))
    expect(row!.n).toBe(0)
  })

  it('a conflicting insert on wave_issuances_one_live resolves with the existing row rather than aborting the transaction', async () => {
    // CRITICAL finding on review: an earlier version of this file tested
    // the concurrency translation with two raced HTTP requests
    // (Promise.all([start(6), start(6)])) asserting only "both 200, same
    // issuanceId" - which the serialized case (second request's check 4
    // simply finds the first request's already-committed row and returns
    // it, no INSERT attempted at all) satisfies identically to the raced
    // case. That version passed whether or not a real conflict ever
    // happened, and the reviewer's own empirical run showed it CAN
    // serialize rather than race, which is exactly the failure mode this
    // test now stops depending on.
    //
    // This calls issuance.ts's claimIssuance() directly, in isolation from
    // issueWave's check 4 (which is precisely what would short-circuit a
    // sequential second call before it ever reached the INSERT). Two REAL,
    // SEPARATE transactions, run one after the other rather than raced:
    // transaction 1 commits a live row, then transaction 2 attempts its
    // OWN insert for the identical (server_id, player_id) - guaranteed to
    // conflict on wave_issuances_one_live regardless of timing, which is
    // what makes this deterministic instead of relying on two HTTP
    // requests happening to overlap at the DB level.
    const { playerId: freshPlayerId } = await setupPlayer(deps)

    const first = await withServer(deps.db, SERVER_ID, (tx) =>
      claimIssuance(tx, SERVER_ID, freshPlayerId, 6, 111n, []))

    // Before the fix (a caught 23505 followed by a recovery SELECT on the
    // SAME now-aborted transaction), this call would REJECT with 25P02
    // rather than resolve - proving Critical 1's finding, not just
    // asserting the fixed behaviour.
    const second = await withServer(deps.db, SERVER_ID, (tx) =>
      claimIssuance(tx, SERVER_ID, freshPlayerId, 6, 222n, []))

    expect(second.issuance.issuanceId).toBe(first.issuance.issuanceId)
    expect(second.issuance.seed).toBe(first.issuance.seed)

    // `claimed` is the half Task 8 added, and it is what the caller commits
    // the deployment's creatures on: the loser is handed the WINNER's row,
    // whose deployment was resolved from the winner's own creatures, so
    // committing the loser's to it would garrison creatures that issuance
    // never deployed. Without this assertion the flag could be hard-coded
    // true and nothing here would notice.
    expect(first.claimed).toBe(true)
    expect(second.claimed).toBe(false)

    // The transaction the conflict happened inside must still be usable
    // afterwards - proof it was never poisoned, not merely that some value
    // came back.
    const stillReadable = await withServer(deps.db, SERVER_ID, (tx) =>
      tx.select().from(waveIssuances).where(eq(waveIssuances.issuanceId, first.issuance.issuanceId)))
    expect(stillReadable).toHaveLength(1)
  })

  // --- The waveId sanity bound (routes/wave.ts's parseStart).
  //
  // 400 AND 409 ARE DIFFERENT ANSWERS and the distinction is the whole
  // point: `invalid_request` says the request was malformed, `wave_locked`
  // says it was understood and refused. A client could not tell those apart
  // for an absurd waveId, because every one of them reached `issueWave` -
  // and paid for a campaign_progress lookup and a bundle scan on the way to
  // being told 409.
  //
  // Nothing in `issueWave` can answer `invalid_request`: its only two
  // refusals are wave_locked and replay_cap_reached. So a 400 here IS the
  // proof that the parse layer answered, without a second assertion about
  // where the answer came from.
  //
  // The two route tests below never reach the database - the refusal is
  // formed before any query - so they leave this file's chained fixture
  // state exactly as they found it. The third one does touch it, and runs
  // last for that reason.

  it('refuses a waveId above what the schema could store, as malformed rather than locked', async () => {
    // wave_issuances.wave_id is a Postgres `integer`, so 2^53-1 is not a
    // wave that happens to be unavailable - it is not a wave id this system
    // could hold under any bundle.
    const res = await start(Number.MAX_SAFE_INTEGER)
    expect(res.status).toBe(400)
    expect(await res.json()).toMatchObject({ code: 'invalid_request' })
  })

  it('refuses a non-positive waveId as malformed rather than locked', async () => {
    // The floor, from the other end and for the same reason: no bundle can
    // author wave 0 or wave -1, so the answer does not depend on content.
    const negative = await start(-1)
    expect(negative.status).toBe(400)
    expect(await negative.json()).toMatchObject({ code: 'invalid_request' })

    const zero = await start(0)
    expect(zero.status).toBe(400)
    expect(await zero.json()).toMatchObject({ code: 'invalid_request' })
  })

  it('issueWave still refuses a non-positive waveId when called directly', async () => {
    // The flip side of the bound above. `/v1/wave/start` is issueWave's only
    // caller (src/routes/wave.ts), so with the parse bound in place nothing
    // in the running service reaches issueWave with a waveId below 1 any
    // more. This pins the OUTCOME at the function's own boundary, so that
    // moving the check up a layer did not quietly change what issueWave
    // answers for the ids it can still be handed directly.
    //
    // WHAT THIS DOES NOT COVER, stated because the reverse is the easy thing
    // to assume: it is NOT a gate on issuance.ts's `if (waveId < 1) return
    // { refused: 'wave_locked' }` line. Deleting that line leaves this test
    // green - measured, not reasoned - because any waveId below 1 is also
    // <= cleared (cleared is never negative), so control falls into the
    // REPLAY branch and check 3 refuses the same ids with the same
    // wave_locked for a different reason: no bundle authors wave 0 or wave
    // -1. That line is a short-circuit worth keeping - it saves the replay
    // branch's count query - but it is not what produces the refusal, and
    // this test should not be read as covering it.
    //
    // No live issuance is created either way: both calls refuse before
    // check 4's select and check 5's insert, which is why they can share one
    // player and one transaction.
    const { playerId } = await setupPlayer(deps)
    const bundle = await loadBundle(deps.bundleStore)

    const refusals = await withServer(deps.db, SERVER_ID, async (tx) => [
      await issueWave(tx, SERVER_ID, playerId, 0, [], bundle),
      await issueWave(tx, SERVER_ID, playerId, -1, [], bundle),
    ])

    expect(refusals).toEqual([{ refused: 'wave_locked' }, { refused: 'wave_locked' }])
  })
})

/**
 * Design §6.1 - the mechanism the whole phase turns on.
 *
 * Phase 5 shipped a knowingly-open hole: `api` re-simulated a replay and
 * verified its arithmetic, and had no idea whether the player owned the
 * creatures in it. It marked the hole with a deliberately-passing test
 * (adversarial.test.ts's `CAN still deploy creatures the player does not own
 * - Phase 6`).
 *
 * WHAT CLOSES IT IS NOT THE CHECK. `wave/start` takes creature IDS; the spec
 * it stores is read off the row each id names. There is no path from a
 * client-supplied value to a `CreatureSpec`, so an unowned deployment is not
 * REFUSED - it is INEXPRESSIBLE. `resolves every spec from the OWNED ROW`
 * below is the test that pins the mechanism rather than the check: it sends
 * trait fields in the body and asserts the stored spec ignored them. The
 * refusals are the cheap path (ownership costs no simulation); they are not
 * the guarantee.
 *
 * EVERY REFUSAL ASSERTS ON STATE, not only on a status code - splice-commit's
 * rule, and for the same reason: a route that refused everything would pass a
 * status-code suite perfectly. What is worth guaranteeing is that a refused
 * `wave/start` ISSUED NOTHING and COMMITTED NOTHING.
 */
describe('POST /v1/wave/start — the deployment is fixed at issuance (design §6.1)', () => {
  type CreatureRow = typeof creatures.$inferSelect

  let mine: CreatureRow[]
  let notMine: CreatureRow

  /**
   * A creature, written through the OWNER connection - splice-commit.test.ts's
   * idiom, and for its reason: `grantBaseStock` is the only `insert(creatures)`
   * on a grant path and it mints Gen-1 Tier-I base stock with no way to ask for
   * a species, a trait or a state.
   */
  async function give(owner: string, o: {
    species?: string
    trait1?: string; tier1?: number | null
    trait2?: string; tier2?: number | null
    instinct?: string
    committedTo?: string | null
    consumed?: boolean
    pruned?: boolean
  } = {}): Promise<CreatureRow> {
    const species = o.species ?? 'Vetch'
    const [row] = await t.ownerDb.insert(creatures).values({
      serverId: SERVER_ID,
      playerId: owner,
      species,
      generation: 1,
      trait1: o.trait1 ?? 'Taunt', tier1: o.tier1 === undefined ? 1 : o.tier1,
      trait2: o.trait2 ?? 'Carapace', tier2: o.tier2 === undefined ? 1 : o.tier2,
      instinct: o.instinct ?? 'Vanguard',
      hpCurrent: creatureHp(species),
      isFounder: false,
      committedTo: o.committedTo ?? null,
      // design §3.2's THIRD state. A pruned row is also consumed - 0005's
      // pruned_creatures_are_stripped is what makes the tombstone a tombstone
      // rather than a live creature wearing a flag.
      consumedAt: o.consumed === true || o.pruned === true ? new Date('2026-09-01T00:00:00Z') : null,
    }).returning()
    const created = row!
    if (o.pruned === true) {
      await t.ownerDb.update(creatures)
        .set({
          pruned: true, trait1: null, tier1: null, trait2: null, tier2: null,
          instinct: null, name: null, hpCurrent: null, regenUntil: null, committedTo: null,
        })
        .where(and(eq(creatures.serverId, SERVER_ID), eq(creatures.creatureId, created.creatureId)))
    }
    return created
  }

  async function reread(ids: string[]): Promise<Map<string, CreatureRow>> {
    const rows = await t.ownerDb.select().from(creatures)
      .where(and(eq(creatures.serverId, SERVER_ID), inArray(creatures.creatureId, ids)))
    return new Map(rows.map((r) => [r.creatureId, r]))
  }

  const deploymentOf = (cs: CreatureRow[]): Deployed[] =>
    cs.map((c, pocket) => ({ creatureId: c.creatureId, pocket }))

  const storedDeployment = async (): Promise<CreatureSpec[] | null | undefined> =>
    (await liveIssuance())?.deployment

  /** Settles the live issuance the OTHER terminal way - design §4.3's abandoned wave. */
  async function expireLiveIssuance(): Promise<void> {
    const live = await liveIssuance()
    if (live === undefined) throw new Error('expireLiveIssuance: nothing live')
    await withServer(deps.db, SERVER_ID, (tx) => settle(tx, live, 'expired'))
  }

  beforeEach(async () => {
    // The other player is made FIRST: setupPlayer rebinds wave-helpers'
    // module-level token, so the player these tests drive must be the last
    // one created.
    const { playerId: otherId } = await setupPlayer(deps)
    notMine = await give(otherId)

    const { playerId } = await setupPlayer(deps)
    // Six, so the cap test has one too many. Species and traits differ per
    // creature so that "the stored spec came from THIS row" is a question
    // with a distinguishable answer - six identical creatures would make
    // every pairing look correct.
    mine = [
      await give(playerId, { species: 'Vetch', trait1: 'Taunt', tier1: 2 }),
      await give(playerId, { species: 'Pale', trait1: 'Chill', tier1: 1 }),
      await give(playerId, { species: 'Ember', trait1: 'Splash', tier1: 3 }),
      await give(playerId, { species: 'Vetch', trait1: 'Carapace', tier1: null }),
      await give(playerId, { species: 'Pale', trait1: 'Chill', tier1: 3, instinct: 'Vanguard' }),
      await give(playerId, { species: 'Ember', trait1: 'Splash', tier1: 2 }),
    ]
  })

  it('refuses a creature the player does not own, and issues nothing', async () => {
    const res = await start(6, [{ creatureId: notMine.creatureId, pocket: 0 }])

    expect(res.status).toBe(409)
    expect(await res.json()).toMatchObject({ code: 'creature_not_owned' })
    // THE ASSERTION WITH TEETH. A 409 alone is satisfied by a route that
    // refuses everything; what design §6.1 promises is that a refused start
    // issued NOTHING and committed NOTHING.
    expect(await liveIssuance()).toBeUndefined()
    expect((await reread([notMine.creatureId])).get(notMine.creatureId)!.committedTo).toBeNull()
  })

  it('refuses a mix of owned and unowned as a whole - nothing partial is issued', async () => {
    const res = await start(6, deploymentOf([mine[0]!, mine[1]!, notMine]))

    expect(res.status).toBe(409)
    expect(await res.json()).toMatchObject({ code: 'creature_not_owned' })
    expect(await liveIssuance()).toBeUndefined()
    const after = await reread(mine.map((c) => c.creatureId))
    for (const c of mine) expect(after.get(c.creatureId)!.committedTo).toBeNull()
  })

  it('refuses a creature that is DEAD, both ways it can be dead', async () => {
    // `committed_to IS NULL` is true of BOTH kinds of dead row, which is why
    // roster/creatures.ts's liveCreature() is the one definition and a
    // hand-rolled predicate here would reintroduce this task's own hole from
    // the other direction. A CONSUMED parent is not pruned and is WHOLE, so
    // it would resolve to a perfectly valid spec; a PRUNED one has no traits
    // at all.
    const { playerId } = await setupPlayer(deps)
    const consumed = await give(playerId, { consumed: true })
    const pruned = await give(playerId, { pruned: true })
    const alive = await give(playerId)

    for (const dead of [consumed, pruned]) {
      const res = await start(6, deploymentOf([alive, dead]))
      expect(res.status).toBe(409)
      expect(await res.json()).toMatchObject({ code: 'creature_not_owned' })
      expect(await liveIssuance()).toBeUndefined()
    }
  })

  it('refuses a creature already committed to another issuance, and issues nothing', async () => {
    // Wave 6 is the only authored wave, so the second start is a REPLAY of
    // it rather than the brief's wave 7 - which `issueWave` would have
    // refused as wave_locked before ever reaching the ownership checks, and
    // the test would have passed for the wrong reason.
    await clearWave(6)
    expect((await start(6, deploymentOf(mine.slice(0, 5)))).status).toBe(200)
    const held = (await liveIssuance())!.issuanceId
    await consumeLiveIssuance()

    // The release happened at settle, so put one back by hand - standing in
    // for "this creature is out fighting for an issuance of its own".
    const other = randomUUID()
    await t.ownerDb.update(creatures).set({ committedTo: other })
      .where(and(eq(creatures.serverId, SERVER_ID), eq(creatures.creatureId, mine[0]!.creatureId)))

    const res = await start(6, deploymentOf(mine.slice(0, 5)))
    expect(res.status).toBe(409)
    expect(await res.json()).toMatchObject({ code: 'creature_committed' })
    expect(await liveIssuance()).toBeUndefined()

    // The commitment it refused on is untouched, and the four it did not
    // refuse on were never taken.
    const after = await reread(mine.map((c) => c.creatureId))
    expect(after.get(mine[0]!.creatureId)!.committedTo).toBe(other)
    expect(other).not.toBe(held)
    for (const c of mine.slice(1)) expect(after.get(c.creatureId)!.committedTo).toBeNull()
  })

  it('refuses more than the deployment cap, as malformed rather than locked', async () => {
    // engine/Runtime/Combat/Stats.cs's DeploymentCap is 5, and a sixth
    // creature is not a deployment that happens to be unavailable - it is
    // one no roster state and no bundle could ever make legal. Same
    // distinction MAX_WAVE_ID draws: 400 says malformed, 409 says understood
    // and refused.
    expect(DEPLOYMENT_CAP).toBe(5)
    expect(mine).toHaveLength(6)

    const res = await start(6, deploymentOf(mine))
    expect(res.status).toBe(400)
    expect(await res.json()).toMatchObject({ code: 'invalid_request' })
    expect(await liveIssuance()).toBeUndefined()

    // And the cap is a CEILING, not a required size - exactly five is fine.
    expect((await start(6, deploymentOf(mine.slice(0, 5)))).status).toBe(200)
  })

  it('refuses the same creature named twice, as malformed rather than unowned', async () => {
    // A body naming one creature in two pockets asks for two creatures out
    // of one. It IS owned, so `creature_not_owned` would be a refusal that
    // misstates its own reason; the body is malformed by construction, the
    // same way an over-cap one is. Checked on the NORMALISED ids, because
    // Postgres `uuid` equality is case-insensitive and JS `===` is not -
    // without that, `[id, id.toUpperCase()]` reads as two distinct ids and
    // falls through to a misleading 409.
    const id = mine[0]!.creatureId
    for (const second of [id, id.toUpperCase()]) {
      const res = await startWaveRaw({
        waveId: 6,
        deployment: [{ creatureId: id, pocket: 0 }, { creatureId: second, pocket: 1 }],
      })
      expect(res.status).toBe(400)
      expect(await res.json()).toMatchObject({ code: 'invalid_request' })
      expect(await liveIssuance()).toBeUndefined()
    }
  })

  it('resolves every spec from the OWNED ROW, not from the request', async () => {
    // DESIGN §2.1, AND THE TEST THIS TASK EXISTS FOR. "There is no path from
    // a client-supplied value to a spec." The request carries ids and
    // pockets; a client that sends trait fields must not be able to
    // influence what is stored, so this one sends them.
    const row = mine[1]! // Pale / Chill / tier 1
    const res = await startWaveRaw({
      waveId: 6,
      deployment: [{
        creatureId: row.creatureId, pocket: 2,
        species: 'Skitter', trait1: 'Chill', tier1: 3, trait2: 'Chill', tier2: 3,
        instinct: 'Ravager',
      }],
    })
    expect(res.status).toBe(200)

    const stored = (await storedDeployment())!
    expect(stored).toHaveLength(1)
    // The ROW's values, named against the row rather than against literals
    // retyped here - a literal would agree with itself and with nothing else.
    expect(stored[0]).toMatchObject({
      species: row.species,
      trait1: row.trait1, tier1: row.tier1,
      trait2: row.trait2, tier2: row.tier2,
      instinct: row.instinct,
    })
    // And NOT the request's, stated separately so the assertion above cannot
    // pass by the two happening to coincide.
    expect(stored[0]!.species).not.toBe('Skitter')
    expect(stored[0]!.tier1).not.toBe(3)
    expect(stored[0]!.instinct).not.toBe('Ravager')
    // The pocket IS the client's to choose, and is the only field of the
    // spec that is. Asserting it pins that "resolve from the row" did not
    // degenerate into "ignore the request".
    expect(stored[0]!.pocket).toBe(2)

    // The response carries what was stored, so the client never has to guess.
    expect(await res.json()).toMatchObject({ deployment: stored })
  })

  it('resolves from the row even when issueWave is handed trait fields DIRECTLY', async () => {
    // THE MECHANISM HAS TWO INDEPENDENT HALVES and the HTTP test above can
    // only see one of them. `parseDeployment` CONSTRUCTS each entry out of
    // exactly { creatureId, pocket }, so trait fields in a body never reach
    // `issueWave` at all - which means inverting the OTHER half, and reading
    // the spec out of the request instead of the row, leaves every
    // HTTP-driven test in this file green. Measured, not supposed.
    //
    // So this one goes under the parse layer and hands `issueWave` the
    // object a forwarding parse layer would have built. It is what makes
    // "the spec comes from the row" load-bearing on its own terms, rather
    // than only in combination with the strip.
    const { playerId } = await setupPlayer(deps)
    const row = await give(playerId, { species: 'Pale', trait1: 'Chill', tier1: 1 })
    const bundle = await loadBundle(deps.bundleStore)

    const asked = {
      creatureId: row.creatureId, pocket: 0,
      species: 'Skitter', trait1: 'Taunt', tier1: 3, trait2: 'Chill', tier2: 3,
      instinct: 'Ravager',
    } as DeployedCreature

    const issued = await withServer(deps.db, SERVER_ID, (tx) =>
      issueWave(tx, SERVER_ID, playerId, 6, [asked], bundle))
    expect(issued).not.toHaveProperty('refused')

    // toEqual, not toMatchObject: the stored spec must be EXACTLY the seven
    // fields of a CreatureSpec, so a resolution that spread the request in
    // and let the row's values win on the overlapping keys still fails here
    // on the extra ones.
    expect((issued as Issuance).deployment).toEqual([{
      species: row.species,
      trait1: row.trait1, tier1: row.tier1,
      trait2: row.trait2, tier2: row.tier2,
      instinct: row.instinct,
      pocket: 0,
    }])
  })

  it('pairs each pocket with the creature the REQUEST named, not with a row in some other order', async () => {
    // A resolution written as `owned.map((c, i) => ({ ...c, pocket:
    // deployment[i].pocket }))` is only correct if the rows come back in
    // request order, and a `WHERE creature_id IN (...)` promises no order at
    // all. Sent in DESCENDING id order on purpose, so that a resolution
    // keyed on the rows' own order - sorted, or whatever the plan produced -
    // mispairs every pocket DETERMINISTICALLY rather than on a coin flip.
    const byIdDesc = [...mine.slice(0, 3)].sort((a, b) => (a.creatureId < b.creatureId ? 1 : -1))
    const res = await start(6, deploymentOf(byIdDesc))
    expect(res.status).toBe(200)

    const stored = (await storedDeployment())!
    expect(stored).toHaveLength(3)
    byIdDesc.forEach((c, pocket) => {
      expect(stored[pocket]).toMatchObject({
        species: c.species, trait1: c.trait1, tier1: c.tier1, pocket,
      })
    })
  })

  it('sets committed_to on every deployed creature, and clears it when the issuance is CONSUMED', async () => {
    const deployed = mine.slice(0, 5)
    const res = await start(6, deploymentOf(deployed))
    expect(res.status).toBe(200)
    const { issuanceId } = await res.json() as { issuanceId: string }

    const committed = await reread(deployed.map((c) => c.creatureId))
    for (const c of deployed) expect(committed.get(c.creatureId)!.committedTo).toBe(issuanceId)
    // The one creature that was NOT deployed is untouched - the commit is
    // scoped to the deployment, not to the roster.
    expect((await reread([mine[5]!.creatureId])).get(mine[5]!.creatureId)!.committedTo).toBeNull()

    await consumeLiveIssuance()

    const released = await reread(deployed.map((c) => c.creatureId))
    for (const c of deployed) expect(released.get(c.creatureId)!.committedTo).toBeNull()
  })

  it('clears committed_to when the issuance EXPIRES too - both terminal states free the roster', async () => {
    // design §2.5: `committed_to` is what stops a creature that is out
    // fighting being spliced away. A release wired to the consumed path
    // alone would strand every abandoned wave's deployment permanently -
    // the player could never splice those five creatures again, and nothing
    // on the consumed path would ever notice.
    const deployed = mine.slice(0, 3)
    const res = await start(6, deploymentOf(deployed))
    expect(res.status).toBe(200)
    const { issuanceId } = await res.json() as { issuanceId: string }

    // Asserted BEFORE the expiry, and not as decoration: without it this
    // test passes vacuously against a server that never commits anything at
    // all - which is exactly what it did before the implementation landed.
    const committed = await reread(deployed.map((c) => c.creatureId))
    for (const c of deployed) expect(committed.get(c.creatureId)!.committedTo).toBe(issuanceId)

    await expireLiveIssuance()

    const released = await reread(deployed.map((c) => c.creatureId))
    for (const c of deployed) expect(released.get(c.creatureId)!.committedTo).toBeNull()
  })

  it('a second start returns the FIRST issuance and its stored deployment, and commits nothing new', async () => {
    // design §2.1's live-issuance rule, extended by one field: the
    // deployment is fixed AT ISSUANCE, so a second call asking for a
    // different one gets the one it already has. Without this the ownership
    // checks would sit in front of check 4 and the player's own second call
    // would be refused `creature_committed` by their own live issuance.
    const first = await start(6, deploymentOf(mine.slice(0, 2)))
    expect(first.status).toBe(200)
    const firstBody = await first.json() as { issuanceId: string; deployment: CreatureSpec[] }

    // THE HONEST DOUBLE-TAP FIRST, with the SAME creatures - and it is this
    // half that pins the ORDER of the checks. Those two creatures are now
    // committed to the live issuance, so roster checks running in front of
    // check 4 refuse the player's own repeat with `creature_committed`.
    // Measured: with the second call naming DIFFERENT creatures instead,
    // moving the checks ahead of check 4 leaves this test green.
    const repeat = await start(6, deploymentOf(mine.slice(0, 2)))
    expect(repeat.status).toBe(200)
    expect(await repeat.json()).toMatchObject({ issuanceId: firstBody.issuanceId })

    const second = await start(6, deploymentOf(mine.slice(2, 5)))
    expect(second.status).toBe(200)
    const secondBody = await second.json() as { issuanceId: string; deployment: CreatureSpec[] }

    expect(secondBody.issuanceId).toBe(firstBody.issuanceId)
    expect(secondBody.deployment).toEqual(firstBody.deployment)
    expect(secondBody.deployment).toHaveLength(2)

    // And the three creatures the second call named were never committed.
    const after = await reread(mine.map((c) => c.creatureId))
    for (const c of mine.slice(2)) expect(after.get(c.creatureId)!.committedTo).toBeNull()
    for (const c of mine.slice(0, 2)) {
      expect(after.get(c.creatureId)!.committedTo).toBe(firstBody.issuanceId)
    }
  })

  it('re-deploys the SAME creatures after an issuance is abandoned', async () => {
    // design §4.3's abandoned-wave path, now that it has a roster to free.
    // check 4 settles the expired row 'expired' and the settlement releases
    // its creatures; only then is the new deployment resolved. Get that
    // order wrong - resolve before check 4, which is the obvious way to put
    // "the cheap refusal first" - and the player is locked out of their own
    // roster by their own abandoned wave, permanently: every retry finds the
    // same five creatures still committed to an issuance nothing will ever
    // settle, because the thing that would settle it is the call being
    // refused. That is Task 4's C1 finding again, one table over.
    const deployed = mine.slice(0, 5)
    const firstBody = await (await start(6, deploymentOf(deployed))).json() as { issuanceId: string }

    // Expired but never settled - exactly what backgrounding the app
    // mid-wave leaves behind. Through the owner connection: a fixture write
    // standing in for "two hours passed".
    await t.ownerDb.execute(sql`
      UPDATE wave_issuances SET expires_at = now() - interval '1 second'
      WHERE issuance_id = ${firstBody.issuanceId}`)

    const res = await start(6, deploymentOf(deployed))
    expect(res.status).toBe(200)
    const secondBody = await res.json() as { issuanceId: string; deployment: CreatureSpec[] }
    expect(secondBody.issuanceId).not.toBe(firstBody.issuanceId)
    expect(secondBody.deployment).toHaveLength(5)

    // And they are committed to the NEW issuance, not still to the old one.
    const after = await reread(deployed.map((c) => c.creatureId))
    for (const c of deployed) {
      expect(after.get(c.creatureId)!.committedTo).toBe(secondBody.issuanceId)
    }
  })

  it('holds the deployed creatures under a row lock from resolution until the commitment', async () => {
    // design §2.5's race, which is the reason `loadOwnedCreatures` reads
    // FOR UPDATE: the deployment stored on an issuance is resolved FROM
    // these rows, so a splice destroying one between that read and the
    // commit would leave a deployment that outlived the roster it came from.
    //
    // THE WAITER IS THE ASSERTION, not a diagnostic - adversarial.test.ts's
    // own rule about the same idiom. The statement below is LITERALLY the
    // one splice/commit.ts's `lockParents` issues (a locking read of one
    // creature by id), so "it blocked here" is a statement about the splice
    // path and not about some UPDATE invented for the test. Drop the
    // `.for('update')` in loadOwnedCreatures and nothing blocks: sawWaiter
    // stays false and this test fails.
    const { playerId } = await setupPlayer(deps)
    const ours = [await give(playerId), await give(playerId)]
    const bundle = await loadBundle(deps.bundleStore)

    let sawWaiter = false
    let waiter: Promise<CreatureRow[]> | undefined

    const issued = await withServer(deps.db, SERVER_ID, (tx) =>
      issueWave(tx, SERVER_ID, playerId, 6, deploymentOf(ours), bundle, {
        beforeClaim: async () => {
          // Started, NOT awaited: under the lock it cannot finish until this
          // transaction commits, so awaiting it here would deadlock the test
          // against the very lock it is checking for.
          //
          // `.execute()` RATHER THAN THE BARE BUILDER, and that is not
          // stylistic: a drizzle query builder is a lazy thenable, so
          // assigning one to a variable issues no SQL at all and there is
          // nothing for the poll below to see. Written the obvious way first,
          // and it failed with sawWaiter false against a lock that was in
          // fact held.
          waiter = t.ownerDb.select().from(creatures)
            .where(and(
              eq(creatures.serverId, SERVER_ID),
              eq(creatures.creatureId, ours[0]!.creatureId),
            ))
            .for('update')
            .execute()

          for (let i = 0; i < 200; i++) {
            const res = await t.ownerDb.execute(sql`
              SELECT count(*)::int AS n FROM pg_stat_activity
              WHERE wait_event_type = 'Lock' AND query ILIKE '%creatures%'`)
            if (((res.rows[0] as { n: number } | undefined)?.n ?? 0) > 0) {
              sawWaiter = true
              break
            }
            await new Promise((r) => setTimeout(r, 25))
          }
        },
      }))

    expect(sawWaiter).toBe(true)
    expect(issued).not.toHaveProperty('refused')

    // And when the waiter IS released, it sees the commitment - which is
    // exactly what splice/commit.ts's `a.committedTo !== null` then refuses
    // on. The lock does not merely delay the splice; it makes the splice
    // read the state this transaction wrote.
    const [seen] = await waiter!
    expect(seen!.committedTo).toBe((issued as Issuance).issuanceId)
  })

  it('commits NOTHING when it loses the insert to a concurrent claim', async () => {
    // `claimIssuance` hands a loser the WINNER's row - design §2.1's
    // one-live rule enforced by wave_issuances_one_live, and the reason that
    // function answers with `claimed` rather than a bare row. The winner's
    // deployment was resolved from the winner's OWN creatures, so committing
    // the loser's to it would garrison five creatures that issuance never
    // deployed and that nothing but its settlement would ever release.
    //
    // Only reachable through the hook. The two racers get this far at all
    // only with DISJOINT deployments (overlapping ones serialize on
    // loadOwnedCreatures' FOR UPDATE and the loser refuses
    // creature_committed), which is exactly the case where the winner's
    // deployment says nothing about the loser's creatures.
    const { playerId } = await setupPlayer(deps)
    const ours = [await give(playerId), await give(playerId)]
    const bundle = await loadBundle(deps.bundleStore)

    let winnerId: string | undefined
    const loser = await withServer(deps.db, SERVER_ID, (tx) =>
      issueWave(tx, SERVER_ID, playerId, 6, deploymentOf(ours), bundle, {
        // A SEPARATE transaction, committed before this one resumes, so the
        // conflict is guaranteed rather than timing-dependent.
        beforeClaim: async () => {
          const claim = await withServer(deps.db, SERVER_ID, (tx2) =>
            claimIssuance(tx2, SERVER_ID, playerId, 6, 777n, []))
          winnerId = claim.issuance.issuanceId
        },
      }))

    expect('refused' in loser).toBe(false)
    // It returned the winner's row, which is design §2.1 working.
    expect((loser as Issuance).issuanceId).toBe(winnerId)

    // And it committed nothing. THIS is the assertion the `claimed` gate
    // exists for; without it both creatures read back as committed to an
    // issuance whose deployment is empty.
    const after = await reread(ours.map((c) => c.creatureId))
    for (const c of ours) expect(after.get(c.creatureId)!.committedTo).toBeNull()
  })

  it('refuses a creatureId that is not a uuid as malformed, and a fabricated one as unowned', async () => {
    // http/ids.ts's rule at a fourth call site. Without the shape check the
    // id reaches a Postgres `uuid` comparison, which raises 22P02 and
    // app.ts's onError turns into `internal` - telling an authenticated
    // caller the server broke for a body they malformed. The positive
    // control is the second half: a well-formed but fabricated id must
    // still get the route's ORDINARY refusal, or a check that swallowed
    // every unknown id into 400 would pass the first half alone.
    const malformed = await startWaveRaw({
      waveId: 6, deployment: [{ creatureId: 'not-a-uuid', pocket: 0 }],
    })
    expect(malformed.status).toBe(400)
    expect(await malformed.json()).toMatchObject({ code: 'invalid_request' })

    const fabricated = await start(6, [{ creatureId: randomUUID(), pocket: 0 }])
    expect(fabricated.status).toBe(409)
    expect(await fabricated.json()).toMatchObject({ code: 'creature_not_owned' })

    // And an UPPER-CASE id the player really owns is accepted, because
    // normalizeUuid lower-cases rather than rejects - the positive control
    // Task 7 learned to write, so "normalise" cannot degenerate into
    // "refuse upper case".
    const upper = await start(6, [{ creatureId: mine[0]!.creatureId.toUpperCase(), pocket: 0 }])
    expect(upper.status).toBe(200)
    expect((await storedDeployment())![0]).toMatchObject({ species: mine[0]!.species })
  })

  it('refuses a pocket that is not a non-negative int32', async () => {
    // The FLOOR and the CEILING are content-independent and the range
    // between them is not, which is why only the two ends are here.
    // Deployments.Problem refuses a negative pocket on every lane there
    // could ever be, and Replay.cs writes the pocket as an int32 - so a
    // value outside that range cannot be expressed in a replay under any
    // terrain. Which pockets a lane actually HAS (Defile's five) is
    // content, and `sim` stays the only authority on it, exactly as
    // `issueWave` stays the only authority on which waves exist.
    for (const pocket of [-1, 1.5, 2_147_483_648, 'x']) {
      const res = await startWaveRaw({
        waveId: 6, deployment: [{ creatureId: mine[0]!.creatureId, pocket }],
      })
      expect(res.status, `pocket ${String(pocket)}`).toBe(400)
      expect(await res.json()).toMatchObject({ code: 'invalid_request' })
    }
    expect(await liveIssuance()).toBeUndefined()
  })

  it('requires the deployment field at all', async () => {
    // design §6.1 grows the body. A start with no deployment is not "deploy
    // nothing" by default - it is a body written against the old contract,
    // and answering it 200 would put an issuance in flight whose stored
    // deployment nothing ever chose.
    const res = await startWaveRaw({ waveId: 6 })
    expect(res.status).toBe(400)
    expect(await res.json()).toMatchObject({ code: 'invalid_request' })
    expect(await liveIssuance()).toBeUndefined()
  })
})
