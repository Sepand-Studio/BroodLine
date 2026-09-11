# Phase 4 — What Execution Left Behind

*Findings parked during Tasks 0–10, and what Task 11 must not walk past*

> **Why this file exists.** Every item below was found by review during
> execution and deliberately not fixed, either because it was out of the task's
> scope or because it was judged not to block. The working ledger that recorded
> them lives in git-ignored scratch and will not survive a `git clean`. This is
> the durable copy.
>
> **State at the time of writing:** Tasks 0–10 complete on `phase_4`, 27
> commits. 71 api tests, 174 .NET tests (0 skipped), 35 Unity EditMode tests.
> Tasks 11 (Terraform, Cloud Run, Cloud SQL, GCS) and 12 (the deployed
> done-when, CI) are **not built** — execution was stopped before provisioning
> billable infrastructure.

---

## 1. Blocked on a human

**GitHub Actions is disabled at the `Sepand-Studio` organisation level.** No
workflow has ever run on this repository. `tests.yml` and `determinism.yml` are
both committed and both unexecuted, so neither has ever been debugged against
reality.

```
PUT /repos/Sepand-Studio/BroodLine/actions/permissions
→ 409 "GitHub Actions is disabled on this repository by the organization"
```

The session token carries `read:org`, not `admin:org`, so this cannot be fixed
from a session. Enable it at
`https://github.com/organizations/Sepand-Studio/settings/actions`, or run
`gh auth refresh -h github.com -s admin:org` and retry the API call. **Task 12
cannot start until this is done**; Tasks 0–10 were unaffected because every gate
verifies locally.

---

## 2. Must be settled inside Task 11, not after it

These three were parked as minor and re-rated by the whole-branch review,
because Task 11 is the phase that makes each of them real.

| | What | Why Task 11 |
|---|---|---|
| **JWT rotation** | `secret()` measures characters, not entropy, and session tokens carry no `kid`. Rotating `JWT_SECRET` silently invalidates every outstanding 90-day refresh token | Task 11 is where `JWT_SECRET` first becomes a Secret Manager value with a rotation policy. A `kid`-aware two-key verify is cheap at provisioning and a migration afterwards. Fold in the missing `iss` claim at the same time |
| **Bundle pointer seeding** | Nothing in the repo seeds `bundles/current`. `loadBundle` reads the pointer and the service 500s on its first request without it | Task 11's seed step must publish the `0.1.0` bundle **and** set the pointer. Confirm the script does both, not just the first |
| **NSwag runtime pin** | `nswag.json` pins `runtime: Net90` and `generate-contract.sh` exports `DOTNET_ROLL_FORWARD=LatestMajor`, both tuned to a machine carrying only .NET 10 | On a runner whose only shared runtime is 8.x, `dotnet tool restore` resolves the net8.0 NSwag build and its internal check rejects `Net90`. Verified to fail **loudly** in every traced path — never silently different output. Verify `dotnet --list-runtimes` against the CI image, or make the value environment-detected |

Two more Task 11 obligations, already written into the plan but worth repeating
because they are easy to miss:

- **Remove `"exclude": ["src/index.ts"]` from `services/api/tsconfig.json`** once
  `gcs-store.ts` lands. `index.ts` is currently the only unchecked file in the
  service, and it already imports a module that does not exist.
- **Confirm "rollback is a config change, not a deploy" is true in practice.**
  `loadBundle` caches per process, so an instance that has not turned over keeps
  serving the old bundle after a pointer change. The claim currently rests on
  Cloud Run instance lifetime, which the code does not control.

---

## 3. Parked for the second-server milestone

**Two holes that open together**, both documented in code, both unexploitable
today only because exactly one server is mapped.

**A client picks its own server.** `assignServer` is a total function of
`body.storefrontRegion`, which is request-supplied — so a client chooses its
region and thereby its server. The docstring in `src/identity/accounts.ts` now
says this plainly rather than claiming the opposite. The real fix is to derive
the storefront from a **verified App Store source**, not the request body.

**The same idempotency key can mint two accounts.** The key's primary key is
`(server_id, key)`. Once two regions are mapped, one key sent with two different
`storefrontRegion` values lands in two key-spaces, collides with neither, and
grants twice — and does not even return `422`, because `storefrontRegion` sits
inside the hashed body so the mismatch check never sees it.

**`GRANT SELECT ON accounts` is table-wide and unscoped.** Nothing exercises it
cross-server today — every read is by `account_id` from a verified `sub` — but
it means "isolation is RLS" carries a permanent documented exception. A future
handler that lists or searches accounts would cross servers with nothing to stop
it. Fix it in the same change as the two above.

---

## 4. Standing debt with no owner yet

**Phase 3's device round-trip proof is not running.** Bumping `SimVersion` to
`0.2.0` moved the engine past the tracked device-replay artifact, so
`ReplayArtifact.Superseded` short-circuits both round-trip tests before their
assertions. `dotnet test` reports 174 passing with 0 skipped **and does not
cover the done-when Phase 3 exists to prove.**

This is recorded in the test's own comment, in Task 12 Step 6, and here. Only a
re-capture on physical hardware under `0.2.0` ends it — and when it happens, the
round-trip tests must go back to **comparing hashes**, not asserting a thrown
`ReplayFormatException`, which is a strictly weaker proof.

**Sign in with Apple has no HTTP surface.** `verifyAppleToken`, `bindApple`,
`createGuest` and `redeemRefreshToken` have no callers outside their own tests.
`POST /v1/account` returns a 90-day refresh token that **no endpoint can
redeem**, so a session ends hard at the 15-minute access-token TTL with no
recovery path. This is plan-consistent — the route table only ever named
`account.ts` and `sync.ts` — and the comments that previously argued otherwise
have been corrected. A refresh route and a sign-in route are unclaimed work.

**`createGuest` is dead code.** `routes/account.ts` inlines its own insert rather
than calling it, so `createGuest` is reachable only from tests. Any future
narrowing of the `accounts` INSERT grant must fix **the route**, not
`createGuest` — following the original note literally would break the live path
while fixing something nothing calls.

---

## 5. Worth doing soon

- `emit-corpus-baseline.sh` aborts with a bare git/grep error if the baseline is
  absent at HEAD or header-only — the one unexplained failure path in a script
  otherwise written to be paranoid about false greens.
- `extractClaims`' own guard has no regression test. Given this suite's
  documented history of auth tests that passed while proving nothing, it is the
  highest-value of the remaining test gaps.
- `BundleWaves` silently defaults a missing or renamed `spawns` field to an
  empty wave. Not a rule to invent — `WaveDef` has no floor either — but a
  bundle author's typo publishes silently instead of failing loudly.
- The pack-ladder check treats `priceUsdCents: 0` as `NaN` and the comparison is
  silently false, so a free pack anywhere but first position is exempted. The
  **only** parked finding that fails silently, inside the validator whose whole
  purpose is failing loudly. Fix before the first non-empty `packs.json`.
- `CREATE TRIGGER accounts_server_id_immutable` is the one statement in `0002`
  that is not re-runnable. `DROP TRIGGER IF EXISTS` first.
- `ledger`'s foreign key to `players` has no `ON DELETE` action. Harmless while
  deletion is soft; decide it when a purge job exists rather than guessing now.
- `generateDataAnnotations: false` is durable config but JSON carries no
  comment, so the reason — Unity lacks `System.ComponentModel.DataAnnotations` —
  lives only in a commit message.
- `Broodline.Model` references `Generated.Api` but uses no type from it. Points
  the model layer at the transport layer for nothing.

---

## 6. Two things that should not be re-litigated

**`idempotency_keys.status` is unobservable by design.** No transaction can read
a row in `in_flight` — it is set and flipped inside one transaction, and any
failure rolls it back. The column and its CHECK are effectively decorative.
Correct as built; do not infer a state machine that is not there.

**A deleted account keeps a working access token for up to 15 minutes.**
`requireSession` does no `deleted_at` lookup, deliberately — a database read on
every authenticated request is exactly what the design avoids. Documented in
`src/http/auth.ts` and accepted.

---

*Owns: nothing normative. This is a record of what execution found and chose not
to fix, so the next person does not have to rediscover it. Each item's real home
is the code it names.*
