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
   *  - Block RE-ADDED BY HAND and STAGED OR COMMITTED: the gate above
   *    fails (`MM`/` M openapi/sim.json`). This one does NOT, and cannot:
   *    the gate regenerates sim.json before this test reads it, so by the
   *    time it runs the hand-added block is already gone.
   *  - Block RE-ADDED BY HAND and left UNSTAGED: NOTHING fails. The
   *    regeneration rewrites the file before `git status` is consulted, so
   *    the edit is erased and the tree is clean. That case is not covered by
   *    anything here, and saying so is the point - an unstaged edit to a
   *    generated file is undone rather than caught, which is the correct
   *    behaviour for a regenerating gate but is NOT the same claim as
   *    "a hand-re-added block fails the build".
   *
   * All three measured, not reasoned about. The middle one is stated
   * explicitly because "an assertion on the committed document catches a
   * hand edit" is the obvious thing to assume and is false; the third
   * because an earlier version of this comment claimed coverage it does not
   * have.
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
   * THE python3 TRAMPOLINE IS BELT-AND-BRACES, and an earlier version of
   * this comment overstated it as necessary. The hazard is real but does
   * not reach here: a process started asynchronously by a non-interactive
   * shell has SIGINT set to IGNORED, that disposition survives exec, and
   * bash cannot reset a signal ignored on entry - which is exactly what
   * silently invalidated the first hand probes of this behaviour, run from
   * a shell. It does NOT apply to a child of node: libuv resets every
   * signal to SIG_DFL in the child before exec, verified directly, so
   * spawning the script straight from here would get a default SIGINT no
   * matter how the runner was launched. The trampoline is kept because it
   * makes the requirement explicit at the one place that depends on it and
   * costs nothing - generate-contract.sh already requires python3 - not
   * because the test would otherwise be environment-dependent.
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

  /**
   * A dead sim host must be reported as DEAD, with its exit status - not as
   * a timeout.
   *
   * WHY THIS IS THE DELIVERABLE AND NOT A NICETY. The readiness loop used to
   * poll curl 40 times on a fixed schedule and never ask whether $SIM_PID was
   * still alive, so a host killed before it bound the port and a host that is
   * merely slow produced the same ten seconds of silence and the same "never
   * became ready" - which reads as a hang. That is not a missed diagnosis, it
   * is a MANUFACTURED one: generate-contract.sh carried, for weeks, the claim
   * that `dotnet run --project` "reliably hung with zero output for the full
   * length of every timeout tried (confirmed up to 90s)". It does not hang. It
   * is SIGKILLed instantly - exit 137, zero output - and the loop's inability
   * to tell those apart is the only reason the false version looked true.
   *
   * HOW THE HOST IS MADE TO DIE, AND WHY NOTHING IN THE SCRIPT IS TEST-AWARE.
   * The port is occupied before the script starts, so its sim host cannot bind
   * and aborts during startup. That needs no test-only hook and no stubbed
   * launch path: the real `dotnet <dll> --urls ...` line runs unchanged, and a
   * stale process holding 5199 is a real failure mode - the one the script's
   * own cleanup() comment exists to prevent the NEXT run from hitting. Nothing
   * in generate-contract.sh branches on an environment variable for this.
   *
   * TWO MEASUREMENT TRAPS, both hit while building this and both load-bearing:
   *
   *  - The squatter must DESTROY each connection, not accept and hold it. The
   *    readiness loop's `curl -fsS` has no --max-time, so a listener that
   *    accepts and never answers blocks it forever: the test would hang rather
   *    than fail, against fixed and unfixed script alike.
   *  - The script must be spawned ASYNCHRONOUSLY. execFileSync/spawnSync block
   *    this process's event loop, so the squatter never runs its destroy
   *    handler - while the kernel still completes the handshake into the
   *    listen backlog, which is precisely the accept-and-never-answer hang
   *    above. The sibling interrupt test can use `spawn` for its own reasons;
   *    here it is a correctness requirement.
   *
   * WHAT IS ASSERTED, AND WHY THE STATUS MUST BE NON-ZERO. `exit status \d+`
   * alone would also match the value SIM_EXIT is pre-set to, so a capture that
   * silently failed would still pass. A host that loses the port never exits
   * 0, so requiring a non-zero status pins that the status was really read
   * back off the dead child rather than defaulted. The exact number is NOT
   * pinned: it is 134 (SIGABRT) for this cause on this machine and 137
   * (SIGKILL) for the cause above, and the branch is the same one either way.
   *
   * Branch-mutation checked, twice, against this specific branch rather than
   * the enclosing loop - see the report for the exact edits and results.
   */
  it('reports a sim host that died, with its exit status, instead of calling it a timeout', async () => {
    const squatter = createServer((socket) => socket.destroy())
    await new Promise<void>((resolve, reject) => {
      squatter.once('error', reject)
      squatter.listen(SIM_PORT, '127.0.0.1', resolve)
    })

    const child = spawn('./implementation/scripts/generate-contract.sh', {
      cwd: REPO,
      stdio: ['ignore', 'ignore', 'pipe'],
    })
    let stderr = ''
    child.stderr?.setEncoding('utf8')
    child.stderr?.on('data', (chunk: string) => { stderr += chunk })

    // 'close', not 'exit': stderr must be drained before it is asserted on.
    const closed = new Promise<{ code: number | null; signal: string | null }>((resolve) => {
      child.on('close', (code, signal) => resolve({ code, signal }))
    })

    try {
      const result = await Promise.race([
        closed,
        new Promise<never>((_, reject) =>
          setTimeout(() => reject(new Error('the script did not exit within 120s - the readiness loop is blocked, not merely slow')), 120_000)),
      ])

      expect(result).toEqual({ code: 1, signal: null })

      // The host died, and the message says so and names what it died of.
      expect(stderr).toMatch(/died before serving \/healthz \(exit status [1-9]\d*/)
      // And does NOT claim the opposite. Without the liveness check this is
      // the line that is printed, and it is false: the host is not running.
      expect(stderr).not.toContain('never served /healthz within the timeout')
    } finally {
      child.kill('SIGKILL')
      // No lingering sockets to drain: every connection was destroyed on
      // arrival, so close() cannot wait on one. (net.Server has no
      // closeAllConnections - that is http.Server's.)
      await new Promise<void>((resolve) => squatter.close(() => resolve()))
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
