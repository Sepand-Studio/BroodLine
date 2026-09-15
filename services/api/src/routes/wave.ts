import { eq } from 'drizzle-orm'
import type { Hono } from 'hono'
import type { Deps } from '../app.ts'
import { loadBundle } from '../config/bundle.ts'
import { withServer, type Tx } from '../db/client.ts'
import { players } from '../db/schema.ts'
import { requireSession } from '../http/auth.ts'
import { fail } from '../http/errors.ts'
import { isUuid } from '../http/ids.ts'
import { hashRequest } from '../http/hash.ts'
import type { SessionClaims } from '../identity/jwt.ts'
import { IdempotencyMismatchError, withIdempotency } from '../money/idempotency.ts'
import { credit } from '../money/ledger.ts'
import { rewardForWave } from '../wave/rewards.ts'
import {
  advanceCampaign, type Issuance, issueWave, loadLiveIssuance, settle,
} from '../wave/issuance.ts'
import { type SimulateBreach, type SimulateEcho, toInt } from '../sim/client.ts'

interface StartBody { waveId: number }

/**
 * The parse layer's SANITY bound on waveId. Not a statement about which
 * waves exist: `issueWave`'s authored-wave check stays the only authority on
 * that, and every id inside this range still goes to it, wave 6 and wave
 * 2,000,000 alike.
 *
 * 2^31-1 because `wave_issuances.wave_id` is a Postgres `integer`
 * (drizzle/0003_wave_issuances.sql). An id above it cannot be stored by this
 * schema under ANY bundle, so it is malformed by construction rather than
 * merely unavailable - which is the distinction this bound exists to
 * restore. `invalid_request` (400) says the request was malformed;
 * `wave_locked` (409) says it was understood and refused, and a client could
 * not tell those apart for `waveId: 2**53`, which reached `issueWave` and
 * cost a campaign_progress lookup and a bundle scan to be told 409.
 *
 * Deliberately far above anything content will ever author, so that changing
 * which waves a bundle carries never means touching this number.
 */
const MAX_WAVE_ID = 2_147_483_647

function parseStart(raw: unknown): StartBody | null {
  if (typeof raw !== 'object' || raw === null) return null
  const b = raw as Record<string, unknown>
  if (typeof b.waveId !== 'number' || !Number.isInteger(b.waveId)) return null
  // The floor is 1 for the same reason as the ceiling, from the other end:
  // no bundle can author wave 0 or a negative wave, so the answer does not
  // depend on content either. `issueWave` still refuses every waveId < 1 as
  // wave_locked and keeps doing so - this route can simply no longer hand it
  // one; test/wave-start.test.ts calls it directly to pin that outcome.
  if (b.waveId < 1 || b.waveId > MAX_WAVE_ID) return null
  return { waveId: b.waveId }
}

interface SubmitBody { issuanceId: string; replay: string }

function parseSubmit(raw: unknown): SubmitBody | null {
  if (typeof raw !== 'object' || raw === null) return null
  const b = raw as Record<string, unknown>
  // `wave_issuances.issuance_id` is a Postgres `uuid`
  // (drizzle/0003_wave_issuances.sql), and `loadLiveIssuance` compares this
  // value against it. Checking only for a non-empty string let
  // `issuanceId: 'not-a-uuid'` reach that comparison, where Postgres raises
  // `22P02` and app.ts's onError returns `internal` - so any authenticated
  // player could make a well-formed request answer 500 for a body they
  // malformed. See http/ids.ts; this is a shape check and NOT a claim about
  // which issuances exist, so a fabricated uuid still gets step 2's
  // ordinary `issuance_invalid`.
  if (!isUuid(b.issuanceId)) return null
  if (typeof b.replay !== 'string' || b.replay.length === 0) return null
  return { issuanceId: b.issuanceId, replay: b.replay }
}

/**
 * Design §4.2 step 5, extracted as a pure function specifically so it can be
 * unit-tested independently of a real `sim` process.
 *
 * `echo.seed` is a `string` in the generated types (correctly - sim.ts's
 * one honestly-typed numeric-looking field) and `issuance.seed` is the same
 * decimal-string customType wave/start stores, so that half compares two
 * real strings directly. `echo.waveId` is NOT honestly typed - it is
 * `number | string`, the generated contract's CRITICAL TYPING HAZARD (see
 * sim/client.ts's `toInt` doc) - so it is narrowed with `toInt` before
 * comparing against `issuance.waveId`, a genuine `number` column. Comparing
 * the raw union with `!==` would still type-check (the union includes
 * `number`) while being wrong the moment the string branch is actually hit:
 * `"6" !== 6` is `true` in JS. test/sim-client.test.ts proves this by
 * constructing an echo whose `waveId` arrives as the string `"6"` and
 * asserting it still matches an issuance whose `waveId` is the number `6`.
 */
export function matchesIssuance(echo: SimulateEcho, issuance: Pick<Issuance, 'seed' | 'waveId'>): boolean {
  return echo.seed === issuance.seed && toInt(echo.waveId) === issuance.waveId
}

/** Design: `api` forwards the breach diagnosis and interprets none of it - narrowed only so the response honours its own numeric schema. */
export function toBreachDto(b: SimulateBreach): {
  tick: number; raider: number; lane: number; type: string
  access: boolean; coverage: boolean; placement: boolean
} {
  return {
    tick: toInt(b.tick), raider: toInt(b.raider), lane: toInt(b.lane), type: b.type,
    access: b.access, coverage: b.coverage, placement: b.placement,
  }
}

async function loadPlayerId(tx: Tx, accountId: string): Promise<string | undefined> {
  const [player] = await tx.select().from(players).where(eq(players.accountId, accountId))
  return player?.playerId
}

function refusalMessage(code: 'issuance_invalid' | 'submission_rejected' | 'wave_locked'): string {
  if (code === 'issuance_invalid') return 'That issuance is not live for this player.'
  if (code === 'submission_rejected') return 'That submission was rejected.'
  return 'That wave has no reward configured.'
}

/**
 * Handles every `sim` rejection OTHER than `engine_too_old` (design §2.3:
 * that one is player-visible and must leave the issuance live so the client
 * can retry after updating). Everything else - forged bytes, a rule
 * violation - is a spent attempt: the player simulated a wave and got an
 * answer, so the issuance is consumed even though nothing is paid.
 *
 * Runs OUTSIDE withIdempotency deliberately, mirroring the brief exactly:
 * the idempotency key protects a PAID response being replayed, not a
 * rejection - a rejection has no side effect worth replaying verbatim, only
 * one worth not repeating (settle()'s own `AND settled_at IS NULL` guard is
 * what makes a retry of a rejected submission land on `issuance_invalid`
 * rather than double-settling anything).
 */
async function consumeAndRefuse(deps: Deps, session: SessionClaims, issuanceId: string): Promise<Response> {
  return withServer(deps.db, session.serverId, async (tx) => {
    const playerId = await loadPlayerId(tx, session.accountId)
    if (playerId === undefined) return fail('issuance_invalid', refusalMessage('issuance_invalid'))

    const issuance = await loadLiveIssuance(tx, session.serverId, playerId, issuanceId)
    if (issuance === undefined) return fail('issuance_invalid', refusalMessage('issuance_invalid'))

    await settle(tx, issuance, 'consumed')
    return fail('submission_rejected', refusalMessage('submission_rejected'))
  })
}

type SubmitOutcome =
  | { refused: 'issuance_invalid' | 'submission_rejected' | 'wave_locked' }
  | { paid: null; result: string; integrityRemaining: number; breaches: SimulateBreach[] }
  | { paid: { currency: string; amount: number }; result: string; integrityRemaining: number; breaches: SimulateBreach[] }

export function registerWaveRoutes(app: Hono, deps: Deps): void {
  app.post('/v1/wave/start', async (c) => {
    // requireSession throws HttpError on a failed check; app.ts's onError
    // special-cases it and returns its response as-is, so there is no
    // try/catch boilerplate needed here. serverId and accountId come from
    // the VERIFIED claim, never from the body - RLS defends against a
    // handler that forgets to scope, not one that scopes to the wrong
    // server.
    const session = await requireSession(c)

    const raw = await c.req.json().catch(() => null)
    const body = parseStart(raw)
    // The message names the whole rule, not just the missing-field half of
    // it: `waveId: 2**53` IS present, and answering it "waveId is required."
    // would be a refusal that misstates its own reason. The client switches
    // on `code`, never on this text (solo_execution 6.2), so the wording is
    // free to be accurate.
    if (body === null) {
      return fail('invalid_request', `waveId must be an integer between 1 and ${MAX_WAVE_ID}.`)
    }

    const bundle = await loadBundle(deps.bundleStore)

    const result = await withServer(deps.db, session.serverId, async (tx) => {
      const [player] = await tx.select().from(players)
        .where(eq(players.accountId, session.accountId))
      if (player === undefined) return null

      return issueWave(tx, session.serverId, player.playerId, body.waveId, bundle)
    })

    if (result === null) return fail('not_found', 'No player on this server for that account.')

    if ('refused' in result) {
      if (result.refused === 'wave_locked') {
        return fail('wave_locked', 'That wave is not available to you right now.')
      }
      return fail('replay_cap_reached', 'You have replayed this wave the maximum number of times today. Try again tomorrow.')
    }

    return c.json({
      issuanceId: result.issuanceId,
      // seed is already a decimal STRING (services/api/src/db/schema.ts's
      // int8String customType) - do not coerce it to a number here or
      // anywhere downstream. A JS number above 2^53 loses precision, which
      // would make the client re-simulate against a different seed than the
      // one stored, and that presents as a hash mismatch on an honest
      // submission.
      seed: result.seed,
      waveId: result.waveId,
      expiresAt: result.expiresAt.toISOString(),
    })
  })

  app.post('/v1/wave/submit', async (c) => {
    const session = await requireSession(c) // 1

    const key = c.req.header('idempotency-key')
    if (!key) return fail('invalid_request', 'An Idempotency-Key header is required.')

    const raw = await c.req.json().catch(() => null)
    const body = parseSubmit(raw)
    // The message names the whole rule rather than only the missing-field
    // half of it - `issuanceId: 'not-a-uuid'` IS present, and "required"
    // would be a refusal that misstates its own reason. The client switches
    // on `code`, never on this text (solo_execution §6.2). Same correction
    // region.ts's parseClaim message already carries.
    if (body === null) {
      return fail('invalid_request', 'issuanceId must be a uuid, and replay is required.')
    }

    // 2. LIVENESS FIRST - design §4.2's own step 2, restored to the position
    // that table specifies ("The sequence, and the order matters"). This
    // handler shipped with steps 2 and 3 transposed, so a fabricated or
    // already-settled issuanceId bought a full re-simulation before being
    // refused.
    //
    // WHY THE TRANSPOSITION HAPPENED, and why undoing it is safe. The
    // constraint the original order was honouring is the one restated
    // below: do not hold a Postgres connection across the sim call. That is
    // NOT the same constraint as "do not touch the database before the sim
    // call", and collapsing the two is what moved this check. The read
    // below opens its own transaction and COMMITS IT before `simulate` is
    // reached, so the connection is back in the pool for the whole duration
    // of the network call. Do not fold this into the withIdempotency
    // transaction below, and do not hoist that transaction up here - either
    // reintroduces exactly the pool exhaustion solo_execution §5.7 names.
    //
    // THIS IS NOT AN ENUMERATION ORACLE, which is the reason that would
    // have made the transposed order load-bearing and does not apply.
    // `loadLiveIssuance` is scoped by serverId AND playerId AND issuanceId
    // together, and serverId/accountId come from the VERIFIED claim, so the
    // only rows this can ever answer about are the caller's own. There is
    // nothing to enumerate: a caller learns whether an issuance they were
    // themselves handed is still live. Contrast /v1/session/refresh
    // (routes/session.ts), which DOES collapse every failure into one
    // indistinguishable 401 - there the caller is unauthenticated and the
    // token IS the credential being guessed, so distinguishing a bad
    // signature from an unknown account is an account-enumeration oracle.
    // Here the caller is already authenticated as the only player whose
    // issuances this query can return. The distinction is also not new:
    // consumeAndRefuse below has always answered `submission_rejected` for
    // a live issuance and `issuance_invalid` for a dead one, in a single
    // request, on the sim-rejected branch.
    //
    // ADVISORY, NOT AUTHORITATIVE, AND IT GATES ONLY THE SIM CALL. The
    // binding check is still the one inside the withIdempotency
    // transaction, where it sits under the same row lock as settle() - this
    // read is outside any transaction that matters and cannot be trusted
    // for the credit decision. It is safe to skip sim on, and only to skip
    // sim on, because the transition is ONE-WAY: settled_at is write-once
    // and expires_at is fixed at issue, so an issuance that reads dead here
    // can never be live by the time the transaction opens. A false skip is
    // therefore impossible; only a false PROCEED is, and that is what the
    // in-transaction check catches.
    //
    // IT MUST NOT SHORT-CIRCUIT THE RESPONSE, and this is the part that
    // bit. Returning `issuance_invalid` from here directly - the obvious
    // shape, and the one written first - BREAKS design §4.2's guard one:
    // the idempotency key protects the RESPONSE, so a client resending a
    // request that already succeeded must get its stored 200 back. By the
    // time it resends, the issuance it paid for is settled, so a refusal
    // taken here would answer a retrying client 409 for a wave it was in
    // fact paid for. That is the phase's central claim ("pays exactly once
    // under retry") failing in the direction the player notices. Caught by
    // `returns the stored response on a resend with the SAME key`, which
    // went red the moment the early return went in. So a dead issuance
    // skips sim and then falls through to withIdempotency exactly as a live
    // one does, and the refusal - when it is a refusal - is produced there.
    //
    // A PLAIN SELECT, never SELECT ... FOR UPDATE. Under READ COMMITTED a
    // plain read does not block on a concurrent uncommitted UPDATE of this
    // row, so a submission racing another consumer still reads it live
    // here, proceeds, and blocks where it is supposed to - on settle()'s
    // own UPDATE inside the money transaction. adversarial.test.ts's
    // `cannot replay a winning submission twice under a genuinely
    // concurrent second attempt` asserts that block is observed
    // (`sawWaiter`), and a locking read here would move it.
    //
    // TWO DEAD-PATH ANSWERS CHANGE, and both are recorded rather than
    // smoothed over (see specs/plans/broodline_phase5_validation.md §4.2's
    // amendment). For a DEAD issuance only: a too-old engine now answers
    // `issuance_invalid` (409) rather than `engine_too_old` (426), and a
    // sim outage answers `issuance_invalid` (409) rather than
    // `sim_unavailable` (503). Neither is reachable without first calling
    // sim, which is the cost this reorder exists to avoid. Design §2.3's
    // invariant is untouched - engine_too_old must LEAVE THE ISSUANCE LIVE,
    // and nothing here settles anything - and a client with a superseded
    // engine still learns so on its next submission against a live
    // issuance, which is the only submission that could ever have
    // succeeded. The outage case is strictly more honest: a retryable 503
    // for an issuance that can never succeed invites a retry loop that
    // cannot terminate. For a LIVE issuance every answer is unchanged, and
    // for a dead one under a key that ALREADY HAS a stored response the
    // answer is that stored response, exactly as before.
    const issuanceIsLive = await withServer(deps.db, session.serverId, async (tx) => {
      const playerId = await loadPlayerId(tx, session.accountId)
      if (playerId === undefined) return false
      return (await loadLiveIssuance(tx, session.serverId, playerId, body.issuanceId)) !== undefined
    })

    // 3. sim is a network call, called BEFORE any transaction opens. Holding
    // a Postgres connection and an open transaction across it is how a 20ms
    // simulation exhausts the pool that solo_execution §5.7 names as the
    // real ceiling. The step-2 read above has already committed and
    // released its connection by this line.
    //
    // `null` is "not asked", not "no answer": there is nothing a
    // verification of these bytes could pay for, because the issuance they
    // name is not live. Every branch below that consumes a verdict is
    // therefore guarded on it, and the withIdempotency callback turns the
    // null into the same `issuance_invalid` its own step-2 check would have
    // produced had sim been called and come back verified.
    const verdict = issuanceIsLive ? await deps.simClient.simulate(body.replay) : null

    if (verdict?.kind === 'unavailable') {
      // An outage leaves the issuance untouched - design §8: no optimistic
      // grant, no clawback. A retryable 503 is honest only because nothing
      // was consumed.
      return fail('sim_unavailable', 'Verification is temporarily unavailable. Retry.')
    }

    if (verdict?.kind === 'rejected') {
      // engine_too_old is player-visible and does NOT consume the issuance
      // - design §2.3, the client is told to update and can retry the same
      // issuance once it has. Every other rejection reason IS a spent
      // attempt.
      if (verdict.reason === 'engine_too_old') {
        return fail('engine_too_old', 'This version of Broodline can no longer submit waves. Please update.')
      }
      return await consumeAndRefuse(deps, session, body.issuanceId)
    }

    const bundle = await loadBundle(deps.bundleStore)

    // Set inside withIdempotency's callback, ONLY when that callback
    // actually runs - i.e. on a fresh call, never on a replayed one (see
    // withIdempotency: a replay reads the stored response and never calls
    // fn again) - AND only once that call has VERIFIED the submission. That
    // makes it double as the write-replay gate below: a replayed response
    // leaves this undefined, which is correct - the object was already
    // written the first time.
    //
    // Assigned at the one point where all three halves of "verified" hold
    // (see its assignment below), NOT on entry to the callback. It was
    // assigned on entry before, which was harmless only because the write
    // also sat behind the non-refused branch; now that the write follows
    // verification rather than response status, this variable IS the
    // verification predicate and has to be set where that predicate
    // becomes true.
    let verifiedPlayerId: string | undefined

    let result: { body: SubmitOutcome }
    try {
      result = await withIdempotency(
        deps.db, session.serverId, key, hashRequest(body),
        async (tx): Promise<SubmitOutcome> => {
          // The step-2 read found the issuance dead, so sim was never
          // called. Same refusal the check below would have produced -
          // reached without a re-simulation, and still stored under this
          // key the way every other outcome of this callback is.
          if (verdict === null) return { refused: 'issuance_invalid' }

          const playerId = await loadPlayerId(tx, session.accountId)
          if (playerId === undefined) return { refused: 'issuance_invalid' }

          // 2, AUTHORITATIVE. The pre-sim read above is advisory and racy
          // by construction; this one runs inside the money transaction and
          // is the check the credit actually rests on. Keep both: deleting
          // this one would let an issuance settled between the two reads be
          // paid. Absent, expired or already consumed are one answer -
          // issuance_invalid - so this endpoint never tells a caller
          // anything about an issuance that is not live for them.
          const issuance = await loadLiveIssuance(tx, session.serverId, playerId, body.issuanceId)
          if (issuance === undefined) return { refused: 'issuance_invalid' }

          // 5. The issuance proves this player was given A wave; this
          // proves the replay is of THAT wave. Without it a player starts
          // wave 7, simulates wave 3 locally against an old seed, and
          // submits it against the wave 7 issuance.
          if (!matchesIssuance(verdict.echo, issuance)) {
            await settle(tx, issuance, 'consumed')
            return { refused: 'submission_rejected' }
          }

          // 6. One transaction: settle, advance, credit. settle() returns
          // whether THIS call performed the settlement - credit() is gated
          // on that, not merely on settle() not throwing, or two concurrent
          // submissions carrying different idempotency keys could both
          // observe "settled" and both pay.
          const settled = await settle(tx, issuance, 'consumed')
          if (!settled) return { refused: 'issuance_invalid' }

          // THE VERIFICATION POINT - design §5.2's amendment. All three
          // halves of "verified" now hold and none of them can be undone by
          // anything below: sim accepted the bytes (this is the `verified`
          // branch), they proved to be of the ISSUED wave (matchesIssuance,
          // above), and THIS call actually consumed the issuance (settle
          // returned true). Everything past this line decides what the
          // player is TOLD, not whether the attempt was real - so this is
          // where the replay becomes owed, and the write below is gated on
          // this variable rather than on the response status.
          verifiedPlayerId = playerId

          const result = verdict.outcome.result
          const integrityRemaining = toInt(verdict.outcome.integrityRemaining)
          const breaches = verdict.outcome.breaches

          if (result !== 'Win') return { paid: null, result, integrityRemaining, breaches }

          // The reward comes from the ISSUANCE's wave id - design §2.2 -
          // never from verdict.echo, which is the client's bytes echoed
          // back. A modified client can win a wave it would otherwise
          // lose; it cannot choose what that wave pays.
          //
          // Computed and checked BEFORE advanceCampaign, not after - review
          // finding: the bundle can be republished during the issuance's
          // two-hour TTL, dropping a wave's reward after wave/start's check
          // 3 already passed. Advancing campaign_progress and only THEN
          // refusing would commit the advance under an error response - the
          // player is told 409 while the server silently records the clear,
          // and their NEXT wave/start call would treat the wave as already
          // cleared even though they never received a success response for
          // it. The issuance still settles 'consumed' either way (above) -
          // they did win the wave; this is a spent attempt, same as a Loss.
          const reward = rewardForWave(bundle, issuance.waveId)
          if (reward === null) return { refused: 'wave_locked' }

          await advanceCampaign(tx, session.serverId, playerId, issuance.waveId)

          await credit(tx, {
            serverId: session.serverId, playerId,
            currency: reward.currency, delta: reward.amount,
            reasonCode: `wave:${issuance.waveId}`,
          })
          return { paid: { currency: reward.currency, amount: reward.amount }, result, integrityRemaining, breaches }
        })
    } catch (err) {
      if (err instanceof IdempotencyMismatchError) {
        return fail('idempotency_key_reused', 'This Idempotency-Key was used for a different request.')
      }
      throw err
    }

    // Written OUTSIDE the money transaction, on the VERIFIED path - which
    // is not the same set as the SUCCESSFUL path, and design §5.2's
    // amendment is the ruling that they differ. This sits ABOVE the refusal
    // branch below deliberately: a `wave_locked` submission is a verified,
    // settled Win that is nonetheless answered 409, and it is exactly the
    // refusal a player cannot appeal without the stored replay.
    //
    // WHAT IS AND IS NOT WRITTEN. verifiedPlayerId is defined here exactly
    // when this call's callback ran (never a replayed response - the object
    // was written the first time) AND reached the verification point: sim
    // accepted the bytes, they proved to be of the issued wave, and settle()
    // consumed the issuance. That covers 200 and `wave_locked` 409. It does
    // NOT cover the two refusals that also settle the issuance, and both
    // exclusions are load-bearing for §5.2's "storage is not an attacker's
    // write primitive": a sim REJECTION (consumeAndRefuse - sim never
    // verified anything) and a seed/wave MISMATCH (matchesIssuance failed -
    // sim verified some replay, but not one of the issued wave, so the
    // bytes are attacker-chosen). Both return before the assignment.
    // replays.test.ts pins the mismatch case.
    //
    // try/catch that logs and swallows, deliberately, and now for TWO
    // reasons. A GCS failure must not roll back a payment that already
    // committed inside withIdempotency - a missing replay is a degraded
    // viewer, not a ledger defect. And on the `wave_locked` path there is
    // no payment to protect but there is still a response: a storage error
    // must not turn that 409 into a 500, which is what an unhandled throw
    // here would do via app.ts's onError.
    if (verifiedPlayerId !== undefined) {
      try {
        await deps.replayStore.put(session.serverId, verifiedPlayerId, body.issuanceId, body.replay)
      } catch (err) {
        console.error('replay store write failed', err)
      }
    }

    // withIdempotency returns { status: 'fresh' | 'replayed', body }. BOTH
    // map to the same response here - that is the entire point of the
    // wrapper - so status is never branched on, only the refusal inside body.
    const outcome = result.body
    if ('refused' in outcome) {
      return fail(outcome.refused, refusalMessage(outcome.refused))
    }

    return c.json({
      result: outcome.result,
      integrityRemaining: outcome.integrityRemaining,
      breaches: outcome.breaches.map(toBreachDto),
      // Absent rather than null on a loss, so a client cannot render a zero.
      ...(outcome.paid === null ? {} : { reward: outcome.paid }),
    })
  })
}
