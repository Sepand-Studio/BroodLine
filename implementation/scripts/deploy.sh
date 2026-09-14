#!/usr/bin/env bash
# Builds, pushes and deploys the api.
#
# MIGRATIONS ARE NOT RUN HERE. solo_execution 7.0: a schema migration and the
# code requiring it never deploy together. Run publish-bundle.sh and
# `pnpm --filter @broodline/api migrate` as their own deliberate steps, BEFORE
# this script, per the order in task-11-brief.md Step 9: schema, then config,
# then code.
#
# =====================================================================
# PREREQUISITES - two secrets, and one of them is a two-pass bootstrap
# =====================================================================
#
# 1. THE DATABASE PASSWORD, every run. Required, no default, supplied through
#    the environment so it never reaches disk or `ps`:
#
#      export TF_VAR_db_password="$(openssl rand -base64 32 | tr -d '\n')"
#
#    Terraform writes it to BOTH the Postgres role and Secret Manager from
#    this one input, so the two cannot drift. It is a write-only argument and
#    does not land in state. Generate it ONCE and keep it (a password
#    manager); a different value on a later run rotates the role's password
#    without incrementing db_password_version, which does nothing at all -
#    see infra/terraform/variables.tf.
#
# 2. THE JWT SIGNING KEY, once per project, and this one is why a fresh
#    project takes TWO passes. infra/terraform/main.tf creates the SECRET but
#    deliberately not a VERSION - a signing key in Terraform is a signing key
#    in every plan - while the api's JWT_SECRET env var resolves
#    `version = "latest"`. On a secret with no versions that resolves to
#    nothing and THE REVISION FAILS TO START, reported by Cloud Run as a
#    generic "container failed to start" that reads like an application crash
#    and sends people into the api logs, where there is nothing to find.
#
#    On a fresh project, in this order:
#
#      a. cd infra/terraform && terraform apply    # creates the secret
#      b. openssl rand -base64 48 \
#           | tr -d '\n' \
#           | gcloud secrets versions add broodline-jwt-secret \
#               --data-file=- --project "$PROJECT_ID"
#      c. re-run this script
#
#    At least 32 characters: services/api/src/identity/session.ts refuses to
#    boot on a shorter one. `openssl rand -base64 48` yields 64.
#
#    The preflight below refuses to deploy until (b) has happened, so the
#    failure arrives here, named, instead of as a dead revision afterwards.
#
# STILL OWED, and this script does NOT yet work without it: `sim_image` is a
# required variable with no default and is not passed below, because nothing
# builds a sim image yet - cloudbuild.yaml builds only the api. Passing a URI
# for a container that was never pushed would deploy a broken revision
# instead of failing here, which is worse. Both belong to the deploy half of
# Task 11. Until then `terraform apply` below stops on the missing variable.
set -euo pipefail
cd "$(dirname "$0")/../.."

: "${PROJECT_ID:?set PROJECT_ID}"
: "${TF_VAR_db_password:?set TF_VAR_db_password - see the PREREQUISITES block above}"

# Does the JWT secret have a usable version? This LISTS versions and never
# reads one: `versions list` returns names and states, not payloads, so the
# preflight needs no access to the key it is checking for. `|| true` because
# a secret that does not exist yet is a NOT_FOUND, which is simply the
# first-pass case and wants the same message.
if [ -z "$(gcloud secrets versions list broodline-jwt-secret \
             --project "$PROJECT_ID" --filter='state:ENABLED' \
             --format='value(name)' --limit=1 2>/dev/null || true)" ]; then
  cat >&2 <<'MSG'
deploy.sh: refusing to deploy - the secret `broodline-jwt-secret` has no
enabled version, so the api revision would resolve JWT_SECRET to nothing and
fail to start with a message that looks like an application crash.

Create one (>= 32 chars; this yields 64):

  openssl rand -base64 48 \
    | tr -d '\n' \
    | gcloud secrets versions add broodline-jwt-secret \
        --data-file=- --project "$PROJECT_ID"

If the secret itself does not exist yet this is a fresh project: run
`terraform apply` in infra/terraform once to create it, then the command
above, then re-run this script. See the PREREQUISITES block in this file.
MSG
  exit 1
fi

REGION="${REGION:-us-central1}"
TAG="$(git rev-parse --short HEAD)"
IMAGE="${REGION}-docker.pkg.dev/${PROJECT_ID}/broodline/api:${TAG}"

# Tagged by commit, never :latest. A revision that cannot be named cannot be
# rolled back to, and 7.0 wants the previous revision one command away.
# --config, not --tag. --tag demands a Dockerfile at the source root;
# ours is at services/api/Dockerfile and needs the repo root as its
# build context. See cloudbuild.yaml.
gcloud builds submit --config cloudbuild.yaml \
  --substitutions "_IMAGE=${IMAGE}" --project "$PROJECT_ID" .

cd infra/terraform
terraform apply -var="project_id=${PROJECT_ID}" -var="region=${REGION}" -var="image=${IMAGE}"
terraform output -raw api_url
