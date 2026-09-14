import { execFileSync } from 'node:child_process'
import { fileURLToPath } from 'node:url'
import { describe, expect, it } from 'vitest'

const REPO = fileURLToPath(new URL('../../../', import.meta.url))

describe('the generated contract', () => {
  it('is committed in both directions and matches its sources', () => {
    execFileSync('./implementation/scripts/generate-contract.sh', { cwd: REPO })

    // FOUR paths, not two. Phase 4 gated the Unity -> api direction only;
    // a gate that covers one direction of a two-direction contract is a
    // gate that lets the other one rot - design 2.4.
    const dirty = execFileSync('git', [
      'status', '--porcelain', '--',
      'openapi/', 'client/Assets/Generated/', 'services/api/src/generated/',
    ], { cwd: REPO, encoding: 'utf8' })

    expect(dirty).toBe('')
  }, 300_000)
})
