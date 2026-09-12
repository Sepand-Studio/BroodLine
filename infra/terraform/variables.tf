variable "project_id" {
  type        = string
  description = "GCP project id. broodline-508416 at milestone 1 - NOT the AI-Studio-created gen-lang-client-* project of the same display name."
}

variable "region" {
  type    = string
  default = "us-central1"
}

variable "image" {
  type        = string
  description = "Artifact Registry image URI for the api, tagged by commit SHA (never :latest - see implementation/scripts/deploy.sh)."
}
