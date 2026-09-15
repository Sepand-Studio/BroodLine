# Phase 5 — What Execution Left Behind

*Findings parked, ruled on, or deliberately left open across Tasks 0–10 and the
seven-task remediation, and what Task 11 must not walk past*

> **Why this file exists.** Every item below was found during execution or
> review and deliberately not closed, either because the thing it needs does not
> exist yet, because a ruling left it open on purpose, or because it sits
> outside the phase. The working ledger that recorded them lives in git-ignored
> scratch and will not survive a `git clean`. This is the durable copy, and for
> most of these items it is the only one.
>
> **State at the time of writing:** parent Tasks 0 and 2–10 complete on
> `phase_5`, plus remediation Tasks 1–7, thirty-two commits. Gates re-run at
> `e29a01a` and reproduced `implementation/results/phase5-test-baseline.txt` row
> for row: **api 176 across 23 files**, **typecheck 0**, **.NET 186 / 0 skipped**
> (174 engine + 12 sim), **Unity EditMode 35 / 0 / 0**, `cross-runtime-diff.sh`
> 500 scenarios agree, contract diff clean.
>
> Parent **Task 1** (re-capture the device proof) is a human's — it needs
> physical hardware and a tap. Parent **Task 11** (Terraform, Cloud Run, Cloud
> SQL, GCS) is **not built**: execution was stopped by ruling before provisioning
> billable infrastructure. Section 5 is what that leaves owed, and it is the one
> part of this file written from the plan rather than from measurement.
>
> **Counts are not quoted here.** They live in
> `implementation/results/phase5-test-baseline.txt`, as a machine-diffable table,
> because Phase 4 wrote "71" into a sentence and it was wrong by three for a
> whole phase. The numbers in the paragraph above are this file's one exception
> and they are superseded by that file, not the other way round.

---

## 1. Knowingly open, and named in the done-when

Two holes this phase left open on purpose. Both are stated in the plan's
"deliberately does not do", both are marked in code, and neither is a defect to
be discovered later.

**Reward inflation is not proven end to end.** Design §2.2 says the reward is a
function of the wave id the *issuance* names, never the one the submitted echo
claims. `weakenings.md` **row 5** — "read the reward from `verdict.echo.waveId`"
— is **struck, and not closed.** Three layers stack, and only the first was
visible when the row was written:

1. **Step 5 subsumes it.** `matchesIssuance` rejects an echo/issuance wave-id
   mismatch *before* `rewardForWave` is ever reached, so at the lookup
   `echo.waveId === issuance.waveId` is guaranteed and reading either source is
   behaviourally identical. The weakening alone cannot break anything.
2. **A combined weakening** — drop step 5 *and* read from the echo — would be
   observable, but needs two authored waves with **different** rewards.
3. **The engine authors exactly one.** `WaveDef.ForId` returns wave 6 and throws
   `WaveCompositionException` for every other id, which `SimulateEndpoint`
   maps to `rejected: rules_violated`. A wave-7 replay is refused by `sim`
   before the handler sees it. **A test-only bundle fixture is powerless** — the
   gate is the engine, and no task in this phase modifies `engine/`.

What shipped instead drives `rewardForWave` against a synthetic two-wave bundle
and proves the *wiring*: that the handler's choice of source is a choice with
consequences. **It does not prove the property.** The real proof is **owed
against the Phase 6 engine content fill**, at which point the weakening becomes
constructible and must actually be run. Do not let this row be quietly dropped
because a unit test sits near it.

**Creature ownership is unchecked, and the marker is a passing test.** Design
§2.2 draws the boundary this phase cannot cross: a deployment the player does
not own still wins. `adversarial.test.ts`'s
`CAN still deploy creatures the player does not own — Phase 6` asserts the hole
**on purpose**, so Phase 6 inherits a marker that flips the day ownership
lands rather than a silence. A `200` there means the unowned deployment was
accepted — not that the replay was rejected for some other reason, which would
have been the same shape as passing for the wrong reason.

> **The sentence that must travel with the done-when.** Design §2.2's boundary
> is *a forged outcome earns nothing*. It is **not** "only what you own can
> fight". A phase that closes by quoting the stronger sentence has closed on a
> claim it did not prove.

---

## 2. Owed against work that does not exist yet

**Retention's 3h/48h split is unguarded — `weakenings.md` row 7.** Design §4.3
splits retention two ways: `'expired'` rows aged out one hour past
`expires_at`, and `'consumed'` rows retained **48** hours *because they are the
replay counter*. **Neither has a test, because the sweep does not exist** —
`drizzle/0003_wave_issuances.sql:95` records that Task 12 owns it. Row 7's
weakening was therefore applied to the nearest shipped equivalent, check 2's
count *window*, which is the split's consumer rather than the split itself.

Row 7 is the worst finding of the weakening exercise: **nothing anywhere in the
repository failed when design §4.3's day boundary stopped being honoured** — the
full api suite stayed green at 137/137.

The shipped test pins the **boundary**, and that is all it guards: one consumed
row exactly at `date_trunc('day', now() AT TIME ZONE 'UTC')` that must count,
one a second earlier that must not. That pair is independent of the time of day
and of the width of any replacement window. The first fix pinned a *distance*
instead and was inert between 00:00 and 03:00 UTC daily, and green under a
rolling-24h window — a real, player-visible change (locked out until 23:50 the
next day instead of a midnight reset) that nothing noticed.

**When the sweep lands it needs its own direct test**, including the 48-vs-24
hour argument: a row written at 23:50 must still be countable at 00:10, which a
24-hour sweep deletes while it is still needed.

**Guard one has no coverage inside the gate file — `weakenings.md` row 10.**
Submit's step-2 liveness check, made to refuse directly instead of only gating
the `sim` call, breaks design §4.2's guard one: a client retrying across a
network failure resends the **same** idempotency key, by which time the issuance
it was paid for is settled and reads "dead" exactly like a fabricated id. It is
answered **409 for a wave it was in fact paid for** — "pays exactly once under
retry", this phase's central claim, failing in the direction a player notices.

**`adversarial.test.ts` does not see it. 15/15 green**, measured against the
weakening rather than predicted. Every double-submit test in that file uses a
**different** key, deliberately.

**This was ruled, not overlooked.** Different-key replay is the **attack** —
idempotency cannot save you and the settlement must. Same-key replay is the
**honest retry**, a correctness property that belongs in `wave-submit.test.ts`.
Closing row 10 by duplicating a same-key test into the adversarial suite would
paper over the gate hole rather than record it.

**So guard one's whole coverage is two named tests in one other file:**

| File | Test |
|---|---|
| `wave-submit.test.ts` | `returns the stored response on a resend with the SAME key` |
| `wave-submit.test.ts` | `refuses a dead issuance without paying for a re-simulation, indistinguishably from before` |

A comment above the first says exactly this, because an open hole drifts:
**delete both and guard one is covered nowhere, and nothing anywhere will say
so.** That note is load-bearing. It is the only thing standing between a routine
test cleanup and the silent loss of the phase's central claim.

---

## 3. The device round-trip is dark, and nothing goes red about it

Carried from Phase 4, still true, and the mechanism that would normally report
it **does not exist in this repository by design.**

The tracked device-replay artifact was captured under engine `0.1.0`;
`SimVersion.Value` is `0.2.0`. `ReplayArtifact.AreCurrent` is therefore false,
so both round-trip tests — `TheDeviceRunReSimulatesToTheSameHash` and
`TheDeviceRunsRallyActuallyChangedTheSimulation` — hit their
`if (ReplayArtifact.Superseded(record, _out)) return;` guard and bail **before
their own asserts run.** They report **passed**, not skipped. All that executes
is `Superseded`'s proof that `Sim.Replay` *throws* on a superseded record: the
artifact being refused, not re-simulated. That is a strictly weaker proof than
the hash comparison Phase 3 exists to make.

**`dotnet.skipped 0` proves nothing about this.** The tests `return`; they do not
`Skip`. There is **no `Skip` anywhere in the solution's tests** — the class
comment on `ReplayArtifactPresenceTests` records why: a Skip-on-missing-artifact
attribute once inverted the gate, so deleting the artifact that *is* Phase 3's
done-when produced `Failed: 0, Passed: 139, Skipped: 2` and exit 0. That row is
close to a constant, not a guard.

**The real marker is a test, and its polarity is inverted.**
`tests/engine/Combat/DeviceReplayTests.cs:127`,
`TheDeviceRunIsSupersededAndIsNotReSimulated`, asserts

```csharp
Assert.NotEqual(SimVersion.Value, ReplayArtifact.CapturedUnder);
Assert.Equal("0.1.0", ReplayArtifact.CapturedUnder);
```

with `CapturedUnder` read out of the artifact **bytes**, not a hand-maintained
constant. Read it as:

| | |
|---|---|
| **green** | captures superseded; design §2.5 is **not** proven by this suite |
| **red** | someone re-captured on hardware; go restore the strong asserts |

Its **green is the bad news**, and its red is the instruction. Two neighbours
guard the artifacts rather than their version:
`BothRoundTripArtifactsArePresent` fails if a tracked artifact was deleted, and
`TheTwoCapturesAgreeAboutTheirEngine` fails on a half-done re-capture. None of
the three is visible in any count.

**Only a re-capture on physical hardware under `0.2.0` ends this** — parent Task
1, and Task 10 of the Phase 3 plan. When it happens, the round-trip tests must
go back to **comparing hashes**.

---

## 4. Undiagnosed, and already misattributed twice

Three items. The discipline that matters here is the one the phase learned the
hard way: **this family has been misattributed twice, and each time the reason
was that a report carried the vitest `FAIL` line and never the underlying error
text.** Capture stderr and run it; do not read the diff and guess.

### Two container-startup flakes, both open

| Shape | Frequency | Scope |
|---|---|---|
| testcontainers `No host port found for host IP` | ~1 in 8–10 | **one** file, at port allocation |
| `Health check not healthy after 120000ms` | seen **once** | **nine** files at once, 305s run |

The second was found when a remediation implementer stopped to report a baseline
that did not reproduce: run 1 was 9 failed files / 14 passed; run 2 on the
**unchanged tree** was 23/23 in 33.5s and held every run after. Docker was
healthy throughout; the likely trigger was back-to-back `dotnet test` and script
runs, i.e. resource pressure. That is a hypothesis consistent with the shape,
**not a diagnosis.**

**What has been ruled out, by evidence rather than by elimination:**

- **Not the `dotnet build` race.** Both flakes are at container **start**, in
  files that build nothing. The build race was real, was diagnosed (MSBuild's
  `-o` never overrides `BaseIntermediateOutputPath`, so three call sites shared
  `services/sim/obj/`), and is fixed by `withDotnetBuildLock`. Its error text is
  `MSB4018` on `rjsmrazor.dswa.cache.json`, which reproduces at the lock-free
  commit `5447765` and does **not** occur in 32 runs across the two lock-bearing
  trees.
- **Not the 57P01 teardown race.** That one was diagnosed and fixed:
  `await pool.end()` does **not** close connections — pg-pool's promise resolves
  the instant `_clients.length` hits 0, same tick, sockets still open — so
  `container.stop()` answered live connections with a FATAL `admin_shutdown`.
  Measured: `end()` resolved in 0ms with three sockets reporting
  `destroyed:false, writable:true, readyState:'open'`. Fixed in `e9104a6` with
  `drainPool` on the pool's `remove` event.
- **Not "one known flake".** They are two, with different shapes.

**Re-run once before believing a disagreement with the baseline.** Do not chase
either by disabling file parallelism — each sim-hosting file binds its own port
and shares the build through the lock precisely so parallelism can stay on.

> **A test-flake investigation found a production crash bug**, which is the real
> reason this section is worth its length. The 57P01 landed on a pool with
> **zero `error` listeners**, and an EventEmitter with no `error` listener
> *rethrows*, from inside a socket data handler no `try/catch` can reach.
> `createPool` is used by the production server. Cloud SQL failover drops idle
> connections with the same 57P01 — so the deployed API would have taken an
> uncatchable uncaught exception on failover. Fixed in the same commit.

### The apphost workaround is not root-caused, and the old hypothesis is disproved

`generate-contract.sh` builds the sim to a scratch directory and runs
`dotnet <dll>` rather than `dotnet run --project`. The workaround stays. What
changed is the reason recorded next to it:

**It was never a hang.** It reproduces as a failure, but not the recorded one:
the apphost exits **137 (SIGKILL) instantly, with zero output**, and
`dotnet run` is gone in ~2s. The "hung for 90s" reading was an **artifact of the
readiness loop having no liveness check** — the loop did not merely fail to
diagnose, it *manufactured* a wrong diagnosis that was then believed and written
down. That loop now names the exit status.

**The "apphost stub is the common thread" hypothesis is disproved.**
`tools/config-validate` uses the same 124712-byte apphost template (50 bytes
differ) and both its apphost and its `dotnet run` exit 0 on this machine.
Sandbox, invalid signature (`codesign -v` passes; manual re-signing does not
help) and output buffering were each ruled out **by experiment**.

**Re-check under CI.** If it does not reproduce on a runner, capture the exit
status there — a non-137 exit, or any exit at all, is new information.

---

## 5. What Task 11 still owes — the gap this file cannot fill

Task 11 provisions billable GCP and was **held by human ruling**. This section
was written entirely from the plan and from what is on disk rather than from
measurement, and said so.

**Most rows below are now measured; one is not.** The infrastructure half of
Task 11 has since been written and planned — `terraform init`, `validate`,
`fmt -check` and `plan` all run clean against the real project, which bills
nothing and provisions nothing. **No apply has happened; the stack is still
unbuilt.** So every row except the first rests on a plan that was actually
run, while the bundle-rollback row remains what it was: unrun, and unrunnable
without an apply.

The plan now stands at **1 to import, 22 to add, 1 to change, 0 to destroy**
(19/0/0 before the `sim` reachability work below; 15 before the two
deploy-stopping gaps). The three networking resources that do exist —
`google_compute_network.main` and the two private-services-access resources —
refresh successfully and appear in the plan as nothing but a refresh. **Nothing
is destroyed and nothing is replaced.** The single import is the auto-mode
us-central1 subnet and the single change is one boolean on it
(`private_ip_google_access = false -> true`).

**That blocker is now closed, by human ruling, in the direction the design
asked for: OIDC *and* internal ingress, two independent gates.** `api` mints a
Google-signed OIDC ID token for `sim` (`services/api/src/sim/oidc.ts`,
`google-auth-library`), and `sim` stays `INGRESS_TRAFFIC_INTERNAL_ONLY` with
the api reaching it over Direct VPC egress. `allUsers` was never granted and
the invoker binding is unchanged. See "Both gates now exist" below.

| | What | Where it stands |
|---|---|---|
| **Bundle rollback** | Step 8: "confirm rollback is a config change, not a deploy". `loadBundle` caches per process, so an instance that has not turned over keeps serving the old bundle after a pointer change. The claim rests on Cloud Run instance lifetime, **which the code does not control** — Phase 4 booked this and Phase 5 could not settle it | **Unrun.** Whatever Step 8 finds belongs in this file; nothing else records it |
| **`terraform output` reports no outputs** | **Diagnosed — and the recorded hypothesis was wrong.** See below the table | **Not a bug.** Nothing to fix |
| **Design §5.1's 30-day replay lifecycle rule** | It ships *now* precisely because it "cannot be retrofitted onto objects already deleted", with the 20-pin exemption unimplemented. When this row was written it did **not** exist in `infra/terraform` even as unapplied HCL — `main.tf` defined the `config` bucket, no replay bucket, and no `lifecycle_rule` anywhere in the directory | **Written, unapplied.** `google_storage_bucket.replays` now carries an `age = 30` / `Delete` `lifecycle_rule` in `main.tf`, and the plan shows it creating. It has still never run against a real object |
| **No `google_sql_user` created the role `DATABASE_URL` names** | Pre-existing and deploy-stopping: the connection string authenticates as `broodline_app` and nothing anywhere created that role. A green apply and a green revision, then a failure on the first query that touches the database — `/healthz` returns `{ok:true}` without consulting Postgres, so the startup probe passes either way | **Fixed, unapplied.** `google_sql_user.app` plus a Secret Manager secret, version and IAM binding for its password. The api receives it as `PGPASSWORD`, which node-postgres reads as the fallback when the connection string omits a password (`pg/lib/connection-parameters.js`), so `DATABASE_URL` is unchanged and **no application code moved** |
| **`JWT_SECRET` resolves `version = "latest"` against a secret with zero versions** | Pre-existing: `google_secret_manager_secret.jwt` is created, no `google_secret_manager_secret_version` ever was, and the revision therefore fails to start. Cloud Run reports it as a generic container-failed-to-start, which reads like an application crash | **Documented, not papered over.** The value stays out of Terraform on purpose. `deploy.sh` now *refuses to deploy* until an enabled version exists and prints the exact `gcloud secrets versions add` command; the check lists versions and never reads one. A fresh project is a **two-pass bootstrap** (apply → add version → deploy) and that is now written down rather than discovered |
| **`sim`'s internal ingress is unreachable from `api`** | Carried here from the Task 11 plan report as "my understanding… I am not certain". **Now settled against the documentation, and the hedge was right** — see below the table | **Fixed, unapplied.** Direct VPC egress at `PRIVATE_RANGES_ONLY`, Private Google Access on the imported us-central1 subnet, and a private Cloud DNS zone resolving `*.run.app` to `199.36.153.8/30`. ~$0.20/month, no Cloud NAT. Plan: 0 destroyed, 0 replaced |
| **`api` cannot authenticate to `sim`** | Pre-existing and fatal independently of reachability: `SimClient` sent `content-type` and nothing else, and `sim` grants `roles/run.invoker` to exactly one member, so every call would have been a 403 that no plan can surface | **Fixed, unapplied.** `SimClient` takes a **required** `SimAuth`; `src/index.ts` wires `googleIdTokenAuth()`. Omitting it is a compile error *and* a throw — never a silent no-op |

### `sim` is unreachable from `api`, and this is now a citation rather than a hunch

The Task 11 plan report flagged this and was honest that it was unsettled:
"my understanding is that a Cloud Run → Cloud Run call without VPC egress is
*not* internal… I am not fully certain and I could not test it without
applying." It has since been checked against Google's documentation.

**The hunch was correct.** From `cloud.google.com/run/docs/securing/ingress`:

> "When calling from Cloud Run or App Engine to a Cloud Run service that's set
> to 'Internal' or 'Internal and Cloud Load Balancing', traffic must route
> through a VPC network that's considered internal."

`api` has no `vpc_access` block, so its call to `sim`'s `*.run.app` address
does not route through this project's VPC and is not internal. `sim` refuses
it **at the network layer, before IAM is consulted** — which makes this a
second gate fully independent of the invoker/OIDC question, and means
resolving that one does not open this one.

**What the documentation says is required**
(`run/docs/securing/private-networking`, "Receive requests from other Cloud
Run resources or App Engine"): configure the *source* with Direct VPC egress
or a connector, then either **(a)** "route all traffic through the VPC network
and enable Private Google Access on the subnet", or **(b)** enable Private
Google Access and "configure DNS to resolve `run.app` URLs to the
`private.googleapis.com` (`199.36.153.8/30`) or `restricted.googleapis.com`
(`199.36.153.4/30`) ranges".

**The cost assumption that framed this was backwards, and that is the useful
part.** The worry was ~$8–10/month, near-doubling a ~$10–12 bill. That price
belongs to the **Serverless VPC Access connector**, which bills always-on VM
instances. **Direct VPC egress carries no connector-instance compute charge
and scales to zero** (`run/docs/configuring/connecting-vpc`). The reachability
fix does not have to be the expensive option.

**Why no HCL was written for it.** Two sub-questions are open and both move
the bill:

- Route (a) sends *all* of `api`'s egress through the VPC, including
  `services/api/src/identity/apple.ts`'s fetch of
  `https://appleid.apple.com/auth/keys` — a non-Google endpoint that Private
  Google Access does not cover and that a Direct-VPC-egress instance, having
  no external IP, cannot reach without **Cloud NAT**, another billable
  resource. The Cloud Run docs do not state plainly whether Cloud NAT is
  strictly required here; the only mention found is a cold-start caveat.
  (That fetch is currently reached only from tests — `verifyAppleToken` is
  wired into no route yet — so this is a loaded gun rather than a live break.)
- Route (b) avoids Cloud NAT but needs a private DNS zone for `run.app`, and
  the documentation does not say whether the default `private-ranges-only`
  egress routes `199.36.153.8/30` through the VPC at all.

Choosing between them is a cost decision taken while the stack's whole fixed
cost is being weighed, and it is entangled with nothing else here. **Left
open deliberately, with the diagnosis now firm enough to decide on.**

### Both gates now exist — OIDC *and* internal ingress

**Human ruling, taken after the research above:** keep `sim` on internal
ingress *and* require the invoker token. Design §3.1's table asks for exactly
that; it was previously unimplementable because the api had no way to
authenticate. Two independent gates, each of which fails closed on its own.

**Gate 1 — the token (`services/api`).** `SimClient`'s constructor now takes a
**required** `SimAuth`, and `src/index.ts` wires the real one
(`src/sim/oidc.ts`, `google-auth-library` 11.0.2 against the instance metadata
server, audience = sim's URL).

The shape matters more than the mechanism, because the mechanism is four
lines. **There is no default and no unconfigured fallback.** A token provider
that quietly degrades to a no-op would put the test path and the production
path on different branches of the same code, and a production wiring that lost
its provider would look exactly like a green suite — §8 of this file is a list
of eleven assertions that already failed in that shape. So:

- `new SimClient(url)` is **error TS2554**. `pnpm --filter @broodline/api
  typecheck` is the thing that enforces it, and `test/sim-auth.test.ts` pins
  the arity with a `@ts-expect-error`, so relaxing the signature fails the
  build the other way round (**TS2578: Unused '@ts-expect-error' directive** —
  verified by giving the parameter a default and watching typecheck go red).
- The constructor **also throws**, because types are erased and a JS caller or
  an `as any` never meets `tsc`.
- The opt-out has to be written out by name *with a reason*:
  `SimClient.noAuth('local sim host on 127.0.0.1…')`. Every test that spawns a
  local `Broodline.Sim.Service` now says so at its own call site.

**A token that fails or hangs degrades to the existing `unavailable`
verdict** — not a 500, and not an unauthenticated retry. The mint happens
inside `simulate`'s existing `try`, so a rejection becomes `{kind:
'unavailable'}` and `routes/wave.ts` answers a retryable 503 having consumed
nothing; `this.fetchImpl` is never called, so no request goes out without the
header. A hang is bounded by the **same** 5s budget as the fetch (one
`AbortSignal.timeout` shared by both halves, raced against the mint, which
takes no signal of its own) rather than each half getting its own.

**The token is not minted per request, and this was read out of the installed
library rather than assumed.** `IdTokenClient.getRequestMetadataAsync` caches
and refetches only inside the eager-refresh window; `GoogleAuth.
getIdTokenClient` does **not** cache and builds a fresh client with empty
credentials on every call. So the *client* is memoised per audience and the
*token* is left to the library. A rejected client promise is evicted, or one
transient metadata blip at startup would make that instance answer
`sim_unavailable` for its entire life and never retry.

**Gate 2 — the network (`infra/terraform`).** Route B of the research:
Direct VPC egress at `PRIVATE_RANGES_ONLY` on `api`, Private Google Access on
the us-central1 subnet, and a private Cloud DNS zone resolving `run.app` (A at
the apex, CNAME wildcard) to `199.36.153.8/30`. About **$0.20/month**, no
Cloud NAT, and `appleid.apple.com` keeps exactly the egress path it uses
today.

### The auto-mode subnet: imported, because custom mode destroys the VPC

The research flagged this as on the critical path and did not design it:
Private Google Access is per-subnet, and `google_compute_network.main` is
auto-mode, so the subnet was not a Terraform resource. Two candidate fixes.
**The choice was made by measuring, not by preference.**

Switching the network to custom mode (`auto_create_subnetworks = false`) was
planned and the result is disqualifying:

```
google_compute_network.main must be replaced
  ~ auto_create_subnetworks = true -> false # forces replacement

Plan: 22 to add, 0 to change, 3 to destroy.
```

The replacement **cascades to all three existing networking resources** —
`google_compute_global_address.private_services` and
`google_service_networking_connection.private_services` both go with it,
because their `network` becomes known-after-apply. That deletes the VPC and
the Private Services Access peering Cloud SQL's private IP depends on. The GCP
API does have an in-place `switchToCustomMode`; **provider 6.50.0 does not
model it**, and what Terraform would execute is a destroy-and-recreate. Not
taken.

**Importing the auto-created subnet was taken instead.** It touches none of
the three existing resources and flips one boolean:

```
google_compute_subnetwork.main will be updated in-place
  (imported from "projects/broodline-508416/regions/us-central1/subnetworks/broodline")
  ~ private_ip_google_access = false -> true
```

**Two costs of the import, written down here rather than discovered later.**
Both are in `main.tf` beside the resource:

1. **A fresh project is a two-pass bootstrap**, like the JWT secret version
   already is. An import block resolves during *plan*, so on a project with no
   VPC yet the plan fails with "Cannot import non-existent remote object".
   Create the network first (`terraform apply -target=google_compute_network.
   main`), then run the full apply; the auto-mode network creates the subnet
   as a side effect, so the second pass always finds it.
2. **`terraform destroy` will stop on it.** An auto-mode network's subnets
   cannot be deleted individually — the API refuses — and Terraform destroys
   the subnet before the network. Run `terraform state rm
   google_compute_subnetwork.main` before the next ephemeral
   verify-then-destroy cycle; deleting the network removes its auto subnets
   anyway, so nothing leaks. **This is the third instance of the same class of
   teardown surprise** (Cloud Run v2's own `deletion_protection` was the first
   two), which is why it is written down before it happens rather than after.

**The VPC-wide trade-off, restated because it does not go away:** the DNS
override applies to the whole `broodline` network. Every `*.run.app` lookup
from anything in it resolves to `199.36.153.8/30` and works only from a subnet
with PGA on. One caller today; a future workload inherits it silently.
`restricted.googleapis.com` (`199.36.153.4/30`, the VPC-SC variant) is carried
by `private-ranges-only` identically, so switching is one `rrdatas` list.

**What is still unproven: everything, by apply.** Both gates are documentation
plus a plan. The composed Cloud-Run-to-Cloud-Run configuration is not
something Google documents end-to-end in one place, and IAM denial happens on
a request rather than on an apply — so **the first wave submission against the
deployed stack is the first real test of either gate.** If the composition is
wrong the failure is a 5s timeout answered as `sim_unavailable`, not a broken
deploy: retryable, nothing consumed, and invisible to a health check.

### The `terraform output` bug is not a bug, and the recorded cause was wrong

This row said the state carried an empty outputs map because **outputs were
added after the last apply, so the state predates them**, and that the tfstate
is **tracked**. Both halves are false. They were a hypothesis that this file
correctly labelled as one, and the hypothesis has now been checked against the
real state rather than reasoned about.

**What is actually true:**

- `infra/terraform/terraform.tfstate` holds **3 of the 13 resources** the
  configuration declared at the time — `google_compute_network.main`,
  `google_compute_global_address.private_services` and
  `google_service_networking_connection.private_services`. All three are
  networking. There is **no Cloud SQL instance, no Cloud Run service, no
  bucket, no service account and no secret** in state, and never has been.
- `outputs.tf` reads `google_cloud_run_v2_service.api.uri` and
  `google_storage_bucket.config.name`. **Neither resource exists.** An output
  whose resource was never created has no value to report, so
  `terraform output` correctly reports nothing. **`outputs.tf` is fine.**
- The tfstate is **git-ignored**, at `.gitignore:124`
  (`infra/terraform/*.tfstate`), and `git ls-files infra/` lists only the four
  `.tf`/`.hcl` files and `terraform.tfvars.example`. Calling it "tracked" put
  a local, uncommitted artifact into the shared record.

**Why the distinction is worth this much space.** The old reading made this a
small bookkeeping fault — re-apply and the outputs populate. The real reading
is a much larger fact about where this project actually stands: **Task 11 is a
first apply of nearly the whole stack, not an incremental change to a standing
one.** The three networking resources exist because an earlier ephemeral cycle
got that far and was torn down; everything downstream of them, Cloud SQL
included, has to be created from nothing. That is the difference between a
cheap correction and the most expensive apply this project has run, and it is
the single most important input to pricing it.

**Nothing here needs fixing before an apply.** The first step of Task 11 was
recorded as "fix the `terraform output` bug". There is no bug. The step is
discharged by this diagnosis.

Two more Task 11 obligations already in the plan, repeated because they are easy
to miss: **`REPLAY_BUCKET` is a hard deploy-order prerequisite** (Step 2b — the
service hard-fails at boot without it, by ruling: a crash-loop caught in seconds
beats a deploy that succeeds and silently stores nothing), and **the bundle seed
must set the pointer, not just publish the bundle**.

**One Task 11 landmine this phase created.** `config/bundles/0.1.0` **no longer
validates**, because Task 7 made `reward` mandatory. `publish-bundle.sh` carries
`./publish-bundle.sh 0.1.0 --activate` as its own canonical bootstrap example,
and that command now throws `BundleInvalidError`. **The ruling was to retire
`0.1.0` as a bootstrap target, not to add a version floor** — a fresh bucket on
`0.1.0` would be non-functional anyway (`rewardForWave` returns `null`, so
`wave/start` refuses every wave as `wave_locked`), and a floor would preserve
the ability to bootstrap into a *broken* state. What is **not** broken, and
matters: `loadBundle` never validates, so live `0.1.0` keeps serving, and
`setPointer` checks only file existence, so **rollback to `0.1.0` remains
possible forever**. Damage is confined to publishing from scratch.

That last clause is also a live hazard with a test attached: a **pointer
rollback to `0.1.0`** is the reachable way to put a rewardless wave in front of
the handler, and `setPointer` does **not** re-validate. A republish can no
longer drop a reward — the validator refuses it — so the rollback is the only
route left.

---

## 6. Running the done-when dirties tracked Unity config

Not Phase 5's to fix, and not Phase 5's doing, but it sits directly in the path
of the close-out and the consequence is a bad commit.

`./implementation/scripts/run-unity-tests.sh EditMode` and
`cross-runtime-diff.sh` open the Unity project, and opening it can rewrite
tracked configuration:

- `client/ProjectSettings/ProjectSettings.asset` — IL2CPP and Sentis flags
- `client/Assets/Settings/PC_RPAsset.asset`
- `client/Assets/Settings/UniversalRenderPipelineGlobalSettings.asset`

and can create **untracked** files under `client/Assets/Resources/`.

**It is a hazard, not a certainty.** It did not reproduce on the whole-branch
review's run, and it did not reproduce on the close-out run either — after both
`cross-runtime-diff.sh` and `run-unity-tests.sh EditMode`, `git status
--porcelain` showed only the close-out's own edits. Two clean runs are not a
disproof; they are two clean runs.

The cost of being wrong is asymmetric, which is why it is written down at all:
anyone who runs the done-when sequence and then reaches for `git add -A` sweeps
Unity build configuration into a commit that has nothing to do with it, and
nobody reviews a diff they did not intend to make.

**Stage named paths.** This phase's own close-out commit does, for exactly this
reason.

---

## 7. Under-specified, not wrong

**`openapi/broodline.json` types `waveId` as an unbounded `integer`**, in both
`WaveStartRequest` and `WaveStartResponse`. The handler is bounded —
remediation Task 4 pinned `parseStart` to `1..2_147_483_647`, the ceiling being
what the `integer` column can store at all and the floor what no bundle can
author, with `issueWave` still the authority on which waves actually exist. The
contract simply does not say so.

Tightening it means editing `openapi.ts` and regenerating **both** directions
(the Unity client and `services/api/src/generated/`), which is why it was booked
rather than smuggled into a task that had no business touching the contract.

**`weakenings.md`'s introduction still says "nine"** where the reconciled answer
is ten or eleven (§8). That file is the phase gate's evidence and was frozen for
the close-out, so the correction lives here rather than there.

---

## 8. Eleven assertions that were green while proving nothing

This is the defect class the phase spent itself finding, and **the enumeration
is the finding — not the number.** A statistic nobody can reconstruct is exactly
the kind of unverifiable claim this phase exists to stamp out, so the list is
carried in full and the count is reported as what it honestly is.

| # | The assertion | Why it proved nothing |
|---|---|---|
| 1 | The OpenAPI drift gate | `Results.Ok(response)` erases to `IResult`, so the generator emitted a document with **no response schemas** — the gate would have passed vacuously |
| 2 | The seed comparison | BigInt-vs-string: a broken seed encoding gets the submission refused anyway, balance unchanged, test green **for the wrong reason** |
| 3 | The unscoped-read isolation test | Pinned default-deny, not **discrimination** — passes even with no policy at all |
| 4 | The `wave/start` concurrency test | Satisfied equally by the **serialized** path; it would go red only the day the collision actually raced |
| 5 | `serverId: 'one'` | A string fails `typeof !== 'number'` and short-circuits, so `Number.isInteger` — the half the task existed to cover — had no test. `NaN` is the trap: it serializes to `null` and lands back in the same redundancy. `1.5` is the answer |
| 6 | The lock's "reclaims a lock owned by a dead pid" | Reclaimed via the **age** branch, not ESRCH. Deleting the ESRCH branch entirely left all three tests green |
| 7 | The seed-shop test | Both calls 500'd and `undefined === undefined` is green. **Self-caught**, inside the file written to stop exactly this |
| 8 | The day-boundary test | Inert 3 hours a day and green under a rolling-24h window — **in the gate file itself** |
| 9 | The EACCES preflight test | Passed having exercised **nothing**: an unexecutable file is skipped by the PATH search, so it found the real `dotnet` further along. Self-caught |
| 10 | `still answers 409 when the replay write fails on a wave_locked refusal` | Never asserted `put()` was **called**, so it could not tell "the swallow works" from "the write is never reached" |
| 11 | `still pays when the replay write fails` | Proving less than its name **since Task 6**; rescued only by its neighbour going red |

**Ten or eleven**, depending on whether the final pair counts as one
house-pattern finding or two distinct assertions. Not nine, which is the number
that rode in the documents for most of the phase.

**The house pattern is bounded and closed.** The shape — a throwing stub with
assertions only on the response — was swept across the whole suite. The only two
instances are both in `replays.test.ts`, `still pays when the replay write fails`
and `still answers 409 when the replay write fails on a wave_locked refusal`, and
both now carry an invocation counter. **The test names are the anchors, not line
numbers** — this repo has had three citations rot inside one phase. The one other
candidate, `idempotency.test.ts`'s rollback test, is **sound**: it asserts the
rejection *and* that the key row is gone *and* that a retry succeeds as
`'fresh'`, and its `rejects.toThrow('boom')` proves the callback actually ran.

**The method lesson, recorded because it changed outcomes repeatedly.** Five of
these survived a *wholesale* mutation and died only under a **targeted** one. A
test naming a specific branch must be mutated **at that branch**; mutating the
enclosing function proves only that *some* path works.

---

## 9. Things that should not be re-litigated

**Row 4's weakening is mis-specified, not the guard unproven.** The settlement
write-once trigger's live definition is `drizzle/0004_trigger_scope_and_bounds.sql`,
**not `0003`** — 0004 drops and re-creates it narrowed to
`BEFORE UPDATE OF settled_at, settlement`. Weakening 0003 today is a **no-op**
(measured 176/176 green), so anyone reproducing the row from the old `Where`
would see green and conclude the record was false. The guard *is* covered, in
`issuance-schema.test.ts`, where a schema guard belongs: no sequence of HTTP
requests can reach it, because `settle()` carries `AND settled_at IS NULL` so a
settlement **rewrite** never happens from the request path.

**The DDL-overwrite exposure is bounded at one instance.** Every migration was
swept: the only object re-created across files is
`wave_issuances_settlement_write_once`. `wave_issuances_one_live` is defined
only in `0003` and never re-issued.

**The timestamp-vs-timestamptz implicit cast is bounded at one instance, fixed.**
`AT TIME ZONE` and `date_trunc` appear at `wave/issuance.ts` and **nowhere else**
in `services/api/src`; zero occurrences in `drizzle/`. Every other expiry
comparison is done in JavaScript against `Date` objects on a `withTimezone`
column, which node-postgres parses to a correct absolute instant — no implicit
cast to get wrong.

**Writing a replay on a *loss* is correct.** Design §5.2's clause is "written
after `sim` **verifies**", not "written on a win". A loss is exactly as
verified, differing only in reward computation, and §5.1's retention draws no
outcome distinction. The gate is confirmed not to branch on outcome.

**`settle()` returns a boolean and `credit()` must stay gated on it.** Under
READ COMMITTED the loser of a concurrent settle gets `rowCount 0` — **not** an
exception — because of the `AND settled_at IS NULL` guard. Crediting anyway
reproduces the double-pay that guard exists to prevent.

**A dead issuance answering `409 issuance_invalid` is the truthful answer**,
even though it moved two responses (`426 engine_too_old` and
`503 sim_unavailable`) off paths they used to take. Neither is knowable without
the `sim` call that the reorder exists to skip, and both would be actively
misleading — "update your app" and "retry later" for a request that will never
succeed. §2.3's "sim unavailable leaves the issuance live" concerns a **live**
issuance and is untouched.

**The `dotnet build` lock's four rounds were spent on a real bug**, not on
test-infrastructure perfectionism. At the lock-free commit the failure
reproduces with `MSB4018` moving between `replays.test.ts` and
`wave-submit.test.ts`; it does not occur in 32 runs with the lock present.

---

## 10. Smaller things, still true

- **`generate-contract.sh`'s committed `openapi/sim.json` advertises
  `"servers": [{"url":"http://127.0.0.1:5199/"}]`** — a loopback address in the
  contract for a Cloud Run service. Normalisation could strip `servers`.
- **`pnpm --filter @broodline/api test` now requires a .NET SDK**, a
  network-capable `dotnet tool restore`, and free ports. The api package's tests
  are no longer runnable on a Node-only machine.
- **The lock's empty-owner sentinel survives its own fix.** During the
  `mkdir`→owner-write window there is no owner file, so both reads return `""`
  and re-verify compares equal. A nonce cannot help; the value does not exist
  yet. Needs an ownerless dir aged past 5 minutes. **Ruled comment-only** —
  closing it needs a CAS primitive a lock directory cannot offer, which is
  disproportionate for test infrastructure.
- **The ABA window in lock reclaim is narrowed, not closed** — from "however
  long the caller was paused" to one filesystem-op dispatch. Ruled: do not
  redesign; the comment must not claim otherwise.
- **`SIGKILL` is uncovered by the child reaper**, stated rather than implied.
- **A bad request body to `sim` that is invalid JSON or omits `replay`** is
  answered by the endpoint-scoped middleware, but a body that *fails binding*
  another way is a narrow gap in the always-200 principle. Unreachable in
  practice: `api` is the only caller.

---

## 11. CI: enabled since 2026-09-12, and the determinism gate has still never run

Added 2026-09-14, from measurement. **Both halves of this correct a claim that
was carried forward as fact.**

**Actions is on.** `2026-09-11-phase4-followups.md` §1 records Actions as
disabled at the `Sepand-Studio` org level, with "No workflow has ever run on
this repository", and makes Phase 4 Task 12 blocked on it. That stopped being
true on **2026-09-12**:

```
GET /repos/Sepand-Studio/BroodLine/actions/permissions
→ {"enabled":true,"allowed_actions":"all","sha_pinning_required":false}
```

`tests.yml` has run green on `phase_4` pushes, on both `phase_4` pull-request
events, and on `develop` for the **PR #4** merge. Phase 4's section now carries
a correction block; the original text is left under it rather than deleted,
because its second half survived.

**`determinism.yml` has never executed once, and nothing went red about it.**
This is the part nobody diagnosed. The workflow requires a self-hosted macOS
runner — deliberately, for the reasons in its own header: hosted macOS bills at
a 10× multiplier, carries no Unity install and no licence, and this job builds
a full IL2CPP player. **No runner is registered at either level:**

```
GET /orgs/Sepand-Studio/actions/runners        → {"total_count":0,"runners":[]}
GET /repos/Sepand-Studio/BroodLine/actions/runners → {"total_count":0,"runners":[]}
```

So every trigger queues until something kills it. Three recorded outcomes, all
cancellations, none of them failures:

| Run | Trigger | Sat for | Ended |
|---|---|---|---|
| 34760566697 | schedule | **24h0m02s** | hit the workflow timeout |
| 34703963157 | push | 5h34m34s | cancelled |
| 34864133881 | schedule | 8h34m13s | cancelled 2026-09-14, by hand, as part of this finding |

**The failure mode worth naming: a queued run is not a failing run.** No status
check ever reported red, no PR was ever blocked, and the branch protection that
would have caught it does not exist — so the gate that exists to stop a
determinism regression from landing has been decorative since it was written.
This is the same shape as §8's eleven assertions: green, or at least not-red,
while proving nothing. `tests.yml` covers the plain `dotnet test` half on hosted
Ubuntu, so the uncovered surface is precisely **the IL2CPP cross-runtime
comparison and the Unity EditMode suite** — the two things only this gate runs,
and the two that make the determinism claim more than an assertion about one
runtime.

**It is not an emergency, and the reason is worth stating so nobody treats it as
one.** `implementation/scripts/cross-runtime-diff.sh` and
`run-unity-tests.sh EditMode` are run by hand at every phase gate, and were run
again on 2026-09-14. The engine is not unverified; it is unverified *by CI*, and
the exposure is a regression landing between two hand-runs.

**What closing it needs — not done, and a human's call.** Registering a
self-hosted macOS runner on a machine carrying the .NET SDK, Unity 6000.6.0f1
with macOS IL2CPP build support, and an activated licence.
`implementation/scripts/verify-prereqs.sh` checks exactly that set and is the
fast way to confirm a candidate machine. **The decision is not technical:** a
self-hosted runner executes workflow code from the repository on the machine it
runs on, and it only reports while that machine is awake. A developer laptop
satisfies the prerequisites and is a poor fit for both halves of that sentence.

**`phase_5` has never been through CI at all** — zero runs on the branch across
its thirty-odd commits, because `tests.yml` landed after the branch did and no
pull request has been opened for it. Whatever opens `phase_5` will be the first
time hosted CI sees this phase's code.

---

*Owns: nothing normative. This is the record of what Phase 5's execution found
and chose not to fix, so the next person does not rediscover it. Each item's
real home is the code, migration or design section it names. Where this file and
a count disagree, `implementation/results/phase5-test-baseline.txt` wins; where
this file and a weakening row disagree, re-run the weakening.*
