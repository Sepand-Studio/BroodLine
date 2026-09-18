#!/usr/bin/env bash
# Asserts every .uss under client/Assets compiles to at least one rule.
#
# WHAT THIS CATCHES THAT NOTHING ELSE DOES: a USS parse error is not a build
# failure. The asset still imports, the sheet still loads non-null, the panel
# still renders - just without the rules. Tokens.uss shipped that way and
# took Broodline's whole design system down with it, silently, past a green
# compile and 267 passing tests. See StylesheetCheck.cs for the full account.
#
# -logFile IS A REAL PATH, NEVER "-" - see capture-screens.sh for why.
set -uo pipefail
cd "$(dirname "$0")/../.."

UNITY="/Applications/Unity/Hub/Editor/6000.6.0f1/Unity.app/Contents/MacOS/Unity"
LOG="$(pwd)/implementation/results/check-stylesheets.log"
mkdir -p implementation/results
rm -f "$LOG"

[ -x "$UNITY" ] || { echo "FAIL: Unity not found at $UNITY"; exit 127; }
[ -d client ] || { echo "FAIL: no client/ project"; exit 1; }

"$UNITY" -batchmode -quit -projectPath "$(pwd)/client" \
  -executeMethod StylesheetCheck.Run \
  -logFile "$LOG"
code=$?

# Report parse errors whether or not they killed a sheet outright - a sheet
# can lose one rule to a bad selector and keep the rest, which this check's
# rule-count test would pass but a reviewer should still see.
if grep -q "USS parsing error" "$LOG" 2>/dev/null; then
  echo "--- USS parse errors ---"
  grep "USS parsing error" "$LOG" | sort -u
fi

grep -E "^\[StylesheetCheck\] (OK|[0-9]+ of)" "$LOG" 2>/dev/null
grep -A 20 "compiled to nothing" "$LOG" 2>/dev/null | grep -E "^\s+Assets/"

[ "$code" -eq 0 ] || { echo "FAIL: stylesheet check exited $code. See $LOG."; exit "$code"; }
echo "OK: all stylesheets compile."
