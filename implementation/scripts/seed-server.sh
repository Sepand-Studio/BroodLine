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
# NODE AND `pg`, NOT psql. The first version of this script shelled out to
# psql and died on a machine that does not have it - which is every machine
# here, because nothing else in this repo needs it. `migrate-cli.ts` connects
# through `createPool` from the same DATABASE_URL, and `pg` is already a
# dependency, so this uses the toolchain the project actually has rather than
# adding one to the prerequisites for a single INSERT.
#
# Phase 8 Task 17 Step 2 calls this between `pnpm migrate` and the revert of
# the temporary public-IP vars, while the database is briefly reachable.
#
# USAGE
#   DATABASE_URL=postgres://... ./implementation/scripts/seed-server.sh
#   SERVER_ID=1 DATABASE_URL=... ./implementation/scripts/seed-server.sh
#
# NOTE ON DATABASE_URL: a password from `openssl rand -base64 32` will usually
# contain `/`, `+` or `=`, and an un-encoded `/` makes the URL unparseable -
# `ERR_INVALID_URL`, which reads as a malformed host rather than a password
# problem. Percent-encode it.
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
reachable, between the migrate and the revert of db_public_ip. Use the same
connection string `pnpm --filter @broodline/api migrate` used.
MSG
  exit 2
fi

# Same node resolution as smoke-wave.sh: nvm's default here is an x86 v10 that
# dies with "Bad CPU type", so this resolves a version rather than trusting
# whatever `node` is on PATH.
NODE_BIN="$(command -v node || true)"
if [ -z "$NODE_BIN" ] || [ "$("$NODE_BIN" -e 'process.stdout.write(String(process.versions.node.split(".")[0]))' 2>/dev/null || echo 0)" -lt 22 ]; then
  CANDIDATE="$(ls -d "$HOME"/.nvm/versions/node/v2[2-9]* 2>/dev/null | sort -V | tail -1 || true)"
  [ -n "$CANDIDATE" ] || { echo "seed-server: need node >= 22; none found" >&2; exit 2; }
  NODE_BIN="$CANDIDATE/bin/node"
fi

# In the REPO ROOT, not $TMPDIR - the program imports ./services/... by
# relative path and those resolve against the runner's own directory. The
# publish-bundle.sh convention, and its comment says why.
RUNNER="./.seed-server-$$.mts"
trap 'rm -f "$RUNNER"' EXIT
cat > "$RUNNER" <<'JS'
import { createPool } from './services/api/src/db/client.ts'

const [serverId, region, tickDay, tickMinute] = process.argv.slice(2)
const pool = createPool(process.env.DATABASE_URL!)
try {
  await pool.query(
    `INSERT INTO servers (server_id, region, state, tick_day_of_week, tick_minute_of_day)
     VALUES ($1, $2, 'open', $3, $4)
     ON CONFLICT (server_id) DO NOTHING`,
    [Number(serverId), region, Number(tickDay), Number(tickMinute)],
  )

  // READ IT BACK. ON CONFLICT DO NOTHING succeeds whether it inserted or not,
  // so a silent no-op against the wrong database looks identical to a
  // successful seed. Same reason check-stylesheets.sh refuses to pass on zero
  // sheets: a command that cannot fail has not verified anything.
  const { rows } = await pool.query('SELECT state FROM servers WHERE server_id = $1', [Number(serverId)])
  const state = rows[0]?.state
  if (state !== 'open') {
    console.error(`  FAIL: server ${serverId} is '${state ?? 'missing'}' after the insert, expected 'open'`)
    process.exit(1)
  }
  console.log(`  ok server ${serverId} present and open`)
} finally {
  await pool.end()
}
JS

echo "[1] servers row ${SERVER_ID} (${REGION}, tick day ${TICK_DAY} minute ${TICK_MINUTE})"
"$NODE_BIN" --experimental-strip-types "$RUNNER" "$SERVER_ID" "$REGION" "$TICK_DAY" "$TICK_MINUTE"

echo
echo "PASS - the one row nothing else creates."
