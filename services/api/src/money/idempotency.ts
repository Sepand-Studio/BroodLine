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

function isUniqueViolation(err: unknown): boolean {
  return typeof err === 'object' && err !== null && (err as { code?: string }).code === '23505'
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
        .set({ status: 'completed', responseBody: body as unknown })
        .where(and(eq(idempotencyKeys.serverId, serverId), eq(idempotencyKeys.key, key)))

      return { status: 'fresh', body }
    })
  } catch (err) {
    if (!isUniqueViolation(err)) throw err

    return withServer(db, serverId, async (tx): Promise<Idempotent<T>> => {
      const [row] = await tx.select().from(idempotencyKeys)
        .where(and(eq(idempotencyKeys.serverId, serverId), eq(idempotencyKeys.key, key)))

      if (row === undefined) {
        // The winner raised 23505 and then rolled back. Vanishingly rare and
        // not silently retryable - a retry would race the same way.
        throw new Error(`idempotency key ${key} conflicted and then vanished; retry the request.`)
      }
      if (row.requestHash !== requestHash) throw new IdempotencyMismatchError(key)

      return { status: 'replayed', body: row.responseBody as T }
    })
  }
}
