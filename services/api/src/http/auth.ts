import { eq } from 'drizzle-orm'
import type { Context } from 'hono'
import { fail } from './errors.ts'
import type { Tx } from '../db/client.ts'
import { players } from '../db/schema.ts'
import { verifyAccessToken, type SessionClaims } from '../identity/jwt.ts'

/**
 * The player row behind a session's account, or undefined if this account
 * has none on this server.
 *
 * Lives here, beside `requireSession`, because every authenticated route
 * needs it immediately after one: the pair is the whole of "who is asking".
 * It was previously copy-pasted byte-for-byte into four route files, which
 * made it the most duplicated logic in the service - and any change to it
 * (scoping by server_id as well, or memoising within a request) would have
 * had to be found in all four with no compiler error for a miss. The same
 * files already import `normalizeUuid` and `liveCreature` from single
 * definitions for exactly this reason.
 */
export async function loadPlayerId(tx: Tx, accountId: string): Promise<string | undefined> {
  const [player] = await tx.select().from(players).where(eq(players.accountId, accountId))
  return player?.playerId
}

/**
 * Thrown rather than returned, so a handler cannot accidentally continue
 * past a failed check by ignoring a return value.
 */
export class HttpError extends Error {
  // Explicit field, NOT a constructor parameter property. Node's
  // --experimental-strip-types cannot compile `constructor(readonly x: T)`
  // - it is strip-only and a parameter property requires emitting an
  // assignment. The whole service runs under that flag in the container,
  // so a parameter property anywhere in index.ts's import graph crashes on
  // boot. Vitest uses esbuild, which DOES support them, which is why the
  // suite stayed green.
  readonly response: Response

  constructor(response: Response) {
    super('http error')
    this.response = response
    this.name = 'HttpError'
  }
}

/**
 * ACCEPTED RESIDUAL WINDOW: this verifies signature and audience only - no
 * deleted_at lookup - so a deleted account's access token keeps working for
 * up to ACCESS_TTL (15m) after deletion. That is intentional, not an
 * oversight: it is what "no database read on every authenticated request"
 * costs.
 *
 * jwt.ts's redeemRefreshToken checks deleted_at on every refresh, which is
 * what will actually end the session - but there is no refresh endpoint yet
 * (the route table today is only account.ts and sync.ts), so no request can
 * reach that function and the token POST /v1/account returns is currently
 * unredeemable. Until a refresh route lands, this fifteen-minute window is
 * the ENTIRE lifetime of a session with no recovery path, not a residual gap
 * on top of one. The reasoning above becomes true the day a refresh route
 * exists.
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
