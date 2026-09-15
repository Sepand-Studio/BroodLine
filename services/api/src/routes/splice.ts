import { and, eq, inArray, sql } from 'drizzle-orm'
import type { Hono } from 'hono'
import type { Deps } from '../app.ts'
import { loadBundle } from '../config/bundle.ts'
import { withServer, type Tx } from '../db/client.ts'
import { creatures, players } from '../db/schema.ts'
import { requireSession } from '../http/auth.ts'
import { fail } from '../http/errors.ts'
import { toCreatureDto } from '../roster/creatures.ts'
import {
  coverageLost, spliceDistribution, type CombatSlot, type SpliceParent, type TraitRef,
} from '../splice/distribution.ts'

/**
 * The splice's first route - design §5.1.
 *
 * `POST /v1/splice/preview` WRITES NOTHING and takes no Idempotency-Key. It
 * returns `spliceDistribution` verbatim, and Task 7's `POST
 * /v1/splice/commit` SAMPLES THE SAME VALUE. That is the whole point of §5.1:
 * bible §2.6 commits to publishing odds, and a forecast computed by a second
 * function would make the odds this route publishes a claim about code rather
 * than a property of it.
 *
 * So this handler must never reshape, round or re-derive a probability. If a
 * future change wants the screen to see a different number, that number
 * changes in splice/distribution.ts, where `commit` reads it too.
 */

/**
 * The parse layer's shape check on a creature id, in the same spirit as
 * wave.ts's MAX_WAVE_ID and region.ts's MAX_NODE_SLOT: it is not a statement
 * about which creatures exist - the roster read below stays the only
 * authority on that, and every well-formed id still goes to it.
 *
 * `creatures.creature_id` is a Postgres `uuid` (drizzle/0005_loop.sql), so a
 * string that is not one cannot be stored by this schema under any bundle. It
 * is malformed by construction rather than merely absent, which is the
 * `invalid_request` (400) / `not_found` (404) distinction this check keeps -
 * and without it Postgres answers `22P02 invalid input syntax for type uuid`
 * from inside the read and the caller gets a 500 for a bad request.
 */
const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i

const SLOTS: readonly CombatSlot[] = ['trait_1', 'trait_2']

interface PreviewBody {
  parentA: string
  parentB: string
  locked: TraitRef
}

function parseLocked(raw: unknown): TraitRef | null {
  if (typeof raw !== 'object' || raw === null) return null
  const l = raw as Record<string, unknown>
  if (l.from !== 'a' && l.from !== 'b') return null
  if (typeof l.slot !== 'string' || !SLOTS.includes(l.slot as CombatSlot)) return null
  return { from: l.from, slot: l.slot as CombatSlot }
}

function parsePreview(raw: unknown): PreviewBody | null {
  if (typeof raw !== 'object' || raw === null) return null
  const b = raw as Record<string, unknown>
  if (typeof b.parentA !== 'string' || !UUID.test(b.parentA)) return null
  if (typeof b.parentB !== 'string' || !UUID.test(b.parentB)) return null
  // A splice CONSUMES both parents (design §5.1), so one creature cannot be
  // both. Refused here rather than at commit because a forecast for a splice
  // that can never happen is worse than no forecast: the pool it would
  // publish is that creature's own two traits, which is a plausible-looking
  // answer to an impossible question.
  if (b.parentA === b.parentB) return null
  const locked = parseLocked(b.locked)
  if (locked === null) return null
  return { parentA: b.parentA, parentB: b.parentB, locked }
}

/** Inlined the way sync.ts, wave.ts and region.ts inline it - one small read. */
async function loadPlayerId(tx: Tx, accountId: string): Promise<string | undefined> {
  const [player] = await tx.select().from(players).where(eq(players.accountId, accountId))
  return player?.playerId
}

/**
 * The player's two live creatures, or undefined if either is not one.
 *
 * `AND NOT pruned` IS LOAD-BEARING and is not an optimisation. A pruned
 * creature keeps its row as a tombstone (design §3.2, drizzle/0005_loop.sql)
 * and the prune NULLS `committed_to`, so every availability predicate written
 * as "uncommitted" is true of a dead ancestor. roster/creatures.ts's
 * `rosterCount` carries the same predicate and the same warning, spelled with
 * the same words the partial indexes use so a query that forgets it loses the
 * index rather than matching it.
 *
 * Here it is also what makes `toCreatureDto` safe: a tombstone has no traits
 * and no Instinct, and forecasting a splice against one would publish odds
 * over `null`.
 */
async function loadParents(
  tx: Tx, serverId: number, playerId: string, ids: [string, string],
): Promise<[SpliceParent, SpliceParent] | undefined> {
  const rows = await tx.select().from(creatures).where(and(
    eq(creatures.serverId, serverId),
    eq(creatures.playerId, playerId),
    inArray(creatures.creatureId, ids),
    sql`NOT ${creatures.pruned}`,
  ))

  const byId = new Map(rows.map((r) => [r.creatureId, r]))
  const a = byId.get(ids[0])
  const b = byId.get(ids[1])
  if (a === undefined || b === undefined) return undefined
  // toCreatureDto rather than a second hand-written projection: it is the one
  // place that refuses to represent a pruned row, and a CreatureDto is
  // structurally a SpliceParent already.
  return [toCreatureDto(a), toCreatureDto(b)]
}

/**
 * One message for "no such creature" and for "not yours", deliberately. Two
 * would make this route an enumeration oracle over other players' creature
 * ids - the same reason http/auth.ts never says WHY a token failed.
 */
const NO_PARENT = 'Both parents must be live creatures on your own roster.'

export function registerSpliceRoutes(app: Hono, deps: Deps): void {
  app.post('/v1/splice/preview', async (c) => {
    const session = await requireSession(c)

    const raw = await c.req.json().catch(() => null)
    const body = parsePreview(raw)
    // The message names the whole rule rather than only the missing-field
    // half of it - `parentA === parentB` IS present and well-formed. The
    // client switches on `code`, never on this text (solo_execution §6.2).
    if (body === null) {
      return fail('invalid_request',
        'parentA and parentB must be two different creature ids, '
        + 'and locked must name a slot (trait_1 or trait_2) on a parent (a or b).')
    }

    const bundle = await loadBundle(deps.bundleStore)

    // No Idempotency-Key and no transaction of its own beyond this read:
    // preview writes nothing. Two previews in a row return the same forecast,
    // and neither costs the player anything - the charge is spent at commit.
    const parents = await withServer(deps.db, session.serverId, async (tx) => {
      const playerId = await loadPlayerId(tx, session.accountId)
      if (playerId === undefined) return null
      return loadParents(tx, session.serverId, playerId, [body.parentA, body.parentB])
    })

    if (parents === null) return fail('not_found', 'No player on this server for that account.')
    if (parents === undefined) return fail('not_found', NO_PARENT)

    const [a, b] = parents
    let forecast
    try {
      forecast = spliceDistribution(a, b, body.locked, bundle)
    } catch (err) {
      // A trait the active bundle authors no dominance flag for. 500 and not
      // 400: the request is well-formed and the player did nothing wrong -
      // the CONTENT is incomplete, which config/validate.ts's
      // validateTraitDominance exists to make unpublishable. Named the way
      // region.ts names its own content failure rather than surfaced as an
      // unhandled error, so the log says which trait.
      console.error('splice forecast', err)
      return fail('internal', 'This splice cannot be forecast against the active config bundle.')
    }

    return c.json({
      forecast,
      // `sample_economy` §7's screen requirement. Named here rather than left
      // to the client: the two traits that do not carry return nothing and
      // are not recoverable, which is why it is a required element of the
      // confirmation rather than a nicety - and the client must not be the
      // thing that works out which coverage is at stake.
      coverageLost: coverageLost(a, b, body.locked, forecast),
    })
  })
}
