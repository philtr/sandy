# ADR 0003: Single-node SQLite and Solid Cable deployment

- Status: Proposed
- Date: 2026-08-26

## Context

Sandy is deployed for one household and has a low rate of parent actions,
agent heartbeats, and realtime broadcasts. It needs durable application data,
jobs, cache, and Action Cable coordination, but a multi-service production
stack would impose setup, monitoring, and recovery work disproportionate to
that load.

## Decision

Use Rails with SQLite-backed stores and Solid Cable. Run one Puma process by
default and keep all production SQLite files in the persistent `sandy_storage`
Docker volume. The public reverse proxy is the only inbound service; the app
container binds locally by default.

This is a deliberately single-node deployment model. SQLite locking and Solid
Cable coordination may permit additional processes when operationally tested,
but horizontal scale is not a current requirement.

## Consequences

- Deployment has one durable data volume and no Redis or external database to
  install or operate.
- A consistent backup is a cold archive of the complete volume, including WAL
  and SHM files; restore is destructive and follows ADR 0001.
- Operators must not run two application versions against the same volume.
- Throughput, high availability, multi-host deployment, and independently
  scalable realtime infrastructure are explicitly deferred.

## Alternatives considered

### PostgreSQL plus Redis

This is the appropriate route for greater concurrency or multiple app hosts,
but adds two services and backup/upgrade concerns that do not benefit the
current deployment.

### Managed cloud services

They reduce host administration but conflict with Sandy's homelab-first,
single-household operating model and add account/cost dependencies.

### In-memory realtime delivery only

It would fail to coordinate broadcasts across processes and loses useful
durability guarantees for Rails infrastructure data.
