import type { BundleStore } from './store.ts'
import type { Currency } from '../money/ledger.ts'

export interface StarterGrant { currency: Currency; amount: number }

export interface WaveSpawn { tick: number; type: string }

/**
 * A wave as authored in waves.json. `reward` is optional (Task 5, task-5-brief
 * §3): today only wave 6 carries one, and only in 0.1.1 - 0.1.0 is already
 * published to GCS and is never edited in place. Task 7 is what makes a
 * missing reward a publish-time validation failure; this file only carries
 * the field.
 */
export interface BundleWave {
  id: number
  integrity: number
  laneCount: number
  reward?: { currency: Currency; amount: number }
  spawns: WaveSpawn[]
}

export interface Bundle {
  version: string
  minimumClientVersion: string
  starterGrants: StarterGrant[]
  waves: BundleWave[]
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
  const waves = JSON.parse(await store.readFile(version, 'waves.json')) as BundleWave[]

  cached = {
    version: manifest.version,
    minimumClientVersion: manifest.minimumClientVersion,
    starterGrants: starter.grants,
    waves,
  }
  return cached
}

/** Tests only. */
export function clearBundleCache(): void { cached = undefined }
