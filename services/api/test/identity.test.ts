import { exportJWK, generateKeyPair, SignJWT, type JWK } from 'jose'
import { afterAll, beforeAll, describe, expect, it } from 'vitest'
import { verifyAppleToken } from '../src/identity/apple.ts'
import { issueAccessToken, issueRefreshToken, redeemRefreshToken, verifyAccessToken } from '../src/identity/jwt.ts'
import { assignServer, bindApple, createGuest } from '../src/identity/accounts.ts'
import { accounts, servers } from '../src/db/schema.ts'
import { startTestDb, type TestDb } from './harness.ts'
import { eq } from 'drizzle-orm'

const AUDIENCE = 'com.sepandstudio.broodline'
let t: TestDb
let appleKey: Awaited<ReturnType<typeof generateKeyPair>>
let appleJwks: { keys: JWK[] }

/** Stands in for Apple, so no test reaches the network. */
async function appleTokenFor(sub: string, over: Record<string, unknown> = {}): Promise<string> {
  return new SignJWT({ ...over })
    .setProtectedHeader({ alg: 'RS256', kid: 'test-key' })
    .setIssuer('https://appleid.apple.com')
    .setAudience(AUDIENCE)
    .setSubject(sub)
    .setExpirationTime('5m')
    .setIssuedAt()
    .sign(appleKey.privateKey)
}

beforeAll(async () => {
  t = await startTestDb()
  await t.ownerDb.insert(servers).values({
    serverId: 1, region: 'us-central1', state: 'open', tickDayOfWeek: 0, tickMinuteOfDay: 1200,
  })
  appleKey = await generateKeyPair('RS256')
  appleJwks = { keys: [{ ...(await exportJWK(appleKey.publicKey)), kid: 'test-key', alg: 'RS256', use: 'sig' }] }
}, 180_000)

afterAll(async () => { await t?.stop() })

describe('Apple token verification', () => {
  it('accepts a well-formed token and returns its sub', async () => {
    const id = await verifyAppleToken(await appleTokenFor('apple-user-1'), { audience: AUDIENCE, jwks: appleJwks })
    expect(id.sub).toBe('apple-user-1')
  })

  it('rejects a token minted for another app', async () => {
    const foreign = await new SignJWT({})
      .setProtectedHeader({ alg: 'RS256', kid: 'test-key' })
      .setIssuer('https://appleid.apple.com').setAudience('com.someone.else')
      .setSubject('x').setExpirationTime('5m').setIssuedAt().sign(appleKey.privateKey)

    await expect(verifyAppleToken(foreign, { audience: AUDIENCE, jwks: appleJwks })).rejects.toThrow()
  })

  it('rejects a token signed by a key Apple does not publish', async () => {
    const attacker = await generateKeyPair('RS256')
    const forged = await new SignJWT({})
      .setProtectedHeader({ alg: 'RS256', kid: 'test-key' })
      .setIssuer('https://appleid.apple.com').setAudience(AUDIENCE)
      .setSubject('x').setExpirationTime('5m').setIssuedAt().sign(attacker.privateKey)

    await expect(verifyAppleToken(forged, { audience: AUDIENCE, jwks: appleJwks })).rejects.toThrow()
  })
})

describe('accounts', () => {
  it('assigns a server from storefront region, and the assignment is the account\'s', async () => {
    expect(await assignServer('us-central1')).toBe(1)
  })

  it('creates a guest with no credential bound', async () => {
    const guest = await createGuest(t.ownerDb, { birthdateBand: 'adult', storefrontRegion: 'us-central1' })
    const [row] = await t.ownerDb.select().from(accounts).where(eq(accounts.accountId, guest.accountId))
    expect(row!.appleSub).toBeNull()
    expect(row!.serverId).toBe(1)
  })

  it('upgrades a guest by binding the sub to the SAME account, migrating nothing', async () => {
    const guest = await createGuest(t.ownerDb, { birthdateBand: 'adult', storefrontRegion: 'us-central1' })
    const bound = await bindApple(t.ownerDb, guest.accountId, 'apple-user-2')

    // 6.4: binding the sub to the existing account id means there is no
    // merge, and therefore no merge conflict.
    expect(bound.accountId).toBe(guest.accountId)
    const [row] = await t.ownerDb.select().from(accounts).where(eq(accounts.accountId, guest.accountId))
    expect(row!.appleSub).toBe('apple-user-2')
  })

  it('refuses to bind a sub already held by another account', async () => {
    const other = await createGuest(t.ownerDb, { birthdateBand: 'adult', storefrontRegion: 'us-central1' })
    // 6.4: a player signing in with an Apple ID already bound to another
    // account is OFFERED that account, never merged into this one.
    await expect(bindApple(t.ownerDb, other.accountId, 'apple-user-2')).rejects.toThrow(/already bound/i)
  })
})

describe('session tokens', () => {
  it('round-trips an access token', async () => {
    const token = await issueAccessToken({ accountId: 'acc-1', serverId: 1 })
    expect(await verifyAccessToken(token)).toMatchObject({ accountId: 'acc-1', serverId: 1 })
  })

  it('refuses an access token as a refresh token', async () => {
    // Distinct audiences, so a stolen access token cannot be traded up for a
    // long-lived one.
    const access = await issueAccessToken({ accountId: 'acc-1', serverId: 1 })
    await expect(redeemRefreshToken(t.ownerDb, access)).rejects.toThrow()
  })

  it('refuses to refresh a deleted account', async () => {
    const doomed = await createGuest(t.ownerDb, { birthdateBand: 'adult', storefrontRegion: 'us-central1' })
    const refresh = await issueRefreshToken({ accountId: doomed.accountId, serverId: 1 })

    await t.ownerDb.update(accounts).set({ deletedAt: new Date() })
      .where(eq(accounts.accountId, doomed.accountId))

    await expect(redeemRefreshToken(t.ownerDb, refresh)).rejects.toThrow(/deleted/i)
  })
})
