import { eq } from 'drizzle-orm'
import { SignJWT, jwtVerify, type JWTPayload } from 'jose'
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

function secret(): Uint8Array {
  const s = process.env.JWT_SECRET
  if (!s || s.length < 32) {
    throw new Error('JWT_SECRET must be set and at least 32 characters.')
  }
  return new TextEncoder().encode(s)
}

async function issue(claims: SessionClaims, audience: string, ttl: string): Promise<string> {
  return new SignJWT({ serverId: claims.serverId })
    .setProtectedHeader({ alg: 'HS256' })
    .setSubject(claims.accountId)
    .setAudience(audience)
    .setIssuedAt()
    .setExpirationTime(ttl)
    .sign(secret())
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
  const { payload } = await jwtVerify(token, secret(), { audience: ACCESS_AUD, algorithms: ['HS256'] })
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
  const { payload } = await jwtVerify(token, secret(), { audience: REFRESH_AUD, algorithms: ['HS256'] })
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
