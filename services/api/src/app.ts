import { Hono } from 'hono'
import type { BundleStore } from './config/store.ts'
import type { Db } from './db/client.ts'
import { HttpError } from './http/auth.ts'
import { fail } from './http/errors.ts'
import type { ReplayStore } from './replays/store.ts'
import { registerAccountRoutes } from './routes/account.ts'
import { registerCreatureRoutes } from './routes/creature.ts'
import { registerFtueRoutes } from './routes/ftue.ts'
import { registerLineageRoutes } from './routes/lineage.ts'
import { registerRegionRoutes } from './routes/region.ts'
import { registerRosterRoutes } from './routes/roster.ts'
import { registerSessionRoutes } from './routes/session.ts'
import { registerSpliceRoutes } from './routes/splice.ts'
import { registerSyncRoutes } from './routes/sync.ts'
import { registerWaveRoutes } from './routes/wave.ts'
import type { SimClient } from './sim/client.ts'

export interface Deps {
  db: Db
  bundleStore: BundleStore
  simClient: SimClient
  replayStore: ReplayStore
}

/**
 * Dependencies are passed in rather than imported, so a test drives the real
 * app against a real Postgres and a real bundle store without a module mock.
 * Nothing in this service reaches for a singleton connection.
 */
export function createApp(deps: Deps): Hono {
  const app = new Hono()

  app.get('/healthz', (c) => c.json({ ok: true }))

  registerAccountRoutes(app, deps)
  registerSessionRoutes(app, deps)
  registerSyncRoutes(app, deps)
  registerWaveRoutes(app, deps)
  registerRegionRoutes(app, deps)
  registerRosterRoutes(app, deps)
  registerSpliceRoutes(app, deps)
  registerCreatureRoutes(app, deps)
  registerFtueRoutes(app, deps)
  registerLineageRoutes(app, deps)

  app.notFound(() => fail('not_found', 'No such route.'))
  app.onError((err) => {
    // HttpError is thrown deliberately (see http/auth.ts) precisely so a
    // failed check can't be ignored - it is not a bug, and treating it as an
    // unhandled 500 (plus a spurious console.error) would defeat its whole
    // point. Its own response is already the correct one to send.
    if (err instanceof HttpError) return err.response
    console.error('unhandled', err)
    return fail('internal', 'Something went wrong.')
  })

  return app
}
