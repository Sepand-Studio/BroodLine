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
import { servers, waveIssuances } from '../src/db/schema.ts'
import { LocalReplayStore } from '../src/replays/store.ts'
import { SimClient } from '../src/sim/client.ts'
import { claimIssuance } from '../src/wave/issuance.ts'
import { startTestDb, type TestDb } from './harness.ts'
import {
  clearWave, consumeLiveIssuance, setupPlayer, startWave as start,
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
    db: t.db, bundleStore: store, simClient: new SimClient('http://127.0.0.1:1'),
    replayStore: new LocalReplayStore(bundleRoot),
  }
  app = createApp(deps)

  await setupPlayer(deps)
}, 240_000)

afterAll(async () => {
  await t?.stop()
  await rm(bundleRoot, { recursive: true, force: true })
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
      claimIssuance(tx, SERVER_ID, freshPlayerId, 6, 111n))

    // Before the fix (a caught 23505 followed by a recovery SELECT on the
    // SAME now-aborted transaction), this call would REJECT with 25P02
    // rather than resolve - proving Critical 1's finding, not just
    // asserting the fixed behaviour.
    const second = await withServer(deps.db, SERVER_ID, (tx) =>
      claimIssuance(tx, SERVER_ID, freshPlayerId, 6, 222n))

    expect(second.issuanceId).toBe(first.issuanceId)
    expect(second.seed).toBe(first.seed)

    // The transaction the conflict happened inside must still be usable
    // afterwards - proof it was never poisoned, not merely that some value
    // came back.
    const stillReadable = await withServer(deps.db, SERVER_ID, (tx) =>
      tx.select().from(waveIssuances).where(eq(waveIssuances.issuanceId, first.issuanceId)))
    expect(stillReadable).toHaveLength(1)
  })
})
