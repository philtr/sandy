# ADR 0008: Server-authoritative time and agent convergence

- Status: Proposed
- Date: 2026-08-26

## Context

Parents can issue concurrent time changes from separate phones. Windows agents
must continue enforcing through short network outages and sleep, while HTTP and
WebSocket state can arrive late or in a different order. A relative countdown
kept only by the agent would make the parent view non-authoritative and make
clock/timer manipulation easier to turn into extra time.

## Decision

Rails owns each device's absolute `expires_at` and a monotonically increasing
`state_version`. Time grants, screen-time revocation, and launcher-edit lease
changes are serialized transactions. They record their audit event and update
the version atomically; broadcast occurs only after commit.

For a grant, the new deadline is:

```text
max(current deadline, server time) + granted duration
```

Grant requests use a unique idempotency key so retries do not add time twice.
Revocation ends the current allowance at server time; it does not pause or bank
unused time.

Every state source carries a complete snapshot. WebSocket delivery provides
low latency, but an authenticated state fetch at startup/reconnect and regular
heartbeats provide correctness. Within a recovery generation, agents accept a
higher state version, or a same-version snapshot that refreshes time
calibration, and reject older state.

Agents cache the latest valid snapshot and project server time from both a
monotonic clock and corrected wall-clock elapsed time. Sleep, reboot, logout,
and network loss therefore do not pause screen time or an edit lease.

## Consequences

- Parents receive a single auditable ordering for grants and revocations.
- Agents remain useful during short outages without becoming a source of
  policy truth.
- Every state-changing endpoint and realtime message must preserve the
  snapshot/version contract.
- There is intentionally no v1 concept of pausing, schedules, banked balances,
  or usage-metered time.
- Database recovery requires the separate recovery-generation policy in
  [ADR 0007](0007-recovery-generation-and-device-reenrollment-after-restore.md).

## Alternatives considered

### Agent-owned relative countdowns

This makes local state authoritative, produces conflicting parent views, and
does not reliably account for sleep or local clock manipulation.

### WebSocket-only synchronization

Realtime delivery is not durable enough to be the correctness path. Agents
need a complete, authenticated pull mechanism after reconnect.

### Mergeable or client-generated timer state

Conflict-free replication is unnecessary for a small control plane with a
single authority and would obscure the audit trail for time changes.
