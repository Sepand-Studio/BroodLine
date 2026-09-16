import { randomUUID } from 'node:crypto'
import { and, eq, inArray, isNull, sql } from 'drizzle-orm'
import type { Bundle } from '../config/bundle.ts'
import type { Tx } from '../db/client.ts'
import { campaignProgress, creatures, waveIssuances, type CreatureSpec } from '../db/schema.ts'
import { liveCreature, loadOwnedCreatures } from '../roster/creatures.ts'

export const ISSUANCE_TTL_MS = 7_200_000 // two hours - design 4.1
export const REPLAY_CAP_PER_DAY = 3 // broodline_campaign_structure.md

/**
 * engine `Stats.DeploymentCap` (engine/Runtime/Combat/Stats.cs), mirrored -
 * a transcription owning no decision, the same way `creatureHp` mirrors
 * `Stats.CreatureHp`.
 *
 * ENFORCED AT THE PARSE LAYER, not here: routes/wave.ts's `parseStart` holds
 * it exactly as it holds MAX_WAVE_ID, because a deployment of six is
 * malformed by construction - no roster state and no bundle could make it
 * legal - and `invalid_request` (400) says that, where every refusal this
 * module can make says "understood and refused" instead. It lives beside the
 * rest of the issuance rules so there is one place to read what an issuance
 * may contain.
 *
 * NOT A CONSTRAINT IN SQL either, for engine/Runtime/Combat/Replay.cs's own
 * stated reason about the same number: it is a BALANCE constant, and freezing
 * it into a migration means a tuning change needs one.
 */
export const DEPLOYMENT_CAP = 5

/**
 * The other end of the same bound, and unlike the cap it mirrors NOTHING in
 * the engine - `Deployments.Problem` is perfectly happy to simulate zero
 * creatures. It is an `api` rule, and it exists because `wave/submit` pays.
 *
 * AN EMPTY DEPLOYMENT USED TO BE LEGAL, and the argument for it was that zero
 * creatures is a deployment the engine simulates fine and loses. The second
 * half of that sentence is the problem: **it is engine content, not a guard.**
 * Wave 6's lone Courser reaches the Ark unaided, so an empty submission loses
 * today and earns nothing - but `deploymentMatches` agrees that `[]` echoes
 * `[]`, so the moment content authors one wave the Ark survives undefended, an
 * empty issuance wins it, and `routes/wave.ts` pays the reward AND grants base
 * stock, three times a day, for deploying nothing. The entire suite would be
 * green: nothing anywhere pinned it, because the thing standing in the way was
 * a raider's movement speed.
 *
 * Phase 7 authors more waves. A pay-for-nothing path one content change away
 * from live, on the exact route this phase exists to protect, is not a risk
 * worth carrying for a body shape no honest client sends - the deployment
 * screen cannot even express it.
 *
 * AT THE PARSE LAYER, beside the cap, for the cap's own reason: a deployment
 * of zero is malformed by construction - no roster state and no bundle could
 * make it legal - and `invalid_request` (400) says exactly that, where every
 * refusal `issueWave` can make says "understood and refused" (409) instead.
 *
 * `issueWave` ITSELF STILL ACCEPTS `[]`, deliberately and like every other
 * parse-layer bound in this file's neighbour: the route can simply no longer
 * hand it one. Tests that drive `issueWave` directly to reach a check that
 * runs BEFORE the roster (the replay cap) keep passing `[]` and keep meaning
 * what they meant.
 */
export const DEPLOYMENT_FLOOR = 1

/** design 6.1's request shape: an id and a pocket, and nothing else. */
export interface DeployedCreature {
  creatureId: string
  pocket: number
}

/**
 * TEST-ONLY seam, the same idiom splice/commit.ts's `SpliceHooks` and
 * map/claim.ts's `ClaimHooks` already use for the same class of problem.
 *
 * A no-op for every real caller - the parameter defaults to `{}` and nothing
 * under `src/routes/` passes it.
 */
export interface IssuanceHooks {
  /**
   * Awaited between the deployment being resolved (and its creatures locked)
   * and the issuance being claimed - THE ONLY WINDOW in which `claimIssuance`
   * can lose its insert, and therefore the only place a test can stand to
   * show that the `claimed` gate below matters.
   *
   * Without it the losing branch is unreachable from a test: a second
   * sequential `issueWave` finds the live row at check 4 and returns it long
   * before it could ever reach the insert, and two HTTP requests essentially
   * never interleave between two back-to-back statements on their own - the
   * finding splice-commit.test.ts's race test was rewritten for.
   */
  beforeClaim?: () => Promise<void>
}

// Re-exported so the wave path has ONE import for what an issuance carries.
// It is declared in db/schema.ts beside the column that holds it, because
// that file cannot import from this one without a cycle.
export type { CreatureSpec }

export type Issuance = typeof waveIssuances.$inferSelect

export type IssuanceRefusal =
  | { refused: 'wave_locked' }
  | { refused: 'replay_cap_reached' }
  | { refused: 'creature_not_owned' }
  | { refused: 'creature_committed' }

/**
 * Five checks, design §4.1's order, all inside the caller's withServer()
 * transaction.
 *
 * DEVIATION FROM THE BRIEF, reported per this run's standing instruction to
 * fix a snippet that does not match reality rather than force it: the
 * brief's check 1 was `waveId > cleared + 1`, i.e. "the next wave is
 * whatever integer follows the last cleared one". That is only true if wave
 * ids are dense and 1-indexed. They are not - the only wave this bundle (or
 * any bundle to date) authors carries id 6 (engine/Runtime/Combat/WaveDef.cs
 * Wave6(), config/bundles/<version>/waves.json), so for a fresh player
 * (highestWaveCleared 0) the brief's formula computes "next" as 1 and
 * refuses `start(6)` - which contradicts the brief's own first test,
 * "issues a seed for the next uncleared wave", calling `start(6)` on a
 * fresh player and expecting 200.
 *
 * The fix: "the next wave" is the smallest AUTHORED wave id greater than
 * `cleared`, not `cleared + 1` taken literally. This also folds check 3
 * ("the wave is authored") into the forward-progress branch for free - an
 * unauthored id can never be anyone's "next" wave - while check 3 still
 * runs explicitly on the replay branch, where an id can be `<= cleared`
 * without currently being in the bundle (a later bundle could in principle
 * retire a wave).
 */
export async function issueWave(
  tx: Tx, serverId: number, playerId: string, waveId: number,
  deployment: readonly DeployedCreature[], bundle: Bundle,
  hooks: IssuanceHooks = {},
): Promise<Issuance | IssuanceRefusal> {
  const [progress] = await tx.select().from(campaignProgress)
    .where(and(eq(campaignProgress.serverId, serverId), eq(campaignProgress.playerId, playerId)))
  const cleared = progress?.highestWaveCleared ?? 0

  if (waveId < 1) return { refused: 'wave_locked' }

  if (waveId <= cleared) {
    // 1 (replay branch). Already cleared - allowed, subject to check 2.

    // 2. The replay cap, counted off CONSUMED issuances since the day
    //    boundary. Not a second counter: one source of truth, and design 4.3
    //    retains consumed rows 48 hours precisely so this query can see them.
    // count(*) over a WHERE with no GROUP BY always returns exactly one row
    // (possibly used: 0) - never zero rows - so the non-null assertion is
    // not a guess.
    const [row] = await tx.select({ used: sql<number>`count(*)::int` })
      .from(waveIssuances)
      .where(and(
        eq(waveIssuances.serverId, serverId),
        eq(waveIssuances.playerId, playerId),
        eq(waveIssuances.waveId, waveId),
        eq(waveIssuances.settlement, 'consumed'),
        // AT TIME ZONE 'UTC' TWICE, and BOTH are load-bearing. The inner one
        // takes the truncation into UTC; the outer one takes the result BACK
        // to timestamptz, so the comparison against this timestamptz column
        // is between two absolute instants.
        //
        // The outer conversion was missing, and its absence was a real bug
        // rather than a tidiness issue - found when
        // test/adversarial.test.ts's day-boundary gate was rewritten to pin
        // the boundary. WITHOUT it, date_trunc(...) returns `timestamp`
        // WITHOUT time zone, and comparing that against a timestamptz column
        // silently re-converts it through the SESSION's TimeZone GUC -
        // reintroducing the exact dependence the inner conversion exists to
        // remove. Verified against a real postgres:16-alpine under
        // `SET TimeZone='America/New_York'`:
        //
        //   without the outer cast -> 2026-09-14 00:00:00     (a bare
        //     timestamp, which the comparison then reads as midnight NEW
        //     YORK, i.e. 04:00 UTC)
        //   with it                -> 2026-09-13 20:00:00-04  (= midnight UTC)
        //
        // i.e. every player's replay cap would reset on the session's local
        // midnight - four hours late in that zone. Correct in production only
        // because the GUC happens to default to UTC on both
        // postgres:16-alpine and Cloud SQL, which is precisely the dependence
        // this line claimed to have removed and had not.
        // test/adversarial.test.ts's "counts against midnight UTC even when
        // the session TimeZone is not UTC" runs this exact query under a
        // deliberately non-UTC session and fails if the outer cast is
        // dropped again.
        sql`${waveIssuances.issuedAt} >= date_trunc('day', now() AT TIME ZONE 'UTC') AT TIME ZONE 'UTC'`))
    if (row!.used >= REPLAY_CAP_PER_DAY) return { refused: 'replay_cap_reached' }

    // 3. Still has to be authored - a wave a later bundle stopped carrying
    //    is not something a stale "already cleared" record can revive.
    if (!bundle.waves.some((w) => w.id === waveId)) return { refused: 'wave_locked' }
  } else {
    // 1 (forward branch) + 3, folded: "next" is the smallest authored wave
    // id past what has been cleared - see the deviation note above.
    const nextWaveId = bundle.waves
      .map((w) => w.id)
      .filter((id) => id > cleared)
      .sort((a, b) => a - b)[0]
    if (nextWaveId === undefined || waveId !== nextWaveId) return { refused: 'wave_locked' }
  }

  // 4. A live issuance is RETURNED, not replaced - design 2.1.
  //
  // Two cases, and the second is the one the first version of this design got
  // wrong. A row with settled_at IS NULL is in the one-live index whether or
  // not it has expired, so an UNEXPIRED one is returned as-is, and an EXPIRED
  // one - the abandoned-wave path - must be SETTLED 'expired' before the
  // insert below, or that insert collides and the player cannot start a wave
  // at all. Settling it 'consumed' instead would charge them a replay they
  // never took, which is why the settlement is an enum.
  const [live] = await tx.select().from(waveIssuances)
    .where(and(
      eq(waveIssuances.serverId, serverId),
      eq(waveIssuances.playerId, playerId),
      isNull(waveIssuances.settledAt)))
  if (live !== undefined) {
    if (live.expiresAt > new Date()) return live
    await settle(tx, live, 'expired')
  }

  // 5. The seed comes from the CSPRNG, and the row is written before the
  //    response is formed. randomUUID is crypto-backed; the seed is drawn
  //    the same way rather than from Math.random, which is neither seeded
  //    nor unpredictable and would make the seed guessable in advance.
  //
  //    MASKED TO 63 BITS. Postgres bigint is signed and the column carries
  //    CHECK (seed >= 0); a full 64-bit draw lands at or above 2^63 half the
  //    time and either errors on insert or, if someone casts around it,
  //    sign-flips into a DIFFERENT wave - which presents as a hash mismatch
  //    on an honest submission. One bit is nothing to a PRNG stream selector.
  const seed = crypto.getRandomValues(new BigUint64Array(1))[0]! & 0x7fff_ffff_ffff_ffffn

  // design 6.1's steps 2, 3 and 4, and the mechanism the whole phase turns
  // on. AFTER check 4, deliberately: the deployment is fixed AT ISSUANCE, so
  // a player whose live issuance already holds one gets that one back (design
  // 2.1) rather than being refused `creature_committed` by their own wave.
  // Running these checks in front of check 4 makes the second call of every
  // honest double-tap a 409.
  const resolved = await resolveDeployment(tx, serverId, playerId, deployment)
  if ('refused' in resolved) return resolved

  // The read-to-insert window, and the only place a test can stand to see
  // whether the gate below is real. A no-op for every real caller.
  if (hooks.beforeClaim) await hooks.beforeClaim()

  // 5 and 6, in that order and in this transaction. The insert first,
  // because the commitment has to name a row that exists.
  const claim = await claimIssuance(tx, serverId, playerId, waveId, seed, resolved.specs)
  // ONLY WHEN THIS CALL ACTUALLY INSERTED. On the lost-race path
  // `claimIssuance` hands back the WINNER's row, whose deployment was
  // resolved from the winner's own creatures - committing ours to it would
  // garrison creatures that issuance never deployed. The two racers only get
  // here at all with DISJOINT deployments (overlapping ones serialize on the
  // FOR UPDATE in `loadOwnedCreatures` and the loser refuses
  // `creature_committed`), which is precisely the case where the winner's
  // deployment says nothing about ours.
  if (claim.claimed) {
    await commitCreatures(
      tx, serverId, playerId, deployment.map((d) => d.creatureId), claim.issuance.issuanceId)
  }
  return claim.issuance
}

/**
 * design 6.1 steps 2-4: the ids the request named, resolved to specs read off
 * the rows that own them.
 *
 * **STEP 4 IS THE WHOLE MECHANISM.** Every field of every spec below comes
 * from `owned`, which is a row this player holds; the only thing the request
 * contributes is WHICH row and WHICH pocket. There is therefore no path from
 * a client-supplied value to a `CreatureSpec`, and a deployment the player
 * does not own is not refused - it is INEXPRESSIBLE. If this function ever
 * reads a trait, a tier, a species or an instinct out of `deployment`, the
 * design has been inverted and the two refusals below have become the
 * guarantee instead of the cheap path.
 *
 * `test/wave-start.test.ts`'s `resolves every spec from the OWNED ROW, not
 * from the request` sends trait fields in the body and asserts the stored
 * spec ignored them. That is the test that pins the mechanism; the refusal
 * tests pin only the check.
 *
 * ITERATED IN REQUEST ORDER, over `deployment` rather than over `owned`. The
 * engine indexes its parallel arrays by deployment order (SimState: "Index ==
 * deployment order"), so the order the client sent is part of what it asked
 * for, and `loadOwnedCreatures` returns a Map precisely so this loop cannot
 * be written the other way round by accident.
 *
 * REFUSES THE WHOLE DEPLOYMENT OR NONE OF IT. A partial issuance - four of
 * the five creatures a player chose - is a wave they did not ask to fight,
 * and design 4.3 makes the same ruling about a partial base-stock grant for
 * the same reason.
 */
async function resolveDeployment(
  tx: Tx, serverId: number, playerId: string, deployment: readonly DeployedCreature[],
): Promise<{ specs: CreatureSpec[] } | IssuanceRefusal> {
  const owned = await loadOwnedCreatures(tx, serverId, playerId, deployment.map((d) => d.creatureId))

  // 2. `deployment` carries no duplicate ids (routes/wave.ts's parseStart
  //    refuses those as malformed), so a short map means at least one id was
  //    not a live creature of this player's. Not owned, not this player's,
  //    pruned and consumed are ONE answer - the same discipline
  //    `loadLiveIssuance` applies below, so this route tells a caller nothing
  //    about rows that are not theirs.
  if (owned.size !== deployment.length) return { refused: 'creature_not_owned' }

  const specs: CreatureSpec[] = []
  for (const d of deployment) {
    // Present by the size check above; `!` rather than a second guard so
    // that a future edit which makes it absent fails loudly here rather than
    // silently storing an `undefined` spec.
    const c = owned.get(d.creatureId)!

    // 3. Already out fighting for another issuance. Checked per creature
    //    rather than as a bulk predicate so the refusal is the specific one
    //    the Splice Chamber screen and the deployment screen both need -
    //    solo_execution 6.2's rule that a client switches on `code`.
    if (c.committedTo !== null) return { refused: 'creature_committed' }

    // 4. THE SPEC, FROM THE ROW. `trait_N` and `instinct` are NOT NULL on a
    //    live creature (0005's live_creatures_are_whole), and `liveCreature()`
    //    is what guarantees this row is one - the non-null assertions are that
    //    constraint restated, not a guess. `tier_N` really is nullable: null
    //    is an Aberrant, which has no coverage, and flattening it to 0 here
    //    would conflate the two states data_model 2 exists to keep apart.
    specs.push({
      species: c.species,
      trait1: c.trait1!, tier1: c.tier1,
      trait2: c.trait2!, tier2: c.tier2,
      instinct: c.instinct!,
      // The ONE field the request supplies, and the only one it may. Which
      // pockets a lane actually has is content, and `sim` stays the only
      // authority on it - routes/wave.ts bounds this to a non-negative int32
      // and no further.
      pocket: d.pocket,
    })
  }

  return { specs }
}

/**
 * design 6.1 step 6: every deployed creature is marked as out fighting for
 * this issuance.
 *
 * `committed_to` HAS NEVER HAD A WRITER UNTIL NOW - data_model 2 specified
 * it and nothing could set it, because there were no creatures. Its reader
 * already exists: splice/commit.ts refuses a committed parent, and design
 * 2.5 makes that the thing that stops a player splicing away a creature that
 * is currently out fighting. The two halves have to agree, or the deployment
 * stored on a live issuance outlives the roster rows it was resolved from.
 *
 * No lock ordering to argue about: every row named here is already held
 * FOR UPDATE by `loadOwnedCreatures`, taken in ascending id order.
 *
 * THE WHERE SAYS THE WHOLE PREDICATE, AND NOT BECAUSE THE LOCK IS IN DOUBT.
 * This statement shipped filtering on `server_id` and `creature_id` alone,
 * which is correct today for one reason and one only: `loadOwnedCreatures`
 * took `FOR UPDATE` on exactly these rows, three statements up, having already
 * checked ownership and liveness, and `resolveDeployment` refused every row
 * whose `committed_to` was not null. Every one of those facts lives in a
 * DIFFERENT function.
 *
 * That matters more than it looks, because this is the write the phase's
 * temporal guarantee rests on: committed -> cannot be spliced
 * (splice/commit.ts) -> cannot be consumed -> cannot be pruned, for exactly as
 * long as the issuance is live. A guarantee of that weight should not be
 * recoverable only by reading three functions in the right order and trusting
 * that a lock stays where it is. So the predicate is restated here in full -
 * the player, the liveness (through the shared `liveCreature()`, never a
 * hand-rolled copy), and `committed_to IS NULL` - and each term is a claim
 * this statement makes for itself rather than one it inherits.
 *
 * AND THE ROW COUNT IS ASSERTED. Adding predicates without checking what they
 * matched would be strictly worse than not adding them: a narrowed WHERE that
 * silently matches fewer rows commits four creatures out of five, the fifth
 * stays spliceable while it is out fighting, and NOTHING downstream can tell -
 * `issueWave` returns the issuance either way. Under the lock this can only
 * fire on a logic bug (a caller passing ids `resolveDeployment` did not
 * resolve, or a predicate that drifts from the one `loadOwnedCreatures`
 * selected on), which is precisely the case worth a loud abort: the throw
 * rolls back the whole transaction, so no issuance exists and no creature is
 * half-committed. It is also covered from the other direction by every
 * successful `wave/start` in the suite - an off-by-one here would 500 all of
 * them.
 *
 * EXPORTED FOR ITS OWN TEST, and that is the point rather than a concession.
 * None of the three predicates is reachable through `wave/start`: by the time
 * this runs, `resolveDeployment` has already refused an unowned, dead or
 * committed creature, so no HTTP request can present this statement with a row
 * it should decline. Which is exactly the argument that made the loose WHERE
 * "correct today" - and a guarantee recoverable only from a lock in another
 * function is one a test cannot hold either. `wave-start.test.ts`'s
 * `commitCreatures refuses a row that is not this player's, live and
 * uncommitted` drives it directly, WITHOUT that lock, which is the only place
 * the predicates can be shown to do anything. Same precedent as `claimIssuance`
 * and `settle`, both exported and driven directly for the same reason.
 */
export async function commitCreatures(
  tx: Tx, serverId: number, playerId: string, ids: readonly string[], issuanceId: string,
): Promise<void> {
  if (ids.length === 0) return
  const res = await tx.update(creatures)
    .set({ committedTo: issuanceId })
    .where(and(
      eq(creatures.serverId, serverId),
      eq(creatures.playerId, playerId),
      inArray(creatures.creatureId, [...ids]),
      isNull(creatures.committedTo),
      liveCreature(),
    ))

  if ((res.rowCount ?? 0) !== ids.length) {
    throw new Error(
      `commitCreatures: issuance ${issuanceId} deployed ${ids.length} creatures but `
      + `${res.rowCount ?? 0} rows were committed - the rows resolveDeployment locked are `
      + `no longer all live, uncommitted and this player's.`)
  }
}

/**
 * Inserts a fresh issuance, or returns the row a conflicting caller already
 * holds.
 *
 * On check 4 versus the unique index: the select-then-insert above is a
 * race. Two concurrent wave/start calls can both find no live row (or, on
 * the abandoned-wave path, both find and settle() the SAME expired row -
 * the loser's settle() there simply affects 0 rows, which is not an error).
 * The partial unique index (wave_issuances_one_live) is what makes that
 * safe. Do NOT "fix" the race by removing the index - see task-5-brief.md.
 *
 * `ON CONFLICT ... DO NOTHING`, NOT a try/catch around a plain insert -
 * that was this function's first shape, and a CRITICAL finding on review
 * caught it as broken: `withServer` opens exactly one real
 * `BEGIN`/`COMMIT` on one connection (`db.transaction(...)`), and
 * `issueWave` never opens a nested `tx.transaction()` to get a SAVEPOINT.
 * A statement error - a unique violation included - therefore ABORTS THE
 * WHOLE ENCLOSING TRANSACTION, and every later statement on that same `tx`
 * fails with `25P02` ("current transaction is aborted") rather than
 * running. A caught 23505 followed by a recovery SELECT on the same `tx`
 * (the old shape) could not ever recover: the SELECT itself would throw
 * 25P02, escape `issueWave` and `withServer` uncaught, and surface at
 * `app.ts`'s `onError` as a 500 - the exact outcome the carried-forward
 * Task 4 concurrency note told this task to prevent. Verified against a
 * real conflict by
 * `test/wave-start.test.ts`'s dedicated `claimIssuance` test, which calls
 * this function twice in separate transactions for the same
 * (server_id, player_id) and asserts the second call resolves (not
 * rejects) with the FIRST call's row, and that its transaction is still
 * usable afterwards.
 *
 * `ON CONFLICT ... DO NOTHING` does not have this failure mode: Postgres
 * evaluates the conflict against the index (waiting, if necessary, for a
 * still-open conflicting inserter to commit or abort) and simply skips the
 * insert - no error is raised, so the transaction is never poisoned. The
 * skipped-insert case is then indistinguishable from "someone else already
 * has the slot", which is exactly the state the recovery SELECT below
 * reads.
 *
 * `deployment` IS REQUIRED, and it stopped having a `= []` default in Task 10.
 * The default was harmless while nothing READ the column and became a hazard
 * the moment something did: an issuance carrying `[]` is indistinguishable
 * from one carrying "the caller did not say", and design §6.2's submit-side
 * comparison would then be asked to tell an honest empty deployment from an
 * unpopulated one. `routes/wave.ts`'s `deploymentMatches` compares LENGTHS
 * FIRST so an empty stored deployment refuses every non-empty echo rather than
 * agreeing with all of them - but a comparison being robust against a default
 * is not a reason to keep the default. Every caller now says what was
 * deployed, `[]` included.
 *
 * RETURNS WHETHER THIS CALL INSERTED, not just the row - the same shape and
 * the same reasoning as `settle` below. It matters because Task 8 gave this
 * function a second side effect to pair with: the creatures of the
 * deployment are committed to the issuance that was minted, and on the
 * lost-race path the row coming back is the WINNER's, whose deployment was
 * resolved from the winner's own creatures. Committing ours to it would
 * garrison creatures that issuance never deployed, and nothing downstream
 * could tell. A bare `Issuance` return (this function's shape before Task 8)
 * gives the caller no way to distinguish the two, which is exactly the
 * mistake `settle`'s doc comment describes on the settle path.
 */
export async function claimIssuance(
  tx: Tx, serverId: number, playerId: string, waveId: number, seed: bigint,
  deployment: CreatureSpec[],
): Promise<{ issuance: Issuance; claimed: boolean }> {
  const [row] = await tx.insert(waveIssuances).values({
    serverId, issuanceId: randomUUID(), playerId, waveId,
    seed: seed.toString(),
    expiresAt: new Date(Date.now() + ISSUANCE_TTL_MS),
    deployment,
  }).onConflictDoNothing({
    // Must match wave_issuances_one_live's target list AND predicate
    // exactly (drizzle/0003_wave_issuances.sql) - Postgres only matches an
    // ON CONFLICT clause against a PARTIAL index when the clause repeats
    // that index's predicate verbatim.
    target: [waveIssuances.serverId, waveIssuances.playerId],
    where: sql`${waveIssuances.settledAt} IS NULL`,
  }).returning()
  if (row !== undefined) return { issuance: row, claimed: true }

  // Skipped: a live row already exists. By the time this INSERT could even
  // be evaluated against the index, any transaction that was still
  // inserting a conflicting row has necessarily committed or aborted -
  // Postgres's conflict checking blocks on an in-flight inserter rather
  // than racing it - so a plain read (no retry loop) is enough.
  const [winner] = await tx.select().from(waveIssuances)
    .where(and(
      eq(waveIssuances.serverId, serverId),
      eq(waveIssuances.playerId, playerId),
      isNull(waveIssuances.settledAt)))
  if (winner !== undefined) return { issuance: winner, claimed: false }

  // Not reachable in practice: DO NOTHING only skips when the predicate
  // matched an existing row, so that row must be selectable in this same
  // scope. A clear error beats returning undefined silently if it ever is.
  throw new Error(
    `issueWave: INSERT into wave_issuances was skipped by ON CONFLICT for ` +
    `server ${serverId} player ${playerId}, but no live row was found afterwards.`)
}

/**
 * The only writer of settled_at/settlement. Task 6 consumes this too, on
 * the submit path.
 *
 * IT ALSO RELEASES THE ROSTER - see `releaseCreatures` below. Task 8 made
 * `wave/start` set `committed_to` on every deployed creature, and the
 * settlement is where that ends: a creature is out fighting for exactly as
 * long as its issuance is live, and both terminal states end it.
 *
 * GATED: `WHERE issuance_id = ... AND settled_at IS NULL`. Two concurrent
 * callers can both select the SAME expired-but-live row (issueWave's check
 * 4) and both call this. They serialize on Postgres's row lock; the loser's
 * WHERE is re-evaluated under READ COMMITTED after the winner commits, no
 * longer matches (settled_at is no longer NULL), and the write-once trigger
 * NEVER FIRES - the loser gets rowCount 0, not an exception. Without the
 * `AND settled_at IS NULL` guard the loser's UPDATE WOULD still match the
 * row (by issuance_id alone) and the trigger would raise a loud unhandled
 * Postgres exception instead.
 *
 * RETURNS whether THIS CALL performed the settlement (rowCount > 0), not
 * void. For the 'expired' path (issueWave's check 4) rowCount 0 simply
 * means "already settled by a concurrent request, proceed to insert" and
 * the caller does not need the distinction. It is NOT optional for the
 * 'consumed' path design §4.2 assigns to Task 6: there, rowCount 0 means
 * *a different submission already consumed this issuance*, and whether
 * `credit()` runs must depend on that. A `void` return (this function's
 * first shape, and what the brief itself specified - flagged here as my
 * error, not a brief deviation) lets two concurrent submits carrying
 * different Idempotency-Keys both observe "settle ran, no exception" and
 * both credit the reward, because neither could tell it had lost the race.
 */
export async function settle(tx: Tx, issuance: Issuance, settlement: 'consumed' | 'expired'): Promise<boolean> {
  const res = await tx.execute(sql`
    UPDATE wave_issuances SET settled_at = now(), settlement = ${settlement}
    WHERE server_id = ${issuance.serverId}
      AND issuance_id = ${issuance.issuanceId}
      AND settled_at IS NULL`)

  await releaseCreatures(tx, issuance)

  return (res.rowCount ?? 0) > 0
}

/**
 * design 6.1's step 6 undone - the other half of `committed_to`'s first
 * writer.
 *
 * HERE, BECAUSE `settle` IS ALREADY THE ONE WRITER OF THE TERMINAL STATE,
 * and BOTH terminal states free the roster. A release wired to the consumed
 * path alone would strand every abandoned wave's deployment permanently:
 * design 4.3's abandoned-wave path settles 'expired', the player never
 * submitted anything, and five creatures would be un-spliceable forever with
 * nothing on the consumed path ever noticing. `test/wave-start.test.ts`'s
 * `clears committed_to when the issuance EXPIRES too` is the test for that
 * half specifically.
 *
 * NOT GATED ON THE SETTLEMENT ACTUALLY HAPPENING, unlike `credit()` on the
 * submit path, and the WHERE is why it does not need to be: it names
 * `committed_to = this issuance`, so a caller that lost the settle race
 * clears rows the winner has already cleared - zero rows, no effect - and can
 * never touch a commitment belonging to a DIFFERENT issuance. Making it
 * conditional would instead make the release depend on which of two
 * concurrent settlers ran first, for no gain.
 *
 * LOCKED IN ASCENDING creature_id ORDER, which a bare `UPDATE ... WHERE
 * committed_to = ...` would not do: that statement locks rows in whatever
 * order the plan produces them, and every other writer of this table
 * (splice/commit.ts's `lockParents`, roster/creatures.ts's
 * `loadOwnedCreatures`) locks in sorted order. A settle releasing two
 * creatures in plan order while a concurrent splice locks the same two in
 * sorted order is a deadlock, which Postgres resolves by aborting one
 * transaction - a 500 on a route that was working. The SELECT ... ORDER BY
 * ... FOR UPDATE takes the locks in the order this codebase's other writers
 * already agree on.
 */
async function releaseCreatures(tx: Tx, issuance: Issuance): Promise<void> {
  const held = await tx.select({ creatureId: creatures.creatureId }).from(creatures)
    .where(and(
      eq(creatures.serverId, issuance.serverId),
      eq(creatures.committedTo, issuance.issuanceId),
    ))
    .orderBy(creatures.creatureId)
    .for('update')
  if (held.length === 0) return

  await tx.update(creatures)
    .set({ committedTo: null })
    .where(and(
      eq(creatures.serverId, issuance.serverId),
      inArray(creatures.creatureId, held.map((h) => h.creatureId)),
    ))
}

/**
 * design §4.2 step 2: "Load the issuance for this player. Absent, expired
 * or already consumed" are ONE answer, issuance_invalid - the caller does
 * not get to distinguish them, which is what stops this endpoint telling an
 * attacker anything about issuances that are not theirs.
 *
 * Scoped by issuanceId AND playerId AND serverId together, not issuanceId
 * alone - a UUID is unguessable but this is the row that gates a credit, so
 * it is checked the same defensively as everything else on this path.
 *
 * Deliberately does NOT settle an expired-but-live row the way issueWave's
 * check 4 does - that behaviour belongs to wave/start (the abandoned-wave
 * path makes room for a NEW issuance), and a submission against an expired
 * one is simply invalid, nothing to make room for.
 */
export async function loadLiveIssuance(
  tx: Tx, serverId: number, playerId: string, issuanceId: string,
): Promise<Issuance | undefined> {
  const [row] = await tx.select().from(waveIssuances)
    .where(and(
      eq(waveIssuances.serverId, serverId),
      eq(waveIssuances.playerId, playerId),
      eq(waveIssuances.issuanceId, issuanceId),
      isNull(waveIssuances.settledAt)))
  if (row === undefined) return undefined
  if (row.expiresAt <= new Date()) return undefined
  return row
}

/**
 * Advances campaign_progress to at least `waveId`. Never regresses it: a
 * replay of an already-cleared wave (design §4.1's replay branch) must not
 * move the high-water mark backwards, and a winning replay of the SAME
 * wave the player already cleared is a no-op write here by construction
 * (Math.max leaves the SET value unchanged).
 *
 * `ON CONFLICT ... DO UPDATE`, not a separate SELECT-then-insert-or-update -
 * review finding: the previous shape's plain INSERT had no conflict
 * handling at all, so two transactions racing a player's FIRST-ever
 * completion (no campaign_progress row yet) would both attempt to INSERT
 * the same (server_id, player_id) primary key, and the loser would throw a
 * raw 23505 that escapes uncaught as a 500. In the SHIPPED handler that
 * race cannot happen - routes/wave.ts's settle() gate means only one
 * transaction per issuance ever reaches this call - so this was never
 * reachable in production. It is fixed anyway: campaign_progress's safety
 * should not rest on a caller in a DIFFERENT module checking settle()'s
 * return correctly. ON CONFLICT DO UPDATE makes the insert-or-update
 * atomic and race-safe on its own terms, independent of that guard.
 */
export async function advanceCampaign(tx: Tx, serverId: number, playerId: string, waveId: number): Promise<void> {
  const [progress] = await tx.select().from(campaignProgress)
    .where(and(eq(campaignProgress.serverId, serverId), eq(campaignProgress.playerId, playerId)))
  const highestWaveCleared = Math.max(progress?.highestWaveCleared ?? 0, waveId)

  await tx.insert(campaignProgress)
    .values({ serverId, playerId, highestWaveCleared })
    .onConflictDoUpdate({
      target: [campaignProgress.serverId, campaignProgress.playerId],
      set: { highestWaveCleared, updatedAt: new Date() },
    })
}
