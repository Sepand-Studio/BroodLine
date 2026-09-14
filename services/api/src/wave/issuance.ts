import { randomUUID } from 'node:crypto'
import { and, eq, isNull, sql } from 'drizzle-orm'
import type { Bundle } from '../config/bundle.ts'
import type { Tx } from '../db/client.ts'
import { campaignProgress, waveIssuances } from '../db/schema.ts'

export const ISSUANCE_TTL_MS = 7_200_000 // two hours - design 4.1
export const REPLAY_CAP_PER_DAY = 3 // broodline_campaign_structure.md

export type Issuance = typeof waveIssuances.$inferSelect

export type IssuanceRefusal =
  | { refused: 'wave_locked' }
  | { refused: 'replay_cap_reached' }

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
  tx: Tx, serverId: number, playerId: string, waveId: number, bundle: Bundle,
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
        // AT TIME ZONE 'UTC', explicit: date_trunc('day', timestamptz)
        // alone truncates in the SESSION's TimeZone GUC, which createPool
        // never pins. Correct today only because both postgres:16-alpine
        // and Cloud SQL default that GUC to UTC - this makes the boundary
        // explicit rather than depending on that default holding.
        sql`${waveIssuances.issuedAt} >= date_trunc('day', now() AT TIME ZONE 'UTC')`))
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

  return claimIssuance(tx, serverId, playerId, waveId, seed)
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
 */
export async function claimIssuance(
  tx: Tx, serverId: number, playerId: string, waveId: number, seed: bigint,
): Promise<Issuance> {
  const [row] = await tx.insert(waveIssuances).values({
    serverId, issuanceId: randomUUID(), playerId, waveId,
    seed: seed.toString(),
    expiresAt: new Date(Date.now() + ISSUANCE_TTL_MS),
  }).onConflictDoNothing({
    // Must match wave_issuances_one_live's target list AND predicate
    // exactly (drizzle/0003_wave_issuances.sql) - Postgres only matches an
    // ON CONFLICT clause against a PARTIAL index when the clause repeats
    // that index's predicate verbatim.
    target: [waveIssuances.serverId, waveIssuances.playerId],
    where: sql`${waveIssuances.settledAt} IS NULL`,
  }).returning()
  if (row !== undefined) return row

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
  if (winner !== undefined) return winner

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
  return (res.rowCount ?? 0) > 0
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
 * wave the player already cleared is a no-op update here by construction
 * (Math.max leaves it unchanged).
 */
export async function advanceCampaign(tx: Tx, serverId: number, playerId: string, waveId: number): Promise<void> {
  const [progress] = await tx.select().from(campaignProgress)
    .where(and(eq(campaignProgress.serverId, serverId), eq(campaignProgress.playerId, playerId)))
  const highestWaveCleared = Math.max(progress?.highestWaveCleared ?? 0, waveId)

  if (progress === undefined) {
    await tx.insert(campaignProgress).values({ serverId, playerId, highestWaveCleared })
  } else if (highestWaveCleared !== progress.highestWaveCleared) {
    await tx.update(campaignProgress).set({ highestWaveCleared, updatedAt: new Date() })
      .where(and(eq(campaignProgress.serverId, serverId), eq(campaignProgress.playerId, playerId)))
  }
}
