import { serve } from '@hono/node-server'
import { createApp } from './app.ts'

// Cloud Run sets PORT and it is not negotiable.
const port = Number(process.env.PORT ?? 8080)

serve({ fetch: createApp().fetch, port }, (info) => {
  console.log(JSON.stringify({ msg: 'listening', port: info.port }))
})
