import { describe, expect, it } from 'vitest'
import { createApp } from '../src/app.ts'

describe('the app', () => {
  it('answers the health check Cloud Run probes', async () => {
    const res = await createApp().request('/healthz')
    expect(res.status).toBe(200)
    expect(await res.json()).toEqual({ ok: true })
  })

  it('returns the error envelope, not a bare string', async () => {
    const res = await createApp().request('/v1/nope')
    expect(res.status).toBe(404)
    // The shape is the contract. A client switching on `code` must find one.
    expect(await res.json()).toMatchObject({ code: 'not_found' })
  })
})
