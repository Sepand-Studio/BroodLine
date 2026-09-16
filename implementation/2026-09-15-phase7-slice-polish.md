# Phase 7 — Slice Polish Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A TestFlight build, on the first deployed stack this project has ever had, in the hands of someone who is not the developer, who can play the first hour — cold open, wave 1, a creature they name, wave 2, a guided splice that mutates, the Lineage View — without being told what to tap. And Phase 6 closed on the way in, because the device capture that blocks it is Task 2.

**Architecture:** The phase opens in the engine, for the reason Phase 6's did — everything above depends on it — and closes the engine's part *before* the device capture is taken, because `specs/plans/broodline_phase7_slice_polish.md` §2.3 makes that ordering the rule: an engine change after the capture supersedes it, which is exactly how Phase 6 darkened its own proof. Task 1 makes the **wave own its lane** (wave 1's six pockets are content, and `Lane.Defile()`'s five were wave 6's), authors waves 1 and 2, and moves `SimVersion` to `0.4.0`. Task 2 re-captures. Everything after that is content, `api` and client: bundle `0.1.3`, one expand-only migration, four new routes, the FTUE's server-side rulings (a Hollow Founder, provided splice stock, a guaranteed first mutation, the wave-6 Pale), the navigation shell and the first-hour screens in UI Toolkit, the outbox `client_architecture` §8 specifies, the minimum billable stack, and the runner that finally executes the determinism gate.

**Tech Stack:** Unchanged from Phase 6 plus UI Toolkit. .NET 10 / ASP.NET for `sim`, `netstandard2.1` engine. Node 22 LTS, pnpm 9.12.0 workspaces, Hono, Drizzle, `pg`, Zod + `@asteasolutions/zod-to-openapi`, Vitest, Testcontainers, Postgres 16. Unity 6000.6.0f1 / IL2CPP; **UI Toolkit (UXML/USS) for every screen** — `com.unity.modules.uielements` is already in `client/Packages/manifest.json`. Terraform ≥ 1.11, google provider 6.x. **New billable infrastructure**, by ruling: one `db-f1-micro` Cloud SQL instance and two Cloud Run services at `min_instance_count = 0`, all of which `infra/terraform/main.tf` already declares and none of which has ever been applied.

## Global Constraints

**The engine constraints from Phases 1–3 bind `engine/`** — no floating point, no `System.Math`, no `System.Linq`, no `Dictionary`, no `HashSet`, no `System.IO`, zero project references, the Cecil float scan, `BannedSymbols.txt`. `EnforcementTests` scans for them.

**Task 1 is the only task that may modify `engine/`, and Task 2's capture follows it.** If a later task appears to need an engine change, stop: the design's §2.3 rule means that change re-supersedes Task 2's capture, and the capture is retaken *after* it, not ignored. There is hardware this phase, so a retake is cheap; a stale capture is not.

From `specs/plans/broodline_phase7_slice_polish.md`, and normative here:

- **Placeholder art throughout.** The rig proof runs as a parallel track and is not this plan's (design §1).
- **Internal TestFlight only.** No Beta App Review, so no Age Gate, Report/Block, Settings support contact or Founder Naming filter path (design §1, §5.2, §12). Founder names are validated for shape only.
- **The wave owns its lane geometry.** `Lane.ForFamily` goes; `WaveDef.Lane` arrives; a replay rebuilds the lane from the wave id it already stores (design §6.2).
- **Wave 6 and wave 7 keep `{6, 10, 13, 17, 20}`.** Their corpus hashes must survive Task 1 byte-identical, and that is proven by diff, not assumed.
- **The beats' content is server-authoritative and derivable.** Nothing about FTUE progress is stored on the client; the client derives the beat from `/v1/sync` (design §4, `client_architecture` §9).
- **The splice's forecast and its roll stay one function.** The guaranteed first mutation is a parameter to `spliceDistribution`, read by *both* callers from the same row count, so `preview` publishes 100% and `commit` honours it (Phase 6 design §5.1, unchanged).
- **`api` never parses a replay.** The Wave Defeat screen's diagnosis comes from `sim`'s echo through `WaveSubmitResponse.breaches`, and the client's own local outcome; `api` interprets neither.
- **Outbox keys are generated when the action is taken**, not when it is sent (`client_architecture` §8).
- **The `Broodline.UI` assembly does not reference `Broodline.Sim`.** Phase 6's rule, kept: the shell and anything that reads an engine `Outcome` lives in `Broodline.Game`, and hands `Broodline.UI` plain data.
- **A schema migration and the code requiring it never deploy together.** Migration `0008` is expand-only and ships with no reader in the same deploy.
- **Every currency mutation writes a ledger row in the same transaction.** Every mutating request carries an `Idempotency-Key`.
- **Generated clients are committed and never hand-edited.** Every task that adds or changes a route ends by running `generate-contract.sh` and committing the four outputs; `contract.test.ts` reddens otherwise.
- **`server_id` leads every key; RLS stays on, `FORCE ROW LEVEL SECURITY`, transaction-scoped `set_config`.** No relaxation because the stack is small.

### Toolchain — read before running any `pnpm` command

Carried verbatim from Phase 6, because it cost time twice: **the default `pnpm` on `PATH` is 3.7.5 under node v10** and fails in ways that read as broken tests (`test` exits 9 into usage text; `typecheck` prints nothing). In every shell:

```bash
export PATH="$HOME/.nvm/versions/node/v22.22.2/bin:$PATH"   # node 22.22.2, pnpm 9.12.0
```

**Also:** `implementation/results/*` is gitignored except `*.csv` and `*-test-baseline.txt`; `git add` on any other results file silently stages nothing. Use `git add -f` for the four capture files, as every prior phase did.

**Also:** `run-unity-tests.sh` and `cross-runtime-diff.sh` refuse to run while the Editor has the project open. Close it first.

### Values, copied verbatim

Values marked **provisional** are rulings taken here against a stated property, owed to a content document for ratification. They are listed so nobody re-derives them in a task.

| | |
|---|---|
| Engine version **after** Task 1 | `SimVersion.Value` = `"0.4.0"` (from `"0.3.0"`) |
| Wave 6 / wave 7 lane | `Lane.Defile()` — 24 tiles, pockets beside `{6, 10, 13, 17, 20}`. **Unchanged** |
| Wave 1 / wave 2 lane — **provisional** | `Lane.DefileSix()` — 24 tiles, pockets beside **`{6, 9, 12, 14, 17, 20}`**. `combat_numbers` §2 puts pockets beside tiles 6–20 and gives the family 4–6; `waves_01_12` wave 1 says six. Owed to `region_roster` §3 |
| Wave 1 — `broodline_waves_01_12.md` | integrity **2**, lanes **1**, **6 Skirmishers** at ticks **90, 150, 210, 270, 330, 390** (one every 2.0 s from t=3). Reward **150 shards** |
| Wave 2 | integrity **2**, lanes **1**, **8 Skirmishers** at ticks **90, 135, 180, 225, 270, 315, 360, 405** and **1 Lash at tick 360** (t=12), authored *after* the tick-360 Skirmisher so the tie-break keeps the Skirmishers first. Reward **165 shards** |
| *(Waves 1 and 2 also author 2 tier-I samples each)* | **Dropped**, as Phase 6 dropped wave 7's — no sample economy |
| Cold-open pair — **provisional** | `starter.json` `creatures`: **Vetch** (Taunt I / Carapace I) and **Ember** (Splash I / Carapace I), Gen-1, Vanguard, **not Founders** |
| The first creature drop — **provisional** | The player's **first** wave completion grants a **Hollow** Founder (`is_founder = true`, unnamed, traits `None`/`None`, Vanguard) instead of the rolled base stock. `waves_01_12` wave 2 expects "Vetch, Ember, Hollow"; `campaign_structure` §1 names Hollow a Founder; bible §3.3 names Founder 1 the first session's named creature |
| Tutorial splice stock — **provisional** | `POST /v1/ftue/splice-stock` grants **Vetch** + **Ember**, Gen-1, Vanguard, not Founders, once, after wave 2 is cleared and before the player's first splice |
| First splice mutation | `mutation = 1` when the player has **zero** rows in `splices`; `MUTATION_RATE` (0.09) otherwise. Both callers |
| Wave-6 Pale | On the **first settlement** of a wave-6 issuance, win or loss: one **Pale** (Chill I / Carapace I), not a Founder. Until then the base-stock pool is **Vetch, Ember** — Pale is withheld (`campaign_structure` §1, `whats_left` §2) |
| Founder name | 1–16 characters after trim, printable, no control characters. Founders only (`only_founders_named`). No filter this phase |
| Tab reveal thresholds — **provisional** | `progression.json`: Map **0**, Ark **0**, Splice **2** (after wave 2), Lab **61**, Allies **61** — 61 is past the campaign, i.e. never this phase |
| Config bundle | **`0.1.3`** — waves 1, 2, 6, 7; `starter.json` creatures; `progression.json`; `minimumClientVersion` **`0.3.0`** |
| Client version | `ProjectSettings.asset` `bundleVersion: 0.3.0`, `buildNumber` iOS **1** |
| Issuance retention — Phase 5 design §4.3 | `expired` or live-past-expiry: delete **one hour past `expires_at`** (three hours after issuance). `consumed`: delete **48 hours after `issued_at`** — it is the replay counter |
| Outbox expiry | **24 hours** — the server's idempotency window |
| Cloud SQL | `db-f1-micro`, `ENTERPRISE`, `deletion_protection = true`, private IP; `db_public_ip = true` **only** during the migration/seed step, with `db_authorized_networks` scoped to one `/32`, then reverted |
| Cloud Run | `api` and `sim`, `min_instance_count = 0`, `max_instance_count = 10` (already in `main.tf`) |
| Internal tester account | `POST /v1/account` with `birthdateBand: "adult"`, `storefrontRegion: "us-central1"` — the Age Gate is deferred, so the client sends the adult band for internal testers and says so in a comment |

## The three things this plan cannot do for you

**1. Task 2 needs a human holding an iPhone, tapping between ticks 184 and 207.** The Editor half can be scripted; the device half cannot. The hardware is available this phase, which is why the capture is a task and not a stop condition — but the tap is a person's.

**2. Task 12 needs a GitHub token with permission to register a runner on `Sepand-Studio/BroodLine`.** The runner is this Mac. `determinism.yml`'s own security note applies: this is a public repository, and a self-hosted runner executes fork pull-request code on the host. Restrict fork workflows in the repository's Actions settings before uncommenting the triggers.

**3. Task 19 spends money.** `terraform apply` creates a Cloud SQL instance that bills continuously. It needs `TF_VAR_db_password` in the environment, `PROJECT_ID=broodline-508416`, and a human who has read `infra/terraform/variables.tf`'s `deletion_protection` note and decided the instance stays up. The ruling in the design is "minimum billable, inside Phase 7"; the bill is still yours.

## What this plan found the design missed

Written here so the design is amended by a task rather than silently outrun — the defect class both prior records spent their review budget on.

1. **Wave 6's Pale grant is not implemented and the first hour cannot recover without it.** `waves_01_12` wave 6: *"The Wave Defeat screen grants a Pale, framed as a Warden resupply … If they somehow win, the Pale grant fires anyway."* Nothing in `services/api/src/wave/` grants it. A first-hour roster with no Chill loses wave 6 by design and then has no path to a Pale except a lucky base-stock roll. Task 8 grants it.
2. **Base stock can already mint a Pale, which pre-empts the designed loss.** `roster/creatures.ts`'s `BASE_STOCK_SPECIES` is Vetch, Pale, Ember. `campaign_structure` §1: *"withhold Pale, and grant the Pale from the Wave Defeat screen on the wave-6 Courser loss."* Task 8 withholds it until the wave-6 grant has fired.
3. **No `servers` row exists anywhere but in tests.** Every test inserts `{ serverId: 1, region: 'us-central1', state: 'open', tickDayOfWeek: 0, tickMinuteOfDay: 1200 }` by hand; no migration or script seeds one. A deployed database with no row refuses every `POST /v1/account`. Task 19 seeds it.
4. **The design says the first hour "joins the supply line", and the supply line has a gap between wave 2 and the guided splice.** A player who has cleared wave 2 holds four creatures — Vetch, Ember, the Hollow Founder, and one base-stock drop — and the guided splice must use *provided* stock, never the named one. Nothing provides it. Task 7's route does.
5. **The refresh token has nowhere safe to live.** `client_architecture` §7 says iOS Keychain; Unity has no Keychain API without a native plugin. Task 13 stores it under `Application.persistentDataPath` with the file marked `NSFileProtectionComplete` via `UnityEngine.iOS.Device.SetNoBackupFlag` and records the Keychain plugin as owed. Internal testers only.

Task 22 promotes all five into the design's §13.

---

## File structure

### New — engine and content

| File | Responsibility |
|---|---|
| `engine/Runtime/Combat/Lane.cs` | `DefileSix()`; `ForFamily` removed |
| `engine/Runtime/Combat/WaveDef.cs` | `Lane` property; `Wave1()`, `Wave2()`; `ForId` returns four waves |
| `tests/engine/Combat/WaveContentTests.cs` | Waves 1 and 2 are winnable with the rosters the FTUE hands out. **The beats' content assertions** |
| `config/bundles/0.1.3/` | waves, traits, nodes, packs, locales, manifest, `starter.json` with creatures, **`progression.json`** |

### New — `api`

| File | Responsibility |
|---|---|
| `services/api/drizzle/0008_ftue_markers.sql` | Three nullable `timestamptz` columns on `campaign_progress`. Expand only |
| `services/api/src/ftue/markers.ts` | Reads and write-once sets for the three markers. The one place they are named |
| `services/api/src/ftue/founder.ts` | `grantFounder` — the Hollow, on the first wave completion |
| `services/api/src/ftue/stock.ts` | `grantTutorialStock` — the provided pair |
| `services/api/src/ftue/pale.ts` | `grantWave6Pale` — the resupply |
| `services/api/src/routes/ftue.ts` | `POST /v1/ftue/splice-stock` |
| `services/api/src/routes/creature.ts` | `POST /v1/creature/name` |
| `services/api/src/routes/lineage.ts` | `GET /v1/lineage` |
| `services/api/src/wave/sweep.ts` | `settleExpiredForPlayer`, `sweepRetention`. The sweep two claimants have waited on |
| `services/api/src/sweep-cli.ts` | Owner-run retention sweep, the shape of `migrate-cli.ts` |
| `services/api/test/ftue.test.ts` | The first hour driven end to end, earned-vs-seeded asserted |
| `services/api/test/sweep.test.ts` | `weakenings.md` row 7, constructible at last |
| `implementation/scripts/seed-server.sh` | The `servers` row, for a deployed database |
| `implementation/scripts/smoke-loop.ts` / `.sh` | The loop against the deployed stack, over HTTP |

### New — client

| File | Responsibility |
|---|---|
| `client/Assets/Game/Shell/BootController.cs` | Composition root: session, cache, outbox pump, tab bar, screen host. **The only MonoBehaviour that knows every assembly** |
| `client/Assets/Game/Shell/Session.cs` | Guest account, tokens, `/v1/sync`, the cold-start sequence |
| `client/Assets/Game/Shell/WaveHost.cs` | Loads `Wave.unity` additively with a deployment and a wave id; unloads; hands back a `WaveReport` |
| `client/Assets/Game/Ftue/Ftue.cs` | **Pure.** `Beat Derive(snapshot, roster)` — the state machine |
| `client/Assets/Game/Tests/FtueTests.cs`, `ProgressionTests.cs` | The derivations, as tables |
| `client/Assets/Model/Progression.cs` | **Pure.** `TabsFor(highestWaveCleared, thresholds)` |
| `client/Assets/Model/SnapshotStore.cs` | Last sync to disk, back on cold start |
| `client/Assets/Net/Outbox.cs` | **Pure** queue: enqueue, next, ack, expire, backoff |
| `client/Assets/Net/OutboxStore.cs` | JSON persistence |
| `client/Assets/Net/Tests/OutboxTests.cs` | Ordering, key stability, expiry, backoff |
| `client/Assets/UI/Shell/` | `PanelSettings.asset`, `Shell.uxml`, `Shell.uss`, `TabBar.cs` |
| `client/Assets/UI/Components/` | `CreatureCard`, `TraitPip`, `CurrencyHeader`, `TimerChip`, `ConfirmDialog` |
| `client/Assets/UI/Screens/` | One `*View.cs` + `*.uxml` per screen — Region, Roster, SpliceChamber, Deploy, FounderNaming, SpliceReveal, Lineage, PostWave, WaveDefeat, CampaignSelect, CodexSheet, WaveHud |
| `client/Assets/Editor/BootSceneBuilder.cs`, `BootBuilder.cs` | The scene, the iOS release build |
| `client/Assets/Resources/BroodlineConfig.json` | `apiBaseUrl`, written by `BootBuilder` from `BROODLINE_API_URL` |

### Modified

| File | Change |
|---|---|
| `engine/Runtime/Combat/Replay.cs`, `Deployments.cs`, `SimVersion.cs` | Lane from the wave; `0.4.0` |
| `client/Assets/Game/WaveRunner.cs`, `View/Tests/WaveFixture.cs` | `WaveDef.Wave6().Lane` — no behaviour change |
| `services/api/src/config/bundle.ts`, `validate.ts` | `starterCreatures`, `progression`; two validators |
| `services/api/src/routes/account.ts` | Starter creatures in the creation transaction |
| `services/api/src/routes/sync.ts`, `schemas.ts`, `openapi.ts` | `config.tabs`, `config.waves`, `config.traits`, `ftue` |
| `services/api/src/wave/base-stock.ts`, `roster/creatures.ts` | Founder on first completion; Pale withheld |
| `services/api/src/splice/distribution.ts`, `commit.ts`, `routes/splice.ts` | `guaranteedMutation` |
| `services/api/src/routes/wave.ts` | Wave-6 Pale on settlement; `settleExpiredForPlayer` ahead of check 1 |
| `services/api/src/wave/issuance.ts` | The expired-settlement moves out of check 4 |
| `services/api/src/http/errors.ts` | `not_a_founder`, `ftue_stock_unavailable` |
| `client/Assets/UI/Tests/LoopGuardTests.cs` | The two codes join the vocabulary |
| `client/Assets/Net/BroodlineClient.cs` | The stale "in Phase 6" comment |
| `client/Assets/View/WaveHud.cs` | **Deleted**, replaced by `UI/Screens/WaveHudView` |
| `client/ProjectSettings/ProjectSettings.asset`, `EditorBuildSettings.asset` | `0.3.0`, Boot scene first |
| `implementation/scripts/verify-unity-settings.sh` | The coupling guard |
| `.github/workflows/determinism.yml` | Triggers uncommented |
| `client/Assets/Editor/BuildSteps/IosFileSharingPostProcess.cs` | Gated on development builds |

---

## Task 0: Baseline — confirm Phase 6's gates still hold, red row included

Before changing the engine, establish exactly what passes today. **`dotnet test` exits 1 by design** — `TheTrackedCapturesAreCurrent` — and anything else red is not this phase's.

**Files:** none modified.

- [ ] **Step 1: Run every gate and record the output**

```bash
export PATH="$HOME/.nvm/versions/node/v22.22.2/bin:$PATH"
dotnet test Broodline.sln --nologo 2>&1 | tee implementation/results/phase7-baseline.txt
./implementation/scripts/cross-runtime-diff.sh 2>&1 | tee -a implementation/results/phase7-baseline.txt
pnpm --filter @broodline/api test 2>&1 | tee -a implementation/results/phase7-baseline.txt
pnpm --filter @broodline/api typecheck 2>&1 | tee -a implementation/results/phase7-baseline.txt
./implementation/scripts/generate-contract.sh && git status --porcelain -- openapi/ client/Assets/Generated/ services/api/src/generated/
./implementation/scripts/run-unity-tests.sh EditMode 2>&1 | tee -a implementation/results/phase7-baseline.txt
```

Expected, against `implementation/results/phase6-test-baseline.txt`: `dotnet` **223 total, 1 failed** and the failure is `TheTrackedCapturesAreCurrent`; `skipped 0`; cross-runtime **500 scenarios agree**; api **409 passed, 0 failed**; typecheck **0 errors**; contract **clean**; unity **87 passed**. Any other red is a Phase 6 regression to fix first.

- [ ] **Step 2: Record the corpus baseline SHA**

```bash
head -1 tests/engine/corpus-baseline.txt     # expect: # simversion 0.3.0
git log -1 --format=%H -- tests/engine/corpus-baseline.txt   # record this SHA
```

Task 1 Step 6 diffs against it. Record the SHA; do not copy the file.

- [ ] **Step 3: Commit**

```bash
git add -f implementation/results/phase7-baseline.txt
git commit -m "test: capture the Phase 7 baseline before the engine changes

dotnet reports exactly one failure, TheTrackedCapturesAreCurrent, which is
Phase 6's deliberate red. Task 1 moves the lane model and SimVersion;
wave 6's hashes must survive that unchanged, and a file captured before
is the only way to say so.

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---
## Task 1: Engine — the wave owns its lane, waves 1 and 2, `SimVersion` 0.4.0

Design §2.2, §6.1, §6.2. **The only task that touches `engine/`.** Task 2's capture follows it.

**Files:**
- Modify: `engine/Runtime/Combat/Lane.cs`, `engine/Runtime/Combat/WaveDef.cs`, `engine/Runtime/Combat/Replay.cs:52-59,150-170`, `engine/Runtime/Combat/Deployments.cs:105-125`, `engine/Runtime/SimVersion.cs`
- Modify: `client/Assets/Game/WaveRunner.cs:51`, `client/Assets/View/Tests/WaveFixture.cs:18`
- Test: `tests/engine/Combat/LaneTests.cs`, `tests/engine/Combat/WaveDefTests.cs` (new), `tests/engine/Combat/WaveContentTests.cs` (new), `tests/engine/Combat/ReplayTests.cs`

**Interfaces:**
- Produces: `Lane.DefileSix()`; `WaveDef.Lane` (get); `WaveDef(int id, int integrity, int laneCount, Lane lane, SpawnEntry[] spawns)` alongside the existing four-argument constructor, which defaults the lane to `Lane.Defile()`; `WaveDef.Wave1()`, `WaveDef.Wave2()`; `WaveDef.ForId` answering 1, 2, 6, 7. `Lane.ForFamily` is **removed**.

- [ ] **Step 1: Write the failing lane and wave tests**

```csharp
// tests/engine/Combat/LaneTests.cs — add
[Fact]
public void DefileSix_HasSixPocketsBesideTiles6To20()
{
    var lane = Lane.DefileSix();
    Assert.Equal(24, lane.Tiles);
    Assert.Equal(6, lane.PocketCount);
    Assert.Equal(Terrain.Defile, lane.Family);
    for (int p = 0; p < lane.PocketCount; p++) Assert.InRange(lane.PocketTiles[p], 6, 20);
    for (int p = 1; p < lane.PocketCount; p++) Assert.True(lane.PocketTiles[p] > lane.PocketTiles[p - 1]);
}
```

```csharp
// tests/engine/Combat/WaveDefTests.cs — new
using Xunit;
using Broodline.Sim.Combat;

namespace Broodline.Sim.Tests.Combat
{
    public class WaveDefTests
    {
        [Fact]
        public void Wave1_IsSixSkirmishersTwoSecondsApart_OnSixPockets()
        {
            var w = WaveDef.Wave1();
            Assert.Equal(1, w.Id); Assert.Equal(2, w.Integrity); Assert.Equal(1, w.LaneCount);
            Assert.Equal(6, w.Lane.PocketCount);
            Assert.Equal(6, w.Spawns.Length);
            for (int i = 0; i < 6; i++)
            {
                Assert.Equal(RaiderType.Skirmisher, w.Spawns[i].Type);
                Assert.Equal(90 + 60 * i, w.Spawns[i].Tick);
            }
        }

        [Fact]
        public void Wave2_IsEightSkirmishersAndALashAtTwelveSeconds()
        {
            var w = WaveDef.Wave2();
            Assert.Equal(2, w.Id); Assert.Equal(2, w.Integrity);
            Assert.Equal(6, w.Lane.PocketCount);
            Assert.Equal(9, w.Spawns.Length);
            int lashes = 0;
            for (int i = 0; i < w.Spawns.Length; i++)
                if (w.Spawns[i].Type == RaiderType.Lash) { lashes++; Assert.Equal(360, w.Spawns[i].Tick); }
            Assert.Equal(1, lashes);
            // The tie at 360 keeps the Skirmisher first: the array index is the
            // spawn index, and the Lash "arrives into a busy lane".
            Assert.Equal(RaiderType.Skirmisher, w.Spawns[6].Type);
            Assert.Equal(RaiderType.Lash, w.Spawns[7].Type);
        }

        [Fact]
        public void Wave6AndWave7_KeepTheFivePocketDefile()
        {
            // The whole corpus and every tracked replay run on this geometry.
            foreach (var w in new[] { WaveDef.Wave6(), WaveDef.Wave7() })
            {
                Assert.Equal(5, w.Lane.PocketCount);
                Assert.Equal(new[] { 6, 10, 13, 17, 20 }, w.Lane.PocketTiles.ToArray());
            }
        }

        [Fact]
        public void ForId_AnswersTheFourAuthoredWavesAndNothingElse()
        {
            foreach (var id in new[] { 1, 2, 6, 7 }) Assert.Equal(id, WaveDef.ForId(id).Id);
            foreach (var id in new[] { 0, 3, 4, 5, 8 })
                Assert.Throws<WaveCompositionException>(() => WaveDef.ForId(id));
        }

        [Fact]
        public void ARunOnALaneOtherThanTheWavesOwn_IsUnrecordable()
        {
            // Wave 1 on wave 6's five pockets: the record would store wave id 1,
            // verification would rebuild six pockets, and a creature in pocket
            // 5 would read back as legal on a lane where it was never placed.
            var r = new SimRunner(WaveDef.Wave1(), Lane.Defile(),
                new[] { new CreatureSpec { Species = Species.Vetch, Pocket = 0, Instinct = Instinct.Vanguard } }, 1);
            Assert.NotNull(r.Unrecordable);
            Assert.Contains("pocket count", r.Unrecordable);
        }
    }
}
```

```csharp
// tests/engine/Combat/WaveContentTests.cs — new. THE BEATS' CONTENT ASSERTIONS.
using Xunit;
using Broodline.Sim.Combat;

namespace Broodline.Sim.Tests.Combat
{
    /// The first hour hands a player specific creatures and promises specific
    /// outcomes. These pin the promises against the engine, so a content edit
    /// that breaks a beat goes red here rather than on a tester's phone.
    public class WaveContentTests
    {
        static CreatureSpec Spec(Species s, int pocket, Trait t1 = Trait.None, int tier1 = 0,
                                 Trait t2 = Trait.None, int tier2 = 0) =>
            new CreatureSpec { Species = s, Pocket = pocket, Trait1 = t1, Tier1 = tier1,
                               Trait2 = t2, Tier2 = tier2, Instinct = Instinct.Vanguard };

        /// starter.json's pair, exactly as Task 3 authors them.
        static CreatureSpec[] ColdOpenPair() => new[]
        {
            Spec(Species.Vetch, 0, Trait.Taunt, 1, Trait.Carapace, 1),
            Spec(Species.Ember, 1, Trait.Splash, 1, Trait.Carapace, 1),
        };

        [Fact]
        public void Wave1_IsWonByTheColdOpenPair_InAnyTwoPockets()
        {
            // "The lane is a Defile - six pockets for two creatures - so no
            // placement is wrong." Every ordered pair of pockets, not one.
            var w = WaveDef.Wave1();
            for (int a = 0; a < 6; a++)
            for (int b = 0; b < 6; b++)
            {
                if (a == b) continue;
                var d = ColdOpenPair(); d[0].Pocket = a; d[1].Pocket = b;
                var o = Broodline.Sim.Combat.Sim.Run(w, w.Lane, d, 1);
                Assert.True(o.Result == Result.Win, "wave 1 lost with pockets " + a + "," + b);
            }
        }

        [Fact]
        public void Wave2_IsWonByTheTrio_WithTauntOnTheVetch()
        {
            // Beat 5: "the Vetch holds the Lash because Taunt is on it". The
            // Hollow is the Founder as Task 6 grants it - no traits.
            var w = WaveDef.Wave2();
            var d = new[]
            {
                Spec(Species.Vetch, 0, Trait.Taunt, 1, Trait.Carapace, 1),
                Spec(Species.Ember, 2, Trait.Splash, 1, Trait.Carapace, 1),
                Spec(Species.Hollow, 4),
            };
            var o = Broodline.Sim.Combat.Sim.Run(w, w.Lane, d, 2);
            Assert.Equal(Result.Win, o.Result);
        }

        [Fact]
        public void Wave2_WithoutTaunt_TheLashReachesPastTheFrontLine()
        {
            // Not necessarily a loss - but the Hollow must take damage it does
            // not take with Taunt present, or beat 5 teaches nothing.
            var w = WaveDef.Wave2();
            var without = new[] { Spec(Species.Vetch, 0, Trait.Carapace, 1, Trait.Carapace, 1),
                                  Spec(Species.Ember, 2, Trait.Splash, 1), Spec(Species.Hollow, 4) };
            var with    = new[] { Spec(Species.Vetch, 0, Trait.Taunt, 1, Trait.Carapace, 1),
                                  Spec(Species.Ember, 2, Trait.Splash, 1), Spec(Species.Hollow, 4) };
            Assert.NotEqual(Broodline.Sim.Combat.Sim.Run(w, w.Lane, without, 2).Hash,
                            Broodline.Sim.Combat.Sim.Run(w, w.Lane, with, 2).Hash);
        }
    }
}
```

**If `Wave1_IsWonByTheColdOpenPair` or `Wave2_IsWonByTheTrio` fails, the content is what moves, not the test.** The levers, in order: the tutorial's pocket choice in the client (Task 17 uses pockets 0 and 2; change it and this test's fixture together), then the spawn spacing in `waves_01_12.md` — which is a document edit to record, not a quiet retune.

- [ ] **Step 2: Run and watch them fail**

```bash
dotnet test tests/engine --nologo --filter "FullyQualifiedName~LaneTests|FullyQualifiedName~WaveDefTests|FullyQualifiedName~WaveContentTests"
```

Expected: FAIL — `DefileSix`, `Wave1`, `Wave2`, `Lane` do not exist.

- [ ] **Step 3: Implement the lane and wave changes**

```csharp
// Lane.cs — add beside Defile(); DELETE ForFamily entirely.
/// Defile, six pockets - the layout waves 1 and 2 run on. PROVISIONAL, owed
/// to region_roster section 3: combat_numbers section 2 gives the family
/// 4-6 pockets beside tiles 6-20, waves_01_12 wave 1 says six, and no
/// document places them. Spread so no two are adjacent.
public static Lane DefileSix() =>
    new Lane(Terrain.Defile, Stats.LaneTiles, new[] { 6, 9, 12, 14, 17, 20 });
```

```csharp
// WaveDef.cs
public Lane Lane { get; }

/// The four-argument form keeps every existing caller compiling and gives
/// synthetic waves the geometry they always had. Authored waves use the
/// five-argument form, and Deployments.Unrecordable holds a run to it.
public WaveDef(int id, int integrity, int laneCount, SpawnEntry[] spawns)
    : this(id, integrity, laneCount, Lane.Defile(), spawns) { }

public WaveDef(int id, int integrity, int laneCount, Lane lane, SpawnEntry[] spawns)
{
    Id = id; Integrity = integrity; LaneCount = laneCount; Lane = lane;
    _spawns = (SpawnEntry[])spawns.Clone();
}

/// Wave 1, waves_01_12: "6 Skirmishers . One every 2.0s from t=3 .
/// Integrity 2 . Defile layout, 6 pockets". Slower and fewer than the
/// Skirmisher's designed pattern on purpose - it is showing the player that
/// placement produces a result, not testing them.
public static WaveDef Wave1()
{
    var spawns = new SpawnEntry[6];
    for (int i = 0; i < 6; i++)
        spawns[i] = new SpawnEntry { Tick = 3 * Stats.TicksPerSecond + i * 60, Type = RaiderType.Skirmisher };
    return new WaveDef(id: 1, integrity: 2, laneCount: 1, Lane.DefileSix(), spawns);
}

/// Wave 2: "8 Skirmishers . 1 Lash . Skirmishers from t=3, 1.5s apart .
/// Lash at t=12". The Lash shares tick 360 with the seventh Skirmisher and
/// is authored AFTER it, so spawn-index order keeps the lane busy when it
/// arrives - "arriving into a busy lane is what makes it register".
public static WaveDef Wave2()
{
    var spawns = new SpawnEntry[9];
    int n = 0;
    for (int i = 0; i < 8; i++)
    {
        int tick = 3 * Stats.TicksPerSecond + i * 45;
        spawns[n++] = new SpawnEntry { Tick = tick, Type = RaiderType.Skirmisher };
        if (tick == 12 * Stats.TicksPerSecond)
            spawns[n++] = new SpawnEntry { Tick = tick, Type = RaiderType.Lash };
    }
    return new WaveDef(id: 2, integrity: 2, laneCount: 1, Lane.DefileSix(), spawns);
}

// Wave6() and Wave7(): pass Lane.Defile() explicitly through the five-argument form.

public static WaveDef ForId(int id)
{
    if (id == 1) return Wave1();
    if (id == 2) return Wave2();
    if (id == 6) return Wave6();
    if (id == 7) return Wave7();
    throw new WaveCompositionException("no authored wave with id " + id);
}
```

```csharp
// Replay.cs — BuildLane rebuilds from the WAVE the record names.
public Lane BuildLane()
{
    WaveDef wave;
    try { wave = WaveDef.ForId(WaveId); }
    catch (WaveCompositionException e)
    { throw new ReplayFormatException("wave id " + WaveId + " is not authored: " + e.Message); }
    if (wave.Lane.Family != Terrain)
        throw new ReplayFormatException("terrain " + (int)Terrain + " disagrees with wave " + WaveId + "'s " + wave.Lane.Family);
    return wave.Lane;
}
// In Validate: `lane = BuildLane();` stays; the LaneTiles and PocketCount
// cross-checks below it are unchanged and now compare against the wave's lane.
```

```csharp
// Deployments.cs — Unrecordable: replace `var rebuilt = Lane.ForFamily(lane.Family); if (rebuilt == null) ...`
var rebuilt = authored.Lane;
if (lane.Family != rebuilt.Family)
    return "terrain " + lane.Family + " disagrees with wave " + wave.Id + "'s " + rebuilt.Family;
// tiles / pocket count / per-pocket comparisons: unchanged.
```

```csharp
// client/Assets/Game/WaveRunner.cs:51 and client/Assets/View/Tests/WaveFixture.cs:18
new SimRunner(WaveDef.Wave6(), WaveDef.Wave6().Lane, Deployment(), Seed)
```

`Corpus.cs` keeps `Lane.Defile()` — it is wave 6's lane, and touching the corpus generator for a no-op is how a hash moves for a reason nobody can name.

- [ ] **Step 4: Run the engine suite**

```bash
dotnet test tests/engine --nologo
```

Expected: everything green **except** `TheTrackedCapturesAreCurrent` (still red from Phase 6 — `SimVersion` has not moved yet) and `CorpusBaselineTests` **green**: wave 6's five hundred hashes are byte-identical, which is the proof that the lane refactor was inert for every existing replay.

**If `CorpusBaselineTests` is red here, stop.** Something changed wave 6's behaviour. Do not run the emitter.

- [ ] **Step 5: Prove the change was inert across runtimes — BEFORE bumping**

```bash
./implementation/scripts/cross-runtime-diff.sh
```

Expected: `PASS: 500 scenarios agree`, under `0.3.0` still.

- [ ] **Step 6: Bump `SimVersion`, and re-baseline the header ONLY**

```csharp
// SimVersion.cs
public const string Value = "0.4.0";
```

A bump without a behaviour change is legal — `SimVersion.cs`'s own doc says so, and this bump is the design's ruling (§6.2): the rule set now answers four wave ids where it answered two, and `WaveDef.ForId` is part of what a replay's re-simulation depends on.

```bash
cp tests/engine/corpus-baseline.txt /tmp/corpus-before.txt
./implementation/scripts/emit-corpus-baseline.sh
diff <(tail -n +2 /tmp/corpus-before.txt) <(tail -n +2 tests/engine/corpus-baseline.txt); echo "exit=$?"   # MUST be 0
head -1 tests/engine/corpus-baseline.txt     # expect: # simversion 0.4.0
```

The emitter's own guard accepts this: version differs, body identical.

- [ ] **Step 7: Run the whole solution**

```bash
dotnet test Broodline.sln --nologo
```

Expected: exactly one failure, `TheTrackedCapturesAreCurrent`, now reporting `0.3.0` captures under a `0.4.0` engine. That is Task 2's input. `tests/sim/` green — `SimulateEndpoint` only stringifies `Terrain`.

- [ ] **Step 8: Commit**

```bash
git add engine/ tests/engine/ client/Assets/Game/WaveRunner.cs client/Assets/View/Tests/WaveFixture.cs
git commit -m "feat(engine): the wave owns its lane; waves 1 and 2; SimVersion 0.4.0

Lane.Defile() was written for wave 6 and every wave from 1 onward
disagreed with it - wave 1 wants six pockets, wave 4 wants a Basin. The
geometry now lives on WaveDef, a replay rebuilds it from the wave id it
already stores, and Lane.ForFamily is gone because a family no longer
determines a layout.

Waves 6 and 7 keep {6,10,13,17,20}: CorpusBaselineTests stayed green
across the refactor and the cross-runtime diff agreed on 500 scenarios
BEFORE the bump, so the change is proven inert rather than assumed.

Waves 1 and 2 are authored from waves_01_12 with the tutorial's six-
pocket Defile, and WaveContentTests pins that the cold-open pair wins
wave 1 in every pocket pair and the trio wins wave 2 - the first hour's
promises, as engine tests.

SimVersion moves to 0.4.0. The tracked captures are superseded and
TheTrackedCapturesAreCurrent is red; Task 2 re-captures them, AFTER this
change and not before - design 2.3.

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

## Task 2: The device capture — Phase 6 closes

Design §3. A human task with a script around it. **Runs after Task 1 and before any other task**, because a later engine change would supersede it again.

**Files:**
- Modify (by capture): `implementation/results/device-replay.bin`, `device-replay-outcome.txt`, `editor-replay.bin`, `editor-replay-outcome.txt`

- [ ] **Step 1: Rebuild the wave scene and the iOS project against `0.4.0`**

```bash
"/Applications/Unity/Hub/Editor/6000.6.0f1/Unity.app/Contents/MacOS/Unity" -batchmode -quit \
  -projectPath "$(pwd)/client" -executeMethod WaveSceneBuilder.Build -logFile - | tail -5
"/Applications/Unity/Hub/Editor/6000.6.0f1/Unity.app/Contents/MacOS/Unity" -batchmode -quit \
  -projectPath "$(pwd)/client" -executeMethod WaveBuilder.BuildIOS -logFile - | tail -5
```

Expected: `build/ios-wave/Unity-iPhone.xcodeproj` exists and the log ends `result=Succeeded`.

- [ ] **Step 2: The Editor capture**

Open `client/` in the Editor, play `Assets/Scenes/Wave.unity`, **tap once while the HUD's tick reads anywhere in 184–240**, let the wave end. `WaveRunner` writes `replay.bin` and `replay-outcome.txt` to `Application.persistentDataPath` (`~/Library/Application Support/Sepand Studio/Broodline Bench/` on macOS).

```bash
cp "$HOME/Library/Application Support/Sepand Studio/Broodline Bench/replay.bin" implementation/results/editor-replay.bin
cp "$HOME/Library/Application Support/Sepand Studio/Broodline Bench/replay-outcome.txt" implementation/results/editor-replay-outcome.txt
```

- [ ] **Step 3: The device capture**

Open `build/ios-wave/Unity-iPhone.xcodeproj` in Xcode, run on the iPhone, **tap once while the tick reads 184–207 — aim near 190**; roughly ten ticks of reaction lag is normal. Let the wave end. Pull the two files with Xcode's Download Container (proven route) or:

```bash
xcrun devicectl device copy from --device <UDID> --domain-type appDataContainer \
  --domain-identifier com.sepandstudio.broodline --source Documents/replay.bin --destination implementation/results/device-replay.bin
xcrun devicectl device copy from --device <UDID> --domain-type appDataContainer \
  --domain-identifier com.sepandstudio.broodline --source Documents/replay-outcome.txt --destination implementation/results/device-replay-outcome.txt
```

- [ ] **Step 4: Verify the capture is worth having**

```bash
dotnet test Broodline.sln --nologo
```

Expected: **0 failed, 0 skipped.** `TheTrackedCapturesAreCurrent` green; `TheDeviceRunReSimulatesToTheSameHash` green; **`TheDeviceRunsRallyActuallyChangedTheSimulation` green** — if it is red with *"changed nothing"*, the tap landed outside 184–207 and the capture proves nothing. Re-tap.

- [ ] **Step 5: Prove the marker still discriminates**

```bash
sed -i '' 's/"0.4.0"/"0.4.1"/' engine/Runtime/SimVersion.cs
dotnet test Broodline.sln --nologo | grep -E "Failed!|failed"    # expect: 1 failed, TheTrackedCapturesAreCurrent
git checkout engine/Runtime/SimVersion.cs
```

Phase 6 did this and it is why its red was trustworthy. Do it again.

- [ ] **Step 6: Commit — Phase 6 is closed by this commit**

```bash
git add -f implementation/results/device-replay.bin implementation/results/device-replay-outcome.txt \
           implementation/results/editor-replay.bin implementation/results/editor-replay-outcome.txt
git commit -m "test(replay): re-capture the round-trip under 0.4.0 - Phase 6 closes

Both captures re-taken on hardware: an iPhone for the device half, the
Editor for the other. TheTrackedCapturesAreCurrent is green for the
first time since Phase 6 Task 1 moved SimVersion, and the rally test
confirms the tap landed while the Courser was still below the first
defender, so the artifact can detect an IL2CPP divergence in what Rally
does rather than round-trip an inert input.

Taken AFTER Task 1's engine change, per the Phase 7 design's 2.3, which
is the ordering Phase 6 got the wrong way round.

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---
## Task 3: Bundle `0.1.3` — four waves, the cold-open pair, the tab thresholds

Design §4, §6.1. Content per `solo_execution` §5.2, validated at publish time.

**Files:**
- Create: `config/bundles/0.1.3/{waves,traits,nodes,packs,starter,progression,manifest}.json`, `config/bundles/0.1.3/locales/en.json`
- Modify: `services/api/src/config/bundle.ts`, `services/api/src/config/validate.ts`, `client/ProjectSettings/ProjectSettings.asset:152`
- Test: `services/api/test/config-validate.test.ts`, fixtures under `services/api/test/fixtures/`

**Interfaces:**
- Produces: `Bundle.starterCreatures: StarterCreature[]` where `StarterCreature = { species, trait1, tier1, trait2, tier2, instinct, isFounder }`; `Bundle.progression: { tabs: Record<string, number> }`; `validateStarterCreatures(dir)`, `validateProgression(dir)`.

- [ ] **Step 1: Write the failing validator tests**

```ts
// config-validate.test.ts — add
it('refuses a starter creature whose species has no authored HP', async () => {
  // roster/creatures.ts's creatureHp THROWS for an unknown species, and
  // account creation would then 500 on every sign-up - the one request a
  // new player cannot retry their way past.
  expect(await validateBundle(fixture('bad-starter-species'))).toEqual(
    expect.arrayContaining([expect.stringMatching(/starter.json creature 0 .* species 'Gryphon'/)]))
})

it('refuses a starter creature naming a trait the bundle does not author', async () => {
  expect(await validateBundle(fixture('bad-starter-trait'))).toEqual(
    expect.arrayContaining([expect.stringMatching(/trait 'Regrow'/)]))
})

it('refuses a progression.json missing any of the five tabs', async () => {
  // client_architecture 9: the bar is a pure function of progress and
  // THESE thresholds. A missing tab is a tab the client can never reveal.
  expect(await validateBundle(fixture('missing-tab-threshold'))).toEqual(
    expect.arrayContaining([expect.stringMatching(/progression.json .* 'Allies'/)]))
})

it('accepts 0.1.3', async () => {
  expect(await validateBundle(join(REPO, 'config/bundles/0.1.3'))).toEqual([])
})
```

Each fixture is a copy of `0.1.3` with one field broken; the existing fixtures (`bad-starter-amount`, `missing-trait-dominance`) show the shape.

- [ ] **Step 2: Run them and watch them fail**

```bash
pnpm --filter @broodline/api test config-validate
```

Expected: FAIL — no `0.1.3`, no such validators.

- [ ] **Step 3: Author the bundle**

```json
// config/bundles/0.1.3/waves.json
[
  { "id": 1, "integrity": 2, "laneCount": 1,
    "reward": { "currency": "shards", "amount": 150 },
    "spawns": [ { "tick": 90, "type": "Skirmisher" }, { "tick": 150, "type": "Skirmisher" },
                { "tick": 210, "type": "Skirmisher" }, { "tick": 270, "type": "Skirmisher" },
                { "tick": 330, "type": "Skirmisher" }, { "tick": 390, "type": "Skirmisher" } ] },
  { "id": 2, "integrity": 2, "laneCount": 1,
    "reward": { "currency": "shards", "amount": 165 },
    "spawns": [ { "tick": 90, "type": "Skirmisher" }, { "tick": 135, "type": "Skirmisher" },
                { "tick": 180, "type": "Skirmisher" }, { "tick": 225, "type": "Skirmisher" },
                { "tick": 270, "type": "Skirmisher" }, { "tick": 315, "type": "Skirmisher" },
                { "tick": 360, "type": "Skirmisher" }, { "tick": 360, "type": "Lash" },
                { "tick": 405, "type": "Skirmisher" } ] },
  { "id": 6, "integrity": 2, "laneCount": 1,
    "reward": { "currency": "shards", "amount": 40 },
    "spawns": [ { "tick": 90, "type": "Courser" } ] },
  { "id": 7, "integrity": 3, "laneCount": 1,
    "reward": { "currency": "shards", "amount": 230 },
    "spawns": [ { "tick": 120, "type": "Lash" },
                { "tick": 180, "type": "Skirmisher" }, { "tick": 225, "type": "Skirmisher" },
                { "tick": 270, "type": "Skirmisher" }, { "tick": 315, "type": "Skirmisher" },
                { "tick": 360, "type": "Skirmisher" }, { "tick": 405, "type": "Skirmisher" } ] }
]
```

The spawn timelines are the engine's `Wave1()`/`Wave2()` transcribed; `tools/config-validate` parses them through `BundleWaves.Parse` and the engine's `Validate()`, and a divergence between the JSON and the engine is not caught by anything — recorded in §13 as owed (a `ForId`-equality check in the tool is the fix, and it is a tool change rather than an engine one).

```json
// config/bundles/0.1.3/starter.json
{
  "grants": [
    { "currency": "splice_charges", "amount": 3 },
    { "currency": "shards", "amount": 250 }
  ],
  "creatures": [
    { "species": "Vetch", "trait1": "Taunt",  "tier1": 1, "trait2": "Carapace", "tier2": 1, "instinct": "Vanguard", "isFounder": false },
    { "species": "Ember", "trait1": "Splash", "tier1": 1, "trait2": "Carapace", "tier2": 1, "instinct": "Vanguard", "isFounder": false }
  ]
}
```

```json
// config/bundles/0.1.3/progression.json
// Reveal a tab when highestWaveCleared >= threshold. client_architecture 9:
// thresholds live here so retuning the reveal is a bundle publish, not an
// App Review cycle. 61 is past the campaign - "never, this phase".
{ "tabs": { "Map": 0, "Ark": 0, "Splice": 2, "Lab": 61, "Allies": 61 } }
```

`traits.json`, `nodes.json`, `packs.json`, `locales/en.json`: copied from `0.1.2` unchanged. `manifest.json`: `{ "version": "0.1.3", "minimumClientVersion": "0.3.0" }`.

- [ ] **Step 4: Extend the loader and add the validators**

```ts
// bundle.ts — add to Bundle, and read in loadBundle with the same catch-on-read tolerance nodes.json has
export interface StarterCreature {
  species: string; trait1: string; tier1: number; trait2: string; tier2: number
  instinct: string; isFounder: boolean
}
export interface Progression { tabs: Record<string, number> }
// Bundle gains:
starterCreatures: StarterCreature[]   // [] when starter.json has no `creatures`
progression: Progression              // { tabs: {} } when progression.json is absent
```

```ts
// validate.ts — add to validateBundle's list, and:
const KNOWN_SPECIES = ['Vetch', 'Ember', 'Skitter', 'Hollow', 'Loam', 'Pale']   // roster/creatures.ts creatureHp
const TABS = ['Map', 'Ark', 'Splice', 'Lab', 'Allies']

async function validateStarterCreatures(dir: string): Promise<string[]> {
  const raw = await readFile(join(dir, 'starter.json'), 'utf8').catch(() => null)
  if (raw === null) return []                        // validateStarterGrants already reports the missing file
  const creatures = (JSON.parse(raw) as { creatures?: StarterCreature[] }).creatures ?? []
  const traitsRaw = await readFile(join(dir, 'traits.json'), 'utf8').catch(() => null)
  const authored = new Set(traitsRaw === null ? [] : (JSON.parse(traitsRaw) as { traits: Array<{ id: string }> }).traits.map((t) => t.id))
  authored.add('None')
  const v: string[] = []
  creatures.forEach((c, i) => {
    if (!KNOWN_SPECIES.includes(c.species)) v.push(`starter.json creature ${i} has species '${c.species}', which has no authored HP.`)
    for (const t of [c.trait1, c.trait2]) if (!authored.has(t)) v.push(`starter.json creature ${i} names trait '${t}', which this bundle does not author.`)
    for (const tier of [c.tier1, c.tier2]) if (!Number.isInteger(tier) || tier < 1 || tier > 3) v.push(`starter.json creature ${i} has coverage tier ${tier} outside 1..3.`)
    if (typeof c.isFounder !== 'boolean') v.push(`starter.json creature ${i} is missing isFounder.`)
  })
  return v
}

async function validateProgression(dir: string): Promise<string[]> {
  const raw = await readFile(join(dir, 'progression.json'), 'utf8').catch(() => null)
  if (raw === null) return []                        // 0.1.0-0.1.2 predate it and are immutable
  const tabs = (JSON.parse(raw) as { tabs?: Record<string, unknown> }).tabs ?? {}
  return TABS.filter((t) => !Number.isInteger(tabs[t]) || (tabs[t] as number) < 0)
    .map((t) => `progression.json has no non-negative integer threshold for tab '${t}'.`)
}
```

- [ ] **Step 5: Move the client's version with the floor**

`client/ProjectSettings/ProjectSettings.asset:152` → `bundleVersion: 0.3.0`. **Same commit as the manifest.** Task 10 builds the guard that makes this coupling a gate; until then it is discipline.

- [ ] **Step 6: Run the tests, then prove each rule alone**

```bash
pnpm --filter @broodline/api test config-validate     # expect PASS
# Weaken: delete the KNOWN_SPECIES check -> the species test reddens; restore.
# Weaken: delete the TABS filter -> the tab test reddens; restore.
```

- [ ] **Step 7: Commit**

```bash
git add config/bundles/0.1.3 services/api/src/config services/api/test/config-validate.test.ts services/api/test/fixtures client/ProjectSettings/ProjectSettings.asset
git commit -m "feat(config): bundle 0.1.3 - waves 1 and 2, the cold-open pair, tab thresholds

starter.json grows a creatures array so the cold open hands over a Vetch
and an Ember that a bundle authored rather than a handler hard-coded.
progression.json carries the tab reveal thresholds client_architecture 9
wants in the bundle. Both validated at publish time: a species with no
authored HP would 500 every account creation, and a missing tab is one
the client can never reveal.

minimumClientVersion moves to 0.3.0 and bundleVersion moves with it in
the same commit. Task 10 makes that a gate.

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

## Task 4: Migration `0008` — three FTUE markers on `campaign_progress`

Design §5, §10.2. Expand only, no reader in this commit.

**Files:**
- Create: `services/api/drizzle/0008_ftue_markers.sql`, `services/api/src/ftue/markers.ts`
- Modify: `services/api/src/db/schema.ts` (campaignProgress)
- Test: `services/api/test/ftue-markers.test.ts` (new)

**Interfaces:**
- Produces: columns `founder_granted_at`, `tutorial_stock_granted_at`, `wave6_pale_granted_at` (`timestamptz NULL`); `readMarkers(tx, serverId, playerId): Promise<Markers>`; `setMarker(tx, serverId, playerId, marker): Promise<boolean>` returning **true only when this call set it** — the `settle()` shape, so a grant can be gated on the write rather than on the absence of an exception.

- [ ] **Step 1: Write the failing test**

```ts
// ftue-markers.test.ts
it('setMarker is write-once and reports whether THIS call wrote it', async () => {
  const first = await withServer(t.db, 1, (tx) => setMarker(tx, 1, playerId, 'founder_granted_at'))
  const second = await withServer(t.db, 1, (tx) => setMarker(tx, 1, playerId, 'founder_granted_at'))
  expect(first).toBe(true)
  expect(second).toBe(false)
  const m = await withServer(t.db, 1, (tx) => readMarkers(tx, 1, playerId))
  expect(m.founderGrantedAt).not.toBeNull()
  expect(m.tutorialStockGrantedAt).toBeNull()
})

it('creates the campaign_progress row if the player has none yet', async () => {
  // A brand-new player has no progress row until their first clear, and the
  // Founder grant fires ON that first clear - so the marker write must not
  // depend on a row advanceCampaign has not written yet.
  const fresh = await setupPlayer(deps)
  const before = await withServer(t.db, 1, (tx) => tx.select().from(campaignProgress)
    .where(and(eq(campaignProgress.serverId, 1), eq(campaignProgress.playerId, fresh.playerId))))
  expect(before.length).toBe(0)
  expect(await withServer(t.db, 1, (tx) => setMarker(tx, 1, fresh.playerId, 'wave6_pale_granted_at'))).toBe(true)
  const after = await withServer(t.db, 1, (tx) => tx.select().from(campaignProgress)
    .where(and(eq(campaignProgress.serverId, 1), eq(campaignProgress.playerId, fresh.playerId))))
  expect(after.length).toBe(1)
  expect(after[0]!.highestWaveCleared).toBe(0)           // the insert took the column defaults
  expect(after[0]!.wave6PaleGrantedAt).not.toBeNull()
})
```

- [ ] **Step 2: Run and watch it fail** — `pnpm --filter @broodline/api test ftue-markers` → FAIL, no module.

- [ ] **Step 3: The migration and the module**

```sql
-- 0008_ftue_markers.sql
-- Three write-once instants on campaign_progress. Each records that a
-- one-time FTUE grant has fired, so the grant is idempotent by a row the
-- player cannot influence rather than by counting creatures (a splice could
-- remove the very creature the count relied on).
--
-- EXPAND ONLY. Nullable, no default, no reader in this deploy -
-- solo_execution 7.0. IF NOT EXISTS for the reason 0006 gives.
ALTER TABLE campaign_progress ADD COLUMN IF NOT EXISTS founder_granted_at        timestamptz;
ALTER TABLE campaign_progress ADD COLUMN IF NOT EXISTS tutorial_stock_granted_at timestamptz;
ALTER TABLE campaign_progress ADD COLUMN IF NOT EXISTS wave6_pale_granted_at     timestamptz;
```

```ts
// src/ftue/markers.ts
import { and, eq, isNull, sql } from 'drizzle-orm'
import type { Tx } from '../db/client.ts'
import { campaignProgress } from '../db/schema.ts'

export type Marker = 'founder_granted_at' | 'tutorial_stock_granted_at' | 'wave6_pale_granted_at'
export interface Markers { founderGrantedAt: Date | null; tutorialStockGrantedAt: Date | null; wave6PaleGrantedAt: Date | null }

export async function readMarkers(tx: Tx, serverId: number, playerId: string): Promise<Markers> {
  const [row] = await tx.select().from(campaignProgress)
    .where(and(eq(campaignProgress.serverId, serverId), eq(campaignProgress.playerId, playerId)))
  return {
    founderGrantedAt: row?.founderGrantedAt ?? null,
    tutorialStockGrantedAt: row?.tutorialStockGrantedAt ?? null,
    wave6PaleGrantedAt: row?.wave6PaleGrantedAt ?? null,
  }
}

/** True iff this call wrote the marker. Inserts the progress row when absent. */
export async function setMarker(tx: Tx, serverId: number, playerId: string, marker: Marker): Promise<boolean> {
  await tx.insert(campaignProgress).values({ serverId, playerId }).onConflictDoNothing()
  const res = await tx.execute(sql`
    UPDATE campaign_progress SET ${sql.identifier(marker)} = now(), updated_at = now()
    WHERE server_id = ${serverId} AND player_id = ${playerId} AND ${sql.identifier(marker)} IS NULL`)
  return (res.rowCount ?? 0) > 0
}
```

`schema.ts`: `founderGrantedAt`, `tutorialStockGrantedAt`, `wave6PaleGrantedAt` as `timestamp(..., { withTimezone: true })` on `campaignProgress`.

- [ ] **Step 4: Run** — PASS. Then `pnpm --filter @broodline/api test` — every suite migrates through `0008` on Testcontainers; all green.

- [ ] **Step 5: Commit**

```bash
git add services/api/drizzle/0008_ftue_markers.sql services/api/src/ftue/markers.ts services/api/src/db/schema.ts services/api/test/ftue-markers.test.ts
git commit -m "feat(db): 0008 - three write-once FTUE markers, expand only

Founder, tutorial stock and the wave-6 Pale each fire once per player,
and \"once\" needs a fact the player cannot move. A creature count is not
one: a splice consumes the creature the count relied on. Three nullable
instants on campaign_progress are, and setMarker reports whether THIS
call wrote it, the way settle() does, so a grant is gated on the write.

No reader ships in this commit - solo_execution 7.0.

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---
## Task 5: Account creation grants the pair; `/v1/sync` learns what the shell needs

Design §4, §5 beat 1. The cold open needs two creatures the moment the account exists, and the tab bar needs thresholds, the wave list and the trait table without a second round trip.

**Files:**
- Modify: `services/api/src/routes/account.ts`, `services/api/src/routes/sync.ts`, `services/api/src/schemas.ts:32-50`, `services/api/src/openapi.ts`
- Test: `services/api/test/account.test.ts`, `services/api/test/sync.test.ts`

**Interfaces:**
- Produces: `SyncResponse.config.tabs: Record<string, number>`, `config.waves: Array<{ id, reward: { currency, amount } | null }>`, `config.traits: Array<{ id, species, counters: string | null }>`, `SyncResponse.ftue: { founderNamed: boolean, tutorialStockGranted: boolean, splices: number }`. All additive; the Phase 6 client keeps parsing.

- [ ] **Step 1: Write the failing tests**

```ts
// account.test.ts — add (publish 0.1.3 in this file's beforeAll)
it('grants the bundle\'s starter creatures in the creation transaction', async () => {
  // Two creatures, Gen-1, not Founders - and a replayed creation under the
  // SAME idempotency key grants nothing twice: the roster is 2, not 4.
  const rows = await roster(playerId)
  expect(rows.map((r) => r.species).sort()).toEqual(['Ember', 'Vetch'])
  expect(rows.every((r) => r.generation === 1 && r.isFounder === false)).toBe(true)
  await app.request('/v1/account', { method: 'POST', headers: { 'content-type': 'application/json', 'idempotency-key': sameKey }, body: sameBody })
  expect((await roster(playerId)).length).toBe(2)
})
```

```ts
// sync.test.ts — add
it('returns the tab thresholds, the wave list and the trait table from the bundle', async () => {
  const body = await sync()
  expect(body.config.tabs).toEqual({ Map: 0, Ark: 0, Splice: 2, Lab: 61, Allies: 61 })
  expect(body.config.waves.map((w) => w.id)).toEqual([1, 2, 6, 7])
  expect(body.config.traits.find((t) => t.id === 'Chill')?.counters).toBe('Courser')
})

it('reports the FTUE facts the client derives beats from', async () => {
  expect((await sync()).ftue).toEqual({ founderNamed: false, tutorialStockGranted: false, splices: 0 })
})
```

- [ ] **Step 2: Run and watch them fail**

```bash
pnpm --filter @broodline/api test account sync
```

Expected: FAIL — no creatures granted, no `config.tabs`, no `ftue`.

- [ ] **Step 3: Implement**

```ts
// account.ts — inside the withIdempotency transaction, after the currency grants:
if (bundle.starterCreatures.length > 0) {
  await tx.insert(creatures).values(bundle.starterCreatures.map((c) => ({
    serverId, playerId: player!.playerId, generation: 1,
    species: c.species, trait1: c.trait1, tier1: c.tier1, trait2: c.trait2, tier2: c.tier2,
    instinct: c.instinct, isFounder: c.isFounder, name: null, hpCurrent: creatureHp(c.species),
  })))
}
```

```ts
// sync.ts — inside withServer, three more reads on the player's own rows:
const [founder] = await tx.select({ named: sql<boolean>`coalesce(bool_or(${creatures.name} IS NOT NULL), false)` })
  .from(creatures).where(and(eq(creatures.serverId, session.serverId), eq(creatures.playerId, player.playerId), eq(creatures.isFounder, true)))
const [spliceCount] = await tx.select({ n: count() }).from(splices)
  .where(and(eq(splices.serverId, session.serverId), eq(splices.playerId, player.playerId)))
const markers = await readMarkers(tx, session.serverId, player.playerId)
// ...and in the response:
config: {
  bundleVersion: bundle.version, minimumClientVersion: bundle.minimumClientVersion,
  tabs: bundle.progression.tabs,
  waves: bundle.waves.map((w) => ({ id: w.id, reward: w.reward ?? null })),
  traits: bundle.traits.map((t) => ({ id: t.id, species: t.species, counters: t.counters })),
},
ftue: {
  founderNamed: founder?.named ?? false,
  tutorialStockGranted: markers.tutorialStockGrantedAt !== null,
  splices: Number(spliceCount?.n ?? 0),
},
```

`schemas.ts` `SyncResponse` gains the two objects; no new path in `openapi.ts`.

**The p99 budget.** `/v1/sync` carries the phase's one SLO, 300 ms. Three more indexed reads on the player's own rows sit inside it and the bundle is cached per process. Do not add a fourth without measuring.

- [ ] **Step 4: Regenerate the contract**

```bash
./implementation/scripts/generate-contract.sh
git status --porcelain -- openapi/ client/Assets/Generated/ services/api/src/generated/   # the four outputs modified, nothing untracked
```

- [ ] **Step 5: Run**

```bash
pnpm --filter @broodline/api test && pnpm --filter @broodline/api typecheck
```

Expected: PASS. `loop.test.ts` **will redden** — its `EXPECTED_EARNED`/`EXPECTED_SEEDED` were booked against an empty starting roster, and the starter pair is now earned through `POST /v1/account`. Update the two constants to what the drive really does and book why in the comment; the file says it is meant to be brittle about exactly this.

- [ ] **Step 6: Commit**

```bash
git add services/api/src services/api/test openapi/ client/Assets/Generated/
git commit -m "feat(api): starter creatures at sign-up; sync carries tabs, waves, traits and the FTUE facts

The cold open needs two creatures the instant the account exists - in
the same transaction as the currency grants and under the same
idempotency key, so a replayed creation grants nothing twice.

sync grows four additive fields so the shell derives the tab bar, the
campaign list, the Codex sheet and the current beat from one cold-start
call. Nothing about FTUE progress is stored on the client.

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

## Task 6: The Founder — granted on the first completion, named by the player; Pale withheld

Design §5 beats 3–4; "what this plan found the design missed" items 1–2.

**Files:**
- Create: `services/api/src/ftue/founder.ts`, `services/api/src/routes/creature.ts`
- Modify: `services/api/src/wave/base-stock.ts`, `services/api/src/map/claim.ts` (the other minting path), `services/api/src/roster/creatures.ts:340-400`, `services/api/src/http/errors.ts`, `services/api/src/schemas.ts`, `services/api/src/openapi.ts`, `services/api/src/app.ts`, `client/Assets/UI/Tests/LoopGuardTests.cs`
- Test: `services/api/test/founder.test.ts` (new), `services/api/test/base-stock.test.ts`

**Interfaces:**
- Produces: `grantFounder(tx, serverId, playerId): Promise<CreatureRow | null>`; `baseStockPool(markers): readonly BaseStockSpecies[]`; `speciesForSeed(seed, pool)`; `grantBaseStock(tx, serverId, playerId, n, seed, pool)`; `POST /v1/creature/name` body `{ creatureId, name }` → `CreatureDto`; error code `not_a_founder` (409).

- [ ] **Step 1: Write the failing tests**

```ts
// founder.test.ts
it('the first wave completion grants a Hollow Founder instead of rolled stock', async () => {
  await setupPlayer(deps)                                  // 0.1.3: roster is Vetch + Ember
  const pair = await ownedPair()                           // the two starter ids, pockets 0 and 2
  const start = await startWave(1, pair)
  const { issuanceId, seed } = await start.json() as { issuanceId: string; seed: string }
  await submit(issuanceId, buildReplayOf(1, BigInt(seed), coldOpenReplayDeployment()), randomUUID())
  const rows = await roster()
  const founder = rows.find((r) => r.isFounder)
  expect(founder?.species).toBe('Hollow')
  expect(founder?.name).toBeNull()
  expect(rows.length).toBe(3)
})

it('the SECOND completion grants rolled base stock, never a second Founder', async () => {
  // clear wave 1 as above, then wave 2 with the trio
  expect((await roster()).filter((r) => r.isFounder).length).toBe(1)
  expect((await roster()).length).toBe(4)
})

it('base stock never mints a Pale before the wave-6 grant has fired', async () => {
  const none = { founderGrantedAt: null, tutorialStockGrantedAt: null, wave6PaleGrantedAt: null }
  const before = baseStockPool(none)
  const after  = baseStockPool({ ...none, wave6PaleGrantedAt: new Date() })
  for (let i = 0; i < 200; i++) expect(speciesForSeed(`s:${i}`, before).species).not.toBe('Pale')
  expect(Array.from({ length: 200 }, (_, i) => speciesForSeed(`s:${i}`, after).species)).toContain('Pale')
})

it('POST /v1/creature/name names a Founder and refuses everything else', async () => {
  const ok = await name(founder.creatureId, '  Ash  ')
  expect(ok.status).toBe(200); expect(((await ok.json()) as { name: string }).name).toBe('Ash')   // trimmed
  expect((await name(nonFounder.creatureId, 'Ash')).status).toBe(409)                               // not_a_founder
  expect((await name(founder.creatureId, '')).status).toBe(400)
  expect((await name(founder.creatureId, 'x'.repeat(17))).status).toBe(400)
  expect((await name(founder.creatureId, 'Ash' + String.fromCharCode(7))).status).toBe(400)         // a control character
  // Renameable at any time - bible 3.3 - so a second name is a 200, not a conflict.
  expect((await name(founder.creatureId, 'Ember Ash')).status).toBe(200)
})
```

- [ ] **Step 2: Run and watch them fail**

```bash
pnpm --filter @broodline/api test founder base-stock
```

Expected: FAIL.

- [ ] **Step 3: Implement**

```ts
// roster/creatures.ts
export const BASE_STOCK_BEFORE_PALE: readonly BaseStockSpecies[] = BASE_STOCK_SPECIES.filter((s) => s.species !== 'Pale')

/** campaign_structure 1 / whats_left 2: Pale is WITHHELD until the Wave Defeat
 *  screen grants it at wave 6. A rolled Pale before then hands the player
 *  Chill before the beat that exists to make them want it. */
export function baseStockPool(m: Markers): readonly BaseStockSpecies[] {
  return m.wave6PaleGrantedAt === null ? BASE_STOCK_BEFORE_PALE : baseStockSpecies
}

export function speciesForSeed(seed: string, pool: readonly BaseStockSpecies[] = baseStockSpecies): BaseStockSpecies {
  const digest = createHash('sha256').update(seed).digest()
  return pool[digest.readUInt32BE(0) % pool.length]!
}
// grantBaseStock(tx, serverId, playerId, n, seed, pool = baseStockSpecies) threads `pool` to speciesForSeed.
```

```ts
// ftue/founder.ts
export const FOUNDER = { species: 'Hollow', trait1: 'None', tier1: null, trait2: 'None', tier2: null, instinct: 'Vanguard' } as const

/** The Hollow, once. Null when the marker was already set by an earlier call. */
export async function grantFounder(tx: Tx, serverId: number, playerId: string): Promise<CreatureRow | null> {
  if (!(await setMarker(tx, serverId, playerId, 'founder_granted_at'))) return null
  const [row] = await tx.insert(creatures).values({
    serverId, playerId, generation: 1, ...FOUNDER, isFounder: true, name: null, hpCurrent: creatureHp('Hollow'),
  }).returning()
  return row!
}
```

```ts
// wave/base-stock.ts — grantWaveBaseStock, after lockRoster and the cap check:
const markers = await readMarkers(tx, serverId, playerId)
if (markers.founderGrantedAt === null) {
  // beat 3: the first completion's drop IS the Founder. Not a roll.
  return (await grantFounder(tx, serverId, playerId)) === null ? 0 : 1
}
const granted = await grantBaseStock(tx, serverId, playerId, WAVE_BASE_STOCK,
  `wave:${serverId}:${playerId}:${issuanceId}`, baseStockPool(markers))
return granted.length
```

`map/claim.ts`'s grant passes `baseStockPool(await readMarkers(tx, serverId, playerId))` the same way — a node claim is the other minting path and it must not mint a Pale either.

```ts
// routes/creature.ts
const NAME_MAX = 16

function parseName(raw: unknown): { creatureId: string; name: string } | null {
  if (typeof raw !== 'object' || raw === null) return null
  const b = raw as Record<string, unknown>
  const creatureId = typeof b.creatureId === 'string' ? normalizeUuid(b.creatureId) : null
  if (creatureId === null || typeof b.name !== 'string') return null
  const name = b.name.trim()
  if (name.length < 1 || name.length > NAME_MAX) return null
  // Printable only. No filter this phase (design 1, 12): internal testers.
  if (/[\p{Cc}\p{Cf}]/u.test(name)) return null
  return { creatureId, name }
}

export function registerCreatureRoutes(app: Hono, deps: Deps): void {
  app.post('/v1/creature/name', async (c) => {
    const session = await requireSession(c)
    const key = c.req.header('idempotency-key')
    if (!key) return fail('invalid_request', 'An Idempotency-Key header is required.')
    const body = parseName(await c.req.json().catch(() => null))
    if (body === null) return fail('invalid_request', `creatureId must be a uuid and name 1-${NAME_MAX} printable characters.`)
    try {
      const result = await withIdempotency(deps.db, session.serverId, key, hashRequest(body), async (tx) => {
        const playerId = await loadPlayerId(tx, session.accountId)
        const [row] = await tx.select().from(creatures).where(and(
          eq(creatures.serverId, session.serverId), eq(creatures.playerId, playerId!),
          eq(creatures.creatureId, body.creatureId), liveCreature())).for('update')
        if (row === undefined) return { refused: 'not_found' as const }
        // only_founders_named in SQL; this is the same rule with a sentence.
        if (!row.isFounder) return { refused: 'not_a_founder' as const }
        const [updated] = await tx.update(creatures).set({ name: body.name })
          .where(and(eq(creatures.serverId, session.serverId), eq(creatures.creatureId, body.creatureId))).returning()
        return { creature: toCreatureDto(updated!) }
      })
      if ('refused' in result.body) {
        return result.body.refused === 'not_found'
          ? fail('not_found', 'No such live creature on your roster.')
          : fail('not_a_founder', 'Only Founders can be named.')
      }
      return c.json(result.body.creature)
    } catch (err) {
      if (err instanceof IdempotencyMismatchError) return fail('idempotency_key_reused', 'This Idempotency-Key was used for a different request.')
      throw err
    }
  })
}
```

`errors.ts`: `| 'not_a_founder'` at **409** — understood, refused on state. `app.ts`: `registerCreatureRoutes(app, deps)`. `schemas.ts`: `CreatureNameRequest`; the response is `CreatureDto`. `openapi.ts`: register `POST /v1/creature/name`. `LoopGuardTests.cs`'s vocabulary list gains `"not_a_founder"` — `EveryCodeTheServerCanSend_IsRecognised` reddens until it does.

- [ ] **Step 4: Regenerate the contract, run everything**

```bash
./implementation/scripts/generate-contract.sh
pnpm --filter @broodline/api test && pnpm --filter @broodline/api typecheck
./implementation/scripts/run-unity-tests.sh EditMode
```

Expected: PASS. `loop.test.ts`'s split moves again (the first completion is now a Founder); update and book it.

- [ ] **Step 5: Prove the withholding**

Weaken `baseStockPool` to always return `baseStockSpecies`; the 200-seed test reddens. Restore.

- [ ] **Step 6: Commit**

```bash
git add services/api client/Assets/UI/Tests/LoopGuardTests.cs openapi/ client/Assets/Generated/
git commit -m "feat(api): the Hollow Founder on the first completion, named by the player; Pale withheld

The first wave completion grants a Hollow flagged is_founder instead of
rolled stock - waves_01_12 wave 2 expects Vetch, Ember, Hollow, and
campaign_structure names Hollow a Founder. Gated on a write-once marker,
so a second completion is ordinary base stock.

POST /v1/creature/name sets a Founder's name: trimmed, 1-16 printable
characters, renameable at any time per bible 3.3, refused 409 for a
non-Founder. No filter - internal testers only, and the design says so.

Base stock withholds Pale until the wave-6 grant has fired. The pool was
Vetch, Pale, Ember, so one drop in three handed the player Chill before
the beat that exists to make them want it.

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---
## Task 7: The guided splice — provided stock, and a first mutation that is guaranteed on both sides

Design §5 beats 6–7. Two rulings, one property: the forecast the player sees before the charge is spent is the roll they get.

**Files:**
- Create: `services/api/src/ftue/stock.ts`, `services/api/src/routes/ftue.ts`
- Modify: `services/api/src/splice/distribution.ts:238-275`, `services/api/src/splice/commit.ts:285-300`, `services/api/src/routes/splice.ts:163-200`, `services/api/src/http/errors.ts`, `services/api/src/schemas.ts`, `services/api/src/openapi.ts`, `services/api/src/app.ts`, `client/Assets/UI/Tests/LoopGuardTests.cs`
- Test: `services/api/test/ftue-stock.test.ts` (new), `services/api/test/splice-distribution.test.ts`, `services/api/test/splice-preview.test.ts`, `services/api/test/splice-commit.test.ts`

**Interfaces:**
- Produces: `POST /v1/ftue/splice-stock` (Idempotency-Key) → `{ creatures: CreatureDto[] }`; error `ftue_stock_unavailable` (409); `spliceDistribution(a, b, locked, traits, opts?: { guaranteedMutation?: boolean })`; `isFirstSplice(tx, serverId, playerId): Promise<boolean>` in `splice/commit.ts`, exported for the preview route.

- [ ] **Step 1: Write the failing tests**

```ts
// ftue-stock.test.ts
it('grants Vetch + Ember once, only after wave 2, only before the first splice', async () => {
  await setupPlayer(deps)
  expect((await stock()).status).toBe(409)               // wave 2 not cleared: ftue_stock_unavailable
  await clearWave(2)
  const ok = await stock()
  expect(ok.status).toBe(200)
  const body = await ok.json() as { creatures: Array<{ species: string; isFounder: boolean; generation: number }> }
  expect(body.creatures.map((c) => c.species).sort()).toEqual(['Ember', 'Vetch'])
  expect(body.creatures.every((c) => !c.isFounder && c.generation === 1)).toBe(true)
  expect((await stock()).status).toBe(409)               // already granted
})

it('the same Idempotency-Key replays the stored 200 rather than a 409', async () => {
  // The key protects the RESPONSE - design 4.2 guard one. A client that
  // retries its successful grant must not be told it is unavailable.
})
```

```ts
// splice-distribution.test.ts — add
it('a guaranteed mutation publishes 1 and sampleSplice honours it for every seed', () => {
  const d = spliceDistribution(a, b, locked, table, { guaranteedMutation: true })
  expect(d.mutation).toBe(1)
  for (let s = 0n; s < 500n; s++) expect(sampleSplice(d, s).mutated).toBe(true)
  // The Aberrant sub-roll is untouched: OF mutations, 5%.
  expect(d.aberrant).toBe(ABERRANT_SUB_ROLL)
})
```

```ts
// splice-preview.test.ts / splice-commit.test.ts — add to each
it('the first splice forecasts mutation 1 and the commit mutates; the second does neither', async () => {
  // preview and commit read the SAME row count, so what preview published is
  // what commit rolled - design 5.1's one-distribution rule, extended by one
  // input rather than broken by a special case.
  expect((await preview(a, b)).forecast.mutation).toBe(1)
  expect((await commit(a, b)).mutated).toBe(true)
  expect((await preview(c, d)).forecast.mutation).toBe(MUTATION_RATE)
})
```

- [ ] **Step 2: Run and watch them fail**

```bash
pnpm --filter @broodline/api test ftue-stock splice-distribution splice-preview splice-commit
```

- [ ] **Step 3: Implement**

```ts
// splice/distribution.ts
export interface DistributionOpts { guaranteedMutation?: boolean }

export function spliceDistribution(
  a: SpliceParent, b: SpliceParent, locked: TraitRef, traits: TraitTable, opts: DistributionOpts = {},
): Distribution {
  // ...unchanged through `instinct`...
  // Beat 7: "Mutation fires. Scripted, guaranteed." A PARAMETER to the one
  // distribution rather than a branch in commit, so preview publishes the
  // certainty and commit samples the same object. supersession_map 3.1: at
  // a 9% base this sets a far less unrealistic expectation than at 3%.
  const mutation = opts.guaranteedMutation === true ? 1 : MUTATION_RATE
  return { combat2, instinct, mutation, aberrant: ABERRANT_SUB_ROLL }
}
```

```ts
// splice/commit.ts
/** Zero rows in `splices` for this player. Read inside the caller's transaction. */
export async function isFirstSplice(tx: Tx, serverId: number, playerId: string): Promise<boolean> {
  const [row] = await tx.select({ n: count() }).from(splices)
    .where(and(eq(splices.serverId, serverId), eq(splices.playerId, playerId)))
  return Number(row?.n ?? 0) === 0
}
// in commitSplice, replacing the forecast line:
const forecast = spliceDistribution(parentA, parentB, req.locked, bundle,
  { guaranteedMutation: await isFirstSplice(tx, serverId, playerId) })
```

```ts
// routes/splice.ts — preview, replacing its forecast line:
forecast = spliceDistribution(a, b, body.locked, bundle,
  { guaranteedMutation: await isFirstSplice(tx, session.serverId, playerId) })
```

```ts
// ftue/stock.ts
export const TUTORIAL_STOCK = [
  { species: 'Vetch', trait1: 'Taunt',  tier1: 1, trait2: 'Carapace', tier2: 1 },
  { species: 'Ember', trait1: 'Splash', tier1: 1, trait2: 'Carapace', tier2: 1 },
] as const

export type StockResult = { kind: 'ok'; creatures: CreatureDto[] } | { kind: 'unavailable'; why: string }

/** The provided pair - splice_confirm_spec 6: "provided specifically for the tutorial and framed as sample stock". */
export async function grantTutorialStock(tx: Tx, serverId: number, playerId: string): Promise<StockResult> {
  const [progress] = await tx.select().from(campaignProgress)
    .where(and(eq(campaignProgress.serverId, serverId), eq(campaignProgress.playerId, playerId)))
  if ((progress?.highestWaveCleared ?? 0) < 2) return { kind: 'unavailable', why: 'Clear wave 2 first.' }
  if (!(await isFirstSplice(tx, serverId, playerId))) return { kind: 'unavailable', why: 'The tutorial splice has already happened.' }
  await lockRoster(tx, serverId, playerId)
  if (!(await setMarker(tx, serverId, playerId, 'tutorial_stock_granted_at'))) return { kind: 'unavailable', why: 'Already granted.' }
  // NO CAP CHECK, deliberately: these two exist to be consumed by the next
  // action, and refusing them roster_full would strand the tutorial at the
  // beat it exists to teach.
  const rows = await tx.insert(creatures).values(TUTORIAL_STOCK.map((c) => ({
    serverId, playerId, generation: 1, ...c, instinct: 'Vanguard', isFounder: false, name: null, hpCurrent: creatureHp(c.species),
  }))).returning()
  return { kind: 'ok', creatures: rows.map(toCreatureDto) }
}
```

`routes/ftue.ts`: the `withIdempotency` shape of `routes/creature.ts`, body-less, calling `grantTutorialStock`; `unavailable` → `fail('ftue_stock_unavailable', why)`. `errors.ts`: `ftue_stock_unavailable: 409`. `app.ts`, `schemas.ts` (`FtueStockResponse`), `openapi.ts`, `LoopGuardTests.cs` vocabulary.

- [ ] **Step 4: Regenerate the contract, run everything**

```bash
./implementation/scripts/generate-contract.sh
pnpm --filter @broodline/api test && pnpm --filter @broodline/api typecheck
./implementation/scripts/run-unity-tests.sh EditMode
```

The **golden vectors** in `splice-distribution.test.ts` pin `uniforms(seed)` and the channel layout; they must be untouched — the guarantee changes the threshold, not the draw.

- [ ] **Step 5: Prove the property holds on both sides**

Weaken commit alone — pass `{}` instead of the `isFirstSplice` result — and the commit test reddens while preview's stays green; that asymmetry is exactly what design §5.1 forbids and what the pair of tests detects. Restore.

- [ ] **Step 6: Commit**

```bash
git add services/api client/Assets/UI/Tests/LoopGuardTests.cs openapi/ client/Assets/Generated/
git commit -m "feat(api): provided splice stock, and a first mutation guaranteed on both sides

POST /v1/ftue/splice-stock grants the tutorial's Vetch and Ember once,
after wave 2 and before the first splice - the sample stock
splice_confirm_spec 6 says the guided splice consumes, so the Founder is
never a parent and nothing the player earned is at stake.

The first splice mutates. It is a PARAMETER to spliceDistribution, read
by preview and commit from the same row count, so the forecast publishes
1 and the roll honours it. A branch in commit alone would have made the
published odds a claim about code again.

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

## Task 8: The wave-6 Pale — the resupply that makes the designed loss recoverable

"What this plan found the design missed" item 1. `waves_01_12` wave 6: *"The Wave Defeat screen grants a Pale, framed as a Warden resupply … If they somehow win, the Pale grant fires anyway."*

**Files:**
- Create: `services/api/src/ftue/pale.ts`
- Modify: `services/api/src/routes/wave.ts` (the submit handler's settlement, win and loss paths), `services/api/src/schemas.ts:147-158` (`WaveSubmitResponse.granted`)
- Test: `services/api/test/wave6-pale.test.ts` (new)

**Interfaces:**
- Produces: `grantWave6Pale(tx, serverId, playerId): Promise<CreatureDto | null>`; `WaveSubmitResponse.granted?: CreatureDto[]` — every creature this settlement minted (base stock, the Founder, the Pale), so Post-Wave and Wave Defeat render what arrived without a roster diff.

- [ ] **Step 1: Write the failing tests**

```ts
// wave6-pale.test.ts
it('a LOST wave 6 grants one Pale, once, and reports it', async () => {
  await setupPlayer(deps); await clearWave(2)
  const { issuanceId, seed } = await startLosingWave6()
  const res = await submit(issuanceId, buildLosingReplay(6, BigInt(seed)), randomUUID())
  const body = await res.json() as { result: string; reward?: unknown; granted?: Array<{ species: string; trait1: string }> }
  expect(body.result).toBe('Loss'); expect(body.reward).toBeUndefined()
  expect(body.granted?.map((c) => c.species)).toEqual(['Pale'])
  expect(body.granted?.[0]?.trait1).toBe('Chill')
  // A second loss grants nothing more.
  const again = await startLosingWave6(); const r2 = await submit(again.issuanceId, buildLosingReplay(6, BigInt(again.seed)), randomUUID())
  expect(((await r2.json()) as { granted?: unknown[] }).granted ?? []).toEqual([])
})

it('a WON wave 6 grants the Pale too - the beat is degraded, not broken', async () => { /* startWinning(6) with a Chill roster; expect granted to contain Pale */ })

it('after the grant, base stock can mint a Pale', async () => { /* readMarkers.wave6PaleGrantedAt set; baseStockPool now includes Pale */ })
```

- [ ] **Step 2: Run and watch them fail** — `pnpm --filter @broodline/api test wave6-pale` → FAIL.

- [ ] **Step 3: Implement**

```ts
// ftue/pale.ts
export const WAVE6_PALE = { species: 'Pale', trait1: 'Chill', tier1: 1, trait2: 'Carapace', tier2: 1, instinct: 'Vanguard' } as const

/** Once per player, on the first settlement of a wave-6 issuance - win or loss. */
export async function grantWave6Pale(tx: Tx, serverId: number, playerId: string): Promise<CreatureDto | null> {
  if (!(await setMarker(tx, serverId, playerId, 'wave6_pale_granted_at'))) return null
  // No cap check, for the tutorial-stock reason: this creature IS the answer
  // the screen just named, and refusing it roster_full teaches the opposite.
  const [row] = await tx.insert(creatures).values({
    serverId, playerId, generation: 1, ...WAVE6_PALE, isFounder: false, name: null, hpCurrent: creatureHp('Pale'),
  }).returning()
  return toCreatureDto(row!)
}
```

```ts
// routes/wave.ts — in the submit handler, inside the settlement transaction on BOTH paths:
//   Loss path (consumeAndRefuse / the Loss branch): after settle(), if issuance.waveId === 6 -> granted.push(await grantWave6Pale(...))
//   Win path: after grantWaveBaseStock -> same.
// `granted` is collected as CreatureDto[] from: grantWaveBaseStock (make it return the rows it minted, mapped),
// grantFounder, grantWave6Pale. Response: `granted: granted.length ? granted : undefined`.
```

`grantWaveBaseStock` changes its return from a count to `CreatureDto[]`; `wave-submit.test.ts`'s assertions on the count move to `.length`.

- [ ] **Step 4: Regenerate the contract, run everything**

```bash
./implementation/scripts/generate-contract.sh
pnpm --filter @broodline/api test && pnpm --filter @broodline/api typecheck
```

- [ ] **Step 5: Commit**

```bash
git add services/api openapi/ client/Assets/Generated/
git commit -m "feat(api): the wave-6 Pale fires on the first settlement, win or loss

waves_01_12 wave 6 grants a Pale from the Wave Defeat screen and fires
it anyway on a win. Nothing implemented it, and a first-hour roster with
no Chill lost wave 6 by design and then had no path to Chill except a
lucky roll - which Task 6 also removed. Once per player, on a marker.

WaveSubmitResponse gains `granted`: every creature the settlement minted,
so Post-Wave and Wave Defeat render what arrived without diffing the
roster.

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

## Task 9: `GET /v1/lineage` — the tree, alive and consumed

Design §5 beat 8. The Lineage View shows *both consumed parents still present in the tree*, so the route returns consumed rows, not just live ones.

**Files:**
- Create: `services/api/src/routes/lineage.ts`
- Modify: `services/api/src/schemas.ts`, `services/api/src/openapi.ts`, `services/api/src/app.ts`
- Test: `services/api/test/lineage-route.test.ts` (new)

**Interfaces:**
- Produces: `GET /v1/lineage` → `{ nodes: LineageNode[] }`, `LineageNode = { creatureId, species, generation, isFounder, name, parentA, parentB, consumedAt: string | null, pruned: boolean, mutated: boolean, trait1, tier1, trait2, tier2 }` — trait fields null on a pruned tombstone.

- [ ] **Step 1: Write the failing test**

```ts
it('returns live, consumed and pruned rows for the player, with parents and the mutation flag', async () => {
  // setup: starter pair, tutorial stock, one splice (mutated, per Task 7)
  const body = await lineage()
  const child = body.nodes.find((n) => n.generation === 2)!
  expect(child.mutated).toBe(true)
  const parents = body.nodes.filter((n) => n.creatureId === child.parentA || n.creatureId === child.parentB)
  expect(parents.length).toBe(2)
  expect(parents.every((p) => p.consumedAt !== null && p.trait1 !== null)).toBe(true)   // dead but WHOLE
  expect(body.nodes.filter((n) => n.isFounder).length).toBe(1)
})

it('never returns another player\'s rows', async () => { /* second player; ids disjoint */ })
```

- [ ] **Step 2: Run and watch it fail.**

- [ ] **Step 3: Implement**

```ts
// routes/lineage.ts — DERIVED AND WRITES NOTHING, like roster.
app.get('/v1/lineage', async (c) => {
  const session = await requireSession(c)
  const nodes = await withServer(deps.db, session.serverId, async (tx) => {
    const playerId = await loadPlayerId(tx, session.accountId)
    if (playerId === undefined) return null
    const rows = await tx.select({ c: creatures, mutated: splices.mutated }).from(creatures)
      .leftJoin(splices, and(eq(splices.serverId, creatures.serverId), eq(splices.childId, creatures.creatureId)))
      .where(and(eq(creatures.serverId, session.serverId), eq(creatures.playerId, playerId)))
      .orderBy(creatures.generation, creatures.acquiredAt)
    return rows.map(({ c, mutated }) => ({
      creatureId: c.creatureId, species: c.species, generation: c.generation, isFounder: c.isFounder,
      name: c.name, parentA: c.parentA, parentB: c.parentB,
      consumedAt: c.consumedAt?.toISOString() ?? null, pruned: c.pruned, mutated: mutated ?? false,
      trait1: c.trait1, tier1: c.tier1, trait2: c.trait2, tier2: c.tier2,
    }))
  })
  if (nodes === null) return fail('not_found', 'No player on this server for that account.')
  return c.json({ nodes })
})
```

`toCreatureDto` is **not** used here on purpose: it refuses a pruned row, and this route is the one reader that wants tombstones.

- [ ] **Step 4: Regenerate the contract; run; commit**

```bash
./implementation/scripts/generate-contract.sh && pnpm --filter @broodline/api test && pnpm --filter @broodline/api typecheck
git add services/api openapi/ client/Assets/Generated/
git commit -m "feat(api): GET /v1/lineage - live, consumed and pruned, with parents

The Lineage View's lesson is that both consumed parents are still in the
tree, so this is the one reader that wants dead rows. Tombstones come
back stripped, as 0005 stores them; consumed parents come back whole.

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

## Task 10: The coupling guard — `bundleVersion` may not fall below the shipped floor

Design §10.1. Followups §12 item 4: nothing asserts the two halves move together, which is how they came to disagree.

**Files:**
- Modify: `implementation/scripts/verify-unity-settings.sh`

- [ ] **Step 1: Add the check**

```bash
# --- The client floor coupling. config/bundles/<highest>/manifest.json's
# minimumClientVersion is what routes/sync.ts refuses clients below with 426;
# ProjectSettings.asset's bundleVersion is what the client sends. Phase 6
# shipped with the floor at 0.1.0 and the client at 0.2.0 - inert - and then
# found parseStart had made the OLD client unusable while the floor still
# told it it was current. The two halves moved in a8e5785; this is what
# keeps them moving together.
highest_bundle=$(ls -d config/bundles/*/ | sed 's#config/bundles/##; s#/##' | sort -V | tail -1)
floor=$(sed -n 's/.*"minimumClientVersion": *"\([0-9.]*\)".*/\1/p' "config/bundles/$highest_bundle/manifest.json")
client=$(sed -n 's/^  bundleVersion: *\([0-9.]*\).*/\1/p' "$P")
lowest=$(printf '%s\n%s\n' "$floor" "$client" | sort -V | head -1)
if [ -n "$floor" ] && [ -n "$client" ] && [ "$lowest" = "$floor" ]; then
  ok "client $client is not below bundle $highest_bundle's minimumClientVersion $floor"
else
  bad "client bundleVersion '$client' is below bundle $highest_bundle's minimumClientVersion '$floor' (or one is unreadable)" \
      "move ProjectSettings.asset bundleVersion and the manifest's floor TOGETHER - a player told they are current while every wave they start is refused is the failure this check exists for"
fi
```

- [ ] **Step 2: Run it, then weaken each half alone**

```bash
bash implementation/scripts/verify-unity-settings.sh                                   # expect ok
sed -i '' 's/bundleVersion: 0.3.0/bundleVersion: 0.2.0/' client/ProjectSettings/ProjectSettings.asset
bash implementation/scripts/verify-unity-settings.sh; git checkout client/ProjectSettings/ProjectSettings.asset   # expect FAIL
sed -i '' 's/"0.3.0"/"9.9.9"/' config/bundles/0.1.3/manifest.json
bash implementation/scripts/verify-unity-settings.sh; git checkout config/bundles/0.1.3/manifest.json             # expect FAIL
```

`tests.yml` already runs this script on every push, so the guard is in CI from this commit.

- [ ] **Step 3: Commit**

```bash
git add implementation/scripts/verify-unity-settings.sh
git commit -m "test(settings): the client floor and the shipped bundleVersion move together, or CI says so

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

## Task 11: The sweep — stranded creatures released, and `weakenings.md` row 7 constructible

Design §10.2. Two claimants: an issuance refused at checks 1–3 strands its creatures `committed_to` an expired-but-unsettled row, and Phase 5 design §4.3's retention split has never had a test because nothing swept.

**Files:**
- Create: `services/api/src/wave/sweep.ts`, `services/api/src/sweep-cli.ts`
- Modify: `services/api/src/wave/issuance.ts:130-215` (the expired-settlement moves ahead of check 1), `services/api/package.json` (`"sweep"` script)
- Test: `services/api/test/sweep.test.ts` (new), `services/api/test/wave-start.test.ts`, `services/api/test/weakenings.md` (row 7 updated)

**Interfaces:**
- Produces: `settleExpiredForPlayer(tx, serverId, playerId): Promise<number>`; `sweepRetention(db, serverId, now): Promise<{ expiredDeleted: number; consumedDeleted: number }>`; `pnpm --filter @broodline/api sweep`.

- [ ] **Step 1: Write the failing tests**

```ts
// sweep.test.ts
it('a wave_locked refusal no longer strands creatures committed to an expired issuance', async () => {
  // Start wave 7 with five creatures; age the issuance past expires_at by
  // hand; un-author wave 7 by publishing a bundle without it; call
  // wave/start(7) -> wave_locked. BEFORE this task the creatures stayed
  // committed forever. Now they are released before check 1 runs.
  await setupPlayer(deps); await clearWave(6)
  const five = await giveRoster(wave7RosterSpecs())          // seeded on purpose: this test is about committed_to, not the supply line
  expect((await startWave(7, five)).status).toBe(200)
  expect((await roster()).every((c) => c.committedTo !== null)).toBe(true)
  await withServer(t.db, 1, (tx) => tx.update(waveIssuances)
    .set({ expiresAt: new Date(Date.now() - 60_000) })
    .where(and(eq(waveIssuances.serverId, 1), isNull(waveIssuances.settledAt))))
  await publishBundleWithoutWave7()                          // a copy of 0.1.3 minus wave 7, activated, cache cleared
  expect((await startWave(7, five)).status).toBe(409)
  expect((await roster()).every((c) => c.committedTo === null)).toBe(true)
})

it('retention: expired rows go one hour past expires_at; consumed rows go 48h past issued_at', async () => {
  // Phase 5 design 4.3, and the 48-vs-24 argument row 7 demanded: a consumed
  // row written at 23:50 must still be countable at 00:10.
  const now = new Date('2026-09-15T00:10:00Z')
  await insertIssuance({ settlement: 'consumed', issuedAt: '2026-09-14T23:50:00Z' })      // kept
  await insertIssuance({ settlement: 'consumed', issuedAt: '2026-09-12T23:50:00Z' })      // deleted (>48h)
  await insertIssuance({ settlement: 'expired', expiresAt: '2026-09-14T22:00:00Z' })      // deleted (>1h past)
  await insertIssuance({ settlement: null,       expiresAt: '2026-09-14T22:00:00Z' })      // live-past-expiry: SETTLED then deleted
  const r = await sweepRetention(t.ownerDb, 1, now)
  expect(r).toEqual({ expiredDeleted: 2, consumedDeleted: 1 })
  expect(await replayCapCountFor(playerId, 7)).toBe(1)                                    // the 23:50 row still counts
})
```

- [ ] **Step 2: Run and watch them fail.**

- [ ] **Step 3: Implement**

```ts
// wave/sweep.ts
/** Settles this player's live-past-expiry issuance, releasing its creatures. Returns rows settled. */
export async function settleExpiredForPlayer(tx: Tx, serverId: number, playerId: string): Promise<number> {
  const rows = await tx.select().from(waveIssuances).where(and(
    eq(waveIssuances.serverId, serverId), eq(waveIssuances.playerId, playerId),
    isNull(waveIssuances.settledAt), sql`${waveIssuances.expiresAt} <= now()`))
  let n = 0
  for (const row of rows) if (await settle(tx, row, 'expired')) n++
  return n
}

const EXPIRED_GRACE_MS = 3_600_000            // one hour past expires_at - design 4.3
const CONSUMED_RETENTION_MS = 172_800_000     // 48h past issued_at - it is the replay counter

/** Runs as the OWNER role: 0003 grants broodline_app no DELETE on purpose. */
export async function sweepRetention(db: Db, serverId: number, now: Date) {
  return db.transaction(async (tx) => {
    // Live-past-expiry rows are settled first so their creatures are released
    // before the row that names them goes away.
    const stale = await tx.select().from(waveIssuances).where(and(
      eq(waveIssuances.serverId, serverId), isNull(waveIssuances.settledAt),
      sql`${waveIssuances.expiresAt} <= ${now}`))
    for (const row of stale) await settle(tx, row, 'expired')
    const expired = await tx.delete(waveIssuances).where(and(
      eq(waveIssuances.serverId, serverId), eq(waveIssuances.settlement, 'expired'),
      sql`${waveIssuances.expiresAt} <= ${new Date(now.getTime() - EXPIRED_GRACE_MS)}`)).returning({ id: waveIssuances.issuanceId })
    const consumed = await tx.delete(waveIssuances).where(and(
      eq(waveIssuances.serverId, serverId), eq(waveIssuances.settlement, 'consumed'),
      sql`${waveIssuances.issuedAt} <= ${new Date(now.getTime() - CONSUMED_RETENTION_MS)}`)).returning({ id: waveIssuances.issuanceId })
    return { expiredDeleted: expired.length, consumedDeleted: consumed.length }
  })
}
```

```ts
// wave/issuance.ts — issueWave: FIRST line of the function body, before the progress read:
await settleExpiredForPlayer(tx, serverId, playerId)
// and check 4 loses its `if (live.expiresAt > new Date()) return live; await settle(tx, live, 'expired')`
// branch: a live row found there is now always unexpired, and is returned.
```

`sweep-cli.ts`: the `migrate-cli.ts` shape — reads `DATABASE_URL` (owner), runs `sweepRetention` for server 1, prints the two counts. **Not scheduled** — `solo_execution` §10 has no scheduler trigger met. It is run by hand, and Task 22 records that as the residual.

- [ ] **Step 4: Run, then update `weakenings.md` row 7**

```bash
pnpm --filter @broodline/api test
```

Row 7 moves from *"owed against the retention sweep"* to the two tests above, with the weakening applied: change `CONSUMED_RETENTION_MS` to 24h and watch the 23:50/00:10 assertion redden. Record that in the row.

- [ ] **Step 5: Commit**

```bash
git add services/api
git commit -m "feat(api): the sweep - stranded creatures released, retention split tested at last

An issuance refused at checks 1-3 stranded its creatures committed_to an
expired row nothing would ever settle, because the only settling path was
check 4 of a start that check 3 had already refused. Expired settlement
now runs before check 1.

sweepRetention is the sweep design 4.3 assumed and 0003 recorded as owed:
expired rows one hour past expiry, consumed rows 48 hours past issue.
weakenings.md row 7 is a test now - a consumed row at 23:50 is still
countable at 00:10, and the 24h weakening reddens it. Owner-run CLI, not
scheduled: no scheduler trigger in solo_execution 10 is met.

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---
## Task 12: The self-hosted runner — the determinism gate runs for the first time

Design §9. A human task around a verification script. Do this **before the client tasks**, so every Unity change from Task 13 onward gets the EditMode suite and the IL2CPP diff in CI rather than on one laptop.

**Files:**
- Modify: `.github/workflows/determinism.yml` (uncomment the `push`, `pull_request` and `schedule` blocks; leave `workflow_dispatch`)

- [ ] **Step 1: Confirm this Mac is a runner candidate**

```bash
./implementation/scripts/verify-prereqs.sh
```

Expected: `.NET SDK`, `Unity 6000.6.0f1 with Mac Build Support (IL2CPP)`, Xcode, git-lfs all `ok`. Anything `FAIL` is fixed before registering — the job needs them.

- [ ] **Step 2: Restrict fork workflows — BEFORE registering**

Repository → Settings → Actions → General → *Fork pull request workflows from outside collaborators*: **Require approval for all outside collaborators.** `determinism.yml`'s own header says why: a self-hosted runner on a public repository executes fork PR code on this machine.

- [ ] **Step 3: Register the runner**

```bash
mkdir -p "$HOME/actions-runner" && cd "$HOME/actions-runner"
# Download the current macOS ARM64 runner tarball from the URL the settings page shows, then:
TOKEN=$(gh api -X POST repos/Sepand-Studio/BroodLine/actions/runners/registration-token --jq .token)
./config.sh --url https://github.com/Sepand-Studio/BroodLine --token "$TOKEN" --labels self-hosted,macOS --unattended
./svc.sh install && ./svc.sh start
gh api repos/Sepand-Studio/BroodLine/actions/runners --jq '.runners[] | {name, status, labels: [.labels[].name]}'
```

Expected: one runner, `status: online`, labels include `self-hosted` and `macOS` — the two `runs-on` names.

- [ ] **Step 4: Run the gate by hand once, on the current tip**

```bash
gh workflow run determinism.yml --ref phase_7
gh run watch "$(gh run list --workflow determinism.yml --limit 1 --json databaseId --jq '.[0].databaseId')"
```

Expected: **green**, on a cold cache in roughly ten minutes. This is the first time the IL2CPP half of the cross-runtime diff has ever executed in CI, and it runs against the phase that moved `SimVersion` and the lane model.

- [ ] **Step 5: Re-enable the triggers, and commit**

Uncomment the `push:`, `pull_request:` and `schedule:` blocks in `determinism.yml` verbatim — the path lists encode two rounds of review findings — and delete the "AUTOMATIC TRIGGERS ARE DISABLED" paragraph, replacing it with two lines naming the runner and the date.

```bash
git add .github/workflows/determinism.yml
git commit -m "ci: the determinism gate runs - a self-hosted macOS runner is registered

Queued-until-cancelled since Phase 4. It now executes on push, PR and
nightly; the first run was green against the Phase 7 engine change.
Fork PR workflows require approval - public repo, self-hosted runner.

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
git push -u origin phase_7      # so the runner picks up this push and every later one
```

---

## Task 13: The navigation shell — one root scene, five tabs, a session, a cache

Design §4, §2.6; `client_architecture` §7, §9. Everything after this task needs somewhere to live.

**Files:**
- Create: `client/Assets/Game/Shell/BootController.cs`, `client/Assets/Game/Shell/Session.cs`, `client/Assets/Game/Shell/ScreenHost.cs`, `client/Assets/Model/Progression.cs`, `client/Assets/Model/SnapshotStore.cs`, `client/Assets/Model/AuthStore.cs`, `client/Assets/UI/Shell/PanelSettings.asset`, `client/Assets/UI/Shell/Shell.uxml`, `client/Assets/UI/Shell/Shell.uss`, `client/Assets/UI/Shell/TabBar.cs`, `client/Assets/Editor/BootSceneBuilder.cs`, `client/Assets/Resources/BroodlineConfig.json`, `client/Assets/Scenes/Boot.unity` (built)
- Modify: `client/Assets/Game/Broodline.Game.asmdef` (references gain `Broodline.UI`, `Broodline.Net`, `Broodline.Model`, `Generated.Api`), `client/Assets/Model/PlayerSnapshot.cs` (the four new sync fields), `client/Assets/UI/Broodline.UI.asmdef` (no change to references — **still no `Broodline.Sim`**), `client/ProjectSettings/EditorBuildSettings.asset` (Boot first, Wave second, Benchmark and SampleScene removed)
- Test: `client/Assets/Game/Tests/Broodline.Game.Tests.asmdef` (new, Editor-only, like `Broodline.View.Tests`), `client/Assets/Game/Tests/ProgressionTests.cs`, `client/Assets/Game/Tests/SessionTests.cs`

**Interfaces:**
- Produces: `Progression.TabsFor(int highestWaveCleared, IReadOnlyDictionary<string,int> thresholds) : IReadOnlyList<string>` (pure); `Session` with `Task<PlayerSnapshot> ColdStartAsync()` and `BroodlineApiClient Api`; `SnapshotStore.Load()/Save(PlayerSnapshot)`; `AuthStore.Load()/Save(tokens)`; `ScreenHost.Show(VisualElement screen)` / `Push` / `Pop` / `ShowSheet`; `BootController` as the scene's single entry MonoBehaviour.

- [ ] **Step 1: Write the failing tests**

```csharp
// ProgressionTests.cs — client_architecture 9: the bar is a PURE FUNCTION of progress and thresholds.
[TestCase(0, new[] { "Map", "Ark" })]
[TestCase(1, new[] { "Map", "Ark" })]
[TestCase(2, new[] { "Map", "Ark", "Splice" })]
[TestCase(60, new[] { "Map", "Ark", "Splice" })]
[TestCase(61, new[] { "Map", "Ark", "Splice", "Lab", "Allies" })]
public void TabsFor_RevealsInFixedOrderAtTheBundleThresholds(int cleared, string[] expected)
{
    var thresholds = new Dictionary<string, int> { ["Map"] = 0, ["Ark"] = 0, ["Splice"] = 2, ["Lab"] = 61, ["Allies"] = 61 };
    CollectionAssert.AreEqual(expected, Progression.TabsFor(cleared, thresholds));
}

[Test]
public void TabsFor_WithNoThresholds_ShowsTheMinimumTwo()
{
    // A fresh install with no cached snapshot: correct for a new player, and
    // wrong for a reinstalling veteran only for the length of one sync.
    CollectionAssert.AreEqual(new[] { "Map", "Ark" }, Progression.TabsFor(0, new Dictionary<string, int>()));
}
```

```csharp
// SessionTests.cs — the cold-start sequence, against a stub HttpMessageHandler (the LoopGuardTests pattern).
[Test]
public async Task ColdStart_RendersTheCachedSnapshotBeforeSyncReturns()
{
    var store = new InMemorySnapshotStore(cached: SnapshotWith(highestWaveCleared: 2));
    var handler = new DelayedSyncHandler(SnapshotWith(highestWaveCleared: 6));
    var rendered = new List<int>();
    var s = new Session(ApiOver(handler), store, new InMemoryAuthStore(token: "t"), onSnapshot: sn => rendered.Add(sn.HighestWaveCleared));
    await s.ColdStartAsync();
    CollectionAssert.AreEqual(new[] { 2, 6 }, rendered);   // cached first, then the server's, no spinner
}

[Test]
public async Task ColdStart_WithNoAccount_CreatesAGuestAndPersistsTheTokens()
{
    // birthdateBand "adult", storefrontRegion "us-central1": the Age Gate is
    // deferred (design 12) and internal testers are adults. Says so in code.
    var handler = new RecordingHandler(createAccount: TokensJson("a1", "r1"), sync: SnapshotJson(highestWaveCleared: 0));
    var auth = new InMemoryAuthStore(token: null);
    var s = new Session(ApiOver(handler), new InMemorySnapshotStore(cached: null), auth, onSnapshot: _ => {});
    await s.ColdStartAsync();
    Assert.AreEqual("/v1/account", handler.Requests[0].Path);
    StringAssert.Contains("\"birthdateBand\":\"adult\"", handler.Requests[0].Body);
    Assert.IsNotNull(handler.Requests[0].Headers["idempotency-key"]);
    Assert.AreEqual("r1", auth.Saved.RefreshToken);
    Assert.AreEqual("Bearer a1", handler.Requests[1].Headers["Authorization"]);   // the sync that followed
}
```

- [ ] **Step 2: Run and watch them fail**

```bash
./implementation/scripts/run-unity-tests.sh EditMode
```

- [ ] **Step 3: Implement the pure pieces**

```csharp
// Model/Progression.cs
public static class Progression
{
    public static readonly string[] Order = { "Map", "Ark", "Splice", "Lab", "Allies" };
    public const string AlwaysA = "Map", AlwaysB = "Ark";

    public static IReadOnlyList<string> TabsFor(int highestWaveCleared, IReadOnlyDictionary<string, int> thresholds)
    {
        var tabs = new List<string>(5);
        foreach (var tab in Order)
        {
            bool always = tab == AlwaysA || tab == AlwaysB;
            if (always || (thresholds.TryGetValue(tab, out var at) && highestWaveCleared >= at)) tabs.Add(tab);
        }
        return tabs;
    }
}
```

`PlayerSnapshot` gains `IReadOnlyDictionary<string,int> Tabs`, `IReadOnlyList<WaveSummary> Waves`, `IReadOnlyList<TraitSummary> Traits`, `FtueFacts Ftue` (founderNamed, tutorialStockGranted, splices). `BroodlineClient.ColdStartAsync` maps them from the regenerated `SyncResponse`.

- [ ] **Step 4: The stores**

```csharp
// Model/SnapshotStore.cs — JSON under Application.persistentDataPath/snapshot.json via JsonUtility-compatible DTO.
// Model/AuthStore.cs   — tokens.json in the same directory. On iOS, after every Save:
//   UnityEngine.iOS.Device.SetNoBackupFlag(path)  and the file is created with
//   the default NSFileProtectionComplete class. THIS IS NOT KEYCHAIN.
//   client_architecture 7 says Keychain; Unity has none without a native
//   plugin. Recorded as owed in the design's 13 (Task 22). Internal testers.
```

- [ ] **Step 5: The session**

```csharp
// Game/Shell/Session.cs
public sealed class Session
{
    public BroodlineApiClient Api { get; }
    public PlayerSnapshot Snapshot { get; private set; }

    readonly BroodlineClient _client;
    readonly ISnapshotStore _snapshots;
    readonly IAuthStore _auth;
    readonly Action<PlayerSnapshot> _onSnapshot;
    readonly HttpClient _http;

    public Session(HttpClient http, string baseUrl, ISnapshotStore snapshots, IAuthStore auth, Action<PlayerSnapshot> onSnapshot)
    {
        _http = http; _snapshots = snapshots; _auth = auth; _onSnapshot = onSnapshot;
        Api = new BroodlineApiClient(http) { BaseUrl = baseUrl };
        _client = new BroodlineClient(baseUrl, http);
    }

    void SetBearer(string token)
    {
        _http.DefaultRequestHeaders.Remove("Authorization");
        _http.DefaultRequestHeaders.Add("Authorization", "Bearer " + token);
    }

    public async Task<PlayerSnapshot> ColdStartAsync()
    {
        var cached = _snapshots.Load();
        if (cached != null) { Snapshot = cached; _onSnapshot(cached); }      // render first
        var tokens = _auth.Load() ?? await CreateGuestAsync();                 // first launch
        SetBearer(tokens.AccessToken);
        try { Snapshot = await _client.ColdStartAsync(tokens.AccessToken, Application.version); }
        catch (ApiException e) when (e.StatusCode == 401)
        { tokens = await RefreshAsync(tokens); SetBearer(tokens.AccessToken); Snapshot = await _client.ColdStartAsync(tokens.AccessToken, Application.version); }
        _snapshots.Save(Snapshot); _onSnapshot(Snapshot);
        return Snapshot;
    }

    async Task<Tokens> CreateGuestAsync()
    {
        // The Age Gate is deferred to the first external build (design 12);
        // every internal tester is an adult, and the band is sent as such.
        var res = await Api.CreateAccountAsync(Guid.NewGuid().ToString(),
            new CreateAccountRequest { BirthdateBand = "adult", StorefrontRegion = "us-central1" });
        var t = new Tokens { AccessToken = res.AccessToken, RefreshToken = res.RefreshToken };
        _auth.Save(t); return t;
    }
}
```

`Application.version` is `bundleVersion` — the value Task 10's guard protects — sent as `x-client-version`, which `routes/sync.ts` compares against the floor.

- [ ] **Step 6: The shell UI and the controller**

`Shell.uxml`: a root `VisualElement` with `#top-bar` (currency header slot), `#screen-host` (flex-grow 1), `#sheet-layer` (absolute, hidden), `#tab-bar`. `Shell.uss`: safe-area padding via `--safe-top/--safe-bottom` set from `Screen.safeArea` in code; no fixed pixel positions (`client_architecture` §10). `PanelSettings.asset`: Scale Mode *Scale With Screen Size*, reference 390×844, match 0.5 — created by `BootSceneBuilder` with `AssetDatabase.CreateAsset`, not by hand.

```csharp
// UI/Shell/TabBar.cs — a VisualElement; `Render(IReadOnlyList<string> tabs, string active, Action<string> onSelect)`.
// Game/Shell/ScreenHost.cs — owns #screen-host and #sheet-layer; Show replaces, Push/Pop keep a back stack and hide the tab bar while depth > 0 (screen_inventory_v2 2), ShowSheet overlays and never pushes.
// Game/Shell/BootController.cs — MonoBehaviour: builds Session from Resources/BroodlineConfig.json's apiBaseUrl, wires TabBar to Progression.TabsFor(snapshot), starts the OutboxPump (Task 18), and hands control to Ftue (Task 17) after ColdStart.
```

`BootSceneBuilder.Build()` (`[MenuItem("Broodline/Build Boot Scene")]`, the `WaveSceneBuilder` pattern): new scene, one `GameObject("Shell")` with `UIDocument` (the PanelSettings and `Shell.uxml`) and `BootController`, saved to `Assets/Scenes/Boot.unity`, and `EditorBuildSettings.scenes` set to `[Boot, Wave]`.

`Resources/BroodlineConfig.json`: `{ "apiBaseUrl": "http://127.0.0.1:8080" }` in the repo; Task 20 overwrites it at build time.

- [ ] **Step 7: Build the scene, run the tests**

```bash
"/Applications/Unity/Hub/Editor/6000.6.0f1/Unity.app/Contents/MacOS/Unity" -batchmode -quit -projectPath "$(pwd)/client" -executeMethod BootSceneBuilder.Build -logFile - | tail -3
./implementation/scripts/run-unity-tests.sh EditMode
bash implementation/scripts/verify-unity-settings.sh
```

Expected: PASS; the settings script still ok (Boot scene added nothing it checks).

- [ ] **Step 8: Commit**

```bash
git add client/Assets/Game client/Assets/Model client/Assets/UI/Shell client/Assets/Editor/BootSceneBuilder.cs client/Assets/Resources client/Assets/Scenes/Boot.unity* client/ProjectSettings/EditorBuildSettings.asset
git commit -m "feat(client): the navigation shell - root scene, five tabs, session, cache

client_architecture 9 as written: one persistent root scene, Wave
Defense additive, five tabs revealed as a pure function of campaign
progress and bundle thresholds, bottom sheets an overlay layer. UI
Toolkit throughout - design 4 - and the shell lives in Broodline.Game so
Broodline.UI still references no engine.

Cold start renders the cached snapshot before /v1/sync returns. A first
launch creates a guest account as an adult: the Age Gate is deferred to
the first external build and the code says so. Tokens are on disk with
the no-backup flag, NOT in Keychain - Unity has none without a plugin,
and the design's 13 now records that as owed.

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

## Task 14: Shared components — the atoms, built once

Design §5.1; `screen_inventory_v2` §11. The creature card appears on at least nine screens and the trait pip is the most-repeated element in the app.

**Files:**
- Create: `client/Assets/UI/Components/{CreatureCard,TraitPip,CurrencyHeader,TimerChip,ConfirmDialog}.cs`, matching `.uxml`/`.uss`
- Test: `client/Assets/UI/Tests/ComponentTests.cs`

**Interfaces:**
- Produces: `new CreatureCard().Bind(CreatureDto c, IReadOnlyDictionary<string,string> counters)`; `new TraitPip().Bind(string trait, int? tier, string counters)`; `new CurrencyHeader().Bind(IReadOnlyDictionary<string,int> balances)`; `new TimerChip().Bind(TimeSpan remaining)`; `ConfirmDialog.Standard(SpliceDialog d, Action confirm, Action cancel)` and `ConfirmDialog.Named(SpliceDialog d, ...)` — the two levels; **`Named` has no dismiss-by-tap-outside and no default button**.

- [ ] **Step 1: Write the failing tests** — EditMode, no scene: a `VisualElement` tree is constructible in the Editor.

```csharp
[Test]
public void CreatureCard_ShowsTwoPipsWithCoverageAndTheFounderName()
{
    var card = new CreatureCard();
    card.Bind(Creature("Vetch", gen: 1, name: "Ash", founder: true, trait1: "Taunt", tier1: 1, trait2: "Carapace", tier2: 1), Counters());
    Assert.AreEqual(2, card.Query<TraitPip>().ToList().Count);           // two, not four - screen_inventory 3
    StringAssert.Contains("Ash", card.Q<Label>("name").text);
    Assert.IsTrue(card.ClassListContains("founder"));
}

[Test]
public void TraitPip_ReadsTierAsRomanAndNamesWhatItCounters()
{
    var pip = new TraitPip(); pip.Bind("Chill", 3, "Courser");
    Assert.AreEqual("Chill III", pip.Q<Label>("label").text);
    StringAssert.Contains("Courser", pip.tooltip);
}

[Test]
public void TraitPip_WithNullTier_IsAberrantNotZero()
{
    // data_model 2: null is an Aberrant, never "tier 0".
    var pip = new TraitPip(); pip.Bind("Chill", null, "Courser");
    Assert.AreEqual("Chill", pip.Q<Label>("label").text);
    Assert.IsTrue(pip.ClassListContains("aberrant"));
}

[Test]
public void NamedConfirm_HasNoDefaultButtonAndCannotBeDismissedOutside()
{
    var d = ConfirmDialog.Named(new SpliceDialog { Title = "Consume Ash?", ConfirmLabel = "Consume Ash", CancelLabel = "Keep Ash", ConfirmIsPrimary = false, Suppressible = false }, () => {}, () => {});
    Assert.IsFalse(d.Q<Button>("confirm").ClassListContains("primary"));
    Assert.IsNull(d.Q("scrim").GetCallbackCount<ClickEvent>());          // no dismiss-on-scrim registered
}
```

- [ ] **Step 2: Run and watch fail; Step 3: implement each as a `VisualElement` subclass with `[UxmlElement]`, loading its UXML via `Resources.Load<VisualTreeAsset>`; Step 4: run**

```bash
./implementation/scripts/run-unity-tests.sh EditMode
```

- [ ] **Step 5: Commit**

```bash
git add client/Assets/UI/Components client/Assets/UI/Tests/ComponentTests.cs
git commit -m "feat(ui): the five shared components, built once

Creature card, trait pip, currency header, timer chip and the two-level
confirm dialog - screen_inventory_v2 11. The pip renders a null tier as
Aberrant and never as zero; the named confirm has no default button and
no scrim dismiss, because bible 3.3's whole argument is that the name
stops a player and a tap-anywhere would let them past it.

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

## Task 15: Presentation over Phase 6's headless models — Region, Roster, Splice Chamber, Deploy

Design §2.1, §5.1. The four `*ScreenModel` classes stay exactly what they are; each gains a view that binds it and **rewrites none of its copy**.

**Files:**
- Create: `client/Assets/UI/Screens/{RegionView,RosterView,SpliceChamberView,DeployView}.cs` and `.uxml`/`.uss`
- Test: `client/Assets/UI/Tests/ScreenBindingTests.cs`

**Interfaces:**
- Produces: `RegionView.Bind(RegionScreenModel m, Action<int> onClaim)`; `RosterView.Bind(RosterScreen r, Action<CreatureDto> onSelect)`; `SpliceChamberView.Bind(SpliceScreenModel m, ISet<Guid> lockedOut, Action onSplice)`; `DeployView.Bind(DeployScreenModel m, Action onStart)`.

- [ ] **Step 1: Write the failing tests** — the property that matters: **the view shows the model's sentences verbatim.**

```csharp
[Test]
public void SpliceChamberView_ShowsTheModelsDestructionNoticeAndCtaVerbatim()
{
    var m = SpliceScreen.Build(Named("Ash"), Unnamed("Ember", 1), forecast, charges: 3);
    var v = new SpliceChamberView(); v.Bind(m, lockedOut: new HashSet<Guid>(), onSplice: () => {});
    Assert.AreEqual(m.DestructionNotice, v.Q<Label>("destruction-notice").text);
    Assert.AreEqual(m.CtaLabel, v.Q<Button>("cta").text);                    // "Splice — consumes both parents."
    Assert.AreEqual(m.CoverageWarning ?? "", v.Q<Label>("coverage-warning").text);
    // The forecast table renders the server's numbers and computes none - client_architecture 9.1.
    Assert.AreEqual(m.Forecast.Combat2.Count, v.Query(className: "forecast-row").ToList().Count);
}

[Test]
public void SpliceChamberView_LockedOutParents_AreVisiblyDisabled()
{
    // splice_confirm_spec 6: "the named Founder is visibly locked out".
    var founder = Named("Ash");
    var v = new SpliceChamberView(); v.Bind(model, lockedOut: new HashSet<Guid> { founder.CreatureId }, onSplice: () => {});
    Assert.IsFalse(v.Q<CreatureCard>(founder.CreatureId.ToString()).enabledSelf);
    Assert.IsTrue(v.Q<CreatureCard>(founder.CreatureId.ToString()).ClassListContains("locked-out"));
}

[Test]
public void DeployView_RefusesToEnableStartBelowTheFloorOrAboveTheCap()
{
    // routes/wave.ts's DEPLOYMENT_FLOOR (1) and cap (5), which DeployScreen already owns.
    Assert.IsFalse(BindWith(slots: 0).Q<Button>("start").enabledSelf);
    Assert.IsTrue(BindWith(slots: 1).Q<Button>("start").enabledSelf);
    Assert.IsFalse(BindWith(slots: 6).Q<Button>("start").enabledSelf);
}

[Test]
public void RosterView_ANeverLoadedRoster_ShowsTheIncompleteNoticeNotAnEmptyList()
{
    // Phase 6's fix 7c95cd9: a roster that never loaded must not claim to be complete.
    var r = new RosterScreen(); var v = new RosterView(); v.Bind(r, _ => {});
    StringAssert.Contains(r.IncompleteNotice, v.Q<Label>("notice").text);
}
```

- [ ] **Step 2–4: fail, implement, pass.** Each view is a `VisualElement` that loads its UXML and binds labels/buttons from the model; **no string literals for player-facing copy in any `*View.cs`** — the copy lives in the models Phase 6 tested.

- [ ] **Step 5: Prove the binding is verbatim**

Change `SpliceScreen.Cta` to `"Begin Splice"`; `SpliceConfirmTests.Cta_StatesItsCost` reddens **and** `SpliceChamberView_ShowsTheModelsDestructionNoticeAndCtaVerbatim` stays green — the view followed the model, which is the property. Restore.

- [ ] **Step 6: Commit**

```bash
git add client/Assets/UI/Screens client/Assets/UI/Tests/ScreenBindingTests.cs
git commit -m "feat(ui): presentation over Phase 6's four headless screens

Region, Roster, Splice Chamber and Deploy get views that bind the models
Phase 6 tested and rewrite none of their copy - the destruction notice,
the CTA that states its cost, the coverage warning arrive on screen as
the tests already pin them. Locked-out parents are visibly disabled;
Start obeys the floor and the cap the model already owns.

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

## Task 16: Wave Defense as a hosted scene — the HUD rebuilt, Post-Wave, Wave Defeat

Design §4, §5.1. The IMGUI HUD goes; the shell loads `Wave.unity` additively with a real deployment and reads back a report.

**Files:**
- Create: `client/Assets/Game/Shell/WaveHost.cs`, `client/Assets/Game/Shell/WaveReport.cs`, `client/Assets/UI/Screens/{WaveHudView,PostWaveView,WaveDefeatView}.cs` + `.uxml`
- Modify: `client/Assets/Game/WaveRunner.cs` (takes `WaveDef`, deployment and seed from `WaveHost` instead of constants; keeps its artifact writer behind a flag), `client/Assets/Editor/WaveSceneBuilder.cs` (adds a `UIDocument` for the HUD)
- Delete: `client/Assets/View/WaveHud.cs`
- Test: `client/Assets/Game/Tests/WaveReportTests.cs`, `client/Assets/UI/Tests/WaveScreensTests.cs`

**Interfaces:**
- Produces: `WaveHost.RunAsync(int waveId, CreatureSpec[] deployment, ulong seed, bool inputEnabled) : Task<WaveReport>`; `WaveReport { Result, Ticks, IntegrityRemaining, byte[] ReplayBytes, IReadOnlyList<BreachSummary> Breaches }`; `BreachSummary { RaiderType: string, Counter: string, Access, Coverage, Placement: bool }` — **plain data, built in `Broodline.Game` from the engine's `Outcome` and the snapshot's trait table**, handed to `Broodline.UI`; `WaveHudView.Bind(Func<HudSnapshot> read)`; `PostWaveView.Bind(WaveSubmitResponse res, IReadOnlyList<CreatureDto> granted, Action next)`; `WaveDefeatView.Bind(WaveReport r, IReadOnlyList<CreatureDto> granted, Action retry)`.

- [ ] **Step 1: Write the failing tests**

```csharp
// WaveReportTests.cs (Broodline.Game.Tests) — the diagnosis names the raider AND the counter, bible 4.11.
[Test]
public void FromOutcome_NamesTheRaiderAndTheTraitThatWouldHaveAnsweredIt()
{
    var runner = new SimRunner(WaveDef.Wave6(), WaveDef.Wave6().Lane, GoldenTests_DeploymentWithoutChill(), 6);
    while (runner.Step()) { }
    var traits = new[] { new TraitSummary { Id = "Chill", Counters = "Courser" }, new TraitSummary { Id = "Taunt", Counters = "Lash" } };
    var report = WaveReport.From(runner, traits);
    Assert.AreEqual("Loss", report.Result);
    Assert.AreEqual("Courser", report.Breaches[0].RaiderType);
    Assert.AreEqual("Chill", report.Breaches[0].Counter);                 // from the bundle's trait table, not the engine
    Assert.IsFalse(report.Breaches[0].Access);                            // no Chill on the roster
}

// WaveScreensTests.cs (Broodline.UI.Tests)
[Test]
public void WaveDefeat_NamesCourserAndChill_AndShowsTheResupply()
{
    var v = new WaveDefeatView();
    v.Bind(LossReportWith(raider: "Courser", counter: "Chill"), granted: new[] { Creature("Pale", trait1: "Chill") }, retry: () => {});
    StringAssert.Contains("Courser", v.Q<Label>("headline").text);
    StringAssert.Contains("Chill", v.Q<Label>("headline").text);
    StringAssert.Contains("Pale", v.Q<Label>("granted").text);
    Assert.IsTrue(v.Q<Button>("retry").enabledSelf);                     // free retry, immediately
}

[Test]
public void PostWave_ShowsTheRewardAndEveryCreatureThatArrived()
{
    var v = new PostWaveView();
    v.Bind(new WaveSubmitResponse { Result = "Win", Reward = new Reward { Currency = "shards", Amount = 150 } }, granted: new[] { Creature("Hollow", founder: true) }, next: () => {});
    StringAssert.Contains("150", v.Q<Label>("reward").text);
    Assert.AreEqual(1, v.Query<CreatureCard>().ToList().Count);
}
```

- [ ] **Step 2: Run and watch fail.**

- [ ] **Step 3: Implement**

```csharp
// Game/Shell/WaveReport.cs
public static WaveReport From(SimRunner r, IReadOnlyList<TraitSummary> traits)
{
    var o = r.Outcome;
    var breaches = new List<BreachSummary>(o.BreachCount);
    foreach (var b in o.Breaches)
    {
        string raider = b.Type.ToString();
        // The counter comes from the BUNDLE's trait table (traits[].counters),
        // the same source the Codex sheet reads - not from Stats.CounterFor,
        // so Broodline.UI needs no engine and the copy follows content.
        string counter = traits.FirstOrDefault(t => t.Counters == raider)?.Id ?? "";
        breaches.Add(new BreachSummary { RaiderType = raider, Counter = counter, Access = b.Access, Coverage = b.Coverage, Placement = b.Placement });
    }
    return new WaveReport { Result = o.Result.ToString(), Ticks = o.Ticks, IntegrityRemaining = o.IntegrityRemaining, ReplayBytes = r.SerializeRecord(), Breaches = breaches };
}
```

`WaveHost`: `SceneManager.LoadSceneAsync("Wave", LoadSceneMode.Additive)`, finds `WaveRunner`, calls `runner.Configure(WaveDef.ForId(waveId), deployment, seed, inputEnabled)`, awaits `runner.Done`, builds the report, unloads the scene, returns. `WaveRunner.Start` no longer constructs wave 6 by itself; the Editor-only capture path (Task 2's) keeps working through a `[SerializeField] bool standaloneCapture` that `WaveSceneBuilder` leaves **true** in the scene asset and `WaveHost` sets false before play — so the tracked-capture procedure is unchanged.

`WaveHudView`: integrity, live tick, per-body bars (world→panel via `RuntimePanelUtils.CameraTransformWorldToPanel`), the Rally affordance — everything `WaveHud.cs` drew, in UXML, with `DrawDebug` dropped. Delete `WaveHud.cs`.

- [ ] **Step 4: Run; and re-verify the capture path still proves something**

```bash
./implementation/scripts/run-unity-tests.sh EditMode
dotnet test Broodline.sln --nologo      # 0 failed - no engine change, captures still current
```

Play `Wave.unity` standalone in the Editor once and confirm `replay.bin` is still written — the Task 2 procedure must survive this task.

- [ ] **Step 5: Commit**

```bash
git add client/Assets/Game client/Assets/UI/Screens client/Assets/UI/Tests client/Assets/View client/Assets/Editor/WaveSceneBuilder.cs client/Assets/Scenes/Wave.unity*
git commit -m "feat(client): Wave Defense hosted by the shell; the HUD in UI Toolkit; Post-Wave and Wave Defeat

WaveHud.cs was IMGUI on purpose - 'a debug surface the real Wave Defense
screen replaces wholesale' - and this is the replacement. The shell loads
Wave.unity additively with a real deployment and reads back a WaveReport:
plain data built in Broodline.Game from the Outcome and the bundle's
trait table, so Broodline.UI still references no engine and the defeat
copy names the counter the CONTENT authored.

Wave Defeat names the raider and the trait that would have answered it
- bible 4.11 - shows the Pale that arrived, and offers the free retry.
The standalone capture path survives behind a flag the scene keeps on.

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---
## Task 17: The first hour — the beat machine and the five screens it needs

Design §5. The beat is **derived**, never stored: a pure function of the snapshot and the roster, tested as a table.

**Files:**
- Create: `client/Assets/Game/Ftue/Ftue.cs`, `client/Assets/Game/Ftue/FtueDirector.cs`, `client/Assets/UI/Screens/{FounderNamingView,SpliceRevealView,LineageView,CampaignSelectView,CodexSheet}.cs` + `.uxml`
- Modify: `client/Assets/Game/Shell/BootController.cs` (hands control to `FtueDirector` after cold start)
- Test: `client/Assets/Game/Tests/FtueTests.cs`, `client/Assets/UI/Tests/FirstHourScreensTests.cs`

**Interfaces:**
- Produces: `enum Beat { ColdOpen, NameFounder, SecondWave, GuidedSplice, Reveal, Lineage, Done }`; `Ftue.Derive(PlayerSnapshot s, IReadOnlyList<CreatureDto> roster) : Beat` (pure); `FtueDirector.RunAsync()` which walks the beats, calling `WaveHost`, `Session.Api` and `ScreenHost`; the five views' `Bind` methods below.

- [ ] **Step 1: Write the failing derivation table**

```csharp
// FtueTests.cs — every row is a beat from bible 9.2, and the inputs are the facts /v1/sync and /v1/roster return.
static PlayerSnapshot S(int cleared, bool founderNamed = false, bool stock = false, int splices = 0) => ...;

[Test] public void FreshAccount_IsTheColdOpen()          => Assert.AreEqual(Beat.ColdOpen,     Ftue.Derive(S(0), Roster("Vetch", "Ember")));
[Test] public void Wave1Cleared_UnnamedFounder_NamesIt()  => Assert.AreEqual(Beat.NameFounder,  Ftue.Derive(S(1), Roster("Vetch", "Ember", Founder("Hollow", named: false))));
[Test] public void Wave1Cleared_NamedFounder_SecondWave() => Assert.AreEqual(Beat.SecondWave,   Ftue.Derive(S(1, founderNamed: true), Roster("Vetch", "Ember", Founder("Hollow", named: true))));
[Test] public void Wave2Cleared_NoSplice_GuidedSplice()   => Assert.AreEqual(Beat.GuidedSplice, Ftue.Derive(S(2, true), Any()));
[Test] public void Wave2Cleared_OneSplice_Lineage()       => Assert.AreEqual(Beat.Lineage,      Ftue.Derive(S(2, true, stock: true, splices: 1), Any()));
[Test] public void Wave6Cleared_Done()                    => Assert.AreEqual(Beat.Done,         Ftue.Derive(S(6, true, true, 1), Any()));

[Test]
public void SkippedNaming_IsStillNameFounderUntilWave2_ThenNeverAgain()
{
    // bible 3.3: the skip path with a good default. Skipping does not name
    // the creature server-side; the beat is offered once per session start
    // while wave 2 is uncleared, and the Roster is where renaming lives after.
    Assert.AreEqual(Beat.NameFounder, Ftue.Derive(S(1), Roster("Vetch", "Ember", Founder("Hollow", named: false))));
    Assert.AreEqual(Beat.GuidedSplice, Ftue.Derive(S(2), Roster("Vetch", "Ember", Founder("Hollow", named: false), "Vetch")));
}
```

- [ ] **Step 2: Run and watch fail.**

- [ ] **Step 3: The derivation**

```csharp
// Game/Ftue/Ftue.cs — PURE. No Unity types.
public static class Ftue
{
    public static Beat Derive(PlayerSnapshot s, IReadOnlyList<CreatureDto> roster)
    {
        bool hasFounder = roster.Any(c => c.IsFounder);
        if (s.HighestWaveCleared < 1) return Beat.ColdOpen;
        if (s.HighestWaveCleared < 2) return hasFounder && !s.Ftue.FounderNamed ? Beat.NameFounder : Beat.SecondWave;
        if (s.Ftue.Splices == 0) return Beat.GuidedSplice;
        if (s.HighestWaveCleared < 6) return Beat.Lineage;     // beat 8 ends the session; wave 6 is session two - screen_inventory 9
        return Beat.Done;
    }
}
```

`Reveal` is not derived — it is the screen that follows a commit inside the same session and is shown by the director, then falls through to `Lineage` on the next derivation.

- [ ] **Step 4: The director**

```csharp
// Game/Ftue/FtueDirector.cs — walks beats; each case is one screen or one hosted wave.
case Beat.ColdOpen:
    // "A wave is already coming when the app opens." Deploy is pre-filled with
    // the pair in pockets 0 and 2 (WaveContentTests' fixture) and shown for
    // one tap: place them. Then wave 1, submit through the outbox, Post-Wave.
    var deploy = DeployScreen.Build(waveId: 1, roster.Take(2).Select((c, i) => (c, pocket: i * 2)));
    await _host.Show(new DeployView().Bind(deploy, onStart: ...));
    var start = await _api.StartWaveAsync(deploy.ToRequest());
    var report = await _waves.RunAsync(1, ToSpecs(start.Deployment), ulong.Parse(start.Seed), inputEnabled: true);
    var submitted = await _outbox.SubmitWaveAsync(start.IssuanceId, report.ReplayBytes);     // Task 18
    await _host.Show(new PostWaveView().Bind(submitted, submitted.Granted, next: ...));
    break;
case Beat.NameFounder:
    var founder = roster.First(c => c.IsFounder);
    await _host.Show(new FounderNamingView().Bind(founder, defaultName: FounderNamingView.DefaultFor(founder),
        onName: async name => await _outbox.NameCreatureAsync(founder.CreatureId, name), onSkip: ...));
    break;
case Beat.SecondWave:   // as ColdOpen, wave 2, three creatures - Vetch p0, Ember p2, Hollow p4
case Beat.GuidedSplice:
    if (!snapshot.Ftue.TutorialStockGranted) await _outbox.FtueSpliceStockAsync();
    var provided = (await _api.RosterAsync()).Creatures.Where(c => !c.IsFounder && c.Generation == 1 && c.AcquiredAfter(stockGrant)).Take(2);
    var lockedOut = roster.Where(c => c.IsFounder).Select(c => c.CreatureId).ToHashSet();     // splice_confirm_spec 6
    var preview = await SpliceScreen.PreviewAsync(_api, provided[0], provided[1], locked: default);
    await _host.Show(new SpliceChamberView().Bind(SpliceScreen.Build(provided[0], provided[1], preview, charges), lockedOut, onSplice: ...));
    // the dialogs: Standard, then (never here) Named - the components decide
    var committed = await _outbox.SpliceCommitAsync(...);
    await _host.Show(new SpliceRevealView().Bind(committed, provided[0], provided[1], next: ...));
    goto case Beat.Lineage;
case Beat.Lineage:
    await _host.Show(new LineageView().Bind(await _api.LineageAsync(), highlight: roster.First(c => c.IsFounder).CreatureId));
    break;
case Beat.Done:
    _host.ShowTabs(); break;                                                 // the ordinary app
```

- [ ] **Step 5: The screens, with their tests**

```csharp
// FirstHourScreensTests.cs
[Test]
public void FounderNaming_OffersADefaultAndASkip_AndSubmitsTrimmed()
{
    var v = new FounderNamingView(); string named = null; bool skipped = false;
    v.Bind(Founder("Hollow"), defaultName: "Hollow", onName: n => named = n, onSkip: () => skipped = true);
    Assert.AreEqual("Hollow", v.Q<TextField>("name").value);                 // bible 3.3: a good default
    v.Q<TextField>("name").value = "  Ash "; v.Q<Button>("confirm").SendClick();
    Assert.AreEqual("Ash", named);
    v.Q<Button>("skip").SendClick(); Assert.IsTrue(skipped);
}

[Test]
public void SpliceReveal_CelebratesTheMutationAndKeepsTheConsumptionLine()
{
    // splice_confirm_spec 5: "parents are consumed and their traits live on
    // in the pedigree" - a confirmation now, not a disclosure.
    var v = new SpliceRevealView(); v.Bind(CommitWith(mutated: true), parentA: Named("Ash"), parentB: Unnamed("Ember", 1), next: () => {});
    Assert.IsTrue(v.ClassListContains("mutated"));
    StringAssert.Contains("live on", v.Q<Label>("consumption").text);
}

[Test]
public void Lineage_ShowsConsumedParentsUnderTheChild_AndMarksTheFounder()
{
    var nodes = new[] { Founder("Hollow", id: F), Consumed("Vetch", id: A), Consumed("Ember", id: B), Child(id: C, parentA: A, parentB: B, mutated: true) };
    var v = new LineageView(); v.Bind(new LineageResponse { Nodes = nodes }, highlight: F);
    Assert.AreEqual(4, v.Query(className: "node").ToList().Count);           // nobody is missing - individuals are consumed, the record survives
    Assert.IsTrue(v.Q(A.ToString()).ClassListContains("consumed"));
    Assert.IsTrue(v.Q(F.ToString()).ClassListContains("founder"));
    Assert.IsTrue(v.Q(C.ToString()).ClassListContains("mutated"));
    Assert.AreEqual(2, v.Query(className: "generation-row").ToList().Count);  // two generations
}

[Test]
public void CampaignSelect_LocksEverythingPastTheNextWave()
{
    var v = new CampaignSelectView(); v.Bind(waves: Ids(1, 2, 6, 7), highestWaveCleared: 2, onPick: _ => {});
    Assert.IsTrue(v.Q("wave-6").enabledSelf);
    Assert.IsFalse(v.Q("wave-7").enabledSelf);                                 // issueWave's forward branch: only the next authored wave
}

[Test]
public void CodexSheet_ListsEveryTraitWithWhatItCounters()
{
    var v = new CodexSheet(); v.Bind(new[] { Trait("Chill", "Pale", "Courser"), Trait("Carapace", "Vetch", null) });
    StringAssert.Contains("Courser", v.Q("Chill").Q<Label>("counters").text);
    StringAssert.Contains("nothing", v.Q("Carapace").Q<Label>("counters").text.ToLower());   // answers nothing, and that is legal
}
```

`FounderNamingView.DefaultFor(c)` returns the species name — the "sensible default" bible §3.3 asks for; the Roster rename lives in `RosterView` (Task 15) from a card's context action.

- [ ] **Step 6: Run everything; play it in the Editor against a local `api`**

```bash
./implementation/scripts/run-unity-tests.sh EditMode
# In one shell: the api against Testcontainers is not a dev server. Use the
# repo's dev script against a local Postgres 16 with 0.1.3 published to a
# LocalBundleStore, then press Play in Boot.unity and walk beats 1-8.
```

Play through once. **Every creature on the closing Lineage View must have arrived through a response** — that is the server-side test in Task 22's `ftue.test.ts`, and this is the eyes-on version.

- [ ] **Step 7: Commit**

```bash
git add client/Assets/Game/Ftue client/Assets/Game/Tests client/Assets/UI/Screens client/Assets/UI/Tests client/Assets/Game/Shell/BootController.cs
git commit -m "feat(client): the first hour - eight beats derived, never stored

Ftue.Derive is a pure function of the sync snapshot and the roster,
tested as a table against bible 9.2. The director walks it: the pair
placed and wave 1 fought within a tap of launch, the Hollow named with a
default and a skip, wave 2, the provided pair spliced with every Founder
visibly locked out, the mutation celebrated, and the tree with both
consumed parents still in it - the image the session exists to leave.

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

## Task 18: The outbox — keys at action time, ordered, flushed on foreground, expired at 24h

Design §8; `client_architecture` §8, implemented as specified. Also corrects `BroodlineClient.cs`'s claim that this arrived in Phase 6.

**Files:**
- Create: `client/Assets/Net/Outbox.cs`, `client/Assets/Net/OutboxStore.cs`, `client/Assets/Net/OutboxClient.cs`, `client/Assets/Game/Shell/OutboxPump.cs`, `client/Assets/Net/Tests/Broodline.Net.Tests.asmdef`, `client/Assets/Net/Tests/OutboxTests.cs`
- Modify: `client/Assets/Net/BroodlineClient.cs:15-18` (the comment)

**Interfaces:**
- Produces: `Outbox` (pure): `Enqueue(OutboxEntry)`, `OutboxEntry Peek()`, `OutboxEntry Next(DateTime now)`, `Ack(string key)`, `Fail(string key, DateTime now)`, `IReadOnlyList<OutboxEntry> Expire(DateTime now)`; `OutboxEntry { Key, Op, Body (bytes), CreatedAt, Attempts, NotBefore }`; `OutboxClient.SubmitWaveAsync / SpliceCommitAsync / ClaimNodeAsync / NameCreatureAsync / FtueSpliceStockAsync` — each **generates the key first, persists the entry, then attempts**; `OutboxPump : MonoBehaviour` flushing on `OnApplicationFocus(true)` and when `Application.internetReachability` changes from `NotReachable`.

- [ ] **Step 1: Write the failing tests** — pure, EditMode, no network.

```csharp
[Test]
public void TheKeyIsGeneratedWhenTheActionIsTaken_AndNeverChanges()
{
    var box = new Outbox();
    var e = OutboxEntry.For("wave/submit", body, now: T0);
    box.Enqueue(e);
    Assert.AreEqual(e.Key, box.Next(T0).Key);
    box.Fail(e.Key, T0);
    Assert.AreEqual(e.Key, box.Next(T0.AddMinutes(5)).Key);      // a retry sends the IDENTICAL key
}

[Test]
public void EntriesDrainOldestFirst_AndALaterOneNeverOvertakesAFailedEarlierOne()
{
    // "A splice that depends on a wave reward must not overtake it."
    var box = new Outbox();
    box.Enqueue(OutboxEntry.For("wave/submit", b1, T0)); box.Enqueue(OutboxEntry.For("splice/commit", b2, T0.AddSeconds(1)));
    var first = box.Next(T0.AddSeconds(2)); box.Fail(first.Key, T0.AddSeconds(2));
    Assert.IsNull(box.Next(T0.AddSeconds(3)));                    // head is backing off; nothing behind it goes
    Assert.AreEqual(first.Key, box.Next(T0.AddSeconds(60)).Key);
}

[Test]
public void BackoffIsExponentialAndCapped()
{
    var e = OutboxEntry.For("x", b, T0); var box = new Outbox(); box.Enqueue(e);
    var delays = new List<double>();
    for (int i = 0; i < 6; i++) { var n = box.Next(T0.AddHours(1)); box.Fail(n.Key, T0.AddHours(1)); delays.Add((box.Peek().NotBefore - T0.AddHours(1)).TotalSeconds); }
    CollectionAssert.AreEqual(new double[] { 2, 4, 8, 16, 32, 60 }, delays);   // 2^n seconds, capped at 60
}

[Test]
public void EntriesOlderThanTheIdempotencyWindow_AreDroppedWithANotice_NotReplayed()
{
    var box = new Outbox(); box.Enqueue(OutboxEntry.For("wave/submit", b, T0));
    var dropped = box.Expire(T0.AddHours(24).AddSeconds(1));
    Assert.AreEqual(1, dropped.Count);
    Assert.IsNull(box.Next(T0.AddHours(25)));
    StringAssert.Contains("did not happen", dropped[0].Notice);
}

[Test]
public void PersistenceRoundTrips_SoAKillLosesNothing()
{
    var store = new OutboxStore(tempPath); var box = new Outbox(); box.Enqueue(OutboxEntry.For("x", b, T0));
    store.Save(box); var back = store.Load();
    Assert.AreEqual(box.Peek().Key, back.Peek().Key);
}
```

- [ ] **Step 2: Run and watch fail; Step 3: implement.**

```csharp
// Net/Outbox.cs — a List<OutboxEntry> ordered by CreatedAt; Next returns the head iff head.NotBefore <= now, else null.
// Fail: Attempts++, NotBefore = now + min(60s, 2^Attempts s). Expire: removes entries with CreatedAt + 24h < now and returns them
// with Notice = $"{Op} from {CreatedAt:t} could not be sent within 24 hours and did not happen."
// Net/OutboxClient.cs — each op: var e = OutboxEntry.For(op, body, now); store.Save(box.Enqueue(e)); return await pump.FlushAsync(untilKey: e.Key)
//   sends with headers { "idempotency-key": e.Key }. Server response is truth: a 4xx (not 5xx/timeout) acks the entry and surfaces the error;
//   a 5xx or transport failure calls Fail.
// Net/OutboxClient.cs — OFFLINE AFFORDANCE: splice/claim/name are shown unavailable when NotReachable rather than queued
//   (client_architecture 8's "not available offline"); wave/submit and ftue/splice-stock queue.
// Game/Shell/OutboxPump.cs — OnApplicationFocus(true) and a reachability poll (2s) trigger FlushAsync; notices from Expire go to the shell's notice list.
```

`BroodlineClient.cs:15-18` becomes: *"The outbox lives in Outbox.cs / OutboxClient.cs (Phase 7). It was booked to arrive 'with the first queueable mutation, in Phase 6' and did not."*

- [ ] **Step 4: Run; then prove ordering against the real server**

```bash
./implementation/scripts/run-unity-tests.sh EditMode
```

And one server-side assertion, in `services/api/test/ftue.test.ts` (Task 22): the same `wave/submit` body sent twice under one key credits once — the server half of the property this client half depends on. It exists already in `adversarial.test.ts`; cite it rather than duplicate it.

- [ ] **Step 5: Commit**

```bash
git add client/Assets/Net client/Assets/Game/Shell/OutboxPump.cs
git commit -m "feat(client): the outbox - keys at action time, ordered, flushed on foreground, 24h expiry

client_architecture 8 as specified. BroodlineClient.cs said this arrived
'with the first queueable mutation, in Phase 6'; Phase 6 shipped four
queueable mutations and no outbox, and the comment said otherwise for a
phase. Corrected in the same change.

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

## Task 19: The deployed stack — apply, migrate, seed, publish, deploy, and drive the loop on it

Design §7. **This task bills.** Read `infra/terraform/variables.tf`'s `deletion_protection` and `db_public_ip` notes first; every command below assumes them.

**Files:**
- Create: `implementation/scripts/seed-server.sh`, `implementation/scripts/smoke-loop.ts`, `implementation/scripts/smoke-loop.sh`
- Modify: `infra/terraform/terraform.tfvars` (**delete** the two `:latest` image lines — `deploy.sh` passes SHA-tagged images and a stale default deploys the wrong engine silently, per `variables.tf`)

- [ ] **Step 1: Prerequisites, once**

```bash
export PROJECT_ID=broodline-508416 REGION=us-central1
export TF_VAR_db_password="$(openssl rand -base64 32 | tr -d '\n')"     # KEEP THIS - a password manager. See variables.tf
gcloud auth login && gcloud config set project "$PROJECT_ID"
cd infra/terraform && terraform init && terraform apply -target=google_compute_network.main     # first pass: the auto-mode subnet must exist before the import block resolves
terraform apply                                                             # second pass: everything. Creates the JWT SECRET, no version
openssl rand -base64 48 | tr -d '\n' | gcloud secrets versions add broodline-jwt-secret --data-file=- --project "$PROJECT_ID"
cd ../..
```

Expected: `terraform output api_url` prints a `run.app` URL; `gcloud sql instances list` shows one `db-f1-micro`. **Cost starts here.**

- [ ] **Step 2: Migrate and seed — schema before code**

```bash
# Temporarily: terraform apply -var db_public_ip=true -var 'db_authorized_networks=["<your.ip>/32"]'
# then, with the Cloud SQL Auth Proxy or the public IP:
export DATABASE_URL="postgres://broodline_owner:...@<ip>:5432/broodline"    # the OWNER role - migrations and the seed need it
pnpm --filter @broodline/api migrate                                          # 0001..0008
./implementation/scripts/seed-server.sh                                       # below
# then revert: terraform apply   (db_public_ip and db_authorized_networks back to defaults)
```

```bash
#!/usr/bin/env bash
# implementation/scripts/seed-server.sh - the one servers row a deployed
# database needs and nothing creates. Values are the ones every test in
# services/api/test inserts by hand; identity/accounts.ts maps us-central1 -> 1.
set -euo pipefail
: "${DATABASE_URL:?set DATABASE_URL (owner role)}"
psql "$DATABASE_URL" -v ON_ERROR_STOP=1 <<'SQL'
INSERT INTO servers (server_id, region, state, tick_day_of_week, tick_minute_of_day)
VALUES (1, 'us-central1', 'open', 0, 1200)
ON CONFLICT (server_id) DO NOTHING;
SQL
echo "servers row 1 present"
```

- [ ] **Step 3: Publish the bundle, then deploy the code — config before code**

```bash
export CONFIG_BUCKET="$(terraform -chdir=infra/terraform output -raw config_bucket)"
./implementation/scripts/publish-bundle.sh 0.1.3 --activate
./implementation/scripts/deploy.sh          # builds api + sim by commit SHA, applies, prints api_url
```

- [ ] **Step 4: The Phase 5 smoke, then the loop**

```bash
./implementation/scripts/smoke-wave.sh      # Phase 5 Task 11's proof: issuance, verified submission, single payment, stored replay
./implementation/scripts/smoke-loop.sh      # this phase's: sign in -> claim -> splice -> start -> submit, over HTTP
```

`smoke-loop.ts` is `loop.test.ts`'s sequence over `fetch` against `API_URL`, asserting from responses only (no database): the shard balance from `/v1/sync` before and after equals the reward; `/v1/roster` shows both parents gone and the child present; a second submit under the same key returns the stored 200 and the balance is unchanged. It cannot assert the ledger row count — that is the Testcontainers test's job, and the two together are the claim.

Expected: both `PASS`. **This is the first time a deployed-stack clause in any phase's done-when has been demonstrated.**

- [ ] **Step 5: Commit the scripts and the tfvars change**

```bash
git add implementation/scripts/seed-server.sh implementation/scripts/smoke-loop.ts implementation/scripts/smoke-loop.sh infra/terraform/terraform.tfvars
git commit -m "feat(infra): the stack is up - seed, smoke, and the loop driven on Cloud Run

First apply of api, sim and Cloud SQL since the Terraform was written.
seed-server.sh writes the servers row no migration ever did; smoke-loop
drives sign-in -> claim -> splice -> start -> submit over HTTP and
asserts from responses. Phase 5 Task 11's clause and Phase 6's are
demonstrated rather than believed, for the first time.

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

## Task 20: TestFlight — a release build in someone else's hands

Design §1's done-when. Internal testers only.

**Files:**
- Create: `client/Assets/Editor/BootBuilder.cs`
- Modify: `client/Assets/Editor/BuildSteps/IosFileSharingPostProcess.cs` (skip unless `EditorUserBuildSettings.development`), `client/ProjectSettings/ProjectSettings.asset` (`productName: Broodline`, `buildNumber: 1`)

- [ ] **Step 1: The builder**

```csharp
// Editor/BootBuilder.cs - the WaveBuilder shape, release.
//   [MenuItem("Broodline/Build Release iOS Xcode Project")] BuildIOS():
//   - writes Assets/Resources/BroodlineConfig.json from BROODLINE_API_URL (throws if unset - a release pointing at localhost is the failure this prevents)
//   - BenchmarkBuilder.ConfigureSigning(); PlayerSettings.productName = "Broodline"
//   - scenes = { "Assets/Scenes/Boot.unity", "Assets/Scenes/Wave.unity" }, options = BuildOptions.None (NOT Development), to build/ios-release
```

`IosFileSharingPostProcess.OnPostProcessBuild`: `if (!EditorUserBuildSettings.development) return;` — its own header says the two plist keys should be reconsidered before anything ships to a real player. A release build gets neither.

- [ ] **Step 2: Build, archive, upload**

```bash
export BROODLINE_API_URL="$(terraform -chdir=infra/terraform output -raw api_url)"
"/Applications/Unity/Hub/Editor/6000.6.0f1/Unity.app/Contents/MacOS/Unity" -batchmode -quit -projectPath "$(pwd)/client" -executeMethod BootBuilder.BuildIOS -logFile - | tail -5
cd build/ios-release
xcodebuild -project Unity-iPhone.xcodeproj -scheme Unity-iPhone -configuration Release -archivePath ./Broodline.xcarchive archive -allowProvisioningUpdates
xcodebuild -exportArchive -archivePath ./Broodline.xcarchive -exportPath ./export -exportOptionsPlist ../../client/Assets/Editor/ExportOptions.plist -allowProvisioningUpdates   # method: app-store-connect, uploadSymbols: true
xcrun altool --upload-app -f export/Broodline.ipa -t ios --apiKey "$ASC_KEY_ID" --apiIssuer "$ASC_ISSUER_ID"
```

Then App Store Connect → TestFlight → the build → add an **internal** group. No Beta App Review for internal testers.

- [ ] **Step 3: Someone else plays the first hour**

Hand it to one person who has not seen the app. They report: did they reach the Lineage View; where they were confused; how long it took. **That report goes into the followups record verbatim** — it is the first playtest signal this project has had, and `whats_left` §7 says the next document worth writing is the one that records what the first playtest found.

- [ ] **Step 4: Commit**

```bash
git add client/Assets/Editor/BootBuilder.cs client/Assets/Editor/ExportOptions.plist client/Assets/Editor/BuildSteps/IosFileSharingPostProcess.cs client/ProjectSettings/ProjectSettings.asset
git commit -m "build(ios): the release builder - Boot + Wave, non-development, api url baked at build

The file-sharing plist keys are skipped on release builds, as their own
header asked. BROODLINE_API_URL is required: a release pointing at
localhost is the failure that check prevents.

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

## Task 21: The document edits the code has outrun

Design §10.3. Three edits and one recorded deferral.

**Files:**
- Modify: `specs/broodline_region_roster.md` (node rates), `specs/broodline_bible.md` §5.3, `specs/broodline_sample_economy.md` §7, `specs/plans/broodline_solo_execution.md` §3.1

- [ ] **Step 1: Node rates.** `region_roster` and bible §5.3 gain the numbers `0.1.2` authors: Common Vein **20/hour**, no cap; Rich Deposit **60/hour**, `totalYield` **8640**, and the sentence that 8640 / 60 = 144 hours = six days, so the duration falls out of the arithmetic.
- [ ] **Step 2: The downtier floor.** `sample_economy` §7's "one tier lower" gains: *"and never below Tier I — the floor is Tier I, never zero and never null. Implemented and tested in Phase 6."*
- [ ] **Step 3: The degradation row.** `solo_execution` §3.1 is narrowed to what `routes/wave.ts` actually does: `sim_unavailable` (503) leaves the issuance live and grants nothing; `engine_too_old` (426) leaves it live; every other rejection consumes it.
- [ ] **Step 4: `client_architecture` §2's `ref readonly SimState`** — **not edited.** Recorded in the design's §13 as the sixth deferral, by count, so the deferral is an act.

```bash
git add specs/
git commit -m "docs: three documents catch up with code that outran them a phase ago

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

## Task 22: The done-when, end to end — and the record

- [ ] **Step 1: The first hour as a gate — `services/api/test/ftue.test.ts`**

`loop.test.ts`'s pattern, walked as the FTUE: account → wave 1 (pair) → Founder granted → name it → wave 2 (trio) → splice stock → preview shows `mutation: 1` → commit mutates → `GET /v1/lineage` shows child, both consumed parents and the named Founder → wave 6 lost → Pale granted → wave 6 won with the Pale. **The roster ledger asserts `seeded === 0`** — every creature arrived through a response. This is followups §12 item 9 closed: not two half-loops meeting on paper, but one line a player can walk, asserted.

- [ ] **Step 2: Run every gate**

```bash
dotnet test Broodline.sln --nologo                     # 0 failed, 0 skipped
./implementation/scripts/cross-runtime-diff.sh         # 500 agree
pnpm --filter @broodline/api test && pnpm --filter @broodline/api typecheck
./implementation/scripts/generate-contract.sh && git status --porcelain -- openapi/ client/Assets/Generated/ services/api/src/generated/   # empty
./implementation/scripts/run-unity-tests.sh EditMode
bash implementation/scripts/verify-unity-settings.sh
gh run list --workflow determinism.yml --limit 1        # green, on this tip
```

- [ ] **Step 3: `implementation/results/phase7-test-baseline.txt`**

The Phase 6 file's format, with `gate.dotnet.failed 0` and the per-file api rows. It supersedes every count typed into prose, including this plan's.

- [ ] **Step 4: The records**

- `implementation/2026-09-15-phase7-followups.md` — the Phase 6 contract: what is owed, what is believed rather than demonstrated (there should be **nothing** under "deployed stack" this time), the tester's report verbatim, the defects found in this plan and the design.
- Design §13 updated with what execution added, including the five items from "What this plan found the design missed" and: the `BundleWaves`→`WaveDef.ForId` equality check owed to `tools/config-validate`; the Keychain plugin; the sweep's scheduler trigger; `client_architecture` §2's sixth deferral; `DefileSix`'s ratification.
- `implementation/README.md` gains the two Phase 7 entries.
- `specs/plans/broodline_solo_execution.md` §8.2's Phase 7 row: **DONE**, with the date and the two rulings.

```bash
git add implementation/ specs/plans/
git commit -m "docs: the Phase 7 record, and what it leaves owed

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

## What this plan deliberately does not do

Design §12's list, unchanged: no real art; no external TestFlight and no submission screens (Age Gate, Report/Block, Settings support, the naming filter); no waves 8–10 and no Brood, Drift, Bulwark or Delver; no Basin terrain; no Gene Lab, world map, relocation, Sample Store or fusing; no Codex content beyond the index sheet and **no threat board**; no new deployable and no scheduler; no replay viewer.

**And four this plan adds from writing it:**

- **No Keychain.** Tokens live on disk with the no-backup flag. A native plugin is owed before an external build.
- **No scheduled sweep.** `sweep-cli.ts` is owner-run. `solo_execution` §10 has no trigger met.
- **No `WaveDef`-equality check in `tools/config-validate`.** The JSON and the engine's `Wave1()`/`Wave2()` are transcribed by hand and nothing diffs them. A tool change, owed.
- **No Founders beyond the first.** Founders 2–5 "across days 1–3" need base stock to flag its next four grants, and that is a day-2 system.

## Definition of done

All six gates green, with:

- **`TheTrackedCapturesAreCurrent` green under `0.4.0`**, taken *after* the lane change, and `TheDeviceRunsRallyActuallyChangedTheSimulation` green with it. `dotnet test` reports **0 failed, 0 skipped**. **Phase 6 is closed by this.**
- **Wave 6's 500 corpus hashes byte-identical** across the lane refactor, and the cross-runtime diff green **before** the bump.
- **`WaveContentTests` green**: the cold-open pair wins wave 1 in every pocket pair; the trio wins wave 2; Taunt changes wave 2's outcome.
- **The determinism gate run in CI**, on the self-hosted runner, on this branch's tip.
- **`ftue.test.ts` green with `seeded === 0`** — the first hour, every creature earned.
- **`preview` publishes `mutation: 1` and `commit` mutates**, on the first splice, from one row count.
- **A lost wave 6 grants a Pale once**, and base stock never mints one before it.
- **`weakenings.md` row 7 a test**, and the 24h weakening seen to redden it.
- **The coupling guard reddening** when either half of the client floor moves alone.
- **`smoke-loop.sh` PASS against Cloud Run** — the loop on a deployed stack, demonstrated.
- **A TestFlight build installed by someone who is not the developer**, and their report in the followups.

**And one sentence to record as earned rather than assumed.** Phase 6's record said *"the loop closes"* was two half-loops that met on paper. With `ftue.test.ts` asserting `seeded === 0` from account creation to the Lineage View, and `smoke-loop.sh` doing the same over HTTP against real infrastructure, **the loop closes, on a deployed stack, along a line a player can walk.** It should be written that way in the record — and the record should say it took until Phase 7.
