# The local stack

Four processes and a schema, in order. This is what Task 17 Step 6 means by
"a local `api`", and what `services/api/src/dev.ts` expects to already be
running when it starts.

`pnpm dev` is NOT this. It runs `src/index.ts`, the deployed entrypoint,
which wires `GcsBundleStore`, `GcsReplayStore` and `googleIdTokenAuth()` —
correct for Cloud Run and unrunnable on a laptop. `dev:local` runs
`src/dev.ts`, which wires the local halves. See that file's header for why
the two are separate entrypoints rather than one with a flag.

## Node

The `pnpm` that resolves first on a default PATH here is 3.7.5 under an nvm
node 10; it prints usage and exits 0 rather than failing, which is the trap
`implementation/results/phase7-test-baseline.txt` documents for the api
suite. Every command below assumes:

```bash
export PATH="$HOME/.nvm/versions/node/v22.22.2/bin:$PATH"
```

## 1. Postgres 16

```bash
docker run -d --name broodline-local-db \
  -e POSTGRES_DB=broodline -e POSTGRES_USER=broodline -e POSTGRES_PASSWORD=broodline \
  -p 55432:5432 postgres:16-alpine
```

Port 55432, not 5432, so it cannot collide with a system Postgres or with the
Testcontainers instances the api suite spins up.

## 2. Schema, and the one row no migration writes

```bash
export DATABASE_URL="postgres://broodline:broodline@127.0.0.1:55432/broodline"
pnpm --filter @broodline/api migrate
```

Then seed `servers`. **No migration or script creates this row** — the Phase 7
plan lists its absence under "what this plan found the design missed", and
Task 19 seeds it for the deployed stack only. Without it `POST /v1/account`
refuses every request.

```bash
docker exec broodline-local-db psql -U broodline -d broodline -c \
  "INSERT INTO servers (server_id, region, state, tick_day_of_week, tick_minute_of_day)
   VALUES (1,'us-central1','open',0,1200) ON CONFLICT (server_id) DO NOTHING;"
```

`us-central1` is not cosmetic. `assignServer` maps storefront region to server
through a one-entry table, and `Session.cs` sends exactly `us-central1`; any
other region 400s with "No server serves storefront region ...".

## 3. The sim service

```bash
dotnet build services/sim/Broodline.Sim.Service.csproj -c Debug -o /tmp/broodline-sim --nologo
dotnet /tmp/broodline-sim/Broodline.Sim.Service.dll --urls http://127.0.0.1:5999
```

Port 5999 is deliberate: `services/api/test/preflight.ts` reserves 5199–5899
one per test file and REFUSES to start the suite while any is held. A dev sim
left running on 5699 blocks `pnpm test` with a message about
`founder.test.ts` and nothing about the dev stack.

## 4. The api

```bash
export DATABASE_URL="postgres://broodline:broodline@127.0.0.1:55432/broodline"
export JWT_SECRET="local-dev-only-secret-not-for-any-deployed-environment-0123456789"
pnpm --filter @broodline/api dev:local
```

`dev.ts` publishes `config/bundles/0.1.3` into `.local-stack/` and sets the
pointer on every start. 0.1.3 because it is the bundle that authors both
`starter.json`'s cold-open pair and waves 1–2; the FTUE cannot be walked on
0.1.1 or 0.1.2, which author neither.

## 5. Check it before pressing Play

```bash
curl -s http://127.0.0.1:8080/healthz && curl -s http://127.0.0.1:5999/healthz
```

A full beat-1 check:

```bash
curl -s -X POST http://127.0.0.1:8080/v1/account \
  -H 'content-type: application/json' -H "idempotency-key: $(uuidgen)" \
  -d '{"birthdateBand":"adult","storefrontRegion":"us-central1"}'
```

Expect `accountId`, `playerId`, `serverId: 1`, `accessToken`, `refreshToken`.
With that token, `/v1/sync` should report `bundleVersion 0.1.3`, four waves,
four traits and `ftue: {founderNamed: false, tutorialStockGranted: false,
splices: 0}`, and `/v1/roster` should hold exactly two gen-1 creatures — Ember
(Splash/Carapace) and Vetch (Taunt/Carapace).

## The client

`client/Assets/Resources/BroodlineConfig.json` already reads
`http://127.0.0.1:8080`, which is `dev.ts`'s default port. Open
`client/Assets/Scenes/Boot.unity` and press Play.

## Teardown

```bash
docker rm -f broodline-local-db
# stop the sim and api processes; rm -rf .local-stack to discard the bundle tree
```
