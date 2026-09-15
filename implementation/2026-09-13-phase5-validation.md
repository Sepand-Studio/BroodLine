# Phase 5 — Validation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A tampered submission earns nothing; an honest one pays exactly once under retry — where "tampered" means what `specs/plans/broodline_phase5_validation.md` §4.4 says it means, and no more.

**Architecture:** The phase opens with the proof it depends on. Phase 3's device round-trip is dark — `SimVersion 0.2.0` superseded the tracked captures, so both round-trip tests assert a thrown exception instead of comparing hashes — and this phase's entire claim is that the server's re-simulation reproduces the client's run. Task 1 re-captures on hardware and restores hash comparison. Everything after it builds the second deployable: `services/sim`, a C# ASP.NET service holding the engine and no database, behind `POST /internal/simulate`; the contract crossing the language boundary in the opposite direction from Phase 4's; a two-call wave protocol whose issuance is single-use; and an adversarial suite in which every test is proven to fail when its guard is weakened.

**Tech Stack:** .NET 10 / ASP.NET minimal API for `sim`, referencing the existing `netstandard2.1` engine by `ProjectReference`. On the TypeScript side the Phase 4 stack unchanged — Node 22 LTS, pnpm workspaces, Hono, Drizzle + drizzle-kit, `pg`, Zod + `@asteasolutions/zod-to-openapi`, Vitest, Testcontainers, Postgres 16. Terraform, Cloud Run, Cloud SQL, GCS. `openapi-typescript` for the new generation direction.

## Global Constraints

**The engine constraints from Phases 1–3 still bind `engine/`** — no floating point, no `System.Math`, no `System.Linq`, no `Dictionary`, no `HashSet`, no `System.IO`, zero project references, the Cecil float scan, `BannedSymbols.txt`. **No task in this phase modifies `engine/`.** Task 1 re-captures an artifact produced by it; Task 2 references it without changing it. If a task appears to need an engine change, stop — the phase is scoped so that it does not, and a change there re-opens the corpus baseline.

`services/sim` is **not** subject to the engine's constraints. It is an ASP.NET service that happens to be C#; it may use `System.Linq`, `System.IO` and the BCL freely. The constraints belong to `engine/Runtime/`, enforced by `Directory.Build.props` and the analyzer, and `sim` is outside that tree.

From `specs/plans/broodline_phase5_validation.md`, and normative here:

- **`api` never parses a replay.** It forwards bytes to `sim` and reads back a verdict. No trait, no rule, no wave definition is interpreted in TypeScript (design §3.2, `solo_execution` §6.1).
- **The reward is a function of the issuance's wave id, never of the submission** (design §2.2). The submission decides *whether*, never *what*.
- **One live issuance per player**, enforced by a partial unique index rather than by a handler remembering to check (design §2.1, §4.3).
- **The issuance is consumed inside the same transaction as the credit.** This is the ledger's guard; the idempotency key protects the response only (design §4.2).
- **A submission whose engine version is not this engine's is rejected**, never rendered. The show-with-a-notice rule is the viewer's (design §2.3).
- **A rejection is a `200` verdict from `sim`, not a `5xx`.** `api` must be able to tell "this submission is bad" from "`sim` is broken", because the two have opposite effects on the issuance (design §3.2).
- **`sim` holds no database handle, no GCS client and no JWT secret.** Internal ingress only (design §3.1).
- **Every currency mutation still writes a ledger row in the same transaction as the balance update.** Phase 4's rule, unchanged.
- **`server_id` still leads every primary key and index on every server-scoped table.** RLS on, `FORCE ROW LEVEL SECURITY`, transaction-scoped `set_config`.
- **Generated clients are committed and never hand-edited**, in **both** directions, and one script regenerates both (design §2.4).
- **A schema migration and the code requiring it never deploy together.** Expand, deploy, migrate, contract.

### Values, copied verbatim

| | |
|---|---|
| Engine version today | `SimVersion.Value` = `"0.2.0"` |
| Region | `us-central1` — one server, one region |
| Issuance expiry | **Two hours** — design §4.1. A starting value, on the playtest list |
| Replay cap | **Three replays per wave per day**, then nothing until tomorrow — `broodline_campaign_structure.md` |
| Unconsumed issuance retention | One hour past `expires_at` — three hours after issuance |
| **Consumed** issuance retention | **48 hours after `issued_at`** — it *is* the replay counter, and the count is against a day boundary (design §4.3) |
| Idempotency key retention | 24 hours, unchanged from Phase 4 |
| Player replay retention | **30 days rolling**, GCS lifecycle rule. Pinning deferred |
| Replay object key | `replays/{server_id}/{player_id}/{issuance_id}.bin` |
| SLO, wave submission | p99 under 500 ms end to end, **including re-simulation** — `solo_execution` §3.1 |
| One wave of CPU | ~20 ms — `solo_execution` §6.1 |
| `sim` ingress | Cloud Run `internal`, invoked by `api`'s service account |
| Authored wave in the bundle | Wave 6 only — `config/bundles/0.1.1/waves.json` from Task 5 |
| Engine roster | One raider (`Courser`), one trait (`Chill`). **Unchanged by this phase** |
| Device capture procedure | `Assets/Scenes/Wave.unity`, a tap between ticks **184 and 240**, four files into `implementation/results/` |
| Apple Developer Team | `R4Z6W7AW86`, automatic signing |
| Unity | 6000.6.0f1, IL2CPP |

**This plan writes no creature ownership check, no raids or auto-resolve, no replay viewer, no replay pinning, no nodes, regions, harvest or splice, no optimistic grant and no clawback, no batching, no Redis, no PgBouncer, no HA, no refresh-token rotation, no `Broodline.UI`, and no engine content beyond what exists.** Those are named at design §8.

---

## The one thing this plan cannot do for you

**GitHub Actions is disabled at the `Sepand-Studio` organisation level.** No workflow has ever run on this repository. The session token carries `read:org`, not `admin:org`.

```
PUT /repos/Sepand-Studio/BroodLine/actions/permissions
→ 409 "GitHub Actions is disabled on this repository by the organization"
```

Enable it at `https://github.com/organizations/Sepand-Studio/settings/actions`, or run `gh auth refresh -h github.com -s admin:org` and retry. **Every gate in this plan verifies locally**, so no task is blocked — but the contract-diff gate at Task 3 and the determinism gate are designed to run in CI and will not until a human does this.

---

## File structure

### New — the second deployable

| File | Responsibility |
|---|---|
| `services/sim/Broodline.Sim.Service.csproj` | ASP.NET minimal API, `net10.0`, `ProjectReference` to `engine/Broodline.Sim.csproj` |
| `services/sim/Program.cs` | Host, routes, the OpenAPI document emitter |
| `services/sim/SimulateEndpoint.cs` | `POST /internal/simulate` — deserialize, re-simulate, map `Outcome` to the response |
| `services/sim/Contracts.cs` | Request and response DTOs. **The C# side is the source of truth for this direction** |
| `services/sim/Dockerfile` | Multi-stage; the engine source tree must be in the build context |
| `tests/sim/Broodline.Sim.Service.Tests.csproj` | xUnit over the service, `WebApplicationFactory` |
| `tests/sim/SimulateTests.cs` | The wire round-trip and every rejection path |

### New — the `api` half

| File | Responsibility |
|---|---|
| `services/api/src/sim/client.ts` | The only caller of `sim`. Distinguishes a verdict from an outage |
| `services/api/src/generated/sim.ts` | **Generated from `sim`'s OpenAPI document. Committed, never hand-edited** |
| `services/api/src/routes/wave.ts` | `POST /v1/wave/start` and `POST /v1/wave/submit` |
| `services/api/src/routes/session.ts` | `POST /v1/session/refresh` |
| `services/api/src/wave/issuance.ts` | Issue, load, consume. The five checks at design §4.1 |
| `services/api/src/wave/rewards.ts` | Reward lookup by wave id, off the config bundle |
| `services/api/src/replays/store.ts` | GCS write on the verified path only |
| `services/api/drizzle/0003_wave_issuances.sql` | The table, its RLS, its partial unique index, its write-once trigger |
| `services/api/test/wave.test.ts` | The protocol, each failure in isolation |
| `services/api/test/adversarial.test.ts` | **Gate.** One test per design §4.4 row |
| `services/api/test/session.test.ts` | Refresh, and the three defects it closes |

### Modified

| File | Change |
|---|---|
| `tests/engine/Combat/DeviceReplayTests.cs` | Round-trip tests back to **comparing `Outcome.Hash`**, not asserting a throw |
| `implementation/results/device-replay.bin` / `-outcome.txt` | Re-captured under `0.2.0` |
| `implementation/results/editor-replay.bin` / `-outcome.txt` | Re-captured under `0.2.0` |
| `implementation/scripts/generate-contract.sh` | Emits **both** directions; one diff gate over all four paths |
| `services/api/src/db/schema.ts` | Adds `waveIssuances` |
| `services/api/src/app.ts` | Registers the wave and session routes; `Deps` gains `simClient` and `replayStore` |
| `services/api/src/identity/jwt.ts` | `kid`-aware verify, the `iss` claim, `deleted_at` on redemption |
| `services/api/src/config/validate.ts` | A rule requiring a reward for every authored wave |
| `config/bundles/0.1.1/` | **New**, copied from `0.1.0`. Wave 6 gains a reward. `0.1.0` is not touched |
| `services/api/tsconfig.json` | **Remove `"exclude": ["src/index.ts"]`** — Phase 4's followups §2 |
| `infra/terraform/main.tf` | `sim` service, its service account and IAM, the replay bucket and its lifecycle rule |
| `Broodline.sln` | Adds `Broodline.Sim.Service` and its test project |
| `.github/workflows/tests.yml` | Adds the `sim` job and widens the contract-diff gate |

---

## Task 0: Prerequisites, and confirming Phase 4's gates still hold

Nothing here is new work. It establishes that the branch this phase builds on is the one Phase 4 finished, because every task below assumes a green baseline and a silent regression discovered at Task 9 costs the whole phase.

**Files:**
- Modify: none. This task commits nothing.

**Interfaces:**
- Consumes: the Phase 4 deliverables in their entirety.
- Produces: a recorded baseline the later tasks are diffed against.

- [ ] **Step 1: Confirm the toolchain**

```bash
./implementation/scripts/verify-prereqs.sh
```

Expected: every line green. If `dotnet --list-runtimes` reports no 10.x, stop — Task 2 targets `net10.0` and Task 3's NSwag pin is already known-fragile against anything else (Phase 4 followups §2).

- [ ] **Step 2: Run every Phase 4 gate and record the numbers**

```bash
dotnet test Broodline.sln --nologo
./implementation/scripts/cross-runtime-diff.sh
pnpm --filter @broodline/api test
pnpm --filter @broodline/api typecheck
./implementation/scripts/generate-contract.sh && git diff --quiet -- openapi/ client/Assets/Generated/
./implementation/scripts/run-unity-tests.sh EditMode
```

Expected: **74 api tests, 174 .NET tests with 0 skipped, 35 Unity EditMode tests**, 500/500 cross-runtime agreement, and a clean contract diff. (The Phase 4 followups say 71 api tests; that figure is stale — three have been added since. Measured on this branch at 74.) Write the actual numbers down — Task 12 compares against them, and a count that *fell* between here and there is a deletion nobody noticed.

- [ ] **Step 3: Confirm the device proof is dark, rather than assuming it**

The premise of Task 1. Prove it rather than trusting this document:

```bash
dotnet test Broodline.sln --nologo --filter "FullyQualifiedName~DeviceReplay" \
  --logger "console;verbosity=detailed" 2>&1 | grep -i "recorded under engine"
```

Expected: the `ReCaptureOwed` message, naming `0.1.0` as the captured version and `0.2.0` as the engine.

> **Do not use `-v n` here.** xUnit does not surface `ITestOutputHelper` output at
> normal verbosity for *passing* tests, and these tests pass either way — so the
> grep prints nothing whether the proof is dark or current, which reads as
> "artifacts are already current" and skips a device capture that is actually
> owed. Found by the Task 0 run that this step was written for.

The independent cross-check, which does not depend on console output at all:

```bash
dotnet test Broodline.sln --nologo \
  --filter "FullyQualifiedName~TheDeviceRunIsSupersededAndIsNotReSimulated"
```

A **pass** means the artifacts are superseded and Task 1's capture is owed. That
test asserts `CapturedUnder != SimVersion.Value` directly. **If the artifacts are
already current it fails**, and Task 1 collapses to its Step 5–6 (restoring hash
comparison) with no hardware needed.

- [ ] **Step 4: Confirm the deployed Phase 4 revision is healthy**

```bash
terraform -chdir=infra/terraform output -raw api_url | xargs -I{} curl -fsS {}/healthz
```

Expected: `{"ok":true}`. Task 11 adds a second service beside this one; a broken first service makes that diagnosis ambiguous.

- [ ] **Step 5: Branch**

```bash
git checkout -b phase_5
```

This plan assumes `phase_5` throughout. Phase 4 landed on `develop`.

---

## Task 1: Re-capture the device proof, and restore hash comparison

> ### ✅ DONE 2026-09-14
>
> Re-captured on an **iPhone 15 Pro** (`iPhone16,1`) and in the Unity 6 Editor,
> both under engine `0.2.0`. Device rally tick **205**, Editor **200** — both
> inside creature 0's engagement, the device one inside the narrower 184..207
> the rally-effectiveness test needs.
>
> `TheDeviceRunReSimulatesToTheSameHash` and
> `TheDeviceRunsRallyActuallyChangedTheSimulation` now **run their assertions
> instead of bailing**, and pass. `TheDeviceRunIsSupersededAndIsNotReSimulated`
> is replaced by `TheTrackedCapturesAreCurrent`, whose green means the
> round-trip is proven rather than dark.
>
> **The new guard was proven by weakening**, not assumed: `SimVersion` set to
> `0.3.0` gave 1 failed / 31 passed, the single red being
> `TheTrackedCapturesAreCurrent` while both round-trip tests reported *passed*
> from their bail-out. Reverted; `engine/` untouched. **.NET 186 / 0 failed /
> 0 skipped.**
>
> Two traps worth carrying forward, neither in the step text below. The
> **effective device window is 184..207**, the overlap of the 184..240 in
> `ReCaptureOwed` and the 150..207 in the rally-effectiveness failure message —
> the wider number alone will produce an inert capture. And opening Unity
> rewrote `ProjectSettings.asset` with `SENTIS_ANALYTICS_ENABLED`, exactly the
> hazard followups §6 names; it was reverted rather than committed.

**This is the phase gate and it is first because it needs physical hardware**, which is the one input no later task can schedule around. Design §2.5: the round-trip tests currently assert a thrown `ReplayFormatException`, which is strictly weaker than comparing hashes, and weaker in exactly the direction this phase depends on.

**Files:**
- Modify: `tests/engine/Combat/DeviceReplayTests.cs`
- Replace: `implementation/results/device-replay.bin`, `device-replay-outcome.txt`, `editor-replay.bin`, `editor-replay-outcome.txt`

**Interfaces:**
- Consumes: `ReplayArtifact.Read`, `ReplayArtifact.AreCurrent`, `Sim.Replay`, `Outcome.Hash`.
- Produces: a `dotnet test` run in which the round-trip tests execute their assertions rather than short-circuiting. Nothing downstream imports from this task; what it produces is confidence, and the phase's done-when cites it.

- [ ] **Step 1: Read what the capture procedure actually is**

Do not invent it. It is recorded in the test that is failing:

```bash
sed -n '/ReCaptureOwed/,/^$/p' tests/engine/Combat/DeviceReplayTests.cs
```

Expected: play `Assets/Scenes/Wave.unity` in the Editor and on device, **with a tap between ticks 184 and 240**, then commit the four files in `implementation/results/`.

The tap window is not decoration. `WaveClockTests` and the Rally semantics at Phase 3 §5.1 depend on the Rally landing inside it; a capture with no tap proves the round-trip for a run with no input, which is the easy half.

- [ ] **Step 2: Capture in the Editor**

Open `client/` in Unity 6000.6.0f1, play `Assets/Scenes/Wave.unity`, tap once inside the window, let the wave finish. Confirm the two files were written and carry the current version:

```bash
ls -la implementation/results/editor-replay.bin implementation/results/editor-replay-outcome.txt
strings implementation/results/editor-replay.bin | head -1
```

Expected: `0.2.0` in the second command's output. The version is the third field of the record and the first string in it — `Replay.Serialize` writes magic, format version, then a length-prefixed UTF-8 version.

- [ ] **Step 3: Capture on device**

Build to the physical iPhone (`R4Z6W7AW86`, automatic signing), play the same scene, tap inside the same window. Pull the artifact off the device the way Phase 0's `CorpusPlayerHarness` does — that mechanism exists and this task does not invent a second one.

```bash
strings implementation/results/device-replay.bin | head -1
```

Expected: `0.2.0`.

- [ ] **Step 4: Confirm the tests now run rather than bail**

```bash
dotnet test Broodline.sln --nologo --filter "FullyQualifiedName~DeviceReplay" -v n
```

Expected: **PASS**, and the `ReCaptureOwed` message absent from the output. `ReplayArtifact.AreCurrent` derives from the bytes, so no constant needs updating — that was deliberately removed.

- [ ] **Step 5: Write the failing test for the restored proof**

The tests pass at Step 4 while still containing the weak assertion, because `Superseded` now returns `false` and the body runs. The body is what has to be strengthened. Confirm what it currently asserts:

```bash
grep -n "Superseded\|Assert.Equal\|Hash" tests/engine/Combat/DeviceReplayTests.cs
```

The round-trip tests must compare `Outcome.Hash` from a re-simulation against the hash recorded in `*-outcome.txt`. Where a test calls `ReplayArtifact.Superseded(record, output)` and returns early, that guard stays — it is correct for a *future* supersession — but the code after it must end in a hash equality, not in a shape check.

```csharp
[Fact]
public void TheDeviceRunReSimulatesToTheSameHash()
{
    var record = ReplayArtifact.Read(ReplayArtifact.DeviceBin);
    if (ReplayArtifact.Superseded(record, _output)) return;

    var recorded = RecordedOutcome(ReplayArtifact.DeviceOutcome);
    var reSimulated = Broodline.Sim.Combat.Sim.Replay(record);

    // THE done-when. Phase 3 section 7 layer 3: a wave run through a
    // renderer, at a variable frame rate, with a human tap in it, consumed
    // the inputs its replay claims. Anything weaker than a hash comparison
    // does not prove that - and asserting a thrown exception, which is what
    // stood here while the captures were superseded, proves the opposite
    // thing entirely.
    Assert.Equal(recorded.Hash, reSimulated.Hash);
    Assert.Equal(recorded.Result, reSimulated.Result);
    Assert.Equal(recorded.IntegrityRemaining, reSimulated.IntegrityRemaining);
}
```

- [ ] **Step 6: Run it to verify it fails, by perturbing the artifact**

A test asserting equality between two things that are equal passes whether or not it is looking. Prove it is looking:

```bash
cp implementation/results/device-replay-outcome.txt /tmp/outcome.bak
sed -i '' 's/[0-9]\{1,\}$/999/' implementation/results/device-replay-outcome.txt
dotnet test Broodline.sln --nologo --filter "FullyQualifiedName~DeviceReplay"
```

Expected: **FAIL** on the hash comparison. Then restore:

```bash
cp /tmp/outcome.bak implementation/results/device-replay-outcome.txt
dotnet test Broodline.sln --nologo --filter "FullyQualifiedName~DeviceReplay"
```

Expected: **PASS**.

- [ ] **Step 7: Run the full suite**

```bash
dotnet test Broodline.sln --nologo
```

Expected: the Task 0 Step 2 count, **0 skipped**, and the device tests among the passing ones on their assertions rather than on their bail-out.

- [ ] **Step 8: Commit**

```bash
git add tests/engine/Combat/DeviceReplayTests.cs implementation/results/
git commit -m "test: re-capture the device round-trip under 0.2.0, and compare hashes again

Phase 3's done-when has not been running since SimVersion moved to 0.2.0:
ReplayArtifact.Superseded short-circuited both round-trip tests before
their assertions, and what remained asserted a thrown exception rather
than an equal hash. Phase 5 re-simulates every submission on the server,
which is the same proposition, so the phase opens by proving it.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 2: `services/sim` — the second deployable

The last directory in `solo_execution` §7's layout that does not exist. A C# ASP.NET service holding the engine, no database, no secrets, internal ingress only.

**Files:**
- Create: `services/sim/Broodline.Sim.Service.csproj`, `services/sim/Program.cs`, `services/sim/Contracts.cs`, `services/sim/SimulateEndpoint.cs`, `services/sim/Dockerfile`
- Create: `tests/sim/Broodline.Sim.Service.Tests.csproj`, `tests/sim/SimulateTests.cs`
- Modify: `Broodline.sln`

**Interfaces:**
- Consumes: `Replay.Deserialize(ReadOnlySpan<byte>)`, `Replay.IsFromThisEngine`, `Sim.Replay(Replay)`, `Outcome`, `Breach`, `ReplayFormatException`, `WaveCompositionException`, `SimVersion.Value`.
- Produces:
  - `POST /internal/simulate` taking `{ replay: string }` (base64) and returning `SimulateResponse`.
  - `SimulateResponse(string Verdict, string? Reason, string EngineVersion, SimulateEcho? Echo, SimulateOutcome? Outcome)` — Task 3 generates the TypeScript from this, and Task 6 consumes it.
  - Verdicts: `"verified"` and `"rejected"`. Reasons: `"replay_malformed"`, `"engine_too_old"`, `"rules_violated"`.

- [ ] **Step 1: Write the failing test**

`tests/sim/SimulateTests.cs`:

```csharp
using System;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using Broodline.Sim.Combat;

namespace Broodline.Sim.Service.Tests
{
    public class SimulateTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly HttpClient _http;
        public SimulateTests(WebApplicationFactory<Program> f) => _http = f.CreateClient();

        /// Wave 6, one Courser, the authored wave the bundle carries. Built
        /// from the engine rather than from a fixture file, so a rules change
        /// breaks this loudly instead of drifting.
        private static Replay ValidRecord()
        {
            var record = new Replay
            {
                WaveId = 6,
                Seed = 0x5EEDu,
                Terrain = Terrain.Defile,
                LaneCount = 1,
                Deployment = new[]
                {
                    new CreatureSpec { Species = Species.Vetch, Trait1 = Trait.Chill, Tier1 = 1,
                                       Trait2 = Trait.None, Tier2 = 0,
                                       Instinct = Instinct.Vanguard, Pocket = 0 },
                },
            };
            var lane = record.BuildLane();
            record.PocketCount = lane.PocketCount;
            record.LaneTiles = lane.Tiles;
            record.DeploymentHp = new[] { Stats.CreatureHp(Species.Vetch) };
            return record;
        }

        private Task<HttpResponseMessage> Post(byte[] bytes) =>
            _http.PostAsJsonAsync("/internal/simulate",
                new { replay = Convert.ToBase64String(bytes) });

        [Fact]
        public async Task AnHonestRecordVerifiesToTheSameHashAsAnInProcessRun()
        {
            var record = ValidRecord();
            var expected = Broodline.Sim.Combat.Sim.Replay(record.Copy());

            var res = await Post(record.Serialize());
            res.EnsureSuccessStatusCode();
            var body = await res.Content.ReadFromJsonAsync<SimulateResponse>();

            Assert.Equal("verified", body!.Verdict);
            // The hash crosses the wire as a DECIMAL STRING. Outcome.Hash is a
            // ulong and JSON numbers are IEEE 754 doubles - values above 2^53
            // arrive silently wrong, and a hash is uniformly distributed over
            // the full 64 bits, so "silently wrong" is the common case rather
            // than the edge one.
            Assert.Equal(expected.Hash.ToString(), body.Outcome!.Hash);
            Assert.Equal(expected.Result.ToString(), body.Outcome.Result);
            Assert.Equal(expected.IntegrityRemaining, body.Outcome.IntegrityRemaining);
            Assert.Equal(6, body.Echo!.WaveId);
        }

        [Fact]
        public async Task ForgedBytesAreRejectedWithTwoHundred()
        {
            // A rejection is a FACT ABOUT THE SUBMISSION, not a fault in the
            // service. api decides whether the issuance survives by reading
            // the status code, so these must not share one - design 3.2.
            var res = await Post(new byte[] { 1, 2, 3, 4, 5 });
            Assert.Equal(System.Net.HttpStatusCode.OK, res.StatusCode);

            var body = await res.Content.ReadFromJsonAsync<SimulateResponse>();
            Assert.Equal("rejected", body!.Verdict);
            Assert.Equal("replay_malformed", body.Reason);
            Assert.Null(body.Outcome);
        }

        [Fact]
        public async Task ARecordFromAnotherEngineIsRejectedRatherThanReSimulated()
        {
            // solo_execution 9.4's show-the-stored-outcome rule is the
            // VIEWER's. On the payout path an unverifiable submission is a
            // rejection, or the server pays out a number it never checked -
            // design 2.3.
            var record = ValidRecord();
            record.EngineVersion = "0.1.0";

            var body = await (await Post(record.Serialize()))
                .Content.ReadFromJsonAsync<SimulateResponse>();

            Assert.Equal("rejected", body!.Verdict);
            Assert.Equal("engine_too_old", body.Reason);
            Assert.Equal(SimVersion.Value, body.EngineVersion);
        }

        [Fact]
        public async Task ARecordViolatingTheRulesIsRejectedAsRulesViolated()
        {
            // Distinct from malformed: these bytes decode perfectly. The
            // record simply describes a run this engine's rules forbid.
            var record = ValidRecord();
            record.DeploymentHp = new[] { 9999 };

            var body = await (await Post(record.Serialize()))
                .Content.ReadFromJsonAsync<SimulateResponse>();

            Assert.Equal("rejected", body!.Verdict);
            Assert.Equal("rules_violated", body.Reason);
        }

        [Fact]
        public async Task TheServiceHoldsNoDatabaseHandle()
        {
            // design 3.1, asserted rather than promised. sim is a pure
            // function behind HTTP; the moment it grows a connection string it
            // grows a pool against the Cloud SQL cap that solo_execution 5.7
            // names as the thing that runs out before CPU does.
            var assembly = typeof(Program).Assembly;
            foreach (var referenced in assembly.GetReferencedAssemblies())
            {
                Assert.DoesNotContain("Npgsql", referenced.Name ?? "");
                Assert.DoesNotContain("Google.Cloud.Storage", referenced.Name ?? "");
            }
        }
    }
}
```

- [ ] **Step 2: Run it to verify it fails**

```bash
dotnet test tests/sim/Broodline.Sim.Service.Tests.csproj --nologo
```

Expected: **FAIL** — the project does not exist.

- [ ] **Step 3: Create the project and wire it to the engine**

`services/sim/Broodline.Sim.Service.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <!-- The engine's Directory.Build.props does NOT reach here. services/ is
         outside engine/, and the analyzer, the banned-symbol list and the
         float scan are engine rules. This is an ASP.NET service that happens
         to be C#. -->
    <InvariantGlobalization>true</InvariantGlobalization>
  </PropertyGroup>
  <ItemGroup>
    <!-- Shared SOURCE, not a package - solo_execution 9.2. A ProjectReference
         cannot be stale the way a rebuilt-and-copied DLL can. -->
    <ProjectReference Include="../../engine/Broodline.Sim.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 4: Write the contracts**

`services/sim/Contracts.cs`:

```csharp
namespace Broodline.Sim.Service
{
    /// The body is the SERIALIZED REPLAY, not its six fields spread out.
    ///
    /// Replay.Serialize is fixed-layout little-endian and Deserialize
    /// validates inside itself, so the format has exactly one parser.
    /// Re-describing the fields in JSON at this boundary builds a second one,
    /// and two parsers for one format can disagree - which is the whole
    /// failure mode the binary format was chosen to avoid.
    public sealed record SimulateRequest(string Replay);

    /// What sim echoes back so api can key a ledger row WITHOUT PARSING
    /// ANYTHING. solo_execution 6.1: api never learns what a trait does.
    public sealed record SimulateEcho(
        int WaveId,
        string Seed,          // ulong as decimal string - see Hash
        string Terrain,
        int LaneCount,
        int RallyTick,
        int RallyCreature);

    public sealed record BreachDto(
        int Tick, int Raider, string Type, int Lane,
        bool Access, bool Coverage, bool Placement);

    public sealed record SimulateOutcome(
        string Result,
        int Ticks,
        int IntegrityRemaining,
        /// DECIMAL STRING, not a number. Outcome.Hash is a ulong; JSON numbers
        /// are doubles, and a uniformly distributed 64-bit value exceeds 2^53
        /// almost always. A number here is silently wrong most of the time.
        string Hash,
        BreachDto[] Breaches);

    public sealed record SimulateResponse(
        string Verdict,             // "verified" | "rejected"
        string? Reason,             // null when verified
        string EngineVersion,
        SimulateEcho? Echo,
        SimulateOutcome? Outcome)
    {
        public static SimulateResponse Rejected(string reason) =>
            new("rejected", reason, SimVersion.Value, null, null);
    }
}
```

- [ ] **Step 5: Write the endpoint**

`services/sim/SimulateEndpoint.cs`:

```csharp
using Broodline.Sim.Combat;

namespace Broodline.Sim.Service
{
    public static class SimulateEndpoint
    {
        /// Three rejection reasons, and the ORDER they are checked in is the
        /// design.
        ///
        /// Deserialize throws on bytes that are not a record at all.
        /// IsFromThisEngine is checked NEXT and explicitly, before Sim.Replay -
        /// because Validate also throws ReplayFormatException on a version
        /// mismatch, and collapsing the two would report a legitimate record
        /// from a later engine as "malformed", sending the reader after a
        /// forgery that is not there. Replay.cs makes the same argument for
        /// the same reason, one layer down.
        public static SimulateResponse Handle(SimulateRequest request)
        {
            byte[] bytes;
            try { bytes = Convert.FromBase64String(request.Replay ?? ""); }
            catch (FormatException) { return SimulateResponse.Rejected("replay_malformed"); }

            Replay record;
            try { record = Replay.Deserialize(bytes); }
            catch (ReplayFormatException) { return SimulateResponse.Rejected("replay_malformed"); }

            if (!record.IsFromThisEngine)
                return SimulateResponse.Rejected("engine_too_old");

            Outcome outcome;
            try { outcome = Combat.Sim.Replay(record); }
            catch (ReplayFormatException) { return SimulateResponse.Rejected("rules_violated"); }
            catch (WaveCompositionException) { return SimulateResponse.Rejected("rules_violated"); }

            var breaches = new BreachDto[outcome.BreachCount];
            for (int i = 0; i < breaches.Length; i++)
            {
                var b = outcome.Breaches[i];
                breaches[i] = new BreachDto(b.Tick, b.Raider, b.Type.ToString(), b.Lane,
                                            b.Access, b.Coverage, b.Placement);
            }

            return new SimulateResponse(
                "verified", null, SimVersion.Value,
                new SimulateEcho(record.WaveId, record.Seed.ToString(),
                                 record.Terrain.ToString(), record.LaneCount,
                                 record.RallyTick, record.RallyCreature),
                new SimulateOutcome(outcome.Result.ToString(), outcome.Ticks,
                                    outcome.IntegrityRemaining,
                                    outcome.Hash.ToString(), breaches));
        }
    }
}
```

- [ ] **Step 6: Write the host**

`services/sim/Program.cs`:

```csharp
using Broodline.Sim.Service;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOpenApi();          // the api -> sim contract, Task 3

var app = builder.Build();
app.MapOpenApi();                       // /openapi/v1.json

app.MapGet("/healthz", () => Results.Ok(new { ok = true }));

// INTERNAL ingress only - there is no authentication here and there must be
// no public route. Cloud Run's ingress setting is the control (Task 11); the
// path prefix is a reminder, not a guard.
app.MapPost("/internal/simulate",
    (SimulateRequest request) => Results.Ok(SimulateEndpoint.Handle(request)))
   .WithName("Simulate");

app.Run();

// WebApplicationFactory<Program> needs the implicit entry point to be
// reachable from the test assembly.
public partial class Program { }
```

- [ ] **Step 7: Add both projects to the solution**

```bash
dotnet sln Broodline.sln add services/sim/Broodline.Sim.Service.csproj
dotnet sln Broodline.sln add tests/sim/Broodline.Sim.Service.Tests.csproj
```

- [ ] **Step 8: Run the tests to verify they pass**

```bash
dotnet test tests/sim/Broodline.Sim.Service.Tests.csproj --nologo
```

Expected: **PASS**, five tests.

- [ ] **Step 9: Prove the hash test is looking**

The equality at Step 1 compares two runs of the same engine, which is exactly the shape of an assertion that passes without checking anything. Break it deliberately:

```bash
# In SimulateEndpoint.Handle, temporarily return outcome.Ticks.ToString()
# in place of outcome.Hash.ToString(), then:
dotnet test tests/sim/Broodline.Sim.Service.Tests.csproj --nologo --filter AnHonestRecord
```

Expected: **FAIL**. Revert, re-run, expect **PASS**.

- [ ] **Step 10: Write the Dockerfile**

`services/sim/Dockerfile`. The build context is the **repository root**, because the `ProjectReference` reaches outside `services/sim/`:

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
# Both trees. A context scoped to services/sim/ cannot see engine/ and the
# restore fails with a path error that reads like a typo.
COPY engine/ engine/
COPY services/sim/ services/sim/
RUN dotnet publish services/sim/Broodline.Sim.Service.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Broodline.Sim.Service.dll"]
```

- [ ] **Step 11: Verify the image builds from the root context**

```bash
docker build -f services/sim/Dockerfile -t broodline-sim:dev .
docker run --rm -p 8080:8080 -d --name sim-smoke broodline-sim:dev
sleep 3 && curl -fsS localhost:8080/healthz; docker rm -f sim-smoke
```

Expected: `{"ok":true}`. Phase 4's followups record that the first container "could not have booted" — a smoke run here costs three seconds and catches it before Terraform does.

- [ ] **Step 12: Commit**

```bash
git add services/sim tests/sim Broodline.sln
git commit -m "feat: services/sim, the second deployable

An ASP.NET service holding the engine by ProjectReference and nothing
else - no database handle, no GCS client, no JWT secret, asserted rather
than promised. POST /internal/simulate takes the serialized replay
rather than its fields, because the format already has exactly one
parser and a second one can disagree with it.

A rejection is a 200 verdict, not a 5xx: api decides whether the wave
issuance survives by telling a bad submission from a broken service.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 3: The `api` → `sim` contract, and one diff gate over both directions

`solo_execution` §6: two contracts generated in opposite directions, clients committed, CI failing on a non-empty diff. Phase 4 built Unity → `api`. This builds `api` → `sim`, and — per design §2.4 — folds both into one script with one gate, because two scripts invite the second being forgotten.

**Files:**
- Create: `services/api/src/generated/sim.ts` (generated), `openapi/sim.json` (generated)
- Modify: `implementation/scripts/generate-contract.sh`, `services/api/package.json`, `nswag.json`

**Interfaces:**
- Consumes: `sim`'s OpenAPI document at `/openapi/v1.json`, the `SimulateResponse` shape from Task 2.
- Produces: `paths['/internal/simulate']` types in `services/api/src/generated/sim.ts`, consumed by Task 6's `simClient`.

- [ ] **Step 1: Write the failing test**

The gate is a shell assertion, not a unit test. Add it to `services/api/test/contract.test.ts` so it runs with the suite rather than only in CI:

```ts
import { execFileSync } from 'node:child_process'
import { fileURLToPath } from 'node:url'
import { describe, expect, it } from 'vitest'

const REPO = fileURLToPath(new URL('../../../', import.meta.url))

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
})
```

- [ ] **Step 2: Run it to verify it fails**

```bash
pnpm --filter @broodline/api test contract
```

Expected: **FAIL** — `openapi/sim.json` and `services/api/src/generated/sim.ts` do not exist, so `git status` reports them untracked.

- [ ] **Step 3: Add the generator dependency**

```bash
pnpm --filter @broodline/api add -D openapi-typescript
```

- [ ] **Step 4: Resolve the NSwag runtime pin before adding a second generator to the same script**

Phase 4 followups §2: `nswag.json` pins `runtime: Net90` and the script exports `DOTNET_ROLL_FORWARD=LatestMajor`, both tuned to a machine carrying only .NET 10. On a runner whose shared runtime is 8.x, `dotnet tool restore` resolves the net8.0 NSwag build and its internal check rejects `Net90`. **It fails loudly in every traced path** — never silently different output — but adding a second generator to a script with a known-fragile step doubles the surface.

Make it environment-detected rather than pinned:

```bash
# In generate-contract.sh, before invoking nswag:
DOTNET_MAJOR="$(dotnet --list-runtimes \
  | awk '/Microsoft.NETCore.App/ {print $2}' | cut -d. -f1 | sort -rn | head -1)"
if [ -z "$DOTNET_MAJOR" ]; then
  echo "no Microsoft.NETCore.App runtime found - nswag cannot run" >&2
  exit 1
fi
# nswag spells it Net80 / Net90 / Net100.
NSWAG_RUNTIME="Net${DOTNET_MAJOR}0"
```

and pass `/runtime:$NSWAG_RUNTIME` on the command line rather than leaving it in `nswag.json`.

- [ ] **Step 5: Extend the script to both directions**

`implementation/scripts/generate-contract.sh` gains a second half. The `sim` document is emitted by running the service and fetching it, because ASP.NET's document is produced by the host:

```bash
# ---- Direction 2: api -> sim. C# is the source of truth. ----
#
# The document is FETCHED FROM A RUNNING HOST rather than produced by a
# build-time tool, because MapOpenApi composes it from the endpoints as
# registered - which is the only description that cannot drift from what the
# service actually serves.
dotnet run --project services/sim/Broodline.Sim.Service.csproj \
  --no-launch-profile --urls http://127.0.0.1:5199 &
SIM_PID=$!
# Kill it however this script exits, including on the set -e path. Without
# this a failed curl leaves a dotnet process holding 5199 and the NEXT run
# fails with a misleading bind error.
trap 'kill "$SIM_PID" 2>/dev/null || true' EXIT

for _ in $(seq 1 40); do
  curl -fsS http://127.0.0.1:5199/healthz >/dev/null 2>&1 && break
  sleep 0.25
done

curl -fsS http://127.0.0.1:5199/openapi/v1.json \
  | python3 -m json.tool --sort-keys > openapi/sim.json

pnpm --filter @broodline/api exec openapi-typescript openapi/sim.json \
  -o src/generated/sim.ts
```

**`json.tool --sort-keys` is load-bearing.** ASP.NET does not guarantee key order between runs, and an unsorted document produces a diff on every regeneration — which turns the gate at Step 1 into noise that gets disabled.

- [ ] **Step 6: Run the script and inspect what it generated**

```bash
./implementation/scripts/generate-contract.sh
git status --porcelain -- openapi/ services/api/src/generated/
grep -c "internal/simulate" openapi/sim.json
```

Expected: two new files, and `1` from the grep.

- [ ] **Step 7: Run the gate to verify it passes**

```bash
git add openapi/sim.json services/api/src/generated/sim.ts
pnpm --filter @broodline/api test contract
```

Expected: **PASS**.

- [ ] **Step 8: Prove the gate is looking**

```bash
# Add a field to SimulateEcho in services/sim/Contracts.cs, then:
pnpm --filter @broodline/api test contract
```

Expected: **FAIL**, naming `openapi/sim.json` and `services/api/src/generated/sim.ts` as dirty. Revert and re-run; expect **PASS**. This is the assertion that the generated half cannot drift from the C# half, and it is the entire reason the direction is generated rather than written.

- [ ] **Step 9: Widen the CI job**

`.github/workflows/tests.yml`: the existing contract step runs `generate-contract.sh` and `git diff --quiet -- openapi/ client/Assets/Generated/`. Widen the path list to include `services/api/src/generated/`, and add a `dotnet test tests/sim/` step.

The workflow still cannot run — GitHub Actions is disabled at the organisation. Commit it correct anyway; a workflow written later, under pressure, against a gate nobody has read is worse.

- [ ] **Step 10: Commit**

```bash
git add implementation/scripts/generate-contract.sh nswag.json openapi/ \
        services/api/src/generated/ services/api/package.json \
        services/api/test/contract.test.ts .github/workflows/tests.yml
git commit -m "build: the api -> sim contract direction, and one gate over both

solo_execution 6 specifies two contracts generated in opposite
directions. Phase 4 built one. This adds the other and folds both into
one script with one diff gate over all four generated paths, because two
scripts invite the second being forgotten in CI - which is the failure
mode rule 3 exists to prevent.

The nswag runtime is detected from dotnet --list-runtimes rather than
pinned to Net90, which was tuned to one machine.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 4: `wave_issuances`, and the two constraints that do the work

Design §4.3. One table, following `0001`'s conventions exactly, whose interesting parts are a partial unique index and a write-once trigger — because §4.1's one-live-issuance rule enforced by a handler is a rule someone can forget, and enforced by Postgres it is not.

**Files:**
- Create: `services/api/drizzle/0003_wave_issuances.sql`
- Modify: `services/api/src/db/schema.ts`
- Create: `services/api/test/issuance-schema.test.ts`

**Interfaces:**
- Consumes: `servers`, `players`, the `withServer()` transaction scope, the `broodline_app` role.
- Produces: `waveIssuances` in Drizzle with columns `serverId`, `issuanceId`, `playerId`, `waveId`, `seed`, `issuedAt`, `expiresAt`, `settledAt`, `settlement`. Tasks 5, 6 and 10 all read it.
- Produces: `seedInt8`, a Drizzle `customType` presenting `int8` as a decimal string in both directions. **Not** `bigint({ mode: 'string' })` — that mode does not exist in drizzle-orm 0.38 and does not typecheck; `'bigint'` typechecks and then throws in `JSON.stringify`.

- [ ] **Step 1: Write the failing test**

`services/api/test/issuance-schema.test.ts`:

```ts
import { sql } from 'drizzle-orm'
import { afterAll, beforeAll, describe, expect, it } from 'vitest'
import { withServer } from '../src/db/client.ts'
import { servers, players, accounts, waveIssuances } from '../src/db/schema.ts'
import { startTestDb, type TestDb } from './harness.ts'

let t: TestDb
const PLAYER = '00000000-0000-0000-0000-0000000000aa'

beforeAll(async () => {
  t = await startTestDb()
  await t.ownerDb.insert(servers).values({
    serverId: 1, region: 'us-central1', state: 'open', tickDayOfWeek: 0, tickMinuteOfDay: 1200,
  })
  // A player to hang issuances off, via the composite FK 0001 established.
  await t.ownerDb.insert(accounts).values({ accountId: PLAYER, serverId: 1 })
  await t.ownerDb.insert(players).values({ serverId: 1, playerId: PLAYER, accountId: PLAYER })
}, 240_000)

afterAll(async () => { await t?.stop() })

const issue = (db: typeof t.db, waveId: number, id: string) =>
  withServer(db, 1, (tx) => tx.insert(waveIssuances).values({
    serverId: 1, issuanceId: id, playerId: PLAYER, waveId,
    seed: '1234', expiresAt: new Date(Date.now() + 7_200_000),
  }))

describe('wave_issuances', () => {
  it('permits exactly one live issuance per player', async () => {
    await issue(t.db, 6, 'aaaaaaaa-0000-0000-0000-000000000001')

    // design 2.1: a client that can hold a hundred valid seeds simulates all
    // of them and submits the winner. The index is the defence, NOT a check
    // in the handler - a handler can be forgotten and an index cannot.
    await expect(issue(t.db, 6, 'aaaaaaaa-0000-0000-0000-000000000002'))
      .rejects.toThrow(/unique|duplicate/i)
  })

  it('permits a new issuance once the previous one is consumed', async () => {
    await withServer(t.db, 1, (tx) => tx.execute(
      sql`UPDATE wave_issuances SET settled_at = now(), settlement = 'consumed'
          WHERE issuance_id = 'aaaaaaaa-0000-0000-0000-000000000001'`))

    await expect(issue(t.db, 6, 'aaaaaaaa-0000-0000-0000-000000000003'))
      .resolves.toBeDefined()
  })

  it('refuses to rewrite a settlement', async () => {
    // The ledger's guard is "consumed inside the credit's transaction".
    // A settlement that can be reverted is not a guard at all.
    await withServer(t.db, 1, (tx) => tx.execute(
      sql`UPDATE wave_issuances SET settled_at = now(), settlement = 'consumed'
          WHERE issuance_id = 'aaaaaaaa-0000-0000-0000-000000000003'`))

    await expect(withServer(t.db, 1, (tx) => tx.execute(
      sql`UPDATE wave_issuances SET settled_at = NULL, settlement = NULL
          WHERE issuance_id = 'aaaaaaaa-0000-0000-0000-000000000003'`)))
      .rejects.toThrow(/settlement is write-once/)
  })

  it('is invisible without a server scope', async () => {
    // Not a new rule - 0002's default-deny, restated for the new table
    // because the isolation gate scans pg_class and this table must be in it.
    const rows = await t.db.execute(sql`SELECT * FROM wave_issuances`)
    expect(rows.rows).toHaveLength(0)
  })
})
```

- [ ] **Step 2: Run it to verify it fails**

```bash
pnpm --filter @broodline/api test issuance-schema
```

Expected: **FAIL** — `waveIssuances` is not exported from `schema.ts`.

- [ ] **Step 3: Write the migration**

`services/api/drizzle/0003_wave_issuances.sql`:

```sql
-- Design 4.3 AS AMENDED. server_id leads the primary key, as it leads every
-- server-scoped key in 0001 - solo_execution 4: no globally meaningful IDs,
-- because a merge must be a re-keying exercise.
--
-- settled_at/settlement REPLACE an earlier consumed_at. The first version
-- keyed the one-live index on `consumed_at IS NULL`, which still covers a row
-- that expired without ever being consumed - so an abandoned wave locked the
-- player out with a 23505 for at least an hour. Time cannot fix that in the
-- predicate: index predicates must be IMMUTABLE and now() is STABLE, so
-- `expires_at > now()` is REJECTED, not merely unwise. One terminal state,
-- reached only by a write, is what makes the index predicate and the
-- handler's liveness test the same expression.
CREATE TABLE IF NOT EXISTS wave_issuances (
  server_id    integer     NOT NULL,
  issuance_id  uuid        NOT NULL,
  player_id    uuid        NOT NULL,
  wave_id      integer     NOT NULL,
  -- Postgres bigint is SIGNED; the engine's seed is a ulong. wave/start draws
  -- from [0, 2^63-1] and this makes that contract loud at the boundary rather
  -- than implicit at four call sites. A seed that sign-flips re-simulates a
  -- DIFFERENT wave and surfaces as a hash mismatch on an honest submission.
  seed         bigint      NOT NULL CHECK (seed >= 0),
  issued_at    timestamptz NOT NULL DEFAULT now(),
  expires_at   timestamptz NOT NULL,
  settled_at   timestamptz,
  settlement   text        CHECK (settlement IN ('consumed','expired')),
  -- Neither half of the settlement exists without the other.
  CONSTRAINT wave_issuances_settled_together
    CHECK ((settled_at IS NULL) = (settlement IS NULL)),
  PRIMARY KEY (server_id, issuance_id),
  -- Composite, so a handler scoped to one server cannot bind an issuance to
  -- another server's player. 0001 added these to wallets, ledger and
  -- campaign_progress for the same reason, before money landed on them.
  FOREIGN KEY (server_id, player_id) REFERENCES players (server_id, player_id)
);

-- ONE LIVE ISSUANCE PER PLAYER, in Postgres rather than in a handler.
-- design 2.1's seed-shop defence, and the load-bearing half of wave/start.
CREATE UNIQUE INDEX IF NOT EXISTS wave_issuances_one_live
  ON wave_issuances (server_id, player_id)
  WHERE settled_at IS NULL;

-- The replay-cap count reads this - design 4.1 check 2. Only 'consumed'
-- counts: settling an abandoned wave must not charge the player a replay
-- they never took, which is why the settlement is an enum and not a boolean.
CREATE INDEX IF NOT EXISTS wave_issuances_replay_count
  ON wave_issuances (server_id, player_id, wave_id, issued_at)
  WHERE settlement = 'consumed';

-- The settlement is WRITE-ONCE. The issuance is the ledger's guard against a
-- second payout; a settlement that can be reverted is not a guard.
CREATE OR REPLACE FUNCTION reject_settlement_rewrite() RETURNS trigger AS $$
BEGIN
  IF OLD.settled_at IS NOT NULL AND
     (NEW.settled_at IS DISTINCT FROM OLD.settled_at OR
      NEW.settlement IS DISTINCT FROM OLD.settlement) THEN
    RAISE EXCEPTION 'wave_issuances settlement is write-once (attempted % / % -> % / %)',
      OLD.settled_at, OLD.settlement, NEW.settled_at, NEW.settlement;
  END IF;
  RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- DROP first. 0002's CREATE TRIGGER accounts_server_id_immutable is the one
-- statement in that file which is not re-runnable, and this is the same
-- pattern - recorded in the Phase 4 followups as worth fixing, and worth not
-- repeating.
DROP TRIGGER IF EXISTS wave_issuances_settlement_write_once ON wave_issuances;
CREATE TRIGGER wave_issuances_settlement_write_once
  BEFORE UPDATE ON wave_issuances
  FOR EACH ROW
  EXECUTE FUNCTION reject_settlement_rewrite();

GRANT SELECT, INSERT, UPDATE ON wave_issuances TO broodline_app;
-- No DELETE. The sweep at Task 12 runs as the owner; a handler has no reason
-- to delete an issuance and every reason not to be able to.

ALTER TABLE wave_issuances ENABLE ROW LEVEL SECURITY;
ALTER TABLE wave_issuances FORCE ROW LEVEL SECURITY;
-- DROP first: CREATE POLICY has no IF NOT EXISTS in PG16, and a file that
-- adds DROP TRIGGER IF EXISTS to avoid 0002's flaw should not repeat it one
-- statement over.
DROP POLICY IF EXISTS server_isolation ON wave_issuances;
CREATE POLICY server_isolation ON wave_issuances
  FOR ALL
  USING      (server_id = NULLIF(current_setting('app.server_id', true), '')::int)
  WITH CHECK (server_id = NULLIF(current_setting('app.server_id', true), '')::int);
```

- [ ] **Step 4: Add the table to Drizzle**

`services/api/src/db/schema.ts`, following the existing table style:

```ts
export const waveIssuances = pgTable('wave_issuances', {
  serverId: integer('server_id').notNull(),
  issuanceId: uuid('issuance_id').notNull(),
  playerId: uuid('player_id').notNull(),
  waveId: integer('wave_id').notNull(),
  // int8 as a decimal STRING, via customType. NOT bigint({mode:'string'}) -
  // that mode does not exist in drizzle-orm 0.38 and does not typecheck. Of
  // the two that do, 'number' reintroduces the 2^53 cliff and 'bigint' hands
  // back a JS BigInt that JSON.stringify THROWS on - and both wave/start and
  // wave/submit serialise this field into a response body.
  seed: seedInt8('seed').notNull(),
  issuedAt: timestamp('issued_at', { withTimezone: true }).notNull().defaultNow(),
  expiresAt: timestamp('expires_at', { withTimezone: true }).notNull(),
  settledAt: timestamp('settled_at', { withTimezone: true }),
  settlement: text('settlement'),
}, (t) => ({
  pk: primaryKey({ columns: [t.serverId, t.issuanceId] }),
}))
```

- [ ] **Step 5: Run the tests to verify they pass**

```bash
pnpm --filter @broodline/api test issuance-schema
```

Expected: **PASS**, four tests.

- [ ] **Step 6: Confirm the isolation gate picked the table up on its own**

Phase 4's gate queries `pg_class` for every ordinary table in `public`, subtracts an explicit allowlist, and requires `relrowsecurity AND relforcerowsecurity` on the rest. That design means a new table is protected by default and a *forgotten* RLS block is a red test rather than a silent hole:

```bash
pnpm --filter @broodline/api test isolation
```

Expected: **PASS**. Then prove the gate is doing it:

```bash
# Comment out the two ALTER TABLE lines in 0003, drop the test database
# volume, re-run:
pnpm --filter @broodline/api test isolation
```

Expected: **FAIL**, naming `wave_issuances`. Restore.

- [ ] **Step 7: Commit**

```bash
git add services/api/drizzle/0003_wave_issuances.sql services/api/src/db/schema.ts \
        services/api/test/issuance-schema.test.ts
git commit -m "feat: wave_issuances, with the one-live rule in the index

A partial unique index on (server_id, player_id) WHERE settled_at IS
NULL, because design 2.1's seed-shop defence enforced by a handler is a
rule someone can forget. The settlement is write-once by trigger, since
it is the ledger's guard against a second payout.

settled_at/settlement rather than a consumed_at: a row that expired
without being consumed is still unconsumed, so keying the index on that
locked a player out after any abandoned wave. One terminal state reached
only by a write makes the index predicate and the handler's liveness
test the same expression.

The trigger drops before it creates - 0002's equivalent does not, which
is the one statement in that file that cannot be re-run.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 5: `POST /v1/wave/start` — five checks, and the seed

Design §4.1. The endpoint's interesting property is that four of its five checks exist to stop something, and the one most easily left out — the replay cap — is the one whose absence makes the phase's own reward claim false.

**Files:**
- Create: `services/api/src/wave/issuance.ts`, `services/api/src/wave/rewards.ts`, `services/api/src/routes/wave.ts`
- Create: `services/api/test/wave-start.test.ts`
- Modify: `services/api/src/app.ts`, `services/api/src/schemas.ts`

**Interfaces:**
- Consumes: `requireSession`, `withServer`, `loadBundle`, `campaignProgress`, `waveIssuances`, `fail`.
- Produces:
  - `issueWave(tx, serverId, playerId, waveId, bundle): Promise<Issuance | IssuanceRefusal>`, where an `Issuance` is a `wave_issuances` row — `{ serverId, issuanceId, playerId, waveId, seed, issuedAt, expiresAt, settledAt, settlement }`.
  - `settle(tx, issuance, 'consumed' | 'expired'): Promise<void>` — the only writer of the settlement columns. Task 6 consumes it.
  - `rewardForWave(bundle, waveId): { currency: Currency; amount: number } | null`.
  - `POST /v1/wave/start` → `{ issuanceId, seed, waveId, expiresAt }`.

- [ ] **Step 1: Write the shared helpers, then the failing test**

Tasks 5, 6, 8 and 10 all drive the same two routes, and a helper redefined in
four files drifts in four directions. **`services/api/test/wave-helpers.ts`
first**, exporting everything those suites use:

```ts
// The replay builders construct records the SAME WAY services/sim's tests do
// - from the engine's own constants rather than from a fixture file - so a
// rules change breaks these loudly instead of letting them drift into
// asserting against a wave that no longer exists.
export interface ReplayOpts { engineVersion?: string; trait?: string; tier?: number }

export function buildWinningReplay(waveId: number, seed: bigint, o: ReplayOpts = {}): string
export function buildLosingReplay(waveId: number, seed: bigint, o: ReplayOpts = {}): string

// Route drivers. `app` and `token` are module-level, set by setupPlayer().
export async function setupPlayer(deps: Deps): Promise<{ playerId: string; token: string }>
export function startWave(waveId: number): Promise<Response>
export function submitInit(issuanceId: string, replay: string, key: string): RequestInit
export function submit(issuanceId: string, replay: string, key: string): Promise<Response>

// State readers - these exist so a test can assert a BALANCE rather than an
// error code, which design 7 names as the difference between a real gate and
// one that passes against a server that rejects everything.
export function balance(currency: Currency): Promise<number>
export function ledgerRowCount(): Promise<number>
export function liveIssuance(): Promise<typeof waveIssuances.$inferSelect | undefined>
export function consumeLiveIssuance(): Promise<void>
export function clearWave(waveId: number): Promise<void>      // consume + advance progress
export function clearThrough(waveId: number): Promise<void>
```

**The replay builders are the load-bearing part.** Both must produce records
that `Replay.Deserialize` accepts, which means writing the binary layout from
TypeScript: magic `0x50524C42`, format version `1`, a length-prefixed UTF-8
engine version, then the header and deployment fields little-endian, exactly as
`engine/Runtime/Combat/Replay.cs` `Serialize()` lays them out. Read that method
and mirror it; **do not guess the field order**, and note the trailing-data check
— a record with even one extra byte is rejected.

`buildLosingReplay` differs in one field: a deployment carrying `Trait.None`
rather than `Trait.Chill`, so the Courser is unanswered and breaches.

Then `services/api/test/wave-start.test.ts`. The setup mirrors `sync.test.ts` — a real player created through the real route, so the endpoint reads what the grant actually wrote:

```ts
describe('POST /v1/wave/start', () => {
  it('issues a seed for the next uncleared wave', async () => {
    const res = await start(6)
    expect(res.status).toBe(200)
    const body = await res.json() as { issuanceId: string; seed: string; expiresAt: string }

    expect(body.issuanceId).toMatch(/^[0-9a-f-]{36}$/)
    // A STRING. The seed is a ulong; a JSON number loses the top bits and
    // the client would re-simulate against a different seed than the server
    // stored - which presents as a hash mismatch on an honest submission,
    // the single most misleading failure this phase could ship.
    expect(typeof body.seed).toBe('string')
    expect(new Date(body.expiresAt).getTime() - Date.now()).toBeGreaterThan(7_100_000)
  })

  it('returns the SAME issuance rather than minting a second', async () => {
    const first = await (await start(6)).json() as { issuanceId: string; seed: string }
    const second = await (await start(6)).json() as { issuanceId: string; seed: string }

    // design 2.1. Determinism is what makes verification cheap; it is also
    // what makes seed-shopping cheap, and this is the defence.
    expect(second.issuanceId).toBe(first.issuanceId)
    expect(second.seed).toBe(first.seed)
  })

  it('refuses a wave beyond the next one', async () => {
    const res = await start(20)
    expect(res.status).toBe(409)
    expect(await res.json()).toMatchObject({ code: 'wave_locked' })
  })

  it('refuses a wave absent from the bundle', async () => {
    // The bundle carries wave 6 only. A wave id the content does not define
    // is wave_locked, not a 500 - the bundle is the content source, per
    // solo_execution 5.2's content-versus-data split.
    const res = await start(1)
    expect([409]).toContain(res.status)
    expect(await res.json()).toMatchObject({ code: 'wave_locked' })
  })

  it('refuses a fourth replay of a cleared wave in one day', async () => {
    await clearWave(6)   // helper: consume an issuance and advance progress

    for (let i = 0; i < 3; i++) {
      expect((await start(6)).status).toBe(200)
      await consumeLiveIssuance()
    }

    // broodline_campaign_structure.md: three replays per wave per day, then
    // nothing until tomorrow. WITHOUT THIS CHECK wave 1 is farmable
    // indefinitely and re-simulation never notices, because every one of
    // those runs is honest - design 4.1 check 2.
    const res = await start(6)
    expect(res.status).toBe(429)
    expect(await res.json()).toMatchObject({ code: 'replay_cap_reached' })
  })

  it('refuses without a session', async () => {
    const res = await app.request('/v1/wave/start', {
      method: 'POST',
      headers: { 'content-type': 'application/json' },
      body: JSON.stringify({ waveId: 6 }),
    })
    expect(res.status).toBe(401)
  })
})
```

- [ ] **Step 2: Run it to verify it fails**

```bash
pnpm --filter @broodline/api test wave-start
```

Expected: **FAIL** — 404 on every case.

- [ ] **Step 3: Give wave 6 a reward, because the lookup below has nothing to read**

`rewardForWave` reads `wave.reward`, and `config/bundles/0.1.0/waves.json`
carries `{id, integrity, laneCount, spawns}` and no reward. The **field** lands
here, with the tests that need it; the **validator rule** that makes it mandatory
for every authored wave is Task 7, because it belongs with the C# validator
rather than with a route.

> **Ruling (human partner, pre-flight): create `0.1.1`, do not edit `0.1.0`.**
> `0.1.0` is already published to GCS from Phase 4. Editing the authored
> directory in place would leave the repo's `0.1.0` disagreeing with what is
> live at `0.1.0` — which is the confusion the never-overwrite rule exists to
> prevent, even though the GCS object itself is untouched.

```bash
cp -R config/bundles/0.1.0 config/bundles/0.1.1
```

Then set `"version": "0.1.1"` in `config/bundles/0.1.1/manifest.json`, leave
`minimumClientVersion` at `0.1.0`, and add the reward to
`config/bundles/0.1.1/waves.json`. **`config/bundles/0.1.0/` is not modified by
this task or any other.** Tests publish `0.1.1`.

`config/bundles/0.1.1/waves.json`:

```json
[
  { "id": 6, "integrity": 2, "laneCount": 1,
    "reward": { "currency": "shards", "amount": 40 },
    "spawns": [{ "tick": 90, "type": "Courser" }] }
]
```

**40 shards is a starting value and belongs on the playtest list**, not in this
plan — `broodline_economy_model.md` anchors 40/hr and owns the real number. What
matters structurally is that it is *content in the bundle*, per `solo_execution`
§5.2's content-versus-data split, so changing it is a bundle publish and not a
deploy.

Extend the bundle's Zod type in `services/api/src/config/bundle.ts` to carry the
optional `reward`. Every suite from Task 5 onward publishes `0.1.1`; update the
`SEED` constant in the existing `sync.test.ts` harness only if it breaks, since
`0.1.0` still validates and still carries no reward.

- [ ] **Step 4: Write the reward lookup**

`services/api/src/wave/rewards.ts`:

```ts
import type { Bundle } from '../config/bundle.ts'
import type { Currency } from '../money/ledger.ts'

export interface Reward { currency: Currency; amount: number }

/**
 * Design 2.2: the reward is a function of the ISSUANCE'S wave id, never of
 * the submission. The submission decides WHETHER it is paid; it never
 * decides WHAT.
 *
 * That is what makes an unowned deployment - which this phase cannot detect,
 * because there is no creature table until Phase 6 - bounded to "win a wave
 * you would otherwise lose" rather than "mint currency".
 */
export function rewardForWave(bundle: Bundle, waveId: number): Reward | null {
  const wave = bundle.waves.find((w) => w.id === waveId)
  if (wave === undefined || wave.reward === undefined) return null
  return { currency: wave.reward.currency, amount: wave.reward.amount }
}
```

- [ ] **Step 5: Write the issuance module**

`services/api/src/wave/issuance.ts`. The five checks in the order design §4.1 sets, all inside one `withServer()` transaction:

```ts
import { randomUUID } from 'node:crypto'
import { and, eq, gt, isNotNull, isNull, sql } from 'drizzle-orm'
import { campaignProgress, waveIssuances } from '../db/schema.ts'

export const ISSUANCE_TTL_MS = 7_200_000    // two hours - design 4.1
export const REPLAY_CAP_PER_DAY = 3         // broodline_campaign_structure.md

export type IssuanceRefusal =
  | { refused: 'wave_locked' }
  | { refused: 'replay_cap_reached' }

export async function issueWave(tx: Tx, serverId: number, playerId: string,
                                waveId: number, bundle: Bundle) {
  const [progress] = await tx.select().from(campaignProgress)
    .where(and(eq(campaignProgress.serverId, serverId),
               eq(campaignProgress.playerId, playerId)))
  const cleared = progress?.highestWaveCleared ?? 0

  // 1. The wave is the next one, or one already cleared.
  if (waveId > cleared + 1 || waveId < 1) return { refused: 'wave_locked' as const }

  // 2. The replay cap, counted off CONSUMED issuances since the day
  //    boundary. Not a second counter: one source of truth, and design 4.3
  //    retains consumed rows 48 hours precisely so this query can see them.
  if (waveId <= cleared) {
    const [{ used }] = await tx.select({ used: sql<number>`count(*)::int` })
      .from(waveIssuances)
      .where(and(
        eq(waveIssuances.serverId, serverId),
        eq(waveIssuances.playerId, playerId),
        eq(waveIssuances.waveId, waveId),
        eq(waveIssuances.settlement, 'consumed'),
        sql`${waveIssuances.issuedAt} >= date_trunc('day', now())`))
    if (used >= REPLAY_CAP_PER_DAY) return { refused: 'replay_cap_reached' as const }
  }

  // 3. The wave is authored in the bundle.
  if (!bundle.waves.some((w) => w.id === waveId)) return { refused: 'wave_locked' as const }

  // 4. A live issuance is RETURNED, not replaced - design 2.1.
  //
  // Two cases, and the second is the one the first version of this design got
  // wrong. A row with settled_at IS NULL is in the one-live index whether or
  // not it has expired, so an UNEXPIRED one is returned as-is, and an EXPIRED
  // one - the abandoned-wave path - must be SETTLED 'expired' before the
  // insert below, or that insert collides and the player cannot start a wave
  // at all. Settling it 'consumed' instead would charge them a replay they
  // never took, which is why the settlement is an enum.
  const [live] = await tx.select().from(waveIssuances)
    .where(and(
      eq(waveIssuances.serverId, serverId),
      eq(waveIssuances.playerId, playerId),
      isNull(waveIssuances.settledAt)))
  if (live !== undefined) {
    if (live.expiresAt > new Date()) return live
    await settle(tx, live, 'expired')
  }

  // 5. The seed comes from the CSPRNG, and the row is written before the
  //    response is formed. randomUUID is crypto-backed; the seed is drawn
  //    the same way rather than from Math.random, which is neither seeded
  //    nor unpredictable and would make the seed guessable in advance.
  //
  //    MASKED TO 63 BITS. Postgres bigint is signed and the column carries
  //    CHECK (seed >= 0); a full 64-bit draw lands at or above 2^63 half the
  //    time and either errors on insert or, if someone casts around it,
  //    sign-flips into a DIFFERENT wave - which presents as a hash mismatch
  //    on an honest submission. One bit is nothing to a PRNG stream selector.
  const seed = crypto.getRandomValues(new BigUint64Array(1))[0] & 0x7fff_ffff_ffff_ffffn

  const [row] = await tx.insert(waveIssuances).values({
    serverId, issuanceId: randomUUID(), playerId, waveId,
    seed: seed.toString(),
    expiresAt: new Date(Date.now() + ISSUANCE_TTL_MS),
  }).returning()
  return row
}
```

**On check 4 versus the unique index.** The select-then-insert is a race: two concurrent `wave/start` calls can both find no live row. The index is what makes that safe — the loser gets a unique violation rather than a second seed. The select exists to return the *same* issuance on the common path, not to prevent the race. **Do not "fix" the race by removing the index.**

- [ ] **Step 6: Register the new error codes, then write the route**

`fail()` takes **three** arguments — `(code, message, details?)` — and derives
the status from a **closed `ErrorCode` union** and a `STATUS` map in
`services/api/src/http/errors.ts`. A handler cannot pass a status, so **every
code this phase introduces must be added to both** or it will not compile:

```ts
export type ErrorCode =
  | 'invalid_request' | 'idempotency_key_reused' | 'unauthorized'
  | 'not_found' | 'conflict' | 'client_too_old' | 'internal'
  // Phase 5
  | 'wave_locked' | 'replay_cap_reached' | 'issuance_invalid'
  | 'submission_rejected' | 'engine_too_old' | 'sim_unavailable'

const STATUS: Record<ErrorCode, number> = {
  ...,
  wave_locked: 409,
  replay_cap_reached: 429,
  issuance_invalid: 409,
  submission_rejected: 409,
  // 426, the same status client_too_old uses. A client whose ENGINE is
  // superseded and one whose BUILD is below the floor both need the same
  // thing from the player, and the codes stay distinct so the client can
  // say which - design 2.3.
  engine_too_old: 426,
  sim_unavailable: 503,
}
```

Two new statuses, `429` and `503`, neither of which Phase 4 used.

Then `services/api/src/routes/wave.ts` — `requireSession` first, `serverId` and `playerId` from the **verified claim**, never from the body, per Phase 4's carried-forward warning that RLS defends against a handler that forgets to scope, not one that scopes to the wrong server.

- [ ] **Step 7: Run the tests to verify they pass**

```bash
pnpm --filter @broodline/api test wave-start
```

Expected: **PASS**, six tests.

- [ ] **Step 8: Prove the replay cap test is looking**

```bash
# Comment out check 2 in issuance.ts, then:
pnpm --filter @broodline/api test wave-start
```

Expected: **FAIL** on the fourth-replay case. Restore.

- [ ] **Step 9: Commit**

```bash
git add services/api/src/wave services/api/src/routes/wave.ts \
        services/api/src/app.ts services/api/test/wave-start.test.ts
git commit -m "feat: POST /v1/wave/start, and the replay cap that makes the reward claim true

Five checks. Four stop something obvious; the fifth - three replays per
wave per day, from broodline_campaign_structure.md - is the one whose
absence makes design 4.4's 'cannot inflate a reward' false, because a
cleared wave is farmable and every one of those runs is honest.

One live issuance per player is returned rather than replaced. The
select is for the common path; the partial unique index is what makes
the race safe.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 6: `POST /v1/wave/submit` — one verification, one credit

The centrepiece. Design §4.2: a six-step sequence in which the order matters, and **two independent once-only guards that fail differently**.

**Files:**
- Create: `services/api/src/sim/client.ts`
- Modify: `services/api/src/routes/wave.ts`, `services/api/src/app.ts`
- Modify: `services/api/src/openapi.ts` — **register BOTH `/v1/wave/start` and `/v1/wave/submit`**
- Regenerate + commit: `openapi/broodline.json`, `client/Assets/Generated/Api/`
- Create: `services/api/test/wave-submit.test.ts`

> **Added after Task 5's review.** `openapi.ts` registers only `/v1/account` and
> `/v1/sync`. NSwag generates the Unity client from that document, so a route
> absent from it **has no method on the shipped client** — and `/v1/wave/start`
> is an endpoint the client is required to call. Task 5 wrote the schemas and
> left them unregistered; no task owned closing it, so both routes ship absent
> on the plan as written.
>
> **The contract gate cannot catch this.** `contract.test.ts` asserts the
> generated output is *fresh*, not that it is *complete* — it stays green
> forever with an endpoint missing. Registering both here produces one coherent
> contract diff rather than two, and `§1`'s scope line already reads as
> expecting these routes in the Unity → `api` direction.

**Interfaces:**
- Consumes: `withIdempotency(db, serverId, key, requestHash, fn)`, `hashRequest`, `credit(tx, m)`, `rewardForWave`, `waveIssuances`, `campaignProgress`, the generated `sim` types from Task 3.
- Produces:
  - `SimClient.simulate(replayBase64): Promise<SimVerdict>` where `SimVerdict = { kind: 'verified'; echo; outcome } | { kind: 'rejected'; reason } | { kind: 'unavailable' }`.
  - `POST /v1/wave/submit` → `{ result, integrityRemaining, breaches, reward? }`.

- [ ] **Step 1: Write the sim client, and make the outage case a distinct kind**

`services/api/src/sim/client.ts`:

```ts
/**
 * The only caller of sim, and the only place that decides what a failure
 * MEANS.
 *
 * Three outcomes, not two. A rejected submission and an unreachable service
 * are both "the request did not succeed", and collapsing them is the bug
 * this type exists to prevent: a rejection must CONSUME nothing and return a
 * refusal, while an outage must leave the issuance live so the player can
 * retry. Design 3.2 is why sim answers a rejection with 200 - so this
 * distinction is available here at all.
 */
export type SimVerdict =
  | { kind: 'verified'; echo: SimulateEcho; outcome: SimulateOutcome }
  | { kind: 'rejected'; reason: string }
  | { kind: 'unavailable' }

export class SimClient {
  constructor(private readonly baseUrl: string, private readonly fetchImpl = fetch) {}

  async simulate(replayBase64: string): Promise<SimVerdict> {
    let res: Response
    try {
      res = await this.fetchImpl(`${this.baseUrl}/internal/simulate`, {
        method: 'POST',
        headers: { 'content-type': 'application/json' },
        body: JSON.stringify({ replay: replayBase64 }),
        // The SLO is p99 500ms end to end including re-simulation, and one
        // wave is ~20ms of CPU. A request still open at 5s is not slow, it
        // is wedged - and without a timeout it holds a Cloud Run instance
        // and a Postgres connection with it.
        signal: AbortSignal.timeout(5_000),
      })
    } catch {
      return { kind: 'unavailable' }
    }

    if (!res.ok) return { kind: 'unavailable' }

    const body = await res.json() as SimulateResponse
    if (body.verdict === 'rejected') return { kind: 'rejected', reason: body.reason ?? 'unknown' }
    return { kind: 'verified', echo: body.echo!, outcome: body.outcome! }
  }
}
```

- [ ] **Step 2: Write the failing test**

`services/api/test/wave-submit.test.ts`. The suite runs against a **real `sim`** — started once for the file — rather than a stub, because a stub is a second implementation of the boundary this phase exists to prove:

```ts
describe('POST /v1/wave/submit', () => {
  it('pays a winning submission exactly once', async () => {
    const { issuanceId, seed } = await startWave(6)
    const replay = buildWinningReplay(6, seed)     // helper, mirrors sim's ValidRecord

    const res = await submit(issuanceId, replay, 'key-1')
    expect(res.status).toBe(200)
    expect(await res.json()).toMatchObject({ result: 'Win', reward: { currency: 'shards', amount: 40 } })

    expect(await balance('shards')).toBe(250 + 40)
    expect(await ledgerRowCount()).toBe(3)          // 2 from the starter grant, 1 from this
  })

  it('returns the stored response on a resend with the SAME key', async () => {
    const { issuanceId, seed } = await startWave(6)
    const replay = buildWinningReplay(6, seed)
    const before = await balance('shards')

    await submit(issuanceId, replay, 'key-2')
    const second = await submit(issuanceId, replay, 'key-2')

    expect(second.status).toBe(200)
    // Guard one: the idempotency key protects the RESPONSE.
    expect(await balance('shards')).toBe(before + 40)
  })

  it('pays nothing on a resend with a DIFFERENT key', async () => {
    const { issuanceId, seed } = await startWave(6)
    const replay = buildWinningReplay(6, seed)
    const before = await balance('shards')

    await submit(issuanceId, replay, 'key-3')
    const second = await submit(issuanceId, replay, 'key-4-different')

    // Guard two: the issuance protects the LEDGER. 6.3's key is
    // client-supplied and a modified client simply sends a new one, so
    // idempotency alone is not a defence - design 4.2.
    expect(second.status).toBe(409)
    expect(await second.json()).toMatchObject({ code: 'issuance_invalid' })

    // Assert the BALANCE, not the code. A test that only checks for
    // issuance_invalid passes identically against a server that rejects
    // everything - design 7's vacuity guard.
    expect(await balance('shards')).toBe(before + 40)
  })

  it('refuses a replay whose seed is not the issued one', async () => {
    const { issuanceId } = await startWave(6)
    const replay = buildWinningReplay(6, 0xDEADBEEFn)   // a seed nobody issued

    const res = await submit(issuanceId, replay, 'key-5')
    expect(res.status).toBe(409)
    expect(await res.json()).toMatchObject({ code: 'submission_rejected' })
    expect(await liveIssuance()).toBeUndefined()        // consumed, not re-usable
  })

  it('refuses a replay of a different wave against this issuance', async () => {
    // Step 5 and step 2 are DIFFERENT checks. The issuance proves the player
    // was given a wave; the seed proves this replay is of THAT wave.
    const { issuanceId } = await startWave(6)
    const res = await submit(issuanceId, buildWinningReplay(6, 1n), 'key-6')
    expect(res.status).toBe(409)
  })

  it('refuses a submission from a superseded engine and tells the client to update', async () => {
    const { issuanceId, seed } = await startWave(6)
    const replay = buildWinningReplay(6, seed, { engineVersion: '0.1.0' })

    const res = await submit(issuanceId, replay, 'key-7')
    expect(res.status).toBe(426)
    expect(await res.json()).toMatchObject({ code: 'engine_too_old' })
  })

  it('leaves the issuance live when sim is unreachable', async () => {
    const { issuanceId, seed } = await startWave(6)
    const broken = createApp({ ...deps, simClient: new SimClient('http://127.0.0.1:1') })

    const res = await broken.request('/v1/wave/submit', submitInit(issuanceId, buildWinningReplay(6, seed), 'key-8'))
    expect(res.status).toBe(503)
    expect(await res.json()).toMatchObject({ code: 'sim_unavailable' })

    // design 8: this phase does NOT grant optimistically and does NOT build
    // a clawback. The issuance surviving is what makes a retryable 503 an
    // honest answer rather than a lost wave.
    expect(await liveIssuance()).toMatchObject({ issuanceId })
  })

  it('records a loss without paying', async () => {
    const { issuanceId, seed } = await startWave(6)
    const res = await submit(issuanceId, buildLosingReplay(6, seed), 'key-9')
    const body = await res.json() as { result: string; reward?: unknown; breaches: unknown[] }

    expect(body.result).toBe('Loss')
    expect(body.reward).toBeUndefined()
    // combat_engine 7's three booleans, forwarded for the Wave Defeat
    // screen. api stores none of them and interprets none of them.
    expect(body.breaches[0]).toHaveProperty('access')
  })
})
```

- [ ] **Step 3: Run it to verify it fails**

```bash
pnpm --filter @broodline/api test wave-submit
```

Expected: **FAIL** — 404 on every case.

- [ ] **Step 4: Write the handler**

In `services/api/src/routes/wave.ts`. The sequence, and **the order is the design**:

```ts
app.post('/v1/wave/submit', async (c) => {
  const session = await requireSession(c)            // 1
  const key = c.req.header('idempotency-key')
  if (!key) return fail('invalid_request', 'An idempotency key is required.')

  const body = parseSubmit(await c.req.json())
  if (body === null) return fail('invalid_request', 'Malformed submission.')

  // 3 BEFORE the transaction. sim is a network call; holding a Postgres
  // connection and an open transaction across it is how a 20ms simulation
  // exhausts the pool that solo_execution 5.7 names as the real ceiling.
  const verdict = await deps.simClient.simulate(body.replay)

  // Three arguments. fail() derives the status from the ErrorCode map that
  // Task 5 Step 6 extended; a handler cannot pass one.
  if (verdict.kind === 'unavailable')
    return fail('sim_unavailable', 'Verification is temporarily unavailable. Retry.')

  if (verdict.kind === 'rejected') {
    // 4. engine_too_old is 426 and player-visible - design 2.3. The other
    // reasons are 409 and say nothing about which check caught them.
    if (verdict.reason === 'engine_too_old')
      return fail('engine_too_old', 'This version of Broodline can no longer submit waves. Please update.')
    return await consumeAndRefuse(session, body.issuanceId)
  }

  const result = await withIdempotency(
    deps.db, session.serverId, key, hashRequest(body),
    async (tx) => {
      // 2. Load the issuance. Absent, expired or consumed are one answer.
      const issuance = await loadLiveIssuance(tx, session.serverId, session.playerId, body.issuanceId)
      if (issuance === undefined) return { refused: 'issuance_invalid' as const }

      // 5. The replay is OF that issuance. Without this a player starts
      //    wave 7, simulates wave 3 locally against an old seed, and
      //    submits it against the wave 7 issuance.
      if (verdict.echo.seed !== issuance.seed || String(verdict.echo.waveId) !== String(issuance.waveId)) {
        await settle(tx, issuance, 'consumed')
        return { refused: 'submission_rejected' as const }
      }

      // 6. One transaction: settle, advance, credit.
      await settle(tx, issuance, 'consumed')

      if (verdict.outcome.result !== 'Win')
        return { paid: null, outcome: verdict.outcome }

      await advanceCampaign(tx, session, issuance.waveId)

      // The reward comes from the ISSUANCE'S wave id - design 2.2. Never
      // from verdict.echo, which is the client's bytes echoed back.
      const reward = rewardForWave(await loadBundle(deps.bundleStore), issuance.waveId)
      if (reward === null) return { refused: 'wave_locked' as const }

      await credit(tx, {
        serverId: session.serverId, playerId: session.playerId,
        currency: reward.currency, amount: reward.amount,
        reason: `wave:${issuance.waveId}`,
      })
      return { paid: reward, outcome: verdict.outcome }
    })

  // withIdempotency returns { status: 'fresh' | 'replayed', body }. BOTH map
  // to the same response - that is the entire point of the wrapper - so the
  // status is not branched on here, only the refusal inside the body.
  const outcome = result.body
  if ('refused' in outcome) return fail(outcome.refused, refusalMessage(outcome.refused))

  return c.json({
    result: outcome.outcome.result,
    integrityRemaining: outcome.outcome.integrityRemaining,
    breaches: outcome.outcome.breaches,
    // Absent rather than null on a loss, so a client cannot render a zero.
    ...(outcome.paid === null ? {} : { reward: outcome.paid }),
  })
})
```

**`IdempotencyMismatchError` must be caught** the way `routes/account.ts`
catches it — the same key with a different `request_hash` is `422`, never
another request's response. Copy that call site's handling rather than
reinventing it.

**Why `settle` runs on the rejection path at step 5 and not on the outage path at step 3.** A submission that reached `sim` and was verified against the wrong issuance is a *spent attempt* — the player simulated a wave and got an answer. An outage produced no answer at all. Consuming on the first and not the second is what makes the retry story honest in both directions.

- [ ] **Step 5: Run the tests to verify they pass**

```bash
pnpm --filter @broodline/api test wave-submit
```

Expected: **PASS**, eight tests.

- [ ] **Step 6: Prove each guard independently**

Three weakenings, each of which must break exactly one test. Run them one at a time, restoring between:

```bash
# (a) Delete the seed comparison at step 5
pnpm --filter @broodline/api test wave-submit   # expect FAIL: "seed is not the issued one"

# (b) Move settle() out of the credit's transaction (await it after withIdempotency)
pnpm --filter @broodline/api test wave-submit   # expect FAIL: "DIFFERENT key"

# (c) Return the reward from verdict.echo.waveId instead of issuance.waveId
pnpm --filter @broodline/api test wave-submit   # expect FAIL once Task 10's suite exists
```

**(c) does not fail yet, and that is the finding.** No test in this file distinguishes the issuance's wave id from the echoed one, because every honest submission agrees on both. It is Task 10's job, and noting it here rather than discovering it there is the point of running the weakening now.

- [ ] **Step 7: Commit**

```bash
git add services/api/src/sim services/api/src/routes/wave.ts \
        services/api/src/app.ts services/api/test/wave-submit.test.ts
git commit -m "feat: POST /v1/wave/submit, verified once and paid once

Two independent guards that fail differently: the idempotency key
protects the response, and the issuance - consumed inside the credit's
transaction - protects the ledger. The key is client-supplied, so a
modified client sends a new one; the issuance is not.

sim is called BEFORE the transaction opens, because holding a Postgres
connection across a network call is how a 20ms simulation exhausts the
pool. An outage leaves the issuance live; a rejection consumes it.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 7: The validator rule — an authored wave without a reward does not publish

Task 5 gave wave 6 a reward. This makes it **mandatory**, in the validator whose whole purpose is failing loudly, and fixes the one parked finding that fails silently inside it.

**Files:**
- Modify: `tools/config-validate/Program.cs`, `services/api/src/config/validate.ts`
- Create: `services/api/test/validate-rewards.test.ts`

**Interfaces:**
- Consumes: the bundle directory shape, `WaveDef.Validate()`.
- Produces: a non-zero exit and a named wave id when a wave carries no reward, or a reward of zero or less.

- [ ] **Step 1: Write the failing test**

> **Corrected before dispatch.** An earlier draft of this step used
> `await expect(validateBundle(dir)).rejects.toThrow(...)` and a `bundleFixture()`
> helper. **Both are wrong.** `validateBundle(dir)` returns
> `Promise<string[]>` — a list of human-readable violations, empty when the
> bundle is good — and it does not throw. Fixtures are **directories** under
> `services/api/test/fixtures/`, reached by the `FIX(name)` helper that
> `config.test.ts` already defines. Follow that file's conventions exactly.

Two new fixture directories, each a minimal bundle copied from
`config/bundles/0.1.1/` with one thing wrong — matching the shape of the
existing `bad-ladder` and `bad-waves` fixtures:

| Fixture | What is wrong |
|---|---|
| `missing-wave-reward` | `waves.json`'s wave 6 has no `reward` key |
| `zero-wave-reward` | wave 6 has `"reward": { "currency": "shards", "amount": 0 }` |

Then, in `services/api/test/config.test.ts`, beside the existing validation
cases:

```ts
it('refuses a wave with no reward', async () => {
  const v = await validateBundle(FIX('missing-wave-reward'))
  expect(v).toHaveLength(1)
  expect(v[0]).toMatch(/wave 6.*reward/i)
})

it('refuses a reward of zero', async () => {
  const v = await validateBundle(FIX('zero-wave-reward'))
  expect(v).toHaveLength(1)
  expect(v[0]).toMatch(/wave 6.*reward/i)
})
```

**Assert on the array, and assert its length.** `expect(v[0]).toMatch(...)` alone
passes against a validator that returns *one correct violation plus four
spurious ones*, and `toHaveLength(1)` is what pins "this rule fired, and only
this rule".

- [ ] **Step 2: Run it to verify it fails**

```bash
pnpm --filter @broodline/api test config
```

Expected: **FAIL** — both fixtures validate clean, so `v` is `[]` and
`toHaveLength(1)` fails.

- [ ] **Step 3: Fix the pack-ladder `NaN` finding while you are in this file**

Phase 4 followups §5 describes this as "`priceUsdCents: 0` coerces to `NaN`,
the comparison is silently false, so a free pack anywhere but first position is
exempted." **That description is wrong, and the real behaviour is worse because
it is inconsistent.** `validatePackLadder` computes `prev.value / prev.priceUsdCents`
directly, so a zero price yields:

| `value` | rate | effect on `currRate < prevRate` |
|---|---|---|
| `> 0` | `Infinity` | A free pack sorts first, so `prevRate` is `Infinity` and **every** later pack reports a FALSE violation |
| `0` | `NaN` | Every comparison against it is false, so the pack is **silently exempt** |

So the same malformed input either fails loudly and wrongly, or passes silently,
depending on a second field. `packs.json` is `{ "packs": [] }` today, so this
costs nothing now and is a real hole the moment it is not.

Guard the input rather than the comparison, and return a violation rather than
throwing — `validateBundle` collects strings:

```ts
for (const pack of packs) {
  if (!Number.isFinite(pack.priceUsdCents) || pack.priceUsdCents <= 0)
    violations.push(`Pack '${pack.id}' has a non-positive priceUsdCents; the ladder is undefined for it.`)
}
if (violations.length > 0) return violations
```

**`<= 0`, not `< 0`.** Zero is the case that breaks the arithmetic, and a free
pack is a store concept the ladder has no opinion about — it must be excluded
from the rate comparison, not ranked within it.

- [ ] **Step 4: Add the reward rule**

Reward validation is a **bundle-shape** rule, not a game rule, so it belongs in `validate.ts` and **not** in the C# CLI. `solo_execution` §6.1 forbids game logic in two languages; "every wave has a reward" is not a game rule — it is a completeness check on content, and the C# validator's job is invoking `WaveDef.Validate()`, which is.

- [ ] **Step 5: Run to verify it passes, then prove the real bundle still publishes**

```bash
pnpm --filter @broodline/api test validate-rewards
./implementation/scripts/publish-bundle.sh --dry-run config/bundles/0.1.1
```

Expected: **PASS**, and the real bundle validating — Task 5 gave it the field.

- [ ] **Step 6: Commit**

```bash
git add tools/config-validate services/api/src/config/validate.ts \
        services/api/test/validate-rewards.test.ts
git commit -m "feat: an authored wave without a reward does not publish

Design 2.2 makes the reward a function of the wave id, which means a
wave missing one is a payout path that 500s rather than a content
oversight. Also fixes the pack ladder treating priceUsdCents: 0 as NaN -
the one parked finding that failed silently, inside the validator whose
whole purpose is failing loudly.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 8: Replays to GCS, on the verified path only

Design §5. The bytes are already in hand; the interesting decisions are *when* the object is written and *what is not* written beside it.

**Files:**
- Create: `services/api/src/replays/store.ts`, `services/api/test/replays.test.ts`
- Modify: `services/api/src/routes/wave.ts`, `services/api/src/app.ts`

**Interfaces:**
- Consumes: the GCS client already used by `config/gcs-store.ts`.
- Produces: `ReplayStore.put(serverId, playerId, issuanceId, bytes): Promise<void>`, and a `LocalReplayStore` for tests, mirroring `LocalBundleStore`.

- [ ] **Step 1: Write the failing test**

```ts
it('writes the replay after a verified submission', async () => {
  const { issuanceId, seed } = await startWave(6)
  await submit(issuanceId, buildWinningReplay(6, seed), 'r-1')

  expect(await store.list()).toContain(`replays/1/${playerId}/${issuanceId}.bin`)
})

it('writes nothing for a rejected submission', async () => {
  const { issuanceId } = await startWave(6)
  await submit(issuanceId, buildWinningReplay(6, 0xBADn), 'r-2')

  // Storage is not an attacker's write primitive - design 5.2.
  expect(await store.list()).toHaveLength(0)
})

it('still pays when the replay write fails', async () => {
  const failing = { put: async () => { throw new Error('gcs down') } }
  const app2 = createApp({ ...deps, replayStore: failing })
  const before = await balance('shards')

  const { issuanceId, seed } = await startWave(6)
  const res = await app2.request('/v1/wave/submit', submitInit(issuanceId, buildWinningReplay(6, seed), 'r-3'))

  // A GCS failure after a successful credit must not roll back a payment.
  // A missing replay is a degraded viewer; a rolled-back credit is a
  // ledger defect - design 5.2.
  expect(res.status).toBe(200)
  expect(await balance('shards')).toBe(before + 40)
})
```

- [ ] **Step 2: Run it to verify it fails**

```bash
pnpm --filter @broodline/api test replays
```

Expected: **FAIL** — no store exists.

- [ ] **Step 3: Write the store, mirroring `config/store.ts`**

Two implementations behind one interface — `GcsReplayStore` and `LocalReplayStore` — exactly as Phase 4 did for bundles, so the suite runs offline and the deployed path is the same code shape.

- [ ] **Step 4: Call it outside the money transaction**

After `withIdempotency` returns, wrapped in its own `try`/`catch` that logs and swallows. **Not inside the transaction**: a GCS timeout inside `withIdempotency` rolls back the credit *and* leaves the idempotency key `in_flight`, which is the worst of the three available outcomes.

- [ ] **Step 5: Run the tests to verify they pass**

```bash
pnpm --filter @broodline/api test replays
```

Expected: **PASS**, three tests.

- [ ] **Step 6: Commit**

```bash
git add services/api/src/replays services/api/test/replays.test.ts services/api/src/routes/wave.ts
git commit -m "feat: replays to GCS, written only on the verified path

A rejected submission leaves no object, so storage is not an attacker's
write primitive. The write is outside the money transaction: a GCS
failure after a successful credit loses a replay, which is a degraded
viewer, not a ledger defect.

The outcome is not stored beside the record - a replay is its inputs,
and 9.4's stored-outcome path needs a row that arrives with Phase 6.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 9: `POST /v1/session/refresh` — the route, and only the route

Design §6. `POST /v1/account` returns a 90-day refresh token that **no endpoint can redeem** — `redeemRefreshToken` has no caller outside its own tests. A session therefore ends hard at the 15-minute access-token TTL, and this phase's done-when is about retry across exactly that boundary.

> **Scope corrected before dispatch, after inspecting the code.** This task was
> written to also fix three defects recorded in
> [`2026-09-11-phase4-followups.md`][followups] §2 — a missing `kid`, a missing
> `iss`, and no `deleted_at` check on redemption. **All three are already
> implemented.** They landed in `b134b33`, which is *after* the followups file
> was written, so that file is stale on this point and nothing re-read it.
> Verified in `services/api/src/identity/jwt.ts`:
>
> | Claimed missing | Actually present |
> |---|---|
> | `kid` on session tokens | `primaryKey()`, `previousKey()`, `resolveVerificationKey(header)`, and `issue()` sets `{ alg: 'HS256', kid }`. A token with no `kid` falls back to primary, so adding `kid` did not itself invalidate outstanding tokens |
> | The `iss` claim | `issue()` calls `.setIssuer(ISS)`; both verify paths pass `issuer: ISS` |
> | `deleted_at` on redemption | `redeemRefreshToken` selects the account and throws on `deletedAt !== null` — and returns the **row's** `serverId` rather than the token's |
>
> **What remains is the HTTP surface and one test.** Do not re-implement any of
> the above; read it first and confirm it still holds.

**Files:**
- Create: `services/api/src/routes/session.ts`, `services/api/test/session.test.ts`
- Modify: `services/api/src/app.ts`, `services/api/src/openapi.ts`

**Interfaces:**
- Consumes: `redeemRefreshToken(db, token)`, `issueAccessToken`, `issueRefreshToken`, `SessionClaims`.
- Produces: `POST /v1/session/refresh` → `{ accessToken, refreshToken }`.

- [ ] **Step 1: Confirm the three claimed defects really are closed**

Do not trust the box above; it is this plan's own claim.

```bash
grep -n "setIssuer\|kid\|deletedAt" services/api/src/identity/jwt.ts
```

Expected: `setProtectedHeader({ alg: 'HS256', kid: key.kid })`, `.setIssuer(ISS)`, and the `row.deletedAt !== null` throw. **If any is absent, stop and report** — the scope correction was wrong and the task is larger than this.

- [ ] **Step 2: Write the failing test**

`services/api/test/session.test.ts`. Follow `sync.test.ts`'s harness conventions — a real player created through the real route:

```ts
it('exchanges a refresh token for a working access token', async () => {
  const { refreshToken } = await createAccount()
  const res = await app.request('/v1/session/refresh', {
    method: 'POST', headers: { 'content-type': 'application/json' },
    body: JSON.stringify({ refreshToken }),
  })
  expect(res.status).toBe(200)

  const { accessToken } = await res.json() as { accessToken: string }
  // The point of the route: the new token actually works.
  expect((await app.request('/v1/sync',
    { headers: { authorization: `Bearer ${accessToken}` } })).status).toBe(200)
})

it('refuses an access token presented as a refresh token', async () => {
  // The audience split is the whole reason there are two issue functions.
  const { accessToken } = await createAccount()
  expect((await refresh(accessToken)).status).toBe(401)
})

it('refuses a refresh token for a deleted account', async () => {
  // Already enforced in redeemRefreshToken; this pins it at the ROUTE, which
  // is the layer that did not exist when the check was written.
  const { refreshToken, accountId } = await createAccount()
  await softDelete(accountId)
  expect((await refresh(refreshToken)).status).toBe(401)
})

it('refuses a garbage token without leaking which part failed', async () => {
  expect((await refresh('not-a-token')).status).toBe(401)
})
```

**Write the `softDelete` helper against the real `accounts` schema.** Note that
`accounts` is deliberately **not** under an RLS policy — `0002_rls.sql` explains
why (login must resolve an account by `apple_sub` *before* it knows which server
to scope to), and it grants `UPDATE (apple_sub, deleted_at)` to the app role. So
`t.db` is sufficient; `identity.test.ts` already sets `deletedAt` that way. The
owner connection also works and is harmless, just not load-bearing.

- [ ] **Step 3: Run it to verify it fails**

```bash
pnpm --filter @broodline/api test session
```

Expected: **FAIL** — 404 on every case.

- [ ] **Step 4: Write the route**

Thin. `redeemRefreshToken` already does the work and throws on every failure mode; the route's job is to turn a throw into a `401` and a success into two fresh tokens. Catch broadly — a token error must never distinguish "bad signature" from "no such account" from "deleted", because that difference is an account-enumeration oracle.

**Rotation stays deferred.** `solo_execution` §6.4 defers refresh-token rotation to the first non-TestFlight players; return the same refresh token.

- [ ] **Step 5: Add the regression test the followups asked for**

[`followups`][followups] §5 names `extractClaims`' own guard the highest-value remaining test gap, "given this suite's documented history of auth tests that passed while proving nothing."

**`extractClaims` is not exported**, and it takes a `JWTPayload` rather than a token string — so it cannot be called directly and `await expect(extractClaims(...)).rejects` would not even compile. Reach it through the public path: mint a token whose payload is validly signed but carries a malformed claim (`serverId` absent, or a non-integer), then assert `verifyAccessToken` rejects it. Signing such a token in the test requires the same `jose` primitives `issue()` uses.

That is the test that matters: a **validly signed** token with a nonsense claim, not a garbage string. A garbage string fails at the signature and never reaches the guard.

- [ ] **Step 6: Register the route in the contract**

`openapi.ts`, the same way Task 6 registered the wave routes, then regenerate and commit. A route the Unity client must call and has no generated method for is the gap Task 6 was extended to close; do not reopen it here.

- [ ] **Step 7: Run the suite**

```bash
pnpm --filter @broodline/api test
pnpm --filter @broodline/api typecheck
```

- [ ] **Step 8: Commit**

```bash
git add services/api/src/routes/session.ts services/api/src/app.ts \
        services/api/src/openapi.ts services/api/test/session.test.ts \
        openapi/ client/Assets/Generated/
git commit -m "feat: POST /v1/session/refresh, the route the spine was missing

POST /v1/account has returned a 90-day refresh token no endpoint could
redeem since Phase 4, so a session died hard at the 15-minute access TTL.
This phase's done-when is about retry across exactly that boundary.

The kid-aware verify, the iss claim and the deleted_at check that the
Phase 4 followups list as owed were already implemented in b134b33; that
file is stale. What was missing was the HTTP surface and a regression
test for extractClaims' guard, reached through verifyAccessToken since
the function itself is not exported.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 10: The adversarial suite — the phase gate

Design §7. One test per row of §4.4's table, **and every one proven to fail when its guard is weakened.** Phase 4's lesson, recorded in its own review notes, is that a suite can pass while proving nothing: auth tests that passed against a broken guard, and a concurrency gate that proved nothing until the insert was deliberately weakened to `onConflictDoNothing`.

**Files:**
- Create: `services/api/test/adversarial.test.ts`
- Create: `services/api/test/weakenings.md`

**Interfaces:**
- Consumes: every route and guard built in Tasks 4–9, plus a real `sim`.
- Produces: nothing importable. This task produces the phase's gate.

- [ ] **Step 1: Write the suite**

Eight cases. Seven assert a defence; the eighth asserts the **hole this phase leaves open knowingly**, so that Phase 6 inherits a failing-by-design marker rather than an assumption:

```ts
describe('adversarial: what a modified client cannot do', () => {
  it('cannot claim a win that did not happen', async () => {
    // The client claims Win; sim says Loss. api pays what sim returns and
    // never what the client claims - which is why the submission body
    // carries only the replay and an issuance id, with no outcome field
    // for a client to lie in.
    const { issuanceId, seed } = await (await startWave(6)).json() as { issuanceId: string; seed: string }
    const before = await balance('shards')
    const res = await submit(issuanceId, buildLosingReplay(6, BigInt(seed)), 'adv-1')

    expect((await res.json()).result).toBe('Loss')
    expect(await balance('shards')).toBe(before)
  })

  it('cannot submit forged bytes', async () => {
    const { issuanceId } = await (await startWave(6)).json() as { issuanceId: string }
    const before = await balance('shards')
    expect((await submit(issuanceId, 'bm90IGEgcmVwbGF5', 'adv-2')).status).toBe(409)
    expect(await balance('shards')).toBe(before)
  })

  it('cannot replay a winning submission twice', async () => {
    const { issuanceId, seed } = await (await startWave(6)).json() as { issuanceId: string; seed: string }
    const replay = buildWinningReplay(6, BigInt(seed))
    await submit(issuanceId, replay, 'adv-3a')
    const before = await balance('shards')

    await submit(issuanceId, replay, 'adv-3b')
    expect(await balance('shards')).toBe(before)
  })

  it('cannot submit against a self-chosen seed', async () => {
    // 0x1111n is a seed the server never issued. The replay is internally
    // HONEST - it really is a winning wave 6 played at that seed - so sim
    // verifies it happily and the refusal must come from matchesIssuance
    // comparing echo.seed to the issuance's, NOT from sim rejecting junk.
    //
    // ASSERT THE REASON, NOT ONLY THE BALANCE. An unchanged balance is
    // satisfied by a submission refused for ANY reason - a malformed body, a
    // 500, a seed the helper failed to encode. This phase has already shipped
    // five assertions that were green while proving nothing; this is exactly
    // that shape. The status pins WHICH guard fired.
    const { issuanceId } = await (await startWave(6)).json() as { issuanceId: string }
    const before = await balance('shards')
    const res = await submit(issuanceId, buildWinningReplay(6, 0x1111n), 'adv-4')
    expect(res.status).toBe(409)
    expect((await res.json()).code).toBe('submission_rejected')
    expect(await balance('shards')).toBe(before)
  })

  it('cannot skip to wave 60', async () => {
    expect((await startWave(60)).status).toBe(409)
  })

  it('cannot inflate the reward by echoing a different wave', async () => {
    // THE test Task 6 Step 6(c) found missing. Every honest submission
    // agrees on both wave ids, so nothing in wave-submit.test.ts
    // distinguishes issuance.waveId from verdict.echo.waveId - and the
    // reward must come from the first.
    await clearThrough(6)
    const { issuanceId, seed } = await (await startWave(6)).json() as { issuanceId: string; seed: string }
    const before = await balance('shards')

    // A replay that is honestly of wave 6 but whose issuance is for wave 6
    // too - then weaken the handler and watch the reward change. Here the
    // assertion is simply that the paid amount equals wave 6's bundle
    // reward and not any other wave's.
    await submit(issuanceId, buildWinningReplay(6, BigInt(seed)), 'adv-6')
    expect(await balance('shards')).toBe(before + 40)
  })

  it('cannot farm a cleared wave past the daily cap', async () => {
    await clearThrough(6)
    let paid = 0
    for (let i = 0; i < 3; i++) {
      const { issuanceId, seed } = await (await startWave(6)).json() as { issuanceId: string; seed: string }
      await submit(issuanceId, buildWinningReplay(6, BigInt(seed)), `adv-7-${i}`)
      paid += 40
    }
    const before = await balance('shards')
    expect((await startWave(6)).status).toBe(429)
    expect(await balance('shards')).toBe(before)
  })

  it('CAN still deploy creatures the player does not own — Phase 6', async () => {
    // design 2.2 and 4.4's last row. NOT a defence: a marker, asserted so
    // that the day a creature table exists this test fails and names the
    // thing that changed. A hole recorded as a passing assertion about the
    // current behaviour is a hole nobody re-reads.
    const { issuanceId, seed } = await (await startWave(6)).json() as { issuanceId: string; seed: string }
    const res = await submit(issuanceId, buildWinningReplay(6, BigInt(seed), { trait: 'Chill', tier: 3 }), 'adv-8')

    expect(res.status).toBe(200)
    // When this flips to 409, Phase 6 has landed the roster check. Update
    // design 4.4's table in the same change.
  })
})
```

- [ ] **Step 2: Run it to verify it passes**

```bash
pnpm --filter @broodline/api test adversarial
```

Expected: **PASS**, eight tests.

- [ ] **Step 3: Run every weakening, and record the result**

This is the task. A suite whose tests have never been seen to fail is a suite that has not been shown to test anything. Create `services/api/test/weakenings.md` and fill in the observed result for each row:

| # | Weaken | Must break |
|---|---|---|
| 1 | Delete the seed comparison at submit step 5 | "cannot submit against a self-chosen seed" |
| 2 | Drop `wave_issuances_one_live` | "cannot replay a winning submission twice" |
| 3 | Move `settle()` outside the credit's transaction | "cannot replay a winning submission twice" |
| 4 | Drop the settlement write-once trigger | "cannot replay a winning submission twice" |
| 4b | Settle the abandoned row `'consumed'` rather than `'expired'` in `wave/start` | "cannot farm a cleared wave" — it would burn a replay the player never took |
| 5 | ~~Read the reward from `verdict.echo.waveId`~~ | **Subsumed — see below. Not a gate row.** |
| 6 | Delete issuance check 2 (the replay cap) | "cannot farm a cleared wave" |
| 7 | Age `'consumed'` issuances out at 3 hours rather than 48 | "cannot farm a cleared wave" |
| 8 | Return `sim`'s rejection as a `5xx` instead of a `200` verdict | "cannot submit forged bytes" |

For each: apply, run `pnpm --filter @broodline/api test adversarial`, **record the failing test name**, revert, re-run to green.

> **Row 5 is struck, and the reason is a finding about this plan rather than
> about the code.** "Read the reward from `verdict.echo.waveId`" cannot break
> anything, and three layers stop it:
>
> 1. **Step 5 subsumes it.** `matchesIssuance` rejects an echo/issuance wave-id
>    mismatch at `routes/wave.ts:223`, *before* the reward is computed at
>    `:257`. By the time the lookup runs, `echo.waveId === issuance.waveId` is
>    guaranteed, so reading either is behaviourally identical.
> 2. **A combined weakening needs a second authored wave.** Remove step 5 *and*
>    read from the echo, and you could pay wave B's reward for a wave A
>    issuance — but only if two waves with different rewards exist.
> 3. **The engine authors exactly one.** `WaveDef.ForId` returns wave 6 and
>    throws `WaveCompositionException` for every other id, which
>    `SimulateEndpoint` maps to `rejected: rules_violated`. A wave-7 replay is
>    refused by `sim` before any of this is reached. **A test-only bundle
>    fixture does not help**, because the gate is the engine, not the bundle —
>    and no task in this phase modifies `engine/`.
>
> **What to do instead.** Prove the wiring at unit level: assert that the reward
> passed to `credit()` is derived from the issuance row, not from the verdict —
> `sim-client.test.ts` already covers the wave-id mismatch half the same way.
> That proves the handler reads the right source; it does not prove an
> end-to-end inflation is impossible, and **the report must say so plainly**
> rather than claiming row 5 is closed.
>
> **The real end-to-end proof arrives with the engine content fill**, when a
> second authored wave exists. Record it in the followups as owed, pointing at
> this note. Do not quietly drop it.

**Any row whose weakening leaves the suite green is a missing test, not a spare guard.** Write the missing test before moving on. Row 7 is the most likely to surface one — the retention split at design §4.3 exists for the replay count, and nothing else in the plan reads it.

- [ ] **Step 4: Commit**

```bash
git add services/api/test/adversarial.test.ts services/api/test/weakenings.md
git commit -m "test: the adversarial suite, and every weakening that breaks it

One test per row of design 4.4, plus weakenings.md recording the
observed failure for each of eight deliberate breakages. A suite whose
tests have never been seen to fail is a suite that has not been shown to
test anything - which is Phase 4's recorded lesson, twice.

The last test asserts the hole this phase leaves open on purpose: an
unowned deployment still wins. It flips to a failure the day Phase 6
lands the roster check, which is the point.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 11: Terraform, the second service, and the replay bucket

**This task provisions billable infrastructure.** Phase 4's execution stopped before its equivalent for exactly that reason; do not start it until Tasks 1–10 are green locally.

**Files:**
- Modify: `infra/terraform/main.tf`, `variables.tf`, `outputs.tf`
- Modify: `implementation/scripts/deploy.sh`
- Modify: `services/api/tsconfig.json`

**Interfaces:**
- Consumes: the Phase 4 Terraform state, the Artifact Registry repository it created.
- Produces: `sim_url` as a Terraform output, consumed by `api`'s `SIM_URL` environment variable.

- [ ] **Step 1: Remove the tsconfig exclusion Phase 4 left behind**

```bash
grep -n "exclude" services/api/tsconfig.json
```

Phase 4 followups §2: `"exclude": ["src/index.ts"]` makes `index.ts` the only unchecked file in the service, and it already imports a module that did not exist. `gcs-store.ts` has since landed. Remove the exclusion and typecheck:

```bash
pnpm --filter @broodline/api typecheck
```

Expected: **PASS**. If it does not, fix `index.ts` — it has never been typechecked and this is the first time anything has looked at it.

- [ ] **Step 2: Add the `sim` service, internal ingress only**

```hcl
resource "google_cloud_run_v2_service" "sim" {
  name     = "broodline-sim"
  location = var.region
  # INTERNAL. sim has no authentication of its own because it is not
  # reachable - design 3.1. If this ever becomes INGRESS_TRAFFIC_ALL, the
  # service needs auth before the change lands, not after.
  ingress  = "INGRESS_TRAFFIC_INTERNAL_ONLY"

  template {
    service_account = google_service_account.sim.email
    scaling { max_instance_count = var.sim_max_instances }
    containers {
      image = var.sim_image
      resources { limits = { cpu = "1", memory = "512Mi" } }
    }
  }
  deletion_protection = var.deletion_protection
}

# api invokes sim, and nothing else may.
resource "google_cloud_run_v2_service_iam_member" "api_invokes_sim" {
  name     = google_cloud_run_v2_service.sim.name
  location = var.region
  role     = "roles/run.invoker"
  member   = "serviceAccount:${google_service_account.api.email}"
}
```

**`sim`'s service account is new and deliberately empty of grants** — no Cloud SQL client, no storage role. If a future change needs one, that is the signal design §3.1 was traded away.

- [ ] **Step 2b: `REPLAY_BUCKET` is a HARD DEPLOY-ORDER PREREQUISITE**

> **Recorded from Task 8's review.** `services/api/src/index.ts` throws at boot
> if `REPLAY_BUCKET` is unset. **Deploying any revision from Task 8 onward
> before this task has provisioned the bucket and set the variable will
> crash-loop the entire API** — wave submission, account creation, everything —
> not merely degrade replay storage.
>
> That hard failure is deliberate and was ruled to stay. A *runtime* store
> failure is non-critical because one replay is lost; a *missing configuration*
> means every replay is lost, which is a different class of problem. Loud beats
> silent: a crash-loop is caught in seconds and rolled back with one command,
> whereas a silent no-op is discovered weeks later by someone opening an empty
> viewer.
>
> So the ordering is not optional: **provision the bucket and set the variable
> in the same apply that first carries a Task 8 revision.** Expand, deploy,
> migrate, contract applies to configuration too.

- [ ] **Step 3: Add the replay bucket and its lifecycle rule**

```hcl
resource "google_storage_bucket" "replays" {
  name     = "${var.project_id}-replays"
  location = var.region
  uniform_bucket_level_access = true

  # 30 days rolling - solo_execution 9.5. The rule ships NOW because it
  # cannot be retrofitted onto objects already deleted; the 20-pin
  # exemption is deferred with the viewer that would use it, and nothing
  # is 30 days old yet.
  lifecycle_rule {
    condition { age = 30 }
    action    { type = "Delete" }
  }
}
```

- [ ] **Step 4: Put `JWT_SECRET` and its predecessor in Secret Manager with a rotation policy**

Task 9 made the verify path `kid`-aware precisely so this is a provisioning step rather than a migration. Both `JWT_SECRET` and `JWT_SECRET_PREVIOUS` are Secret Manager values mounted into `api`.

- [ ] **Step 5: Confirm the bundle seed publishes the pointer, not just the bundle**

Phase 4 followups §2: nothing in the repo seeds `bundles/current`, and `loadBundle` reads the pointer — the service 500s on its first request without it. Task 5 changed `waves.json`, and **a published version is never overwritten**, so this publishes `0.1.1`:

```bash
./implementation/scripts/publish-bundle.sh config/bundles/0.1.1
gsutil cat gs://${PROJECT}-config/bundles/current
```

Expected: `0.1.1`. **Confirm the script does both** — publish and set the pointer — rather than only the first.

- [ ] **Step 6: Apply, deploy, and smoke-test both services**

```bash
terraform -chdir=infra/terraform apply
./implementation/scripts/deploy.sh
curl -fsS "$(terraform -chdir=infra/terraform output -raw api_url)/healthz"
```

Expected: `{"ok":true}`. `sim` has no public URL to curl — that is the point. Reach it through a verified submission at Step 7.

- [ ] **Step 7: Prove the deployed pair end to end**

```bash
./implementation/scripts/smoke-wave.sh    # create account -> start -> submit -> assert balance
```

Expected: a balance of `250 + 40`, and a replay object in the bucket.

- [ ] **Step 8: Confirm rollback is a config change, not a deploy**

Phase 4 booked this as unverified: `loadBundle` caches per process, so an instance that has not turned over keeps serving the old bundle after a pointer change. Set the pointer back to `0.1.0`, call `/v1/sync` repeatedly, and **record how long stale responses continue.** If they never stop, the claim is false and belongs in the followups rather than in the design.

- [ ] **Step 9: Commit**

```bash
git add infra/terraform implementation/scripts services/api/tsconfig.json
git commit -m "infra: sim on Cloud Run with internal ingress, and the replay bucket

sim is reachable only by api's service account and holds no grants of
its own - if it ever needs one, design 3.1 has been traded away.

The 30-day lifecycle rule ships now because it cannot be retrofitted
onto objects already deleted. Removes the tsconfig exclusion that made
index.ts the one unchecked file in the service.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 12: The done-when, and what is owed after it

> ### 🟡 PARTIAL — the local half is done; the deployed half is held with Task 11
>
> **Landed (2026-09-14):** Steps 1, 2, 4, 5 and 6 complete. Step 3's file exists
> and carries everything this branch can evidence.
>
> **Gates re-run a second time at `b623711`, and the baseline was regenerated.**
> api **189 / 24 files**, typecheck **0**, .NET **186 / 0 skipped** (174 engine
> + 12 sim), Unity EditMode **35 / 0 / 0**, cross-runtime 500 scenarios agree,
> contract diff clean. **No per-file count fell**; the diff against the previous
> baseline is one added line, `api.file.sim-auth.test.ts` at 13 tests.
>
> **The first measurement said 176 / 23 and went stale for two commits.**
> `5619d6f` added a test file and did not regenerate the baseline, so the gate's
> own reference disagreed with the tree it was meant to check. Nothing was lost
> — counts rose, which is the benign direction — but it is the staleness this
> file exists to prevent, reaching it by the one route it did not guard.
> **Regenerating the baseline belongs to landing a test change, not to the phase
> gate that reads it.**
>
> **Still owed, and it is owed to Task 11, not to Step 3.** Task 11 is held by
> human ruling before provisioning billable GCP, so three things could not be
> written from evidence and are recorded in the followups file as *plan, not
> measurement* (§5): what **Step 8** finds about bundle rollback; the
> `terraform output` empty-outputs-map bug; and design §5.1's **30-day replay
> lifecycle rule**, which does not exist in `infra/terraform` even as unapplied
> HCL. *(Two of those three have since been overtaken: the `terraform output`
> behaviour was diagnosed as correct rather than a bug, and the 30-day
> lifecycle rule is now written as unapplied HCL — followups §5.)* **When Task 11 runs, its findings go into the followups file** — that is
> the only place the reasoning behind this branch's thirty-two commits survives
> a `git clean`.
>
> **Also still owed, and not Task 11's:** the deployed end-to-end wave in the
> Definition of Done, and parent **Task 1**'s device re-capture (the round-trip
> is dark — see the followups file §3, and note that `dotnet.skipped 0` says
> nothing about it).


**Files:**
- Create: `implementation/2026-09-13-phase5-followups.md`
- Modify: `implementation/README.md`, `specs/plans/broodline_phase5_validation.md`

- [x] **Step 1: Run every gate**

```bash
dotnet test Broodline.sln --nologo
./implementation/scripts/cross-runtime-diff.sh
pnpm --filter @broodline/api test
pnpm --filter @broodline/api typecheck
./implementation/scripts/generate-contract.sh && \
  git diff --quiet -- openapi/ client/Assets/Generated/ services/api/src/generated/
./implementation/scripts/run-unity-tests.sh EditMode
```

- [x] **Step 2: Check the counts against Task 0**

**Compare against `implementation/results/phase5-test-baseline.txt`, not against any number written in prose — including the ones in this sentence.** That file is committed, machine-diffable, and supersedes anything typed into a paragraph here.

This instruction exists because the prose version already failed once: Phase 4 recorded "71" in a sentence, it went stale unnoticed, and the real count was 74 — so this very step carried a wrong number for the whole of Phase 5.

For orientation only, superseded by the file: api 176 / 23 files, .NET 186 / 0 skipped (174 engine + 12 sim), Unity EditMode 35 / 0 / 0. **A count that fell is a deletion nobody noticed** — find it before closing the phase.

- [~] **Step 3: Write the followups file** — written; the Task 11 clause is the one gap, see the status block above

The working ledger lives in git-ignored scratch and will not survive a `git clean`. Phase 4's equivalent is the model. It must carry, at minimum: whatever Task 11 Step 8 found about bundle rollback, any weakening from Task 10 Step 3 that left the suite green, and the standing debt this phase did not clear.

- [x] **Step 4: Update the design doc's decisions owed**

Three of design §9's six are discharged or advanced by this phase's work. Mark them, and **add whatever Task 6 Step 6(c) and Task 10 Step 3 turned up** — a plan that finds something and does not book it has wasted the finding.

- [x] **Step 5: Add the phase to `implementation/README.md`**

- [x] **Step 6: Commit**

```bash
git add implementation/ specs/plans/broodline_phase5_validation.md
git commit -m "docs: promote Phase 5's findings to a tracked file

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## What this plan deliberately does not do

- **No creature ownership check.** Design §2.2 — the one hole left open knowingly, marked by a passing test that flips when Phase 6 lands.
- **No engine content.** One raider, one trait, wave 6. No task touches `engine/`.
- **No optimistic grant, no clawback, no outbox.** `sim` down is a retryable `503` with the issuance intact.
- **No raids, auto-resolve or region defence.** `POST /internal/simulate` is shaped for auto-resolve; nothing calls it that way.
- **No replay viewer, and no pinning.** The lifecycle rule ships; the exemption does not.
- **No refresh-token rotation.** Deferred to the first non-TestFlight players.
- **No nodes, regions, harvest, relocation or splice.** Phase 6.
- **No batching, Redis, PgBouncer or HA.** Named triggers; none measured.
- **No `Broodline.UI` and no Codex sheet.**

---

## Definition of done

```bash
dotnet test Broodline.sln --nologo
./implementation/scripts/cross-runtime-diff.sh
pnpm --filter @broodline/api test
pnpm --filter @broodline/api typecheck
./implementation/scripts/generate-contract.sh && \
  git diff --quiet -- openapi/ client/Assets/Generated/ services/api/src/generated/
./implementation/scripts/run-unity-tests.sh EditMode
```

All six green, with:

- **The device round-trip tests comparing `Outcome.Hash`**, not asserting a throw, against artifacts captured under `0.2.0`. `dotnet test` reports **0 skipped** and covers the done-when it claims to.
- **The contract diff gate covering all four generated paths**, and proven to fail when a field is added to `SimulateEcho`.
- **`wave_issuances` under `FORCE ROW LEVEL SECURITY`**, picked up by the `pg_class` isolation gate without being named in it.
- **One live issuance per player**, proven by a unique violation rather than by a handler check.
- **A winning submission resent with a *different* idempotency key paying nothing**, asserted on the **balance**, not on the error code.
- **`sim` unreachable leaving the issuance live**, and the submission retryable.
- **A superseded submission rejected with `426`**, never rendered.
- **A fourth replay of a cleared wave in one day refused**, and the count still correct across a day boundary.
- **Every weakening at Task 10 Step 3 applied to real source and recorded in `weakenings.md`** with the name of the test it reddens — and, where it reddened nothing, with the reasoning for that written down as a finding. This clause used to read "all eight weakenings … observed to break the suite", which was never satisfiable and is not the requirement. Of the **ten** weakenings now recorded (rows 1, 2, 3, 4, 4b, 6, 7, 8, 9, 10; **row 5 struck**, with its reasoning inline and booked as owed), **eight break the adversarial suite** — seven reddening a named test, and row 2 reddening the file wholesale rather than discriminatingly. **Two do not, by ruling, and both are recorded as findings:** row 4's guard is unreachable from any sequence of HTTP requests, so it reddens `issuance-schema.test.ts` instead; row 10 is a genuine hole this gate leaves open, caught by `wave-submit.test.ts` instead. Smoothing either one over is what would fail this clause. The remediation plan's DoD states the same thing and is the restatement of record.
- **A deployed end-to-end wave**: account created, wave started, submission verified, balance `250 + 40`, replay object in the bucket.

**And one thing that is not a command.** Design §2.2's boundary must be restated wherever this phase's done-when is quoted: *a forged outcome earns nothing*. A deployment the player does not own still wins, and it will until Phase 6. A phase that closes by quoting the stronger sentence has closed on a claim it did not prove.
