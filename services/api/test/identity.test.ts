import { errors, exportJWK, exportSPKI, generateKeyPair, SignJWT, type JWK } from 'jose'
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

/**
 * Captures a rejection's REASON instead of just asserting "it threw" - a bare
 * `rejects.toThrow()` passes on ANY error, including one raised by an
 * unrelated bug (a bad uuid reaching Postgres, a typo in a fixture), which is
 * exactly how a test can stay green after the property it claims to check
 * has been deleted. If the promise resolves, this itself throws, so a test
 * built on it cannot silently pass on a non-rejection either.
 */
async function captureRejection<T>(promise: Promise<T>): Promise<unknown> {
  return promise.then(
    () => { throw new Error('Expected the promise to reject, but it resolved.') },
    (err: unknown) => err,
  )
}

beforeAll(async () => {
  t = await startTestDb()
  // servers has no app-role INSERT grant (only SELECT) - this fixture MUST
  // go through ownerDb. Everything below it that exercises an actual
  // identity code path goes through t.db, the non-superuser connection.
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

    // Typed, not a bare toThrow(): this is jose's actual claim-validation
    // error naming the exact claim that failed, confirmed by executing this
    // scenario against jose directly before writing the assertion.
    const err = await captureRejection(verifyAppleToken(foreign, { audience: AUDIENCE, jwks: appleJwks }))
    expect(err).toBeInstanceOf(errors.JWTClaimValidationFailed)
    expect((err as { claim?: string }).claim).toBe('aud')
  })

  it('rejects a token from the wrong issuer', async () => {
    const wrongIssuer = await new SignJWT({})
      .setProtectedHeader({ alg: 'RS256', kid: 'test-key' })
      .setIssuer('https://evil.example').setAudience(AUDIENCE)
      .setSubject('x').setExpirationTime('5m').setIssuedAt().sign(appleKey.privateKey)

    const err = await captureRejection(verifyAppleToken(wrongIssuer, { audience: AUDIENCE, jwks: appleJwks }))
    expect(err).toBeInstanceOf(errors.JWTClaimValidationFailed)
    expect((err as { claim?: string }).claim).toBe('iss')
  })

  it('rejects a token signed by a key Apple does not publish', async () => {
    const attacker = await generateKeyPair('RS256')
    const forged = await new SignJWT({})
      .setProtectedHeader({ alg: 'RS256', kid: 'test-key' })
      .setIssuer('https://appleid.apple.com').setAudience(AUDIENCE)
      .setSubject('x').setExpirationTime('5m').setIssuedAt().sign(attacker.privateKey)

    // This is the ONLY test proving signature verification itself works, so
    // it must assert the specific signature-verification error, not any
    // error - confirmed to be JWSSignatureVerificationFailed by execution.
    const err = await captureRejection(verifyAppleToken(forged, { audience: AUDIENCE, jwks: appleJwks }))
    expect(err).toBeInstanceOf(errors.JWSSignatureVerificationFailed)
  })

  it('rejects a token with alg: none', async () => {
    // Built by hand: jose's own signer will not produce an "alg: none"
    // token, and that is the point - this is the forgery a real attacker
    // sends, constructed the same way they would.
    const header = Buffer.from(JSON.stringify({ alg: 'none', kid: 'test-key' })).toString('base64url')
    const payload = Buffer.from(JSON.stringify({
      iss: 'https://appleid.apple.com', aud: AUDIENCE, sub: 'attacker',
      exp: Math.floor(Date.now() / 1000) + 300,
    })).toString('base64url')
    const noneToken = `${header}.${payload}.`

    const err = await captureRejection(verifyAppleToken(noneToken, { audience: AUDIENCE, jwks: appleJwks }))
    expect(err).toBeInstanceOf(errors.JOSEAlgNotAllowed)
  })

  it('rejects the classic algorithm-confusion attack: HS256 signed with the RSA public key as the HMAC secret', async () => {
    // The attack this app is actually exposed to if `algorithms` were ever
    // left open: the RSA public key is, well, public - an attacker can use
    // it as an HMAC secret and self-sign a token, if the verifier doesn't
    // pin the algorithm and would otherwise reuse this same key to verify.
    const publicKeyPem = await exportSPKI(appleKey.publicKey)
    const confusion = await new SignJWT({})
      .setProtectedHeader({ alg: 'HS256', kid: 'test-key' })
      .setIssuer('https://appleid.apple.com').setAudience(AUDIENCE)
      .setSubject('attacker').setExpirationTime('5m').setIssuedAt()
      .sign(new TextEncoder().encode(publicKeyPem))

    const err = await captureRejection(verifyAppleToken(confusion, { audience: AUDIENCE, jwks: appleJwks }))
    expect(err).toBeInstanceOf(errors.JOSEAlgNotAllowed)
  })

  it('rejects an expired Apple token', async () => {
    const expired = await new SignJWT({})
      .setProtectedHeader({ alg: 'RS256', kid: 'test-key' })
      .setIssuer('https://appleid.apple.com').setAudience(AUDIENCE)
      .setSubject('x').setExpirationTime('-10s').setIssuedAt().sign(appleKey.privateKey)

    const err = await captureRejection(verifyAppleToken(expired, { audience: AUDIENCE, jwks: appleJwks }))
    expect(err).toBeInstanceOf(errors.JWTExpired)
  })

  it('rejects a token with an empty sub - apple.ts\'s own guard, not jose\'s', async () => {
    // jose itself is happy to verify a token whose sub is "" (confirmed by
    // execution); the empty-sub rejection is apple.ts's own guard, and this
    // is the only test that exercises it rather than assuming it fires.
    const emptySub = await appleTokenFor('')
    await expect(verifyAppleToken(emptySub, { audience: AUDIENCE, jwks: appleJwks })).rejects.toThrow(/no subject/i)
  })
})

describe('accounts', () => {
  it('assigns a server from storefront region, and the assignment is the account\'s', async () => {
    expect(await assignServer('us-central1')).toBe(1)
  })

  it('creates a guest with no credential bound', async () => {
    const guest = await createGuest(t.db, { birthdateBand: 'adult', storefrontRegion: 'us-central1' })
    const [row] = await t.db.select().from(accounts).where(eq(accounts.accountId, guest.accountId))
    expect(row!.appleSub).toBeNull()
    expect(row!.serverId).toBe(1)
  })

  it('upgrades a guest by binding the sub to the SAME account, migrating nothing', async () => {
    const guest = await createGuest(t.db, { birthdateBand: 'adult', storefrontRegion: 'us-central1' })
    const bound = await bindApple(t.db, guest.accountId, 'apple-user-2')

    // 6.4: binding the sub to the existing account id means there is no
    // merge, and therefore no merge conflict.
    expect(bound.accountId).toBe(guest.accountId)
    const [row] = await t.db.select().from(accounts).where(eq(accounts.accountId, guest.accountId))
    expect(row!.appleSub).toBe('apple-user-2')
  })

  it('refuses to bind a sub already held by another account', async () => {
    const other = await createGuest(t.db, { birthdateBand: 'adult', storefrontRegion: 'us-central1' })
    // 6.4: a player signing in with an Apple ID already bound to another
    // account is OFFERED that account, never merged into this one.
    await expect(bindApple(t.db, other.accountId, 'apple-user-2')).rejects.toThrow(/already bound/i)
  })

  it('refuses to REBIND an account that already has a different apple sub - bindApple binds, it never rebinds', async () => {
    const guest = await createGuest(t.db, { birthdateBand: 'adult', storefrontRegion: 'us-central1' })
    await bindApple(t.db, guest.accountId, 'apple-user-original')

    await expect(bindApple(t.db, guest.accountId, 'apple-user-hijack')).rejects.toThrow(/already.*bound|bound.*already/i)

    // Not just that it threw - the credential must be UNCHANGED.
    const [row] = await t.db.select().from(accounts).where(eq(accounts.accountId, guest.accountId))
    expect(row!.appleSub).toBe('apple-user-original')
  })

  it('races two binds to the same sub safely: one wins, one gets the friendly error, never a raw 23505', async () => {
    const a = await createGuest(t.db, { birthdateBand: 'adult', storefrontRegion: 'us-central1' })
    const b = await createGuest(t.db, { birthdateBand: 'adult', storefrontRegion: 'us-central1' })
    const contestedSub = 'apple-race-contested'

    const results = await Promise.allSettled([
      bindApple(t.db, a.accountId, contestedSub),
      bindApple(t.db, b.accountId, contestedSub),
    ])

    const fulfilled = results.filter((r): r is PromiseFulfilledResult<{ accountId: string }> => r.status === 'fulfilled')
    const rejected = results.filter((r): r is PromiseRejectedResult => r.status === 'rejected')
    expect(fulfilled).toHaveLength(1)
    expect(rejected).toHaveLength(1)
    // The friendly error, not a raw "duplicate key value violates unique
    // constraint accounts_apple_sub_key" - that is what the 23505 catch in
    // bindApple exists to normalize.
    expect((rejected[0]!.reason as Error).message).toMatch(/already bound/i)
  })
})

describe('session tokens', () => {
  it('round-trips an access token', async () => {
    const token = await issueAccessToken({ accountId: 'acc-1', serverId: 1 })
    expect(await verifyAccessToken(token)).toMatchObject({ accountId: 'acc-1', serverId: 1 })
  })

  it('refuses an access token as a refresh token', async () => {
    // Distinct audiences, so a stolen access token cannot be traded up for a
    // long-lived one. A real uuid is used so a removed audience check would
    // surface as an audience failure and NOT as an accidental 22P02 from a
    // non-uuid string reaching the accountId column - that accidental
    // failure mode is what let the old version of this test pass for the
    // wrong reason.
    const access = await issueAccessToken({ accountId: '11111111-1111-1111-1111-111111111111', serverId: 1 })
    const err = await captureRejection(redeemRefreshToken(t.db, access))
    expect(err).toBeInstanceOf(errors.JWTClaimValidationFailed)
    expect((err as { claim?: string }).claim).toBe('aud')
  })

  it('refuses a refresh token as an access token (the reverse direction)', async () => {
    const refresh = await issueRefreshToken({ accountId: '11111111-1111-1111-1111-111111111111', serverId: 1 })
    const err = await captureRejection(verifyAccessToken(refresh))
    expect(err).toBeInstanceOf(errors.JWTClaimValidationFailed)
    expect((err as { claim?: string }).claim).toBe('aud')
  })

  it('returns the ACCOUNT\'s serverId on refresh, not the token\'s claim', async () => {
    const guest = await createGuest(t.db, { birthdateBand: 'adult', storefrontRegion: 'us-central1' })
    // A token minted with a serverId that does not match reality - the only
    // way to construct this today, since assignment is immutable, but the
    // fix must not trust the claim regardless of how it could get here.
    const refresh = await issueRefreshToken({ accountId: guest.accountId, serverId: 999 })
    const claims = await redeemRefreshToken(t.db, refresh)
    expect(claims.serverId).toBe(1)
  })

  it('refuses to refresh a deleted account', async () => {
    const doomed = await createGuest(t.db, { birthdateBand: 'adult', storefrontRegion: 'us-central1' })
    const refresh = await issueRefreshToken({ accountId: doomed.accountId, serverId: 1 })

    await t.db.update(accounts).set({ deletedAt: new Date() })
      .where(eq(accounts.accountId, doomed.accountId))

    await expect(redeemRefreshToken(t.db, refresh)).rejects.toThrow(/deleted/i)
  })
})
