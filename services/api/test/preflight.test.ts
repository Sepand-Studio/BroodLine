import { execFileSync } from 'node:child_process'
import { chmodSync, mkdtempSync, writeFileSync } from 'node:fs'
import { chmod, mkdtemp, writeFile } from 'node:fs/promises'
import { createServer, type Server } from 'node:net'
import { tmpdir } from 'node:os'
import { delimiter, join } from 'node:path'
import { fileURLToPath } from 'node:url'
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

/** A directory holding one stub binary, to put at the front of PATH. */
async function fakeBinDir(script: string, mode = 0o755, name = 'dotnet'): Promise<string> {
  const dir = await mkdtemp(join(tmpdir(), 'broodline-preflight-'))
  const bin = join(dir, name)
  await writeFile(bin, script, 'utf8')
  await chmod(bin, mode)
  return dir
}

describe('preflight: dotnet is absent', () => {
  /**
   * Branch: checkDotnet's ENOENT arm. Mutating ONLY that arm - dropping the
   * `code === 'ENOENT'` test so everything falls through to 'dotnet-broken'
   * - makes this test, and no other, fail.
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

describe('preflight: dotnet is present but cannot run', () => {
  /**
   * Branch: checkDotnet's non-ENOENT arm. Collapsing the two arms back into
   * one - the shape this file shipped with in 4dd50f8 - makes this test,
   * and no other, fail.
   *
   * WHY IT IS ITS OWN BRANCH. `dotnet` exits non-zero while sitting right
   * there on PATH for several ordinary reasons, and the first one below is
   * the most common of them. Reporting those as "`dotnet` is not on PATH"
   * tells the reader to install an SDK they already have and throws away
   * the one line that says what is actually wrong - which is the precise
   * defect this whole preflight exists to stop, reproduced inside it.
   */
  const brokenSdk = [
    '#!/bin/sh',
    'echo "A compatible .NET SDK was not found. Requested SDK version 9.0.100 from global.json" >&2',
    'exit 1',
  ].join('\n')

  it('reports it as broken, not as missing, and forwards what dotnet actually said', async () => {
    const dir = await fakeBinDir(brokenSdk)
    const failure = checkDotnet({ ...process.env, PATH: `${dir}${delimiter}${process.env.PATH ?? ''}` })

    expect(failure?.kind).toBe('dotnet-broken')
    // The real cause, carried through verbatim. This is the assertion that
    // fails if the two arms are ever collapsed again.
    expect(failure?.message).toContain('Requested SDK version 9.0.100 from global.json')
    expect(failure?.message).toContain('global.json')
    // And it must NOT send the reader off to install what they already have.
    expect(failure?.message).not.toContain('is not on PATH')
    expect(failure?.message).not.toContain('brew install')
  })

  /**
   * The other non-ENOENT shape: a `dotnet` that cannot even be executed, so
   * there is no stderr to forward. The message must still say something
   * true rather than print an empty quotation - EACCES is what node reports
   * here, verified.
   */
  it('still says something true when the spawn fails with no output at all', async () => {
    const dir = await fakeBinDir('#!/bin/sh\nexit 0\n', 0o644) // present, not executable
    // ONLY this directory on PATH. An unexecutable file is SKIPPED by the
    // PATH search rather than being an error, so leaving the real PATH
    // appended would simply find the working dotnet further along and this
    // test would pass having exercised nothing - checked, it did exactly
    // that on the first run.
    const failure = checkDotnet({ PATH: dir })

    expect(failure?.kind).toBe('dotnet-broken')
    expect(failure?.message).toContain('EACCES')
    expect(failure?.message).not.toContain('It said:')
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
    expect(SIM_PORTS.map((p) => p.port)).toEqual([5199, 5299, 5399, 5499, 5599, 5699, 5799])
  })
})

/**
 * The same defect, one directory over: implementation/scripts/generate-contract.sh.
 *
 * WHY THIS LIVES HERE. The preflight above exists because "the failure being
 * illegible" is worth a check of its own; these two guards are that same
 * argument applied to the contract generator, so they are tested by the same
 * method - fire each branch by SIMULATING ITS OWN CONDITION, with a stub
 * binary on PATH.
 *
 * WHAT THEY CATCH, measured rather than supposed. The pnpm that resolves
 * first on a default PATH on this machine is 3.7.5, and pnpm 3 answers
 * `pnpm openapi` by printing usage and EXITING 0. `set -e` sees success, so
 * the script ran on and died ~200 lines later inside `dotnet nswag` with a
 * FileNotFoundException naming a scratch temp file. That error names neither
 * pnpm nor the generator, and it cost several agents time during Phase 6 -
 * it is carried in that phase's ledger for exactly that reason.
 */
describe('generate-contract.sh: the toolchain guard', () => {
  const SCRIPT = fileURLToPath(new URL('../../../implementation/scripts/generate-contract.sh', import.meta.url))

  /**
   * Runs the script with `binDir` at the front of PATH and returns what it
   * printed. The real PATH is APPENDED, not replaced: the guards themselves
   * shell out to python3, and a test that starved them of it would be
   * measuring the wrong failure.
   *
   * The timeout is not ceremony. If a guard ever stops firing, the script
   * does not fail - it proceeds into `dotnet tool restore`, an NSwag run and
   * a sim host on port 5199, for minutes, inside a unit test. Failing fast
   * is what keeps a broken guard legible as a broken guard.
   */
  function run(binDir: string): { status: number | null; output: string } {
    try {
      execFileSync(SCRIPT, [], {
        env: { ...process.env, PATH: `${binDir}${delimiter}${process.env.PATH ?? ''}` },
        stdio: 'pipe',
        timeout: 60_000,
      })
      return { status: 0, output: '' }
    } catch (err) {
      const e = err as { status?: number | null; stdout?: Buffer; stderr?: Buffer }
      return {
        status: e.status ?? null,
        output: `${e.stdout?.toString() ?? ''}${e.stderr?.toString() ?? ''}`,
      }
    }
  }

  /**
   * Branch: the pnpm-major comparison. The stub reproduces 3.7.5's actual
   * observed behaviour - usage text on stdout, exit 0 - rather than a
   * stand-in that fails honestly, because a stub that exited non-zero would
   * be caught by `set -e` alone and would prove nothing about this guard.
   */
  it('refuses a pnpm too old to run a package script, before generating anything', () => {
    const dir = fakeBinDirSync([
      '#!/bin/sh',
      'if [ "$1" = "--version" ]; then echo 3.7.5; exit 0; fi',
      'echo "Usage: pnpm [command] [flags]"',
      'exit 0',
    ].join('\n'))

    const { status, output } = run(dir)

    expect(status).toBe(1)
    expect(output).toContain("cannot run this repo's package scripts")
    // The diagnosis: what was found, what is needed, and WHY exit 0 is the
    // trap. Without the last of these the reader upgrades pnpm without ever
    // learning why the NSwag error was pointing at the wrong thing.
    expect(output).toContain('pnpm 3.7.5')
    expect(output).toContain('EXITING 0')
    expect(output).toContain('FileNotFoundException')
    // And the fix, not just the diagnosis - the same requirement the dotnet
    // branches above are held to.
    expect(output).toContain('export PATH=')
    // It must stop BEFORE the generator: nothing downstream may have run.
    expect(output).not.toContain('--- generated ---')
  })

  /**
   * Branch: the output assertion after `pnpm openapi`.
   *
   * The stub reports the REQUIRED major, so the version check above is
   * satisfied and cannot be what fires. That makes these two tests each
   * other's vacuity control: a version check that refused unconditionally
   * would fail this test, and an output assertion that fired unconditionally
   * would fire in the test above, where it is asserted absent.
   */
  it('refuses a generation that exits 0 having written no document', () => {
    const dir = fakeBinDirSync([
      '#!/bin/sh',
      'if [ "$1" = "--version" ]; then echo 9.12.0; exit 0; fi',
      'exit 0',
    ].join('\n'))

    const { status, output } = run(dir)

    expect(status).toBe(1)
    expect(output).toContain('exited 0 but produced no usable OpenAPI document')
    expect(output).toContain('no such file')
    // The version check is satisfied here, so its message must be ABSENT -
    // this is what pins that the two guards are independent rather than one
    // guard firing twice.
    expect(output).not.toContain('cannot run this repo')
    expect(output).not.toContain('--- generated ---')
  })

  /**
   * Branch: the same assertion's JSON parse. A file that exists and is
   * non-empty but truncated is the other way the document can be useless,
   * and NSwag's complaint about it is no more legible than its complaint
   * about a missing one - so `-s` alone would leave half the condition
   * unguarded.
   */
  it('refuses a document that was written but is not valid JSON', () => {
    const dir = fakeBinDirSync([
      '#!/bin/sh',
      'if [ "$1" = "--version" ]; then echo 9.12.0; exit 0; fi',
      'printf \'{"openapi":"3.1.0","paths":{\' > "$OPENAPI_OUTPUT_PATH"',
      'exit 0',
    ].join('\n'))

    const { status, output } = run(dir)

    expect(status).toBe(1)
    expect(output).toContain('exited 0 but produced no usable OpenAPI document')
    expect(output).toContain('is not valid JSON')
    expect(output).not.toContain('no such file')
  })
})

/**
 * Synchronous sibling of fakeBinDir, for the spawn-based tests above: those
 * call execFileSync, so there is nothing to await against and an async
 * helper would only add a `then` around a directory that has to exist before
 * the spawn anyway.
 */
function fakeBinDirSync(script: string, name = 'pnpm'): string {
  const dir = mkdtempSync(join(tmpdir(), 'broodline-contract-guard-'))
  const bin = join(dir, name)
  writeFileSync(bin, script, 'utf8')
  chmodSync(bin, 0o755)
  return dir
}
