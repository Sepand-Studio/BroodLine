/**
 * Runs ONLY aborted-beforeall.fixture.ts, for ../masked-teardown.test.ts to
 * drive as a subprocess.
 *
 * A separate config rather than a CLI flag because vitest exposes no
 * `--include`: the fixture has to be reachable by SOME include glob, and
 * widening the real suite's glob to reach a file that fails on purpose is
 * exactly what must not happen.
 */
import { defineConfig } from 'vitest/config'

export default defineConfig({
  test: {
    include: ['test/fixtures/aborted-beforeall.fixture.ts'],
  },
})
