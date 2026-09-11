import { Hono } from 'hono'
import type { BundleStore } from './config/store.ts'
import type { Db } from './db/client.ts'
import { fail } from './http/errors.ts'
import { registerAccountRoutes } from './routes/account.ts'

export interface Deps {
  db: Db
  bundleStore: BundleStore
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

  app.notFound(() => fail('not_found', 'No such route.'))
  app.onError((err) => {
    console.error('unhandled', err)
    return fail('internal', 'Something went wrong.')
  })

  return app
}
