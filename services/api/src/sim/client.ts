import type { components } from '../generated/sim.ts'

export type SimulateEcho = components['schemas']['SimulateEcho']
export type SimulateOutcome = components['schemas']['SimulateOutcome']
export type SimulateBreach = components['schemas']['BreachDto']
type SimulateResponse = components['schemas']['SimulateResponse']

/**
 * Narrows a field the generated types carry as `number | string`.
 *
 * services/api/src/generated/sim.ts is generated from sim's OpenAPI
 * document, and every integer field in it comes out `number | string` -
 * minimal APIs use JsonSerializerDefaults.Web, which sets
 * NumberHandling.AllowReadingFromString, and that leniency is reflected in
 * the schema NSwag/ASP.NET emits even though sim's actual JSON responses
 * always write a plain number. The type is faithful to what the schema
 * ALLOWS, not to what sim ACTUALLY sends - which makes it over-broad for a
 * response body, and dangerous to compare or store without narrowing first:
 * a bare `!==` between a `number | string` and a `number` is exactly the
 * kind of comparison that silently always passes type-checking while being
 * able to silently always DISAGREE at runtime if the string branch is ever
 * hit (`"6" !== 6` is `true` in JS, even though both denote the wave the
 * player was issued).
 */
export function toInt(v: number | string): number {
  const n = typeof v === 'string' ? Number(v) : v
  if (!Number.isFinite(n)) {
    throw new Error(`toInt: not a finite number: ${JSON.stringify(v)}`)
  }
  return n
}

/**
 * The only caller of sim, and the only place that decides what a failure
 * MEANS.
 *
 * Three outcomes, not two. A rejected submission and an unreachable service
 * are both "the request did not succeed", and collapsing them is the bug
 * this type exists to prevent: a rejection must CONSUME nothing and return a
 * refusal, while an outage must leave the issuance live so the player can
 * retry. Design 3.2 is why sim answers a rejection with 200 - so this
 * distinction is available here at all.
 */
export type SimVerdict =
  | { kind: 'verified'; echo: SimulateEcho; outcome: SimulateOutcome }
  | { kind: 'rejected'; reason: string }
  | { kind: 'unavailable' }

export class SimClient {
  constructor(private readonly baseUrl: string, private readonly fetchImpl = fetch) {}

  async simulate(replayBase64: string): Promise<SimVerdict> {
    let body: SimulateResponse
    try {
      const res = await this.fetchImpl(`${this.baseUrl}/internal/simulate`, {
        method: 'POST',
        headers: { 'content-type': 'application/json' },
        body: JSON.stringify({ replay: replayBase64 }),
        // The SLO is p99 500ms end to end including re-simulation, and one
        // wave is ~20ms of CPU. A request still open at 5s is not slow, it
        // is wedged - and without a timeout it holds a Cloud Run instance
        // and a Postgres connection with it.
        signal: AbortSignal.timeout(5_000),
      })

      if (!res.ok) return { kind: 'unavailable' }

      // A dropped connection mid-body, or a response that is 200 but not
      // valid JSON, is exactly as much an outage as a failed fetch - both
      // mean "no verdict was obtained", not "the submission was refused".
      // The brief's original shape only wrapped the fetch call itself in
      // try/catch and left this parse outside it, which would have this
      // throw out of simulate() uncaught and surface as an unhandled 500
      // rather than the retryable 503 the design promises for an outage.
      body = await res.json() as SimulateResponse
    } catch {
      return { kind: 'unavailable' }
    }

    if (body.verdict === 'rejected') return { kind: 'rejected', reason: body.reason ?? 'unknown' }
    return { kind: 'verified', echo: body.echo!, outcome: body.outcome! }
  }
}
