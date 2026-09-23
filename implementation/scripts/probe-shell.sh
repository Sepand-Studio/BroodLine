#!/usr/bin/env bash
# Asks the real shell what a thumb hits - Phase 9 Task 21i.
#
# Usage: probe-shell.sh [WxH] [safeTop,safeBottom] [subject,subject,...]
# Default: 402x874, insets 62,34, the two sheets and the four bottom-CTA screens.
#
# WHY THIS EXISTS BESIDE probe-frame.sh. That script hosts ONE fixture in a
# bare frame with a `#screen-host` slot around it, and reports rects. It has no
# tab bar, no sheet layer and no notice layer, so no run of it has ever had two
# shell layers to be wrong about - which is how a primary action spent this
# phase with its lower half under the tab bar, drawn, visible, and dead to the
# touch, with a clean corpus and a green suite the whole time.
#
# THIS ONE ASSERTS AND EXITS NON-ZERO, WHICH probe-frame.sh AND
# probe-label-box.sh DELIBERATELY DO NOT. Their reason is that geometry needs a
# live panel and EditMode builds none, so an assertion there reads zeros and
# passes on anything. That reason is about EditMode. Here there IS a live panel
# - this is -batchmode WITH a graphics device, the same as capture-screens.sh -
# and `IPanel.Pick` answers the exact question the defect asked: does a point
# inside this button resolve to this button, or to the bar drawn across it.
#
# NEEDS A GRAPHICS DEVICE: -batchmode WITHOUT -nographics, same as
# capture-screens.sh and probe-frame.sh. -logFile IS A REAL PATH, NEVER "-".
set -uo pipefail
cd "$(dirname "$0")/../.."

FRAME="${1:-402x874}"
INSETS="${2:-62,34}"
SUBJECTS="${3:-}"

UNITY="/Applications/Unity/Hub/Editor/6000.6.0f1/Unity.app/Contents/MacOS/Unity"
LOG="$(pwd)/implementation/results/shell-probe-${FRAME}.log"
mkdir -p implementation/results

# Delete previous results FIRST, the run-unity-tests.sh convention: a run that
# never starts (the Editor already holding the project is the common case) must
# not leave a prior probe's numbers behind to be read as this one's.
rm -f "$LOG"

[ -x "$UNITY" ] || { echo "FAIL: Unity not found at $UNITY"; exit 127; }
[ -d client ] || { echo "FAIL: no client/ project"; exit 1; }

args=(-batchmode -quit -projectPath "$(pwd)/client"
      -executeMethod ShellProbe.Run
      -shellFrame "$FRAME"
      -shellInsets "$INSETS")
[ -n "$SUBJECTS" ] && args+=(-shellSubjects "$SUBJECTS")

"$UNITY" "${args[@]}" -logFile "$LOG"
code=$?

grep '\[shell\]' "$LOG" || { echo "FAIL: no [shell] lines in $LOG - the Editor is probably open."; exit 1; }
[ "$code" -eq 0 ] || { echo "FAIL: probe exited $code - a control has points a player can see and cannot press."; exit "$code"; }
echo "OK: every asserted control is reachable at every point of its own box"
