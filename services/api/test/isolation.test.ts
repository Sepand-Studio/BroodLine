import { sql } from 'drizzle-orm'
import { afterAll, beforeAll, describe, expect, it } from 'vitest'
import { withServer } from '../src/db/client.ts'
import { accounts, ledger, players, servers, wallets } from '../src/db/schema.ts'
import { startTestDb, type TestDb } from './harness.ts'

const SERVER_A = 1
const SERVER_B = 2

let t: TestDb
let playerA: string
let playerB: string

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

  it('forces RLS on every server-scoped table, so ownership grants no exemption', async () => {
    const r = await t.db.execute(sql`
      SELECT relname, relrowsecurity, relforcerowsecurity
        FROM pg_class
       WHERE relname IN ('players','wallets','ledger','idempotency_keys','campaign_progress')
       ORDER BY relname`)
    expect(r.rows).toHaveLength(5)
    for (const row of r.rows as Array<{ relrowsecurity: boolean; relforcerowsecurity: boolean }>) {
      expect(row.relrowsecurity).toBe(true)
      expect(row.relforcerowsecurity).toBe(true)
    }
  })
})

describe('cross-server isolation', () => {
  it('shows a scoped read only its own server, across every table', async () => {
    const seen = await withServer(t.db, SERVER_A, async (tx) => ({
      players: await tx.select().from(players),
      wallets: await tx.select().from(wallets),
      ledger: await tx.select().from(ledger),
    }))

    expect(seen.players.map((p) => p.serverId)).toEqual([SERVER_A])
    expect(seen.wallets.map((w) => w.serverId)).toEqual([SERVER_A])
    expect(seen.ledger.map((l) => l.serverId)).toEqual([SERVER_A])

    // Named explicitly: server B's 999 shards must not appear anywhere.
    expect(seen.wallets.some((w) => w.balance === 999)).toBe(false)
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

  it('does not let app.server_id survive a transaction onto the next checkout', async () => {
    // THE reason set_config's third argument is true. A setting that leaked
    // across a pooled checkout would hand the next request the last one's
    // server, and with one server in testing it would never be noticed.
    await withServer(t.db, SERVER_B, async (tx) => {
      expect(await tx.select().from(wallets)).toHaveLength(1)
    })

    const leaked = await t.db.execute(sql`SELECT current_setting('app.server_id', true) AS v`)
    expect((leaked.rows[0] as { v: string | null }).v ?? '').toBe('')

    expect(await t.db.select().from(wallets)).toEqual([])
  })
})
