import { execFileSync } from 'node:child_process'
import { createServer } from 'node:net'
import { fileURLToPath, pathToFileURL } from 'node:url'

/**
 * What `pnpm --filter @broodline/api test` needs from the machine, checked
 * before the suite rather than discovered inside it.
 *
 * THE REQUIREMENT IS NOT REMOVABLE, and this file is not a step towards
 * removing it. contract.test.ts regenerates the api -> sim contract from a
 * RUNNING sim host, and wave-submit.test.ts / replays.test.ts /
 * adversarial.test.ts submit real replays to a real one. A fake sim would be
 * a second implementation of the exact boundary those tests exist to prove,
 * so every one of those guarantees would survive the substitution and mean
 * nothing. See ../README.md.
 *
 * What IS fixable is the failure being illegible. Without this, a machine
 * with no .NET SDK fails several files deep with `spawn dotnet ENOENT`
 * inside a 240s beforeAll; an occupied port fails with a bind error from a
 * child whose stdio is 'ignore', so nothing is printed at all; and an
 * unreachable NuGet fails inside `dotnet nswag` with a restore error
 * attributed to the contract generator. Three different real causes, none
 * of them named where they happen.
 *
 * Each condition gets its OWN message naming what is missing and how to fix
 * it. One generic "your environment is not set up" line would be the same
 * defect wearing a different hat.
 */

/** Every port the suite's sim hosts bind, and the file that binds each. */
export const SIM_PORTS: ReadonlyArray<{ port: number; owner: string }> = [
  { port: 5199, owner: 'implementation/scripts/generate-contract.sh, via contract.test.ts' },
  { port: 5299, owner: 'test/wave-submit.test.ts' },
  { port: 5399, owner: 'test/replays.test.ts' },
  { port: 5499, owner: 'test/adversarial.test.ts' },
  { port: 5599, owner: 'test/loop.test.ts' },
  { port: 5699, owner: 'test/founder.test.ts' },
  { port: 5799, owner: 'test/wave6-pale.test.ts' },
]

const REPO = fileURLToPath(new URL('../../../', import.meta.url))

export interface PreflightFailure {
  /** Short machine-ish tag, so tests can assert on the BRANCH, not the prose. */
  kind: 'dotnet-missing' | 'dotnet-broken' | 'tool-restore-failed' | 'port-in-use'
  message: string
}

/** Whatever a failed execFileSync managed to print, as one trimmed string. */
function outputOf(err: unknown): string {
  const e = err as { stdout?: Buffer; stderr?: Buffer }
  return `${e.stdout?.toString() ?? ''}${e.stderr?.toString() ?? ''}`.trim()
}

/** Indents a tool's own output so it reads as a quotation rather than as ours. */
function quoted(output: string): string[] {
  return output ? ['', `  It said:\n${output.split('\n').map((l) => `    ${l}`).join('\n')}`] : []
}

/**
 * Branch one: no .NET SDK at all - AND, separately, branch one-and-a-half:
 * an SDK that is there but cannot run.
 *
 * THE SPLIT IS THE POINT. An earlier version of this treated any non-zero
 * `dotnet --version` as "not on PATH", which reproduced inside this file
 * the exact defect the file exists to fix. `dotnet` exits 1 while sitting
 * right there on PATH for at least three ordinary reasons - a global.json
 * pinning an SDK version that is not installed, a corrupt install, and an
 * architecture mismatch (the `Bad CPU type` shape this repo already hits on
 * its node binary). In every one of those, `dotnet --version` prints the
 * one line that says what is actually wrong, and "install the .NET SDK"
 * both discards that line and tells the reader to install something they
 * already have.
 *
 * ENOENT - and only ENOENT - means the command could not be found. Verified
 * against node rather than assumed: an absent binary throws with
 * `code: 'ENOENT'` and `status: null`; a binary that runs and exits 1
 * throws with `code: undefined`, `status: 1` and a populated `stderr`; a
 * present-but-unexecutable file throws `code: 'EACCES'`. So the first is
 * "missing" and everything else is "broken", with the tool's own output
 * forwarded the way checkToolRestore already forwards it.
 *
 * `env` is a parameter so preflight.test.ts can drive these branches with a
 * PATH that contains no dotnet, or one containing a stub that fails - node
 * resolves the command through options.env.PATH, verified, so this needs no
 * other seam.
 */
export function checkDotnet(env: NodeJS.ProcessEnv = process.env): PreflightFailure | undefined {
  try {
    execFileSync('dotnet', ['--version'], { env, stdio: 'pipe' })
    return undefined
  } catch (err) {
    if ((err as { code?: string }).code === 'ENOENT') {
      return {
        kind: 'dotnet-missing',
        message: [
          'preflight: `dotnet` is not on PATH.',
          '',
          "  This package's tests run the REAL sim service (a .NET project) as a child",
          '  process, and regenerate the api -> sim contract from it. That is deliberate and',
          '  cannot be worked around by faking the sim - see services/api/README.md.',
          '',
          '  Install the .NET SDK (10.x):',
          '    macOS:  brew install --cask dotnet-sdk',
          '    other:  https://dotnet.microsoft.com/download',
          '  then make sure `dotnet --version` works in this shell and re-run.',
        ].join('\n'),
      }
    }

    const output = outputOf(err)
    const errno = (err as { code?: string }).code
    return {
      kind: 'dotnet-broken',
      message: [
        'preflight: `dotnet` is on PATH but `dotnet --version` failed.',
        '',
        '  The SDK is installed; something about this machine stops it running. Common',
        '  causes: a global.json pinning an SDK version that is not installed, a',
        '  half-finished or corrupt install, or a binary built for another architecture.',
        '',
        '  Do NOT reinstall blind - the output below says which. Reproduce it with:',
        '    dotnet --version',
        ...(output
          ? quoted(output)
          : ['', `  It printed nothing; the spawn itself failed with ${errno ?? 'an unknown error'}.`]),
      ].join('\n'),
    }
  }
}

/**
 * Branch two: a .NET SDK that cannot restore the repo's tool manifest -
 * no network, a blocked or misconfigured NuGet feed, an empty cache.
 */
export function checkToolRestore(env: NodeJS.ProcessEnv = process.env): PreflightFailure | undefined {
  try {
    execFileSync('dotnet', ['tool', 'restore'], { cwd: REPO, env, stdio: 'pipe' })
    return undefined
  } catch (err) {
    const output = outputOf(err)
    return {
      kind: 'tool-restore-failed',
      message: [
        'preflight: `dotnet tool restore` failed.',
        '',
        '  .config/dotnet-tools.json pins NSwag 14.2.0, which the contract generator',
        '  (implementation/scripts/generate-contract.sh, run by contract.test.ts) needs.',
        '  Restoring it requires reaching the configured NuGet feed, normally',
        '  https://api.nuget.org - so this usually means no network, a proxy that needs',
        '  configuring, or a feed blocked by policy.',
        '',
        '  Reproduce it directly with:  dotnet tool restore',
        ...quoted(output),
      ].join('\n'),
    }
  }
}

/**
 * Branch three: a sim port already taken.
 *
 * Checked by BINDING it, which is the same syscall the sim host is about to
 * make - not by parsing lsof, and not by connecting, which would miss a
 * socket bound by a process that is not accepting.
 */
export function checkPort(port: number, owner: string): Promise<PreflightFailure | undefined> {
  return new Promise((resolve) => {
    const server = createServer()
    server.once('error', () => resolve({
      kind: 'port-in-use',
      message: [
        `preflight: port ${port} is already in use.`,
        '',
        `  ${owner} binds 127.0.0.1:${port} to run a sim host, and will fail with an`,
        '  "address already in use" error that names nothing about what is holding it.',
        '',
        '  Most often this is an orphaned sim host from an earlier run that was killed',
        '  before its teardown could run. Find and stop it with:',
        `    lsof -nP -i :${port} -sTCP:LISTEN`,
        `    kill <pid>`,
      ].join('\n'),
    }))
    server.listen(port, '127.0.0.1', () => server.close(() => resolve(undefined)))
  })
}

/**
 * Every branch, in dependency order: there is no point reporting a failed
 * restore on a machine whose `dotnet` is missing OR unrunnable - the restore
 * would fail for that same reason and add a second, derived message on top
 * of the real one. Ports are independent of both and are always reported.
 */
export async function preflight(env: NodeJS.ProcessEnv = process.env): Promise<PreflightFailure[]> {
  const failures: PreflightFailure[] = []

  const sdk = checkDotnet(env)
  if (sdk) failures.push(sdk)
  else {
    const restore = checkToolRestore(env)
    if (restore) failures.push(restore)
  }

  for (const { port, owner } of SIM_PORTS) {
    const busy = await checkPort(port, owner)
    if (busy) failures.push(busy)
  }

  return failures
}

// CLI entry: `node --experimental-strip-types test/preflight.ts`, wired into
// this package's `test` script ahead of vitest. Guarded so importing this
// module from preflight.test.ts does not exit the test runner.
if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  const failures = await preflight()
  if (failures.length > 0) {
    for (const failure of failures) console.error(`\n${failure.message}\n`)
    console.error(
      `preflight: ${failures.length} problem${failures.length === 1 ? '' : 's'} above. ` +
      'The suite was not started.\n',
    )
    process.exit(1)
  }
}
