#!/usr/bin/env bash
# Regenerates BOTH halves of the cross-language contract:
#
#   Direction 1: Unity <- api.  TypeScript (Zod) is the source of truth.
#                openapi/broodline.json, then the NSwag C# client.
#   Direction 2: api -> sim.    C# (ASP.NET minimal API) is the source of
#                truth. openapi/sim.json, then the openapi-typescript client.
#
# ALL FOUR outputs are COMMITTED, so a contract change is a reviewable diff
# and neither side needs to run the other's toolchain to consume it -
# solo_execution section 6, rules 2 and 3. The INTENT is that CI runs this
# and fails on a non-empty diff, which is what would stop a generated client
# drifting from the schema/handlers it is generated from - but no CI runs on
# this repository today: GitHub Actions is disabled at the organisation
# level and enabling it is blocked on the owner. Making that guarantee real,
# rather than aspirational, is Task 12's job. In the meantime
# services/api/test/contract.test.ts runs this script and asserts the tree
# is clean afterwards, so the gate at least runs with the test suite.
#
# ONE script, ONE gate, covering both directions - design 2.4. Two scripts
# invite the second one being forgotten in CI.
set -euo pipefail
cd "$(dirname "$0")/../.."

# --- The dotnet-build lock. This script's Direction 2 build (below) and
# services/api/test/wave-submit.test.ts / replays.test.ts each build
# services/sim/Broodline.Sim.Service.csproj independently for their own sim
# instance. MSBuild's -o/--output overrides OutputPath but never
# BaseIntermediateOutputPath, so all three share services/sim/obj/
# regardless of where -o points, and two concurrent builds racing there
# intermittently fail with "the process cannot access the file
# ...rjsmrazor.dswa.cache.json". This bash copy cannot import
# wave-helpers.ts's withDotnetBuildLock() (that is the shared definition
# for the two TS callers, same reasoning as every other helper in that
# file) so it re-implements the identical protocol by hand - the path
# construction, staleness threshold and reclaim logic below MUST be kept in
# sync with that file's lockDir()/isStale()/acquireOnce(); a path or
# timeout edit applied to only one side yields two locks, no exclusion, and
# a flake indistinguishable from the one this exists to close.
#
# Ownership is a pid file written inside the lock dir right after mkdir
# claims it. A hard kill or Ctrl-C runs neither this script's own trap NOR
# node's try/finally in the TS copy, so without staleness detection a
# single interrupted run would leave the lock stuck and poison every later
# run - all three call sites, indefinitely, until a human manually removes
# a /tmp path. (An earlier version of this comment claimed a crash "never
# leaves the lock stuck," which was false - see wave-helpers.ts's comment
# on the same point.) Reclaim: on contention, if the owner pid is
# confirmed dead (or unconfirmable, via the age fallback below), remove the
# lock and let the NEXT mkdir decide who actually gets it - removal itself
# grants no ownership, so two waiters racing to reclaim cannot both end up
# believing they hold it.
#
# Must match wave-helpers.ts's repoRoot()+lockDir() byte-for-byte (both
# hash the repo root with no trailing slash/newline) so this script and the
# two TS callers converge on the exact same lock path.
if command -v shasum >/dev/null 2>&1; then
  REPO_HASH="$(printf '%s' "$(pwd)" | shasum -a 256 | cut -c1-10)"
else
  REPO_HASH="$(printf '%s' "$(pwd)" | sha256sum | cut -c1-10)"
fi
# Namespaced by OS user (so two developers/CI identities sharing one /tmp
# never contend or block each other - load-bearing, since /tmp's sticky bit
# means one user cannot remove another user's stale lock directory at all)
# and by REPO_HASH (so two clones of this repo do not share a lock either -
# lower stakes, since sharing one there would only over-serialize two
# otherwise-independent clones, not produce a correctness bug).
BUILD_LOCK="${TMPDIR:-/tmp}/broodline-sim-dotnet-build-$(id -u)-${REPO_HASH}.lock"
BUILD_LOCK_OWNER_FILE="$BUILD_LOCK/owner.pid"
BUILD_LOCK_STALE_SECONDS=300   # critical section is ~1-2s; minutes is a generous margin
BUILD_LOCK_TIMEOUT_TENTHS=600  # 60s, in 0.1s polling ticks

# Whether $BUILD_LOCK is abandoned and safe to reclaim. Mirrors
# wave-helpers.ts's isStale(): a missing/unreadable owner file falls back
# to the lock dir's own mtime rather than being treated as stale outright,
# which protects the brief window between mkdir succeeding and the owner
# file being written.
build_lock_is_stale() {
  local pid=""
  [ -f "$BUILD_LOCK_OWNER_FILE" ] && pid="$(cat "$BUILD_LOCK_OWNER_FILE" 2>/dev/null)"

  if [ -n "$pid" ] && [ "$pid" -eq "$pid" ] 2>/dev/null; then
    if kill -0 "$pid" 2>/dev/null; then
      return 1 # owner is alive - never stale, regardless of age
    fi
    # kill -0 failed: this shell cannot reliably distinguish "no such
    # process" (owner gone) from "not permitted" (owned by another user,
    # still alive) by exit code alone, so fall through to the age check
    # below rather than guess - same conservative handling the TS side
    # applies to EPERM.
  fi

  [ -d "$BUILD_LOCK" ] || return 1  # already gone; the next mkdir will just succeed
  local mtime now age
  mtime="$(stat -f %m "$BUILD_LOCK" 2>/dev/null || stat -c %Y "$BUILD_LOCK")"
  now="$(date +%s)"
  age=$((now - mtime))
  [ "$age" -gt "$BUILD_LOCK_STALE_SECONDS" ]
}

acquire_build_lock() {
  local waited=0
  while true; do
    local mkdir_err=""
    if mkdir_err="$(mkdir "$BUILD_LOCK" 2>&1)"; then
      echo "$$" > "$BUILD_LOCK_OWNER_FILE"
      return 0
    fi
    if [ ! -d "$BUILD_LOCK" ]; then
      # mkdir failed for a reason OTHER than contention (permission denied,
      # ENOSPC, an invalid path) - report it immediately rather than
      # spinning the full 60s and reporting a misleading "timed out."
      echo "dotnet build lock: mkdir failed: $mkdir_err" >&2
      exit 1
    fi
    if build_lock_is_stale; then
      rm -rf "$BUILD_LOCK" 2>/dev/null || true
      continue  # try mkdir again now, immediately - see the reclaim note above
    fi
    sleep 0.1
    waited=$((waited + 1))
    if [ "$waited" -gt "$BUILD_LOCK_TIMEOUT_TENTHS" ]; then
      echo "timed out waiting for the dotnet build lock at $BUILD_LOCK" >&2
      exit 1
    fi
  done
}

release_build_lock() {
  # Only remove the lock if it is still OURS - never a blind rm. Safe to
  # call unconditionally (cleanup() below does, on every exit path): a
  # no-op if this process never acquired the lock or already released it.
  if [ -f "$BUILD_LOCK_OWNER_FILE" ] && [ "$(cat "$BUILD_LOCK_OWNER_FILE" 2>/dev/null)" = "$$" ]; then
    rm -rf "$BUILD_LOCK" 2>/dev/null || true
  fi
}

# Both generators write to a scratch directory first and are moved into place
# together only once BOTH have succeeded. Without this, `pnpm openapi`
# writing openapi/broodline.json and THEN `dotnet nswag` failing leaves the
# committed OpenAPI document already rewritten while the C# client is still
# generated from the old one - an inconsistent tree that the next successful
# run silently overwrites, so the window it was ever in that state is never
# noticed even though a commit could land during it.
WORK="$(mktemp -d)"

# ONE EXIT trap for the whole script, covering both directions. SIM_PID is
# unset until Direction 2 starts the sim host; cleanup() no-ops that part
# until then. Without killing the background `dotnet run` on every exit path
# - including set -e failing out of the middle of Direction 2 - a failed
# curl leaves a dotnet process holding port 5199, and the NEXT run of this
# script fails with a misleading "address already in use" bind error that
# has nothing to do with what actually broke.
SIM_PID=""
cleanup() {
  if [ -n "$SIM_PID" ]; then
    kill "$SIM_PID" 2>/dev/null || true
  fi
  # release_build_lock is ownership-checked and safe to call unconditionally
  # on every exit path, including a `set -e` failure mid-build: a no-op if
  # this process never acquired the lock, or already released it. No
  # separate "did we hold it" flag is needed (an earlier version tracked
  # one in BUILD_LOCK_HELD, which had its own race between mkdir succeeding
  # and the flag being set/cleared - see wave-helpers.ts's releaseOnce()
  # for why ownership-checking replaces that instead of patching around it).
  release_build_lock
  rm -rf "$WORK"
}
trap cleanup EXIT

# ---- Direction 1: Unity <- api. TypeScript (Zod) is the source of truth. ----

OPENAPI_OUT="openapi/broodline.json"
CLIENT_OUT="client/Assets/Generated/Api/BroodlineApiClient.cs"
OPENAPI_TMP="$WORK/broodline.json"
CLIENT_TMP="$WORK/BroodlineApiClient.cs"
NSWAG_TMP="$WORK/nswag.generated.json"

# src/openapi.ts honours this to redirect its write; see that file.
OPENAPI_OUTPUT_PATH="$OPENAPI_TMP" pnpm openapi

dotnet tool restore

# nswag.json names fixed, committed input/output paths - point a scratch copy
# of it at the freshly-generated temp document and a temp output location
# instead, so a failing run never touches CLIENT_OUT.
python3 - "$OPENAPI_TMP" "$CLIENT_TMP" "$NSWAG_TMP" <<'PY'
import json, sys
openapi_tmp, client_tmp, nswag_tmp = sys.argv[1:4]
with open("nswag.json") as f:
    cfg = json.load(f)
cfg["documentGenerator"]["fromDocument"]["url"] = openapi_tmp
cfg["codeGenerators"]["openApiToCSharpClient"]["output"] = client_tmp
with open(nswag_tmp, "w") as f:
    json.dump(cfg, f)
PY

# This machine's only installed shared runtime is a major version newer than
# any NSwag.ConsoleCore 14.2.0 build targets (net8.0/net9.0). The exact-match
# framework resolution refuses to roll a *major* version forward by default,
# so without this the tool fails to launch at all with a missing-runtime
# error. LatestMajor lets the installed .NET 10 runtime host whichever build
# of the TOOL actually launches (net9.0, per `dotnet tool restore`'s own
# resolution - confirmed by the "NSwag bin directory: .../tools/net9.0/any"
# line nswag prints on every run).
export DOTNET_ROLL_FORWARD=LatestMajor

# nswag.json previously pinned "runtime": "Net90" to match that net9.0 tool
# build. That pin does NOT generalise, for two independent reasons found
# while wiring up Direction 2's second generator on this same script:
#
#   1. NSwag's own "runtime" field is a check against which FRAMEWORK BUILD
#      OF THE NSWAG TOOL ITSELF loaded (net8.0 vs net9.0, the two folders
#      NSwag.ConsoleCore 14.2.0 ships) - not against the host's installed
#      SDK/runtime major version. `dotnet --list-runtimes` on this machine
#      reports only Microsoft.NETCore.App 10.0.12, but the NSwag process
#      that actually runs still reports itself as "Net90" (its own
#      TargetFramework), because DOTNET_ROLL_FORWARD let the net9.0 tool
#      build roll forward onto the installed net10.0 shared runtime. Those
#      are two different "major versions" and conflating them breaks here:
#      naming the runtime "Net${DOTNET_MAJOR}0" from `dotnet --list-runtimes`
#      would compute "Net100", which is rejected before the check even runs
#      (see point 2).
#   2. "Net100" is not a spelling NSwag.Commands 14.2.0 accepts at all - its
#      Runtime enum defines exactly Default, WinX64, WinX86, Net80, Net90,
#      Debug (confirmed via `strings` and the package's XML doc comments).
#      Passing it throws a JsonSerializationException converting the value,
#      before any document is even loaded. There is no Net100 to detect.
#
# "runtime": "Default" ("Use default and do no checks", per NSwag's own XML
# doc) sidesteps both problems: it is a real, documented enum member that
# disables the equality check entirely, so it needs no detection logic and
# does not go stale as the installed SDK, or NSwag's own supported target
# frameworks, move forward. Verified empirically: with the OpenAPI document
# held fixed, "runtime": "Default" and the previous "runtime": "Net90"
# produce byte-identical generated C#, because the field only gates whether
# generation is ATTEMPTED - it has no effect on what gets generated.
#
# (NSwag's `run` command also does not accept a `/runtime:` command-line
# argument - only `/variables:name=value`, which /runtime: is not; passing
# it throws NConsole.UnusedArgumentException. So even with a valid spelling,
# "pass it on the command line instead of nswag.json" was not an available
# option here. The fix lives in nswag.json's "runtime" field, set to
# "Default" once, not computed per run.)

dotnet nswag run "$NSWAG_TMP"

# BOTH generators succeeded - now, and only now, move both outputs into
# place. `mv` within the same filesystem is a rename, not a copy+delete.
mkdir -p "$(dirname "$CLIENT_OUT")"
mv "$OPENAPI_TMP" "$OPENAPI_OUT"
mv "$CLIENT_TMP" "$CLIENT_OUT"

# ---- Direction 2: api -> sim. C# is the source of truth. ----
#
# The document is FETCHED FROM A RUNNING HOST rather than produced by a
# build-time tool, because MapOpenApi composes it from the endpoints as
# registered - which is the only description that cannot drift from what the
# service actually serves.
#
# Built once, then run via `dotnet <dll>` rather than `dotnet run --project`
# or the native apphost binary directly: on this machine both of those
# reliably hung with zero output for the full length of every timeout tried
# (confirmed up to 90s), while `dotnet build` of the identical project
# consistently finished in under two seconds and `dotnet
# <the built dll>` served /healthz within a second. `dotnet run` launches a
# project with UseAppHost=true (the Web SDK's default) via that same native
# apphost stub, which is the common thread between the two hanging paths and
# the one thing `dotnet <dll>` never touches - it loads the assembly
# in-process under the long-lived `dotnet` binary instead of executing a
# freshly-built native executable. The two-step form below is the same
# effective operation (build, then run the host) without whatever that was.
# -o/--output overrides OutputPath, but NEVER BaseIntermediateOutputPath -
# the obj/ tree is computed from the latter regardless of -o, so this build
# and services/api/test/wave-submit.test.ts's and replays.test.ts's (each
# building this SAME project independently, for their own sim instance)
# were all writing intermediate files into the one shared services/sim/obj/,
# racing there whenever vitest ran those files in parallel with this
# script's own contract.test.ts.
#
# The obvious fix - redirecting BaseIntermediateOutputPath/BaseOutputPath
# per call site - does NOT work for this project: confirmed by direct
# reproduction, overriding either property (relative or absolute path, with
# or without -o, with or without isolating the engine ProjectReference via
# GlobalPropertiesToRemove) reproducibly makes MSBuild emit this project's
# own generated files (AssemblyInfo.cs, the TargetFrameworkAttribute file,
# MvcApplicationPartsAssemblyInfo.cs) TWICE into the same csc invocation,
# failing every build with CS0579 duplicate-attribute errors - independent
# of staleness, independent of the separate engine cross-project collision.
# This looks like a genuine SDK/MSBuild quirk in how Microsoft.NET.Sdk.Web
# computes its generated-file item groups; fixing it at that level would
# mean changing services/sim/Broodline.Sim.Service.csproj's own item globs,
# a much larger and riskier change than is warranted here.
#
# So the fix is a lock around JUST this step instead (acquire_build_lock /
# release_build_lock, defined near the top of this script alongside
# BUILD_LOCK - see that block's comment for the full protocol, including
# staleness reclaim, and why it must stay in sync with wave-helpers.ts's
# withDotnetBuildLock()).
acquire_build_lock

dotnet build services/sim/Broodline.Sim.Service.csproj -c Debug \
  -o "$WORK/sim-build" --nologo

release_build_lock
dotnet "$WORK/sim-build/Broodline.Sim.Service.dll" \
  --urls http://127.0.0.1:5199 &
SIM_PID=$!
# cleanup() (trapped on EXIT above) kills $SIM_PID however this script
# exits, including on the set -e path. Without that, a failed curl below
# would leave this dotnet process holding port 5199, and the NEXT run would
# fail with a misleading bind error instead of whatever actually broke.

READY=0
for _ in $(seq 1 40); do
  curl -fsS http://127.0.0.1:5199/healthz >/dev/null 2>&1 && { READY=1; break; }
  sleep 0.25
done
# Without this check the loop falls through SILENTLY on a host that never
# comes up, and the next thing printed is json.tool's "Expecting value:
# line 1 column 1 (char 0)" against an empty curl response - which points
# at JSON parsing, not at the actual problem, and sends whoever is
# debugging it down the wrong path entirely.
[ "$READY" = 1 ] || {
  echo "sim host never became ready on 127.0.0.1:5199" >&2
  exit 1
}

OPENAPI2_OUT="openapi/sim.json"
CLIENT2_OUT="services/api/src/generated/sim.ts"
OPENAPI2_TMP="$WORK/sim.json"
CLIENT2_TMP="$WORK/sim.ts"

# Same staging rule as Direction 1 (see the comment at the top of the
# script): write to $WORK and move both into place only once BOTH have
# succeeded. `curl ... > openapi/sim.json` directly would have the shell
# truncate that file to zero bytes BEFORE curl even runs, so any fetch
# failure - the host dies, the network hiccups - leaves a zero-byte
# openapi/sim.json sitting in the working tree; and generating sim.ts
# directly into place would leave it stale (referencing the OLD document)
# if openapi-typescript failed after a successful curl. Either is exactly
# the inconsistent-tree state the scratch dance exists to prevent, and a
# `git add -A` mid-window would commit an empty or mismatched contract.
#
# --sort-keys is LOAD-BEARING. ASP.NET does not guarantee key order between
# runs of MapOpenApi, so an unsorted document would produce a spurious diff
# on every regeneration even when nothing actually changed - which turns the
# contract.test.ts gate into noise, and noise gets disabled.
curl -fsS http://127.0.0.1:5199/openapi/v1.json \
  | python3 -m json.tool --sort-keys > "$OPENAPI2_TMP"

# Absolute paths on both sides: `pnpm --filter <pkg> exec` runs the command
# with its CWD set to that package's directory (services/api), not the repo
# root this script cd'd to above - confirmed with `pnpm --filter
# @broodline/api exec pwd`. $WORK is already absolute, so both the input
# and the output land in the scratch dir regardless of that CWD switch.
pnpm --filter @broodline/api exec openapi-typescript "$OPENAPI2_TMP" \
  -o "$CLIENT2_TMP"

# BOTH generators succeeded - now, and only now, move both outputs into
# place, exactly as Direction 1 does above.
mkdir -p "$(dirname "$CLIENT2_OUT")"
mv "$OPENAPI2_TMP" "$OPENAPI2_OUT"
mv "$CLIENT2_TMP" "$CLIENT2_OUT"

echo "--- generated ---"
git --no-pager diff --stat -- openapi/ client/Assets/Generated/ services/api/src/generated/
