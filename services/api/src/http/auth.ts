import type { Context } from 'hono'
import { fail } from './errors.ts'
import { verifyAccessToken, type SessionClaims } from '../identity/jwt.ts'

/**
 * Thrown rather than returned, so a handler cannot accidentally continue
 * past a failed check by ignoring a return value.
 */
export class HttpError extends Error {
  constructor(readonly response: Response) {
    super('http error')
    this.name = 'HttpError'
  }
}

/**
 * ACCEPTED RESIDUAL WINDOW: this verifies signature and audience only - no
 * deleted_at lookup - so a deleted account's access token keeps working for
 * up to ACCESS_TTL (15m) after deletion. That is intentional, not an
 * oversight: it is what "no database read on every authenticated request"
 * costs, and jwt.ts's redeemRefreshToken is what actually ends the session,
 * by checking deleted_at on every refresh. The gap matters more from
 * Task 9 onward - once GET /v1/sync is behind this guard, a just-deleted
 * account can still read its own data for up to fifteen minutes.
 */
export async function requireSession(c: Context): Promise<SessionClaims> {
  const header = c.req.header('authorization')
  if (!header?.startsWith('Bearer ')) {
    throw new HttpError(fail('unauthorized', 'A bearer token is required.'))
  }
  try {
    return await verifyAccessToken(header.slice('Bearer '.length))
  } catch {
    // Never say WHY. Distinguishing "expired" from "bad signature" to an
    // unauthenticated caller is free information.
    throw new HttpError(fail('unauthorized', 'That token is not valid.'))
  }
}

/** Semver-ish compare, sufficient for a three-part version floor. Used by Task 9. */
export function isBelow(version: string, floor: string): boolean {
  const v = version.split('.').map(Number)
  const f = floor.split('.').map(Number)
  for (let i = 0; i < 3; i++) {
    const a = v[i] ?? 0
    const b = f[i] ?? 0
    if (a !== b) return a < b
  }
  return false
}
