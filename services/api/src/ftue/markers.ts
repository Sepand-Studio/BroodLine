import { and, eq, sql } from 'drizzle-orm'
import type { Tx } from '../db/client.ts'
import { campaignProgress } from '../db/schema.ts'

/**
 * The three one-time FTUE grants, named by the `campaign_progress` column
 * that records each one having fired. A STRING-LITERAL UNION, not `string`:
 * it is what lets `setMarker` interpolate `marker` into SQL via
 * `sql.identifier` safely - the type is the safety, not the interpolation
 * site, so widening this union is the one change that needs re-review of
 * that call.
 */
export type Marker = 'founder_granted_at' | 'tutorial_stock_granted_at' | 'wave6_pale_granted_at'

export interface Markers {
  founderGrantedAt: Date | null
  tutorialStockGrantedAt: Date | null
  wave6PaleGrantedAt: Date | null
}

export async function readMarkers(tx: Tx, serverId: number, playerId: string): Promise<Markers> {
  const [row] = await tx.select().from(campaignProgress)
    .where(and(eq(campaignProgress.serverId, serverId), eq(campaignProgress.playerId, playerId)))
  return {
    founderGrantedAt: row?.founderGrantedAt ?? null,
    tutorialStockGrantedAt: row?.tutorialStockGrantedAt ?? null,
    wave6PaleGrantedAt: row?.wave6PaleGrantedAt ?? null,
  }
}

/**
 * True iff THIS call wrote the marker. Inserts the progress row when absent -
 * the Founder grant fires ON the first wave clear, so it has to be able to
 * write a marker before `advanceCampaign` has ever written a
 * `campaign_progress` row for this player.
 *
 * RETURNS WHETHER THIS CALL SET IT, not merely that nothing threw - the same
 * shape and the same reasoning as `wave/issuance.ts`'s `settle()`. A grant
 * gated on "no exception" rather than on the write itself lets two
 * concurrent callers both observe success and both grant: the UPDATE below
 * is unconditionally reachable (unlike a bare INSERT, it never errors on a
 * duplicate key), so "did not throw" is true of every caller regardless of
 * who actually flipped the column. Gating on `rowCount > 0` from
 * `WHERE <marker> IS NULL` is what makes exactly one of two concurrent
 * callers see `true`.
 *
 * `sql.identifier(marker)`, not the raw string, even though `Marker` makes
 * that safe today - the identifier form keeps the safety property in the
 * type rather than in this call site, so a later widening of `Marker` fails
 * loudly instead of quietly becoming an injection.
 */
export async function setMarker(tx: Tx, serverId: number, playerId: string, marker: Marker): Promise<boolean> {
  await tx.insert(campaignProgress).values({ serverId, playerId }).onConflictDoNothing()
  const res = await tx.execute(sql`
    UPDATE campaign_progress SET ${sql.identifier(marker)} = now(), updated_at = now()
    WHERE server_id = ${serverId} AND player_id = ${playerId} AND ${sql.identifier(marker)} IS NULL`)
  return (res.rowCount ?? 0) > 0
}
