import { spawn } from 'node:child_process'
import { mkdir, mkdtemp, readFile, rm, utimes, writeFile } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { afterEach, describe, expect, it } from 'vitest'
import { __createDotnetBuildLockForTest, __LOCK_CONSTANTS_FOR_TEST } from './wave-helpers.ts'

/**
 * Regression tests for the mkdir-based mutex wave-helpers.ts's
 * withDotnetBuildLock wraps around `dotnet build` (see that file's comment,
 * and wave-submit.test.ts / replays.test.ts / adversarial.test.ts /
 * generate-contract.sh for the four real callers). Round 2 review finding: without staleness
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
 * Round 4 found a SEVENTH guard in this phase with no test that fails when
 * it breaks: releaseOnce's own ownership check. Mutating it to a blind
 * release left all five then-existing tests green. The last describe block
 * below is that test; it is also what pins the owner value being
 * `${pid}:${nonce}` rather than a bare pid, and therefore what stops the
 * lock silently depending on vitest's default pool being `forks`.
 *
 * Every test uses __createDotnetBuildLockForTest, an isolated lock
 * instance pointed at its own throwaway directory - never the real
 * production lock path - specifically so testing reclaim/contention here
 * can never race whatever wave-submit.test.ts, replays.test.ts or
 * adversarial.test.ts is doing
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
    // Attached immediately: if the assertion below throws, the finally
    // still awaits `waiterDone`, and an unhandled rejection reported after
    // the test would MASK the real assertion failure with a confusing
    // ENOENT from the waiter's next mkdir (afterEach removes the temp root
    // out from under a waiter still polling).
    waiterDone.catch(() => {})

    try {
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
    } finally {
      // Without this, a failure at `expect(waiterRan).toBe(false)` above
      // skips holder.kill() entirely: the setInterval child is orphaned and
      // OUTLIVES the vitest worker, and the waiter keeps polling a
      // directory afterEach is about to delete. Both are cleaned up here on
      // every path, so a failing assertion reports itself rather than
      // whatever the debris throws next.
      holder.kill()
      await waiterDone.catch(() => {})
    }
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
    // replays.test.ts, adversarial.test.ts) and exists only for this
    // reproduction.
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
    // that catches the mismatch and aborts.
    //
    // Re-verify is now the ONLY layer. reclaim() used to follow it with
    // an "atomic detach" (rename the lock aside, then rm the detached
    // copy), which round 4's review removed: mutation-checked here and
    // in the round-3 report, swapping that rename for a blind
    // `rm(dir, ...)` left all five tests green, and a direct experiment
    // at the one interleaving its comment claimed it for produced
    // IDENTICAL outcomes either way. It was also actively harmful - a
    // hard kill between the rename and the rm orphaned a
    // `.reclaim-<pid>-<uuid>` directory in TMPDIR that nothing ever
    // cleans, in exactly the hard-kill-mid-build scenario this lock
    // exists for. The tests staying green across that removal is the
    // POINT, not a coverage gap: there was never a behavior there to
    // cover.
    expect(events).toEqual(['enter:B', 'exit:B', 'enter:A', 'exit:A'])
  })
})

describe("withDotnetBuildLock - release after someone else's reclaim", () => {
  it('a holder whose lock was reclaimed out from under it does not delete the new owner\'s lock', async () => {
    const lockDir = await freshLockDir()

    // releaseOnce's ownership guard is the subject. Round 4's review found
    // it entirely uncovered: mutating it to a blind release left all five
    // other tests in this file green, because in every one of them the
    // releasing holder IS still the owner, so "check, then remove" and
    // "remove" are indistinguishable. This test is the one arrangement
    // where they differ.
    //
    // Both lock instances live in THIS process, deliberately. That is only
    // a meaningful test because the owner value is `${pid}:${nonce}` rather
    // than a bare pid: with a bare pid, A and B would write the SAME owner
    // value here and A's release would delete B's lock no matter what the
    // guard said. So this test also pins the property that the lock does
    // not depend on vitest's pool being `forks` - see wave-helpers.ts's
    // factory comment.
    const opts = { timeoutMs: 5_000, staleMs: 60_000, absoluteCeilingMs: 60_000, pollMs: 20 }
    const lockA = __createDotnetBuildLockForTest(lockDir, opts)
    const lockB = __createDotnetBuildLockForTest(lockDir, opts)

    let resolveAEntered: () => void
    const aEntered = new Promise<void>((resolve) => { resolveAEntered = resolve })
    let releaseA: () => void
    const aMayRelease = new Promise<void>((resolve) => { releaseA = resolve })
    let resolveBEntered: () => void
    const bEntered = new Promise<void>((resolve) => { resolveBEntered = resolve })
    let releaseB: () => void
    const bMayRelease = new Promise<void>((resolve) => { releaseB = resolve })

    const runA = lockA.withLock(async () => {
      resolveAEntered()
      await aMayRelease
    })
    await aEntered
    const ownerA = await readFile(join(lockDir, 'owner.pid'), 'utf8')

    // A is still inside its critical section. Now simulate what a reclaimer
    // would have done to it - a build that somehow outran the staleness
    // threshold, which is the one case releaseOnce's comment names. Done
    // directly rather than through a second reclaiming instance so the
    // scenario is deterministic: the point under test is what A's RELEASE
    // does afterwards, not how the reclaim came about.
    await rm(lockDir, { recursive: true, force: true })

    const runB = lockB.withLock(async () => {
      resolveBEntered()
      await bMayRelease
    })
    await bEntered
    const ownerB = await readFile(join(lockDir, 'owner.pid'), 'utf8')

    // Same process, same pid, different owner values - finding 4's property,
    // asserted rather than assumed. If these were equal, the guard below
    // could not distinguish A from B at all.
    expect(ownerB).not.toBe(ownerA)
    expect(ownerA.split(':')[0]).toBe(String(process.pid))
    expect(ownerB.split(':')[0]).toBe(String(process.pid))

    // A releases. It no longer owns anything; B does.
    releaseA!()
    await runA

    // THE ASSERTION: B's lock is untouched. Branch mutated to confirm this
    // test depends on exactly that guard - releaseOnce's
    // `if (raw === ownerValue)` -> `if (true)` (a blind release) makes ONLY
    // this test fail, on this read: A's rm takes B's live lock with it and
    // the owner file is gone.
    const afterARelease = await readFile(join(lockDir, 'owner.pid'), 'utf8').catch(() => undefined)
    expect(afterARelease).toBe(ownerB)

    releaseB!()
    await runB

    // And B's OWN release still works - the guard rejects a foreign owner
    // without also rejecting the legitimate one.
    const afterBRelease = await readFile(join(lockDir, 'owner.pid'), 'utf8').catch(() => undefined)
    expect(afterBRelease).toBeUndefined()
  })
})

/**
 * THE TWO IMPLEMENTATIONS AGREE, ASSERTED RATHER THAN ASKED FOR.
 *
 * `wave-helpers.ts` and `implementation/scripts/generate-contract.sh` each
 * implement this mutex, because a bash script cannot import a TypeScript
 * module. Both files carry comments instructing an editor to change the
 * constants together, and until Task 22's fix round **those comments were the
 * entire mechanism** - which is precisely the shape this package refuses
 * elsewhere: `preflight.test.ts` pins SIM_PORTS with a test, and
 * `replay-format.ts` parses SimVersion.cs rather than retyping it, on the
 * stated grounds that a constant duplicating a fact already in a file "is a
 * claim with a maintenance cost and no enforcement".
 *
 * WHAT DRIFT ACTUALLY COSTS, stated accurately because an earlier version of
 * the comment in `wave-helpers.ts` overstated it. A divergent lock PATH would
 * give two locks and no exclusion - but the path cannot drift, because both
 * sides derive it by hashing the repo root, so there is nothing here to pin.
 * A divergent TIMEOUT still excludes correctly; the two sides simply give up
 * at different moments, so a contended suite goes red in one place and green
 * in another for no reason a reader can see. That is a legibility failure
 * rather than a correctness one, and it is worth pinning for exactly that
 * reason - not by pretending it is worse than it is.
 *
 * THE SHELL SIDE IS PARSED, NOT RETYPED, for the same reason: a third copy of
 * the numbers in this file would agree with itself and with nothing else.
 */
describe('the build lock is defined twice and the two definitions agree', () => {
  const SCRIPT = fileURLToPath(
    new URL('../../../implementation/scripts/generate-contract.sh', import.meta.url))

  /**
   * Reads `NAME=<digits>` off the script. Throws rather than returning a
   * default on a miss: a silent fallback here would make this whole file
   * pass against a script that had renamed the variable, which is one of the
   * exact drifts it exists to catch.
   */
  async function shellConstant(name: string): Promise<number> {
    const source = await readFile(SCRIPT, 'utf8')
    const match = new RegExp(`^${name}=(\\d+)`, 'm').exec(source)
    if (match === null) {
      throw new Error(
        `dotnet-build-lock.test.ts: expected a line '${name}=<digits>' in ${SCRIPT}, and found `
        + 'none. The script\'s shape changed and this parser must move with it - see this '
        + 'block\'s comment on why the shell side is parsed rather than retyped.')
    }
    return Number(match[1])
  }

  it('the acquire timeout matches, in the unit each side spells it in', async () => {
    // The shell counts 0.1s polling ticks; TypeScript counts milliseconds.
    const tenths = await shellConstant('BUILD_LOCK_TIMEOUT_TENTHS')
    expect(tenths * 100).toBe(__LOCK_CONSTANTS_FOR_TEST.acquireTimeoutMs)

    // NOT A TAUTOLOGY, and this is the assertion that keeps the one above
    // from becoming one: 0 === 0 would satisfy a multiplication against two
    // absent values, and the parser's throw only covers a MISSING line, not
    // a zeroed one.
    expect(tenths).toBeGreaterThan(0)
  })

  it('the staleness window and the absolute ceiling match too', async () => {
    expect(await shellConstant('BUILD_LOCK_STALE_SECONDS') * 1000)
      .toBe(__LOCK_CONSTANTS_FOR_TEST.staleMs)
    expect(await shellConstant('BUILD_LOCK_ABSOLUTE_CEILING_SECONDS') * 1000)
      .toBe(__LOCK_CONSTANTS_FOR_TEST.absoluteCeilingMs)
  })

  it('the ordering the three constants only make sense in still holds', async () => {
    // Ordered rather than merely equal, because equality across the two files
    // would still be satisfied by three numbers that are jointly nonsense -
    // a ceiling below the staleness window, say, which would make the
    // confirmed-alive branch fire before the unconfirmable one.
    const { acquireTimeoutMs, staleMs, absoluteCeilingMs } = __LOCK_CONSTANTS_FOR_TEST
    expect(acquireTimeoutMs).toBeLessThan(staleMs)
    expect(staleMs).toBeLessThan(absoluteCeilingMs)
  })
})
