#!/usr/bin/env bash
# Runs Unity EditMode tests headlessly. Usage: run-unity-tests.sh [EditMode|PlayMode]
set -uo pipefail
cd "$(dirname "$0")/../.."
UNITY="/Applications/Unity/Hub/Editor/6000.6.0f1/Unity.app/Contents/MacOS/Unity"
PLATFORM="${1:-EditMode}"
RESULTS="$(pwd)/implementation/results/test-results-$PLATFORM.xml"
mkdir -p implementation/results

[ -x "$UNITY" ] || { echo "FAIL: Unity not found at $UNITY"; exit 127; }
[ -d client ] || { echo "FAIL: no client/ project — Task 2 must run first"; exit 1; }

"$UNITY" -batchmode -runTests \
  -projectPath "$(pwd)/client" \
  -testPlatform "$PLATFORM" \
  -testResults "$RESULTS" \
  -logFile - 2>&1 | tail -40
code=${PIPESTATUS[0]}

echo "--- unity exit code: $code ---"
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
  echo "no results file written"
fi
# Unity: 0 = all passed, 2 = tests failed
exit $code
