#!/usr/bin/env bash
# The one `servers` row nothing else creates.
#
# WHY THIS EXISTS AS A SCRIPT. Every other table in this database is written
# by the app: an account row comes from POST /v1/account, a creature from the
# cold open, a ledger row from credit/debit. `servers` is the exception -
# nothing in `services/api/src` ever inserts one. The tests each insert their
# own in `beforeAll` (account.test.ts:33, adversarial.test.ts:269), which is
# why the suite passes against a database that a fresh deploy does not have.
#
# WITHOUT IT, THE FAILURE IS NOT OBVIOUS. `map/claim.ts:117` selects the
# server row and every request carries a serverId that `db/client.ts:95` sets
# as `app.server_id` for row-level security. A missing row does not produce
# "no server" - it produces empty result sets from tables whose RLS policies
# compare against a server that is not there, which reads as "the deploy
# worked but the game has no data".
#
# Phase 8 Task 17 Step 2 calls this between `pnpm migrate` and the revert of
# the temporary public-IP vars, while the database is briefly reachable.
#
# USAGE
#   DATABASE_URL=postgres://... ./implementation/scripts/seed-server.sh
#   SERVER_ID=1 DATABASE_URL=... ./implementation/scripts/seed-server.sh
#
# IDEMPOTENT. ON CONFLICT DO NOTHING, so running it twice is not an error and
# re-running after a partial Task 17 is safe.
set -euo pipefail
cd "$(dirname "$0")/../.."

SERVER_ID="${SERVER_ID:-1}"
REGION="${SERVER_REGION:-us-central1}"

# The weekly tick, matching what every test seeds: Sunday (0) at minute 1200,
# i.e. 20:00. `broodline_region_roster.md` section 6.3 owns the real schedule;
# this is the value the suite has always used and the one the config bundle
# was authored against.
TICK_DAY="${SERVER_TICK_DAY:-0}"
TICK_MINUTE="${SERVER_TICK_MINUTE:-1200}"

if [ -z "${DATABASE_URL:-}" ]; then
  cat >&2 <<'MSG'
seed-server: DATABASE_URL is not set.

Task 17 Step 2 runs this while the Cloud SQL instance is temporarily
reachable, between the migrate and the revert of db_public_ip. The same
connection string `pnpm --filter @broodline/api migrate` used is the one to
use here.
MSG
  exit 2
fi

command -v psql >/dev/null || { echo "seed-server: psql not found on PATH" >&2; exit 127; }

echo "[1] servers row ${SERVER_ID} (${REGION}, tick day ${TICK_DAY} minute ${TICK_MINUTE})"
psql "$DATABASE_URL" -v ON_ERROR_STOP=1 -q <<SQL
INSERT INTO servers (server_id, region, state, tick_day_of_week, tick_minute_of_day)
VALUES (${SERVER_ID}, '${REGION}', 'open', ${TICK_DAY}, ${TICK_MINUTE})
ON CONFLICT (server_id) DO NOTHING;
SQL

# READ IT BACK, because ON CONFLICT DO NOTHING succeeds whether it inserted or
# not, and a silent no-op against the wrong database looks identical to a
# successful seed. This is the same reason check-stylesheets.sh refuses to
# pass on zero sheets: a command that cannot fail has not verified anything.
FOUND="$(psql "$DATABASE_URL" -tAc "SELECT state FROM servers WHERE server_id = ${SERVER_ID}")"
if [ "$FOUND" != "open" ]; then
  echo "  FAIL: server ${SERVER_ID} is '${FOUND:-missing}' after the insert, expected 'open'" >&2
  exit 1
fi
echo "  ✓ server ${SERVER_ID} present and open"

echo
echo "PASS - the one row nothing else creates."
