import { sql } from 'drizzle-orm'
import type { Tx } from '../db/client.ts'
import { ledger } from '../db/schema.ts'

export type Currency = 'shards' | 'splice_charges' | 'marks' | 'premium'

export interface Mutation {
  serverId: number
  playerId: string
  currency: Currency
  /**
   * Negative debits, and they go through `debit` rather than `credit` - the
   * two are different statements, not one function with a sign. The wallet's
   * CHECK (balance >= 0) is what refuses an overdraft, but only on the
   * UPDATE path; see the refusal at the top of `credit`.
   */
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
  // A NEGATIVE DELTA HERE IS A BUG, AND IT IS NOT THE ONE IT LOOKS LIKE.
  // The statement below is INSERT ... ON CONFLICT DO UPDATE, and Postgres
  // runs a table's CHECK constraints against the PROPOSED INSERT TUPLE
  // before it probes for the conflict - so `delta: -1` is checked as
  // `balance = -1`, `wallets_balance_check` fires, and the catch block turns
  // it into InsufficientFundsError NO MATTER WHAT THE BALANCE IS.
  //
  // Measured, not reasoned about: Task 7's first splice raised "Insufficient
  // splice_charges" against a wallet holding three of them. Nothing before
  // that task had ever passed this function a negative delta - every caller
  // to date is a grant or a reward - so the trap had never been sprung.
  //
  // Refused here rather than quietly handled, because a debit is a different
  // statement (see `debit`) and silently rewriting one into the other would
  // hide which of the two a caller meant.
  if (m.delta < 0) {
    throw new Error(
      `credit() cannot take a negative delta (${m.delta} ${m.currency}); use debit()`)
  }

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

  await record(tx, m, balanceAfter)
  return balanceAfter
}

/**
 * The other direction, and it needs its own statement rather than a sign
 * flip - see the refusal at the top of `credit` for the measurement that
 * forced this function into existence.
 *
 * A PLAIN UPDATE, so the CHECK is evaluated against the row's REAL new
 * balance and `wallets_balance_check` means what it says: an overdraft is
 * refused, an affordable debit is not. `credit`'s upsert cannot do that,
 * because the tuple it proposes carries the delta itself.
 *
 * NO ROW MEANS NO FUNDS, not a wallet to create. A player with no
 * `splice_charges` row has never been granted one, which is exactly zero -
 * and inserting a wallet in order to overdraw it would be a stranger thing
 * to do than refusing.
 *
 * `delta` is NEGATIVE here, the same sign the ledger row carries, so the
 * ledger's arithmetic stays "sum of deltas" in one direction for both
 * functions. Callers pass what they want recorded.
 *
 * MUST be called inside withServer(), for the reason `credit` gives.
 */
export async function debit(tx: Tx, m: Mutation): Promise<number> {
  if (m.delta >= 0) {
    throw new Error(
      `debit() takes a negative delta (got ${m.delta} ${m.currency}); use credit()`)
  }

  let balanceAfter: number
  try {
    // The UPDATE takes the row lock itself, so this is correct under any
    // concurrency for the same reason credit()'s upsert is: a second
    // transaction waits rather than reading a stale balance and writing it
    // back.
    const res = await tx.execute(sql`
      UPDATE wallets
         SET balance = balance + ${m.delta},
             version = version + 1
       WHERE server_id = ${m.serverId} AND player_id = ${m.playerId}
         AND currency = ${m.currency}::currency
      RETURNING balance`)

    const row = res.rows[0] as { balance: string | number } | undefined
    // Zero rows is the no-wallet case above. It is NOT the RLS case: a write
    // the policy blocks raises 42501 rather than matching nothing.
    if (row === undefined) throw new InsufficientFundsError(m.currency)
    balanceAfter = Number(row.balance)
  } catch (err) {
    // Gated on the CONSTRAINT as well as the SQLSTATE, exactly as credit()
    // is and for the same reason: a second CHECK landing on wallets later
    // would otherwise surface as a false "Insufficient shards".
    const e = err as { code?: string; constraint?: string } | null
    if (typeof err === 'object' && e !== null
      && e.code === '23514' && e.constraint === 'wallets_balance_check') {
      throw new InsufficientFundsError(m.currency)
    }
    throw err
  }

  await record(tx, m, balanceAfter)
  return balanceAfter
}

/**
 * The append-only ledger row, written in the SAME transaction as the balance
 * it describes - solo_execution 5.3, and the half that makes a balance
 * auditable. One definition, so `credit` and `debit` cannot record the same
 * event two different ways.
 */
async function record(tx: Tx, m: Mutation, balanceAfter: number): Promise<void> {
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
}
