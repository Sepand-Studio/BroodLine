import { and, eq } from 'drizzle-orm'
import { withServer, type Db, type Tx } from '../db/client.ts'
import { idempotencyKeys } from '../db/schema.ts'

export interface Idempotent<T> {
  status: 'fresh' | 'replayed'
  body: T
}

export class IdempotencyMismatchError extends Error {
  constructor(readonly key: string) {
    super('This idempotency key was used for a different request.')
    this.name = 'IdempotencyMismatchError'
  }
}

/**
 * Gated on the CONSTRAINT, not just the SQLSTATE. A bare 23505 check is safe
 * only as long as idempotency_keys_pkey is the only unique constraint `fn`
 * can ever hit - true today (credit()'s wallet write is ON CONFLICT DO
 * UPDATE, and the ledger PK carries a random uuid), but false the moment a
 * later task's `fn` touches accounts.apple_sub or players_by_account.
 *
 * Without this gate, a foreign 23505 from inside `fn` would be caught here,
 * gets misread as "this key already ran", and either (a) sequentially: the
 * key row rolled back with the aborted transaction, so the replay read finds
 * nothing and throws the "vanished" error - discarding the real domain error
 * and telling the caller to retry something that can never succeed - or (b)
 * concurrently: another caller with the same key/hash commits first, and the
 * replay read returns ITS response body as a success for a mutation that
 * never happened. In a currency system that is silent, wrong money.
 *
 * idempotency_keys_pkey is the name Postgres generates for the unnamed
 * composite PRIMARY KEY (server_id, key) in 0001_tables.sql - confirmed by
 * querying pg_constraint against a live migrated database, not assumed.
 */
function isIdempotencyKeyConflict(err: unknown): boolean {
  const e = err as { code?: string; constraint?: string } | null
  return typeof err === 'object' && e !== null
    && e.code === '23505' && e.constraint === 'idempotency_keys_pkey'
}

/**
 * Runs `fn` at most once per (server, key), whatever the network does.
 *
 * solo_execution 6.3: the key is inserted as the FIRST statement inside the
 * same transaction as the mutation. On unique violation the stored response
 * is returned; if the key matches but the request hash differs, 422 - that is
 * a client bug, and returning another request's response would be worse than
 * failing.
 *
 * WHY A PLAIN INSERT RATHER THAN ON CONFLICT DO NOTHING: the conflicting
 * insert is what makes this correct under concurrency. Postgres BLOCKS the
 * second inserter until the first transaction resolves, then raises 23505 if
 * it committed - so the loser learns the winner's outcome. ON CONFLICT DO
 * NOTHING returns immediately instead, and the follow-up SELECT under READ
 * COMMITTED cannot see the winner's uncommitted row: both transactions would
 * conclude the key was free.
 *
 * The unique violation poisons the transaction, so the replay read happens in
 * a second one. That is not a race - the winner has committed by then, which
 * is the only reason the violation was raised.
 */
export async function withIdempotency<T>(
  db: Db,
  serverId: number,
  key: string,
  requestHash: string,
  fn: (tx: Tx) => Promise<T>,
): Promise<Idempotent<T>> {
  try {
    return await withServer(db, serverId, async (tx): Promise<Idempotent<T>> => {
      await tx.insert(idempotencyKeys).values({
        serverId, key, requestHash, status: 'in_flight',
      })

      const body = await fn(tx)

      await tx.update(idempotencyKeys)
        // Drizzle's mapUpdateSet drops `undefined` fields from the SET list
        // entirely, so a void `fn` (undefined body) would otherwise leave
        // response_body at its column default of NULL. That is a silent
        // divergence: the fresh caller gets `body: undefined` back in memory
        // while every replayer reads `body: null` from the row - two
        // different answers to the same request, which is exactly what this
        // module exists to prevent. Normalize explicitly instead of relying
        // on NULL and undefined happening to compare loosely equal.
        .set({ status: 'completed', responseBody: (body === undefined ? null : body) as unknown })
        .where(and(eq(idempotencyKeys.serverId, serverId), eq(idempotencyKeys.key, key)))

      return { status: 'fresh', body }
    })
  } catch (err) {
    if (!isIdempotencyKeyConflict(err)) throw err

    return withServer(db, serverId, async (tx): Promise<Idempotent<T>> => {
      const [row] = await tx.select().from(idempotencyKeys)
        .where(and(eq(idempotencyKeys.serverId, serverId), eq(idempotencyKeys.key, key)))

      if (row === undefined) {
        // The winner raised 23505 on idempotency_keys_pkey and then rolled
        // back. With the constraint gate above this is genuinely rare -
        // vanishingly so, and not silently retryable, since a retry would
        // race the same way - so the original error is worth keeping.
        throw new Error(`idempotency key ${key} conflicted and then vanished; retry the request.`, { cause: err })
      }
      if (row.requestHash !== requestHash) throw new IdempotencyMismatchError(key)

      return { status: 'replayed', body: row.responseBody as T }
    })
  }
}
