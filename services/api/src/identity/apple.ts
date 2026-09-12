import { createLocalJWKSet, createRemoteJWKSet, jwtVerify, type JSONWebKeySet, type JWTVerifyGetKey } from 'jose'

const APPLE_ISSUER = 'https://appleid.apple.com'
const APPLE_JWKS_URL = 'https://appleid.apple.com/auth/keys'

export interface AppleOpts {
  /** The app's bundle identifier. Apple puts it in `aud`. */
  audience: string
  /** Injected in tests. Production omits it and the remote set is used. */
  jwks?: JSONWebKeySet
}

let remote: JWTVerifyGetKey | undefined
function appleKeys(): JWTVerifyGetKey {
  // createRemoteJWKSet caches and handles rotation, so it is built once per
  // process rather than per request. Apple rotates these without notice.
  remote ??= createRemoteJWKSet(new URL(APPLE_JWKS_URL))
  return remote
}

/**
 * Verifies an Apple identity token and returns the stable user identifier.
 *
 * `sub` is the only field taken. Apple sends the email and name once, at
 * first authorization, and the design does not use either - solo_execution
 * 6.4 makes recovery Apple's problem precisely so there is no email to hold.
 */
export async function verifyAppleToken(token: string, opts: AppleOpts): Promise<{ sub: string }> {
  const keys = opts.jwks ? createLocalJWKSet(opts.jwks) : appleKeys()

  const { payload } = await jwtVerify(token, keys, {
    issuer: APPLE_ISSUER,
    audience: opts.audience,
    // RS256 only. Leaving the algorithm open is how a token signed with
    // `alg: none`, or an HMAC over the public key, gets accepted.
    algorithms: ['RS256'],
  })

  if (typeof payload.sub !== 'string' || payload.sub.length === 0) {
    throw new Error('Apple token carried no subject.')
  }
  return { sub: payload.sub }
}
