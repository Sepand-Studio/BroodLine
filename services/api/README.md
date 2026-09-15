# @broodline/api

The authoritative game server: accounts and identity, the money ledger, wave
issuance and submission, config bundles, and replay storage.

```
pnpm --filter @broodline/api dev         # watch-mode server
pnpm --filter @broodline/api typecheck   # tsc --noEmit
pnpm --filter @broodline/api test        # preflight, then the full suite
```

## Running the tests needs more than Node

`pnpm --filter @broodline/api test` does **not** run on a Node-only machine.
It needs, in addition to Node 22+ and pnpm:

| Requirement | Why | Used by |
| --- | --- | --- |
| **Docker** (running) | Real Postgres, via testcontainers | every file that touches the database |
| **.NET SDK 10.x** (`dotnet` on PATH) | Builds and runs the real sim service | `contract.test.ts`, `wave-submit.test.ts`, `replays.test.ts`, `adversarial.test.ts`, `loop.test.ts` |
| **Network access to nuget.org** | `dotnet tool restore` fetches NSwag 14.2.0, pinned in `.config/dotnet-tools.json` | `contract.test.ts`, via `implementation/scripts/generate-contract.sh` |
| **Free TCP ports 5199, 5299, 5399, 5499, 5599** | One sim host per test file, each on its own port so the files can run in parallel | as above, one port each |

`test/preflight.ts` runs before vitest and checks the .NET half of that list,
failing with a message that names the specific problem and how to fix it. It
exists because each of these used to fail several files deep inside a 240-second
`beforeAll`: a missing SDK as `spawn dotnet ENOENT`, an occupied port as nothing
at all (the sim host's stdio is `ignore`, so its bind error is discarded), and an
unreachable feed as a restore error attributed to the contract generator.

The preflight does not check Docker — testcontainers already fails quickly and
legibly when the daemon is not reachable.

### Why the sim is not faked, and will not be

The obvious way to make this package run anywhere is to stand a stub in for the
sim service. It is not on the table, and the reason is not inertia:

- **`contract.test.ts` regenerates `openapi/sim.json` from a running sim host.**
  The document is composed by `MapOpenApi` from the endpoints as actually
  registered, which is the only description of that service that cannot drift
  from what it really serves. Generated from a stub, it would describe the stub.
- **`wave-submit.test.ts`, `replays.test.ts`, `adversarial.test.ts` and
  `loop.test.ts` submit real replays and assert on the real ACCEPT/REJECT
  split.** Phase 5's entire claim is that the server, not the client, decides
  whether a wave was won, and Phase 6's end-to-end drive earns its creatures
  across that same boundary. A stub sim is a second implementation of it — so every one of
  those assertions would still pass, and none of them would mean anything.

A fake would not make those tests cheaper to run. It would make them stop
testing the thing they exist for, while continuing to report success. If you
need to work on this package without a .NET SDK, run the files that do not need
one (`vitest run test/ledger.test.ts test/identity.test.ts …`) rather than
substituting the sim; the preflight only gates the `test` script.

## Sim ports

| Port | Bound by |
| --- | --- |
| 5199 | `implementation/scripts/generate-contract.sh`, run by `contract.test.ts` |
| 5299 | `test/wave-submit.test.ts` |
| 5399 | `test/replays.test.ts` |
| 5499 | `test/adversarial.test.ts` |
| 5599 | `test/loop.test.ts` |

They are distinct so the files can run under vitest's default file parallelism.
If one is occupied it is usually an orphaned host from an interrupted run —
`test/child-reaper.ts` kills these on every worker-exit path except SIGKILL, and
`lsof -nP -i :<port> -sTCP:LISTEN` finds whatever is left.
