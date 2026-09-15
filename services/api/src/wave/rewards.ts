import type { Bundle } from '../config/bundle.ts'
import type { Currency } from '../money/ledger.ts'

export interface Reward { currency: Currency; amount: number }

/**
 * Design 2.2: the reward is a function of the ISSUANCE'S wave id, never of
 * the submission. The submission decides WHETHER it is paid; it never
 * decides WHAT.
 *
 * That is what makes an unowned deployment - which this phase cannot detect,
 * because there is no creature table until Phase 6 - bounded to "win a wave
 * you would otherwise lose" rather than "mint currency".
 */
export function rewardForWave(bundle: Bundle, waveId: number): Reward | null {
  const wave = bundle.waves.find((w) => w.id === waveId)
  if (wave === undefined || wave.reward === undefined) return null
  return { currency: wave.reward.currency, amount: wave.reward.amount }
}
