import { and, eq, sql } from 'drizzle-orm'
import type { Bundle } from '../config/bundle.ts'
import type { Tx } from '../db/client.ts'
import { arks, harvestPositions, servers, wallets } from '../db/schema.ts'
import { credit } from '../money/ledger.ts'
import {
  grantBaseStock, lockRoster, rosterCap, rosterCount, toCreatureDto, type CreatureDto,
} from '../roster/creatures.ts'
import { accrue, baseStockFor } from './accrual.ts'
import { epochFor, nodesFor, type NodeState } from './rotation.ts'

/**
 * Where the map meets the database - design 4.3. The only file under
 * `map/` that may import `db/`: rotation.ts and accrual.ts stay pure so
 * their tests need no container and their purity is checkable by
 * inspection rather than by discipline.
 *
 * Two entry points and one rule between them: `regionState` WRITES NOTHING
 * and `claimNode` writes everything in one transaction.
 */

/**
 * The one region this phase ships - design 4.1, and `region_id` is on every
 * row precisely so the other twenty-nine are a content change rather than a
 * migration.
 *
 * NOT `holdfast`, which `broodline_region_roster.md` 3 names as the
 * starting region, and the reason is that naming it so would assert
 * something false: Holdfast is authored as three Common Veins and NO Rich
 * Deposit slot, while `nodesFor` gives every region one of each this phase.
 * A placeholder id that names no authored region is honest about being a
 * placeholder; borrowing an authored one would quietly contradict the
 * document that authored it. This is the id 0005_loop.sql's own tests
 * already use.
 */
export const THE_REGION = 'verdant-shelf'

/**
 * `nodesFor` takes a per-server seed and `servers` has no seed column.
 *
 * It does not need one YET: the seed exists so the cross-region shuffle is
 * a change to `nodesFor`'s body rather than to its callers (see that
 * function), and with one region and no relocation nothing varies by it -
 * `nodesFor` accepts it and ignores it. Deriving it from `server_id` keeps
 * it a real per-server value rather than a literal, and makes the column
 * that must eventually exist a change in ONE place. A random one would be
 * worse than either: rotation would stop being reproducible, which is the
 * property design 2.3 chose a pure function to get.
 */
function serverSeed(serverId: number): bigint {
  return BigInt(serverId)
}

/**
 * The Ark's row, or the defaults it would be created with.
 *
 * NOTHING CREATES AN ARK ROW YET. `POST /v1/account` predates the table and
 * this phase adds no writer, and `GET /v1/region/state` must write nothing
 * (design 4.1) - so a player who has never had a row written for them must
 * still be able to read their region and claim from it.
 *
 * THE THREE TIERS mirror 0005_loop.sql's column defaults, including the
 * Splicing Chamber's tier 3 (design 3.3 - tiers 1-2 cap coverage at Tier I,
 * which would make design 5.3's recessive downtier unobservable in the only
 * configuration this phase ships). `regionId` MIRRORS NOTHING:
 * `arks.region_id` is `text NOT NULL` with no DEFAULT at all
 * (0005_loop.sql:221), because there is no sensible server-wide default for
 * a column that will name one of thirty regions. THE_REGION below is this
 * phase's stand-in for the region an Ark would have been parked in at
 * creation, and the day an `arks` writer exists it - not this constant -
 * becomes the authority.
 *
 * A real writer is owed, and account creation is its natural home: design
 * 3.3 says one row per player, and this function's `??` is a stand-in for
 * that row, not a decision that the row is optional. Nothing depends on the
 * row existing today - no foreign key points at `arks` - so the stand-in is
 * safe rather than merely convenient.
 */
export interface ArkView {
  regionId: string
  harvestArrayTier: number
  hatcheryTier: number
  splicingChamberTier: number
}

const DEFAULT_ARK: ArkView = {
  regionId: THE_REGION,
  harvestArrayTier: 1,
  hatcheryTier: 1,
  splicingChamberTier: 3,
}

export async function loadArk(tx: Tx, serverId: number, playerId: string): Promise<ArkView> {
  const [row] = await tx.select().from(arks)
    .where(and(eq(arks.serverId, serverId), eq(arks.playerId, playerId)))
  return row === undefined ? DEFAULT_ARK : {
    regionId: row.regionId,
    harvestArrayTier: row.harvestArrayTier,
    hatcheryTier: row.hatcheryTier,
    splicingChamberTier: row.splicingChamberTier,
  }
}

/**
 * The epoch, from the server's own tick fields - design 2.3's derived
 * rotation, with no scheduler and nothing to catch up after downtime.
 *
 * `epochFor` returns a bigint because it is a pure count with no upper
 * bound stated; the two columns that store it are `bigint mode: 'number'`
 * on the same judgement wallets.balance is (schema.ts: a small counter, not
 * a seed). Narrowed in ONE place, here, so the rest of this file and every
 * response carries a plain number.
 */
async function loadEpoch(tx: Tx, serverId: number, now: Date): Promise<number> {
  const [server] = await tx.select().from(servers).where(eq(servers.serverId, serverId))
  if (server === undefined) throw new Error(`no server ${serverId}; a session claim named one that does not exist`)
  return Number(epochFor(server, now))
}

/** The node set for this region and epoch, or `undefined` if the bundle authors none. */
function nodeSet(bundle: Bundle, serverId: number, regionId: string, epoch: number): NodeState[] | undefined {
  // `nodes` is empty when the published bundle carries no nodes.json - see
  // config/bundle.ts, which cannot tell "this bundle authors no map" from
  // "this bundle forgot its map" and therefore refuses to guess. Serving an
  // empty region would present a content failure as an empty screen.
  if (bundle.nodes.length === 0) return undefined
  return nodesFor(serverId, regionId, BigInt(epoch), serverSeed(serverId), bundle)
}

interface Depletion {
  harvestedUnits: number
  depletedAt: Date | null
}

/**
 * THE OVERSHOOT GUARD, and the reason this is an upsert rather than a
 * SELECT.
 *
 * `node_depletion.harvested_units` carries no CHECK against a node's total
 * yield and CANNOT: `total_yield` is bundle config, not a column, so no
 * SQL constraint can express the bound. Under READ COMMITTED two claims on
 * the same nearly-dead node both read `harvested_units` as it was, both
 * compute the same `remaining`, and both credit it - the node pays twice
 * what it holds, and the NEXT caller computes a negative `remaining`.
 * accrue()'s floor-at-zero stops that from becoming a negative credit; it
 * does nothing about the overpayment that caused it, which is why the guard
 * has to live on the write path. Task 4's report books this decision here
 * explicitly.
 *
 * `INSERT ... ON CONFLICT DO UPDATE ... RETURNING` is credit()'s own idiom
 * and it does three things in one statement: it creates the row if this is
 * the epoch's first claim, it TAKES THE ROW LOCK, and it returns the counter as
 * of that lock. A second claim blocks here until the first commits and then
 * reads the updated counter, so `remaining` is a value nobody else can
 * change for the rest of the transaction and accrue()'s `min(units,
 * remaining)` is a true bound. `ON CONFLICT DO NOTHING` would not do it -
 * it returns immediately and the follow-up read cannot see the other
 * transaction's uncommitted row, which is the same trap money/idempotency.ts
 * documents at length.
 *
 * IT ALSO CLOSES A SECOND RACE, and this is why the Common Vein - which has
 * no yield bound at all - takes the lock too. Two concurrent claims by the
 * SAME player on the same node under DIFFERENT idempotency keys would
 * otherwise both read the same `last_settled_at` and both be paid for the
 * same hours. Taking this lock BEFORE the position is read is what makes
 * the second one see the settled position and accrue zero. The statement
 * order in `claimNode` is therefore load-bearing, not incidental.
 *
 * THE COST, stated rather than discovered: claims against one node in one
 * epoch are serialised across every player on the server. That is the
 * honest price of a shared finite budget - two transactions cannot both
 * spend the last of it - and it is bounded because this transaction holds
 * no network call (the pool's lock_timeout of 5s in db/client.ts is the
 * backstop). The alternative, a conditional `UPDATE ... WHERE
 * harvested_units + units <= total`, trades the wait for a retry loop that
 * has to decide what a partially-affordable claim pays, which is a harder
 * question than this one.
 *
 * LOCK ORDER: this row is the only one a claim touches that is shared
 * BETWEEN players, and it is taken first. Everything after it - the wallet,
 * the creatures, the position - is the caller's own. One shared lock taken
 * at one point cannot deadlock against itself.
 */
async function lockDepletion(
  tx: Tx, serverId: number, regionId: string, slot: number, epoch: number,
): Promise<Depletion> {
  const res = await tx.execute(sql`
    INSERT INTO node_depletion (server_id, region_id, node_slot, epoch, harvested_units)
    VALUES (${serverId}, ${regionId}, ${slot}, ${epoch}, 0)
    ON CONFLICT (server_id, region_id, node_slot, epoch) DO UPDATE
      SET harvested_units = node_depletion.harvested_units
    RETURNING harvested_units, depleted_at`)
  const row = res.rows[0] as { harvested_units: string | number; depleted_at: Date | null } | undefined
  if (row === undefined) {
    throw new Error(`node_depletion upsert returned no row for server ${serverId} node ${slot}`)
  }
  return { harvestedUnits: Number(row.harvested_units), depletedAt: row.depleted_at }
}

/**
 * Yield this node has left, or `null` for a node that never depletes.
 *
 * Floored at zero for the caller's benefit, not accrue()'s: a stored
 * overshoot from before this guard existed would otherwise be reported to a
 * client as a negative quantity of ore.
 */
function remainingOf(node: NodeState, depletion: Depletion): number | null {
  return node.totalYield === null ? null : Math.max(0, node.totalYield - depletion.harvestedUnits)
}

/**
 * Every depletion row for one region-epoch, in ONE round trip.
 *
 * `regionState` used to call `readDepletion` per node, and `node-postgres`
 * cannot pipeline on a single transaction, so each was a full network hop on
 * the map screen - the most frequently opened endpoint there is - against a
 * pool capped at five connections. The same function already hoists its
 * roster count out of the loop for the same reason; this is that discipline
 * applied to the other two reads. Scoped by region and epoch rather than by
 * a slot list because a region's node set is single digits: fetching it whole
 * is cheaper than building an `ANY` array, and the shape does not change as
 * content adds nodes.
 */
async function readDepletions(
  tx: Tx, serverId: number, regionId: string, epoch: number,
): Promise<Map<number, Depletion>> {
  const res = await tx.execute(sql`
    SELECT node_slot, harvested_units, depleted_at FROM node_depletion
     WHERE server_id = ${serverId} AND region_id = ${regionId} AND epoch = ${epoch}`)
  const out = new Map<number, Depletion>()
  for (const r of res.rows as { node_slot: number; harvested_units: string | number; depleted_at: string | Date | null }[]) {
    out.set(Number(r.node_slot), {
      harvestedUnits: Number(r.harvested_units),
      // `tx.execute` returns raw driver values - a timestamptz arrives as a
      // string, where `tx.select()` would have mapped it to a Date. Coerced
      // here so callers see the same shape either way.
      depletedAt: r.depleted_at === null ? null : new Date(r.depleted_at),
    })
  }
  return out
}

/** A player's stake in one node this epoch: when it last settled, and what
 * it had left over towards the next creature. */
interface Position {
  lastSettledAt: Date
  carried: number
}

/** Every position this player holds in one region-epoch, in ONE round trip. */
async function readPositions(
  tx: Tx, serverId: number, playerId: string, regionId: string, epoch: number,
): Promise<Map<number, Position>> {
  const res = await tx.execute(sql`
    SELECT node_slot, last_settled_at, base_stock_carried FROM harvest_positions
     WHERE server_id = ${serverId} AND player_id = ${playerId}::uuid
       AND region_id = ${regionId} AND epoch = ${epoch}`)
  const out = new Map<number, Position>()
  for (const r of res.rows as
    { node_slot: number; last_settled_at: string | Date; base_stock_carried: string | number }[]) {
    // new Date() for the same reason readDepletions coerces: a raw execute
    // hands back the driver's own value, and `accrue` calls .getTime() on it.
    out.set(Number(r.node_slot), {
      lastSettledAt: new Date(r.last_settled_at),
      carried: Number(r.base_stock_carried),
    })
  }
  return out
}

/**
 * A player's position on a node, WITHOUT writing.
 *
 * An absent row means `now`, which pays nothing - not twelve hours of
 * backdated yield. The rows are keyed by epoch, so this is also what the
 * weekly boundary does: a new epoch starts every player at zero on every
 * node rather than handing them the cap for having been away. Gifting the
 * cap instead would make a player's first-ever claim, and every epoch
 * rollover, free money nobody authored.
 */
async function loadPosition(
  tx: Tx, serverId: number, playerId: string, regionId: string, slot: number, epoch: number, now: Date,
): Promise<Position> {
  const [row] = await tx.select().from(harvestPositions).where(and(
    eq(harvestPositions.serverId, serverId),
    eq(harvestPositions.playerId, playerId),
    eq(harvestPositions.regionId, regionId),
    eq(harvestPositions.nodeSlot, slot),
    eq(harvestPositions.epoch, epoch),
  ))
  // An absent row has carried nothing towards a creature, for the same
  // reason it settles at `now`: nothing has been harvested here yet.
  return {
    lastSettledAt: row?.lastSettledAt ?? now,
    carried: row?.baseStockCarried ?? 0,
  }
}

async function settlePosition(
  tx: Tx, serverId: number, playerId: string, regionId: string, slot: number, epoch: number,
  now: Date, carried: number,
): Promise<void> {
  // `carried` moves with `lastSettledAt` and in the same statement. They are
  // one fact - how far this position has got towards its next creature - and
  // settling the clock without the carry would drop the remainder on every
  // claim, which is the drift 0007 exists to remove.
  await tx.insert(harvestPositions)
    .values({
      serverId, playerId, regionId, nodeSlot: slot, epoch,
      lastSettledAt: now, baseStockCarried: carried,
    })
    .onConflictDoUpdate({
      target: [harvestPositions.serverId, harvestPositions.playerId, harvestPositions.regionId,
        harvestPositions.nodeSlot, harvestPositions.epoch],
      set: { lastSettledAt: now, baseStockCarried: carried },
    })
}

/**
 * design 4.3's fourth write. `depleted_at` is set the moment the node's
 * remaining yield reaches zero and never moved afterwards (COALESCE), so it
 * records when the node ran dry rather than when it was last claimed
 * against.
 */
async function addHarvested(
  tx: Tx, serverId: number, regionId: string, slot: number, epoch: number,
  units: number, remaining: number | null, now: Date,
): Promise<void> {
  const depletedNow = remaining !== null && units >= remaining
  await tx.execute(sql`
    UPDATE node_depletion
       SET harvested_units = harvested_units + ${units},
           depleted_at = ${depletedNow ? sql`COALESCE(depleted_at, ${now})` : sql`depleted_at`}
     WHERE server_id = ${serverId} AND region_id = ${regionId}
       AND node_slot = ${slot} AND epoch = ${epoch}`)
}

async function readBalance(tx: Tx, serverId: number, playerId: string): Promise<number> {
  const [row] = await tx.select().from(wallets).where(and(
    eq(wallets.serverId, serverId),
    eq(wallets.playerId, playerId),
    eq(wallets.currency, 'shards'),
  ))
  return row?.balance ?? 0
}

export interface NodeStateDto {
  slot: number
  type: string
  accrued: number
  remaining: number | null
  /**
   * Creatures a claim on this node would grant RIGHT NOW.
   *
   * Here so the 409 is predictable rather than a surprise: without it a
   * client has no way to know that claiming is about to be refused for a
   * full roster, and design 4.3 refuses the WHOLE claim - the shards go
   * unpaid too. `grants > 0 && roster.count + grants > roster.cap` is the
   * exact condition `claimNode` applies, so a client can grey the button
   * and say why instead of discovering it by being refused.
   *
   * ADVISORY, like every other number on this screen. It is computed
   * outside any lock and the claim recomputes it under one, so a
   * concurrent claim on the same node can still change the answer between
   * this read and that claim.
   */
  grants: number
}

export interface RegionState {
  regionId: string
  epoch: number
  nodes: NodeStateDto[]
  /** The other half of the condition above - bible 7.2's Hatchery cap. */
  roster: { count: number; cap: number }
}

/**
 * TEST-ONLY seams, and the same idiom test/wave-helpers.ts's lock factory
 * already uses (`testBeforeReclaim`) for the same kind of problem.
 *
 * `afterDepletionRead` is awaited between the depletion read and every
 * write this claim makes. THE RACE LIVES IN THAT WINDOW, and no test driving
 * two HTTP requests can produce the interleaving reliably: it needs a second
 * claim to read AFTER the first has read and BEFORE the first has written.
 * Without this seam a test can only observe that the second claim stalls
 * somewhere, which an UNGUARDED implementation also does - it stalls on
 * `addHarvested`'s own UPDATE, just after it has already decided what to
 * pay. Measured, not supposed: the whole suite stayed green against a
 * `lockDepletion` rewritten as `ON CONFLICT DO NOTHING` plus a plain SELECT
 * until this hook existed.
 *
 * A no-op for every real caller - `claimNode`'s parameter defaults to `{}`,
 * and nothing under `src/routes/` passes it.
 */
export interface ClaimHooks {
  afterDepletionRead?: () => Promise<void>
}

export type ClaimResult =
  | { kind: 'no_nodes' }
  | { kind: 'unknown_node' }
  | { kind: 'roster_full'; cap: number }
  | { kind: 'ok'; slot: number; shards: number; creatures: CreatureDto[]; balance: number }

/**
 * `GET /v1/region/state`'s whole body of work, and it writes NOTHING.
 *
 * Every number here is derived: the node set from `nodesFor`, the epoch
 * from the server's tick fields, the accrual from two timestamps. Settling
 * the position as a side effect of reading - the obvious "while we're here"
 * optimisation - would mean a player who opened the region screen twice saw
 * zero the second time, and would have been PAID nothing for the accrual it
 * consumed. Design 4.1 makes the read derived for exactly this reason.
 */
export async function regionState(
  tx: Tx, serverId: number, playerId: string, bundle: Bundle, now: Date,
): Promise<RegionState | { kind: 'no_nodes' }> {
  const ark = await loadArk(tx, serverId, playerId)
  const epoch = await loadEpoch(tx, serverId, now)
  const nodes = nodeSet(bundle, serverId, ark.regionId, epoch)
  if (nodes === undefined) return { kind: 'no_nodes' }

  // ONE roster count for the whole response, not one per node: it is the
  // same number for every node, and design 4.3's cap is a property of the
  // player rather than of the ground they are standing on.
  const cap = rosterCap(ark.hatcheryTier)
  const count = await rosterCount(tx, serverId, playerId)

  // TWO round trips for the whole response, not two PER NODE - see
  // readDepletions. Both are plain reads; this path still writes nothing.
  const depletions = await readDepletions(tx, serverId, ark.regionId, epoch)
  const positions = await readPositions(tx, serverId, playerId, ark.regionId, epoch)

  const dtos: NodeStateDto[] = []
  for (const node of nodes) {
    const depletion = depletions.get(node.slot) ?? { harvestedUnits: 0, depletedAt: null }
    const remaining = remainingOf(node, depletion)
    // Same fallback `loadLastSettled` applies, for the same reason: an
    // absent row pays nothing rather than backdating to the cap, and has
    // carried nothing towards a creature yet.
    const position = positions.get(node.slot)
    const lastSettledAt = position?.lastSettledAt ?? now

    // The SAME functions the claim pays and grants from, not a second
    // estimate of either. A display computed differently from the credit is
    // a bug report the player is right to file.
    const args = {
      lastSettledAt, now, ratePerHour: node.ratePerHour,
      arrayTier: ark.harvestArrayTier, remaining,
    }
    dtos.push({
      slot: node.slot,
      type: node.type,
      accrued: accrue(args),
      remaining,
      // The COUNT only. This is the read path: the carry it would leave is
      // deliberately discarded, because persisting it here is the write
      // regionState is not allowed to make.
      grants: baseStockFor(args, position?.carried ?? 0).creatures,
    })
  }

  return { regionId: ark.regionId, epoch, nodes: dtos, roster: { count, cap } }
}

/**
 * `POST /v1/node/claim`, in one transaction - design 4.3.
 *
 * THE STATEMENT ORDER IS THE DESIGN. `lockDepletion` first (see its
 * comment: it is both the overshoot guard and what serialises a player's
 * own concurrent claims), then the position, then the decision, then the
 * writes. Reading the position before taking that lock would reopen the
 * double-pay it closes.
 *
 * THE HATCHERY REFUSAL COSTS NOTHING AND GRANTS NOTHING. Design 4.3 says
 * the check happens "before the transaction opens, not truncated inside
 * it", and what that sentence is protecting is the player: a partial grant
 * that silently drops creatures is a loss they report as theft. It is
 * enforced here as "before anything in the transaction is written" rather
 * than as a read in an earlier transaction, which is STRICTER rather than
 * looser - a count taken outside this transaction could be stale by the
 * time the grant lands, and the refusal would then be racing the thing it
 * is protecting. Nothing is credited, nothing is granted, and the position
 * is NOT settled, so the accrual is still there when a slot is freed.
 *
 * THE REFUSAL STILL TAKES THE DEPLETION LOCK, AND STILL COMMITS, because
 * the cap check needs `grants`, `grants` needs `remaining`, and `remaining`
 * is only trustworthy under that lock. So a client polling `claim` against
 * a full roster churns the hottest shared row on the server doing nothing.
 * Considered and DECLINED rather than missed: the cheap fix - decide the
 * refusal from an UNLOCKED read before taking the lock - is not safe on a
 * money path. `harvested_units` only grows within an epoch, so an unlocked
 * read can only over-estimate `grants`; refusing on it would refuse a
 * player whose locked claim would have granted nothing and PAID THEM
 * SHARDS, whenever another claim drained the node in between. Refusing
 * shards someone is owed is the same family of loss as truncating a grant,
 * and this whole refusal exists to avoid that family. The real fix is for
 * the client not to poll a claim it can already see will be refused, which
 * is what `RegionState.grants` and `RegionState.roster` are for.
 */
export async function claimNode(
  tx: Tx, serverId: number, playerId: string, bundle: Bundle,
  slot: number, now: Date, idempotencyKey: string,
  hooks: ClaimHooks = {},
): Promise<ClaimResult> {
  const ark = await loadArk(tx, serverId, playerId)
  const epoch = await loadEpoch(tx, serverId, now)
  const nodes = nodeSet(bundle, serverId, ark.regionId, epoch)
  if (nodes === undefined) return { kind: 'no_nodes' }

  const node = nodes.find((n) => n.slot === slot)
  if (node === undefined) return { kind: 'unknown_node' }

  const depletion = await lockDepletion(tx, serverId, ark.regionId, slot, epoch)
  // The read-to-write window, and the ONLY place a test can stand to see
  // whether the line above took a lock - see ClaimHooks. A no-op for every
  // real caller.
  if (hooks.afterDepletionRead) await hooks.afterDepletionRead()
  const remaining = remainingOf(node, depletion)

  const position = await loadPosition(
    tx, serverId, playerId, ark.regionId, slot, epoch, now)
  const lastSettledAt = position.lastSettledAt
  const args = {
    lastSettledAt, now, ratePerHour: node.ratePerHour,
    arrayTier: ark.harvestArrayTier, remaining,
  }
  const units = accrue(args)
  const baseStock = baseStockFor(args, position.carried)
  const grants = baseStock.creatures

  // Before the count, not after - see lockRoster. This and
  // `grantWaveBaseStock` are the only granting paths and they share no lock
  // otherwise: two claims on DIFFERENT node slots contend on nothing, so
  // both could read the same pre-grant count and both clear the cap.
  const cap = rosterCap(ark.hatcheryTier)
  if (grants > 0) await lockRoster(tx, serverId, playerId)
  if (grants > 0 && await rosterCount(tx, serverId, playerId) + grants > cap) {
    return { kind: 'roster_full', cap }
  }

  // NO LEDGER ROW FOR A ZERO CLAIM. credit() writes its row unconditionally
  // - correctly, it is the function that makes a balance change auditable -
  // so calling it with a delta of 0 would append an entry recording that
  // nothing happened, on every claim a player makes too soon. The ledger is
  // the audit trail for MUTATIONS; a claim that pays nothing is not one,
  // and is not an error either (design 4.3's second-claim case: accrual
  // since the first is genuinely zero).
  const balance = units > 0
    ? await credit(tx, {
      serverId, playerId, currency: 'shards', delta: units,
      reasonCode: 'node_claim', refType: 'node', refId: `${slot}:${epoch}`,
      idempotencyKey,
    })
    : await readBalance(tx, serverId, playerId)

  // The species roll is reproducible from this string - see speciesForSeed.
  // Every component is already fixed by the time the grant happens, so a
  // replayed claim would re-derive the same creatures rather than a fresh
  // roll (withIdempotency returns the stored response and never re-runs
  // this function, so that is a property nothing depends on today - but it
  // is the property a dispute would be settled with).
  const granted = await grantBaseStock(tx, serverId, playerId, grants,
    `${serverId}:${playerId}:${ark.regionId}:${epoch}:${slot}:${idempotencyKey}`)
  await settlePosition(tx, serverId, playerId, ark.regionId, slot, epoch, now, baseStock.carried)
  await addHarvested(tx, serverId, ark.regionId, slot, epoch, units, remaining, now)

  return { kind: 'ok', slot, shards: units, creatures: granted.map(toCreatureDto), balance }
}
