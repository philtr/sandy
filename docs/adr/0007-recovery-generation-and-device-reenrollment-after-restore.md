# ADR 0007: Recovery generation and device re-enrollment after database restore

- Status: Proposed
- Date: 2026-08-26

## Context

Sandy agents can enforce a cached timer snapshot while offline. Every snapshot
currently carries a per-device, monotonically increasing `state_version`, and
an agent rejects a snapshot with a lower version. This prevents delayed
WebSocket messages and stale HTTP responses from restoring an older deadline.

A cold SQLite-volume restore intentionally rolls the server database back to a
previous point in time. The restored `state_version` can therefore be lower
than an enrolled agent's cached version. An ordinary authenticated state fetch
cannot safely be treated as an exception to the ordering rule: doing so would
also make a delayed response capable of replacing a newer state.

Restore also has a security implication. A backup from before an unenrollment
contains the then-valid device-token digest, so restoring it can otherwise
make that credential valid again.

## Decision

Treat database recovery as a new, explicit recovery generation.

1. The control plane has an opaque, random `recovery_generation` value. It is
   stable during normal operation and is included in every authoritative timer
   snapshot. A device credential records the generation under which it was
   issued.
2. `state_version` is ordered only within a recovery generation. A current
   agent may cross to a different generation only after a new authenticated
   `GET /api/v1/state` reconciliation. It replaces its cached snapshot, then
   opens a new realtime connection. Realtime messages and heartbeat responses
   from a different generation are ignored until that reconciliation succeeds.
3. A restore is incomplete until an operator runs a dedicated recovery command
   before restoring public service. The command rotates `recovery_generation`
   and invalidates every device credential issued in the preceding generation.
   An affected token receives the machine-readable
   `device_reenrollment_required` response.
4. On `device_reenrollment_required`, a current agent clears its credential and
   cached snapshot, restores Explorer's taskbar, resets its in-memory timer,
   and offers the normal current-join-code enrollment flow. It does not keep
   enforcing an old cached state after confirmed recovery invalidation.
5. Agents that predate this response code receive the existing generic
   unauthorized response and remain fail-closed. Operators should upgrade
   agents before relying on seamless recovery; legacy agents must re-enroll
   manually.

The recovery command's exact name, storage schema, and UI are implementation
details. Its security and ordering effects are not optional.

## Consequences

- A successful database restore cannot silently reauthorize a device that was
  unenrolled after the backup point.
- Every enrolled PC must be re-enrolled after a destructive restore. This is a
  deliberate recovery-time cost for a small household deployment.
- The state protocol, agent cache, credential model, API errors, Windows
  recovery flow, test suite, and recovery runbook must be updated together.
- The runbook must distinguish a restore drill on an isolated host from a
  production recovery that rotates the generation and invalidates device
  credentials.
- Time grants, audit history, and parent-account changes made after the backup
  are still rolled back; this ADR prevents stale agent state and device-token
  resurrection, not general data-loss effects.

## Alternatives considered

### Clear agent caches manually

This is unreliable, does not address an already-running agent, and leaves a
previously revoked device token usable after restore.

### Accept any lower version from an authenticated state fetch

This makes the source of a snapshot, rather than its ordering, decide whether
it is safe. It weakens the stale-message protection that `state_version`
provides and is difficult to reason about when HTTP and WebSocket work
concurrently.

### Preserve existing credentials with an external revocation ledger

An append-only ledger outside the restored database could preserve
unenrollments and avoid re-enrollment. It adds another critical datastore and
backup/recovery procedure. That operational complexity is not justified for
Sandy's single-household deployment at this stage.
