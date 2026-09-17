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
 * `toInt` for a field that is allowed to be ABSENT, and the only two fields
 * that are: `CreatureSpecDto.tier1` and `tier2`.
 *
 * NULL IS NOT ZERO AND MUST NEVER BECOME ZERO. data_model 2: a null coverage
 * tier is an ABERRANT, which has no coverage at all, and zero would sort and
 * display as "less than tier I" - two states the design keeps apart on
 * purpose. drizzle/0005_loop.sql's `coverage_tier_N_not_zero` makes 0
 * unstorable on this side (`tier_N IS NULL OR tier_N BETWEEN 1 AND 3`), and
 * services/sim/SimulateEndpoint.cs's `Coverage()` translates the engine's own
 * spelling of the same state (a plain `int` 0) into null on the way out. So
 * the two sides already agree; this function's whole job is not to undo that
 * agreement on the last hop.
 *
 * `v ?? 0`, `Number(v)` and `toInt(v as number)` are each one keystroke away
 * and each wrong in the same direction: they turn "no coverage" into "tier
 * zero", which equals nothing api can store, and design 6.2's comparison
 * would then reject an HONEST Aberrant on every submission it ever saw.
 * `toInt` refusing `null` at compile time is what forced this decision to be
 * made rather than defaulted into.
 */
export function toTier(v: null | number | string): number | null {
  return v === null ? null : toInt(v)
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

/**
 * Turns an AbortSignal into a promise that REJECTS when it fires, so a
 * non-abortable promise (google-auth-library's token mint takes no signal)
 * can still be bounded by the same budget the fetch uses.
 */
function abortAsRejection(signal: AbortSignal): Promise<never> {
  return new Promise((_resolve, reject) => {
    if (signal.aborted) {
      reject(signal.reason)
      return
    }
    signal.addEventListener('abort', () => reject(signal.reason), { once: true })
  })
}

/**
 * HOW `SimClient` PROVES TO `sim` THAT IT IS `api` - and why this is a
 * REQUIRED constructor argument rather than something with a default.
 *
 * `sim` is `INGRESS_TRAFFIC_INTERNAL_ONLY` with exactly one invoker binding
 * (`google_cloud_run_v2_service_iam_member.api_invokes_sim`, infra/terraform/
 * main.tf), which is design 3.1's table: only `api`'s service account may
 * invoke it, and only VPC-routed traffic may reach it. Two independent
 * gates. The IAM half needs an `Authorization: Bearer <OIDC ID token>` whose
 * audience is sim's URL; without one Cloud Run answers 403 before sim's
 * process is ever reached.
 *
 * THE FAILURE SHAPE THIS TYPE EXISTS TO PREVENT, and it is the one this
 * project keeps rediscovering: a token provider that DEFAULTS to a no-op
 * when unconfigured. Under that shape the tests (which run against a local
 * sim host with no auth at all) and production take different branches of
 * the same code, and a production wiring that quietly loses its provider
 * looks exactly like a passing test suite. An auth gate that is green while
 * protecting nothing is worse than no gate, because it is also believed.
 *
 * So there is no default. `SimAuth` is a REQUIRED second constructor
 * argument, `kind: 'none'` has to be written out by name with a reason, and
 * `new SimClient(url)` is a compile error (test/sim-auth.test.ts pins that
 * with a `@ts-expect-error`, so the arity cannot silently relax). The
 * constructor ALSO throws on a missing or malformed value, because types are
 * erased: JS callers, `as any`, and a seam that widens later all survive
 * `tsc` and would otherwise reach production as an unauthenticated request.
 */
export type SimAuth =
  | {
      readonly kind: 'oidc'
      /**
       * Resolves the complete `Authorization` header value - `Bearer eyJ...`,
       * not the bare token - for the given audience. Returning the whole
       * header value is what google-auth-library's `getRequestHeaders()`
       * already produces, so the production provider (sim/oidc.ts) hands it
       * across unedited rather than splitting and reassembling a credential.
       */
      readonly authorization: (audience: string) => Promise<string>
    }
  | {
      readonly kind: 'none'
      /**
       * Why this particular client is deliberately unauthenticated. Required
       * so the opt-out cannot be typed by reflex: every occurrence has to
       * state, at the call site, what makes it safe.
       */
      readonly reason: string
    }

export class SimClient {
  /**
   * The explicit opt-out. A STATIC rather than a bare exported function so a
   * call site cannot acquire it by accident - you have to name `SimClient`
   * to get it, and the name says what it does.
   *
   * The only legitimate users are tests that run against a locally spawned
   * `Broodline.Sim.Service` on 127.0.0.1, which has no Cloud Run in front of
   * it and therefore no invoker check to satisfy. Production wiring
   * (src/index.ts) uses googleIdTokenAuth() from ./oidc.ts.
   */
  static noAuth(reason: string): SimAuth {
    return { kind: 'none', reason }
  }

  // EXPLICIT FIELDS, NOT CONSTRUCTOR PARAMETER PROPERTIES, and this class is
  // where that rule was actually broken. `config/store.ts` and
  // `replays/store.ts` each carry a comment saying a parameter property
  // "anywhere in index.ts's import graph crashes on boot" because the
  // container runs `node --experimental-strip-types` (Dockerfile CMD), which
  // is strip-only and cannot emit the assignment a parameter property needs.
  // index.ts imports THIS file, so index.ts could not load at all:
  // `ERR_UNSUPPORTED_TYPESCRIPT_SYNTAX`, at module load, before a line of
  // configuration was read. The service would have crash-looped on its first
  // deploy.
  //
  // Nothing caught it because nothing runs the entrypoint. Vitest compiles
  // with esbuild, which DOES support parameter properties, so all 455 api
  // tests passed over a service that could not start - the precise trap the
  // two store files predicted in writing and this one then walked into.
  private readonly baseUrl: string
  private readonly auth: SimAuth
  private readonly fetchImpl: typeof fetch
  private readonly budgetMs: number

  constructor(
    baseUrl: string,
    auth: SimAuth,
    fetchImpl = fetch,
    /**
     * One budget for the WHOLE outbound call - minting the ID token and the
     * request itself share it, rather than each getting 5s and the pair
     * getting 10. The original comment below is the reason and it applies to
     * the call, not to the fetch half of it.
     *
     * A parameter (with the real default) only so the timeout BRANCH is
     * directly testable: proving "a token fetch that hangs degrades to
     * unavailable and issues no unauthenticated request" otherwise means a
     * five-second unit test, which is how that assertion ends up deleted.
     */
    budgetMs = 5_000,
  ) {
    this.baseUrl = baseUrl
    this.auth = auth
    this.fetchImpl = fetchImpl
    this.budgetMs = budgetMs
    // LOUD AT CONSTRUCTION, not quiet at runtime. See the SimAuth comment:
    // the type is the primary gate, this is the one that survives erasure.
    // The alternative failure is a 403 per wave submission on a deploy that
    // came up green, which surfaces as `sim_unavailable` to players and as
    // nothing at all to a health check.
    if (auth == null || (auth.kind !== 'oidc' && auth.kind !== 'none')) {
      throw new Error(
        'SimClient requires an explicit SimAuth: googleIdTokenAuth() from ./oidc.ts in production, '
        + 'or SimClient.noAuth(reason) against a local sim host. There is no default on purpose - '
        + 'see the SimAuth comment in src/sim/client.ts.',
      )
    }
    if (auth.kind === 'none' && auth.reason.trim() === '') {
      throw new Error('SimClient.noAuth(reason) requires a non-empty reason.')
    }
  }

  async simulate(replayBase64: string): Promise<SimVerdict> {
    let body: SimulateResponse
    try {
      // The SLO is p99 500ms end to end including re-simulation, and one
      // wave is ~20ms of CPU. A request still open at 5s is not slow, it
      // is wedged - and without a timeout it holds a Cloud Run instance
      // and a Postgres connection with it.
      const budget = AbortSignal.timeout(this.budgetMs)

      const headers: Record<string, string> = { 'content-type': 'application/json' }
      if (this.auth.kind === 'oidc') {
        // MINTED INSIDE THIS try, ON PURPOSE. A token fetch that fails or
        // hangs is an outage of the verification path, not a rejection of
        // the player's submission and not a 500: falling through to the
        // catch below yields `unavailable`, which leaves the issuance live
        // and retryable (routes/wave.ts answers it with a 503). The two
        // wrong alternatives are both reachable from here and both silent -
        // letting the rejection escape simulate() surfaces as an unhandled
        // 500 that consumes nothing but tells the player the wrong thing,
        // and catching it to retry WITHOUT the header turns a transient
        // metadata-server blip into a request that quietly drops the gate.
        //
        // Note what does NOT happen when this throws: this.fetchImpl is
        // never called. There is no unauthenticated attempt.
        headers.authorization = await Promise.race([
          this.auth.authorization(this.baseUrl),
          abortAsRejection(budget),
        ])
      }

      const res = await this.fetchImpl(`${this.baseUrl}/internal/simulate`, {
        method: 'POST',
        headers,
        body: JSON.stringify({ replay: replayBase64 }),
        signal: budget,
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

    // Fail CLOSED rather than open. `sim` emits only 'verified' and
    // 'rejected' today, so this branch is unreached in practice - but the
    // previous shape assumed 'verified' as the default for ANY value this
    // union does not name (a typo'd verdict string, a future sim emitting a
    // third kind, `echo`/`outcome` genuinely absent on a verdict claiming
    // 'verified') and forced them past the type system with `!`. That is
    // exactly the wrong default across the boundary this phase exists to
    // harden: an unrecognised response should read as an outage (retryable,
    // issuance stays live), never as a verified win.
    if (body.verdict === 'verified' && body.echo != null && body.outcome != null) {
      return { kind: 'verified', echo: body.echo, outcome: body.outcome }
    }
    return { kind: 'unavailable' }
  }
}
