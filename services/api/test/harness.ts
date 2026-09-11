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
      await appPool.end()
      await ownerPool.end()
      await container.stop()
    },
  }
}
