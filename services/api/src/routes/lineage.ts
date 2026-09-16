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
 * tree.
 *
 * SCOPED BY `server_id` AND `player_id` BOTH, with RLS enforcing the former
 * underneath - the same two-part scope every other roster read in this
 * service uses, so a player can never resolve another player's lineage.
 */
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
        // GENERATION FIRST, then acquisition order within it - a tree reads
        // top-down, and `GET /v1/roster`'s own ordering note applies again
        // here: two reads in a row must agree, and unordered Postgres is
        // free to return either.
        .orderBy(creatures.generation, creatures.acquiredAt)

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

    // AN EMPTY TREE IS A 200, not a 404 - roster.ts's reasoning applies
    // again: a new player who has never spliced anything owns a correct,
    // empty lineage, not a broken session.
    return c.json({ nodes })
  })
}
