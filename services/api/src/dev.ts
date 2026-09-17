import { serve } from '@hono/node-server'
import { existsSync } from 'node:fs'
import { mkdir } from 'node:fs/promises'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { createApp } from './app.ts'
import { publishBundle } from './config/publish.ts'
import { LocalBundleStore } from './config/store.ts'
import { createDb, createPool } from './db/client.ts'
import { LocalReplayStore } from './replays/store.ts'
import { SimClient } from './sim/client.ts'

/**
 * THE LOCAL ENTRYPOINT. `index.ts` is the deployed one and this is not it.
 *
 * Task 17 Step 6 asks for the first hour to be walked in the Editor "against
 * a local `api`... with 0.1.3 published to a LocalBundleStore". Nothing could
 * do that. `index.ts` hard-wires `GcsBundleStore`, `GcsReplayStore` and
 * `googleIdTokenAuth()`, so `pnpm dev` demands two GCS buckets and a
 * Google-signed OIDC token for sim before it will serve a single request -
 * correct for Cloud Run, unrunnable on a laptop. The local halves have
 * existed all along (`LocalBundleStore`, `LocalReplayStore`,
 * `SimClient.noAuth`) but only the test suite ever wired them, inside
 * `beforeAll`, where nothing outside vitest can reach them. This file is that
 * same wiring with a `serve()` on the end.
 *
 * WHY A SECOND ENTRYPOINT RATHER THAN A FLAG IN `index.ts`. A flag would put
 * the local stores in the deployed file's import graph, one environment
 * variable away from a production process serving bundles off a container's
 * ephemeral disk and dropping every replay into /tmp. The deployed path
 * should not be able to express that, so it cannot name these classes at all.
 *
 * NOT DEPLOYABLE, and it refuses rather than trusting the reader: the guard
 * below exits non-zero if anything in the environment looks like Cloud Run.
 * `Dockerfile`/`deploy.sh` run `index.ts`; this file is invoked by hand.
 *
 * WHAT IT DOES NOT DO: create a database, run migrations, seed the `servers`
 * row, or start sim. Those are four processes and a schema, and burying them
 * in an entrypoint would make a failure in any one of them look like the api
 * failing to boot. `docs/local-stack.md` drives them in order; this serves.
 */

// design 3.1 puts sim behind Cloud Run's internal ingress, so the deployed
// client signs every call. A local sim host has nothing in front of it, and
// `noAuth` demands a reason in writing rather than defaulting - see
// SimClient's own doc for what a defaulted auth would silently cost.
const NO_AUTH_REASON = 'local sim host on 127.0.0.1: no Cloud Run in front of it'

// The bundle beats 3-5 are actually played on. 0.1.1/0.1.2 author neither
// starter.json's cold-open pair nor waves 1 and 2, so the FTUE cannot be
// walked on them - founder.test.ts picks 0.1.3 for exactly this reason.
const BUNDLE_VERSION = '0.1.3'

const REPO = fileURLToPath(new URL('../../../', import.meta.url))

// K_SERVICE is set by Cloud Run on every revision; the others are belt and
// braces for a CI runner or a container someone points at a real database.
for (const key of ['K_SERVICE', 'K_REVISION', 'CLOUD_RUN_JOB', 'GAE_ENV']) {
  if (process.env[key]) {
    console.error(`dev.ts refuses to run: ${key} is set, so this is a managed runtime. Deploy src/index.ts.`)
    process.exit(1)
  }
}

const port = Number(process.env.PORT ?? 8080)
const url = process.env.DATABASE_URL
if (!url) throw new Error('DATABASE_URL must be set. See docs/local-stack.md.')
// 5999, NOT one of test/preflight.ts's SIM_PORTS (5199-5899). Those are
// reserved one-per-test-file, and preflight REFUSES TO START THE SUITE if
// any is held - correctly, since a squatter would otherwise surface as an
// unexplained "address already in use" inside one file. A dev stack left
// running all afternoon on 5699 would block `pnpm test` with a message about
// founder.test.ts, so it lives outside the range instead.
const simBaseUrl = process.env.SIM_BASE_URL ?? 'http://127.0.0.1:5999'
// One tree holds both stores, the way the test suite's single `bundleRoot`
// does. It is scratch: delete it and the next start republishes.
const root = process.env.LOCAL_STORE_ROOT ?? join(REPO, '.local-stack')

if (!process.env.JWT_SECRET || process.env.JWT_SECRET.length < 32) {
  throw new Error('JWT_SECRET must be set and at least 32 characters. See docs/local-stack.md.')
}

await mkdir(root, { recursive: true })
const bundleStore = new LocalBundleStore(root)

// PUBLISH IS IMMUTABLE AND THROWS ON A REPUBLISH, so this is conditional
// rather than unconditional - a second `pnpm dev` must not die on a bundle
// the first one already wrote. The POINTER is set every time regardless:
// `loadBundle` reads it on every cold start, and a store with a published
// bundle and no pointer 500s on the first request.
if (!(await bundleStore.hasBundle(BUNDLE_VERSION))) {
  const seed = join(REPO, 'config/bundles', BUNDLE_VERSION)
  if (!existsSync(seed)) throw new Error(`No bundle to seed from at ${seed}.`)
  await publishBundle(bundleStore, seed, BUNDLE_VERSION)
  console.log(JSON.stringify({ msg: 'bundle published', version: BUNDLE_VERSION, root }))
}
await bundleStore.setPointer(BUNDLE_VERSION)

const app = createApp({
  db: createDb(createPool(url)),
  bundleStore,
  simClient: new SimClient(simBaseUrl, SimClient.noAuth(NO_AUTH_REASON)),
  replayStore: new LocalReplayStore(root),
})

serve({ fetch: app.fetch, port }, (info) => {
  console.log(JSON.stringify({
    msg: 'listening (LOCAL DEV - local bundle/replay stores, unauthenticated sim)',
    port: info.port, sim: simBaseUrl, bundle: BUNDLE_VERSION, root,
  }))
})
