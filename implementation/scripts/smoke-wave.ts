/**
 * Task 11 Step 7 — prove the deployed pair end to end.
 *
 * WHY THIS EXISTS AND WHY IT IS NOT A curl SCRIPT. `sim` has no public URL:
 * design 3.1 puts it on internal ingress behind an invoker token, so there is
 * nothing to health-check directly and that is the point. The ONLY way to
 * prove `sim` is alive, reachable from `api`, and holding the same engine the
 * client built its replay with, is to submit a real replay and read the
 * verdict back. Everything below exists to get to that one call.
 *
 * WHY IT IMPORTS THE TEST HELPER. `buildWinningReplay` owns the replay wire
 * format - magic, format version, length-prefixed engine version, then the
 * deployment table. Re-implementing that here would create a second copy that
 * goes stale the first time the format moves, and the failure would look like
 * a broken deployment rather than a stale script. One source of truth, even
 * though it means a production smoke test importing from test/.
 *
 * WHY NOT implementation/results/editor-replay.bin. It is engine `0.1.0` and
 * every submission of it is refused as `engine_too_old` - correctly. The
 * tracked artifact proves Phase 3's round-trip, not this.
 *
 * THE PHASE GOAL IS THE LAST ASSERTION, NOT THE FIRST. Phase 5's stated goal
 * is "a tampered submission earns nothing; an honest one pays exactly once
 * under retry". A single winning submission proves the pair is wired. The
 * REPLAY of that submission under the same idempotency key is what proves the
 * ledger guard, and it is the assertion worth having.
 *
 * Run it through smoke-wave.sh, which resolves API_URL from terraform output
 * and checks the replay object landed in the bucket afterwards.
 */
import { randomUUID } from 'node:crypto'
import { buildWinningReplay } from '../../services/api/test/replay-format.ts'

const BASE = (process.env.API_URL ?? '').replace(/\/+$/, '')

// Wave 6 is the only wave the engine authors: WaveDef.ForId returns it and
// throws WaveCompositionException for every other id, which SimulateEndpoint
// maps to rejected/rules_violated. A smoke test on any other wave is refused
// by `sim` before the handler sees it.
const WAVE = 6
const STARTER_SHARDS = 250 // config/bundles/0.1.1/starter.json
const WAVE_REWARD = 40 // config/bundles/0.1.1/waves.json

let step = 0
function ok(msg: string): void {
  console.log(`  ✓ ${msg}`)
}
function fail(msg: string, detail?: unknown): never {
  console.error(`\nFAIL at step ${step}: ${msg}`)
  if (detail !== undefined) {
    console.error(typeof detail === 'string' ? detail : JSON.stringify(detail, null, 2))
  }
  process.exit(1)
}
function begin(label: string): void {
  step += 1
  console.log(`\n[${step}] ${label}`)
}

async function call(
  path: string,
  init: { method?: string; token?: string; idem?: string; body?: unknown } = {},
): Promise<{ status: number; body: any; text: string }> {
  const headers: Record<string, string> = {}
  if (init.body !== undefined) headers['content-type'] = 'application/json'
  if (init.token) headers['authorization'] = `Bearer ${init.token}`
  if (init.idem) headers['idempotency-key'] = init.idem
  let res: Response
  try {
    res = await fetch(`${BASE}${path}`, {
      method: init.method ?? (init.body === undefined ? 'GET' : 'POST'),
      headers,
      body: init.body === undefined ? undefined : JSON.stringify(init.body),
      signal: AbortSignal.timeout(30_000),
    })
  } catch (e) {
    // A connection-level failure here is the deployment, not the request:
    // wrong URL, revision that never started, or ingress refusing us.
    return fail(`${path} did not answer`, e instanceof Error ? e.message : String(e))
  }
  const text = await res.text()
  let body: any
  try {
    body = JSON.parse(text)
  } catch {
    body = undefined
  }
  return { status: res.status, body, text }
}

if (!BASE) {
  console.error('smoke-wave: set API_URL (smoke-wave.sh reads it from terraform output)')
  process.exit(2)
}

console.log(`smoke-wave → ${BASE}`)

// ---------------------------------------------------------------------------
begin('healthz')
// /healthz answers {ok:true} WITHOUT consulting Postgres - deliberately, so the
// startup probe passes before migrations. So a green here proves the revision
// booted and nothing more. It is a precondition, never evidence.
{
  const r = await call('/healthz')
  if (r.status !== 200 || r.body?.ok !== true) {
    fail(`expected 200 {"ok":true}, got ${r.status}`, r.text.slice(0, 400))
  }
  ok('the revision is up (says nothing about the database - see the comment)')
}

// ---------------------------------------------------------------------------
begin('create an account')
let token: string
{
  const r = await call('/v1/account', {
    idem: randomUUID(),
    body: { birthdateBand: 'adult', storefrontRegion: 'us-central1' },
  })
  if (r.status !== 200) {
    // This is the first call that touches Postgres. A 500 here, with healthz
    // green above, almost always means the schema was never migrated - see
    // the followups file section 12 on the public-IP window.
    fail(`create account returned ${r.status} (healthz was green, so suspect the schema)`, r.text.slice(0, 600))
  }
  if (typeof r.body?.accessToken !== 'string') fail('no accessToken in the response', r.body)
  token = r.body.accessToken
  const shards = r.body?.balances?.shards
  if (shards !== STARTER_SHARDS) {
    fail(`starter grant is ${shards}, expected ${STARTER_SHARDS} - is bundles/current pointing at 0.1.1?`, r.body?.balances)
  }
  ok(`account created, starter grant ${shards} shards`)
}

// ---------------------------------------------------------------------------
begin('claim a node (arms the harvest clock, and may grant base stock)')
let creatureId: string | undefined
{
  // Claiming also ARMS THE CLOCK. `harvest_positions.last_settled_at` is
  // written only by this route, and an absent row reads as `now`, so until
  // a player claims once nothing accrues at all - see map/claim.ts's
  // loadLastSettled. Slot 1 is the Rich Deposit, the faster of the two.
  const r = await call('/v1/node/claim', {
    token, idem: randomUUID(), body: { slot: 1 },
  })
  if (r.status !== 200) fail(`node/claim returned ${r.status}`, r.text.slice(0, 600))
  const granted = r.body?.creatures
  if (Array.isArray(granted) && granted.length > 0) creatureId = granted[0]?.creatureId
  ok(`claimed slot 1, ${r.body?.shards ?? 0} shards, ${granted?.length ?? 0} creature(s)`)
}

// ---------------------------------------------------------------------------
begin(`start wave ${WAVE}`)
let issuanceId: string
let seed: string
{
  // `deployment` is REQUIRED as of Task 8 (design §6.1) and must carry at
  // least one OWNED creature id as of Task 10's DEPLOYMENT_FLOOR. It used to
  // send `[]`, which the floor rejects unconditionally - so this script
  // failed on every run against a perfectly healthy deploy, and a real sim
  // outage was indistinguishable from that.
  //
  // A fresh account cannot supply one on its own: the starter grant is
  // currency only, and node base stock needs hours of accrual before the
  // first creature drops. So the claim above is the honest attempt, and
  // SMOKE_CREATURE_ID is the way to run the full round trip - the part of
  // this script that actually proves sim is alive - against an account that
  // already holds one.
  creatureId ??= process.env.SMOKE_CREATURE_ID
  if (creatureId === undefined) {
    fail(
      'no creature to deploy, so the wave legs cannot run. This is NOT a sim '
      + 'or api failure: a fresh account holds no creatures, and base stock '
      + 'accrues over hours. Re-run with SMOKE_CREATURE_ID set to a creature '
      + 'owned by an account this script can sign in as, or point the script '
      + 'at an established account.')
  }

  const r = await call('/v1/wave/start', {
    token, body: { waveId: WAVE, deployment: [{ creatureId, pocket: 0 }] },
  })
  if (r.status !== 200) {
    // wave_locked here usually means rewardForWave returned null, i.e. the
    // live bundle has no reward on this wave - the 0.1.0 rollback hazard.
    fail(`wave/start returned ${r.status}`, r.text.slice(0, 600))
  }
  issuanceId = r.body?.issuanceId
  seed = r.body?.seed
  if (!issuanceId || !seed) fail('issuanceId or seed missing', r.body)
  ok(`issuance ${issuanceId}, server-issued seed received`)
}

// ---------------------------------------------------------------------------
begin('submit an honest winning replay')
// The seed is the SERVER's. Building the replay against it is the whole point:
// sim re-simulates from this seed and compares outcomes, so a replay built on
// any other seed is the tampering case, not the honest one.
const replay = buildWinningReplay(WAVE, BigInt(seed))
const idem = randomUUID()
{
  const r = await call('/v1/wave/submit', { token, idem, body: { issuanceId, replay } })
  if (r.status !== 200) {
    // 503 sim_unavailable is the interesting failure: api is fine and sim is
    // not reachable or not answering. That is the OIDC token or the VPC route,
    // which are the two gates 5619d6f added.
    fail(`wave/submit returned ${r.status}`, r.text.slice(0, 600))
  }
  if (r.body?.result !== 'win') fail(`result was "${r.body?.result}", expected "win"`, r.body)
  if (r.body?.reward?.amount !== WAVE_REWARD) {
    fail(`reward was ${JSON.stringify(r.body?.reward)}, expected ${WAVE_REWARD} shards`, r.body)
  }
  ok(`verdict win, reward ${r.body.reward.amount} ${r.body.reward.currency} - sim answered, so it is reachable and authenticated`)
}

// ---------------------------------------------------------------------------
begin('balance reflects exactly one payment')
{
  const r = await call('/v1/sync', { token })
  if (r.status !== 200) fail(`sync returned ${r.status}`, r.text.slice(0, 400))
  const shards = r.body?.balances?.shards
  const expected = STARTER_SHARDS + WAVE_REWARD
  if (shards !== expected) fail(`balance ${shards}, expected ${expected}`, r.body?.balances)
  if ((r.body?.campaign?.highestWaveCleared ?? 0) < WAVE) {
    fail(`highestWaveCleared is ${r.body?.campaign?.highestWaveCleared}, expected >= ${WAVE}`, r.body?.campaign)
  }
  ok(`balance ${shards} = ${STARTER_SHARDS} + ${WAVE_REWARD}, wave ${WAVE} cleared`)
}

// ---------------------------------------------------------------------------
begin('retry the same submission - it must pay exactly once')
// THIS IS THE PHASE GOAL. The issuance is consumed inside the same transaction
// as the credit, so a retry must not find a live issuance to spend again. The
// idempotency key protects the RESPONSE; the issuance protects the LEDGER.
// A balance that moved here means the ledger guard is not holding in the
// deployed configuration, whatever the tests say locally.
{
  const r = await call('/v1/wave/submit', { token, idem, body: { issuanceId, replay } })
  if (r.status !== 200) fail(`retry returned ${r.status}, expected the stored 200`, r.text.slice(0, 600))

  const s = await call('/v1/sync', { token })
  const shards = s.body?.balances?.shards
  const expected = STARTER_SHARDS + WAVE_REWARD
  if (shards !== expected) {
    fail(`DOUBLE PAY: balance ${shards} after retry, expected ${expected}`, s.body?.balances)
  }
  ok(`retry returned the stored response and the balance held at ${shards}`)
}

console.log('\nPASS - an honest submission paid exactly once, and sim was reached to decide it.')
console.log(`\nreplay should now exist in the replay bucket; smoke-wave.sh checks that next.`)
