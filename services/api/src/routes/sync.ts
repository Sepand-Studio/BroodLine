import { and, count, eq, sql } from 'drizzle-orm'
import type { Hono } from 'hono'
import type { Deps } from '../app.ts'
import { loadBundle } from '../config/bundle.ts'
import { withServer } from '../db/client.ts'
import { campaignProgress, creatures, players, splices, wallets } from '../db/schema.ts'
import { readMarkers } from '../ftue/markers.ts'
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

      // Three more indexed reads on the player's own rows - the p99 budget
      // comment below is what bounds adding a fourth without measuring.
      const [founder] = await tx.select({
        named: sql<boolean>`coalesce(bool_or(${creatures.name} IS NOT NULL), false)`,
      }).from(creatures).where(and(
        eq(creatures.serverId, session.serverId),
        eq(creatures.playerId, player.playerId),
        eq(creatures.isFounder, true)))

      const [spliceCount] = await tx.select({ n: count() }).from(splices)
        .where(and(eq(splices.serverId, session.serverId), eq(splices.playerId, player.playerId)))

      const markers = await readMarkers(tx, session.serverId, player.playerId)

      return { player, walletRows, progress, founder, spliceCount, markers }
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
      // bundleVersion and minimumClientVersion shipped from Phase 4; tabs,
      // waves and traits join them here (Task 5, design §4/§5 beat 1) so the
      // navigation shell derives its tab bar, campaign list and trait Codex
      // from this one cold-start call rather than a second round trip.
      config: {
        bundleVersion: bundle.version,
        minimumClientVersion: bundle.minimumClientVersion,
        tabs: bundle.progression.tabs,
        waves: bundle.waves.map((w) => ({ id: w.id, reward: w.reward ?? null })),
        traits: bundle.traits.map((t) => ({ id: t.id, species: t.species, counters: t.counters })),
      },
      // What the client derives the current tutorial beat from - nothing
      // about FTUE progress is stored on the client. `tutorialStockGranted`
      // is the only marker read today; the other two land with the grant
      // paths that set them (Tasks 6-8).
      ftue: {
        founderNamed: snapshot.founder?.named ?? false,
        tutorialStockGranted: snapshot.markers.tutorialStockGrantedAt !== null,
        splices: Number(snapshot.spliceCount?.n ?? 0),
      },
    })
  })
}
