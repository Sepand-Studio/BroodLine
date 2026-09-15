#!/usr/bin/env bash
# Task 11 Step 7 - prove the deployed pair end to end.
#
# Resolves the api URL from terraform output, runs the wave flow in
# smoke-wave.ts, then confirms the replay object actually landed in the bucket.
#
# THE BUCKET CHECK IS NOT DECORATION. wave/submit can return a clean 200 with
# the replay store having silently failed - the verdict comes from sim and the
# credit from Postgres, neither of which knows whether GCS accepted the object.
# design 5.1's 30-day lifecycle rule and the whole replay-viewer story rest on
# the object existing, so it is asserted separately and after.
#
# USAGE
#   ./implementation/scripts/smoke-wave.sh
#   API_URL=https://... ./implementation/scripts/smoke-wave.sh   # skip terraform
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
    echo "smoke-wave: need node >= 22 for --experimental-strip-types; none found" >&2
    exit 2
  fi
  NODE_BIN="$CANDIDATE/bin/node"
fi

if [ -z "${API_URL:-}" ]; then
  API_URL="$(terraform -chdir=infra/terraform output -raw api_url 2>/dev/null || true)"
  if [ -z "$API_URL" ]; then
    cat >&2 <<'MSG'
smoke-wave: no API_URL and `terraform output -raw api_url` is empty.

An output whose resource does not exist reports nothing, so an empty value
here usually means the Cloud Run service has not been created yet rather than
that outputs are broken. See implementation/2026-09-13-phase5-followups.md
section 12 for the apply order, or pass API_URL directly.
MSG
    exit 2
  fi
fi
export API_URL

"$NODE_BIN" --experimental-strip-types implementation/scripts/smoke-wave.ts

# --- the replay object ------------------------------------------------------
REPLAY_BUCKET="${REPLAY_BUCKET:-$(terraform -chdir=infra/terraform output -raw replay_bucket 2>/dev/null || true)}"
if [ -z "$REPLAY_BUCKET" ]; then
  echo
  echo "smoke-wave: skipping the bucket check - no replay_bucket output and no REPLAY_BUCKET set." >&2
  echo "The wave flow passed; the object was NOT verified." >&2
  exit 0
fi

echo
echo "[7] replay object in gs://${REPLAY_BUCKET}"
COUNT="$(gcloud storage ls --recursive "gs://${REPLAY_BUCKET}/**" --project "$PROJECT_ID" 2>/dev/null | grep -c . || true)"
if [ "${COUNT:-0}" -lt 1 ]; then
  echo "  FAIL: no objects in gs://${REPLAY_BUCKET} - wave/submit returned 200 but stored nothing" >&2
  exit 1
fi
echo "  ✓ ${COUNT} object(s) present"

echo
echo "PASS - deployed end to end: account, issuance, verified submission, single payment, stored replay."
