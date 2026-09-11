import { eq } from 'drizzle-orm'
import { mkdtemp, rm } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { afterAll, beforeAll, describe, expect, it } from 'vitest'
import { createApp } from '../src/app.ts'
import { clearBundleCache } from '../src/config/bundle.ts'
import { publishBundle } from '../src/config/publish.ts'
import { LocalBundleStore } from '../src/config/store.ts'
import { withServer } from '../src/db/client.ts'
import { accounts, ledger, players, servers, wallets } from '../src/db/schema.ts'
import { HttpError } from '../src/http/auth.ts'
import { redeemRefreshToken } from '../src/identity/jwt.ts'
import { startTestDb, type TestDb } from './harness.ts'

// fileURLToPath, not .pathname - a path containing a space would arrive
// percent-encoded and every join below would miss.
const REPO = fileURLToPath(new URL('../../../', import.meta.url))
const SEED = join(REPO, 'config/bundles/0.1.0')

let t: TestDb
let app: ReturnType<typeof createApp>
let bundleRoot: string

beforeAll(async () => {
  t = await startTestDb()
  await t.ownerDb.insert(servers).values({
    serverId: 1, region: 'us-central1', state: 'open', tickDayOfWeek: 0, tickMinuteOfDay: 1200,
  })

  bundleRoot = await mkdtemp(join(tmpdir(), 'broodline-acct-'))
  const store = new LocalBundleStore(bundleRoot)
  await publishBundle(store, SEED, '0.1.0')
  await store.setPointer('0.1.0')
  clearBundleCache()

  app = createApp({ db: t.db, bundleStore: store })
}, 240_000)

afterAll(async () => {
  await t?.stop()
  await rm(bundleRoot, { recursive: true, force: true })
})

function create(key: string, body: Record<string, unknown> = { birthdateBand: 'adult', storefrontRegion: 'us-central1' }) {
  return app.request('/v1/account', {
    method: 'POST',
    headers: { 'content-type': 'application/json', 'idempotency-key': key },
    body: JSON.stringify(body),
  })
}

describe('POST /v1/account', () => {
  it('creates an account, a player, wallets and their ledger rows', async () => {
    const res = await create('k-1')
    expect(res.status).toBe(200)

    const body = await res.json() as {
      accountId: string; playerId: string; serverId: number
      accessToken: string; refreshToken: string
      balances: Record<string, number>
    }

    expect(body.serverId).toBe(1)
    expect(body.accessToken).toBeTruthy()
    // The amounts come from starter.json, not from a constant in the handler.
    expect(body.balances).toEqual({ splice_charges: 3, shards: 250 })

    const rows = await withServer(t.db, 1, (tx) =>
      tx.select().from(ledger).where(eq(ledger.playerId, body.playerId)))
    expect(rows).toHaveLength(2)
    expect(rows.every((r) => r.reasonCode === 'STARTER_GRANT')).toBe(true)
    // The key is recorded ON the ledger row, so a grant is traceable back to
    // the request that caused it.
    expect(rows.every((r) => r.idempotencyKey === 'k-1')).toBe(true)
  })

  it('grants exactly once when the same key is replayed', async () => {
    const first = await create('k-2')
    const firstBody = await first.json() as { accountId: string; playerId: string }

    const second = await create('k-2')
    expect(second.status).toBe(200)
    const secondBody = await second.json() as { accountId: string; playerId: string }

    // The identical response, not a second account.
    expect(secondBody.accountId).toBe(firstBody.accountId)

    const allAccounts = await t.ownerDb.select().from(accounts)
      .where(eq(accounts.accountId, firstBody.accountId))
    expect(allAccounts).toHaveLength(1)

    const rows = await withServer(t.db, 1, (tx) =>
      tx.select().from(ledger).where(eq(ledger.playerId, firstBody.playerId)))
    expect(rows).toHaveLength(2)
  })

  it('grants exactly once under eight simultaneous retries', async () => {
    // The network failure this actually models: a player on a bad connection
    // whose client retries while the first request is still in flight.
    const responses = await Promise.all(Array.from({ length: 8 }, () => create('k-3')))
    expect(responses.every((r) => r.status === 200)).toBe(true)

    const bodies = await Promise.all(responses.map((r) => r.json() as Promise<{ playerId: string }>))
    const playerIds = new Set(bodies.map((b) => b.playerId))
    expect(playerIds.size).toBe(1)

    const playerId = [...playerIds][0]!
    const walletRows = await withServer(t.db, 1, (tx) =>
      tx.select().from(wallets).where(eq(wallets.playerId, playerId)))
    expect(walletRows.find((w) => w.currency === 'shards')!.balance).toBe(250)

    const playerRows = await withServer(t.db, 1, (tx) =>
      tx.select().from(players).where(eq(players.playerId, playerId)))
    expect(playerRows).toHaveLength(1)
  })

  it('returns 422 when a key is reused for a different body', async () => {
    await create('k-4')
    const res = await create('k-4', { birthdateBand: 'teen', storefrontRegion: 'us-central1' })
    expect(res.status).toBe(422)
    expect(await res.json()).toMatchObject({ code: 'idempotency_key_reused' })
  })

  it('requires an idempotency key', async () => {
    const res = await app.request('/v1/account', {
      method: 'POST',
      headers: { 'content-type': 'application/json' },
      body: JSON.stringify({ birthdateBand: 'adult', storefrontRegion: 'us-central1' }),
    })
    expect(res.status).toBe(400)
    expect(await res.json()).toMatchObject({ code: 'invalid_request' })
  })
})

describe('DELETE /v1/account', () => {
  it('disables the account immediately and ends the session', async () => {
    const res = await create('k-del')
    const body = await res.json() as { accountId: string; accessToken: string; refreshToken: string }

    const del = await app.request('/v1/account', {
      method: 'DELETE',
      headers: { authorization: `Bearer ${body.accessToken}` },
    })
    expect(del.status).toBe(200)

    const [row] = await t.ownerDb.select().from(accounts)
      .where(eq(accounts.accountId, body.accountId))
    expect(row!.deletedAt).not.toBeNull()

    // The refresh path is what actually ends the session - Task 5 checks
    // deleted_at on every redemption, which is the whole reason refresh is
    // allowed to be stateless.
    await expect(redeemRefreshToken(t.ownerDb, body.refreshToken)).rejects.toThrow(/deleted/i)
  })

  it('keeps the ledger rows, because they are a financial record', async () => {
    const res = await create('k-del-2')
    const body = await res.json() as { accountId: string; playerId: string; accessToken: string }

    await app.request('/v1/account', {
      method: 'DELETE', headers: { authorization: `Bearer ${body.accessToken}` },
    })

    // 6.4: player-visible data is removed on a timer, but the ledger is
    // RETAINED in pseudonymised form - it is a financial record, and
    // store_iap's refund path depends on it.
    const rows = await withServer(t.db, 1, (tx) =>
      tx.select().from(ledger).where(eq(ledger.playerId, body.playerId)))
    expect(rows).toHaveLength(2)
  })

  it('refuses an unauthenticated deletion', async () => {
    expect((await app.request('/v1/account', { method: 'DELETE' })).status).toBe(401)
  })
})
