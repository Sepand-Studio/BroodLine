import { eq, sql } from 'drizzle-orm'
import { afterAll, beforeAll, describe, expect, it } from 'vitest'
import { withServer } from '../src/db/client.ts'
import {
  accounts, campaignProgress, idempotencyKeys, ledger, players, servers, waveIssuances, wallets,
} from '../src/db/schema.ts'
import { startTestDb, type TestDb } from './harness.ts'

const SERVER_A = 1
const SERVER_B = 2

let t: TestDb
let playerA: string
let playerB: string
let accountB: string

beforeAll(async () => {
  t = await startTestDb()

  // Fixtures go in as the OWNER, because seeding two servers is precisely
  // what a policy-bound connection is not allowed to do.
  await t.ownerDb.insert(servers).values([
    { serverId: SERVER_A, region: 'us-central1', state: 'open', tickDayOfWeek: 0, tickMinuteOfDay: 1200 },
    { serverId: SERVER_B, region: 'us-central1', state: 'open', tickDayOfWeek: 0, tickMinuteOfDay: 1217 },
  ])

  const [accA] = await t.ownerDb.insert(accounts)
    .values({ birthdateBand: 'adult', homeRegion: 'us-central1', serverId: SERVER_A }).returning()
  const [accB] = await t.ownerDb.insert(accounts)
    .values({ birthdateBand: 'adult', homeRegion: 'us-central1', serverId: SERVER_B }).returning()
  accountB = accB!.accountId

  const [pA] = await t.ownerDb.insert(players)
    .values({ serverId: SERVER_A, accountId: accA!.accountId }).returning()
  const [pB] = await t.ownerDb.insert(players)
    .values({ serverId: SERVER_B, accountId: accB!.accountId }).returning()
  playerA = pA!.playerId
  playerB = pB!.playerId

  await t.ownerDb.insert(wallets).values([
    { serverId: SERVER_A, playerId: playerA, currency: 'shards', balance: 100 },
    { serverId: SERVER_B, playerId: playerB, currency: 'shards', balance: 999 },
  ])
  await t.ownerDb.insert(ledger).values([
    { serverId: SERVER_A, playerId: playerA, currency: 'shards', delta: 100, balanceAfter: 100, reasonCode: 'FIXTURE' },
    { serverId: SERVER_B, playerId: playerB, currency: 'shards', delta: 999, balanceAfter: 999, reasonCode: 'FIXTURE' },
  ])
  // idempotency_keys and campaign_progress previously had no fixture rows
  // and no behavioural assertion anywhere - their policies were attested
  // only by metadata, and metadata says a policy exists, not that it
  // discriminates. Seeded here so 'shows a scoped read only its own
  // server' below actually exercises all five server-scoped tables.
  await t.ownerDb.insert(idempotencyKeys).values([
    { serverId: SERVER_A, key: 'fixture-a', requestHash: 'hash-a', status: 'completed' },
    { serverId: SERVER_B, key: 'fixture-b', requestHash: 'hash-b', status: 'completed' },
  ])
  await t.ownerDb.insert(campaignProgress).values([
    { serverId: SERVER_A, playerId: playerA, highestWaveCleared: 3, milestonesClaimed: 1 },
    { serverId: SERVER_B, playerId: playerB, highestWaveCleared: 9, milestonesClaimed: 4 },
  ])
  // wave_issuances is new since Task 4. A policy that merely exists but
  // compares the wrong column - or an omitted policy under ENABLE/FORCE
  // alone - would pass 'is invisible without a server scope' identically to
  // a correct one, exactly the gap the repo already learned about
  // idempotencyKeys and campaignProgress above. Seeded here so the scoped
  // cross-server read below actually exercises discrimination on this
  // table, not just default-deny.
  await t.ownerDb.insert(waveIssuances).values([
    {
      serverId: SERVER_A, issuanceId: '11111111-0000-0000-0000-000000000001', playerId: playerA,
      waveId: 1, seed: '1', expiresAt: new Date(Date.now() + 7_200_000),
    },
    {
      serverId: SERVER_B, issuanceId: '11111111-0000-0000-0000-000000000002', playerId: playerB,
      waveId: 1, seed: '2', expiresAt: new Date(Date.now() + 7_200_000),
    },
  ])
}, 180_000)

afterAll(async () => { await t?.stop() })

describe('the gate itself', () => {
  // Do not trust a gate that has never been proven capable of failing. If the
  // app role were a superuser, or the tables lacked FORCE, every assertion
  // below would pass while the policies did nothing.
  it('runs as a role that cannot bypass RLS', async () => {
    const r = await t.db.execute(sql`
      SELECT current_user AS who, rolsuper, rolbypassrls
        FROM pg_roles WHERE rolname = current_user`)
    const row = r.rows[0] as { who: string; rolsuper: boolean; rolbypassrls: boolean }
    expect(row.who).toBe('broodline_app')
    expect(row.rolsuper).toBe(false)
    expect(row.rolbypassrls).toBe(false)
  })

  it('forces RLS on every table in the fleet, not just the five known today, so ownership grants no exemption', async () => {
    // Assert the INVARIANT, not an enumerated list of names. The previous
    // version of this test compared against a hardcoded array of five
    // table names - the SAME five names the 0002_rls.sql policy loop and
    // its GRANT are hardcoded against. A table added by a later phase and
    // forgotten in all three places would get no ENABLE, no FORCE, no
    // policy - fully exposed across the fleet - while this test kept
    // comparing against the same five names and stayed green. Forgetting
    // the GRANT fails loudly (every query 403s); forgetting RLS fails
    // silently, which is the direction that matters.
    //
    // Global tables are allowlisted here explicitly, so adding to this list
    // is a visible, reviewable decision rather than an accidental
    // exclusion. Keep it in sync with the tables 0002_rls.sql declines to
    // put a policy on.
    const r = await t.db.execute(sql`
      SELECT c.relname, c.relrowsecurity, c.relforcerowsecurity
        FROM pg_class c
        JOIN pg_namespace n ON n.oid = c.relnamespace
       WHERE n.nspname = 'public' AND c.relkind IN ('r', 'p')
         AND c.relname NOT IN ('servers', 'accounts', '_migrations')
       ORDER BY c.relname`)

    // Sanity check on the allowlist subtraction itself: if it accidentally
    // swallowed every table, the loop below would vacuously pass on zero
    // rows. Five is what exists today; a later phase should only grow this.
    expect(r.rows.length).toBeGreaterThanOrEqual(5)
    for (const row of r.rows as Array<{ relname: string; relrowsecurity: boolean; relforcerowsecurity: boolean }>) {
      expect(row.relrowsecurity, `${row.relname}.relrowsecurity`).toBe(true)
      expect(row.relforcerowsecurity, `${row.relname}.relforcerowsecurity`).toBe(true)
    }
  })
})

describe('cross-server isolation', () => {
  it('shows a scoped read only its own server, across every server-scoped table', async () => {
    const seen = await withServer(t.db, SERVER_A, async (tx) => ({
      players: await tx.select().from(players),
      wallets: await tx.select().from(wallets),
      ledger: await tx.select().from(ledger),
      idempotencyKeys: await tx.select().from(idempotencyKeys),
      campaignProgress: await tx.select().from(campaignProgress),
      waveIssuances: await tx.select().from(waveIssuances),
    }))

    expect(seen.players.map((p) => p.serverId)).toEqual([SERVER_A])
    expect(seen.wallets.map((w) => w.serverId)).toEqual([SERVER_A])
    expect(seen.ledger.map((l) => l.serverId)).toEqual([SERVER_A])
    expect(seen.idempotencyKeys.map((k) => k.serverId)).toEqual([SERVER_A])
    expect(seen.campaignProgress.map((c) => c.serverId)).toEqual([SERVER_A])
    expect(seen.waveIssuances.map((w) => w.serverId)).toEqual([SERVER_A])

    // Named explicitly: server B's rows must not appear anywhere.
    expect(seen.wallets.some((w) => w.balance === 999)).toBe(false)
    expect(seen.idempotencyKeys.some((k) => k.key === 'fixture-b')).toBe(false)
    expect(seen.campaignProgress.some((c) => c.highestWaveCleared === 9)).toBe(false)
    // seed is a genuine string end-to-end (schema.ts's int8String custom
    // type), so this is a real comparison, not the vacuous bigint-vs-string
    // one it would have been against drizzle-orm's built-in bigint modes -
    // see the Task 4 report's typecheck finding for why that mattered.
    expect(seen.waveIssuances.some((w) => w.seed === '2')).toBe(false)
  })

  it('returns ZERO rows when nothing scoped the query, rather than everything', async () => {
    // The default-deny property. A handler that forgets withServer finds
    // nothing; it does not quietly read the whole fleet.
    const rows = await t.db.select().from(wallets)
    expect(rows).toEqual([])
  })

  it('refuses to INSERT a row belonging to another server', async () => {
    // WITH CHECK, not USING. Writing into a neighbour is the more damaging
    // direction and USING alone does not stop it.
    await expect(
      withServer(t.db, SERVER_A, async (tx) => {
        await tx.insert(wallets).values({
          serverId: SERVER_B, playerId: playerB, currency: 'marks', balance: 1,
        })
      }),
    ).rejects.toThrow(/row-level security/i)
  })

  it('refuses an UPDATE that migrates a row onto another server - WITH CHECK, not just USING', async () => {
    // The row-migration attack: unlike the INSERT case, the row already
    // exists and is legitimately visible/writable in this session; the
    // attack is changing WHICH server it belongs to. USING alone would not
    // stop this - it only filters what the UPDATE can see going in. WITH
    // CHECK is what rejects the row coming OUT with a foreign server_id.
    await expect(
      withServer(t.db, SERVER_A, async (tx) => {
        await tx.update(wallets).set({ serverId: SERVER_B }).where(eq(wallets.playerId, playerA))
      }),
    ).rejects.toThrow(/row-level security/i)
  })

  it('lets a DELETE outside its scope affect zero rows rather than erroring - DELETE has no WITH CHECK', async () => {
    // Different semantics from INSERT/UPDATE above, and worth recording
    // precisely because it is easy to assume DELETE behaves the same way.
    // DELETE has nothing to WITH CHECK - there is no new row to validate -
    // so a DELETE aimed at another server's row is not rejected. It is
    // scoped down by USING to nothing: a silent no-op, zero rows affected,
    // and the neighbour's row survives untouched.
    const result = await withServer(t.db, SERVER_A, async (tx) =>
      tx.delete(wallets).where(eq(wallets.playerId, playerB)))
    expect(result.rowCount ?? 0).toBe(0)

    const stillThere = await withServer(t.db, SERVER_B, async (tx) => tx.select().from(wallets))
    expect(stillThere.some((w) => w.playerId === playerB)).toBe(true)
  })

  it('does not let app.server_id survive a transaction onto the next checkout', async () => {
    // THE reason set_config's third argument is true. A setting that leaked
    // across a pooled checkout would hand the next request the last one's
    // server, and with one server in testing it would never be noticed.
    let pidInsideTx = -1
    await withServer(t.db, SERVER_B, async (tx) => {
      expect(await tx.select().from(wallets)).toHaveLength(1)
      const r = await tx.execute(sql`SELECT pg_backend_pid() AS pid`)
      pidInsideTx = (r.rows[0] as { pid: number }).pid
    })

    // This test only means something if the query below reuses the SAME
    // physical connection the transaction just ran on - pg-pool is LIFO so
    // it does today, but nothing enforces that as a contract. A pool
    // change, a different pooling library, or the connection simply idling
    // out would make the "leaked" check below trivially pass on a FRESH
    // connection that never had the setting to begin with. Pin the
    // precondition before trusting the conclusion.
    const pidAfter = await t.db.execute(sql`SELECT pg_backend_pid() AS pid`)
    expect((pidAfter.rows[0] as { pid: number }).pid).toBe(pidInsideTx)

    const leaked = await t.db.execute(sql`SELECT current_setting('app.server_id', true) AS v`)
    expect((leaked.rows[0] as { v: string | null }).v ?? '').toBe('')

    expect(await t.db.select().from(wallets)).toEqual([])
  })
})

describe('accounts: unscoped but narrowly writable', () => {
  // accounts carries no RLS policy at all (see 0002_rls.sql for why: the
  // login path resolves an account before it knows which server to scope
  // to). That is a deliberate exposure, not an oversight - so it is pinned
  // here exactly like the RLS-bound tables are pinned above: what the app
  // role CAN and CANNOT do to a NEIGHBOURING server's account row.
  it('lets the app role update apple_sub on ANY account, including a neighbouring server\'s - a pinned, deliberate exposure', async () => {
    await t.db.update(accounts).set({ appleSub: 'apple:pinned-test' }).where(eq(accounts.accountId, accountB))
    const [row] = await t.db.select().from(accounts).where(eq(accounts.accountId, accountB))
    expect(row?.appleSub).toBe('apple:pinned-test')
  })

  it('refuses the app role permission to update a column outside the granted set (e.g. home_region)', async () => {
    // Column-level GRANT, not a blanket one - only apple_sub and
    // deleted_at are reachable. Anything else, including a column with no
    // special sensitivity like home_region, must be refused at the grant
    // level so the exposure stays exactly as narrow as pinned above.
    await expect(
      t.db.update(accounts).set({ homeRegion: 'eu-west1' }).where(eq(accounts.accountId, accountB)),
    ).rejects.toThrow(/permission denied/i)
  })

  it('refuses the app role permission to update server_id at all - it is not even a granted column', async () => {
    await expect(
      t.db.update(accounts).set({ serverId: SERVER_A }).where(eq(accounts.accountId, accountB)),
    ).rejects.toThrow(/permission denied/i)
  })

  it('rejects an UPDATE that changes accounts.server_id even from the owner connection, where table grants do not apply', async () => {
    // The column-level GRANT above already keeps server_id unreachable for
    // the app role, but a table OWNER or superuser is not bound by grants
    // at all. This is what proves the immutability claim is enforced by
    // the trigger for every writer, not merely by the grant for one of them.
    await expect(
      t.ownerDb.update(accounts).set({ serverId: SERVER_A }).where(eq(accounts.accountId, accountB)),
    ).rejects.toThrow(/immutable/i)
  })
})
