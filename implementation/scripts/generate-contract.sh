#!/usr/bin/env bash
# Regenerates openapi/broodline.json from the Zod schemas, then the C# client
# from that document.
#
# BOTH outputs are COMMITTED, so a contract change is a reviewable diff and a
# Unity build needs no Node - solo_execution section 6, rules 2 and 3. The
# INTENT is that CI runs this and fails on a non-empty diff, which is what
# would stop the generated client drifting from the handlers it is generated
# from - but no CI runs on this repository today: GitHub Actions is disabled
# at the organisation level and enabling it is blocked on the owner. Making
# that guarantee real, rather than aspirational, is Task 12's job.
set -euo pipefail
cd "$(dirname "$0")/../.."

# Both generators write to a scratch directory first and are moved into place
# together only once BOTH have succeeded. Without this, `pnpm openapi`
# writing openapi/broodline.json and THEN `dotnet nswag` failing leaves the
# committed OpenAPI document already rewritten while the C# client is still
# generated from the old one - an inconsistent tree that the next successful
# run silently overwrites, so the window it was ever in that state is never
# noticed even though a commit could land during it.
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

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
# error. LatestMajor lets the installed runtime host whichever build actually
# launches; nswag.json's "runtime" field must then name that same build
# (see nswag.json) or NSwag's own document-runtime check rejects it. The
# generated output does not depend on this - it only decides which host
# executes the generator. (The scratch copy above carries this field through
# unchanged.)
export DOTNET_ROLL_FORWARD=LatestMajor

dotnet nswag run "$NSWAG_TMP"

# BOTH generators succeeded - now, and only now, move both outputs into
# place. `mv` within the same filesystem is a rename, not a copy+delete.
mkdir -p "$(dirname "$CLIENT_OUT")"
mv "$OPENAPI_TMP" "$OPENAPI_OUT"
mv "$CLIENT_TMP" "$CLIENT_OUT"

echo "--- generated ---"
git --no-pager diff --stat -- openapi/ client/Assets/Generated/
