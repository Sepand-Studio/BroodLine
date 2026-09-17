import { and, eq } from 'drizzle-orm'
import type { Hono } from 'hono'
import type { Deps } from '../app.ts'
import { withServer } from '../db/client.ts'
import { creatures, splices } from '../db/schema.ts'
import { loadPlayerId, requireSession } from '../http/auth.ts'
import { fail } from '../http/errors.ts'

/**
 * `GET /v1/lineage` - design 5 beat 8, `splice_confirm_spec` 5.
 *
 * THE ONE READER IN THIS CODEBASE THAT WANTS DEAD ROWS. Every other route
 * reads `creatures` through `liveCreature()` (roster/creatures.ts) and hands
 * the result to `toCreatureDto`, which THROWS on a pruned row precisely
 * because every other screen would otherwise render a creature with no
 * traits and no Instinct as though it still existed. The Lineage View is the
 * opposite screen: bible 2.1 and `splice_confirm_spec` 5 make showing a
 * splice's two consumed parents, still whole, immediately after the splice
 * the FTUE's entire lesson - "their traits live on in the pedigree" - so
 * this route must resolve through a tombstone rather than refuse it.
 *
 * `toCreatureDto` IS DELIBERATELY NOT USED HERE. Its own doc says exactly
 * why it cannot be: "PRUNED ROWS HAVE NO DTO... Throwing says so, rather
 * than emitting `null` into a response for a creature that does not exist
 * any more." That is correct for every route that calls it - a roster
 * listing, a splice's forecast, a deployment - because none of them has
 * business showing a dead ancestor at all. This route's whole job is
 * showing dead ancestors, so it builds its own projection straight off the
 * row instead: `trait1`/`tier1`/`trait2`/`tier2` are typed NULLABLE on the
 * wire (`LineageNode` in schemas.ts, unlike `CreatureDto`'s), and a pruned
 * tombstone comes back with those four null and everything 0005_loop.sql's
 * `pruned_creatures_are_stripped` guarantees still present - `species`,
 * `generation`, `isFounder`, and the parent pointers the tree resolves
 * through.
 *
 * NO LIVENESS FILTER AT ALL. `roster/creatures.ts`'s `liveCreature()` is the
 * one definition of "still on the roster" and every route above uses it
 * because a splice parent or a wave deployment must never be resolved
 * against a dead row. This route reads the OPPOSITE thing - the player's
 * whole tree, live and dead - so applying that predicate here would be the
 * same drift `liveCreature`'s own doc warns about, aimed the other way: it
 * would hide exactly the consumed parents this screen exists to show.
 *
 * `mutated` COMES FROM A LEFT JOIN ONTO `splices` ON THIS CREATURE AS THE
 * CHILD, not from `creatures` itself - the flag is rolled and stored on the
 * splice record (`splice/commit.ts`), and a creature that is not a splice's
 * child (base stock, the Founder, the tutorial pair) has no splice row to
 * join and reports `false` rather than null. A creature can be the CHILD of
 * at most one splice, so the join can never fan a row out into duplicates.
 *
 * DERIVED AND WRITES NOTHING, like `GET /v1/roster`. No Idempotency-Key, no
 * transaction beyond the one read, and two calls in a row return the same
 * tree - which is what the query's `creatureId` tiebreaker (below) is FOR:
 * `(generation, acquiredAt)` alone ties whenever a grant mints more than one
 * creature in a single statement, and `creatureId` is the one column that
 * cannot.
 *
 * SCOPED BY `server_id` AND `player_id` BOTH, with RLS enforcing the former
 * underneath - the same two-part scope every other roster read in this
 * service uses, so a player can never resolve another player's lineage.
 */

/**
 * The tiebreak-complete sort key for this route's tree, named rather than
 * inlined so a test can hold it to account. `lineage-route.test.ts`'s
 * plan-forcing proof (fix round 1) imports this exact tuple instead of a
 * hand-copied duplicate - a hand-copied one could silently stop matching
 * what the route actually orders by, which is precisely the drift that
 * would have hidden this bug from a black-box test in the first place (see
 * that test's own comment on why the naive black-box version could not).
 */
export const LINEAGE_ORDER = [creatures.generation, creatures.acquiredAt, creatures.creatureId] as const

/// The most creatures one response will draw as a tree. Far above anything
/// this phase's campaign can produce - waves 1-7 grant single digits - and
/// low enough that a pathological account cannot ask the database for an
/// unbounded row count. See the probe at the query for why exceeding it is
/// a refusal rather than a truncation.
export const MAX_LINEAGE_NODES = 2000

export function registerLineageRoutes(app: Hono, deps: Deps): void {
  app.get('/v1/lineage', async (c) => {
    const session = await requireSession(c)

    const nodes = await withServer(deps.db, session.serverId, async (tx) => {
      const playerId = await loadPlayerId(tx, session.accountId)
      if (playerId === undefined) return null

      const rows = await tx.select({ c: creatures, mutated: splices.mutated }).from(creatures)
        .leftJoin(splices, and(
          eq(splices.serverId, creatures.serverId),
          eq(splices.childId, creatures.creatureId),
        ))
        .where(and(
          eq(creatures.serverId, session.serverId),
          eq(creatures.playerId, playerId),
        ))
        // GENERATION FIRST, then acquisition order within it, THEN
        // `creatureId` as a tiebreaker that can never be shared - fix round
        // 1's finding. `(generation, acquiredAt)` alone is not unique:
        // `grantTutorialStock` inserts its pair in ONE multi-row statement
        // inside ONE transaction, and Postgres's `now()` is transaction-
        // stable rather than per-row, so both rows land with the SAME
        // `acquired_at` and the SAME `generation`. Without a further key,
        // their relative order is unspecified by the SQL and free to differ
        // between two calls - `roster/creatures.ts`'s `loadRoster` orders by
        // `creatureId` for the identical reason, on the identical hazard.
        .orderBy(...LINEAGE_ORDER)
        // BOUNDED, BUT NOT TRUNCATED - and the difference is the whole
        // reason this is a probe rather than a plain `.limit(MAX)`.
        //
        // A lineage is a TREE: every node carries `parentA`/`parentB` and
        // `LineageView` draws the edges between them. Cutting the result at
        // a limit would hand the client nodes whose parents are missing -
        // a silently wrong picture on the one screen whose job is to show
        // that consumed parents are still in the record. Unbounded, though,
        // this grows with every creature the account has ever owned, live or
        // consumed or pruned, forever.
        //
        // So: ask for one more than the cap. Under it, the tree is whole and
        // this costs a row. Over it, nothing is rendered from a partial
        // answer - the route says so instead.
        .limit(MAX_LINEAGE_NODES + 1)

      if (rows.length > MAX_LINEAGE_NODES) return 'too_large' as const

      return rows.map(({ c, mutated }) => ({
        creatureId: c.creatureId,
        species: c.species,
        generation: c.generation,
        isFounder: c.isFounder,
        name: c.name,
        parentA: c.parentA,
        parentB: c.parentB,
        consumedAt: c.consumedAt?.toISOString() ?? null,
        pruned: c.pruned,
        mutated: mutated ?? false,
        trait1: c.trait1,
        tier1: c.tier1,
        trait2: c.trait2,
        tier2: c.tier2,
      }))
    })

    // The same sentence roster.ts and region.ts use for the same condition.
    if (nodes === null) return fail('not_found', 'No player on this server for that account.')

    // Unreachable in this phase's campaign - waves 1-7 grant single digits -
    // and a loud refusal rather than a quiet half-tree if it ever is not.
    if (nodes === 'too_large') {
      return fail('invalid_request',
        `This lineage is larger than ${MAX_LINEAGE_NODES} creatures and cannot be drawn as one tree.`)
    }

    // AN EMPTY TREE IS A 200, not a 404 - roster.ts's reasoning applies
    // again: a new player who has never spliced anything owns a correct,
    // empty lineage, not a broken session.
    return c.json({ nodes })
  })
}
