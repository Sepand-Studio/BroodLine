/**
 * The node set for a region and epoch, and the epoch itself - design §4.1.
 * Pure, total, and the only authority on which nodes exist. Neither
 * function reads a clock or the database: `epochFor` takes the timestamp
 * it is asked about and `nodesFor` takes the epoch it is asked about, which
 * is what lets a weekly rotation be a millisecond-long test instead of the
 * scheduler design §2.3 declines to build.
 */

const WEEK_MS = 7 * 24 * 60 * 60 * 1000
const DAY_MS = 24 * 60 * 60 * 1000
const MINUTE_MS = 60 * 1000

// 1970-01-01T00:00:00Z (the Date/Unix epoch) was a Thursday - Date#getUTCDay() === 4.
const UNIX_EPOCH_DAY_OF_WEEK = 4

export interface ServerTick {
  /** 0 (Sunday) - 6 (Saturday), matching `Date#getUTCDay()`. */
  tickDayOfWeek: number
  /** Minutes since UTC midnight, 0-1439. */
  tickMinuteOfDay: number
}

/**
 * The slice of the config bundle this module needs. Defined locally rather
 * than imported from config/bundle.ts's `Bundle`, which this module must not
 * depend on for its own purity to stay structurally checkable - so this stays
 * decoupled from the config-loading and database layers.
 *
 * NOT a placeholder waiting for the real bundle to grow a `nodes` field.
 * That happened in Task 5 - config/bundle.ts:119 reads nodes.json - and this
 * comment went on claiming the opposite for three tasks afterwards, in the
 * same words as config/validate.ts's docstring and config-validate.test.ts's,
 * all three corrected together rather than one at a time. What structural
 * typing buys is that the real `Bundle` ALREADY satisfies this interface with
 * neither side importing the other. That is the decoupling, not a migration
 * still owed.
 */
export interface BundleNode {
  id: string
  ratePerHour: number
  totalYield: number | null
}

export interface Bundle {
  nodes: BundleNode[]
}

/**
 * The node ids `nodesFor` REQUIRES a bundle to author, as a value rather
 * than as two string literals buried in a function body.
 *
 * It is exported so config/validate.ts can enumerate it at publish time
 * instead of restating it - the same reason validate.ts reads the currency
 * enum off db/schema.ts rather than listing currencies again. A bundle whose
 * nodes.json misspells one of these used to pass every check and then throw
 * a TypeError out of `nodesFor` on GET /v1/region/state, for every player on
 * the server, until someone rolled the bundle back.
 */
export const REQUIRED_NODE_IDS = ['common_vein', 'rich_deposit'] as const

export type NodeId = typeof REQUIRED_NODE_IDS[number]

export interface NodeState {
  slot: number
  type: NodeId
  ratePerHour: number
  totalYield: number | null
}

/**
 * Weeks elapsed since the first server tick at or before the epoch origin
 * (the Unix epoch, 1970-01-01T00:00:00Z) - a pure function of the server's
 * fixed tick fields and a timestamp, which is what lets a test cross a week
 * boundary without waiting a week.
 *
 * The brief leaves this function's body unspecified. Choices made here:
 *   - The reference tick instant is computed exactly, in integer
 *     milliseconds, and the elapsed-weeks division is done in BigInt rather
 *     than as a float `Math.floor`, so there is no floating-point rounding
 *     risk near a week boundary.
 *   - `tickDayOfWeek`/`tickMinuteOfDay` are UTC fields, matching
 *     `Date#getUTCDay()`. No timezone handling beyond UTC is implemented -
 *     nothing in the brief's tests asks for more.
 */
export function epochFor(server: ServerTick, at: Date): bigint {
  const daysBeforeOrigin = (UNIX_EPOCH_DAY_OF_WEEK - server.tickDayOfWeek + 7) % 7
  let referenceTick = -daysBeforeOrigin * DAY_MS + server.tickMinuteOfDay * MINUTE_MS
  // The candidate above is the tick on the origin's own calendar day. When
  // that lands AFTER the origin instant (same day, but later in the day),
  // it is not "at or before" the origin, so step back a full week.
  if (referenceTick > 0) referenceTick -= WEEK_MS

  const elapsedMs = BigInt(at.getTime() - referenceTick)
  const weekMs = BigInt(WEEK_MS)
  const quotient = elapsedMs / weekMs
  const remainder = elapsedMs % weekMs
  // BigInt division truncates toward zero; correct to a floor for negative elapsed.
  return remainder !== 0n && elapsedMs < 0n ? quotient - 1n : quotient
}

/**
 * Slot 0 is ALWAYS the Common Vein. The floor is structural rather than
 * probabilistic, so no seed can produce an epoch without one - bible §5.3,
 * design §4.1.
 *
 * `serverId`, `epoch` and `seed` are accepted but not yet used: with one
 * region and no relocation, the epoch boundary respawns the Rich Deposit in
 * place rather than moving it (design §4.1's note), so today's node set does
 * not vary by region, epoch or seed. The parameters exist so the
 * cross-region shuffle that DOES vary by them is a change to this
 * function's body, not to its callers.
 */
export function nodesFor(
  serverId: number, regionId: string, epoch: bigint, seed: bigint, bundle: Bundle,
): NodeState[] {
  // Typed `NodeId`, so the two ids below are checked against
  // REQUIRED_NODE_IDS rather than being free-floating strings: drop one from
  // that constant and this function stops compiling, which is what keeps the
  // set config/validate.ts enforces and the set this function demands from
  // drifting apart. The `!` is sound only BECAUSE the validator enforces
  // that set at publish time - see validateNodeRates.
  const byId = (id: NodeId) => bundle.nodes.find(n => n.id === id)!
  const common = byId('common_vein')
  const rich = byId('rich_deposit')
  return [
    { slot: 0, type: 'common_vein', ratePerHour: common.ratePerHour, totalYield: null },
    { slot: 1, type: 'rich_deposit', ratePerHour: rich.ratePerHour, totalYield: rich.totalYield },
  ]
}
