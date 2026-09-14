import type { ChildProcess } from 'node:child_process'

/**
 * Kills a spawned child when THIS process goes away, not only when the
 * test file's afterAll gets to run.
 *
 * WHY THIS EXISTS. wave-submit.test.ts, replays.test.ts and
 * adversarial.test.ts each spawn a real sim host - a `dotnet` process
 * holding 5299, 5399 and 5499 respectively - in beforeAll, and kill it in
 * afterAll. afterAll is not a guarantee. vitest runs each test file in a
 * forked worker and terminates that worker by signal on its own teardown
 * paths (an interrupted run, a hung file hitting the runner's kill timeout,
 * a `--bail` stop), and a worker that dies by signal runs no afterAll at
 * all. The sim host is then orphaned, keeps its port, and the NEXT run of
 * that file fails to bind with "address already in use" - an error that
 * names nothing about what actually went wrong.
 *
 * That is an observed leak, not a projected one: this work started with a
 * `dotnet Broodline.Sim.Service.dll --urls http://127.0.0.1:5299` from an
 * earlier run of wave-submit.test.ts still holding the port, hours after
 * the run that spawned it had finished.
 *
 * WHAT IT DOES NOT COVER, stated rather than implied: SIGKILL. There is no
 * handler for it by definition, so a hard-killed worker still orphans its
 * child. Closing that needs the child to watch the parent (PDEATHSIG, or a
 * supervisor), which is a native-code dependency this suite does not have
 * and cannot justify for a test host. What is covered is every path where
 * the worker is asked to stop rather than shot: normal exit, SIGTERM,
 * SIGINT, SIGHUP.
 *
 * Deliberately free of app imports - `node:child_process` types only - so
 * it can be loaded by a bare `node --experimental-strip-types` process.
 * child-reaper.test.ts's fixture does exactly that, and could not if this
 * lived in wave-helpers.ts or harness.ts.
 */
export function reapOnExit(proc: ChildProcess): () => void {
  const kill = () => {
    try {
      // SIGTERM, not SIGKILL: the sim host handles it and shuts down
      // cleanly, and kill(2) has already delivered it by the time this
      // returns - so the signal lands even though this process is about to
      // exit and will never see the child reaped.
      proc.kill('SIGTERM')
    } catch {
      // already gone, or never started
    }
  }

  // Synchronous by contract - 'exit' listeners may not defer work. This is
  // the branch that covers an ordinary process.exit() taken before or
  // instead of afterAll.
  const onExit = () => kill()

  // A signal listener SUPPRESSES node's default disposition, so each of
  // these must exit by hand or the worker would hang holding the signal
  // that was meant to stop it. 128+n is what a parent reads as "died of
  // SIGn"; exiting 0 here would report an interrupted run as a clean one.
  const onSignal = (signal: NodeJS.Signals, status: number) => () => {
    kill()
    process.exit(status)
  }
  const handlers: Array<[NodeJS.Signals, () => void]> = [
    ['SIGTERM', onSignal('SIGTERM', 143)],
    ['SIGINT', onSignal('SIGINT', 130)],
    ['SIGHUP', onSignal('SIGHUP', 129)],
  ]

  process.on('exit', onExit)
  for (const [signal, handler] of handlers) process.on(signal, handler)

  return () => {
    process.off('exit', onExit)
    for (const [signal, handler] of handlers) process.off(signal, handler)
  }
}
