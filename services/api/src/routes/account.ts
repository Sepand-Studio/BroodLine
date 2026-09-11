import { eq } from 'drizzle-orm'
import type { Hono } from 'hono'
import type { Deps } from '../app.ts'
import { loadBundle } from '../config/bundle.ts'
import { accounts, players } from '../db/schema.ts'
import { HttpError, requireSession } from '../http/auth.ts'
import { fail } from '../http/errors.ts'
import { hashRequest } from '../http/hash.ts'
import { assignServer } from '../identity/accounts.ts'
import { issueAccessToken, issueRefreshToken } from '../identity/jwt.ts'
import { IdempotencyMismatchError, withIdempotency } from '../money/idempotency.ts'
import { credit } from '../money/ledger.ts'

interface CreateBody {
  birthdateBand: string
  storefrontRegion: string
}

function parse(raw: unknown): CreateBody | null {
  if (typeof raw !== 'object' || raw === null) return null
  const b = raw as Record<string, unknown>
  if (typeof b.birthdateBand !== 'string' || typeof b.storefrontRegion !== 'string') return null
  return { birthdateBand: b.birthdateBand, storefrontRegion: b.storefrontRegion }
}

export function registerAccountRoutes(app: Hono, deps: Deps): void {
  app.post('/v1/account', async (c) => {
    const key = c.req.header('idempotency-key')
    if (!key) {
      // Not optional. client_architecture section 8 has the client generate
      // the key when the action is TAKEN, so a retry after a kill, a crash or
      // three days offline carries the identical key.
      return fail('invalid_request', 'An Idempotency-Key header is required.')
    }

    const raw = await c.req.json().catch(() => null)
    const body = parse(raw)
    if (body === null) {
      return fail('invalid_request', 'birthdateBand and storefrontRegion are required.')
    }

    let serverId: number
    try {
      serverId = await assignServer(body.storefrontRegion)
    } catch {
      return fail('invalid_request', `No server serves storefront region ${body.storefrontRegion}.`)
    }

    const bundle = await loadBundle(deps.bundleStore)

    try {
      const result = await withIdempotency(deps.db, serverId, key, hashRequest(body), async (tx) => {
        // One transaction, and the statement order is normative. The key was
        // inserted by withIdempotency before any of this ran.
        const [account] = await tx.insert(accounts).values({
          appleSub: null,
          birthdateBand: body.birthdateBand,
          homeRegion: body.storefrontRegion,
          serverId,
        }).returning()

        const [player] = await tx.insert(players).values({
          serverId, accountId: account!.accountId,
        }).returning()

        const balances: Record<string, number> = {}
        for (const grant of bundle.starterGrants) {
          balances[grant.currency] = await credit(tx, {
            serverId,
            playerId: player!.playerId,
            currency: grant.currency,
            delta: grant.amount,
            reasonCode: 'STARTER_GRANT',
            idempotencyKey: key,
          })
        }

        return {
          accountId: account!.accountId,
          playerId: player!.playerId,
          serverId,
          balances,
        }
      })

      // Tokens are minted OUTSIDE the idempotent body and are not stored in
      // the key's response. A replayed creation should still get a usable,
      // freshly-dated session rather than one expiring on the original clock.
      const claims = { accountId: result.body.accountId, serverId }
      return c.json({
        ...result.body,
        accessToken: await issueAccessToken(claims),
        refreshToken: await issueRefreshToken(claims),
      })
    } catch (err) {
      if (err instanceof IdempotencyMismatchError) {
        return fail('idempotency_key_reused', 'This Idempotency-Key was used for a different request.')
      }
      throw err
    }
  })

  app.delete('/v1/account', async (c) => {
    let session
    try {
      session = await requireSession(c)
    } catch (err) {
      if (err instanceof HttpError) return err.response
      throw err
    }

    // A SOFT delete plus a scheduled purge - solo_execution 6.4. The account
    // is disabled immediately, which is what redeemRefreshToken checks, and
    // player-visible data is removed on a timer.
    //
    // THE LEDGER IS NOT TOUCHED. It is a financial record and store_iap's
    // refund path depends on it; 6.4 retains it pseudonymised. Creature
    // tombstones survive for the same kind of reason - a deleted player's
    // descendants still render in other players' lineage views, which is
    // exactly why tombstones are minimal.
    //
    // THE PURGE JOB IS NOT BUILT HERE. There is no player-visible data beyond
    // the wallet yet, and a timer with nothing to delete is a job that cannot
    // be tested. It arrives with the first phase that stores creatures.
    await deps.db.update(accounts)
      .set({ deletedAt: new Date() })
      .where(eq(accounts.accountId, session.accountId))

    return c.json({ deleted: true })
  })
}
