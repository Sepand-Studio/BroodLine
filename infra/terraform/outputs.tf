output "api_url" {
  value = google_cloud_run_v2_service.api.uri
}

output "config_bucket" {
  value = google_storage_bucket.config.name
}

output "sql_connection" {
  value = google_sql_database_instance.main.connection_name
}

# NOTE on why this file appeared to be broken. `terraform output` reporting
# "No outputs found" was diagnosed as outputs added after the last apply, so
# that the state predated them. It is not that. The state holds 3 of the 13
# resources declared here - google_compute_network.main and the two private-
# services-access resources, all networking - and nothing else has ever been
# created. api_url and config_bucket read attributes of resources that do not
# exist, so there is nothing to report. The outputs are fine; the stack is
# almost entirely unapplied. See implementation/2026-09-13-phase5-followups.md
# section 5.

output "sim_url" {
  # INTERNAL ingress (see main.tf). Printing it is not publishing it - the URL
  # is not a secret and not reachable from outside the perimeter - but it is
  # also NOT a URL a developer can curl from a workstation, which is worth
  # knowing before someone reads a 403 here as a broken deploy.
  value = google_cloud_run_v2_service.sim.uri
}

output "replay_bucket" {
  value = google_storage_bucket.replays.name
}
