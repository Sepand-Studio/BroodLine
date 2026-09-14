import { serve } from '@hono/node-server'
import { createApp } from './app.ts'
import { GcsBundleStore } from './config/gcs-store.ts'
import { createDb, createPool } from './db/client.ts'
import { SimClient } from './sim/client.ts'

const port = Number(process.env.PORT ?? 8080)

const url = process.env.DATABASE_URL
if (!url) throw new Error('DATABASE_URL must be set.')
const bucket = process.env.CONFIG_BUCKET
if (!bucket) throw new Error('CONFIG_BUCKET must be set.')
// solo_execution 3.1: sim is a second Cloud Run service, internal ingress
// only - this is its address, not a public one.
const simBaseUrl = process.env.SIM_BASE_URL
if (!simBaseUrl) throw new Error('SIM_BASE_URL must be set.')

const app = createApp({
  db: createDb(createPool(url)),
  bundleStore: new GcsBundleStore(bucket),
  simClient: new SimClient(simBaseUrl),
})

serve({ fetch: app.fetch, port }, (info) => {
  console.log(JSON.stringify({ msg: 'listening', port: info.port }))
})
