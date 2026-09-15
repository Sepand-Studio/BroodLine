import { describe, expect, it } from 'vitest'
import { matchesIssuance, toBreachDto } from '../src/routes/wave.ts'
import { toInt, type SimulateBreach, type SimulateEcho } from '../src/sim/client.ts'

/**
 * services/api/src/generated/sim.ts types every integer `number | string` -
 * minimal APIs' JsonSerializerDefaults.Web sets NumberHandling.
 * AllowReadingFromString, which is faithful generation but over-broad for a
 * RESPONSE body, where sim in practice always writes a plain JSON number.
 * The whole point of `toInt` and the call sites that use it
 * (routes/wave.ts's `matchesIssuance`, `toBreachDto`) is to make code that
 * consumes these fields correct EVEN IF the string branch is ever hit, not
 * only correct against what sim happens to send today. These tests
 * construct exactly that string-typed case, so they fail if the narrowing
 * is ever removed and a bare `!==`/passthrough is put back in its place.
 */
describe('toInt', () => {
  it('passes a real number through unchanged', () => {
    expect(toInt(6)).toBe(6)
  })

  it('parses the string a generated field is typed to allow', () => {
    expect(toInt('6')).toBe(6)
  })
})

describe('matchesIssuance', () => {
  const issuance = { seed: '12345', waveId: 6 }

  it('matches when echo.waveId is a genuine number', () => {
    const echo = { waveId: 6, seed: '12345' } as SimulateEcho
    expect(matchesIssuance(echo, issuance)).toBe(true)
  })

  // THE LOAD-BEARING CASE. echo.waveId's generated type is `number |
  // string` - this constructs the string branch directly, something a real
  // sim response is unlikely to ever produce but the TYPE explicitly
  // allows, which is exactly why the comparison must not assume the number
  // branch. Replace matchesIssuance's `toInt(echo.waveId) === issuance.
  // waveId` with a bare `echo.waveId === issuance.waveId` and this fails:
  // the string "6" and the number 6 are `!==` in JavaScript, so a matching
  // wave id would be reported as a mismatch - the seed-shopping / wrong-wave
  // guard (design §4.2 step 5) firing on an HONEST submission.
  it('still matches when echo.waveId arrives as the string the type allows', () => {
    const echo = { waveId: '6', seed: '12345' } as SimulateEcho
    expect(matchesIssuance(echo, issuance)).toBe(true)
  })

  it('refuses a genuine mismatch regardless of which branch the type took', () => {
    const wrongNumber = { waveId: 7, seed: '12345' } as SimulateEcho
    const wrongString = { waveId: '7', seed: '12345' } as SimulateEcho
    expect(matchesIssuance(wrongNumber, issuance)).toBe(false)
    expect(matchesIssuance(wrongString, issuance)).toBe(false)
  })

  it('refuses a seed mismatch even when the wave id matches', () => {
    const echo = { waveId: 6, seed: '99999' } as SimulateEcho
    expect(matchesIssuance(echo, issuance)).toBe(false)
  })
})

describe('toBreachDto', () => {
  it('narrows every number|string field to a real number and keeps the booleans', () => {
    const breach = {
      tick: '90', raider: '0', lane: '0', type: 'Courser',
      access: false, coverage: true, placement: true,
    } as SimulateBreach

    const dto = toBreachDto(breach)
    expect(dto).toEqual({
      tick: 90, raider: 0, lane: 0, type: 'Courser',
      access: false, coverage: true, placement: true,
    })
    expect(typeof dto.tick).toBe('number')
    expect(typeof dto.raider).toBe('number')
    expect(typeof dto.lane).toBe('number')
  })
})
