import { eq } from 'drizzle-orm'
import type { Hono } from 'hono'
import type { Deps } from '../app.ts'
import { loadBundle } from '../config/bundle.ts'
import { withServer, type Tx } from '../db/client.ts'
import { players } from '../db/schema.ts'
import { requireSession } from '../http/auth.ts'
import { fail } from '../http/errors.ts'
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

function parseStart(raw: unknown): StartBody | null {
  if (typeof raw !== 'object' || raw === null) return null
  const b = raw as Record<string, unknown>
  if (typeof b.waveId !== 'number' || !Number.isInteger(b.waveId)) return null
  return { waveId: b.waveId }
}

interface SubmitBody { issuanceId: string; replay: string }

function parseSubmit(raw: unknown): SubmitBody | null {
  if (typeof raw !== 'object' || raw === null) return null
  const b = raw as Record<string, unknown>
  if (typeof b.issuanceId !== 'string' || b.issuanceId.length === 0) return null
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
    if (body === null) return fail('invalid_request', 'waveId is required.')

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
    if (body === null) return fail('invalid_request', 'issuanceId and replay are required.')

    // sim is a network call, called BEFORE any transaction opens. Holding a
    // Postgres connection and an open transaction across it is how a 20ms
    // simulation exhausts the pool that solo_execution §5.7 names as the
    // real ceiling.
    const verdict = await deps.simClient.simulate(body.replay)

    if (verdict.kind === 'unavailable') {
      // An outage leaves the issuance untouched - design §8: no optimistic
      // grant, no clawback. A retryable 503 is honest only because nothing
      // was consumed.
      return fail('sim_unavailable', 'Verification is temporarily unavailable. Retry.')
    }

    if (verdict.kind === 'rejected') {
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
    // fn again). That makes it double as the write-replay gate below: a
    // replayed response leaves this undefined, which is correct - the
    // object was already written the first time.
    let playerIdForReplay: string | undefined

    let result: { body: SubmitOutcome }
    try {
      result = await withIdempotency(
        deps.db, session.serverId, key, hashRequest(body),
        async (tx): Promise<SubmitOutcome> => {
          const playerId = await loadPlayerId(tx, session.accountId)
          if (playerId === undefined) return { refused: 'issuance_invalid' }
          playerIdForReplay = playerId

          // 2. Absent, expired or already consumed are one answer -
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

    // withIdempotency returns { status: 'fresh' | 'replayed', body }. BOTH
    // map to the same response here - that is the entire point of the
    // wrapper - so status is never branched on, only the refusal inside body.
    const outcome = result.body
    if ('refused' in outcome) {
      return fail(outcome.refused, refusalMessage(outcome.refused))
    }

    // Written OUTSIDE the money transaction and only on this, the verified
    // path - design 5.2. playerIdForReplay is defined here exactly when
    // this call's withIdempotency callback ran AND produced a non-refused
    // outcome, which is "verified" in the sense that matters: sim accepted
    // the bytes, they proved to be of the issued wave, and settle()
    // actually consumed the issuance. A rejected submission returns above
    // and never reaches this line, so it leaves no object.
    //
    // try/catch that logs and swallows, deliberately: a GCS failure here
    // must not roll back a payment that already committed inside
    // withIdempotency. A missing replay is a degraded viewer, not a ledger
    // defect - it must never become one by being folded into the
    // transaction above.
    if (playerIdForReplay !== undefined) {
      try {
        await deps.replayStore.put(session.serverId, playerIdForReplay, body.issuanceId, body.replay)
      } catch (err) {
        console.error('replay store write failed', err)
      }
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
