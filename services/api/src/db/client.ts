import { sql } from 'drizzle-orm'
import { drizzle, type NodePgDatabase } from 'drizzle-orm/node-postgres'
import pg from 'pg'
import * as schema from './schema.ts'

export type Db = NodePgDatabase<typeof schema>
export type Tx = Parameters<Parameters<Db['transaction']>[0]>[0]

export function createPool(connectionString: string): pg.Pool {
  return new pg.Pool({
    connectionString,
    // Cloud Run scales to hundreds of instances and each opens a pool;
    // Postgres runs out of connections long before CPU. solo_execution 5.7
    // defers PgBouncer behind a hard instance cap plus a small per-instance
    // pool - this is that pool.
    max: 5,
    idleTimeoutMillis: 10_000,
  })
}

export function createDb(pool: pg.Pool): Db {
  return drizzle(pool, { schema })
}

/**
 * The ONLY sanctioned way to touch a server-scoped table.
 *
 * Every server-scoped table is under a policy comparing server_id against
 * current_setting('app.server_id'). Outside this helper the setting is
 * unset, the comparison is NULL, and every query returns zero rows - so a
 * handler that forgets this does not leak, it simply finds nothing.
 *
 * The THIRD ARGUMENT to set_config is the load-bearing one: it scopes the
 * setting to the transaction, so it cannot survive on a pooled connection
 * and be inherited by whoever checks that connection out next. That is the
 * difference between this and `SET search_path`, and isolation.test.ts pins
 * it.
 */
export async function withServer<T>(db: Db, serverId: number, fn: (tx: Tx) => Promise<T>): Promise<T> {
  // Fail closed, but visibly. Without this, a non-integer serverId (most
  // realistically NaN from an upstream parse bug) survives set_config as
  // the literal string 'NaN' - NULLIF leaves it intact, and it only fails
  // later on the policy's `::int` cast, surfacing as an opaque 500 from
  // deep inside an unrelated query instead of a clear denial here.
  if (!Number.isInteger(serverId)) {
    throw new Error(`withServer: serverId must be an integer, got ${String(serverId)}`)
  }
  return db.transaction(async (tx) => {
    await tx.execute(sql`SELECT set_config('app.server_id', ${String(serverId)}, true)`)
    return fn(tx)
  })
}
