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
# Held only across the `dotnet build` call below (see the comment there);
# tracked here so cleanup() can release it on ANY exit path, including a
# `set -e` failure mid-build, without which a crashed run would leave the
# lock stuck and every subsequent run - this script's and both
# wave-submit.test.ts's/replays.test.ts's - would hang until their own
# 60s timeout.
BUILD_LOCK_HELD=""
cleanup() {
  if [ -n "$SIM_PID" ]; then
    kill "$SIM_PID" 2>/dev/null || true
  fi
  if [ -n "$BUILD_LOCK_HELD" ]; then
    rmdir "$BUILD_LOCK" 2>/dev/null || true
  fi
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
# So the fix is a lock around JUST this step instead: `flock` isn't
# installed on macOS by default, but `mkdir` is atomic on POSIX filesystems
# (fails with "File exists" if the directory is already there), which is
# enough for a simple retry-based mutex. Shared with
# services/api/test/wave-submit.test.ts's and replays.test.ts's
# withDotnetBuildLock() via the same fixed /tmp path - the critical section
# is ~1-2s, so worst-case three-way contention adds a few seconds, not the
# tens of seconds --no-file-parallelism would cost by serializing all 17
# test files instead of just this one shared resource.
BUILD_LOCK="/tmp/broodline-sim-dotnet-build.lock"
WAITED=0
while ! mkdir "$BUILD_LOCK" 2>/dev/null; do
  sleep 0.1
  WAITED=$((WAITED + 1))
  if [ "$WAITED" -gt 600 ]; then
    echo "timed out waiting for the dotnet build lock at $BUILD_LOCK" >&2
    exit 1
  fi
done
BUILD_LOCK_HELD=1

dotnet build services/sim/Broodline.Sim.Service.csproj -c Debug \
  -o "$WORK/sim-build" --nologo

rmdir "$BUILD_LOCK"
BUILD_LOCK_HELD=""
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
