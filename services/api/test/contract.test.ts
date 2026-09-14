import { execFileSync, spawn } from 'node:child_process'
import { readFile } from 'node:fs/promises'
import { createServer } from 'node:net'
import { fileURLToPath } from 'node:url'
import { describe, expect, it } from 'vitest'

const REPO = fileURLToPath(new URL('../../../', import.meta.url))
const SIM_PORT = 5199 // generate-contract.sh's own harness port

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

  /**
   * The `servers` strip, pinned directly rather than only via the diff above.
   *
   * MapOpenApi fills `servers` in from the address the document was fetched
   * from, which for this contract is generate-contract.sh's local harness
   * host - so the committed description for a service that will run on Cloud
   * Run was shipping `http://127.0.0.1:5199/` as its base URL, and any
   * generated client honouring `servers` points at localhost.
   *
   * Two assertions, not one, and what each one actually covers was
   * established by weakening the strip two different ways rather than
   * assumed:
   *
   *  - Strip REMOVED FROM THE SCRIPT: both fail. The gate above fails with
   *    ` M openapi/sim.json` (regeneration re-adds the block, tree dirty);
   *    this one fails with `expected [ { url: 'http://127.0.0.1:5199/' } ]
   *    to be undefined`. Only the second names the defect - the first says
   *    a file changed and leaves the reader to work out which key and why.
   *    That difference is this test's entire justification.
   *  - Block RE-ADDED TO THE COMMITTED DOCUMENT by hand: the gate above
   *    fails (`MM openapi/sim.json`). This one does NOT, and cannot: the
   *    gate regenerates sim.json before this test reads it, so by the time
   *    it runs the hand-added block is already gone. Stated rather than
   *    implied, because the reverse is the obvious thing to assume about an
   *    assertion on a committed file, and assuming it would make this test
   *    look like cover it does not provide.
   *
   * No regeneration of its own on purpose: it reads whatever is on disk,
   * which costs nothing and is the same bytes the gate just vouched for.
   */
  it('does not commit a loopback server URL into the sim contract', async () => {
    const doc = JSON.parse(await readFile(new URL('../../../openapi/sim.json', import.meta.url), 'utf8'))

    expect(doc.servers).toBeUndefined()
    // Belt: the URL must not reappear anywhere else in the document either
    // (an `x-` extension, a description, a future `servers` under a path
    // item), since any of those would put it back in front of a generator.
    expect(JSON.stringify(doc)).not.toContain('127.0.0.1:5199')
  })

  /**
   * The interrupt path: an interrupted run must not leave the sim host
   * holding port 5199, because the next run then fails with an "address
   * already in use" bind error that has nothing to do with what broke.
   *
   * WHY SIGINT, AND WHY DELIVERED TO THE PID ALONE. `trap cleanup EXIT` in
   * generate-contract.sh already covers Ctrl-C (a GROUP SIGINT), vitest's
   * SIGTERM and SIGHUP - bash runs the EXIT trap before re-raising those
   * with their default disposition, measured against the real script. The
   * one case it does not cover is a SIGINT delivered to the script's pid
   * alone: bash terminates on SIGINT only when the foreground child it is
   * waiting on also died of SIGINT, so with the child untouched the
   * interrupt is swallowed and the script runs to completion. That is the
   * hole the script's `trap 'on_signal INT' INT` closes, and it is
   * therefore the only signal this test can deliver and still be a test -
   * SIGTERM passes with the handlers deleted, so asserting on it would
   * prove nothing. Branch-mutation checked: deleting ONLY that one trap
   * line makes this test fail (the script exits 0, having ignored the
   * signal and finished the run), while the gate above stays green.
   *
   * python3 is a trampoline, not decoration. A process started
   * asynchronously by a non-interactive shell has SIGINT set to IGNORED,
   * and that disposition is inherited through exec - bash cannot reset a
   * signal that was ignored on entry, so spawning the script directly makes
   * this test's result depend on how the test runner itself was launched.
   * Restoring SIG_DFL before exec removes that dependency. python3 rather
   * than perl because generate-contract.sh already requires python3.
   */
  it('frees port 5199 when interrupted, instead of leaving the sim host behind', async () => {
    const child = spawn('python3', [
      '-c',
      'import signal,os,sys; signal.signal(signal.SIGINT, signal.SIG_DFL); os.execv(sys.argv[1], sys.argv[1:])',
      './implementation/scripts/generate-contract.sh',
    ], { cwd: REPO, stdio: 'ignore' })

    const exited = new Promise<{ code: number | null; signal: string | null }>((resolve) => {
      child.on('exit', (code, signal) => resolve({ code, signal }))
    })

    try {
      // Wait for the sim host to actually be serving. Signalling before it
      // is up would test nothing: with no SIM_PID there is nothing to leak,
      // and the test would pass against a script with no teardown at all.
      let up = false
      for (let i = 0; i < 480 && !up; i++) {
        try {
          up = (await fetch(`http://127.0.0.1:${SIM_PORT}/healthz`)).ok
        } catch {
          // not up yet
        }
        if (!up) await new Promise((r) => setTimeout(r, 250))
      }
      expect(up, 'the sim host never came up, so the interrupt would prove nothing').toBe(true)

      child.kill('SIGINT')

      const result = await Promise.race([
        exited,
        new Promise<never>((_, reject) =>
          setTimeout(() => reject(new Error('the script did not exit within 30s of SIGINT')), 30_000)),
      ])

      // 130, not "killed by SIGINT" and not 0. 0 is what the unguarded
      // script returns: signal swallowed, run completed.
      expect(result).toEqual({ code: 130, signal: null })

      // The property that actually matters: the NEXT run can bind. Asserted
      // by binding it, rather than by parsing lsof - this is the same
      // syscall the next run makes.
      await expect(bindable(SIM_PORT)).resolves.toBe(true)

      // And no orphaned host survives without the port (a process that has
      // released the socket but is still running is still a leak).
      expect(straySimPids()).toEqual([])
    } finally {
      child.kill('SIGKILL')
      for (const pid of straySimPids()) {
        try {
          process.kill(pid, 'SIGKILL')
        } catch {
          // already gone
        }
      }
    }
  }, 300_000)
})

/** Whether this process can bind the port right now. Closes it again immediately. */
function bindable(port: number): Promise<boolean> {
  return new Promise((resolve) => {
    const server = createServer()
    server.once('error', () => resolve(false))
    server.listen(port, '127.0.0.1', () => server.close(() => resolve(true)))
  })
}

/** Any sim host still running against SIM_PORT, by command line. */
function straySimPids(): number[] {
  try {
    return execFileSync('pgrep', [
      '-f', `Broodline.Sim.Service.dll --urls http://127.0.0.1:${SIM_PORT}`,
    ], { encoding: 'utf8' })
      .split('\n').map((s) => s.trim()).filter(Boolean).map(Number)
  } catch {
    // pgrep exits 1 when nothing matches - the expected case here.
    return []
  }
}
