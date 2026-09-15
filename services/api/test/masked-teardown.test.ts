import { execFile } from 'node:child_process'
import { fileURLToPath } from 'node:url'
import { promisify } from 'node:util'
import { describe, expect, it } from 'vitest'

const execFileAsync = promisify(execFile)

/**
 * The teardown-masking regression, pinned.
 *
 * Every sim-and-bundle file in this suite assigns `bundleRoot` PARTWAY
 * through beforeAll and removes it in afterAll. `t?.stop()` and
 * `sim?.stop()` were optional-chained; `rm(bundleRoot, ...)` was not. So
 * whenever beforeAll aborted before that assignment - a container that
 * would not start, a sim host that never bound its port, a bundle that
 * failed to publish - afterAll threw
 *
 *     TypeError: The "path" argument must be of type string ...
 *
 * on top of the real error, and that TypeError is what a report skimmed
 * from the tail of the output carries.
 *
 * THIS IS NOT COSMETIC, which is why it gets a test rather than a one-line
 * fix. That exact shape got an intermittent flake in this suite
 * misattributed twice, and three reviews passed over a real bug because the
 * reports they were built from carried vitest's FAIL line and the masking
 * TypeError, never the underlying error.
 *
 * Both halves are asserted. Test one is the guard; test two is the
 * mutation, kept runnable, because "the real cause is visible" is a claim
 * that means nothing unless the unguarded shape is shown to bury it.
 *
 * The fixture is run as a SUBPROCESS with its own config: an aborting
 * beforeAll is precisely what must not be collected into this suite, and
 * vitest offers no `--include` to reach a file outside the real glob.
 */

const API = fileURLToPath(new URL('../', import.meta.url))
const VITEST = fileURLToPath(new URL('../node_modules/.bin/vitest', import.meta.url))
const CONFIG = 'test/fixtures/vitest.fixture.config.ts'

/** Runs the fixture and returns everything it printed. It always fails - that is the point. */
async function runFixture(guarded: boolean): Promise<string> {
  try {
    const { stdout, stderr } = await execFileAsync(VITEST, ['run', '--config', CONFIG], {
      cwd: API,
      env: { ...process.env, BROODLINE_FIXTURE_GUARDED: guarded ? '1' : '0' },
      maxBuffer: 10 * 1024 * 1024,
    })
    return stdout + stderr
  } catch (err) {
    // Non-zero exit is expected: the fixture's beforeAll throws on purpose.
    const e = err as { stdout?: string; stderr?: string }
    return (e.stdout ?? '') + (e.stderr ?? '')
  }
}

describe('an aborted beforeAll', () => {
  it('surfaces its own error, with no teardown TypeError on top of it', async () => {
    const output = await runFixture(true)

    expect(output).toContain('THE_REAL_CAUSE')
    // The specific thing that must not be there. Asserting on the message
    // rather than on "no TypeError anywhere" keeps this pinned to the
    // masking defect instead of to vitest's formatting.
    expect(output).not.toContain('The "path" argument must be of type string')
  }, 120_000)

  it('is what the guard prevents: unguarded, the teardown TypeError is reported too', async () => {
    const output = await runFixture(false)

    // Both present - which is the whole problem. The real cause is not
    // absent, it is accompanied, and the accompanying error is the one that
    // reads like a cause and gets copied into reports.
    expect(output).toContain('THE_REAL_CAUSE')
    expect(output).toContain('The "path" argument must be of type string')
  }, 120_000)
})
