terraform {
  # 1.11, not 1.9, and the bump is load-bearing rather than housekeeping.
  # google_sql_user.password_wo and google_secret_manager_secret_version.
  # secret_data_wo are WRITE-ONLY arguments, which Terraform did not support
  # before 1.11. They are the whole reason the database password never
  # appears in terraform.tfstate (see google_sql_user.app below); on 1.10 or
  # earlier the arguments are simply unknown and the config fails to parse,
  # which is the right failure - the alternative is `password`, which lands
  # the credential in state in cleartext.
  required_version = ">= 1.11"
  required_providers {
    # >= 6.23 for the same reason: that is the release that added the
    # write-only password/secret_data attributes. `~> 6.0` alone would let
    # `terraform init` select a provider that cannot parse this file.
    google = { source = "hashicorp/google", version = ">= 6.23, < 7.0" }
  }
}

provider "google" {
  project = var.project_id
  region  = var.region
}

# One region at milestone 1. solo_execution section 4: servers are
# region-pinned and account data is region-partitioned from day one even while
# only the US exists, because moving EU player data into the EU later is a
# migration under legal pressure.

resource "google_artifact_registry_repository" "api" {
  location      = var.region
  repository_id = "broodline"
  format        = "DOCKER"
}

resource "google_compute_network" "main" {
  name                    = "broodline"
  auto_create_subnetworks = true
}

# Private Services Access: the peering that lets Cloud SQL allocate a private
# IP address inside this VPC at all. Required the moment ip_configuration
# sets private_network, independent of whether ipv4_enabled is also true -
# this is exactly the kind of fact that only an apply proves (see
# deletion_protection in variables.tf), and the first ephemeral cycle found
# it missing: "failed to create instance because the network doesn't have at
# least 1 private services connection."
resource "google_compute_global_address" "private_services" {
  name          = "broodline-psa-range"
  purpose       = "VPC_PEERING"
  address_type  = "INTERNAL"
  prefix_length = 16
  network       = google_compute_network.main.id
}

resource "google_service_networking_connection" "private_services" {
  network                 = google_compute_network.main.id
  service                 = "servicenetworking.googleapis.com"
  reserved_peering_ranges = [google_compute_global_address.private_services.name]
}

resource "google_sql_database_instance" "main" {
  # The instance's private IP allocation depends on the peering existing
  # first; Terraform cannot infer this from the private_network reference
  # alone because the peering is a property of the network, not a direct
  # input attribute of the instance.
  depends_on = [google_service_networking_connection.private_services]

  name             = "broodline-main"
  database_version = "POSTGRES_16"
  region           = var.region

  settings {
    # Smallest tier. solo_execution section 3: the Postgres instance is the
    # only always-on component and the whole bill is $25-50/month pre-players.
    # ENTERPRISE, pinned explicitly. The provider now defaults new instances
    # to ENTERPRISE_PLUS, on which the shared-core tiers are rejected outright
    # ("Invalid Tier (db-f1-micro) for (ENTERPRISE_PLUS) Edition") and the
    # cheapest alternative is a db-perf-optimized-N-* machine costing many
    # times more. Pre-launch this instance is the only always-on cost in the
    # stack, so the edition is a cost decision, not a formality.
    #
    # Found by applying. terraform plan accepted db-f1-micro without complaint
    # - edition compatibility is enforced by the API, not the schema.
    edition = "ENTERPRISE"
    tier    = "db-f1-micro"

    backup_configuration {
      # From day one. With the ledger intact, economy state is reconstructible
      # even from an imperfect restore - solo_execution 5.7.
      enabled                        = true
      point_in_time_recovery_enabled = true
      start_time                     = "09:00"
    }

    ip_configuration {
      # Cloud Run reaches it over the Cloud SQL connector rather than a public
      # IP. A database with a public IP is one password from being
      # everyone's. db_public_ip/db_authorized_networks (variables.tf) exist
      # ONLY for the ephemeral verify-then-destroy cycle described on
      # deletion_protection; both default off, so this stays private-IP-only
      # committed.
      ipv4_enabled    = var.db_public_ip
      private_network = google_compute_network.main.id

      dynamic "authorized_networks" {
        for_each = var.db_public_ip ? var.db_authorized_networks : []
        content {
          name  = "ephemeral-cycle-${replace(authorized_networks.value, "/", "-")}"
          value = authorized_networks.value
        }
      }
    }
  }

  # HA is DEFERRED behind its section 10 trigger: the first non-TestFlight
  # players. Turning it on is a settings block, not a migration.
  #
  # deletion_protection is a VARIABLE defaulting to true, not a literal, so an
  # ephemeral verify-then-destroy cycle is a flag rather than an edit. See
  # variables.tf for why that cycle is worth running at all.
  deletion_protection = var.deletion_protection
}

resource "google_sql_database" "app" {
  name     = "broodline"
  instance = google_sql_database_instance.main.name
}

# The role DATABASE_URL has always named and nothing has ever created.
#
# `postgresql://broodline_app@localhost/broodline?host=/cloudsql/...` (see the
# api service below) connects as `broodline_app`. There was no
# google_sql_user anywhere in this directory, so the first deploy would have
# authenticated as a role Postgres has never heard of and failed on the first
# query - after a green apply and a green revision, because nothing consults
# the database until a request arrives.
#
# WHY A PASSWORD AT ALL, given the URL carries none. Two routes exist and only
# one of them is reachable from here:
#
#   - Cloud SQL IAM database authentication (type = CLOUD_IAM_SERVICE_ACCOUNT)
#     needs no password, which would match the URL exactly. But the caller
#     must present an OAuth token AS the password, and only the Cloud SQL
#     LANGUAGE connectors do that automatically. Cloud Run's built-in
#     /cloudsql volume mount (the one this service uses) is not documented to
#     perform automatic IAM authentication, so taking this route means adding
#     the Node connector library to services/api - application code, which
#     this round does not touch.
#   - A built-in user with a password, delivered out of band. That is this.
#
# WHY THE PASSWORD IS NOT IN THE URL. Putting it in DATABASE_URL would put a
# live credential in this file, in the plan output, in state, and in the Cloud
# Run env var where anyone with viewer on the project can read it. Instead the
# api receives it as PGPASSWORD from Secret Manager (see the api service),
# which node-postgres reads as the fallback when the connection string omits a
# password - pg/lib/connection-parameters.js resolves `password` through
# process.env.PGPASSWORD. So DATABASE_URL stays exactly as it was and no
# application code changes.
#
# WHY TERRAFORM WRITES THE SECRET VERSION HERE, when JWT_SECRET's version is
# deliberately created out of band. JWT_SECRET has ONE consumer: whatever
# value the api reads is correct by definition. This password has TWO - the
# Postgres role and the api - and they must agree. Creating them out of band
# independently is how they silently drift, and a drifted database password
# is a 28P01 at 3am with two plausible causes. One input, written to both
# places by one apply, cannot drift.
#
# WHY NOTHING LANDS IN STATE ANYWAY. password_wo and secret_data_wo are
# WRITE-ONLY arguments (provider >= 6.23, Terraform >= 1.11 - see the
# required_version block): they are accepted from configuration and stored in
# neither the plan nor the state file. var.db_password itself has no default
# and is supplied as TF_VAR_db_password at apply time, so the value is in
# neither this file nor the committed tfvars (terraform.tfvars is gitignored
# at .gitignore:126 and only terraform.tfvars.example is tracked).
resource "google_secret_manager_secret" "db_password" {
  secret_id = "broodline-db-password"
  replication {
    auto {}
  }
}

resource "google_secret_manager_secret_version" "db_password" {
  secret         = google_secret_manager_secret.db_password.id
  secret_data_wo = var.db_password
  # Bumping var.db_password_version is what makes a rotation take effect;
  # changing the VALUE alone is invisible to Terraform, because a write-only
  # argument is not stored and therefore cannot be diffed. That is the
  # documented rotation mechanism for write-only attributes, and it is the
  # one sharp edge they carry.
  secret_data_wo_version = var.db_password_version
}

resource "google_secret_manager_secret_iam_member" "api_db_password" {
  secret_id = google_secret_manager_secret.db_password.id
  role      = "roles/secretmanager.secretAccessor"
  member    = "serviceAccount:${google_service_account.api.email}"
}

resource "google_sql_user" "app" {
  name     = "broodline_app"
  instance = google_sql_database_instance.main.name

  password_wo = var.db_password
  # Must move in lockstep with the secret version above - they are two halves
  # of one rotation, and bumping only one of them is precisely the drift this
  # arrangement exists to prevent.
  password_wo_version = var.db_password_version
}

resource "google_storage_bucket" "config" {
  name                        = "${var.project_id}-broodline-config"
  location                    = var.region
  uniform_bucket_level_access = true

  versioning {
    # Bundles are immutable by convention; versioning makes the POINTER
    # recoverable too, since it is the one object that is deliberately
    # overwritten.
    enabled = true
  }
}

resource "google_service_account" "api" {
  account_id   = "broodline-api"
  display_name = "Broodline api"
}

resource "google_project_iam_member" "api_sql" {
  project = var.project_id
  role    = "roles/cloudsql.client"
  member  = "serviceAccount:${google_service_account.api.email}"
}

resource "google_storage_bucket_iam_member" "api_config" {
  bucket = google_storage_bucket.config.name
  # Read only. The api never publishes a bundle; publishing is a developer
  # action run from a workstation or CI, which is what keeps a compromised
  # request handler from shipping config to every player at once.
  role   = "roles/storage.objectViewer"
  member = "serviceAccount:${google_service_account.api.email}"
}

resource "google_secret_manager_secret" "jwt" {
  secret_id = "broodline-jwt-secret"
  replication {
    auto {}
  }
}

resource "google_secret_manager_secret_iam_member" "api_jwt" {
  secret_id = google_secret_manager_secret.jwt.id
  role      = "roles/secretmanager.secretAccessor"
  member    = "serviceAccount:${google_service_account.api.email}"
}

resource "google_cloud_run_v2_service" "api" {
  # The role must exist before the revision that authenticates as it. Nothing
  # in the template references google_sql_user.app - the credential arrives
  # by env var, not by attribute - so Terraform has no way to infer this
  # edge, exactly as it had none for the Service Networking peering above.
  # Without it the service can be created first and the first request in is
  # the thing that discovers the role is missing.
  depends_on = [google_sql_user.app]

  name     = "broodline-api"
  location = var.region
  ingress  = "INGRESS_TRAFFIC_ALL"

  # Cloud Run v2 carries its OWN deletion_protection, separate from Cloud
  # SQL's and defaulting to true. Wiring only the database to the variable
  # left `terraform destroy` refusing to remove the service - discovered
  # mid-teardown, with the stack still standing.
  deletion_protection = var.deletion_protection

  template {
    service_account = google_service_account.api.email

    scaling {
      min_instance_count = 0
      # A HARD CAP, deliberately. solo_execution 5.7: Cloud Run scales to
      # hundreds of instances and each opens a pool, so Postgres runs out of
      # connections long before CPU. The cheap version of that fix is this
      # flag plus a small per-instance pool; PgBouncer arrives when the cap
      # throttles real traffic.
      max_instance_count = 10
    }

    volumes {
      name = "cloudsql"
      cloud_sql_instance { instances = [google_sql_database_instance.main.connection_name] }
    }

    containers {
      image = var.image

      volume_mounts {
        name       = "cloudsql"
        mount_path = "/cloudsql"
      }

      env {
        name  = "CONFIG_BUCKET"
        value = google_storage_bucket.config.name
      }
      # Design 5.1's second collection. index.ts hard-fails at boot if this is
      # unset, ON PURPOSE (its comment explains why: a missing bucket loses
      # every replay silently and forever, while a crash-loop is caught in
      # seconds and rolls back with one command). That makes this line a
      # DEPLOY-ORDER PREREQUISITE, not a convenience - the bucket above must
      # exist before the revision that reads this ships.
      env {
        name  = "REPLAY_BUCKET"
        value = google_storage_bucket.replays.name
      }
      # ############################################################
      # DEPLOY PREREQUISITE - THIS SECRET HAS NO VERSION IN TERRAFORM
      # ############################################################
      #
      # `version = "latest"` resolves to nothing on a secret with zero
      # versions, and google_secret_manager_secret.jwt above creates the
      # SECRET but deliberately not a VERSION - a JWT signing key in
      # Terraform is a JWT signing key in state and in every plan output, so
      # the value is created out of band on purpose.
      #
      # The consequence, stated plainly rather than implied: until a human
      # runs the command below, THIS REVISION FAILS TO START. Cloud Run
      # reports the failure as a generic "Revision is not ready" / container
      # failed to start, which reads like an application crash and sends
      # people into the api logs, where there is nothing to find.
      #
      # WHAT A HUMAN MUST RUN, once per project, BEFORE the first deploy:
      #
      #   openssl rand -base64 48 \
      #     | tr -d '\n' \
      #     | gcloud secrets versions add broodline-jwt-secret \
      #         --data-file=- --project "$PROJECT_ID"
      #
      # At least 32 characters: services/api/src/identity/session.ts refuses
      # to boot on a shorter one. `openssl rand -base64 48` yields 64.
      #
      # implementation/scripts/deploy.sh refuses to deploy until that version
      # exists, and names this command when it refuses - so the failure is
      # legible BEFORE the revision ships rather than as a container that
      # will not start afterwards. The check there lists versions; it never
      # reads the value, which is the point of keeping the value out of here.
      env {
        name = "JWT_SECRET"
        value_source {
          secret_key_ref {
            secret  = google_secret_manager_secret.jwt.secret_id
            version = "latest"
          }
        }
      }
      env {
        name  = "DATABASE_URL"
        value = "postgresql://broodline_app@localhost/broodline?host=/cloudsql/${google_sql_database_instance.main.connection_name}"
      }
      # The password half of DATABASE_URL, kept OUT of DATABASE_URL.
      #
      # node-postgres resolves a missing connection-string password through
      # process.env.PGPASSWORD (pg/lib/connection-parameters.js), so these two
      # env vars compose into the credential without the credential ever
      # appearing in a URL, in this file, or in a plan. See google_sql_user.
      # app for why the role needs a password at all.
      #
      # Unlike JWT_SECRET above this one DOES have a version created by
      # Terraform, so it is not a deploy prerequisite - google_secret_manager_
      # secret_version.db_password writes it from the same var.db_password
      # that sets the role's password.
      env {
        name = "PGPASSWORD"
        value_source {
          secret_key_ref {
            secret = google_secret_manager_secret.db_password.secret_id
            # Pinned to the version this apply wrote, not "latest". A
            # rotation must change the ROLE and the api together; pointing at
            # "latest" would let a new version reach the api on its next cold
            # start while google_sql_user still carried the old password, so
            # the two would disagree for exactly as long as it took someone
            # to notice.
            version = google_secret_manager_secret_version.db_password.version
          }
        }
      }
      # sim's address, from the resource rather than hand-typed: a Cloud Run
      # URL is assigned at create time and a literal here would be a guess
      # that survives typechecking and fails at boot. index.ts hard-fails
      # without it, same as REPLAY_BUCKET.
      #
      # This is an INTERNAL-ingress URL (see the sim service above). It is
      # still an https://*.run.app address - internal ingress changes who may
      # reach it, not what it is called - so a value appearing here is not by
      # itself evidence that the api can reach it.
      env {
        name  = "SIM_BASE_URL"
        value = google_cloud_run_v2_service.sim.uri
      }

      startup_probe {
        http_get { path = "/healthz" }
        initial_delay_seconds = 5
        failure_threshold     = 10
      }
    }
  }
}

# ---------------------------------------------------------------------------
# sim - the second deployable. Design 3.1.
# ---------------------------------------------------------------------------

# Its OWN service account, and the interesting thing about it is what is NOT
# attached below: no roles/cloudsql.client, no bucket binding, no secret
# accessor. Design 3.1 - "no Cloud SQL client, no GCS client, no JWT_SECRET,
# no knowledge of players, wallets or servers" - is enforced HERE, by the
# absence, not by sim's source happening not to import a client today. Running
# sim as the api's account would have made every one of those grants sim's
# too, silently, and made that line a comment rather than a control.
resource "google_service_account" "sim" {
  account_id   = "broodline-sim"
  display_name = "Broodline sim"
}

resource "google_cloud_run_v2_service" "sim" {
  name     = "broodline-sim"
  location = var.region

  # THE control, per design 3.1 and the comment above MapPost in
  # services/sim/Program.cs: "there is no authentication here and there must
  # be no public route." sim takes bytes and returns a verdict; it checks no
  # token, rate-limits nothing, and knows no player. The `/internal/` path
  # prefix is a reminder to a reader, not a guard - routing serves that path
  # to anyone who can reach the service, so reachability is the whole of the
  # defence and this one line is all of it.
  #
  # ############################################################
  # THIS SERVICE IS NOT REACHABLE FROM `api` AS THIS STACK STANDS
  # ############################################################
  #
  # The previous note here said "my understanding is that a Cloud Run ->
  # Cloud Run call without VPC egress is not internal... I am not fully
  # certain." It has since been checked against the documentation rather than
  # reasoned about, and the hedge was right:
  #
  #   "When calling from Cloud Run or App Engine to a Cloud Run service
  #    that's set to 'Internal' or 'Internal and Cloud Load Balancing',
  #    traffic must route through a VPC network that's considered internal."
  #       - cloud.google.com/run/docs/securing/ingress, "Access internal
  #         services"
  #
  # `api` has NO vpc_access block, so its call to sim's *.run.app address
  # does not route through this project's VPC and is therefore not internal.
  # sim rejects it at the network layer, BEFORE IAM is consulted - so this is
  # a second, independent gate from the invoker binding below, and fixing
  # that one does not open this one.
  #
  # WHAT THE DOCUMENTATION SAYS IS REQUIRED (run/docs/securing/private-
  # networking, "Receive requests from other Cloud Run resources or App
  # Engine"): configure the SOURCE service with Direct VPC egress or a
  # connector, and then either
  #   (a) "route all traffic through the VPC network and enable Private
  #       Google Access on the subnet", or
  #   (b) "enable Private Google Access on the subnet associated with the
  #       source resource and configure DNS to resolve run.app URLs to the
  #       private.googleapis.com (199.36.153.8/30) or restricted.
  #       googleapis.com (199.36.153.4/30) ranges".
  #
  # NOT WRITTEN HERE, DELIBERATELY, AND THE REASON IS COST AND A LIVE CODE
  # PATH. Route (a) sends ALL of api's egress through the VPC, including
  # services/api/src/identity/apple.ts's fetch of
  # https://appleid.apple.com/auth/keys - a non-Google endpoint that Private
  # Google Access does not cover and that a Direct-VPC-egress instance, which
  # has no external IP, cannot reach without Cloud NAT. Cloud NAT is another
  # billable resource, and the documentation does not state plainly whether
  # it is strictly required here. Route (b) avoids that but needs a private
  # DNS zone for run.app, and the docs do not say whether the default
  # `private-ranges-only` egress routes 199.36.153.8/30 through the VPC at
  # all. Both sub-questions are open, both move the bill, and the bill is
  # what is currently being decided. See the Task 11 gaps report.
  #
  # THE ONE FIRM COST FINDING, because it inverts the assumption that framed
  # this as expensive: Direct VPC egress carries NO connector-instance
  # compute charge and scales to zero (run/docs/configuring/connecting-vpc);
  # it is the SERVERLESS VPC ACCESS CONNECTOR, the other option, that bills
  # always-on VMs at roughly $8-10/month. The reachability fix therefore does
  # not have to be the expensive one.
  ingress = "INGRESS_TRAFFIC_INTERNAL_ONLY"

  # Same reason as the api service above, learned the same way: Cloud Run v2
  # carries its own deletion_protection defaulting to true, and a service
  # that is not wired to this variable refuses `terraform destroy` mid-
  # teardown with the rest of the stack already standing. Wiring the api and
  # not sim would reproduce that exact discovery on the next ephemeral cycle.
  deletion_protection = var.deletion_protection

  template {
    service_account = google_service_account.sim.email

    scaling {
      # Scales to zero - design 3.1: "nothing to drain, no connection pool
      # against the Postgres cap". sim is the one component here that is
      # genuinely free at rest.
      min_instance_count = 0

      # Capped for a DIFFERENT reason than the api's cap. The api's 10 is a
      # Postgres connection ceiling (solo_execution 5.7); sim holds no
      # connection and could safely run wider. This cap is a COST and blast-
      # radius bound: sim is invoked once per wave submission, the api that
      # invokes it is itself capped at 10, and nothing should be able to fan
      # this service out past the only caller that exists.
      max_instance_count = 10
    }

    containers {
      image = var.sim_image

      # No env block at all, and no volumes. sim reads no configuration:
      # ASPNETCORE_URLS is baked into services/sim/Dockerfile and the engine
      # arrives by ProjectReference, not by a bundle it would have to fetch.
      # An env var here would be the first thing sim has to be told, and it
      # does not need telling.

      startup_probe {
        # services/sim/Program.cs maps GET /healthz.
        http_get { path = "/healthz" }
        initial_delay_seconds = 5
        failure_threshold     = 10
      }
    }
  }
}

# api -> sim. Design 3.1: "invoked by api's service account."
#
# Cloud Run requires an authenticated caller unless allUsers holds this role,
# and no such binding exists here - so this is the ONLY identity that can
# invoke sim, which is the shape the design asks for. It is also, at the time
# of writing, a shape services/api/src/sim/client.ts cannot satisfy: SimClient
# calls fetch() with a content-type header and no Authorization header, and
# the api has no google-auth-library dependency to mint an OIDC ID token with.
# See the Task 11 report - this is a runtime gap that no plan can surface,
# because IAM denial happens on a request, not on an apply.
resource "google_cloud_run_v2_service_iam_member" "api_invokes_sim" {
  name     = google_cloud_run_v2_service.sim.name
  location = google_cloud_run_v2_service.sim.location
  role     = "roles/run.invoker"
  member   = "serviceAccount:${google_service_account.api.email}"
}

# ---------------------------------------------------------------------------
# The player-visible replay collection. Design 5.1 / 5.2.
# ---------------------------------------------------------------------------

# A SECOND bucket, deliberately not a prefix inside the config bucket: the two
# collections have opposite retention (config objects are versioned and kept,
# replays are deleted at 30 days) and opposite access for the api (read-only
# on config, write-only here). A lifecycle rule is a bucket-level object, so
# one bucket could not carry both policies without the age rule reaching the
# config bundles too.
resource "google_storage_bucket" "replays" {
  name                        = "${var.project_id}-broodline-replays"
  location                    = var.region
  uniform_bucket_level_access = true

  # DESIGN 5.1, AND THE REASON IT SHIPS BEFORE PINNING DOES: "a GCS lifecycle
  # rule deleting at 30 days is one line of Terraform and cannot be
  # retrofitted onto objects already deleted." An object written before this
  # rule exists is not retroactively dated; it is simply kept forever unless
  # something later goes looking for it. The rule is cheap now and impossible
  # later, so it lands with the bucket.
  #
  # The 20-pin exemption from design 5.1 is NOT implemented and is not owed
  # here: pinning needs a pinned_replays table and a UI that do not exist, and
  # at milestone-1 scale nothing is 30 days old yet. When pinning arrives, the
  # exemption is a matches_prefix condition or a separate pinned/ prefix, not
  # a change to this rule's age.
  lifecycle_rule {
    condition {
      age = 30
    }
    action {
      type = "Delete"
    }
  }

  # NO versioning block here, in deliberate contrast to the config bucket
  # above. Config versioning exists because the POINTER object is
  # deliberately overwritten; replay objects are write-once, keyed by
  # issuance id (design 5.2), and never rewritten. Turning versioning on
  # would also quietly defeat the rule above: with versioning enabled an
  # age-30 Delete only moves the live object to a NONCURRENT version, which
  # then needs its own with_state/num_newer_versions rule to actually go
  # away. The bucket would look like it deleted at 30 days and would in fact
  # be keeping every byte, billed, forever - the exact failure the rule is
  # here to prevent.
}

# api -> replay bucket. Least privilege, and objectCreator is the whole of it.
#
# NOT objectAdmin and not objectUser: GcsReplayStore
# (services/api/src/replays/gcs-store.ts) only ever calls .save(). It has no
# list() - removed in review precisely so this surface stays small - and no
# delete. objectCreator grants storage.objects.create and nothing else, so a
# compromised request handler cannot enumerate other players' replays or
# erase the evidence behind a disputed submission; the 30-day sweep is the
# lifecycle rule's job, running as GCS itself rather than as this identity.
#
# One consequence worth knowing rather than discovering: objectCreator cannot
# OVERWRITE, because replacing a live object needs storage.objects.delete. A
# second write to an already-written issuance key therefore 403s. That is
# harmless here and arguably correct - the write is gated on a fresh
# withIdempotency callback (routes/wave.ts), and the call site already wraps
# it in try/catch and swallows the error as a degraded viewer per design 5.2.
resource "google_storage_bucket_iam_member" "api_replays" {
  bucket = google_storage_bucket.replays.name
  role   = "roles/storage.objectCreator"
  member = "serviceAccount:${google_service_account.api.email}"
}
