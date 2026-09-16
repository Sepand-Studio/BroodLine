/**
 * The parse layer's shape check on an id that reaches a Postgres `uuid`
 * column, in the same spirit as routes/wave.ts's MAX_WAVE_ID and
 * routes/region.ts's MAX_NODE_SLOT.
 *
 * IT IS NOT A STATEMENT ABOUT WHICH ROWS EXIST. The read each caller makes
 * stays the only authority on that, and every well-formed id still goes to
 * it - a fabricated uuid gets the route's ordinary refusal, unchanged.
 *
 * What it keeps is the `invalid_request` (400) / `not_found` (404) /
 * `conflict` (409) distinction. A string that is not a uuid cannot be STORED
 * by any of these columns under any bundle, so it is malformed by
 * construction rather than merely absent - and without this check Postgres
 * answers `22P02 invalid input syntax for type uuid` from inside the read,
 * which app.ts's `onError` turns into `internal` (500). Telling an
 * authenticated caller the server broke, when in fact they sent a malformed
 * body, is the failure this exists to prevent.
 *
 * SHARED RATHER THAN COPIED PER ROUTE, and deliberately: three call sites
 * need the identical check, and a validation predicate duplicated across
 * route files is one that drifts. The column each caller is protecting is
 * cited at the call site; the reason is here.
 *
 * IT NORMALISES, AND THAT IS NOT COSMETIC - see `normalizeUuid`.
 */
const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i

/**
 * Validates AND lower-cases, and returns `null` for anything that is not a
 * uuid. **This is the only exported entry point on purpose.**
 *
 * A PREDICATE THAT VALIDATES WITHOUT NORMALISING IS A TRAP, and it was one:
 * the regex above accepts upper case (`/i`, correctly - RFC 4122 hex is
 * case-insensitive and clients send both), Postgres `uuid` equality is
 * case-insensitive, and JavaScript `===` is not. So a handler comparing two
 * request ids, or a request id against one it read back, is asking a
 * DIFFERENT question than the database would.
 *
 * Measured, on `POST /v1/splice/commit`: `{parentA: id, parentB: id
 * .toUpperCase()}` passed `parentA === parentB`, both locks resolved to the
 * SAME row, and the splice ran past the debit before `consume` found one
 * parent where it expected two - a 500 on the only route in the game that
 * destroys player property, for a body `POST /v1/splice/preview` answers 404
 * for. Nothing was destroyed (the transaction rolls back, and `splices`'
 * `splice_parents_differ` is a second backstop), but a 500 is not an answer.
 *
 * THE OTHER HALF, quieter and also real: both splice/commit and wave/submit
 * hash the PARSED body for their idempotency key (`hashRequest`). Without
 * normalisation the same logical retry with a differently-cased id hashes
 * differently, and the caller gets 422 `idempotency_key_reused` for a
 * request that is theirs and identical.
 *
 * Returning the normalised string rather than a boolean is what makes the
 * fix un-forgettable: a caller cannot use this and still hold the raw value.
 */
export function normalizeUuid(value: unknown): string | null {
  return typeof value === 'string' && UUID.test(value) ? value.toLowerCase() : null
}
