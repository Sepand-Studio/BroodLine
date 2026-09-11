import { Hono } from 'hono'
import { fail } from './http/errors.ts'

/**
 * A factory rather than a module-level singleton, so every test gets a clean
 * app with no listener and no shared state. src/index.ts is the only place
 * that binds a port.
 */
export function createApp(): Hono {
  const app = new Hono()

  // Cloud Run's health check. Deliberately touches nothing - a probe that
  // queries the database turns a slow query into a rolled-back deploy.
  app.get('/healthz', (c) => c.json({ ok: true }))

  app.notFound(() => fail('not_found', 'No such route.'))

  app.onError((err) => {
    // Never leak an exception message to a client; it is the fastest route
    // from a stack trace to a schema disclosure.
    console.error('unhandled', err)
    return fail('internal', 'Something went wrong.')
  })

  return app
}
