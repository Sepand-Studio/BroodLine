import { and, eq, inArray } from 'drizzle-orm'
import type { Hono } from 'hono'
import type { Deps } from '../app.ts'
import { loadBundle } from '../config/bundle.ts'
import { withServer, type Tx } from '../db/client.ts'
import { creatures, players } from '../db/schema.ts'
import { loadPlayerId, requireSession } from '../http/auth.ts'
import { fail } from '../http/errors.ts'
import { hashRequest } from '../http/hash.ts'
import { normalizeUuid } from '../http/ids.ts'
import { IdempotencyMismatchError, withIdempotency } from '../money/idempotency.ts'
import { liveCreature, toCreatureDto, type CreatureDto } from '../roster/creatures.ts'
import { commitSplice, isFirstSplice } from '../splice/commit.ts'
import {
  coverageLost, spliceDistribution, type CombatSlot, type SpliceParent, type TraitRef,
} from '../splice/distribution.ts'

/**
 * The splice's two routes - design §5.1.
 *
 * `POST /v1/splice/preview` WRITES NOTHING and takes no Idempotency-Key. It
 * returns `spliceDistribution` verbatim, and `POST /v1/splice/commit`
 * SAMPLES THE SAME VALUE. That is the whole point of §5.1:
 * bible §2.6 commits to publishing odds, and a forecast computed by a second
 * function would make the odds this route publishes a claim about code rather
 * than a property of it.
 *
 * So neither handler may reshape, round or re-derive a probability. If a
 * future change wants the screen to see a different number, that number
 * changes in splice/distribution.ts, where `commit` reads it too.
 *
 * THE TWO ROUTES SHARE THEIR PARSE LAYER AND THEIR REFUSAL VOCABULARY on
 * purpose. A forecast the player can obtain for a splice the commit would
 * refuse is a screen that offers a button it will then take away, and two
 * copies of "which parents are eligible" is how that happens. `parseParents`
 * below is the one definition; the ONE thing commit refuses that preview
 * does not is a COMMITTED parent, and Task 6 recorded why - preview's job is
 * to publish odds, and design §2.5's check belongs to the paid action, in
 * one place.
 */

const SLOTS: readonly CombatSlot[] = ['trait_1', 'trait_2']

interface ParentsBody {
  parentA: string
  parentB: string
  locked: TraitRef
}

interface CommitBody extends ParentsBody {
  bodyFrom: string
}

function parseLocked(raw: unknown): TraitRef | null {
  if (typeof raw !== 'object' || raw === null) return null
  const l = raw as Record<string, unknown>
  if (l.from !== 'a' && l.from !== 'b') return null
  if (typeof l.slot !== 'string' || !SLOTS.includes(l.slot as CombatSlot)) return null
  return { from: l.from, slot: l.slot as CombatSlot }
}

/**
 * The half both routes share - two different, well-formed parent ids and a
 * lock that names a real slot on a real parent.
 *
 * ONE DEFINITION, because the alternative is a forecast the player can get
 * for a splice the commit then refuses on a shape question. Commit adds
 * `bodyFrom` and nothing else.
 */
function parseParents(raw: unknown): ParentsBody | null {
  if (typeof raw !== 'object' || raw === null) return null
  const b = raw as Record<string, unknown>
  // `creatures.creature_id` is a Postgres `uuid` (drizzle/0005_loop.sql) -
  // see http/ids.ts for why the shape is checked here rather than met as a
  // 22P02 inside the roster read, and why it NORMALISES rather than merely
  // accepting.
  const parentA = normalizeUuid(b.parentA)
  const parentB = normalizeUuid(b.parentB)
  if (parentA === null || parentB === null) return null
  // A splice CONSUMES both parents (design §5.1), so one creature cannot be
  // both. Refused here rather than at commit because a forecast for a splice
  // that can never happen is worse than no forecast: the pool it would
  // publish is that creature's own two traits, which is a plausible-looking
  // answer to an impossible question.
  //
  // COMPARED AFTER NORMALISATION, and that is the whole of a measured bug
  // rather than tidiness. `===` is case-sensitive, the uuid regex is not,
  // and Postgres `uuid` equality is not - so `{parentA: id, parentB: id
  // .toUpperCase()}` passed this line, resolved BOTH parent locks to the
  // same row, and ran the splice past the debit before `consume` found one
  // parent where it expected two: a 500 on the only route that destroys
  // player property, for a body preview answered 404 for. http/ids.ts holds
  // the reasoning; this line is the one that was wrong.
  if (parentA === parentB) return null
  const locked = parseLocked(b.locked)
  if (locked === null) return null
  return { parentA, parentB, locked }
}

/**
 * `bodyFrom` is checked for SHAPE here and for MEANING in commitSplice.
 *
 * The parse layer cannot do the real check - "one of the two parents'
 * species" needs the rows - and it must not guess at a species list either:
 * species are engine content (`Species` in Ids.cs), and a list retyped here
 * would refuse a species a later bundle adds. So this is the same division
 * `isUuid` and `MAX_NODE_SLOT` keep: malformed by construction is a 400 from
 * here, refused by state is a refusal from inside the transaction.
 */
function parseCommit(raw: unknown): CommitBody | null {
  const parents = parseParents(raw)
  if (parents === null) return null
  const b = raw as Record<string, unknown>
  if (typeof b.bodyFrom !== 'string' || b.bodyFrom.length === 0) return null
  return { ...parents, bodyFrom: b.bodyFrom }
}


/**
 * The player's two live creatures, or undefined if either is not one.
 *
 * `liveCreature()` IS LOAD-BEARING and is not an optimisation. It carries
 * BOTH halves of "still on the roster" and neither is redundant: a PRUNED
 * ancestor keeps its row as a tombstone and the prune nulls `committed_to`,
 * so every predicate written as "uncommitted" is true of one; a CONSUMED
 * parent is not pruned at all and is whole, so a predicate written as `NOT
 * pruned` would forecast a splice against a creature that no longer exists.
 * roster/creatures.ts owns the definition and the measured cost of leaving
 * either half out.
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
    liveCreature(),
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
    const body = parseParents(raw)
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
    const resolved = await withServer(deps.db, session.serverId, async (tx) => {
      const playerId = await loadPlayerId(tx, session.accountId)
      if (playerId === undefined) return null
      const parents = await loadParents(tx, session.serverId, playerId, [body.parentA, body.parentB])
      if (parents === undefined) return undefined
      // THE SAME ROW COUNT `commitSplice` reads for this player - Task 7's
      // `guaranteedMutation` option, read inside this transaction so it
      // reflects the count AT THIS READ rather than one a concurrent commit
      // could move before `spliceDistribution` runs below.
      const guaranteedMutation = await isFirstSplice(tx, session.serverId, playerId)
      return { parents, guaranteedMutation }
    })

    if (resolved === null) return fail('not_found', 'No player on this server for that account.')
    if (resolved === undefined) return fail('not_found', NO_PARENT)

    const [a, b] = resolved.parents
    let forecast
    try {
      forecast = spliceDistribution(a, b, body.locked, bundle, { guaranteedMutation: resolved.guaranteedMutation })
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

  app.post('/v1/splice/commit', async (c) => {
    const session = await requireSession(c)

    // REQUIRED, unlike preview's. This route spends a charge and destroys
    // two creatures, and solo_execution §6.3 puts the key row inside the
    // same transaction as the mutation - so a retry that arrives while the
    // first is still in flight BLOCKS on it and reads its stored answer
    // rather than splicing twice.
    const key = c.req.header('idempotency-key')
    if (!key) return fail('invalid_request', 'An Idempotency-Key header is required.')

    const raw = await c.req.json().catch(() => null)
    const body = parseCommit(raw)
    if (body === null) {
      return fail('invalid_request',
        'parentA and parentB must be two different creature ids, locked must name a slot '
        + '(trait_1 or trait_2) on a parent (a or b), and bodyFrom must be a species.')
    }

    const bundle = await loadBundle(deps.bundleStore)
    // ONE clock read per request, taken here and passed down - region.ts's
    // discipline, and for the same reason: `consumed_at` records when the
    // splice happened, and two `new Date()` calls further in would put two
    // different answers on the two parents of one splice.
    const now = new Date()

    let result: { body: CommitOutcome }
    try {
      result = await withIdempotency(
        deps.db, session.serverId, key, hashRequest(body),
        async (tx): Promise<CommitOutcome> => {
          const playerId = await loadPlayerId(tx, session.accountId)
          if (playerId === undefined) return { refused: 'no_player' }

          const splice = await commitSplice(
            tx, session.serverId, playerId, bundle, body, now, key)
          if (splice.kind === 'ok') {
            // `mutated` and `aberrant` ARE ROLLED AND STORED, and are
            // deliberately NOT here. design §10 defers Aberrant traits out of
            // this phase, so the bundle authors nothing for a mutation to
            // produce and the child's slots are identical either way -
            // reporting `mutated: true` for an event with no effect is the
            // lossy direction, not the safe one, because a client would show
            // the player a mutation that did not happen. The seed on the row
            // is what makes the roll re-derivable, so nothing is lost by
            // withholding the flags until the content lands.
            return {
              child: splice.child, spliceId: splice.spliceId, seed: splice.seed,
              balance: splice.balance,
            }
          }
          // RETURNED, never thrown, so a refusal is stored under this key
          // like any other outcome - region.ts's shape. Throwing would roll
          // the key row back and let a retry of a request that can only be
          // refused run the whole splice again, which on this route means
          // re-taking two row locks on creatures a player still owns.
          if (splice.kind === 'generation_ceiling') {
            return { refused: splice.kind, ceiling: splice.ceiling, would: splice.would }
          }
          if (splice.kind === 'invalid_body') {
            return { refused: splice.kind, allowed: splice.allowed }
          }
          return { refused: splice.kind }
        })
    } catch (err) {
      if (err instanceof IdempotencyMismatchError) {
        return fail('idempotency_key_reused', 'This Idempotency-Key was used for a different request.')
      }
      throw err
    }

    const outcome = result.body
    if ('refused' in outcome) {
      // The payload-carrying refusals first, because checking them here is
      // what narrows their extra fields into scope.
      if (outcome.refused === 'generation_ceiling') {
        return fail('generation_ceiling',
          `This splice would make a Gen-${outcome.would} creature, and your Splicing Chamber `
          + `caps at Gen-${outcome.ceiling}. Upgrade the Chamber to splice deeper - `
          + 'nothing has been consumed.',
          { ceiling: outcome.ceiling, would: outcome.would })
      }
      if (outcome.refused === 'invalid_body') {
        return fail('invalid_request',
          'bodyFrom must be one of the two parents\' species.',
          { allowed: outcome.allowed })
      }
      if (outcome.refused === 'creature_committed') {
        return fail('creature_committed',
          'One of these creatures is out fighting. Wait for it to come back, then splice - '
          + 'nothing has been consumed.')
      }
      if (outcome.refused === 'insufficient_charges') {
        return fail('insufficient_charges',
          'You have no Splice Charge to spend. Nothing has been consumed.')
      }
      if (outcome.refused === 'unforecastable') {
        return fail('internal', 'This splice cannot be forecast against the active config bundle.')
      }
      if (outcome.refused === 'not_owned') return fail('not_found', NO_PARENT)
      return fail('not_found', 'No player on this server for that account.')
    }

    return c.json(outcome)
  })
}

/**
 * NOT OWNED IS A 404, NOT A 409, and this deliberately departs from the task
 * brief's sketch. `/v1/splice/preview` answers 404 for the same condition
 * with the same single message, for the reason stated at NO_PARENT: two
 * routes that disagree about whether a creature id exists are an enumeration
 * oracle over other players' rosters, and the two here read the same rows
 * through the same predicate.
 *
 * `creature_committed` IS a 409, because by then ownership is established -
 * it is the caller's own creature, refused on state the caller can change,
 * which is the shape `roster_full` and `wave_locked` already take.
 */
type CommitOutcome =
  | { refused: 'no_player' | 'not_owned' | 'creature_committed' | 'insufficient_charges' | 'unforecastable' }
  | { refused: 'generation_ceiling'; ceiling: number; would: number }
  | { refused: 'invalid_body'; allowed: string[] }
  | {
    child: CreatureDto
    spliceId: string
    /**
     * A DECIMAL STRING, like every other int8 that crosses the wire. The
     * seed is stored so a disputed roll can be re-derived (design §5.1), so
     * it has to be exact - and a JS number loses precision above 2^53 while
     * a BigInt is what JSON.stringify throws on.
     */
    seed: string
    /** Splice Charges left. The top bar shows them on every screen. */
    balance: number
  }
