#!/usr/bin/env bash
# Renders every screen to implementation/results/screens/*.png at 430x932.
#
# NEEDS A GRAPHICS DEVICE: -batchmode WITHOUT -nographics. On a headless box
# this produces blank or black images rather than failing, so the size check
# below is not decoration - it is the only thing between a silent blank and a
# reviewer looking at nothing.
#
# -logFile IS A REAL PATH, NEVER "-". Piping Unity's log through stdout ("-")
# buffers it until the process exits, which makes a slow run and a hung one
# look identical - documented the hard way earlier this phase. Tail the file
# below, or `tail -f` it from another shell while this runs.
set -uo pipefail
cd "$(dirname "$0")/../.."

UNITY="/Applications/Unity/Hub/Editor/6000.6.0f1/Unity.app/Contents/MacOS/Unity"
LOG="$(pwd)/implementation/results/capture-screens.log"
OUT="$(pwd)/implementation/results/screens"
mkdir -p implementation/results

# Delete any previous results FIRST, the run-unity-tests.sh convention: a run
# that never starts (the editor already holding the project is the common
# case) must not leave a prior successful capture behind for the checks below
# to pass against. A stale OK is worse than a failure.
rm -f "$LOG"
rm -rf "$OUT"

[ -x "$UNITY" ] || { echo "FAIL: Unity not found at $UNITY"; exit 127; }
[ -d client ] || { echo "FAIL: no client/ project"; exit 1; }

"$UNITY" -batchmode -quit -projectPath "$(pwd)/client" \
  -executeMethod ScreenshotCapture.CaptureAll \
  -logFile "$LOG"
code=$?

echo "--- unity log: $LOG ---"
tail -20 "$LOG"

n=$(ls "$OUT"/*.png 2>/dev/null | wc -l | tr -d ' ')
[ "$n" -ge 12 ] || { echo "FAIL: expected >=12 screens, got $n. See $LOG."; exit 1; }

small=$(find "$OUT" -name '*.png' -size -8k)
[ -z "$small" ] || { echo "FAIL: these captures are suspiciously small (blank?):"; echo "$small"; exit 1; }

[ "$code" -eq 0 ] || { echo "FAIL: Unity exited $code despite $n screens on disk - check the log."; exit "$code"; }

echo "OK: $n screens captured."
