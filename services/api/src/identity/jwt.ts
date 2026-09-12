import { eq } from 'drizzle-orm'
import { SignJWT, jwtVerify, type JWSHeaderParameters, type JWTPayload } from 'jose'
import type { Db } from '../db/client.ts'
import { accounts } from '../db/schema.ts'

export interface SessionClaims {
  accountId: string
  serverId: number
}

const ACCESS_AUD = 'broodline/access'
const REFRESH_AUD = 'broodline/refresh'
const ACCESS_TTL = '15m'
const REFRESH_TTL = '90d'

/**
 * Every token minted by this service claims this issuer, and verification
 * checks it. Without it, nothing stops a token accepted here from also being
 * accepted by some other component that is one day handed the same secret -
 * `iss` is what keeps the two acceptance surfaces separate even if that ever
 * happens.
 */
const ISS = 'broodline-api'

interface SigningKey {
  kid: string
  secret: Uint8Array
}

/**
 * The key currently used to SIGN new tokens. `JWT_SECRET_KID` defaults to
 * 'primary' rather than requiring every deployment to set it - a rotation
 * only needs the kid to be distinct from the previous one, not any specific
 * value.
 */
function primaryKey(): SigningKey {
  const s = process.env.JWT_SECRET
  if (!s || s.length < 32) {
    throw new Error('JWT_SECRET must be set and at least 32 characters.')
  }
  return { kid: process.env.JWT_SECRET_KID ?? 'primary', secret: new TextEncoder().encode(s) }
}

/**
 * The OUTGOING key from a rotation, trusted for verification only. Keeping a
 * previous key readable - never used to sign - is what lets a 90-day refresh
 * token minted under the old secret keep verifying while JWT_SECRET rotates,
 * instead of every outstanding refresh token silently dying the moment the
 * secret changes. Deliberately just one previous key: a full keyring is more
 * machinery than a solo-run rotation window needs.
 */
function previousKey(): SigningKey | undefined {
  const s = process.env.JWT_SECRET_PREVIOUS
  if (!s) return undefined
  if (s.length < 32) {
    throw new Error('JWT_SECRET_PREVIOUS must be at least 32 characters.')
  }
  return { kid: process.env.JWT_SECRET_PREVIOUS_KID ?? 'previous', secret: new TextEncoder().encode(s) }
}

/**
 * Resolves the verification key from the token's own `kid` header. A token
 * with no `kid` at all (anything minted before this change) falls back to
 * the primary key, so rotating in `kid` support does not itself invalidate
 * every outstanding token - only rotating the SECRET does, and now that can
 * happen behind a `kid` change instead of silently.
 */
function resolveVerificationKey(header: JWSHeaderParameters): Uint8Array {
  const primary = primaryKey()
  if (!header.kid || header.kid === primary.kid) return primary.secret

  const previous = previousKey()
  if (previous && header.kid === previous.kid) return previous.secret

  throw new Error(`Unknown signing key id: ${header.kid}`)
}

async function issue(claims: SessionClaims, audience: string, ttl: string): Promise<string> {
  const key = primaryKey()
  return new SignJWT({ serverId: claims.serverId })
    .setProtectedHeader({ alg: 'HS256', kid: key.kid })
    .setSubject(claims.accountId)
    .setIssuer(ISS)
    .setAudience(audience)
    .setIssuedAt()
    .setExpirationTime(ttl)
    .sign(key.secret)
}

export const issueAccessToken = (c: SessionClaims) => issue(c, ACCESS_AUD, ACCESS_TTL)
export const issueRefreshToken = (c: SessionClaims) => issue(c, REFRESH_AUD, REFRESH_TTL)

/**
 * Validates claim SHAPE at the trust boundary, right after signature/audience
 * verification and before anything downstream treats them as real values.
 *
 * `String(undefined)` is the literal string `"undefined"`, and `Number(undefined)`
 * is `NaN` - both satisfy their TypeScript types while being nonsense, so a
 * naive cast here would let a malformed but validly-signed token (or a bug in
 * `issue`) launder a bad claim past the type system. NaN in particular still
 * passes `typeof x === 'number'`, so every downstream `===` comparison on it
 * silently fails instead of raising - this is why Number.isInteger is checked
 * explicitly rather than trusting `typeof`.
 */
function extractClaims(payload: JWTPayload): SessionClaims {
  if (typeof payload.sub !== 'string' || payload.sub.length === 0) {
    throw new Error('Token carried no subject.')
  }
  if (typeof payload.serverId !== 'number' || !Number.isInteger(payload.serverId)) {
    throw new Error('Token carried an invalid serverId claim.')
  }
  return { accountId: payload.sub, serverId: payload.serverId }
}

export async function verifyAccessToken(token: string): Promise<SessionClaims> {
  const { payload } = await jwtVerify(token, resolveVerificationKey, {
    audience: ACCESS_AUD,
    issuer: ISS,
    algorithms: ['HS256'],
  })
  return extractClaims(payload)
}

/**
 * Verifies a refresh token and returns the session claims it carries -
 * re-checked against the account row, not merely against the token's own
 * signature. It mints nothing itself; there is no refresh endpoint yet to
 * call it (see http/auth.ts and routes/account.ts), so today this function
 * has no caller outside its own tests. When a refresh route lands, that
 * route is what turns this into "a new pair."
 *
 * DELIBERATELY STATELESS. There is no refresh_tokens table, so an individual
 * token cannot be revoked before it expires - what CAN be revoked is the
 * account, and that is checked here on every refresh. That becomes the thing
 * milestone 1 must honour - the App Store's in-app deletion path actually
 * ending the session - once a refresh route exists to call this function;
 * until then, no request reaches it.
 *
 * DEFERRED, with a trigger: a refresh_tokens table with rotation and reuse
 * detection arrives when the first non-TestFlight players do. Until then the
 * exposure is a stolen token on a device the player still holds, and the cost
 * of the table is an eighth table plus a write on every refresh.
 */
export async function redeemRefreshToken(db: Db, token: string): Promise<SessionClaims> {
  const { payload } = await jwtVerify(token, resolveVerificationKey, {
    audience: REFRESH_AUD,
    issuer: ISS,
    algorithms: ['HS256'],
  })
  const claims = extractClaims(payload)

  const [row] = await db.select().from(accounts).where(eq(accounts.accountId, claims.accountId))
  if (row === undefined) throw new Error('No such account.')
  if (row.deletedAt !== null) throw new Error('This account was deleted.')

  // The ACCOUNT's serverId, not the token's. Assignment is immutable so
  // these can never legitimately diverge, but returning the row's own value
  // closes the gap permanently rather than trusting a claim the account
  // itself can now confirm or deny.
  return { accountId: claims.accountId, serverId: row.serverId }
}
