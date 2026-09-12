import { readFile } from 'node:fs/promises'
import { join } from 'node:path'
import type { BundleStore } from './store.ts'
import { validateBundle } from './validate.ts'

export class BundleInvalidError extends Error {
  // Explicit field, NOT a constructor parameter property. Node's
  // --experimental-strip-types cannot compile `constructor(readonly x: T)`
  // - it is strip-only and a parameter property requires emitting an
  // assignment. The whole service runs under that flag in the container,
  // so a parameter property anywhere in index.ts's import graph crashes on
  // boot. Vitest uses esbuild, which DOES support them, which is why the
  // suite stayed green.
  readonly violations: string[]

  constructor(violations: string[]) {
    super(`Bundle failed validation:\n  ${violations.join('\n  ')}`)
    this.violations = violations
    this.name = 'BundleInvalidError'
  }
}

/**
 * Validate, then publish immutably. A bundle that fails validation is NOT
 * published - solo_execution 5.2, and the whole safety model.
 *
 * Publishing does NOT move the pointer. Making a bundle live is a separate,
 * deliberate act, so a publish can be staged and verified before any client
 * is told about it.
 */
export async function publishBundle(store: BundleStore, dir: string, version: string): Promise<void> {
  const manifest = JSON.parse(await readFile(join(dir, 'manifest.json'), 'utf8')) as { version: string }
  if (manifest.version !== version) {
    throw new BundleInvalidError([
      `manifest.json says version '${manifest.version}' but this is being published as '${version}'.`,
    ])
  }

  const violations = await validateBundle(dir)
  if (violations.length > 0) throw new BundleInvalidError(violations)

  await store.putBundle(version, dir)
}
