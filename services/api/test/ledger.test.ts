import { and, eq } from 'drizzle-orm'
import { afterAll, beforeAll, describe, expect, it } from 'vitest'
import { withServer } from '../src/db/client.ts'
import { InsufficientFundsError, credit } from '../src/money/ledger.ts'
import { findDrift } from '../src/money/invariant.ts'
import { accounts, ledger, players, servers, wallets } from '../src/db/schema.ts'
import { startTestDb, type TestDb } from './harness.ts'

const S = 1
let t: TestDb
let player: string

beforeAll(async () => {
  t = await startTestDb()
  await t.ownerDb.insert(servers).values({
    serverId: S, region: 'us-central1', state: 'open', tickDayOfWeek: 0, tickMinuteOfDay: 1200,
  })
  const [acc] = await t.ownerDb.insert(accounts)
    .values({ birthdateBand: 'adult', homeRegion: 'us-central1', serverId: S }).returning()
  const [p] = await t.ownerDb.insert(players)
    .values({ serverId: S, accountId: acc!.accountId }).returning()
  player = p!.playerId
}, 180_000)

afterAll(async () => { await t?.stop() })

describe('credit', () => {
  it('creates the wallet on first credit and writes its ledger row', async () => {
    const balance = await withServer(t.db, S, (tx) =>
      credit(tx, { serverId: S, playerId: player, currency: 'shards', delta: 250, reasonCode: 'TEST_GRANT' }))

    expect(balance).toBe(250)

    const rows = await withServer(t.db, S, (tx) =>
      tx.select().from(ledger).where(and(eq(ledger.playerId, player), eq(ledger.reasonCode, 'TEST_GRANT'))))

    expect(rows).toHaveLength(1)
    expect(rows[0]!.delta).toBe(250)
    // balance_after is recorded, not recomputed at read time. It is what makes
    // "where did my shards go" answerable without replaying the whole history.
    expect(rows[0]!.balanceAfter).toBe(250)
  })

  it('accumulates, and each mutation leaves its own row', async () => {
    await withServer(t.db, S, async (tx) => {
      await credit(tx, { serverId: S, playerId: player, currency: 'marks', delta: 10, reasonCode: 'A' })
      await credit(tx, { serverId: S, playerId: player, currency: 'marks', delta: 5, reasonCode: 'B' })
    })

    const [w] = await withServer(t.db, S, (tx) =>
      tx.select().from(wallets).where(and(eq(wallets.playerId, player), eq(wallets.currency, 'marks'))))

    expect(w!.balance).toBe(15)
    // version moves on every write - the optimistic-concurrency column at 5.4.
    expect(w!.version).toBe(1)
  })

  it('refuses to overdraw, and leaves nothing behind', async () => {
    await expect(
      withServer(t.db, S, (tx) =>
        credit(tx, { serverId: S, playerId: player, currency: 'marks', delta: -1000, reasonCode: 'OVERDRAW' })),
    ).rejects.toBeInstanceOf(InsufficientFundsError)

    // The whole transaction rolled back, so no orphan ledger row survives.
    const orphans = await withServer(t.db, S, (tx) =>
      tx.select().from(ledger).where(eq(ledger.reasonCode, 'OVERDRAW')))
    expect(orphans).toEqual([])
  })
})

describe('the invariant job', () => {
  it('finds no drift between the ledger and the wallets', async () => {
    // solo_execution 5.3: drift is either a bug or a duplication exploit and
    // warrants same-day attention. Running it as a test means it is exercised
    // long before it is ever a 3am page.
    const drift = await withServer(t.db, S, (tx) => findDrift(tx))
    expect(drift).toEqual([])
  })
})
