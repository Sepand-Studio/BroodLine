import { GoogleAuth, type IdTokenClient } from 'google-auth-library'
import type { SimAuth } from './client.ts'

/**
 * The PRODUCTION half of SimClient's auth. src/index.ts wires this; nothing
 * else may.
 *
 * `sim` is `INGRESS_TRAFFIC_INTERNAL_ONLY` and grants `roles/run.invoker` to
 * exactly one member - `api`'s service account (infra/terraform/main.tf,
 * `api_invokes_sim`). Cloud Run's invoker check wants a Google-signed OIDC
 * ID TOKEN (not an access token) whose `aud` is the receiving service's URL,
 * presented as `Authorization: Bearer <token>`. On Cloud Run the credential
 * behind it is Application Default Credentials, which resolve to the
 * instance metadata server; nothing here reads a key file and none exists.
 *
 * WHY THE CLIENT IS MEMOISED AND THE TOKEN IS NOT (this was read out of the
 * installed library rather than assumed - google-auth-library 11.0.2,
 * build/src/auth/):
 *
 *   - `IdTokenClient.getRequestMetadataAsync()` (idtokenclient.js) DOES
 *     cache. It calls `fetchIdToken` only when there is no `id_token`, no
 *     `expiry_date`, or `isTokenExpiring()` - the eager-refresh window
 *     defaults to five minutes before `exp`, and a Cloud Run ID token lives
 *     an hour. So per-request `getRequestHeaders()` is a cache read, not a
 *     metadata-server round trip, and there is nothing to memoise here.
 *   - `GoogleAuth.getIdTokenClient()` (googleauth.js:795) does NOT cache. It
 *     `await`s `getClient()` and then constructs a BRAND NEW IdTokenClient,
 *     with empty credentials, every single call. Calling it per request
 *     would therefore throw the cache away each time and mint a fresh token
 *     on every wave submission - the exact opposite of what the caching
 *     above buys. Hence the map: one client per audience, for the life of
 *     the process.
 *
 * A REJECTED client promise is EVICTED rather than left in the map. Storing
 * a rejected promise would make one transient metadata-server failure at
 * startup permanent for the life of the instance: every later request would
 * await the same settled rejection and every wave submission would answer
 * `sim_unavailable` forever, with nothing in the logs after the first. A
 * failure of `getRequestHeaders()` on an already-built client is NOT
 * evicted - the client is fine, only that token fetch failed, and the next
 * call retries it.
 *
 * NOTHING IS PROBED AT BOOT, deliberately. `getIdTokenClient` is a network
 * call; running it in index.ts would put the metadata server on the startup
 * path, where a transient failure is a crash-loop rather than a retry. The
 * degradation without it is the designed one: a token that cannot be minted
 * throws into SimClient.simulate()'s catch and becomes `unavailable`, which
 * routes/wave.ts answers with a retryable 503 having consumed nothing.
 *
 * THE `auth` PARAMETER IS A TEST SEAM AND NOTHING ELSE. It defaults to the
 * real GoogleAuth, so production wiring (`googleIdTokenAuth()`, index.ts)
 * cannot pick up a substitute by omission - unlike SimAuth itself, which has
 * no default because a defaulted one would be a security gate that can go
 * missing. What this narrow interface buys is that the memoisation and the
 * eviction described above are provable without a metadata server; a
 * behaviour that can only be checked by deploying is a behaviour that gets
 * asserted about instead of tested.
 */
export interface IdTokenClientFactory {
  getIdTokenClient(targetAudience: string): Promise<Pick<IdTokenClient, 'getRequestHeaders'>>
}

export function googleIdTokenAuth(auth: IdTokenClientFactory = new GoogleAuth()): SimAuth {
  const clients = new Map<string, Promise<Pick<IdTokenClient, 'getRequestHeaders'>>>()

  return {
    kind: 'oidc',
    authorization: async (audience: string): Promise<string> => {
      let client = clients.get(audience)
      if (client === undefined) {
        client = auth.getIdTokenClient(audience).catch((err: unknown) => {
          clients.delete(audience)
          throw err
        })
        clients.set(audience, client)
      }

      // Returns a WHATWG Headers in v11 (authclient.d.ts:145), carrying
      // `authorization: 'Bearer <id_token>'` assembled by the library.
      const headers = await (await client).getRequestHeaders()
      const value = headers.get('authorization')
      if (value == null || value === '') {
        // Never fall through to an unauthenticated request. Throwing here
        // lands in SimClient.simulate()'s catch and becomes `unavailable`;
        // returning '' would send a blank header and read as a 403 from
        // Cloud Run with no clue as to why.
        throw new Error(`google-auth-library returned no authorization header for audience ${audience}`)
      }
      return value
    },
  }
}
