import { eq } from 'drizzle-orm'
import { SignJWT } from 'jose'
import { randomUUID } from 'node:crypto'
import { mkdtemp, rm } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { afterAll, beforeAll, describe, expect, it } from 'vitest'
import { createApp } from '../src/app.ts'
import { clearBundleCache } from '../src/config/bundle.ts'
import { publishBundle } from '../src/config/publish.ts'
import { LocalBundleStore } from '../src/config/store.ts'
import { accounts, servers } from '../src/db/schema.ts'
import { verifyAccessToken } from '../src/identity/jwt.ts'
import { LocalReplayStore } from '../src/replays/store.ts'
import { SimClient } from '../src/sim/client.ts'
import { startTestDb, type TestDb } from './harness.ts'

// fileURLToPath, not .pathname - a path containing a space would arrive
// percent-encoded and every join below would miss.
const REPO = fileURLToPath(new URL('../../../', import.meta.url))
const SEED = join(REPO, 'config/bundles/0.1.1')

let t: TestDb
let app: ReturnType<typeof createApp>
let bundleRoot: string

beforeAll(async () => {
  t = await startTestDb()
  await t.ownerDb.insert(servers).values({
    serverId: 1, region: 'us-central1', state: 'open', tickDayOfWeek: 0, tickMinuteOfDay: 1200,
  })

  bundleRoot = await mkdtemp(join(tmpdir(), 'broodline-session-'))
  const store = new LocalBundleStore(bundleRoot)
  await publishBundle(store, SEED, '0.1.1')
  await store.setPointer('0.1.1')
  clearBundleCache()

  app = createApp({
    db: t.db, bundleStore: store, simClient: new SimClient('http://127.0.0.1:1'),
    replayStore: new LocalReplayStore(bundleRoot), // this file never submits a wave
  })
}, 240_000)

afterAll(async () => {
  await t?.stop()
  // Guarded: bundleRoot is assigned partway through beforeAll, so an
  // aborted beforeAll left this throwing ERR_INVALID_ARG_TYPE on top of the
  // real error and burying it. See wave-submit.test.ts's afterAll for the
  // full account, and masked-teardown.test.ts for the test.
  if (bundleRoot) await rm(bundleRoot, { recursive: true, force: true })
})

/** A real player, created through the real route - not a fixture shaped like one. */
async function createAccount(): Promise<{ accountId: string; accessToken: string; refreshToken: string }> {
  const res = await app.request('/v1/account', {
    method: 'POST',
    headers: { 'content-type': 'application/json', 'idempotency-key': randomUUID() },
    body: JSON.stringify({ birthdateBand: 'adult', storefrontRegion: 'us-central1' }),
  })
  return res.json() as Promise<{ accountId: string; accessToken: string; refreshToken: string }>
}

async function refresh(refreshToken: string): Promise<Response> {
  return app.request('/v1/session/refresh', {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify({ refreshToken }),
  })
}

/**
 * Sets deleted_at through the OWNER connection, mirroring
 * wave-helpers.ts's convention for reaching past the app role for test
 * setup - this is setup, not a path the app itself would take (the app's
 * own DELETE /v1/account handler updates through the app role; this helper
 * exists only to put a row into the deleted state before a test starts).
 */
async function softDelete(accountId: string): Promise<void> {
  await t.ownerDb.update(accounts).set({ deletedAt: new Date() })
    .where(eq(accounts.accountId, accountId))
}

describe('POST /v1/session/refresh', () => {
  it('exchanges a refresh token for a working access token', async () => {
    const { refreshToken } = await createAccount()
    const res = await refresh(refreshToken)
    expect(res.status).toBe(200)

    const body = await res.json() as { accessToken: string; refreshToken: string }
    // Rotation stays deferred (solo_execution 6.4) - the same token comes back.
    expect(body.refreshToken).toBe(refreshToken)

    // The point of the route: the new token actually works, proven against
    // a real authenticated route rather than just decoding the JWT.
    const syncRes = await app.request('/v1/sync', { headers: { authorization: `Bearer ${body.accessToken}` } })
    expect(syncRes.status).toBe(200)
  })

  it('refuses an access token presented as a refresh token', async () => {
    // The audience split is the whole reason there are two issue functions.
    const { accessToken } = await createAccount()
    const res = await refresh(accessToken)
    expect(res.status).toBe(401)
    expect(await res.json()).toMatchObject({ code: 'unauthorized' })
  })

  it('refuses a refresh token for a deleted account', async () => {
    // Already enforced in redeemRefreshToken; this pins it at the ROUTE,
    // which is the layer that did not exist when the check was written.
    const { accountId, refreshToken } = await createAccount()
    await softDelete(accountId)
    expect((await refresh(refreshToken)).status).toBe(401)
  })

  it('refuses a garbage token without leaking which part failed', async () => {
    const res = await refresh('not-a-token')
    expect(res.status).toBe(401)
    expect(await res.json()).toMatchObject({ code: 'unauthorized' })
  })
})

describe("extractClaims' own guard (jwt.ts) - the highest-value gap the phase 4 followups named", () => {
  // extractClaims is not exported, and it takes a JWTPayload rather than a
  // token string, so it cannot be called directly - `await
  // expect(extractClaims(...)).rejects` would not even compile. Reached
  // instead through the public path: a token minted with the SAME jose
  // primitives issue() uses and the SAME secret the test environment sets
  // (vitest.config.ts's JWT_SECRET), so it is genuinely, validly signed -
  // and carrying a claim extractClaims itself must reject.
  //
  // A garbage string is NOT this test: it fails jwtVerify's signature check
  // and never reaches extractClaims at all, which is exactly the failure
  // mode the followups file warns about - a green test that proves nothing.
  it('rejects a validly-signed access token whose serverId claim is missing', async () => {
    const key = new TextEncoder().encode(process.env.JWT_SECRET!)
    // No serverId in the payload at all. jwt.ts's own comment on
    // extractClaims explains why this matters: Number(undefined) is NaN,
    // which still satisfies `typeof x === 'number'` is false... but the real
    // danger it documents is a naive cast trusting `payload.serverId` as a
    // number when it is actually undefined - Number.isInteger(undefined) is
    // false, so this must be rejected rather than coerced into a fake 0.
    const malformed = await new SignJWT({})
      .setProtectedHeader({ alg: 'HS256' })
      .setSubject('11111111-1111-1111-1111-111111111111')
      .setIssuer('broodline-api')
      .setAudience('broodline/access')
      .setIssuedAt()
      .setExpirationTime('15m')
      .sign(key)

    await expect(verifyAccessToken(malformed)).rejects.toThrow(/serverId/i)
  })

  it('rejects a validly-signed access token whose serverId claim is a non-integer number', async () => {
    const key = new TextEncoder().encode(process.env.JWT_SECRET!)
    // 1.5, not a string and not NaN. A string claim fails extractClaims'
    // `typeof payload.serverId !== 'number'` half and short-circuits before
    // Number.isInteger ever runs, so it cannot isolate that half - it is
    // redundant with the "missing claim" case above, not complementary to
    // it. NaN looks like the obvious pick for "a number that isn't an
    // integer," but JSON.stringify(NaN) serializes to `null`, so it would
    // round-trip through the JWT payload as null and land back in the same
    // typeof-failure branch. 1.5 survives JSON round-tripping as a real
    // `number`, so this is the only payload that actually reaches
    // Number.isInteger and exercises the half jwt.ts's own comment on
    // extractClaims calls out by name.
    const malformed = await new SignJWT({ serverId: 1.5 })
      .setProtectedHeader({ alg: 'HS256' })
      .setSubject('11111111-1111-1111-1111-111111111111')
      .setIssuer('broodline-api')
      .setAudience('broodline/access')
      .setIssuedAt()
      .setExpirationTime('15m')
      .sign(key)

    await expect(verifyAccessToken(malformed)).rejects.toThrow(/serverId/i)
  })
})
