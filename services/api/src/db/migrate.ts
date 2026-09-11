import { readdir, readFile } from 'node:fs/promises'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import type { Pool, PoolClient } from 'pg'

const DIR = join(dirname(fileURLToPath(import.meta.url)), '../../drizzle')

// Arbitrary but fixed key for the bootstrap advisory lock below. Any int
// works - the only requirement is that every instance agrees on it.
const BOOTSTRAP_LOCK_KEY = 7_224_591

/**
 * Rolls a client's transaction back without letting a broken connection's
 * ROLLBACK error hide the real failure. A dead connection - a common cause
 * of the original error - makes ROLLBACK itself throw, and an unguarded
 * `await client.query('ROLLBACK')` in a catch block would replace the
 * caller's actual error with that one.
 */
async function safeRollback(client: PoolClient): Promise<void> {
  try {
    await client.query('ROLLBACK')
  } catch {
    // Swallowed deliberately: the caller's original error is the one that
    // matters and is already on its way out of the enclosing catch.
  }
}

/**
 * Applies every .sql file in drizzle/, in filename order, once.
 *
 * Hand-rolled rather than drizzle-kit's runner because this is thirty lines
 * and the alternative is a build-time dependency in the deploy image for
 * something that reads files and runs them in a transaction.
 *
 * Expand, deploy, migrate, contract - solo_execution 7.0. A migration and
 * the code that requires it never deploy together, which is what makes
 * rollback possible.
 */
export async function migrate(pool: Pool): Promise<void> {
  // `CREATE TABLE IF NOT EXISTS` is not safe under concurrency: two sessions
  // can both see the table absent and both attempt to create it, and one
  // loses with a duplicate-key error on a system catalog index
  // (pg_type_typname_nsp_index) rather than a clean "already exists". A
  // transaction-scoped advisory lock, held only for this one statement via
  // its own short transaction, closes that window without holding anything
  // across the rest of the run.
  const bootstrap = await pool.connect()
  try {
    await bootstrap.query('BEGIN')
    await bootstrap.query('SELECT pg_advisory_xact_lock($1)', [BOOTSTRAP_LOCK_KEY])
    await bootstrap.query(`
      CREATE TABLE IF NOT EXISTS _migrations (
        name text PRIMARY KEY,
        applied_at timestamptz NOT NULL DEFAULT now()
      )`)
    await bootstrap.query('COMMIT')
  } catch (err) {
    await safeRollback(bootstrap)
    throw err
  } finally {
    bootstrap.release()
  }

  const files = (await readdir(DIR)).filter((f) => f.endsWith('.sql')).sort()

  for (const name of files) {
    const client = await pool.connect()
    try {
      await client.query('BEGIN')
      // The unique primary key on `name` does the locking, not FOR UPDATE:
      // FOR UPDATE locks rows it RETURNS, and on a cold start there is no
      // row yet to lock, so under READ COMMITTED two racing instances would
      // both see rowCount 0 and both run the DDL - the only thing that
      // saved this before was the second instance's CREATE TABLE colliding
      // and rolling back, which is a crash-looping second instance, not a
      // clean skip. This INSERT is real serialisation: the second runner's
      // INSERT blocks on the first's uncommitted row lock, then - once that
      // transaction commits - returns zero rows via ON CONFLICT DO NOTHING,
      // and applies nothing.
      const claimed = await client.query(
        'INSERT INTO _migrations (name) VALUES ($1) ON CONFLICT DO NOTHING RETURNING 1', [name])
      if (claimed.rowCount === 1) {
        await client.query(await readFile(join(DIR, name), 'utf8'))
      }
      await client.query('COMMIT')
    } catch (err) {
      await safeRollback(client)
      throw new Error(`migration ${name} failed: ${String(err)}`)
    } finally {
      client.release()
    }
  }
}
