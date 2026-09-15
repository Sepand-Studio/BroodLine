import type { Socket } from 'node:net'
import { PostgreSqlContainer, type StartedPostgreSqlContainer } from '@testcontainers/postgresql'
import type pg from 'pg'
import { afterAll, beforeAll, describe, expect, it, vi } from 'vitest'
import { createPool } from '../src/db/client.ts'
import { drainPool } from './harness.ts'

/**
 * The two halves of the teardown flake that failed this suite roughly one
 * run in seven: a pool whose end() resolves before its sockets close, and a
 * pool with no 'error' listener to catch what arrives on one of them.
 *
 * No migrations and no app here on purpose - this is about the pool and the
 * socket underneath it, so a bare container is the whole fixture. One
 * container for the file, since neither test writes anything.
 */
let container: StartedPostgreSqlContainer
let uri: string

beforeAll(async () => {
  container = await new PostgreSqlContainer('postgres:16-alpine').start()
  uri = container.getConnectionUri()
})

afterAll(async () => { await container?.stop() })

/**
 * Collect the underlying sockets via the pool's public 'connect' event
 * rather than reaching into pool._idle - the array is pg-pool's private
 * bookkeeping and is emptied during teardown, which is precisely the
 * window these tests are about. Must be attached before any query.
 */
function trackSockets(pool: pg.Pool): Socket[] {
  const sockets: Socket[] = []
  pool.on('connect', (client) => {
    const stream = (client as unknown as { connection?: { stream?: Socket } }).connection?.stream
    if (stream) sockets.push(stream)
  })
  return sockets
}

async function openThreeConnections(pool: pg.Pool): Promise<void> {
  // Three concurrent queries, so the pool must open three clients rather
  // than serving all three from one - a single socket would let a drain
  // bug hide behind a lucky ordering.
  await Promise.all([pool.query('SELECT 1'), pool.query('SELECT 1'), pool.query('SELECT 1')])
}

describe('pool teardown', () => {
  it('pool.end() on its own resolves with the sockets still open', async () => {
    // The control. Without it the test below is unfalsifiable: if a future
    // pg made end() actually await its closes, "sockets are shut after
    // drainPool" would pass for a reason that has nothing to do with
    // drainPool, and the wait it performs would be dead code nobody
    // noticed. If THIS test starts failing, drainPool is what to re-examine.
    const pool = createPool(uri)
    const sockets = trackSockets(pool)
    await openThreeConnections(pool)
    expect(sockets).toHaveLength(3)

    await pool.end()
    expect(sockets.some((s) => s.destroyed === false)).toBe(true)
  })

  it('drainPool does not resolve until those sockets are really closed', async () => {
    const pool = createPool(uri)
    const sockets = trackSockets(pool)
    await openThreeConnections(pool)
    expect(sockets.map((s) => s.destroyed)).toEqual([false, false, false])

    await drainPool(pool)

    // This is the property harness.stop() depends on before it lets
    // container.stop() send Postgres a shutdown signal. Drop drainPool's
    // `await drained` and these are still false.
    expect(sockets.map((s) => s.destroyed)).toEqual([true, true, true])
  })

  it('an idle client whose backend is terminated is logged, not thrown out as an uncaught exception', async () => {
    const victim = createPool(uri)
    const killer = createPool(uri)
    const logged = vi.spyOn(console, 'error').mockImplementation(() => {})

    try {
      // createPool's handler, and nothing else. At zero, Node rethrows what
      // the pool emits, from inside a socket data handler where no
      // try/catch up the stack can reach it - which is how a FATAL on an
      // idle connection became a whole-run failure with every test green.
      expect(victim.listenerCount('error')).toBe(1)

      const { rows } = await victim.query<{ pid: number }>('SELECT pg_backend_pid() AS pid')
      const backendPid = rows[0]!.pid

      // The client is now IDLE in victim's pool, holding pg-pool's internal
      // idleListener as its only 'error' listener - the exact state the
      // flake caught it in. pg_terminate_backend produces the same SQLSTATE
      // and the same message a container stop does (57P01, "terminating
      // connection due to administrator command"), so this reproduces the
      // captured error without having to lose a timing race on purpose.
      await killer.query('SELECT pg_terminate_backend($1)', [backendPid])

      await vi.waitFor(() => { expect(logged).toHaveBeenCalled() }, { timeout: 10_000 })
      const err = logged.mock.calls[0]![1] as { code?: string; severity?: string }
      expect(err.code).toBe('57P01')
      expect(err.severity).toBe('FATAL')
    } finally {
      logged.mockRestore()
      await drainPool(killer)
      await drainPool(victim)
    }
  })
})
