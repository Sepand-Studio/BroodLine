import { readdir, readFile } from 'node:fs/promises'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import type { Pool } from 'pg'

const DIR = join(dirname(fileURLToPath(import.meta.url)), '../../drizzle')

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
  await pool.query(`
    CREATE TABLE IF NOT EXISTS _migrations (
      name text PRIMARY KEY,
      applied_at timestamptz NOT NULL DEFAULT now()
    )`)

  const files = (await readdir(DIR)).filter((f) => f.endsWith('.sql')).sort()

  for (const name of files) {
    const client = await pool.connect()
    try {
      await client.query('BEGIN')
      // Skip-if-applied inside the transaction, so two instances racing on a
      // cold start cannot both apply the same file.
      const done = await client.query('SELECT 1 FROM _migrations WHERE name = $1 FOR UPDATE', [name])
      if (done.rowCount === 0) {
        await client.query(await readFile(join(DIR, name), 'utf8'))
        await client.query('INSERT INTO _migrations (name) VALUES ($1)', [name])
      }
      await client.query('COMMIT')
    } catch (err) {
      await client.query('ROLLBACK')
      throw new Error(`migration ${name} failed: ${String(err)}`)
    } finally {
      client.release()
    }
  }
}
