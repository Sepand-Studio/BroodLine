import { and, eq } from 'drizzle-orm'
import type { Tx } from '../db/client.ts'
import { campaignProgress, creatures } from '../db/schema.ts'
import { creatureHp, lockRoster, toCreatureDto, type CreatureDto } from '../roster/creatures.ts'
import { isFirstSplice } from '../splice/commit.ts'
import { setMarker } from './markers.ts'

/**
 * `POST /v1/ftue/splice-stock`'s grant - design §5 beats 6-7,
 * `splice_confirm_spec` §6.
 *
 * THE PROVIDED PAIR, never anything the player earned and never the Founder
 * they just named - `splice_confirm_spec` §6: this stock is "provided
 * specifically for the tutorial and framed as sample stock", so the guided
 * splice's lesson (splicing destroys both parents) lands with nothing real at
 * stake. Vetch and Ember are the two base-stock species with an authored
 * SECOND trait of their own (roster/creatures.ts's `baseStockSpecies`) -
 * Pale's is withheld until wave 6 (Task 6) and would leak Chill into the
 * tutorial ahead of the beat that exists to make the player want it.
 */
export const TUTORIAL_STOCK = [
  { species: 'Vetch', trait1: 'Taunt', tier1: 1, trait2: 'Carapace', tier2: 1 },
  { species: 'Ember', trait1: 'Splash', tier1: 1, trait2: 'Carapace', tier2: 1 },
] as const

export type StockResult =
  | { kind: 'ok'; creatures: CreatureDto[] }
  | { kind: 'unavailable'; why: string }

/**
 * Grants the tutorial's Vetch and Ember, or refuses and says why.
 *
 * THREE GATES, IN ORDER, and each is a genuinely different reason to say
 * "not now" - collapsing them into one refusal would be the same mistake
 * `generation_ceiling`/`creature_committed`/`insufficient_charges` staying
 * distinct exists to avoid (http/errors.ts):
 *
 *  - wave 2 not yet cleared - the tutorial has not reached this beat yet;
 *  - this player's first splice has already happened - by the time it has,
 *    the destroys-both-parents lesson already landed on something else, and
 *    handing over a second guaranteed-mutation pair now would be a second
 *    freebie, not the beat design §5 wrote;
 *  - the marker is already set - `setMarker`'s own once-only write, so a
 *    retry after a successful grant is refused rather than minting a second
 *    pair. Checked LAST and via `setMarker`'s own return, not via a
 *    caller-side read of the marker, for the concurrency reason
 *    ftue/markers.ts's own doc gives: two racing grants must not both see
 *    "not yet granted" and both insert.
 *
 * `lockRoster` RUNS BEFORE THE SECOND GATE, not merely before the third -
 * fix round 1's finding, and the actual defect it closes. `isFirstSplice` is
 * a bare, unlocked `count(*)` on `splices`, and reading it outside the lock
 * that guards it is what let a real `POST /v1/splice/commit` land
 * concurrently with this grant: under READ COMMITTED, this transaction could
 * count zero splice rows at the exact moment a commit was writing its first
 * one, and both would proceed - the tutorial pair granted to a player who
 * had already spliced. `commitSplice` now takes this SAME advisory lock
 * FIRST, before anything else it does, so the two paths fully serialise:
 * whichever wins is committed (or refused) before the other reads anything,
 * and the loser observes the winner's true state rather than a stale one.
 *
 * NO CAP CHECK, deliberately: this pair exists to be spliced away
 * immediately (`splice_confirm_spec` §6), and refusing it on a full Hatchery
 * would strand the tutorial at the one beat that teaches its central lesson.
 */
export async function grantTutorialStock(tx: Tx, serverId: number, playerId: string): Promise<StockResult> {
  const [progress] = await tx.select().from(campaignProgress)
    .where(and(eq(campaignProgress.serverId, serverId), eq(campaignProgress.playerId, playerId)))
  if ((progress?.highestWaveCleared ?? 0) < 2) return { kind: 'unavailable', why: 'Clear wave 2 first.' }

  await lockRoster(tx, serverId, playerId)

  if (!(await isFirstSplice(tx, serverId, playerId))) {
    return { kind: 'unavailable', why: 'The tutorial splice has already happened.' }
  }

  if (!(await setMarker(tx, serverId, playerId, 'tutorial_stock_granted_at'))) {
    return { kind: 'unavailable', why: 'Already granted.' }
  }

  const rows = await tx.insert(creatures).values(TUTORIAL_STOCK.map((c) => ({
    serverId,
    playerId,
    generation: 1,
    ...c,
    instinct: 'Vanguard',
    isFounder: false,
    name: null,
    hpCurrent: creatureHp(c.species),
  }))).returning()
  return { kind: 'ok', creatures: rows.map(toCreatureDto) }
}
