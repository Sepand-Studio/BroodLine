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
 * SHARED RATHER THAN COPIED PER ROUTE, and deliberately: two routes needed
 * the identical check, and a validation predicate duplicated across route
 * files is one that drifts. The column each caller is protecting is cited at
 * the call site; the reason is here.
 */
const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i

export function isUuid(value: unknown): value is string {
  return typeof value === 'string' && UUID.test(value)
}
