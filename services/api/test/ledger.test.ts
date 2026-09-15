import { and, eq } from 'drizzle-orm'
import { afterAll, beforeAll, describe, expect, it } from 'vitest'
import { withServer } from '../src/db/client.ts'
import { InsufficientFundsError, credit, debit } from '../src/money/ledger.ts'
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

  it('refuses a negative delta outright - a debit is a different statement', async () => {
    // THIS TEST USED TO PASS FOR THE WRONG REASON, and Task 7 is what found
    // it. It called credit() with delta -1000 against a wallet holding 15 and
    // asserted InsufficientFundsError - which arrived, and NOT because the
    // wallet was short.
    //
    // credit()'s statement is INSERT ... ON CONFLICT DO UPDATE, and Postgres
    // evaluates a table's CHECK constraints against the PROPOSED INSERT TUPLE
    // before probing for the conflict. The proposed tuple carries
    // `balance = -1000`, so wallets_balance_check fires whatever the real
    // balance is. Measured directly: Task 7's first splice raised
    // "Insufficient splice_charges" against a wallet holding three.
    //
    // No caller before that task had ever passed a negative delta - every one
    // is a grant or a reward - so the trap sat here unsprung, with a green
    // test standing over it.
    await expect(
      withServer(t.db, S, (tx) =>
        credit(tx, { serverId: S, playerId: player, currency: 'marks', delta: -1, reasonCode: 'OVERDRAW' })),
    ).rejects.toThrow(/use debit/)
  })
})

describe('debit', () => {
  it('spends from an existing wallet and writes its ledger row', async () => {
    // The positive control the old overdraw test never had: a debit a wallet
    // CAN afford must succeed. Without this, "debit refuses" is
    // indistinguishable from "debit refuses everything", which is exactly the
    // state credit() was in.
    await withServer(t.db, S, (tx) =>
      credit(tx, { serverId: S, playerId: player, currency: 'premium', delta: 5, reasonCode: 'SEED' }))

    const after = await withServer(t.db, S, (tx) =>
      debit(tx, { serverId: S, playerId: player, currency: 'premium', delta: -2, reasonCode: 'SPEND' }))

    expect(after).toBe(3)
    const [row] = await withServer(t.db, S, (tx) =>
      tx.select().from(ledger).where(and(eq(ledger.playerId, player), eq(ledger.reasonCode, 'SPEND'))))
    expect(row!.delta).toBe(-2)
    expect(row!.balanceAfter).toBe(3)
  })

  it('refuses to overdraw, and leaves nothing behind', async () => {
    await expect(
      withServer(t.db, S, (tx) =>
        debit(tx, { serverId: S, playerId: player, currency: 'marks', delta: -1000, reasonCode: 'OVERDRAW' })),
    ).rejects.toBeInstanceOf(InsufficientFundsError)

    // The whole transaction rolled back, so no orphan ledger row survives.
    const orphans = await withServer(t.db, S, (tx) =>
      tx.select().from(ledger).where(eq(ledger.reasonCode, 'OVERDRAW')))
    expect(orphans).toEqual([])
  })

  it('refuses a currency the player has no wallet for', async () => {
    // No row is exactly zero, and creating a wallet in order to overdraw it
    // would be a stranger thing to do than refusing. Distinct from the
    // overdraw case above, which has a row.
    await expect(
      withServer(t.db, S, (tx) =>
        debit(tx, { serverId: S, playerId: player, currency: 'splice_charges', delta: -1, reasonCode: 'NOWALLET' })),
    ).rejects.toBeInstanceOf(InsufficientFundsError)

    const orphans = await withServer(t.db, S, (tx) =>
      tx.select().from(ledger).where(eq(ledger.reasonCode, 'NOWALLET')))
    expect(orphans).toEqual([])
  })

  it('refuses a positive delta - that is a credit, and naming it matters', async () => {
    await expect(
      withServer(t.db, S, (tx) =>
        debit(tx, { serverId: S, playerId: player, currency: 'marks', delta: 5, reasonCode: 'WRONG' })),
    ).rejects.toThrow(/use credit/)
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
