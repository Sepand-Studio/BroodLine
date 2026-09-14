/**
 * The one error shape, from solo_execution 6.2: the client switches on
 * `code`, never on message text. Rewording a message must never be a
 * breaking change, which it becomes the moment a client matches on it.
 */
export type ErrorCode =
  | 'invalid_request'
  | 'idempotency_key_reused'
  | 'unauthorized'
  | 'not_found'
  | 'conflict'
  | 'client_too_old'
  | 'internal'
  // Phase 5
  | 'wave_locked'
  | 'replay_cap_reached'
  | 'issuance_invalid'
  | 'submission_rejected'
  | 'engine_too_old'
  | 'sim_unavailable'

export interface ErrorBody {
  code: ErrorCode
  message: string
  details?: unknown
}

const STATUS: Record<ErrorCode, number> = {
  invalid_request: 400,
  unauthorized: 401,
  not_found: 404,
  conflict: 409,
  // 422 and not 409: the key is valid and was accepted before, but this
  // request's body differs from the one it was accepted for. Returning the
  // stored response would answer a question the caller did not ask.
  idempotency_key_reused: 422,
  client_too_old: 426,
  internal: 500,
  wave_locked: 409,
  replay_cap_reached: 429,
  issuance_invalid: 409,
  submission_rejected: 409,
  // 426, the same status client_too_old uses. A client whose ENGINE is
  // superseded and one whose BUILD is below the floor both need the same
  // thing from the player, and the codes stay distinct so the client can
  // say which - design 2.3.
  engine_too_old: 426,
  sim_unavailable: 503,
}

export function fail(code: ErrorCode, message: string, details?: unknown): Response {
  const body: ErrorBody = details === undefined ? { code, message } : { code, message, details }
  return new Response(JSON.stringify(body), {
    status: STATUS[code],
    headers: { 'content-type': 'application/json' },
  })
}
