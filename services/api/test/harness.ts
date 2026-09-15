import { PostgreSqlContainer, type StartedPostgreSqlContainer } from '@testcontainers/postgresql'
import type pg from 'pg'
import { createDb, createPool, type Db } from '../src/db/client.ts'
import { migrate } from '../src/db/migrate.ts'

export interface TestDb {
  db: Db          // connected as broodline_app - non-superuser, RLS applies
  ownerDb: Db     // connected as the superuser - migrations and fixtures only
  pool: pg.Pool
  stop: () => Promise<void>
}

/**
 * `await pool.end()` is NOT "the connections are closed." It is only "close
 * was requested," and the difference is a real, reproduced test failure.
 *
 * pg-pool's end() drains via _pulseQueue, which for each idle client calls
 * _remove. _remove splices the client out of the pool's `_clients` array
 * SYNCHRONOUSLY and only then calls client.end(), which is asynchronous -
 * it still has to write a Terminate message and complete the FIN handshake.
 * end()'s own promise is resolved the moment `_clients.length` hits zero,
 * so it settles while every socket is still open. Measured directly against
 * this harness: end() resolved in 0ms with three sockets reporting
 * destroyed=false, writable=true, readyState='open'.
 *
 * That is why stop()'s `await appPool.end(); await ownerPool.end(); await
 * container.stop()` looked correct and was not - the awaits order the
 * REQUESTS, not the closes, so container.stop() races sockets that are
 * still up. When docker's SIGINT reached Postgres first (the postgres image
 * sets STOPSIGNAL SIGINT, i.e. a fast shutdown), the backend answered the
 * still-open connections with FATAL 57P01 and pg re-emitted it on the pool.
 * Under this suite's default file parallelism - measured at eleven Postgres
 * containers up at once on this twelve-core machine - that race was lost
 * about one full-suite run in eight, and it presented as a file-level
 * failure with every test green: "19 passed, 143 passed, 1 error."
 *
 * The 'remove' event is the signal end()'s promise is not: pg-pool emits it
 * from inside client.end()'s completion callback, so it fires only after
 * the socket is genuinely destroyed (verified: destroyed=false before,
 * true after). Count how many clients the pool holds and wait for that many.
 *
 * Reading totalCount and attaching the listener happen in the same
 * synchronous tick ON PURPOSE - no await may be introduced between them, or
 * a client removed in the gap would be a 'remove' this function never
 * counted and it would wait forever for an event that already fired.
 */
export async function drainPool(pool: pg.Pool): Promise<void> {
  const expected = pool.totalCount
  let seen = 0
  const drained = expected === 0
    ? Promise.resolve()
    : new Promise<void>((resolve) => {
        pool.on('remove', () => { if (++seen >= expected) resolve() })
      })

  await pool.end()
  await drained
}

/**
 * Real Postgres, never a mock and never SQLite.
 *
 * RLS, set_config's transaction scoping, unique-violation semantics and
 * transactional rollback are the four properties this phase's gates test,
 * and all four are exactly what a substitute fakes.
 */
export async function startTestDb(): Promise<TestDb> {
  const container: StartedPostgreSqlContainer = await new PostgreSqlContainer('postgres:16-alpine').start()

  const ownerPool = createPool(container.getConnectionUri())
  await migrate(ownerPool)

  // The app role exists but has NOLOGIN, so give it a password and a way in.
  // Kept here rather than in the migration because production grants its
  // login through a Cloud SQL IAM binding, not a password.
  await ownerPool.query(`ALTER ROLE broodline_app LOGIN PASSWORD 'app'`)

  const appUri = new URL(container.getConnectionUri())
  appUri.username = 'broodline_app'
  appUri.password = 'app'
  const appPool = createPool(appUri.toString())

  return {
    db: createDb(appPool),
    ownerDb: createDb(ownerPool),
    pool: appPool,
    stop: async () => {
      // drainPool, never a bare pool.end() - see its comment. end() resolves
      // with the sockets still open, and stopping the container underneath
      // an open connection is what made this suite fail roughly one run in
      // eight with a FATAL 57P01 and no failing assertion.
      await drainPool(appPool)
      await drainPool(ownerPool)
      await container.stop()
    },
  }
}
