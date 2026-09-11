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
#
# GNU FIRST. On coreutils -f is --file-system and takes no argument, so
# `stat -f %m FILE` parses %m as a filename, prints a multi-line filesystem
# report to stdout, and exits 1 - after which the fallback APPENDS the real
# mtime, leaving a blob containing free-block counts that drift on their own.
# The guard below would then go green on disk activity rather than on a write.
# BSD stat rejects -c cleanly, so this order is correct on both hosts.
mtime () { stat -c %Y "$1" 2>/dev/null || stat -f %m "$1" 2>/dev/null; }

before=$(shasum "$BASELINE" | cut -d' ' -f1)
before_mtime=$(mtime "$BASELINE")

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

after=$(shasum "$BASELINE" | cut -d' ' -f1)
after_mtime=$(mtime "$BASELINE")

# THE POLICY, ENFORCED HERE RATHER THAN IN A TEST.
#
# The emitter regenerates the whole file from the current engine, so the header
# it writes is ALWAYS the current SimVersion. A test comparing the header to
# SimVersion therefore cannot catch a re-baseline under an unbumped version -
# the file agrees with itself. The deliberate act is this script, so the guard
# belongs in this script.
#
# Hashes compared WITHOUT the header, or a bump alone would read as a
# behaviour change and mask the thing being checked.
body_before=$(git show HEAD:"$BASELINE" | grep -v '^# simversion ' | shasum | cut -d' ' -f1)
body_after=$(grep -v '^# simversion ' "$BASELINE" | shasum | cut -d' ' -f1)
ver_before=$(git show HEAD:"$BASELINE" | sed -n 's/^# simversion //p')
ver_after=$(sed -n 's/^# simversion //p' "$BASELINE")

if [ "$body_before" != "$body_after" ] && [ "$ver_before" = "$ver_after" ]; then
  git checkout -- "$BASELINE"
  echo "FAIL: the engine's output changed but SimVersion did not."
  echo ""
  echo "  SimVersion.Value is still '$ver_after'."
  echo ""
  echo "A replay stores the version it was recorded under, and a replay whose"
  echo "version matches the running engine is RE-SIMULATED rather than shown."
  echo "Re-baselining without a bump means old replays silently re-simulate"
  echo "into different outcomes - solo_execution section 9.4 exists to stop"
  echo "exactly that."
  echo ""
  echo "Bump engine/Runtime/SimVersion.cs, then run this script again."
  echo "$BASELINE has been restored."
  exit 1
fi

# No existence check. The file is TRACKED - the comment above says so - so it is
# on disk before this script starts and stays there whether or not the emitter
# writes a byte; asking whether it is absent afterwards was checking a condition
# this script's own rationale declares impossible. `set -e` plus an unguarded
# shasum covers the genuinely impossible case.
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
