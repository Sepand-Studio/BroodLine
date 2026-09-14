import { fileURLToPath } from 'node:url'
import { writeFile } from 'node:fs/promises'
import { OpenAPIRegistry, OpenApiGeneratorV31 } from '@asteasolutions/zod-to-openapi'
import {
  CreateAccountRequest, CreateAccountResponse, DeleteAccountResponse, ErrorResponse, SyncResponse,
  WaveStartRequest, WaveStartResponse, WaveSubmitRequest, WaveSubmitResponse,
} from './schemas.ts'

const registry = new OpenAPIRegistry()

// HTTP bearer (JWT). requireSession (http/auth.ts) is what every authenticated
// route actually checks; this is what tells the generated client HOW to send
// what that check expects, which is the piece that was missing before this
// fix - see BroodlineClient.cs for the symptom this was causing.
const bearerAuth = registry.registerComponent('securitySchemes', 'bearerAuth', {
  type: 'http',
  scheme: 'bearer',
  bearerFormat: 'JWT',
})

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
  security: [{ [bearerAuth.name]: [] }],
  parameters: [{ name: 'X-Client-Version', in: 'header', required: false, schema: { type: 'string' } }],
  responses: {
    200: { description: 'The cold-start snapshot', content: { 'application/json': { schema: SyncResponse } } },
    ...errors([401, 404, 426]),
  },
})

registry.registerPath({
  method: 'delete',
  path: '/v1/account',
  operationId: 'deleteAccount',
  security: [{ [bearerAuth.name]: [] }],
  responses: {
    200: { description: 'Deleted', content: { 'application/json': { schema: DeleteAccountResponse } } },
    ...errors([401]),
  },
})

// Added after Task 5's review: this route existed in code but was never
// registered here, so it shipped absent from the client NSwag generates -
// see the phase 5 task-6-brief.md header for the finding.
registry.registerPath({
  method: 'post',
  path: '/v1/wave/start',
  operationId: 'startWave',
  security: [{ [bearerAuth.name]: [] }],
  request: { body: { content: { 'application/json': { schema: WaveStartRequest } } } },
  responses: {
    200: { description: 'The live issuance for this wave', content: { 'application/json': { schema: WaveStartResponse } } },
    ...errors([401, 404, 409, 429]),
  },
})

registry.registerPath({
  method: 'post',
  path: '/v1/wave/submit',
  operationId: 'submitWave',
  security: [{ [bearerAuth.name]: [] }],
  parameters: [{
    name: 'Idempotency-Key', in: 'header', required: true,
    schema: { type: 'string' },
    description: 'Generated when the action is taken, not when it is sent.',
  }],
  request: { body: { content: { 'application/json': { schema: WaveSubmitRequest } } } },
  responses: {
    200: { description: 'The verified outcome, paid at most once', content: { 'application/json': { schema: WaveSubmitResponse } } },
    ...errors([400, 401, 409, 422, 426, 503]),
  },
})

const document = new OpenApiGeneratorV31(registry.definitions).generateDocument({
  openapi: '3.1.0',
  info: { title: 'Broodline API', version: '1' },
  servers: [{ url: 'https://api.broodline.example' }],
})

// fileURLToPath, not .pathname: this repo lives under a directory containing
// a space, and .pathname percent-encodes it.
//
// OPENAPI_OUTPUT_PATH lets generate-contract.sh redirect this write to a
// scratch location so it can move both generated outputs into place
// together, only once NSwag has also succeeded - see that script. Nothing
// else should set this; the default is the committed path.
const out = process.env.OPENAPI_OUTPUT_PATH
  ?? fileURLToPath(new URL('../../../openapi/broodline.json', import.meta.url))
// Trailing newline and two-space indent, fixed, because this file is
// committed and CI fails on a non-empty diff - formatting drift would read
// as a contract change.
await writeFile(out, `${JSON.stringify(document, null, 2)}\n`, 'utf8')
console.log(`wrote ${out}`)
