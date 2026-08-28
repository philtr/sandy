# ADR 0014: Use Hotwire Native for the iOS parent app

- Date: 2026-08-28

## Context

Parents currently manage Sandy through a mobile-responsive Rails PWA. The PWA
already owns authentication, parent attribution, device controls, audit history,
and realtime dashboard updates. An iOS app should feel at home on iPhone and
iPad without duplicating those rules in a second client or requiring a parallel
parent API.

Sandy deployments are self-hosted and have different public origins. A native
client therefore cannot assume one server hostname, and it must keep working
with the single-household, server-authoritative control plane described by the
existing architecture.

## Decision

Build the iOS parent app as a Hotwire Native shell around the existing Rails
HTML interface. Use one native navigation stack, persistent WebKit storage, and
server-driven path configuration. Keep parent authentication and actions in
Rails; do not introduce a separate iOS authentication or domain API.

Let the user configure and validate the deployment's HTTPS origin on first
launch. Store that non-secret origin locally and allow it to be changed from the
app. Bundle each path-configuration version in the app and serve the same
version from Rails so navigation can evolve without requiring every installed
client to update immediately.

Adapt Rails-rendered pages when the Hotwire Native user agent is present by
removing duplicate browser chrome and exposing equivalent settings, sign-out,
and server-connection controls. The PWA remains a supported parent client and
keeps its existing presentation.

## Consequences

- Parent behavior remains server-authoritative and ships with Rails deployments.
- Native navigation, error recovery, persistent sessions, and platform styling
  improve the iPhone and iPad experience without duplicating domain logic.
- The app requires network access for mutations; offline parent actions are not
  queued.
- Breaking navigation changes require a new versioned path-configuration
  resource while older resources remain available to installed clients.
- Native screens, bridge components, push notifications, biometrics, tabs, and
  Android support remain optional future enhancements.

## Alternatives considered

### Fully native Swift screens backed by JSON APIs

This would provide maximum native control but duplicate presentation and state
handling, expand the API and authentication surface, and slow delivery of
ordinary parent-interface changes.

### Continue with only the PWA

The PWA remains functional, but it cannot provide the same native navigation,
connection setup, TestFlight distribution, or platform-level error recovery.

### Hard-code one Sandy deployment origin

This is simpler for one household but conflicts with the self-hosted deployment
model and requires a new app build whenever the public hostname changes.
