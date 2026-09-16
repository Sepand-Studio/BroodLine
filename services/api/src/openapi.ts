import { fileURLToPath } from 'node:url'
import { writeFile } from 'node:fs/promises'
import { OpenAPIRegistry, OpenApiGeneratorV31 } from '@asteasolutions/zod-to-openapi'
import {
  CreateAccountRequest, CreateAccountResponse, DeleteAccountResponse, ErrorResponse,
  NodeClaimRequest, NodeClaimResponse, RefreshRequest, RefreshResponse,
  RegionStateResponse, RosterResponse, SpliceCommitRequest, SpliceCommitResponse,
  SplicePreviewRequest, SplicePreviewResponse, SyncResponse,
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
    ...errors([400, 401, 404, 409, 429]),
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

// Phase 5, Task 9. Registered the same way Task 6 registered the wave
// routes - a route the Unity client must call and has no generated method
// for is the gap Task 6 was extended to close; this route existed in code
// before it existed here, and that gap does not get to reopen.
registry.registerPath({
  method: 'post',
  path: '/v1/session/refresh',
  operationId: 'refreshSession',
  request: { body: { content: { 'application/json': { schema: RefreshRequest } } } },
  responses: {
    200: { description: 'A fresh access token; the refresh token is unchanged', content: { 'application/json': { schema: RefreshResponse } } },
    ...errors([400, 401]),
  },
})

// Phase 6, Task 5. design 4.3's two map routes, registered in the task that
// adds them - see schemas.ts for why that is stated rather than assumed.
registry.registerPath({
  method: 'get',
  path: '/v1/region/state',
  operationId: 'regionState',
  security: [{ [bearerAuth.name]: [] }],
  responses: {
    200: { description: 'The region, its nodes, and what each has accrued', content: { 'application/json': { schema: RegionStateResponse } } },
    ...errors([401, 404, 500]),
  },
})

// Phase 6, Task 12. The roster listing the phase's done-when needs and no
// earlier task was assigned - see schemas.ts's RosterResponse for why it is
// its own route rather than a field on /v1/region/state. Registered in the
// SAME task that adds it, which is the discipline every route below states.
registry.registerPath({
  method: 'get',
  path: '/v1/roster',
  operationId: 'roster',
  security: [{ [bearerAuth.name]: [] }],
  responses: {
    200: { description: "The player's live creatures, and the Hatchery cap", content: { 'application/json': { schema: RosterResponse } } },
    // 500 FOR THE SAME REASON /v1/region/state DECLARES ONE, and it is the
    // same CALL: both read the Ark and pass its tier to `rosterCap()`, which
    // THROWS for a tier nobody authored rather than guessing a cap, and
    // app.ts's onError turns that into `internal`. Unreachable today - every
    // Ark is pinned to tier 1 - but two routes making opposite claims about
    // one function is what misleads whoever adds the upgrade path.
    //
    // An empty roster is still a 200, not a 404 and not this: a new player
    // owns zero creatures, and that is a correct answer about a player who
    // exists.
    ...errors([401, 404, 500]),
  },
})

registry.registerPath({
  method: 'post',
  path: '/v1/node/claim',
  operationId: 'claimNode',
  security: [{ [bearerAuth.name]: [] }],
  parameters: [{
    name: 'Idempotency-Key', in: 'header', required: true,
    schema: { type: 'string' },
    description: 'Generated when the action is taken, not when it is sent.',
  }],
  request: { body: { content: { 'application/json': { schema: NodeClaimRequest } } } },
  responses: {
    200: { description: 'Shards credited, base stock granted, the node settled', content: { 'application/json': { schema: NodeClaimResponse } } },
    ...errors([400, 401, 404, 409, 422, 500]),
  },
})

// Phase 6, Task 6. design 5.1's forecast route, registered here in the task
// that adds it - the discipline the region routes above state, kept.
registry.registerPath({
  method: 'post',
  path: '/v1/splice/preview',
  operationId: 'splicePreview',
  security: [{ [bearerAuth.name]: [] }],
  // No Idempotency-Key: preview writes nothing and spends nothing. The charge
  // is spent at /v1/splice/commit, which does take one.
  request: { body: { content: { 'application/json': { schema: SplicePreviewRequest } } } },
  responses: {
    200: { description: 'The odds this splice rolls against, and the coverage it destroys', content: { 'application/json': { schema: SplicePreviewResponse } } },
    ...errors([400, 401, 404, 500]),
  },
})

// Phase 6, Task 7. design 5.1's paid half. The FIRST route in this API that
// destroys player property, which is why its error list is long: every
// refusal it can answer with is one the Splice Chamber screen has to be able
// to say out loud before the CTA is tapped (splice_confirm_spec 2).
registry.registerPath({
  method: 'post',
  path: '/v1/splice/commit',
  operationId: 'spliceCommit',
  security: [{ [bearerAuth.name]: [] }],
  parameters: [{
    name: 'Idempotency-Key', in: 'header', required: true,
    schema: { type: 'string' },
    description: 'Generated when the action is taken, not when it is sent.',
  }],
  request: { body: { content: { 'application/json': { schema: SpliceCommitRequest } } } },
  responses: {
    200: { description: 'The child, and the roll that produced it', content: { 'application/json': { schema: SpliceCommitResponse } } },
    ...errors([400, 401, 404, 409, 422, 500]),
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
