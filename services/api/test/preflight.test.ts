import { chmod, mkdtemp, writeFile } from 'node:fs/promises'
import { createServer, type Server } from 'node:net'
import { tmpdir } from 'node:os'
import { delimiter, join } from 'node:path'
import { afterEach, describe, expect, it } from 'vitest'
import { checkDotnet, checkPort, checkToolRestore, SIM_PORTS } from './preflight.ts'

/**
 * The three preflight branches, each fired by SIMULATING ITS OWN CONDITION -
 * not by asserting the code reads a certain way.
 *
 * These matter because the preflight is the only thing standing between a
 * Node-only machine and a failure several files deep inside a 240s
 * beforeAll. A preflight with a branch that never fires is worse than none:
 * it reads as coverage.
 *
 * Each test names the branch it depends on. All three have been checked by
 * deleting THAT branch alone - not the enclosing function - and confirming
 * only the matching test fails.
 */

let opened: Server[] = []

afterEach(async () => {
  await Promise.all(opened.map((s) => new Promise<void>((r) => s.close(() => r()))))
  opened = []
})

/** A PATH with nothing on it, plus a directory we control at the front. */
async function fakeBinDir(script: string): Promise<string> {
  const dir = await mkdtemp(join(tmpdir(), 'broodline-preflight-'))
  const bin = join(dir, 'dotnet')
  await writeFile(bin, script, 'utf8')
  await chmod(bin, 0o755)
  return dir
}

describe('preflight: dotnet is absent', () => {
  /**
   * Branch: checkDotnet's catch. Mutating ONLY that catch to `return
   * undefined` makes this test - and no other - fail.
   *
   * A PATH pointing at nothing is the honest simulation of the condition:
   * node resolves a spawned command through options.env.PATH, so this is
   * exactly what the check sees on a machine with no .NET SDK.
   */
  it('reports it, naming dotnet and how to install it', () => {
    const failure = checkDotnet({ PATH: '/nonexistent-preflight-probe' })

    expect(failure?.kind).toBe('dotnet-missing')
    expect(failure?.message).toContain('`dotnet` is not on PATH')
    // The message must carry a fix, not just a diagnosis - that is the whole
    // deliverable for 3c, so it is asserted rather than assumed.
    expect(failure?.message).toContain('dotnet-sdk')
    expect(failure?.message).toContain('https://dotnet.microsoft.com/download')
  })

  it('passes on this machine, which has one', () => {
    // Vacuity guard: without this, the test above would pass identically
    // against a checkDotnet that reported "missing" unconditionally.
    expect(checkDotnet()).toBeUndefined()
  })
})

describe('preflight: dotnet tool restore fails', () => {
  /**
   * Branch: checkToolRestore's catch. Mutating ONLY that catch to `return
   * undefined` makes this test - and no other - fail.
   *
   * A stub `dotnet` that succeeds for `--version` and fails for `tool
   * restore` is the condition as it actually appears: an SDK that is
   * installed and working, but a restore that cannot reach its feed. A test
   * that simply removed dotnet would fire the FIRST branch and prove
   * nothing about this one.
   */
  it('reports it separately from a missing SDK, naming the feed and the command', async () => {
    const dir = await fakeBinDir([
      '#!/bin/sh',
      'if [ "$1" = "--version" ]; then echo 10.0.100; exit 0; fi',
      'if [ "$1" = "tool" ]; then echo "error NU1301: Unable to load the service index for source https://api.nuget.org/v3/index.json." >&2; exit 1; fi',
      'exit 0',
    ].join('\n'))
    const env = { ...process.env, PATH: `${dir}${delimiter}${process.env.PATH ?? ''}` }

    // The SDK itself must look fine, or this would be the other branch.
    expect(checkDotnet(env)).toBeUndefined()

    const failure = checkToolRestore(env)
    expect(failure?.kind).toBe('tool-restore-failed')
    expect(failure?.message).toContain('`dotnet tool restore` failed')
    expect(failure?.message).toContain('api.nuget.org')
    // The tool's own output is forwarded - without it the message is a
    // guess about the cause rather than a report of it.
    expect(failure?.message).toContain('NU1301')
  })
})

describe('preflight: a sim port is occupied', () => {
  /**
   * Branch: checkPort's 'error' listener. Mutating ONLY that listener to
   * `resolve(undefined)` makes this test - and no other - fail.
   *
   * The port is really held, by a real listener, and checked by really
   * binding it - the same syscall the sim host makes.
   */
  it('reports it, naming the port, the file that needs it and how to find the holder', async () => {
    const holder = createServer()
    opened.push(holder)
    const port = await new Promise<number>((resolve) => {
      holder.listen(0, '127.0.0.1', () => resolve((holder.address() as { port: number }).port))
    })

    const failure = await checkPort(port, 'test/wave-submit.test.ts')

    expect(failure?.kind).toBe('port-in-use')
    expect(failure?.message).toContain(`port ${port} is already in use`)
    expect(failure?.message).toContain('test/wave-submit.test.ts')
    expect(failure?.message).toContain(`lsof -nP -i :${port}`)
  })

  it('passes on a port nothing holds', async () => {
    // Vacuity guard, same reason as the dotnet one above.
    const free = createServer()
    const port = await new Promise<number>((resolve) => {
      free.listen(0, '127.0.0.1', () => resolve((free.address() as { port: number }).port))
    })
    await new Promise<void>((r) => free.close(() => r()))

    expect(await checkPort(port, 'nothing')).toBeUndefined()
  })

  it('covers every port the suite actually binds', () => {
    // A preflight that checks one of the four ports leaves the other three
    // failing exactly the way it exists to prevent - so the list is pinned
    // against the ports the test files really use.
    expect(SIM_PORTS.map((p) => p.port)).toEqual([5199, 5299, 5399, 5499])
  })
})
