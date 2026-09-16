import type { Tx } from '../db/client.ts'
import { grantFounder } from '../ftue/founder.ts'
import { readMarkers } from '../ftue/markers.ts'
import { loadArk } from '../map/claim.ts'
import { baseStockPool, grantBaseStock, lockRoster, rosterCap, rosterCount } from '../roster/creatures.ts'

/**
 * Design §2.4 - the supply line that makes the loop closeable rather than
 * merely closeable once.
 *
 * A splice is net **−1** creature (`sample_economy` §8, restated by design
 * §2.4), so a loop with no source runs for as many splices as the player has
 * fodder and then seizes. `POST /v1/node/claim` is one source; this is the
 * other, and it is the one `base_stock` §3 makes a FLOOR.
 *
 * ITS OWN MODULE RATHER THAN A FUNCTION IN `wave/rewards.ts`, which is where
 * it first went. `rewards.ts` is pure - it reads a bundle and returns a
 * number, imports no database, and adversarial.test.ts drives it in a
 * deliberately container-free `describe` that says so in its own comment.
 * Putting a `Tx`-taking function in it would drag `db/client.ts` (and, through
 * `loadArk`, `map/claim.ts`) into that block's import graph for two
 * synchronous assertions that touch neither.
 */

/**
 * One Gen-1 creature per wave completion.
 *
 * `base_stock` §3's table gives campaign and replay waves 2.0 / 4.0 / 5.0 a
 * day across the three archetypes and §7 restates it as "four uniform wave
 * drops a day". Against `REPLAY_CAP_PER_DAY` of 3 plus campaign progression,
 * one per completion lands inside that band; a larger number could not,
 * because the replay cap bounds completions from above.
 *
 * A CONSTANT AND NOT A CURVE, which is the same shape `rosterCap` has and for
 * a stronger reason: a curve here would be a thing that scales, and the whole
 * point of this line is that nothing scales it.
 */
export const WAVE_BASE_STOCK = 1

/**
 * `base_stock` §3's guardrail, enforced by the signature.
 *
 * > **Wave-completion base stock never scales with any facility, purchase,
 * > tier or event.** It is the floor under every player, and it is the only
 * > supply line that cannot be accelerated by anything.
 *
 * THERE IS NO MULTIPLIER ARGUMENT, AND THAT IS THE ENFORCEMENT. `claimNode`'s
 * grant takes the Harvest Array tier because `base_stock` §3.1 scales node
 * yield by it; this one cannot be handed that tier because it does not accept
 * it. The cleanest way to honour a rule that says *never scale* is for the
 * function not to accept the thing it must ignore - a parameter it merely
 * ignored would be one edit away from being used, and the edit would look
 * like a bug fix.
 *
 * THE HATCHERY CAP IS A SKIP, NOT A REFUSAL, and this is the one place the
 * wave path and the claim path deliberately differ over the same write.
 * Design §4.3 refuses the WHOLE claim when a grant would exceed the cap,
 * because a claim grants several creatures and truncating the grant silently
 * is a loss a player reports as theft. Here the grant is ONE creature and the
 * alternative is refusing a wave the player WON - which would make the
 * Hatchery cap a soft lockout for anyone holding a full roster, and bible
 * §7.2 forbids exactly that ("roster capacity must never bind below what the
 * counter system requires"). So a player at the cap is paid their shards and
 * granted nothing. `roster/creatures.ts`'s `grantBaseStock` deliberately
 * checks no cap of its own precisely so these two callers can make different
 * rulings over it.
 *
 * COUNTED UNDER THE CALLER'S TRANSACTION, not before it. `routes/wave.ts`
 * calls this inside the same transaction that settles the issuance and writes
 * the credit, so the count this decides on is one nothing else can change
 * before the insert lands.
 *
 * THE SEED IS THE ISSUANCE, which is a deviation from the brief's
 * `grantWaveBaseStock(tx, serverId, playerId)` and is reported as one.
 * `speciesForSeed` is a pure function of a string BECAUSE the species roll is
 * something a player can complain about (design §5.1's discipline, applied to
 * a grant), and a signature with nothing to derive that string from would
 * leave `Math.random()` or a clock as the only options. The issuance id is
 * fixed long before the grant, is unique, and is consumed exactly once - so
 * the roll is re-derivable after a dispute and cannot be re-rolled by
 * retrying. It is an IDENTITY, not a multiplier; the guardrail above is
 * untouched by it.
 *
 * BEAT 3: THE FIRST COMPLETION'S DROP IS THE FOUNDER, NOT A ROLL. design §5
 * beats 3-4 and campaign_structure name Hollow a Founder handed over on
 * session one's first wave clear, for the player to name at beat 4. That is
 * a DIFFERENT shape from every later completion, which rolls ordinary base
 * stock - so the branch below reads `founderGrantedAt` before deciding which
 * grant this is, rather than the Founder being a fourth species base stock
 * could roll.
 *
 * THE CAP CHECK ABOVE ALREADY COVERS BOTH BRANCHES. `WAVE_BASE_STOCK` is 1
 * whether the drop is a Founder or a roll, so a player at the cap is skipped
 * before either grant path runs - the Founder does not get a cap exemption
 * a rolled creature would not.
 */
export async function grantWaveBaseStock(
  tx: Tx, serverId: number, playerId: string, issuanceId: string,
): Promise<number> {
  // Before the count, not after: this and `claimNode` are the only two
  // granting paths and they hold no lock in common, so without it both can
  // read the same pre-grant count and both pass the cap check.
  //
  // REDUNDANT BUT HARMLESS when reached from `routes/wave.ts`'s wave-submit
  // handler, which is this function's only real caller - fix round 2.
  // `pg_advisory_xact_lock` is re-entrant within one transaction, and that
  // handler now takes this SAME lock, on this SAME player, as the first
  // thing it does in the whole submit transaction, for a different reason
  // (serialising against a concurrent `splice/commit` at the row-lock layer
  // - fix round 2's deadlock). LEFT HERE rather than trimmed: this
  // function's own contract - "does not race `claimNode`'s cap check" -
  // should not silently start depending on a caller having already taken
  // the lock for an unrelated reason. The cost of keeping it is one no-op
  // re-acquisition per call.
  await lockRoster(tx, serverId, playerId)

  const ark = await loadArk(tx, serverId, playerId)
  const cap = rosterCap(ark.hatcheryTier)
  if (await rosterCount(tx, serverId, playerId) + WAVE_BASE_STOCK > cap) return 0

  const markers = await readMarkers(tx, serverId, playerId)
  if (markers.founderGrantedAt === null) {
    // `grantFounder` returns null only when some other call already set the
    // marker between the read above and this insert - ftue/markers.ts's
    // `setMarker` is what makes that exactly-once under concurrency. That
    // caller is granting the Founder; this one grants nothing further,
    // rather than falling through to a rolled creature the cap check above
    // has already budgeted for as "one creature, this completion".
    return (await grantFounder(tx, serverId, playerId)) === null ? 0 : 1
  }

  const granted = await grantBaseStock(tx, serverId, playerId, WAVE_BASE_STOCK,
    `wave:${serverId}:${playerId}:${issuanceId}`, baseStockPool(markers))
  return granted.length
}
