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

node --experimental-strip-types - "$VERSION" "${2:-}" <<'JS'
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
