import type { BundleStore } from './store.ts'
import type { Currency } from '../money/ledger.ts'

export interface StarterGrant { currency: Currency; amount: number }

/**
 * A creature as authored in starter.json's `creatures` array (Task 3, bundle
 * 0.1.3) - the cold-open pair a new account is handed alongside its currency
 * grants. Structurally the authored subset of roster/creatures.ts's
 * `CreatureDto`: no `creatureId`, `generation`, or `name`, because those are
 * assigned at grant time rather than authored.
 */
export interface StarterCreature {
  species: string
  trait1: string
  tier1: number
  trait2: string
  tier2: number
  instinct: string
  isFounder: boolean
}

/** The tab-reveal thresholds authored in progression.json (Task 3). */
export interface Progression { tabs: Record<string, number> }

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

/**
 * A harvest node as authored in nodes.json (Task 2, bundle 0.1.2).
 *
 * Structurally identical to map/rotation.ts's own `BundleNode`, and
 * deliberately NOT imported from there or exported to it: that module
 * declares its slice locally so its purity stays checkable by inspection
 * (it imports nothing from `config/` or `db/`), and TypeScript's structural
 * typing already lets a `Bundle` loaded here satisfy `nodesFor`'s parameter.
 */
export interface BundleNode {
  id: string
  ratePerHour: number
  totalYield: number | null
}

export interface Bundle {
  version: string
  minimumClientVersion: string
  starterGrants: StarterGrant[]
  waves: BundleWave[]
  /**
   * EMPTY when the published bundle carries no nodes.json, rather than a
   * load failure - and the choice is forced rather than preferred. Bundles
   * 0.1.0 and 0.1.1 predate the file and are immutable (0.1.0 is published
   * to GCS and must stay byte-identical to what shipped in Phase 4), and
   * most of this suite publishes one of those two, so requiring the file
   * here would fail every one of those tests at `loadBundle` for a file
   * their bundle could never have carried. config/validate.ts's
   * `validateNodeRates` treats it as optional for the same reason.
   *
   * The cost is that a bundle which was SUPPOSED to ship nodes and forgot
   * loads as a region with no nodes. routes/region.ts refuses in that case
   * rather than serving an empty map, so the failure is named where it is
   * legible - but that is a check at request time, not at publish time, and
   * making it a publish-time one is still owed (Task 4's report, note 3).
   */
  nodes: BundleNode[]
  /**
   * The trait table - `traits.json`, which is OBJECT-WRAPPED (`{"traits":
   * [...]}`) and not a bare array the way nodes.json and waves.json are.
   *
   * `dominant` is what design §5.3's rolled slot reads: full coverage if
   * dominant, one tier lower if recessive. splice/distribution.ts declares
   * its own structural slice of this rather than importing `Bundle`, so that
   * module's purity stays checkable by inspection - the same split
   * map/rotation.ts keeps with `BundleNode`.
   *
   * EMPTY when the bundle carries no traits.json, for the reason `nodes` is:
   * bundle 0.1.0 is published to GCS and must stay byte-identical to what
   * shipped in Phase 4, and a hard read here would make a rollback to it a
   * boot failure. Unlike nodes.json this is NOT a tolerance the validator
   * shares - `validateTraitDominance` already refuses to publish a bundle
   * whose traits.json is missing or whose flags are absent - so the empty
   * case is unreachable for anything published since Phase 4. The refusal
   * for a trait nobody authored is `spliceDistribution`'s, where it can name
   * the trait.
   */
  traits: BundleTrait[]
  /**
   * The cold-open pair - EMPTY when starter.json carries no `creatures` array,
   * for the same reason `nodes` and `traits` are empty rather than a load
   * failure: bundles 0.1.0-0.1.2 predate the field and are immutable, and
   * 0.1.0 must stay byte-identical to what is published to GCS.
   */
  starterCreatures: StarterCreature[]
  /**
   * The tab-reveal thresholds - `{ tabs: {} }` when progression.json is
   * absent, same tolerance as above: bundles 0.1.0-0.1.2 predate the file.
   */
  progression: Progression
}

/**
 * A trait as authored in traits.json. `counters` and `species` are carried
 * because the file has them and a reader of this type should see the whole
 * authored row; only `dominant` is read today.
 */
export interface BundleTrait {
  id: string
  species: string
  counters: string | null
  dominant: boolean
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
  const starter = JSON.parse(await store.readFile(version, 'starter.json')) as {
    grants: StarterGrant[]; creatures?: StarterCreature[]
  }
  const waves = JSON.parse(await store.readFile(version, 'waves.json')) as BundleWave[]
  // Absent means "this bundle authors no nodes", not "this bundle is
  // broken" - see Bundle.nodes. The catch is on the READ, so a nodes.json
  // that exists and is malformed still throws from JSON.parse rather than
  // being silently swallowed into an empty region.
  const nodesRaw = await store.readFile(version, 'nodes.json').catch(() => null)
  // Same shape of tolerance, same placement of the catch - see Bundle.traits.
  // A traits.json that EXISTS and is malformed still throws from JSON.parse.
  const traitsRaw = await store.readFile(version, 'traits.json').catch(() => null)
  // Same tolerance again - see Bundle.progression. progression.json that
  // EXISTS and is malformed still throws from JSON.parse.
  const progressionRaw = await store.readFile(version, 'progression.json').catch(() => null)

  cached = {
    version: manifest.version,
    minimumClientVersion: manifest.minimumClientVersion,
    starterGrants: starter.grants,
    waves,
    nodes: nodesRaw === null ? [] : JSON.parse(nodesRaw) as BundleNode[],
    // Object-wrapped, unlike every other file here.
    traits: traitsRaw === null ? [] : (JSON.parse(traitsRaw) as { traits: BundleTrait[] }).traits,
    starterCreatures: starter.creatures ?? [],
    progression: progressionRaw === null
      ? { tabs: {} }
      : JSON.parse(progressionRaw) as Progression,
  }
  return cached
}

/** Tests only. */
export function clearBundleCache(): void { cached = undefined }
