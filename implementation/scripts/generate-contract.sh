#!/usr/bin/env bash
# Regenerates BOTH halves of the cross-language contract:
#
#   Direction 1: Unity <- api.  TypeScript (Zod) is the source of truth.
#                openapi/broodline.json, then the NSwag C# client.
#   Direction 2: api -> sim.    C# (ASP.NET minimal API) is the source of
#                truth. openapi/sim.json, then the openapi-typescript client.
#
# ALL FOUR outputs are COMMITTED, so a contract change is a reviewable diff
# and neither side needs to run the other's toolchain to consume it -
# solo_execution section 6, rules 2 and 3. The INTENT is that CI runs this
# and fails on a non-empty diff, which is what would stop a generated client
# drifting from the schema/handlers it is generated from - but no CI runs on
# this repository today: GitHub Actions is disabled at the organisation
# level and enabling it is blocked on the owner. Making that guarantee real,
# rather than aspirational, is Task 12's job. In the meantime
# services/api/test/contract.test.ts runs this script and asserts the tree
# is clean afterwards, so the gate at least runs with the test suite.
#
# ONE script, ONE gate, covering both directions - design 2.4. Two scripts
# invite the second one being forgotten in CI.
set -euo pipefail
cd "$(dirname "$0")/../.."

# --- The dotnet-build lock. This script's Direction 2 build (below) and
# services/api/test/wave-submit.test.ts / replays.test.ts /
# adversarial.test.ts each build
# services/sim/Broodline.Sim.Service.csproj independently for their own sim
# instance. MSBuild's -o/--output overrides OutputPath but never
# BaseIntermediateOutputPath, so all four share services/sim/obj/
# regardless of where -o points, and two concurrent builds racing there
# intermittently fail with "the process cannot access the file
# ...rjsmrazor.dswa.cache.json". This bash copy cannot import
# wave-helpers.ts's withDotnetBuildLock() (that is the shared definition
# for the two TS callers, same reasoning as every other helper in that
# file) so it re-implements the identical protocol by hand - the path
# construction, staleness threshold and reclaim logic below MUST be kept in
# sync with that file's lockDir()/isStale()/acquireOnce(); a path or
# timeout edit applied to only one side yields two locks, no exclusion, and
# a flake indistinguishable from the one this exists to close.
#
# Ownership is an owner file written inside the lock dir right after mkdir
# claims it, holding `<pid>:<nonce>` - NOT a bare pid. The nonce is what
# makes two GENERATIONS of the lock distinguishable even when they share a
# pid; see wave-helpers.ts's factory comment for the full argument (on that
# side a bare pid would have made the whole protocol depend on vitest's
# pool setting). The two sides only ever compare owner values for equality
# and split the pid off the front, so all they must agree on is "pid first,
# then a colon" - which is why this uses $$ plus $RANDOM entropy rather
# than trying to mirror node's randomUUID. A hard kill or Ctrl-C runs
# neither this script's own trap NOR
# node's try/finally in the TS copy, so without staleness detection a
# single interrupted run would leave the lock stuck and poison every later
# run - all three call sites, indefinitely, until a human manually removes
# a /tmp path. (An earlier version of this comment claimed a crash "never
# leaves the lock stuck," which was false - see wave-helpers.ts's comment
# on the same point.) Reclaim: on contention, if the owner pid is
# confirmed dead (or unconfirmable, via the age fallback below), remove the
# lock and let the NEXT mkdir decide who actually gets it - removal itself
# grants no ownership, so two waiters racing to reclaim cannot both end up
# believing they hold it.
#
# Must match wave-helpers.ts's repoRoot()+lockDir() byte-for-byte (both
# hash the repo root with no trailing slash/newline) so this script and the
# two TS callers converge on the exact same lock path.
if command -v shasum >/dev/null 2>&1; then
  REPO_HASH="$(printf '%s' "$(pwd)" | shasum -a 256 | cut -c1-10)"
else
  REPO_HASH="$(printf '%s' "$(pwd)" | sha256sum | cut -c1-10)"
fi
# Namespaced by OS user (so two developers/CI identities sharing one /tmp
# never contend or block each other - load-bearing, since /tmp's sticky bit
# means one user cannot remove another user's stale lock directory at all)
# and by REPO_HASH (so two clones of this repo do not share a lock either -
# lower stakes, since sharing one there would only over-serialize two
# otherwise-independent clones, not produce a correctness bug).
BUILD_LOCK="${TMPDIR:-/tmp}/broodline-sim-dotnet-build-$(id -u)-${REPO_HASH}.lock"
BUILD_LOCK_OWNER_FILE="$BUILD_LOCK/owner.pid"
# This run's owner value, computed ONCE (this script acquires the lock at
# most once per run, so per-run and per-acquisition are the same thing
# here). Three $RANDOM draws, because a single one is only 0..32767.
BUILD_LOCK_OWNER="$$:${RANDOM}${RANDOM}${RANDOM}"
BUILD_LOCK_STALE_SECONDS=300            # critical section is ~1-2s; minutes is a generous margin
# A SEPARATE, much longer ceiling from the above - deliberately not folded
# into one OR, and deliberately not a heartbeat (disproportionate here).
# BUILD_LOCK_STALE_SECONDS governs the "liveness unconfirmable" case, where
# a short margin is safe because the critical section is short.
# BUILD_LOCK_ABSOLUTE_CEILING_SECONDS governs the "liveness CONFIRMED alive"
# case instead, which the short margin cannot: if the OS recycles a
# SIGKILLed owner's pid onto some unrelated, still-running process,
# build_lock_is_stale's liveness check reports "alive" forever, and a stuck
# lock is not bounded by a short critical section - it is abandoned, and
# sits for as long as nobody clears it. Set far above any plausible
# legitimate build so it can never steal from one. Mirrors
# wave-helpers.ts's LOCK_ABSOLUTE_CEILING_MS.
BUILD_LOCK_ABSOLUTE_CEILING_SECONDS=1800  # 30 minutes
BUILD_LOCK_TIMEOUT_TENTHS=600            # 60s, in 0.1s polling ticks

# Reads the owner file, or prints nothing if it is missing or unreadable.
#
# The `|| true` is LOAD-BEARING and this is deliberately a function rather
# than three copies of the idiom. The previous shape,
#
#     local observed=""
#     [ -f "$BUILD_LOCK_OWNER_FILE" ] && observed="$(cat "$BUILD_LOCK_OWNER_FILE" 2>/dev/null)"
#
# is a TOCTOU that `set -e` turns into a SILENT ABORT of the whole script.
# `[ -f ]` and `cat` are two separate syscalls, and the gap between them is
# exactly when a releasing holder's `rm -rf` lands - this lock exists
# because four call sites contend here, so that gap gets hit. When it is:
# the test succeeds, `cat` fails, stderr is discarded by the `2>/dev/null`,
# the `&&` list's status is 1, and because it is a top-level statement
# (not an `if` condition, where `set -e` would be suspended) the script
# exits 1 having printed NOTHING about why.
#
# That was not theoretical. It is the diagnosed cause of the intermittent
# `contract.test.ts` failure this phase had been carrying as an
# unexplained, supposedly-environmental flake: `Command failed:
# ./implementation/scripts/generate-contract.sh` with no further output,
# roughly 1 run in 20 of the full suite and 1 in 5 under deliberate
# contention. Introduced with the owner file in round 2 and reproduced
# under a DEBUG trap, which showed the abort landing on this exact `cat`.
#
# `cat ... 2>/dev/null || true` collapses "missing", "unreadable" and
# "vanished underneath us" into the same empty string, which is what every
# caller here already wants: build_lock_is_stale treats an empty owner as
# "liveness unconfirmable" and falls back to the age check, and both
# comparisons against it simply fail to match.
read_build_lock_owner() {
  cat "$BUILD_LOCK_OWNER_FILE" 2>/dev/null || true
}

# Whether $BUILD_LOCK is abandoned and safe to reclaim. Mirrors
# wave-helpers.ts's isStale(): a missing/unreadable owner file falls back
# to the lock dir's own mtime rather than being treated as stale outright,
# which protects the brief window between mkdir succeeding and the owner
# file being written; a CONFIRMED-alive owner is checked against the
# absolute ceiling rather than trusted forever, guarding pid reuse.
build_lock_is_stale() {
  # $1 is the OWNER VALUE (`<pid>:<nonce>`); liveness needs only the pid,
  # so strip from the first colon. A value with no colon (a bare pid, which
  # is what a lock left behind by an older build of this script or of
  # wave-helpers.ts contains) is left intact, so an in-flight upgrade
  # cannot produce an unreclaimable lock.
  local pid="${1%%:*}"

  [ -d "$BUILD_LOCK" ] || return 1  # already gone; the next mkdir will just succeed
  local mtime now age
  # BSD stat first, GNU second, then `|| true` - and the emptiness check
  # below, which is the point. `[ -d ]` above and this stat are two
  # syscalls, and the lock can be released in between; when it is, BSD stat
  # fails, the GNU fallback runs on macOS where `-c` is not a legal option
  # and prints "stat: illegal option -- c" into this script's stderr (seen
  # in the wild, forwarded verbatim through contract.test.ts), and mtime
  # ends up empty. Empty then evaluates as 0 inside $(( )), making `age`
  # the entire Unix epoch and every threshold comparison below trivially
  # true - i.e. a confidently WRONG "this lock is stale" verdict derived
  # from a lock that no longer exists. Treat a missing mtime the same way
  # the `[ -d ]` check treats a missing directory: not stale, let the next
  # mkdir decide.
  mtime="$(stat -f %m "$BUILD_LOCK" 2>/dev/null || stat -c %Y "$BUILD_LOCK" 2>/dev/null || true)"
  [ -n "$mtime" ] || return 1
  now="$(date +%s)"
  age=$((now - mtime))

  if [ -n "$pid" ] && [ "$pid" -eq "$pid" ] 2>/dev/null; then
    if kill -0 "$pid" 2>/dev/null; then
      # Confirmed alive per the OS - but NOT unconditionally trusted: see
      # BUILD_LOCK_ABSOLUTE_CEILING_SECONDS's comment above for why a
      # recycled pid must not pin this lock stuck forever.
      [ "$age" -gt "$BUILD_LOCK_ABSOLUTE_CEILING_SECONDS" ]
      return $?
    fi
    # kill -0 failed: this shell cannot reliably distinguish "no such
    # process" (owner gone) from "not permitted" (owned by another user,
    # still alive) by exit code alone, so fall through to the shorter age
    # check below rather than guess - same conservative handling the TS
    # side applies to EPERM.
  fi

  [ "$age" -gt "$BUILD_LOCK_STALE_SECONDS" ]
}

# Re-verify, then destroy. ONE layer, deliberately. An earlier version had
# a second - `mv` the lock aside (a rename(2)) and then rm the detached
# copy - justified by a comment claiming neither layer sufficed alone.
# Round 4's review DISPROVED that by direct experiment: at the one
# interleaving the comment said the mv earned its keep on, blind rm and
# atomic mv produced identical outcomes. mv detaches whatever generation is
# at the path just as blindly as rm removes it; the only case it genuinely
# wins is two reclaimers destroying the same STILL-DEAD lock, which is
# harmless either way (one wins, the other's rm no-ops on a gone path).
#
# It had also acquired a cost: "${BUILD_LOCK}.reclaim-<pid>-<rand>" is a
# name nothing ever matches or cleans, so a hard kill landing between the
# mv and the rm left that directory in TMPDIR forever - and hard kills
# mid-build are precisely the scenario this lock exists for. Removed on
# both sides; wave-helpers.ts's reclaim() carries the same note.
#
# What does the work is the re-read: called with the EXACT owner value
# build_lock_is_stale judged (`$1`, not re-derived), it re-reads the
# CURRENT owner file immediately before touching anything and compares. A
# mismatch - even to a value this function cannot itself interpret - means
# some OTHER, legitimate acquirer has claimed this path since the decision
# was made, and reclaim aborts rather than destroying work that is not its
# to destroy.
#
# THE WINDOW IS NARROWED, NOT CLOSED - this is a check-then-act, and the
# act (a fork/exec'd `rm`) can still be delayed past another contender's
# reclaim+mkdir+owner-write, which round 3 measured at roughly 20ms here.
# That is ordinary jitter, so it is an ACCEPTED RESIDUAL, not a closed
# hole, and it is NOT the same kind of thing as mkdir's own atomic EEXIST.
# Closing it properly needs a compare-and-swap primitive a lock directory
# does not offer; disproportionate for test infrastructure whose worst case
# is one flaky `dotnet build`. See wave-helpers.ts's reclaim() for the
# longer version of this note.
#
# A SECOND residual sits alongside it, and the owner value's nonce cannot
# close this one - it acts on a value that does not exist yet. During the
# mkdir -> owner-write window there is no owner file at all, so `$observed`
# and `$still_there` are BOTH the empty string, `[ "$still_there" =
# "$observed" ]` compares equal, and the rm proceeds. It takes an ownerless
# lock dir aged past BUILD_LOCK_STALE_SECONDS (a crash inside that window):
# A reads an absent owner and judges stale, another contender reclaims,
# mkdirs a fresh lock and has not yet written its owner, and A's re-verify
# reads absent too, matches, and removes that LIVE lock. wave-helpers.ts's
# reclaim() has the identical hole with `undefined` in place of "". Accepted
# for the same reason: telling "absent because not written yet" from "absent
# because never written" needs the compare-and-swap this design rules out.
build_lock_reclaim() {
  local observed="$1"
  # read_build_lock_owner, NOT `[ -f ... ] && x="$(cat ...)"` - see that
  # function's comment. The file vanishing between the test and the read is
  # not hypothetical here: it is precisely the "someone else reclaimed it
  # first" case this line exists to detect.
  local still_there
  still_there="$(read_build_lock_owner)"
  [ "$still_there" = "$observed" ] || return 0  # changed since the decision - not ours to reclaim

  rm -rf "$BUILD_LOCK" 2>/dev/null || true
}

acquire_build_lock() {
  local waited=0
  while true; do
    local mkdir_err=""
    if mkdir_err="$(mkdir "$BUILD_LOCK" 2>&1)"; then
      echo "$BUILD_LOCK_OWNER" > "$BUILD_LOCK_OWNER_FILE"
      return 0
    fi
    if [ ! -d "$BUILD_LOCK" ]; then
      # mkdir failed and the directory still does not exist a moment
      # later - could be a real error (permission denied, ENOSPC, a bad
      # path), OR ordinary contention where the holder released between
      # the failed mkdir and this check (a real, if sub-millisecond,
      # window). Retry mkdir once immediately before concluding it is a
      # real error: contention resolves on the retry; a genuine error
      # fails identically and IS reported, rather than spinning the full
      # 60s toward a misleading "timed out."
      local retry_err=""
      if retry_err="$(mkdir "$BUILD_LOCK" 2>&1)"; then
        echo "$BUILD_LOCK_OWNER" > "$BUILD_LOCK_OWNER_FILE"
        return 0
      fi
      if [ ! -d "$BUILD_LOCK" ]; then
        echo "dotnet build lock: mkdir failed: $retry_err" >&2
        exit 1
      fi
      # else: contention again (someone else won the retry too) - fall
      # through to the normal staleness check / poll below.
    fi
    # Read ONCE, feed the same observed value to both the staleness
    # decision and reclaim's later re-check - reading twice here would
    # just move the ABA window rather than close it.
    local observed
    observed="$(read_build_lock_owner)"
    if build_lock_is_stale "$observed"; then
      build_lock_reclaim "$observed"
      # No sleep: retry mkdir immediately - whether the reclaim removed the
      # lock or aborted because someone else now owns it, mkdir is the thing
      # that decides who gets it, and sleeping first would just hand the
      # path to a slower contender.
      :
    else
      sleep 0.1
    fi
    # The counter advances on EVERY iteration, including the reclaim ones.
    # An earlier version `continue`d straight past this from the reclaim
    # branch, so BUILD_LOCK_TIMEOUT_TENTHS did not advance there at all: no
    # sustained interleaving that reaches it repeatedly was constructible
    # (a changed owner is either alive, which takes the sleep path, or
    # reclaimable on the next iteration), but a persistently failing rm -
    # a read-only TMPDIR, say - would have spun hot forever with the 60s
    # cap effectively disabled. Counting reclaim iterations makes the cap
    # a real bound in every case; it costs only that a reclaim-heavy wait
    # burns ticks faster than 0.1s each, which is the correct trade for a
    # timeout whose job is to stop rather than to measure.
    waited=$((waited + 1))
    if [ "$waited" -gt "$BUILD_LOCK_TIMEOUT_TENTHS" ]; then
      echo "timed out waiting for the dotnet build lock at $BUILD_LOCK" >&2
      exit 1
    fi
  done
}

release_build_lock() {
  # Only remove the lock if it is still OURS - never a blind rm. Safe to
  # call unconditionally (cleanup() below does, on every exit path): a
  # no-op if this process never acquired the lock or already released it.
  # Compared as the FULL owner value, not by pid: a pid comparison would be
  # satisfied by any generation this process wrote, including one a
  # reclaimer has since handed to someone else. Reads through
  # read_build_lock_owner so all three readers in this script share one
  # shape and nobody re-introduces the `set -e` hazard by copying the wrong
  # one (this particular read was already safe - a `[ -f ]` inside an `if`
  # condition is exempt from `set -e` - but that is far too subtle a
  # distinction to leave standing next to two that were not).
  if [ "$(read_build_lock_owner)" = "$BUILD_LOCK_OWNER" ]; then
    rm -rf "$BUILD_LOCK" 2>/dev/null || true
  fi
}

# Both generators write to a scratch directory first and are moved into place
# together only once BOTH have succeeded. Without this, `pnpm openapi`
# writing openapi/broodline.json and THEN `dotnet nswag` failing leaves the
# committed OpenAPI document already rewritten while the C# client is still
# generated from the old one - an inconsistent tree that the next successful
# run silently overwrites, so the window it was ever in that state is never
# noticed even though a commit could land during it.
WORK="$(mktemp -d)"

# ONE EXIT trap for the whole script, covering both directions. SIM_PID is
# unset until Direction 2 starts the sim host; cleanup() no-ops that part
# until then. Without killing the background `dotnet run` on every exit path
# - including set -e failing out of the middle of Direction 2 - a failed
# curl leaves a dotnet process holding port 5199, and the NEXT run of this
# script fails with a misleading "address already in use" bind error that
# has nothing to do with what actually broke.
SIM_PID=""
cleanup() {
  if [ -n "$SIM_PID" ]; then
    kill "$SIM_PID" 2>/dev/null || true
  fi
  # release_build_lock is ownership-checked and safe to call unconditionally
  # on every exit path, including a `set -e` failure mid-build: a no-op if
  # this process never acquired the lock, or already released it. No
  # separate "did we hold it" flag is needed (an earlier version tracked
  # one in BUILD_LOCK_HELD, which had its own race between mkdir succeeding
  # and the flag being set/cleared - see wave-helpers.ts's releaseOnce()
  # for why ownership-checking replaces that instead of patching around it).
  release_build_lock
  # -rf, so a $WORK already removed by an earlier call is not an error: this
  # function is called on both the signal path and the EXIT path and MUST
  # tolerate running twice. Verified by direct experiment, not by reading -
  # calling cleanup() twice in a row with a live SIM_PID, a held lock and an
  # existing $WORK leaves the same end state as calling it once, and returns
  # 0 both times (a non-zero return from a trap handler under `set -e` would
  # be its own silent-abort hazard).
  rm -rf "$WORK"
}

# Teardown on a signal. WHY THIS IS NOT `trap cleanup EXIT INT TERM`:
#
# A trap handler that does not exit RESUMES the script. Bash runs the
# handler when the current foreground command returns and then carries on
# from where it left off - so the one-line shape would kill the sim, release
# the build lock and `rm -rf` the scratch directory UNDERNEATH A STILL-
# RUNNING SCRIPT, which then curls a host it just killed and `mv`s files out
# of a directory that no longer exists. That is a worse failure than the one
# it is meant to fix, and it is not hypothetical: it is what this script did
# when the one-line shape was tried here first.
#
# And note what this is NOT fixing, because the ledger item overstated it.
# `trap cleanup EXIT` ALONE already runs on Ctrl-C, on vitest's SIGTERM and
# on SIGHUP: bash installs handlers for its terminating signals and runs the
# EXIT trap before re-raising with the default disposition. Measured against
# this script, not assumed - SIGTERM mid-run exits 143 with cleanup having
# run, nothing left holding 5199 and no stray dotnet; group SIGINT (what
# Ctrl-C actually delivers) exits 130 the same way.
#
# The case bash does NOT cover is a SIGINT delivered to this script's PID
# alone - `kill -INT <pid>` from another terminal, or any supervisor that
# signals a single process rather than a group. Bash only terminates on
# SIGINT when the foreground child it is waiting on died of SIGINT too; when
# the child is untouched (it is in the same process group but was not
# signalled), bash swallows the interrupt entirely and the script runs to
# completion. Measured: unguarded, a single-pid SIGINT delivered while the
# sim host was up left the script running through the rest of Direction 2
# and exiting 0, interrupt ignored.
#
# So these two handlers do two things: they make that swallowed case
# terminate promptly with the teardown run, and they make the other three
# EXPLICIT rather than resting on a bash implementation detail that no other
# shell shares - this script is `#!/usr/bin/env bash`, but "the EXIT trap
# happens to fire on fatal signals" is not a property worth depending on
# silently for the one thing that frees port 5199.
on_signal() {
  # Disarm first: this handler owns the teardown from here, and a cleanup
  # running from both the signal path and the EXIT path is redundant rather
  # than harmful only because cleanup is idempotent. Disarming makes the
  # single run the normal case and keeps idempotency as the belt, not the
  # braces.
  trap - EXIT INT TERM
  cleanup
  # 128+n, not 1. A parent (vitest, a CI runner, an interactive shell) reads
  # 130/143 as "died of SIGINT/SIGTERM"; exiting 1 would present an
  # interrupted run as an ordinary generator failure and send whoever is
  # reading the log looking for a contract defect that is not there.
  case "$1" in
    INT) exit 130 ;;
    TERM) exit 143 ;;
  esac
  exit 1
}
trap cleanup EXIT
trap 'on_signal INT' INT
trap 'on_signal TERM' TERM

# ---- Direction 1: Unity <- api. TypeScript (Zod) is the source of truth. ----

OPENAPI_OUT="openapi/broodline.json"
CLIENT_OUT="client/Assets/Generated/Api/BroodlineApiClient.cs"
OPENAPI_TMP="$WORK/broodline.json"
CLIENT_TMP="$WORK/BroodlineApiClient.cs"
NSWAG_TMP="$WORK/nswag.generated.json"

# src/openapi.ts honours this to redirect its write; see that file.
OPENAPI_OUTPUT_PATH="$OPENAPI_TMP" pnpm openapi

dotnet tool restore

# nswag.json names fixed, committed input/output paths - point a scratch copy
# of it at the freshly-generated temp document and a temp output location
# instead, so a failing run never touches CLIENT_OUT.
python3 - "$OPENAPI_TMP" "$CLIENT_TMP" "$NSWAG_TMP" <<'PY'
import json, sys
openapi_tmp, client_tmp, nswag_tmp = sys.argv[1:4]
with open("nswag.json") as f:
    cfg = json.load(f)
cfg["documentGenerator"]["fromDocument"]["url"] = openapi_tmp
cfg["codeGenerators"]["openApiToCSharpClient"]["output"] = client_tmp
with open(nswag_tmp, "w") as f:
    json.dump(cfg, f)
PY

# This machine's only installed shared runtime is a major version newer than
# any NSwag.ConsoleCore 14.2.0 build targets (net8.0/net9.0). The exact-match
# framework resolution refuses to roll a *major* version forward by default,
# so without this the tool fails to launch at all with a missing-runtime
# error. LatestMajor lets the installed .NET 10 runtime host whichever build
# of the TOOL actually launches (net9.0, per `dotnet tool restore`'s own
# resolution - confirmed by the "NSwag bin directory: .../tools/net9.0/any"
# line nswag prints on every run).
export DOTNET_ROLL_FORWARD=LatestMajor

# nswag.json previously pinned "runtime": "Net90" to match that net9.0 tool
# build. That pin does NOT generalise, for two independent reasons found
# while wiring up Direction 2's second generator on this same script:
#
#   1. NSwag's own "runtime" field is a check against which FRAMEWORK BUILD
#      OF THE NSWAG TOOL ITSELF loaded (net8.0 vs net9.0, the two folders
#      NSwag.ConsoleCore 14.2.0 ships) - not against the host's installed
#      SDK/runtime major version. `dotnet --list-runtimes` on this machine
#      reports only Microsoft.NETCore.App 10.0.12, but the NSwag process
#      that actually runs still reports itself as "Net90" (its own
#      TargetFramework), because DOTNET_ROLL_FORWARD let the net9.0 tool
#      build roll forward onto the installed net10.0 shared runtime. Those
#      are two different "major versions" and conflating them breaks here:
#      naming the runtime "Net${DOTNET_MAJOR}0" from `dotnet --list-runtimes`
#      would compute "Net100", which is rejected before the check even runs
#      (see point 2).
#   2. "Net100" is not a spelling NSwag.Commands 14.2.0 accepts at all - its
#      Runtime enum defines exactly Default, WinX64, WinX86, Net80, Net90,
#      Debug (confirmed via `strings` and the package's XML doc comments).
#      Passing it throws a JsonSerializationException converting the value,
#      before any document is even loaded. There is no Net100 to detect.
#
# "runtime": "Default" ("Use default and do no checks", per NSwag's own XML
# doc) sidesteps both problems: it is a real, documented enum member that
# disables the equality check entirely, so it needs no detection logic and
# does not go stale as the installed SDK, or NSwag's own supported target
# frameworks, move forward. Verified empirically: with the OpenAPI document
# held fixed, "runtime": "Default" and the previous "runtime": "Net90"
# produce byte-identical generated C#, because the field only gates whether
# generation is ATTEMPTED - it has no effect on what gets generated.
#
# (NSwag's `run` command also does not accept a `/runtime:` command-line
# argument - only `/variables:name=value`, which /runtime: is not; passing
# it throws NConsole.UnusedArgumentException. So even with a valid spelling,
# "pass it on the command line instead of nswag.json" was not an available
# option here. The fix lives in nswag.json's "runtime" field, set to
# "Default" once, not computed per run.)

dotnet nswag run "$NSWAG_TMP"

# BOTH generators succeeded - now, and only now, move both outputs into
# place. `mv` within the same filesystem is a rename, not a copy+delete.
mkdir -p "$(dirname "$CLIENT_OUT")"
mv "$OPENAPI_TMP" "$OPENAPI_OUT"
mv "$CLIENT_TMP" "$CLIENT_OUT"

# ---- Direction 2: api -> sim. C# is the source of truth. ----
#
# The document is FETCHED FROM A RUNNING HOST rather than produced by a
# build-time tool, because MapOpenApi composes it from the endpoints as
# registered - which is the only description that cannot drift from what the
# service actually serves.
#
# Built once, then run via `dotnet <dll>` rather than `dotnet run --project`
# or the native apphost binary directly: on this machine both of those
# reliably hung with zero output for the full length of every timeout tried
# (confirmed up to 90s), while `dotnet build` of the identical project
# consistently finished in under two seconds and `dotnet
# <the built dll>` served /healthz within a second. `dotnet run` launches a
# project with UseAppHost=true (the Web SDK's default) via that same native
# apphost stub, which is the common thread between the two hanging paths and
# the one thing `dotnet <dll>` never touches - it loads the assembly
# in-process under the long-lived `dotnet` binary instead of executing a
# freshly-built native executable. The two-step form below is the same
# effective operation (build, then run the host) without whatever that was.
# -o/--output overrides OutputPath, but NEVER BaseIntermediateOutputPath -
# the obj/ tree is computed from the latter regardless of -o, so this build
# and services/api/test/wave-submit.test.ts's and replays.test.ts's (each
# building this SAME project independently, for their own sim instance)
# were all writing intermediate files into the one shared services/sim/obj/,
# racing there whenever vitest ran those files in parallel with this
# script's own contract.test.ts.
#
# The obvious fix - redirecting BaseIntermediateOutputPath/BaseOutputPath
# per call site - does NOT work for this project: confirmed by direct
# reproduction, overriding either property (relative or absolute path, with
# or without -o, with or without isolating the engine ProjectReference via
# GlobalPropertiesToRemove) reproducibly makes MSBuild emit this project's
# own generated files (AssemblyInfo.cs, the TargetFrameworkAttribute file,
# MvcApplicationPartsAssemblyInfo.cs) TWICE into the same csc invocation,
# failing every build with CS0579 duplicate-attribute errors - independent
# of staleness, independent of the separate engine cross-project collision.
# This looks like a genuine SDK/MSBuild quirk in how Microsoft.NET.Sdk.Web
# computes its generated-file item groups; fixing it at that level would
# mean changing services/sim/Broodline.Sim.Service.csproj's own item globs,
# a much larger and riskier change than is warranted here.
#
# So the fix is a lock around JUST this step instead (acquire_build_lock /
# release_build_lock, defined near the top of this script alongside
# BUILD_LOCK - see that block's comment for the full protocol, including
# staleness reclaim, and why it must stay in sync with wave-helpers.ts's
# withDotnetBuildLock()).
acquire_build_lock

dotnet build services/sim/Broodline.Sim.Service.csproj -c Debug \
  -o "$WORK/sim-build" --nologo

release_build_lock
dotnet "$WORK/sim-build/Broodline.Sim.Service.dll" \
  --urls http://127.0.0.1:5199 &
SIM_PID=$!
# cleanup() (trapped on EXIT above) kills $SIM_PID however this script
# exits, including on the set -e path. Without that, a failed curl below
# would leave this dotnet process holding port 5199, and the NEXT run would
# fail with a misleading bind error instead of whatever actually broke.

READY=0
for _ in $(seq 1 40); do
  curl -fsS http://127.0.0.1:5199/healthz >/dev/null 2>&1 && { READY=1; break; }
  sleep 0.25
done
# Without this check the loop falls through SILENTLY on a host that never
# comes up, and the next thing printed is json.tool's "Expecting value:
# line 1 column 1 (char 0)" against an empty curl response - which points
# at JSON parsing, not at the actual problem, and sends whoever is
# debugging it down the wrong path entirely.
[ "$READY" = 1 ] || {
  echo "sim host never became ready on 127.0.0.1:5199" >&2
  exit 1
}

OPENAPI2_OUT="openapi/sim.json"
CLIENT2_OUT="services/api/src/generated/sim.ts"
OPENAPI2_TMP="$WORK/sim.json"
CLIENT2_TMP="$WORK/sim.ts"

# Same staging rule as Direction 1 (see the comment at the top of the
# script): write to $WORK and move both into place only once BOTH have
# succeeded. `curl ... > openapi/sim.json` directly would have the shell
# truncate that file to zero bytes BEFORE curl even runs, so any fetch
# failure - the host dies, the network hiccups - leaves a zero-byte
# openapi/sim.json sitting in the working tree; and generating sim.ts
# directly into place would leave it stale (referencing the OLD document)
# if openapi-typescript failed after a successful curl. Either is exactly
# the inconsistent-tree state the scratch dance exists to prevent, and a
# `git add -A` mid-window would commit an empty or mismatched contract.
#
# Key-sorting is LOAD-BEARING. ASP.NET does not guarantee key order between
# runs of MapOpenApi, so an unsorted document would produce a spurious diff
# on every regeneration even when nothing actually changed - which turns the
# contract.test.ts gate into noise, and noise gets disabled.
#
# STRIPPING `servers` IS ALSO LOAD-BEARING, and is why this is a normaliser
# rather than the `python3 -m json.tool --sort-keys` it replaces. MapOpenApi
# fills `servers` in from the address the document was FETCHED FROM, which
# here is the local harness host - so the committed contract for a service
# that will run on Cloud Run was carrying
# `"servers": [{"url": "http://127.0.0.1:5199/"}]`, and every generated
# client that honours `servers` resolves its base URL to localhost. The
# address of the machine that happened to serve the description is not part
# of the contract; the paths, schemas and operations are.
#
# The strip happens HERE, during normalisation, rather than as an edit to
# openapi/sim.json: that file is generated, and anything done to it by hand
# is undone by the next regeneration.
#
# Enforcement is contract.test.ts, and it was weakened two ways to find out
# what it actually enforces. Delete this pop and BOTH of that file's
# contract assertions fail - the diff gate on a dirty tree, and the
# `servers` assertion on the key itself. Re-add the block to the COMMITTED
# document by hand and only the diff gate fails, because it regenerates the
# file before the other assertion reads it. Either way the strip cannot be
# quietly undone, which is the property that matters; the second case is
# recorded because "an assertion on the committed document catches a hand
# edit" is the natural thing to assume here, and it is not true.
#
# The document is written to $WORK exactly as before - the curl output lands
# in its own scratch file first only because the normaliser is fed to python
# on stdin as a heredoc and cannot also read the document from there.
# Neither file is inside the tree, so the staging rule above is unchanged.
curl -fsS http://127.0.0.1:5199/openapi/v1.json > "$WORK/sim.raw.json"

# indent=4 + sort_keys + ensure_ascii + a trailing newline reproduces
# `python3 -m json.tool --sort-keys` byte for byte (verified by diffing this
# normaliser's output against json.tool's on the same document, with the
# `servers` pop disabled: identical). Keeping that parity is what makes the
# regeneration diff for this change exactly the removed `servers` block and
# nothing else.
python3 - "$WORK/sim.raw.json" "$OPENAPI2_TMP" <<'PY'
import json, sys
raw, out = sys.argv[1:3]
with open(raw) as f:
    doc = json.load(f)
# pop, not del: a future sim host that emits no `servers` at all must not
# turn this into a KeyError and a failed regeneration.
doc.pop("servers", None)
with open(out, "w") as f:
    json.dump(doc, f, indent=4, sort_keys=True)
    f.write("\n")
PY

# Absolute paths on both sides: `pnpm --filter <pkg> exec` runs the command
# with its CWD set to that package's directory (services/api), not the repo
# root this script cd'd to above - confirmed with `pnpm --filter
# @broodline/api exec pwd`. $WORK is already absolute, so both the input
# and the output land in the scratch dir regardless of that CWD switch.
pnpm --filter @broodline/api exec openapi-typescript "$OPENAPI2_TMP" \
  -o "$CLIENT2_TMP"

# BOTH generators succeeded - now, and only now, move both outputs into
# place, exactly as Direction 1 does above.
mkdir -p "$(dirname "$CLIENT2_OUT")"
mv "$OPENAPI2_TMP" "$OPENAPI2_OUT"
mv "$CLIENT2_TMP" "$CLIENT2_OUT"

echo "--- generated ---"
git --no-pager diff --stat -- openapi/ client/Assets/Generated/ services/api/src/generated/
