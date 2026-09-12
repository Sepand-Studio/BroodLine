# Phase 4 — Backend Spine Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A cold start fetches a real player from a real server in one call, and a repeated account creation carrying the same idempotency key grants exactly once.

**Architecture:** The phase opens with one engine task — `SimVersion` gains a bump policy that the corpus baseline enforces, closing Phase 3's owed decision before anything new depends on versioning. Everything after it is the TypeScript half of `solo_execution` §7's layout, which does not exist at all today: a pnpm workspace, `services/api` on Hono, a Drizzle schema whose isolation is Row-Level Security, and a ledger that every currency mutation writes in the same transaction as the balance. Two adversarial suites are the gate. The config bundle pipeline validates game rules by shelling out to a C# CLI over the engine rather than reimplementing them, and the contract crosses the language boundary by generation in one direction only.

**Tech Stack:** Node 22 LTS, pnpm workspaces, TypeScript, Hono, Drizzle ORM + drizzle-kit, `pg`, Zod + `@asteasolutions/zod-to-openapi`, Vitest, Testcontainers, Postgres 16, Terraform, Cloud Run, Cloud SQL, GCS, NSwag. On the C# side: the existing engine at `netstandard2.1`, tests at `net10.0`, xUnit.

## Global Constraints

**The engine constraints from Phases 1–3 still bind `engine/`** and are still build failures rather than conventions — no floating point, no `System.Math`, no `System.Linq`, no `Dictionary`, no `HashSet`, no `System.IO`, zero project references, the Cecil float scan, `BannedSymbols.txt`. **Task 1 is the only task that touches `engine/`, and Task 7 references it without modifying it.** Nothing in `services/` is subject to any of them.

From `specs/plans/broodline_phase4_backend_spine.md`, and normative here:

- **`server_id` leads every primary key and index on every server-scoped table.** No globally meaningful IDs — a merge must be a re-keying exercise (§4).
- **Row-Level Security is on, and the setting is transaction-scoped.** `set_config('app.server_id', $1, true)` — the third argument is load-bearing (§4).
- **Every currency mutation writes a ledger row in the same transaction as the balance update.** Balances are never derived by summing the ledger at read time (§5).
- **Every mutating request carries an idempotency key, inserted as the first statement inside the mutation's transaction** (§5).
- **A matching key with a differing `request_hash` returns `422`**, never another request's response (§5).
- **No game rule is implemented in TypeScript.** The publish path validates waves by invoking the engine, not by reimplementing it (§2.3).
- **Generated API clients are committed and never hand-edited.** CI regenerates and fails on a non-empty diff (§7).
- **A config bundle that fails publish-time validation is not published**, and a published version is never overwritten (§3.3).
- **A schema migration and the code requiring it never deploy together.** Expand, deploy, migrate, contract (§4).
- **No gameplay traffic touches Vercel.** Not a proxy, not an edge cache, not one endpoint.

### Values, copied verbatim

| | |
|---|---|
| Region, milestone 1 | `us-central1` — one server, one region |
| Postgres | **16**, Cloud SQL, smallest tier, automated backups and PITR from day one |
| Idempotency key retention | **24 hours**, then aged out |
| Tick slot storage | **Minute granularity**, not hour — `solo_execution` §9.7 |
| Server assignment | By storefront region at signup, **immutable**. No transfers, ever |
| Offline harvest cap | 12 hours — not exercised in this phase, but the column exists |
| Access token | Short-lived JWT. Refresh token in the iOS Keychain, never in a file |
| Error envelope | `{ code, message, details? }` — the client switches on `code`, never on message text |
| SLO, `GET /v1/sync` | p99 under 300 ms — it is on every cold start |
| Engine version today | `SimVersion.Value` = `"0.1.0"`, unchanged since Phase 1 |
| Corpus | 500 scenarios — `Corpus.ScenarioCount` |
| Unity | 6000.6.0f1, IL2CPP |
| Apple Developer Team | `R4Z6W7AW86`, automatic signing — already configured, Phase 3 shipped a device build on it |

**This plan writes no `sim` service, no submit-and-verify, no nodes, regions, harvest or splice, no second deployable, no Redis, no PgBouncer, no HA, no push, no `Broodline.UI`, no Codex sheet, no outbox, no LiveOps console and no real art.** Those are named at the design doc §9.

---

## One correction this plan feeds back into the design

Writing Task 1 found a claim in the design that would not survive contact, and it inverts the mechanism rather than refining it.

**A header that records the version is not a policy that enforces one.** Design §3.1 says the baseline "gains a header carrying the `SimVersion` it was generated under, and a test asserts the pairing." But `emit-corpus-baseline.sh` regenerates the whole file from the current engine, so the header it writes is *always* the current `SimVersion` — a re-baseline under an unbumped version produces a file that agrees with itself, and the test passes. The enforcement has to live where the deliberate act happens.

> **Corrected: the guard is in the emitter, not the test.** `emit-corpus-baseline.sh` compares the new hashes against the committed ones and **refuses to re-baseline when the hashes changed and `SimVersion` did not**, restoring the file and naming the fix. The test keeps a narrower job — asserting the committed header names the current version — which catches a bump that forgot to re-run the emitter.

The useful side effect is that the emitter now *reports* whether a bump was behavioural: run it after bumping and "unchanged: the engine already reproduces it" is proof the release changed no output. The design doc's §3.1 is corrected to match — it is this phase's own document and not yet merged.

---

## File structure

### New — the TypeScript half of `solo_execution` §7

| File | Responsibility |
|---|---|
| `pnpm-workspace.yaml` | Declares `services/*` as workspace packages |
| `package.json` (root) | Node engine floor, the scripts every task runs |
| `services/api/src/index.ts` | Hono app, route registration, the Cloud Run entrypoint |
| `services/api/src/db/client.ts` | The `pg` pool, the Drizzle handle, and `withServer()` — the only way a handler touches a server-scoped table |
| `services/api/src/db/schema.ts` | All seven tables, in Drizzle |
| `services/api/src/db/migrate.ts` | Applies committed migrations; the only writer of DDL |
| `services/api/src/money/ledger.ts` | `credit()` — the single function that writes a currency mutation |
| `services/api/src/money/idempotency.ts` | `withIdempotency()` — key insert, replay, `422` on hash mismatch |
| `services/api/src/money/invariant.ts` | The ledger-versus-wallet drift check, run as a test and as a job |
| `services/api/src/identity/apple.ts` | Apple token verification, guest creation, the `sub` binding |
| `services/api/src/identity/jwt.ts` | Access token issue and verify, refresh rotation |
| `services/api/src/routes/account.ts` | `POST /v1/account` — the starter grant |
| `services/api/src/routes/sync.ts` | `GET /v1/sync` |
| `services/api/src/config/validate.ts` | Pack ladder and locale-key checks; invokes the C# CLI for waves |
| `services/api/src/config/publish.ts` | Immutable publish to GCS, and rollback by naming a version |
| `services/api/src/http/errors.ts` | The `{ code, message, details? }` envelope |
| `services/api/src/openapi.ts` | Emits the OpenAPI 3.1 document from the Zod registry |
| `services/api/test/harness.ts` | Testcontainers Postgres, migrated once per run |
| `services/api/test/isolation.test.ts` | **Gate.** Cross-server leakage |
| `services/api/test/idempotency.test.ts` | **Gate.** Concurrent identical keys |
| `config/bundles/0.1.0/` | The seed bundle — waves and traits |
| `infra/terraform/` | Cloud SQL, Cloud Run, GCS, service accounts |

### New — C# and Unity

| File | Responsibility |
|---|---|
| `tools/config-validate/Broodline.Config.Validate.csproj` | A CLI over the engine's wave rules. References `Broodline.Sim`, owns its own JSON |
| `tools/config-validate/Program.cs` | Reads a bundle directory, constructs each `WaveDef`, calls `Validate()`, reports |
| `client/Assets/Generated/Api/` | NSwag output. Committed, never hand-edited |
| `client/Assets/Model/` | `Broodline.Model` — the `/v1/sync` snapshot, cached |
| `client/Assets/Net/` | `Broodline.Net` — HTTP, auth, the cold-start call |

### Modified

| File | Change |
|---|---|
| `engine/Runtime/SimVersion.cs` | The policy, as a doc comment that says what forces a bump |
| `tests/engine/CorpusBaselineTests.cs` | Header round-trip; the header-names-current-version assertion |
| `tests/engine/corpus-baseline.txt` | Gains a header line. **500 hashes unchanged** |
| `implementation/scripts/emit-corpus-baseline.sh` | The refuse-to-re-baseline-without-a-bump guard |
| `Broodline.sln` | Adds `Broodline.Config.Validate` |
| `.github/workflows/tests.yml` | Adds the TypeScript job and the contract-diff gate |
| `.gitignore` | Terraform state, bundle build output |

---

## Task 0: Prerequisites, and the setting that makes every gate real

Phase 4 builds on the merged Phase 3. Confirm it is green — and fix the repository setting that has kept every committed workflow from ever running.

**Files:**
- Modify: none. This is a gate and one repository setting.

**Interfaces:**
- Consumes: the Phase 3 client and engine on `develop`.
- Produces: a CI system that can actually execute.

- [ ] **Step 1: Confirm the branch and the toolchain**

```bash
git rev-parse --abbrev-ref HEAD && ./implementation/scripts/verify-prereqs.sh; echo "exit=$?"
```

Expected: `phase_4`, the prereq script's `ok` lines, `exit=0`.

- [ ] **Step 2: Confirm the suite is green and record the count**

```bash
dotnet test Broodline.sln --nologo
```

Expected: all pass, **`Skipped: 0`**. Write the total down — later tasks add to it, and a drop means something was deleted rather than extended.

A skip here is a failure. Phase 3's `DeviceReplayTests` skip themselves when the device artifact is absent, and the artifact is tracked, so a skip means it went missing.

- [ ] **Step 3: Confirm the cross-runtime gate passes**

```bash
./implementation/scripts/cross-runtime-diff.sh
```

Expected: `PASS: 500 scenarios agree`, exit 0. Close the Unity editor first.

**If this fails, stop.** Task 1 rewrites the baseline file, and starting from a red gate means never knowing which change broke it.

- [ ] **Step 4: Establish that Actions is disabled**

```bash
gh api repos/Sepand-Studio/BroodLine/actions/permissions
```

Expected: `{"enabled":false,...}`.

This is why `tests.yml` and `determinism.yml` have never run despite both being committed. Every gate this plan adds is decorative until it changes.

- [ ] **Step 5: Enable Actions**

```bash
gh api -X PUT repos/Sepand-Studio/BroodLine/actions/permissions -F enabled=true -f allowed_actions=all
```

**`-F` for `enabled`, not `-f`.** The API wants a JSON boolean; `-f` sends the string `"true"` and the request is rejected. `allowed_actions` really is a string, so it keeps `-f`.

Then confirm:

```bash
gh api repos/Sepand-Studio/BroodLine/actions/permissions
```

Expected: `{"enabled":true,...}`.

- [ ] **Step 6: Prove a workflow now runs**

Push the branch and watch the first run of the already-committed `tests.yml`:

```bash
git push -u origin phase_4 && gh run list --branch phase_4 --limit 3
```

Expected: a run appears. It may fail — `tests.yml` has never executed and has therefore never been debugged. **A failing run is a pass for this step; a run that does not appear is not.** Fix any failure here, before new code can be blamed for it.

- [ ] **Step 7: Commit whatever Step 6 needed**

If `tests.yml` needed fixing, commit it. If it passed untouched, skip this step.

```bash
git add .github/workflows/tests.yml
git commit -m "ci: the first workflow run this repository has ever had

Actions was disabled at the repository level, so tests.yml and
determinism.yml were both committed and both had never executed. Phase 3
corrected the header that misdiagnosed this as a missing self-hosted
runner; this flips the setting itself.

Whatever this commit changes in tests.yml is the cost of a workflow that
had never been run against reality."
```

**Registering the self-hosted macOS runner is deliberately not in scope.** It gates the IL2CPP comparison and the Unity EditMode suite, which this phase does not touch, and it is real setup work on the developer's machine rather than a toggle.

---

## Task 1: The `SimVersion` policy, enforced by the corpus baseline

**This closes Phase 3's owed decision, and it is the only task in this plan that touches `engine/`.**

`SimVersion.Value` has read `"0.1.0"` since Phase 1. Phase 3's Task 4 moved every hash in the project and did not bump it. Nothing reads it except the replay's `EngineVersion` field, which means `solo_execution` §9.4 — *"a replay recorded under a superseded engine version renders its stored outcome and is not re-simulated"* — has never been armed: every replay ever written claims the current version, so every replay would be silently re-simulated.

The policy needs a mechanical enforcer, and Phase 3 already built the detector. The corpus baseline is reproduced byte-for-byte by every run, so **a change that requires re-baselining is exactly a change that requires a bump.** The two sets are identical by construction.

**Files:**
- Modify: `engine/Runtime/SimVersion.cs`
- Modify: `tests/engine/CorpusBaselineTests.cs`
- Modify: `tests/engine/corpus-baseline.txt` (regenerated — header added, 500 hashes unchanged)
- Modify: `implementation/scripts/emit-corpus-baseline.sh`

**Interfaces:**
- Consumes: `SimVersion.Value` → `string`; `Corpus.ScenarioCount` = `500`; `Corpus.RunScenario(int)` → `ulong`; `TestPaths.ProjectDir()` → `string`.
- Produces: `corpus-baseline.txt` with a first line of exactly `# simversion <value>`, followed by 500 `index hash` lines.

- [ ] **Step 1: Write the policy into `SimVersion.cs`**

`engine/Runtime/SimVersion.cs` — replace the file:

```csharp
namespace Broodline.Sim
{
    /// The engine's behavioural identity.
    ///
    /// WHAT FORCES A BUMP: any change that alters engine output for any input.
    /// That set is not a judgment call - it is exactly the set of changes that
    /// fail to reproduce tests/engine/corpus-baseline.txt, so the baseline IS
    /// the detector and emit-corpus-baseline.sh refuses to re-baseline without
    /// a bump.
    ///
    /// WHAT READS IT: Replay.EngineVersion, and nothing else. A replay whose
    /// stored version differs from this renders its recorded outcome and is
    /// never re-simulated - solo_execution section 9.4. That rule was inert
    /// until this constant started moving.
    ///
    /// NOT SEMANTIC. The only comparison anyone performs is equality, so the
    /// string's shape is a readability choice and nothing parses its parts.
    ///
    /// A bump WITHOUT a behaviour change is legal - a deliberate marker for a
    /// release. Run the emitter afterwards; it will report the baseline as
    /// unchanged, which is the proof the release was non-behavioural.
    public static class SimVersion
    {
        public const string Value = "0.2.0";
    }
}
```

**`0.2.0`, not `0.1.0`.** Phase 3's Task 4 changed engine output and did not bump; this is that bump, taken late and taken once. Phase 3's tracked device and Editor replay artifacts were recorded under `0.1.0` and **will now read as superseded**, which is correct and is the rule working — they record a pre-bump engine.

- [ ] **Step 2: Write the failing test for the header**

`tests/engine/CorpusBaselineTests.cs` — add inside the class:

```csharp
        [Fact]
        public void TheBaselineHeaderNamesTheCurrentEngineVersion()
        {
            // The emitter writes this line from SimVersion.Value, and refuses
            // to re-baseline when the hashes moved and the version did not.
            // This assertion catches the other direction: a bump that never
            // re-ran the emitter, leaving the file claiming an older engine.
            var lines = File.ReadAllLines(BaselinePath);
            Assert.True(lines.Length > 0, "corpus-baseline.txt is empty.");
            Assert.Equal("# simversion " + SimVersion.Value, lines[0]);
        }
```

- [ ] **Step 3: Run it to verify it fails**

```bash
dotnet test Broodline.sln --nologo --filter "FullyQualifiedName~TheBaselineHeaderNamesTheCurrentEngineVersion"
```

Expected: **FAIL.** The committed file's first line is `0 3621896074069225025` — there is no header at all.

- [ ] **Step 4: Teach the renderer and the comparison about the header**

`tests/engine/CorpusBaselineTests.cs` — replace `Render()` and `EveryScenarioMatchesTheCommittedBaseline()`:

```csharp
        private const string HeaderPrefix = "# simversion ";

        private static string Render()
        {
            var sb = new StringBuilder();
            sb.Append(HeaderPrefix).Append(SimVersion.Value).Append('\n');
            for (int i = 0; i < Corpus.ScenarioCount; i++)
                sb.Append(i).Append(' ').Append(Corpus.RunScenario(i)).Append('\n');
            return sb.ToString();
        }

        [Fact]
        public void EveryScenarioMatchesTheCommittedBaseline()
        {
            Assert.True(File.Exists(BaselinePath),
                "corpus-baseline.txt was not copied to the output directory - check the csproj Content item.");

            var lines = File.ReadAllLines(BaselinePath);

            // One header line, then one line per scenario. Asserting the total
            // rather than the body length keeps a truncated file from reading
            // as a header problem.
            Assert.Equal(Corpus.ScenarioCount + 1, lines.Length);
            Assert.StartsWith(HeaderPrefix, lines[0]);

            var drifted = new List<string>();
            for (int i = 0; i < Corpus.ScenarioCount; i++)
            {
                string actual = i + " " + Corpus.RunScenario(i);
                // +1 throughout: line 0 is the header, scenario i is line i+1.
                if (actual != lines[i + 1]) drifted.Add("  line " + (i + 1) + ": expected '" + lines[i + 1] + "', got '" + actual + "'");
                if (drifted.Count == 10) break;
            }

            Assert.True(drifted.Count == 0,
                "The engine no longer reproduces the committed corpus.\n" +
                "If this is an INTENDED behaviour change, BUMP SimVersion.Value first,\n" +
                "then run\n" +
                "  ./implementation/scripts/emit-corpus-baseline.sh\n" +
                "and say in the commit message what changed and why.\n" +
                "First differences:\n" + string.Join("\n", drifted));
        }
```

- [ ] **Step 5: Add the emitter's refusal guard**

`implementation/scripts/emit-corpus-baseline.sh` — after the block that computes `after`/`after_mtime` and **before** the `if [ "$before" = "$after" ]` reporting block, insert:

```bash
# THE POLICY, ENFORCED HERE RATHER THAN IN A TEST.
#
# The emitter regenerates the whole file from the current engine, so the header
# it writes is ALWAYS the current SimVersion. A test comparing the header to
# SimVersion therefore cannot catch a re-baseline under an unbumped version -
# the file agrees with itself. The deliberate act is this script, so the guard
# belongs in this script.
#
# Hashes compared WITHOUT the header, or a bump alone would read as a
# behaviour change and mask the thing being checked.
body_before=$(git show HEAD:"$BASELINE" | grep -v '^# simversion ' | shasum | cut -d' ' -f1)
body_after=$(grep -v '^# simversion ' "$BASELINE" | shasum | cut -d' ' -f1)
ver_before=$(git show HEAD:"$BASELINE" | sed -n 's/^# simversion //p')
ver_after=$(sed -n 's/^# simversion //p' "$BASELINE")

if [ "$body_before" != "$body_after" ] && [ "$ver_before" = "$ver_after" ]; then
  git checkout -- "$BASELINE"
  echo "FAIL: the engine's output changed but SimVersion did not."
  echo ""
  echo "  SimVersion.Value is still '$ver_after'."
  echo ""
  echo "A replay stores the version it was recorded under, and a replay whose"
  echo "version matches the running engine is RE-SIMULATED rather than shown."
  echo "Re-baselining without a bump means old replays silently re-simulate"
  echo "into different outcomes - solo_execution section 9.4 exists to stop"
  echo "exactly that."
  echo ""
  echo "Bump engine/Runtime/SimVersion.cs, then run this script again."
  echo "$BASELINE has been restored."
  exit 1
fi
```

**`git show HEAD:` rather than a copy taken at the top of the script**, because the emitter has already overwritten the working file by this point. The committed version is the only surviving "before".

- [ ] **Step 6: Re-baseline, acquiring the header**

```bash
./implementation/scripts/emit-corpus-baseline.sh
```

Expected: the script runs, and reports the file as **regenerated**.

The guard does not fire: `ver_before` is empty (the committed file has no header) and `ver_after` is `0.2.0`, so the versions differ and the branch is skipped. That is correct — this run is a version change.

- [ ] **Step 7: Prove the regeneration changed only the header**

**This is the step that makes Step 6 safe.** The 500 hashes must be byte-identical; only a line has been added.

```bash
git diff --numstat -- tests/engine/corpus-baseline.txt
```

Expected: `1	0	tests/engine/corpus-baseline.txt` — one line added, zero removed.

```bash
diff <(git show HEAD:tests/engine/corpus-baseline.txt) <(grep -v '^# simversion ' tests/engine/corpus-baseline.txt) && echo "IDENTICAL BODY"
```

Expected: `IDENTICAL BODY`, no diff output.

**If either check disagrees, stop.** A hash moved, which means something in `engine/` changed between Phase 3's merge and now, and that is a different investigation from this task.

- [ ] **Step 8: Run the full suite**

```bash
dotnet test Broodline.sln --nologo
```

Expected: all pass, `Skipped: 0`, **two more tests than Task 0 recorded**.

Phase 3's `DeviceReplayTests.TheDeviceRunWasRecordedByThisEngineVersion` asserts the tracked artifact's version equals `SimVersion.Value`. It was recorded under `0.1.0`, so the bump **breaks it** — and that is the rule working rather than a regression.

- [ ] **Step 9: Correct the test the bump invalidated**

`tests/engine/Combat/DeviceReplayTests.cs` — the assertion is now backwards. The test is **`ReplayArtifactPresenceTests.TheTrackedCapturesAreCurrent`**, which asserts `ReplayArtifact.AreCurrent`. (An earlier draft of this plan named it `DeviceReplayTests.TheDeviceRunWasRecordedByThisEngineVersion`, quoting the Phase 3 *plan* rather than the code — no such member exists.) Replace it with:

```csharp
        [Fact]
        public void TheDeviceRunIsSupersededAndIsNotReSimulated()
        {
            // Recorded under 0.1.0, before Phase 4 bumped the engine to 0.2.0
            // for Phase 3's own unbumped behaviour change. solo_execution 9.4:
            // a replay from a superseded engine shows its stored outcome and
            // is never re-simulated.
            //
            // The round-trip test above still re-simulates this artifact
            // DELIBERATELY - it is the phase's evidence, and comparing it
            // against the engine that recorded it is the whole proof. This
            // test pins the fact that a PRODUCTION reader must not.
            var record = Replay.Deserialize(File.ReadAllBytes(Artifact("device-replay.bin")));
            Assert.NotEqual(SimVersion.Value, record.EngineVersion);
            Assert.Equal("0.1.0", record.EngineVersion);
        }
```

**Read this carefully before accepting it.** It asserts the artifact is superseded, which is a weaker claim than the original. The original asserted the artifact and engine agreed, which was Phase 3's guard against comparing a hash across a balance change. That guard is genuinely gone once the engine moves past the artifact — the honest options are to re-capture the artifact on a device under `0.2.0`, or to pin the exact version it was recorded under, which is what this does. **Re-capturing needs the physical device and is the better answer**; do it at Task 12 if the device is to hand, and replace this test then.

- [ ] **Step 10: Prove the guard actually refuses**

Do not trust a guard that has never fired.

**Two traps here, both found the hard way.**

**Do not perturb `Attacks.cs`'s `damage = damage * 115 / 100`.** Changing 115 to 116 provably moves no hash: integer truncation swallows it at every damage value in the game — at damage 6, `6*115/100` and `6*116/100` are both 6. A probe that changes nothing "proves" the guard fires while proving nothing at all. **Perturb `Stats.cs`'s base `CreatureDamage` instead**, which enters the arithmetic directly.

**The guard reads `git show HEAD:`, so it cannot fire against an uncommitted baseline.** At this point in the task the header-bearing file is still in the working tree, so `ver_before` would be empty and the guard would correctly skip. Commit the Step 6–8 work first, then probe:

```bash
# Perturb base CreatureDamage in engine/Runtime/Combat/Stats.cs by 1, then:
./implementation/scripts/emit-corpus-baseline.sh; echo "exit=$?"
```

Expected: **`FAIL: the engine's output changed but SimVersion did not.`**, `exit=1`, and `git status` showing `corpus-baseline.txt` unmodified.

- [ ] **Step 11: Revert the probe**

```bash
git checkout -- engine/Runtime/Combat/Attacks.cs engine/Runtime/SimVersion.cs
rm -f engine/Runtime/SimVersion.cs.bak
git status --short
```

Expected: only `CorpusBaselineTests.cs`, `corpus-baseline.txt`, `emit-corpus-baseline.sh` and `DeviceReplayTests.cs` modified. **If `SimVersion.cs` reverted to `0.1.0`, re-apply Step 1** — Step 11 reverts it because Step 10 touched it.

- [ ] **Step 12: Run everything and commit**

```bash
dotnet test Broodline.sln --nologo && ./implementation/scripts/cross-runtime-diff.sh
```

Expected: both green.

```bash
git add engine/Runtime/SimVersion.cs tests/engine/CorpusBaselineTests.cs \
        tests/engine/corpus-baseline.txt tests/engine/Combat/DeviceReplayTests.cs \
        implementation/scripts/emit-corpus-baseline.sh
git commit -m "feat: SimVersion gets a bump policy, and the emitter enforces it

SimVersion has read 0.1.0 since Phase 1. Phase 3's Task 4 moved every
hash in the project without bumping it, which left solo_execution 9.4
inert: nothing reads the constant except Replay.EngineVersion, so every
replay ever written claimed the current engine and would have been
silently re-simulated into a different outcome.

The policy needed a mechanical enforcer and Phase 3 had already built
the detector. A change that requires re-baselining the corpus is exactly
a change that requires a bump - the two sets are identical by
construction - so emit-corpus-baseline.sh now refuses to re-baseline
when the hashes moved and the version did not, and restores the file.

The guard is in the emitter rather than in a test, correcting the design
doc. The emitter regenerates the whole file from the current engine, so
the header it writes is always the current SimVersion and a test
comparing the two cannot catch the case: the file agrees with itself.
The test keeps the narrower job of catching a bump that never re-ran the
emitter.

Bumped to 0.2.0 - Phase 3's change, marked late and marked once. The
tracked device replay was recorded under 0.1.0 and now reads as
superseded, which is the rule working. Its version test is rewritten to
pin that rather than to assert an agreement that no longer holds;
re-capturing the artifact on device under 0.2.0 is the better fix and is
noted at Task 12.

Verified by execution: the guard was made to fire by perturbing a damage
constant, and the re-baseline that added the header was proven to change
one line and leave all 500 hashes byte-identical.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

- [ ] **Step 13: Correct the design doc**

`specs/plans/broodline_phase4_backend_spine.md` §3.1 — replace the resolution blockquote with one that puts the guard in the emitter, matching what was built. This document is this phase's own and is not yet merged, so it is corrected rather than carried as debt.

```bash
git add specs/plans/broodline_phase4_backend_spine.md
git commit -m "docs: the SimVersion guard belongs in the emitter, not a test

Writing Task 1 found the design's mechanism could not work. The emitter
regenerates the file from the current engine, so its header is always
the current SimVersion - a re-baseline under an unbumped version
produces a file that agrees with itself and the test passes.

Corrected in place rather than noted beside, since this document is
still in an open PR."
```

---

## Task 2: The TypeScript workspace, and an `api` that answers

Nothing in `services/` exists. This task creates the workspace, one service, and the smallest thing that proves the toolchain runs — before any database, any auth, or any schema is in the picture.

**Files:**
- Create: `pnpm-workspace.yaml`, `package.json`, `.npmrc`
- Create: `services/api/package.json`, `services/api/tsconfig.json`, `services/api/vitest.config.ts`
- Create: `services/api/src/index.ts`, `services/api/src/app.ts`, `services/api/src/http/errors.ts`
- Create: `services/api/test/health.test.ts`
- Modify: `.gitignore`

**Interfaces:**
- Consumes: nothing.
- Produces: `createApp(): Hono` — the app factory every later route registers on, and every test mounts without a network listener. `fail(code, message, details?)` → a `Response` carrying `{ code, message, details? }`.

- [ ] **Step 1: Create the workspace root**

`pnpm-workspace.yaml`:

```yaml
packages:
  - 'services/*'
```

`package.json`:

```json
{
  "name": "broodline",
  "private": true,
  "engines": {
    "node": ">=22.0.0"
  },
  "packageManager": "pnpm@9.12.0",
  "scripts": {
    "test": "pnpm -r test",
    "typecheck": "pnpm -r typecheck",
    "openapi": "pnpm --filter @broodline/api openapi"
  }
}
```

`.npmrc`:

```
engine-strict=true
```

- [ ] **Step 2: Create the `api` package**

`services/api/package.json`:

```json
{
  "name": "@broodline/api",
  "private": true,
  "type": "module",
  "scripts": {
    "dev": "node --watch --experimental-strip-types src/index.ts",
    "test": "vitest run",
    "typecheck": "tsc --noEmit"
  },
  "dependencies": {
    "@hono/node-server": "^1.13.7",
    "hono": "^4.6.14"
  },
  "devDependencies": {
    "@types/node": "^22.10.2",
    "typescript": "^5.7.2",
    "vitest": "^2.1.8"
  }
}
```

`services/api/tsconfig.json`:

```json
{
  "compilerOptions": {
    "target": "ES2023",
    "module": "ESNext",
    "moduleResolution": "bundler",
    "lib": ["ES2023"],
    "types": ["node"],
    "strict": true,
    "noUncheckedIndexedAccess": true,
    "noEmit": true,
    "skipLibCheck": true,
    "verbatimModuleSyntax": true,
    "allowImportingTsExtensions": true
  },
  "include": ["src/**/*.ts", "test/**/*.ts"]
}
```

**`allowImportingTsExtensions` is not optional here.** Every file in this service imports its siblings with an explicit `.ts` extension, because the runtime is `node --experimental-strip-types` with no build step — Node resolves real paths, so an extensionless relative import fails at runtime even when it typechecks. TypeScript rejects the explicit extension with **TS5097** unless this flag is set, so the two requirements only meet here. It is safe alongside `noEmit`.

**`noUncheckedIndexedAccess` is on deliberately.** Most of this service indexes into rows returned from the database, and the difference between `Row` and `Row | undefined` is the difference between a crash at 2am and a type error now.

`services/api/vitest.config.ts`:

```ts
import { defineConfig } from 'vitest/config'

export default defineConfig({
  test: {
    // Testcontainers pulls and boots a real Postgres; the default 5s is not
    // enough for the first run on a cold image cache.
    testTimeout: 60_000,
    hookTimeout: 120_000,
  },
})
```

- [ ] **Step 3: Write the error envelope**

`services/api/src/http/errors.ts`:

```ts
/**
 * The one error shape, from solo_execution 6.2: the client switches on
 * `code`, never on message text. Rewording a message must never be a
 * breaking change, which it becomes the moment a client matches on it.
 */
export type ErrorCode =
  | 'invalid_request'
  | 'idempotency_key_reused'
  | 'unauthorized'
  | 'not_found'
  | 'conflict'
  | 'client_too_old'
  | 'internal'

export interface ErrorBody {
  code: ErrorCode
  message: string
  details?: unknown
}

const STATUS: Record<ErrorCode, number> = {
  invalid_request: 400,
  unauthorized: 401,
  not_found: 404,
  conflict: 409,
  // 422 and not 409: the key is valid and was accepted before, but this
  // request's body differs from the one it was accepted for. Returning the
  // stored response would answer a question the caller did not ask.
  idempotency_key_reused: 422,
  client_too_old: 426,
  internal: 500,
}

export function fail(code: ErrorCode, message: string, details?: unknown): Response {
  const body: ErrorBody = details === undefined ? { code, message } : { code, message, details }
  return new Response(JSON.stringify(body), {
    status: STATUS[code],
    headers: { 'content-type': 'application/json' },
  })
}
```

- [ ] **Step 4: Write the failing test**

`services/api/test/health.test.ts`:

```ts
import { describe, expect, it } from 'vitest'
import { createApp } from '../src/app.ts'

describe('the app', () => {
  it('answers the health check Cloud Run probes', async () => {
    const res = await createApp().request('/healthz')
    expect(res.status).toBe(200)
    expect(await res.json()).toEqual({ ok: true })
  })

  it('returns the error envelope, not a bare string', async () => {
    const res = await createApp().request('/v1/nope')
    expect(res.status).toBe(404)
    // The shape is the contract. A client switching on `code` must find one.
    expect(await res.json()).toMatchObject({ code: 'not_found' })
  })
})
```

- [ ] **Step 5: Install and run it to verify it fails**

```bash
pnpm install && pnpm --filter @broodline/api test
```

Expected: **FAIL** — `Cannot find module '../src/app.ts'`.

- [ ] **Step 6: Write the app**

`services/api/src/app.ts`:

```ts
import { Hono } from 'hono'
import { fail } from './http/errors.ts'

/**
 * A factory rather than a module-level singleton, so every test gets a clean
 * app with no listener and no shared state. src/index.ts is the only place
 * that binds a port.
 */
export function createApp(): Hono {
  const app = new Hono()

  // Cloud Run's health check. Deliberately touches nothing - a probe that
  // queries the database turns a slow query into a rolled-back deploy.
  app.get('/healthz', (c) => c.json({ ok: true }))

  app.notFound(() => fail('not_found', 'No such route.'))

  app.onError((err) => {
    // Never leak an exception message to a client; it is the fastest route
    // from a stack trace to a schema disclosure.
    console.error('unhandled', err)
    return fail('internal', 'Something went wrong.')
  })

  return app
}
```

`services/api/src/index.ts`:

```ts
import { serve } from '@hono/node-server'
import { createApp } from './app.ts'

// Cloud Run sets PORT and it is not negotiable.
const port = Number(process.env.PORT ?? 8080)

serve({ fetch: createApp().fetch, port }, (info) => {
  console.log(JSON.stringify({ msg: 'listening', port: info.port }))
})
```

- [ ] **Step 7: Run the tests to verify they pass**

```bash
pnpm --filter @broodline/api test && pnpm --filter @broodline/api typecheck
```

Expected: **2 passed**, and typecheck clean.

- [ ] **Step 8: Ignore what should not be committed**

`.gitignore` — append:

```
# Phase 4 - the TypeScript half
services/**/dist/
services/**/*.tsbuildinfo

# Terraform
infra/terraform/.terraform/
infra/terraform/*.tfstate
infra/terraform/*.tfstate.*
infra/terraform/*.tfvars

# Config bundle build output. The AUTHORED json under config/bundles is
# tracked; what the publish step assembles is not.
config/out/
```

`node_modules/`, `.env` and `.env.*` are already ignored, with `!.env.example` excepted.

- [ ] **Step 9: Commit**

```bash
git add pnpm-workspace.yaml package.json .npmrc .gitignore services/ pnpm-lock.yaml
git commit -m "build: the TypeScript workspace, and an api that answers

services/ did not exist. This is the whole TS half of solo_execution
section 7's layout reduced to the smallest thing that proves the
toolchain: a pnpm workspace, one Hono service, a health check that
deliberately touches nothing, and the error envelope.

createApp() is a factory rather than a module singleton so tests mount
the app with no listener and no shared state; index.ts is the only file
that binds a port.

The envelope ships now rather than later because the client switches on
code and never on message text - solo_execution 6.2 - and retrofitting
that means the first reworded message is a breaking change.

noUncheckedIndexedAccess is on. Most of this service indexes rows out of
the database, and Row versus Row|undefined is the difference between a
type error today and a crash at 2am.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 3: The schema, real Postgres in tests, and the isolation gate

**The first of the phase's two gates.** §5.1 says cross-server leakage is *"nearly invisible in testing with one server and catastrophic with fifty"* — it cannot be found by observation, only by a test that deliberately creates the second server.

**Files:**
- Create: `services/api/drizzle/0001_tables.sql`, `services/api/drizzle/0002_rls.sql`
- Create: `services/api/src/db/schema.ts`, `services/api/src/db/client.ts`, `services/api/src/db/migrate.ts`
- Create: `services/api/test/harness.ts`, `services/api/test/isolation.test.ts`
- Modify: `services/api/package.json`

**Interfaces:**
- Consumes: nothing from earlier tasks.
- Produces:
  - `withServer<T>(serverId: number, fn: (tx: Tx) => Promise<T>): Promise<T>` — opens a transaction, scopes `app.server_id` to it, runs `fn`. **The only sanctioned way to touch a server-scoped table.**
  - `db` — the Drizzle handle for the two global tables.
  - `migrate(pool: Pool): Promise<void>`.
  - Tables: `accounts`, `servers`, `players`, `wallets`, `ledger`, `idempotencyKeys`, `campaignProgress`.
  - `startTestDb(): Promise<TestDb>` with `{ appUrl, ownerUrl, stop() }`.

- [ ] **Step 1: Add the database dependencies**

`services/api/package.json` — add to `dependencies`:

```json
    "drizzle-orm": "^0.38.2",
    "pg": "^8.13.1"
```

and to `devDependencies`:

```json
    "@testcontainers/postgresql": "^10.16.0",
    "@types/pg": "^8.11.10",
    "testcontainers": "^10.16.0"
```

```bash
pnpm install
```

- [ ] **Step 2: Write the table DDL by hand**

**Hand-written SQL rather than `drizzle-kit generate`.** The generator's output cannot be reviewed before it exists, and this schema's load-bearing details — composite primary keys leading with `server_id`, and the policies in `0002` — are exactly what a generator round-trip tends to reorder. `schema.ts` in Step 4 mirrors this file for typed queries, and every query in the suite fails loudly if the two disagree.

`services/api/drizzle/0001_tables.sql`:

```sql
-- Two GLOBAL tables. data_model section 1 makes the Player global, and
-- solo_execution 5.1 keeps the account record small because it is the only
-- component that region-partitions when the first non-US market lands.

CREATE TABLE servers (
  server_id           integer PRIMARY KEY,
  region              text    NOT NULL,
  state               text    NOT NULL CHECK (state IN ('open', 'closed')),
  -- MINUTE granularity, not hour - solo_execution 9.7. Every server in a
  -- region ticking at the same minute makes the weekly tick simultaneously
  -- the heaviest scheduled job and the largest push fan-out in the game.
  -- Storing an hour and needing minutes later is a migration; storing
  -- minutes and only ever using :00 costs nothing.
  tick_day_of_week    smallint NOT NULL CHECK (tick_day_of_week BETWEEN 0 AND 6),
  tick_minute_of_day  smallint NOT NULL CHECK (tick_minute_of_day BETWEEN 0 AND 1439),
  opened_at           timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE accounts (
  account_id      uuid PRIMARY KEY DEFAULT gen_random_uuid(),
  -- NULL for a guest. solo_execution 6.4: guests are real accounts with no
  -- credential bound, and upgrading binds the sub to the existing account so
  -- nothing migrates and there is no merge to conflict.
  apple_sub       text UNIQUE,
  birthdate_band  text NOT NULL,
  home_region     text NOT NULL,
  -- Assignment is by storefront region at signup and IMMUTABLE. There is no
  -- transfer path, ever - solo_execution section 4.
  server_id       integer NOT NULL REFERENCES servers(server_id),
  created_at      timestamptz NOT NULL DEFAULT now(),
  -- Soft delete. The App Store requires an in-app deletion path; the ledger
  -- is retained pseudonymised because it is a financial record - 6.4.
  deleted_at      timestamptz
);

CREATE TYPE currency AS ENUM ('shards', 'splice_charges', 'marks', 'premium');

-- Five SERVER-SCOPED tables. server_id leads every primary key and index, so
-- a merge is a re-keying exercise and nothing is globally meaningful.

CREATE TABLE players (
  server_id   integer NOT NULL,
  player_id   uuid    NOT NULL DEFAULT gen_random_uuid(),
  account_id  uuid    NOT NULL REFERENCES accounts(account_id),
  created_at  timestamptz NOT NULL DEFAULT now(),
  PRIMARY KEY (server_id, player_id)
);
CREATE UNIQUE INDEX players_by_account ON players (server_id, account_id);

CREATE TABLE wallets (
  server_id  integer  NOT NULL,
  player_id  uuid     NOT NULL,
  currency   currency NOT NULL,
  balance    bigint   NOT NULL CHECK (balance >= 0),
  -- Optimistic concurrency. solo_execution 5.4: compare-and-set, retry once,
  -- surface a conflict on the second failure. No locks.
  version    integer  NOT NULL DEFAULT 0,
  PRIMARY KEY (server_id, player_id, currency)
);

CREATE TABLE ledger (
  server_id        integer  NOT NULL,
  -- uuid, not bigserial: a sequence is globally meaningful and would collide
  -- on a server merge.
  entry_id         uuid     NOT NULL DEFAULT gen_random_uuid(),
  player_id        uuid     NOT NULL,
  currency         currency NOT NULL,
  delta            bigint   NOT NULL,
  balance_after    bigint   NOT NULL,
  reason_code      text     NOT NULL,
  ref_type         text,
  ref_id           text,
  idempotency_key  text,
  created_at       timestamptz NOT NULL DEFAULT now(),
  PRIMARY KEY (server_id, entry_id)
);
CREATE INDEX ledger_by_player ON ledger (server_id, player_id, currency, created_at);

CREATE TABLE idempotency_keys (
  server_id     integer NOT NULL,
  key           text    NOT NULL,
  request_hash  text    NOT NULL,
  status        text    NOT NULL CHECK (status IN ('in_flight', 'completed')),
  response_body jsonb,
  created_at    timestamptz NOT NULL DEFAULT now(),
  PRIMARY KEY (server_id, key)
);
-- Rows age out after 24 hours - solo_execution 6.3.
CREATE INDEX idempotency_keys_by_age ON idempotency_keys (server_id, created_at);

CREATE TABLE campaign_progress (
  server_id            integer NOT NULL,
  player_id            uuid    NOT NULL,
  highest_wave_cleared integer NOT NULL DEFAULT 0,
  milestones_claimed   integer NOT NULL DEFAULT 0,
  updated_at           timestamptz NOT NULL DEFAULT now(),
  PRIMARY KEY (server_id, player_id)
);
```

- [ ] **Step 3: Write the RLS migration**

`services/api/drizzle/0002_rls.sql`:

```sql
-- Isolation is Row-Level Security, per solo_execution 5.1. A query missing
-- its WHERE server_id returns ZERO rows rather than another server's data.
--
-- TWO things are needed and each defeats a different bypass:
--
--   FORCE ROW LEVEL SECURITY - without it the table OWNER is exempt, so a
--   suite connecting as the owner watches every policy do nothing while
--   reporting green.
--
--   A NON-SUPERUSER role - a superuser bypasses RLS entirely and FORCE does
--   not change that. Testcontainers' default user IS a superuser, so a suite
--   that skips this is testing nothing.

CREATE ROLE broodline_app NOLOGIN NOSUPERUSER NOBYPASSRLS;

GRANT SELECT, INSERT, UPDATE, DELETE ON
  players, wallets, ledger, idempotency_keys, campaign_progress
  TO broodline_app;

-- The two global tables are deliberately NOT under a policy. The account
-- record carries no server_id (5.1) and the registry describes servers
-- rather than living inside one.
GRANT SELECT, INSERT, UPDATE ON accounts TO broodline_app;
GRANT SELECT ON servers TO broodline_app;

DO $$
DECLARE t text;
BEGIN
  FOREACH t IN ARRAY ARRAY['players','wallets','ledger','idempotency_keys','campaign_progress']
  LOOP
    EXECUTE format('ALTER TABLE %I ENABLE ROW LEVEL SECURITY', t);
    EXECUTE format('ALTER TABLE %I FORCE ROW LEVEL SECURITY', t);
    -- current_setting(..., true) returns NULL when unset instead of raising.
    -- NULL = server_id is NULL, never true, so an unscoped query sees
    -- nothing. That is default-deny, and it is the behaviour the isolation
    -- suite pins.
    EXECUTE format($p$
      CREATE POLICY server_isolation ON %I
        FOR ALL
        USING      (server_id = NULLIF(current_setting('app.server_id', true), '')::int)
        WITH CHECK (server_id = NULLIF(current_setting('app.server_id', true), '')::int)
    $p$, t);
  END LOOP;
END $$;
```

**`WITH CHECK` as well as `USING`.** `USING` filters what is read; without `WITH CHECK` a handler could still *insert* a row carrying another server's id, which is the more damaging direction.

- [ ] **Step 4: Write the Drizzle schema**

`services/api/src/db/schema.ts`:

```ts
import {
  bigint, index, integer, jsonb, pgEnum, pgTable, primaryKey,
  smallint, text, timestamp, uniqueIndex, uuid,
} from 'drizzle-orm/pg-core'

export const currency = pgEnum('currency', ['shards', 'splice_charges', 'marks', 'premium'])

export const servers = pgTable('servers', {
  serverId: integer('server_id').primaryKey(),
  region: text('region').notNull(),
  state: text('state').notNull(),
  tickDayOfWeek: smallint('tick_day_of_week').notNull(),
  tickMinuteOfDay: smallint('tick_minute_of_day').notNull(),
  openedAt: timestamp('opened_at', { withTimezone: true }).notNull().defaultNow(),
})

export const accounts = pgTable('accounts', {
  accountId: uuid('account_id').primaryKey().defaultRandom(),
  appleSub: text('apple_sub'),
  birthdateBand: text('birthdate_band').notNull(),
  homeRegion: text('home_region').notNull(),
  serverId: integer('server_id').notNull(),
  createdAt: timestamp('created_at', { withTimezone: true }).notNull().defaultNow(),
  deletedAt: timestamp('deleted_at', { withTimezone: true }),
})

export const players = pgTable('players', {
  serverId: integer('server_id').notNull(),
  playerId: uuid('player_id').notNull().defaultRandom(),
  accountId: uuid('account_id').notNull(),
  createdAt: timestamp('created_at', { withTimezone: true }).notNull().defaultNow(),
}, (t) => ({
  pk: primaryKey({ columns: [t.serverId, t.playerId] }),
  byAccount: uniqueIndex('players_by_account').on(t.serverId, t.accountId),
}))

export const wallets = pgTable('wallets', {
  serverId: integer('server_id').notNull(),
  playerId: uuid('player_id').notNull(),
  currency: currency('currency').notNull(),
  // bigint as a JS number would silently lose precision past 2^53. mode
  // 'number' is chosen anyway because no balance in this game approaches it,
  // and the alternative poisons every arithmetic site with BigInt. If a
  // currency ever could, this is the line that changes.
  balance: bigint('balance', { mode: 'number' }).notNull(),
  version: integer('version').notNull().default(0),
}, (t) => ({
  pk: primaryKey({ columns: [t.serverId, t.playerId, t.currency] }),
}))

export const ledger = pgTable('ledger', {
  serverId: integer('server_id').notNull(),
  entryId: uuid('entry_id').notNull().defaultRandom(),
  playerId: uuid('player_id').notNull(),
  currency: currency('currency').notNull(),
  delta: bigint('delta', { mode: 'number' }).notNull(),
  balanceAfter: bigint('balance_after', { mode: 'number' }).notNull(),
  reasonCode: text('reason_code').notNull(),
  refType: text('ref_type'),
  refId: text('ref_id'),
  idempotencyKey: text('idempotency_key'),
  createdAt: timestamp('created_at', { withTimezone: true }).notNull().defaultNow(),
}, (t) => ({
  pk: primaryKey({ columns: [t.serverId, t.entryId] }),
  byPlayer: index('ledger_by_player').on(t.serverId, t.playerId, t.currency, t.createdAt),
}))

export const idempotencyKeys = pgTable('idempotency_keys', {
  serverId: integer('server_id').notNull(),
  key: text('key').notNull(),
  requestHash: text('request_hash').notNull(),
  status: text('status').notNull(),
  responseBody: jsonb('response_body'),
  createdAt: timestamp('created_at', { withTimezone: true }).notNull().defaultNow(),
}, (t) => ({
  pk: primaryKey({ columns: [t.serverId, t.key] }),
  byAge: index('idempotency_keys_by_age').on(t.serverId, t.createdAt),
}))

export const campaignProgress = pgTable('campaign_progress', {
  serverId: integer('server_id').notNull(),
  playerId: uuid('player_id').notNull(),
  highestWaveCleared: integer('highest_wave_cleared').notNull().default(0),
  milestonesClaimed: integer('milestones_claimed').notNull().default(0),
  updatedAt: timestamp('updated_at', { withTimezone: true }).notNull().defaultNow(),
}, (t) => ({
  pk: primaryKey({ columns: [t.serverId, t.playerId] }),
}))
```

- [ ] **Step 5: Write the migration runner**

`services/api/src/db/migrate.ts`:

```ts
import { readdir, readFile } from 'node:fs/promises'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'
import type { Pool } from 'pg'

const DIR = join(dirname(fileURLToPath(import.meta.url)), '../../drizzle')

/**
 * Applies every .sql file in drizzle/, in filename order, once.
 *
 * Hand-rolled rather than drizzle-kit's runner because this is thirty lines
 * and the alternative is a build-time dependency in the deploy image for
 * something that reads files and runs them in a transaction.
 *
 * Expand, deploy, migrate, contract - solo_execution 7.0. A migration and
 * the code that requires it never deploy together, which is what makes
 * rollback possible.
 */
export async function migrate(pool: Pool): Promise<void> {
  await pool.query(`
    CREATE TABLE IF NOT EXISTS _migrations (
      name text PRIMARY KEY,
      applied_at timestamptz NOT NULL DEFAULT now()
    )`)

  const files = (await readdir(DIR)).filter((f) => f.endsWith('.sql')).sort()

  for (const name of files) {
    const client = await pool.connect()
    try {
      await client.query('BEGIN')
      // Skip-if-applied inside the transaction, so two instances racing on a
      // cold start cannot both apply the same file.
      const done = await client.query('SELECT 1 FROM _migrations WHERE name = $1 FOR UPDATE', [name])
      if (done.rowCount === 0) {
        await client.query(await readFile(join(DIR, name), 'utf8'))
        await client.query('INSERT INTO _migrations (name) VALUES ($1)', [name])
      }
      await client.query('COMMIT')
    } catch (err) {
      await client.query('ROLLBACK')
      throw new Error(`migration ${name} failed: ${String(err)}`)
    } finally {
      client.release()
    }
  }
}
```

- [ ] **Step 6: Write the client and `withServer`**

`services/api/src/db/client.ts`:

```ts
import { sql } from 'drizzle-orm'
import { drizzle, type NodePgDatabase } from 'drizzle-orm/node-postgres'
import pg from 'pg'
import * as schema from './schema.ts'

export type Db = NodePgDatabase<typeof schema>
export type Tx = Parameters<Parameters<Db['transaction']>[0]>[0]

export function createPool(connectionString: string): pg.Pool {
  return new pg.Pool({
    connectionString,
    // Cloud Run scales to hundreds of instances and each opens a pool;
    // Postgres runs out of connections long before CPU. solo_execution 5.7
    // defers PgBouncer behind a hard instance cap plus a small per-instance
    // pool - this is that pool.
    max: 5,
    idleTimeoutMillis: 10_000,
  })
}

export function createDb(pool: pg.Pool): Db {
  return drizzle(pool, { schema })
}

/**
 * The ONLY sanctioned way to touch a server-scoped table.
 *
 * Every server-scoped table is under a policy comparing server_id against
 * current_setting('app.server_id'). Outside this helper the setting is
 * unset, the comparison is NULL, and every query returns zero rows - so a
 * handler that forgets this does not leak, it simply finds nothing.
 *
 * The THIRD ARGUMENT to set_config is the load-bearing one: it scopes the
 * setting to the transaction, so it cannot survive on a pooled connection
 * and be inherited by whoever checks that connection out next. That is the
 * difference between this and `SET search_path`, and isolation.test.ts pins
 * it.
 */
export async function withServer<T>(db: Db, serverId: number, fn: (tx: Tx) => Promise<T>): Promise<T> {
  return db.transaction(async (tx) => {
    await tx.execute(sql`SELECT set_config('app.server_id', ${String(serverId)}, true)`)
    return fn(tx)
  })
}
```

- [ ] **Step 7: Write the test harness**

`services/api/test/harness.ts`:

```ts
import { PostgreSqlContainer, type StartedPostgreSqlContainer } from '@testcontainers/postgresql'
import type pg from 'pg'
import { createDb, createPool, type Db } from '../src/db/client.ts'
import { migrate } from '../src/db/migrate.ts'

export interface TestDb {
  db: Db          // connected as broodline_app - non-superuser, RLS applies
  ownerDb: Db     // connected as the superuser - migrations and fixtures only
  pool: pg.Pool
  stop: () => Promise<void>
}

/**
 * Real Postgres, never a mock and never SQLite.
 *
 * RLS, set_config's transaction scoping, unique-violation semantics and
 * transactional rollback are the four properties this phase's gates test,
 * and all four are exactly what a substitute fakes.
 */
export async function startTestDb(): Promise<TestDb> {
  const container: StartedPostgreSqlContainer = await new PostgreSqlContainer('postgres:16-alpine').start()

  const ownerPool = createPool(container.getConnectionUri())
  await migrate(ownerPool)

  // The app role exists but has NOLOGIN, so give it a password and a way in.
  // Kept here rather than in the migration because production grants its
  // login through a Cloud SQL IAM binding, not a password.
  await ownerPool.query(`ALTER ROLE broodline_app LOGIN PASSWORD 'app'`)

  const appUri = new URL(container.getConnectionUri())
  appUri.username = 'broodline_app'
  appUri.password = 'app'
  const appPool = createPool(appUri.toString())

  return {
    db: createDb(appPool),
    ownerDb: createDb(ownerPool),
    pool: appPool,
    stop: async () => {
      await appPool.end()
      await ownerPool.end()
      await container.stop()
    },
  }
}
```

- [ ] **Step 8: Write the isolation gate**

`services/api/test/isolation.test.ts`:

```ts
import { sql } from 'drizzle-orm'
import { afterAll, beforeAll, describe, expect, it } from 'vitest'
import { withServer } from '../src/db/client.ts'
import { accounts, ledger, players, servers, wallets } from '../src/db/schema.ts'
import { startTestDb, type TestDb } from './harness.ts'

const SERVER_A = 1
const SERVER_B = 2

let t: TestDb
let playerA: string
let playerB: string

beforeAll(async () => {
  t = await startTestDb()

  // Fixtures go in as the OWNER, because seeding two servers is precisely
  // what a policy-bound connection is not allowed to do.
  await t.ownerDb.insert(servers).values([
    { serverId: SERVER_A, region: 'us-central1', state: 'open', tickDayOfWeek: 0, tickMinuteOfDay: 1200 },
    { serverId: SERVER_B, region: 'us-central1', state: 'open', tickDayOfWeek: 0, tickMinuteOfDay: 1217 },
  ])

  const [accA] = await t.ownerDb.insert(accounts)
    .values({ birthdateBand: 'adult', homeRegion: 'us-central1', serverId: SERVER_A }).returning()
  const [accB] = await t.ownerDb.insert(accounts)
    .values({ birthdateBand: 'adult', homeRegion: 'us-central1', serverId: SERVER_B }).returning()

  const [pA] = await t.ownerDb.insert(players)
    .values({ serverId: SERVER_A, accountId: accA!.accountId }).returning()
  const [pB] = await t.ownerDb.insert(players)
    .values({ serverId: SERVER_B, accountId: accB!.accountId }).returning()
  playerA = pA!.playerId
  playerB = pB!.playerId

  await t.ownerDb.insert(wallets).values([
    { serverId: SERVER_A, playerId: playerA, currency: 'shards', balance: 100 },
    { serverId: SERVER_B, playerId: playerB, currency: 'shards', balance: 999 },
  ])
  await t.ownerDb.insert(ledger).values([
    { serverId: SERVER_A, playerId: playerA, currency: 'shards', delta: 100, balanceAfter: 100, reasonCode: 'FIXTURE' },
    { serverId: SERVER_B, playerId: playerB, currency: 'shards', delta: 999, balanceAfter: 999, reasonCode: 'FIXTURE' },
  ])
}, 180_000)

afterAll(async () => { await t?.stop() })

describe('the gate itself', () => {
  // Do not trust a gate that has never been proven capable of failing. If the
  // app role were a superuser, or the tables lacked FORCE, every assertion
  // below would pass while the policies did nothing.
  it('runs as a role that cannot bypass RLS', async () => {
    const r = await t.db.execute(sql`
      SELECT current_user AS who, rolsuper, rolbypassrls
        FROM pg_roles WHERE rolname = current_user`)
    const row = r.rows[0] as { who: string; rolsuper: boolean; rolbypassrls: boolean }
    expect(row.who).toBe('broodline_app')
    expect(row.rolsuper).toBe(false)
    expect(row.rolbypassrls).toBe(false)
  })

  it('forces RLS on every server-scoped table, so ownership grants no exemption', async () => {
    const r = await t.db.execute(sql`
      SELECT relname, relrowsecurity, relforcerowsecurity
        FROM pg_class
       WHERE relname IN ('players','wallets','ledger','idempotency_keys','campaign_progress')
       ORDER BY relname`)
    expect(r.rows).toHaveLength(5)
    for (const row of r.rows as Array<{ relrowsecurity: boolean; relforcerowsecurity: boolean }>) {
      expect(row.relrowsecurity).toBe(true)
      expect(row.relforcerowsecurity).toBe(true)
    }
  })
})

describe('cross-server isolation', () => {
  it('shows a scoped read only its own server, across every table', async () => {
    const seen = await withServer(t.db, SERVER_A, async (tx) => ({
      players: await tx.select().from(players),
      wallets: await tx.select().from(wallets),
      ledger: await tx.select().from(ledger),
    }))

    expect(seen.players.map((p) => p.serverId)).toEqual([SERVER_A])
    expect(seen.wallets.map((w) => w.serverId)).toEqual([SERVER_A])
    expect(seen.ledger.map((l) => l.serverId)).toEqual([SERVER_A])

    // Named explicitly: server B's 999 shards must not appear anywhere.
    expect(seen.wallets.some((w) => w.balance === 999)).toBe(false)
  })

  it('returns ZERO rows when nothing scoped the query, rather than everything', async () => {
    // The default-deny property. A handler that forgets withServer finds
    // nothing; it does not quietly read the whole fleet.
    const rows = await t.db.select().from(wallets)
    expect(rows).toEqual([])
  })

  it('refuses to INSERT a row belonging to another server', async () => {
    // WITH CHECK, not USING. Writing into a neighbour is the more damaging
    // direction and USING alone does not stop it.
    await expect(
      withServer(t.db, SERVER_A, async (tx) => {
        await tx.insert(wallets).values({
          serverId: SERVER_B, playerId: playerB, currency: 'marks', balance: 1,
        })
      }),
    ).rejects.toThrow(/row-level security/i)
  })

  it('does not let app.server_id survive a transaction onto the next checkout', async () => {
    // THE reason set_config's third argument is true. A setting that leaked
    // across a pooled checkout would hand the next request the last one's
    // server, and with one server in testing it would never be noticed.
    await withServer(t.db, SERVER_B, async (tx) => {
      expect(await tx.select().from(wallets)).toHaveLength(1)
    })

    const leaked = await t.db.execute(sql`SELECT current_setting('app.server_id', true) AS v`)
    expect((leaked.rows[0] as { v: string | null }).v ?? '').toBe('')

    expect(await t.db.select().from(wallets)).toEqual([])
  })
})
```

- [ ] **Step 9: Run the gate**

```bash
pnpm --filter @broodline/api test
```

Expected: **8 passed** across the package — 2 from Task 2, plus 6 here: the gate's own 2 meta-assertions and 4 isolation assertions. Docker must be running; the first run pulls `postgres:16-alpine`.

**If `runs as a role that cannot bypass RLS` fails, stop and fix it before reading any other result.** Every isolation assertion below it is meaningless while it is red.

- [ ] **Step 10: Prove the gate can fail**

Comment out the `FORCE ROW LEVEL SECURITY` line in `0002_rls.sql`, then run the suite:

```bash
pnpm --filter @broodline/api test isolation 2>&1 | tail -20
```

Expected while weakened: **FAIL** on `forces RLS on every server-scoped table`. That assertion is the one standing between a real gate and a suite that reports green while every policy does nothing.

Restore the line and re-run to green.

**`git checkout --` will not help here**: `0002_rls.sql` is created by this task and is untracked (or staged-only) at this point, so there is no committed version to restore from. Uncomment the line by hand, then confirm you restored it exactly:

```bash
grep -c "FORCE ROW LEVEL SECURITY" services/api/drizzle/0002_rls.sql
```

Expected: `2` — once in the explanatory comment block at the top, once in the `EXECUTE format(...)` line.

```bash
pnpm --filter @broodline/api test isolation
```

Expected: green again.

- [ ] **Step 11: Wire the test script and commit**

```bash
pnpm --filter @broodline/api test && pnpm --filter @broodline/api typecheck
```

Expected: green, typecheck clean.

```bash
git add services/api pnpm-lock.yaml
git commit -m "feat: the schema, real Postgres in tests, and the isolation gate

Seven tables. server_id leads every primary key and index on the five
server-scoped ones, so a merge is a re-keying exercise; accounts and
servers stay global because the account record is the only component
that region-partitions later.

Isolation is RLS and the gate is two assertions most suites would not
think to write. FORCE ROW LEVEL SECURITY, because without it the table
owner is exempt and a suite connecting as the owner watches every policy
do nothing while reporting green. And a NOSUPERUSER NOBYPASSRLS app
role, because a superuser bypasses RLS entirely and FORCE does not
change that - Testcontainers' default user is a superuser, so a suite
that skips this is testing nothing at all. Both are asserted directly,
before any isolation claim is made.

The policy uses current_setting(..., true) so an unscoped query sees
NULL and returns zero rows rather than raising - default-deny. WITH
CHECK as well as USING, because writing into a neighbouring server is
the more damaging direction and USING alone permits it.

The last test is the one 5.1 is really about: app.server_id must not
survive its transaction onto the next pooled checkout. That is what
set_config's third argument buys, and with one server in testing a leak
would never be noticed.

Migrations are hand-written SQL rather than drizzle-kit output. The
generator's SQL cannot be reviewed before it exists, and the load-bearing
details here are exactly what a round-trip reorders.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 4: Wallets, the ledger, idempotency, and the concurrency gate

§5.3 calls the ledger *"the highest-value small piece of code in the backend"* and notes that games adding it after launch never fully recover the first six months. This task writes it, and the second gate proves the property that makes it trustworthy: **a retry pays exactly once.**

**Files:**
- Create: `services/api/src/money/ledger.ts`, `services/api/src/money/idempotency.ts`, `services/api/src/money/invariant.ts`
- Create: `services/api/test/idempotency.test.ts`, `services/api/test/ledger.test.ts`

**Interfaces:**
- Consumes: `withServer`, `Tx`, `Db`, the `wallets`/`ledger`/`idempotencyKeys` tables from Task 3.
- Produces:
  - `credit(tx: Tx, m: Mutation): Promise<number>` — applies a delta and writes its ledger row in the same transaction; returns the new balance.
  - `withIdempotency<T>(db, serverId, key, requestHash, fn): Promise<Idempotent<T>>` where `Idempotent<T> = { status: 'fresh' | 'replayed'; body: T }`.
  - `IdempotencyMismatchError` — carries the `422`.
  - `InsufficientFundsError` — carries the `409`.
  - `findDrift(tx: Tx): Promise<Drift[]>`.

- [ ] **Step 1: Write the failing ledger test**

`services/api/test/ledger.test.ts`:

```ts
import { and, eq } from 'drizzle-orm'
import { afterAll, beforeAll, describe, expect, it } from 'vitest'
import { withServer } from '../src/db/client.ts'
import { InsufficientFundsError, credit } from '../src/money/ledger.ts'
import { findDrift } from '../src/money/invariant.ts'
import { accounts, ledger, players, servers, wallets } from '../src/db/schema.ts'
import { startTestDb, type TestDb } from './harness.ts'

const S = 1
let t: TestDb
let player: string

beforeAll(async () => {
  t = await startTestDb()
  await t.ownerDb.insert(servers).values({
    serverId: S, region: 'us-central1', state: 'open', tickDayOfWeek: 0, tickMinuteOfDay: 1200,
  })
  const [acc] = await t.ownerDb.insert(accounts)
    .values({ birthdateBand: 'adult', homeRegion: 'us-central1', serverId: S }).returning()
  const [p] = await t.ownerDb.insert(players)
    .values({ serverId: S, accountId: acc!.accountId }).returning()
  player = p!.playerId
}, 180_000)

afterAll(async () => { await t?.stop() })

describe('credit', () => {
  it('creates the wallet on first credit and writes its ledger row', async () => {
    const balance = await withServer(t.db, S, (tx) =>
      credit(tx, { serverId: S, playerId: player, currency: 'shards', delta: 250, reasonCode: 'TEST_GRANT' }))

    expect(balance).toBe(250)

    const rows = await withServer(t.db, S, (tx) =>
      tx.select().from(ledger).where(and(eq(ledger.playerId, player), eq(ledger.reasonCode, 'TEST_GRANT'))))

    expect(rows).toHaveLength(1)
    expect(rows[0]!.delta).toBe(250)
    // balance_after is recorded, not recomputed at read time. It is what makes
    // "where did my shards go" answerable without replaying the whole history.
    expect(rows[0]!.balanceAfter).toBe(250)
  })

  it('accumulates, and each mutation leaves its own row', async () => {
    await withServer(t.db, S, async (tx) => {
      await credit(tx, { serverId: S, playerId: player, currency: 'marks', delta: 10, reasonCode: 'A' })
      await credit(tx, { serverId: S, playerId: player, currency: 'marks', delta: 5, reasonCode: 'B' })
    })

    const [w] = await withServer(t.db, S, (tx) =>
      tx.select().from(wallets).where(and(eq(wallets.playerId, player), eq(wallets.currency, 'marks'))))

    expect(w!.balance).toBe(15)
    // version moves on every write - the optimistic-concurrency column at 5.4.
    expect(w!.version).toBe(1)
  })

  it('refuses to overdraw, and leaves nothing behind', async () => {
    await expect(
      withServer(t.db, S, (tx) =>
        credit(tx, { serverId: S, playerId: player, currency: 'marks', delta: -1000, reasonCode: 'OVERDRAW' })),
    ).rejects.toBeInstanceOf(InsufficientFundsError)

    // The whole transaction rolled back, so no orphan ledger row survives.
    const orphans = await withServer(t.db, S, (tx) =>
      tx.select().from(ledger).where(eq(ledger.reasonCode, 'OVERDRAW')))
    expect(orphans).toEqual([])
  })
})

describe('the invariant job', () => {
  it('finds no drift between the ledger and the wallets', async () => {
    // solo_execution 5.3: drift is either a bug or a duplication exploit and
    // warrants same-day attention. Running it as a test means it is exercised
    // long before it is ever a 3am page.
    const drift = await withServer(t.db, S, (tx) => findDrift(tx))
    expect(drift).toEqual([])
  })
})
```

- [ ] **Step 2: Run it to verify it fails**

```bash
pnpm --filter @broodline/api test ledger
```

Expected: **FAIL** — `Cannot find module '../src/money/ledger.ts'`.

- [ ] **Step 3: Write the ledger**

`services/api/src/money/ledger.ts`:

```ts
import { sql } from 'drizzle-orm'
import type { Tx } from '../db/client.ts'
import { ledger } from '../db/schema.ts'

export type Currency = 'shards' | 'splice_charges' | 'marks' | 'premium'

export interface Mutation {
  serverId: number
  playerId: string
  currency: Currency
  /** Negative debits. The wallet's CHECK (balance >= 0) is what refuses one. */
  delta: number
  reasonCode: string
  refType?: string
  refId?: string
  idempotencyKey?: string
}

export class InsufficientFundsError extends Error {
  constructor(readonly currency: Currency) {
    super(`Insufficient ${currency}.`)
    this.name = 'InsufficientFundsError'
  }
}

/**
 * The single function that writes a currency mutation.
 *
 * solo_execution 5.3: every currency mutation writes an append-only ledger
 * row in the SAME TRANSACTION as the balance update, and balances are never
 * derived by summing the ledger at read time. Both halves matter - summing at
 * read time is how a ledger becomes too slow to keep, and a balance written
 * without its row is how drift starts.
 *
 * MUST be called inside withServer(). Outside it the policy hides every row
 * and the upsert below silently inserts into nothing it can then read.
 */
export async function credit(tx: Tx, m: Mutation): Promise<number> {
  let balanceAfter: number
  try {
    // One statement, so it is correct under any concurrency: ON CONFLICT DO
    // UPDATE takes the row lock, and a second transaction waits rather than
    // reading a stale balance and writing it back.
    const res = await tx.execute(sql`
      INSERT INTO wallets (server_id, player_id, currency, balance, version)
      VALUES (${m.serverId}, ${m.playerId}, ${m.currency}::currency, ${m.delta}, 0)
      ON CONFLICT (server_id, player_id, currency) DO UPDATE
        SET balance = wallets.balance + EXCLUDED.balance,
            version = wallets.version + 1
      RETURNING balance`)

    const row = res.rows[0] as { balance: string | number } | undefined
    if (row === undefined) {
      // Reachable only if the policy hid the row - i.e. credit() was called
      // outside withServer, or with a server_id the transaction is not scoped
      // to. Both are programming errors and both must be loud.
      throw new Error(
        `credit() wrote no wallet row for server ${m.serverId}. ` +
        `Was it called outside withServer(), or with a mismatched server_id?`)
    }
    balanceAfter = Number(row.balance)
  } catch (err) {
    // 23514 is check_violation - here, always balance >= 0.
    if (typeof err === 'object' && err !== null && (err as { code?: string }).code === '23514') {
      throw new InsufficientFundsError(m.currency)
    }
    throw err
  }

  await tx.insert(ledger).values({
    serverId: m.serverId,
    playerId: m.playerId,
    currency: m.currency,
    delta: m.delta,
    balanceAfter,
    reasonCode: m.reasonCode,
    refType: m.refType ?? null,
    refId: m.refId ?? null,
    idempotencyKey: m.idempotencyKey ?? null,
  })

  return balanceAfter
}
```

- [ ] **Step 4: Write the invariant job**

`services/api/src/money/invariant.ts`:

```ts
import { sql } from 'drizzle-orm'
import type { Tx } from '../db/client.ts'
import type { Currency } from './ledger.ts'

export interface Drift {
  playerId: string
  currency: Currency
  walletBalance: number
  ledgerSum: number
}

/**
 * Sums ledger.delta per (player, currency) and compares it to the stored
 * wallet balance. solo_execution 5.3: drift is either a bug or a duplication
 * exploit, and both get worse the longer they run.
 *
 * A FULL OUTER JOIN rather than a join from wallets, because the two
 * interesting failures are asymmetric: a wallet with no ledger history is a
 * balance written without its row, and ledger history with no wallet is a
 * wallet deleted out from under its own audit trail. A one-sided join sees
 * only the first.
 */
export async function findDrift(tx: Tx): Promise<Drift[]> {
  const res = await tx.execute(sql`
    WITH sums AS (
      SELECT player_id, currency, SUM(delta) AS total
        FROM ledger GROUP BY player_id, currency
    )
    SELECT COALESCE(w.player_id, s.player_id)   AS player_id,
           COALESCE(w.currency,  s.currency)    AS currency,
           COALESCE(w.balance, 0)               AS wallet_balance,
           COALESCE(s.total,   0)               AS ledger_sum
      FROM wallets w
      FULL OUTER JOIN sums s
        ON w.player_id = s.player_id AND w.currency = s.currency
     WHERE COALESCE(w.balance, 0) <> COALESCE(s.total, 0)`)

  return res.rows.map((r) => {
    const row = r as Record<string, unknown>
    return {
      playerId: String(row.player_id),
      currency: row.currency as Currency,
      walletBalance: Number(row.wallet_balance),
      ledgerSum: Number(row.ledger_sum),
    }
  })
}
```

- [ ] **Step 5: Run the ledger tests to verify they pass**

```bash
pnpm --filter @broodline/api test ledger
```

Expected: **4 passed**.

- [ ] **Step 6: Write the failing concurrency gate**

`services/api/test/idempotency.test.ts`:

```ts
import { eq } from 'drizzle-orm'
import { afterAll, beforeAll, describe, expect, it } from 'vitest'
import { withServer } from '../src/db/client.ts'
import { IdempotencyMismatchError, withIdempotency } from '../src/money/idempotency.ts'
import { credit } from '../src/money/ledger.ts'
import { accounts, idempotencyKeys, ledger, players, servers, wallets } from '../src/db/schema.ts'
import { startTestDb, type TestDb } from './harness.ts'

const S = 1
const PARALLEL = 8
let t: TestDb
let player: string

beforeAll(async () => {
  t = await startTestDb()
  await t.ownerDb.insert(servers).values({
    serverId: S, region: 'us-central1', state: 'open', tickDayOfWeek: 0, tickMinuteOfDay: 1200,
  })
  const [acc] = await t.ownerDb.insert(accounts)
    .values({ birthdateBand: 'adult', homeRegion: 'us-central1', serverId: S }).returning()
  const [p] = await t.ownerDb.insert(players)
    .values({ serverId: S, accountId: acc!.accountId }).returning()
  player = p!.playerId
}, 180_000)

afterAll(async () => { await t?.stop() })

describe('idempotency under concurrency', () => {
  it('pays exactly once when the same key arrives eight times at once', async () => {
    const key = 'grant-abc'
    const hash = 'h1'

    const results = await Promise.all(
      Array.from({ length: PARALLEL }, () =>
        withIdempotency(t.db, S, key, hash, (tx) =>
          credit(tx, {
            serverId: S, playerId: player, currency: 'shards',
            delta: 100, reasonCode: 'CONCURRENT_GRANT', idempotencyKey: key,
          }).then((balance) => ({ balance })))),
    )

    // Exactly one request did the work; the rest replayed its answer.
    expect(results.filter((r) => r.status === 'fresh')).toHaveLength(1)
    expect(results.filter((r) => r.status === 'replayed')).toHaveLength(PARALLEL - 1)

    // Every caller got the same answer, whether it did the work or not.
    expect(new Set(results.map((r) => r.body.balance))).toEqual(new Set([100]))

    // THE ASSERTION. 100, not 800.
    const [w] = await withServer(t.db, S, (tx) =>
      tx.select().from(wallets).where(eq(wallets.playerId, player)))
    expect(w!.balance).toBe(100)

    const rows = await withServer(t.db, S, (tx) =>
      tx.select().from(ledger).where(eq(ledger.reasonCode, 'CONCURRENT_GRANT')))
    expect(rows).toHaveLength(1)
  })

  it('rejects a reused key carrying a different body, rather than answering the wrong question', async () => {
    const key = 'grant-def'
    await withIdempotency(t.db, S, key, 'hash-one', async () => ({ ok: true }))

    await expect(
      withIdempotency(t.db, S, key, 'hash-TWO', async () => ({ ok: true })),
    ).rejects.toBeInstanceOf(IdempotencyMismatchError)
  })

  it('does not strand a key when the work throws', async () => {
    const key = 'grant-ghi'

    await expect(
      withIdempotency(t.db, S, key, 'h', async () => { throw new Error('boom') }),
    ).rejects.toThrow('boom')

    // The transaction rolled back, key included, so an honest retry can still
    // succeed. A key stranded as in_flight by a crash would lock the caller
    // out of an action they never completed.
    const rows = await withServer(t.db, S, (tx) =>
      tx.select().from(idempotencyKeys).where(eq(idempotencyKeys.key, key)))
    expect(rows).toEqual([])

    const retry = await withIdempotency(t.db, S, key, 'h', async () => ({ ok: true }))
    expect(retry.status).toBe('fresh')
  })
})
```

- [ ] **Step 7: Run it to verify it fails**

```bash
pnpm --filter @broodline/api test idempotency
```

Expected: **FAIL** — `Cannot find module '../src/money/idempotency.ts'`.

- [ ] **Step 8: Write idempotency**

`services/api/src/money/idempotency.ts`:

```ts
import { and, eq } from 'drizzle-orm'
import { withServer, type Db, type Tx } from '../db/client.ts'
import { idempotencyKeys } from '../db/schema.ts'

export interface Idempotent<T> {
  status: 'fresh' | 'replayed'
  body: T
}

export class IdempotencyMismatchError extends Error {
  constructor(readonly key: string) {
    super('This idempotency key was used for a different request.')
    this.name = 'IdempotencyMismatchError'
  }
}

function isUniqueViolation(err: unknown): boolean {
  return typeof err === 'object' && err !== null && (err as { code?: string }).code === '23505'
}

/**
 * Runs `fn` at most once per (server, key), whatever the network does.
 *
 * solo_execution 6.3: the key is inserted as the FIRST statement inside the
 * same transaction as the mutation. On unique violation the stored response
 * is returned; if the key matches but the request hash differs, 422 - that is
 * a client bug, and returning another request's response would be worse than
 * failing.
 *
 * WHY A PLAIN INSERT RATHER THAN ON CONFLICT DO NOTHING: the conflicting
 * insert is what makes this correct under concurrency. Postgres BLOCKS the
 * second inserter until the first transaction resolves, then raises 23505 if
 * it committed - so the loser learns the winner's outcome. ON CONFLICT DO
 * NOTHING returns immediately instead, and the follow-up SELECT under READ
 * COMMITTED cannot see the winner's uncommitted row: both transactions would
 * conclude the key was free.
 *
 * The unique violation poisons the transaction, so the replay read happens in
 * a second one. That is not a race - the winner has committed by then, which
 * is the only reason the violation was raised.
 */
export async function withIdempotency<T>(
  db: Db,
  serverId: number,
  key: string,
  requestHash: string,
  fn: (tx: Tx) => Promise<T>,
): Promise<Idempotent<T>> {
  try {
    return await withServer(db, serverId, async (tx): Promise<Idempotent<T>> => {
      await tx.insert(idempotencyKeys).values({
        serverId, key, requestHash, status: 'in_flight',
      })

      const body = await fn(tx)

      await tx.update(idempotencyKeys)
        .set({ status: 'completed', responseBody: body as unknown })
        .where(and(eq(idempotencyKeys.serverId, serverId), eq(idempotencyKeys.key, key)))

      return { status: 'fresh', body }
    })
  } catch (err) {
    if (!isUniqueViolation(err)) throw err

    return withServer(db, serverId, async (tx): Promise<Idempotent<T>> => {
      const [row] = await tx.select().from(idempotencyKeys)
        .where(and(eq(idempotencyKeys.serverId, serverId), eq(idempotencyKeys.key, key)))

      if (row === undefined) {
        // The winner raised 23505 and then rolled back. Vanishingly rare and
        // not silently retryable - a retry would race the same way.
        throw new Error(`idempotency key ${key} conflicted and then vanished; retry the request.`)
      }
      if (row.requestHash !== requestHash) throw new IdempotencyMismatchError(key)

      return { status: 'replayed', body: row.responseBody as T }
    })
  }
}
```

- [ ] **Step 9: Run the gate**

```bash
pnpm --filter @broodline/api test idempotency
```

Expected: **3 passed**. The first is the gate: one `fresh`, seven `replayed`, balance `100`, one ledger row.

- [ ] **Step 10: Prove the gate can fail**

A gate that has never gone red is a guess. Break idempotency deliberately and confirm it catches it.

In `idempotency.ts`, temporarily replace the plain `insert` with an ignoring one:

```ts
      await tx.insert(idempotencyKeys).values({
        serverId, key, requestHash, status: 'in_flight',
      }).onConflictDoNothing()
```

```bash
pnpm --filter @broodline/api test idempotency 2>&1 | tail -25
```

Expected: **FAIL**, with the wallet holding more than 100 — several transactions each concluded the key was free. That is exactly the race the comment describes, and seeing it is worth the two minutes.

Restore the plain insert and re-run to green.

```bash
pnpm --filter @broodline/api test idempotency
```

- [ ] **Step 11: Run everything and commit**

```bash
pnpm --filter @broodline/api test && pnpm --filter @broodline/api typecheck
```

Expected: green — 2 health, 12 isolation, 4 ledger, 3 idempotency. **21 across the package.** The isolation suite grew from 6 to 12 during Task 3's review round; see the corrections section below.

```bash
git add services/api
git commit -m "feat: wallets, the ledger, idempotency, and the concurrency gate

credit() is the single function that writes a currency mutation, and it
writes the balance and its ledger row in one transaction. The upsert is
one statement so it is correct under any concurrency - ON CONFLICT DO
UPDATE takes the row lock, so a second transaction waits rather than
reading a stale balance and writing it back. balance_after is recorded
rather than recomputed, which is what makes 'where did my shards go'
answerable without replaying history.

The gate is eight parallel requests carrying one idempotency key, and it
asserts the wallet holds 100 rather than 800.

The correctness turns on a plain INSERT rather than ON CONFLICT DO
NOTHING, which is the opposite of the instinct. A conflicting insert
BLOCKS the second writer until the first transaction resolves and then
raises 23505, so the loser learns the winner committed. DO NOTHING
returns immediately, and the follow-up SELECT under READ COMMITTED
cannot see an uncommitted row - so every concurrent caller concludes the
key is free and the grant pays N times. Verified by execution: swapping
in onConflictDoNothing makes the gate go red with a balance above 100.

The invariant job ships as a test rather than only as a scheduled job.
It FULL OUTER JOINs because the two interesting failures are asymmetric:
a wallet with no history is a balance written without its row, and
history with no wallet is an audit trail whose wallet was deleted. A
one-sided join sees only the first.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 5: Identity — Sign in with Apple and the guest path

`solo_execution` §6.4. Apple as primary, a guest path that upgrades by binding the `sub` to the existing account, and server assignment taken once at creation from storefront region and never again.

**Files:**
- Create: `services/api/src/identity/apple.ts`, `services/api/src/identity/jwt.ts`, `services/api/src/identity/accounts.ts`
- Create: `services/api/test/identity.test.ts`
- Modify: `services/api/package.json`

**Interfaces:**
- Consumes: `db`, `accounts`, `players`, `servers`.
- Produces:
  - `verifyAppleToken(token: string, opts: AppleOpts): Promise<{ sub: string }>`
  - `issueAccessToken(claims: SessionClaims): Promise<string>`, `verifyAccessToken(token: string): Promise<SessionClaims>`
  - `issueRefreshToken`, `redeemRefreshToken`
  - `assignServer(storefrontRegion: string): Promise<number>`
  - `SessionClaims = { accountId: string; serverId: number }`

- [ ] **Step 1: Add `jose`**

`services/api/package.json` — add to `dependencies`:

```json
    "jose": "^5.9.6"
```

```bash
pnpm install
```

- [ ] **Step 2: Write the failing identity test**

`services/api/test/identity.test.ts`:

```ts
import { exportJWK, generateKeyPair, SignJWT, type JWK } from 'jose'
import { afterAll, beforeAll, describe, expect, it } from 'vitest'
import { verifyAppleToken } from '../src/identity/apple.ts'
import { issueAccessToken, issueRefreshToken, redeemRefreshToken, verifyAccessToken } from '../src/identity/jwt.ts'
import { assignServer, bindApple, createGuest } from '../src/identity/accounts.ts'
import { accounts, servers } from '../src/db/schema.ts'
import { startTestDb, type TestDb } from './harness.ts'
import { eq } from 'drizzle-orm'

const AUDIENCE = 'com.sepandstudio.broodline'
let t: TestDb
let appleKey: Awaited<ReturnType<typeof generateKeyPair>>
let appleJwks: { keys: JWK[] }

/** Stands in for Apple, so no test reaches the network. */
async function appleTokenFor(sub: string, over: Record<string, unknown> = {}): Promise<string> {
  return new SignJWT({ ...over })
    .setProtectedHeader({ alg: 'RS256', kid: 'test-key' })
    .setIssuer('https://appleid.apple.com')
    .setAudience(AUDIENCE)
    .setSubject(sub)
    .setExpirationTime('5m')
    .setIssuedAt()
    .sign(appleKey.privateKey)
}

beforeAll(async () => {
  t = await startTestDb()
  await t.ownerDb.insert(servers).values({
    serverId: 1, region: 'us-central1', state: 'open', tickDayOfWeek: 0, tickMinuteOfDay: 1200,
  })
  appleKey = await generateKeyPair('RS256')
  appleJwks = { keys: [{ ...(await exportJWK(appleKey.publicKey)), kid: 'test-key', alg: 'RS256', use: 'sig' }] }
}, 180_000)

afterAll(async () => { await t?.stop() })

describe('Apple token verification', () => {
  it('accepts a well-formed token and returns its sub', async () => {
    const id = await verifyAppleToken(await appleTokenFor('apple-user-1'), { audience: AUDIENCE, jwks: appleJwks })
    expect(id.sub).toBe('apple-user-1')
  })

  it('rejects a token minted for another app', async () => {
    const foreign = await new SignJWT({})
      .setProtectedHeader({ alg: 'RS256', kid: 'test-key' })
      .setIssuer('https://appleid.apple.com').setAudience('com.someone.else')
      .setSubject('x').setExpirationTime('5m').setIssuedAt().sign(appleKey.privateKey)

    await expect(verifyAppleToken(foreign, { audience: AUDIENCE, jwks: appleJwks })).rejects.toThrow()
  })

  it('rejects a token signed by a key Apple does not publish', async () => {
    const attacker = await generateKeyPair('RS256')
    const forged = await new SignJWT({})
      .setProtectedHeader({ alg: 'RS256', kid: 'test-key' })
      .setIssuer('https://appleid.apple.com').setAudience(AUDIENCE)
      .setSubject('x').setExpirationTime('5m').setIssuedAt().sign(attacker.privateKey)

    await expect(verifyAppleToken(forged, { audience: AUDIENCE, jwks: appleJwks })).rejects.toThrow()
  })
})

describe('accounts', () => {
  it('assigns a server from storefront region, and the assignment is the account\'s', async () => {
    expect(await assignServer('us-central1')).toBe(1)
  })

  it('creates a guest with no credential bound', async () => {
    const guest = await createGuest(t.ownerDb, { birthdateBand: 'adult', storefrontRegion: 'us-central1' })
    const [row] = await t.ownerDb.select().from(accounts).where(eq(accounts.accountId, guest.accountId))
    expect(row!.appleSub).toBeNull()
    expect(row!.serverId).toBe(1)
  })

  it('upgrades a guest by binding the sub to the SAME account, migrating nothing', async () => {
    const guest = await createGuest(t.ownerDb, { birthdateBand: 'adult', storefrontRegion: 'us-central1' })
    const bound = await bindApple(t.ownerDb, guest.accountId, 'apple-user-2')

    // 6.4: binding the sub to the existing account id means there is no
    // merge, and therefore no merge conflict.
    expect(bound.accountId).toBe(guest.accountId)
    const [row] = await t.ownerDb.select().from(accounts).where(eq(accounts.accountId, guest.accountId))
    expect(row!.appleSub).toBe('apple-user-2')
  })

  it('refuses to bind a sub already held by another account', async () => {
    const other = await createGuest(t.ownerDb, { birthdateBand: 'adult', storefrontRegion: 'us-central1' })
    // 6.4: a player signing in with an Apple ID already bound to another
    // account is OFFERED that account, never merged into this one.
    await expect(bindApple(t.ownerDb, other.accountId, 'apple-user-2')).rejects.toThrow(/already bound/i)
  })
})

describe('session tokens', () => {
  it('round-trips an access token', async () => {
    const token = await issueAccessToken({ accountId: 'acc-1', serverId: 1 })
    expect(await verifyAccessToken(token)).toMatchObject({ accountId: 'acc-1', serverId: 1 })
  })

  it('refuses an access token as a refresh token', async () => {
    // Distinct audiences, so a stolen access token cannot be traded up for a
    // long-lived one.
    const access = await issueAccessToken({ accountId: 'acc-1', serverId: 1 })
    await expect(redeemRefreshToken(t.ownerDb, access)).rejects.toThrow()
  })

  it('refuses to refresh a deleted account', async () => {
    const doomed = await createGuest(t.ownerDb, { birthdateBand: 'adult', storefrontRegion: 'us-central1' })
    const refresh = await issueRefreshToken({ accountId: doomed.accountId, serverId: 1 })

    await t.ownerDb.update(accounts).set({ deletedAt: new Date() })
      .where(eq(accounts.accountId, doomed.accountId))

    await expect(redeemRefreshToken(t.ownerDb, refresh)).rejects.toThrow(/deleted/i)
  })
})
```

- [ ] **Step 3: Run it to verify it fails**

```bash
pnpm --filter @broodline/api test identity
```

Expected: **FAIL** — the three identity modules do not exist.

- [ ] **Step 4: Write the Apple verifier**

`services/api/src/identity/apple.ts`:

```ts
import { createLocalJWKSet, createRemoteJWKSet, jwtVerify, type JSONWebKeySet, type JWTVerifyGetKey } from 'jose'

const APPLE_ISSUER = 'https://appleid.apple.com'
const APPLE_JWKS_URL = 'https://appleid.apple.com/auth/keys'

export interface AppleOpts {
  /** The app's bundle identifier. Apple puts it in `aud`. */
  audience: string
  /** Injected in tests. Production omits it and the remote set is used. */
  jwks?: JSONWebKeySet
}

let remote: JWTVerifyGetKey | undefined
function appleKeys(): JWTVerifyGetKey {
  // createRemoteJWKSet caches and handles rotation, so it is built once per
  // process rather than per request. Apple rotates these without notice.
  remote ??= createRemoteJWKSet(new URL(APPLE_JWKS_URL))
  return remote
}

/**
 * Verifies an Apple identity token and returns the stable user identifier.
 *
 * `sub` is the only field taken. Apple sends the email and name once, at
 * first authorization, and the design does not use either - solo_execution
 * 6.4 makes recovery Apple's problem precisely so there is no email to hold.
 */
export async function verifyAppleToken(token: string, opts: AppleOpts): Promise<{ sub: string }> {
  const keys = opts.jwks ? createLocalJWKSet(opts.jwks) : appleKeys()

  const { payload } = await jwtVerify(token, keys, {
    issuer: APPLE_ISSUER,
    audience: opts.audience,
    // RS256 only. Leaving the algorithm open is how a token signed with
    // `alg: none`, or an HMAC over the public key, gets accepted.
    algorithms: ['RS256'],
  })

  if (typeof payload.sub !== 'string' || payload.sub.length === 0) {
    throw new Error('Apple token carried no subject.')
  }
  return { sub: payload.sub }
}
```

- [ ] **Step 5: Write the session tokens**

`services/api/src/identity/jwt.ts`:

```ts
import { eq } from 'drizzle-orm'
import { SignJWT, jwtVerify } from 'jose'
import type { Db } from '../db/client.ts'
import { accounts } from '../db/schema.ts'

export interface SessionClaims {
  accountId: string
  serverId: number
}

const ACCESS_AUD = 'broodline/access'
const REFRESH_AUD = 'broodline/refresh'
const ACCESS_TTL = '15m'
const REFRESH_TTL = '90d'

function secret(): Uint8Array {
  const s = process.env.JWT_SECRET
  if (!s || s.length < 32) {
    throw new Error('JWT_SECRET must be set and at least 32 characters.')
  }
  return new TextEncoder().encode(s)
}

async function issue(claims: SessionClaims, audience: string, ttl: string): Promise<string> {
  return new SignJWT({ serverId: claims.serverId })
    .setProtectedHeader({ alg: 'HS256' })
    .setSubject(claims.accountId)
    .setAudience(audience)
    .setIssuedAt()
    .setExpirationTime(ttl)
    .sign(secret())
}

export const issueAccessToken = (c: SessionClaims) => issue(c, ACCESS_AUD, ACCESS_TTL)
export const issueRefreshToken = (c: SessionClaims) => issue(c, REFRESH_AUD, REFRESH_TTL)

export async function verifyAccessToken(token: string): Promise<SessionClaims> {
  const { payload } = await jwtVerify(token, secret(), { audience: ACCESS_AUD, algorithms: ['HS256'] })
  return { accountId: String(payload.sub), serverId: Number(payload.serverId) }
}

/**
 * Redeems a refresh token for a new pair.
 *
 * DELIBERATELY STATELESS. There is no refresh_tokens table, so an individual
 * token cannot be revoked before it expires - what CAN be revoked is the
 * account, and that is checked here on every refresh. That is enough for the
 * one thing milestone 1 must honour: the App Store's in-app deletion path
 * has to actually end the session.
 *
 * DEFERRED, with a trigger: a refresh_tokens table with rotation and reuse
 * detection arrives when the first non-TestFlight players do. Until then the
 * exposure is a stolen token on a device the player still holds, and the cost
 * of the table is an eighth table plus a write on every refresh.
 */
export async function redeemRefreshToken(db: Db, token: string): Promise<SessionClaims> {
  const { payload } = await jwtVerify(token, secret(), { audience: REFRESH_AUD, algorithms: ['HS256'] })
  const claims: SessionClaims = { accountId: String(payload.sub), serverId: Number(payload.serverId) }

  const [row] = await db.select().from(accounts).where(eq(accounts.accountId, claims.accountId))
  if (row === undefined) throw new Error('No such account.')
  if (row.deletedAt !== null) throw new Error('This account was deleted.')

  return claims
}
```

- [ ] **Step 6: Write account creation and binding**

`services/api/src/identity/accounts.ts`:

```ts
import { eq } from 'drizzle-orm'
import type { Db, Tx } from '../db/client.ts'
import { accounts } from '../db/schema.ts'

export interface NewAccount {
  birthdateBand: string
  storefrontRegion: string
}

/**
 * Server assignment, taken once at account creation and IMMUTABLE.
 *
 * solo_execution section 4: assignment is by storefront region at signup and
 * there are no transfers, ever. A player wanting to play elsewhere creates a
 * second unlinked account. That is also what closes the guest-reroll hole -
 * assignment is not something a client can influence, so a guest cannot
 * reroll onto a low-population server to farm its Apex Veins.
 *
 * ONE SERVER AT MILESTONE 1. The mapping is a function rather than a constant
 * so the shape is right when the second one opens; solo_execution section 4
 * defers the population-trigger opening to that point.
 */
export async function assignServer(storefrontRegion: string): Promise<number> {
  const REGION_TO_SERVER: Record<string, number> = { 'us-central1': 1 }
  const serverId = REGION_TO_SERVER[storefrontRegion]
  if (serverId === undefined) {
    throw new Error(`No server serves storefront region ${storefrontRegion}.`)
  }
  return serverId
}

export async function createGuest(db: Db | Tx, a: NewAccount): Promise<{ accountId: string; serverId: number }> {
  const serverId = await assignServer(a.storefrontRegion)
  const [row] = await db.insert(accounts).values({
    appleSub: null,
    birthdateBand: a.birthdateBand,
    homeRegion: a.storefrontRegion,
    serverId,
  }).returning()
  return { accountId: row!.accountId, serverId }
}

/**
 * Binds an Apple sub to an existing account. No data moves, because there is
 * nothing to move - the guest was always a real account.
 */
export async function bindApple(db: Db | Tx, accountId: string, sub: string): Promise<{ accountId: string }> {
  const [existing] = await db.select().from(accounts).where(eq(accounts.appleSub, sub))
  if (existing !== undefined && existing.accountId !== accountId) {
    // NEVER a merge. 6.4: the player is offered the account that already
    // holds this sub. Merging two rosters has no correct answer.
    throw new Error('This Apple ID is already bound to another account.')
  }

  const [row] = await db.update(accounts).set({ appleSub: sub })
    .where(eq(accounts.accountId, accountId)).returning()
  if (row === undefined) throw new Error('No such account.')
  return { accountId: row.accountId }
}
```

- [ ] **Step 7: Give the tests a secret**

`services/api/vitest.config.ts` — add to the `test` block:

```ts
    env: {
      JWT_SECRET: 'test-secret-that-is-at-least-32-characters-long',
    },
```

- [ ] **Step 8: Run the tests to verify they pass**

```bash
pnpm --filter @broodline/api test identity
```

Expected: **10 passed.**

- [ ] **Step 9: Commit**

```bash
pnpm --filter @broodline/api test && pnpm --filter @broodline/api typecheck
```

```bash
git add services/api
git commit -m "feat: Sign in with Apple, the guest path, and session tokens

Apple as primary, guests as real accounts with no credential bound, and
an upgrade that binds the sub to the EXISTING account id - so nothing
migrates and there is no merge to conflict. A sub already held by
another account is refused rather than merged; 6.4 offers the player
that account instead, because merging two rosters has no correct answer.

Three verification tests are the point of the Apple module: a token for
another app, and a token signed by a key Apple does not publish, are
both rejected. algorithms is pinned to RS256 - leaving it open is how a
token with alg none, or an HMAC over the public key, gets accepted.

Refresh is deliberately stateless and the commit should say so. There is
no refresh_tokens table, so a single token cannot be revoked before it
expires; what can be revoked is the ACCOUNT, and deleted_at is checked
on every refresh. That covers the one thing milestone 1 must honour -
the App Store's in-app deletion path has to actually end the session.
Rotation and reuse detection arrive with the first non-TestFlight
players, which is the named trigger.

Server assignment is a function rather than a constant even though one
server exists, because the shape is what the second one needs.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 6: `Broodline.Config.Validate` — the engine's rules on the publish path

Design §2.3. `solo_execution` §5.2 requires publish-time validation to run the engine's wave-composition rules; §6.1 forbids reimplementing a game rule outside the engine. The publish pipeline is TypeScript and the rules are C#, so the pipeline invokes them rather than copying them.

**What this can actually catch today is narrower than it sounds, and the plan says so rather than implying otherwise.** `RaiderType` currently has exactly one value — `Courser = 0`, with `RaiderTypeCount = 1` — so of `WaveDef`'s three assertions:

| Assertion | Can it fire today? |
|---|---|
| `AssertSpawnsOrdered` | **Yes.** Any authored wave can have its timeline out of order |
| `AssertTypeCount` (max 4) | **No.** A wave cannot carry 5 distinct types when 1 exists |
| `AssertNoSharedCounter` | **No.** It needs two distinct types to compare |

The architecture is still the right one, and this is the argument for it: when Skirmisher lands, the other two rules start being enforced at publish **without anyone remembering to mirror them**. Building the TypeScript copy instead would mean the rules silently diverge the moment the engine gains a raider.

**Files:**
- Create: `tools/config-validate/Broodline.Config.Validate.csproj`, `tools/config-validate/Program.cs`, `tools/config-validate/BundleWaves.cs`
- Create: `tests/engine/BundleWavesTests.cs`
- Modify: `Broodline.sln`, `tests/engine/Broodline.Sim.Tests.csproj`

**Interfaces:**
- Consumes: `WaveDef(int, int, int, SpawnEntry[])`, `WaveDef.Validate()`, `WaveCompositionException`, `RaiderType`.
- Produces:
  - `BundleWaves.Parse(string json)` → `IReadOnlyList<WaveDef>`, throwing `WaveCompositionException` on a violation.
  - A CLI: `dotnet run --project tools/config-validate -- <bundle-dir>`, exit `0` valid / `1` invalid, emitting `{"ok":bool,"violations":[...]}` on stdout.

- [ ] **Step 1: Create the project**

`tools/config-validate/Broodline.Config.Validate.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <RootNamespace>Broodline.Config.Validate</RootNamespace>
    <AssemblyName>Broodline.Config.Validate</AssemblyName>
    <Nullable>disable</Nullable>
    <IsPackable>false</IsPackable>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>
  <ItemGroup>
    <!--
      References the engine, which is the entire point: the wave-composition
      rules live in WaveDef and are invoked here rather than reimplemented in
      the publish pipeline. solo_execution 6.1 forbids a game rule existing in
      two languages, and 5.2's validation list would otherwise require exactly
      that.

      This project is NOT subject to the engine's constraints. It uses
      System.IO and System.Text.Json freely - the Cecil scan and
      BannedSymbols.txt target Broodline.Sim, and nothing here ships in the
      client or the simulation.
    -->
    <ProjectReference Include="../../engine/Broodline.Sim.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Write the failing test**

`tests/engine/BundleWavesTests.cs`:

```csharp
using System;
using Xunit;
using Broodline.Sim.Combat;
using Broodline.Config.Validate;

namespace Broodline.Sim.Tests
{
    /// The bundle's wave JSON, mapped onto the engine's own WaveDef and
    /// validated by the engine's own rules. Nothing here reimplements a rule;
    /// these tests pin the MAPPING, which is the only part that is new.
    public class BundleWavesTests
    {
        private const string Wave6 = @"
        [
          { ""id"": 6, ""integrity"": 2, ""laneCount"": 1,
            ""spawns"": [ { ""tick"": 90, ""type"": ""Courser"" } ] }
        ]";

        [Fact]
        public void ParsesAnAuthoredWave()
        {
            var waves = BundleWaves.Parse(Wave6);
            Assert.Single(waves);
            Assert.Equal(6, waves[0].Id);
            Assert.Equal(2, waves[0].Integrity);
            Assert.Equal(1, waves[0].LaneCount);
            Assert.Equal(1, waves[0].Spawns.Length);
            Assert.Equal(90, waves[0].Spawns[0].Tick);
            Assert.Equal(RaiderType.Courser, waves[0].Spawns[0].Type);
        }

        [Fact]
        public void ThrowsWhenTheTimelineIsOutOfOrder()
        {
            // The ONE composition rule that can currently fire. RaiderType has
            // a single value, so the max-four-types and shared-counter rules
            // have nothing to compare until a second raider exists - at which
            // point they start being enforced here with no change to this
            // project, which is the argument for invoking the engine rather
            // than copying it.
            const string outOfOrder = @"
            [
              { ""id"": 6, ""integrity"": 2, ""laneCount"": 1,
                ""spawns"": [ { ""tick"": 120, ""type"": ""Courser"" },
                              { ""tick"": 90,  ""type"": ""Courser"" } ] }
            ]";

            var ex = Assert.Throws<WaveCompositionException>(() => BundleWaves.Parse(outOfOrder));
            Assert.Contains("ordered by tick ascending", ex.Message);
        }

        [Fact]
        public void ThrowsOnARaiderThisEngineDoesNotHave()
        {
            // A bundle naming a raider the engine cannot simulate must fail at
            // PUBLISH rather than at wave load on a player's device.
            const string unknown = @"
            [
              { ""id"": 6, ""integrity"": 2, ""laneCount"": 1,
                ""spawns"": [ { ""tick"": 90, ""type"": ""Skirmisher"" } ] }
            ]";

            var ex = Assert.Throws<WaveCompositionException>(() => BundleWaves.Parse(unknown));
            Assert.Contains("Skirmisher", ex.Message);
        }
    }
}
```

- [ ] **Step 3: Reference the tool from the test project**

`tests/engine/Broodline.Sim.Tests.csproj` — add inside the existing `ProjectReference` `ItemGroup`:

```xml
    <ProjectReference Include="../../tools/config-validate/Broodline.Config.Validate.csproj" />
```

**A test project referencing a tool project, rather than a fourth project existing to test a hundred lines.** The mapping is the only new logic and it belongs beside the engine tests that pin the rules it delegates to.

- [ ] **Step 4: Add both projects to the solution**

```bash
dotnet sln Broodline.sln add tools/config-validate/Broodline.Config.Validate.csproj
```

- [ ] **Step 5: Run the test to verify it fails**

```bash
dotnet test Broodline.sln --nologo --filter "FullyQualifiedName~BundleWavesTests"
```

Expected: **FAIL** to build — `BundleWaves` does not exist.

- [ ] **Step 6: Write the mapping**

`tools/config-validate/BundleWaves.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Text.Json;
using Broodline.Sim.Combat;

namespace Broodline.Config.Validate
{
    /// Maps a bundle's waves.json onto the engine's WaveDef, then hands each
    /// one to the engine's own Validate().
    ///
    /// This file contains NO game rules. It contains a JSON shape and a type
    /// lookup, and everything that could reject a wave is thrown by WaveDef.
    /// If a rule ever appears here, it has been copied out of the engine and
    /// the two will diverge - that is the failure this whole project exists
    /// to prevent.
    public static class BundleWaves
    {
        public static IReadOnlyList<WaveDef> Parse(string json)
        {
            var doc = JsonSerializer.Deserialize<List<WaveJson>>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            });
            if (doc == null) throw new WaveCompositionException("waves.json did not parse as an array.");

            var waves = new List<WaveDef>();
            foreach (var w in doc)
            {
                var spawns = new SpawnEntry[w.Spawns?.Count ?? 0];
                for (int i = 0; i < spawns.Length; i++)
                {
                    var s = w.Spawns[i];
                    // Enum.TryParse rather than Enum.Parse: a bundle naming a
                    // raider this engine does not have is a PUBLISH failure
                    // with a readable message, not an ArgumentException.
                    if (!Enum.TryParse<RaiderType>(s.Type, ignoreCase: false, out var type))
                    {
                        throw new WaveCompositionException(
                            "Wave " + w.Id + " spawn " + i + " names raider type '" + s.Type +
                            "', which this engine does not have.");
                    }
                    spawns[i] = new SpawnEntry { Tick = s.Tick, Type = type };
                }

                var def = new WaveDef(w.Id, w.Integrity, w.LaneCount, spawns);
                // The engine's rules, invoked rather than reimplemented.
                def.Validate();
                waves.Add(def);
            }
            return waves;
        }

        private sealed class WaveJson
        {
            public int Id { get; set; }
            public int Integrity { get; set; }
            public int LaneCount { get; set; }
            public List<SpawnJson> Spawns { get; set; }
        }

        private sealed class SpawnJson
        {
            public int Tick { get; set; }
            public string Type { get; set; }
        }
    }
}
```

- [ ] **Step 7: Write the CLI**

`tools/config-validate/Program.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Broodline.Sim.Combat;

namespace Broodline.Config.Validate
{
    public static class Program
    {
        /// Usage: Broodline.Config.Validate <bundle-dir>
        ///
        /// Emits {"ok":bool,"violations":[string]} on stdout and exits 0 or 1,
        /// so the TypeScript publish step parses a result rather than scraping
        /// a log. A bundle that fails is not published - solo_execution 5.2.
        public static int Main(string[] args)
        {
            if (args.Length != 1)
            {
                Console.Error.WriteLine("usage: Broodline.Config.Validate <bundle-dir>");
                return 2;
            }

            var violations = new List<string>();
            string wavesPath = Path.Combine(args[0], "waves.json");

            if (!File.Exists(wavesPath))
            {
                violations.Add("waves.json is missing from " + args[0]);
            }
            else
            {
                try
                {
                    BundleWaves.Parse(File.ReadAllText(wavesPath));
                }
                catch (WaveCompositionException ex)
                {
                    violations.Add(ex.Message);
                }
                catch (JsonException ex)
                {
                    violations.Add("waves.json is not valid JSON: " + ex.Message);
                }
            }

            Console.WriteLine(JsonSerializer.Serialize(new
            {
                ok = violations.Count == 0,
                violations,
            }));
            return violations.Count == 0 ? 0 : 1;
        }
    }
}
```

- [ ] **Step 8: Run the tests to verify they pass**

```bash
dotnet test Broodline.sln --nologo --filter "FullyQualifiedName~BundleWavesTests"
```

Expected: **3 passed.**

- [ ] **Step 9: Prove the CLI end-to-end**

```bash
mkdir -p /tmp/bundle-probe && cat > /tmp/bundle-probe/waves.json <<'JSON'
[ { "id": 6, "integrity": 2, "laneCount": 1,
    "spawns": [ { "tick": 120, "type": "Courser" }, { "tick": 90, "type": "Courser" } ] } ]
JSON
dotnet run --project tools/config-validate -- /tmp/bundle-probe; echo "exit=$?"
```

Expected: `{"ok":false,"violations":["Spawns must be ordered by tick ascending; ..."]}` and `exit=1`.

```bash
cat > /tmp/bundle-probe/waves.json <<'JSON'
[ { "id": 6, "integrity": 2, "laneCount": 1, "spawns": [ { "tick": 90, "type": "Courser" } ] } ]
JSON
dotnet run --project tools/config-validate -- /tmp/bundle-probe; echo "exit=$?"
```

Expected: `{"ok":true,"violations":[]}` and `exit=0`.

```bash
rm -rf /tmp/bundle-probe
```

- [ ] **Step 10: Run everything and commit**

```bash
dotnet test Broodline.sln --nologo
```

Expected: all pass, three more than Task 1 recorded.

```bash
git add tools/ Broodline.sln tests/engine/BundleWavesTests.cs tests/engine/Broodline.Sim.Tests.csproj
git commit -m "feat: the bundle validator invokes the engine's rules, never copies them

solo_execution 5.2 requires publish-time validation to run the two
wave-composition rules. Those rules are C# and live in WaveDef; the
publish pipeline is TypeScript. Writing them again in the pipeline puts
a game rule in two languages, which is exactly what 6.1 exists to
prevent - it was written about the request path and applies with equal
force here, and nothing in the set said so.

So the pipeline shells out. BundleWaves.cs holds a JSON shape and an
enum lookup and not one rule; everything that can reject a wave is
thrown by WaveDef.Validate().

Worth being honest about what this catches TODAY: RaiderType has one
value, so max-four-types and shared-counter have nothing to compare and
only the spawn-ordering rule can fire. That is the argument for this
architecture rather than against it - when Skirmisher lands, the other
two start being enforced at publish with no change to this project,
where a TypeScript copy would have silently diverged.

The CLI emits JSON and an exit code so the publish step parses a result
instead of scraping a log.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 7: The config bundle pipeline

Design §2.2. Four steps from `solo_execution` §5.2 — author in-repo as JSON, validate at publish time, publish immutably under a version, roll back by naming the previous version.

**Rollback must not be a deploy**, which §5.2 states outright. On Cloud Run an environment-variable change *is* a new revision, so the active version cannot live in the service's environment. It lives in a **mutable pointer object beside the immutable bundles**: the bundles are never overwritten, and rollback rewrites one small file.

**Files:**
- Create: `services/api/src/config/store.ts`, `services/api/src/config/validate.ts`, `services/api/src/config/publish.ts`, `services/api/src/config/bundle.ts`
- Create: `config/bundles/0.1.0/manifest.json`, `waves.json`, `traits.json`, `packs.json`, `starter.json`, `locales/en.json`
- Create: `services/api/test/config.test.ts`, and fixtures under `services/api/test/fixtures/`

**Interfaces:**
- Consumes: the CLI from Task 6, invoked as a child process.
- Produces:
  - `BundleStore` — `hasBundle(v)`, `putBundle(v, dir)`, `readFile(v, name)`, `getPointer()`, `setPointer(v)`.
  - `LocalBundleStore(root: string)` implementing it.
  - `validateBundle(dir: string): Promise<string[]>` — the violations, empty when valid.
  - `publishBundle(store, dir, version): Promise<void>` — validates, then writes; throws if the version exists.
  - `loadBundle(store): Promise<Bundle>` — reads the pointer and returns the active bundle's parsed contents.

- [ ] **Step 1: Author the seed bundle**

`config/bundles/0.1.0/manifest.json`:

```json
{
  "version": "0.1.0",
  "minimumClientVersion": "0.1.0"
}
```

`config/bundles/0.1.0/waves.json`:

```json
[
  { "id": 6, "integrity": 2, "laneCount": 1,
    "spawns": [ { "tick": 90, "type": "Courser" } ] }
]
```

Wave 6 is the one authored wave the engine has — `WaveDef.Wave6()`, integrity 2, one Courser at t=3s, which is tick 90 at 30 Hz.

`config/bundles/0.1.0/traits.json`:

```json
{ "traits": [] }
```

`config/bundles/0.1.0/packs.json`:

```json
{ "packs": [] }
```

**Empty on purpose.** No store packs exist yet — `commerce` is deferred behind its §10 trigger. The monotonic-ladder validator still ships, because §5.2's argument is that a bad bundle reaches every player at once and cannot be recalled by an app update, which argues the validator must exist before the first real content does, not before it is large. Step 6 proves the rule against a fixture that does have packs.

`config/bundles/0.1.0/starter.json`:

```json
{
  "grants": [
    { "currency": "splice_charges", "amount": 3 },
    { "currency": "shards", "amount": 250 }
  ]
}
```

**The starter grant's amounts live in the bundle, not in code** — Task 8 reads them from here. That is deliberate coupling: it makes the starter package tunable without an app update, which is the first thing live-ops will reach for, and it means Task 8's done-when exercises this pipeline as well as the ledger.

`config/bundles/0.1.0/locales/en.json`:

```json
{
  "wave.defeat.title": "The line broke.",
  "wave.victory.title": "Held."
}
```

- [ ] **Step 2: Write the store**

`services/api/src/config/store.ts`:

```ts
import { cp, mkdir, readFile, writeFile } from 'node:fs/promises'
import { existsSync } from 'node:fs'
import { join } from 'node:path'

/**
 * Published bundles are IMMUTABLE and versioned; the POINTER naming the
 * active one is not.
 *
 * That split is what makes solo_execution 5.2's "rollback is a config change,
 * not a deploy" true on Cloud Run, where an env-var change is a new revision.
 * Rollback rewrites one small object; no bundle is ever overwritten, so the
 * version rolled back to is byte-identical to the one that shipped.
 */
export interface BundleStore {
  hasBundle(version: string): Promise<boolean>
  putBundle(version: string, sourceDir: string): Promise<void>
  readFile(version: string, name: string): Promise<string>
  getPointer(): Promise<string>
  setPointer(version: string): Promise<void>
}

export class LocalBundleStore implements BundleStore {
  constructor(private readonly root: string) {}

  private dir(version: string): string { return join(this.root, 'bundles', version) }
  private get pointerPath(): string { return join(this.root, 'bundles', 'current') }

  async hasBundle(version: string): Promise<boolean> {
    return existsSync(this.dir(version))
  }

  async putBundle(version: string, sourceDir: string): Promise<void> {
    if (await this.hasBundle(version)) {
      throw new Error(`Bundle ${version} is already published. Bundles are immutable; publish a new version.`)
    }
    await mkdir(this.dir(version), { recursive: true })
    await cp(sourceDir, this.dir(version), { recursive: true })
  }

  async readFile(version: string, name: string): Promise<string> {
    return readFile(join(this.dir(version), name), 'utf8')
  }

  async getPointer(): Promise<string> {
    return (await readFile(this.pointerPath, 'utf8')).trim()
  }

  async setPointer(version: string): Promise<void> {
    if (!(await this.hasBundle(version))) {
      // Rollback must never name a version that was never published - that
      // turns a recovery into an outage.
      throw new Error(`Cannot point at ${version}: no such published bundle.`)
    }
    await mkdir(join(this.root, 'bundles'), { recursive: true })
    await writeFile(this.pointerPath, version, 'utf8')
  }
}
```

- [ ] **Step 3: Write the validator**

`services/api/src/config/validate.ts`:

```ts
import { execFile } from 'node:child_process'
import { readdir, readFile } from 'node:fs/promises'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { promisify } from 'node:util'

const run = promisify(execFile)

/** The locale every other locale is checked against. */
const REFERENCE_LOCALE = 'en'

/**
 * Publish-time validation. solo_execution 5.2: a bundle that fails is not
 * published, and that is the entire safety model - a bad bundle is shipped to
 * every player at once and cannot be recalled by an app update.
 *
 * Three checks, and only one of them lives here in full. The wave rules are
 * the engine's and are invoked, never copied - see tools/config-validate.
 */
export async function validateBundle(dir: string): Promise<string[]> {
  const violations: string[] = []
  violations.push(...(await validateWaves(dir)))
  violations.push(...(await validatePackLadder(dir)))
  violations.push(...(await validateLocales(dir)))
  return violations
}

async function validateWaves(dir: string): Promise<string[]> {
  try {
    const { stdout } = await run('dotnet', [
      // NO --nologo. It is not a `dotnet run` option here, so it is forwarded
      // to the app, which then sees two arguments, prints its usage line to
      // stderr and exits 2. Verified: with it, every bundle fails validation
      // with "wave validator could not run"; without it, exit 0 and clean
      // JSON. Fails closed either way, but for entirely the wrong reason.
      'run', '--project', 'tools/config-validate', '--', dir,
    ], {
      cwd: repoRoot(),
      // Both of these keep the CLI's contract - ONE json line on stdout -
      // true on a machine that has never run dotnet before. The first-use
      // telemetry banner goes to STDOUT ahead of program output, which would
      // put unparseable text where the JSON is expected. GitHub's setup-dotnet
      // happens to set these already; a developer's laptop and a fresh
      // container do not, and relying on one CI's defaults is not a contract.
      env: { ...process.env, DOTNET_NOLOGO: '1', DOTNET_CLI_TELEMETRY_OPTOUT: '1' },
    })
    return (JSON.parse(lastJsonLine(stdout)) as { violations: string[] }).violations
  } catch (err) {
    // Exit 1 means violations, and execFile rejects on a non-zero exit - the
    // payload is still on stdout, so a rejection is not automatically a
    // failure of the validator itself.
    const e = err as { stdout?: string }
    if (typeof e.stdout === 'string' && e.stdout.includes('"violations"')) {
      return (JSON.parse(lastJsonLine(e.stdout)) as { violations: string[] }).violations
    }
    return [`wave validator could not run: ${String(err)}`]
  }
}

/**
 * `dotnet run` prints MSBuild output before the program's own stdout on any
 * run that rebuilds, so the JSON is not reliably the first line. Take the last
 * line that starts with `{`. Verified against a real invocation rather than
 * assumed — the CLI's contract is exit code plus one JSON line on stdout, with
 * usage errors going to stderr.
 */
function lastJsonLine(stdout: string): string {
  const lines = stdout.trim().split('\n')
  for (let i = lines.length - 1; i >= 0; i--) {
    const line = lines[i]!.trim()
    if (line.startsWith('{')) return line
  }
  throw new Error(`validator produced no JSON:\n${stdout}`)
}

function repoRoot(): string {
  // services/api/src/config -> repo root.
  //
  // fileURLToPath, NOT .pathname. A file:// URL percent-encodes, so on a
  // checkout under a directory with a space - "Personal Development" on the
  // machine this was written on - .pathname yields "Personal%20Development"
  // and every path built from it is wrong. migrate.ts already does this
  // correctly; follow it.
  return fileURLToPath(new URL('../../../../', import.meta.url))
}

interface Pack { id: string; priceUsdCents: number; value: number }

/**
 * broodline_monetization.md's monotonic ladder: value per dollar must never
 * DECREASE as pack size rises. solo_execution 5.2 notes this was broken once
 * by hand, which is the argument for the machine owning it.
 *
 * A pure data property with no game semantics, so it belongs on this side of
 * the language boundary.
 */
async function validatePackLadder(dir: string): Promise<string[]> {
  const raw = await readFile(join(dir, 'packs.json'), 'utf8').catch(() => null)
  if (raw === null) return ['packs.json is missing.']

  const packs = (JSON.parse(raw) as { packs: Pack[] }).packs
  const ladder = [...packs].sort((a, b) => a.priceUsdCents - b.priceUsdCents)

  const violations: string[] = []
  for (let i = 1; i < ladder.length; i++) {
    const prev = ladder[i - 1]!
    const curr = ladder[i]!
    const prevRate = prev.value / prev.priceUsdCents
    const currRate = curr.value / curr.priceUsdCents
    if (currRate < prevRate) {
      violations.push(
        `Pack ladder is not monotonic: '${curr.id}' gives ${currRate.toFixed(4)} per cent ` +
        `but the cheaper '${prev.id}' gives ${prevRate.toFixed(4)}.`)
    }
  }
  return violations
}

/**
 * Every key present in the reference locale must exist in every other locale
 * in the bundle - broodline_localization.md. A missing key renders as a raw
 * identifier on a player's screen.
 */
async function validateLocales(dir: string): Promise<string[]> {
  const localeDir = join(dir, 'locales')
  const files = await readdir(localeDir).catch(() => null)
  if (files === null) return ['locales/ is missing.']

  const reference = `${REFERENCE_LOCALE}.json`
  if (!files.includes(reference)) return [`locales/${reference} is missing.`]

  const refKeys = Object.keys(JSON.parse(await readFile(join(localeDir, reference), 'utf8')) as object)
  const violations: string[] = []

  for (const file of files.filter((f) => f.endsWith('.json') && f !== reference)) {
    const keys = new Set(Object.keys(JSON.parse(await readFile(join(localeDir, file), 'utf8')) as object))
    const missing = refKeys.filter((k) => !keys.has(k))
    if (missing.length > 0) {
      violations.push(`locales/${file} is missing ${missing.length} key(s): ${missing.slice(0, 5).join(', ')}`)
    }
  }
  return violations
}
```

- [ ] **Step 4: Write publish and load**

`services/api/src/config/publish.ts`:

```ts
import { readFile } from 'node:fs/promises'
import { join } from 'node:path'
import type { BundleStore } from './store.ts'
import { validateBundle } from './validate.ts'

export class BundleInvalidError extends Error {
  constructor(readonly violations: string[]) {
    super(`Bundle failed validation:\n  ${violations.join('\n  ')}`)
    this.name = 'BundleInvalidError'
  }
}

/**
 * Validate, then publish immutably. A bundle that fails validation is NOT
 * published - solo_execution 5.2, and the whole safety model.
 *
 * Publishing does NOT move the pointer. Making a bundle live is a separate,
 * deliberate act, so a publish can be staged and verified before any client
 * is told about it.
 */
export async function publishBundle(store: BundleStore, dir: string, version: string): Promise<void> {
  const manifest = JSON.parse(await readFile(join(dir, 'manifest.json'), 'utf8')) as { version: string }
  if (manifest.version !== version) {
    throw new BundleInvalidError([
      `manifest.json says version '${manifest.version}' but this is being published as '${version}'.`,
    ])
  }

  const violations = await validateBundle(dir)
  if (violations.length > 0) throw new BundleInvalidError(violations)

  await store.putBundle(version, dir)
}
```

`services/api/src/config/bundle.ts`:

```ts
import type { BundleStore } from './store.ts'
import type { Currency } from '../money/ledger.ts'

export interface StarterGrant { currency: Currency; amount: number }

export interface Bundle {
  version: string
  minimumClientVersion: string
  starterGrants: StarterGrant[]
}

let cached: Bundle | undefined

/**
 * Reads the pointer and returns the active bundle.
 *
 * Cached per process: the pointer changes on a rollback, and an instance that
 * has not restarted keeps serving the old version until it does. That is
 * acceptable because Cloud Run instances are short-lived, and the alternative
 * - reading a remote object on every /v1/sync - puts a network call on the
 * p99-300ms cold-start path.
 */
export async function loadBundle(store: BundleStore, opts: { refresh?: boolean } = {}): Promise<Bundle> {
  if (cached !== undefined && opts.refresh !== true) return cached

  const version = await store.getPointer()
  const manifest = JSON.parse(await store.readFile(version, 'manifest.json')) as {
    version: string; minimumClientVersion: string
  }
  const starter = JSON.parse(await store.readFile(version, 'starter.json')) as { grants: StarterGrant[] }

  cached = {
    version: manifest.version,
    minimumClientVersion: manifest.minimumClientVersion,
    starterGrants: starter.grants,
  }
  return cached
}

/** Tests only. */
export function clearBundleCache(): void { cached = undefined }
```

- [ ] **Step 5: Write the fixtures**

`services/api/test/fixtures/bad-ladder/manifest.json`:

```json
{ "version": "9.9.9", "minimumClientVersion": "0.1.0" }
```

`services/api/test/fixtures/bad-ladder/waves.json`:

```json
[ { "id": 6, "integrity": 2, "laneCount": 1, "spawns": [ { "tick": 90, "type": "Courser" } ] } ]
```

`services/api/test/fixtures/bad-ladder/packs.json`:

```json
{
  "packs": [
    { "id": "small",  "priceUsdCents": 99,   "value": 100 },
    { "id": "medium", "priceUsdCents": 499,  "value": 400 }
  ]
}
```

`small` gives 1.010 per cent, `medium` gives 0.802 — the ladder goes backwards, which is exactly the mistake §5.2 says was made once by hand.

`services/api/test/fixtures/bad-ladder/starter.json`:

```json
{ "grants": [] }
```

`services/api/test/fixtures/bad-ladder/locales/en.json`:

```json
{ "a": "A" }
```

`services/api/test/fixtures/bad-waves/` — copy every file from `bad-ladder` except use a valid `packs.json` (`{"packs": []}`) and this `waves.json`:

```json
[ { "id": 6, "integrity": 2, "laneCount": 1,
    "spawns": [ { "tick": 120, "type": "Courser" }, { "tick": 90, "type": "Courser" } ] } ]
```

`services/api/test/fixtures/missing-locale-key/` — copy `bad-ladder` with `{"packs": []}`, valid waves, and a second locale missing a key:

`locales/en.json`: `{ "a": "A", "b": "B" }`
`locales/ja.json`: `{ "a": "あ" }`

- [ ] **Step 6: Write the tests**

`services/api/test/config.test.ts`:

```ts
import { mkdtemp, rm } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { afterEach, beforeEach, describe, expect, it } from 'vitest'
import { clearBundleCache, loadBundle } from '../src/config/bundle.ts'
import { BundleInvalidError, publishBundle } from '../src/config/publish.ts'
import { LocalBundleStore } from '../src/config/store.ts'
import { validateBundle } from '../src/config/validate.ts'

// fileURLToPath, not .pathname - a path containing a space would arrive
// percent-encoded and every join below would miss.
const REPO = fileURLToPath(new URL('../../../', import.meta.url))
const SEED = join(REPO, 'config/bundles/0.1.0')
const FIX = (name: string) => join(REPO, 'services/api/test/fixtures', name)

let root: string
let store: LocalBundleStore

beforeEach(async () => {
  root = await mkdtemp(join(tmpdir(), 'broodline-bundles-'))
  store = new LocalBundleStore(root)
  clearBundleCache()
})
afterEach(async () => { await rm(root, { recursive: true, force: true }) })

describe('validation', () => {
  it('passes the authored seed bundle', async () => {
    expect(await validateBundle(SEED)).toEqual([])
  }, 120_000)

  it('catches a wave the ENGINE rejects, not a rule reimplemented here', async () => {
    const v = await validateBundle(FIX('bad-waves'))
    expect(v.join(' ')).toMatch(/ordered by tick ascending/)
  }, 120_000)

  it('catches a pack ladder that goes backwards', async () => {
    const v = await validateBundle(FIX('bad-ladder'))
    expect(v.join(' ')).toMatch(/not monotonic/)
  }, 120_000)

  it('catches a locale missing a key the reference has', async () => {
    const v = await validateBundle(FIX('missing-locale-key'))
    expect(v.join(' ')).toMatch(/ja\.json is missing 1 key/)
  }, 120_000)
})

describe('publish', () => {
  it('publishes a valid bundle and leaves the pointer alone', async () => {
    await publishBundle(store, SEED, '0.1.0')
    expect(await store.hasBundle('0.1.0')).toBe(true)
    // Publishing does not make a bundle live. That is a second, deliberate act.
    await expect(store.getPointer()).rejects.toThrow()
  }, 120_000)

  it('refuses to publish an invalid bundle at all', async () => {
    await expect(publishBundle(store, FIX('bad-ladder'), '9.9.9')).rejects.toBeInstanceOf(BundleInvalidError)
    expect(await store.hasBundle('9.9.9')).toBe(false)
  }, 120_000)

  it('refuses to overwrite a published version', async () => {
    await publishBundle(store, SEED, '0.1.0')
    await expect(publishBundle(store, SEED, '0.1.0')).rejects.toThrow(/immutable/i)
  }, 120_000)

  it('refuses a manifest whose version disagrees with the publish target', async () => {
    await expect(publishBundle(store, SEED, '0.2.0')).rejects.toThrow(/manifest\.json says version/)
  }, 120_000)
})

describe('the pointer', () => {
  it('makes a bundle live, and rollback names a previous version', async () => {
    await publishBundle(store, SEED, '0.1.0')
    await store.setPointer('0.1.0')

    const bundle = await loadBundle(store)
    expect(bundle.version).toBe('0.1.0')
    expect(bundle.minimumClientVersion).toBe('0.1.0')
    // The starter grant's amounts come from the bundle, not from code.
    expect(bundle.starterGrants).toEqual([
      { currency: 'splice_charges', amount: 3 },
      { currency: 'shards', amount: 250 },
    ])
  }, 120_000)

  it('refuses to point at a version that was never published', async () => {
    // A rollback naming a missing version turns a recovery into an outage.
    await expect(store.setPointer('0.0.9')).rejects.toThrow(/no such published bundle/)
  })
})
```

- [ ] **Step 7: Run them**

```bash
pnpm --filter @broodline/api test config
```

Expected: **11 passed.** The wave checks shell out to `dotnet run`, which is why they carry a long timeout; the first invocation builds the tool.

- [ ] **Step 8: Commit**

```bash
pnpm --filter @broodline/api test && pnpm --filter @broodline/api typecheck
```

```bash
git add config/ services/api
git commit -m "feat: the config bundle pipeline, validator first

Four steps from solo_execution 5.2: author in-repo as JSON, validate at
publish, publish immutably under a version, roll back by naming the
previous one.

Rollback must not be a deploy, which 5.2 states outright - and on Cloud
Run an env-var change IS a new revision, so the active version cannot
live in the service environment. It lives in a mutable pointer object
beside the immutable bundles. The bundles are never overwritten, so the
version rolled back to is byte-identical to the one that shipped.

Publishing deliberately does not move the pointer. Making a bundle live
is a second act, so a publish can be staged and checked first.

packs.json ships empty because no store packs exist yet, and the
monotonic-ladder validator ships anyway. 5.2's argument is that a bad
bundle reaches every player at once and cannot be recalled by an app
update, which says the validator must exist before the first real
content does - not before it is large. A fixture proves the rule against
packs that actually go backwards.

The starter grant's amounts live in starter.json rather than in code, so
the first thing live-ops will want to tune does not need an app update -
and so Task 8's done-when exercises this pipeline too.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 8: The starter grant

**Phase 4's mutating path, and the half of the done-when that §8.2 was missing.** Design §2.1.

**Files:**
- Create: `services/api/src/routes/account.ts`, `services/api/src/http/hash.ts`, `services/api/src/http/auth.ts`
- Create: `services/api/test/account.test.ts`
- Modify: `services/api/src/app.ts`

**Interfaces:**
- Consumes: `withIdempotency`, `credit`, `createGuest`, `bindApple`, `verifyAppleToken`, `issueAccessToken`, `issueRefreshToken`, `loadBundle`.
- Produces: `POST /v1/account` → `{ accountId, playerId, serverId, accessToken, refreshToken, balances }`.

- [ ] **Step 1: Write the request hash helper**

`services/api/src/http/hash.ts`:

```ts
import { createHash } from 'node:crypto'

/**
 * The hash an idempotency key is bound to.
 *
 * solo_execution 6.3: if the key matches but the request hash differs, 422 -
 * that is a client bug, and returning another request's response would be
 * worse than failing.
 *
 * Stable key order, so two structurally identical bodies that serialised
 * their fields differently are not treated as different requests.
 */
export function hashRequest(body: unknown): string {
  return createHash('sha256').update(stableStringify(body)).digest('hex')
}

function stableStringify(v: unknown): string {
  if (v === null || typeof v !== 'object') return JSON.stringify(v) ?? 'null'
  if (Array.isArray(v)) return `[${v.map(stableStringify).join(',')}]`
  const entries = Object.entries(v as Record<string, unknown>).sort(([a], [b]) => (a < b ? -1 : 1))
  return `{${entries.map(([k, val]) => `${JSON.stringify(k)}:${stableStringify(val)}`).join(',')}}`
}
```

- [ ] **Step 2: Write the failing test**

`services/api/test/account.test.ts`:

```ts
import { eq } from 'drizzle-orm'
import { mkdtemp, rm } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { afterAll, beforeAll, describe, expect, it } from 'vitest'
import { createApp } from '../src/app.ts'
import { clearBundleCache } from '../src/config/bundle.ts'
import { publishBundle } from '../src/config/publish.ts'
import { LocalBundleStore } from '../src/config/store.ts'
import { withServer } from '../src/db/client.ts'
import { accounts, ledger, players, servers, wallets } from '../src/db/schema.ts'
import { startTestDb, type TestDb } from './harness.ts'

// fileURLToPath, not .pathname - a path containing a space would arrive
// percent-encoded and every join below would miss.
const REPO = fileURLToPath(new URL('../../../', import.meta.url))
const SEED = join(REPO, 'config/bundles/0.1.0')

let t: TestDb
let app: ReturnType<typeof createApp>
let bundleRoot: string

beforeAll(async () => {
  t = await startTestDb()
  await t.ownerDb.insert(servers).values({
    serverId: 1, region: 'us-central1', state: 'open', tickDayOfWeek: 0, tickMinuteOfDay: 1200,
  })

  bundleRoot = await mkdtemp(join(tmpdir(), 'broodline-acct-'))
  const store = new LocalBundleStore(bundleRoot)
  await publishBundle(store, SEED, '0.1.0')
  await store.setPointer('0.1.0')
  clearBundleCache()

  app = createApp({ db: t.db, bundleStore: store })
}, 240_000)

afterAll(async () => {
  await t?.stop()
  await rm(bundleRoot, { recursive: true, force: true })
})

function create(key: string, body: Record<string, unknown> = { birthdateBand: 'adult', storefrontRegion: 'us-central1' }) {
  return app.request('/v1/account', {
    method: 'POST',
    headers: { 'content-type': 'application/json', 'idempotency-key': key },
    body: JSON.stringify(body),
  })
}

describe('POST /v1/account', () => {
  it('creates an account, a player, wallets and their ledger rows', async () => {
    const res = await create('k-1')
    expect(res.status).toBe(200)

    const body = await res.json() as {
      accountId: string; playerId: string; serverId: number
      accessToken: string; refreshToken: string
      balances: Record<string, number>
    }

    expect(body.serverId).toBe(1)
    expect(body.accessToken).toBeTruthy()
    // The amounts come from starter.json, not from a constant in the handler.
    expect(body.balances).toEqual({ splice_charges: 3, shards: 250 })

    const rows = await withServer(t.db, 1, (tx) =>
      tx.select().from(ledger).where(eq(ledger.playerId, body.playerId)))
    expect(rows).toHaveLength(2)
    expect(rows.every((r) => r.reasonCode === 'STARTER_GRANT')).toBe(true)
    // The key is recorded ON the ledger row, so a grant is traceable back to
    // the request that caused it.
    expect(rows.every((r) => r.idempotencyKey === 'k-1')).toBe(true)
  })

  it('grants exactly once when the same key is replayed', async () => {
    const first = await create('k-2')
    const firstBody = await first.json() as { accountId: string; playerId: string }

    const second = await create('k-2')
    expect(second.status).toBe(200)
    const secondBody = await second.json() as { accountId: string; playerId: string }

    // The identical response, not a second account.
    expect(secondBody.accountId).toBe(firstBody.accountId)

    const allAccounts = await t.ownerDb.select().from(accounts)
      .where(eq(accounts.accountId, firstBody.accountId))
    expect(allAccounts).toHaveLength(1)

    const rows = await withServer(t.db, 1, (tx) =>
      tx.select().from(ledger).where(eq(ledger.playerId, firstBody.playerId)))
    expect(rows).toHaveLength(2)
  })

  it('grants exactly once under eight simultaneous retries', async () => {
    // The network failure this actually models: a player on a bad connection
    // whose client retries while the first request is still in flight.
    const responses = await Promise.all(Array.from({ length: 8 }, () => create('k-3')))
    expect(responses.every((r) => r.status === 200)).toBe(true)

    const bodies = await Promise.all(responses.map((r) => r.json() as Promise<{ playerId: string }>))
    const playerIds = new Set(bodies.map((b) => b.playerId))
    expect(playerIds.size).toBe(1)

    const playerId = [...playerIds][0]!
    const walletRows = await withServer(t.db, 1, (tx) =>
      tx.select().from(wallets).where(eq(wallets.playerId, playerId)))
    expect(walletRows.find((w) => w.currency === 'shards')!.balance).toBe(250)

    const playerRows = await withServer(t.db, 1, (tx) =>
      tx.select().from(players).where(eq(players.playerId, playerId)))
    expect(playerRows).toHaveLength(1)
  })

  it('returns 422 when a key is reused for a different body', async () => {
    await create('k-4')
    const res = await create('k-4', { birthdateBand: 'teen', storefrontRegion: 'us-central1' })
    expect(res.status).toBe(422)
    expect(await res.json()).toMatchObject({ code: 'idempotency_key_reused' })
  })

  it('requires an idempotency key', async () => {
    const res = await app.request('/v1/account', {
      method: 'POST',
      headers: { 'content-type': 'application/json' },
      body: JSON.stringify({ birthdateBand: 'adult', storefrontRegion: 'us-central1' }),
    })
    expect(res.status).toBe(400)
    expect(await res.json()).toMatchObject({ code: 'invalid_request' })
  })
})
```

- [ ] **Step 3: Run it to verify it fails**

```bash
pnpm --filter @broodline/api test account
```

Expected: **FAIL** — `createApp` takes no arguments and `/v1/account` does not exist.

- [ ] **Step 4: Give the app its dependencies**

`services/api/src/app.ts` — replace:

```ts
import { Hono } from 'hono'
import type { BundleStore } from './config/store.ts'
import type { Db } from './db/client.ts'
import { fail } from './http/errors.ts'
import { registerAccountRoutes } from './routes/account.ts'

export interface Deps {
  db: Db
  bundleStore: BundleStore
}

/**
 * Dependencies are passed in rather than imported, so a test drives the real
 * app against a real Postgres and a real bundle store without a module mock.
 * Nothing in this service reaches for a singleton connection.
 */
export function createApp(deps: Deps): Hono {
  const app = new Hono()

  app.get('/healthz', (c) => c.json({ ok: true }))

  registerAccountRoutes(app, deps)

  app.notFound(() => fail('not_found', 'No such route.'))
  app.onError((err) => {
    console.error('unhandled', err)
    return fail('internal', 'Something went wrong.')
  })

  return app
}
```

`services/api/test/health.test.ts` — the two existing tests now need deps. Replace `createApp()` with a minimal stub, since neither test touches the database:

```ts
import { describe, expect, it } from 'vitest'
import { createApp } from '../src/app.ts'
import type { Db } from '../src/db/client.ts'
import type { BundleStore } from '../src/config/store.ts'

const deps = { db: {} as Db, bundleStore: {} as BundleStore }

describe('the app', () => {
  it('answers the health check Cloud Run probes', async () => {
    const res = await createApp(deps).request('/healthz')
    expect(res.status).toBe(200)
    expect(await res.json()).toEqual({ ok: true })
  })

  it('returns the error envelope, not a bare string', async () => {
    const res = await createApp(deps).request('/v1/nope')
    expect(res.status).toBe(404)
    expect(await res.json()).toMatchObject({ code: 'not_found' })
  })
})
```

`services/api/src/index.ts` — build the real dependencies:

```ts
import { serve } from '@hono/node-server'
import { createApp } from './app.ts'
import { GcsBundleStore } from './config/gcs-store.ts'
import { createDb, createPool } from './db/client.ts'

const port = Number(process.env.PORT ?? 8080)

const url = process.env.DATABASE_URL
if (!url) throw new Error('DATABASE_URL must be set.')
const bucket = process.env.CONFIG_BUCKET
if (!bucket) throw new Error('CONFIG_BUCKET must be set.')

const app = createApp({
  db: createDb(createPool(url)),
  bundleStore: new GcsBundleStore(bucket),
})

serve({ fetch: app.fetch, port }, (info) => {
  console.log(JSON.stringify({ msg: 'listening', port: info.port }))
})
```

**`GcsBundleStore` does not exist yet** — it arrives at Task 11 with Terraform. `index.ts` will not typecheck until then, which is deliberate: the file that needs the cloud is the only file that needs the cloud. Add it to `tsconfig.json`'s `exclude` for now:

```json
  "exclude": ["src/index.ts"]
```

and remove the exclusion at Task 11.

- [ ] **Step 5: Write the route**

`services/api/src/routes/account.ts`:

```ts
import type { Hono } from 'hono'
import type { Deps } from '../app.ts'
import { loadBundle } from '../config/bundle.ts'
import { fail } from '../http/errors.ts'
import { hashRequest } from '../http/hash.ts'
import { assignServer } from '../identity/accounts.ts'
import { issueAccessToken, issueRefreshToken } from '../identity/jwt.ts'
import { IdempotencyMismatchError, withIdempotency } from '../money/idempotency.ts'
import { credit } from '../money/ledger.ts'
import { accounts, players } from '../db/schema.ts'

interface CreateBody {
  birthdateBand: string
  storefrontRegion: string
}

function parse(raw: unknown): CreateBody | null {
  if (typeof raw !== 'object' || raw === null) return null
  const b = raw as Record<string, unknown>
  if (typeof b.birthdateBand !== 'string' || typeof b.storefrontRegion !== 'string') return null
  return { birthdateBand: b.birthdateBand, storefrontRegion: b.storefrontRegion }
}

export function registerAccountRoutes(app: Hono, deps: Deps): void {
  app.post('/v1/account', async (c) => {
    const key = c.req.header('idempotency-key')
    if (!key) {
      // Not optional. client_architecture section 8 has the client generate
      // the key when the action is TAKEN, so a retry after a kill, a crash or
      // three days offline carries the identical key.
      return fail('invalid_request', 'An Idempotency-Key header is required.')
    }

    const raw = await c.req.json().catch(() => null)
    const body = parse(raw)
    if (body === null) {
      return fail('invalid_request', 'birthdateBand and storefrontRegion are required.')
    }

    let serverId: number
    try {
      serverId = await assignServer(body.storefrontRegion)
    } catch {
      return fail('invalid_request', `No server serves storefront region ${body.storefrontRegion}.`)
    }

    const bundle = await loadBundle(deps.bundleStore)

    try {
      const result = await withIdempotency(deps.db, serverId, key, hashRequest(body), async (tx) => {
        // One transaction, and the statement order is normative. The key was
        // inserted by withIdempotency before any of this ran.
        const [account] = await tx.insert(accounts).values({
          appleSub: null,
          birthdateBand: body.birthdateBand,
          homeRegion: body.storefrontRegion,
          serverId,
        }).returning()

        const [player] = await tx.insert(players).values({
          serverId, accountId: account!.accountId,
        }).returning()

        const balances: Record<string, number> = {}
        for (const grant of bundle.starterGrants) {
          balances[grant.currency] = await credit(tx, {
            serverId,
            playerId: player!.playerId,
            currency: grant.currency,
            delta: grant.amount,
            reasonCode: 'STARTER_GRANT',
            idempotencyKey: key,
          })
        }

        return {
          accountId: account!.accountId,
          playerId: player!.playerId,
          serverId,
          balances,
        }
      })

      // Tokens are minted OUTSIDE the idempotent body and are not stored in
      // the key's response. A replayed creation should still get a usable,
      // freshly-dated session rather than one expiring on the original clock.
      const claims = { accountId: result.body.accountId, serverId }
      return c.json({
        ...result.body,
        accessToken: await issueAccessToken(claims),
        refreshToken: await issueRefreshToken(claims),
      })
    } catch (err) {
      if (err instanceof IdempotencyMismatchError) {
        return fail('idempotency_key_reused', 'This Idempotency-Key was used for a different request.')
      }
      throw err
    }
  })
}
```

- [ ] **Step 6: Run the tests to verify they pass**

```bash
pnpm --filter @broodline/api test account
```

Expected: **5 passed.** The third is the one that matters: eight simultaneous creations, one player, 250 shards.

- [ ] **Step 7: Write the failing deletion test**

`solo_execution` §6.4: *"Deletion is a product requirement, not a nicety. The App Store requires an in-app path to account deletion for any app that supports account creation."* Task 5 built the `deleted_at` check on refresh; nothing can yet set the column.

`services/api/test/account.test.ts` — append:

```ts
describe('DELETE /v1/account', () => {
  it('disables the account immediately and ends the session', async () => {
    const res = await create('k-del')
    const body = await res.json() as { accountId: string; accessToken: string; refreshToken: string }

    const del = await app.request('/v1/account', {
      method: 'DELETE',
      headers: { authorization: `Bearer ${body.accessToken}` },
    })
    expect(del.status).toBe(200)

    const [row] = await t.ownerDb.select().from(accounts)
      .where(eq(accounts.accountId, body.accountId))
    expect(row!.deletedAt).not.toBeNull()

    // The refresh path is what actually ends the session - Task 5 checks
    // deleted_at on every redemption, which is the whole reason refresh is
    // allowed to be stateless.
    await expect(redeemRefreshToken(t.ownerDb, body.refreshToken)).rejects.toThrow(/deleted/i)
  })

  it('keeps the ledger rows, because they are a financial record', async () => {
    const res = await create('k-del-2')
    const body = await res.json() as { accountId: string; playerId: string; accessToken: string }

    await app.request('/v1/account', {
      method: 'DELETE', headers: { authorization: `Bearer ${body.accessToken}` },
    })

    // 6.4: player-visible data is removed on a timer, but the ledger is
    // RETAINED in pseudonymised form - it is a financial record, and
    // store_iap's refund path depends on it.
    const rows = await withServer(t.db, 1, (tx) =>
      tx.select().from(ledger).where(eq(ledger.playerId, body.playerId)))
    expect(rows).toHaveLength(2)
  })

  it('refuses an unauthenticated deletion', async () => {
    expect((await app.request('/v1/account', { method: 'DELETE' })).status).toBe(401)
  })
})
```

Add the two imports this needs at the top of the file:

```ts
import { redeemRefreshToken } from '../src/identity/jwt.ts'
import { HttpError } from '../src/http/auth.ts'
```

- [ ] **Step 8: Run it to verify it fails**

```bash
pnpm --filter @broodline/api test account
```

Expected: **FAIL** — `DELETE /v1/account` returns 404.

- [ ] **Step 9: Write the auth guard**

Needed here rather than at Task 9, because deletion is the first authenticated route.

`services/api/src/http/auth.ts`:

```ts
import type { Context } from 'hono'
import { fail } from './errors.ts'
import { verifyAccessToken, type SessionClaims } from '../identity/jwt.ts'

/**
 * Thrown rather than returned, so a handler cannot accidentally continue
 * past a failed check by ignoring a return value.
 */
export class HttpError extends Error {
  constructor(readonly response: Response) {
    super('http error')
    this.name = 'HttpError'
  }
}

export async function requireSession(c: Context): Promise<SessionClaims> {
  const header = c.req.header('authorization')
  if (!header?.startsWith('Bearer ')) {
    throw new HttpError(fail('unauthorized', 'A bearer token is required.'))
  }
  try {
    return await verifyAccessToken(header.slice('Bearer '.length))
  } catch {
    // Never say WHY. Distinguishing "expired" from "bad signature" to an
    // unauthenticated caller is free information.
    throw new HttpError(fail('unauthorized', 'That token is not valid.'))
  }
}

/** Semver-ish compare, sufficient for a three-part version floor. Used by Task 9. */
export function isBelow(version: string, floor: string): boolean {
  const v = version.split('.').map(Number)
  const f = floor.split('.').map(Number)
  for (let i = 0; i < 3; i++) {
    const a = v[i] ?? 0
    const b = f[i] ?? 0
    if (a !== b) return a < b
  }
  return false
}
```

- [ ] **Step 10: Write the deletion route**

`services/api/src/routes/account.ts` — add inside `registerAccountRoutes`, and add `eq` to the `drizzle-orm` import plus `HttpError, requireSession` from `../http/auth.ts`:

```ts
  app.delete('/v1/account', async (c) => {
    let session
    try {
      session = await requireSession(c)
    } catch (err) {
      if (err instanceof HttpError) return err.response
      throw err
    }

    // A SOFT delete plus a scheduled purge - solo_execution 6.4. The account
    // is disabled immediately, which is what redeemRefreshToken checks, and
    // player-visible data is removed on a timer.
    //
    // THE LEDGER IS NOT TOUCHED. It is a financial record and store_iap's
    // refund path depends on it; 6.4 retains it pseudonymised. Creature
    // tombstones survive for the same kind of reason - a deleted player's
    // descendants still render in other players' lineage views, which is
    // exactly why tombstones are minimal.
    //
    // THE PURGE JOB IS NOT BUILT HERE. There is no player-visible data beyond
    // the wallet yet, and a timer with nothing to delete is a job that cannot
    // be tested. It arrives with the first phase that stores creatures.
    await deps.db.update(accounts)
      .set({ deletedAt: new Date() })
      .where(eq(accounts.accountId, session.accountId))

    return c.json({ deleted: true })
  })
```

- [ ] **Step 11: Run the tests to verify they pass**

```bash
pnpm --filter @broodline/api test account
```

Expected: **8 passed.**

- [ ] **Step 12: Run everything and commit**

```bash
pnpm --filter @broodline/api test && pnpm --filter @broodline/api typecheck
```

Expected: green — health 2, isolation 7, ledger 4, idempotency 3, identity 10, config 11, account 8.

```bash
git add services/api
git commit -m "feat: the starter grant, and the half of the done-when 8.2 was missing

solo_execution 8.2 names ledger, wallets and idempotency among Phase 4's
deliverables and then sets the done-when at a GET. Built to that
criterion all three are dormant code, first executed in Phase 5 against
a grant path that is itself new - and 5.3 is explicit that games adding
a ledger late never fully recover the first six months.

Account creation is the mutating path because it needs no new schema. It
writes the first wallet row regardless, and 5.3 requires every currency
mutation to carry a ledger row in the same transaction. The alternative
candidate, harvest claim, would have meant inventing the node tables
Phase 6 owns.

It also puts the idempotency key on the most-retried request in the
application: first launch, cold network, a player who force-quits the
spinner and taps again. The gate here is eight simultaneous creations
producing one player and 250 shards.

Tokens are minted outside the idempotent body deliberately. A replayed
creation gets a freshly-dated session rather than one expiring on the
original request's clock, which would hand a retrying client a token
already most of the way through its life.

The grant amounts come from starter.json. The handler holds no numbers.

Deletion ships in the same task because 6.4 calls it a product
requirement rather than a nicety: the App Store requires an in-app
deletion path for any app that supports account creation. It is a soft
delete, and the ledger is deliberately untouched - it is a financial
record and store_iap's refund path depends on it. The purge job is not
built, because there is no player-visible data beyond the wallet yet and
a timer with nothing to delete cannot be tested.

index.ts is excluded from typecheck until Task 11 brings GcsBundleStore
- the file that needs the cloud is the only file that needs the cloud.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 9: `GET /v1/sync`

The one cold-start call, per `solo_execution` §6.2. Settled balances, charges and their regen timers, active timers, campaign progress, the config bundle version and `minimumClientVersion`.

**Files:**
- Create: `services/api/src/routes/sync.ts`
- Create: `services/api/test/sync.test.ts`
- Modify: `services/api/src/app.ts`

**Interfaces:**
- Consumes: `verifyAccessToken`, `loadBundle`, `withServer`, the `wallets`/`players`/`campaignProgress` tables.
- Produces:
  - `requireSession(c): Promise<SessionClaims>` — throws a `Response` on failure.
  - `GET /v1/sync` → `{ player, balances, campaign, config }`.

- [ ] **Step 1: Write the failing test**

`services/api/test/sync.test.ts`:

```ts
import { mkdtemp, rm } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { afterAll, beforeAll, describe, expect, it } from 'vitest'
import { createApp } from '../src/app.ts'
import { clearBundleCache } from '../src/config/bundle.ts'
import { publishBundle } from '../src/config/publish.ts'
import { LocalBundleStore } from '../src/config/store.ts'
import { issueAccessToken } from '../src/identity/jwt.ts'
import { servers } from '../src/db/schema.ts'
import { startTestDb, type TestDb } from './harness.ts'

// fileURLToPath, not .pathname - a path containing a space would arrive
// percent-encoded and every join below would miss.
const REPO = fileURLToPath(new URL('../../../', import.meta.url))
const SEED = join(REPO, 'config/bundles/0.1.0')

let t: TestDb
let app: ReturnType<typeof createApp>
let bundleRoot: string
let token: string
let playerId: string

beforeAll(async () => {
  t = await startTestDb()
  await t.ownerDb.insert(servers).values({
    serverId: 1, region: 'us-central1', state: 'open', tickDayOfWeek: 0, tickMinuteOfDay: 1200,
  })

  bundleRoot = await mkdtemp(join(tmpdir(), 'broodline-sync-'))
  const store = new LocalBundleStore(bundleRoot)
  await publishBundle(store, SEED, '0.1.0')
  await store.setPointer('0.1.0')
  clearBundleCache()

  app = createApp({ db: t.db, bundleStore: store })

  // Create a real player through the real route, so sync reads what the
  // grant actually wrote rather than a fixture shaped like it.
  const res = await app.request('/v1/account', {
    method: 'POST',
    headers: { 'content-type': 'application/json', 'idempotency-key': 'sync-setup' },
    body: JSON.stringify({ birthdateBand: 'adult', storefrontRegion: 'us-central1' }),
  })
  const body = await res.json() as { accountId: string; playerId: string; accessToken: string }
  token = body.accessToken
  playerId = body.playerId
}, 240_000)

afterAll(async () => {
  await t?.stop()
  await rm(bundleRoot, { recursive: true, force: true })
})

const sync = (headers: Record<string, string> = {}) =>
  app.request('/v1/sync', { headers: { authorization: `Bearer ${token}`, ...headers } })

describe('GET /v1/sync', () => {
  it('returns the whole cold start in one call', async () => {
    const res = await sync()
    expect(res.status).toBe(200)

    const body = await res.json() as {
      player: { playerId: string; serverId: number }
      balances: Record<string, number>
      campaign: { highestWaveCleared: number }
      config: { bundleVersion: string; minimumClientVersion: string }
    }

    expect(body.player.playerId).toBe(playerId)
    expect(body.player.serverId).toBe(1)
    // Read from wallets, never summed from the ledger - solo_execution 5.3.
    expect(body.balances).toEqual({ splice_charges: 3, shards: 250 })
    expect(body.campaign.highestWaveCleared).toBe(0)
    expect(body.config.bundleVersion).toBe('0.1.0')
    expect(body.config.minimumClientVersion).toBe('0.1.0')
  })

  it('refuses a request with no token', async () => {
    const res = await app.request('/v1/sync')
    expect(res.status).toBe(401)
    expect(await res.json()).toMatchObject({ code: 'unauthorized' })
  })

  it('refuses a token this server did not sign', async () => {
    const res = await app.request('/v1/sync', { headers: { authorization: 'Bearer not-a-token' } })
    expect(res.status).toBe(401)
  })

  it('tells a client below the floor to update, rather than serving it', async () => {
    // minimumClientVersion ships from day one because, added after players
    // exist, the players who most need to upgrade are running the build that
    // cannot be told to - solo_execution 6.2.
    const res = await sync({ 'x-client-version': '0.0.1' })
    expect(res.status).toBe(426)
    expect(await res.json()).toMatchObject({ code: 'client_too_old' })
  })

  it('serves a client at or above the floor', async () => {
    expect((await sync({ 'x-client-version': '0.1.0' })).status).toBe(200)
    expect((await sync({ 'x-client-version': '1.4.2' })).status).toBe(200)
  })
})
```

- [ ] **Step 2: Run it to verify it fails**

```bash
pnpm --filter @broodline/api test sync
```

Expected: **FAIL** — `/v1/sync` returns 404.

- [ ] **Step 3: Confirm the auth guard is in place**

`services/api/src/http/auth.ts` was written at Task 8 Step 9, because deletion was the first authenticated route. It already exports `requireSession`, `HttpError` and `isBelow`; this task adds nothing to it.

```bash
grep -c "export function isBelow" services/api/src/http/auth.ts
```

Expected: `1`. If it is `0`, Task 8 Step 9 did not run — go back and write that file before continuing.

- [ ] **Step 4: Write the route**

`services/api/src/routes/sync.ts`:

```ts
import { and, eq } from 'drizzle-orm'
import type { Hono } from 'hono'
import type { Deps } from '../app.ts'
import { loadBundle } from '../config/bundle.ts'
import { withServer } from '../db/client.ts'
import { campaignProgress, players, wallets } from '../db/schema.ts'
import { HttpError, isBelow, requireSession } from '../http/auth.ts'
import { fail } from '../http/errors.ts'

export function registerSyncRoutes(app: Hono, deps: Deps): void {
  app.get('/v1/sync', async (c) => {
    let session
    try {
      session = await requireSession(c)
    } catch (err) {
      if (err instanceof HttpError) return err.response
      throw err
    }

    const bundle = await loadBundle(deps.bundleStore)

    // Checked BEFORE any query. A client that must update should not cost a
    // database round trip, and this endpoint is on every cold start with a
    // p99 budget of 300 ms.
    const clientVersion = c.req.header('x-client-version')
    if (clientVersion !== undefined && isBelow(clientVersion, bundle.minimumClientVersion)) {
      return fail('client_too_old', 'This version of Broodline can no longer connect. Please update.', {
        minimumClientVersion: bundle.minimumClientVersion,
      })
    }

    const snapshot = await withServer(deps.db, session.serverId, async (tx) => {
      const [player] = await tx.select().from(players)
        .where(eq(players.accountId, session.accountId))
      if (player === undefined) return null

      const walletRows = await tx.select().from(wallets)
        .where(eq(wallets.playerId, player.playerId))

      const [progress] = await tx.select().from(campaignProgress)
        .where(and(
          eq(campaignProgress.serverId, session.serverId),
          eq(campaignProgress.playerId, player.playerId)))

      return { player, walletRows, progress }
    })

    if (snapshot === null) return fail('not_found', 'No player on this server for that account.')

    const balances: Record<string, number> = {}
    // Straight off the wallet rows. solo_execution 5.3: balances are NEVER
    // derived by summing the ledger at read time - that is how a ledger
    // becomes too expensive to keep, and the invariant job is what checks the
    // two against each other.
    for (const w of snapshot.walletRows) balances[w.currency] = w.balance

    return c.json({
      player: {
        playerId: snapshot.player.playerId,
        serverId: snapshot.player.serverId,
      },
      balances,
      campaign: {
        highestWaveCleared: snapshot.progress?.highestWaveCleared ?? 0,
        milestonesClaimed: snapshot.progress?.milestonesClaimed ?? 0,
      },
      // Nothing accrues yet: 5.5 puts settlement on claim, on depletion, or
      // on a raid resolving, and Phase 4 has no nodes. The shape is here so
      // Phase 6 fills it rather than adding it.
      timers: [],
      config: {
        bundleVersion: bundle.version,
        minimumClientVersion: bundle.minimumClientVersion,
      },
    })
  })
}
```

- [ ] **Step 5: Register it**

`services/api/src/app.ts` — add the import and the registration beside the account routes:

```ts
import { registerSyncRoutes } from './routes/sync.ts'
```

```ts
  registerAccountRoutes(app, deps)
  registerSyncRoutes(app, deps)
```

- [ ] **Step 6: Run the tests to verify they pass**

```bash
pnpm --filter @broodline/api test sync
```

Expected: **5 passed.**

- [ ] **Step 7: Run everything and commit**

```bash
pnpm --filter @broodline/api test && pnpm --filter @broodline/api typecheck
```

```bash
git add services/api
git commit -m "feat: GET /v1/sync, the one cold-start call

Settled balances, campaign progress, the bundle version and
minimumClientVersion in a single request - solo_execution 6.2, and the
endpoint carrying the phase's only real SLO at p99 300ms.

Balances come straight off the wallet rows and are never summed from the
ledger. That is 5.3's rule and it is the reason the invariant job exists
to check the two against each other rather than making one derive the
other.

The version floor is checked BEFORE any query, because a client that
must update should not cost a database round trip on the call that is on
every cold start. It ships now rather than later for 6.2's reason: added
after players exist, the players who most need to upgrade are running
the build that cannot be told to.

timers is present and empty. 5.5 puts settlement writes on claim, on
depletion or on a raid resolving, and this phase has no nodes - so the
shape is here for Phase 6 to fill rather than to add.

The auth failure never says why. Distinguishing an expired token from a
bad signature to an unauthenticated caller is free information.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 10: The generated contract, and the three client assemblies

`solo_execution` §6: two contracts generated in opposite directions. **Phase 4 builds one of them** — Unity → `api`, with TypeScript as the source of truth. The `api` → `sim` direction arrives with Phase 5.

Design §7: the generated code is its own assembly, with `Model` above it and `Net` above that. Phase 3 created none of the three.

**Files:**
- Create: `services/api/src/schemas.ts`, `services/api/src/openapi.ts`
- Create: `openapi/broodline.json` (generated, committed)
- Create: `nswag.json`, `.config/dotnet-tools.json`
- Create: `client/Assets/Generated/Api/Generated.Api.asmdef` (+ `.meta`)
- Create: `client/Assets/Model/Broodline.Model.asmdef`, `client/Assets/Model/PlayerSnapshot.cs` (+ `.meta`)
- Create: `client/Assets/Net/Broodline.Net.asmdef`, `client/Assets/Net/BroodlineClient.cs` (+ `.meta`)
- Create: `implementation/scripts/generate-contract.sh`
- Modify: `client/Packages/manifest.json`, `services/api/package.json`

**Interfaces:**
- Consumes: the route shapes from Tasks 8 and 9.
- Produces:
  - `openapi/broodline.json` — the generated document, committed.
  - `Broodline.Api.Client` — NSwag's generated `HttpClient` client.
  - `Broodline.Model.PlayerSnapshot` — the cached `/v1/sync` shape.
  - `Broodline.Net.BroodlineClient.ColdStartAsync()` → `PlayerSnapshot`.

- [ ] **Step 1: Add the OpenAPI generator**

`services/api/package.json` — add to `dependencies`:

```json
    "@asteasolutions/zod-to-openapi": "^7.3.0",
    "zod": "^3.24.1"
```

and to `scripts`:

```json
    "openapi": "node --experimental-strip-types src/openapi.ts"
```

```bash
pnpm install
```

- [ ] **Step 2: Write the schemas**

`services/api/src/schemas.ts`:

```ts
import { extendZodWithOpenApi } from '@asteasolutions/zod-to-openapi'
import { z } from 'zod'

extendZodWithOpenApi(z)

/**
 * TypeScript is the source of truth for the Unity -> api direction, per
 * solo_execution section 6. These schemas are what the OpenAPI document is
 * generated FROM, and NSwag generates the C# client from that document.
 *
 * Nothing in client/Assets/Generated is ever hand-edited, and CI fails on a
 * non-empty regeneration diff.
 */
export const CreateAccountRequest = z.object({
  birthdateBand: z.string().openapi({ example: 'adult' }),
  storefrontRegion: z.string().openapi({ example: 'us-central1' }),
}).openapi('CreateAccountRequest')

export const CreateAccountResponse = z.object({
  accountId: z.string().uuid(),
  playerId: z.string().uuid(),
  serverId: z.number().int(),
  accessToken: z.string(),
  refreshToken: z.string(),
  balances: z.record(z.string(), z.number().int()),
}).openapi('CreateAccountResponse')

export const SyncResponse = z.object({
  player: z.object({
    playerId: z.string().uuid(),
    serverId: z.number().int(),
  }),
  balances: z.record(z.string(), z.number().int()),
  campaign: z.object({
    highestWaveCleared: z.number().int(),
    milestonesClaimed: z.number().int(),
  }),
  timers: z.array(z.object({
    kind: z.string(),
    completesAt: z.string(),
  })),
  config: z.object({
    bundleVersion: z.string(),
    minimumClientVersion: z.string(),
  }),
}).openapi('SyncResponse')

export const ErrorResponse = z.object({
  code: z.string(),
  message: z.string(),
  details: z.unknown().optional(),
}).openapi('ErrorResponse')
```

- [ ] **Step 3: Write the emitter**

`services/api/src/openapi.ts`:

```ts
import { writeFile } from 'node:fs/promises'
import { OpenAPIRegistry, OpenApiGeneratorV31 } from '@asteasolutions/zod-to-openapi'
import { CreateAccountRequest, CreateAccountResponse, ErrorResponse, SyncResponse } from './schemas.ts'

const registry = new OpenAPIRegistry()

const errors = (codes: number[]) => Object.fromEntries(codes.map((c) => [
  String(c), { description: 'Error', content: { 'application/json': { schema: ErrorResponse } } },
]))

registry.registerPath({
  method: 'post',
  path: '/v1/account',
  operationId: 'createAccount',
  parameters: [{
    name: 'Idempotency-Key', in: 'header', required: true,
    schema: { type: 'string' },
    description: 'Generated when the action is taken, not when it is sent.',
  }],
  request: { body: { content: { 'application/json': { schema: CreateAccountRequest } } } },
  responses: {
    200: { description: 'Created, or replayed', content: { 'application/json': { schema: CreateAccountResponse } } },
    ...errors([400, 422]),
  },
})

registry.registerPath({
  method: 'get',
  path: '/v1/sync',
  operationId: 'sync',
  parameters: [{ name: 'X-Client-Version', in: 'header', required: false, schema: { type: 'string' } }],
  responses: {
    200: { description: 'The cold-start snapshot', content: { 'application/json': { schema: SyncResponse } } },
    ...errors([401, 404, 426]),
  },
})

const document = new OpenApiGeneratorV31(registry.definitions).generateDocument({
  openapi: '3.1.0',
  info: { title: 'Broodline API', version: '1' },
  servers: [{ url: 'https://api.broodline.example' }],
})

const out = new URL('../../../openapi/broodline.json', import.meta.url)
// Trailing newline and two-space indent, fixed, because this file is
// committed and CI fails on a non-empty diff - formatting drift would read
// as a contract change.
await writeFile(out, `${JSON.stringify(document, null, 2)}\n`, 'utf8')
console.log(`wrote ${fileURLToPath(out)}`)
```

- [ ] **Step 4: Generate the document**

```bash
mkdir -p openapi && pnpm openapi && head -20 openapi/broodline.json
```

Expected: the file is written and begins `{ "openapi": "3.1.0", ...`.

- [ ] **Step 5: Add NSwag as a local tool**

```bash
dotnet new tool-manifest --force && dotnet tool install NSwag.ConsoleCore --version 14.2.0
```

`nswag.json`:

```json
{
  "runtime": "Net80",
  "documentGenerator": {
    "fromDocument": { "url": "openapi/broodline.json" }
  },
  "codeGenerators": {
    "openApiToCSharpClient": {
      "className": "BroodlineApiClient",
      "namespace": "Broodline.Api",
      "generateClientInterfaces": true,
      "generateDtoTypes": true,
      "generateExceptionClasses": true,
      "exceptionClass": "BroodlineApiException",
      "useBaseUrl": true,
      "injectHttpClient": true,
      "disposeHttpClient": false,
      "generateOptionalParameters": true,
      "jsonLibrary": "NewtonsoftJson",
      "output": "client/Assets/Generated/Api/BroodlineApiClient.cs"
    }
  }
}
```

**`NewtonsoftJson`, not `SystemTextJson`.** Unity's IL2CPP build does not carry `System.Text.Json`, and `com.unity.nuget.newtonsoft-json` is the supported path — added in Step 7.

**If `dotnet nswag run` fails with a missing runtime**, the `runtime` field names a .NET version this machine does not have. Set it to one `dotnet --list-runtimes` reports and re-run; the generated output is identical either way, because the runtime only decides which host executes the generator.

- [ ] **Step 6: Write the regeneration script**

`implementation/scripts/generate-contract.sh`:

```bash
#!/usr/bin/env bash
# Regenerates openapi/broodline.json from the Zod schemas, then the C# client
# from that document.
#
# BOTH outputs are COMMITTED, so a contract change is a reviewable diff and a
# Unity build needs no Node - solo_execution section 6, rules 2 and 3. CI runs
# this and fails on a non-empty diff, which is what stops the generated client
# drifting from the handlers it is generated from.
set -euo pipefail
cd "$(dirname "$0")/../.."

pnpm openapi
dotnet tool restore
dotnet nswag run nswag.json

echo "--- generated ---"
git --no-pager diff --stat -- openapi/ client/Assets/Generated/
```

```bash
chmod +x implementation/scripts/generate-contract.sh
```

- [ ] **Step 7: Add Newtonsoft to Unity**

`client/Packages/manifest.json` — add to `dependencies`, in alphabetical position:

```json
    "com.unity.nuget.newtonsoft-json": "3.2.1",
```

- [ ] **Step 8: Generate the client**

```bash
./implementation/scripts/generate-contract.sh
```

Expected: `client/Assets/Generated/Api/BroodlineApiClient.cs` appears, containing `CreateAccountResponse`, `SyncResponse` and `BroodlineApiClient`.

- [ ] **Step 9: Create the three assemblies**

`client/Assets/Generated/Api/Generated.Api.asmdef`:

```json
{
  "name": "Generated.Api",
  "rootNamespace": "Broodline.Api",
  "references": ["Unity.Plastic.Newtonsoft.Json"],
  "includePlatforms": [],
  "excludePlatforms": [],
  "allowUnsafeCode": false,
  "overrideReferences": true,
  "precompiledReferences": ["Newtonsoft.Json.dll"],
  "autoReferenced": false,
  "noEngineReferences": false
}
```

`client/Assets/Model/Broodline.Model.asmdef`:

```json
{
  "name": "Broodline.Model",
  "rootNamespace": "Broodline.Model",
  "references": ["Generated.Api"],
  "includePlatforms": [],
  "excludePlatforms": [],
  "allowUnsafeCode": false,
  "overrideReferences": false,
  "autoReferenced": true,
  "noEngineReferences": false
}
```

`client/Assets/Net/Broodline.Net.asmdef`:

```json
{
  "name": "Broodline.Net",
  "rootNamespace": "Broodline.Net",
  "references": ["Broodline.Model", "Generated.Api"],
  "includePlatforms": [],
  "excludePlatforms": [],
  "allowUnsafeCode": false,
  "overrideReferences": false,
  "autoReferenced": true,
  "noEngineReferences": false
}
```

**None of the three references `Broodline.Sim`, and `Broodline.View` references none of them.** `client_architecture` §1's arrows point one way and the asmdefs are what make that a compile error rather than a convention.

Create the directory `.meta` files as Phase 3 did — **a directory's own meta lives one level up.**

- [ ] **Step 10: Write the model**

`client/Assets/Model/PlayerSnapshot.cs`:

```csharp
using System.Collections.Generic;

namespace Broodline.Model
{
    /// The last /v1/sync response, cached.
    ///
    /// client_architecture section 7: the client is a cache with an outbox and
    /// is never a source of truth. Balances here are for DISPLAY - a cached
    /// balance is a label, not a number the client may do arithmetic on before
    /// spending. The next sync replaces this wholesale, with no merge.
    public sealed class PlayerSnapshot
    {
        public string PlayerId;
        public int ServerId;
        public IReadOnlyDictionary<string, int> Balances;
        public int HighestWaveCleared;
        public string BundleVersion;
        public string MinimumClientVersion;
    }
}
```

- [ ] **Step 11: Write the network layer**

`client/Assets/Net/BroodlineClient.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Broodline.Api;
using Broodline.Model;

namespace Broodline.Net
{
    /// The client's one call at cold start.
    ///
    /// client_architecture section 7: render the cached snapshot immediately,
    /// call /v1/sync, replace. A player never watches a spinner to see their
    /// own roster.
    ///
    /// NO OUTBOX HERE YET. client_architecture section 8 scopes it to durable
    /// queued MUTATIONS, and this phase's only mutation is account creation,
    /// which cannot be queued - a player with no account has nothing to queue
    /// against. It arrives with the first queueable mutation, in Phase 6.
    public sealed class BroodlineClient
    {
        private readonly BroodlineApiClient _api;

        public BroodlineClient(string baseUrl, HttpClient http)
        {
            _api = new BroodlineApiClient(baseUrl, http);
        }

        public async Task<PlayerSnapshot> ColdStartAsync(string accessToken, string clientVersion)
        {
            _api.HttpClient.DefaultRequestHeaders.Remove("Authorization");
            _api.HttpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + accessToken);

            var res = await _api.SyncAsync(clientVersion).ConfigureAwait(false);

            var balances = new Dictionary<string, int>();
            if (res.Balances != null)
            {
                foreach (var kv in res.Balances) balances[kv.Key] = kv.Value;
            }

            return new PlayerSnapshot
            {
                PlayerId = res.Player.PlayerId,
                ServerId = res.Player.ServerId,
                Balances = balances,
                HighestWaveCleared = res.Campaign.HighestWaveCleared,
                BundleVersion = res.Config.BundleVersion,
                MinimumClientVersion = res.Config.MinimumClientVersion,
            };
        }
    }
}
```

**The exact member names NSwag produces depend on the document.** Open `BroodlineApiClient.cs` after Step 8 and match them; if `SyncAsync`'s parameter list differs, adjust this file rather than hand-editing the generated one.

- [ ] **Step 12: Confirm Unity compiles the new assemblies**

```bash
./implementation/scripts/run-unity-tests.sh EditMode
```

Expected: the existing EditMode suite still passes and the three new assemblies compile. **A compile error naming `Broodline.Sim` from `Model` or `Net` means an asmdef reference is wrong** — that is the layering working.

- [ ] **Step 13: Prove the contract gate catches drift**

```bash
git status --short openapi/ client/Assets/Generated/
```

Expected: clean — Step 8 generated them and Step 12 changed nothing.

Now change a schema and confirm regeneration produces a diff. In `schemas.ts`, rename `highestWaveCleared` to `highestWave`, then:

```bash
./implementation/scripts/generate-contract.sh && git --no-pager diff --stat -- openapi/ client/Assets/Generated/
```

Expected: **a non-empty diff in both** — this is exactly what CI fails on at Task 12. Revert:

```bash
git checkout -- services/api/src/schemas.ts openapi/ client/Assets/Generated/
```

- [ ] **Step 14: Commit**

```bash
pnpm --filter @broodline/api test && pnpm --filter @broodline/api typecheck
```

```bash
git add services/api openapi/ nswag.json .config/ client/Assets/Generated client/Assets/Model \
        client/Assets/Net client/Packages/manifest.json implementation/scripts/generate-contract.sh
git commit -m "feat: the generated contract, and the three client assemblies

TypeScript is the source of truth for the Unity->api direction, per
solo_execution section 6. Zod schemas emit OpenAPI 3.1, NSwag emits the
C# DTOs and an HttpClient client, and BOTH outputs are committed so a
contract change is a reviewable diff and a Unity build needs no Node.

Three client assemblies, not one. client_architecture section 1 puts the
generated code in Generated.Api with Model above it and Net above that,
and Phase 3 deliberately created none of them. The done-when needs the
CLIENT to make the cold-start call, so all three land - Model minimally,
holding the sync snapshot section 7 requires it to cache.

None of the three references Broodline.Sim and View references none of
them. The asmdefs are what make that a compile error rather than a
convention.

NewtonsoftJson rather than SystemTextJson: IL2CPP does not carry
System.Text.Json, and com.unity.nuget.newtonsoft-json is the supported
path.

No outbox. section 8 scopes it to durable queued mutations and this
phase's only mutation is account creation, which cannot be queued - a
player with no account has nothing to queue against.

Verified by execution: renaming a field in schemas.ts and regenerating
produces a non-empty diff in both outputs, which is what CI fails on.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 11: Terraform, and the first deployed revision

**The first task in the project that costs money.** Nothing before Phase 4 required GCP to exist; `solo_execution` §3 estimates $25–50/month before players, with Cloud Run scaling to zero and the Postgres instance the only always-on component.

**Files:**
- Create: `infra/terraform/main.tf`, `variables.tf`, `outputs.tf`, `terraform.tfvars.example`
- Create: `services/api/Dockerfile`, `services/api/.dockerignore`
- Create: `services/api/src/config/gcs-store.ts`
- Create: `services/api/src/migrate-cli.ts`
- Create: `implementation/scripts/deploy.sh`, `implementation/scripts/publish-bundle.sh`
- Modify: `services/api/tsconfig.json` (remove the `index.ts` exclusion), `services/api/package.json`

**Interfaces:**
- Consumes: `BundleStore`, `migrate`.
- Produces: `GcsBundleStore(bucket: string)` implementing `BundleStore`; a deployed Cloud Run revision; `terraform output api_url`.

- [ ] **Step 1: Write the Terraform**

`infra/terraform/variables.tf`:

```hcl
variable "project_id" { type = string }
variable "region"     { type = string, default = "us-central1" }
variable "image"      { type = string, description = "Artifact Registry image URI for the api" }
```

`infra/terraform/main.tf`:

```hcl
terraform {
  required_version = ">= 1.9"
  required_providers {
    google = { source = "hashicorp/google", version = "~> 6.0" }
  }
}

provider "google" {
  project = var.project_id
  region  = var.region
}

# One region at milestone 1. solo_execution section 4: servers are
# region-pinned and account data is region-partitioned from day one even while
# only the US exists, because moving EU player data into the EU later is a
# migration under legal pressure.

resource "google_artifact_registry_repository" "api" {
  location      = var.region
  repository_id = "broodline"
  format        = "DOCKER"
}

resource "google_sql_database_instance" "main" {
  name             = "broodline-main"
  database_version = "POSTGRES_16"
  region           = var.region

  settings {
    # Smallest tier. solo_execution section 3: the Postgres instance is the
    # only always-on component and the whole bill is $25-50/month pre-players.
    tier = "db-f1-micro"

    backup_configuration {
      # From day one. With the ledger intact, economy state is reconstructible
      # even from an imperfect restore - solo_execution 5.7.
      enabled                        = true
      point_in_time_recovery_enabled = true
      start_time                     = "09:00"
    }

    ip_configuration {
      ipv4_enabled = false
      # Cloud Run reaches it over the Cloud SQL connector rather than a public
      # IP. A database with a public IP is one password from being everyone's.
      private_network = google_compute_network.main.id
    }
  }

  # HA is DEFERRED behind its section 10 trigger: the first non-TestFlight
  # players. Turning it on is a settings block, not a migration.
  deletion_protection = true
}

resource "google_compute_network" "main" {
  name                    = "broodline"
  auto_create_subnetworks = true
}

resource "google_sql_database" "app" {
  name     = "broodline"
  instance = google_sql_database_instance.main.name
}

resource "google_storage_bucket" "config" {
  name                        = "${var.project_id}-broodline-config"
  location                    = var.region
  uniform_bucket_level_access = true

  versioning {
    # Bundles are immutable by convention; versioning makes the POINTER
    # recoverable too, since it is the one object that is deliberately
    # overwritten.
    enabled = true
  }
}

resource "google_service_account" "api" {
  account_id   = "broodline-api"
  display_name = "Broodline api"
}

resource "google_project_iam_member" "api_sql" {
  project = var.project_id
  role    = "roles/cloudsql.client"
  member  = "serviceAccount:${google_service_account.api.email}"
}

resource "google_storage_bucket_iam_member" "api_config" {
  bucket = google_storage_bucket.config.name
  # Read only. The api never publishes a bundle; publishing is a developer
  # action run from a workstation or CI, which is what keeps a compromised
  # request handler from shipping config to every player at once.
  role   = "roles/storage.objectViewer"
  member = "serviceAccount:${google_service_account.api.email}"
}

resource "google_secret_manager_secret" "jwt" {
  secret_id = "broodline-jwt-secret"
  replication { auto {} }
}

resource "google_secret_manager_secret_iam_member" "api_jwt" {
  secret_id = google_secret_manager_secret.jwt.id
  role      = "roles/secretmanager.secretAccessor"
  member    = "serviceAccount:${google_service_account.api.email}"
}

resource "google_cloud_run_v2_service" "api" {
  name     = "broodline-api"
  location = var.region
  ingress  = "INGRESS_TRAFFIC_ALL"

  template {
    service_account = google_service_account.api.email

    scaling {
      min_instance_count = 0
      # A HARD CAP, deliberately. solo_execution 5.7: Cloud Run scales to
      # hundreds of instances and each opens a pool, so Postgres runs out of
      # connections long before CPU. The cheap version of that fix is this
      # flag plus a small per-instance pool; PgBouncer arrives when the cap
      # throttles real traffic.
      max_instance_count = 10
    }

    volumes {
      name = "cloudsql"
      cloud_sql_instance { instances = [google_sql_database_instance.main.connection_name] }
    }

    containers {
      image = var.image

      volume_mounts {
        name       = "cloudsql"
        mount_path = "/cloudsql"
      }

      env {
        name  = "CONFIG_BUCKET"
        value = google_storage_bucket.config.name
      }
      env {
        name = "JWT_SECRET"
        value_source {
          secret_key_ref {
            secret  = google_secret_manager_secret.jwt.secret_id
            version = "latest"
          }
        }
      }
      env {
        name  = "DATABASE_URL"
        value = "postgresql://broodline_app@localhost/broodline?host=/cloudsql/${google_sql_database_instance.main.connection_name}"
      }

      startup_probe {
        http_get { path = "/healthz" }
        initial_delay_seconds = 5
        failure_threshold     = 10
      }
    }
  }
}
```

`infra/terraform/outputs.tf`:

```hcl
output "api_url"      { value = google_cloud_run_v2_service.api.uri }
output "config_bucket" { value = google_storage_bucket.config.name }
output "sql_connection" { value = google_sql_database_instance.main.connection_name }
```

`infra/terraform/terraform.tfvars.example`:

```hcl
project_id = "your-gcp-project"
region     = "us-central1"
image      = "us-central1-docker.pkg.dev/your-gcp-project/broodline/api:latest"
```

- [ ] **Step 2: Write the container**

`services/api/Dockerfile`:

```dockerfile
FROM node:22-slim AS deps
WORKDIR /app
RUN corepack enable
COPY package.json pnpm-lock.yaml pnpm-workspace.yaml ./
COPY services/api/package.json services/api/
RUN pnpm install --frozen-lockfile --prod

FROM node:22-slim
WORKDIR /app
ENV NODE_ENV=production
COPY --from=deps /app/node_modules ./node_modules
COPY --from=deps /app/services/api/node_modules ./services/api/node_modules
COPY services/api ./services/api
COPY config ./config

# Node 22 strips TypeScript types natively, so there is no build step and no
# dist/ to go stale against the source. The tradeoff is that the container
# carries .ts files; at this size that is cheaper than a bundler in the loop.
EXPOSE 8080
CMD ["node", "--experimental-strip-types", "services/api/src/index.ts"]
```

`services/api/.dockerignore`:

```
node_modules
test
*.test.ts
vitest.config.ts
```

- [ ] **Step 3: Write the GCS store**

`services/api/package.json` — add to `dependencies`:

```json
    "@google-cloud/storage": "^7.14.0"
```

`services/api/src/config/gcs-store.ts`:

```ts
import { readdir, readFile as readLocal } from 'node:fs/promises'
import { join, relative } from 'node:path'
import { Storage } from '@google-cloud/storage'
import type { BundleStore } from './store.ts'

/**
 * The published bundle store. Bundles are immutable objects under
 * bundles/<version>/; the pointer at bundles/current is the one object that
 * is deliberately overwritten, which is what makes rollback a config change
 * rather than a deploy - solo_execution 5.2.
 */
export class GcsBundleStore implements BundleStore {
  private readonly storage = new Storage()

  constructor(private readonly bucketName: string) {}

  private get bucket() { return this.storage.bucket(this.bucketName) }
  private key(version: string, name: string) { return `bundles/${version}/${name}` }

  async hasBundle(version: string): Promise<boolean> {
    const [exists] = await this.bucket.file(this.key(version, 'manifest.json')).exists()
    return exists
  }

  async putBundle(version: string, sourceDir: string): Promise<void> {
    if (await this.hasBundle(version)) {
      throw new Error(`Bundle ${version} is already published. Bundles are immutable; publish a new version.`)
    }
    for (const file of await walk(sourceDir)) {
      const name = relative(sourceDir, file)
      await this.bucket.file(this.key(version, name)).save(await readLocal(file), {
        contentType: 'application/json',
        // No resumable session for files this small; it costs an extra round
        // trip per object and the whole bundle is a few kilobytes.
        resumable: false,
      })
    }
  }

  async readFile(version: string, name: string): Promise<string> {
    const [buf] = await this.bucket.file(this.key(version, name)).download()
    return buf.toString('utf8')
  }

  async getPointer(): Promise<string> {
    const [buf] = await this.bucket.file('bundles/current').download()
    return buf.toString('utf8').trim()
  }

  async setPointer(version: string): Promise<void> {
    if (!(await this.hasBundle(version))) {
      throw new Error(`Cannot point at ${version}: no such published bundle.`)
    }
    await this.bucket.file('bundles/current').save(version, { contentType: 'text/plain', resumable: false })
  }
}

async function walk(dir: string): Promise<string[]> {
  const out: string[] = []
  for (const entry of await readdir(dir, { withFileTypes: true })) {
    const full = join(dir, entry.name)
    if (entry.isDirectory()) out.push(...(await walk(full)))
    else out.push(full)
  }
  return out
}
```

- [ ] **Step 4: Write the migration CLI**

`services/api/src/migrate-cli.ts`:

```ts
import { createPool } from './db/client.ts'
import { migrate } from './db/migrate.ts'

/**
 * Run SEPARATELY from a deploy, never on container start.
 *
 * solo_execution 7.0: never deploy a schema migration and the code that
 * depends on it in the same step. Expand, deploy, migrate, contract - the
 * same discipline that makes rollback possible. A container that migrates on
 * boot makes every rollback a schema question.
 */
const url = process.env.DATABASE_URL
if (!url) throw new Error('DATABASE_URL must be set.')

const pool = createPool(url)
try {
  await migrate(pool)
  console.log('migrations applied')
} finally {
  await pool.end()
}
```

`services/api/package.json` — add to `scripts`:

```json
    "migrate": "node --experimental-strip-types src/migrate-cli.ts"
```

- [ ] **Step 5: Re-enable typechecking on `index.ts`**

`services/api/tsconfig.json` — remove the line added at Task 8 Step 4:

```json
  "exclude": ["src/index.ts"]
```

```bash
pnpm --filter @broodline/api typecheck
```

Expected: clean. `GcsBundleStore` now exists.

- [ ] **Step 6: Write the deploy and publish scripts**

`implementation/scripts/deploy.sh`:

```bash
#!/usr/bin/env bash
# Builds, pushes and deploys the api.
#
# MIGRATIONS ARE NOT RUN HERE. solo_execution 7.0: a schema migration and the
# code requiring it never deploy together. Run publish-bundle.sh and
# `pnpm --filter @broodline/api migrate` as their own deliberate steps.
set -euo pipefail
cd "$(dirname "$0")/../.."

: "${PROJECT_ID:?set PROJECT_ID}"
REGION="${REGION:-us-central1}"
TAG="$(git rev-parse --short HEAD)"
IMAGE="${REGION}-docker.pkg.dev/${PROJECT_ID}/broodline/api:${TAG}"

# Tagged by commit, never :latest. A revision that cannot be named cannot be
# rolled back to, and 7.0 wants the previous revision one command away.
gcloud builds submit --tag "$IMAGE" --project "$PROJECT_ID" .

cd infra/terraform
terraform apply -var="project_id=${PROJECT_ID}" -var="region=${REGION}" -var="image=${IMAGE}"
terraform output -raw api_url
```

`implementation/scripts/publish-bundle.sh`:

```bash
#!/usr/bin/env bash
# Validates and publishes a config bundle, then optionally points at it.
#
# A bundle that fails validation is NOT published - solo_execution 5.2, and
# the entire safety model. Publishing does not make a bundle live; that is the
# second argument.
set -euo pipefail
cd "$(dirname "$0")/../.."

VERSION="${1:?usage: publish-bundle.sh <version> [--activate]}"
: "${CONFIG_BUCKET:?set CONFIG_BUCKET (terraform output config_bucket)}"

node --experimental-strip-types - "$VERSION" "${2:-}" <<'JS'
import { publishBundle } from './services/api/src/config/publish.ts'
import { GcsBundleStore } from './services/api/src/config/gcs-store.ts'

const [version, flag] = process.argv.slice(2)
const store = new GcsBundleStore(process.env.CONFIG_BUCKET)
await publishBundle(store, `config/bundles/${version}`, version)
console.log(`published ${version}`)
if (flag === '--activate') {
  await store.setPointer(version)
  console.log(`pointer now names ${version}`)
}
JS
```

```bash
chmod +x implementation/scripts/deploy.sh implementation/scripts/publish-bundle.sh
```

- [ ] **Step 7: Stand up the infrastructure**

```bash
cd infra/terraform && cp terraform.tfvars.example terraform.tfvars
```

Edit `terraform.tfvars` with the real project id, then:

```bash
terraform init && terraform plan
```

Review the plan. **`google_sql_database_instance` takes several minutes and is the one resource with `deletion_protection`** — read that line before applying.

```bash
terraform apply
```

- [ ] **Step 8: Create the app role and the secret**

Terraform creates the instance and database; the app role is created by migration `0002` but has `NOLOGIN`, and production grants it a password rather than an IAM binding at this milestone.

```bash
gcloud sql users create broodline_app --instance=broodline-main --password="$(openssl rand -base64 32)" --project="$PROJECT_ID"
```

```bash
openssl rand -base64 48 | gcloud secrets versions add broodline-jwt-secret --data-file=- --project="$PROJECT_ID"
```

**Record neither value in the repository.** `.env` and `.env.*` are gitignored; `.env.example` is the only tracked one.

- [ ] **Step 9: Migrate, publish, then deploy — in that order**

```bash
cloud_sql_proxy -instances="$(cd infra/terraform && terraform output -raw sql_connection)=tcp:5433" &
```

Then, substituting the `postgres` superuser password set when the instance was created — **migrations run as the owner, not as `broodline_app`**, because `0002_rls.sql` creates a role and grants privileges:

```bash
DATABASE_URL='postgresql://postgres:YOUR_PASSWORD@localhost:5433/broodline' \
  pnpm --filter @broodline/api migrate
```

Expected: `migrations applied`.

```bash
export CONFIG_BUCKET="$(cd infra/terraform && terraform output -raw config_bucket)"
./implementation/scripts/publish-bundle.sh 0.1.0 --activate
```

Expected: `published 0.1.0` then `pointer now names 0.1.0`.

```bash
PROJECT_ID=your-project ./implementation/scripts/deploy.sh
```

Expected: a Cloud Run URL.

**Schema first, config second, code third.** The code being deployed reads a bundle and a schema that must already exist; the reverse order is a revision that starts, fails its probe, and rolls back.

- [ ] **Step 10: Prove the deployed service answers**

```bash
API_URL="$(cd infra/terraform && terraform output -raw api_url)"
curl -fsS "$API_URL/healthz"; echo
```

Expected: `{"ok":true}`.

- [ ] **Step 11: Commit**

```bash
pnpm --filter @broodline/api test && pnpm --filter @broodline/api typecheck
```

```bash
git add infra/ services/api implementation/scripts/deploy.sh implementation/scripts/publish-bundle.sh
git commit -m "build: Terraform, the container, and the first deployed revision

Two deployables were budgeted and one is built; sim arrives in Phase 5.
Cloud Run, Cloud SQL Postgres 16 on the smallest tier with backups and
PITR from day one, a GCS bucket for bundles, and Secret Manager for the
JWT secret.

max_instance_count is a hard cap rather than a default. 5.7: Cloud Run
scales to hundreds of instances and each opens a pool, so Postgres runs
out of connections long before CPU - the cheap version of that fix is
this flag plus a small per-instance pool, and PgBouncer waits until the
cap throttles real traffic.

The api's bucket role is objectViewer, not admin. It never publishes a
bundle; publishing is a developer action, which is what stops a
compromised request handler shipping config to every player at once.

Migrations do NOT run on container start, and deploy.sh says so. 7.0:
never deploy a schema migration and the code requiring it together - a
container that migrates on boot makes every rollback a schema question.
The order is schema, then config, then code.

The image is tagged by commit and never :latest, because a revision that
cannot be named cannot be rolled back to.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 12: The done-when, and the CI that keeps it true

**Files:**
- Create: `services/api/test/e2e.md` (the manual runbook), `implementation/results/phase4-coldstart.txt`
- Modify: `.github/workflows/tests.yml`
- Modify: `implementation/README.md`

**Interfaces:**
- Consumes: everything.
- Produces: the recorded evidence, and a CI job that runs the two gates plus the contract diff on every push.

- [ ] **Step 1: Add the TypeScript job and the contract gate to CI**

`.github/workflows/tests.yml` — add two jobs:

```yaml
  api:
    name: api - tests and the two gates
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: pnpm/action-setup@v4
      - uses: actions/setup-node@v4
        with:
          node-version: 22
          cache: pnpm
      # The wave validator shells out to the engine, so the .NET SDK is not
      # optional here - config.test.ts runs the real CLI against real bundles.
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
      - run: pnpm install --frozen-lockfile
      - run: pnpm --filter @broodline/api typecheck
      # Testcontainers needs a Docker daemon, which ubuntu-latest provides.
      # Real Postgres, never a mock: RLS, set_config's transaction scoping and
      # unique-violation semantics are the properties under test and all three
      # are exactly what a substitute fakes.
      - run: pnpm --filter @broodline/api test

  contract:
    name: contract - generated client matches the handlers
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: pnpm/action-setup@v4
      - uses: actions/setup-node@v4
        with:
          node-version: 22
          cache: pnpm
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
      - run: pnpm install --frozen-lockfile
      - run: ./implementation/scripts/generate-contract.sh
      # solo_execution section 6, rule 3. A generated client that drifts from
      # the handlers it was generated from is worse than no generated client,
      # because it looks authoritative.
      - name: Fail on a non-empty regeneration diff
        run: |
          if ! git diff --quiet -- openapi/ client/Assets/Generated/; then
            echo "::error::The committed contract does not match the schemas."
            echo "Run ./implementation/scripts/generate-contract.sh and commit the result."
            git --no-pager diff -- openapi/ client/Assets/Generated/
            exit 1
          fi
```

- [ ] **Step 2: Push and confirm both jobs run and pass**

```bash
git add .github/workflows/tests.yml && git commit -m "ci: the api suite and the contract gate" && git push
gh run list --branch phase_4 --limit 3
```

Expected: both jobs appear and pass. **If `api` fails on Docker, Testcontainers could not reach a daemon** — that is an environment problem, not a test problem, and the fix is in the workflow rather than in the suite.

- [ ] **Step 3: Run the done-when against the deployed service**

`services/api/test/e2e.md` — the runbook, because this is a manual proof against real infrastructure and it should be repeatable:

````markdown
# Phase 4 done-when

Run against a deployed revision. Records to
`implementation/results/phase4-coldstart.txt`.

```bash
API_URL="$(cd infra/terraform && terraform output -raw api_url)"
KEY="$(uuidgen)"

# 1. Create an account. The starter grant writes wallets and ledger rows.
FIRST="$(curl -fsS -X POST "$API_URL/v1/account" \
  -H 'content-type: application/json' -H "idempotency-key: $KEY" \
  -d '{"birthdateBand":"adult","storefrontRegion":"us-central1"}')"
echo "$FIRST"
TOKEN="$(printf '%s' "$FIRST" | python3 -c 'import json,sys; print(json.load(sys.stdin)["accessToken"])')"

# 2. The SAME key again. Must return the same account, and grant nothing more.
curl -fsS -X POST "$API_URL/v1/account" \
  -H 'content-type: application/json' -H "idempotency-key: $KEY" \
  -d '{"birthdateBand":"adult","storefrontRegion":"us-central1"}'

# 3. Cold start, one call, with the token from step 1.
curl -fsS "$API_URL/v1/sync" -H "authorization: Bearer $TOKEN"
```

Pass when: steps 1 and 2 return the identical `accountId` and `playerId`,
`balances` reads `{"splice_charges":3,"shards":250}` in both, and step 3
returns that same player with the same balances and `bundleVersion` `0.1.0`.
````

Run it, and save the three responses:

```bash
bash -c 'set -euo pipefail; ...' | tee implementation/results/phase4-coldstart.txt
```

- [ ] **Step 4: Confirm the grant paid exactly once, in the database**

The HTTP responses agreeing is necessary and not sufficient — they would also agree if the second request created a second player and happened to grant the same amounts.

```bash
cloud_sql_proxy -instances="$(cd infra/terraform && terraform output -raw sql_connection)=tcp:5433" &
psql "postgresql://postgres@localhost:5433/broodline" -c \
  "SELECT count(*) FROM players; SELECT count(*) FROM ledger WHERE reason_code = 'STARTER_GRANT';"
```

Expected: **one player, two ledger rows** — one per starter grant currency. Append the output to the results file.

- [ ] **Step 5: Track the evidence**

`implementation/results/` is gitignored except for `*.csv`. This file is evidence that needed a deployed environment to produce, so it is tracked the way the sweep CSVs and the device replay are.

```bash
git add -f implementation/results/phase4-coldstart.txt
```

`implementation/README.md` — add to the file list:

```markdown
- `2026-09-11-phase4-backend-spine.md` — the backend spine: infrastructure,
  the schema and its isolation, the ledger, identity, the config bundle
  pipeline and the generated contract.
```

and to the `results/` paragraph:

```markdown
  `phase4-coldstart.txt` is tracked for the same reason as the sweep CSVs and
  the device replay: reproducing it needs a deployed Cloud Run revision and a
  real Cloud SQL instance, not a rerun.
```

- [ ] **Step 6: Re-capture the device replay under `0.2.0`, if the device is to hand**

Task 1 Step 9 weakened `DeviceReplayTests` to assert the tracked artifact is *superseded*, because the `SimVersion` bump moved the engine past it. **The better fix is to re-record it**, which restores Phase 3's original guard.

If an iPhone is available, follow Phase 3's Task 10 procedure to capture a fresh `device-replay.bin` under `0.2.0`, then restore the strong assertion in `ReplayArtifactPresenceTests` — the test Task 1 rewrote as `TheDeviceRunIsSupersededAndIsNotReSimulated` goes back to asserting `ReplayArtifact.AreCurrent`:

```csharp
        [Fact]
        public void TheTrackedCapturesAreCurrent()
        {
            Assert.True(ReplayArtifact.AreCurrent);
        }
```

**Check what the round-trip tests do once the artifact is current again.** Task 1 routed them through `ReplayArtifact.Superseded`, asserting the `ReplayFormatException` the engine throws on a superseded replay. With a freshly captured artifact they must go back to comparing hashes, which is the actual Phase 3 proof — a round-trip test that only asserts a throw proves nothing about determinism.

**If no device is available, leave the weakened test and say so in the commit.** It is honest debt, and it is recorded at "Edits owed elsewhere" below rather than left silent.

- [ ] **Step 7: Tick this plan's checkboxes**

Phases 1 and 2 both finished with their plans reading as untouched. Walk this document and tick every step that ran, noting any that did not and why.

- [ ] **Step 8: Commit**

```bash
dotnet test Broodline.sln --nologo && pnpm --filter @broodline/api test && pnpm --filter @broodline/api typecheck
```

```bash
git add implementation/ .github/
git commit -m "test: the done-when, and the CI that keeps it true

A cold start fetches a real player from a real server in one call, and a
repeated account creation carrying the same idempotency key grants
exactly once. Recorded against a deployed revision.

The HTTP responses agreeing is necessary and not sufficient - they would
agree just as well if the retry created a SECOND player that happened to
be granted the same amounts. So the proof also counts rows: one player,
two ledger rows.

CI gains two jobs. The api job runs both gates against real Postgres via
Testcontainers, and carries the .NET SDK because the bundle validator
shells out to the engine rather than reimplementing its rules. The
contract job regenerates the OpenAPI document and the NSwag client and
fails on a non-empty diff - a generated client that has drifted from its
handlers is worse than none, because it still looks authoritative.

The result file is tracked against the results gitignore, like the sweep
CSVs and the device replay, and for the same reason: reproducing it
needs a deployed Cloud Run revision rather than a rerun.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Corrections found during execution

The code is ahead of this document in several places. Each correction below was
found by review during execution, landed in the commit named, and is recorded
here so this plan is not read as the design of record where it was wrong.

**Task 1 — the guard could not have worked as designed.** §3.1 of the design put
the `SimVersion` enforcement in a test. The emitter regenerates the baseline from
the current engine, so its header is always the current version and the file
agrees with itself. The guard moved into `emit-corpus-baseline.sh` (`62362ae`),
and the fix round added a history check so a version *revert* cannot disarm it
either (`f7c93bf`).

**Task 1 — two probe defects.** The named test did not exist, and the probe
constant could not perturb a hash: `damage * 115 / 100` swallows a one-point
change at every damage value in the game. Both corrected in this document at
`5d1d887`.

**Task 3 — the migration runner's concurrency claim was false.** `SELECT ... FOR
UPDATE` locks nothing when the row does not exist, so two cold-start runners both
entered the DDL and the comment claimed the opposite. Replaced with
`INSERT ... ON CONFLICT DO NOTHING RETURNING 1`, which makes the loser block on
the winner's row lock, plus an advisory lock around the `_migrations` bootstrap
(`4a58309`).

**Task 3 — `GRANT UPDATE ON accounts` was an account-takeover primitive.** A
handler scoped to one server could rewrite any account's `apple_sub` — the
credential later tasks authenticate against — and flip `server_id`, which
`0001_tables.sql` declares immutable and nothing enforced. Narrowed to
`GRANT UPDATE (apple_sub, deleted_at)` with a trigger enforcing the immutability
(`4a58309`). The comment justifying the exemption was also factually wrong: the
real reason is that login resolves by `apple_sub` before a server is known.

**Task 3 — the gate pinned an enumeration rather than an invariant.** The
five-table list appeared in three places, so a table added later and forgotten
would be unprotected *and* green. The gate now queries `pg_class` for every
ordinary table in `public`, subtracts an explicit allowlist, and requires
`relrowsecurity AND relforcerowsecurity` on the rest, with a vacuity guard
(`4a58309`).

**Task 3 — the schema had no composite foreign keys.** `wallets`, `ledger` and
`campaign_progress` could reference a player that does not exist, and a handler
scoped to one server could bind a player to another server's account. Added in
`4a58309`, before money landed on them.

**Carried forward, not yet acted on.** RLS defends against a handler that forgets
to scope, not one that scopes to the *wrong* server — `broodline_app` can set
`app.server_id` to any value, since custom GUCs are `USERSET`. Tasks 5 and 8 must
take `serverId` from the verified token claim and never from request input, or
this phase's first gate is bypassed by a query parameter.

---

## What this plan deliberately does not do

- **No `sim` service, no server-issued seeds, no submit-and-verify, no replays to GCS.** Phase 5. `Sim.Replay` has the shape it will take, from Phase 3.
- **No nodes, regions, harvest, relocation or splice.** Phase 6. Task 8 uses the starter grant precisely so the node schema is not invented a phase early.
- **No second deployable.** `commerce`, `social`, `stream` and `notify` stay modules inside `api` until their §10 triggers fire, and none fires here.
- **No Redis, no PgBouncer, no Cloud SQL HA, no provisioning automation, no merge tooling.** Each has a named trigger; none is measured yet.
- **No refresh-token table.** Stateless refresh with a `deleted_at` check, and rotation deferred to the first non-TestFlight players — Task 5.
- **No outbox.** This phase's only mutation cannot be queued — Task 10.
- **No `Broodline.UI` and no Codex sheet.** The bundle it was deferred against now exists, which is what closes Phase 3's deferral; building the screen layer is a client phase's work.
- **No push, no APNs, no region partitioning, no BigQuery, no LiveOps console, no web shop.**
- **No second raider and no engine behaviour change.** Task 1 bumps `SimVersion` to mark Phase 3's change; it does not make one.

---

## Definition of done

```bash
dotnet test Broodline.sln --nologo
./implementation/scripts/cross-runtime-diff.sh
pnpm --filter @broodline/api test
pnpm --filter @broodline/api typecheck
./implementation/scripts/generate-contract.sh && git diff --quiet -- openapi/ client/Assets/Generated/
./implementation/scripts/run-unity-tests.sh EditMode
```

All six green, with:

- **GitHub Actions enabled**, and `tests.yml` observed to run rather than merely committed.
- `SimVersion` at `0.2.0`, and `emit-corpus-baseline.sh` **proven to refuse** a re-baseline under an unbumped version by perturbing a damage constant.
- The corpus baseline carrying its header, with all **500 hashes byte-identical** to the pre-header file.
- The isolation suite green, **including its two meta-assertions** — the app role is `NOSUPERUSER NOBYPASSRLS`, and every server-scoped table has `FORCE ROW LEVEL SECURITY`.
- An unscoped query returning **zero rows, not every row**, and `app.server_id` proven not to survive its transaction onto the next pooled checkout.
- Eight parallel requests on one idempotency key producing **one ledger row and a balance of 100**, with the gate proven to fail when the insert is weakened to `onConflictDoNothing`.
- `findDrift` reporting zero drift between ledger and wallets.
- The seed bundle validating; `bad-waves` rejected **by the engine's own message**, `bad-ladder` by the monotonicity rule, `missing-locale-key` by the locale rule.
- A published bundle version **refusing to be overwritten**, and the pointer refusing to name a version that was never published.
- Eight simultaneous `POST /v1/account` calls producing **one player and 250 shards**, and a reused key with a different body returning `422`.
- `GET /v1/sync` returning the player, the balances, the bundle version and `minimumClientVersion` in one call, and `426` for a client below the floor.
- `DELETE /v1/account` setting `deleted_at`, a subsequent refresh **refused**, and the ledger rows **still present**.
- The three client assemblies compiling, with **no reference from `Model` or `Net` to `Broodline.Sim`**.
- The contract diff gate proven to fail by renaming a schema field.
- `implementation/results/phase4-coldstart.txt` tracked, showing **one player and two ledger rows** after a duplicate creation.
- **`dotnet test` reporting `Skipped: 0`.**

---

## Edits owed elsewhere

Tracked here rather than made silently, because all four are normative text in documents this plan does not own.

| Document | Change | State |
|---|---|---|
| `specs/plans/broodline_phase4_backend_spine.md` §3.1 | The `SimVersion` guard lives in the emitter, not in a test — a test cannot catch a re-baseline under an unbumped version | **Applied** at Task 1 Step 13. The design was still in an open PR |
| `specs/plans/broodline_client_architecture.md` §2 | `ref readonly SimState` guarantees nothing — `SimState` is a sealed class whose readonly arrays hold mutable contents | **Owed since Phase 3.** Still at line 52 |
| `specs/plans/broodline_solo_execution.md` §5.2 and §6.1 | §6.1 forbids a game rule existing in two languages on the *request* path; §5.2's validation list silently requires exactly that on the *publish* path. The rule generalises and the owning document should say so | **Owed.** Task 6 builds the resolution; the normative text does not yet record it |
| `specs/broodline_supersession_map.md` | Re-run it. It accounts for none of `plans/` — not `solo_execution`, not `client_architecture`, not the three phase designs. `solo_execution` §9.9 asked for this and it has not happened | **Owed, and growing by one document per phase** |
| `tests/engine/Combat/DeviceReplayTests.cs` | If Task 12 Step 6 could not re-capture the artifact on a device, `ReplayArtifactPresenceTests` remains weakened to assert supersession rather than agreement — **and the round-trip tests assert a thrown `ReplayFormatException` rather than a matching hash**, which is a strictly weaker proof than Phase 3's | **Conditional.** Record which way it went |
