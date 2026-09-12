import { eq } from 'drizzle-orm'
import { afterAll, beforeAll, describe, expect, it } from 'vitest'
import { withServer } from '../src/db/client.ts'
import { IdempotencyMismatchError, withIdempotency } from '../src/money/idempotency.ts'
import { credit } from '../src/money/ledger.ts'
import { accounts, idempotencyKeys, ledger, players, servers, wallets } from '../src/db/schema.ts'
import { startTestDb, type TestDb } from './harness.ts'

const S = 1
const PARALLEL = 8
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

describe('idempotency under concurrency', () => {
  it('pays exactly once when the same key arrives eight times at once', async () => {
    const key = 'grant-abc'
    const hash = 'h1'

    const results = await Promise.all(
      Array.from({ length: PARALLEL }, () =>
        withIdempotency(t.db, S, key, hash, (tx) =>
          credit(tx, {
            serverId: S, playerId: player, currency: 'shards',
            delta: 100, reasonCode: 'CONCURRENT_GRANT', idempotencyKey: key,
          }).then((balance) => ({ balance })))),
    )

    // Exactly one request did the work; the rest replayed its answer.
    expect(results.filter((r) => r.status === 'fresh')).toHaveLength(1)
    expect(results.filter((r) => r.status === 'replayed')).toHaveLength(PARALLEL - 1)

    // Every caller got the same answer, whether it did the work or not.
    expect(new Set(results.map((r) => r.body.balance))).toEqual(new Set([100]))

    // THE ASSERTION. 100, not 800.
    const [w] = await withServer(t.db, S, (tx) =>
      tx.select().from(wallets).where(eq(wallets.playerId, player)))
    expect(w!.balance).toBe(100)

    const rows = await withServer(t.db, S, (tx) =>
      tx.select().from(ledger).where(eq(ledger.reasonCode, 'CONCURRENT_GRANT')))
    expect(rows).toHaveLength(1)
  })

  it('rejects a reused key carrying a different body, rather than answering the wrong question', async () => {
    const key = 'grant-def'
    await withIdempotency(t.db, S, key, 'hash-one', async () => ({ ok: true }))

    await expect(
      withIdempotency(t.db, S, key, 'hash-TWO', async () => ({ ok: true })),
    ).rejects.toBeInstanceOf(IdempotencyMismatchError)
  })

  it('does not strand a key when the work throws', async () => {
    const key = 'grant-ghi'

    await expect(
      withIdempotency(t.db, S, key, 'h', async () => { throw new Error('boom') }),
    ).rejects.toThrow('boom')

    // The transaction rolled back, key included, so an honest retry can still
    // succeed. A key stranded as in_flight by a crash would lock the caller
    // out of an action they never completed.
    const rows = await withServer(t.db, S, (tx) =>
      tx.select().from(idempotencyKeys).where(eq(idempotencyKeys.key, key)))
    expect(rows).toEqual([])

    const retry = await withIdempotency(t.db, S, key, 'h', async () => ({ ok: true }))
    expect(retry.status).toBe('fresh')
  })

  it('propagates a foreign unique violation rather than treating it as a replay', async () => {
    // Fix Round 1, Finding 1: isIdempotencyKeyConflict must gate on the
    // CONSTRAINT (idempotency_keys_pkey), not just SQLSTATE 23505. No path
    // inside credit() can raise a 23505 today, but a later task's `fn` will
    // touch accounts.apple_sub - so this simulates that with a UNIQUE
    // violation on a DIFFERENT constraint from inside `fn`, entirely unrelated
    // to the idempotency key itself.
    const key = 'grant-foreign-conflict'

    const err: unknown = await withIdempotency(t.db, S, key, 'h', async (tx) => {
      await tx.insert(accounts).values({
        birthdateBand: 'adult', homeRegion: 'us-central1', serverId: S, appleSub: 'dup-sub',
      })
      // Same transaction, same apple_sub: raises 23505 on
      // accounts_apple_sub_key, not on idempotency_keys_pkey.
      await tx.insert(accounts).values({
        birthdateBand: 'adult', homeRegion: 'us-central1', serverId: S, appleSub: 'dup-sub',
      })
      return { ok: true }
    }).then(
      () => { throw new Error('withIdempotency resolved; expected the foreign violation to propagate') },
      (e: unknown) => e,
    )

    // The ORIGINAL unique-violation error, not IdempotencyMismatchError, not
    // the "vanished" error, and not a replayed body from a request that
    // never happened.
    expect(err).not.toBeInstanceOf(IdempotencyMismatchError)
    expect(err).toMatchObject({ code: '23505', constraint: 'accounts_apple_sub_key' })

    // The whole transaction rolled back, key included, so an honest retry of
    // THIS idempotency key is not blocked by someone else's constraint.
    const rows = await withServer(t.db, S, (tx) =>
      tx.select().from(idempotencyKeys).where(eq(idempotencyKeys.key, key)))
    expect(rows).toEqual([])
  })
})
