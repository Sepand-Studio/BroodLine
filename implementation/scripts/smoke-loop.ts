/**
 * Task 17 Step 4 — drive design §1's LOOP against the deployed stack.
 *
 * NEVER RUN. Nothing is deployed as this is written: `terraform output -raw
 * api_url` is empty, there is no Cloud Run service, and Step 5 (which runs
 * it) needs the `terraform apply` Step 1 has not been authorised to do. What
 * follows is derived by reading `services/api/src` route by route, not by
 * watching it pass. Said here rather than only in the report, because a smoke
 * script that has never smoked anything is the one kind of script whose
 * header must say so.
 *
 * WHAT THIS IS THAT `smoke-wave.ts` IS NOT. That script proves the deployed
 * PAIR: one wave, submitted, verified by `sim`, paid once under retry. It is
 * a proof about two services. This is a proof about the GAME - design §1's
 * loop, which is harvest a node, splice two creatures into one, fight a wave,
 * get paid. Four verbs, and until all four run against one account in one
 * session nobody has shown a player can reach the second one.
 *
 * WHY IT IMPORTS THE TEST HELPER, for `smoke-wave.ts`'s reason verbatim:
 * `replay-format.ts` owns the replay wire format, and a second copy of that
 * byte layout here would go stale the first time the format moved and the
 * failure would look like a broken deployment rather than a stale script.
 * `coldOpenDeployment()` and `wave2TrioDeployment()` are imported for the
 * same reason one step up - they are the two deployments `ftue.test.ts`
 * already drives these waves with, and `wave/submit` compares the deployment
 * `sim` echoed against the one the issuance froze, INDEXED and length-first
 * (routes/wave.ts's `deploymentMatches`), so a hand-rolled deployment here
 * that disagreed by one field would surface as `deployment_mismatch` several
 * beats after the mistake.
 *
 * ---------------------------------------------------------------------------
 * THE PLAN'S STATED ORDER IS NOT REACHABLE, AND THIS IS THE DEVIATION.
 *
 * Task 17 Step 4 writes the flow as: account -> sync -> claim a node -> wait
 * out or fast-forward the harvest -> `POST /v1/ftue/splice-stock` -> preview
 * -> commit -> start wave -> submit. The API refuses that order, and it is
 * right to.
 *
 * `ftue/stock.ts`'s `grantTutorialStock` opens with three gates, and the
 * FIRST is `highestWaveCleared < 2 -> 'Clear wave 2 first.'`. So the splice
 * cannot precede the waves: the tutorial pair is the only pair a fresh
 * account can splice (the cold-open pair is the player's actual roster and
 * `splice_confirm_spec` §6 is explicit that the guided splice spends
 * PROVIDED stock), and it is not handed over until wave 2 is beaten. The
 * waves therefore move in front of the splice, and the loop's last leg is a
 * REPLAY of wave 1 rather than a first clear.
 *
 * That is not a workaround. It is the order `ftue.test.ts` walks and the
 * order design §5's beats are numbered in; the plan's sentence had the
 * splice and the wave the wrong way round.
 *
 * ---------------------------------------------------------------------------
 * THE HARVEST CANNOT BE FAST-FORWARDED, AND NOTHING HERE PRETENDS IT CAN.
 *
 * Step 4 says "wait out or fast-forward the harvest". There is no third
 * option and both of those are unavailable to a script:
 *
 *   - `POST /v1/node/claim` ARMS the clock. `map/claim.ts`'s `loadLastSettled`
 *     reads an absent `harvest_positions` row as `now`, so a fresh account's
 *     FIRST claim settles a zero-length interval and pays exactly zero. That
 *     is correct and it is also the whole of what a fresh claim can do.
 *   - Paying anything needs elapsed wall-clock time, and earning a CREATURE
 *     off a node needs `UNITS_PER_CREATURE` = 480 units: eight hours on the
 *     Rich Deposit at Harvest Array tier 1, twenty-four on the Common Vein.
 *   - The contract exposes thirteen paths (`openapi/broodline.json`) and not
 *     one of them moves a clock. `map/accrual.ts` is a pure function of two
 *     timestamps by design - `solo_execution` §5.5, "never tick players" -
 *     and the only clock it ever sees is the one `routes/region.ts` reads per
 *     request. There is no debug route, no seam, no env var.
 *
 * So the claim below is the honest claim, its payout is READ rather than
 * asserted to be positive, and whatever it pays is folded into the closing
 * arithmetic. Against a fresh account that is zero and the run still proves
 * the node was claimed and the clock armed; against an aged account handed to
 * it by `SMOKE_TOKEN` it is a real credit with a real `node_claim` ledger row,
 * and the assertions below accommodate both without being weakened for either.
 *
 * Run it through smoke-loop.sh, which resolves API_URL from terraform output
 * and asserts the ledger rows and the replay objects afterwards.
 */
import { randomUUID } from 'node:crypto'
import { writeFileSync } from 'node:fs'
import {
  buildReplayOf, coldOpenDeployment, SPECIES, wave2TrioDeployment,
} from '../../services/api/test/replay-format.ts'

const BASE = (process.env.API_URL ?? '').replace(/\/+$/, '')

// config/bundles/0.1.3. NOT 0.1.1, which is what smoke-wave.ts reads against:
// 0.1.1 authors neither starter.json's cold-open pair nor waves 1 and 2, so
// the loop cannot be walked on it at all (src/dev.ts's BUNDLE_VERSION comment
// records the same constraint for the same reason).
const STARTER_SHARDS = 250
const STARTER_CHARGES = 3
const WAVE_1_REWARD = 150
const WAVE_2_REWARD = 165

// Slot 1 is the Rich Deposit, the faster of the two nodes bundle 0.1.3
// authors (nodes.json: 60/hour against the Common Vein's 20).
const NODE_SLOT = 1

interface Creature {
  creatureId: string
  species: string
  generation: number
  name: string | null
  isFounder: boolean
  trait1: string
  tier1: number | null
  trait2: string
  tier2: number | null
}

interface Deployed { creatureId: string; pocket: number }

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
  console.error('smoke-loop: set API_URL (smoke-loop.sh reads it from terraform output)')
  process.exit(2)
}

console.log(`smoke-loop → ${BASE}`)

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
let playerId: string
let serverId: number
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
  if (typeof r.body?.playerId !== 'string') fail('no playerId in the response', r.body)
  if (typeof r.body?.serverId !== 'number') fail('no serverId in the response', r.body)
  token = r.body.accessToken
  playerId = r.body.playerId
  serverId = r.body.serverId
  const shards = r.body?.balances?.shards
  const charges = r.body?.balances?.splice_charges
  if (shards !== STARTER_SHARDS || charges !== STARTER_CHARGES) {
    fail(
      `starter grant is ${shards} shards / ${charges} charges, expected `
      + `${STARTER_SHARDS}/${STARTER_CHARGES} - is bundles/current pointing at 0.1.3?`,
      r.body?.balances)
  }
  ok(`account created on server ${serverId}, starter grant ${shards} shards and ${charges} charges`)
}

// ---------------------------------------------------------------------------
begin('sync - the one cold-start call')
{
  const r = await call('/v1/sync', { token })
  if (r.status !== 200) fail(`sync returned ${r.status}`, r.text.slice(0, 400))
  const cleared = r.body?.campaign?.highestWaveCleared ?? 0
  if (cleared !== 0) fail(`a fresh account reports highestWaveCleared ${cleared}, expected 0`, r.body?.campaign)
  // The bundle is NOT incidental. Every reward and every gate below is read
  // out of it, so a run against 0.1.1 would fail three steps later on a wave
  // the bundle does not author, pointing at the wave rather than at the
  // pointer.
  const version = r.body?.config?.bundleVersion
  if (typeof version !== 'string') fail('sync reported no config.bundleVersion', r.body?.config)
  const authored: number[] = (r.body?.config?.waves ?? []).map((w: { id: number }) => w.id)
  for (const needed of [1, 2]) {
    if (!authored.includes(needed)) {
      fail(
        `the live bundle (${version}) does not author wave ${needed}, so the loop cannot be `
        + 'walked on it. Waves 1 and 2 arrive with 0.1.3; publish it and point at it.',
        authored)
    }
  }
  ok(`live bundle ${version}, authoring waves [${authored.join(', ')}], nothing cleared yet`)
}

// ---------------------------------------------------------------------------
begin('the cold-open pair, read off the roster')
let vetchId: string
let emberId: string
{
  const r = await call('/v1/roster', { token })
  if (r.status !== 200) fail(`roster returned ${r.status}`, r.text.slice(0, 400))
  const opened: Creature[] = r.body?.creatures ?? []
  // READ, NOT ASSUMED. `POST /v1/account` does not return the creatures it
  // grants (routes/account.ts returns balances and tokens only), and
  // `GET /v1/roster` orders by creatureId - so neither the response nor the
  // order is a source for which row is which.
  const vetch = opened.find((c) => c.species === 'Vetch')
  const ember = opened.find((c) => c.species === 'Ember')
  if (vetch === undefined || ember === undefined) {
    fail(
      'starter.json must author exactly a Vetch and an Ember - bundle 0.1.3\'s cold-open pair. '
      + 'An empty roster here means the live bundle predates starter.json\'s `creatures` array.',
      opened.map((c) => c.species))
  }
  // The rows AS THE REPLAY WILL CLAIM THEM, pinned before anything else can
  // add a second Vetch or Ember - the tutorial pair below is exactly that.
  // A disagreement caught here beats the same disagreement caught as a
  // `deployment_mismatch` two requests later.
  const minted = [vetch, ember].map((c) => ({
    species: c.species, trait1: c.trait1, tier1: c.tier1, trait2: c.trait2, tier2: c.tier2,
  }))
  const expected = [
    { species: 'Vetch', trait1: 'Taunt', tier1: 1, trait2: 'Carapace', tier2: 1 },
    { species: 'Ember', trait1: 'Splash', tier1: 1, trait2: 'Carapace', tier2: 1 },
  ]
  if (JSON.stringify(minted) !== JSON.stringify(expected)) {
    fail('the rows the server minted are not the pair coldOpenDeployment() claims', { minted, expected })
  }
  // And in that order: `deploymentMatches` is INDEXED, so the order the two
  // creatures are sent in below is part of what is being asked for.
  const claimedOrder = coldOpenDeployment().map((c) => c.species)
  if (JSON.stringify(claimedOrder) !== JSON.stringify([SPECIES.Vetch, SPECIES.Ember])) {
    fail('coldOpenDeployment() no longer claims Vetch then Ember - the order below is wrong', claimedOrder)
  }
  vetchId = vetch.creatureId
  emberId = ember.creatureId
  ok(`Vetch ${vetchId.slice(0, 8)} and Ember ${emberId.slice(0, 8)}, matching the authored pair`)
}

// ---------------------------------------------------------------------------
begin(`harvest: claim node slot ${NODE_SLOT} (the Rich Deposit)`)
// THE FIRST OF THE LOOP'S FOUR VERBS, and the one the API cannot hurry. See
// the header: a fresh account's first claim settles a zero-length interval
// and pays zero, because `loadLastSettled` reads an absent row as `now`. That
// is the claim ARMING the clock, which is the only thing a claim can do on
// the session it is first made.
let harvestShards = 0
{
  const r = await call('/v1/node/claim', {
    token, idem: randomUUID(), body: { slot: NODE_SLOT },
  })
  if (r.status !== 200) fail(`node/claim returned ${r.status}`, r.text.slice(0, 600))
  if (typeof r.body?.shards !== 'number') fail('node/claim reported no shards', r.body)
  harvestShards = r.body.shards
  const granted: Creature[] = r.body?.creatures ?? []
  ok(`claimed slot ${NODE_SLOT}: ${harvestShards} shards, ${granted.length} creature(s)`)
  if (harvestShards === 0) {
    console.log(
      '    (zero, and that is the CORRECT answer for a first claim: the clock is armed here, '
      + 'not settled. Eight hours on this node buy one creature and there is no route that '
      + 'moves a clock - see the header.)')
  }
}

// ---------------------------------------------------------------------------
/**
 * One wave, played honestly: start with a deployment of OWNED creatures and
 * submit a replay of exactly those creatures, in the same order.
 *
 * The two arguments are two descriptions of one thing and they MUST agree -
 * `deploymentMatches` compares what `sim` echoed against what the issuance
 * froze, indexed and length-first. Taking both rather than deriving one from
 * the other is `ftue.test.ts`'s shape and for its reason: deriving would make
 * every submission honest by construction, and honesty is the thing being
 * demonstrated rather than assumed.
 */
async function playWave(
  waveId: number, deployment: Deployed[], claimed: ReturnType<typeof coldOpenDeployment>,
): Promise<{ result: string; reward?: { currency: string; amount: number }; granted: Creature[]; issuanceId: string }> {
  const start = await call('/v1/wave/start', { token, body: { waveId, deployment } })
  if (start.status !== 200) {
    // wave_locked here is worth reading twice: `issueWave`'s forward branch
    // only ever issues the SMALLEST authored wave id past what is cleared, so
    // a refusal usually means the campaign is not where this script thinks it
    // is - not that the wave is broken.
    fail(`wave/start(${waveId}) returned ${start.status}`, start.text.slice(0, 600))
  }
  const issuanceId = start.body?.issuanceId
  const seed = start.body?.seed
  if (typeof issuanceId !== 'string' || typeof seed !== 'string') {
    fail(`wave/start(${waveId}) returned no issuanceId or seed`, start.body)
  }
  const replay = buildReplayOf(waveId, BigInt(seed), claimed)
  const sub = await call('/v1/wave/submit', {
    token, idem: randomUUID(), body: { issuanceId, replay },
  })
  if (sub.status !== 200) {
    // 503 sim_unavailable is the interesting failure: api is fine and sim is
    // not reachable or not answering. That is the OIDC token or the VPC route.
    fail(`wave/submit(${waveId}) returned ${sub.status}`, sub.text.slice(0, 600))
  }
  return {
    result: sub.body?.result,
    reward: sub.body?.reward,
    granted: sub.body?.granted ?? [],
    issuanceId,
  }
}

function expectWin(waveId: number, played: { result: string; reward?: { currency: string; amount: number } }, amount: number): void {
  if (played.result !== 'Win') fail(`wave ${waveId} came back "${played.result}", expected "Win"`, played)
  if (played.reward?.currency !== 'shards' || played.reward?.amount !== amount) {
    fail(`wave ${waveId} paid ${JSON.stringify(played.reward)}, expected ${amount} shards`, played)
  }
}

const issuanceIds: string[] = []

// ---------------------------------------------------------------------------
begin('wave 1, with the cold-open pair - and the Founder it grants')
const pairDeployed: Deployed[] = [
  { creatureId: vetchId, pocket: 0 }, { creatureId: emberId, pocket: 2 },
]
let founderId: string
{
  const played = await playWave(1, pairDeployed, coldOpenDeployment())
  expectWin(1, played, WAVE_1_REWARD)
  issuanceIds.push(played.issuanceId)
  // THE COUNT ALONE CANNOT SEE THIS. `WAVE_BASE_STOCK` is 1 whether the drop
  // is the Founder or a roll, so a grant of one says nothing about which -
  // and design §5 beat 3 makes the FIRST completion's drop the Founder.
  const founder = played.granted.find((c) => c.isFounder)
  if (founder === undefined) {
    fail('the first wave completion must grant the Founder, not a roll', played.granted)
  }
  if (founder.species !== 'Hollow') fail(`the Founder is a ${founder.species}, expected Hollow`, founder)
  founderId = founder.creatureId
  ok(`Win, ${WAVE_1_REWARD} shards, and the Founder Hollow ${founderId.slice(0, 8)} - sim answered, so it is reachable and authenticated`)
}

// ---------------------------------------------------------------------------
begin('wave 2, with the Founder in the back pocket')
{
  const trioDeployed: Deployed[] = [...pairDeployed, { creatureId: founderId, pocket: 4 }]
  const played = await playWave(2, trioDeployed, wave2TrioDeployment())
  expectWin(2, played, WAVE_2_REWARD)
  issuanceIds.push(played.issuanceId)
  if (played.granted.length !== 1) fail('wave 2 grants exactly one rolled creature', played.granted)
  if (played.granted[0]?.isFounder) {
    fail('a SECOND Founder is the bug the first-completion branch exists to avoid', played.granted)
  }
  ok(`Win, ${WAVE_2_REWARD} shards, one rolled ${played.granted[0]?.species} - the Vetch held the Lash because Taunt is on it`)
}

// ---------------------------------------------------------------------------
begin('the tutorial pair - POST /v1/ftue/splice-stock')
// Its three gates are all now true and none of them is anything a client
// could fabricate: wave 2 cleared by PLAYING it, this player's first splice
// not yet spent, the write-once marker unset. Reaching this step at all is
// the point of the two waves above.
let parentA: Creature
let parentB: Creature
{
  const r = await call('/v1/ftue/splice-stock', { token, method: 'POST', idem: randomUUID() })
  if (r.status !== 200) {
    fail(
      `ftue/splice-stock returned ${r.status}. "Clear wave 2 first." here means the wave legs `
      + 'above reported a win the campaign did not record.',
      r.text.slice(0, 600))
  }
  const stock: Creature[] = r.body?.creatures ?? []
  if (stock.length !== 2) fail('the tutorial pair is TWO creatures', stock)
  const [a, b] = [stock[0], stock[1]]
  if (a === undefined || b === undefined) fail('the tutorial pair is TWO creatures', stock)
  // BY ID, NOT BY SPECIES. The roster now holds two Vetch and two Ember, and
  // picking a parent by species would be free to splice away a starter
  // creature - one of the two the last leg below still has to deploy.
  for (const id of [a.creatureId, b.creatureId]) {
    if (id === vetchId || id === emberId) {
      fail('the tutorial pair must be NEW rows, never the cold-open pair the player earned with', stock)
    }
  }
  parentA = a
  parentB = b
  ok(`granted ${a.species} and ${b.species} as sample stock, distinct from the starters`)
}

// ---------------------------------------------------------------------------
begin('the forecast, before the charge is spent - POST /v1/splice/preview')
// `locked` names a slot on a parent; preview writes nothing and costs
// nothing, which is what makes it safe to call before committing.
const locked = { from: 'a', slot: 'trait_1' }
{
  const r = await call('/v1/splice/preview', {
    token, body: { parentA: parentA.creatureId, parentB: parentB.creatureId, locked },
  })
  if (r.status !== 200) fail(`splice/preview returned ${r.status}`, r.text.slice(0, 600))
  // `mutation: 1` is the whole lesson of the beat - this player's FIRST
  // splice is a guaranteed mutation, which is what makes the first splice
  // show them that a splice changes something.
  if (r.body?.forecast?.mutation !== 1) {
    fail(`forecast.mutation is ${r.body?.forecast?.mutation}, expected 1 on a first splice`, r.body?.forecast)
  }
  if (r.body?.coverageLost === undefined) {
    fail('the confirmation screen is not told what the two unlocked slots cost', r.body)
  }
  ok('mutation 1 guaranteed, and coverageLost published for the confirmation screen')
}

// ---------------------------------------------------------------------------
begin('splice two creatures into one - POST /v1/splice/commit')
// THE SECOND VERB. Net -1 creature and -1 charge, both parents destroyed.
let childId: string
{
  const r = await call('/v1/splice/commit', {
    token, idem: randomUUID(),
    // `bodyFrom` must be one of the two parents' species, READ off the parent
    // rather than named - which is what the client does for want of a body
    // picker.
    body: {
      parentA: parentA.creatureId, parentB: parentB.creatureId,
      locked, bodyFrom: parentA.species,
    },
  })
  if (r.status !== 200) fail(`splice/commit returned ${r.status}`, r.text.slice(0, 600))
  if (r.body?.child?.generation !== 2) {
    fail(`two gen-1 parents must make a gen-2 child, got ${r.body?.child?.generation}`, r.body?.child)
  }
  if (r.body?.balance !== STARTER_CHARGES - 1) {
    fail(`splice charges left ${r.body?.balance}, expected ${STARTER_CHARGES - 1} - design §5.2, one splice one charge`, r.body)
  }
  childId = r.body.child.creatureId
  ok(`child ${childId.slice(0, 8)} at gen 2, ${r.body.balance} charges left, both parents consumed`)
}

// ---------------------------------------------------------------------------
begin('both parents gone from the roster, the child on it')
{
  const r = await call('/v1/roster', { token })
  if (r.status !== 200) fail(`roster returned ${r.status}`, r.text.slice(0, 400))
  const ids: string[] = (r.body?.creatures ?? []).map((c: Creature) => c.creatureId)
  if (!ids.includes(childId)) fail('the child is not on the roster', ids)
  for (const id of [parentA.creatureId, parentB.creatureId]) {
    if (ids.includes(id)) fail('a spliced parent is still on the roster - it must be consumed', ids)
  }
  ok(`${ids.length} creatures, child present, both parents consumed`)
}

// ---------------------------------------------------------------------------
begin('fight a wave and get paid - wave 1, replayed')
// THE LAST TWO VERBS, and the wave is WAVE 1 AGAIN rather than wave 6 - a
// deliberate choice, stated because the alternative is the more obvious one.
//
// After wave 2 the campaign's next forward wave is 6, and wave 6 is one
// Courser down an empty lane whose only answer is Chill. This player has no
// Chill: `ftue/pale.ts` withholds the Pale until a wave-6 LOSS grants one, so
// reaching a wave-6 win takes a designed loss first. That is the right shape
// for `ftue.test.ts`, which is about the first hour. It is the wrong shape
// here, because it would put a deliberate LOSS in the middle of a smoke test
// whose job is to be unambiguous about failure.
//
// Wave 1 replayed is the same proof with none of that: `issueWave`'s replay
// branch allows it (1 <= cleared), the cap is three per wave per day and this
// is the second, `rewardForWave` pays off the ISSUANCE's wave id so it pays
// the full 150 again, and the cold-open pair beats it deterministically.
//
// The splice child is deliberately NOT deployed. Its species and traits are
// ROLLED, so a deployment containing it would make this wave's outcome depend
// on a die - and a smoke test that sometimes loses the wave it claims to win
// is worth less than a smaller claim that always holds.
{
  const played = await playWave(1, pairDeployed, coldOpenDeployment())
  expectWin(1, played, WAVE_1_REWARD)
  issuanceIds.push(played.issuanceId)
  ok(`Win, ${WAVE_1_REWARD} shards - the loop closed`)
}

// ---------------------------------------------------------------------------
begin('shards credited, as the player sees them')
// The HTTP-observable half of "get paid". The DATABASE half - a ledger row
// per mutation, and the wallet agreeing with their sum - is smoke-loop.sh's,
// asserted separately and after, for the reason its header gives.
const expectedShards = STARTER_SHARDS + harvestShards + WAVE_1_REWARD + WAVE_2_REWARD + WAVE_1_REWARD
{
  const r = await call('/v1/sync', { token })
  if (r.status !== 200) fail(`sync returned ${r.status}`, r.text.slice(0, 400))
  const shards = r.body?.balances?.shards
  if (shards !== expectedShards) {
    fail(
      `balance ${shards}, expected ${expectedShards} = ${STARTER_SHARDS} starter + ${harvestShards} `
      + `harvest + ${WAVE_1_REWARD} + ${WAVE_2_REWARD} + ${WAVE_1_REWARD}`,
      r.body?.balances)
  }
  const charges = r.body?.balances?.splice_charges
  if (charges !== STARTER_CHARGES - 1) {
    fail(`splice charges ${charges}, expected ${STARTER_CHARGES - 1}`, r.body?.balances)
  }
  if ((r.body?.campaign?.highestWaveCleared ?? 0) < 2) {
    fail(`highestWaveCleared is ${r.body?.campaign?.highestWaveCleared}, expected >= 2`, r.body?.campaign)
  }
  if (r.body?.ftue?.splices !== 1) {
    fail(`sync reports ${r.body?.ftue?.splices} splices, expected 1`, r.body?.ftue)
  }
  ok(`${shards} shards, ${charges} charges, wave 2 cleared, 1 splice on the books`)
}

// ---------------------------------------------------------------------------
// The handoff to the shell. Every number smoke-loop.sh needs to assert the
// ledger and the replay objects is decided HERE, by the run that made them -
// a shell recomputing the expected row count from constants of its own would
// be a second copy of this arithmetic, free to agree with itself and with
// nothing else.
//
// `node_claim` writes a ledger row ONLY when the claim paid something
// (map/claim.ts credits `units > 0` and reads the balance otherwise), which
// is why the expected rows are built from `harvestShards` rather than listed.
const expectedLedger = [
  { reasonCode: 'STARTER_GRANT', currency: 'splice_charges', delta: STARTER_CHARGES },
  { reasonCode: 'STARTER_GRANT', currency: 'shards', delta: STARTER_SHARDS },
  ...(harvestShards > 0 ? [{ reasonCode: 'node_claim', currency: 'shards', delta: harvestShards }] : []),
  { reasonCode: 'wave:1', currency: 'shards', delta: WAVE_1_REWARD },
  { reasonCode: 'wave:2', currency: 'shards', delta: WAVE_2_REWARD },
  { reasonCode: 'splice', currency: 'splice_charges', delta: -1 },
  { reasonCode: 'wave:1', currency: 'shards', delta: WAVE_1_REWARD },
]

const summaryPath = process.env.SMOKE_LOOP_SUMMARY
if (summaryPath) {
  writeFileSync(summaryPath, JSON.stringify({
    serverId,
    playerId,
    issuanceIds,
    harvestShards,
    expectedShards,
    expectedCharges: STARTER_CHARGES - 1,
    expectedLedger,
  }, null, 2))
}

console.log(
  '\nPASS - the loop closed: a node claimed, two creatures spliced into one, a wave fought, '
  + `and ${expectedShards - STARTER_SHARDS - harvestShards} shards earned across three waves.`)
console.log(
  `\n${expectedLedger.length} ledger rows and ${issuanceIds.length} replay objects should now exist; `
  + 'smoke-loop.sh checks both next.')
