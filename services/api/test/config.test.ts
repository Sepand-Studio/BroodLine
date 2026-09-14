import { mkdtemp, rm } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { clearBundleCache, loadBundle } from '../src/config/bundle.ts'
import { BundleInvalidError, publishBundle } from '../src/config/publish.ts'
import { LocalBundleStore } from '../src/config/store.ts'
import { validateBundle } from '../src/config/validate.ts'

// fileURLToPath (not .pathname) so a space anywhere in the path - as in this
// very repo's parent directory - is decoded rather than left as a literal %20.
const REPO = fileURLToPath(new URL('../../../', import.meta.url))
// 0.1.1, not 0.1.0: wave 6 carries no reward in 0.1.0, and Task 7 makes a
// missing reward a validation failure, so publishing 0.1.0 here would fail.
// 0.1.0 is already published to GCS and must stay byte-identical to what
// shipped in Phase 4, so it is never used as a "valid bundle" fixture again -
// see wave-start.test.ts and wave-submit.test.ts, which made the same call.
const SEED = join(REPO, 'config/bundles/0.1.1')
const FIX = (name: string) => join(REPO, 'services/api/test/fixtures', name)

let root: string
let store: LocalBundleStore

beforeEach(async () => {
  root = await mkdtemp(join(tmpdir(), 'broodline-bundles-'))
  store = new LocalBundleStore(root)
  clearBundleCache()
})
// Guarded: `root` is assigned by beforeEach's first statement, so an
// mkdtemp that fails left this removing `undefined` and throwing
// ERR_INVALID_ARG_TYPE on top of the real error. A one-statement window,
// but the same shape removed from six files this round - see
// wave-submit.test.ts's afterAll and masked-teardown.test.ts.
afterEach(async () => { if (root) await rm(root, { recursive: true, force: true }) })

describe('validation', () => {
  it('passes the authored seed bundle', async () => {
    expect(await validateBundle(SEED)).toEqual([])
  }, 120_000)

  it('catches a wave the ENGINE rejects, not a rule reimplemented here', async () => {
    const v = await validateBundle(FIX('bad-waves'))
    expect(v.join(' ')).toMatch(/ordered by tick ascending/)
  }, 120_000)

  it('catches a pack ladder that goes backwards', async () => {
    const v = await validateBundle(FIX('bad-ladder'))
    expect(v.join(' ')).toMatch(/not monotonic/)
  }, 120_000)

  it('catches a free pack instead of reporting a false violation on every pack after it', async () => {
    // priceUsdCents: 0 with value > 0: the OLD code computed
    // prevRate = value / priceUsdCents = Infinity, so the free pack sorted
    // first and EVERY later pack failed `currRate < prevRate` for no reason.
    const v = await validateBundle(FIX('pack-ladder-infinity'))
    expect(v).toHaveLength(1)
    expect(v[0]).toMatch(/Pack 'free' has a non-positive priceUsdCents/)
  }, 120_000)

  it('catches a free pack instead of silently exempting it from the ladder', async () => {
    // priceUsdCents: 0 with value === 0: the OLD code computed
    // prevRate = 0 / 0 = NaN, and every comparison against NaN is false, so
    // the pack - and the real gap right after it - passed silently.
    const v = await validateBundle(FIX('pack-ladder-nan'))
    expect(v).toHaveLength(1)
    expect(v[0]).toMatch(/Pack 'free' has a non-positive priceUsdCents/)
  }, 120_000)

  it('catches a locale missing a key the reference has', async () => {
    const v = await validateBundle(FIX('missing-locale-key'))
    expect(v.join(' ')).toMatch(/ja\.json is missing 1 key/)
  }, 120_000)

  it('catches a starter grant naming a currency outside the Postgres enum', async () => {
    const v = await validateBundle(FIX('bad-starter-currency'))
    expect(v.join(' ')).toMatch(/unrecognized currency 'gems'/)
    // Not the OTHER new check - this fixture's manifest and amounts are fine.
    expect(v.join(' ')).not.toMatch(/minimumClientVersion/)
    expect(v.join(' ')).not.toMatch(/invalid amount/)
  }, 120_000)

  it('catches a starter grant with a negative amount', async () => {
    const v = await validateBundle(FIX('bad-starter-amount'))
    expect(v.join(' ')).toMatch(/invalid amount -5/)
    // Not the OTHER new check - this fixture's currencies and manifest are fine.
    expect(v.join(' ')).not.toMatch(/unrecognized currency/)
    expect(v.join(' ')).not.toMatch(/minimumClientVersion/)
  }, 120_000)

  it('catches a manifest with no minimumClientVersion', async () => {
    const v = await validateBundle(FIX('missing-minimum-client-version'))
    expect(v.join(' ')).toMatch(/manifest\.json is missing minimumClientVersion/)
    // Not the OTHER new check - this fixture's starter grants are fine.
    expect(v.join(' ')).not.toMatch(/unrecognized currency/)
    expect(v.join(' ')).not.toMatch(/invalid amount/)
  }, 120_000)
})

describe('publish', () => {
  it('publishes a valid bundle and leaves the pointer alone', async () => {
    await publishBundle(store, SEED, '0.1.1')
    expect(await store.hasBundle('0.1.1')).toBe(true)
    // Publishing does not make a bundle live. That is a second, deliberate act.
    await expect(store.getPointer()).rejects.toThrow()
  }, 120_000)

  it('refuses to publish an invalid bundle at all', async () => {
    await expect(publishBundle(store, FIX('bad-ladder'), '9.9.9')).rejects.toBeInstanceOf(BundleInvalidError)
    expect(await store.hasBundle('9.9.9')).toBe(false)
  }, 120_000)

  it('refuses to overwrite a published version', async () => {
    await publishBundle(store, SEED, '0.1.1')
    await expect(publishBundle(store, SEED, '0.1.1')).rejects.toThrow(/immutable/i)
  }, 120_000)

  it('refuses a manifest whose version disagrees with the publish target', async () => {
    await expect(publishBundle(store, SEED, '0.2.0')).rejects.toThrow(/manifest\.json says version/)
  }, 120_000)
})

describe('the pointer', () => {
  it('makes a bundle live, and rollback names a previous version', async () => {
    await publishBundle(store, SEED, '0.1.1')
    await store.setPointer('0.1.1')

    const bundle = await loadBundle(store)
    expect(bundle.version).toBe('0.1.1')
    expect(bundle.minimumClientVersion).toBe('0.1.0')
    // The starter grant's amounts come from the bundle, not from code.
    expect(bundle.starterGrants).toEqual([
      { currency: 'splice_charges', amount: 3 },
      { currency: 'shards', amount: 250 },
    ])
  }, 120_000)

  it('refuses to point at a version that was never published', async () => {
    // A rollback naming a missing version turns a recovery into an outage.
    await expect(store.setPointer('0.0.9')).rejects.toThrow(/no such published bundle/)
  })
})
