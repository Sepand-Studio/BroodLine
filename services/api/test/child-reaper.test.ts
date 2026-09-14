import { type ChildProcess, spawn } from 'node:child_process'
import { fileURLToPath } from 'node:url'
import { afterEach, describe, expect, it } from 'vitest'

/**
 * Regression tests for child-reaper.ts - the guard that stops a signalled
 * vitest worker orphaning the sim host it spawned.
 *
 * The leak these are about is observed, not projected: this work began with
 * a `dotnet Broodline.Sim.Service.dll --urls http://127.0.0.1:5299` left
 * over from an earlier wave-submit.test.ts run still holding the port.
 *
 * Both tests drive the SAME fixture, differing only in whether it installs
 * the guard - so the second test IS the mutation for the first, running on
 * every suite run rather than once by hand. If reapOnExit stopped working,
 * test one fails; if the fixture's grandchild started dying on its own for
 * some unrelated reason (making test one pass vacuously), test two fails.
 */

const FIXTURE = fileURLToPath(new URL('./fixtures/reaper-fixture.ts', import.meta.url))

let started: ChildProcess[] = []
let orphans: number[] = []

afterEach(() => {
  for (const proc of started) proc.kill('SIGKILL')
  for (const pid of orphans) {
    try {
      process.kill(pid, 'SIGKILL')
    } catch {
      // already gone
    }
  }
  started = []
  orphans = []
})

/** Whether the pid names a live process. signal 0 checks without delivering. */
function alive(pid: number): boolean {
  try {
    process.kill(pid, 0)
    return true
  } catch {
    return false
  }
}

/**
 * Runs the fixture, waits for it to report its grandchild's pid, SIGTERMs
 * it, and returns whether the grandchild survived.
 */
async function grandchildSurvivesSigterm(mode: 'guarded' | 'unguarded'): Promise<{ survived: boolean; pid: number }> {
  const proc = spawn(process.execPath, ['--experimental-strip-types', FIXTURE, mode], {
    stdio: ['ignore', 'pipe', 'ignore'],
  })
  started.push(proc)

  const pid = await new Promise<number>((resolve, reject) => {
    let buffered = ''
    const timer = setTimeout(() => reject(new Error(`fixture never reported a grandchild: ${buffered}`)), 20_000)
    proc.stdout!.on('data', (chunk: Buffer) => {
      buffered += chunk.toString()
      const match = buffered.match(/GRANDCHILD (\d+)/)
      if (match) {
        clearTimeout(timer)
        resolve(Number(match[1]))
      }
    })
    proc.on('exit', () => { clearTimeout(timer); reject(new Error(`fixture exited early: ${buffered}`)) })
  })
  orphans.push(pid)

  expect(alive(pid), 'the grandchild must be running before the fixture is signalled').toBe(true)

  const exited = new Promise<void>((resolve) => proc.on('exit', () => resolve()))
  proc.kill('SIGTERM')
  await exited

  // The signal has been delivered by the time the fixture is gone, but the
  // grandchild's own exit is asynchronous - poll rather than sample once.
  // Bounded, and the bound is part of the claim: a child that takes longer
  // than this to die has, for the next run's purposes, not been reaped.
  for (let i = 0; i < 40 && alive(pid); i++) {
    await new Promise((r) => setTimeout(r, 50))
  }

  return { survived: alive(pid), pid }
}

describe('reapOnExit', () => {
  it('kills the spawned host when its own process is signalled', async () => {
    const { survived } = await grandchildSurvivesSigterm('guarded')
    expect(survived).toBe(false)
  }, 60_000)

  /**
   * The mutation, kept runnable. Without this, test one could pass for a
   * reason that has nothing to do with the guard - a grandchild that exits
   * on its own, a fixture that never really spawned one - and nobody would
   * know. This asserts the leak is real when the guard is absent, which is
   * the only thing that makes test one's result mean anything.
   */
  it('is what stops it: without the guard the host outlives its parent', async () => {
    const { survived } = await grandchildSurvivesSigterm('unguarded')
    expect(survived).toBe(true)
  }, 60_000)
})
