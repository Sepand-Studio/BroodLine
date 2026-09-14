import { sql } from 'drizzle-orm'
import { afterAll, beforeAll, describe, expect, it } from 'vitest'
import { withServer } from '../src/db/client.ts'
import { servers, players, accounts, waveIssuances } from '../src/db/schema.ts'
import { startTestDb, type TestDb } from './harness.ts'

let t: TestDb
const PLAYER = '00000000-0000-0000-0000-0000000000aa'

beforeAll(async () => {
  t = await startTestDb()
  await t.ownerDb.insert(servers).values({
    serverId: 1, region: 'us-central1', state: 'open', tickDayOfWeek: 0, tickMinuteOfDay: 1200,
  })
  // A player to hang issuances off, via the composite FK 0001 established.
  // birthdateBand/homeRegion are NOT NULL on accounts (0001_tables.sql) -
  // added here to satisfy that constraint; the brief's verbatim fixture
  // omitted them.
  await t.ownerDb.insert(accounts).values({
    accountId: PLAYER, serverId: 1, birthdateBand: 'adult', homeRegion: 'us-central1',
  })
  await t.ownerDb.insert(players).values({ serverId: 1, playerId: PLAYER, accountId: PLAYER })
}, 240_000)

afterAll(async () => { await t?.stop() })

const issue = (db: typeof t.db, waveId: number, id: string) =>
  withServer(db, 1, (tx) => tx.insert(waveIssuances).values({
    serverId: 1, issuanceId: id, playerId: PLAYER, waveId,
    seed: '1234', expiresAt: new Date(Date.now() + 7_200_000),
  }))

describe('wave_issuances', () => {
  it('permits exactly one live issuance per player', async () => {
    await issue(t.db, 6, 'aaaaaaaa-0000-0000-0000-000000000001')

    // design 2.1: a client that can hold a hundred valid seeds simulates all
    // of them and submits the winner. The index is the defence, NOT a check
    // in the handler - a handler can be forgotten and an index cannot.
    await expect(issue(t.db, 6, 'aaaaaaaa-0000-0000-0000-000000000002'))
      .rejects.toThrow(/unique|duplicate/i)
  })

  it('permits a new issuance once the previous one is consumed', async () => {
    await withServer(t.db, 1, (tx) => tx.execute(
      sql`UPDATE wave_issuances SET consumed_at = now()
          WHERE issuance_id = 'aaaaaaaa-0000-0000-0000-000000000001'`))

    await expect(issue(t.db, 6, 'aaaaaaaa-0000-0000-0000-000000000003'))
      .resolves.toBeDefined()
  })

  it('refuses to rewrite consumed_at', async () => {
    // The ledger's guard is "consumed inside the credit's transaction".
    // A consumed_at that can be set back to NULL is not a guard at all.
    await withServer(t.db, 1, (tx) => tx.execute(
      sql`UPDATE wave_issuances SET consumed_at = now()
          WHERE issuance_id = 'aaaaaaaa-0000-0000-0000-000000000003'`))

    await expect(withServer(t.db, 1, (tx) => tx.execute(
      sql`UPDATE wave_issuances SET consumed_at = NULL
          WHERE issuance_id = 'aaaaaaaa-0000-0000-0000-000000000003'`)))
      .rejects.toThrow(/consumed_at is write-once/)
  })

  it('is invisible without a server scope', async () => {
    // Not a new rule - 0002's default-deny, restated for the new table
    // because the isolation gate scans pg_class and this table must be in it.
    const rows = await t.db.execute(sql`SELECT * FROM wave_issuances`)
    expect(rows.rows).toHaveLength(0)
  })
})
