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
}

export function fail(code: ErrorCode, message: string, details?: unknown): Response {
  const body: ErrorBody = details === undefined ? { code, message } : { code, message, details }
  return new Response(JSON.stringify(body), {
    status: STATUS[code],
    headers: { 'content-type': 'application/json' },
  })
}
