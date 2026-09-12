#!/usr/bin/env bash
# Validates and publishes a config bundle, then optionally points at it.
#
# A bundle that fails validation is NOT published - solo_execution 5.2, and
# the entire safety model. Publishing does not make a bundle live; that is the
# second argument.
#
# --activate IS THE ONLY THING THAT MOVES bundles/current. Without it, this
# script publishes an immutable, inert bundle and nothing more: the running
# service keeps serving whatever the pointer already names. loadBundle reads
# that pointer on every cold start, so a first-time seed that forgets
# --activate leaves bundles/current unset and the service 500s on its very
# first request. To seed a brand-new bucket:
#
#   ./implementation/scripts/publish-bundle.sh 0.1.0 --activate
set -euo pipefail
cd "$(dirname "$0")/../.."

VERSION="${1:?usage: publish-bundle.sh <version> [--activate]}"
: "${CONFIG_BUCKET:?set CONFIG_BUCKET (terraform output config_bucket)}"

# Written to a REAL FILE rather than piped to `node -`. Node's type stripping
# does not apply to stdin: a heredoc'd TypeScript program fails with
# ERR_UNSUPPORTED_TYPESCRIPT_SYNTAX the moment it hits an import of a .ts
# module. Found by running this against a real bucket; it had never been
# executed before.
# In the REPO ROOT, not $TMPDIR: the program below imports ./services/... by
# relative path, and those resolve against the RUNNER's own directory. A
# runner in /tmp looks for /tmp/services and fails.
RUNNER="./.publish-bundle-$$.mts"
trap 'rm -f "$RUNNER"' EXIT
cat > "$RUNNER" <<'JS'
import { publishBundle } from './services/api/src/config/publish.ts'
import { GcsBundleStore } from './services/api/src/config/gcs-store.ts'

const [version, flag] = process.argv.slice(2)
const store = new GcsBundleStore(process.env.CONFIG_BUCKET)
await publishBundle(store, `config/bundles/${version}`, version)
console.log(`published ${version}`)

if (flag === '--activate') {
  await store.setPointer(version)
  console.log(`pointer now names ${version}`)
} else {
  console.log(`NOT ACTIVATED: bundles/current is UNCHANGED. ${version} is published but no client will ever load it until you run:`)
  console.log(`  CONFIG_BUCKET=${process.env.CONFIG_BUCKET} ./implementation/scripts/publish-bundle.sh ${version} --activate`)
}
JS

node --experimental-strip-types "$RUNNER" "$VERSION" "${2:-}"
