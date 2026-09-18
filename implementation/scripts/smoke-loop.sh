#!/usr/bin/env bash
# Task 17 Step 4 - drive design §1's LOOP against the deployed stack.
#
# smoke-wave.sh proves the deployed PAIR: one wave, verified by sim, paid once
# under retry. The loop is larger - HARVEST A NODE, SPLICE TWO CREATURES INTO
# ONE, FIGHT A WAVE, GET PAID - so this resolves the api URL the same way,
# runs the flow in smoke-loop.ts, and then asserts the side effects that no
# HTTP response can show it.
#
# THE LEDGER CHECK IS NOT DECORATION. Every step of the loop can return a clean
# 200 while the ledger row that makes it reconstructible was never written -
# the verdict comes from sim and the credit from Postgres, and a missing ledger
# row is invisible to both. broodline_solo_execution.md calls the ledger "the
# highest-value small piece of code in the backend" and says games that add it
# after launch never fully recover the first six months. So it is asserted
# separately, and after.
#
# NEVER RUN. Nothing is deployed as this is written - `terraform output -raw
# api_url` is empty and there is no Cloud Run service - so this script has been
# parsed, not executed, and no line below has ever answered against a live
# stack. Step 5 is what runs it, and Step 5 needs the apply Step 1 has not been
# authorised to make.
#
# USAGE
#   ./implementation/scripts/smoke-loop.sh
#   API_URL=https://... ./implementation/scripts/smoke-loop.sh   # skip terraform
#
# The ledger assertion needs a database connection, which the api's own does
# not lend it: Cloud SQL is private and the service reaches it over the VPC
# connector. Set DATABASE_URL to an owner-role connection through the Cloud SQL
# Auth Proxy, or through the scoped public-IP window
# implementation/2026-09-13-phase5-followups.md §12 describes and reverts.
set -euo pipefail
cd "$(dirname "$0")/../.."

PROJECT_ID="${PROJECT_ID:-broodline-508416}"

# node >= 22 with --experimental-strip-types. nvm's default on this machine is
# an x86 v10 that dies with "Bad CPU type", which is why this resolves a
# version rather than trusting `node` on PATH - the same trap the test baseline
# file documents for the api suite.
NODE_BIN="$(command -v node || true)"
if [ -z "$NODE_BIN" ] || [ "$("$NODE_BIN" -e 'process.stdout.write(String(process.versions.node.split(".")[0]))' 2>/dev/null || echo 0)" -lt 22 ]; then
  CANDIDATE="$(ls -d "$HOME"/.nvm/versions/node/v2[2-9]* 2>/dev/null | sort -V | tail -1 || true)"
  if [ -z "$CANDIDATE" ]; then
    echo "smoke-loop: need node >= 22 for --experimental-strip-types; none found" >&2
    exit 2
  fi
  NODE_BIN="$CANDIDATE/bin/node"
fi

if [ -z "${API_URL:-}" ]; then
  API_URL="$(terraform -chdir=infra/terraform output -raw api_url 2>/dev/null || true)"
  if [ -z "$API_URL" ]; then
    cat >&2 <<'MSG'
smoke-loop: no API_URL and `terraform output -raw api_url` is empty.

An output whose resource does not exist reports nothing, so an empty value
here usually means the Cloud Run service has not been created yet rather than
that outputs are broken. See implementation/2026-09-13-phase5-followups.md
section 12 for the apply order, or pass API_URL directly.
MSG
    exit 2
  fi
fi
export API_URL

# What the flow decided, handed to the assertions below. The expected ledger
# rows are built by the RUN THAT MADE THEM rather than recomputed here from
# constants of this script's own: a second copy of that arithmetic would be
# free to agree with itself and with nothing else, and it would be wrong the
# first time a node claim actually paid.
SUMMARY="$(mktemp -t smoke-loop)"
trap 'rm -f "$SUMMARY"' EXIT
export SMOKE_LOOP_SUMMARY="$SUMMARY"

"$NODE_BIN" --experimental-strip-types implementation/scripts/smoke-loop.ts

if [ ! -s "$SUMMARY" ]; then
  echo "smoke-loop: the flow passed but wrote no summary - nothing can be asserted about it" >&2
  exit 1
fi

# One JSON reader, because bash 3.2 has none and `node` is already resolved.
# `-e` runs as CommonJS (the repo's root package.json declares no "type"), so
# `require` is available without an --input-type flag.
summary_field() {
  "$NODE_BIN" -e '
    const s = JSON.parse(require("fs").readFileSync(process.argv[1], "utf8"))
    const v = s[process.argv[2]]
    process.stdout.write(Array.isArray(v) ? v.join("\n") : String(v))
  ' "$SUMMARY" "$1"
}

SERVER_ID="$(summary_field serverId)"
PLAYER_ID="$(summary_field playerId)"
EXPECTED_SHARDS="$(summary_field expectedShards)"

# --- a ledger row per mutation ----------------------------------------------
echo
echo "[A] a ledger row per mutation, in Postgres"

if [ -z "${DATABASE_URL:-}" ]; then
  cat >&2 <<'MSG'
  FAIL: DATABASE_URL is unset, so the ledger cannot be read.

  This is a FAILURE and not a skip, and that is the difference between this
  script and smoke-wave.sh's bucket check. The ledger row is the thing the
  header above says is invisible to every other observer in the system: the
  flow already returned 200 everywhere and the balance already reconciled
  over HTTP, so there is nothing left that would notice its absence. A run
  that printed PASS without reading it would be asserting exactly the part
  it did not check.

  Set DATABASE_URL to an owner-role connection - through the Cloud SQL Auth
  Proxy, or through the scoped public-IP window in
  implementation/2026-09-13-phase5-followups.md section 12.
MSG
  exit 1
fi

if ! command -v psql >/dev/null 2>&1; then
  echo "  FAIL: psql is not on PATH, and DATABASE_URL is set - install libpq or use the proxy's client" >&2
  exit 1
fi

# `SET app.server_id` IS REQUIRED, not belt and braces. 0002_rls.sql applies
# FORCE ROW LEVEL SECURITY to every table, which removes the owner's usual
# exemption, so without this the SELECT below returns zero rows against a
# perfectly correct ledger and the failure reads as "nothing was written".
# A superuser bypasses RLS entirely and is unaffected by it either way.
LEDGER_SQL="SET app.server_id = '${SERVER_ID}';
SELECT reason_code, currency, delta
  FROM ledger
 WHERE server_id = ${SERVER_ID} AND player_id = '${PLAYER_ID}';"

# SORTED, NOT IN INSERTION ORDER, and the reason is in 0001_tables.sql: the
# ledger's `entry_id` is a uuid rather than a bigserial ("a sequence is
# globally meaningful and would collide on a server merge"), and rows written
# inside one transaction share a `created_at`. There is therefore no column
# that recovers the order the two STARTER_GRANT rows went in. A multiset
# comparison still catches a missing row, an extra row, a wrong currency and a
# wrong delta; only the sequence is unasserted, and it is unasserted because
# the schema does not record it.
# PSQL FIRST, INTO A VARIABLE, AND THE FILTER SECOND. Written as one pipeline
# this reads better and is wrong under `set -o pipefail`: an EMPTY result
# makes `grep -v` exit 1, which fails the pipeline, which kills the script -
# so a ledger that holds nothing at all, which is precisely the failure this
# check exists to catch, would abort here with no message instead of printing
# the diff below. Split, psql's own non-zero exit still aborts loudly (a
# refused connection must not read as "no rows"), and an empty success flows
# through to the comparison.
LEDGER_RAW="$(psql "$DATABASE_URL" -v ON_ERROR_STOP=1 --no-psqlrc -A -t -F '|' -c "$LEDGER_SQL")"
ACTUAL_LEDGER="$(printf '%s\n' "$LEDGER_RAW" | grep -v '^$' | LC_ALL=C sort || true)"
EXPECTED_LEDGER="$("$NODE_BIN" -e '
  const s = JSON.parse(require("fs").readFileSync(process.argv[1], "utf8"))
  for (const r of s.expectedLedger) console.log([r.reasonCode, r.currency, r.delta].join("|"))
' "$SUMMARY" | LC_ALL=C sort)"

if [ "$ACTUAL_LEDGER" != "$EXPECTED_LEDGER" ]; then
  echo "  FAIL: the ledger does not hold one row per mutation." >&2
  echo "  expected:" >&2
  echo "$EXPECTED_LEDGER" | sed 's/^/    /' >&2
  echo "  actual:" >&2
  echo "$ACTUAL_LEDGER" | sed 's/^/    /' >&2
  exit 1
fi
echo "  ✓ $(echo "$EXPECTED_LEDGER" | grep -c .) row(s), one per mutation, deltas as issued"

# --- shards credited, as Postgres holds them --------------------------------
# money/invariant.ts's findDrift, scoped to this one player: the wallet is the
# balance the game reads and the ledger sum is the balance the game can prove,
# and solo_execution 5.3 is that the two are written in one transaction and
# never derived from each other at read time. Drift is either a bug or a
# duplication exploit.
echo
echo "[B] the wallet agrees with the sum of its ledger"
DRIFT_SQL="SET app.server_id = '${SERVER_ID}';
WITH sums AS (
  SELECT currency, SUM(delta) AS total FROM ledger
   WHERE server_id = ${SERVER_ID} AND player_id = '${PLAYER_ID}'
   GROUP BY currency
)
SELECT w.currency, w.balance, COALESCE(s.total, 0)
  FROM wallets w LEFT JOIN sums s ON s.currency = w.currency
 WHERE w.server_id = ${SERVER_ID} AND w.player_id = '${PLAYER_ID}'
   AND w.balance <> COALESCE(s.total, 0);"

# Split for the reason the ledger read above is split: no drift is the HAPPY
# case here and it returns zero rows, so a one-pipeline form would fail under
# pipefail on every healthy run.
DRIFT_RAW="$(psql "$DATABASE_URL" -v ON_ERROR_STOP=1 --no-psqlrc -A -t -F '|' -c "$DRIFT_SQL")"
DRIFT="$(printf '%s\n' "$DRIFT_RAW" | grep -v '^$' || true)"
if [ -n "$DRIFT" ]; then
  echo "  FAIL: wallet and ledger disagree (currency|wallet|ledger_sum):" >&2
  echo "$DRIFT" | sed 's/^/    /' >&2
  exit 1
fi

SHARDS_SQL="SET app.server_id = '${SERVER_ID}';
SELECT balance FROM wallets
 WHERE server_id = ${SERVER_ID} AND player_id = '${PLAYER_ID}' AND currency = 'shards';"
SHARDS_RAW="$(psql "$DATABASE_URL" -v ON_ERROR_STOP=1 --no-psqlrc -A -t -c "$SHARDS_SQL")"
STORED_SHARDS="$(printf '%s\n' "$SHARDS_RAW" | grep -v '^$' || true)"
if [ "$STORED_SHARDS" != "$EXPECTED_SHARDS" ]; then
  echo "  FAIL: wallets holds ${STORED_SHARDS:-<no row>} shards, the run earned ${EXPECTED_SHARDS}" >&2
  exit 1
fi
echo "  ✓ ${STORED_SHARDS} shards credited, and every wallet matches its ledger sum"

# --- the replay objects -----------------------------------------------------
echo
REPLAY_BUCKET="${REPLAY_BUCKET:-$(terraform -chdir=infra/terraform output -raw replay_bucket 2>/dev/null || true)}"
if [ -z "$REPLAY_BUCKET" ]; then
  echo "smoke-loop: skipping the replay check - no replay_bucket output and no REPLAY_BUCKET set." >&2
  echo "The loop closed and the ledger holds; the replay objects were NOT verified." >&2
  exit 0
fi

# BY KEY, NOT BY COUNT. replays/gcs-store.ts writes
# `replays/{serverId}/{playerId}/{issuanceId}.bin`, so each submission this run
# made is nameable - and a bucket-wide count would be satisfied by objects a
# previous run left behind, which is the shape of check that passes for years
# and then passes on the day it should not.
echo "[C] a replay object per submission in gs://${REPLAY_BUCKET}"
PREFIX="replays/${SERVER_ID}/${PLAYER_ID}"
LISTED="$(gcloud storage ls --recursive "gs://${REPLAY_BUCKET}/${PREFIX}/**" \
  --project "$PROJECT_ID" 2>/dev/null || true)"

MISSING=0
for ISSUANCE in $(summary_field issuanceIds); do
  if ! echo "$LISTED" | grep -q "${PREFIX}/${ISSUANCE}.bin"; then
    echo "  FAIL: no object for issuance ${ISSUANCE} - wave/submit returned 200 but stored nothing" >&2
    MISSING=$((MISSING + 1))
  fi
done
if [ "$MISSING" -gt 0 ]; then
  exit 1
fi
echo "  ✓ $(echo "$LISTED" | grep -c .) object(s) under ${PREFIX}/, one per submission"

echo
echo "PASS - the loop closed against the deployed stack: a node claimed, two creatures spliced"
echo "into one, three waves fought and paid, a ledger row behind every mutation, and every"
echo "submission's replay stored."
