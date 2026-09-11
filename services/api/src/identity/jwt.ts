import { eq } from 'drizzle-orm'
import { SignJWT, jwtVerify } from 'jose'
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

export async function verifyAccessToken(token: string): Promise<SessionClaims> {
  const { payload } = await jwtVerify(token, secret(), { audience: ACCESS_AUD, algorithms: ['HS256'] })
  return { accountId: String(payload.sub), serverId: Number(payload.serverId) }
}

/**
 * Redeems a refresh token for a new pair.
 *
 * DELIBERATELY STATELESS. There is no refresh_tokens table, so an individual
 * token cannot be revoked before it expires - what CAN be revoked is the
 * account, and that is checked here on every refresh. That is enough for the
 * one thing milestone 1 must honour: the App Store's in-app deletion path
 * has to actually end the session.
 *
 * DEFERRED, with a trigger: a refresh_tokens table with rotation and reuse
 * detection arrives when the first non-TestFlight players do. Until then the
 * exposure is a stolen token on a device the player still holds, and the cost
 * of the table is an eighth table plus a write on every refresh.
 */
export async function redeemRefreshToken(db: Db, token: string): Promise<SessionClaims> {
  const { payload } = await jwtVerify(token, secret(), { audience: REFRESH_AUD, algorithms: ['HS256'] })
  const claims: SessionClaims = { accountId: String(payload.sub), serverId: Number(payload.serverId) }

  const [row] = await db.select().from(accounts).where(eq(accounts.accountId, claims.accountId))
  if (row === undefined) throw new Error('No such account.')
  if (row.deletedAt !== null) throw new Error('This account was deleted.')

  return claims
}
