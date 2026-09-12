import { createPool } from './db/client.ts'
import { migrate } from './db/migrate.ts'

/**
 * Run SEPARATELY from a deploy, never on container start.
 *
 * solo_execution 7.0: never deploy a schema migration and the code that
 * depends on it in the same step. Expand, deploy, migrate, contract - the
 * same discipline that makes rollback possible. A container that migrates on
 * boot makes every rollback a schema question.
 */
const url = process.env.DATABASE_URL
if (!url) throw new Error('DATABASE_URL must be set.')

const pool = createPool(url)
try {
  await migrate(pool)
  console.log('migrations applied')
} finally {
  await pool.end()
}
