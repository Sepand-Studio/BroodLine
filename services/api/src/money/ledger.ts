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
  constructor(readonly currency: Currency) {
    super(`Insufficient ${currency}.`)
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
      // Reachable only if the policy hid the row - i.e. credit() was called
      // outside withServer, or with a server_id the transaction is not scoped
      // to. Both are programming errors and both must be loud.
      throw new Error(
        `credit() wrote no wallet row for server ${m.serverId}. ` +
        `Was it called outside withServer(), or with a mismatched server_id?`)
    }
    balanceAfter = Number(row.balance)
  } catch (err) {
    // 23514 is check_violation - here, always balance >= 0.
    if (typeof err === 'object' && err !== null && (err as { code?: string }).code === '23514') {
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
