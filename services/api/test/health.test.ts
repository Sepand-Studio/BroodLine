import { describe, expect, it } from 'vitest'
import { createApp } from '../src/app.ts'
import type { Db } from '../src/db/client.ts'
import type { BundleStore } from '../src/config/store.ts'
import type { SimClient } from '../src/sim/client.ts'

const deps = { db: {} as Db, bundleStore: {} as BundleStore, simClient: {} as SimClient }

describe('the app', () => {
  it('answers the health check Cloud Run probes', async () => {
    const res = await createApp(deps).request('/healthz')
    expect(res.status).toBe(200)
    expect(await res.json()).toEqual({ ok: true })
  })

  it('returns the error envelope, not a bare string', async () => {
    const res = await createApp(deps).request('/v1/nope')
    expect(res.status).toBe(404)
    expect(await res.json()).toMatchObject({ code: 'not_found' })
  })
})
