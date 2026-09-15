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
 * ALL FOUR ROUTES, not one. reapOnExit registers on 'exit', SIGTERM, SIGINT
 * and SIGHUP, and an earlier version of this file exercised only SIGTERM -
 * so trimming `handlers` down to `['SIGTERM']` left the suite green while
 * an interrupted run went back to orphaning 5299 and 5399, which is the
 * exact leak the module was written to stop. Each route is now driven
 * separately.
 *
 * Both halves for every route: guarded must reap, UNGUARDED MUST LEAK. The
 * second is not decoration - without it, a route where the grandchild
 * happened to die for some unrelated reason (a signal reaching the process
 * group, say) would pass the guarded test having proven nothing about the
 * guard. Every unguarded case below was confirmed to actually leak.
 */

const FIXTURE = fileURLToPath(new URL('./fixtures/reaper-fixture.ts', import.meta.url))

/**
 * The four ways the worker can go away. 'exit' is the ordinary-exit route
 * and reaches reapOnExit's 'exit' listener; the other three reach one
 * signal handler each.
 *
 * Node resets every spawned child's signal dispositions to SIG_DFL (libuv
 * does it in the child before exec - verified directly), so these arrive at
 * the fixture with default behaviour no matter how this test runner was
 * itself launched. That is NOT true down a chain of shells, where a process
 * started with `&` by a non-interactive shell inherits SIGINT as ignored.
 */
const ROUTES = ['exit', 'SIGTERM', 'SIGINT', 'SIGHUP'] as const
type Route = (typeof ROUTES)[number]

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
 * Runs the fixture, waits for it to report its grandchild's pid, makes it go
 * away by `route`, and returns whether the grandchild survived.
 */
async function grandchildSurvives(mode: 'guarded' | 'unguarded', route: Route): Promise<boolean> {
  const proc = spawn(process.execPath, [
    '--experimental-strip-types', FIXTURE, mode, route === 'exit' ? 'exit' : 'signal',
  ], { stdio: ['ignore', 'pipe', 'ignore'] })
  started.push(proc)

  const exited = new Promise<void>((resolve) => proc.on('exit', () => resolve()))

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
    // No early-exit rejection here: on the 'exit' route the fixture is
    // SUPPOSED to exit, and it may do so before this promise settles.
    proc.on('exit', () => {
      if (!buffered.includes('GRANDCHILD')) {
        clearTimeout(timer)
        reject(new Error(`fixture exited without reporting a grandchild: ${buffered}`))
      }
    })
  })
  orphans.push(pid)

  expect(alive(pid), 'the grandchild must be running before the fixture goes away').toBe(true)

  // 'exit' needs no push - the fixture is already on its way out.
  if (route !== 'exit') proc.kill(route)
  await exited

  // The signal has been delivered by the time the fixture is gone, but the
  // grandchild's own exit is asynchronous - poll rather than sample once.
  // Bounded, and the bound is part of the claim: a child that takes longer
  // than this to die has, for the next run's purposes, not been reaped.
  for (let i = 0; i < 40 && alive(pid); i++) {
    await new Promise((r) => setTimeout(r, 50))
  }

  return alive(pid)
}

describe('reapOnExit', () => {
  it.each(ROUTES)('kills the spawned host when its own process goes away via %s', async (route) => {
    expect(await grandchildSurvives('guarded', route)).toBe(false)
  }, 60_000)

  /**
   * The mutation, kept runnable, once per route. Without these, the tests
   * above could pass for reasons that have nothing to do with the guard.
   */
  it.each(ROUTES)('is what stops it: without the guard, %s leaves the host behind', async (route) => {
    expect(await grandchildSurvives('unguarded', route)).toBe(true)
  }, 60_000)
})
