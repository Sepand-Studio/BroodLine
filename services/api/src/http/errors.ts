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
  // Phase 6
  | 'roster_full'

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
  // 409, not 400: the request was understood and is refused because of
  // state the caller can change (splice or retire a creature and claim
  // again), which is the same shape wave_locked and issuance_invalid take.
  // design 4.3 refuses the WHOLE claim rather than truncating the grant, so
  // this is the only answer a full roster can get - a partial grant that
  // silently drops creatures is a loss a player reports as theft.
  roster_full: 409,
}

export function fail(code: ErrorCode, message: string, details?: unknown): Response {
  const body: ErrorBody = details === undefined ? { code, message } : { code, message, details }
  return new Response(JSON.stringify(body), {
    status: STATUS[code],
    headers: { 'content-type': 'application/json' },
  })
}
