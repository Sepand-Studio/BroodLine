import { serve } from '@hono/node-server'
import { createApp } from './app.ts'
import { GcsBundleStore } from './config/gcs-store.ts'
import { createDb, createPool } from './db/client.ts'
import { GcsReplayStore } from './replays/gcs-store.ts'
import { SimClient } from './sim/client.ts'
import { googleIdTokenAuth } from './sim/oidc.ts'

const port = Number(process.env.PORT ?? 8080)

const url = process.env.DATABASE_URL
if (!url) throw new Error('DATABASE_URL must be set.')
const bucket = process.env.CONFIG_BUCKET
if (!bucket) throw new Error('CONFIG_BUCKET must be set.')
// Design 5.1: the player-visible replay collection, a separate bucket from
// CONFIG_BUCKET with its own 30-day lifecycle rule. The bucket itself is
// provisioned by Task 11's Terraform, not this task - so deploying THIS
// revision before Task 11 lands has no bucket to point at, and the
// service crash-loops on boot. That is deliberate, not an oversight:
// design 5.2 treats a RUNTIME replay-store failure as non-critical (a
// missing replay is a degraded viewer, one lost object, logged and
// swallowed - see routes/wave.ts's write-after-withIdempotency), but a
// missing BUCKET means every replay would be silently lost, forever, for
// as long as the misconfiguration stands. A loud crash-loop caught in
// seconds with a one-command rollback beats a quiet deploy that looks
// healthy and drops every replay until someone opens a viewer that does
// not exist yet and finds it empty. Fail hard here on purpose - do not
// make this optional. Sequence deploys accordingly: Task 11 (or later
// infra) before this revision ships.
const replayBucket = process.env.REPLAY_BUCKET
if (!replayBucket) throw new Error('REPLAY_BUCKET must be set.')
// solo_execution 3.1: sim is a second Cloud Run service, internal ingress
// only - this is its address, not a public one.
const simBaseUrl = process.env.SIM_BASE_URL
if (!simBaseUrl) throw new Error('SIM_BASE_URL must be set.')

const app = createApp({
  db: createDb(createPool(url)),
  bundleStore: new GcsBundleStore(bucket),
  // The SECOND of design 3.1's two gates, and the only one that lives in
  // application code. `sim` grants roles/run.invoker to exactly one member -
  // this service account - so every call needs a Google-signed OIDC ID token
  // with sim's URL as its audience or Cloud Run answers 403 before sim runs.
  // There is no default for this argument on purpose; see SimAuth in
  // sim/client.ts for what a defaulted one would silently cost.
  simClient: new SimClient(simBaseUrl, googleIdTokenAuth()),
  replayStore: new GcsReplayStore(replayBucket),
})

serve({ fetch: app.fetch, port }, (info) => {
  console.log(JSON.stringify({ msg: 'listening', port: info.port }))
})
