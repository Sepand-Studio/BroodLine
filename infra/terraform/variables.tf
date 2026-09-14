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

variable "db_public_ip" {
  type        = bool
  default     = false
  description = <<-EOT
    Gives the Cloud SQL instance a public IP alongside its private one.
    Defaults to false: the committed shape is private-IP-only, per
    ip_configuration's comment in main.tf - a database with a public IP is
    one password from being everyone's.

    Set true ONLY for the same ephemeral verify-then-destroy cycle described
    on deletion_protection, and only paired with db_authorized_networks
    scoped to the one machine running the cycle. The reason: the migration
    step and the manual done-when runbook run from a workstation outside the
    VPC, and the Cloud SQL Auth Proxy cannot bridge a private-only instance
    without a VPN or Interconnect into that VPC, which this stack does not
    build. A temporary public IP plus an authorized network limited to one
    /32 is the smallest opening that unblocks the proxy for a same-day
    cycle; both variables must be back at their defaults before the instance
    is meant to stay up.
  EOT
}

variable "db_authorized_networks" {
  type        = list(string)
  default     = []
  description = <<-EOT
    CIDR blocks allowed to reach the Cloud SQL public IP directly (e.g.
    ["203.0.113.4/32"] for one workstation). Only takes effect when
    db_public_ip is true; leave empty otherwise, since an authorized network
    on a private-only instance does nothing but is one more thing to forget
    to revert.
  EOT
}

variable "sim_image" {
  type        = string
  description = <<-EOT
    Artifact Registry image URI for `sim`, tagged by commit SHA on the same
    rule as `image` (never :latest).

    A SECOND image, not a second tag of the first. `sim` is .NET and builds
    from a different Dockerfile (services/sim/Dockerfile) over a context that
    must include engine/ as well as services/sim/ - design 3.1's
    ProjectReference, one source tree and two manifests - so the api image
    cannot serve both services.

    Required, with no default, deliberately and for the same reason `image`
    is: a default here would be a URI pointing at whatever happened to be
    true when this line was written, and a stale default deploys the wrong
    engine version silently. NOTE that this makes it a new required input -
    implementation/scripts/deploy.sh passes `image` and does not yet pass
    this, nor does cloudbuild.yaml build a sim container at all. Both are
    owed by the deploy half of Task 11; this file is the infrastructure half.
  EOT
}
