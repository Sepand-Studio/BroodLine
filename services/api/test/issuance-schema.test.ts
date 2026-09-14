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

const issue = (db: typeof t.db, waveId: number, id: string, expiresAt = new Date(Date.now() + 7_200_000)) =>
  withServer(db, 1, (tx) => tx.insert(waveIssuances).values({
    serverId: 1, issuanceId: id, playerId: PLAYER, waveId,
    seed: '1234', expiresAt,
  }))

const settle = (db: typeof t.db, id: string, settlement: 'consumed' | 'expired') =>
  withServer(db, 1, (tx) => tx.execute(sql`
    UPDATE wave_issuances SET settled_at = now(), settlement = ${settlement}
    WHERE issuance_id = ${id} AND settled_at IS NULL`))

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
    await settle(t.db, 'aaaaaaaa-0000-0000-0000-000000000001', 'consumed')

    await expect(issue(t.db, 6, 'aaaaaaaa-0000-0000-0000-000000000003'))
      .resolves.toBeDefined()
  })

  // C1/C2: the first version of this migration keyed the one-live index on
  // `consumed_at IS NULL`, which also covers a row that is EXPIRED but never
  // consumed - the ordinary abandoned-wave path (a player backgrounds the
  // app mid-wave). `wave/start` would exclude that row as expired, try to
  // insert a fresh one, and collide on the index for at least an hour, every
  // time. Postgres forbids fixing this by adding expiry to the index
  // predicate: predicates must be IMMUTABLE and now() is STABLE. The fix is
  // that liveness is now settled_at alone, and settling a stale row is a
  // WRITE the handler makes in the same transaction as the new insert -
  // never something the clock does for it. This proves that write-then-
  // insert pattern actually works, not the old broken one.
  it('lets an abandoned issuance - expired but never settled - be settled and replaced in one transaction', async () => {
    // Clear the slot left live by the previous test so only the abandoned
    // row below occupies it.
    await settle(t.db, 'aaaaaaaa-0000-0000-0000-000000000003', 'consumed')

    const abandoned = 'aaaaaaaa-0000-0000-0000-000000000004'
    await issue(t.db, 6, abandoned, new Date(Date.now() - 1000)) // already expired, still live (settled_at NULL)

    await expect(withServer(t.db, 1, async (tx) => {
      const settled = await tx.execute(sql`
        UPDATE wave_issuances SET settled_at = now(), settlement = 'expired'
        WHERE issuance_id = ${abandoned} AND settled_at IS NULL`)
      expect(settled.rowCount).toBe(1)
      return tx.insert(waveIssuances).values({
        serverId: 1, issuanceId: 'aaaaaaaa-0000-0000-0000-000000000005', playerId: PLAYER, waveId: 6,
        seed: '5678', expiresAt: new Date(Date.now() + 7_200_000),
      })
    })).resolves.toBeDefined()
  })

  it('refuses to rewrite settlement once set', async () => {
    // The ledger's guard is "consumed inside the credit's transaction".
    // A settlement that can be rewritten is not a guard at all - neither
    // reverted to NULL nor silently moved to a different value.
    await settle(t.db, 'aaaaaaaa-0000-0000-0000-000000000005', 'consumed')

    await expect(withServer(t.db, 1, (tx) => tx.execute(
      sql`UPDATE wave_issuances SET settled_at = NULL, settlement = NULL
          WHERE issuance_id = 'aaaaaaaa-0000-0000-0000-000000000005'`)))
      .rejects.toThrow(/write-once/)

    // The other direction: a rewrite that silently MOVES the settlement
    // (e.g. from 'consumed' to 'expired') rather than erasing it is exactly
    // as dangerous - it is the rewrite that would hide a payout that already
    // happened, not just the one that reopens the row.
    await expect(withServer(t.db, 1, (tx) => tx.execute(
      sql`UPDATE wave_issuances SET settlement = 'expired'
          WHERE issuance_id = 'aaaaaaaa-0000-0000-0000-000000000005'`)))
      .rejects.toThrow(/write-once/)
  })

  it('rejects a seed outside the engine\'s ulong range representable in a signed bigint', async () => {
    // bigint is int8 (signed); the engine's seed is a ulong. A seed at or
    // above 2^63 must be refused at insert, not silently sign-flipped into a
    // different wave. No live row occupies the slot at this point (the
    // previous test's row is already settled), so this fails on the CHECK
    // alone, not on the one-live index.
    await expect(withServer(t.db, 1, (tx) => tx.insert(waveIssuances).values({
      serverId: 1, issuanceId: 'aaaaaaaa-0000-0000-0000-000000000006', playerId: PLAYER, waveId: 6,
      seed: '-1', expiresAt: new Date(Date.now() + 7_200_000),
    }))).rejects.toThrow(/check/i)
  })

  it('is invisible without a server scope', async () => {
    // Not a new rule - 0002's default-deny, restated for the new table
    // because the isolation gate scans pg_class and this table must be in it.
    const rows = await t.db.execute(sql`SELECT * FROM wave_issuances`)
    expect(rows.rows).toHaveLength(0)
  })
})
