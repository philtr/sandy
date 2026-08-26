# ADR 0012: Local launcher state and online edit lease

- Date: 2026-08-26

## Context

The launcher must be simple for a child to use without turning Sandy into an
application catalog, allowlist, or device-management system. Parents need a
bounded way to permit maintenance and pin changes, but these actions neither
require nor should imply Windows elevation.

## Decision

Store launcher pins as local, per-user agent data. Do not catalog applications
in the background or treat the server as the source of a device's application
list. During a parent-issued, absolute edit lease, the agent may enumerate
Start shortcuts and `shell:AppsFolder` on demand and accept an explicitly
chosen supported target.

The server owns the edit-lease expiry and broadcasts it in the authoritative
timer snapshot. The agent permits pin mutation only while the lease is current
under corrected server time, the screen-time allowance is active, and the
agent is online. Connection loss, lease expiry, or screen-time expiry locks
editing and returns any temporarily exposed desktop to Sandy.

## Consequences

- Pin contents remain on the PC, avoiding server-side inventory and discovery
  of installed applications.
- Parent authorization remains timely and cannot be extended by local clock
  changes or offline operation.
- The exact pin limit, lease duration, accepted target forms, and icon-cache
  location are product/protocol details rather than ADR commitments.
- Sandy Home and the temporary normal-desktop path are maintenance
  conveniences, not an elevation or security feature.

## Alternatives considered

### Server-managed application inventory and pins

This requires continual discovery, increases privacy and compatibility scope,
and turns local launcher preference into remote policy.

### Always allow pin changes

It removes the parent control needed to keep the launcher predictable.

### Permit offline editing during a lease

The agent could not reliably know whether a parent revoked the lease, so an
otherwise bounded authorization would remain active beyond its intended scope.
