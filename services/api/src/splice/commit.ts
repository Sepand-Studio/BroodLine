import { randomUUID, getRandomValues } from 'node:crypto'
import { and, eq, sql } from 'drizzle-orm'
import type { Tx } from '../db/client.ts'
import { creatures, splices, wallets } from '../db/schema.ts'
import { loadArk } from '../map/claim.ts'
import { debit } from '../money/ledger.ts'
import { creatureHp, liveCreature, toCreatureDto, type CreatureDto } from '../roster/creatures.ts'
import { pruneLineage } from '../roster/lineage.ts'
import {
  combatPool, sampleSplice, spliceDistribution,
  type SpliceParent, type TraitRef, type TraitTable,
} from './distribution.ts'

/**
 * `POST /v1/splice/commit` - design 5.1, and the first thing this game does
 * that DESTROYS PLAYER PROPERTY.
 *
 * Both parents, one charge, the child, the splice record and the prune move
 * in ONE transaction. Every refusal below happens before any of them, and
 * that ordering is the whole shape of this file: a partial splice costs a
 * player two creatures for no child, which is the loss the confirmation
 * spec exists to prevent and is not a thing a retry can repair.
 *
 * IT SAMPLES `spliceDistribution`; it never computes a probability. That is
 * design 5.1's one-function-two-callers rule, and it is what makes the odds
 * `POST /v1/splice/preview` published a property of the code rather than a
 * claim about it. If this file ever wants a probability, that is the signal
 * to stop.
 */

/**
 * The generation ceiling the Splicing Chamber sets - `combat_numbers` 7,
 * transcribed, and the reason design 3.3 pins the Chamber at tier 3.
 *
 * THROWS for a tier the table does not author, rather than extrapolating,
 * for the reason `rosterCap` and `accrual.ts`'s multiplier tables throw: the
 * ceiling decides whether a splice is REFUSED after two creatures have been
 * chosen, and a guessed ceiling is a refusal nobody wrote down.
 * 0005_loop.sql's `splicing_chamber_tier_authored` is the same table as a
 * CHECK, so the column cannot hold a tier this refuses; widen the two
 * together.
 */
export function maxGeneration(chamberTier: number): number {
  const authored: Record<number, number> = {
    1: 2, 2: 2,
    3: 4, 4: 4, 5: 4,
    6: 6, 7: 6, 8: 6,
    9: 9, 10: 9, 11: 9, 12: 9,
  }
  const ceiling = authored[chamberTier]
  if (ceiling === undefined) {
    throw new Error(`no authored generation ceiling for Splicing Chamber tier ${chamberTier}`)
  }
  return ceiling
}

/**
 * The seed the roll is derived from, drawn the same way `issueWave` draws a
 * wave seed and masked for the same reason.
 *
 * MASKED TO 63 BITS: `splices.seed` is a Postgres bigint, which is signed,
 * and carries CHECK (seed >= 0). A full 64-bit draw lands at or above 2^63
 * half the time and either errors on insert or, if someone casts around it,
 * sign-flips into a DIFFERENT roll - which is the one failure mode a stored
 * seed exists to make impossible.
 *
 * From the CSPRNG rather than Math.random, which is neither seeded nor
 * unpredictable: this decides a paid outcome, so a client that could predict
 * or influence it could farm mutations.
 */
function randomSeed(): bigint {
  return getRandomValues(new BigUint64Array(1))[0]! & 0x7fff_ffff_ffff_ffffn
}

export interface SpliceRequest {
  parentA: string
  parentB: string
  locked: TraitRef
  /** design 5.2's free choice - free meaning UNRANDOMISED, not unconstrained. */
  bodyFrom: string
}

export type SpliceResult =
  | { kind: 'not_owned' }
  | { kind: 'creature_committed' }
  | { kind: 'generation_ceiling'; ceiling: number; would: number }
  | { kind: 'invalid_body'; allowed: string[] }
  | { kind: 'insufficient_charges' }
  | { kind: 'unforecastable' }
  | {
    kind: 'ok'
    child: CreatureDto
    spliceId: string
    seed: string
    mutated: boolean
    aberrant: boolean
    balance: number
  }

type CreatureRow = typeof creatures.$inferSelect

/**
 * Both parents, LOCKED FOR UPDATE, or undefined if either is not a live
 * creature of this player's.
 *
 * THE LOCK IS THE POINT, and without it this route double-spends creatures.
 * Two concurrent commits naming the same pair under DIFFERENT idempotency
 * keys would otherwise both read `consumed_at IS NULL`, both write, and both
 * commit - two children out of two parents, which is the splice's own
 * version of minting money. `withIdempotency` does not close this: it makes
 * ONE key run once, and the attack uses two.
 *
 * LOCKED IN SORTED ID ORDER, one statement per parent. Two requests naming
 * (A, B) and (B, A) are the same splice from the player's side and would
 * deadlock on a lock order taken from the request; sorting gives every
 * caller the same order. One statement each rather than `IN (a, b)` because
 * a single scan takes its locks in whatever order the plan produces rows,
 * which is not something to build a deadlock argument on.
 *
 * THE LIVENESS PREDICATE IS `liveCreature()`, which is both halves: a pruned
 * ancestor has no traits to splice, and a CONSUMED one is whole and would
 * otherwise splice perfectly well - a second time.
 */
async function lockParents(
  tx: Tx, serverId: number, playerId: string, ids: [string, string],
): Promise<[CreatureRow, CreatureRow] | undefined> {
  const rows = new Map<string, CreatureRow>()
  for (const id of [...ids].sort()) {
    const [row] = await tx.select().from(creatures)
      .where(and(
        eq(creatures.serverId, serverId),
        eq(creatures.playerId, playerId),
        eq(creatures.creatureId, id),
        liveCreature(),
      ))
      .for('update')
    if (row === undefined) return undefined
    rows.set(id, row)
  }
  const a = rows.get(ids[0])
  const b = rows.get(ids[1])
  if (a === undefined || b === undefined) return undefined
  return [a, b]
}

/**
 * The charge balance, under the wallet's own row lock.
 *
 * `debit()` would refuse an overdraft on its own - `wallets_balance_check`
 * is the authority and raises `InsufficientFundsError` - but a raise inside
 * this transaction aborts it, which rolls the idempotency key back with it.
 * Reading FOR UPDATE first turns "you have no charges" into a REFUSAL that
 * is stored under the key like every other refusal on this route, instead of
 * an exception, and the lock is what makes the read still true by the time
 * the debit happens.
 *
 * LOCK ORDER: after the parents, always. Every path through this file takes
 * creatures then the wallet, so two concurrent splices cannot hold one
 * another's next lock.
 */
async function lockCharges(tx: Tx, serverId: number, playerId: string): Promise<number> {
  const [row] = await tx.select().from(wallets)
    .where(and(
      eq(wallets.serverId, serverId),
      eq(wallets.playerId, playerId),
      eq(wallets.currency, 'splice_charges'),
    ))
    .for('update')
  return row?.balance ?? 0
}

/** design 5.2's cost. One splice, one charge. */
const CHARGE_COST = 1

/**
 * TEST-ONLY seam, and the same idiom map/claim.ts's `ClaimHooks` already uses
 * for the same class of problem.
 *
 * `afterParentsLocked` is awaited between the parents being locked and every
 * write this splice makes. THE RACE LIVES IN THAT WINDOW, and no test driving
 * two HTTP requests can produce the interleaving reliably - it needs a second
 * commit to READ the parents after the first has read them and before the
 * first has written. Measured, not supposed: the HTTP-level race test in
 * splice-commit.test.ts stayed GREEN against a `lockParents` with its
 * `FOR UPDATE` removed, because two requests through one app rarely overlap
 * in that window at all.
 *
 * A no-op for every real caller - the parameter defaults to `{}` and nothing
 * under `src/routes/` passes it.
 */
export interface SpliceHooks {
  afterParentsLocked?: () => Promise<void>
}

/**
 * The splice, in one transaction.
 *
 * THE ORDER OF THE REFUSALS IS DELIBERATE and runs cheapest-and-most-
 * specific first: ownership, then commitment, then the ceiling, then the
 * body, then the charge. Every one of them returns before anything is
 * written, so a blocked splice consumes NOTHING - not a parent, not a
 * charge, not the roll.
 *
 * THE ORDER OF THE WRITES IS ALSO LOAD-BEARING, and 0005's composite keys on
 * `splices` now enforce the important half of it: the child exists before
 * the record that names it. The debit carries the splice id it is paying
 * for, so the ledger row points at the record even though the record is
 * written after it - both are in this transaction, so there is no moment at
 * which one is visible without the other.
 */
export async function commitSplice(
  tx: Tx, serverId: number, playerId: string, bundle: TraitTable,
  req: SpliceRequest, now: Date, idempotencyKey: string,
  hooks: SpliceHooks = {},
): Promise<SpliceResult> {
  const parents = await lockParents(tx, serverId, playerId, [req.parentA, req.parentB])
  if (parents === undefined) return { kind: 'not_owned' }
  const [a, b] = parents
  // The read-to-write window, and the ONLY place a test can stand to see
  // whether the line above took a lock - see SpliceHooks. A no-op for every
  // real caller.
  if (hooks.afterParentsLocked) await hooks.afterParentsLocked()

  // design 2.5, and the race this closes rather than a nicety: the
  // deployment stored on a live issuance was resolved FROM these rows, so
  // splicing one away between wave/start and wave/submit would let a
  // deployment outlive the roster it was resolved from. `committed_to` gets
  // its first writer in Task 8; this is the reader that makes it mean
  // something.
  if (a.committedTo !== null || b.committedTo !== null) return { kind: 'creature_committed' }

  const ark = await loadArk(tx, serverId, playerId)
  const ceiling = maxGeneration(ark.splicingChamberTier)
  const generation = Math.max(a.generation, b.generation) + 1
  // BLOCKED, NOT TRUNCATED. `splice_confirm_spec` 2 wants the Chamber
  // upgrade surfaced rather than a bare error, so the ceiling travels out
  // with the refusal; silently capping the generation instead would hand the
  // player a child they did not ask for out of two creatures they cannot get
  // back.
  if (generation > ceiling) return { kind: 'generation_ceiling', ceiling, would: generation }

  // design 5.2's body is a FREE CHOICE between the two parents' species -
  // "free" meaning unrandomised, NOT unconstrained. Without this a client
  // mints any species it likes out of two it owns, which is the splice's own
  // version of the ownership hole design 6 closes on the wave path: the only
  // legitimate source of a species is a row the player owns.
  if (req.bodyFrom !== a.species && req.bodyFrom !== b.species) {
    return { kind: 'invalid_body', allowed: [...new Set([a.species, b.species])] }
  }

  const charges = await lockCharges(tx, serverId, playerId)
  if (charges < CHARGE_COST) return { kind: 'insufficient_charges' }

  // `toCreatureDto` rather than a hand-written projection, exactly as
  // preview's `loadParents` does it: a CreatureDto is structurally a
  // SpliceParent, and it is the one function that refuses to represent a row
  // with no traits.
  const parentA: SpliceParent = toCreatureDto(a)
  const parentB: SpliceParent = toCreatureDto(b)

  // Drawn before the roll and carried into the `splices` row unchanged, so
  // the stored seed is the one the outcome came from and not a second draw
  // that happens to be nearby.
  const seed = randomSeed()
  // Generated here rather than taken from the row's DEFAULT, because the
  // ledger row is written BEFORE the splice record and has to name it -
  // design 5.1's audit trail is only worth having if the debit points at the
  // roll it paid for.
  const spliceId = randomUUID()

  let outcome
  let locked
  try {
    // THE SAME VALUE preview returns for this pair and this lock -
    // `spliceDistribution` is pure and takes no clock, so recomputing it
    // here inside the transaction yields the identical object rather than a
    // second opinion about it.
    const forecast = spliceDistribution(parentA, parentB, req.locked, bundle)
    // The locked INSTANCE, by position, out of the same pool the forecast
    // was built from - not a second reading of the request. `combatPool` is
    // the one definition of what the four combat slots are, and
    // `spliceDistribution` has already refused a lock that names none of
    // them by the time this runs.
    locked = combatPool(parentA, parentB)
      .find((t) => t.from === req.locked.from && t.slot === req.locked.slot)!
    outcome = sampleSplice(forecast, seed)
  } catch (err) {
    // A trait the active bundle authors no dominance flag for, or a pool
    // with nothing left to roll. 500 rather than 400, the way preview and
    // region.ts's NO_NODES are 500: the request is well formed and the
    // player did nothing wrong - the CONTENT is incomplete. Nothing has been
    // written at this point, so the refusal costs them nothing.
    console.error('splice commit forecast', err)
    return { kind: 'unforecastable' }
  }

  // Everything above this line is a read. Everything below is a write, and
  // all of it is in the caller's one transaction.

  // `debit`, not `credit` with a negative delta - money/ledger.ts refuses
  // that outright now, because the upsert credit() uses checks the balance
  // against the tuple it PROPOSES and would report "insufficient charges"
  // against a full wallet. Found here, by this route being the first debit
  // in the game.
  const balance = await debit(tx, {
    serverId, playerId, currency: 'splice_charges', delta: -CHARGE_COST,
    reasonCode: 'splice', refType: 'splice', refId: spliceId, idempotencyKey,
  })

  await consume(tx, serverId, [a.creatureId, b.creatureId], now)

  const child = await insertChild(tx, {
    serverId, playerId,
    species: req.bodyFrom,
    generation,
    // design 5.3: the LOCKED trait carries at its parent's FULL coverage -
    // no dominance roll and no downtier. The rolled slot is the only one
    // `carriedTier` touches.
    trait1: locked.trait, tier1: locked.tier,
    trait2: outcome.combat2.trait, tier2: outcome.combat2.tier,
    instinct: outcome.instinct,
    parentA: a.creatureId, parentB: b.creatureId,
    hpCurrent: creatureHp(req.bodyFrom),
  })

  await tx.insert(splices).values({
    serverId, spliceId, playerId,
    parentA: a.creatureId, parentB: b.creatureId, childId: child.creatureId,
    // Stored so a disputed roll can be RE-DERIVED - design 5.1. The channel
    // layout and the hashed text in distribution.ts's `uniforms` are file
    // format because of this column, and splice-distribution.ts's golden
    // vectors are what pin them.
    seed: seed.toString(),
    // ROLLED, RECORDED, AND WITH NOTHING YET TO PRODUCE - stated here rather
    // than discovered. design 5.4 makes mutation "the only entry point for
    // Apex traits into the economy", and design 10 defers Aberrant traits
    // out of this phase entirely: the bundle authors four ordinary traits
    // and no Apex or Aberrant at all, so a mutated splice has nothing to
    // mutate INTO and the child's slots are unaffected.
    //
    // The roll still happens and is still stored, because the alternative is
    // worse in both directions: not rolling would make every splice already
    // recorded un-re-derivable the day the content lands, and suppressing
    // the flag would hide from the player a 9% event the forecast promised
    // them. OWED with the Aberrant content, and the report books it.
    mutated: outcome.mutated, aberrant: outcome.aberrant,
  })

  // design 3.2: the prune runs HERE, on the write that changes the depth,
  // rather than as a sweep.
  await pruneLineage(tx, serverId, child.creatureId)

  return {
    kind: 'ok',
    child: toCreatureDto(child),
    spliceId,
    seed: seed.toString(),
    mutated: outcome.mutated,
    aberrant: outcome.aberrant,
    balance,
  }
}

/**
 * Destroys both parents WITHOUT stripping them - 0005_loop.sql's
 * `consumed_at`.
 *
 * NOT A PRUNE, and the difference is the whole of design 3.2. A consumed
 * parent is dead but WHOLE: it leaves the roster and the Hatchery cap
 * immediately, and it keeps its traits so the lineage view can render it for
 * five generations - which `splice_confirm_spec` 5 makes the lesson of the
 * splice ("their traits live on in the pedigree", and the consumed parents
 * appear in the tree immediately after). Stripping here would also make
 * consuming a FOUNDER a `founders_are_never_pruned` violation, and
 * `splice_confirm_spec` 4 considered forbidding Founder consumption and
 * rejected it.
 *
 * `consumed_at IS NULL` in the WHERE is belt and braces over the FOR UPDATE
 * locks `lockParents` already holds; the row count is what turns a lock that
 * was somehow not held into a failed transaction rather than a silent
 * double-consume.
 */
async function consume(tx: Tx, serverId: number, ids: string[], now: Date): Promise<void> {
  const res = await tx.execute(sql`
    UPDATE creatures SET consumed_at = ${now}
     WHERE server_id = ${serverId}
       AND creature_id IN (${sql.join(ids.map((id) => sql`${id}::uuid`), sql`, `)})
       AND consumed_at IS NULL`)
  if (res.rowCount !== ids.length) {
    throw new Error(
      `splice consumed ${res.rowCount} of ${ids.length} parents on server ${serverId}`)
  }
}

interface NewChild {
  serverId: number
  playerId: string
  species: string
  generation: number
  trait1: string
  tier1: number | null
  trait2: string
  tier2: number | null
  instinct: string
  parentA: string
  parentB: string
  hpCurrent: number
}

/**
 * The child, and the only `insert(creatures)` on any splice path.
 *
 * FOUR FIELDS ARE LITERALS RATHER THAN PARAMETERS, deliberately, and each
 * closes something:
 *
 *  - `isFounder: false` - Founders are GRANTED, never rolled. bible 3.3.
 *  - `name: null` - only Founders may be named, and 0005's
 *    `only_founders_named` refuses the pair outright.
 *  - `pruned: false` and `consumedAt: null` - 0005 deliberately ALLOWS a
 *    born-pruned skeleton row (a server merge has to re-insert already-dead
 *    ancestors) and records that "no request path reaches it" is an
 *    app-layer promise rather than a DB-enforced one. This is the handler
 *    that would otherwise be the first path to reach it, and writing the two
 *    columns as constants is how the promise is kept: there is no argument
 *    a caller could pass to mint a creature that is born dead.
 */
async function insertChild(tx: Tx, c: NewChild): Promise<CreatureRow> {
  const [child] = await tx.insert(creatures).values({
    ...c,
    isFounder: false,
    name: null,
    pruned: false,
    consumedAt: null,
  }).returning()
  if (child === undefined) throw new Error('the splice inserted no child')
  return child
}
