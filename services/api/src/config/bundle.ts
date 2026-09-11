import type { BundleStore } from './store.ts'
import type { Currency } from '../money/ledger.ts'

export interface StarterGrant { currency: Currency; amount: number }

export interface Bundle {
  version: string
  minimumClientVersion: string
  starterGrants: StarterGrant[]
}

let cached: Bundle | undefined

/**
 * Reads the pointer and returns the active bundle.
 *
 * Cached per process: the pointer changes on a rollback, and an instance that
 * has not restarted keeps serving the old version until it does. That is
 * acceptable because Cloud Run instances are short-lived, and the alternative
 * - reading a remote object on every /v1/sync - puts a network call on the
 * p99-300ms cold-start path.
 */
export async function loadBundle(store: BundleStore, opts: { refresh?: boolean } = {}): Promise<Bundle> {
  if (cached !== undefined && opts.refresh !== true) return cached

  const version = await store.getPointer()
  const manifest = JSON.parse(await store.readFile(version, 'manifest.json')) as {
    version: string; minimumClientVersion: string
  }
  const starter = JSON.parse(await store.readFile(version, 'starter.json')) as { grants: StarterGrant[] }

  cached = {
    version: manifest.version,
    minimumClientVersion: manifest.minimumClientVersion,
    starterGrants: starter.grants,
  }
  return cached
}

/** Tests only. */
export function clearBundleCache(): void { cached = undefined }
