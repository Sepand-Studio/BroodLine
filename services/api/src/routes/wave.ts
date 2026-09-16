import { eq } from 'drizzle-orm'
import type { Hono } from 'hono'
import type { Deps } from '../app.ts'
import { loadBundle } from '../config/bundle.ts'
import { withServer, type Tx } from '../db/client.ts'
import { players } from '../db/schema.ts'
import { loadPlayerId, requireSession } from '../http/auth.ts'
import { fail } from '../http/errors.ts'
import { normalizeUuid } from '../http/ids.ts'
import { hashRequest } from '../http/hash.ts'
import type { SessionClaims } from '../identity/jwt.ts'
import { IdempotencyMismatchError, withIdempotency } from '../money/idempotency.ts'
import { credit } from '../money/ledger.ts'
import { grantWave6Pale } from '../ftue/pale.ts'
import { lockRoster, type CreatureDto } from '../roster/creatures.ts'
import { grantWaveBaseStock } from '../wave/base-stock.ts'
import { rewardForWave } from '../wave/rewards.ts'
import {
  advanceCampaign, type CreatureSpec, DEPLOYMENT_CAP, DEPLOYMENT_FLOOR,
  type DeployedCreature, type Issuance, issueWave, loadLiveIssuance, settle,
} from '../wave/issuance.ts'
import { type SimulateBreach, type SimulateEcho, toInt, toTier } from '../sim/client.ts'

interface StartBody { waveId: number; deployment: DeployedCreature[] }

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

/**
 * The parse layer's SANITY bound on a pocket, and it is only the two ENDS
 * of the range because only the two ends are content-independent.
 *
 * The floor is 0: `Deployments.Problem` refuses a negative pocket on every
 * lane that could ever exist. The ceiling is int32 because
 * engine/Runtime/Combat/Replay.cs writes the pocket as a 4-byte signed
 * integer, so a value outside that range cannot be expressed in a replay
 * under ANY terrain - malformed by construction, exactly like MAX_WAVE_ID.
 *
 * WHICH POCKETS A LANE ACTUALLY HAS IS CONTENT (Defile authors five) and
 * `sim` stays the only authority on it, the same way `issueWave` stays the
 * only authority on which waves exist. Transcribing Defile's five here would
 * put a second, unauthored copy of a geometry constant in the one file that
 * has no business owning it, and a new terrain would then be refused by the
 * HTTP layer before the engine ever saw it.
 */
const MAX_POCKET = 2_147_483_647

/**
 * design 6.1's deployment, reduced to the only two fields a client is
 * allowed to contribute.
 *
 * **THIS FUNCTION IS HALF THE MECHANISM.** It does not validate the entry and
 * pass it on - it CONSTRUCTS a new one out of exactly `creatureId` and
 * `pocket`, so every other field a body carries is dropped here and can never
 * reach `issueWave` to be read by accident. `resolveDeployment` is the other
 * half: what it stores comes off the owned row. Between them there is no path
 * from a client-supplied value to a stored spec.
 *
 * THE CAP IS HERE, not in `issueWave`, for MAX_WAVE_ID's reason: a deployment
 * longer than `Stats.DeploymentCap` is malformed by construction - no roster
 * state and no bundle could make it legal - and `invalid_request` (400) says
 * that, where every refusal `issueWave` can make says "understood and
 * refused" (409) instead. A client could not otherwise tell the two apart.
 *
 * DUPLICATE IDS ARE MALFORMED TOO, and refused here rather than being left to
 * fall out of the ownership check downstream as a short map. A body naming one
 * creature in two pockets asks for two creatures out of one; the player DOES
 * own it, so `creature_not_owned` would be a refusal that misstates its own
 * reason. Compared on the NORMALISED ids, because Postgres `uuid` equality is
 * case-insensitive and JS `===` is not - without that, `[id,
 * id.toUpperCase()]` reads as two distinct ids here and as one row in the
 * database, which is precisely the bug Task 7 fixed on /v1/splice/commit.
 *
 * AN EMPTY DEPLOYMENT IS NOT LEGAL, and a MISSING one is not either - two
 * different malformed bodies with one answer. An absent field is a body
 * written against the pre-Task-8 contract, and answering it 200 would put an
 * issuance in flight whose stored deployment nothing ever chose. An empty
 * ARRAY is a request to fight a wave with nothing, which no deployment screen
 * can express and which `wave/submit` would PAY for the moment content authors
 * a wave the Ark survives undefended - see DEPLOYMENT_FLOOR, which carries the
 * full argument. The floor was added in Task 10's fix round; until then this
 * function accepted `[]` on the grounds that the engine loses with it, which
 * was a fact about wave 6 wearing the costume of a guard.
 */
function parseDeployment(raw: unknown): DeployedCreature[] | null {
  if (!Array.isArray(raw)) return null
  if (raw.length < DEPLOYMENT_FLOOR || raw.length > DEPLOYMENT_CAP) return null

  const deployment: DeployedCreature[] = []
  const seen = new Set<string>()
  for (const entry of raw) {
    if (typeof entry !== 'object' || entry === null) return null
    const e = entry as Record<string, unknown>

    // `creatures.creature_id` is a Postgres `uuid` and `loadOwnedCreatures`
    // compares this value against it. Without the shape check a non-uuid
    // reaches that comparison, Postgres raises 22P02, and app.ts's onError
    // turns it into `internal` - telling an authenticated caller the server
    // broke for a body they malformed. See http/ids.ts; NORMALISED rather
    // than merely accepted, both for the duplicate check above and because
    // the id is what the row is looked up and keyed by.
    const creatureId = normalizeUuid(e.creatureId)
    if (creatureId === null) return null
    if (seen.has(creatureId)) return null
    seen.add(creatureId)

    if (typeof e.pocket !== 'number' || !Number.isInteger(e.pocket)) return null
    if (e.pocket < 0 || e.pocket > MAX_POCKET) return null

    deployment.push({ creatureId, pocket: e.pocket })
  }
  return deployment
}

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
  const deployment = parseDeployment(b.deployment)
  if (deployment === null) return null
  return { waveId: b.waveId, deployment }
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
  // NORMALISED, not merely accepted: this value is hashed into the
  // idempotency key (`hashRequest`), so an un-normalised id makes the same
  // retry with different casing a DIFFERENT request - 422 for something the
  // caller sent twice on purpose. See http/ids.ts.
  const issuanceId = normalizeUuid(b.issuanceId)
  if (issuanceId === null) return null
  if (typeof b.replay !== 'string' || b.replay.length === 0) return null
  return { issuanceId, replay: b.replay }
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

/**
 * THE THIRD FIELD OF THE SAME COMPARISON, and the line Phase 5's knowingly
 * open hole closes on - design §6.2.
 *
 * `matchesIssuance` above proves the replay is of the issued WAVE, at the
 * issued SEED. This proves it is of the issued DEPLOYMENT: `api` resolved
 * those specs from rows the player owns at issuance (design §6.1), `sim`
 * reports what the submitted bytes actually claimed, and a disagreement is a
 * breach taken on the path that already existed for the other two.
 *
 * NOT A NEW KIND OF CHECK, and deliberately not a roster lookup. Nothing here
 * reads `creatures`; the entitlement question was answered at issuance and
 * this only asks whether the submission is of THAT issuance. A version of this
 * that went back to the roster would be the shape design §2.1 rejects - a
 * shape match, under which two identical creatures are indistinguishable and a
 * player who once owned a matching creature can deploy its ghost forever.
 *
 * LENGTH FIRST, AND THAT ORDER IS LOAD-BEARING. Written as a loop over
 * `stored`, this function runs ZERO ITERATIONS against an empty stored
 * deployment and returns true for every echo there is - which is exactly the
 * vacuous pass `claimIssuance`'s old `deployment: CreatureSpec[] = []`
 * default made reachable (that default is gone as of this task, and the length
 * check is what makes its return harmless rather than fatal). It also makes
 * the function safe against an echo that is not an array at all: `sim`'s
 * response is an unchecked cast in sim/client.ts, and `Array.isArray` costs
 * nothing.
 *
 * A NULL STORED DEPLOYMENT IS A MISMATCH, NOT A SKIP, and this is a ruling
 * rather than a fallback. `wave_issuances.deployment` is nullable because
 * drizzle/0006 is the EXPAND step (it ships with no reader, so a rollback is
 * possible), so for up to ISSUANCE_TTL_MS after this handler deploys a player
 * can hold a live issuance minted by a build that never populated the column.
 * The alternative - "no stored deployment, so skip the check" - reopens the
 * exact hole this function exists to close, for every player, for two hours,
 * on every deploy that crosses this boundary. It is also the kind of hole that
 * does not announce itself: the suite would be green throughout.
 *
 * What refusing costs instead is bounded and visible: an honest player who
 * started a wave just before the deploy loses that ONE attempt (the issuance
 * settles 'consumed', the same as any other breach) and starts another. Worth
 * saying plainly - that is a real cost paid by innocent players, and it is
 * chosen because a two-hour window in which anyone can deploy creatures they
 * do not own is the larger loss. The window closes by itself and cannot
 * recur: every issuance this build mints carries a deployment, so the null
 * branch is unreachable for anything issued after the deploy, and the
 * contract migration that makes the column NOT NULL removes it for good.
 */
export function deploymentMatches(
  echo: SimulateEcho['deployment'], stored: CreatureSpec[] | null,
): boolean {
  if (stored === null) return false
  if (!Array.isArray(echo) || echo.length !== stored.length) return false

  // INDEXED, NOT SET-COMPARED. The engine indexes its parallel arrays by
  // deployment order (SimState: "Index == deployment order") and
  // `resolveDeployment` iterates the REQUEST's order for that reason, so the
  // order the client sent is part of what it asked for. Two deployments that
  // agree as multisets and differ in order are different deployments, and
  // `rejects a submission that deploys the SAME creatures in a different
  // order` is what stops this being rewritten as a sort-and-compare.
  return stored.every((s, i) => {
    const e = echo[i]
    if (e === undefined) return false
    return e.species === s.species
      && e.trait1 === s.trait1 && toTier(e.tier1) === s.tier1
      && e.trait2 === s.trait2 && toTier(e.tier2) === s.tier2
      && e.instinct === s.instinct
      // `pocket` is `number | string` for the generated contract's usual
      // reason (sim/client.ts's toInt doc) and `CreatureSpec.pocket` is a
      // genuine number, so a bare `!==` would type-check while being able to
      // disagree at runtime the moment the string branch is hit.
      && toInt(e.pocket) === s.pocket
  })
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


type SubmitRefusal =
  'issuance_invalid' | 'submission_rejected' | 'deployment_mismatch' | 'wave_locked'

function refusalMessage(code: SubmitRefusal): string {
  if (code === 'issuance_invalid') return 'That issuance is not live for this player.'
  if (code === 'submission_rejected') return 'That submission was rejected.'
  // NAMES THE STATE THE CALLER CAN ACT ON, without naming a creature. The
  // deployment screen's next action is to re-read the roster and start again,
  // which is a different sentence from submission_rejected's - which is the
  // whole reason this is a separate code (solo_execution 6.2). It says nothing
  // about WHICH spec disagreed: that is the same discipline wave/start's two
  // roster refusals keep, and telling a modified client which of its seven
  // fields was caught is free information it has no use for honestly.
  if (code === 'deployment_mismatch') {
    return 'That replay was not of the deployment this wave was issued for.'
  }
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
  | { refused: SubmitRefusal }
  | {
    paid: null; result: string; integrityRemaining: number; breaches: SimulateBreach[]
    granted: CreatureDto[]
  }
  | {
    paid: { currency: string; amount: number }; result: string; integrityRemaining: number
    breaches: SimulateBreach[]; granted: CreatureDto[]
  }

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
      return fail('invalid_request',
        `waveId must be an integer between 1 and ${MAX_WAVE_ID}, and deployment must be ` +
        `${DEPLOYMENT_FLOOR} to ${DEPLOYMENT_CAP} entries of { creatureId, pocket } ` +
        `naming distinct creatures.`)
    }

    const bundle = await loadBundle(deps.bundleStore)

    const result = await withServer(deps.db, session.serverId, async (tx) => {
      const [player] = await tx.select().from(players)
        .where(eq(players.accountId, session.accountId))
      if (player === undefined) return null

      // THE PLAYER-LEVEL LOCK, BEFORE `issueWave` - fix round 1's finding,
      // reproduced as a real Postgres `40P01` rather than reasoned about
      // (see sweep.test.ts's "wave/start's two-statement creature lock"
      // tests and task-11-report.md).
      //
      // Task 11 gave `issueWave` a SECOND creature-row-locking statement:
      // `settleExpiredForPlayer` (sorted) now runs before check 1, and
      // `resolveDeployment` -> `loadOwnedCreatures` (sorted) still runs
      // near the end. Each statement is internally sorted, but the
      // TRANSACTION's combined lock order across the two is not - the
      // first set is fixed by whatever was already committed to a stale
      // issuance, the second by the request, and nothing relates the two.
      // A `splice/commit.ts` transaction naming one creature from each set
      // locks them in the OPPOSITE relative order (its own single sorted
      // statement), and the two can deadlock purely on row locks - no
      // advisory lock on either side, which is what makes this a genuinely
      // different case from the splice/commit-vs-wave/submit deadlock a
      // task ago.
      //
      // THE RULE THIS GENERALISES, now paid for twice on this branch: a
      // transaction that locks a player's creature rows in MORE THAN ONE
      // STATEMENT must take `lockRoster` first, exactly as `wave/submit`
      // (routes/wave.ts, below), `splice/commit.ts`'s `commitSplice`,
      // `map/claim.ts`'s `claimNode` and `wave/base-stock.ts`'s
      // `grantWaveBaseStock` already do. A transaction that locks them in
      // exactly ONE sorted statement (routes/creature.ts's single-row
      // lock, `consumeAndRefuse`'s single `releaseCreatures` call above)
      // needs no advisory lock at all - there is only one statement to
      // order against itself, and it already is.
      await lockRoster(tx, session.serverId, player.playerId)

      return issueWave(
        tx, session.serverId, player.playerId, body.waveId, body.deployment, bundle)
    })

    if (result === null) return fail('not_found', 'No player on this server for that account.')

    if ('refused' in result) {
      if (result.refused === 'wave_locked') {
        return fail('wave_locked', 'That wave is not available to you right now.')
      }
      // design 6.1's two roster refusals. Both 409 and both distinct codes,
      // because the deployment screen needs a different sentence and a
      // different next action for each - solo_execution 6.2's rule that a
      // client switches on `code`. Neither says WHICH creature: a refusal
      // naming one would tell a caller something about a row that may not be
      // theirs, which is the discipline loadLiveIssuance already keeps.
      if (result.refused === 'creature_not_owned') {
        return fail('creature_not_owned',
          'One of those creatures is not on your roster any more. Refresh and try again.')
      }
      if (result.refused === 'creature_committed') {
        return fail('creature_committed',
          'One of those creatures is already out fighting. Finish or abandon that wave first.')
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
      // What was ACTUALLY issued, which is not always what this request
      // asked for: design 2.1 returns a live issuance rather than replacing
      // it, so a second start carrying a different deployment gets the first
      // one's back. Echoing it is what lets a client show the player which
      // creatures are committed without guessing, and it is null only for an
      // issuance minted before this column had a writer - see
      // drizzle/0006_issuance_deployment.sql.
      deployment: result.deployment,
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

          // THE PLAYER-LEVEL LOCK, FIRST IN THIS TRANSACTION - fix round 2's
          // finding, and the reason round 1's own fix (splice/commit.ts
          // taking this SAME advisory lock before its row locks) introduced
          // a Critical deadlock rather than closing one. `settle()` below
          // (every branch - `submission_rejected`, `deployment_mismatch` and
          // the win path) calls `releaseCreatures`, which takes ROW locks
          // (`SELECT ... FOR UPDATE`) on every creature this issuance
          // committed - and only much later, on a verified Win,
          // `grantWaveBaseStock` takes this advisory lock. Row locks then
          // advisory is the OPPOSITE order `commitSplice` now takes them in,
          // so a player who deploys a creature to a wave and then splices it
          // as a parent while racing their own `wave/submit` could put one
          // transaction holding the row lock and wanting the advisory lock
          // while the other holds the advisory lock and wants the row lock -
          // Postgres aborts one with `40P01`.
          //
          // Taking it HERE, before `loadLiveIssuance` and before EVERY
          // `settle()` call site in this callback (not only the win branch -
          // `releaseCreatures` runs on all three), makes the invariant true
          // for the WHOLE transaction rather than function-by-function:
          // wave-submit now takes the same order `commitSplice`,
          // `grantWaveBaseStock` and `claimNode` already agree on - the
          // advisory lock, then any row lock. `grantWaveBaseStock`'s own
          // `lockRoster` call further down is now a redundant re-acquisition
          // of a lock this transaction already holds (`pg_advisory_xact_lock`
          // is re-entrant within one transaction) - see that function's own
          // doc for why it stays rather than being trimmed.
          await lockRoster(tx, session.serverId, playerId)

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

          // 5, THE THIRD FIELD - design §6.2, and the line Phase 5's
          // knowingly-open hole closes on. The two checks above prove this
          // replay is of the issued WAVE at the issued SEED; this proves it
          // is of the issued DEPLOYMENT. Same breach, same settlement, a
          // distinct code because the deployment screen needs a different
          // sentence from "that replay is not of this wave"
          // (solo_execution §6.2).
          //
          // BEFORE settle() and before the credit, like its two neighbours:
          // a submission that fails it is a spent attempt that pays nothing,
          // and `verifiedPlayerId` below is never reached - so a mismatched
          // replay is not stored either. That exclusion is design §5.2's
          // "storage is not an attacker's write primitive", and it is the
          // same reason the seed mismatch returns here rather than falling
          // through.
          if (!deploymentMatches(verdict.echo.deployment, issuance.deployment)) {
            await settle(tx, issuance, 'consumed')
            return { refused: 'deployment_mismatch' }
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

          if (result !== 'Win') {
            // waves_01_12 wave 6: a designed loss (a lone, unanswerable
            // Courser) whose OWN Wave Defeat screen hands over a Pale
            // carrying Chill as a Warden resupply - the only path to Chill
            // a player who lost wave 6 has, now that base stock withholds
            // Pale until this fires (roster/creatures.ts's `baseStockPool`).
            // Once per player, gated on `grantWave6Pale`'s own marker, not
            // on `waveId === 6` alone - a second loss on wave 6 grants
            // nothing further.
            const granted: CreatureDto[] = []
            if (issuance.waveId === 6) {
              const pale = await grantWave6Pale(tx, session.serverId, playerId)
              if (pale !== null) granted.push(pale)
            }
            return { paid: null, result, integrityRemaining, breaches, granted }
          }

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

          // Design §2.4, and it is BESIDE the credit rather than after the
          // transaction on purpose. Phase 4's rule is that every currency
          // mutation writes its ledger row in the same transaction as the
          // balance update; the creature grant joins that transaction for
          // the same reason - a player who was paid but not granted has lost
          // a creature to a crash and cannot tell, and a player granted but
          // not paid is a supply line that runs without the wave being won.
          //
          // ON THE WIN BRANCH ONLY. `base_stock` §3 sources this from wave
          // COMPLETION; a Loss returns above, before the reward lookup, and
          // grants nothing here (wave 6's Loss branch grants its OWN Pale,
          // above). Skipped rather than refused at the Hatchery cap - see
          // grantWaveBaseStock, which is also where the "no multiplier
          // argument" guardrail lives.
          const granted = await grantWaveBaseStock(tx, session.serverId, playerId, issuance.issuanceId)

          // THE WAVE-6 PALE, AGAIN - waves_01_12's "if they somehow win, the
          // Pale grant fires anyway", so the beat degrades rather than
          // breaks on a survived Courser. AFTER `grantWaveBaseStock`, not
          // before: that call reads `baseStockPool(markers)` off
          // `wave6PaleGrantedAt`, and this grant is what flips that marker -
          // running it first would un-withhold Pale from THIS SAME roll,
          // handing a player who merely won wave 6 two chances at Chill
          // instead of one guaranteed one.
          if (issuance.waveId === 6) {
            const pale = await grantWave6Pale(tx, session.serverId, playerId)
            if (pale !== null) granted.push(pale)
          }

          return { paid: { currency: reward.currency, amount: reward.amount }, result, integrityRemaining, breaches, granted }
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
    // NOT cover the THREE refusals that also settle the issuance, and every
    // exclusion is load-bearing for §5.2's "storage is not an attacker's
    // write primitive": a sim REJECTION (consumeAndRefuse - sim never
    // verified anything), a seed/wave MISMATCH (matchesIssuance failed - sim
    // verified some replay, but not one of the issued wave), and a
    // DEPLOYMENT MISMATCH (deploymentMatches failed - sim verified a replay
    // of the issued wave, fought by creatures this issuance did not commit).
    // In all three the bytes are attacker-chosen, and all three return
    // before the assignment. replays.test.ts pins the seed case.
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
      // Absent rather than `[]` when this settlement minted nothing - the
      // Phase 6 client parses WaveSubmitResponse without this field at all,
      // and an empty array would be a new, always-present shape to ignore
      // rather than a genuinely optional one.
      ...(outcome.granted.length ? { granted: outcome.granted } : {}),
    })
  })
}
