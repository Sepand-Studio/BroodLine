import { sql } from 'drizzle-orm'
import type { Tx } from '../db/client.ts'
import { ledger } from '../db/schema.ts'

export type Currency = 'shards' | 'splice_charges' | 'marks' | 'premium'

export interface Mutation {
  serverId: number
  playerId: string
  currency: Currency
  /** Negative debits. The wallet's CHECK (balance >= 0) is what refuses one. */
  delta: number
  reasonCode: string
  refType?: string
  refId?: string
  idempotencyKey?: string
}

export class InsufficientFundsError extends Error {
  // Explicit field, NOT a constructor parameter property. Node's
  // --experimental-strip-types cannot compile `constructor(readonly x: T)`
  // - it is strip-only and a parameter property requires emitting an
  // assignment. The whole service runs under that flag in the container,
  // so a parameter property anywhere in index.ts's import graph crashes on
  // boot. Vitest uses esbuild, which DOES support them, which is why the
  // suite stayed green.
  readonly currency: Currency

  constructor(currency: Currency) {
    super(`Insufficient ${currency}.`)
    this.currency = currency
    this.name = 'InsufficientFundsError'
  }
}

/**
 * The single function that writes a currency mutation.
 *
 * solo_execution 5.3: every currency mutation writes an append-only ledger
 * row in the SAME TRANSACTION as the balance update, and balances are never
 * derived by summing the ledger at read time. Both halves matter - summing at
 * read time is how a ledger becomes too slow to keep, and a balance written
 * without its row is how drift starts.
 *
 * MUST be called inside withServer(). Outside it the policy hides every row
 * and the upsert below silently inserts into nothing it can then read.
 */
export async function credit(tx: Tx, m: Mutation): Promise<number> {
  let balanceAfter: number
  try {
    // One statement, so it is correct under any concurrency: ON CONFLICT DO
    // UPDATE takes the row lock, and a second transaction waits rather than
    // reading a stale balance and writing it back.
    const res = await tx.execute(sql`
      INSERT INTO wallets (server_id, player_id, currency, balance, version)
      VALUES (${m.serverId}, ${m.playerId}, ${m.currency}::currency, ${m.delta}, 0)
      ON CONFLICT (server_id, player_id, currency) DO UPDATE
        SET balance = wallets.balance + EXCLUDED.balance,
            version = wallets.version + 1
      RETURNING balance`)

    const row = res.rows[0] as { balance: string | number } | undefined
    if (row === undefined) {
      // Belt-and-braces, not a reachable outcome: INSERT ... ON CONFLICT DO
      // UPDATE ... RETURNING always inserts, updates, or raises - it cannot
      // return zero rows for the row it just wrote or updated. The failure
      // mode this guard used to attribute to "called outside withServer" is
      // real but does not arrive here: RLS blocking the write raises 42501
      // (new row violates row-level security policy) instead, in the catch
      // block below as `err`, never as an empty result set.
      throw new Error(
        `credit() wrote no wallet row for server ${m.serverId}. ` +
        `Was it called outside withServer(), or with a mismatched server_id?`)
    }
    balanceAfter = Number(row.balance)
  } catch (err) {
    // Gated on the CONSTRAINT, not just the SQLSTATE (23514, check_violation)
    // - exact today because wallets carries exactly one CHECK, but a second
    // one landing later would otherwise surface as a false "Insufficient
    // shards" for an unrelated violation. wallets_balance_check is the name
    // Postgres generates for the unnamed CHECK (balance >= 0) in
    // 0001_tables.sql - confirmed by querying pg_constraint, not assumed.
    const e = err as { code?: string; constraint?: string } | null
    if (typeof err === 'object' && e !== null
      && e.code === '23514' && e.constraint === 'wallets_balance_check') {
      throw new InsufficientFundsError(m.currency)
    }
    throw err
  }

  await tx.insert(ledger).values({
    serverId: m.serverId,
    playerId: m.playerId,
    currency: m.currency,
    delta: m.delta,
    balanceAfter,
    reasonCode: m.reasonCode,
    refType: m.refType ?? null,
    refId: m.refId ?? null,
    idempotencyKey: m.idempotencyKey ?? null,
  })

  return balanceAfter
}
