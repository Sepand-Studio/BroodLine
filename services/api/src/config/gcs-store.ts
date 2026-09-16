import { readdir, readFile as readLocal } from 'node:fs/promises'
import { join, relative } from 'node:path'
import { Storage } from '@google-cloud/storage'
import type { BundleStore } from './store.ts'

/**
 * The published bundle store. Bundles are immutable objects under
 * bundles/<version>/; the pointer at bundles/current is the one object that
 * is deliberately overwritten, which is what makes rollback a config change
 * rather than a deploy - solo_execution 5.2.
 */
export class GcsBundleStore implements BundleStore {
  private readonly storage = new Storage()

  // Explicit field, NOT a constructor parameter property. Node's
  // --experimental-strip-types cannot compile `constructor(readonly x: T)`
  // - it is strip-only and a parameter property requires emitting an
  // assignment. The whole service runs under that flag in the container,
  // so a parameter property anywhere in index.ts's import graph crashes on
  // boot. Vitest uses esbuild, which DOES support them, which is why the
  // suite stayed green.
  private readonly bucketName: string

  constructor(bucketName: string) {
    this.bucketName = bucketName
  }

  private get bucket() { return this.storage.bucket(this.bucketName) }
  private key(version: string, name: string) { return `bundles/${version}/${name}` }

  async hasBundle(version: string): Promise<boolean> {
    const [exists] = await this.bucket.file(this.key(version, 'manifest.json')).exists()
    return exists
  }

  async putBundle(version: string, sourceDir: string): Promise<void> {
    if (await this.hasBundle(version)) {
      throw new Error(`Bundle ${version} is already published. Bundles are immutable; publish a new version.`)
    }
    for (const file of await walk(sourceDir)) {
      const name = relative(sourceDir, file)
      await this.bucket.file(this.key(version, name)).save(await readLocal(file), {
        contentType: 'application/json',
        // No resumable session for files this small; it costs an extra round
        // trip per object and the whole bundle is a few kilobytes.
        resumable: false,
      })
    }
  }

  async readFile(version: string, name: string): Promise<string> {
    const [buf] = await this.bucket.file(this.key(version, name)).download()
    return buf.toString('utf8')
  }

  async getPointer(): Promise<string> {
    const [buf] = await this.bucket.file('bundles/current').download()
    return buf.toString('utf8').trim()
  }

  /**
   * EXISTENCE WAS NOT ENOUGH, and rollback is where that bit.
   *
   * This method re-activates an already-published bundle, which is what
   * makes rollback a config change rather than a deploy - so it never ran
   * the publish-time validators, and a bundle that was legal under the
   * validators active at ITS publish time can be pointed at long after they
   * tighten. Bundles 0.1.0 and 0.1.1 ship `traits.json` as `{"traits": []}`,
   * predating `validateTraitDominance`: pointing back at either leaves
   * region/state and claim working while EVERY splice - preview and commit
   * alike - answers 500, because `isDominant` finds no flag for any trait.
   * A partial outage that looks unrelated to the rollback that caused it is
   * the worst shape this failure could take.
   *
   * The full validator cannot run here - `validateBundle` reads a local
   * directory and a published bundle lives in the bucket - so this is the
   * narrow check for the one thing measured to break: an unusable trait
   * table. Refusing the rollback is deliberate. That recovery path is
   * already broken for splice; failing loudly at the moment an operator
   * chooses it beats discovering it through player 500s.
   *
   * OWED: a store-reading validator, so this is the whole publish check
   * rather than the subset rollback is known to need.
   */
  async setPointer(version: string): Promise<void> {
    if (!(await this.hasBundle(version))) {
      throw new Error(`Cannot point at ${version}: no such published bundle.`)
    }

    const traitsRaw = await this.readFile(version, 'traits.json').catch(() => null)
    const traits = traitsRaw === null
      ? null
      : (JSON.parse(traitsRaw) as { traits?: unknown[] }).traits
    if (!Array.isArray(traits) || traits.length === 0) {
      throw new Error(
        `Cannot point at ${version}: it authors no traits, so every splice `
        + 'would fail against it. Publish a bundle carrying traits.json instead.')
    }

    await this.bucket.file('bundles/current').save(version, { contentType: 'text/plain', resumable: false })
  }
}

async function walk(dir: string): Promise<string[]> {
  const out: string[] = []
  for (const entry of await readdir(dir, { withFileTypes: true })) {
    const full = join(dir, entry.name)
    if (entry.isDirectory()) out.push(...(await walk(full)))
    else out.push(full)
  }
  return out
}
