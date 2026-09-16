import { and, eq } from 'drizzle-orm'
import type { Hono } from 'hono'
import type { Deps } from '../app.ts'
import { creatures } from '../db/schema.ts'
import { loadPlayerId, requireSession } from '../http/auth.ts'
import { fail } from '../http/errors.ts'
import { hashRequest } from '../http/hash.ts'
import { normalizeUuid } from '../http/ids.ts'
import { IdempotencyMismatchError, withIdempotency } from '../money/idempotency.ts'
import { liveCreature, toCreatureDto, type CreatureDto } from '../roster/creatures.ts'

/**
 * `POST /v1/creature/name` - design §5 beat 4, bible §3.3.
 *
 * NAMING IS REPEATABLE. Bible §3.3: every Founder is "renameable at any time
 * from the Roster", so a second name for the same creature is a 200, not a
 * conflict - 0005_loop.sql's `only_founders_named` CHECK is the storage-level
 * half of that rule and this route's `not_a_founder` refusal is the other
 * half, spelled as a sentence.
 *
 * A FOUNDER IS AN ORDINARY SPLICE PARENT THAT HAPPENS TO CARRY A NAME, so
 * this route adds no refusal beyond ownership and liveness: `0005_loop.sql`'s
 * `founders_are_never_pruned` forbids PRUNING one, and neither that CHECK nor
 * `splice_confirm_spec` §4 forbids CONSUMING one (bible §3.3 permits it behind
 * a second, named confirm dialog the client owns) - so a Founder mid-splice
 * is refused the same way any other committed or already-consumed creature
 * would be, by `liveCreature()`, not by a rule specific to this route.
 *
 * NO PROFANITY FILTER THIS PHASE - design 1, 12: internal testers only. The
 * only shape check is "printable": no control or format characters, 1-16
 * characters after trimming.
 */

const NAME_MAX = 16

interface NameBody {
  creatureId: string
  name: string
}

function parseName(raw: unknown): NameBody | null {
  if (typeof raw !== 'object' || raw === null) return null
  const b = raw as Record<string, unknown>
  const creatureId = normalizeUuid(b.creatureId)
  if (creatureId === null || typeof b.name !== 'string') return null
  const name = b.name.trim()
  if (name.length < 1 || name.length > NAME_MAX) return null
  // Printable only. \p{Cc} is control characters (a bare BEL, a newline);
  // \p{Cf} is format characters (a zero-width joiner) - neither renders as
  // anything a player typed on purpose.
  if (/[\p{Cc}\p{Cf}]/u.test(name)) return null
  return { creatureId, name }
}

type NameOutcome =
  | { refused: 'no_player' | 'not_found' | 'not_a_founder' }
  | { creature: CreatureDto }

export function registerCreatureRoutes(app: Hono, deps: Deps): void {
  app.post('/v1/creature/name', async (c) => {
    const session = await requireSession(c)

    // This route mutates a creature's name, so it takes a key like every
    // other mutation in this service.
    const key = c.req.header('idempotency-key')
    if (!key) return fail('invalid_request', 'An Idempotency-Key header is required.')

    const body = parseName(await c.req.json().catch(() => null))
    if (body === null) {
      return fail('invalid_request',
        `creatureId must be a uuid and name 1-${NAME_MAX} printable characters.`)
    }

    let result: { body: NameOutcome }
    try {
      result = await withIdempotency(
        deps.db, session.serverId, key, hashRequest(body),
        async (tx): Promise<NameOutcome> => {
          const playerId = await loadPlayerId(tx, session.accountId)
          if (playerId === undefined) return { refused: 'no_player' }

          const [row] = await tx.select().from(creatures).where(and(
            eq(creatures.serverId, session.serverId), eq(creatures.playerId, playerId),
            eq(creatures.creatureId, body.creatureId), liveCreature())).for('update')
          if (row === undefined) return { refused: 'not_found' }
          // only_founders_named in SQL; this is the same rule with a sentence.
          if (!row.isFounder) return { refused: 'not_a_founder' }

          const [updated] = await tx.update(creatures).set({ name: body.name })
            .where(and(eq(creatures.serverId, session.serverId), eq(creatures.creatureId, body.creatureId)))
            .returning()
          return { creature: toCreatureDto(updated!) }
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
      if (outcome.refused === 'not_found') return fail('not_found', 'No such live creature on your roster.')
      return fail('not_a_founder', 'Only Founders can be named.')
    }

    return c.json(outcome.creature)
  })
}
