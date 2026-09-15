import { sql } from 'drizzle-orm'
import { drizzle, type NodePgDatabase } from 'drizzle-orm/node-postgres'
import pg from 'pg'
import * as schema from './schema.ts'

export type Db = NodePgDatabase<typeof schema>
export type Tx = Parameters<Parameters<Db['transaction']>[0]>[0]

export function createPool(connectionString: string): pg.Pool {
  const pool = new pg.Pool({
    connectionString,
    // Cloud Run scales to hundreds of instances and each opens a pool;
    // Postgres runs out of connections long before CPU. solo_execution 5.7
    // defers PgBouncer behind a hard instance cap plus a small per-instance
    // pool - this is that pool.
    max: 5,
    idleTimeoutMillis: 10_000,
    // The idempotency design (money/idempotency.ts) depends on a conflicting
    // INSERT BLOCKING the loser until the winner's transaction resolves -
    // that is the whole mechanism. Without a cap, a mutation stuck on a
    // network partition (or any other hang) holds that row lock forever:
    // every retry of the same key queues up behind it, and each blocked
    // retry burns one of this pool's five slots. Five stuck retries is the
    // instance's entire pool gone - a single hung query on the service's
    // only mutation path turns into a full instance-wide deadlock. These
    // options bound that: lock_timeout aborts a statement that waits too
    // long for a row lock (freeing the slot with a clear error rather than
    // hanging it), and statement_timeout is the backstop for any other
    // runaway query, set looser since some legitimate statements (batched
    // reads, migrations run outside this pool) may run longer than a lock
    // wait reasonably should.
    options: '-c lock_timeout=5000 -c statement_timeout=30000',
  })

  // NOT optional, and not a test concession. pg.Pool extends EventEmitter,
  // and an EventEmitter with zero 'error' listeners RETHROWS what it is
  // asked to emit - so the line below is the difference between logging a
  // dropped connection and killing the process.
  //
  // A query's error reaches whoever awaited the query. This channel is for
  // the other kind: a client sitting IDLE in the pool whose backend goes
  // away underneath it. Nobody is awaiting that client, so pg-pool's
  // internal idle listener removes it and re-emits here (pg-pool's
  // makeIdleListener -> pool.emit('error', err, client)). With no listener,
  // Node turns it into an uncaught exception from inside a socket data
  // handler, which no try/catch anywhere up the stack can intercept.
  //
  // Idle backends DO go away in normal operation: Cloud SQL maintenance and
  // failover both drop live connections with FATAL 57P01 ("terminating
  // connection due to administrator command"), and so does the test
  // harness's own container.stop(). Without this handler a routine Cloud
  // SQL restart takes the Cloud Run instance with it, and in the test suite
  // it was a ~1-in-8 whole-run failure attributed to whichever file's
  // worker happened to be holding the connection - see harness.ts's stop()
  // for the teardown half of that story.
  //
  // The client is already removed from the pool by the time this fires;
  // there is nothing to clean up and nothing to retry. Log and continue -
  // the next checkout just opens a fresh connection.
  pool.on('error', (err) => {
    console.error('pg pool: idle client error, connection discarded', err)
  })

  return pool
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
