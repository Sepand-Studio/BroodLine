import { and, eq } from 'drizzle-orm'
import type { Hono } from 'hono'
import type { Deps } from '../app.ts'
import { loadBundle } from '../config/bundle.ts'
import { withServer } from '../db/client.ts'
import { campaignProgress, players, wallets } from '../db/schema.ts'
import { isBelow, requireSession } from '../http/auth.ts'
import { fail } from '../http/errors.ts'

/**
 * The one cold-start call - solo_execution 6.2. Settled balances, campaign
 * progress, the config bundle version and minimumClientVersion in a single
 * round trip, because this is the request every client makes before it can
 * do anything else, and it carries the phase's only real SLO: p99 300ms.
 */
export function registerSyncRoutes(app: Hono, deps: Deps): void {
  app.get('/v1/sync', async (c) => {
    // requireSession throws HttpError on a failed check; app.ts's onError
    // special-cases it and returns its response as-is, so there is no
    // try/catch boilerplate needed here.
    const session = await requireSession(c)

    const bundle = await loadBundle(deps.bundleStore)

    // Checked BEFORE any query. A client that must update should not cost a
    // database round trip, and this endpoint is on every cold start with a
    // p99 budget of 300 ms.
    const clientVersion = c.req.header('x-client-version')
    if (clientVersion !== undefined && isBelow(clientVersion, bundle.minimumClientVersion)) {
      return fail('client_too_old', 'This version of Broodline can no longer connect. Please update.', {
        minimumClientVersion: bundle.minimumClientVersion,
      })
    }

    const snapshot = await withServer(deps.db, session.serverId, async (tx) => {
      const [player] = await tx.select().from(players)
        .where(eq(players.accountId, session.accountId))
      if (player === undefined) return null

      const walletRows = await tx.select().from(wallets)
        .where(eq(wallets.playerId, player.playerId))

      const [progress] = await tx.select().from(campaignProgress)
        .where(and(
          eq(campaignProgress.serverId, session.serverId),
          eq(campaignProgress.playerId, player.playerId)))

      return { player, walletRows, progress }
    })

    if (snapshot === null) return fail('not_found', 'No player on this server for that account.')

    const balances: Record<string, number> = {}
    // Straight off the wallet rows. solo_execution 5.3: balances are NEVER
    // derived by summing the ledger at read time - that is how a ledger
    // becomes too expensive to keep, and the invariant job is what checks the
    // two against each other.
    for (const w of snapshot.walletRows) balances[w.currency] = w.balance

    return c.json({
      player: {
        playerId: snapshot.player.playerId,
        serverId: snapshot.player.serverId,
      },
      balances,
      campaign: {
        highestWaveCleared: snapshot.progress?.highestWaveCleared ?? 0,
        milestonesClaimed: snapshot.progress?.milestonesClaimed ?? 0,
      },
      // Nothing accrues yet: 5.5 puts settlement on claim, on depletion, or
      // on a raid resolving, and Phase 4 has no nodes. The shape is here so
      // Phase 6 fills it rather than adding it.
      timers: [],
      config: {
        bundleVersion: bundle.version,
        minimumClientVersion: bundle.minimumClientVersion,
      },
    })
  })
}
