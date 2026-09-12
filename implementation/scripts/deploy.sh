#!/usr/bin/env bash
# Builds, pushes and deploys the api.
#
# MIGRATIONS ARE NOT RUN HERE. solo_execution 7.0: a schema migration and the
# code requiring it never deploy together. Run publish-bundle.sh and
# `pnpm --filter @broodline/api migrate` as their own deliberate steps, BEFORE
# this script, per the order in task-11-brief.md Step 9: schema, then config,
# then code.
set -euo pipefail
cd "$(dirname "$0")/../.."

: "${PROJECT_ID:?set PROJECT_ID}"
REGION="${REGION:-us-central1}"
TAG="$(git rev-parse --short HEAD)"
IMAGE="${REGION}-docker.pkg.dev/${PROJECT_ID}/broodline/api:${TAG}"

# Tagged by commit, never :latest. A revision that cannot be named cannot be
# rolled back to, and 7.0 wants the previous revision one command away.
gcloud builds submit --tag "$IMAGE" --project "$PROJECT_ID" .

cd infra/terraform
terraform apply -var="project_id=${PROJECT_ID}" -var="region=${REGION}" -var="image=${IMAGE}"
terraform output -raw api_url
