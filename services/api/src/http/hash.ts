import { createHash } from 'node:crypto'

/**
 * The hash an idempotency key is bound to.
 *
 * solo_execution 6.3: if the key matches but the request hash differs, 422 -
 * that is a client bug, and returning another request's response would be
 * worse than failing.
 *
 * Stable key order, so two structurally identical bodies that serialised
 * their fields differently are not treated as different requests.
 */
export function hashRequest(body: unknown): string {
  return createHash('sha256').update(stableStringify(body)).digest('hex')
}

function stableStringify(v: unknown): string {
  if (v === null || typeof v !== 'object') return JSON.stringify(v) ?? 'null'
  if (Array.isArray(v)) return `[${v.map(stableStringify).join(',')}]`
  const entries = Object.entries(v as Record<string, unknown>).sort(([a], [b]) => (a < b ? -1 : 1))
  return `{${entries.map(([k, val]) => `${JSON.stringify(k)}:${stableStringify(val)}`).join(',')}}`
}
