#!/usr/bin/env bash
# Runs the 500-scenario corpus on CoreCLR and on a real IL2CPP player, and
# diffs the hashes. This is Phase 1's whole reason for existing: proving the
# engine compiles identically on both runtimes it actually ships on.
#
# IMPORTANT — this builds and runs a player, it does not use -executeMethod
# to compute hashes. -executeMethod runs inside the Unity Editor, and the
# Editor executes managed code under Mono, not IL2CPP. A gate built that way
# would compare CoreCLR against Editor-Mono and print "scenarios agree"
# having never executed a single IL2CPP-compiled instruction — see Task 7
# Step 3's report. So this script:
#   1. emits the CoreCLR side via `dotnet test` (tests/engine/CorpusTests.cs),
#   2. drives Unity via -executeMethod only to *build* a macOS ARM64,
#      non-development, IL2CPP standalone player (client/Assets/Editor/
#      DeterminismHarness.cs),
#   3. runs that built player as its own process, which emits its own side
#      (client/Assets/Determinism/CorpusPlayerHarness.cs),
#   4. diffs the two files it wrote.
#
# Timing: with a warm client/Library (Unity's own build cache, gitignored),
# the IL2CPP build is incremental and takes roughly 19-36 seconds. On a
# machine that has never built this player before — a fresh CI runner, or
# the first run on a new dev machine — the build is COLD: expect roughly
# 10 minutes, most of it one-time URP/Sentis shader-variant compilation that
# happens before IL2CPP codegen even starts. This is normal; it is not hung.
# Do not judge the timeout on the fast path alone.
#
# Streaming, not silent buffering: the Unity build is piped live to stdout
# (via `-logFile -`) rather than written to a log file that we only read
# after Unity exits. Several runs of this gate have previously been killed
# by a 600-second no-output watchdog while blocked on a silent multi-minute
# cold build — streaming avoids that, and it is also simply the right design
# for a CI log a human might be reading while it runs.
#
# Fix round 1, Finding 1: `grep -q "result=Succeeded"` only proves *a* build
# succeeded, not which scripting backend it used — a silently-Mono build
# would satisfy it too. Once the build log exists, this script also asserts
# the backend/artifact evidence DeterminismHarness.cs itself now throws on
# (see its pre-build readback check and ReportRuntimeArtifacts), so a future
# softening of the harness's own guard alone is not enough to make this gate
# pass on Mono.
#
# Fix round 1, Finding 2: a trap (EXIT/INT/TERM, installed below once
# restore_known_churn is defined) guarantees the ProjectSettings.asset /
# URP-asset restoration runs even if this script or Unity is killed
# mid-build — Ctrl-C, a CI job hitting `timeout-minutes`, an OOM — not only
# on the normal completion path. Verified this does not change the normal
# PASS (exit 0) / FAIL (exit 1) behavior; see the fix-round-1 report.
set -uo pipefail
cd "$(dirname "$0")/../.."

UNITY="/Applications/Unity/Hub/Editor/6000.6.0f1/Unity.app/Contents/MacOS/Unity"
PROJECT="$(pwd)/client"
OUT="$(pwd)/implementation/results"
PLAYER_APP="$OUT/il2cpp-player/BroodlineCorpus.app"
PLAYER_BIN="$PLAYER_APP/Contents/MacOS/Broodline Bench"

CORPUS_CORECLR="$OUT/corpus-coreclr.txt"
CORPUS_IL2CPP="$OUT/corpus-il2cpp.txt"
CORPUS_DIFF="$OUT/corpus-diff.txt"
DOTNET_LOG="$OUT/corpus-dotnet-test.log"
BUILD_LOG="$OUT/il2cpp-build.log"
PLAYER_LOG="$OUT/il2cpp-player-run.log"

mkdir -p "$OUT"

# Every Unity batchmode invocation that builds this player reproducibly
# re-serialises these three files as a side effect of switching the active
# build target to StandaloneOSX and back — two URP assets pick up a
# shader-prefiltering field, and ProjectSettings.asset gains explicit
# (but equivalent-in-meaning) scriptingBackend/platformArchitecture keys.
# See Task 7 Step 3's report for the exact diffs. None of it is a real
# content change, but left in place it means "did this leave the tree
# dirty?" fails forever downstream (e.g. in CI). We restore exactly these
# three paths and NEVER `git checkout -- client/` or any other directory-wide
# revert: this branch takes concurrent commits from a human elsewhere under
# client/ (Benchmark work, at time of writing), and a blanket restore could
# silently discard their uncommitted work.
RESTORE_PATHS=(
  "client/ProjectSettings/ProjectSettings.asset"
  "client/Assets/Settings/PC_RPAsset.asset"
  "client/Assets/Settings/UniversalRenderPipelineGlobalSettings.asset"
)

# NOT restored, deliberately: the harness leaves Unity's active build target
# set to StandaloneOSX (EditorUserBuildSettings, which lives under
# client/Library/ — gitignored, not a git-cleanliness problem). Switching it
# back would just make the *next* run (or the next iOS build) pay a reimport
# cost with nothing gained, since nothing tracked by git reflects this value.

echo "--- preflight ---"
[ -x "$UNITY" ] || { echo "FAIL: Unity not found at $UNITY"; exit 127; }
[ -d "$PROJECT" ] || { echo "FAIL: no client/ project at $PROJECT"; exit 1; }
if pgrep -x Unity >/dev/null 2>&1; then
  echo "FAIL: a Unity editor is already running — close it first (a second instance cannot open the project)"
  exit 1
fi

# Snapshot which restore paths are ALREADY dirty before we touch anything.
# If one is, that is a human's in-progress edit, not our churn, and we have
# no way to tell which part of a later diff would be ours to discard — so we
# leave that specific file alone rather than guess (see git status/staging
# discipline: never touch a file you didn't dirty).
ALREADY_DIRTY=()
for p in "${RESTORE_PATHS[@]}"; do
  git diff --quiet HEAD -- "$p" 2>/dev/null || ALREADY_DIRTY+=("$p")
done

restore_known_churn() {
  for p in "${RESTORE_PATHS[@]}"; do
    dirty_before=0
    for d in "${ALREADY_DIRTY[@]:-}"; do
      [ "$d" = "$p" ] && dirty_before=1
    done
    if [ "$dirty_before" = 1 ]; then
      echo "NOTE: $p was already modified before this run — leaving it as-is, not restoring."
    else
      git checkout -- "$p" 2>/dev/null || true
    fi
  done
}

# Belt-and-suspenders for the explicit restore_known_churn call below (which
# still runs first, right after the Unity step, to keep the dirty window as
# short as possible on the normal path): this trap guarantees the same
# restoration happens however the script actually leaves, including a kill
# between the backend being set inside Unity and that explicit call ever
# being reached. restore_known_churn itself never calls exit, so it cannot
# clobber an exit code already decided elsewhere — verified: a bare
# `trap restore_known_churn EXIT` does not change $? even when
# restore_known_churn's own last command fails. INT/TERM get their own
# handlers because bash only re-checks a pending trap once a foreground
# child returns control to it — a signal delivered to just this script's own
# PID while it is blocked on Unity would otherwise sit pending until Unity
# exits on its own; delivered to the whole process group (how a terminal
# Ctrl-C and a CI cancellation actually signal a job) it fires promptly,
# which is what was verified here.
trap restore_known_churn EXIT
trap 'exit 130' INT
trap 'exit 143' TERM

echo "--- CoreCLR ---"
rm -f "$CORPUS_CORECLR"
if ! BROODLINE_CORPUS_OUT="$CORPUS_CORECLR" \
    dotnet test Broodline.sln --filter EmitCorpusHashes >"$DOTNET_LOG" 2>&1; then
  echo "FAIL: dotnet test could not emit the CoreCLR corpus — see $DOTNET_LOG"
  tail -40 "$DOTNET_LOG"
  exit 1
fi
[ -s "$CORPUS_CORECLR" ] || { echo "FAIL: CoreCLR emitted no corpus file at $CORPUS_CORECLR"; exit 1; }

echo "--- Unity / IL2CPP: build the player (cold: ~10 min; incremental: ~19-36s) ---"
rm -f "$BUILD_LOG"
"$UNITY" -batchmode -quit -projectPath "$PROJECT" \
  -executeMethod DeterminismHarness.BuildMacIl2CppPlayer \
  -corpusPlayerOut "$PLAYER_APP" \
  -logFile - 2>&1 | tee "$BUILD_LOG"
build_code=${PIPESTATUS[0]}

# Restore the known churn right away, regardless of whether the build
# succeeded — the build-target switch that dirties these files happens near
# the start of DeterminismHarness.BuildMacIl2CppPlayer, before the actual
# BuildPipeline.BuildPlayer call, so a build that fails downstream still
# leaves the same files dirty.
restore_known_churn

if [ "$build_code" -ne 0 ] || ! grep -q "result=Succeeded" "$BUILD_LOG"; then
  echo "FAIL: IL2CPP player build failed (unity exit $build_code) — see $BUILD_LOG"
  exit 1
fi

# Fix round 1, Finding 1: result=Succeeded alone says nothing about which
# scripting backend actually shipped — a silently-Mono build satisfies it
# just as well. DeterminismHarness.cs now refuses to return a Mono player on
# its own (it throws, which would already have failed the check above via a
# non-zero unity exit), but that guard lives in Unity C# code a future edit
# could soften without this script noticing. Assert the same evidence
# independently, against the literal build log text, so softening the
# harness alone is not enough to make this gate pass on Mono.
if ! grep -q "building with backend=IL2CPP" "$BUILD_LOG"; then
  echo "FAIL: build log never shows IL2CPP read back from PlayerSettings before the build — see $BUILD_LOG"
  exit 1
fi
if ! grep -q "GameAssembly.dylib present=True" "$BUILD_LOG"; then
  echo "FAIL: build log does not confirm GameAssembly.dylib shipped in the player bundle (IL2CPP evidence) — see $BUILD_LOG"
  exit 1
fi
if grep -q "MonoBleedingEdge/ present=True" "$BUILD_LOG"; then
  echo "FAIL: build log shows a MonoBleedingEdge/ tree in the player bundle — that is a Mono player, not IL2CPP — see $BUILD_LOG"
  exit 1
fi

[ -d "$PLAYER_APP" ] || { echo "FAIL: build reported success but $PLAYER_APP is missing"; exit 1; }

echo "--- Unity / IL2CPP: run the player ---"
rm -f "$CORPUS_IL2CPP" "$PLAYER_LOG"
"$PLAYER_BIN" -batchmode -nographics -logfile - -corpusOut "$CORPUS_IL2CPP" 2>&1 | tee "$PLAYER_LOG"
player_code=${PIPESTATUS[0]}
if [ "$player_code" -ne 0 ]; then
  echo "FAIL: IL2CPP player exited $player_code (0=wrote corpus, 1=emit failed, 2=called without -corpusOut) — see $PLAYER_LOG"
  exit 1
fi
[ -s "$CORPUS_IL2CPP" ] || { echo "FAIL: IL2CPP player emitted no corpus file at $CORPUS_IL2CPP"; exit 1; }

echo "--- diff ---"
if diff -u "$CORPUS_CORECLR" "$CORPUS_IL2CPP" >"$CORPUS_DIFF"; then
  echo "PASS: $(wc -l < "$CORPUS_CORECLR" | tr -d ' ') scenarios agree"
  exit 0
else
  echo "FAIL: runtimes disagree. First differences:"
  head -20 "$CORPUS_DIFF"
  exit 1
fi
