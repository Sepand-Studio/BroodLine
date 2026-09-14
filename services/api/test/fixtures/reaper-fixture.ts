/**
 * A stand-in for a sim-hosting test file, driven by child-reaper.test.ts.
 *
 * Spawns a long-lived grandchild (the sim host's role), optionally installs
 * reapOnExit over it, prints the grandchild's pid so the test can watch it,
 * and then stays alive until it is signalled - which is the whole point:
 * the test signals THIS process and asks whether the grandchild outlived it.
 *
 * argv[2] is 'guarded' or 'unguarded'. The unguarded mode is the mutation
 * this fixture exists to make runnable: it is the same file with the single
 * reapOnExit call removed, so the test can assert that without it the
 * grandchild survives.
 *
 * Not named *.test.ts on purpose - vitest's include glob would otherwise
 * collect it as a suite and run it, where it would hang forever.
 */
import { spawn } from 'node:child_process'
import { reapOnExit } from '../child-reaper.ts'

const guarded = process.argv[2] === 'guarded'

// A grandchild that will not exit on its own, so "is it still alive?" has
// exactly one cause: whether anything killed it.
const child = spawn(process.execPath, ['-e', 'setInterval(() => {}, 1000)'], { stdio: 'ignore' })

if (guarded) reapOnExit(child)

child.on('spawn', () => {
  process.stdout.write(`GRANDCHILD ${child.pid}\n`)
})

setInterval(() => {}, 1000)
