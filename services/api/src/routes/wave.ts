import { eq } from 'drizzle-orm'
import type { Hono } from 'hono'
import type { Deps } from '../app.ts'
import { loadBundle } from '../config/bundle.ts'
import { withServer } from '../db/client.ts'
import { players } from '../db/schema.ts'
import { requireSession } from '../http/auth.ts'
import { fail } from '../http/errors.ts'
import { issueWave } from '../wave/issuance.ts'

interface StartBody { waveId: number }

function parseStart(raw: unknown): StartBody | null {
  if (typeof raw !== 'object' || raw === null) return null
  const b = raw as Record<string, unknown>
  if (typeof b.waveId !== 'number' || !Number.isInteger(b.waveId)) return null
  return { waveId: b.waveId }
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
}
