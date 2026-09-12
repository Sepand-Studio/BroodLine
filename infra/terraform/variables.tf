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

variable "deletion_protection" {
  type        = bool
  default     = true
  description = <<-EOT
    Guards the Cloud SQL instance against `terraform destroy`.

    Defaults to true, which is what any long-lived environment wants. Set it
    to false ONLY for a deliberately ephemeral verification cycle - stand the
    stack up, prove the done-when against real Cloud Run and real Cloud SQL,
    then tear it down again. That cycle exists because `terraform plan` proves
    syntax and API shape and nothing else: whether the private IP resolves,
    whether the Service Networking peering comes up, and whether Cloud Run can
    actually reach the database through the connector are all facts that only
    an apply can establish.

    Pre-launch the instance is the only always-on cost in this stack, so the
    intended lifecycle is: apply -> verify -> destroy -> develop locally
    against Testcontainers -> re-apply near soft launch with this back at true.
  EOT
}
