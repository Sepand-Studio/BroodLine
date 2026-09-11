import { execFile } from 'node:child_process'
import { readdir, readFile } from 'node:fs/promises'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { promisify } from 'node:util'

const run = promisify(execFile)

/** The locale every other locale is checked against. */
const REFERENCE_LOCALE = 'en'

/**
 * Publish-time validation. solo_execution 5.2: a bundle that fails is not
 * published, and that is the entire safety model - a bad bundle is shipped to
 * every player at once and cannot be recalled by an app update.
 *
 * Three checks, and only one of them lives here in full. The wave rules are
 * the engine's and are invoked, never copied - see tools/config-validate.
 */
export async function validateBundle(dir: string): Promise<string[]> {
  const violations: string[] = []
  violations.push(...(await validateWaves(dir)))
  violations.push(...(await validatePackLadder(dir)))
  violations.push(...(await validateLocales(dir)))
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
  const ladder = [...packs].sort((a, b) => a.priceUsdCents - b.priceUsdCents)

  const violations: string[] = []
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
