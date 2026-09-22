# ADR 0015: Restart the agent after an unexpected exit

- Date: 2026-09-22

## Context

The local timer keeps counting during a network outage, but it cannot enforce
the deadline after the agent process exits. The taskbar guardian restores
Explorer after a crash and then exits. A PC can remain usable past its cached
deadline until someone starts Sandy again.

## Decision

Extend the existing per-user guardian to restart Sandy after an unexpected
process exit. Restore Explorer before starting the replacement. The restarted
agent uses the normal startup path, which enforces its cached timer state before
network reconciliation.

Deleting the guardian lease marks an intentional exit. Normal shutdown,
unenrollment shutdown, Windows session exit, and update handoff must remove this
lease before the agent exits. The guardian must not restart those exits.

Allow at most three consecutive recovery launches. After an agent stays alive
for at least one minute, reset that count. Wait two seconds before a recovery
launch. If the process is still alive but its UI lease is stale, restore Explorer
and keep watching for process exit; do not kill the process or launch a duplicate.

## Consequences

- A crashed agent can resume offline enforcement without a parent restarting it.
- Repeated startup crashes cannot create an endless restart loop.
- This remains a per-user recovery aid within the trust boundary in
  [ADR 0010](0010-interactive-agent-and-cooperative-trust-boundary.md).
- A hung process, failed recovery launch, or repeated crash can still prevent
  enforcement. This change adds no service, privilege, or tamper resistance.
- Crash recovery, intentional exits, and update handoff need Windows acceptance
  checks in addition to tests of the recovery policy.
