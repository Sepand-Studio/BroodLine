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

  constructor(private readonly bucketName: string) {}

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

  async setPointer(version: string): Promise<void> {
    if (!(await this.hasBundle(version))) {
      throw new Error(`Cannot point at ${version}: no such published bundle.`)
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
