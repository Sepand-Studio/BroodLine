terraform {
  required_version = ">= 1.9"
  required_providers {
    google = { source = "hashicorp/google", version = "~> 6.0" }
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
  # See the report for Task 11: internal ingress means api must reach sim as
  # INTERNAL traffic, which is a property of how api egresses, not of this
  # setting. `terraform plan` cannot prove that hop.
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
