import type { Tx } from '../db/client.ts'
import { creatures } from '../db/schema.ts'
import { creatureHp, toCreatureDto, type CreatureDto } from '../roster/creatures.ts'
import { setMarker } from './markers.ts'

/**
 * The wave-6 Pale - "what this plan found the design missed" item 1, and
 * waves_01_12 wave 6's own text: "The Wave Defeat screen grants a Pale,
 * framed as a Warden resupply … If they somehow win, the Pale grant fires
 * anyway."
 *
 * Wave 6 is a DESIGNED LOSS - a single Courser runs an empty lane, and the
 * only counter to a Courser is Chill (Stats.CounterFor(Courser),
 * replay-format.ts's own comment on `losingDeployment`). Task 6 withheld
 * Pale - the only species base stock ever authors with Chill - from every
 * base-stock roll until this grant fires, so before this task a player who
 * lost wave 6 had NO path to Chill at all: not a roll, not a claim, nothing.
 * This is that path.
 *
 * trait2/tier2/instinct mirror `BASE_STOCK_SPECIES`'s own Pale entry
 * (roster/creatures.ts) rather than re-deriving them, for the same reason
 * that table gives every species Carapace in slot 2 and Vanguard as its
 * Instinct: this IS a base-stock Pale, just handed over by name instead of
 * by roll.
 */
export const WAVE6_PALE = {
  species: 'Pale',
  trait1: 'Chill',
  tier1: 1,
  trait2: 'Carapace',
  tier2: 1,
  instinct: 'Vanguard',
} as const

/**
 * Once per player, on the first settlement of a wave-6 issuance - win or
 * loss alike, matching the authored text's "fires anyway" on a win.
 *
 * NULL WHEN THE MARKER WAS ALREADY SET - the same mechanism `grantFounder`
 * uses and for the same reason: `setMarker` returns true only for the call
 * that actually flips `wave6_pale_granted_at` from NULL, so two settlements
 * racing to be "the first" cannot both mint a Pale.
 *
 * NO CAP CHECK, unlike `grantWaveBaseStock`'s own grants. This creature IS
 * the answer the Wave Defeat screen just named the player a raider and a
 * trait for; refusing it `roster_full` at the Hatchery cap would teach the
 * opposite of what the beat exists to teach. `grantWaveBaseStock` is not
 * this function's caller - the two are independent grants inside the same
 * settlement transaction - so that function's cap check does not run here.
 */
export async function grantWave6Pale(tx: Tx, serverId: number, playerId: string): Promise<CreatureDto | null> {
  if (!(await setMarker(tx, serverId, playerId, 'wave6_pale_granted_at'))) return null
  const [row] = await tx.insert(creatures).values({
    serverId,
    playerId,
    generation: 1,
    ...WAVE6_PALE,
    isFounder: false,
    name: null,
    hpCurrent: creatureHp(WAVE6_PALE.species),
  }).returning()
  return toCreatureDto(row!)
}
