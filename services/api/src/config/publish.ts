import { readFile } from 'node:fs/promises'
import { join } from 'node:path'
import type { BundleStore } from './store.ts'
import { validateBundle } from './validate.ts'

export class BundleInvalidError extends Error {
  constructor(readonly violations: string[]) {
    super(`Bundle failed validation:\n  ${violations.join('\n  ')}`)
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
