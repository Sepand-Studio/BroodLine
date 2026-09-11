#!/usr/bin/env bash
# Regenerates openapi/broodline.json from the Zod schemas, then the C# client
# from that document.
#
# BOTH outputs are COMMITTED, so a contract change is a reviewable diff and a
# Unity build needs no Node - solo_execution section 6, rules 2 and 3. CI runs
# this and fails on a non-empty diff, which is what stops the generated client
# drifting from the handlers it is generated from.
set -euo pipefail
cd "$(dirname "$0")/../.."

pnpm openapi
dotnet tool restore

# This machine's only installed shared runtime is a major version newer than
# any NSwag.ConsoleCore 14.2.0 build targets (net8.0/net9.0). The exact-match
# framework resolution refuses to roll a *major* version forward by default,
# so without this the tool fails to launch at all with a missing-runtime
# error. LatestMajor lets the installed runtime host whichever build actually
# launches; nswag.json's "runtime" field must then name that same build
# (see nswag.json) or NSwag's own document-runtime check rejects it. The
# generated output does not depend on this - it only decides which host
# executes the generator.
export DOTNET_ROLL_FORWARD=LatestMajor

dotnet nswag run nswag.json

echo "--- generated ---"
git --no-pager diff --stat -- openapi/ client/Assets/Generated/
