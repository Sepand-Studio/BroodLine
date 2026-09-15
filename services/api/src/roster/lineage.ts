import { sql } from 'drizzle-orm'
import type { Tx } from '../db/client.ts'

/**
 * design 3.2's retention rule, as the one operation that enforces it.
 *
 * > Retain ancestors five generations deep. Retain all Founders permanently.
 * > Prune everything else to a tombstone {id, species, generation,
 * > was_founder}.
 *
 * PRUNING IS AN UPDATE, NOT A DELETE, and that is settled rather than
 * stylistic - 0005_loop.sql's header carries the collision it came out of.
 * Every ancestor is referenced by its own child through a composite parent
 * key with NO ACTION, so DELETE on any prunable row violates its own child's
 * key: the prune could delete nothing at all. The row stays, stripped to
 * {species, generation, is_founder}, which is also why an id can never be
 * reused - the row never goes away.
 *
 * IT RUNS ON THE SPLICE PATH rather than as a sweep - design 3.2, and the
 * same reasoning design 2.3 applies to rotation. The depth of a line changes
 * at exactly one moment, and work that can ride a write already happening
 * should not become a job with a scheduler, an internal endpoint and an auth
 * surface on it.
 */

/**
 * Bible 2.1 caps the lineage view at five generations, so an ancestor six
 * deep is never displayed and never needs to exist. Depth is measured from
 * the CHILD: its parents are depth 1, and depth 6 is the first that prunes.
 *
 * data_model 4 is what this buys: ~300 records per player against ~9,000.
 */
export const RETAINED_DEPTH = 5

/**
 * A CYCLE GUARD, and nothing to do with retention.
 *
 * `generation = max(parents) + 1` makes the parent graph acyclic, but the
 * SCHEMA does not enforce that - `generation` carries only `>= 1`, and the
 * parent keys would happily close a loop. A recursive CTE over a cycle does
 * not terminate, and this one runs inside a transaction that is destroying
 * player property, so it gets a hard bound rather than a trust.
 *
 * Far above any reachable line: the Splicing Chamber caps generation at G4
 * this phase (combat_numbers 7), and a real ancestry is at most a handful of
 * generations deep with everything past RETAINED_DEPTH + 1 already pruned by
 * the splice that created the previous depth. Rows a walk this deep could
 * miss are rows that are already tombstones.
 */
const MAX_WALK_DEPTH = 32

/**
 * Prunes every ancestor of `childId` deeper than `RETAINED_DEPTH`, and
 * returns how many rows it tombstoned.
 *
 * WHAT IT REFUSES TO TOUCH, each for its own reason:
 *
 *  - FOUNDERS, at any depth. design 3.2 retains them permanently and bible
 *    3.3 makes their names the anchor of every descendant's tree; 0005's
 *    `founders_are_never_pruned` would refuse the write anyway, so omitting
 *    this would turn a Founder deep in a line into a CHECK violation that
 *    fails the whole splice rather than a creature that is simply kept.
 *  - ROWS THAT ARE ALREADY PRUNED. Re-stripping is a no-op, but counting it
 *    is not: the return value is the evidence the prune did something.
 *  - LIVE CREATURES. An ancestor is by definition a creature some splice
 *    consumed, so `consumed_at IS NOT NULL` should always hold here - it is
 *    in the WHERE because the one thing this function must never do is strip
 *    a creature the player still has, and a bad parent pointer should cost
 *    nothing rather than a roster row.
 *  - ANOTHER PLAYER'S CREATURES. The parent keys are composite on
 *    (server_id, creature_id) and say nothing about `player_id`, so a
 *    cross-player pointer is representable even though no write path creates
 *    one. Scoping the UPDATE to the child's own owner makes "the prune
 *    cannot reach another player's roster" structural instead of incidental.
 *
 * THE SET LIST MUST NAME EVERY STRIPPED COLUMN. 0005's
 * `pruned_creatures_are_stripped` refuses the write if one is missed - which
 * is the point of that constraint: a prune that forgot `committed_to` would
 * drop the row out of every roster query while it still held a live
 * commitment, and the player could neither see nor recall it.
 *
 * MIN DEPTH, NOT ANY DEPTH. A creature can be its own descendant's ancestor
 * by more than one path (design 5.2 does not forbid splicing two creatures
 * that share an ancestor), so the same row appears at several depths. It is
 * retained if ANY path reaches it within five, which is what `MIN(depth)`
 * says and what a per-path test would get wrong in the direction that
 * destroys data.
 */
export async function pruneLineage(tx: Tx, serverId: number, childId: string): Promise<number> {
  const res = await tx.execute(sql`
    WITH RECURSIVE ancestry (creature_id, depth) AS (
        -- The child's own two parents, at depth 1. LATERAL VALUES rather
        -- than two UNIONed branches: Postgres allows the recursive term to
        -- reference the CTE exactly once, so "follow both parents" has to be
        -- one branch that yields two rows, not two branches.
        SELECT p.id, 1
          FROM creatures c
          CROSS JOIN LATERAL (VALUES (c.parent_a), (c.parent_b)) AS p (id)
         WHERE c.server_id = ${serverId} AND c.creature_id = ${childId}
           AND p.id IS NOT NULL
      UNION
        SELECT p.id, a.depth + 1
          FROM ancestry a
          JOIN creatures c ON c.server_id = ${serverId} AND c.creature_id = a.creature_id
          CROSS JOIN LATERAL (VALUES (c.parent_a), (c.parent_b)) AS p (id)
         WHERE p.id IS NOT NULL AND a.depth < ${MAX_WALK_DEPTH}
    ),
    -- UNION above already collapses a repeated (id, depth); this collapses
    -- the same id reached at DIFFERENT depths down to its shallowest.
    shallowest AS (
      SELECT creature_id, MIN(depth) AS depth FROM ancestry GROUP BY creature_id
    )
    UPDATE creatures
       SET pruned = true,
           trait_1 = NULL, tier_1 = NULL, trait_2 = NULL, tier_2 = NULL,
           instinct = NULL, name = NULL, hp_current = NULL,
           regen_until = NULL, committed_to = NULL
     WHERE server_id = ${serverId}
       AND creature_id IN (SELECT creature_id FROM shallowest WHERE depth > ${RETAINED_DEPTH})
       AND NOT pruned
       AND NOT is_founder
       AND consumed_at IS NOT NULL
       AND player_id = (SELECT player_id FROM creatures
                         WHERE server_id = ${serverId} AND creature_id = ${childId})`)

  return res.rowCount ?? 0
}
