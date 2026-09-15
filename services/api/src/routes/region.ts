import { eq } from 'drizzle-orm'
import type { Hono } from 'hono'
import type { Deps } from '../app.ts'
import { loadBundle } from '../config/bundle.ts'
import { withServer, type Tx } from '../db/client.ts'
import { players } from '../db/schema.ts'
import { requireSession } from '../http/auth.ts'
import { fail } from '../http/errors.ts'
import { hashRequest } from '../http/hash.ts'
import { claimNode, regionState } from '../map/claim.ts'
import { IdempotencyMismatchError, withIdempotency } from '../money/idempotency.ts'
import type { CreatureDto } from '../roster/creatures.ts'

/**
 * The map's two routes - design 4.3.
 *
 * `GET /v1/region/state` is DERIVED and writes nothing; `POST /v1/node/claim`
 * writes everything it writes in one transaction. The split is the design's,
 * and map/claim.ts holds both halves of it - this file is the HTTP shape
 * around them.
 */

/**
 * The parse layer's SANITY bound on `slot`, exactly parallel to wave.ts's
 * MAX_WAVE_ID and for the same reason: it is not a statement about which
 * nodes exist. `nodesFor` stays the only authority on that, and every slot
 * inside this range still goes to it.
 *
 * 0 to 32767 because `node_depletion.node_slot` and
 * `harvest_positions.node_slot` are Postgres `smallint`
 * (drizzle/0005_loop.sql). A slot outside that cannot be STORED by this
 * schema under any bundle, so it is malformed by construction rather than
 * merely absent - which is the distinction between `invalid_request` (400)
 * and `not_found` (404) that this bound exists to keep.
 */
const MAX_NODE_SLOT = 32_767

interface ClaimBody { slot: number }

function parseClaim(raw: unknown): ClaimBody | null {
  if (typeof raw !== 'object' || raw === null) return null
  const b = raw as Record<string, unknown>
  if (typeof b.slot !== 'number' || !Number.isInteger(b.slot)) return null
  if (b.slot < 0 || b.slot > MAX_NODE_SLOT) return null
  return { slot: b.slot }
}

/** Inlined the way sync.ts and wave.ts inline it - one small scoped read. */
async function loadPlayerId(tx: Tx, accountId: string): Promise<string | undefined> {
  const [player] = await tx.select().from(players).where(eq(players.accountId, accountId))
  return player?.playerId
}

/**
 * The published bundle authors no nodes - see config/bundle.ts's
 * `Bundle.nodes`. 500 rather than an empty region: a map with nothing on it
 * is a content failure, and returning `nodes: []` would present it to the
 * player as a region that exists and is empty.
 */
const NO_NODES = 'The active config bundle authors no harvest nodes.'

export function registerRegionRoutes(app: Hono, deps: Deps): void {
  app.get('/v1/region/state', async (c) => {
    const session = await requireSession(c)
    const bundle = await loadBundle(deps.bundleStore)

    // ONE clock read per request, taken here and passed down. accrue and
    // epochFor take a timestamp and never read a clock (design 4.1, 4.2);
    // a second `new Date()` further in would put a real clock back inside
    // the thing that was made pure to keep it out.
    const now = new Date()

    const result = await withServer(deps.db, session.serverId, async (tx) => {
      const playerId = await loadPlayerId(tx, session.accountId)
      if (playerId === undefined) return null
      return regionState(tx, session.serverId, playerId, bundle, now)
    })

    if (result === null) return fail('not_found', 'No player on this server for that account.')
    if ('kind' in result) return fail('internal', NO_NODES)

    return c.json(result)
  })

  app.post('/v1/node/claim', async (c) => {
    const session = await requireSession(c)

    const key = c.req.header('idempotency-key')
    if (!key) return fail('invalid_request', 'An Idempotency-Key header is required.')

    const raw = await c.req.json().catch(() => null)
    const body = parseClaim(raw)
    // The message names the whole rule rather than only the missing-field
    // half of it - `slot: 2**53` IS present, and "slot is required." would
    // be a refusal that misstates its own reason. The client switches on
    // `code`, never on this text (solo_execution 6.2).
    if (body === null) {
      return fail('invalid_request', `slot must be an integer between 0 and ${MAX_NODE_SLOT}.`)
    }

    const bundle = await loadBundle(deps.bundleStore)
    const now = new Date()

    let result: { body: ClaimOutcome }
    try {
      // withIdempotency opens the transaction, and claimNode does all of
      // its work inside that one - design 4.3. The key row is its first
      // statement, so a retry that arrives while the first is still in
      // flight BLOCKS on it and then reads its stored answer rather than
      // claiming twice (money/idempotency.ts).
      result = await withIdempotency(
        deps.db, session.serverId, key, hashRequest(body),
        async (tx): Promise<ClaimOutcome> => {
          const playerId = await loadPlayerId(tx, session.accountId)
          if (playerId === undefined) return { refused: 'no_player' }

          const claim = await claimNode(tx, session.serverId, playerId, bundle, body.slot, now, key)
          if (claim.kind === 'ok') {
            return {
              slot: claim.slot, shards: claim.shards,
              creatures: claim.creatures, balance: claim.balance,
            }
          }
          // A refusal is RETURNED, never thrown, so it is stored under this
          // key like any other outcome - the same shape wave/submit uses.
          // Throwing would roll the key row back and let a retry of a
          // request that can only be refused run the whole claim again.
          if (claim.kind === 'roster_full') return { refused: 'roster_full', cap: claim.cap }
          return { refused: claim.kind }
        })
    } catch (err) {
      if (err instanceof IdempotencyMismatchError) {
        return fail('idempotency_key_reused', 'This Idempotency-Key was used for a different request.')
      }
      throw err
    }

    const outcome = result.body
    if ('refused' in outcome) {
      // roster_full FIRST, because it is the only refusal carrying a
      // payload and checking it here is what narrows `cap` into scope.
      if (outcome.refused === 'roster_full') {
        return fail('roster_full',
          'Your Hatchery is full, so this claim would have nothing to put its creatures in. '
          + 'Splice or free a slot and claim again - nothing has been taken from the node.',
          { cap: outcome.cap })
      }
      if (outcome.refused === 'no_nodes') return fail('internal', NO_NODES)
      if (outcome.refused === 'unknown_node') return fail('not_found', 'No such node in this region right now.')
      return fail('not_found', 'No player on this server for that account.')
    }

    return c.json(outcome)
  })
}

type ClaimOutcome =
  | { refused: 'no_player' | 'no_nodes' | 'unknown_node' }
  | { refused: 'roster_full'; cap: number }
  | { slot: number; shards: number; creatures: CreatureDto[]; balance: number }
