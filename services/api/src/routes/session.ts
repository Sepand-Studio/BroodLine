import type { Hono } from 'hono'
import type { Deps } from '../app.ts'
import { fail } from '../http/errors.ts'
import { issueAccessToken, redeemRefreshToken } from '../identity/jwt.ts'

interface RefreshBody { refreshToken: string }

function parse(raw: unknown): RefreshBody | null {
  if (typeof raw !== 'object' || raw === null) return null
  const b = raw as Record<string, unknown>
  if (typeof b.refreshToken !== 'string' || b.refreshToken.length === 0) return null
  return { refreshToken: b.refreshToken }
}

/**
 * POST /v1/account has returned a 90-day refresh token since Phase 4 that no
 * endpoint could redeem - redeemRefreshToken (identity/jwt.ts) already does
 * every bit of the real work (verifies signature/audience/issuer, checks the
 * claim shape via extractClaims, and re-checks deleted_at against the
 * account row) and had no caller outside its own tests. This route is the
 * thin HTTP surface that was missing.
 */
export function registerSessionRoutes(app: Hono, deps: Deps): void {
  app.post('/v1/session/refresh', async (c) => {
    const raw = await c.req.json().catch(() => null)
    const body = parse(raw)
    if (body === null) return fail('invalid_request', 'refreshToken is required.')

    // Caught broadly and on purpose: a bad signature, an unknown account and
    // a deleted account all become the SAME 401. Distinguishing any of them
    // to an unauthenticated caller is an account-enumeration oracle -
    // exactly the reasoning requireSession (http/auth.ts) already applies to
    // access tokens, extended here to refresh tokens.
    let claims
    try {
      claims = await redeemRefreshToken(deps.db, body.refreshToken)
    } catch {
      return fail('unauthorized', 'That refresh token is not valid.')
    }

    // Rotation stays deferred to the first non-TestFlight players
    // (solo_execution 6.4) - the SAME refresh token is handed back rather
    // than minting a new one, so there is no refresh_tokens table to hold a
    // rotation/reuse-detection record yet.
    return c.json({
      accessToken: await issueAccessToken(claims),
      refreshToken: body.refreshToken,
    })
  })
}
