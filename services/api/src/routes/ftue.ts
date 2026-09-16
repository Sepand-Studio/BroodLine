import type { Hono } from 'hono'
import type { Deps } from '../app.ts'
import { grantTutorialStock } from '../ftue/stock.ts'
import { loadPlayerId, requireSession } from '../http/auth.ts'
import { fail } from '../http/errors.ts'
import { hashRequest } from '../http/hash.ts'
import { IdempotencyMismatchError, withIdempotency } from '../money/idempotency.ts'
import type { CreatureDto } from '../roster/creatures.ts'

type StockOutcome =
  | { refused: 'no_player' }
  | { refused: 'unavailable'; why: string }
  | { creatures: CreatureDto[] }

/**
 * `POST /v1/ftue/splice-stock` - design §5 beats 6-7, `splice_confirm_spec` §6.
 *
 * BODY-LESS, the same shape `routes/creature.ts` uses for a mutation with
 * nothing for the caller to supply: everything this route needs (wave
 * progress, splice count, the marker) is read server-side from the session's
 * player. An Idempotency-Key is still required - this route mutates, exactly
 * as `POST /v1/creature/name` does.
 */
export function registerFtueRoutes(app: Hono, deps: Deps): void {
  app.post('/v1/ftue/splice-stock', async (c) => {
    const session = await requireSession(c)

    const key = c.req.header('idempotency-key')
    if (!key) return fail('invalid_request', 'An Idempotency-Key header is required.')

    let result: { body: StockOutcome }
    try {
      result = await withIdempotency(
        // No body to hash - `hashRequest(null)` is the same constant value
        // for every well-formed call, so a retried key can only ever match.
        deps.db, session.serverId, key, hashRequest(null),
        async (tx): Promise<StockOutcome> => {
          const playerId = await loadPlayerId(tx, session.accountId)
          if (playerId === undefined) return { refused: 'no_player' }

          const grant = await grantTutorialStock(tx, session.serverId, playerId)
          if (grant.kind === 'unavailable') return { refused: 'unavailable', why: grant.why }
          return { creatures: grant.creatures }
        })
    } catch (err) {
      if (err instanceof IdempotencyMismatchError) {
        return fail('idempotency_key_reused', 'This Idempotency-Key was used for a different request.')
      }
      throw err
    }

    const outcome = result.body
    if ('refused' in outcome) {
      if (outcome.refused === 'no_player') return fail('not_found', 'No player on this server for that account.')
      return fail('ftue_stock_unavailable', outcome.why)
    }

    return c.json({ creatures: outcome.creatures })
  })
}
