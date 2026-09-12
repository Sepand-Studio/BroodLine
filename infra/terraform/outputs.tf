output "api_url" {
  value = google_cloud_run_v2_service.api.uri
}

output "config_bucket" {
  value = google_storage_bucket.config.name
}

output "sql_connection" {
  value = google_sql_database_instance.main.connection_name
}
