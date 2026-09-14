import { existsSync } from 'node:fs'
import { mkdir, readdir, writeFile } from 'node:fs/promises'
import { dirname, join, relative, sep } from 'node:path'

/**
 * A replay is its INPUTS - seed, deployment, rally tap - not a recording of
 * state. A few hundred bytes from which `Sim.Replay` reproduces the whole
 * run - design 5.
 *
 * Written only on the verified path (design 5.2): a rejected submission
 * leaves no object, so storage is never an attacker's write primitive.
 * `api` never parses a replay (solo_execution 5.2 / phase4_backend_spine
 * 134) and that includes not undoing its own transport encoding just to
 * store it - `bytes` is the request body's `replay` field VERBATIM, the
 * same base64 string handed to `sim`, never decoded on this side. A future
 * replay viewer is what decodes it.
 */
export interface ReplayStore {
  put(serverId: number, playerId: string, issuanceId: string, bytes: string): Promise<void>
}

export class LocalReplayStore implements ReplayStore {
  // Explicit field, NOT a constructor parameter property - see
  // config/store.ts's LocalBundleStore for why (--experimental-strip-types
  // cannot compile a parameter property, and the whole service runs under
  // that flag in the container).
  private readonly root: string

  constructor(root: string) {
    this.root = root
  }

  private path(serverId: number, playerId: string, issuanceId: string): string {
    return join(this.root, 'replays', String(serverId), playerId, `${issuanceId}.bin`)
  }

  async put(serverId: number, playerId: string, issuanceId: string, bytes: string): Promise<void> {
    const path = this.path(serverId, playerId, issuanceId)
    await mkdir(dirname(path), { recursive: true })
    await writeFile(path, bytes, 'utf8')
  }

  /**
   * Full object keys, e.g. `replays/1/<playerId>/<issuanceId>.bin`.
   *
   * Deliberately NOT on the `ReplayStore` interface - review finding.
   * `Deps.replayStore` is typed against the interface, so a method there
   * ships to production on `GcsReplayStore` whether or not anything calls
   * it; this has exactly one caller, `replays.test.ts`, which already
   * holds a concrete `LocalReplayStore` (see `freshReplayStore()`), so
   * typing against the concrete class costs nothing and keeps
   * `bucket.getFiles({ prefix: 'replays/' })` out of the production
   * surface entirely.
   */
  async list(): Promise<string[]> {
    const root = join(this.root, 'replays')
    if (!existsSync(root)) return []
    return (await walk(root)).map((f) => relative(this.root, f).split(sep).join('/'))
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
