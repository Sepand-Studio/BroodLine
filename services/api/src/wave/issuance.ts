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
        sql`${waveIssuances.issuedAt} >= date_trunc('day', now())`))
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

  // On check 4 versus the unique index. The select-then-insert above is a
  // race: two concurrent wave/start calls can both find no live row (or,
  // on the abandoned-wave path, can both find and settle the SAME expired
  // row - the loser's settle() below simply affects 0 rows, which is not an
  // error). The partial unique index (wave_issuances_one_live) is what
  // makes that safe: the loser of the INSERT gets 23505, not a second seed.
  // Do NOT "fix" the race by removing the index - see task-5-brief.md.
  try {
    const [row] = await tx.insert(waveIssuances).values({
      serverId, issuanceId: randomUUID(), playerId, waveId,
      seed: seed.toString(),
      expiresAt: new Date(Date.now() + ISSUANCE_TTL_MS),
    }).returning()
    return row!
  } catch (err) {
    // Gated on the CONSTRAINT, not just the SQLSTATE, for the same reason
    // money/ledger.ts's credit() gates on wallets_balance_check - 23505 is
    // reachable from more than one unique constraint in principle, and only
    // THIS one means "a live issuance already exists, read it back".
    const e = err as { code?: string; constraint?: string } | null
    if (typeof err === 'object' && e !== null
      && e.code === '23505' && e.constraint === 'wave_issuances_one_live') {
      const [row] = await tx.select().from(waveIssuances)
        .where(and(
          eq(waveIssuances.serverId, serverId),
          eq(waveIssuances.playerId, playerId),
          isNull(waveIssuances.settledAt)))
      // The concurrent winner's transaction has committed by the time this
      // INSERT could observe the conflict at all - READ COMMITTED promotes
      // to a fresh snapshot only after the blocking write resolves - so the
      // row it wrote is visible here without a retry loop.
      if (row !== undefined) return row
    }
    throw err
  }
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
 * Postgres exception instead. rowCount 0 here is not an error: it means
 * "already settled by a concurrent request", and the caller proceeds
 * exactly as if it had won.
 */
export async function settle(tx: Tx, issuance: Issuance, settlement: 'consumed' | 'expired'): Promise<void> {
  await tx.execute(sql`
    UPDATE wave_issuances SET settled_at = now(), settlement = ${settlement}
    WHERE server_id = ${issuance.serverId}
      AND issuance_id = ${issuance.issuanceId}
      AND settled_at IS NULL`)
}
