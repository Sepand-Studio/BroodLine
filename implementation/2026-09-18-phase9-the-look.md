# Phase 9 — The Look Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix the two defects nothing can be judged through, bring the nine tester-path screens from the handoff's skeleton to the handoff's fidelity, put 3D creatures on the lane and on the cards from a socketed recipe pipeline, and ship a TestFlight build on the exit gate written in `specs/plans/broodline_phase9_the_look.md` §8.

**Architecture:** One new server route and one healing read close the abandoned-wave lockout; a visibility callback threaded through `WaveHost` closes the shell-over-battlefield layering, gated by a PlayMode test this time. A new runtime assembly, `Broodline.Creatures`, turns C# recipes (spheres, capsules, boxes, sockets, bones) into committed meshes through an Editor generator, shades them with one hand-written URP shader, bakes them to layered card sprites, and assembles them on the lane and in a portrait studio. The UI assembly never references it: it receives PNG sprites and a `Texture`. Screens are then rebuilt from a widened component vocabulary, in the order a tester meets them, on top of real art.

**Tech Stack:** Unity 6000.6.0f1, URP 17.7.0, UI Toolkit (UXML/USS), C# EditMode tests under NUnit via `Broodline.TestHarness.EditModeRunner`, PlayMode tests in the Editor's Test Runner window, Hono + Drizzle + Postgres (`services/api`, vitest), NSwag for the C# client, bash gates in `implementation/scripts/`, Terraform + Cloud Run for the deployed stack, Xcode / App Store Connect.

**Design:** `specs/plans/broodline_phase9_the_look.md`. Read it first. Where this plan and that design disagree, §"What this plan found the design missed" below says so and the plan wins; the design gets an erratum in Task 22.

---

## Global Constraints

**No task in this plan modifies `engine/`.** `SimVersion` stays `0.4.0`; the device capture at `implementation/results/device-replay.bin` stays valid. If a task appears to need an engine change, stop — that is a phase-scope decision.

**The engine constraints from Phases 1–3 still bind `engine/`** — no floating point, no `System.Math`, no `System.Linq`, no `Dictionary`, no `HashSet`, no `System.IO`, zero project references. `EnforcementTests` scans for them. Stated so the paragraph above reads as scope, not permission.

From the design, normative here:

- **`Broodline.UI` references neither `Broodline.Sim`, `Broodline.View` nor `Broodline.Creatures`.** Its asmdef stays `["Broodline.Model", "Broodline.Net", "Generated.Api"]`. Sprites cross the boundary as `Texture2D` loaded from `Resources`; the portrait crosses as a `Texture` handed to `CreatureStage.SetTexture`. Code does not cross.
- **`Broodline.Creatures` references nothing project-side.** Its species colours are mirrored from `PaletteContrast.Species`, and `CreatureColourTests` in `Broodline.Game.Tests` (which sees both) asserts the mirror.
- **Every colour, size, radius and spacing value lives in `Tokens.uss`.** `verify-uss-tokens.sh` enforces it. A stylesheet that needs a new value adds the token. No `calc()` (it imports as a warning and drops the declaration), no `box-shadow` (elevation is the `.elev-1`/`.elev-2` wrapper), no `--radius-pill` (check 3 reddens on it; a pill is half the measured height rounded down, as a literal beside its measurement).
- **Screens are `[UxmlElement] public partial class XView : VisualElement`** in `Broodline.UI.Screens`, composing `ScreenScaffold`, bound through `Bind(model, callbacks)`. `RosterView.cs` is the reference shape. `ScaffoldTests.EveryScreenComposesTheScaffold` counts exactly 12 constructible elements in that namespace and exempts exactly `CodexSheet` and `WaveHudView`; **new components go in `Broodline.UI.Components`, never in `.Screens`**, and the one new sheet goes in `.Components` too.
- **UI tests construct plain `VisualElement` trees with no scene and no live `Panel`.** Assert structure, not dispatch. Anything needing a real panel is a PlayMode test.
- **PlayMode tests run from the Editor's Test Runner window, not headlessly.** `run-unity-tests.sh PlayMode` is documented to deadlock on this Editor. A task that adds a PlayMode test records its run in `implementation/results/` by hand: the Test Runner's result count and the Editor log line.
- **Nothing on the startup path merges without `run-unity-tests.sh EditMode` green.** This is how the reverted fix happened. Tasks 4, 5 and 6 each end with that gate.
- **The wave view changes what it draws, never what it reads.** Everything on the lane still comes off `WavePair`; `WaveCapturePlayTests` and the determinism gate keep proving the sim untouched.
- **Never `-unity-font-style: bold` on a Label.** Nunito-Bold SDF is already the bold face; synthetic emboldening closes counters at 11px. Never set a numeral in Baloo 2. Every number a decision depends on carries `.t-num`.
- **Internal TestFlight only.** No Beta App Review. Carried from Phase 7.
- **A release build never points at `localhost`.** `BootBuilder` throws when `BROODLINE_API_URL` is unset or contains `localhost`.

### Toolchain — read before running anything

**pnpm.** The default `pnpm` on `PATH` is 3.7.5 under node v10 and fails in ways that read as broken tests. In every shell:

```bash
export PATH="$HOME/.nvm/versions/node/v22.22.2/bin:$PATH"   # node 22.22.2, pnpm 9.12.0
```

**The Unity lock.** `run-unity-tests.sh`, `check-stylesheets.sh`, `capture-screens.sh` and every `-batchmode` command in this plan refuse to run while the Editor has `client/` open. Close it first. There is one Editor; **no two agents work on `client/` at once**, and tasks that touch it are serial.

**Results.** `implementation/results/*` is gitignored except `*.csv` and `*-test-baseline.txt`. Use `git add -f` for anything else that must be tracked (this plan tracks `phase9-visual-review.md` and `phase9-test-baseline.txt`; the contact sheet and captures stay untracked).

**LFS.** `*.png` is LFS-tracked by `.gitattributes`; `*.asset`, `*.prefab`, `*.unity` are text and must stay diffable. Baked sprites go through LFS; generated meshes are text YAML and are committed as such.

**Bash.** macOS bash 3.2, BSD `sed`, no `timeout`, no `psql`. Scripts in this plan use `python3` for anything that needs more than `grep`.

**Gates and their exit codes.** `run-unity-tests.sh EditMode` exits 0 on all-pass, 2 on failure, 127 if Unity is missing, and prints `total= passed= failed= skipped=`. The Phase 8 baseline is in `implementation/results/phase8-test-baseline.txt`: 323 total, 322 passed, 1 failed (`MainThreadAffinityTests`, deliberately red; `phase8-followups.md` §2.5). Every task below states the count it expects to move to.

---

## What this plan found the design missed

Eight things. None changes scope; four change where a piece of work lands, one moves a number.

**1. The snapshot carries no creatures, so the forfeit sheet cannot live in `BootController`.** Design §2.1 puts the committed-creature check "after cold start and before the director runs". `PlayerSnapshot` has `Balances`, `Waves`, `Traits`, `Ftue` and no roster (`client/Assets/Model/PlayerSnapshot.cs:40-59`). The roster is loaded by `RosterScreen.LoadAsync` from inside `FtueDirector.RunAsync` (line 184). The check therefore lands in the director, immediately after that load and before `Ftue.Derive`, as a static `FtueDirector.NeedsForfeit(IReadOnlyList<CreatureDto>)` that a test can drive plus one awaited step in the loop. Task 5.

**2. PlayMode cannot run headlessly here.** Design §2.2's proof is a PlayMode test. `run-unity-tests.sh PlayMode` uses `-runTests` and is documented to deadlock on this Editor. The test is still written and still gates the task — it runs from the Editor's Test Runner window, and the task records the run. The EditMode suite still runs headlessly before the commit, which is the half of the gate the reverted fix skipped. Task 6.

**3. There is no spare layer for a studio camera.** `TagManager.asset` names only Unity's five builtin layers. A portrait camera that sees the shell scene's nothing is fine, but the wave scene's orthographic camera at y=20 with a 1000-unit far plane would see a studio placed anywhere in its column. Task 8 adds layer 6, `Studio`, to `TagManager.asset`; the wave camera's culling mask excludes it; studio and lane-stage objects live on it. `verify-unity-settings.sh` does not inspect `TagManager.asset`, so nothing reddens.

**4. The Mobile quality tier caps skinning at two bone influences.** `QualitySettings.asset` level 0 (`Mobile`, the iPhone default) has `skinWeights: 2`; level 1 (`PC`, the Editor) has 4. A mesh authored with four influences would look different on the phone than in the Editor. The mesher writes **at most two influences per vertex** so both tiers render the same thing. Task 7.

**5. Raider bodies are chosen from the wave definition, not the runner.** `SimRunner` exposes `RaiderType` only as a `ReadOnlySpan`, and `Phases.Spawn` fills it from `WaveDef.Spawns[r].Type` at spawn time. `WaveRunner.Configure` already holds the `WaveDef`, so the view gets the spawn table's types at build time rather than a new snapshot field. Task 10.

**6. `loadLiveIssuance` refuses a past-expiry row, so abandon needs its own loader.** Design §2.1 says abandon "settles the caller's live issuance". The existing loader (`issuance.ts:675-687`) needs an `issuanceId` and returns `undefined` past `expiresAt`. Abandon is body-less — the player has at most one unsettled row (`wave_issuances_one_live`) — and must settle it whether or not it has expired. Task 1 adds `loadUnsettledIssuance(tx, serverId, playerId)`.

**7. The roster route's own header becomes false.** `roster.ts:20-22` says "DERIVED AND WRITES NOTHING… two calls in a row return the same list." After Task 2 the first call after an expiry writes. The header is rewritten in the same commit.

**8. The body triangle budget is 2500, not 1500.** Design §3.4's 1500 assumed a decimation step the plan does not build. Naive surface nets at a grid that reads as smooth produce about 2000–2500 per body. 2500 is a quarter of the 10000 `rig_proof.md` §8 measured at 104 entities, with that document's margin still unspent. Task 7 states it; Task 22 records the erratum.

---

## File structure

### New — server

| Path | Responsibility |
|---|---|
| `services/api/src/routes/wave.ts` (modified) | `POST /v1/wave/abandon` |
| `services/api/src/wave/issuance.ts` (modified) | `loadUnsettledIssuance` |
| `services/api/src/routes/roster.ts` (modified) | `settleExpiredForPlayer` before `loadRoster`; header rewritten |
| `services/api/src/schemas.ts`, `src/openapi.ts` (modified) | `WaveAbandonResponse`; the route registered |
| `services/api/test/wave-abandon.test.ts` | Four abandon tests, one healing-read test |
| `services/api/test/wave-helpers.ts` (modified) | `abandon()` driver |
| `openapi/broodline.json`, `client/Assets/Generated/Api/BroodlineApiClient.cs` | Regenerated by `generate-contract.sh`; never hand-edited |
| `implementation/scripts/smoke-loop.ts` (modified) | The abandoned-wave step |

### New — client, shell and blockers

| Path | Responsibility |
|---|---|
| `client/Assets/UI/Components/NoticeToast.cs` + `Resources/NoticeToast.uxml/.uss` | The notice surface |
| `client/Assets/UI/Components/AbandonedWaveSheet.cs` + resources | The forfeit sheet |
| `client/Assets/UI/Shell/Shell.uxml`, `Shell.uss` (modified) | `#notice-layer` beside `#shell-root` |
| `client/Assets/Game/Shell/BootController.cs` (modified) | Toast wiring; `setShellVisible` to the host |
| `client/Assets/Game/Shell/OutboxPump.cs` (modified) | `OnNotice` callback |
| `client/Assets/Game/Shell/WaveHost.cs` (modified) | Hide after load, restore in `finally` |
| `client/Assets/Game/Ftue/FtueDirector.cs` (modified) | `NeedsForfeit`, the forfeit step |
| `client/Assets/Game/Tests/PlayMode/WaveCapturePlayTests.cs` (modified) | Visibility assertions on both paths |
| `client/Assets/Game/Tests/FtueDirectorTests.cs`, `client/Assets/UI/Tests/ComponentTests.cs` (modified) | Forfeit, toast, sheet |

### New — creatures

| Path | Responsibility |
|---|---|
| `client/Assets/Creatures/Broodline.Creatures.asmdef` | Runtime; references nothing project-side |
| `client/Assets/Creatures/Recipe.cs` | `Primitive`, `BoneDef`, `SocketDef`, `BodyRecipe`, `PartRecipe`, `CreatureLook` |
| `client/Assets/Creatures/Recipes/SpeciesRecipes.cs` | Six bodies |
| `client/Assets/Creatures/Recipes/PartRecipes.cs` | Twelve trait parts |
| `client/Assets/Creatures/Recipes/RaiderRecipes.cs` | Courser, Lash, Skirmisher |
| `client/Assets/Creatures/SpeciesColours.cs` | Base and underside per species, mirrored |
| `client/Assets/Creatures/Sdf.cs` | Distance functions and smooth union |
| `client/Assets/Creatures/SurfaceNets.cs` | Grid → mesh, normals, two-influence weights |
| `client/Assets/Creatures/CreatureLibrary.cs` | Prefabs from `Resources/Creatures/...` |
| `client/Assets/Creatures/CreatureAssembler.cs` | Body + parts at sockets + growth, one `GameObject` |
| `client/Assets/Creatures/CreatureMotion.cs` | Breathe, bob, flinch, hurt |
| `client/Assets/Creatures/Shaders/Creature.shader` | Ramp, rim, underside, desaturate |
| `client/Assets/Creatures/Resources/Creatures/Bodies/*.prefab` + `Meshes/*.asset` | Generated, committed |
| `client/Assets/Creatures/Resources/Creatures/Parts/*.prefab` + meshes | Generated, committed |
| `client/Assets/Creatures/Resources/Creatures/Raiders/*.prefab` + meshes | Generated, committed |
| `client/Assets/Creatures/Editor/Broodline.Creatures.Editor.asmdef` | Editor-only |
| `client/Assets/Creatures/Editor/CreatureGenerator.cs` | Recipes → assets; menu + batchmode |
| `client/Assets/Creatures/Editor/CreatureBaker.cs` | Sprites, contact sheet, Cinderplate |
| `client/Assets/Creatures/Tests/Broodline.Creatures.Tests.asmdef` + `*.cs` | Budgets, sockets, overlap, drift |
| `client/Assets/UI/Resources/Art/creatures/bodies/*.png`, `parts/*.png` | Baked sprites (LFS) |
| `client/Assets/UI/Components/CreatureSprites.cs` | Body and part textures by name; replaces `SpeciesProxy` |
| `client/Assets/Game/Shell/PortraitStudio.cs` | One camera, one render texture, one creature |
| `client/Assets/Game/Shell/LaneStage.cs` | The deploy card's lane |
| `client/Assets/View/LaneDressing.cs` | Ground, path, trees, Ark |
| `implementation/scripts/generate-creatures.sh` | Batchmode generate + bake |
| `client/ProjectSettings/TagManager.asset` (modified) | Layer 6 `Studio` |

### New — UI components and screens

| Path | Responsibility |
|---|---|
| `client/Assets/UI/Components/{GenChip,TraitChip,HeroSlot,InheritanceBar,MutationBanner,LineageStrip,CostCtaRow,FieldSlotRow,CreatureStage,LanePreviewCard}.cs` + resources | The widened vocabulary |
| `client/Assets/UI/Components/ScreenScaffold.cs` + resources (modified) | Eyebrow, back pill, resource pill |
| `client/Assets/UI/Components/CreatureCard.cs` + resources (modified) | Three-layer silhouette slot |
| `client/Assets/UI/Art/generate-textures.py` (modified) + `amber-ramp.png`, `hybrid-ramp.png` | Two more ramps |
| `client/Assets/UI/Shell/Tokens.uss` (modified) | New tokens, each named in its task |
| Nine screen `.cs/.uxml/.uss` (modified) | Fidelity |
| `client/Assets/Editor/ScreenFixtures.cs` (modified) | Loss fixture, new fixtures |
| `implementation/results/phase9-visual-review.md` (tracked with `-f`) | The walk |
| `implementation/results/phase9-test-baseline.txt` | The count |
| `implementation/2026-09-18-phase9-followups.md` | The record |

---

## Task 0: The record — confirm the baseline before anything moves

**Files:**
- Read: `implementation/results/phase8-test-baseline.txt`
- Create: nothing yet; this task produces a number in the log

No task in this plan may claim a count it did not measure. The Editor must be closed.

- [ ] **Step 1: Branch and tree**

```bash
git branch --show-current            # phase_9
git status --short                   # only ` M client/Assets/Scenes/Boot.unity` (Editor fileID noise, ignore)
git log --oneline -1                 # 4940ad6 docs(design): Phase 9 design, The Look
```

- [ ] **Step 2: Run the EditMode gate**

```bash
./implementation/scripts/run-unity-tests.sh EditMode
```

Expected: `total=323 passed=322 failed=1 skipped=0`, the one failure `Broodline.Game.Tests.MainThreadAffinityTests…`. Exit code 2. If the run writes no results file, the Editor is open — close it and re-run. If the count differs from 323/322/1, stop and record why in `implementation/results/phase9-test-baseline.txt` before continuing; every later task's expected count is relative to this one.

- [ ] **Step 3: Run the stylesheet and token gates**

```bash
bash implementation/scripts/check-stylesheets.sh
bash implementation/scripts/verify-uss-tokens.sh
```

Expected: both print `ok` lines and exit 0.

- [ ] **Step 4: Confirm the server suite runs**

```bash
export PATH="$HOME/.nvm/versions/node/v22.22.2/bin:$PATH"
pnpm --version                                    # 9.12.0
pnpm --filter @broodline/api test 2>&1 | tail -5  # needs Docker for testcontainers
```

Expected: all green. If Docker is not running, start it; the abandon tests in Task 1 need it.

No commit. This task changes nothing.

---

## Task 1: `POST /v1/wave/abandon` — the forfeit, on the server

Design §2.1 part 1. The route settles the caller's one unsettled issuance as `'expired'` through `settle()`, which already releases the roster, and answers `{ settled: true }`. No issuance → `{ settled: false }`, 200. No `Idempotency-Key`: the route is naturally idempotent because `settle()`'s `AND settled_at IS NULL` guard makes the second call a no-op, and it pays nothing. Body-less, like `/v1/ftue/splice-stock`. Registered in `openapi.ts` in the same commit — `/v1/wave/start` once shipped without registration and was absent from the generated client.

**Files:**
- Modify: `services/api/src/wave/issuance.ts` (after `loadLiveIssuance`, ~line 687)
- Modify: `services/api/src/routes/wave.ts` (a third `app.post` after `/v1/wave/submit`)
- Modify: `services/api/src/schemas.ts` (after `WaveSubmitResponse`, ~line 229)
- Modify: `services/api/src/openapi.ts` (import list lines 4–11; a `registerPath` after the submit one)
- Modify: `services/api/test/wave-helpers.ts` (an `abandon()` driver after `submit`)
- Create: `services/api/test/wave-abandon.test.ts`
- Regenerate: `openapi/broodline.json`, `client/Assets/Generated/Api/BroodlineApiClient.cs`

**Interfaces:**
- Consumes: `settle(tx, issuance, 'expired')`, `withServer`, `loadPlayerId`, `requireSession`, `fail`, the test harness (`startTestDb`, `setupPlayer`, `startWave`, `liveIssuance`, `winningRoster`).
- Produces: `loadUnsettledIssuance(tx: Tx, serverId: number, playerId: string): Promise<Issuance | undefined>`; `POST /v1/wave/abandon` → `200 { settled: boolean }`; operationId `abandonWave`, so the generated C# method is `AbandonWaveAsync()` returning `Task<WaveAbandonResponse>` with `bool Settled`. Task 5 calls it.

- [ ] **Step 1: The test driver**

In `services/api/test/wave-helpers.ts`, after `submit` (line ~662):

```ts
/** POST /v1/wave/abandon as the current player. Body-less; no Idempotency-Key. */
export async function abandon(): Promise<Response> {
  return app.request('/v1/wave/abandon', {
    method: 'POST',
    headers: { authorization: `Bearer ${token}` },
  })
}
```

- [ ] **Step 2: The failing tests**

Create `services/api/test/wave-abandon.test.ts`. The setup block is `wave-start.test.ts:1-76` repeated, because a test file is read on its own:

```ts
import { mkdtemp, rm } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { eq, sql } from 'drizzle-orm'
import { afterAll, beforeAll, beforeEach, describe, expect, it } from 'vitest'
import { createApp, type Deps } from '../src/app.ts'
import { clearBundleCache, LocalBundleStore, publishBundle } from '../src/config/bundle.ts'
import { servers, waveIssuances } from '../src/db/schema.ts'
import { LocalReplayStore } from '../src/replay/store.ts'
import { SimClient } from '../src/sim/client.ts'
import { startTestDb, type TestDb } from './harness.ts'
import {
  abandon, type Deployed, liveIssuance, setupPlayer, startWave as start, winningRoster,
} from './wave-helpers.ts'

const REPO = fileURLToPath(new URL('../../../', import.meta.url))
const SEED = join(REPO, 'config/bundles/0.1.1')

let t: TestDb
let deps: Deps
let app: ReturnType<typeof createApp>
let bundleRoot: string

beforeAll(async () => {
  t = await startTestDb()
  await t.ownerDb.insert(servers).values({
    serverId: 1, region: 'us-central1', state: 'open', tickDayOfWeek: 0, tickMinuteOfDay: 1200,
  })
  bundleRoot = await mkdtemp(join(tmpdir(), 'broodline-wave-abandon-'))
  const store = new LocalBundleStore(bundleRoot)
  await publishBundle(store, SEED, '0.1.1')
  await store.setPointer('0.1.1')
  clearBundleCache()
  deps = {
    db: t.db, bundleStore: store,
    simClient: new SimClient('http://127.0.0.1:1', SimClient.noAuth('abandon never calls sim')),
    replayStore: new LocalReplayStore(bundleRoot),
  }
  app = createApp(deps)
}, 240_000)

afterAll(async () => {
  await t?.stop()
  if (bundleRoot) await rm(bundleRoot, { recursive: true, force: true })
})

describe('POST /v1/wave/abandon', () => {
  let mine: Deployed[]
  let token: string

  beforeEach(async () => {
    const p = await setupPlayer(deps)
    token = p.token
    mine = await winningRoster()
  })

  async function committedTo(ids: string[]): Promise<Map<string, string | null>> {
    const r = await app.request('/v1/roster', { headers: { authorization: `Bearer ${token}` } })
    expect(r.status).toBe(200)
    const body = await r.json() as { creatures: { creatureId: string; committedTo: string | null }[] }
    const m = new Map<string, string | null>()
    for (const c of body.creatures) if (ids.includes(c.creatureId)) m.set(c.creatureId, c.committedTo)
    return m
  }

  it('settles the live issuance as expired and releases every deployed creature', async () => {
    const deployed = mine.slice(0, 2)
    const res = await start(1, deployed)
    expect(res.status).toBe(200)
    const { issuanceId } = await res.json() as { issuanceId: string }
    for (const v of (await committedTo(deployed.map((d) => d.creatureId))).values()) expect(v).toBe(issuanceId)

    const ab = await abandon()
    expect(ab.status).toBe(200)
    expect(await ab.json()).toEqual({ settled: true })

    for (const v of (await committedTo(deployed.map((d) => d.creatureId))).values()) expect(v).toBeNull()
    expect(await liveIssuance()).toBeUndefined()
    const [row] = await t.ownerDb.select().from(waveIssuances)
      .where(eq(waveIssuances.issuanceId, issuanceId))
    expect(row?.settlement).toBe('expired')
  })

  it('is a no-op with nothing live', async () => {
    const ab = await abandon()
    expect(ab.status).toBe(200)
    expect(await ab.json()).toEqual({ settled: false })
  })

  it('does not double-settle: the second call answers settled:false', async () => {
    await start(1, mine.slice(0, 2))
    expect(await (await abandon()).json()).toEqual({ settled: true })
    expect(await (await abandon()).json()).toEqual({ settled: false })
  })

  it('settles a live issuance that is already past expiry, because that is the case the client hits', async () => {
    const deployed = mine.slice(0, 2)
    const res = await start(1, deployed)
    const { issuanceId } = await res.json() as { issuanceId: string }
    await t.ownerDb.execute(sql`
      UPDATE wave_issuances SET expires_at = now() - interval '1 second'
      WHERE issuance_id = ${issuanceId}`)

    expect(await (await abandon()).json()).toEqual({ settled: true })
    for (const v of (await committedTo(deployed.map((d) => d.creatureId))).values()) expect(v).toBeNull()
  })

  it('needs a session', async () => {
    const r = await app.request('/v1/wave/abandon', { method: 'POST' })
    expect(r.status).toBe(401)
  })
})
```

- [ ] **Step 3: Run it to see it fail**

```bash
export PATH="$HOME/.nvm/versions/node/v22.22.2/bin:$PATH"
cd services/api && npx vitest run test/wave-abandon.test.ts
```

Expected: 4 of 5 fail with status 404 (`not_found`, no such route); "needs a session" may already pass because `app.notFound` runs after nothing — accept either.

- [ ] **Step 4: The loader**

In `services/api/src/wave/issuance.ts`, after `loadLiveIssuance`:

```ts
/**
 * The player's ONE unsettled issuance, expired or not - `wave_issuances_one_live`
 * allows at most one. `loadLiveIssuance` refuses a past-expiry row because
 * a submit against one must be refused; abandon is the opposite case, and
 * exists precisely for the row the client cannot otherwise reach.
 */
export async function loadUnsettledIssuance(
  tx: Tx, serverId: number, playerId: string,
): Promise<Issuance | undefined> {
  const [row] = await tx.select().from(waveIssuances)
    .where(and(
      eq(waveIssuances.serverId, serverId),
      eq(waveIssuances.playerId, playerId),
      isNull(waveIssuances.settledAt)))
  return row
}
```

- [ ] **Step 5: The schema**

In `services/api/src/schemas.ts`, after `WaveSubmitResponse`:

```ts
export const WaveAbandonResponse = z.object({
  settled: z.boolean(),
}).openapi('WaveAbandonResponse')
```

- [ ] **Step 6: The route**

In `services/api/src/routes/wave.ts`, add `loadUnsettledIssuance` to the `../wave/issuance.ts` import, and after the `/v1/wave/submit` handler (inside `registerWaveRoutes`):

```ts
  /**
   * The forfeit. Phase 9 design §2.1: a wave started and never submitted
   * leaves its creatures `committed_to` a row the client can only reach
   * through a deploy screen it refuses to open while they are committed.
   * This settles that row 'expired' - the same settlement the two-hour
   * expiry applies, applied now - and `settle()` releases the roster.
   *
   * NO IDEMPOTENCY-KEY AND NO BODY. `settle()`'s `AND settled_at IS NULL`
   * makes a repeat a no-op that answers settled:false, and nothing here
   * pays, so the key would protect nothing. One sorted `releaseCreatures`
   * statement, so no `lockRoster` either (see the lock-ordering rule on
   * `consumeAndRefuse`).
   */
  app.post('/v1/wave/abandon', async (c) => {
    const session = await requireSession(c)

    const result = await withServer(deps.db, session.serverId, async (tx) => {
      const playerId = await loadPlayerId(tx, session.accountId)
      if (playerId === undefined) return null

      const issuance = await loadUnsettledIssuance(tx, session.serverId, playerId)
      if (issuance === undefined) return { settled: false }

      return { settled: await settle(tx, issuance, 'expired') }
    })

    if (result === null) return fail('not_found', 'No player on this server for that account.')
    return c.json(result)
  })
```

- [ ] **Step 7: Register it**

In `services/api/src/openapi.ts`, add `WaveAbandonResponse` to the schemas import, and after the `/v1/wave/submit` registration:

```ts
registry.registerPath({
  method: 'post',
  path: '/v1/wave/abandon',
  operationId: 'abandonWave',
  security: [{ [bearerAuth.name]: [] }],
  responses: {
    200: { description: 'Whether a live issuance was settled and its creatures released', content: { 'application/json': { schema: WaveAbandonResponse } } },
    ...errors([401, 404]),
  },
})
```

- [ ] **Step 8: Run the tests to see them pass**

```bash
cd services/api && npx vitest run test/wave-abandon.test.ts
```

Expected: 5 passed.

- [ ] **Step 9: Regenerate the contract and the C# client**

```bash
./implementation/scripts/generate-contract.sh
git status --porcelain -- openapi/ client/Assets/Generated/ services/api/src/generated/
grep -n "AbandonWaveAsync\|class WaveAbandonResponse" client/Assets/Generated/Api/BroodlineApiClient.cs | head
```

Expected: `openapi/broodline.json` and `BroodlineApiClient.cs` modified; `AbandonWaveAsync` present.

- [ ] **Step 10: The whole api suite, then typecheck**

```bash
pnpm --filter @broodline/api test 2>&1 | tail -5
pnpm --filter @broodline/api typecheck
```

Expected: all green, including `contract.test.ts` (which re-runs the generator and checks the tree is clean).

- [ ] **Step 11: Commit**

```bash
git add services/api/src/wave/issuance.ts services/api/src/routes/wave.ts \
        services/api/src/schemas.ts services/api/src/openapi.ts \
        services/api/test/wave-helpers.ts services/api/test/wave-abandon.test.ts \
        openapi/broodline.json client/Assets/Generated/Api/BroodlineApiClient.cs
git commit -m "feat(api): POST /v1/wave/abandon settles the live issuance as expired

The forfeit for a wave started and never submitted. settle() already
releases the roster on both terminal states; this reaches it from a route
the client can call without a deploy screen. Body-less, no key: the
settled_at IS NULL guard makes a repeat a no-op.

loadUnsettledIssuance is new because loadLiveIssuance refuses a past-expiry
row, and the past-expiry row is exactly the one the client is stuck on.

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

## Task 2: The roster read heals a past-expiry issuance

Design §2.1 part 2. `GET /v1/roster` runs `settleExpiredForPlayer` before `loadRoster`, so a player who returns after the two-hour window sees a free roster and no sheet. The route's header is rewritten because "writes nothing" stops being true.

**Files:**
- Modify: `services/api/src/routes/roster.ts`
- Modify: `services/api/test/wave-abandon.test.ts` (one more test in a second `describe`)

**Interfaces:**
- Consumes: `settleExpiredForPlayer(tx, serverId, playerId)` from `../wave/sweep.ts`.
- Produces: nothing new on the wire.

- [ ] **Step 1: The failing test**

Append to `services/api/test/wave-abandon.test.ts`, inside the file, a second block:

```ts
describe('GET /v1/roster heals', () => {
  it('releases creatures committed to an issuance past its expiry, with no abandon call', async () => {
    const p = await setupPlayer(deps)
    const mine = await winningRoster()
    const deployed = mine.slice(0, 2)
    const res = await start(1, deployed)
    const { issuanceId } = await res.json() as { issuanceId: string }
    await t.ownerDb.execute(sql`
      UPDATE wave_issuances SET expires_at = now() - interval '1 second'
      WHERE issuance_id = ${issuanceId}`)

    const r = await app.request('/v1/roster', { headers: { authorization: `Bearer ${p.token}` } })
    expect(r.status).toBe(200)
    const body = await r.json() as { creatures: { creatureId: string; committedTo: string | null }[] }
    for (const c of body.creatures) expect(c.committedTo).toBeNull()
    expect(await liveIssuance()).toBeUndefined()
  })
})
```

- [ ] **Step 2: Run it to see it fail**

```bash
cd services/api && npx vitest run test/wave-abandon.test.ts -t heals
```

Expected: FAIL — `committedTo` is still the issuance id.

- [ ] **Step 3: The healing read**

In `services/api/src/routes/roster.ts`, import `settleExpiredForPlayer` from `'../wave/sweep.ts'`, and in the handler:

```ts
    const result = await withServer(deps.db, session.serverId, async (tx) => {
      const playerId = await loadPlayerId(tx, session.accountId)
      if (playerId === undefined) return null

      // Phase 9 design §2.1: the same expiry settle `wave/start` runs before
      // its checks, run here, so a roster read after the window is enough
      // to free creatures a never-submitted wave left committed. At most one
      // row, one sorted release statement - no lockRoster needed.
      await settleExpiredForPlayer(tx, session.serverId, playerId)

      const ark = await loadArk(tx, session.serverId, playerId)
      return {
        creatures: await loadRoster(tx, session.serverId, playerId),
        cap: rosterCap(ark.hatcheryTier),
      }
    })
```

Replace the header paragraph beginning `DERIVED AND WRITES NOTHING` with:

```ts
 * MOSTLY DERIVED. The one write is `settleExpiredForPlayer`: a live-but-
 * expired issuance is settled 'expired' and its creatures released before
 * the list is read, so two calls in a row return the same list EXCEPT
 * across an expiry, where the second is the healed one. No Idempotency-Key,
 * because the write is idempotent by `settle()`'s own guard.
```

- [ ] **Step 4: Run the tests to see them pass**

```bash
cd services/api && npx vitest run test/wave-abandon.test.ts && npx vitest run test/roster.test.ts
```

Expected: all green (if `test/roster.test.ts` does not exist under that name, run the whole suite).

- [ ] **Step 5: Commit**

```bash
git add services/api/src/routes/roster.ts services/api/test/wave-abandon.test.ts
git commit -m "feat(api): the roster read settles a past-expiry issuance before listing

A player who returns after the two-hour window now sees a free roster with
no dialog. The route's header said it wrote nothing; it now says what it
writes and why that is still idempotent.

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

## Task 3: The smoke loop exercises "started and never finished"

Design §6.2 row 2. No suite anywhere exercised the abandoned state, because a test never stops mid-wave. This step starts wave 1, submits nothing, abandons, and asserts the roster is free — against the deployed stack. It runs **before** the "fight a wave and get paid" step so the replayed wave 1 can still be issued (one live row per player), and the abandoned issuance id is **not** pushed to `issuanceIds`, because the `[C]` replay-object check asserts an object per id and an abandoned wave stores no replay.

**Files:**
- Modify: `implementation/scripts/smoke-loop.ts` (insert before line ~506 `begin('fight a wave and get paid - wave 1, replayed')`)

- [ ] **Step 1: Deploy the api**

The stack is up; the new route is not on it until the api image is rebuilt and deployed. Follow `docs/local-stack.md`'s deploy section (`cloudbuild.yaml` → Cloud Run `api`). Confirm:

```bash
API_URL="$(terraform -chdir=infra/terraform output -raw api_url)"
curl -s -o /dev/null -w '%{http_code}\n' -X POST "$API_URL/v1/wave/abandon"    # 401, not 404
```

- [ ] **Step 2: The step**

Insert into `smoke-loop.ts` before the `fight a wave and get paid` block:

```ts
// ---------------------------------------------------------------------------
begin('a wave started and never submitted, then forfeited - the roster comes back')
// Phase 8's eyes-on pass found this in the first minute and no suite had:
// wave/start commits the deployment, and nothing but a settlement releases
// it. The client forfeits through wave/abandon; this proves the route on
// the deployed stack. Wave 1 again, because it is issuable (1 <= cleared).
// The issuance id is deliberately NOT pushed to issuanceIds: an abandoned
// wave stores no replay, and [C] asserts an object per id.
{
  const started = await call('/v1/wave/start', { token, body: { waveId: 1, deployment: pairDeployed } })
  if (started.status !== 200) fail(`wave/start(1) for the abandon step returned ${started.status}`, started.text.slice(0, 600))

  const locked = await call('/v1/roster', { token })
  const lockedIds = (locked.body?.creatures ?? [])
    .filter((c: Creature & { committedTo: string | null }) => c.committedTo !== null)
    .map((c: Creature) => c.creatureId)
  if (lockedIds.length !== pairDeployed.length) fail('wave/start did not commit the deployment', locked.body)

  const ab = await call('/v1/wave/abandon', { token, method: 'POST' })
  if (ab.status !== 200 || ab.body?.settled !== true) fail(`wave/abandon returned ${ab.status}`, ab.text.slice(0, 400))

  const freed = await call('/v1/roster', { token })
  const stillLocked = (freed.body?.creatures ?? [])
    .filter((c: Creature & { committedTo: string | null }) => c.committedTo !== null)
  if (stillLocked.length !== 0) fail('creatures still committed after abandon', stillLocked)

  const again = await call('/v1/wave/abandon', { token, method: 'POST' })
  if (again.body?.settled !== false) fail('a second abandon must be a no-op', again.body)
  ok(`${lockedIds.length} creatures committed, forfeited, and free again; the repeat was a no-op`)
}
```

If `Creature` in this file already carries `committedTo`, drop the intersection types.

- [ ] **Step 3: Run the loop**

```bash
bash implementation/scripts/smoke-loop.sh 2>&1 | tail -30
```

Expected: the new step prints its `✓`, the later replayed wave 1 still wins, `[A]`–`[C]` pass, final `PASS`. The ledger expectations are unchanged because abandon pays nothing.

- [ ] **Step 4: Commit**

```bash
git add implementation/scripts/smoke-loop.ts
git commit -m "test(smoke): a wave started, never submitted, and forfeited

The state no suite ever reached, against the deployed stack. The
abandoned issuance is not added to the replay-object check because an
abandoned wave stores no replay.

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

## Task 4: `NoticeToast` — somewhere to see it

Design §2.1 "the notice surface". `BootController.OnNotice` logs and stops; `OutboxPump.Notices` fills a list nothing renders. This task gives both a surface: one element in a `#notice-layer` that is a **sibling** of `#shell-root`, so it survives the shell being hidden during a wave (Task 6). It shows a sentence for a few seconds, stacks a second one, and hides when empty. Tokens only.

**Files:**
- Create: `client/Assets/UI/Components/NoticeToast.cs`, `client/Assets/UI/Components/Resources/NoticeToast.uxml`, `client/Assets/UI/Components/Resources/NoticeToast.uss`
- Modify: `client/Assets/UI/Shell/Shell.uxml`, `client/Assets/UI/Shell/Shell.uss`, `client/Assets/UI/Shell/Tokens.uss`
- Modify: `client/Assets/Game/Shell/BootController.cs`, `client/Assets/Game/Shell/OutboxPump.cs`
- Test: `client/Assets/UI/Tests/ComponentTests.cs`

**Interfaces:**
- Produces: `NoticeToast : VisualElement` with `const string UssClassName = "notice-toast"`, `const int HoldMs = 4000`, `void Show(string text)`, `int Pending` (count of rows currently shown). `OutboxPump.OnNotice : Action<string>` (a public field, invoked after `_notices.Add`).

- [ ] **Step 1: The failing tests**

In `client/Assets/UI/Tests/ComponentTests.cs`, add:

```csharp
        [Test]
        public void NoticeToast_IsHiddenUntilShown_AndStacksRows()
        {
            var toast = new NoticeToast();
            Assert.AreEqual(DisplayStyle.None, toast.style.display.value, "empty toast must not occupy the layer");

            toast.Show("Your roster could not be loaded. Try again.");
            Assert.AreEqual(DisplayStyle.Flex, toast.style.display.value);
            Assert.AreEqual(1, toast.Pending);
            StringAssert.Contains("roster", toast.Q<Label>(className: NoticeToast.RowUssClassName).text);

            toast.Show("A second sentence.");
            Assert.AreEqual(2, toast.Pending, "a second notice stacks rather than replacing the first");
        }

        [Test]
        public void NoticeToast_IgnoresEmptyText()
        {
            var toast = new NoticeToast();
            toast.Show(null);
            toast.Show("   ");
            Assert.AreEqual(0, toast.Pending);
            Assert.AreEqual(DisplayStyle.None, toast.style.display.value);
        }
```

- [ ] **Step 2: Run to see them fail**

```bash
./implementation/scripts/run-unity-tests.sh EditMode 2>&1 | tail -8
```

Expected: a compile error naming `NoticeToast` (Unity reports it in the log; the results file is not written). That is the red.

- [ ] **Step 3: The component**

`client/Assets/UI/Components/Resources/NoticeToast.uxml`:

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements" editor-extension-mode="False">
    <ui:Style src="NoticeToast.uss" />
    <ui:VisualElement name="rows" class="notice-toast__rows" />
</ui:UXML>
```

`client/Assets/UI/Components/Resources/NoticeToast.uss`:

```css
/* The notice surface. Sits in #notice-layer, a sibling of #shell-root, so it
 * is drawn whether or not the shell is - a blocked beat's sentence must
 * reach the player during a wave too. Anchored to the top, under the safe
 * area the layer applies. */
.notice-toast {
    position: absolute;
    left: var(--gutter);
    right: var(--gutter);
    top: var(--space-6);
    flex-direction: column;
}

.notice-toast__rows { flex-direction: column; }

.notice-toast__row {
    background-color: var(--ink-deep);
    color: var(--surface);
    border-radius: var(--radius-row);
    padding-top: var(--space-3);
    padding-bottom: var(--space-3);
    padding-left: var(--space-4);
    padding-right: var(--space-4);
    margin-bottom: var(--space-2);
    font-size: var(--text-body);
    white-space: normal;
    opacity: 0.96;
}
```

`client/Assets/UI/Components/NoticeToast.cs`:

```csharp
using UnityEngine;
using UnityEngine.UIElements;

namespace Broodline.UI.Components
{
    /// The notice surface. Phase 9 design §2.1: `BootController.OnNotice`
    /// used to log and stop, and `OutboxPump.Notices` filled a list nothing
    /// rendered. A sentence shown here for `HoldMs` is what turns a tester's
    /// "it went blank" into "it said X".
    ///
    /// STACKS, NEVER REPLACES. Two notices in one frame are two facts.
    ///
    /// HIDDEN WHEN EMPTY. The element sits in an absolutely positioned layer
    /// over everything; an empty toast that still occupied the layer would
    /// swallow taps on the row beneath it.
    ///
    /// The hold timer uses the element's scheduler, which only ticks with a
    /// live panel. Without one (every EditMode test) rows simply stay, which
    /// is what the structure tests assert against.
    public sealed class NoticeToast : VisualElement
    {
        public const string UssClassName = "notice-toast";
        public const string RowUssClassName = "notice-toast__row";
        public const int HoldMs = 4000;

        readonly VisualElement _rows;

        public NoticeToast()
        {
            AddToClassList(UssClassName);
            Resources.Load<VisualTreeAsset>("NoticeToast").CloneTree(this);
            _rows = this.Q<VisualElement>("rows");
            pickingMode = PickingMode.Ignore;
            _rows.pickingMode = PickingMode.Ignore;
            style.display = DisplayStyle.None;
        }

        public int Pending => _rows.childCount;

        public void Show(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            var row = new Label(text.Trim());
            row.AddToClassList(RowUssClassName);
            row.pickingMode = PickingMode.Ignore;
            _rows.Add(row);
            style.display = DisplayStyle.Flex;

            row.schedule.Execute(() =>
            {
                row.RemoveFromHierarchy();
                if (_rows.childCount == 0) style.display = DisplayStyle.None;
            }).StartingIn(HoldMs);
        }
    }
}
```

- [ ] **Step 4: The layer in the shell**

`client/Assets/UI/Shell/Shell.uxml` — add after `#shell-root`'s closing tag, inside the UXML root:

```xml
    <ui:VisualElement name="notice-layer" class="notice-layer" picking-mode="Ignore" />
```

`client/Assets/UI/Shell/Shell.uss` — in the layout half, after `.sheet-layer`:

```css
/* Sibling of #shell-root, not a child: Task 6 hides the shell while a wave
 * is resident, and a notice must still reach the player then. Picking is
 * ignored so the layer never swallows a tap; only its rows are visible. */
.notice-layer {
    position: absolute;
    top: 0;
    left: 0;
    right: 0;
    bottom: 0;
}
```

No new tokens are needed: `--ink-deep`, `--surface`, `--radius-row`, `--space-*`, `--gutter`, `--text-body` exist.

- [ ] **Step 5: Wire it**

`client/Assets/Game/Shell/OutboxPump.cs` — add a public field beside `Notices` and invoke it where `_notices.Add(notice)` is (line ~112):

```csharp
        /// The surface, when one is attached. `Notices` keeps the log either way.
        public Action<string> OnNotice;
```

```csharp
            _notices.Add(notice);
            if (_notices.Count > MaxNotices) _notices.RemoveRange(0, _notices.Count - MaxNotices);
            OnNotice?.Invoke(notice);
```

(`using System;` if the file lacks it.)

`client/Assets/Game/Shell/BootController.cs` — a field `NoticeToast _toast;`, and in `Start()` right after the tab bar is added (line ~66):

```csharp
            var noticeLayer = root.Q<VisualElement>("notice-layer");
            _toast = new NoticeToast();
            noticeLayer.Add(_toast);
```

After `_pump.Configure(_outbox);`:

```csharp
            _pump.OnNotice = OnNotice;
```

Replace `OnNotice`'s body and its header comment:

```csharp
        /// Where a blocked beat's sentence goes: the toast, and the log so a
        /// capture still carries it. Phase 9 Task 4 closed the gap Phase 7
        /// Task 13 recorded here.
        void OnNotice(string notice)
        {
            if (string.IsNullOrEmpty(notice)) return;
            Debug.LogWarning("[Ftue] " + notice);
            _toast?.Show(notice);
        }
```

Add `using Broodline.UI.Components;`.

- [ ] **Step 6: Gates**

```bash
./implementation/scripts/run-unity-tests.sh EditMode 2>&1 | tail -4
bash implementation/scripts/check-stylesheets.sh | tail -3
bash implementation/scripts/verify-uss-tokens.sh | tail -3
```

Expected: `total=325 passed=324 failed=1`; both scripts green.

- [ ] **Step 7: Commit**

```bash
git add client/Assets/UI/Components/NoticeToast.cs client/Assets/UI/Components/NoticeToast.cs.meta \
        client/Assets/UI/Components/Resources/NoticeToast.uxml client/Assets/UI/Components/Resources/NoticeToast.uxml.meta \
        client/Assets/UI/Components/Resources/NoticeToast.uss client/Assets/UI/Components/Resources/NoticeToast.uss.meta \
        client/Assets/UI/Shell/Shell.uxml client/Assets/UI/Shell/Shell.uss \
        client/Assets/Game/Shell/BootController.cs client/Assets/Game/Shell/OutboxPump.cs \
        client/Assets/UI/Tests/ComponentTests.cs
git commit -m "feat(shell): a notice toast, in a layer beside the shell root

A blocked beat's sentence reaches the player instead of a console. The
layer is a sibling of #shell-root so it survives the shell being hidden
during a wave. Stacks, never replaces; hidden when empty so it never
swallows a tap.

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

## Task 5: The forfeit sheet, in the director

Design §2.1 part 3, relocated per §"What this plan found the design missed" 1. After the director loads the roster and before it derives a beat, if any known creature is committed it shows `AbandonedWaveSheet` with one button, **Forfeit**. Forfeit calls `AbandonWaveAsync`, reloads the roster, and the loop continues. If the roster is still locked afterwards (the server refused, or the network failed), the director says so through the notice and returns — which now shows a toast rather than a blank screen.

**Files:**
- Create: `client/Assets/UI/Components/AbandonedWaveSheet.cs`, `Resources/AbandonedWaveSheet.uxml`, `Resources/AbandonedWaveSheet.uss`
- Modify: `client/Assets/Game/Ftue/FtueDirector.cs`
- Test: `client/Assets/Game/Tests/FtueDirectorTests.cs`, `client/Assets/UI/Tests/ComponentTests.cs`

**Interfaces:**
- Consumes: `BroodlineApiClient.AbandonWaveAsync()` (Task 1), `ScreenFlow.ShowSheetAsync<T>(Func<Action<T>, VisualElement> build)`, `RosterScreen.Known`, `LoadRosterAsync()`.
- Produces: `AbandonedWaveSheet(Action onForfeit)` with `UssClassName = "abandoned-wave-sheet"`, `const string Headline`, `const string Body`, `const string ForfeitLabel`; `static bool FtueDirector.NeedsForfeit(IReadOnlyList<CreatureDto> known)`; `const string FtueNotice.ForfeitFailed`.

- [ ] **Step 1: The failing tests**

`client/Assets/Game/Tests/FtueDirectorTests.cs`:

```csharp
        [Test]
        public void NeedsForfeit_IsTrueWhenAnyKnownCreatureIsCommitted()
        {
            var live = Guid.NewGuid();
            Assert.IsFalse(FtueDirector.NeedsForfeit(new[] { Creature("Vetch"), Creature("Ember") }));
            Assert.IsTrue(FtueDirector.NeedsForfeit(new[] { Creature("Vetch"), Creature("Ember", committedTo: live) }));
            Assert.IsFalse(FtueDirector.NeedsForfeit(new CreatureDto[0]), "an empty roster has nothing to forfeit");
            Assert.IsFalse(FtueDirector.NeedsForfeit(new CreatureDto[] { null }), "a null slot is skipped, as FightAsync skips it");
        }
```

`client/Assets/UI/Tests/ComponentTests.cs`:

```csharp
        [Test]
        public void AbandonedWaveSheet_StatesTheSituationAndOffersOnlyForfeit()
        {
            var sheet = new AbandonedWaveSheet(onForfeit: () => { });
            Assert.AreEqual(AbandonedWaveSheet.Headline, sheet.Q<Label>("headline").text);
            Assert.AreEqual(AbandonedWaveSheet.Body, sheet.Q<Label>("body").text);
            var buttons = sheet.Query<Button>().ToList();
            Assert.AreEqual(1, buttons.Count, "one way out, and it is forfeit");
            Assert.AreEqual(AbandonedWaveSheet.ForfeitLabel, buttons[0].text);
            Assert.IsTrue(buttons[0].ClassListContains("btn-primary"));
            Assert.IsNull(sheet.Q<VisualElement>(className: ScreenScaffold.UssClassName), "a sheet takes no scaffold");
        }
```

- [ ] **Step 2: Run to see them fail**

```bash
./implementation/scripts/run-unity-tests.sh EditMode 2>&1 | tail -6
```

Expected: compile error naming `NeedsForfeit` / `AbandonedWaveSheet`.

- [ ] **Step 3: The sheet**

`Resources/AbandonedWaveSheet.uxml`:

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements" editor-extension-mode="False">
    <ui:Style src="AbandonedWaveSheet.uss" />
    <ui:VisualElement name="scrim" class="abandoned-wave-sheet__scrim" />
    <ui:VisualElement name="card" class="abandoned-wave-sheet__card elev-2">
        <ui:Label name="eyebrow" class="abandoned-wave-sheet__eyebrow t-micro" />
        <ui:Label name="headline" class="abandoned-wave-sheet__headline t-section" />
        <ui:Label name="body" class="abandoned-wave-sheet__body t-body" />
        <ui:Button name="forfeit" class="btn-primary" />
    </ui:VisualElement>
</ui:UXML>
```

`Resources/AbandonedWaveSheet.uss`:

```css
.abandoned-wave-sheet { position: absolute; top: 0; left: 0; right: 0; bottom: 0; justify-content: flex-end; }
.abandoned-wave-sheet__scrim { position: absolute; top: 0; left: 0; right: 0; bottom: 0; background-color: var(--hud-scrim); }
.abandoned-wave-sheet__card {
    background-color: var(--surface);
    border-top-left-radius: var(--radius-card);
    border-top-right-radius: var(--radius-card);
    padding-top: var(--space-6);
    padding-bottom: var(--space-6);
    padding-left: var(--space-4);
    padding-right: var(--space-4);
}
.abandoned-wave-sheet__eyebrow { color: var(--mute); margin-bottom: var(--space-1); }
.abandoned-wave-sheet__headline { margin-bottom: var(--space-2); }
.abandoned-wave-sheet__body { color: var(--ink); white-space: normal; margin-bottom: var(--space-4); }
```

`AbandonedWaveSheet.cs`:

```csharp
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Broodline.UI.Components
{
    /// "A wave was left unfinished." Phase 9 design §2.1: the only way out of
    /// a roster committed to a wave nobody will submit. One button, because
    /// resume is Phase 10's on playtest evidence, and a sheet that offers a
    /// choice it cannot honour is worse than one that offers none.
    ///
    /// A SHEET, LIKE `CodexSheet`: no scaffold, destined for `#sheet-layer`
    /// through `ScreenFlow.ShowSheetAsync`, which hides it on resume.
    public sealed class AbandonedWaveSheet : VisualElement
    {
        public const string UssClassName = "abandoned-wave-sheet";
        public const string Eyebrow = "Unfinished wave";
        public const string Headline = "A wave was left unfinished.";
        public const string Body =
            "Your creatures are still out there. Forfeit the wave to bring them home - nothing is lost but the fight.";
        public const string ForfeitLabel = "Forfeit";

        public AbandonedWaveSheet(Action onForfeit)
        {
            AddToClassList(UssClassName);
            Resources.Load<VisualTreeAsset>("AbandonedWaveSheet").CloneTree(this);

            this.Q<Label>("eyebrow").text = Eyebrow;
            this.Q<Label>("headline").text = Headline;
            this.Q<Label>("body").text = Body;

            var forfeit = this.Q<Button>("forfeit");
            forfeit.text = ForfeitLabel;
            if (onForfeit != null) forfeit.clicked += onForfeit;
        }
    }
}
```

- [ ] **Step 4: The director**

In `client/Assets/Game/Ftue/FtueDirector.cs`, add to `FtueNotice`:

```csharp
        public const string ForfeitFailed =
            "Your creatures are still out fighting and the wave could not be forfeited. Try again in a moment.";
```

Add the predicate beside `WantedFor`:

```csharp
        /// Whether any known creature is committed to a live issuance - the
        /// state Phase 8's eyes-on pass found in its first minute, in which
        /// `FightAsync` would find nothing to deploy and the walk would end.
        public static bool NeedsForfeit(IReadOnlyList<CreatureDto> known)
        {
            if (known == null) return false;
            foreach (var creature in known)
                if (creature != null && creature.CommittedTo != null) return true;
            return false;
        }
```

Add the step, and call it in `RunAsync` between `LoadRosterAsync` and `Derive`:

```csharp
                if (!await LoadRosterAsync()) return;
                if (!await ForfeitIfLockedAsync()) return;

                var beat = Ftue.Derive(snapshot, _roster.Known);
```

```csharp
        /// Phase 9 design §2.1 part 3. Shown BEFORE the beat is derived, so
        /// `FightAsync` never meets an all-committed roster. Returns false only
        /// when the forfeit did not free the roster - the server refused, or
        /// the call failed - in which case the notice says so and the walk
        /// stops on a toast rather than a blank screen.
        async Task<bool> ForfeitIfLockedAsync()
        {
            if (!NeedsForfeit(_roster.Known)) return true;

            await _flow.ShowSheetAsync<bool>(resume =>
                new AbandonedWaveSheet(onForfeit: () => resume(true)));

            try
            {
                await _api.AbandonWaveAsync();
            }
            catch (Exception error)
            {
                _notice(ServerError.From(error).PlayerMessage);
                return false;
            }

            if (!await LoadRosterAsync()) return false;
            if (NeedsForfeit(_roster.Known))
            {
                _notice(FtueNotice.ForfeitFailed);
                return false;
            }
            return true;
        }
```

(`using Broodline.UI.Components;` if absent. No `ConfigureAwait` anywhere — the file's own rule.)

- [ ] **Step 5: Gates**

```bash
./implementation/scripts/run-unity-tests.sh EditMode 2>&1 | tail -4
bash implementation/scripts/check-stylesheets.sh | tail -2
bash implementation/scripts/verify-uss-tokens.sh | tail -2
```

Expected: `total=327 passed=326 failed=1`; scripts green.

- [ ] **Step 6: See it, once, in the Editor**

Open the Editor, run `Boot.unity` against the local stack (`docs/local-stack.md`), start a wave, stop Play mid-wave, press Play again. Expected: the sheet, then Forfeit, then the first-hour screen. Record one line in `implementation/results/phase9-blockers.txt`: `forfeit sheet seen: <date>`.

- [ ] **Step 7: Commit**

```bash
git add client/Assets/UI/Components/AbandonedWaveSheet.cs* client/Assets/UI/Components/Resources/AbandonedWaveSheet.* \
        client/Assets/Game/Ftue/FtueDirector.cs client/Assets/Game/Tests/FtueDirectorTests.cs \
        client/Assets/UI/Tests/ComponentTests.cs
git commit -m "feat(ftue): a locked roster is forfeited from a sheet, not stranded

Shown after the roster loads and before the beat is derived, so FightAsync
never meets an all-committed roster. One button: resume is Phase 10's.
A forfeit that does not free the roster stops the walk on a toast.

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

## Task 6: The shell hides while a wave is resident — and this time it is proven

Design §2.2. `WaveHost` takes a `setShellVisible` callback. It hides the shell **after** the additive load completes and the runner is found, and restores it as the **first** statement of the `finally`, wrapped so a throwing callback cannot skip the unload. The reverted attempt hid before the load and had no exception-path restore. A PlayMode test records the callback's calls through a completed wave and through the forced-throw path.

**Files:**
- Modify: `client/Assets/Game/Shell/WaveHost.cs`, `client/Assets/Game/Shell/BootController.cs`
- Modify: `client/Assets/Game/Tests/PlayMode/WaveCapturePlayTests.cs`

**Interfaces:**
- Produces: `WaveHost(Func<IReadOnlyList<TraitSummary>> traits, Action<bool> setShellVisible = null)`. The existing two-site construction in the PlayMode tests keeps compiling through the default.

- [ ] **Step 1: The failing PlayMode assertions**

In `WaveCapturePlayTests.cs`, change the two constructions and add assertions. In `AHostedWave_ReportsItsOutcomeAndWritesNoCapture`:

```csharp
            var visibility = new List<bool>();
            var host = new WaveHost(() => Bundle, visible => visibility.Add(visible));
            var run = host.RunAsync(
                WaveRunner.CaptureWaveId, WaveRunner.Deployment(), WaveRunner.Seed, inputEnabled: false);

            WaveRunner hosted = null;
            yield return Until(() => (hosted = UnityEngine.Object.FindAnyObjectByType<WaveRunner>()) != null &&
                                     hosted.Runner != null,
                               "WaveHost never configured a runner");

            // Phase 9 design §2.2: hidden AFTER the scene is resident, never
            // before the load - and hidden by now, because the runner is.
            CollectionAssert.AreEqual(new[] { false }, visibility,
                "the shell must be hidden exactly once by the time the runner is configured");
```

and after the existing `Assert.IsFalse(WaveRunner.Hosted, ...)` at the end:

```csharp
            CollectionAssert.AreEqual(new[] { false, true }, visibility,
                "the shell must be restored when the run ends");
```

In `AHostedWaveThatThrows_StillUnloadsTheBattlefield`:

```csharp
            var visibility = new List<bool>();
            var host = new WaveHost(() => Bundle, visible => visibility.Add(visible));
            var run = host.RunAsync(999, WaveRunner.Deployment(), WaveRunner.Seed, inputEnabled: false);
```

and at the end:

```csharp
            // The white screen the reverted fix produced is consistent with the
            // restore never running on this path. It runs on this path.
            CollectionAssert.AreEqual(new[] { false, true }, visibility,
                "hidden after the load, restored by the finally, even when Configure throws");
```

Add `using System.Collections.Generic;` if absent.

- [ ] **Step 2: Run them to see them fail**

Open the Editor → Window → General → Test Runner → PlayMode → run `WaveCapturePlayTests`. Expected: both fail to compile until Step 3 (the two-argument constructor does not exist); after Step 3 without Step 4's body they fail on the empty `visibility` list. Close the Editor before running any script.

- [ ] **Step 3: The host**

`client/Assets/Game/Shell/WaveHost.cs`:

```csharp
        readonly Func<IReadOnlyList<TraitSummary>> _traits;
        readonly Action<bool> _setShellVisible;

        public WaveHost(Func<IReadOnlyList<TraitSummary>> traits, Action<bool> setShellVisible = null)
        {
            _traits = traits ?? throw new ArgumentNullException(nameof(traits));
            _setShellVisible = setShellVisible ?? (_ => { });
        }
```

In `RunAsync`, after `var runner = FindRunner();`:

```csharp
                // Phase 9 design §2.2. AFTER the load, not before: the wave
                // scene's UIDocument is a sibling root in the shared panel and
                // is attached by now, so hiding the shell here leaves the HUD
                // and the battlefield on screen and nothing else. Hidden
                // before the load, the frame between hide and attach showed
                // whatever the last camera cleared to - the reverted fix.
                _setShellVisible(false);
```

In the `finally`, as its first statements, before `await UnloadAsync();`:

```csharp
                // RESTORE FIRST, and on every path. The exception path is the
                // one the reverted fix never restored. Guarded so a throwing
                // callback cannot skip the unload below.
                try { _setShellVisible(true); }
                catch (Exception error) { Debug.LogError("[WaveHost] setShellVisible(true) threw: " + error); }
```

- [ ] **Step 4: The boot wiring**

`client/Assets/Game/Shell/BootController.cs`, where `_waves` is constructed:

```csharp
            var shellRoot = root.Q<VisualElement>("shell-root");
            _waves = new WaveHost(
                () => _session.Snapshot?.Traits,
                visible => shellRoot.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None);
```

`#shell-root` is `Shell.uxml`'s single child of the document root; an inline `display` wins over `Shell.uss:20` and `Theme.uss:100`.

- [ ] **Step 5: EditMode green before anything else**

```bash
./implementation/scripts/run-unity-tests.sh EditMode 2>&1 | tail -4
```

Expected: `total=327 passed=326 failed=1` (no EditMode test changes; this is the compile-and-regress gate the reverted fix skipped).

- [ ] **Step 6: PlayMode green, in the Editor**

Open the Editor → Test Runner → PlayMode → run `WaveCapturePlayTests`. Expected: all pass, including the two amended tests. Record in `implementation/results/phase9-blockers.txt`: `WaveCapturePlayTests PlayMode: <n> passed, <date>, Editor 6000.6.0f1`.

- [ ] **Step 7: See it in a running wave**

Still in the Editor, run `Boot.unity` against the local stack, reach a wave. Expected: the battlefield and HUD, no paper-coloured shell over them; the post-wave screen afterwards with the shell back. Append to `phase9-blockers.txt`: `shell hidden during wave, restored after: seen <date>`. Close the Editor.

- [ ] **Step 8: Commit**

```bash
git add client/Assets/Game/Shell/WaveHost.cs client/Assets/Game/Shell/BootController.cs \
        client/Assets/Game/Tests/PlayMode/WaveCapturePlayTests.cs
git add -f implementation/results/phase9-blockers.txt
git commit -m "fix(shell): hide the shell root while the wave scene is resident

After the additive load completes and the runner is found, not before the
load; restored as the first statement of the finally, on the completion
path and the exception path both. The PlayMode capture tests now record
the visibility calls on both paths, and the EditMode suite ran green
before this landed - the two things the reverted attempt did not have.

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

## Task 7: `Broodline.Creatures` — recipes, the mesher, the shader, and Vetch

Design §3.2–§3.4, §3.6. A new runtime assembly that references nothing project-side. A body is a recipe of primitives, bones and sockets; an Editor generator evaluates it as a signed distance field, meshes it with naive surface nets on a fixed grid, assigns at most two bone influences per vertex, and writes a `Mesh` asset, a material and a prefab under `Resources` so the lane and the studio can load them. One hand-written URP shader shades everything. Vetch and its two parts plus Cinder are authored here; the rest in Task 15.

**One number moves from the design.** §3.4 said under 1500 triangles per body. Naive surface nets at a grid that reads as smooth (24 cells across a body) produce about 2000–2500. The budget here is **2500 per body, 400 per part** — a quarter of the 10000 `rig_proof.md` §8 measured at 104 entities, with the margin that document says not to spend still unspent. Recorded as an erratum in Task 22.

**Files:**
- Create: `client/Assets/Creatures/Broodline.Creatures.asmdef`
- Create: `client/Assets/Creatures/Recipe.cs`, `Sdf.cs`, `SurfaceNets.cs`, `SpeciesColours.cs`
- Create: `client/Assets/Creatures/Recipes/SpeciesRecipes.cs`, `Recipes/PartRecipes.cs`, `Recipes/RaiderRecipes.cs` (empty registry until Task 15)
- Create: `client/Assets/Creatures/Shaders/Creature.shader`
- Create: `client/Assets/Creatures/Editor/Broodline.Creatures.Editor.asmdef`, `Editor/CreatureGenerator.cs`
- Create: `client/Assets/Creatures/Tests/Broodline.Creatures.Tests.asmdef`, `Tests/RecipeTests.cs`, `Tests/MesherTests.cs`, `Tests/DriftTests.cs`
- Create: `implementation/scripts/generate-creatures.sh`
- Generated and committed: `client/Assets/Creatures/Resources/Creatures/Meshes/*.asset`, `Materials/*.mat`, `Bodies/vetch.prefab`, `Parts/{carapace,taunt,cinder}.prefab`
- Modify: `client/Assets/Editor/TestHarness/EditModeRunner.cs` (`Assemblies`), `client/Assets/Editor/TestHarness/Broodline.TestHarness.asmdef` (references)

**Interfaces (produced, used by Tasks 8–11, 15):**

```csharp
namespace Broodline.Creatures
{
    public enum PrimitiveKind { Sphere, Capsule, Box }
    public struct Primitive { PrimitiveKind Kind; Vector3 A, B, Half; float Radius; string Bone; float Growth;
        static Primitive Sphere(Vector3 c, float r, string bone, float growth = 0f);
        static Primitive Capsule(Vector3 a, Vector3 b, float r, string bone, float growth = 0f);
        static Primitive Box(Vector3 c, Vector3 half, string bone, float growth = 0f); }
    public sealed class BoneDef { string Name; string Parent; Vector3 Position; }
    public sealed class SocketDef { string Name; Vector3 Position; Vector3 Euler; float Scale = 1f; }
    public static class Sockets { const string Dorsal = "sk_dorsal", Flank = "sk_flank", Crown = "sk_crown", Kit = "sk_kit"; }
    public sealed class BodyRecipe { string Id; Primitive[] Primitives; BoneDef[] Bones; SocketDef[] Sockets; float Blend = 0.22f; int Grid = 24; float Padding = 0.25f; bool Raider; }
    public sealed class PartRecipe { string Id; Primitive[] Primitives; float Blend = 0.06f; int Grid = 16; float Padding = 0.15f; Color Base; Color Under; }
    public static class SpeciesRecipes { IReadOnlyList<BodyRecipe> All; BodyRecipe For(string species); }   // "vetch" ... "pale", lowercase
    public static class PartRecipes { IReadOnlyList<PartRecipe> All; PartRecipe For(string trait); }        // "carapace" ... lowercase
    public static class RaiderRecipes { IReadOnlyList<BodyRecipe> All; BodyRecipe For(string raiderType); } // "courser","lash","skirmisher"
    public static class SpeciesColours { (Color Base, Color Under) For(string species); string BaseHex(string species); }
    public static class Sdf { float Field(Primitive[] prims, float blend, Vector3 x); Bounds BoundsOf(Primitive[] prims, float padding); }
    public sealed class GeneratedMesh { Vector3[] Vertices; Vector3[] Normals; int[] Triangles; BoneWeight[] Weights; }
    public static class SurfaceNets { GeneratedMesh Build(Primitive[] prims, float blend, Bounds bounds, int grid, string[] boneNames); }
    public static class CreaturePaths { const string MeshDir, MaterialDir, BodyDir, PartDir, RaiderDir; string Body(string id); string Part(string id); string Raider(string id); } // "Creatures/Bodies/vetch" etc. for Resources.Load
}
namespace Broodline.Creatures.Editor { public static class CreatureGenerator { void Generate(); GeneratedMesh Regenerate(BodyRecipe r); GeneratedMesh Regenerate(PartRecipe r); } }
```

- [ ] **Step 1: The assemblies**

`client/Assets/Creatures/Broodline.Creatures.asmdef`:

```json
{
  "name": "Broodline.Creatures",
  "rootNamespace": "Broodline.Creatures",
  "references": [],
  "includePlatforms": [],
  "excludePlatforms": [],
  "allowUnsafeCode": false,
  "overrideReferences": false,
  "precompiledReferences": [],
  "autoReferenced": true,
  "defineConstraints": [],
  "versionDefines": [],
  "noEngineReferences": false
}
```

`client/Assets/Creatures/Editor/Broodline.Creatures.Editor.asmdef`:

```json
{
  "name": "Broodline.Creatures.Editor",
  "rootNamespace": "Broodline.Creatures.Editor",
  "references": ["Broodline.Creatures"],
  "includePlatforms": ["Editor"],
  "excludePlatforms": [],
  "allowUnsafeCode": false,
  "overrideReferences": false,
  "precompiledReferences": [],
  "autoReferenced": true,
  "defineConstraints": [],
  "versionDefines": [],
  "noEngineReferences": false
}
```

`client/Assets/Creatures/Tests/Broodline.Creatures.Tests.asmdef`:

```json
{
  "name": "Broodline.Creatures.Tests",
  "rootNamespace": "Broodline.Creatures.Tests",
  "references": ["Broodline.Creatures", "Broodline.Creatures.Editor", "UnityEngine.TestRunner", "UnityEditor.TestRunner"],
  "includePlatforms": ["Editor"],
  "excludePlatforms": [],
  "overrideReferences": true,
  "precompiledReferences": ["nunit.framework.dll"],
  "autoReferenced": false,
  "defineConstraints": ["UNITY_INCLUDE_TESTS"],
  "versionDefines": [],
  "noEngineReferences": false
}
```

In `client/Assets/Editor/TestHarness/EditModeRunner.cs`, add `"Broodline.Creatures.Tests"` to `Assemblies`; in `Broodline.TestHarness.asmdef`, add it to `references`. The runner reports `assembly not loaded` otherwise and the tests never run.

- [ ] **Step 2: The failing tests**

`client/Assets/Creatures/Tests/RecipeTests.cs`:

```csharp
using System.Linq;
using Broodline.Creatures;
using NUnit.Framework;

namespace Broodline.Creatures.Tests
{
    public class RecipeTests
    {
        static readonly string[] Standard = { Sockets.Dorsal, Sockets.Flank, Sockets.Crown };

        [Test]
        public void EveryBody_CarriesTheThreeStandardSockets()
        {
            // rig_proof.md section 3: authored on every body, before the first
            // creature is modelled. A socket that has never carried geometry
            // is not standardised; a body without one is not a body.
            foreach (var body in SpeciesRecipes.All)
            {
                var names = body.Sockets.Select(s => s.Name).ToArray();
                CollectionAssert.IsSubsetOf(Standard, names, body.Id + " is missing a standard socket");
                Assert.IsTrue(body.Sockets.All(s => s.Scale > 0f), body.Id + " has a zero-scale socket");
            }
        }

        [Test]
        public void EveryBody_NamesOnlyBonesItDeclares()
        {
            foreach (var body in SpeciesRecipes.All)
            {
                var bones = body.Bones.Select(b => b.Name).ToHashSet();
                foreach (var p in body.Primitives)
                    Assert.IsTrue(bones.Contains(p.Bone), body.Id + " primitive names unknown bone " + p.Bone);
                Assert.LessOrEqual(body.Bones.Length, 8, body.Id + " exceeds eight bones");
                Assert.AreEqual(1, body.Bones.Count(b => b.Parent == null), body.Id + " must have exactly one root bone");
            }
        }

        [Test]
        public void SpeciesRecipes_AreLookedUpByLowercaseName_AndVetchExists()
        {
            Assert.IsNotNull(SpeciesRecipes.For("vetch"));
            Assert.IsNotNull(SpeciesRecipes.For("Vetch"), "case-insensitive, like SpeciesProxy was");
            Assert.IsNull(SpeciesRecipes.For("ash"), "an unknown species is null, never a guess");
        }

        [Test]
        public void PartRecipes_HaveColoursAndAreLookedUpByTrait()
        {
            foreach (var part in PartRecipes.All)
                Assert.AreNotEqual(part.Base, part.Under, part.Id + " needs a darker underside");
            Assert.IsNotNull(PartRecipes.For("cinder"));
            Assert.IsNotNull(PartRecipes.For("Carapace"));
        }
    }
}
```

`client/Assets/Creatures/Tests/MesherTests.cs`:

```csharp
using System.Linq;
using Broodline.Creatures;
using NUnit.Framework;
using UnityEngine;

namespace Broodline.Creatures.Tests
{
    public class MesherTests
    {
        public const int BodyTriangleBudget = 2500;
        public const int PartTriangleBudget = 400;

        static GeneratedMesh Mesh(BodyRecipe r) =>
            SurfaceNets.Build(r.Primitives, r.Blend, Sdf.BoundsOf(r.Primitives, r.Padding), r.Grid,
                              r.Bones.Select(b => b.Name).ToArray());

        [Test]
        public void EveryBody_IsInsideTheTriangleBudget_AndNotEmpty()
        {
            foreach (var r in SpeciesRecipes.All.Concat(RaiderRecipes.All))
            {
                var m = Mesh(r);
                var tris = m.Triangles.Length / 3;
                Assert.Greater(tris, 200, r.Id + " meshed to almost nothing - check its bounds");
                Assert.LessOrEqual(tris, BodyTriangleBudget, r.Id + " is over budget at " + tris);
            }
        }

        [Test]
        public void EveryPart_IsInsideItsBudget()
        {
            foreach (var p in PartRecipes.All)
            {
                var m = SurfaceNets.Build(p.Primitives, p.Blend, Sdf.BoundsOf(p.Primitives, p.Padding), p.Grid, new[] { "root" });
                var tris = m.Triangles.Length / 3;
                Assert.Greater(tris, 20, p.Id + " meshed to almost nothing");
                Assert.LessOrEqual(tris, PartTriangleBudget, p.Id + " is over budget at " + tris);
            }
        }

        [Test]
        public void Weights_UseAtMostTwoInfluences_AndSumToOne()
        {
            // QualitySettings.asset: the Mobile tier caps skinning at two
            // influences. A vertex authored with four would look different on
            // the phone than in the Editor, so the mesher never writes them.
            var m = Mesh(SpeciesRecipes.For("vetch"));
            var boneCount = SpeciesRecipes.For("vetch").Bones.Length;
            foreach (var w in m.Weights)
            {
                Assert.AreEqual(0f, w.weight2, "third influence must be zero");
                Assert.AreEqual(0f, w.weight3, "fourth influence must be zero");
                Assert.AreEqual(1f, w.weight0 + w.weight1, 1e-4f);
                Assert.Less(w.boneIndex0, boneCount);
                Assert.Less(w.boneIndex1, boneCount);
            }
        }

        [Test]
        public void Normals_AreUnitLength_AndTrianglesIndexRealVertices()
        {
            var m = Mesh(SpeciesRecipes.For("vetch"));
            Assert.AreEqual(m.Vertices.Length, m.Normals.Length);
            foreach (var n in m.Normals) Assert.AreEqual(1f, n.magnitude, 1e-3f);
            Assert.AreEqual(0, m.Triangles.Length % 3);
            Assert.IsTrue(m.Triangles.All(i => i >= 0 && i < m.Vertices.Length));
        }

        [Test]
        public void SmoothMin_IsMinAtZeroBlend_AndBelowMinOtherwise()
        {
            Assert.AreEqual(-1f, Sdf.SmoothMin(-1f, 2f, 0f));
            Assert.Less(Sdf.SmoothMin(0.1f, 0.1f, 0.5f), 0.1f, "two touching shapes blend into one");
        }
    }
}
```

`client/Assets/Creatures/Tests/DriftTests.cs`:

```csharp
using System.Linq;
using Broodline.Creatures;
using Broodline.Creatures.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Broodline.Creatures.Tests
{
    /// The drift gate. A recipe edited without `Broodline > Generate Creatures`
    /// (or generate-creatures.sh) fails here, so the committed assets are
    /// always what the recipes say.
    public class DriftTests
    {
        static string Hash(GeneratedMesh m)
        {
            unchecked
            {
                long h = 17;
                foreach (var v in m.Vertices)
                    h = h * 31 + Mathf.RoundToInt(v.x * 1000f) * 7 + Mathf.RoundToInt(v.y * 1000f) * 13 + Mathf.RoundToInt(v.z * 1000f) * 17;
                foreach (var i in m.Triangles) h = h * 31 + i;
                return h.ToString("x");
            }
        }

        static string Hash(Mesh m)
        {
            var g = new GeneratedMesh { Vertices = m.vertices, Triangles = m.triangles };
            return Hash(g);
        }

        [Test]
        public void EveryCommittedBodyMesh_MatchesItsRecipe()
        {
            foreach (var r in SpeciesRecipes.All.Concat(RaiderRecipes.All))
            {
                var path = "Assets/Creatures/Resources/" + CreaturePaths.MeshDir + "/" + r.Id + ".asset";
                var committed = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                Assert.IsNotNull(committed, "no committed mesh at " + path + " - run generate-creatures.sh");
                Assert.AreEqual(Hash(CreatureGenerator.Regenerate(r)), Hash(committed),
                    r.Id + " drifted from its recipe - run generate-creatures.sh and commit");
            }
        }

        [Test]
        public void EveryCommittedPartMesh_MatchesItsRecipe()
        {
            foreach (var p in PartRecipes.All)
            {
                var path = "Assets/Creatures/Resources/" + CreaturePaths.MeshDir + "/part-" + p.Id + ".asset";
                var committed = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                Assert.IsNotNull(committed, "no committed mesh at " + path);
                Assert.AreEqual(Hash(CreatureGenerator.Regenerate(p)), Hash(committed), p.Id + " drifted");
            }
        }

        [Test]
        public void EveryPrefab_Exists_AndCarriesItsSockets()
        {
            foreach (var r in SpeciesRecipes.All)
            {
                var prefab = Resources.Load<GameObject>(CreaturePaths.Body(r.Id));
                Assert.IsNotNull(prefab, "no prefab for " + r.Id);
                foreach (var s in r.Sockets)
                    Assert.IsNotNull(prefab.transform.Find(s.Name), r.Id + " prefab lacks socket " + s.Name);
                Assert.IsNotNull(prefab.GetComponentInChildren<SkinnedMeshRenderer>(), r.Id + " has no skinned renderer");
            }
            foreach (var p in PartRecipes.All)
                Assert.IsNotNull(Resources.Load<GameObject>(CreaturePaths.Part(p.Id)), "no prefab for part " + p.Id);
        }
    }
}
```

- [ ] **Step 3: Run to see them fail**

```bash
./implementation/scripts/run-unity-tests.sh EditMode 2>&1 | tail -6
```

Expected: compile errors (nothing under `Broodline.Creatures` exists).

- [ ] **Step 4: `Recipe.cs`**

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Broodline.Creatures
{
    /// A creature is a recipe, not a model. Phase 9 design §3.3: a list of
    /// blended primitives, each tagged with the bone that moves it and how
    /// much it grows from runt to apex; sockets as named transforms per
    /// rig_proof.md section 3; the mesh is generated from this and committed.
    ///
    /// CONVENTIONS, so every recipe agrees with every other:
    ///   +X is forward (the snout), +Y is up, +Z is the creature's left.
    ///   A body stands on y = 0 and is about one unit long.
    ///   A part is authored at the origin with +Y pointing out of the body
    ///   surface and +X forward; the socket's transform places and sizes it.
    public enum PrimitiveKind { Sphere, Capsule, Box }

    public struct Primitive
    {
        public PrimitiveKind Kind;
        public Vector3 A;       // centre (Sphere, Box) or first end (Capsule)
        public Vector3 B;       // second end (Capsule)
        public Vector3 Half;    // half extents (Box)
        public float Radius;    // Sphere, Capsule
        public string Bone;
        public float Growth;    // 0 = does not grow; 1 = doubles from runt to apex

        public static Primitive Sphere(Vector3 c, float r, string bone, float growth = 0f) =>
            new Primitive { Kind = PrimitiveKind.Sphere, A = c, Radius = r, Bone = bone, Growth = growth };
        public static Primitive Capsule(Vector3 a, Vector3 b, float r, string bone, float growth = 0f) =>
            new Primitive { Kind = PrimitiveKind.Capsule, A = a, B = b, Radius = r, Bone = bone, Growth = growth };
        public static Primitive Box(Vector3 c, Vector3 half, string bone, float growth = 0f) =>
            new Primitive { Kind = PrimitiveKind.Box, A = c, Half = half, Bone = bone, Growth = growth };
    }

    public sealed class BoneDef
    {
        public string Name;
        public string Parent;   // null on the one root
        public Vector3 Position;
    }

    public sealed class SocketDef
    {
        public string Name;
        public Vector3 Position;
        public Vector3 Euler;
        public float Scale = 1f;
    }

    public static class Sockets
    {
        public const string Dorsal = "sk_dorsal";
        public const string Flank = "sk_flank";
        public const string Crown = "sk_crown";
        public const string Kit = "sk_kit";
    }

    public sealed class BodyRecipe
    {
        public string Id;
        public Primitive[] Primitives;
        public BoneDef[] Bones;
        public SocketDef[] Sockets;
        public float Blend = 0.22f;
        public int Grid = 24;
        public float Padding = 0.25f;
        public bool Raider;
    }

    public sealed class PartRecipe
    {
        public string Id;
        public Primitive[] Primitives;
        public float Blend = 0.06f;
        public int Grid = 16;
        public float Padding = 0.15f;
        public Color Base;
        public Color Under;
    }

    /// What the assembler needs to build one creature. Strings, because this
    /// assembly references no engine enum and the roster carries strings.
    public sealed class CreatureLook
    {
        public string Species;
        public string Trait1;
        public string Trait2;
        public float Growth01;
        public string RaiderType;   // set instead of Species for a raider
    }

    public static class CreaturePaths
    {
        public const string MeshDir = "Creatures/Meshes";
        public const string MaterialDir = "Creatures/Materials";
        public const string BodyDir = "Creatures/Bodies";
        public const string PartDir = "Creatures/Parts";
        public const string RaiderDir = "Creatures/Raiders";
        public static string Body(string id) => BodyDir + "/" + id;
        public static string Part(string id) => PartDir + "/" + id;
        public static string Raider(string id) => RaiderDir + "/" + id;
    }
}
```

- [ ] **Step 5: `SpeciesColours.cs`**

```csharp
using System;
using UnityEngine;

namespace Broodline.Creatures
{
    /// Base and underside per species. THE BASE HEXES ARE MIRRORED FROM
    /// `PaletteContrast.Species` (Broodline.UI) because this assembly
    /// references nothing; `CreatureColourTests` in Broodline.Game.Tests
    /// asserts the mirror. The underside is the value structure bible 10.4
    /// measured Pale as needing - darker on every species, and cooler on
    /// Pale, whose base is the palest colour in the game.
    public static class SpeciesColours
    {
        static readonly (string Name, string Base, string Under)[] Table =
        {
            ("vetch",   "#6ba7c0", "#3f6f86"),
            ("ember",   "#e5867a", "#a8574d"),
            ("skitter", "#e8b34a", "#a67a22"),
            ("hollow",  "#7a6ac0", "#4d4088"),
            ("loam",    "#7cc492", "#4c8a60"),
            ("pale",    "#c6cede", "#8f9bb5"),
        };

        public static string BaseHex(string species)
        {
            foreach (var row in Table)
                if (string.Equals(row.Name, species, StringComparison.OrdinalIgnoreCase)) return row.Base;
            return null;
        }

        public static (Color Base, Color Under) For(string species)
        {
            foreach (var row in Table)
                if (string.Equals(row.Name, species, StringComparison.OrdinalIgnoreCase))
                    return (Parse(row.Base), Parse(row.Under));
            return (Color.magenta, Color.black);   // loud, never silent
        }

        public static readonly Color RaiderBase = Parse("#3a3547");
        public static readonly Color RaiderUnder = Parse("#221f2c");
        public static readonly Color RaiderAccent = Parse("#ff6b5c");

        public static Color Parse(string hex)
        {
            if (!ColorUtility.TryParseHtmlString(hex, out var c)) throw new ArgumentException("not a colour: " + hex);
            return c;
        }
    }
}
```

- [ ] **Step 6: `Sdf.cs`**

```csharp
using UnityEngine;

namespace Broodline.Creatures
{
    public static class Sdf
    {
        public static float Sphere(Vector3 p, Vector3 c, float r) => (p - c).magnitude - r;

        public static float Capsule(Vector3 p, Vector3 a, Vector3 b, float r)
        {
            var pa = p - a; var ba = b - a;
            float h = Mathf.Clamp01(Vector3.Dot(pa, ba) / Mathf.Max(1e-6f, Vector3.Dot(ba, ba)));
            return (pa - ba * h).magnitude - r;
        }

        public static float Box(Vector3 p, Vector3 c, Vector3 half)
        {
            var q = new Vector3(Mathf.Abs(p.x - c.x) - half.x, Mathf.Abs(p.y - c.y) - half.y, Mathf.Abs(p.z - c.z) - half.z);
            var outside = Vector3.Max(q, Vector3.zero).magnitude;
            var inside = Mathf.Min(Mathf.Max(q.x, Mathf.Max(q.y, q.z)), 0f);
            return outside + inside;
        }

        /// Polynomial smooth minimum. k = 0 is a hard union.
        public static float SmoothMin(float a, float b, float k)
        {
            if (k <= 0f) return Mathf.Min(a, b);
            float h = Mathf.Clamp01(0.5f + 0.5f * (b - a) / k);
            return Mathf.Lerp(b, a, h) - k * h * (1f - h);
        }

        public static float Eval(in Primitive p, Vector3 x)
        {
            switch (p.Kind)
            {
                case PrimitiveKind.Sphere: return Sphere(x, p.A, p.Radius);
                case PrimitiveKind.Capsule: return Capsule(x, p.A, p.B, p.Radius);
                default: return Box(x, p.A, p.Half);
            }
        }

        public static float Field(Primitive[] prims, float blend, Vector3 x)
        {
            float d = float.MaxValue;
            for (int i = 0; i < prims.Length; i++) d = SmoothMin(d, Eval(prims[i], x), blend);
            return d;
        }

        public static Bounds BoundsOf(Primitive[] prims, float padding)
        {
            var min = Vector3.positiveInfinity; var max = Vector3.negativeInfinity;
            foreach (var p in prims)
            {
                Vector3 lo, hi;
                switch (p.Kind)
                {
                    case PrimitiveKind.Sphere: lo = p.A - Vector3.one * p.Radius; hi = p.A + Vector3.one * p.Radius; break;
                    case PrimitiveKind.Capsule: lo = Vector3.Min(p.A, p.B) - Vector3.one * p.Radius; hi = Vector3.Max(p.A, p.B) + Vector3.one * p.Radius; break;
                    default: lo = p.A - p.Half; hi = p.A + p.Half; break;
                }
                min = Vector3.Min(min, lo); max = Vector3.Max(max, hi);
            }
            min -= Vector3.one * padding; max += Vector3.one * padding;
            var b = new Bounds(); b.SetMinMax(min, max); return b;
        }
    }
}
```

- [ ] **Step 7: `SurfaceNets.cs`**

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace Broodline.Creatures
{
    public sealed class GeneratedMesh
    {
        public Vector3[] Vertices;
        public Vector3[] Normals;
        public int[] Triangles;
        public BoneWeight[] Weights;
    }

    /// Naive surface nets over a regular grid: one vertex per sign-changing
    /// cell at the mean of its edge crossings, one quad per sign-changing
    /// grid edge joining the four cells around it. No lookup tables, no
    /// cracks, and a soft, even topology that suits the design's "chunky and
    /// smooth" read. Normals come from the field gradient, not the faces.
    ///
    /// AT MOST TWO INFLUENCES PER VERTEX - QualitySettings' Mobile tier caps
    /// skinning at two, and a vertex authored with four would look different
    /// on the phone than in the Editor.
    public static class SurfaceNets
    {
        public static GeneratedMesh Build(Primitive[] prims, float blend, Bounds bounds, int grid, string[] boneNames)
        {
            int n = grid + 1;
            var size = bounds.size; var origin = bounds.min;
            var step = new Vector3(size.x / grid, size.y / grid, size.z / grid);

            // 1. Sample the field at every corner.
            var field = new float[n * n * n];
            int Idx(int x, int y, int z) => (x * n + y) * n + z;
            Vector3 At(int x, int y, int z) => origin + new Vector3(x * step.x, y * step.y, z * step.z);
            for (int x = 0; x < n; x++) for (int y = 0; y < n; y++) for (int z = 0; z < n; z++)
                field[Idx(x, y, z)] = Sdf.Field(prims, blend, At(x, y, z));

            // 2. One vertex per cell that the surface crosses.
            var cellVertex = new int[grid * grid * grid];
            for (int i = 0; i < cellVertex.Length; i++) cellVertex[i] = -1;
            int Cell(int x, int y, int z) => (x * grid + y) * grid + z;
            var verts = new List<Vector3>();
            int[,] edges =
            {
                {0,0,0, 1,0,0}, {0,1,0, 1,1,0}, {0,0,1, 1,0,1}, {0,1,1, 1,1,1},
                {0,0,0, 0,1,0}, {1,0,0, 1,1,0}, {0,0,1, 0,1,1}, {1,0,1, 1,1,1},
                {0,0,0, 0,0,1}, {1,0,0, 1,0,1}, {0,1,0, 0,1,1}, {1,1,0, 1,1,1},
            };
            for (int x = 0; x < grid; x++) for (int y = 0; y < grid; y++) for (int z = 0; z < grid; z++)
            {
                var sum = Vector3.zero; int count = 0;
                for (int e = 0; e < 12; e++)
                {
                    int ax = x + edges[e, 0], ay = y + edges[e, 1], az = z + edges[e, 2];
                    int bx = x + edges[e, 3], by = y + edges[e, 4], bz = z + edges[e, 5];
                    float fa = field[Idx(ax, ay, az)], fb = field[Idx(bx, by, bz)];
                    if ((fa < 0f) == (fb < 0f)) continue;
                    float t = fa / (fa - fb);
                    sum += Vector3.Lerp(At(ax, ay, az), At(bx, by, bz), t); count++;
                }
                if (count == 0) continue;
                cellVertex[Cell(x, y, z)] = verts.Count;
                verts.Add(sum / count);
            }

            // 3. One quad per sign-changing edge, wound so the face points outward.
            var tris = new List<int>();
            void Quad(int a, int b, int c, int d, bool flip)
            {
                if (a < 0 || b < 0 || c < 0 || d < 0) return;
                if (flip) { tris.Add(a); tris.Add(c); tris.Add(b); tris.Add(a); tris.Add(d); tris.Add(c); }
                else      { tris.Add(a); tris.Add(b); tris.Add(c); tris.Add(a); tris.Add(c); tris.Add(d); }
            }
            for (int x = 0; x < grid; x++) for (int y = 0; y < grid; y++) for (int z = 0; z < grid; z++)
            {
                float f0 = field[Idx(x, y, z)];
                bool inside = f0 < 0f;
                // edge along +X from corner (x,y,z): cells (x, y-1..y, z-1..z)
                if (x < grid && y > 0 && z > 0 && (field[Idx(x + 1, y, z)] < 0f) != inside)
                    Quad(cellVertex[Cell(x, y - 1, z - 1)], cellVertex[Cell(x, y, z - 1)],
                         cellVertex[Cell(x, y, z)], cellVertex[Cell(x, y - 1, z)], !inside);
                // edge along +Y: cells (x-1..x, y, z-1..z)
                if (y < grid && x > 0 && z > 0 && (field[Idx(x, y + 1, z)] < 0f) != inside)
                    Quad(cellVertex[Cell(x - 1, y, z - 1)], cellVertex[Cell(x - 1, y, z)],
                         cellVertex[Cell(x, y, z)], cellVertex[Cell(x, y, z - 1)], !inside);
                // edge along +Z: cells (x-1..x, y-1..y, z)
                if (z < grid && x > 0 && y > 0 && (field[Idx(x, y, z + 1)] < 0f) != inside)
                    Quad(cellVertex[Cell(x - 1, y - 1, z)], cellVertex[Cell(x, y - 1, z)],
                         cellVertex[Cell(x, y, z)], cellVertex[Cell(x - 1, y, z)], !inside);
            }

            // 4. Normals from the gradient; weights from the two nearest primitives' bones.
            var normals = new Vector3[verts.Count];
            var weights = new BoneWeight[verts.Count];
            float eps = Mathf.Min(step.x, Mathf.Min(step.y, step.z)) * 0.5f;
            for (int i = 0; i < verts.Count; i++)
            {
                var p = verts[i];
                var g = new Vector3(
                    Sdf.Field(prims, blend, p + Vector3.right * eps) - Sdf.Field(prims, blend, p - Vector3.right * eps),
                    Sdf.Field(prims, blend, p + Vector3.up * eps) - Sdf.Field(prims, blend, p - Vector3.up * eps),
                    Sdf.Field(prims, blend, p + Vector3.forward * eps) - Sdf.Field(prims, blend, p - Vector3.forward * eps));
                normals[i] = g.sqrMagnitude > 1e-12f ? g.normalized : Vector3.up;
                weights[i] = WeightsAt(prims, p, boneNames);
            }

            // Winding check: if the mesh faces inward, the gradient says so.
            FixWinding(verts, normals, tris);

            return new GeneratedMesh { Vertices = verts.ToArray(), Normals = normals, Triangles = tris.ToArray(), Weights = weights };
        }

        static BoneWeight WeightsAt(Primitive[] prims, Vector3 p, string[] boneNames)
        {
            int best = -1, second = -1; float bd = float.MaxValue, sd = float.MaxValue;
            for (int i = 0; i < prims.Length; i++)
            {
                float d = Mathf.Max(0f, Sdf.Eval(prims[i], p)) + 1e-3f;
                int bone = System.Array.IndexOf(boneNames, prims[i].Bone);
                if (bone < 0) bone = 0;
                if (d < bd) { if (bone != best) { second = best; sd = bd; } best = bone; bd = d; }
                else if (d < sd && bone != best) { second = bone; sd = d; }
            }
            if (second < 0) return new BoneWeight { boneIndex0 = best, weight0 = 1f };
            float w0 = 1f / bd, w1 = 1f / sd; float sum = w0 + w1;
            return new BoneWeight { boneIndex0 = best, weight0 = w0 / sum, boneIndex1 = second, weight1 = w1 / sum };
        }

        static void FixWinding(List<Vector3> v, Vector3[] n, List<int> t)
        {
            int agree = 0, disagree = 0;
            for (int i = 0; i < t.Count; i += 3)
            {
                var face = Vector3.Cross(v[t[i + 1]] - v[t[i]], v[t[i + 2]] - v[t[i]]);
                if (Vector3.Dot(face, n[t[i]] + n[t[i + 1]] + n[t[i + 2]]) >= 0f) agree++; else disagree++;
            }
            if (disagree <= agree) return;
            for (int i = 0; i < t.Count; i += 3) { var tmp = t[i + 1]; t[i + 1] = t[i + 2]; t[i + 2] = tmp; }
        }
    }
}
```

- [ ] **Step 8: Vetch and three parts**

`client/Assets/Creatures/Recipes/SpeciesRecipes.cs` — bible §1.2: "Low dome, four stubby legs, no neck". Facing +X.

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Broodline.Creatures
{
    public static class SpeciesRecipes
    {
        static readonly Vector3 V = Vector3.zero;

        public static readonly BodyRecipe Vetch = new BodyRecipe
        {
            Id = "vetch",
            Blend = 0.22f, Grid = 24, Padding = 0.2f,
            Bones = new[]
            {
                new BoneDef { Name = "root", Position = new Vector3(0f, 0.32f, 0f) },
                new BoneDef { Name = "head", Parent = "root", Position = new Vector3(0.55f, 0.34f, 0f) },
                new BoneDef { Name = "leg_fl", Parent = "root", Position = new Vector3(0.32f, 0.22f, 0.3f) },
                new BoneDef { Name = "leg_fr", Parent = "root", Position = new Vector3(0.32f, 0.22f, -0.3f) },
                new BoneDef { Name = "leg_bl", Parent = "root", Position = new Vector3(-0.32f, 0.22f, 0.3f) },
                new BoneDef { Name = "leg_br", Parent = "root", Position = new Vector3(-0.32f, 0.22f, -0.3f) },
            },
            Primitives = new[]
            {
                Primitive.Sphere(new Vector3(0f, 0.42f, 0f), 0.52f, "root", 0.15f),                          // the dome
                Primitive.Box(new Vector3(0f, 0.28f, 0f), new Vector3(0.58f, 0.16f, 0.46f), "root", 0.15f),  // the low belly that flattens it
                Primitive.Sphere(new Vector3(0.58f, 0.32f, 0f), 0.22f, "head", 0.35f),                        // no neck: the head is a lump on the front
                Primitive.Capsule(new Vector3(0.32f, 0.26f, 0.3f), new Vector3(0.36f, 0.0f, 0.34f), 0.11f, "leg_fl", 0.25f),
                Primitive.Capsule(new Vector3(0.32f, 0.26f, -0.3f), new Vector3(0.36f, 0.0f, -0.34f), 0.11f, "leg_fr", 0.25f),
                Primitive.Capsule(new Vector3(-0.32f, 0.26f, 0.3f), new Vector3(-0.36f, 0.0f, 0.34f), 0.11f, "leg_bl", 0.25f),
                Primitive.Capsule(new Vector3(-0.32f, 0.26f, -0.3f), new Vector3(-0.36f, 0.0f, -0.34f), 0.11f, "leg_br", 0.25f),
            },
            Sockets = new[]
            {
                new SocketDef { Name = Sockets.Dorsal, Position = new Vector3(-0.05f, 0.92f, 0f), Euler = Vector3.zero, Scale = 1f },
                new SocketDef { Name = Sockets.Flank, Position = new Vector3(0f, 0.45f, -0.5f), Euler = new Vector3(90f, 0f, 0f), Scale = 0.8f },
                new SocketDef { Name = Sockets.Crown, Position = new Vector3(0.66f, 0.55f, 0f), Euler = new Vector3(0f, 0f, -35f), Scale = 0.55f },
            },
        };

        // Ember, Skitter, Hollow, Loam, Pale: Task 15.
        public static IReadOnlyList<BodyRecipe> All { get; } = new List<BodyRecipe> { Vetch };

        public static BodyRecipe For(string species)
        {
            if (string.IsNullOrWhiteSpace(species)) return null;
            foreach (var r in All)
                if (string.Equals(r.Id, species.Trim(), StringComparison.OrdinalIgnoreCase)) return r;
            return null;
        }
    }
}
```

`client/Assets/Creatures/Recipes/PartRecipes.cs` — Carapace (Vetch's plate), Taunt (Vetch's banner), Cinder (Ember's crest row, the mascot's three spikes). Authored at the origin, +Y outward.

```csharp
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Broodline.Creatures
{
    public static class PartRecipes
    {
        static Color C(string hex) => SpeciesColours.Parse(hex);

        public static readonly PartRecipe Carapace = new PartRecipe
        {
            Id = "carapace", Base = C("#5d93ab"), Under = C("#355d70"), Blend = 0.08f, Grid = 16,
            Primitives = new[]
            {
                Primitive.Box(new Vector3(0f, 0.06f, 0f), new Vector3(0.34f, 0.05f, 0.3f), "root"),
                Primitive.Box(new Vector3(0f, 0.15f, 0f), new Vector3(0.22f, 0.05f, 0.2f), "root"),
                Primitive.Sphere(new Vector3(0f, 0.2f, 0f), 0.12f, "root"),
            },
        };

        public static readonly PartRecipe Taunt = new PartRecipe
        {
            Id = "taunt", Base = C("#e5867a"), Under = C("#a8574d"), Blend = 0.05f, Grid = 16,
            Primitives = new[]
            {
                Primitive.Capsule(new Vector3(0f, 0f, 0f), new Vector3(-0.1f, 0.5f, 0f), 0.04f, "root"),   // the pole
                Primitive.Box(new Vector3(-0.22f, 0.42f, 0f), new Vector3(0.14f, 0.1f, 0.02f), "root"),   // the banner
            },
        };

        public static readonly PartRecipe Cinder = new PartRecipe
        {
            Id = "cinder", Base = C("#e5867a"), Under = C("#a8574d"), Blend = 0.05f, Grid = 16,
            Primitives = new[]
            {
                Primitive.Capsule(new Vector3(-0.22f, 0f, 0f), new Vector3(-0.26f, 0.32f, 0f), 0.07f, "root"),
                Primitive.Capsule(new Vector3(0f, 0f, 0f), new Vector3(0f, 0.44f, 0f), 0.08f, "root"),
                Primitive.Capsule(new Vector3(0.22f, 0f, 0f), new Vector3(0.26f, 0.32f, 0f), 0.07f, "root"),
            },
        };

        // The other nine: Task 15.
        public static IReadOnlyList<PartRecipe> All { get; } = new List<PartRecipe> { Carapace, Taunt, Cinder };

        public static PartRecipe For(string trait)
        {
            if (string.IsNullOrWhiteSpace(trait)) return null;
            foreach (var p in All)
                if (string.Equals(p.Id, trait.Trim(), StringComparison.OrdinalIgnoreCase)) return p;
            return null;
        }
    }
}
```

`client/Assets/Creatures/Recipes/RaiderRecipes.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace Broodline.Creatures
{
    public static class RaiderRecipes
    {
        // Courser, Lash, Skirmisher: Task 15.
        public static IReadOnlyList<BodyRecipe> All { get; } = new List<BodyRecipe>();

        public static BodyRecipe For(string raiderType)
        {
            if (string.IsNullOrWhiteSpace(raiderType)) return null;
            foreach (var r in All)
                if (string.Equals(r.Id, raiderType.Trim(), StringComparison.OrdinalIgnoreCase)) return r;
            return null;
        }
    }
}
```

- [ ] **Step 9: The shader**

`client/Assets/Creatures/Shaders/Creature.shader`:

```hlsl
Shader "Broodline/Creature"
{
    // Phase 9 design §3.6: half-Lambert through a warm key and a cool fill,
    // a rim so a body separates from a near-white card, a two-tone body
    // whose underside darkens (bible 10.4's value structure, and what Pale
    // needs to exist on paper), a desaturation float for damage-as-posture
    // (bible 10.7), and a tint. Hand-written so it is text under review.
    Properties
    {
        _BaseColor ("Base", Color) = (1, 1, 1, 1)
        _UnderColor ("Underside", Color) = (0.5, 0.5, 0.5, 1)
        _KeyColor ("Key light", Color) = (1.0, 0.97, 0.9, 1)
        _FillColor ("Fill light", Color) = (0.66, 0.7, 0.84, 1)
        _RimColor ("Rim", Color) = (1, 1, 1, 1)
        _RimPower ("Rim power", Range(1, 8)) = 3
        _RimStrength ("Rim strength", Range(0, 1)) = 0.35
        _Desaturate ("Desaturate", Range(0, 1)) = 0
        _Tint ("Tint", Color) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor, _UnderColor, _KeyColor, _FillColor, _RimColor, _Tint;
                float _RimPower, _RimStrength, _Desaturate;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 positionWS : TEXCOORD1; };

            Varyings vert(Attributes a)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(a.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(a.normalOS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                Light L = GetMainLight();
                float ndl = saturate(dot(n, L.direction) * 0.5 + 0.5);
                float3 ramp = lerp(_FillColor.rgb, _KeyColor.rgb, smoothstep(0.25, 0.85, ndl));
                float3 body = lerp(_BaseColor.rgb, _UnderColor.rgb, saturate(-n.y));
                float3 col = body * ramp * L.color;
                float3 v = normalize(GetWorldSpaceViewDir(i.positionWS));
                float rim = pow(1.0 - saturate(dot(n, v)), _RimPower) * _RimStrength;
                col += _RimColor.rgb * rim;
                float grey = dot(col, float3(0.299, 0.587, 0.114));
                col = lerp(col, grey.xxx, _Desaturate);
                col *= _Tint.rgb;
                return half4(col, 1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
    Fallback Off
}
```

If the Editor logs an error on the `UsePass` line, delete it; depth-only is a nicety here and shadows are not in scope.

- [ ] **Step 10: The generator**

`client/Assets/Creatures/Editor/CreatureGenerator.cs`:

```csharp
using System.IO;
using System.Linq;
using Broodline.Creatures;
using UnityEditor;
using UnityEngine;

namespace Broodline.Creatures.Editor
{
    /// Recipes to assets. Phase 9 design §3.4. Menu for a person, `Generate`
    /// for batchmode (generate-creatures.sh). Idempotent: existing assets are
    /// overwritten in place so their GUIDs, and every reference to them, hold.
    public static class CreatureGenerator
    {
        const string Root = "Assets/Creatures/Resources/";
        const string ShaderName = "Broodline/Creature";

        [MenuItem("Broodline/Generate Creatures")]
        public static void Generate()
        {
            foreach (var dir in new[] { CreaturePaths.MeshDir, CreaturePaths.MaterialDir, CreaturePaths.BodyDir, CreaturePaths.PartDir, CreaturePaths.RaiderDir })
                Directory.CreateDirectory(Root + dir);

            int n = 0;
            foreach (var r in SpeciesRecipes.All) { Body(r, CreaturePaths.BodyDir, SpeciesColours.For(r.Id)); n++; }
            foreach (var r in RaiderRecipes.All) { Body(r, CreaturePaths.RaiderDir, (SpeciesColours.RaiderBase, SpeciesColours.RaiderUnder)); n++; }
            foreach (var p in PartRecipes.All) { Part(p); n++; }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[creatures] generated " + n + " assets under " + Root);
        }

        public static GeneratedMesh Regenerate(BodyRecipe r) =>
            SurfaceNets.Build(r.Primitives, r.Blend, Sdf.BoundsOf(r.Primitives, r.Padding), r.Grid, r.Bones.Select(b => b.Name).ToArray());

        public static GeneratedMesh Regenerate(PartRecipe p) =>
            SurfaceNets.Build(p.Primitives, p.Blend, Sdf.BoundsOf(p.Primitives, p.Padding), p.Grid, new[] { "root" });

        static void Body(BodyRecipe r, string dir, (Color Base, Color Under) colours)
        {
            var g = Regenerate(r);
            var boneNames = r.Bones.Select(b => b.Name).ToArray();

            var mesh = WriteMesh(Root + CreaturePaths.MeshDir + "/" + r.Id + ".asset", g);
            var material = WriteMaterial(Root + CreaturePaths.MaterialDir + "/" + r.Id + ".mat", colours.Base, colours.Under);

            var go = new GameObject(r.Id);
            try
            {
                // Bones, parented per the recipe, at their rest positions.
                var bones = new Transform[r.Bones.Length];
                for (int i = 0; i < r.Bones.Length; i++)
                {
                    var t = new GameObject(r.Bones[i].Name).transform;
                    t.SetParent(go.transform, false);
                    t.position = r.Bones[i].Position;
                    bones[i] = t;
                }
                for (int i = 0; i < r.Bones.Length; i++)
                    if (r.Bones[i].Parent != null)
                        bones[i].SetParent(bones[System.Array.IndexOf(boneNames, r.Bones[i].Parent)], true);

                var bind = new Matrix4x4[bones.Length];
                for (int i = 0; i < bones.Length; i++) bind[i] = bones[i].worldToLocalMatrix * go.transform.localToWorldMatrix;
                mesh.boneWeights = g.Weights;
                mesh.bindposes = bind;

                var body = new GameObject("body");
                body.transform.SetParent(go.transform, false);
                var smr = body.AddComponent<SkinnedMeshRenderer>();
                smr.sharedMesh = mesh;
                smr.bones = bones;
                smr.rootBone = bones[0];
                smr.sharedMaterial = material;
                smr.updateWhenOffscreen = false;
                smr.localBounds = mesh.bounds;

                foreach (var s in r.Sockets)
                {
                    var t = new GameObject(s.Name).transform;
                    t.SetParent(go.transform, false);
                    t.localPosition = s.Position;
                    t.localRotation = Quaternion.Euler(s.Euler);
                    t.localScale = Vector3.one * s.Scale;
                }

                go.AddComponent<CreatureMotion>();   // Task 8 adds the component; until then, remove this line
                PrefabUtility.SaveAsPrefabAsset(go, Root + dir + "/" + r.Id + ".prefab");
            }
            finally { Object.DestroyImmediate(go); }
            EditorUtility.SetDirty(mesh);
        }

        static void Part(PartRecipe p)
        {
            var g = Regenerate(p);
            var mesh = WriteMesh(Root + CreaturePaths.MeshDir + "/part-" + p.Id + ".asset", g);
            var material = WriteMaterial(Root + CreaturePaths.MaterialDir + "/part-" + p.Id + ".mat", p.Base, p.Under);
            var go = new GameObject(p.Id);
            try
            {
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = material;
                PrefabUtility.SaveAsPrefabAsset(go, Root + CreaturePaths.PartDir + "/" + p.Id + ".prefab");
            }
            finally { Object.DestroyImmediate(go); }
        }

        static Mesh WriteMesh(string path, GeneratedMesh g)
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null) { mesh = new Mesh(); AssetDatabase.CreateAsset(mesh, path); }
            mesh.Clear();
            mesh.name = Path.GetFileNameWithoutExtension(path);
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt16;
            mesh.vertices = g.Vertices;
            mesh.normals = g.Normals;
            mesh.triangles = g.Triangles;
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        static Material WriteMaterial(string path, Color baseColour, Color under)
        {
            var shader = Shader.Find(ShaderName);
            if (shader == null) throw new System.InvalidOperationException("no shader " + ShaderName);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, path); }
            mat.shader = shader;
            mat.SetColor("_BaseColor", baseColour);
            mat.SetColor("_UnderColor", under);
            EditorUtility.SetDirty(mat);
            return mat;
        }
    }
}
```

For this task, delete the `go.AddComponent<CreatureMotion>()` line; Task 8 restores it.

- [ ] **Step 11: The script**

`implementation/scripts/generate-creatures.sh`:

```bash
#!/usr/bin/env bash
# Regenerates every creature mesh, material and prefab from its recipe, and
# (with BAKE=1) re-bakes the card sprites and the contact sheet. Batchmode;
# the Editor must be closed. Usage: generate-creatures.sh   |   BAKE=1 generate-creatures.sh
set -uo pipefail
cd "$(dirname "$0")/../.."
UNITY="/Applications/Unity/Hub/Editor/6000.6.0f1/Unity.app/Contents/MacOS/Unity"
LOG="$(pwd)/implementation/results/generate-creatures.log"
mkdir -p implementation/results
rm -f "$LOG"
[ -x "$UNITY" ] || { echo "FAIL: Unity not found at $UNITY"; exit 127; }

"$UNITY" -batchmode -quit -projectPath "$(pwd)/client" \
  -executeMethod Broodline.Creatures.Editor.CreatureGenerator.Generate -logFile "$LOG"
code=$?
grep '\[creatures\]' "$LOG" || { echo "FAIL: no [creatures] line in $LOG - the Editor is probably open"; exit 1; }
[ "$code" -eq 0 ] || { echo "FAIL: generate exited $code"; tail -30 "$LOG"; exit "$code"; }

if [ "${BAKE:-0}" = "1" ]; then
  "$UNITY" -batchmode -quit -projectPath "$(pwd)/client" \
    -executeMethod Broodline.Creatures.Editor.CreatureBaker.Bake -logFile "$LOG.bake"
  code=$?
  grep '\[bake\]' "$LOG.bake" || { echo "FAIL: no [bake] line"; exit 1; }
  [ "$code" -eq 0 ] || { echo "FAIL: bake exited $code"; tail -30 "$LOG.bake"; exit "$code"; }
fi
echo "ok"
```

```bash
chmod +x implementation/scripts/generate-creatures.sh
```

- [ ] **Step 12: Generate, then test**

```bash
./implementation/scripts/generate-creatures.sh
ls client/Assets/Creatures/Resources/Creatures/*/
./implementation/scripts/run-unity-tests.sh EditMode 2>&1 | tail -6
```

Expected: `vetch.asset`, `part-carapace.asset`, `part-taunt.asset`, `part-cinder.asset`, four `.mat`, `vetch.prefab`, three part prefabs. Tests: `total=339 passed=338 failed=1` (12 new: four recipe, five mesher, three drift). If `EveryBody_IsInsideTheTriangleBudget` fails high, lower `Vetch.Grid` to 22 and regenerate; if it fails low, check `Padding`.

- [ ] **Step 13: Look at it once**

Open the Editor, drag `vetch.prefab` into `SampleScene`, add a directional light if none. It should read as a low dome on four stubs with a lump of a head, teal on top, darker underneath, a soft rim. If it is inside-out (dark, see-through), `FixWinding` lost its vote — invert the `flip` argument in `Quad` and regenerate. Close the Editor.

- [ ] **Step 14: Commit**

```bash
git add client/Assets/Creatures implementation/scripts/generate-creatures.sh \
        client/Assets/Editor/TestHarness/EditModeRunner.cs client/Assets/Editor/TestHarness/Broodline.TestHarness.asmdef
git commit -m "feat(creatures): recipes, a surface-nets mesher, one shader, and Vetch

A new runtime assembly that references nothing. A body is primitives,
bones and sockets; the Editor generator meshes it and commits the result.
Two influences per vertex because the Mobile tier caps skinning at two.
The drift test regenerates every recipe and fails on a mesh that no
longer matches it. Budget is 2500 per body - an erratum against the
design's 1500, a quarter of the measured bound.

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

## Task 8: The assembler, motion, growth, and the `Studio` layer

Design §3.5. `CreatureAssembler.Build(look)` instantiates a body prefab, mounts combat one at `sk_dorsal` and combat two at `sk_flank` (slot index, whatever the traits — rig proof §3.1), applies growth as per-bone scale, and returns one `GameObject` carrying a `CreatureMotion`. `CreatureMotion` breathes at idle, bobs when moving, flinches on demand, and droops and desaturates with `Hurt01`. This task also adds layer 6 `Studio` for Tasks 9 and 11, and the colour-mirror test in `Broodline.Game.Tests`.

**Files:**
- Create: `client/Assets/Creatures/CreatureLibrary.cs`, `CreatureAssembler.cs`, `CreatureMotion.cs`
- Create: `client/Assets/Creatures/Tests/AssemblerTests.cs`, `client/Assets/Game/Tests/CreatureColourTests.cs`
- Modify: `client/Assets/Creatures/Editor/CreatureGenerator.cs` (restore the `CreatureMotion` line; regenerate)
- Modify: `client/ProjectSettings/TagManager.asset` (layer 6)

**Interfaces (produced):**

```csharp
namespace Broodline.Creatures
{
    public static class CreatureLibrary { GameObject BodyPrefab(string species); GameObject PartPrefab(string trait); GameObject RaiderPrefab(string type); }
    public static class CreatureAssembler
    {
        const int StudioLayer = 6;
        GameObject Build(CreatureLook look);                 // never null; unknown species -> a magenta sphere named "missing-<species>", logged once
        void ApplyGrowth(GameObject creature, BodyRecipe recipe, float growth01);
        void SetLayerRecursively(GameObject go, int layer);
    }
    public sealed class CreatureMotion : MonoBehaviour
    {
        bool Moving;            // bob when true
        float Hurt01;           // 0 healthy .. 1 nearly dead: droop + desaturate
        void Flinch();          // a 250 ms squash
        void Tick(float time, float dt);   // driven by Update; callable by tests
    }
}
```

- [ ] **Step 1: The failing tests**

`client/Assets/Creatures/Tests/AssemblerTests.cs`:

```csharp
using Broodline.Creatures;
using NUnit.Framework;
using UnityEngine;

namespace Broodline.Creatures.Tests
{
    public class AssemblerTests
    {
        static CreatureLook Vetch(string t1 = "carapace", string t2 = "taunt") =>
            new CreatureLook { Species = "vetch", Trait1 = t1, Trait2 = t2, Growth01 = 0f };

        [Test]
        public void Build_MountsCombatOneAtDorsal_AndCombatTwoAtFlank_BySlotNotByTrait()
        {
            // rig_proof.md 3.1: slot index decides the socket. Swap the traits
            // and the parts swap sockets; nothing about the trait chooses.
            var a = CreatureAssembler.Build(Vetch("carapace", "cinder"));
            var b = CreatureAssembler.Build(Vetch("cinder", "carapace"));
            try
            {
                Assert.AreEqual("carapace", a.transform.Find(Sockets.Dorsal).GetChild(0).name);
                Assert.AreEqual("cinder", a.transform.Find(Sockets.Flank).GetChild(0).name);
                Assert.AreEqual("cinder", b.transform.Find(Sockets.Dorsal).GetChild(0).name);
                Assert.AreEqual("carapace", b.transform.Find(Sockets.Flank).GetChild(0).name);
            }
            finally { Object.DestroyImmediate(a); Object.DestroyImmediate(b); }
        }

        [Test]
        public void Build_CinderplateIsVetchWithCinderAtDorsal_AndNothingElseMakesIt()
        {
            // bible 10.9: the mascot, produced by the pipeline.
            var c = CreatureAssembler.Build(Vetch("cinder", "carapace"));
            try
            {
                Assert.IsNotNull(c.GetComponentInChildren<SkinnedMeshRenderer>());
                Assert.AreEqual("cinder", c.transform.Find(Sockets.Dorsal).GetChild(0).name);
                Assert.IsNotNull(c.GetComponent<CreatureMotion>());
            }
            finally { Object.DestroyImmediate(c); }
        }

        [Test]
        public void Build_UnknownTrait_LeavesTheSocketEmpty_AndUnknownSpecies_IsLoud()
        {
            var c = CreatureAssembler.Build(Vetch("no-such-trait", null));
            try { Assert.AreEqual(0, c.transform.Find(Sockets.Dorsal).childCount); }
            finally { Object.DestroyImmediate(c); }

            var m = CreatureAssembler.Build(new CreatureLook { Species = "ash" });
            try { StringAssert.StartsWith("missing-", m.name); }
            finally { Object.DestroyImmediate(m); }
        }

        [Test]
        public void Growth_MovesMassOutward_OnOneRig()
        {
            // bible 10.2 rule 3: runt to apex is the same creature with mass
            // moved outward - bigger head, heavier limbs, same profile. The
            // head bone grows more than the root because its primitives say so.
            var runt = CreatureAssembler.Build(Vetch());
            var apex = CreatureAssembler.Build(new CreatureLook { Species = "vetch", Trait1 = "carapace", Trait2 = "taunt", Growth01 = 1f });
            try
            {
                float headRunt = runt.transform.Find("root/head").localScale.x;
                float headApex = apex.transform.Find("root/head").localScale.x;
                float rootApex = apex.GetComponent<CreatureMotion>().GrowthScale;
                Assert.AreEqual(1f, headRunt, 1e-5f);
                Assert.Greater(headApex, rootApex, "the head grows more than the body");
                Assert.Greater(rootApex, 1f);
            }
            finally { Object.DestroyImmediate(runt); Object.DestroyImmediate(apex); }
        }

        [Test]
        public void Motion_Breathes_BobsWhenMoving_AndDroopsWhenHurt()
        {
            var c = CreatureAssembler.Build(Vetch());
            try
            {
                var motion = c.GetComponent<CreatureMotion>();
                var root = c.transform.Find("root");
                motion.Tick(0f, 0f);
                var restY = root.localPosition.y;
                motion.Tick(0.4f, 0.4f);
                Assert.AreNotEqual(root.localScale.y, 1f, "breathing scales the root");

                motion.Moving = true; motion.Tick(0.8f, 0.4f);
                Assert.AreNotEqual(restY, root.localPosition.y, "moving bobs the root");

                motion.Hurt01 = 1f; motion.Tick(1.2f, 0.4f);
                Assert.Greater(Mathf.Abs(root.localRotation.eulerAngles.x % 360f), 0.5f, "hurt droops the root");
                Assert.AreEqual(1f, c.GetComponentInChildren<SkinnedMeshRenderer>().material.GetFloat("_Desaturate"), 0.05f);
            }
            finally { Object.DestroyImmediate(c); }
        }
    }
}
```

`client/Assets/Game/Tests/CreatureColourTests.cs`:

```csharp
using System.Linq;
using Broodline.Creatures;
using Broodline.UI.Diagnostics;
using NUnit.Framework;

namespace Broodline.Game.Tests
{
    /// Broodline.Creatures references nothing, so its species colours are a
    /// mirror of PaletteContrast.Species. This is the one place that sees
    /// both, and it fails when they part.
    public class CreatureColourTests
    {
        [Test]
        public void SpeciesColours_MirrorThePalette()
        {
            foreach (var (name, hex) in PaletteContrast.Species)
                Assert.AreEqual(hex.ToLowerInvariant(), SpeciesColours.BaseHex(name).ToLowerInvariant(), name);
        }
    }
}
```

Add `"Broodline.Creatures"` to `Broodline.Game.Tests.asmdef`'s references, and to `Broodline.Game.asmdef`'s and `Broodline.View.asmdef`'s references now (Tasks 10 and 11 need them).

- [ ] **Step 2: Run to see them fail**

```bash
./implementation/scripts/run-unity-tests.sh EditMode 2>&1 | tail -6
```

Expected: compile errors naming `CreatureAssembler`, `CreatureMotion`.

- [ ] **Step 3: `CreatureLibrary.cs`**

```csharp
using UnityEngine;

namespace Broodline.Creatures
{
    /// The committed prefabs, by lowercase id. Null when there is none;
    /// callers decide how loud to be.
    public static class CreatureLibrary
    {
        public static GameObject BodyPrefab(string species) => Load(CreaturePaths.Body(Lower(species)));
        public static GameObject PartPrefab(string trait) => Load(CreaturePaths.Part(Lower(trait)));
        public static GameObject RaiderPrefab(string type) => Load(CreaturePaths.Raider(Lower(type)));

        static string Lower(string s) => string.IsNullOrWhiteSpace(s) ? "" : s.Trim().ToLowerInvariant();
        static GameObject Load(string path) => Resources.Load<GameObject>(path);
    }
}
```

- [ ] **Step 4: `CreatureAssembler.cs`**

```csharp
using UnityEngine;

namespace Broodline.Creatures
{
    /// Body + two parts + growth, one GameObject. Phase 9 design §3.5.
    public static class CreatureAssembler
    {
        public const int StudioLayer = 6;

        public static GameObject Build(CreatureLook look)
        {
            if (look == null) return Missing("null");

            var isRaider = !string.IsNullOrEmpty(look.RaiderType);
            var recipe = isRaider ? RaiderRecipes.For(look.RaiderType) : SpeciesRecipes.For(look.Species);
            var prefab = isRaider ? CreatureLibrary.RaiderPrefab(look.RaiderType) : CreatureLibrary.BodyPrefab(look.Species);
            if (recipe == null || prefab == null) return Missing(isRaider ? look.RaiderType : look.Species);

            var go = Object.Instantiate(prefab);
            go.name = recipe.Id;
            if (go.GetComponent<CreatureMotion>() == null) go.AddComponent<CreatureMotion>();

            if (!isRaider)
            {
                Mount(go, Sockets.Dorsal, look.Trait1);
                Mount(go, Sockets.Flank, look.Trait2);
            }
            ApplyGrowth(go, recipe, look.Growth01);
            return go;
        }

        static void Mount(GameObject creature, string socketName, string trait)
        {
            var socket = creature.transform.Find(socketName);
            if (socket == null) return;
            var prefab = CreatureLibrary.PartPrefab(trait);
            if (prefab == null) return;   // a trait this build has no part for: the socket stays empty, the card still names it
            var part = Object.Instantiate(prefab, socket, false);
            part.name = PartRecipes.For(trait).Id;
        }

        /// bible 10.2 rule 3, as per-bone scale over one rig. A bone's growth
        /// is the mean growth of the primitives it moves; the root grows by
        /// the recipe's smallest so the profile holds while the extremities
        /// swell.
        public static void ApplyGrowth(GameObject creature, BodyRecipe recipe, float growth01)
        {
            growth01 = Mathf.Clamp01(growth01);
            foreach (var bone in recipe.Bones)
            {
                float sum = 0f; int n = 0;
                foreach (var p in recipe.Primitives) if (p.Bone == bone.Name) { sum += p.Growth; n++; }
                float g = n == 0 ? 0f : sum / n;
                if (bone.Parent == null)
                {
                    // The root's scale is CreatureMotion's to write every frame
                    // (breath and flinch); growth goes in as its multiplier.
                    var motion = creature.GetComponent<CreatureMotion>();
                    if (motion != null) motion.GrowthScale = 1f + g * growth01;
                    continue;
                }
                var t = FindDeep(creature.transform, bone.Name);
                if (t != null) t.localScale = Vector3.one * (1f + g * growth01);
            }
        }

        public static void SetLayerRecursively(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform) SetLayerRecursively(child.gameObject, layer);
        }

        static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform c in root) { var f = FindDeep(c, name); if (f != null) return f; }
            return null;
        }

        static GameObject Missing(string id)
        {
            Debug.LogError("[creatures] no body for '" + id + "' - run generate-creatures.sh, or the roster names a species this build does not carry");
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "missing-" + id;
            go.GetComponent<Renderer>().material.color = Color.magenta;
            return go;
        }
    }
}
```

Note: bone scale compounds down the hierarchy (a head under a scaled root scales twice). That is the intended "mass outward" read; the test asserts head > root.

- [ ] **Step 5: `CreatureMotion.cs`**

```csharp
using UnityEngine;

namespace Broodline.Creatures
{
    /// Body-level, procedural, no clips - bible 10.3's "animation is
    /// body-level, never trait-level", and the Animator cost rig_proof.md
    /// section 8 never measured is not incurred. Breathe at idle, bob when
    /// moving, a flinch on demand, and damage as posture (bible 10.7): a
    /// droop and a desaturation, never injury.
    public sealed class CreatureMotion : MonoBehaviour
    {
        public const float BreathHz = 0.6f;
        public const float BreathAmount = 0.035f;
        public const float BobHz = 2.4f;
        public const float BobAmount = 0.06f;
        public const float FlinchSeconds = 0.25f;
        public const float DroopDegrees = 18f;

        public bool Moving;
        public float Hurt01;
        /// Growth, applied by CreatureAssembler; multiplies the root's scale.
        public float GrowthScale = 1f;

        Transform _root;
        Vector3 _restPosition;
        Renderer[] _renderers;
        MaterialPropertyBlock _block;
        float _flinchUntil = -1f;
        float _phase;

        static readonly int DesaturateId = Shader.PropertyToID("_Desaturate");

        void Awake()
        {
            _root = transform.Find("root") ?? transform;
            _restPosition = _root.localPosition;
            _renderers = GetComponentsInChildren<Renderer>();
            _block = new MaterialPropertyBlock();
            _phase = Random.value * 6.28f;   // a wave is not a chorus line
        }

        public void Flinch() => _flinchUntil = Time.time + FlinchSeconds;

        void Update() => Tick(Time.time, Time.deltaTime);

        public void Tick(float time, float dt)
        {
            if (_root == null) Awake();
            float t = time + _phase;

            float breath = 1f + Mathf.Sin(t * BreathHz * 6.2832f) * BreathAmount * (1f - 0.5f * Hurt01);
            float squash = time < _flinchUntil ? 0.85f : 1f;
            _root.localScale = new Vector3(1f / Mathf.Sqrt(squash), breath * squash, 1f / Mathf.Sqrt(squash)) * GrowthScale;

            float bob = Moving ? Mathf.Abs(Mathf.Sin(t * BobHz * 3.1416f)) * BobAmount : 0f;
            _root.localPosition = _restPosition + Vector3.up * bob;

            _root.localRotation = Quaternion.Euler(Hurt01 * DroopDegrees, 0f, 0f);

            foreach (var r in _renderers)
            {
                r.GetPropertyBlock(_block);
                _block.SetFloat(DesaturateId, Hurt01);
                r.SetPropertyBlock(_block);
            }
        }
    }
}
```

The root bone's scale is written every frame by `Tick`, so `ApplyGrowth` never writes it directly: it sets `GrowthScale`, which `Tick` multiplies in. Child bones (head, legs, wings, segments) are scaled once by `ApplyGrowth` and left alone.

- [ ] **Step 6: The layer**

`client/ProjectSettings/TagManager.asset` — in `layers:`, replace the seventh entry (index 6, currently an empty string) with `Studio`:

```yaml
  layers:
  - Default
  - TransparentFX
  - Ignore Raycast
  - 
  - Water
  - UI
  - Studio
```

- [ ] **Step 7: Restore the generator line, regenerate, run the gate**

Put `go.AddComponent<CreatureMotion>();` back in `CreatureGenerator.Body`. Then:

```bash
./implementation/scripts/generate-creatures.sh
./implementation/scripts/run-unity-tests.sh EditMode 2>&1 | tail -6
```

Expected: `total=345 passed=344 failed=1` (five assembler, one colour mirror). The drift test still passes (the mesh did not change; only the prefab gained a component).

- [ ] **Step 8: Commit**

```bash
git add client/Assets/Creatures client/Assets/Game/Tests/CreatureColourTests.cs* \
        client/Assets/Game/Tests/Broodline.Game.Tests.asmdef client/Assets/Game/Broodline.Game.asmdef \
        client/Assets/View/Broodline.View.asmdef client/ProjectSettings/TagManager.asset
git commit -m "feat(creatures): the assembler, procedural motion, growth, and a Studio layer

Combat one to the dorsal socket, combat two to the flank, by slot and
never by trait. Growth is per-bone scale over one rig. Motion is body-
level and clip-free: breathe, bob, flinch, and damage as posture.

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

## Task 9: The bake — layered card sprites, and the card that stacks them

Design §3.7. An Editor bake renders each body and each part-in-socket-on-body from one fixed camera at 192×192 with a transparent background, into `client/Assets/UI/Resources/Art/creatures/`. The card's silhouette slot becomes three stacked images: body, dorsal part, flank part. `CreatureSprites` replaces `SpeciesProxy`; `SilhouetteTests` re-points at the baked bodies; `ArtImportSettings` keeps the readback rule for the new path. The forty-eight-combination contact sheet and the Cinderplate render are written to `implementation/results/` once every recipe exists (Task 15 re-runs this bake).

**Files:**
- Create: `client/Assets/Creatures/Editor/CreatureBaker.cs`
- Create: `client/Assets/UI/Components/CreatureSprites.cs`; delete `SpeciesProxy.cs` and the six proxy PNG/SVG pairs under `client/Assets/UI/Art/proxies/`
- Modify: `client/Assets/UI/Components/CreatureCard.cs`, `Resources/CreatureCard.uxml`, `Resources/CreatureCard.uss`
- Modify: `client/Assets/UI/Tests/SilhouetteTests.cs`, `ComponentTests.cs` (any test naming `SpeciesProxy`), `client/Assets/Editor/ArtImportSettings.cs`, `client/Assets/Editor/ScreenFixtures.cs` (if it names `SpeciesProxy`)
- Generated: `client/Assets/UI/Resources/Art/creatures/bodies/vetch.png`, `parts/vetch-sk_dorsal-{carapace,taunt,cinder}.png`, `parts/vetch-sk_flank-{...}.png`

**Interfaces (produced):**

```csharp
namespace Broodline.UI.Components
{
    public static class CreatureSprites
    {
        const string Root = "Art/creatures";
        Texture2D Body(string species);                                  // null when missing
        Texture2D Part(string species, string socket, string trait);     // null when missing
        string BodyPath(string species);  string PartPath(string species, string socket, string trait);   // Resources-relative, lowercase
    }
}
namespace Broodline.Creatures.Editor { public static class CreatureBaker { void Bake(); const int Size = 192; } }
```

`CreatureCard` gains three children in the slot: `#silhouette` (body), `#part-dorsal`, `#part-flank`, and `Bind` sets each `style.backgroundImage` from `CreatureSprites`, clearing it when null.

- [ ] **Step 1: The failing tests**

Re-point `SilhouetteTests.Mask`:

```csharp
            var path = $"Assets/UI/Resources/Art/creatures/bodies/{species}.png";
```

and its class comment's first paragraph to say: *"bible 10.2 rule 1, asserted against the BAKED bodies from Phase 9's pipeline; a collision here is a collision in the recipes."* Keep the 8% threshold and the blank-check. Add:

```csharp
        [Test]
        public void EveryBakedBody_HasATransparentBackground()
        {
            // A baked body on an opaque background would make every mask a
            // filled square and pass the pairwise test trivially. The bake
            // clears to alpha 0; this is the check that it did.
            foreach (var s in Species)
            {
                var mask = Mask(s);
                Assert.IsFalse(mask[0], s + "'s top-left corner is opaque - the bake did not clear to transparent");
                Assert.IsFalse(mask[mask.Length - 1], s + "'s bottom-right corner is opaque");
            }
        }
```

Until Task 15 only Vetch is baked, so for this task change `Species` to `{ "vetch" }` with a `// Task 15 restores the six` comment; Task 15 restores it.

In `ComponentTests.cs`:

```csharp
        [Test]
        public void CreatureCard_StacksBodyAndBothPartsInTheSilhouetteSlot()
        {
            var card = new CreatureCard();
            card.Bind(Creature("Vetch", gen: 2, name: "Ash", founder: false, trait1: "Carapace", tier1: 1, trait2: "Cinder", tier2: 1), Counters());
            var body = card.Q<VisualElement>("silhouette");
            var dorsal = card.Q<VisualElement>("part-dorsal");
            var flank = card.Q<VisualElement>("part-flank");
            Assert.IsNotNull(body.style.backgroundImage.value.texture, "the body sprite");
            Assert.IsNotNull(dorsal.style.backgroundImage.value.texture, "combat one at the dorsal socket");
            Assert.IsNotNull(flank.style.backgroundImage.value.texture, "combat two at the flank socket");
            Assert.AreEqual(CreatureSprites.Part("vetch", "sk_dorsal", "carapace"), dorsal.style.backgroundImage.value.texture);
        }

        [Test]
        public void CreatureCard_UnknownSpecies_LeavesTheSlotEmpty()
        {
            var card = new CreatureCard();
            card.Bind(Creature("Ash", gen: 1, name: "X", founder: false, trait1: "Taunt", tier1: 1, trait2: "Carapace", tier2: 1), Counters());
            Assert.IsNull(card.Q<VisualElement>("silhouette").style.backgroundImage.value.texture);
        }
```

Delete or rewrite every existing test that references `SpeciesProxy` (grep `SpeciesProxy` under `client/Assets/UI/Tests` and `client/Assets/Editor`): the ones asserting `proxy--vetch` classes become the two above.

- [ ] **Step 2: Run to see them fail**

```bash
./implementation/scripts/run-unity-tests.sh EditMode 2>&1 | tail -6
```

Expected: compile errors on `CreatureSprites`; `SilhouetteTests` cannot load the new path.

- [ ] **Step 3: The baker**

`client/Assets/Creatures/Editor/CreatureBaker.cs`:

```csharp
using System.IO;
using Broodline.Creatures;
using UnityEditor;
using UnityEngine;

namespace Broodline.Creatures.Editor
{
    /// Renders every body, and every part in each socket on each body, from
    /// ONE fixed camera into aligned 192x192 PNGs with a transparent
    /// background. The card stacks body / dorsal / flank, so both combat
    /// traits are visible on every card at zero runtime cost - bible 10.4's
    /// single most important functional requirement, met in 2D.
    ///
    /// 192 is the proxies' convention: authored at 3x, drawn at 64, read at
    /// 40 by SilhouetteTests. Everything is on the Studio layer so no scene
    /// camera sees it and it sees no scene.
    public static class CreatureBaker
    {
        public const int Size = 192;
        const string OutRoot = "Assets/UI/Resources/Art/creatures/";
        static readonly Vector3 Far = new Vector3(0f, -500f, 0f);

        /// `Application.dataPath` is `client/Assets`; the repo root is two up.
        static string Project(string relative) => Path.GetFullPath(Path.Combine(Application.dataPath, "..", relative));
        static string Results(string file) => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "implementation", "results", file));

        [MenuItem("Broodline/Bake Creature Sprites")]
        public static void Bake()
        {
            Directory.CreateDirectory(Project(OutRoot + "bodies"));
            Directory.CreateDirectory(Project(OutRoot + "parts"));

            var rig = new GameObject("bake-rig");
            var camera = new GameObject("bake-camera").AddComponent<Camera>();
            var light = new GameObject("bake-light").AddComponent<Light>();
            var rt = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            int n = 0;
            try
            {
                camera.transform.SetParent(rig.transform, false);
                light.transform.SetParent(rig.transform, false);
                rig.transform.position = Far;

                camera.orthographic = true;
                camera.orthographicSize = 0.9f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
                camera.cullingMask = 1 << CreatureAssembler.StudioLayer;
                camera.targetTexture = rt;
                camera.nearClipPlane = 0.1f; camera.farClipPlane = 20f;
                // Three-quarter front-left, a little above: the snout (+X) reads, the flank (-Z) faces us.
                camera.transform.localPosition = new Vector3(3.2f, 2.2f, -3.6f);
                camera.transform.LookAt(rig.transform.position + new Vector3(0f, 0.42f, 0f));

                light.type = LightType.Directional;
                light.cullingMask = 1 << CreatureAssembler.StudioLayer;
                light.transform.rotation = Quaternion.Euler(42f, -35f, 0f);
                light.intensity = 1.1f;

                foreach (var body in SpeciesRecipes.All)
                {
                    Shoot(rig, camera, rt, new CreatureLook { Species = body.Id }, Project(OutRoot + "bodies/" + body.Id + ".png"), partOnly: null); n++;
                    foreach (var socket in new[] { Sockets.Dorsal, Sockets.Flank })
                        foreach (var part in PartRecipes.All)
                        {
                            var look = socket == Sockets.Dorsal
                                ? new CreatureLook { Species = body.Id, Trait1 = part.Id }
                                : new CreatureLook { Species = body.Id, Trait2 = part.Id };
                            Shoot(rig, camera, rt, look, Project(OutRoot + "parts/" + body.Id + "-" + socket + "-" + part.Id + ".png"), partOnly: socket); n++;
                        }
                }

                ContactSheet(rig, camera, rt);
                Shoot(rig, camera, rt, new CreatureLook { Species = "vetch", Trait1 = "cinder", Trait2 = "carapace" },
                      Results("cinderplate.png"), partOnly: null, composite: true);
            }
            finally
            {
                camera.targetTexture = null;
                Object.DestroyImmediate(rt);
                Object.DestroyImmediate(rig);
            }
            AssetDatabase.Refresh();
            Debug.Log("[bake] wrote " + n + " sprites under " + OutRoot);
        }

        /// One frame. `partOnly` hides the body so the part PNG holds only the
        /// part, at its socket, from the same camera - aligned with the body PNG.
        static void Shoot(GameObject rig, Camera camera, RenderTexture rt, CreatureLook look, string path, string partOnly, bool composite = false)
        {
            var creature = CreatureAssembler.Build(look);
            try
            {
                creature.transform.SetParent(rig.transform, false);
                creature.transform.localPosition = Vector3.zero;
                creature.transform.localRotation = Quaternion.identity;
                CreatureAssembler.SetLayerRecursively(creature, CreatureAssembler.StudioLayer);
                if (partOnly != null && !composite)
                {
                    var smr = creature.GetComponentInChildren<SkinnedMeshRenderer>();
                    if (smr != null) smr.enabled = false;
                    foreach (var s in new[] { Sockets.Dorsal, Sockets.Flank })
                        if (s != partOnly) { var t = creature.transform.Find(s); if (t != null) t.gameObject.SetActive(false); }
                }
                creature.GetComponent<CreatureMotion>().Tick(0f, 0f);   // rest pose, no breath phase

                camera.Render();
                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
            }
            finally { Object.DestroyImmediate(creature); }
        }

        /// rig_proof.md section 4 item 2, rendered: every part in both sockets
        /// on Vetch and Pale (48 tiles when Pale exists), for the eyes-on pass.
        static void ContactSheet(GameObject rig, Camera camera, RenderTexture rt)
        {
            var bodies = new System.Collections.Generic.List<BodyRecipe>();
            foreach (var id in new[] { "vetch", "pale" }) { var b = SpeciesRecipes.For(id); if (b != null) bodies.Add(b); }
            int cols = PartRecipes.All.Count, rows = bodies.Count * 2;
            if (cols == 0 || rows == 0) return;
            var sheet = new Texture2D(cols * Size, rows * Size, TextureFormat.RGBA32, false);
            int row = 0;
            foreach (var body in bodies)
                foreach (var socket in new[] { Sockets.Dorsal, Sockets.Flank })
                {
                    int col = 0;
                    foreach (var part in PartRecipes.All)
                    {
                        var look = socket == Sockets.Dorsal
                            ? new CreatureLook { Species = body.Id, Trait1 = part.Id }
                            : new CreatureLook { Species = body.Id, Trait2 = part.Id };
                        var tmp = Path.Combine(Application.temporaryCachePath, "tile.png");
                        Shoot(rig, camera, rt, look, tmp, partOnly: null, composite: true);
                        var tile = new Texture2D(2, 2); tile.LoadImage(File.ReadAllBytes(tmp));
                        sheet.SetPixels(col * Size, (rows - 1 - row) * Size, Size, Size, tile.GetPixels());
                        Object.DestroyImmediate(tile);
                        col++;
                    }
                    row++;
                }
            sheet.Apply();
            File.WriteAllBytes(Results("creature-contact-sheet.png"), sheet.EncodeToPNG());
            Object.DestroyImmediate(sheet);
        }
    }
}
```

- [ ] **Step 4: `CreatureSprites.cs`**

```csharp
using System;
using UnityEngine;

namespace Broodline.UI.Components
{
    /// The baked sprites, by name. Replaces `SpeciesProxy`: the six interim
    /// proxies were a pre-test of bible 10.2 rule 1; these are the bodies
    /// the pipeline renders, and `SilhouetteTests` now measures them.
    ///
    /// NULL WHEN MISSING, NEVER A GUESS - the same rule SpeciesProxy kept.
    /// A trait this build has no part for leaves its layer empty; the card
    /// still names it in the pip.
    public static class CreatureSprites
    {
        public const string Root = "Art/creatures";

        public static string BodyPath(string species) => Root + "/bodies/" + Lower(species);
        public static string PartPath(string species, string socket, string trait) =>
            Root + "/parts/" + Lower(species) + "-" + Lower(socket) + "-" + Lower(trait);

        public static Texture2D Body(string species) =>
            string.IsNullOrWhiteSpace(species) ? null : Resources.Load<Texture2D>(BodyPath(species));

        public static Texture2D Part(string species, string socket, string trait) =>
            string.IsNullOrWhiteSpace(species) || string.IsNullOrWhiteSpace(trait) ? null
                : Resources.Load<Texture2D>(PartPath(species, socket, trait));

        static string Lower(string s) => (s ?? string.Empty).Trim().ToLowerInvariant();
    }
}
```

- [ ] **Step 5: The card**

`CreatureCard.uxml` — replace the single silhouette element:

```xml
    <ui:VisualElement name="slot" class="creature-card__slot">
        <ui:VisualElement name="silhouette" class="creature-card__layer" />
        <ui:VisualElement name="part-dorsal" class="creature-card__layer" />
        <ui:VisualElement name="part-flank" class="creature-card__layer" />
    </ui:VisualElement>
```

`CreatureCard.uss` — replace `.creature-card__silhouette` and delete the whole `.proxy` block:

```css
/* The slot final art drops into. Three aligned layers from one bake camera:
 * body, then combat one at the dorsal socket, then combat two at the flank.
 * Same 64px height as before, so nothing else in the card reflows. */
.creature-card__slot {
    height: 64px;
    border-radius: 12px;
    background-color: var(--surface-sunk);
    margin-bottom: 8px;
}
.creature-card__layer {
    position: absolute;
    top: 0; left: 0; right: 0; bottom: 0;
    -unity-background-scale-mode: scale-to-fit;
}
```

`CreatureCard.cs` — in `Bind`, replace the `SpeciesProxy` lines with:

```csharp
            SetLayer("silhouette", CreatureSprites.Body(creature.Species));
            SetLayer("part-dorsal", CreatureSprites.Part(creature.Species, "sk_dorsal", creature.Trait1));
            SetLayer("part-flank", CreatureSprites.Part(creature.Species, "sk_flank", creature.Trait2));
```

```csharp
        void SetLayer(string name, Texture2D texture)
        {
            var layer = this.Q<VisualElement>(name);
            layer.style.backgroundImage = texture == null ? new StyleBackground(StyleKeyword.None) : new StyleBackground(texture);
        }
```

Delete `SpeciesProxy.cs` and `client/Assets/UI/Art/proxies/` (with `.meta` files). Grep `SpeciesProxy` and `proxy--` across `client/Assets` and remove every remaining reference.

- [ ] **Step 6: Import settings**

In `client/Assets/Editor/ArtImportSettings.cs`, change the prefix check to `assetPath.StartsWith("Assets/UI/Resources/Art/creatures/")` and its comment's first line to name the bake.

- [ ] **Step 7: Bake, gate, look**

```bash
BAKE=1 ./implementation/scripts/generate-creatures.sh
ls client/Assets/UI/Resources/Art/creatures/bodies client/Assets/UI/Resources/Art/creatures/parts
./implementation/scripts/run-unity-tests.sh EditMode 2>&1 | tail -6
bash implementation/scripts/check-stylesheets.sh | tail -2
```

Expected: `vetch.png` and six part PNGs; about `total=348 passed=347 failed=1` — three added, the `SpeciesProxy` tests removed; the exact count depends on how many of those there were, so record what the run prints in the commit message. Open `vetch.png`: a teal dome, darker underneath, transparent corners. If the sprite is tiny or clipped, adjust `orthographicSize` (0.9) and the camera position; re-bake.

- [ ] **Step 8: Commit**

```bash
git add client/Assets/Creatures/Editor/CreatureBaker.cs* client/Assets/UI/Resources/Art \
        client/Assets/UI/Components/CreatureSprites.cs* client/Assets/UI/Components/CreatureCard.cs \
        client/Assets/UI/Components/Resources/CreatureCard.uxml client/Assets/UI/Components/Resources/CreatureCard.uss \
        client/Assets/UI/Tests/SilhouetteTests.cs client/Assets/UI/Tests/ComponentTests.cs \
        client/Assets/Editor/ArtImportSettings.cs
git rm -r client/Assets/UI/Art/proxies client/Assets/UI/Components/SpeciesProxy.cs client/Assets/UI/Components/SpeciesProxy.cs.meta
git commit -m "feat(ui): baked creature sprites, stacked three deep on the card

One fixed camera renders each body and each part in each socket into
aligned PNGs; the card stacks body, dorsal part, flank part, so both
combat traits are visible on every card at zero runtime cost. The six
proxies retire; SilhouetteTests measures the baked bodies instead.

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

## Task 10: The lane — creatures and raiders from the pipeline, and the dressing

Design §3.9. `WaveView.Build` takes the `CreatureSpec[]` deployment and the `WaveDef` and builds bodies through the assembler instead of `SyntheticCreature`. Raider bodies come from `wave.Spawns[i].Type`. `Render` drives `CreatureMotion`: moving while a raider's tile changes, a flinch when a creature's or raider's HP drops between the two retained snapshots, `Hurt01` from HP against the first HP seen. `LaneDressing` draws the ground, the path, the trees and the Ark. **What the view reads does not change**: every value still comes off `WavePair`.

**Files:**
- Modify: `client/Assets/View/WaveView.cs`, `client/Assets/Game/WaveRunner.cs` (`_view.Build(_runner, deployment, wave)`)
- Create: `client/Assets/View/LaneDressing.cs`
- Modify: `client/Assets/Editor/WaveSceneBuilder.cs` (background colour, culling mask), then rebuild `Wave.unity`
- Test: `client/Assets/View/Tests/WaveViewTests.cs` (new; the View test assembly already references `Broodline.Sim`)

**Interfaces (produced):**

```csharp
public void WaveView.Build(SimRunner r, CreatureSpec[] deployment, WaveDef wave)   // deployment may be null: species from r.CreatureSpecies, no parts
public static class LaneDressing { GameObject Build(Transform parent, int laneTiles, float tileSize, float pocketOffset); }
```

- [ ] **Step 1: The failing test**

`client/Assets/View/Tests/WaveViewTests.cs`:

```csharp
using Broodline.Sim.Combat;
using Broodline.View;
using NUnit.Framework;
using UnityEngine;

namespace Broodline.View.Tests
{
    public class WaveViewTests
    {
        [Test]
        public void Build_MakesOneAssembledCreaturePerDeploymentSlot_AndDressesTheLane()
        {
            var wave = WaveDef.ForId(1);
            var deployment = new[]
            {
                new CreatureSpec { Species = Species.Vetch, Trait1 = Trait.Carapace, Tier1 = 1, Trait2 = Trait.Taunt, Tier2 = 1, Instinct = Instinct.Vanguard, Pocket = 0 },
                new CreatureSpec { Species = Species.Vetch, Trait1 = Trait.Taunt, Tier1 = 1, Trait2 = Trait.Carapace, Tier2 = 1, Instinct = Instinct.Vanguard, Pocket = 1 },
            };
            var runner = new SimRunner(wave, wave.Lane, deployment, 6UL);
            var go = new GameObject("view");
            try
            {
                var view = go.AddComponent<WaveView>();
                view.Build(runner, deployment, wave);

                var creatures = go.transform.Find("creatures");
                Assert.AreEqual(2, creatures.childCount);
                Assert.AreEqual("carapace", creatures.GetChild(0).Find("sk_dorsal").GetChild(0).name);
                Assert.AreEqual("taunt", creatures.GetChild(1).Find("sk_dorsal").GetChild(0).name, "slot index, not trait, picks the socket");
                Assert.IsNotNull(go.transform.Find("dressing/ark"));
                Assert.IsNotNull(go.transform.Find("dressing/path"));
                Assert.AreEqual(runner.RaiderHp.Length, go.transform.Find("raiders").childCount, "one body per raider slot, inactive until visible");
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
```

`Broodline.View.Tests.asmdef` must reference `Broodline.Creatures` too.

- [ ] **Step 2: Run to see it fail**

```bash
./implementation/scripts/run-unity-tests.sh EditMode 2>&1 | tail -6
```

Expected: compile error, `Build` has no three-argument overload.

- [ ] **Step 3: `LaneDressing.cs`**

Colours are C# constants because the view cannot read USS; each is named for the token it mirrors.

```csharp
using UnityEngine;

namespace Broodline.View
{
    /// The handoff's lane, as dressing: a pastel field, a path strip with
    /// dashes, soft blob trees, an Ark prism. None of it is read by anything;
    /// the lane stays straight because Lane's distance table is straight.
    public static class LaneDressing
    {
        // Mirrors: --paper #f7f4fb, --green-tint #e8f5ec, the handoff's path #e5d9c7, --violet #7a6ac0, --surface #ffffff.
        static readonly Color Field = Hex("#e8f5ec");
        static readonly Color Path = Hex("#e5d9c7");
        static readonly Color Dash = Hex("#ffffff");
        static readonly Color Tree = Hex("#cfe6d2");
        static readonly Color Ark = Hex("#7a6ac0");

        public static GameObject Build(Transform parent, int laneTiles, float tileSize, float pocketOffset)
        {
            var root = new GameObject("dressing");
            root.transform.SetParent(parent, false);
            float length = laneTiles * tileSize;

            Flat("field", root.transform, new Vector3(length * 0.5f, -0.02f, 0.5f), new Vector3(length + 6f, 0.02f, 8f), Field);
            Flat("path", root.transform, new Vector3(length * 0.5f, -0.01f, 0f), new Vector3(length + 1f, 0.02f, 0.9f), Path);
            for (int t = 0; t < laneTiles; t++)
                Flat("dash" + t, root.transform, new Vector3(t * tileSize + 0.5f, 0.0f, 0f), new Vector3(0.4f, 0.02f, 0.08f), Dash);

            var trees = new[] { new Vector3(3f, 0f, 2.6f), new Vector3(9f, 0f, -1.9f), new Vector3(15.5f, 0f, 2.9f), new Vector3(20f, 0f, -2.2f) };
            for (int i = 0; i < trees.Length; i++)
            {
                var tree = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                tree.name = "tree" + i;
                tree.transform.SetParent(root.transform, false);
                tree.transform.localPosition = trees[i];
                tree.transform.localScale = new Vector3(1.6f, 0.7f, 1.6f);
                Paint(tree, Tree);
            }

            var ark = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ark.name = "ark";
            ark.transform.SetParent(root.transform, false);
            ark.transform.localPosition = new Vector3(length, 0.5f, 0f);
            ark.transform.localScale = new Vector3(1.4f, 0.5f, 1.4f);
            Paint(ark, Ark);
            return root;
        }

        static void Flat(string name, Transform parent, Vector3 at, Vector3 size, Color colour)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.transform.localScale = size;
            Paint(go, colour);
        }

        static void Paint(GameObject go, Color colour)
        {
            var r = go.GetComponent<Renderer>();
            r.material = new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = colour };
            Object.Destroy(go.GetComponent<Collider>());
        }

        static Color Hex(string hex) { ColorUtility.TryParseHtmlString(hex, out var c); return c; }
    }
}
```

In EditMode `Object.Destroy` is deferred; use `Object.DestroyImmediate` when `!Application.isPlaying`.

- [ ] **Step 4: `WaveView.cs`**

Replace `Build` and extend `Render`:

```csharp
        private Transform[] _raiders;
        private Transform[] _creatures;
        private CreatureMotion[] _raiderMotion;
        private CreatureMotion[] _creatureMotion;
        private int[] _raiderMaxHp;
        private int[] _creatureMaxHp;
        private float[] _raiderLastTile;

        public void Build(SimRunner r) => Build(r, null, null);

        public void Build(SimRunner r, CreatureSpec[] deployment, WaveDef wave)
        {
            LaneDressing.Build(transform, r.LaneTiles, TileSize, PocketOffset);

            var creaturesRoot = new GameObject("creatures").transform;
            creaturesRoot.SetParent(transform, false);
            _creatures = new Transform[r.CreatureCount];
            _creatureMotion = new CreatureMotion[r.CreatureCount];
            _creatureMaxHp = new int[r.CreatureCount];
            for (int c = 0; c < r.CreatureCount; c++)
            {
                var look = new CreatureLook { Species = r.CreatureSpecies[c].ToString() };
                if (deployment != null && c < deployment.Length)
                {
                    look.Trait1 = deployment[c].Trait1 == Trait.None ? null : deployment[c].Trait1.ToString();
                    look.Trait2 = deployment[c].Trait2 == Trait.None ? null : deployment[c].Trait2.ToString();
                }
                var body = CreatureAssembler.Build(look);
                body.name = "creature" + c + "-" + r.CreatureSpecies[c];
                body.transform.SetParent(creaturesRoot, false);
                body.transform.position = new Vector3(r.Lane.PocketTiles[r.CreaturePocket[c]] * TileSize, 0f, PocketOffset);
                body.transform.rotation = Quaternion.LookRotation(Vector3.back, Vector3.up) * Quaternion.Euler(0f, 90f, 0f); // snout (+X) toward the lane (-Z)
                _creatures[c] = body.transform;
                _creatureMotion[c] = body.GetComponent<CreatureMotion>();
                _creatureMaxHp[c] = 0;
            }

            var raidersRoot = new GameObject("raiders").transform;
            raidersRoot.SetParent(transform, false);
            _raiders = new Transform[r.RaiderHp.Length];
            _raiderMotion = new CreatureMotion[_raiders.Length];
            _raiderMaxHp = new int[_raiders.Length];
            _raiderLastTile = new float[_raiders.Length];
            for (int i = 0; i < _raiders.Length; i++)
            {
                var type = wave != null && i < wave.Spawns.Length ? wave.Spawns[i].Type.ToString() : "courser";
                var body = CreatureAssembler.Build(new CreatureLook { RaiderType = type });
                body.name = "raider" + i + "-" + type;
                body.transform.SetParent(raidersRoot, false);
                body.transform.rotation = Quaternion.identity;   // raiders walk +X, snout forward
                body.SetActive(false);
                _raiders[i] = body.transform;
                _raiderMotion[i] = body.GetComponent<CreatureMotion>();
            }
        }
```

In `Render`, after each raider's position is set:

```csharp
                    if (_raiderMotion[i] != null)
                    {
                        int hp = current.RaiderHp(i);
                        if (_raiderMaxHp[i] == 0) _raiderMaxHp[i] = hp;
                        _raiderMotion[i].Moving = !Mathf.Approximately(tile, _raiderLastTile[i]);
                        _raiderMotion[i].Hurt01 = _raiderMaxHp[i] > 0 ? 1f - (float)hp / _raiderMaxHp[i] : 0f;
                        if (hp < pair.Previous.RaiderHp(i)) _raiderMotion[i].Flinch();
                        _raiderLastTile[i] = tile;
                    }
```

and for creatures after their position:

```csharp
                    if (_creatureMotion[c] != null)
                    {
                        int hp = current.CreatureHp(c);
                        if (_creatureMaxHp[c] == 0) _creatureMaxHp[c] = hp;
                        _creatureMotion[c].Hurt01 = _creatureMaxHp[c] > 0 ? 1f - (float)hp / _creatureMaxHp[c] : 0f;
                        if (hp < pair.Previous.CreatureHp(c)) _creatureMotion[c].Flinch();
                    }
```

Delete `BuildMarker`, `BodySpec` and the `SyntheticCreature` usage from this file (the class itself stays for the benchmark). Add `using Broodline.Creatures;`. Until Task 15 adds raider recipes, `CreatureAssembler.Build` returns a magenta "missing" sphere for raiders and logs once per raider — expected and visible, not a failure; Task 15 removes it.

`WaveRunner.Configure`: `_view.Build(_runner, deployment, wave);`.

- [ ] **Step 5: The scene**

In `WaveSceneBuilder.Build`: `camera.backgroundColor = new Color(0.91f, 0.96f, 0.93f);` (the field colour, so the lane's edges do not show black) and `camera.cullingMask = ~(1 << 6);` (everything but `Studio`). Then, with the Editor closed:

```bash
"/Applications/Unity/Hub/Editor/6000.6.0f1/Unity.app/Contents/MacOS/Unity" -batchmode -quit \
  -projectPath "$(pwd)/client" -executeMethod WaveSceneBuilder.Build -logFile implementation/results/scene-build.log
grep "wrote Assets/Scenes/Wave.unity" implementation/results/scene-build.log
```

- [ ] **Step 6: Gates**

```bash
./implementation/scripts/run-unity-tests.sh EditMode 2>&1 | tail -6
```

Expected: one more passing test. Then, in the Editor, run `WaveCapturePlayTests` (PlayMode) — the capture tests must still pass unchanged, which is the proof the sim did not move; record the count in `phase9-blockers.txt`. Then press Play on `Wave.unity` standalone: Vetch bodies in pockets, magenta spheres walking the path (raiders, until Task 15), the dressing. Close the Editor.

- [ ] **Step 7: Commit**

```bash
git add client/Assets/View client/Assets/Game/WaveRunner.cs client/Assets/Editor/WaveSceneBuilder.cs client/Assets/Scenes/Wave.unity
git commit -m "feat(view): the lane draws assembled creatures and is dressed

Bodies from the pipeline instead of synthetic meshes; raider bodies from
the wave definition's spawn table; motion driven off the retained
snapshot pair and nothing else. What the view reads did not change; the
capture tests say so.

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

## Task 11: The portrait studio and `CreatureStage`

Design §3.8. `PortraitStudio` (game assembly) owns one camera, one 512² render texture, one directional light and one creature, all on the `Studio` layer far below the origin, turning slowly. `CreatureStage` (UI) shows a `Texture` and nothing more. The studio is created by `BootController` and handed to the director; a screen that wants a portrait receives the texture through its `Bind`. Only one portrait is live at a time.

**Files:**
- Create: `client/Assets/Game/Shell/PortraitStudio.cs`
- Create: `client/Assets/UI/Components/CreatureStage.cs`, `Resources/CreatureStage.uxml`, `Resources/CreatureStage.uss`
- Modify: `client/Assets/Game/Shell/BootController.cs`, `client/Assets/Game/Ftue/FtueDirector.cs` (constructor gains `PortraitStudio studio`, nullable for tests)
- Test: `client/Assets/UI/Tests/ComponentTests.cs`; a PlayMode test `client/Assets/Game/Tests/PlayMode/PortraitStudioPlayTests.cs`

**Interfaces (produced):**

```csharp
namespace Broodline.Game.Shell
{
    public sealed class PortraitStudio : MonoBehaviour
    {
        const int Size = 512;  const float TurnDegreesPerSecond = 28f;
        static PortraitStudio Create(Transform host);        // builds the rig on the Studio layer at (0, -400, 0)
        Texture Show(string species, string trait1, string trait2, float growth01);   // replaces the current creature; returns the render texture
        void Clear();                                        // destroys the current creature; the texture goes transparent
    }
}
namespace Broodline.UI.Components
{
    public sealed class CreatureStage : VisualElement { const string UssClassName = "creature-stage"; void SetTexture(Texture texture); }   // null clears
}
```

- [ ] **Step 1: The failing tests**

`ComponentTests.cs`:

```csharp
        [Test]
        public void CreatureStage_ShowsATexture_AndClearsOnNull()
        {
            var stage = new CreatureStage();
            var tex = new Texture2D(4, 4);
            stage.SetTexture(tex);
            Assert.AreEqual(tex, stage.Q<VisualElement>("frame").style.backgroundImage.value.texture);
            stage.SetTexture(null);
            Assert.IsNull(stage.Q<VisualElement>("frame").style.backgroundImage.value.texture);
            Object.DestroyImmediate(tex);
        }
```

`PortraitStudioPlayTests.cs`:

```csharp
using System.Collections;
using Broodline.Game.Shell;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Broodline.Game.PlayTests
{
    public class PortraitStudioPlayTests
    {
        [UnityTest]
        public IEnumerator Show_ProducesANonBlankTexture_WithinAFewFrames()
        {
            var host = new GameObject("studio-host");
            var studio = PortraitStudio.Create(host.transform);
            var texture = (RenderTexture)studio.Show("vetch", "carapace", "taunt", 0f);
            for (int i = 0; i < 5; i++) yield return null;

            var prev = RenderTexture.active;
            RenderTexture.active = texture;
            var read = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
            read.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
            read.Apply();
            RenderTexture.active = prev;

            int opaque = 0;
            foreach (var p in read.GetPixels()) if (p.a > 0.5f) opaque++;
            Assert.Greater(opaque, texture.width * texture.height / 50, "the studio rendered nothing");
            Assert.Less(opaque, texture.width * texture.height / 2, "the background must stay transparent");

            Object.Destroy(read);
            Object.Destroy(host);
        }
    }
}
```

- [ ] **Step 2: Run to see them fail**

```bash
./implementation/scripts/run-unity-tests.sh EditMode 2>&1 | tail -6
```

Expected: compile errors on `CreatureStage`, `PortraitStudio`.

- [ ] **Step 3: `CreatureStage`**

`Resources/CreatureStage.uxml`:

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements" editor-extension-mode="False">
    <ui:Style src="CreatureStage.uss" />
    <ui:VisualElement name="frame" class="creature-stage__frame" />
</ui:UXML>
```

`Resources/CreatureStage.uss`:

```css
.creature-stage { align-items: center; justify-content: center; }
.creature-stage__frame {
    width: 128px;
    height: 128px;
    border-radius: 64px;   /* half the measured height: the hero slot is a circle */
    background-color: var(--surface-sunk);
    -unity-background-scale-mode: scale-to-fit;
}
```

`CreatureStage.cs`:

```csharp
using UnityEngine;
using UnityEngine.UIElements;

namespace Broodline.UI.Components
{
    /// A texture, framed. The portrait studio in Broodline.Game owns the
    /// camera and the creature; this element knows nothing about either -
    /// which is what keeps Broodline.UI free of Broodline.Creatures.
    public sealed class CreatureStage : VisualElement
    {
        public const string UssClassName = "creature-stage";
        readonly VisualElement _frame;

        public CreatureStage()
        {
            AddToClassList(UssClassName);
            Resources.Load<VisualTreeAsset>("CreatureStage").CloneTree(this);
            _frame = this.Q<VisualElement>("frame");
        }

        public void SetTexture(Texture texture)
        {
            if (texture == null) _frame.style.backgroundImage = new StyleBackground(StyleKeyword.None);
            else if (texture is RenderTexture rt) _frame.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(rt));
            else _frame.style.backgroundImage = new StyleBackground((Texture2D)texture);
        }
    }
}
```

- [ ] **Step 4: `PortraitStudio`**

```csharp
using Broodline.Creatures;
using UnityEngine;

namespace Broodline.Game.Shell
{
    /// One camera, one render texture, one creature, turning. Phase 9 design
    /// §3.8. Far below the origin on the Studio layer so no scene camera
    /// sees it and it sees no scene. Created once by BootController; the
    /// director shows and clears it around the three hero moments.
    public sealed class PortraitStudio : MonoBehaviour
    {
        public const int Size = 512;
        public const float TurnDegreesPerSecond = 28f;
        static readonly Vector3 Far = new Vector3(0f, -400f, 0f);

        Camera _camera;
        RenderTexture _texture;
        GameObject _creature;

        public static PortraitStudio Create(Transform host)
        {
            var go = new GameObject("portrait-studio");
            go.transform.SetParent(host, false);
            go.transform.position = Far;
            go.layer = CreatureAssembler.StudioLayer;
            var studio = go.AddComponent<PortraitStudio>();

            var cam = new GameObject("camera").AddComponent<Camera>();
            cam.transform.SetParent(go.transform, false);
            cam.orthographic = true;
            cam.orthographicSize = 0.9f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            cam.cullingMask = 1 << CreatureAssembler.StudioLayer;
            cam.nearClipPlane = 0.1f; cam.farClipPlane = 20f;
            cam.transform.localPosition = new Vector3(3.2f, 2.2f, -3.6f);
            cam.transform.LookAt(go.transform.position + new Vector3(0f, 0.42f, 0f));
            cam.gameObject.layer = CreatureAssembler.StudioLayer;

            var light = new GameObject("light").AddComponent<Light>();
            light.transform.SetParent(go.transform, false);
            light.type = LightType.Directional;
            light.cullingMask = 1 << CreatureAssembler.StudioLayer;
            light.transform.rotation = Quaternion.Euler(42f, -35f, 0f);
            light.intensity = 1.1f;

            studio._camera = cam;
            studio._texture = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            cam.targetTexture = studio._texture;
            cam.enabled = false;   // costs nothing until Show
            return studio;
        }

        public Texture Show(string species, string trait1, string trait2, float growth01)
        {
            Clear();
            _creature = CreatureAssembler.Build(new CreatureLook { Species = species, Trait1 = trait1, Trait2 = trait2, Growth01 = growth01 });
            _creature.transform.SetParent(transform, false);
            _creature.transform.localPosition = Vector3.zero;
            CreatureAssembler.SetLayerRecursively(_creature, CreatureAssembler.StudioLayer);
            _camera.enabled = true;
            return _texture;
        }

        public void Clear()
        {
            if (_creature != null) Destroy(_creature);
            _creature = null;
            if (_camera != null) _camera.enabled = false;
        }

        void Update()
        {
            if (_creature != null) _creature.transform.Rotate(0f, TurnDegreesPerSecond * Time.deltaTime, 0f, Space.World);
        }

        void OnDestroy()
        {
            if (_camera != null) _camera.targetTexture = null;
            if (_texture != null) _texture.Release();
        }
    }
}
```

The camera is disabled while nothing is shown, so the studio costs nothing between hero moments.

- [ ] **Step 5: Wiring**

`BootController.Start`, before the director is constructed: `_studio = PortraitStudio.Create(transform);` (a field `PortraitStudio _studio;`). `FtueDirector`'s constructor gains a trailing `PortraitStudio studio = null` parameter stored in `_studio`; Tasks 14 and 16 use it. `BootController` passes `_studio`.

- [ ] **Step 6: Gates**

```bash
./implementation/scripts/run-unity-tests.sh EditMode 2>&1 | tail -6
```

Expected: one more passing test. Then, in the Editor, run `PortraitStudioPlayTests` from the Test Runner (PlayMode); record the result in `phase9-blockers.txt`.

- [ ] **Step 7: Commit**

```bash
git add client/Assets/Game/Shell/PortraitStudio.cs* client/Assets/UI/Components/CreatureStage.cs* \
        client/Assets/UI/Components/Resources/CreatureStage.* client/Assets/Game/Shell/BootController.cs \
        client/Assets/Game/Ftue/FtueDirector.cs client/Assets/UI/Tests/ComponentTests.cs \
        client/Assets/Game/Tests/PlayMode/PortraitStudioPlayTests.cs*
git commit -m "feat(shell): a portrait studio, and a stage element that shows its texture

One camera, one render texture, one creature turning on the Studio
layer far below the origin. The UI element knows only a Texture, which
is what keeps the UI assembly free of the creatures assembly.

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

## Task 12: STOP — Vetch, on a card and on the lane, in front of the developer

Design §5 step 2. This is the earliest point at which the 3D look can be judged, and the point where "no, warmer" costs one recipe rather than six. **A human does this. No agent completes this task.**

- [ ] **Step 1: Capture what exists**

With the Editor closed:

```bash
bash implementation/scripts/capture-screens.sh 2>&1 | tail -3
open implementation/results/screens/RosterView.png implementation/results/cinderplate.png
```

- [ ] **Step 2: Look, in the Editor**

Open the Editor. Play `Wave.unity` standalone: Vetch in its pockets, breathing, flinching when hit. Drag `vetch.prefab` into `SampleScene` next to a `CreatureStage`-sized reference and rotate it. Look at `RosterView.png` at phone size.

- [ ] **Step 3: Write the verdict**

Create `implementation/results/phase9-vetch-stop.md` (tracked with `-f`) with three lines answered in the developer's words: **shape** (does the recipe read as bible §1.2's "low dome, four stubby legs, no neck"?), **surface** (is the ramp warm enough, the rim visible on a white card, the underside dark enough?), **motion** (does it feel alive or mechanical?). Then one of: `GO` — Task 13 proceeds; or `ADJUST: <what>` — the named recipe or shader value is changed in a fix-up commit, regenerated, re-baked, and this step repeats.

- [ ] **Step 4: Commit**

```bash
git add -f implementation/results/phase9-vetch-stop.md
git commit -m "docs(record): the Vetch stop - the 3D look judged on one creature

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

## Task 13: The component vocabulary, and the scaffold's header

Design §4.1. Everything the nine screens are built from, built once. Each component is one element with one purpose, in `Broodline.UI.Components`, loading its UXML the way `StatCell` does, with a structure test in `ComponentTests.cs` and a row in the `Components` fixture so the capture shows it. The scaffold header gains the eyebrow, the back pill and the resource pill. Two more ramp textures are generated. Every new value is a token.

**Files:**
- Create, each with `Resources/<Name>.uxml` and `.uss`: `GenChip.cs`, `TraitChip.cs`, `HeroSlot.cs`, `InheritanceBar.cs`, `MutationBanner.cs`, `LineageStrip.cs`, `CostCtaRow.cs`, `FieldSlotRow.cs`, `LanePreviewCard.cs`
- Modify: `ScreenScaffold.cs`, `Resources/ScreenScaffold.uxml/.uss`, `StatCell.cs` + resources (trend arrow), `Shell/Tokens.uss`, `Shell/Theme.uss` (`.btn-primary--hybrid`), `Art/generate-textures.py` (+ `amber-ramp.png`, `hybrid-ramp.png`), `Editor/ScreenFixtures.cs` (`Components()`)
- Test: `UI/Tests/ComponentTests.cs`, `UI/Tests/ScaffoldTests.cs`

**Interfaces (produced; every later screen task uses these verbatim):**

```csharp
// Scaffold
public ScreenScaffold(string title, bool pushed = false, Action onBack = null, string eyebrow = null)
public string Eyebrow { set; }                       // null/empty hides the label
// back pill: existing #back Button, now class "screen-scaffold__back pill-back"; onBack semantics unchanged (null removes it)
public void SetResourcePill(string icon, string value, string suffix = null)   // e.g. ("icon--charge", "4", "/5"); null value removes the pill from HeaderSlot

// Chips
public GenChip(int generation)                       // text "G4", class "chip gen-chip", numeral on .t-num
public TraitChip(string trait, int? tier, string species)   // text via CreatureLabel.TraitWithTier; class "chip trait-chip trait-chip--<species>"; null tier adds "aberrant"

// Slots and stages
public HeroSlot()                                    // dashed ring + three stacked layers, 96px; Bind(CreatureDto) sets the layers from CreatureSprites; Bind(null) clears
public HeroSlot(CreatureStage stage)                 // the ring around a live turntable instead of sprites
public LanePreviewCard()                             // 4:3 card showing a Texture via SetTexture(Texture); SetSlots(IReadOnlyList<(string label, bool filled)>) draws the A/B/C/D tags along the bottom edge

// Bars and banners
public InheritanceBar(string trait, float odds01, string tag, string modifier)   // label, .t-num percentage, ProgressBar with progress-bar--<modifier>, tag chip ("DOM"/"REC")
public MutationBanner()                              // .panel-amber with the sparkle icon; Text setter collapses on empty
public LineageStrip()                                // Bind(IReadOnlyList<(int gen, string species, bool current)>, string note): nodes on a rail, generation labels on .t-num, a note line
                                                     // const NodeUssClassName = "lineage-strip__node", CurrentUssClassName = "lineage-strip__node--current", GenUssClassName = "lineage-strip__gen"

// Rows
public CostCtaRow(string costIcon, string cost, string ctaLabel, Action onCta)   // left: "COST" eyebrow + icon + .t-num; right: the primary button; .Enabled
public FieldSlotRow(string slotLetter, string label, bool filled, Action onTap)  // "A  Vetch Wall R2" / "B  Empty"; class field-slot-row, modifier --filled, --selected via .Selected

// StatCell
public StatCell(string label, string value, Trend trend = Trend.None)   // enum Trend { None, Up, Down }; adds icon--trend-up / icon--trend-down beside the value
```

- [ ] **Step 1: Tokens and textures**

Append to `Tokens.uss` (each with its comment naming its use):

```css
    --hero-slot: 96px;            /* HeroSlot outer size; the handoff's 116px parent art circle at 0.83 scale for a 390 frame */
    --hero-ring: 2px;             /* dashed ring width */
    --lane-card-height: 220px;    /* LanePreviewCard, 4:3 at the 366px content width */
    --amber-bright-deep: #e7a324; /* amber ramp end - MutationBanner gradient */
    --hybrid-tint-deep: #ebe4f7;  /* predicted-hybrid panel gradient end */
    --field-badge: 22px;          /* FieldSlotRow letter badge */
```

In `generate-textures.py`, add two ramps beside the CTA ramp, 2×64 each: `amber-ramp.png` from `#fdf1d8` to `#f7e0a8` (the banner's fill), `hybrid-ramp.png` from `#f7f4fb` to `#ebe4f7`. Run it: `python3 client/Assets/UI/Art/generate-textures.py`. Both are LFS.

- [ ] **Step 2: The failing tests**

In `ComponentTests.cs`, one test per component, the pattern being structure and text. Write all nine before implementing:

```csharp
        [Test] public void GenChip_ShowsGWithTheGeneration_OnTNum()
        { var c = new GenChip(4); Assert.AreEqual("G4", c.Q<Label>().text); Assert.IsTrue(c.Q<Label>().ClassListContains("t-num")); }

        [Test] public void TraitChip_IsTintedByTheSpecies_AndMarksAberrant()
        { var c = new TraitChip("Carapace", 3, "Vetch"); Assert.IsTrue(c.ClassListContains("trait-chip--vetch")); StringAssert.Contains("Carapace", c.Q<Label>().text);
          var a = new TraitChip("Cinder", null, "Ember"); Assert.IsTrue(a.ClassListContains("aberrant")); }

        [Test] public void HeroSlot_StacksThreeLayers_AndClearsOnNull()
        { var h = new HeroSlot(); h.Bind(Creature("Vetch", gen: 1, name: "Ash", founder: true, trait1: "Carapace", tier1: 1, trait2: "Taunt", tier2: 1));
          Assert.IsNotNull(h.Q<VisualElement>("body").style.backgroundImage.value.texture);
          Assert.IsNotNull(h.Q<VisualElement>("ring")); h.Bind(null); Assert.IsNull(h.Q<VisualElement>("body").style.backgroundImage.value.texture); }

        [Test] public void InheritanceBar_ShowsOddsOnTNum_AndTheTag()
        { var b = new InheritanceBar("Carapace III", 0.78f, "DOM", "teal"); Assert.AreEqual("78%", b.Q<Label>("odds").text);
          Assert.IsTrue(b.Q<Label>("odds").ClassListContains("t-num")); Assert.AreEqual("DOM", b.Q<Label>("tag").text);
          Assert.IsNotNull(b.Q<VisualElement>(className: "progress-bar--teal")); }

        [Test] public void MutationBanner_CollapsesWhenEmpty()
        { var m = new MutationBanner(); Assert.AreEqual(DisplayStyle.None, m.style.display.value); m.Text = "Mutation window open"; Assert.AreEqual(DisplayStyle.Flex, m.style.display.value); }

        [Test] public void LineageStrip_DrawsOneNodePerGeneration_AndMarksTheCurrent()
        { var s = new LineageStrip(); s.Bind(new[] { (1, "vetch", false), (4, "vetch", false), (7, "vetch", true) }, "Unbroken Vetch line since G1");
          Assert.AreEqual(3, s.Query<VisualElement>(className: LineageStrip.NodeUssClassName).ToList().Count);
          Assert.AreEqual(1, s.Query<VisualElement>(className: LineageStrip.CurrentUssClassName).ToList().Count);
          Assert.AreEqual("G7", s.Query<Label>(className: LineageStrip.GenUssClassName).Last().text); }

        [Test] public void CostCtaRow_ShowsCostOnTNum_AndOnePrimaryButton()
        { var r = new CostCtaRow("icon--charge", "2", "Begin Splice", () => { }); Assert.AreEqual("2", r.Q<Label>("cost").text);
          Assert.IsTrue(r.Q<Label>("cost").ClassListContains("t-num")); Assert.AreEqual("Begin Splice", r.Q<Button>().text); Assert.IsTrue(r.Q<Button>().ClassListContains("btn-primary")); }

        [Test] public void FieldSlotRow_StatesTheLetterAndTheOccupant()
        { var e = new FieldSlotRow("B", "Empty", filled: false, onTap: () => { }); Assert.AreEqual("B", e.Q<Label>("letter").text); Assert.IsFalse(e.ClassListContains("field-slot-row--filled"));
          var f = new FieldSlotRow("A", "Vetch Wall R2", filled: true, onTap: () => { }); Assert.IsTrue(f.ClassListContains("field-slot-row--filled")); f.Selected = true; Assert.IsTrue(f.ClassListContains("field-slot-row--selected")); }

        [Test] public void LanePreviewCard_ShowsATextureAndFourSlotTags()
        { var c = new LanePreviewCard(); var t = new Texture2D(4, 4); c.SetTexture(t); Assert.AreEqual(t, c.Q<VisualElement>("lane").style.backgroundImage.value.texture);
          c.SetSlots(new[] { ("A", true), ("B", false), ("C", false), ("D", true) }); Assert.AreEqual(4, c.Query<Label>(className: LanePreviewCard.SlotUssClassName).ToList().Count); Object.DestroyImmediate(t); }

        [Test] public void StatCell_ShowsATrendArrow()
        { var c = new StatCell("Armor", "B+", StatCell.Trend.Up); Assert.IsNotNull(c.Q<VisualElement>(className: "icon--trend-up")); }
```

In `ScaffoldTests.cs`:

```csharp
        [Test]
        public void Scaffold_ShowsAnEyebrow_AndAResourcePill_WhenGiven()
        {
            var s = new ScreenScaffold("Splicing Chamber", eyebrow: "Gene Lab");
            Assert.AreEqual("Gene Lab", s.Q<Label>("eyebrow").text);
            Assert.AreEqual(DisplayStyle.Flex, s.Q<Label>("eyebrow").style.display.value);
            s.SetResourcePill("icon--charge", "4", "/5");
            Assert.AreEqual("4", s.Q<Label>("pill-value").text);
            Assert.IsTrue(s.Q<Label>("pill-value").ClassListContains("t-num"));
            s.SetResourcePill(null, null);
            Assert.IsNull(s.Q<VisualElement>("resource-pill"));
        }
```

- [ ] **Step 3: Run to see them fail**

```bash
./implementation/scripts/run-unity-tests.sh EditMode 2>&1 | tail -6
```

Expected: compile errors.

- [ ] **Step 4: The scaffold header**

`ScreenScaffold.uxml`:

```xml
    <ui:VisualElement name="header" class="screen-scaffold__header">
        <ui:Button name="back" class="screen-scaffold__back" />
        <ui:VisualElement name="titles" class="screen-scaffold__titles">
            <ui:Label name="eyebrow" class="screen-scaffold__eyebrow t-micro" />
            <ui:Label name="title" class="screen-scaffold__title t-screen-title" />
        </ui:VisualElement>
        <ui:VisualElement name="header-slot" class="screen-scaffold__header-slot" />
    </ui:VisualElement>
```

`ScreenScaffold.uss`: `.screen-scaffold__titles { flex-grow: 1; flex-direction: column; }`, `.screen-scaffold__title { flex-grow: 0; }`, `.screen-scaffold__eyebrow { color: var(--mute); letter-spacing: 1px; display: none; }`, and the back control becomes the handoff's white pill: `background-color: var(--surface); border-radius: 17px; /* half of the measured 34px */` inside an `.elev-1` wrapper is not possible for a Button, so keep the flat pill with a `--hairline` 1px border. The resource pill: a `VisualElement.resource-pill` (`background-color: var(--surface); border-radius: 17px; /* half of 34px */ padding: 6px 12px 6px 7px; flex-direction: row; align-items: center;`) holding an icon element and a `Label.t-num` plus an optional muted suffix label.

`ScreenScaffold.cs`: the new constructor parameter, the `Eyebrow` setter (hide on empty), and `SetResourcePill` building or removing `#resource-pill` inside `HeaderSlot`.

- [ ] **Step 5: The nine components**

Each follows `StatCell.cs`'s shape exactly: `AddToClassList(UssClassName)`, `Resources.Load<VisualTreeAsset>(name).CloneTree(this)`, `Q<>` lookups, no `[UxmlElement]` unless a parameterless constructor is needed (`HeroSlot`, `MutationBanner`, `LineageStrip`, `LanePreviewCard` need one). Value-bearing rules, all tokens:

- `GenChip.uss`: `.gen-chip { }` — inherits `.chip`.
- `TraitChip.uss`: six `.trait-chip--<species>` rules setting `background-color` to the species tint (`--teal-tint`, `--coral-tint`, `--amber-tint`, `--violet-tint`, `--green-tint`, and for pale `--surface-sunk`) and `color` to the matching `--*-text` (pale: `--ink`). `.trait-chip.aberrant` adds a `--violet-pale` 1px border.
- `HeroSlot.uss`: `.hero-slot { width: var(--hero-slot); height: var(--hero-slot); align-items: center; justify-content: center; }`, `.hero-slot__ring { position: absolute; top: 0; left: 0; right: 0; bottom: 0; border-radius: 48px; /* half of --hero-slot */ border-width: var(--hero-ring); border-color: var(--mute-soft); }` (USS has no dashed borders; a dotted look is a 3× ring PNG generated in `generate-textures.py` as `hero-ring.png`, used as `background-image` — add it), three `.hero-slot__layer` absolute children.
- `InheritanceBar.uss`: a row with a 10px species dot, the trait label, the odds on `.t-num`, then a `ProgressBar` under it; the tag chip inherits `.chip`.
- `MutationBanner.uss`: `.mutation-banner { background-image: url("project:///Assets/UI/Art/amber-ramp.png"); border-radius: var(--radius-row); border-width: 1px; border-color: var(--amber); padding: var(--space-3); flex-direction: row; align-items: center; }`; text on `.t-warn`.
- `LineageStrip.uss`: `.lineage-strip__rail { height: 2px; background-color: var(--hairline); }`, nodes 24px squares `border-radius: var(--radius-chip)` tinted by species with `--current` getting a `--violet` 2px border; generation labels `.t-num`.
- `CostCtaRow.uss`: `flex-direction: row; align-items: center;` left column `COST` eyebrow (`.t-micro`, `--mute`) over icon + `.t-num` value; the button `flex-grow: 1`.
- `FieldSlotRow.uss`: `background-color: var(--surface-sunk); border-radius: var(--radius-row); padding: var(--space-3); flex-direction: row; align-items: center;`, letter badge `width: var(--field-badge); height: var(--field-badge); border-radius: 11px; /* half of --field-badge */ background-color: var(--violet-tint); color: var(--violet-text);`, `--filled` sets the label to `--ink` (empty: `--mute`), `--selected` adds a 2px `--ring` border.
- `LanePreviewCard.uss`: `.lane-preview-card { height: var(--lane-card-height); border-radius: var(--radius-card); background-color: var(--green-tint); overflow: hidden; }`, `.lane-preview-card__lane { position: absolute; top: 0; left: 0; right: 0; bottom: 0; -unity-background-scale-mode: scale-and-crop; }`, slot tags along the bottom as small white pills.
- `StatCell`: a `Trend` enum; the arrow is an `icon` element with `icon--trend-up` (green) or `icon--trend-down` (coral); add the two 19px glyphs to the icon sheet the way Task 4 of Phase 8 added the thirteen (`icons.uss` + the sheet PNG regenerated).

- [ ] **Step 6: Fixtures**

In `ScreenFixtures.Components()`, add one instance of every new component with sample data, each with `flexShrink = 0`.

- [ ] **Step 7: Gates**

```bash
./implementation/scripts/run-unity-tests.sh EditMode 2>&1 | tail -6
bash implementation/scripts/check-stylesheets.sh | tail -2
bash implementation/scripts/verify-uss-tokens.sh | tail -2
bash implementation/scripts/capture-screens.sh 2>&1 | tail -3
open implementation/results/screens/Components.png
```

Expected: eleven more passing tests; scripts green; `Components.png` shows every new part. Look at it.

- [ ] **Step 8: Commit**

```bash
git add client/Assets/UI/Components client/Assets/UI/Shell/Tokens.uss client/Assets/UI/Shell/Theme.uss client/Assets/UI/Shell/icons.uss \
        client/Assets/UI/Art client/Assets/UI/Tests client/Assets/Editor/ScreenFixtures.cs
git commit -m "feat(ui): the component vocabulary the handoff's screens are made of

Eyebrow, back pill and resource pill on the scaffold; gen and trait
chips, the hero slot, inheritance bars, the mutation banner, the lineage
strip, the cost-plus-CTA row, field slot rows, the lane preview card, a
trend arrow on the stat cell. Every value is a token; two more ramps.

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

## Task 14: Founder naming and campaign select

Design §4.2 rows 1–2. The first two screens a tester sees.

**Files:**
- Modify: `client/Assets/UI/Screens/FounderNamingView.cs` + `Resources/FounderNamingView.uxml/.uss`, `CampaignSelectView.cs` + resources, `client/Assets/UI/FirstHourScreens.cs` (copy), `client/Assets/Game/Ftue/FtueDirector.cs` (`NameFounderAsync` shows the studio), `client/Assets/Editor/ScreenFixtures.cs`
- Test: `client/Assets/UI/Tests/FirstHourScreensTests.cs`

**Interfaces:**
- `FounderNamingView.Bind(CreatureDto founder, string defaultName, Action<string> onName, Action onSkip, Texture portrait = null)` — the new trailing parameter; null falls back to the sprite stack.
- `FounderNamingScreen.Eyebrow = "Your Gene Ark"`, `Step = "1 / 5"`, `Note = "Founders keep their names for life."` (the existing footer note becomes the violet tip note).

- [ ] **Step 1: Founder naming, against `Onboarding.dc.html` step 1**

1. Scaffold with eyebrow `FounderNamingScreen.Eyebrow`; five progress pips in the header slot (a new `.progress-pip` rule in `FounderNamingView.uss`: `width: var(--space-2); height: var(--space-1); border-radius: 2px; /* half of --space-1 */ background-color: var(--hairline);` with `--current` in `--violet`), step label `.t-num`.
2. Above the prompt: a `HeroSlot(CreatureStage)` when `portrait` is given, else `HeroSlot()` bound to the founder — inside a `SectionCard`, centred.
3. The name field in a `SectionCard` with `.elev-1`; the blocker collapses as before.
4. The note in a `.panel-violet` with the tip glyph (tone: violet tip, per the handoff's step 1).
5. CTA row: `Name it` primary, `Not now` quiet — unchanged labels, unchanged callbacks (`FirstHourScreensTests` pins them).
6. Delete from `FounderNamingView.uss` every rule a component now provides. The stylesheet shrinks.

In `FtueDirector.NameFounderAsync`, before `_flow.ShowAsync`: `var portrait = _studio == null ? null : _studio.Show(founder.Species, founder.Trait1, founder.Trait2, 0f);` and pass it; after the turn resolves, `_studio?.Clear()`.

- [ ] **Step 2: Campaign select**

1. Scaffold with eyebrow `"Hollow Reach"` (`CampaignSelectScreen.Eyebrow`), title unchanged.
2. Option rows inside one `SectionCard` — the handoff's composition, and the WCAG note's resolution: the sunk fill sits on white.
3. The locked row keeps `icon--lock`; the cleared rows get a `GenChip`-styled `.chip` reading `Cleared` in `--green-tint`/`--green-text`.
4. Stylesheet shrinks.

- [ ] **Step 3: Tests**

In `FirstHourScreensTests.cs`, add: the eyebrow text is present on both screens; founder naming shows a `CreatureStage` when a texture is passed and a `HeroSlot` body layer when not; campaign select's option rows sit inside a `SectionCard`. Existing tests stay green unchanged.

- [ ] **Step 4: Gates and captures**

```bash
./implementation/scripts/run-unity-tests.sh EditMode 2>&1 | tail -4
bash implementation/scripts/check-stylesheets.sh | tail -2 && bash implementation/scripts/verify-uss-tokens.sh | tail -2
bash implementation/scripts/capture-screens.sh 2>&1 | tail -3
open implementation/results/screens/FounderNamingView.png implementation/results/screens/CampaignSelectView.png
```

Put each capture beside `specs/Designs/shots/` and the `.dc.html` in a browser at 390px. Fix what differs. Iterate until the gap is spacing you can name, not structure.

- [ ] **Step 5: Commit**

```bash
git add client/Assets/UI/Screens client/Assets/UI/FirstHourScreens.cs client/Assets/Game/Ftue/FtueDirector.cs \
        client/Assets/UI/Tests/FirstHourScreensTests.cs client/Assets/Editor/ScreenFixtures.cs
git commit -m "feat(ui): founder naming and campaign select to the handoff

The founder turns in the portrait studio above the name field; the
onboarding step frame, pips and tip note; campaign options inside a
section card, which is where the handoff puts them.

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

## Task 15: The other five species, nine parts, three raiders — and the full bake

Design §3.3, §3.7, §3.9; design §5 step 4. Recipes only, in the pipeline Task 7 built. Then the bake produces all six bodies, 144 part sprites, the forty-eight-combination contact sheet and Cinderplate; `SilhouetteTests` returns to six species; the lane's magenta raiders disappear.

**Files:**
- Modify: `client/Assets/Creatures/Recipes/SpeciesRecipes.cs`, `PartRecipes.cs`, `RaiderRecipes.cs`
- Modify: `client/Assets/UI/Tests/SilhouetteTests.cs` (`Species` back to six)
- Generated: meshes, materials, prefabs, sprites, `implementation/results/creature-contact-sheet.png`, `cinderplate.png`

**Interfaces:** unchanged. `SpeciesRecipes.All` holds six in bible §1.2 order; `PartRecipes.All` holds twelve: `carapace, taunt, cinder, splash, sprint, litter, reach, pierce, regrow, burrow, screen, chill`; `RaiderRecipes.All` holds `courser, lash, skirmisher`.

- [ ] **Step 1: Restore the six in `SilhouetteTests`** — the failing test for this task: five bodies have no sprite.

- [ ] **Step 2: The bodies**, from bible §1.2's silhouette column, each facing +X, standing on y=0, about one unit long, eight bones or fewer, three sockets, growth on head and limbs. Recipes are authored in the same shape as Vetch (Task 7 Step 8); the silhouettes to hit:

| Species | Silhouette (bible §1.2) | The recipe, in words |
|---|---|---|
| Ember | Tall narrow torso, head crest, two legs | Capsule torso (0.2, 0.15)→(0.2, 1.0) r 0.18; head sphere at (0.3, 1.15) r 0.16; two leg capsules; the crest is the `sk_crown` socket's job — leave the head bare. Dorsal at (0.05, 1.15, 0) on the shoulders, flank at (0.2, 0.7, -0.22) |
| Skitter | Small body, six long thin legs, tiny head | Body sphere r 0.22 at y 0.45; head sphere r 0.09; six capsules r 0.035 out to ±0.55; blend 0.08 so legs stay thin. Grid 28 |
| Hollow | Tiny body, stilt legs, long forward neck | Body sphere r 0.18 at (0, 0.75); two stilt capsules r 0.045 to the ground; neck capsule (0.1, 0.8)→(0.75, 1.05) r 0.06; head sphere r 0.1 at (0.8, 1.08) |
| Loam | Segmented ground-hugger, blunt snout, no legs | Five spheres r 0.24 along x from -0.55 to 0.55 at y 0.24, blend 0.12; snout box at the front; bones `seg0..seg4` chained so the socket on `seg2` rides a segment (rig proof §4 item 10 answered: the part rides one segment) |
| Pale | Broad wing arc, small hanging body | Body sphere r 0.16 at y 0.55; two wing capsules (0, 0.7, 0)→(±0.05, 0.95, ±0.8) r 0.09 flattened by a box each; the wings on bones `wing_l`, `wing_r`. Dorsal at (0, 0.85, 0) between the wing roots; flank at (0, 0.5, -0.2). Grid 28 |

- [ ] **Step 3: The parts**, each authored at the origin, +Y outward, sized for a unit body, colours from the species that owns the trait (bible §1.2), under = the species' underside:

| Part | Shape |
|---|---|
| splash | a flat disc (box 0.3×0.03×0.3) with three small spheres on its rim — a splash plate |
| sprint | two swept fins: capsules leaning back along -X |
| litter | a cluster of five small spheres, blend 0.1 — a clutch |
| reach | a long thin capsule (0,0,0)→(0.35,0.55,0) r 0.045 with a sphere tip — a lance |
| pierce | a narrow cone: three capsules converging on (0,0.45,0) |
| regrow | a stubby stem with two leaf boxes |
| burrow | a wedge box tilted forward, drill-nosed |
| screen | a fan: four thin boxes rotated 0/25/50/75 degrees about X |
| chill | a crystal: two boxes rotated 45 degrees about Y, one taller |

- [ ] **Step 4: The raiders**, from `Character Bible.dc.html`: Courser "forward-raked diamond, speed lines"; Lash "wedge body, long trailing whip"; Skirmisher "angular wedge, two blade legs". Boxes only, `Blend = 0.02f`, `Raider = true`, one `sk_kit` socket, two or three bones, `SpeciesColours.RaiderBase/RaiderUnder`. Facing +X (they walk toward the Ark).

- [ ] **Step 5: Generate, bake, gate**

```bash
BAKE=1 ./implementation/scripts/generate-creatures.sh
ls client/Assets/UI/Resources/Art/creatures/parts | wc -l          # 144
./implementation/scripts/run-unity-tests.sh EditMode 2>&1 | tail -6
open implementation/results/creature-contact-sheet.png implementation/results/cinderplate.png
```

Expected: 144 part sprites; `SilhouetteTests` green on six — if a pair collides at 40px, the fix is in the recipe (rig proof §6 routes it to the bible only if the recipe already matches the silhouette column); `EveryBody_IsInsideTheTriangleBudget` green (lower `Grid` on any body over 2500). Look at the contact sheet: forty-eight tiles, every part in both sockets on Vetch and Pale. Look at Cinderplate: a teal dome with three coral spikes.

- [ ] **Step 6: The lane, once more**

Open the Editor, play `Wave.unity` standalone: raiders are wedges, not spheres. Close it.

- [ ] **Step 7: Commit**

```bash
git add client/Assets/Creatures client/Assets/UI/Resources/Art client/Assets/UI/Tests/SilhouetteTests.cs
git commit -m "feat(creatures): six species, twelve parts, three raiders

From bible 1.2's silhouette column and the character bible's raider
shapes. Loam's socket rides a segment, which answers rig proof item 10
with a stated rule. The bake writes the forty-eight-combination sheet
and Cinderplate for the eyes-on pass.

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

## Task 16: Roster, splice chamber, splice reveal

Design §4.2 rows 3–5. The two screens the game asks the player to care about an animal on, plus the list they come from.

**Files:**
- Modify: `client/Assets/UI/Screens/RosterView.cs` + resources, `SpliceChamberView.cs` + resources, `SpliceRevealView.cs` + resources; `client/Assets/UI/SpliceScreen.cs`, `FirstHourScreens.cs` (copy); `client/Assets/Game/Ftue/FtueDirector.cs` (`SpliceAsync` shows the studio for the predicted hybrid and the reveal); `client/Assets/Editor/ScreenFixtures.cs`
- Test: `client/Assets/UI/Tests/ScreenBindingTests.cs`, `SpliceConfirmTests.cs` (untouched, must stay green), `FirstHourScreensTests.cs`

**Interfaces:**
- `SpliceChamberView.Bind(SpliceScreenModel m, ISet<Guid> lockedOut, Action onSplice, Texture predicted = null)`
- `SpliceRevealView.Bind(SpliceCommitResponse committed, CreatureDto parentA, CreatureDto parentB, ..., Texture portrait = null)` — the existing parameters unchanged plus the trailing texture.
- `SpliceScreen.Eyebrow = "Gene Lab"`, `Title` stays `"Splice Chamber"`; `SpliceScreen.PredictedHeading = "Predicted hybrid"`, `InheritanceHeading = "Trait inheritance"`, `LineageHeading = "Lineage"`, `BeginLabel = "Begin Splice"`.

- [ ] **Step 1: Roster, against `Creature Roster.dc.html`**

1. Scaffold eyebrow: `"Hatchery · " + count + " of " + cap + " slots"` (`.t-num` on the numbers via a `RosterScreen.Eyebrow(int, int)` helper); title `"Your hybrids"` becomes `RosterScreen.Title` — check `FtueDirectorTests` and `ScreenBindingTests` for the old title and update the constant in one place.
2. The card grid unchanged in structure; each `CreatureCard` now shows the sprite stack (Task 9), a `GenChip` in the header replacing the bare generation label, and `TraitChip`s under the name replacing the two `TraitPip`s' text (keep the pips' tier semantics: `CreatureLabel.TraitWithTier`).
3. Empty state and footer note unchanged.

- [ ] **Step 2: Splice chamber, against `Splice Chamber.dc.html`**

Compose, top to bottom, inside `scaffold.Content`:

1. Header: eyebrow `Gene Lab`, back pill (`pushed` stays as it is), resource pill `SetResourcePill("icon--charge", m.ChargesRemaining.ToString(), "/5")`.
2. Parents row: two `SectionCard`s side by side (`flex-direction: row; gap` via margins), each with `PARENT A`/`PARENT B` eyebrow, a `GenChip`, a `HeroSlot` bound to the parent, the name on `.t-card-title`, the role line (`CreatureLabel` species role, `--mute`), and two `TraitChip`s. The join control between them: a 44px white circle with `icon--splice` in `--violet`. The existing `OptionRow` pickers are replaced; the `lockedOut` set greys a card with `locked-out` as before.
3. Predicted hybrid panel: a `SectionCard` with `background-image: url(hybrid-ramp.png)`, eyebrow `Predicted hybrid`, `GenChip(gen)` right-aligned, a `HeroSlot(CreatureStage)` when `predicted` is given else a `HeroSlot()` bound to the body-parent, the name `Unnamed Hybrid` on `.t-hero`, the lineage line `"<A> × <B> lineage"`, and two `StatCell`s with trends (`Armor`, `Speed`) from the forecast — where the forecast names no stat, the cells collapse.
4. Inheritance card: `SectionCard(InheritanceHeading)` with one `InheritanceBar` per forecast row (`odds` from the forecast, tag `DOM` above 50% else `REC`, modifier = species colour of the trait's owner), then the `MutationBanner` with the mutation sentence — collapsing when the server names none, exactly as today's `mutation` label does.
5. `LineageStrip` bound to the body-parent's generation chain (`G1`, then the parents' generations, then the child's) with the pedigree note when the server sends one; collapsed when there is no lineage.
6. The destruction notice stays a `.panel-coral` line directly above the CTA.
7. CTA row: `CostCtaRow("icon--charge", "1", BeginLabel, onSplice)`; the coverage warning above it as today.

The confirm flow is untouched: `onSplice` still resumes the director's turn, and `SpliceConfirmTests` stays green.

In `FtueDirector.SpliceAsync`: before showing the chamber, `var predicted = _studio?.Show(bodyParent.Species, model.Forecast?.Trait1 ?? parentA.Trait1, model.Forecast?.Trait2 ?? parentB.Trait2, 0.3f)`; clear after the turn.

- [ ] **Step 3: Splice reveal, against `Splice Reveal.dc.html`**

1. Eyebrow `Splice complete · charge spent`, headline `A new line begins`.
2. The hero card (`.elev-2`, `reveal-flare` kept) holds a `HeroSlot(CreatureStage)` with the child's portrait when given, the `MUTATION ROLLED · <trait>` pill when `MutatedUssClassName` applies, the child's name on `.t-hero`, and the `Gen N · A × B` line.
3. Inherited traits card: three rows (`From <A>`, `From <B>`, `Mutation`) with `TraitChip`s; the note in `.panel-amber` for a mutation, `.panel-violet` otherwise.
4. CTA `See the lineage` unchanged.

In `FtueDirector.SpliceAsync` after the commit: `_studio?.Show(child.Species, child.Trait1, child.Trait2, 0f)` handed to the reveal; cleared after.

- [ ] **Step 4: Tests**

`ScreenBindingTests`: the chamber shows two `HeroSlot`s, one `InheritanceBar` per forecast row, the banner collapses on an empty mutation, the cost row's button carries `BeginLabel`; the reveal shows a `CreatureStage` when given a texture. All existing assertions stay green.

- [ ] **Step 5: Gates and captures**

```bash
./implementation/scripts/run-unity-tests.sh EditMode 2>&1 | tail -4
bash implementation/scripts/check-stylesheets.sh | tail -2 && bash implementation/scripts/verify-uss-tokens.sh | tail -2
bash implementation/scripts/capture-screens.sh 2>&1 | tail -3
open implementation/results/screens/SpliceChamberView.png specs/Designs/shots/splice-chamber.png
```

Side by side. Iterate on spacing until the two read as the same screen.

- [ ] **Step 6: Commit**

```bash
git add client/Assets/UI client/Assets/Game/Ftue/FtueDirector.cs client/Assets/Editor/ScreenFixtures.cs
git commit -m "feat(ui): roster, splice chamber and splice reveal to the handoff

Hero slots and chips for the parents, the predicted hybrid turning in
the studio with stat cells and trends, inheritance bars with odds tags,
the mutation banner, the lineage strip, the cost-plus-CTA row. The
reveal's hero card carries the child's portrait. The confirm flow is
untouched.

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

## Task 17: The lane stage and the deploy screen

Design §3.8 (lane stage), §4.2 row 6. The deploy card shows the real lane with the selected creatures standing in their pockets, rendered by `LaneStage` to a texture the `LanePreviewCard` shows. Tapping a `FieldSlotRow` selects or deselects a creature; pockets stay assigned by selection order, which `DeployScreenModel` owns and the protocol depends on.

**Files:**
- Create: `client/Assets/Game/Shell/LaneStage.cs`
- Modify: `client/Assets/UI/Screens/DeployView.cs` + resources, `client/Assets/UI/DeployScreen.cs` (copy constants), `client/Assets/Game/Ftue/FtueDirector.cs` (`FightAsync` builds the stage's picture and rebuilds it on selection change), `client/Assets/Editor/ScreenFixtures.cs`, `client/Assets/Editor/WaveSceneBuilder.cs` (nothing; the stage reuses `LaneDressing`)
- Test: `client/Assets/UI/Tests/WaveScreensTests.cs` (deploy), PlayMode `LaneStagePlayTests.cs`

**Interfaces:**

```csharp
public sealed class LaneStage : MonoBehaviour   // Broodline.Game.Shell
{
    const int Width = 720, Height = 480;
    static LaneStage Create(Transform host);
    Texture Show(int waveId, IReadOnlyList<CreatureLook> placed, IReadOnlyList<int> pockets);   // rebuilds the picture; placed[i] stands at pocket pockets[i]
    void Clear();
}
public void DeployView.Bind(DeployScreenModel m, Action onStart, Action onBack = null, Texture lane = null, IReadOnlyList<CreatureDto> roster = null, Action<Guid> onToggle = null)
public const string DeployScreen.Eyebrow = "Hollow Reach · defense";   // `WaveTitle(int)` = "Wave N", IncomingHeading = "Incoming wave N", FieldHeading = "On the field", FieldHint = "tap a row to place"
```

- [ ] **Step 1: The failing tests**

`WaveScreensTests.cs`: the deploy view shows a `LanePreviewCard` whose texture is the one passed; shows one `FieldSlotRow` per roster creature with the filled ones first; the incoming-wave `SectionCard` has three `StatCell`s (`Foes`, `Deployed`, `Reward`); the blocker still collapses; Start still carries `DeployScreen.Cta`.

`LaneStagePlayTests.cs`: `Show(1, [vetch look], [0])` produces a non-blank texture within five frames (the shape of `PortraitStudioPlayTests`).

- [ ] **Step 2: `LaneStage`**

The `PortraitStudio` shape at `(0, -800, 0)` on the `Studio` layer: a camera framed like `WaveSceneBuilder`'s but aimed at a 720×480 texture from a lower three-quarter angle so the pockets read as a row (`orthographic`, `orthographicSize` 5.5, positioned at `(laneMid, 9, -7)` looking at `(laneMid, 0, 0.5)`), a directional light, `LaneDressing.Build(...)` once, and on `Show` the creatures assembled and placed at `WaveDef.ForId(waveId).Lane.PocketTiles[pocket] * WaveView.TileSize` with z `WaveView.PocketOffset`, turned to face the lane. The camera is enabled only while shown.

- [ ] **Step 3: The deploy view, against `Wave Defense.dc.html` in `placing`**

1. Scaffold: eyebrow `DeployScreen.Eyebrow`, title `Wave N` with `N` on `.t-num` (a two-label title: the scaffold's title label plus a `.t-num` sibling), the speed pill omitted here (it belongs to the fight).
2. Two stat pills in a row: `Gene energy` and `Ark integrity` as `SectionCard`s with an icon dot and a `.t-num` value — energy from the snapshot's balances (`shards`), integrity `100%` before a wave.
3. The `LanePreviewCard` with the stage's texture and slot tags `A`–`D` (filled when a pocket is taken).
4. Incoming-wave card: eyebrow `Incoming wave N`, the tag `STANDARD` (`--coral-text`, right-aligned), the sentence from `WaveDefeatScreen`'s diagnosis vocabulary or `"Raiders are coming down the lane."` when nothing is authored, three `StatCell`s (`Foes` = the wave's spawn count from `WaveDef.ForId(n).Spawns.Length`, passed in as an int by the director; `Deployed` = `slots/cap`; `Reward` = the wave's reward from the snapshot).
5. On the field: `SectionCard(FieldHeading)` with the hint right-aligned; one `FieldSlotRow` per roster creature, letter `A`… by roster order, filled when selected, `Selected` while tapped; `onToggle(creatureId)`.
6. Blocker line, then the CTA row with `Start` — unchanged callback.
7. Stylesheet shrinks; `OptionRow` leaves this screen.

- [ ] **Step 4: The director**

`FightAsync` keeps its selection rule for the initial selection. It now builds the deploy model, shows the stage's texture for the selected creatures, and on `onToggle` rebuilds the selection (respecting `WantedFor`'s floor and `DeployScreen.Cap`), the model, the stage's picture, and re-binds the view — without ending the turn. Start ends the turn as before. After the turn, `_stage.Clear()`.

- [ ] **Step 5: Gates, PlayMode, captures**

```bash
./implementation/scripts/run-unity-tests.sh EditMode 2>&1 | tail -4
bash implementation/scripts/check-stylesheets.sh | tail -2 && bash implementation/scripts/verify-uss-tokens.sh | tail -2
bash implementation/scripts/capture-screens.sh 2>&1 | tail -3
open implementation/results/screens/DeployView.png specs/Designs/shots/wave-defense.png
```

Then, in the Editor: run `LaneStagePlayTests`; play `Boot.unity` against the local stack and reach the deploy screen — the lane card shows the real creatures in their pockets. Record in `phase9-blockers.txt`.

- [ ] **Step 6: Commit**

```bash
git add client/Assets/Game/Shell/LaneStage.cs* client/Assets/UI client/Assets/Game/Ftue/FtueDirector.cs \
        client/Assets/Editor/ScreenFixtures.cs client/Assets/Game/Tests/PlayMode/LaneStagePlayTests.cs*
git commit -m "feat(ui): the deploy screen shows the real lane, with creatures in their pockets

A lane stage renders the dressing and the assembled creatures to a
texture the lane card shows; field rows select and deselect; pockets
stay assigned by selection order, which the protocol depends on.

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

## Task 18: The fight's chrome, post-wave, and defeat

Design §4.2 rows 7–9. The HUD gets the handoff's chrome over the full-screen battlefield; post-wave and defeat get their cards; a loss fixture exists for the first time.

**Files:**
- Modify: `client/Assets/UI/Screens/WaveHudView.cs` + resources, `PostWaveView.cs` + resources, `WaveDefeatView.cs` + resources, `client/Assets/UI/WaveScreens.cs` (copy), `client/Assets/Editor/ScreenFixtures.cs` (a `WaveDefeat` fixture with a loss, a `PostWave` fixture with the loss case too)
- Test: `client/Assets/UI/Tests/WaveScreensTests.cs`

**Interfaces:**
- `WaveHudView.Bind(Func<HudSnapshot> read)` unchanged. `HudSnapshot` gains nothing; the chrome reads `WaveId`, `Integrity`, and the tick it already carries.
- `WaveHudScreen.Eyebrow = "Hollow Reach · defense"`, `SpeedLabel(int) = "1×"/"2×"`.

- [ ] **Step 1: The HUD, against `Wave Defense.dc.html` in `running`**

1. A top band over the scrim (`--hud-scrim` stays): pause pill left (`icon--pause`, no handler yet — the runner has no pause; the pill is inert and says so in a comment), eyebrow + `Wave N / 12` (`.t-num` on both numbers), speed pill right (`1×`, inert for the same reason).
2. Two stat pills under it: `Gene energy` (from the snapshot the director hands the runner — `HudSnapshot` already carries what the bars need; energy is not in it, so this pill shows the wave's reward as `+150 on a win` rather than a balance, and the comment says why), `Ark integrity` with the existing integrity readout moved into it.
3. The bars, tags and Rally countdown are unchanged; they are pooled and must stay so.
4. `WaveHudView.uss`: raw values were tokenised in Phase 8; keep it that way.

- [ ] **Step 2: Post-wave, against the `won` phase**

1. Eyebrow `Hollow Reach · wave N of 12`, headline `Wave held` on `.t-hero` in `--green-text` for a win (the verdict toggle stays).
2. A result card with three `StatCell`s: `Reward`, `Integrity left`, `Kept` (creatures alive — from the response's breach count against the deployment count, passed in).
3. Grants as `CreatureCard`s in a row, as today.
4. `Continue` unchanged.

- [ ] **Step 3: Defeat, against `Wave Defeat.dc.html`**

1. Eyebrow `Hollow Reach · wave N of 12`, headline `Ark breached` on `.t-hero` in `--coral-text`, the sentence from `WaveDefeatScreen`'s diagnosis.
2. Three `StatCell`s: `Leaked` (breaches), `Integrity` (`0%`), `Kept`.
3. A `Defender performance` card is out of scope (the report carries no per-creature damage); instead the diagnosis card names **which raider broke through and which trait answers it** with a `TraitChip` for the counter — the handoff's teaching sentence, from `report.Breaches[0].RaiderType` and `.Counter`.
4. `Retry — free` unchanged.

- [ ] **Step 4: The loss fixture**

In `ScreenFixtures`, `WaveDefeat()` binds a `WaveReport` with `Result = "Loss"`, one breach `{ RaiderType = "Courser", Counter = "Chill" }`, and a granted Pale — the wave-6 designed loss. `PostWave()` keeps the win. Add `"WaveDefeatLoss"` only if the existing fixture is a win; otherwise amend it.

- [ ] **Step 5: Tests, gates, captures**

`WaveScreensTests`: the HUD's top band shows `Wave N`; post-wave shows three stat cells; defeat names the counter in a `TraitChip`; the loss headline is coral. Then:

```bash
./implementation/scripts/run-unity-tests.sh EditMode 2>&1 | tail -4
bash implementation/scripts/check-stylesheets.sh | tail -2 && bash implementation/scripts/verify-uss-tokens.sh | tail -2
bash implementation/scripts/capture-screens.sh 2>&1 | tail -3
```

Then, in the Editor, `WaveCapturePlayTests` again (the HUD changed; the capture must not have), and a wave played by eye with the shell hidden and the chrome over the lane. Record in `phase9-blockers.txt`.

- [ ] **Step 6: Commit**

```bash
git add client/Assets/UI client/Assets/Editor/ScreenFixtures.cs
git commit -m "feat(ui): the fight's chrome, post-wave and defeat to the handoff

The HUD's top band and stat pills over the battlefield; verdict cards
with stat cells; the defeat screen names the raider that broke through
and the trait that answers it. A loss fixture exists for the first time.

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

## Task 19: The consistency pass — Codex and Lineage

Design §4.3. Neither has a handoff screen and neither was reached in the Phase 8 walk. They get the same header, chips, cards and spacing so nothing looks like a different app. No new layout.

**Files:**
- Modify: `client/Assets/UI/Screens/CodexSheet.cs` + resources, `LineageView.cs` + resources
- Test: `client/Assets/UI/Tests/FirstHourScreensTests.cs` (existing assertions stay green)

- [ ] **Step 1: Codex** — entries become `SectionCard`s with a `TraitChip` and the counter sentence; the dismiss button becomes `btn-secondary`; the sheet's top edge takes `--radius-card` like `AbandonedWaveSheet`.
- [ ] **Step 2: Lineage** — the scaffold gets eyebrow `Pedigree`; nodes keep their marks (Founder / Mutated / Consumed in words — bible §10.4) and gain the species tint from `TraitChip`'s six rules; the two `StatCell`s (`In the record`, `Living`) unchanged; `Continue` unchanged.
- [ ] **Step 3: Gates and captures** — the three scripts, `capture-screens.sh`, look at both.
- [ ] **Step 4: Commit**

```bash
git add client/Assets/UI
git commit -m "feat(ui): codex and lineage brought to the vocabulary

Same header, chips, cards and spacing; no new layout. Neither has a
handoff screen and neither was reached in the Phase 8 walk.

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

## Task 20: The corpus, the baseline, and the smoke loop, once more

Everything visual exists. This task measures it before a human looks.

- [ ] **Step 1: Every gate, from a closed Editor**

```bash
./implementation/scripts/run-unity-tests.sh EditMode 2>&1 | tail -4 | tee implementation/results/phase9-test-baseline.txt
bash implementation/scripts/check-stylesheets.sh | tail -2
bash implementation/scripts/verify-uss-tokens.sh | tail -2
bash implementation/scripts/verify-unity-settings.sh | tail -2
./implementation/scripts/generate-contract.sh && git status --porcelain -- openapi/ client/Assets/Generated/
BAKE=1 ./implementation/scripts/generate-creatures.sh && git status --porcelain -- client/Assets/Creatures client/Assets/UI/Resources/Art
bash implementation/scripts/capture-screens.sh 2>&1 | tail -3
```

Expected: EditMode `failed=1` and only `MainThreadAffinityTests`; every script green; the two `git status` lines empty (no drift).

- [ ] **Step 2: PlayMode, in the Editor** — `WaveCapturePlayTests`, `PortraitStudioPlayTests`, `LaneStagePlayTests`; counts into `phase9-blockers.txt`.

- [ ] **Step 3: The deployed stack**

```bash
bash implementation/scripts/smoke-loop.sh 2>&1 | tail -6     # PASS, abandon step included
```

- [ ] **Step 4: Commit the baseline**

```bash
git add implementation/results/phase9-test-baseline.txt
git add -f implementation/results/phase9-blockers.txt
git commit -m "test(record): the Phase 9 baseline before the walk

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

## Task 21: The exit gate — the walk, then TestFlight

Design §8, in its words. **A human does this.** The checklist first, then the walk, then — on a yes — the archive.

- [ ] **Step 1: The checklist**, each line answered with evidence in `implementation/results/phase9-visual-review.md` (tracked with `-f`):

1. Both blockers fixed, gated, and seen in a running wave (`phase9-blockers.txt`).
2. Nine screens at handoff fidelity, each capture beside its shot; Codex and Lineage consistent.
3. Six species and three raiders on the lane; layered sprites on every card; Cinderplate from the pipeline; the forty-eight-combination sheet rendered.
4. Smoke loop green against the deployed stack, abandoned-wave step included.
5. EditMode baseline held, in `phase9-test-baseline.txt`.

- [ ] **Step 2: The walk.** `Boot.unity` against the deployed stack, phone-sized viewport, the first hour, the same walk as Phase 8. Every screen reached gets a row: reached, verdict in the developer's words. Motion and the HUD in a running wave are judged here. The verdict is quoted, not paraphrased, and the file ends with `GO` or `NO: <the named gap>`. On `NO`, the gap becomes a fix-up task and the walk repeats.

- [ ] **Step 3: TestFlight**, on `GO`. Phase 8 Task 18's procedure, unchanged: iOS `buildNumber` moves `1 → 2` in `BootBuilder.ReleaseBuildNumber` (`bundleVersion` stays `0.3.0`), then:

```bash
export BROODLINE_API_URL="$(terraform -chdir=infra/terraform output -raw api_url)"
"/Applications/Unity/Hub/Editor/6000.6.0f1/Unity.app/Contents/MacOS/Unity" -batchmode -quit \
  -projectPath "$(pwd)/client" -executeMethod BootBuilder.BuildIOS -logFile implementation/results/ios-build.log
cd build/ios-release
xcodebuild -project Unity-iPhone.xcodeproj -scheme Unity-iPhone -configuration Release \
  -archivePath ./Broodline.xcarchive archive -allowProvisioningUpdates
xcodebuild -exportArchive -archivePath ./Broodline.xcarchive -exportPath ./export \
  -exportOptionsPlist ../../client/Assets/Editor/ExportOptions.plist -allowProvisioningUpdates
xcrun altool --upload-app -f export/Broodline.ipa -t ios --apiKey "$ASC_KEY_ID" --apiIssuer "$ASC_ISSUER_ID"
```

Then App Store Connect → TestFlight → the build → an **internal** group. No Beta App Review.

- [ ] **Step 4: Someone who is not the developer plays the first hour.** They report: did they reach the Lineage View; where they were confused; how long it took. **That report goes into the Phase 9 record verbatim** (Task 22).

- [ ] **Step 5: Commit**

```bash
git add -f implementation/results/phase9-visual-review.md
git add client/Assets/Editor/BootBuilder.cs
git commit -m "docs(record): the Phase 9 walk, and the build that went to TestFlight

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

## Task 22: The record, and the errata

**Files:**
- Create: `implementation/2026-09-18-phase9-followups.md`
- Modify: `specs/plans/broodline_phase9_the_look.md` (§11 Errata), `specs/broodline_bible.md` §10.3 (a one-line note that the pipeline exists and where), `specs/broodline_rig_proof.md` §9 (item 10's answer for Loam: the part rides one segment)

- [ ] **Step 1: The follow-ups document**, in the shape of `2026-09-17-phase8-followups.md`: what closed, what is owed (resume of an abandoned wave; the eight-raider roster beyond three; Instinct cues; an artist commission's entry point; the inert pause and speed pills; the HUD's energy pill showing reward rather than balance), defects found in this plan, the tester's report verbatim.

- [ ] **Step 2: The errata in the design**: §3.4's 1500 → 2500 and why; §2.1's "before the director runs" → "inside the director, after the roster loads"; §2.2's proof runs from the Editor's Test Runner; the `Studio` layer.

- [ ] **Step 3: Commit**

```bash
git add implementation/2026-09-18-phase9-followups.md specs/plans/broodline_phase9_the_look.md specs/broodline_bible.md specs/broodline_rig_proof.md
git commit -m "docs(record): Phase 9 follow-ups, and the design's errata

Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

## Definition of done

The design's §1.1, item by item, each with the task that meets it: 1 (Tasks 1–3, 5), 2 (Task 6), 3 (Task 4), 4 (Tasks 7, 15), 5 (Tasks 9, 15), 6 (Task 10), 7 (Tasks 11, 14, 16), 8 (Tasks 13–19), 9 (Task 15), 10 (Task 21).

## What this plan deliberately does not do

- It does not resume an abandoned wave, reopen the Region map, commission an artist, add Instinct cues, or add raiders the engine does not carry.
- It does not make the pause or speed pills work; the runner has no pause and the plan does not add one.
- It does not touch `engine/`.
