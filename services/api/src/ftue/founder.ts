import type { Tx } from '../db/client.ts'
import { creatures } from '../db/schema.ts'
import { creatureHp, type CreatureRow } from '../roster/creatures.ts'
import { setMarker } from './markers.ts'

/**
 * The Founder - design §5 beats 3-4, campaign_structure's naming of Hollow.
 *
 * A HOLLOW, NO TRAITS, VANGUARD. base_stock 155 names Hollow the species
 * granted "session one, beat 3 - the named one", and roster/creatures.ts's
 * own base-stock table has no authored trait pair for Hollow at all (only
 * Vetch, Pale and Ember have one) - so `trait1`/`trait2` are 'None' with no
 * coverage tier, the same shape wave7RosterSpecs() and WaveContentTests'
 * ColdOpenPair-adjacent Hollow fixture already give it. Vanguard because
 * every base-stock grant carries it (bible 1.4's per-species weighting is
 * owed, not guessed - roster/creatures.ts's own comment on the point).
 */
export const FOUNDER = {
  species: 'Hollow',
  trait1: 'None',
  tier1: null,
  trait2: 'None',
  tier2: null,
  instinct: 'Vanguard',
} as const

/**
 * The Hollow, once - design §5 beat 3: "the first completion's drop IS the
 * Founder. Not a roll."
 *
 * NULL WHEN THE MARKER WAS ALREADY SET, and that is the entire mechanism
 * for "granted on the first completion" rather than a caller-side check of
 * `highestWaveCleared`. `setMarker` returns true only for the call that
 * actually flips `founder_granted_at` from NULL - ftue/markers.ts's own
 * doc has the concurrency argument - so two wave completions racing to be
 * "the first" cannot both mint a Founder: exactly one sees `true` and
 * inserts, the other sees `false` and returns null, and its caller
 * (`wave/base-stock.ts`) falls back to nothing rather than a second Founder.
 *
 * NEVER NAMED HERE. `name` is null on every grant - beat 4 is a separate
 * moment, the player's own, and `POST /v1/creature/name` (routes/creature.ts)
 * is the only writer of it.
 */
export async function grantFounder(tx: Tx, serverId: number, playerId: string): Promise<CreatureRow | null> {
  if (!(await setMarker(tx, serverId, playerId, 'founder_granted_at'))) return null
  const [row] = await tx.insert(creatures).values({
    serverId,
    playerId,
    generation: 1,
    ...FOUNDER,
    isFounder: true,
    name: null,
    hpCurrent: creatureHp(FOUNDER.species),
  }).returning()
  return row!
}
