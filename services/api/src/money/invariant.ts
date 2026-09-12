import { sql } from 'drizzle-orm'
import type { Tx } from '../db/client.ts'
import type { Currency } from './ledger.ts'

export interface Drift {
  serverId: number
  playerId: string
  currency: Currency
  walletBalance: number
  ledgerSum: number
  /**
   * The same two values as walletBalance/ledgerSum, but as the raw text
   * Postgres returned (SUM(bigint) is numeric, and node-postgres hands
   * numeric back as a string precisely so callers do not silently truncate
   * it). See the comment on the SELECT below for why the numbers alone are
   * not trustworthy for anything but detection.
   */
  walletBalanceRaw: string
  ledgerSumRaw: string
}

/**
 * Sums ledger.delta per (server, player, currency) and compares it to the
 * stored wallet balance. solo_execution 5.3: drift is either a bug or a
 * duplication exploit, and both get worse the longer they run.
 *
 * SCOPED BY server_id, not just (player_id, currency), and not delegated to
 * RLS. Inside withServer() on the app role RLS would scope this correctly on
 * its own, but 0002_rls.sql states outright that a superuser bypasses RLS
 * entirely and FORCE does not change that - and an admin psql session is
 * exactly how this job gets run at 3am. Run there without server_id in the
 * query, it would aggregate ACROSS every server and silently report
 * cross-server drift; even run correctly scoped, a caller could not tell
 * which server drifted. Task 3 added composite foreign keys for the same
 * reason this needs its own server_id column: a correctness property should
 * not rest on one mechanism (here, RLS) alone.
 *
 * A FULL OUTER JOIN rather than a join from wallets, because the two
 * interesting failures are asymmetric: a wallet with no ledger history is a
 * balance written without its row, and ledger history with no wallet is a
 * wallet deleted out from under its own audit trail. A one-sided join sees
 * only the first.
 *
 * walletBalance/ledgerSum are convenience numbers - fine for logging, NOT
 * for a precision-sensitive comparison. SUM(bigint) returns numeric, which
 * node-postgres hands back as a string, and Number() rounds anything past
 * 2^53. The <> in the WHERE clause below runs IN POSTGRES on the exact
 * values, so DETECTION here is exact regardless. But the raw strings are
 * carried through too, because a report that prints walletBalance ===
 * ledgerSum for a real one-unit drift on a large balance reads as a false
 * positive and trains whoever's on call to stop trusting the job - worse
 * than a missed alert.
 */
export async function findDrift(tx: Tx): Promise<Drift[]> {
  const res = await tx.execute(sql`
    WITH sums AS (
      SELECT server_id, player_id, currency, SUM(delta) AS total
        FROM ledger GROUP BY server_id, player_id, currency
    )
    SELECT COALESCE(w.server_id, s.server_id)   AS server_id,
           COALESCE(w.player_id, s.player_id)   AS player_id,
           COALESCE(w.currency,  s.currency)    AS currency,
           COALESCE(w.balance, 0)               AS wallet_balance,
           COALESCE(s.total,   0)               AS ledger_sum
      FROM wallets w
      FULL OUTER JOIN sums s
        ON w.server_id = s.server_id AND w.player_id = s.player_id AND w.currency = s.currency
     WHERE COALESCE(w.balance, 0) <> COALESCE(s.total, 0)`)

  return res.rows.map((r) => {
    const row = r as Record<string, unknown>
    return {
      serverId: Number(row.server_id),
      playerId: String(row.player_id),
      currency: row.currency as Currency,
      walletBalance: Number(row.wallet_balance),
      ledgerSum: Number(row.ledger_sum),
      walletBalanceRaw: String(row.wallet_balance),
      ledgerSumRaw: String(row.ledger_sum),
    }
  })
}
