#!/usr/bin/env bash
# Runs Unity EditMode tests headlessly. Usage: run-unity-tests.sh [EditMode|PlayMode]
set -uo pipefail
cd "$(dirname "$0")/../.."
UNITY="/Applications/Unity/Hub/Editor/6000.6.0f1/Unity.app/Contents/MacOS/Unity"
PLATFORM="${1:-EditMode}"
RESULTS="$(pwd)/implementation/results/test-results-$PLATFORM.xml"
LOG="$(pwd)/implementation/results/unity-$PLATFORM.log"
mkdir -p implementation/results

# Delete any previous results FIRST. Without this, a run that never starts —
# the editor holding the project is the common case — leaves the old XML in
# place and this script parses it, printing a green summary beside a non-zero
# exit code. A stale pass is worse than a failure.
rm -f "$LOG" "$RESULTS"

[ -x "$UNITY" ] || { echo "FAIL: Unity not found at $UNITY"; exit 127; }
[ -d client ] || { echo "FAIL: no client/ project — Task 2 must run first"; exit 1; }

if [ "$PLATFORM" = "EditMode" ]; then
  "$UNITY" -batchmode -quit \
    -projectPath "$(pwd)/client" \
    -executeMethod Broodline.TestHarness.EditModeRunner.Run \
    -testResults "$RESULTS" \
    -logFile "$LOG"
  code=$?
else
  # PlayMode still uses -runTests and is EXPECTED TO DEADLOCK on this Editor.
  # Not fixed here: no Phase 8 task needs PlayMode. Left honest rather than
  # silently routed somewhere that would report a false green.
  "$UNITY" -batchmode -runTests \
    -projectPath "$(pwd)/client" \
    -testPlatform "$PLATFORM" \
    -testResults "$RESULTS" \
    -logFile "$LOG"
  code=$?
fi

echo "--- unity log: $LOG ---"
tail -40 "$LOG"

if [ -f "$RESULTS" ]; then
  python3 - "$RESULTS" <<'PY'
import sys, xml.etree.ElementTree as ET
r = ET.parse(sys.argv[1]).getroot()
print("total=%s passed=%s failed=%s skipped=%s" % (
    r.get("total"), r.get("passed"), r.get("failed"), r.get("skipped")))
for tc in r.iter("test-case"):
    if tc.get("result") not in ("Passed", "Skipped"):
        print("FAILED:", tc.get("fullname"))
        m = tc.find(".//message")
        if m is not None and m.text: print("   ", m.text.strip().splitlines()[0])
PY
else
  echo "FAIL: Unity wrote no results file — the run did not reach the tests."
  echo "      Most often the editor has the project open; Unity refuses a second instance."
  exit 1
fi
# Unity: 0 = all passed, 2 = tests failed
exit $code
