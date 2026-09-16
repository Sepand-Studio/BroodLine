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
# reads as "no problem" in exactly the case it was written to catch. The
# per-line loop below is for the same reason - no pipeline, so no SIGPIPE to
# invert.
#
# SCOPED TO THE ASSEMBLY THAT OWNS THE TEST, and that is not a refinement -
# unscoped, this guard failed every healthy run. `dotnet test Broodline.sln`
# walks EVERY test project, and Phase 5 added a second one; vstest prints
# "No test matches the given testcase filter ... in Broodline.Sim.Service.Tests.dll"
# for it, correctly, because that project has no CorpusBaselineTests and never
# will. So the guard fired on a run in which the emitter had passed - AFTER the
# emitter had already rewritten the tracked baseline, and BEFORE the policy
# guard below ever executed. The safety property this script exists for -
# refusing a re-baseline that was not accompanied by a SimVersion bump - was
# unreachable, and the failure it printed instead named the wrong cause.
EMITTER_ASSEMBLY=Broodline.Sim.Tests.dll
unmatched=""
while IFS= read -r line; do
  case "$line" in
    *"No test matches"*"$EMITTER_ASSEMBLY"*) unmatched="$line" ;;
  esac
done <<< "$out"

if [ -n "$unmatched" ]; then
  echo "$out"
  echo "FAIL: the emitter test did not run - the filter matched nothing in $EMITTER_ASSEMBLY."
  exit 1
fi

# The summary, on the success path too. Swallowing it meant the one line that
# says how many tests actually ran was visible only when something else had
# already failed.
echo "$out" | grep -E 'Passed!|Failed!|Passed:|passed' | tail -2 || true

after=$(shasum "$BASELINE" | cut -d' ' -f1)
after_mtime=$(mtime "$BASELINE")

# No existence check. The file is TRACKED - the comment above says so - so it is
# on disk before this script starts and stays there whether or not the emitter
# writes a byte; asking whether it is absent afterwards was checking a condition
# this script's own rationale declares impossible. `set -e` plus an unguarded
# shasum covers the genuinely impossible case.
#
# THIS CHECK RUNS BEFORE THE POLICY GUARD BELOW, DELIBERATELY. The guard's
# "before" and "after" are only meaningful once we know THIS run actually
# wrote the file - otherwise a silent no-op emitter (this check's own job to
# catch) leaves a stale or hand-edited working copy sitting there, the guard
# below hashes that instead of anything the engine produced, and reports
# "the engine's output changed" for a failure that has nothing to do with the
# engine. Ordering this first means that failure gets the accurate message.
[ "$after_mtime" != "$before_mtime" ] || {
  echo "FAIL: $BASELINE was not rewritten by this run."
  echo "The emitter is gated on BROODLINE_EMIT_BASELINE=1 and writes to the"
  echo "PROJECT folder (TestPaths.ProjectDir), not to bin/. If that lookup"
  echo "moved, this script has been silently regenerating nothing."
  exit 1
}

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

if [ -z "$ver_before" ]; then
  # The committed baseline predates this guard - it has no header at all, so
  # there is no earlier version to compare against and this run cannot be a
  # regression by construction. Chosen deliberately, not an accident of the
  # string comparisons below: this is the one-time bootstrap that GIVES the
  # file its first header.
  :
elif [ "$ver_before" = "$ver_after" ]; then
  if [ "$body_before" != "$body_after" ]; then
    git checkout HEAD -- "$BASELINE"
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
else
  # ver_before and ver_after differ, which ordinarily means a legitimate
  # bump. But "differ" is not "moved forward": SimVersion.Value is NOT
  # semantic (see its own doc comment) - there is no ordering to check - and
  # a REVERT (a bad merge or a rebase that drops a later bump) also makes
  # these differ, letting a real behaviour change ride along disguised as a
  # bump. Concretely: reverting SimVersion.cs to an older value the tracked
  # device artifact was recorded under makes that artifact read as current
  # again, and re-arms silent re-simulation across whatever actually changed.
  #
  # What IS checkable without an ordering: whichever body a version string
  # has ever named, it must keep naming that body. So if ver_after has been
  # committed before, its body then and its body now must agree - if they
  # don't, this is that version being reused over different behaviour, which
  # is exactly the unsafe case whether or not it looks like a "revert".
  if [ "$body_before" != "$body_after" ]; then
    prior_commit=$(git log --format=%H -S"# simversion $ver_after" -- "$BASELINE" | tail -1)
    if [ -n "$prior_commit" ]; then
      prior_body=$(git show "$prior_commit":"$BASELINE" | grep -v '^# simversion ' | shasum | cut -d' ' -f1)
      if [ "$prior_body" != "$body_after" ]; then
        git checkout HEAD -- "$BASELINE"
        echo "FAIL: SimVersion '$ver_after' was already used (commit ${prior_commit:0:8}) with DIFFERENT engine output."
        echo ""
        echo "A version string must always mean the same behaviour - AreCurrent"
        echo "compares SimVersion.Value with no notion of direction, so a replay"
        echo "tagged '$ver_after' is treated as current whichever way the running"
        echo "engine arrived at that value. This looks like a revert - a merge or"
        echo "rebase that dropped a later bump - reintroducing an old version"
        echo "number over new behaviour."
        echo ""
        echo "Give this change its own, never-before-used SimVersion.Value."
        echo "$BASELINE has been restored."
        exit 1
      fi
    fi
  fi
fi

if [ "$before" = "$after" ]; then
  echo "--- $BASELINE unchanged: the engine already reproduces it ---"
else
  echo "--- regenerated: $BASELINE ---"
fi
git --no-pager diff --stat -- "$BASELINE"
