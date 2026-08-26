# ADR 0011: Device capabilities and explicit revocation

- Date: 2026-08-26

## Context

Windows agents need unattended access to their own state and realtime stream,
while parents need separate authority to control time. An agent must also tell
the difference between a confirmed unenrollment and a transient outage; it
must not remove its enrollment merely because a request failed.

## Decision

Enrollment exchanges a rate-limited, human-readable family join code for a
random device capability. The server stores only digests of join codes and
device tokens; the agent stores its token using current-user DPAPI. A device
capability authorizes only that device's HTTP API and Action Cable stream. It
cannot access parent pages or grant time.

Parent requests use the normal authenticated Rails session. A selected parent
profile is signed attribution state, not a second authentication factor.

Unenrollment preserves a digest tombstone and returns the explicit
`device_revoked` result to a matching credential. The agent clears its matching
credential and cache only after that result, then restores Explorer and offers
re-enrollment. Unknown credentials, malformed realtime traffic, and network
failures remain generic unauthorized or offline conditions and do not imply
unenrollment.

## Consequences

- Parent and device privileges remain separate and narrowly scoped.
- A join code cannot be redisplayed after creation; replacing it needs explicit
  confirmation and does not invalidate enrolled devices.
- Logs must filter join codes, setup tokens, session material, authorization
  headers, and device tokens.
- Revocation has an understandable agent UX without treating connectivity
  problems as a destructive state change.
- Recovery-generation invalidation is a distinct result from ordinary
  revocation and is governed by ADR 0007.

## Alternatives considered

### One shared API token or parent credential on the PC

It would let a managed PC exercise parent authority and makes compromise of
the agent credential much more damaging.

### Mutual TLS or per-device certificates

This offers stronger credential binding but creates certificate enrollment,
renewal, and recovery work disproportionate to the present homelab scope.

### Delete the device row on unenrollment

The server could not distinguish a known revoked token from an unknown token,
forcing agents to infer revocation from ambiguous failures.
