import { and, eq, isNull, sql } from 'drizzle-orm'
import type { Db, Tx } from '../db/client.ts'
import { waveIssuances } from '../db/schema.ts'
import { type IssuanceHooks, settle } from './issuance.ts'

/**
 * Task 11, design §10.2 and design §4.3's retention split.
 *
 * TWO CLAIMANTS CLOSE HERE. First, a bug: `issueWave`'s checks 1-3 can
 * refuse a start (a wave un-authored by a bundle rollback, a replay cap,
 * a stale-but-live issuance whose wave the current bundle no longer
 * carries) WITHOUT EVER REACHING check 4, which was the only place a
 * live-but-expired issuance got settled. A player whose wave becomes
 * unavailable is then stuck: their creatures are `committed_to` a row
 * nothing will ever settle, and `commitCreatures`'s own liveness guarantee
 * - committed -> cannot be spliced -> cannot be consumed -> cannot be
 * pruned - holds FOREVER instead of "for exactly as long as the issuance is
 * live". `settleExpiredForPlayer` is `issueWave`'s fix: run before check 1,
 * so a refusal at ANY check still releases a live-but-expired issuance's
 * creatures first.
 *
 * Second, the retention sweep itself - `0003_wave_issuances.sql`'s own
 * comment names it as owed ("the sweep... runs as the owner") and
 * `weakenings.md` row 7's CONTROLLER RULING books it as owed against this
 * task specifically because there was no code to weaken: design §4.3 splits
 * retention by settlement, and until `sweepRetention` existed neither half
 * had anything backing it.
 */

/**
 * Settles THIS PLAYER's live-past-expiry issuance, releasing its creatures.
 * Returns the number of rows settled - 0 or 1, since `wave_issuances_one_live`
 * (0003) allows at most one live row per (server, player) at a time.
 *
 * CALLED FIRST IN `issueWave`, before check 1, which is the whole point: a
 * refusal at ANY of `issueWave`'s five checks now runs after this, so a
 * wave that becomes unavailable (an un-authoring bundle rollback, most
 * concretely) no longer strands its deployment's creatures behind a row
 * nothing else will ever settle.
 *
 * SETTLES 'expired', never 'consumed' - design §4.3's abandoned-wave rule,
 * the same one `issueWave`'s own former check 4 applied: a row this
 * function finds was never submitted, so charging it as a played replay
 * would spend a cap slot the player never used.
 *
 * `hooks` IS `issueWave`'s OWN `IssuanceHooks`, forwarded verbatim to
 * `settle()` - not a new seam. `afterRelease` is what fix round 1's
 * reproduction (`sweep.test.ts`'s "wave/start's two-statement lock does not
 * deadlock against a splice naming the creature it just released") pauses
 * on: the ONLY window in which THIS creature's row lock is held, released
 * (committed_to already cleared, uncommitted), and nothing past this point
 * in `issueWave` has run yet - the exact window fix round 2's `afterRelease`
 * doc already describes for the settle-inside-submit case. A no-op for
 * every real caller, same as everywhere else this hook shape is used.
 */
export async function settleExpiredForPlayer(
  tx: Tx, serverId: number, playerId: string, hooks: IssuanceHooks = {},
): Promise<number> {
  const rows = await tx.select().from(waveIssuances).where(and(
    eq(waveIssuances.serverId, serverId), eq(waveIssuances.playerId, playerId),
    isNull(waveIssuances.settledAt), sql`${waveIssuances.expiresAt} <= now()`))
  let n = 0
  for (const row of rows) if (await settle(tx, row, 'expired', hooks)) n++
  return n
}

// design 4.3's asymmetry, and it is the whole point rather than an
// inconsistency to tidy up:
//
//   - 'expired' (or still-live-and-long-past-expiry): one hour past
//     expires_at, i.e. THREE hours after issuance (ISSUANCE_TTL_MS is two
//     hours). It never counts toward anything - dead weight, deleted soon
//     after nobody could still need it.
//   - 'consumed': 48 hours past issued_at, because a consumed row IS the
//     replay counter. `issueWave` check 2 counts consumed rows since the
//     UTC day boundary to enforce REPLAY_CAP_PER_DAY. Swept on the
//     'expired' schedule instead, a row issued this morning would be gone
//     by early afternoon and a check later the SAME day would undercount -
//     the cap would never bind. 48h is a safety margin over the ~24h a row
//     issued right at a day's start needs to survive to be counted for the
//     rest of that day, not the minimum itself.
const EXPIRED_GRACE_MS = 3_600_000 // one hour past expires_at - design 4.3
const CONSUMED_RETENTION_MS = 172_800_000 // 48h past issued_at - it is the replay counter

export interface SweepResult { expiredDeleted: number; consumedDeleted: number }

/**
 * Deletes retired `wave_issuances` rows past design §4.3's windows.
 *
 * RUNS AS THE OWNER ROLE. `0003_wave_issuances.sql` grants `broodline_app`
 * SELECT/INSERT/UPDATE only, with a comment saying so on purpose: a handler
 * has no reason to delete an issuance and every reason to be unable to.
 * `db` is therefore expected to be an owner-role connection (`sweep-cli.ts`'s
 * `DATABASE_URL`, or a test's `ownerDb`) - never `withServer`'s
 * `set_config('app.server_id', ...)` scoping, which is for the app role's
 * RLS policies. Every query below names `server_id` itself instead.
 *
 * ORDER MATTERS. Live-past-expiry rows are settled FIRST, in the same
 * transaction and before the 'expired' delete below - `settle` is what
 * releases a row's committed creatures (`releaseCreatures`), so settling
 * after deleting the row that named them would leave those creatures
 * committed to an issuance_id that no longer exists anywhere, forever.
 *
 * NOT SCHEDULED. `broodline_solo_execution.md` §10 lists a scheduler among
 * deferred work with no trigger met yet - this is a hand-run CLI
 * (`sweep-cli.ts`), and Task 22 records the absence of a cron as the
 * residual.
 */
export async function sweepRetention(db: Db, serverId: number, now: Date): Promise<SweepResult> {
  return db.transaction(async (tx) => {
    // Live-past-expiry rows are settled first so their creatures are released
    // before the row that names them goes away.
    const stale = await tx.select().from(waveIssuances).where(and(
      eq(waveIssuances.serverId, serverId), isNull(waveIssuances.settledAt),
      sql`${waveIssuances.expiresAt} <= ${now}`))
    for (const row of stale) await settle(tx, row, 'expired')

    const expired = await tx.delete(waveIssuances).where(and(
      eq(waveIssuances.serverId, serverId), eq(waveIssuances.settlement, 'expired'),
      sql`${waveIssuances.expiresAt} <= ${new Date(now.getTime() - EXPIRED_GRACE_MS)}`))
      .returning({ id: waveIssuances.issuanceId })

    const consumed = await tx.delete(waveIssuances).where(and(
      eq(waveIssuances.serverId, serverId), eq(waveIssuances.settlement, 'consumed'),
      sql`${waveIssuances.issuedAt} <= ${new Date(now.getTime() - CONSUMED_RETENTION_MS)}`))
      .returning({ id: waveIssuances.issuanceId })

    return { expiredDeleted: expired.length, consumedDeleted: consumed.length }
  })
}
