import { createDb, createPool } from './db/client.ts'
import { sweepRetention } from './wave/sweep.ts'

/**
 * Run BY HAND, like migrate-cli.ts - never on container start and never
 * scheduled. `broodline_solo_execution.md` §10 lists a scheduler among
 * deferred work with no trigger met yet; Task 22 records the absence of a
 * cron here as the residual this task leaves behind.
 *
 * `DATABASE_URL` MUST NAME THE OWNER, exactly as migrate-cli.ts's does -
 * substitute the same superuser connection string used for migrations, not
 * the `broodline_app` one the deployed service runs with.
 * `0003_wave_issuances.sql` grants `broodline_app` SELECT/INSERT/UPDATE on
 * `wave_issuances` and deliberately no DELETE, with a comment saying a
 * handler has no reason to delete an issuance and every reason to be unable
 * to - `sweepRetention` is the one thing in this codebase that does, and it
 * has to run as a role that can.
 */
const url = process.env.DATABASE_URL
if (!url) throw new Error('DATABASE_URL must be set.')

// The only server any bundle has authored content for so far - see the same
// constant, inlined the same way, in every test file that seeds one server.
// Sweeping a second server is a one-line change here whenever a second one
// exists; nothing about sweepRetention itself is single-server.
const SERVER_ID = 1

const pool = createPool(url)
try {
  const db = createDb(pool)
  const result = await sweepRetention(db, SERVER_ID, new Date())
  console.log(
    `swept server ${SERVER_ID}: ${result.expiredDeleted} expired issuance(s), ` +
    `${result.consumedDeleted} consumed issuance(s) deleted`)
} finally {
  await pool.end()
}
