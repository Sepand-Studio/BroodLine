import { spawn } from 'node:child_process'
import { mkdir, mkdtemp, readFile, rm, writeFile } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { afterEach, describe, expect, it } from 'vitest'
import { __createDotnetBuildLockForTest } from './wave-helpers.ts'

/**
 * Regression tests for the mkdir-based mutex wave-helpers.ts's
 * withDotnetBuildLock wraps around `dotnet build` (see that file's comment,
 * and wave-submit.test.ts/replays.test.ts/generate-contract.sh for the
 * three real callers). Round 2 review finding: without staleness
 * detection, a Ctrl-C or SIGKILL during a build - a routine developer
 * action, not an edge case - leaves the lock on disk forever, since neither
 * a bash EXIT trap nor a TS try/finally runs on a hard kill. Every one of
 * these tests uses __createDotnetBuildLockForTest, an isolated lock
 * instance pointed at its own throwaway directory - never the real
 * production lock path - specifically so testing reclaim/contention here
 * can never race whatever wave-submit.test.ts or replays.test.ts is doing
 * against the actual shared lock under vitest's parallel file execution.
 */

let dir: string | undefined

afterEach(async () => {
  if (dir !== undefined) await rm(dir, { recursive: true, force: true })
  dir = undefined
})

async function freshLockDir(): Promise<string> {
  const root = await mkdtemp(join(tmpdir(), 'broodline-lock-test-'))
  dir = root
  return join(root, 'the.lock')
}

/** A pid guaranteed to be dead: a real child process, spawned and awaited to exit. */
async function deadPid(): Promise<number> {
  const child = spawn(process.execPath, ['-e', 'process.exit(0)'])
  const pid = child.pid!
  await new Promise<void>((resolve) => child.on('exit', () => resolve()))
  return pid
}

describe('withDotnetBuildLock - staleness reclaim', () => {
  it('reclaims a lock owned by a dead pid, rather than timing out', async () => {
    const lockDir = await freshLockDir()
    const pid = await deadPid()

    // Simulate exactly what a Ctrl-C mid-build leaves behind: the lock
    // directory exists, its owner.pid names a real-but-now-dead process,
    // and nothing ever ran the release path.
    await mkdir(lockDir)
    await writeFile(join(lockDir, 'owner.pid'), String(pid), 'utf8')

    // A short acquire timeout AND a short staleness threshold: if this
    // resolves, it is because staleness reclaim fired well inside that
    // window, not because it happened to poll past a long timeout - a
    // build cannot legitimately spend anywhere near this "critical
    // section," so any tighter margin would be indistinguishable from a
    // real bug.
    const lock = __createDotnetBuildLockForTest(lockDir, { timeoutMs: 3_000, staleMs: 500, pollMs: 20 })

    let ran = false
    const result = await lock.withLock(() => {
      ran = true
      return 'built'
    })

    expect(ran).toBe(true)
    expect(result).toBe('built')

    // And the lock is released and clean afterward, owned by nothing -
    // the reclaim path does not leave debris of its own.
    const stillThere = await readFile(join(lockDir, 'owner.pid'), 'utf8').catch(() => undefined)
    expect(stillThere).toBeUndefined()
  })

  it('does NOT reclaim a lock whose owner is still alive, no matter how old the age check alone would allow', async () => {
    const lockDir = await freshLockDir()

    // This test's OWN pid: unquestionably alive for the test's duration.
    await mkdir(lockDir)
    await writeFile(join(lockDir, 'owner.pid'), String(process.pid), 'utf8')

    // staleMs: 0 means the AGE check alone would call anything reclaimable
    // instantly - if reclaim fired here, it would only be because the pid
    // check was skipped or wrong, not because of a slow test. A live
    // owner must block acquisition regardless of what the age threshold
    // says.
    const lock = __createDotnetBuildLockForTest(lockDir, { timeoutMs: 400, staleMs: 0, pollMs: 20 })

    await expect(lock.withLock(() => 'should not run')).rejects.toThrow(/timed out waiting for the dotnet build lock/)

    // The live lock must still be standing, still owned by this process -
    // proof the timeout was a real "kept waiting," not a reclaim-then-fail.
    const owner = await readFile(join(lockDir, 'owner.pid'), 'utf8')
    expect(owner).toBe(String(process.pid))
  })
})

describe('withDotnetBuildLock - live-lock contention (not stolen)', () => {
  it('a waiter blocks behind a live external holder, then proceeds once it releases', async () => {
    const lockDir = await freshLockDir()
    const lock = __createDotnetBuildLockForTest(lockDir, { timeoutMs: 5_000, staleMs: 60_000, pollMs: 20 })

    // An external holder: acquire the lock via a SEPARATE, concurrently
    // running dotnet-build-lock instance pointed at the same directory
    // (mirrors two real processes contending for one production lock),
    // held open until the test explicitly releases it below.
    let releaseExternalHolder: (() => void) | undefined
    const externalHolderDone = lock.withLock(
      () => new Promise<void>((resolve) => { releaseExternalHolder = resolve }),
    )
    // Let the external holder actually win the mkdir before the waiter
    // below starts contending, so this is a genuine "arrives after" case.
    await new Promise((r) => setTimeout(r, 50))

    let waiterRan = false
    const waiterDone = lock.withLock(() => { waiterRan = true })

    // The core assertion for "a live lock must not be stolen": after
    // several of the waiter's own poll intervals, it must STILL be
    // waiting - not have snuck in, not have reclaimed a lock that a) has a
    // live owner and b) is nowhere near its (60s) staleness threshold.
    await new Promise((r) => setTimeout(r, 300))
    expect(waiterRan).toBe(false)

    // Release the external holder; only now may the waiter proceed.
    releaseExternalHolder!()
    await externalHolderDone
    await waiterDone

    expect(waiterRan).toBe(true)
  })
})
