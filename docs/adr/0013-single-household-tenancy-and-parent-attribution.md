# ADR 0013: Single-household tenancy and parent attribution

- Status: Proposed
- Date: 2026-08-26

## Context

Sandy is operated as one household's self-hosted control plane. It needs clear
ownership of devices and audit attribution for the parents who make time
changes, without introducing organization provisioning, invitations, roles,
or cross-household administration.

## Decision

Use a family as the household boundary. A deployment serves one family, with
one shared parent account and two parent profiles for action attribution. Each
device belongs to that family. The selected parent profile is signed session
state used in audit records; it is not independent authentication or an
authorization boundary.

## Consequences

- The data model and dashboard are intentionally simple for a household
  deployment.
- An audit entry can say which parent profile granted, revoked, or unlocked
  something without requiring separate parent credentials.
- Multi-family hosting, invitations, per-parent roles, account recovery flows,
  and delegated administration are deferred rather than implicitly supported.
- A future multi-tenant design would require a deliberate migration of account
  and authorization semantics, not just a new dashboard filter.

## Alternatives considered

### Multi-tenant families with individual parent accounts

This supports a hosted product and finer-grained access control, but adds a
substantial identity, invitation, authorization, and support surface.

### One undifferentiated shared account

This is slightly simpler but loses the attribution that makes the household
activity history useful.

### Parent profiles as separate authentication factors

Profiles are convenient attribution labels, not credentials. Treating them as
authentication would provide misleading security without separate secrets or
identity verification.
