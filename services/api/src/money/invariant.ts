import { sql } from 'drizzle-orm'
import type { Tx } from '../db/client.ts'
import type { Currency } from './ledger.ts'

export interface Drift {
  playerId: string
  currency: Currency
  walletBalance: number
  ledgerSum: number
}

/**
 * Sums ledger.delta per (player, currency) and compares it to the stored
 * wallet balance. solo_execution 5.3: drift is either a bug or a duplication
 * exploit, and both get worse the longer they run.
 *
 * A FULL OUTER JOIN rather than a join from wallets, because the two
 * interesting failures are asymmetric: a wallet with no ledger history is a
 * balance written without its row, and ledger history with no wallet is a
 * wallet deleted out from under its own audit trail. A one-sided join sees
 * only the first.
 */
export async function findDrift(tx: Tx): Promise<Drift[]> {
  const res = await tx.execute(sql`
    WITH sums AS (
      SELECT player_id, currency, SUM(delta) AS total
        FROM ledger GROUP BY player_id, currency
    )
    SELECT COALESCE(w.player_id, s.player_id)   AS player_id,
           COALESCE(w.currency,  s.currency)    AS currency,
           COALESCE(w.balance, 0)               AS wallet_balance,
           COALESCE(s.total,   0)               AS ledger_sum
      FROM wallets w
      FULL OUTER JOIN sums s
        ON w.player_id = s.player_id AND w.currency = s.currency
     WHERE COALESCE(w.balance, 0) <> COALESCE(s.total, 0)`)

  return res.rows.map((r) => {
    const row = r as Record<string, unknown>
    return {
      playerId: String(row.player_id),
      currency: row.currency as Currency,
      walletBalance: Number(row.wallet_balance),
      ledgerSum: Number(row.ledger_sum),
    }
  })
}
