import { fileURLToPath } from 'node:url'
import { writeFile } from 'node:fs/promises'
import { OpenAPIRegistry, OpenApiGeneratorV31 } from '@asteasolutions/zod-to-openapi'
import { CreateAccountRequest, CreateAccountResponse, ErrorResponse, SyncResponse } from './schemas.ts'

const registry = new OpenAPIRegistry()

const errors = (codes: number[]) => Object.fromEntries(codes.map((c) => [
  String(c), { description: 'Error', content: { 'application/json': { schema: ErrorResponse } } },
]))

registry.registerPath({
  method: 'post',
  path: '/v1/account',
  operationId: 'createAccount',
  parameters: [{
    name: 'Idempotency-Key', in: 'header', required: true,
    schema: { type: 'string' },
    description: 'Generated when the action is taken, not when it is sent.',
  }],
  request: { body: { content: { 'application/json': { schema: CreateAccountRequest } } } },
  responses: {
    200: { description: 'Created, or replayed', content: { 'application/json': { schema: CreateAccountResponse } } },
    ...errors([400, 422]),
  },
})

registry.registerPath({
  method: 'get',
  path: '/v1/sync',
  operationId: 'sync',
  parameters: [{ name: 'X-Client-Version', in: 'header', required: false, schema: { type: 'string' } }],
  responses: {
    200: { description: 'The cold-start snapshot', content: { 'application/json': { schema: SyncResponse } } },
    ...errors([401, 404, 426]),
  },
})

const document = new OpenApiGeneratorV31(registry.definitions).generateDocument({
  openapi: '3.1.0',
  info: { title: 'Broodline API', version: '1' },
  servers: [{ url: 'https://api.broodline.example' }],
})

// fileURLToPath, not .pathname: this repo lives under a directory containing
// a space, and .pathname percent-encodes it.
const out = fileURLToPath(new URL('../../../openapi/broodline.json', import.meta.url))
// Trailing newline and two-space indent, fixed, because this file is
// committed and CI fails on a non-empty diff - formatting drift would read
// as a contract change.
await writeFile(out, `${JSON.stringify(document, null, 2)}\n`, 'utf8')
console.log(`wrote ${out}`)
