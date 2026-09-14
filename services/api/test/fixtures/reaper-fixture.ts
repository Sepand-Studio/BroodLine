/**
 * A stand-in for a sim-hosting test file, driven by child-reaper.test.ts.
 *
 * Spawns a long-lived grandchild (the sim host's role), optionally installs
 * reapOnExit over it, prints the grandchild's pid so the test can watch it,
 * and then either stays alive until it is signalled or exits on its own -
 * which is the whole point: the test asks whether the grandchild outlived
 * this process, by each of the routes reapOnExit claims to cover.
 *
 *   argv[2]  'guarded' | 'unguarded'  - whether reapOnExit is installed.
 *   argv[3]  'signal'  | 'exit'       - how this process goes away.
 *
 * The unguarded mode is the mutation this fixture exists to make runnable:
 * it is the same file with the single reapOnExit call removed, so the test
 * can assert that without it the grandchild survives every route.
 *
 * Not named *.test.ts on purpose - vitest's include glob would otherwise
 * collect it as a suite and run it, where it would hang forever.
 */
import { spawn } from 'node:child_process'
import { reapOnExit } from '../child-reaper.ts'

const guarded = process.argv[2] === 'guarded'
const how = process.argv[3] ?? 'signal'

// A grandchild that will not exit on its own, so "is it still alive?" has
// exactly one cause: whether anything killed it.
const child = spawn(process.execPath, ['-e', 'setInterval(() => {}, 1000)'], { stdio: 'ignore' })

if (guarded) reapOnExit(child)

child.on('spawn', () => {
  process.stdout.write(`GRANDCHILD ${child.pid}\n`)
  if (how === 'exit') {
    // A short delay, not an immediate exit: process.exit() can truncate a
    // pending write to a pipe, and the test cannot watch a pid it never
    // received. This is the ordinary-exit route - no signal is involved,
    // so it exercises reapOnExit's 'exit' listener specifically.
    setTimeout(() => process.exit(0), 150)
  }
})

setInterval(() => {}, 1000)
