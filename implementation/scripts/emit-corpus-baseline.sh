#!/usr/bin/env bash
# Regenerates tests/engine/corpus-baseline.txt from the CURRENT engine.
#
# This is a DELIBERATE re-baseline, not a routine step. Running it makes
# CorpusBaselineTests pass by definition, so it must only be run when the
# behaviour change is intended and understood - a balance change, or a state
# vector that genuinely grew. If a test went red and you are here looking for
# the button that makes it green, you are in the wrong place.
set -euo pipefail
cd "$(dirname "$0")/../.."
BASELINE=tests/engine/corpus-baseline.txt

# dotnet test EXITS 0 WHEN A FILTER MATCHES NOTHING. A typo in the filter, or a
# rename of the test, made this script print its success banner and an empty
# diff while regenerating nothing - and "nothing changed" is the reassuring
# reading. So the run is captured and the match count asserted.
before=$(shasum "$BASELINE" 2>/dev/null | cut -d' ' -f1 || echo "absent")

out=$(dotnet test Broodline.sln --nologo \
  --filter "FullyQualifiedName~CorpusBaselineTests.Emit" \
  -e BROODLINE_EMIT_BASELINE=1 2>&1) || { echo "$out"; echo "FAIL: emitter run failed"; exit 1; }

if echo "$out" | grep -q "No test matches"; then
  echo "$out"
  echo "FAIL: the emitter test did not run - the filter matched nothing."
  exit 1
fi

after=$(shasum "$BASELINE" 2>/dev/null | cut -d' ' -f1 || echo "absent")
[ "$after" != "absent" ] || { echo "FAIL: $BASELINE was not written"; exit 1; }

if [ "$before" = "$after" ]; then
  echo "--- $BASELINE unchanged: the engine already reproduces it ---"
else
  echo "--- regenerated: $BASELINE ---"
fi
git --no-pager diff --stat -- "$BASELINE"
