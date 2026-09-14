import { describe, expect, it } from 'vitest'
import { type SimAuth, SimClient } from '../src/sim/client.ts'
import { googleIdTokenAuth } from '../src/sim/oidc.ts'

/**
 * THE GATE THIS FILE GUARDS, and why it is a separate file rather than a
 * section of sim-client.test.ts: `sim` is reachable only by `api`'s service
 * account (`roles/run.invoker`, one member, infra/terraform/main.tf) and
 * only over the VPC (`INGRESS_TRAFFIC_INTERNAL_ONLY`). Design 3.1's two
 * gates. The IAM half is the only one that lives in application code, and
 * the whole of it is one header.
 *
 * Every test below fails if a SPECIFIC branch of SimClient/googleIdTokenAuth
 * is removed - not merely if the enclosing function is broken. The mutations
 * that each one catches are named on the test.
 */

const SIM_URL = 'https://broodline-sim-abc123-uc.a.run.app'
const DEAD = 'http://127.0.0.1:1'

/** A fetch stub that records every call and never touches the network. */
function recordingFetch(response: () => Response) {
  const calls: Array<{ url: string; init: RequestInit }> = []
  const impl = (async (url: string | URL | Request, init?: RequestInit) => {
    calls.push({ url: String(url), init: init ?? {} })
    return response()
  }) as unknown as typeof fetch
  return { calls, impl }
}

const verifiedBody = () => new Response(
  JSON.stringify({ verdict: 'verified', echo: { waveId: 6, seed: '1' }, outcome: { cleared: true } }),
  { status: 200, headers: { 'content-type': 'application/json' } },
)

function headerOf(init: RequestInit, name: string): string | undefined {
  return (init.headers as Record<string, string> | undefined)?.[name]
}

describe('SimClient construction refuses to be unauthenticated by accident', () => {
  /**
   * THE COMPILE-TIME HALF, and the @ts-expect-error is the assertion - not
   * decoration. If the constructor's second parameter ever acquires a
   * default (the exact regression this whole design exists to prevent: a
   * token provider that silently becomes a no-op when unconfigured), then
   * this line stops erroring and `tsc` fails the build with
   *   error TS2578: Unused '@ts-expect-error' directive.
   * So `pnpm --filter @broodline/api typecheck` is the thing that pins the
   * arity, and it fails in BOTH directions.
   *
   * The runtime half is on the same line: types are erased, so the
   * constructor also throws, which is what catches a JS caller or an `as
   * any` that tsc never sees.
   */
  it('is a type error AND a throw when the SimAuth argument is omitted', () => {
    // @ts-expect-error - omitting SimAuth must not compile. See above.
    expect(() => new SimClient(DEAD)).toThrow(/requires an explicit SimAuth/)
  })

  // Mutation that this catches on its own: delete the `auth == null` clause
  // of the constructor's guard and this stops throwing - it would build a
  // client that sends no Authorization header and 403s on every wave.
  it('throws when the SimAuth argument is present but undefined', () => {
    expect(() => new SimClient(DEAD, undefined as unknown as SimAuth)).toThrow(/requires an explicit SimAuth/)
  })

  // Mutation: delete the `auth.kind !== 'oidc' && auth.kind !== 'none'`
  // clause. A plausible-looking object with the wrong shape (a half-finished
  // provider, a stale one after a rename) would then construct, take neither
  // branch in simulate(), and send an unauthenticated request.
  it('throws on a SimAuth-shaped object that is neither kind', () => {
    expect(() => new SimClient(DEAD, { kind: 'bearer' } as unknown as SimAuth))
      .toThrow(/requires an explicit SimAuth/)
  })

  // Mutation: delete the empty-reason guard. `SimClient.noAuth('')` is how
  // the opt-out becomes a reflex again - the reason field exists so every
  // unauthenticated client has to say what makes it safe.
  it('throws when the opt-out is taken without a reason', () => {
    expect(() => new SimClient(DEAD, SimClient.noAuth('   '))).toThrow(/non-empty reason/)
  })

  it('accepts either explicit kind', () => {
    expect(() => new SimClient(DEAD, SimClient.noAuth('a local sim host'))).not.toThrow()
    expect(() => new SimClient(DEAD, { kind: 'oidc', authorization: async () => 'Bearer x' })).not.toThrow()
  })
})

describe('SimClient attaches the ID token, and fails closed without one', () => {
  it('sends Authorization with the minted value, and the sim URL as the audience', async () => {
    const seen: string[] = []
    const auth: SimAuth = {
      kind: 'oidc',
      authorization: async (audience) => { seen.push(audience); return 'Bearer test-id-token' },
    }
    const { calls, impl } = recordingFetch(verifiedBody)

    const verdict = await new SimClient(SIM_URL, auth, impl).simulate('AAAA')

    expect(verdict.kind).toBe('verified')
    expect(calls).toHaveLength(1)
    // Mutation: drop `headers.authorization = ...` from simulate(). Nothing
    // else in the suite notices - every other test runs against a local sim
    // host with no invoker check - and production 403s on every submission.
    expect(headerOf(calls[0]!.init, 'authorization')).toBe('Bearer test-id-token')
    expect(headerOf(calls[0]!.init, 'content-type')).toBe('application/json')
    // Cloud Run validates `aud` against the RECEIVING service's URL. Mutate
    // the argument to a literal, to the path, or to sim's hostname without
    // the scheme and the token is minted for the wrong audience - a 403 that
    // looks identical to having no token at all.
    expect(seen).toEqual([SIM_URL])
  })

  /**
   * THE CENTRAL DEGRADATION REQUIREMENT. A token that cannot be minted is an
   * outage of the verification path:
   *   - NOT a 500. `simulate` must return, not throw - routes/wave.ts turns
   *     `unavailable` into a retryable 503 having consumed nothing, while an
   *     escaping rejection becomes an unhandled 500.
   *   - NOT an unauthenticated retry. `calls` must stay EMPTY. Catching the
   *     mint failure and proceeding without the header would turn a
   *     transient metadata-server blip into a request that quietly drops the
   *     gate - and it would succeed today, because sim's own process checks
   *     nothing.
   *
   * Mutation: move the `await this.auth.authorization(...)` outside the try,
   * and the first expectation fails with the raised error instead. Wrap it
   * in its own try that swallows, and the second fails with one recorded
   * call.
   */
  it('degrades a FAILING token fetch to unavailable, with no unauthenticated call', async () => {
    const { calls, impl } = recordingFetch(verifiedBody)
    const auth: SimAuth = {
      kind: 'oidc',
      authorization: async () => { throw new Error('metadata server: connect ECONNREFUSED 169.254.169.254:80') },
    }

    const verdict = await new SimClient(SIM_URL, auth, impl).simulate('AAAA')

    expect(verdict).toEqual({ kind: 'unavailable' })
    expect(calls).toEqual([])
  })

  /**
   * The same requirement for a mint that HANGS rather than rejects, which is
   * the likelier metadata-server failure. google-auth-library's token fetch
   * accepts no AbortSignal, so the only thing that can bound it is the race
   * in simulate() against the shared budget.
   *
   * Mutation: delete `abortAsRejection(budget)` from the Promise.race and
   * this test hangs until vitest's own timeout kills it - i.e. in
   * production, a wedged metadata server holds a Cloud Run instance and its
   * Postgres connection open indefinitely, which is the failure the 5s
   * budget was put there to prevent in the first place.
   *
   * budgetMs is passed explicitly here so the branch is provable in 25ms
   * instead of 5s; the default is the real 5_000.
   */
  it('degrades a HANGING token fetch to unavailable inside the budget, with no call', async () => {
    const { calls, impl } = recordingFetch(verifiedBody)
    const auth: SimAuth = { kind: 'oidc', authorization: () => new Promise<string>(() => {}) }

    const started = Date.now()
    const verdict = await new SimClient(SIM_URL, auth, impl, 25).simulate('AAAA')

    expect(verdict).toEqual({ kind: 'unavailable' })
    expect(calls).toEqual([])
    expect(Date.now() - started).toBeLessThan(2_000)
  })

  // The opt-out must be genuinely unauthenticated, or the local-sim tests
  // would be exercising a different path from the one they claim to.
  // Mutation: make the `kind === 'oidc'` check unconditional and this fails.
  it('sends NO Authorization header under the explicit opt-out', async () => {
    const { calls, impl } = recordingFetch(verifiedBody)

    const verdict = await new SimClient(SIM_URL, SimClient.noAuth('a local sim host'), impl).simulate('AAAA')

    expect(verdict.kind).toBe('verified')
    expect(headerOf(calls[0]!.init, 'authorization')).toBeUndefined()
  })
})

describe('googleIdTokenAuth caches the client, not the call', () => {
  function fakeAuth(headers: () => Headers) {
    let clientsBuilt = 0
    let headerCalls = 0
    return {
      get clientsBuilt() { return clientsBuilt },
      get headerCalls() { return headerCalls },
      getIdTokenClient: async (_audience: string) => {
        clientsBuilt += 1
        return { getRequestHeaders: async () => { headerCalls += 1; return headers() } }
      },
    }
  }

  const bearer = () => new Headers({ authorization: 'Bearer eyJhbGciOi.payload.sig' })

  /**
   * MEASURED FROM THE INSTALLED LIBRARY, not assumed (google-auth-library
   * 11.0.2): `IdTokenClient.getRequestMetadataAsync` caches the token and
   * refetches only inside the eager-refresh window, but
   * `GoogleAuth.getIdTokenClient` constructs a BRAND NEW client with empty
   * credentials on every call. So calling the factory per request throws the
   * library's cache away and mints a fresh token per wave submission.
   *
   * Mutation: remove the `clients` map (call `auth.getIdTokenClient` inline)
   * and clientsBuilt becomes 3.
   */
  it('builds one client per audience and reuses it across requests', async () => {
    const fake = fakeAuth(bearer)
    const provider = googleIdTokenAuth(fake)
    if (provider.kind !== 'oidc') throw new Error('expected oidc')

    await provider.authorization(SIM_URL)
    await provider.authorization(SIM_URL)
    await provider.authorization(SIM_URL)

    expect(fake.clientsBuilt).toBe(1)
    expect(fake.headerCalls).toBe(3)
  })

  /**
   * Mutation: drop the `.catch(() => { clients.delete(...) })` and the
   * REJECTED promise stays in the map forever. Every later request awaits
   * the same settled rejection, so one transient metadata-server failure at
   * startup makes that instance answer `sim_unavailable` for its entire
   * life, logging nothing after the first - and it never retries, so it
   * never recovers. Without the eviction, the second call below still
   * rejects and `clientsBuilt` stays at 1.
   */
  it('evicts a rejected client so the next request retries instead of failing forever', async () => {
    let attempt = 0
    const flaky = {
      getIdTokenClient: async (_audience: string) => {
        attempt += 1
        if (attempt === 1) throw new Error('metadata server unreachable')
        return { getRequestHeaders: async () => bearer() }
      },
    }
    const provider = googleIdTokenAuth(flaky)
    if (provider.kind !== 'oidc') throw new Error('expected oidc')

    await expect(provider.authorization(SIM_URL)).rejects.toThrow(/metadata server unreachable/)
    await expect(provider.authorization(SIM_URL)).resolves.toBe('Bearer eyJhbGciOi.payload.sig')
    expect(attempt).toBe(2)
  })

  /**
   * Mutation: delete the empty-header guard and `authorization` returns
   * undefined/'' instead of throwing. SimClient would then set a blank
   * Authorization header and Cloud Run would answer 403 with nothing
   * anywhere saying the token was never produced. Throwing routes it into
   * simulate()'s catch, i.e. the same retryable `unavailable`.
   */
  it('throws rather than returning a blank Authorization header', async () => {
    const provider = googleIdTokenAuth(fakeAuth(() => new Headers()))
    if (provider.kind !== 'oidc') throw new Error('expected oidc')

    await expect(provider.authorization(SIM_URL)).rejects.toThrow(/no authorization header/)
  })

  // End to end through SimClient: a provider failure must still be a verdict,
  // never an exception out of simulate().
  it('composes with SimClient so a provider failure is a verdict, not a throw', async () => {
    const { calls, impl } = recordingFetch(verifiedBody)
    const provider = googleIdTokenAuth({
      getIdTokenClient: async () => { throw new Error('metadata server unreachable') },
    })

    const verdict = await new SimClient(SIM_URL, provider, impl).simulate('AAAA')

    expect(verdict).toEqual({ kind: 'unavailable' })
    expect(calls).toEqual([])
  })
})
