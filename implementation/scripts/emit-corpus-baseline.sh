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
#
# mtime, not existence. The old guard asked whether the file was absent
# afterwards, which it never is: it is TRACKED, so it is on disk before this
# script starts and stays there whether or not the emitter writes a byte. The
# question worth asking is whether this run touched it.
before=$(shasum "$BASELINE" 2>/dev/null | cut -d' ' -f1 || echo "absent")
before_mtime=$(stat -f %m "$BASELINE" 2>/dev/null || stat -c %Y "$BASELINE" 2>/dev/null || echo 0)

out=$(dotnet test Broodline.sln --nologo \
  --filter "FullyQualifiedName~CorpusBaselineTests.Emit" \
  -e BROODLINE_EMIT_BASELINE=1 2>&1) || { echo "$out"; echo "FAIL: emitter run failed"; exit 1; }

# A bash string test rather than `echo "$out" | grep -q`. grep -q exits the
# moment it matches, which closes the pipe and hands echo a SIGPIPE; under
# `set -o pipefail` that makes the PIPELINE non-zero on a MATCH, so the guard
# reads as "no problem" in exactly the case it was written to catch.
if [[ "$out" == *"No test matches"* ]]; then
  echo "$out"
  echo "FAIL: the emitter test did not run - the filter matched nothing."
  exit 1
fi

# The summary, on the success path too. Swallowing it meant the one line that
# says how many tests actually ran was visible only when something else had
# already failed.
echo "$out" | grep -E 'Passed!|Failed!|Passed:|passed' | tail -2 || true

after=$(shasum "$BASELINE" 2>/dev/null | cut -d' ' -f1 || echo "absent")
after_mtime=$(stat -f %m "$BASELINE" 2>/dev/null || stat -c %Y "$BASELINE" 2>/dev/null || echo 0)

[ "$after" != "absent" ] || { echo "FAIL: $BASELINE is gone"; exit 1; }
[ "$after_mtime" != "$before_mtime" ] || {
  echo "FAIL: $BASELINE was not rewritten by this run."
  echo "The emitter is gated on BROODLINE_EMIT_BASELINE=1 and writes to the"
  echo "PROJECT folder (TestPaths.ProjectDir), not to bin/. If that lookup"
  echo "moved, this script has been silently regenerating nothing."
  exit 1
}

if [ "$before" = "$after" ]; then
  echo "--- $BASELINE unchanged: the engine already reproduces it ---"
else
  echo "--- regenerated: $BASELINE ---"
fi
git --no-pager diff --stat -- "$BASELINE"
