import { eq } from 'drizzle-orm'
import type { Hono } from 'hono'
import type { Deps } from '../app.ts'
import { withServer, type Tx } from '../db/client.ts'
import { players } from '../db/schema.ts'
import { requireSession } from '../http/auth.ts'
import { fail } from '../http/errors.ts'
import { loadArk } from '../map/claim.ts'
import { loadRoster, rosterCap } from '../roster/creatures.ts'

/**
 * `GET /v1/roster` - the player's live creatures.
 *
 * WHY THIS EXISTS. Phase 6's done-when is that harvest -> splice -> fight ->
 * reward closes without leaving the app, and every one of those beats needs
 * the player to see and choose creatures. Before this route the only creature
 * lists a client could hold were the incidental ones: what `node/claim` just
 * granted, and the child `splice/commit` just made. A relaunch lost every id,
 * so the loop closed only inside a single session. No earlier task was
 * assigned the listing; this is that gap closed.
 *
 * DERIVED AND WRITES NOTHING, like `GET /v1/region/state`. No
 * Idempotency-Key, no transaction beyond the two reads, and two calls in a
 * row return the same list.
 *
 * LIVENESS IS `liveCreature()` AND IS NOT WRITTEN HERE. `loadRoster` in
 * roster/creatures.ts owns the predicate, next to the definition and the two
 * other readers of it. A hand-rolled one in this file is precisely the drift
 * that header warns about - and on THIS route it is the worst-placed copy in
 * the codebase, because the roster is what the deployment screen greys out
 * from and what the Splice Chamber offers as parents. A listing that leaked a
 * consumed parent would offer a player a creature that no longer exists, and
 * `toCreatureDto` would throw outright on a pruned one.
 */
export function registerRosterRoutes(app: Hono, deps: Deps): void {
  app.get('/v1/roster', async (c) => {
    const session = await requireSession(c)

    const result = await withServer(deps.db, session.serverId, async (tx) => {
      const playerId = await loadPlayerId(tx, session.accountId)
      if (playerId === undefined) return null

      // The Ark read is what makes `cap` this player's rather than a
      // constant; `rosterCap` throws for a tier nobody authored, which is
      // the same refusal region/state makes and for the same reason - a
      // guessed cap decides whether a player's creatures are REFUSED.
      const ark = await loadArk(tx, session.serverId, playerId)
      return {
        creatures: await loadRoster(tx, session.serverId, playerId),
        cap: rosterCap(ark.hatcheryTier),
      }
    })

    // The same sentence region.ts and splice.ts use for the same condition.
    if (result === null) return fail('not_found', 'No player on this server for that account.')

    // AN EMPTY ROSTER IS A 200, not a 404. A new player who has claimed
    // nothing yet owns zero creatures, and that is a correct answer about a
    // player who exists - answering 404 would make the roster screen
    // indistinguishable from a broken session on the one launch where the
    // player most needs to be told to go and harvest something.
    return c.json(result)
  })
}

/** Inlined the way sync.ts, wave.ts and region.ts inline it - one small scoped read. */
async function loadPlayerId(tx: Tx, accountId: string): Promise<string | undefined> {
  const [player] = await tx.select().from(players).where(eq(players.accountId, accountId))
  return player?.playerId
}
