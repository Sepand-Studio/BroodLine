import { defineConfig } from 'vitest/config'

export default defineConfig({
  test: {
    // Testcontainers pulls and boots a real Postgres; the default 5s is not
    // enough for the first run on a cold image cache.
    testTimeout: 60_000,
    hookTimeout: 120_000,
    env: {
      JWT_SECRET: 'test-secret-that-is-at-least-32-characters-long',
    },
  },
})
