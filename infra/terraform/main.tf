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

      startup_probe {
        http_get { path = "/healthz" }
        initial_delay_seconds = 5
        failure_threshold     = 10
      }
    }
  }
}
