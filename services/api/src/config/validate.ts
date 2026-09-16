import { execFile } from 'node:child_process'
import { readdir, readFile } from 'node:fs/promises'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { promisify } from 'node:util'
import { currency as currencyEnum } from '../db/schema.ts'
import { REQUIRED_NODE_IDS } from '../map/rotation.ts'

const run = promisify(execFile)

/** The locale every other locale is checked against. */
const REFERENCE_LOCALE = 'en'

/** The Postgres enum, not a value reimplemented here - see validateStarterGrants. */
const VALID_CURRENCIES: readonly string[] = currencyEnum.enumValues

/** Matches isBelow's assumption in http/auth.ts: three dot-separated integers. */
const VERSION_PATTERN = /^\d+\.\d+\.\d+$/

/**
 * Publish-time validation. solo_execution 5.2: a bundle that fails is not
 * published, and that is the entire safety model - a bad bundle is shipped to
 * every player at once and cannot be recalled by an app update.
 *
 * Eight checks now. Wave *shape* rules are the engine's and are invoked, never
 * copied - see tools/config-validate. Reward completeness is different: Design
 * 2.2 pays a wave's reward by looking it up from the bundle - rewardForWave in
 * wave/rewards.ts, called from routes/wave.ts's submit handler - so a wave
 * with no reward, or a reward of zero or less, is a payout path that would
 * refuse every clear of that wave (wave_locked) instead of paying it, and
 * only at claim time rather than at publish time. That is a completeness
 * check on authored content, not a game rule, so it lives here rather than
 * in the C# CLI.
 *
 * starter.json and manifest.json carry that exact same blast radius -
 * config/bundle.ts feeds their contents straight to routes/account.ts's
 * credit() call and routes/sync.ts's isBelow() call with nothing between
 * this validator and production - so both are checked here alongside waves,
 * the pack ladder, wave rewards and locales.
 *
 * Phase 6 adds three more, all authored-content completeness checks in the
 * same sense as reward completeness above - none is a game rule, so none
 * belongs in the C# CLI:
 *
 *   - trait dominance (validateTraitDominance): design 5.3 says the rolled
 *     slot in a splice carries at full coverage if the trait is dominant and
 *     one tier lower if recessive - spliceDistribution (a later task) reads
 *     that flag directly off the bundle. A trait authored with no `dominant`
 *     makes that roll undefined on a paid action.
 *   - wave reward distinctness (validateWaveRewardDistinctness): guards
 *     test/weakenings.md row 5, not a general content rule. Phase 5 could
 *     not construct the reward-inflation weakening because only one wave was
 *     authored; this stops a future bundle from quietly re-creating that
 *     one-wave condition by giving two authored waves equal rewards.
 *   - node rates AND node identity (validateNodeRates): map/accrual.ts's
 *     accrue() (Task 4) converts a node's ratePerHour to a BigInt, so a
 *     non-integer rate throws a cryptic RangeError deep inside a claim
 *     instead of failing here. The identity half is sharper still - a
 *     nodes.json missing either id map/rotation.ts's `nodesFor` requires
 *     500s GET /v1/region/state and the claim route for every player on the
 *     server. See that function for both findings.
 */
export async function validateBundle(dir: string): Promise<string[]> {
  const violations: string[] = []
  violations.push(...(await validateWaves(dir)))
  violations.push(...(await validatePackLadder(dir)))
  violations.push(...(await validateWaveRewards(dir)))
  violations.push(...(await validateWaveRewardDistinctness(dir)))
  violations.push(...(await validateLocales(dir)))
  violations.push(...(await validateStarterGrants(dir)))
  violations.push(...(await validateManifest(dir)))
  violations.push(...(await validateTraitDominance(dir)))
  violations.push(...(await validateNodeRates(dir)))
  return violations
}

async function validateWaves(dir: string): Promise<string[]> {
  try {
    // NOTE: no `--nologo` here. That is not a `dotnet run` option - it gets
    // forwarded past `--` to the app itself, which then sees two arguments,
    // prints usage to stderr, and exits 2. That silently fails EVERY bundle
    // ("wave validator could not run") for the wrong reason. The first-run
    // telemetry/logo banner is suppressed via the child env instead.
    const { stdout } = await run('dotnet', [
      'run', '--project', 'tools/config-validate', '--', dir,
    ], {
      cwd: repoRoot(),
      env: { ...process.env, DOTNET_NOLOGO: '1', DOTNET_CLI_TELEMETRY_OPTOUT: '1' },
    })
    return (JSON.parse(lastJsonLine(stdout)) as { violations: string[] }).violations
  } catch (err) {
    // Exit 1 means violations, and execFile rejects on a non-zero exit - the
    // payload is still on stdout, so a rejection is not automatically a
    // failure of the validator itself.
    const e = err as { stdout?: string }
    if (typeof e.stdout === 'string' && e.stdout.includes('"violations"')) {
      return (JSON.parse(lastJsonLine(e.stdout)) as { violations: string[] }).violations
    }
    return [`wave validator could not run: ${String(err)}`]
  }
}

/** `dotnet run` may print build output before the JSON; take the last line that parses. */
function lastJsonLine(stdout: string): string {
  const lines = stdout.trim().split('\n')
  for (let i = lines.length - 1; i >= 0; i--) {
    const line = lines[i]!.trim()
    if (line.startsWith('{')) return line
  }
  throw new Error(`validator produced no JSON:\n${stdout}`)
}

function repoRoot(): string {
  // services/api/src/config -> repo root. fileURLToPath (not .pathname) so a
  // space anywhere in the path - as in this very repo's parent directory -
  // is decoded rather than left as a literal %20.
  return fileURLToPath(new URL('../../../../', import.meta.url))
}

interface WaveReward { currency: string; amount: number }
interface AuthoredWave { id: number; reward?: WaveReward }

/**
 * Task 5 gave wave 6 a `reward` (services/api/src/config/bundle.ts) because
 * rewardForWave (wave/rewards.ts) pays a wave's reward by looking it up from
 * the bundle at claim time. This is what makes it mandatory: an authored wave
 * with no reward, or a reward of zero or less, is not a content oversight -
 * it is a payout path that refuses every clear of that wave with wave_locked
 * instead of paying it (routes/wave.ts). See validateBundle's doc comment for
 * why this check lives here and not in tools/config-validate.
 */
async function validateWaveRewards(dir: string): Promise<string[]> {
  const raw = await readFile(join(dir, 'waves.json'), 'utf8').catch(() => null)
  if (raw === null) return ['waves.json is missing.']

  const waves = JSON.parse(raw) as AuthoredWave[]
  const violations: string[] = []

  for (const wave of waves) {
    if (wave.reward === undefined) {
      violations.push(`Wave ${wave.id} has no reward.`)
    } else if (!Number.isFinite(wave.reward.amount) || wave.reward.amount <= 0) {
      violations.push(
        `Wave ${wave.id} has a reward of ${wave.reward.amount}, which must be a positive amount.`)
    }
  }
  return violations
}

/**
 * test/weakenings.md row 5's guard, not a general rule about content. Row 5
 * records a reward-inflation weakening ("read the reward from the wrong
 * wave") that Phase 5 could not construct because only wave 6 was authored -
 * there was no second reward for a misattributed lookup to pay out. Once a
 * second authored wave exists, that proof is only meaningful if the two
 * rewards actually differ: a bundle that quietly gave two waves the same
 * reward would make the weakening's assertion pass for a reason that has
 * nothing to do with the guard it is meant to exercise.
 *
 * Waves with no reward are skipped here - validateWaveRewards above already
 * rejects those on their own terms, and folding them into this Set would
 * report a confusing second violation for the same missing field.
 */
async function validateWaveRewardDistinctness(dir: string): Promise<string[]> {
  const raw = await readFile(join(dir, 'waves.json'), 'utf8').catch(() => null)
  if (raw === null) return ['waves.json is missing.']

  const waves = JSON.parse(raw) as AuthoredWave[]
  // BOTH SIDES FILTERED. The Set was built over the waves that carry a
  // reward while the count below was taken over all of them, so a two-wave
  // bundle missing one reward reported this violation - about reward
  // distinctness - on top of validateWaveRewards' correct "Wave N has no
  // reward", pointing the author at the wrong fix. That is precisely the
  // confusing second violation the docstring above says skipping them
  // avoids; skipping them only avoids it if the length is filtered too.
  const rewarded = waves.filter((w) => w.reward !== undefined)
  const rewards = new Set(rewarded.map((w) => `${w.reward!.currency}:${w.reward!.amount}`))
  if (rewarded.length > 1 && rewards.size < 2) {
    return ['authored waves must carry at least two distinct reward values']
  }
  return []
}

interface Pack { id: string; priceUsdCents: number; value: number }

/**
 * broodline_monetization.md's monotonic ladder: value per dollar must never
 * DECREASE as pack size rises. solo_execution 5.2 notes this was broken once
 * by hand, which is the argument for the machine owning it.
 *
 * A pure data property with no game semantics, so it belongs on this side of
 * the language boundary.
 */
async function validatePackLadder(dir: string): Promise<string[]> {
  const raw = await readFile(join(dir, 'packs.json'), 'utf8').catch(() => null)
  if (raw === null) return ['packs.json is missing.']

  const packs = (JSON.parse(raw) as { packs: Pack[] }).packs

  const violations: string[] = []
  for (const pack of packs) {
    // A non-positive price breaks the rate arithmetic below in two different
    // ways depending on `value` (see Program.cs's followups and task-7-brief),
    // and a free pack is a store concept the ladder has no opinion about - so
    // it is guarded out of the rate comparison entirely rather than ranked
    // within it. `<= 0`, not `< 0`: zero is the case that breaks the division.
    if (!Number.isFinite(pack.priceUsdCents) || pack.priceUsdCents <= 0) {
      violations.push(
        `Pack '${pack.id}' has a non-positive priceUsdCents; the ladder is undefined for it.`)
    }
  }
  if (violations.length > 0) return violations

  const ladder = [...packs].sort((a, b) => a.priceUsdCents - b.priceUsdCents)
  for (let i = 1; i < ladder.length; i++) {
    const prev = ladder[i - 1]!
    const curr = ladder[i]!
    const prevRate = prev.value / prev.priceUsdCents
    const currRate = curr.value / curr.priceUsdCents
    if (currRate < prevRate) {
      violations.push(
        `Pack ladder is not monotonic: '${curr.id}' gives ${currRate.toFixed(4)} per cent ` +
        `but the cheaper '${prev.id}' gives ${prevRate.toFixed(4)}.`)
    }
  }
  return violations
}

/**
 * Every key present in the reference locale must exist in every other locale
 * in the bundle - broodline_localization.md. A missing key renders as a raw
 * identifier on a player's screen.
 */
async function validateLocales(dir: string): Promise<string[]> {
  const localeDir = join(dir, 'locales')
  const files = await readdir(localeDir).catch(() => null)
  if (files === null) return ['locales/ is missing.']

  const reference = `${REFERENCE_LOCALE}.json`
  if (!files.includes(reference)) return [`locales/${reference} is missing.`]

  const refKeys = Object.keys(JSON.parse(await readFile(join(localeDir, reference), 'utf8')) as object)
  const violations: string[] = []

  for (const file of files.filter((f) => f.endsWith('.json') && f !== reference)) {
    const keys = new Set(Object.keys(JSON.parse(await readFile(join(localeDir, file), 'utf8')) as object))
    const missing = refKeys.filter((k) => !keys.has(k))
    if (missing.length > 0) {
      violations.push(`locales/${file} is missing ${missing.length} key(s): ${missing.slice(0, 5).join(', ')}`)
    }
  }
  return violations
}

/**
 * config/bundle.ts hands `starter.grants` straight to routes/account.ts,
 * which feeds `grant.currency` and `grant.amount` directly into
 * money/ledger.ts's credit() on every single account creation. Nothing
 * between this validator and that call site checks either field:
 *
 *   - a currency outside the Postgres `currency` enum fails the row's
 *     `::currency` cast with 22P02 (invalid_text_representation) - a 500 on
 *     every account creation, forever, until the bundle is rolled back;
 *   - a negative or non-integer amount fails the wallet's
 *     `CHECK (balance >= 0)` (or is simply not a whole number of currency
 *     units) - also a 500 on every account creation.
 */
async function validateStarterGrants(dir: string): Promise<string[]> {
  const raw = await readFile(join(dir, 'starter.json'), 'utf8').catch(() => null)
  if (raw === null) return ['starter.json is missing.']

  const grants = (JSON.parse(raw) as { grants: Array<{ currency: string; amount: number }> }).grants
  const violations: string[] = []

  for (const grant of grants) {
    if (!VALID_CURRENCIES.includes(grant.currency)) {
      violations.push(
        `starter.json grants an unrecognized currency '${grant.currency}' ` +
        `(must be one of ${VALID_CURRENCIES.join(', ')}).`)
    }
    if (!Number.isInteger(grant.amount) || grant.amount < 0) {
      violations.push(
        `starter.json grant for '${grant.currency}' has an invalid amount ${grant.amount} ` +
        `(must be a non-negative integer).`)
    }
  }
  return violations
}

/**
 * config/bundle.ts reads `manifest.minimumClientVersion` and hands it
 * straight to routes/sync.ts, which passes it as the second argument to
 * http/auth.ts's isBelow(). A missing field arrives there as `undefined`;
 * isBelow calls `.split('.')` on its `floor` parameter with no guard, so a
 * missing minimumClientVersion throws a TypeError on GET /v1/sync - the ONE
 * cold-start call every client makes, so this fails every request, not a
 * fraction of them.
 */
async function validateManifest(dir: string): Promise<string[]> {
  const raw = await readFile(join(dir, 'manifest.json'), 'utf8').catch(() => null)
  if (raw === null) return ['manifest.json is missing.']

  const manifest = JSON.parse(raw) as { minimumClientVersion?: unknown }
  if (manifest.minimumClientVersion === undefined) {
    return ['manifest.json is missing minimumClientVersion.']
  }
  if (typeof manifest.minimumClientVersion !== 'string' || !VERSION_PATTERN.test(manifest.minimumClientVersion)) {
    return [
      `manifest.json's minimumClientVersion '${String(manifest.minimumClientVersion)}' is not a valid ` +
      `MAJOR.MINOR.PATCH version.`,
    ]
  }
  return []
}

interface AuthoredTrait { id: string; dominant?: unknown }

/**
 * design 5.3: the rolled slot in a splice carries at full coverage if the
 * trait is dominant and one tier lower if recessive - spliceDistribution (a
 * later task) reads `bundle.traitById(t.trait).dominant` directly off this
 * file. A trait authored with no `dominant` flag makes that roll undefined,
 * and an undefined roll on a paid action (splicing spends splice_charges) is
 * the failure this check exists to make unshippable.
 *
 * As of Phase 6 the bundle's `dominant` values are themselves provisional -
 * bible 2.2 adopts dominance as a mechanic but assigns it to no trait in the
 * authored set, and the flags in config/bundles/0.1.2/traits.json are owed
 * to combat_numbers 4 for ratification. This check only enforces that the
 * flag is PRESENT and boolean, not any particular value.
 */
async function validateTraitDominance(dir: string): Promise<string[]> {
  const raw = await readFile(join(dir, 'traits.json'), 'utf8').catch(() => null)
  if (raw === null) return ['traits.json is missing.']

  const traits = (JSON.parse(raw) as { traits: AuthoredTrait[] }).traits
  const violations: string[] = []

  for (const trait of traits) {
    if (typeof trait.dominant !== 'boolean') {
      violations.push(`trait ${trait.id}: dominance flag is required`)
    }
  }
  return violations
}

interface AuthoredNode { id: string; ratePerHour: unknown; totalYield: unknown }

/**
 * map/accrual.ts's accrue() (Task 4) does `BigInt(a.ratePerHour)` - a
 * fractional rate throws `RangeError: The number 20.5 cannot be converted
 * to a BigInt because it is not an integer`, deep inside a claim, instead of
 * failing here as a config problem, at publish time, where it is cheap and
 * legible (solo_execution 5.2's whole argument for this file). `totalYield`
 * feeds `remaining` the same way and is checked for the same reason.
 *
 * THE SECOND CHECK IS THE ONE THAT MATTERS MOST, and it was the Task 4
 * carry-forward nobody closed: map/rotation.ts's `nodesFor` looks the two
 * REQUIRED_NODE_IDS up with a non-null assertion, and map/claim.ts's
 * `nodeSet` guards only `nodes.length === 0`. So a nodes.json carrying one
 * typo'd id - `rich_desposit` - passed every check here and then threw
 * `TypeError: Cannot read properties of undefined (reading 'ratePerHour')`
 * out of `nodesFor`: a 500 on GET /v1/region/state and on the claim route,
 * for EVERY player on the server, until someone rolled the bundle back.
 * Two tasks each assumed the other had this. Publish time is where it dies,
 * because a published bundle reaches every player at once and cannot be
 * recalled by an app update.
 *
 * The id set is IMPORTED from rotation.ts rather than restated here, for the
 * reason VALID_CURRENCIES is read off the Postgres enum: a set written down
 * twice is a set that drifts, and this check exists precisely because two
 * places disagreed about it.
 *
 * nodes.json is OPTIONAL here, deliberately: a bundle that authors no map is
 * not a broken bundle - config/bundle.ts's loadBundle reads nodes.json
 * through a catch and leaves `nodes` empty when it is absent, and claim.ts's
 * `nodeSet` answers "no region" rather than an empty one. No fixture in this
 * suite carries a nodes.json, and every one of them must keep passing. A
 * bundle that is supposed to ship one but omits it is not this check's
 * problem to catch; a bundle that ships one and gets it WRONG is.
 */
async function validateNodeRates(dir: string): Promise<string[]> {
  const raw = await readFile(join(dir, 'nodes.json'), 'utf8').catch(() => null)
  if (raw === null) return []

  const nodes = JSON.parse(raw) as AuthoredNode[]
  const violations: string[] = []

  const authored = new Set(nodes.map((n) => n.id))
  for (const required of REQUIRED_NODE_IDS) {
    if (!authored.has(required)) {
      violations.push(
        `nodes.json authors no node with id '${required}'. map/rotation.ts's ` +
        `nodesFor requires it; without it GET /v1/region/state and the claim ` +
        `route answer 500 for every player on the server.`)
    }
  }

  for (const node of nodes) {
    if (typeof node.ratePerHour !== 'number' || !Number.isInteger(node.ratePerHour) || node.ratePerHour <= 0) {
      violations.push(
        `nodes.json node '${node.id}' has a ratePerHour of ${String(node.ratePerHour)}, ` +
        `which must be a positive integer.`)
    }
    if (node.totalYield !== null
        && (typeof node.totalYield !== 'number' || !Number.isInteger(node.totalYield) || node.totalYield <= 0)) {
      violations.push(
        `nodes.json node '${node.id}' has a totalYield of ${String(node.totalYield)}, ` +
        `which must be null or a positive integer.`)
    }
  }
  return violations
}
