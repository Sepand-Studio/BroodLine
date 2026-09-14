import { spawn } from 'node:child_process'
import { mkdir, mkdtemp, readFile, rm, utimes, writeFile } from 'node:fs/promises'
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
 * a bash EXIT trap nor a TS try/finally runs on a hard kill.
 *
 * Round 3 review found two further defects this file's tests must actually
 * exercise, not merely appear to:
 *  - An ABA race in reclaim itself: deciding a lock is stale and then
 *    acting on it a moment later can destroy a DIFFERENT, live lock some
 *    other process legitimately acquired in between (see the "ABA race"
 *    describe block below).
 *  - The original "reclaims a dead pid" test used a staleMs low enough
 *    that the AGE fallback alone reclaimed it - the ESRCH (confirmed-dead)
 *    branch could be deleted entirely and the test stayed green, having
 *    verified nothing about dead-pid detection specifically. Every test
 *    below is written so that ONLY the branch it names can make it pass -
 *    each one has been branch-mutated to confirm this; see the comment
 *    on each test for exactly which branch and what mutating it does.
 *
 * Every test uses __createDotnetBuildLockForTest, an isolated lock
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

    // staleMs is set ABOVE timeoutMs, so the age fallback CANNOT possibly
    // fire within this test's window - the only way this can succeed is
    // via the ESRCH branch (isStale's `process.kill(pid, 0)` throwing
    // ESRCH) actually detecting the dead pid. An earlier version of this
    // test used staleMs: 500 with timeoutMs: 3_000, which the age branch
    // alone satisfies well inside the timeout - branch-mutation-verified
    // to leave ALL THREE THEN-EXISTING tests green when the ESRCH branch
    // was deleted entirely, proving it exercised nothing about dead-pid
    // detection. Branch mutated to confirm THIS version actually depends
    // on it: `if (... === 'ESRCH') return true` -> `return false` (fall
    // through to the age check instead of reclaiming immediately) makes
    // ONLY this test fail (timeout), because with staleMs above timeoutMs
    // the age fallback can never rescue it.
    const lock = __createDotnetBuildLockForTest(lockDir, { timeoutMs: 1_000, staleMs: 60_000, pollMs: 20 })

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

  it('does NOT reclaim a lock whose owner is confirmed alive and well within the absolute ceiling', async () => {
    const lockDir = await freshLockDir()

    // This test's OWN pid: unquestionably alive for the test's duration.
    await mkdir(lockDir)
    await writeFile(join(lockDir, 'owner.pid'), String(process.pid), 'utf8')

    // staleMs: 0 means the (irrelevant, for a confirmed-alive owner) age
    // fallback alone would call anything reclaimable instantly, and
    // absoluteCeilingMs is left at its large default - a lock created
    // moments ago is nowhere near it. If reclaim fired here, it would only
    // be because the confirmed-alive short-circuit (isStale's
    // `process.kill(pid, 0)` succeeding) was skipped, or its ceiling
    // comparison ignored - not because of a slow test or a low ceiling.
    // Branch mutated to confirm: `return age > absoluteCeilingMs` ->
    // `return true` unconditionally (treat "confirmed alive" as
    // reclaimable outright, regardless of age) makes this test fail (the
    // lock gets stolen instead of timing out).
    const lock = __createDotnetBuildLockForTest(lockDir, { timeoutMs: 400, staleMs: 0, pollMs: 20 })

    await expect(lock.withLock(() => 'should not run')).rejects.toThrow(/timed out waiting for the dotnet build lock/)

    // The live lock must still be standing, still owned by this process -
    // proof the timeout was a real "kept waiting," not a reclaim-then-fail.
    const owner = await readFile(join(lockDir, 'owner.pid'), 'utf8')
    expect(owner).toBe(String(process.pid))
  })

  it('DOES eventually reclaim a lock whose recorded pid is alive, once it exceeds the absolute ceiling (pid-reuse recovery)', async () => {
    const lockDir = await freshLockDir()

    // Round 3 finding 3's scenario: a SIGKILLed build's dead pid gets
    // reassigned by the OS to some unrelated, still-running process (or,
    // as here, simply IS one - this test's own pid is unquestionably
    // alive), so the liveness check alone would report "alive" forever.
    // Without the absolute ceiling, this lock could never recover.
    await mkdir(lockDir)
    await writeFile(join(lockDir, 'owner.pid'), String(process.pid), 'utf8')
    // Backdate the lock's own mtime rather than actually waiting, so the
    // test can use a short ceiling without a real multi-minute sleep.
    const old = new Date(Date.now() - 10_000)
    await utimes(lockDir, old, old)

    // Branch mutated to confirm: `return age > absoluteCeilingMs` ->
    // `return false` unconditionally (never reclaim via the ceiling, no
    // matter the age) makes this test fail (times out - the lock never
    // recovers), while leaving the PREVIOUS test (ceiling: 0, fresh lock)
    // unaffected in the other direction - the same line, opposite
    // mutation, opposite failing test: together they prove both outcomes
    // of the comparison are covered.
    const lock = __createDotnetBuildLockForTest(lockDir, {
      timeoutMs: 2_000, staleMs: 60_000, absoluteCeilingMs: 1_000, pollMs: 20,
    })

    let ran = false
    await lock.withLock(() => { ran = true })
    expect(ran).toBe(true)
  })
})

describe('withDotnetBuildLock - live-lock contention (not stolen)', () => {
  it('a waiter blocks behind a genuinely external live holder, then proceeds once it releases', async () => {
    const lockDir = await freshLockDir()

    // A GENUINELY external holder: a real, separate child process, alive
    // for the duration of the wait - not this test's own pid, so the
    // pid-comparison logic in isStale()/releaseOnce is actually exercised
    // rather than trivially matching itself (an earlier version of this
    // test used ONE lock object's two concurrent withLock() calls in this
    // SAME process, so "holder" and "waiter" shared one pid throughout -
    // it could not have detected a release, or a reclaim, that stole
    // another holder's lock). The lock is written directly rather than
    // acquired through withLock(), so there is no async "did the holder
    // actually win the mkdir yet" race to paper over with a fixed sleep.
    const holder = spawn(process.execPath, ['-e', 'setInterval(() => {}, 1000)'])
    await mkdir(lockDir)
    await writeFile(join(lockDir, 'owner.pid'), String(holder.pid), 'utf8')

    const lock = __createDotnetBuildLockForTest(lockDir, {
      timeoutMs: 5_000, staleMs: 60_000, absoluteCeilingMs: 60_000, pollMs: 20,
    })

    let waiterRan = false
    const waiterDone = lock.withLock(() => { waiterRan = true })

    // The core assertion: after several of the waiter's own poll
    // intervals, it must STILL be waiting - not have snuck in, not have
    // reclaimed a lock that has a live (if foreign) owner and is nowhere
    // near either staleness threshold.
    await new Promise((r) => setTimeout(r, 300))
    expect(waiterRan).toBe(false)

    // Release the external holder for real: kill the process, then remove
    // the lock the way that process's own releaseOnce would have on a
    // clean exit - only now may the waiter proceed.
    holder.kill()
    await new Promise<void>((resolve) => holder.on('exit', () => resolve()))
    await rm(lockDir, { recursive: true, force: true })

    await waiterDone
    expect(waiterRan).toBe(true)
  })
})

describe('withDotnetBuildLock - ABA race in reclaim', () => {
  it('two reclaimers racing the SAME stale lock cannot both enter the critical section', async () => {
    const lockDir = await freshLockDir()
    const pid = await deadPid()
    await mkdir(lockDir)
    await writeFile(join(lockDir, 'owner.pid'), String(pid), 'utf8')

    // Reproduces, deterministically, the exact scenario round 3's review
    // demonstrated against the OLD isStale()-then-rm(dir) shape: "A: mkdir
    // EEXIST -> isStale() true. A sits [...]. B: [...] isStale() true ->
    // rm(L1) -> mkdir succeeds -> L2, writes B's pid -> B ENTERS. A
    // resumes: rm(dir) deletes L2 - B's LIVE lock." A guessed millisecond
    // delay would only reproduce this probabilistically (confirmed via a
    // standalone bash version of this exact test: 1 violation in 5 runs
    // against the pre-fix shape with a fixed 150ms sleep, 0 in 5 against
    // the fix - real, but not something a single CI run could prove either
    // way). testBeforeReclaim instead makes A's reclaim wait for an actual
    // SIGNAL - B's critical section genuinely starting - so A's
    // destructive step is guaranteed to land exactly inside B's held-open
    // critical section every run, not merely "probably inside it."
    //
    // No other logic differs from the real production path;
    // testBeforeReclaim is a no-op for every real caller (wave-submit.test.ts,
    // replays.test.ts) and exists only for this reproduction.
    let resolveBEntered: () => void
    const bEntered = new Promise<void>((resolve) => { resolveBEntered = resolve })

    const lockA = __createDotnetBuildLockForTest(lockDir, {
      timeoutMs: 5_000, staleMs: 1, pollMs: 20,
      testBeforeReclaim: () => bEntered, // A's destructive step waits until B is confirmed inside its critical section
    })
    const lockB = __createDotnetBuildLockForTest(lockDir, { timeoutMs: 5_000, staleMs: 1, pollMs: 20 })

    const events: string[] = []
    const HOLD_MS = 150 // comfortably longer than the few ms A needs to act once released
    const runB = lockB.withLock(async () => {
      events.push('enter:B')
      resolveBEntered()
      await new Promise((r) => setTimeout(r, HOLD_MS))
      events.push('exit:B')
    })
    const runA = lockA.withLock(async () => {
      events.push('enter:A')
      await new Promise((r) => setTimeout(r, HOLD_MS))
      events.push('exit:A')
    })

    await Promise.all([runA, runB])

    // Non-overlapping critical sections: whichever of A/B actually wins
    // must fully enter-then-exit before the other ever enters - no
    // "enter:B enter:A" (or the reverse) before either exit, which is
    // EXACTLY the pattern the review's reproduction produced (two
    // concurrent "dotnet build"s on the shared services/sim/obj/, the
    // failure this whole file exists to prevent). Given the signal-based
    // wait above, B is GUARANTEED to enter first (A cannot even attempt
    // its reclaim until B signals it has already entered), so the only
    // question is whether A's subsequent reclaim steals B's still-live
    // lock.
    //
    // Branch mutated to confirm what THIS test actually depends on:
    // reclaim()'s re-verify (`if (stillThere !== observedRaw) return`)
    // disabled makes this test fail deterministically, every run - by
    // the time A's reclaim runs, B has already replaced the dead-pid
    // lock A observed with its own live one, so re-verify is the layer
    // that catches the mismatch and aborts. Separately mutation-checked
    // (and NOT caught by this or any other test here): reverting the
    // atomic rename to a blind `rm(dir, ...)` while KEEPING the
    // re-verify passes all 5 tests unchanged - in this file's scenarios,
    // by the time any reclaim's destructive step runs, re-verify has
    // already established the lock is still the SAME stale object no
    // legitimate acquirer has touched, so an unconditional destroy at
    // that point is no more dangerous than the atomic one. The rename's
    // OWN distinct value - two reclaimers racing to destroy the exact
    // SAME still-current stale lock at the exact same instant - is real
    // (without it, both would blind-rm the same dead lock, which is
    // harmless: one wins, the other's rm just no-ops on an
    // already-gone path) but is not independently dangerous enough for
    // this repo's actual failure mode to warrant a dedicated
    // mutation-isolating test; kept for defense in depth and because it
    // costs nothing.
    expect(events).toEqual(['enter:B', 'exit:B', 'enter:A', 'exit:A'])
  })
})
