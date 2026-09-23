#!/usr/bin/env bash
# Measures screens at a frame the CALLER names - Phase 9 Task 21e.
#
# Usage: probe-frame.sh [WxH,WxH,...] [screen,screen,...]
# Default: 402x874, the frame the Phase 9 exit-gate walk ran in, all fixtures.
#
# SEVERAL FRAMES IN ONE LAUNCH, because Unity holds one lock on client/ and a
# sweep run as N invocations is N cold starts serialized behind each other.
# The safe-area insets are part of the frame a screen actually gets: an
# iPhone 17 is 402x874 points and SafeAreaBinder pads the shell root by the
# top and bottom insets, so the frame a SCREEN sees is shorter than the
# device's. Probe both and the difference is visible.
#
# WHY THIS EXISTS BESIDE capture-screens.sh. That script renders the corpus at
# 430x932, the handoff's reference frame. The app runs at 390-402 points, and
# the gap has hidden two defects that only a human walking the packaged app
# found: Task 21b's lane crop (0.903 units at 390, 0.100 at 430) and Task
# 21e's clipped founder card. The corpus stays at 430 - it is the comparison
# baseline and moving it would invalidate every stored PNG - and this reports
# rect-by-rect at whatever frame you ask for.
#
# THIS REPORTS, IT DOES NOT ASSERT, on probe-label-box.sh's precedent and for
# its reason: geometry needs a real panel, and EditMode builds none.
#
# NEEDS A GRAPHICS DEVICE: -batchmode WITHOUT -nographics, same as
# capture-screens.sh. -logFile IS A REAL PATH, NEVER "-".
set -uo pipefail
cd "$(dirname "$0")/../.."

SIZES="${1:-402x874}"
SCREENS="${2:-}"
TAG="$(echo "$SIZES" | tr ',' '_')"

UNITY="/Applications/Unity/Hub/Editor/6000.6.0f1/Unity.app/Contents/MacOS/Unity"
LOG="$(pwd)/implementation/results/frame-probe-${TAG}.log"
OUT="$(pwd)/implementation/results/frames"
mkdir -p implementation/results

# Delete previous results FIRST, the run-unity-tests.sh convention: a run that
# never starts (the Editor already holding the project is the common case)
# must not leave a prior probe's numbers behind to be read as this one's.
rm -f "$LOG"
for s in ${SIZES//,/ }; do rm -rf "$OUT/$s"; done

[ -x "$UNITY" ] || { echo "FAIL: Unity not found at $UNITY"; exit 127; }
[ -d client ] || { echo "FAIL: no client/ project"; exit 1; }

args=(-batchmode -quit -projectPath "$(pwd)/client"
      -executeMethod FrameProbe.Run
      -frameSizes "$SIZES")
[ -n "$SCREENS" ] && args+=(-frameScreens "$SCREENS")

"$UNITY" "${args[@]}" -logFile "$LOG"
code=$?

grep '\[frame\]' "$LOG" || { echo "FAIL: no [frame] lines in $LOG - the Editor is probably open."; exit 1; }
[ "$code" -eq 0 ] || { echo "FAIL: probe exited $code"; tail -30 "$LOG"; exit "$code"; }
echo "OK: frames in $OUT"
