import { cp, mkdir, readFile, writeFile } from 'node:fs/promises'
import { existsSync } from 'node:fs'
import { join } from 'node:path'

/**
 * Published bundles are IMMUTABLE and versioned; the POINTER naming the
 * active one is not.
 *
 * That split is what makes solo_execution 5.2's "rollback is a config change,
 * not a deploy" true on Cloud Run, where an env-var change is a new revision.
 * Rollback rewrites one small object; no bundle is ever overwritten, so the
 * version rolled back to is byte-identical to the one that shipped.
 */
export interface BundleStore {
  hasBundle(version: string): Promise<boolean>
  putBundle(version: string, sourceDir: string): Promise<void>
  readFile(version: string, name: string): Promise<string>
  getPointer(): Promise<string>
  setPointer(version: string): Promise<void>
}

export class LocalBundleStore implements BundleStore {
  constructor(private readonly root: string) {}

  private dir(version: string): string { return join(this.root, 'bundles', version) }
  private get pointerPath(): string { return join(this.root, 'bundles', 'current') }

  async hasBundle(version: string): Promise<boolean> {
    return existsSync(this.dir(version))
  }

  async putBundle(version: string, sourceDir: string): Promise<void> {
    if (await this.hasBundle(version)) {
      throw new Error(`Bundle ${version} is already published. Bundles are immutable; publish a new version.`)
    }
    await mkdir(this.dir(version), { recursive: true })
    await cp(sourceDir, this.dir(version), { recursive: true })
  }

  async readFile(version: string, name: string): Promise<string> {
    return readFile(join(this.dir(version), name), 'utf8')
  }

  async getPointer(): Promise<string> {
    return (await readFile(this.pointerPath, 'utf8')).trim()
  }

  async setPointer(version: string): Promise<void> {
    if (!(await this.hasBundle(version))) {
      // Rollback must never name a version that was never published - that
      // turns a recovery into an outage.
      throw new Error(`Cannot point at ${version}: no such published bundle.`)
    }
    await mkdir(join(this.root, 'bundles'), { recursive: true })
    await writeFile(this.pointerPath, version, 'utf8')
  }
}
