#!/usr/bin/env bash
# Measures the nested-Label doubling - Phase 9 Task 20, instrument (c).
#
# Six sightings across four tasks reasoned this defect out of CAPTURES and
# never measured the mechanism. This runs LabelBoxProbe, which builds a real
# panel the way capture-screens.sh does and reports resolvedStyle heights.
#
# THIS REPORTS, IT DOES NOT ASSERT - see LabelBoxProbe.cs for why a gate is
# the wrong shape here (EditMode builds no panel, so an EditMode assertion
# would read zeros and pass on anything).
#
# NEEDS A GRAPHICS DEVICE: -batchmode WITHOUT -nographics, same as
# capture-screens.sh. -logFile IS A REAL PATH, NEVER "-".
set -uo pipefail
cd "$(dirname "$0")/../.."
UNITY="/Applications/Unity/Hub/Editor/6000.6.0f1/Unity.app/Contents/MacOS/Unity"
LOG="$(pwd)/implementation/results/label-box-probe.log"
mkdir -p implementation/results
rm -f "$LOG"
[ -x "$UNITY" ] || { echo "FAIL: Unity not found at $UNITY"; exit 127; }

"$UNITY" -batchmode -quit -projectPath "$(pwd)/client" \
  -executeMethod LabelBoxProbe.Run -logFile "$LOG"
code=$?

grep '\[labelbox\]' "$LOG" || { echo "FAIL: no [labelbox] lines in $LOG - the Editor is probably open."; exit 1; }
[ "$code" -eq 0 ] || { echo "FAIL: probe exited $code"; tail -30 "$LOG"; exit "$code"; }
