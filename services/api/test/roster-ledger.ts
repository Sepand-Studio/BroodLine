import { expect } from 'vitest'
import { rosterCount } from './wave-helpers.ts'

/**
 * THE ROSTER LEDGER - earned versus seeded, asserted rather than described.
 *
 * WHY IT EXISTS, and it is worth being blunt about it. Phase 6's central
 * demonstration was originally a TRANSCRIPT: a driver run once by hand, its
 * output pasted into a report, the driver deleted. That transcript was
 * internally impossible and nobody noticed for a full review cycle. It
 * reported thirteen creatures on the closing roster against only two node
 * claims, and asserted alongside them that "every mutation came from an HTTP
 * request". Both could not be true - ten of the thirteen had been inserted
 * directly by `giveRoster`.
 *
 * What caught it was not a test. It was a reviewer multiplying shard deltas
 * by hand. **A done-when that can only be checked by a human doing arithmetic
 * over a transcript is not a gate, it is a claim**, and the specific thing it
 * failed to notice - SEEDED creatures being described as EARNED - is free to
 * recur invisibly as long as nothing asserts the distinction.
 *
 * ---------------------------------------------------------------------------
 * WHY IT IS ITS OWN MODULE, extracted from `loop.test.ts` by Task 22.
 *
 * `ftue.test.ts` needs the same instrument for the opposite reading:
 * `loop.test.ts` books ten SEEDED creatures and says so, and `ftue.test.ts`
 * asserts `seeded === 0`. A second copy of a sixty-line class whose entire
 * job is to prevent one specific class of dishonesty is exactly the shape
 * `wave-helpers.ts`'s own header refuses ("a helper redefined in four test
 * files drifts in four directions"), and the drift here would be silent: a
 * copy that stopped measuring, or stopped reconciling, still reads as a
 * ledger and still prints a plausible total.
 *
 * ---------------------------------------------------------------------------
 * HONEST BY CONSTRUCTION, not by discipline.
 *
 * Three properties do the work, and none of them relies on a future editor
 * remembering anything:
 *
 *   1. EVERY booking is of a MEASURED delta - `rosterCount()` before and
 *      after - never of an argument's length. A `giveRoster(specs)` that
 *      inserted a different number than it was asked for still books what it
 *      really did.
 *
 *   2. `reconcile()` runs after EVERY step and compares the database's own
 *      count against the booked total. So a creature that enters the roster
 *      without being booked reddens at the very next step. There is no way to
 *      add creatures to a drive quietly - not by calling `giveRoster` again,
 *      not by inserting directly, not by a route grant nobody expected.
 *
 *   3. The closing assertion is on the SPLIT, not the sum. `earned + seeded
 *      === 13` is satisfied by booking all thirteen to either bucket, which
 *      is exactly the conflation this exists to prevent. `seeded === 10` and
 *      `earned === 3` are separate assertions and both must hold.
 *
 * The exact numbers a drive expects are DELIBERATELY BRITTLE. If content or
 * code changes the balance - a node that grants two, a wave that grants none
 * - the drive goes red. That is correct: the supply line changed, and someone
 * should look at it rather than have the total absorb it.
 *
 * ---------------------------------------------------------------------------
 * `rosterCount()` READS `wave-helpers.ts`'s MODULE STATE, which `setupPlayer`
 * installs. So a ledger belongs to whichever player `setupPlayer` most
 * recently created, and a drive that creates a second player mid-flight needs
 * a second ledger. Both current callers create exactly one.
 */
export class RosterLedger {
  earned = 0
  seeded = 0
  readonly entries: string[] = []

  private book(delta: number, kind: 'EARNED' | 'SEEDED', why: string): void {
    if (kind === 'EARNED') this.earned += delta
    else this.seeded += delta
    this.entries.push(`${delta >= 0 ? '+' : ''}${delta}  ${kind.padEnd(6)}  ${why}`)
  }

  /**
   * Runs `action`, measures what it actually did to the roster, and books
   * THAT. The measured delta is returned so a caller can assert on it.
   *
   * Measuring rather than trusting the caller is property 1 from the header:
   * a route that grants two where one was expected, or a `giveRoster` that
   * inserts a different number than asked, is booked for what it really did.
   */
  private async record<T>(kind: 'EARNED' | 'SEEDED', why: string, action: () => T | Promise<T>):
    Promise<{ result: T; delta: number }> {
    const before = await rosterCount()
    // `T | Promise<T>` rather than `Promise<T>`: Hono types `app.request` as
    // `Response | Promise<Response>`, and widening here beats an `async` on
    // every call site - one of which would eventually be forgotten, and the
    // resulting unawaited action would be booked with a delta of zero.
    const result = await action()
    const delta = (await rosterCount()) - before
    this.book(delta, kind, why)
    return { result, delta }
  }

  /** A roster change the player obtained through an HTTP response. */
  earn<T>(why: string, action: () => T | Promise<T>): Promise<{ result: T; delta: number }> {
    return this.record('EARNED', why, action)
  }

  /** A roster change written straight into the table. Never silent - see the header. */
  seed<T>(why: string, action: () => T | Promise<T>): Promise<{ result: T; delta: number }> {
    return this.record('SEEDED', why, action)
  }

  /**
   * THE ONE BOOKING THAT CANNOT MEASURE ITS OWN "BEFORE", and it is account
   * creation.
   *
   * `rosterCount()` reads `wave-helpers.ts`'s module-level `playerId`, which
   * `setupPlayer` is what installs - so there is no call that could be made
   * ahead of the account to measure against. The before is nevertheless known
   * EXACTLY rather than assumed: `POST /v1/account` mints the player id it
   * returns, so no row anywhere can be keyed to it beforehand. Zero is a
   * consequence of the id being new, not a default standing in for a number
   * nobody took.
   *
   * Booked as EARNED because that is what `earn` means here - the creatures
   * arrived because a request was made and a response came back. `starter.json`'s
   * pair is granted inside the account-creation transaction, so it is the
   * first thing in the drive that a player did rather than a fixture did.
   *
   * The whole resulting count is booked, so a bundle that granted three, or
   * none, moves the ledger rather than being absorbed by it.
   */
  async open(why: string): Promise<number> {
    if (this.entries.length > 0) {
      throw new Error('roster-ledger: open() is the first booking of a drive, and this one already has bookings')
    }
    const delta = await rosterCount()
    this.book(delta, 'EARNED', why)
    return delta
  }

  get total(): number { return this.earned + this.seeded }

  /** The itemisation, for a failure message. A reader of a green run does not need it. */
  get detail(): string { return `\n${this.entries.map((e) => `  ${e}`).join('\n')}\n` }

  /**
   * Property 2: the database's own count against the booked total, after
   * every step. A creature that entered without being booked reddens HERE, at
   * the next step, rather than surviving to be described as earned.
   */
  async reconcile(label: string): Promise<void> {
    const actual = await rosterCount()
    expect(
      actual,
      `roster arithmetic after ${label}: the database holds ${actual} live creature(s) but `
      + `${this.total} were booked (${this.earned} earned + ${this.seeded} seeded). `
      + `A difference means something changed the roster without being accounted for.`
      + this.detail,
    ).toBe(this.total)
  }
}
