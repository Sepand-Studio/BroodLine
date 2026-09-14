/**
 * A test file whose beforeAll aborts, driven by ../masked-teardown.test.ts.
 *
 * This is the exact shape every sim-and-bundle test file in this suite has:
 * a beforeAll that assigns `bundleRoot` partway through its setup, and an
 * afterAll that removes it. When beforeAll throws BEFORE that assignment -
 * a container that will not start, a dotnet host that never binds, a bundle
 * that fails to publish - `bundleRoot` is still undefined, and an unguarded
 * `rm(bundleRoot, ...)` throws ERR_INVALID_ARG_TYPE on top of the real
 * error.
 *
 * BROODLINE_FIXTURE_GUARDED selects between the guard and its absence, so
 * the test can assert both halves: that the guard lets the real cause
 * through, and that without it the cause is buried.
 *
 * Not named *.test.ts - vitest's default include glob would collect it into
 * the real suite, where it would fail on purpose. It is reached only
 * through vitest.fixture.config.ts.
 */
import { mkdtemp, rm } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { afterAll, beforeAll, it } from 'vitest'

let bundleRoot: string

beforeAll(async () => {
  // Aborts BEFORE the assignment below, which is the whole shape being
  // reproduced. The condition is always true; it is a condition rather than
  // a bare throw only so the assignment stays reachable to the compiler -
  // an unconditional throw makes tsc report bundleRoot as never assigned,
  // which is a different defect from the one this fixture is about.
  if (process.env.BROODLINE_FIXTURE_ABORT !== 'no') throw new Error('THE_REAL_CAUSE')
  bundleRoot = await mkdtemp(join(tmpdir(), 'broodline-fixture-'))
})

afterAll(async () => {
  if (process.env.BROODLINE_FIXTURE_GUARDED === '1') {
    if (bundleRoot) await rm(bundleRoot, { recursive: true, force: true })
  } else {
    await rm(bundleRoot, { recursive: true, force: true })
  }
})

it('never runs, because beforeAll aborted', () => {})
