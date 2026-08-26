# Architecture

## Architecture decision records

Architecture decision records capture durable choices and their tradeoffs. The existing sections below describe Sandy's current architecture; these records add focused context for decisions as they are made.

| ADR | Status | Decision summary |
| --- | --- | --- |
| [ADR 0001: Use a shared Sandy visual language](adr/0001-use-a-shared-sandy-visual-language.md) | Accepted | Use the launcher's design language across Sandy-owned Windows surfaces and the parent PWA while preserving each surface's interaction model. |
| [ADR 0002: Keep advance warnings passive](adr/0002-keep-advance-warnings-passive.md) | Accepted | Keep visual timer warnings non-activating, associate them with compatible fullscreen targets, and rely on audio for exclusive fullscreen. |
| [ADR 0003: Minimize fullscreen applications at expiration](adr/0003-minimize-fullscreen-apps-at-expiration.md) | Accepted | Minimize a foreground fullscreen application so the expiration overlay is visible without closing or terminating the application. |
| [ADR 0004: Use spoken threshold cues and session ducking](adr/0004-use-spoken-threshold-cues-and-session-ducking.md) | Accepted | Play bundled cues once at 15, 5, and 1 minutes while temporarily reducing other shared-mode audio sessions to half volume. |
| [ADR 0005: Publish agent releases from version tags](adr/0005-publish-agent-releases-from-version-tags.md) | Accepted | Let any intentional `agent-v*` tag trigger a fully validated Windows release, including prereleases from pull-request branches. |
| [ADR 0006: Use Conventional Commits](adr/0006-use-conventional-commits.md) | Accepted | Use typed, optionally scoped commit subjects so project history and release metadata remain consistent and machine-readable. |
| [ADR 0007: Recovery generation and device re-enrollment after database restore](adr/0007-recovery-generation-and-device-reenrollment-after-restore.md) | Proposed | Rotate the recovery generation and invalidate existing device credentials after a destructive database restore. |
| [ADR 0008: Server-authoritative time and agent convergence](adr/0008-server-authoritative-time-and-agent-convergence.md) | Proposed | Make Rails the source of timer policy while agents converge from complete, versioned snapshots. |
| [ADR 0009: Single-node SQLite and Solid Cable deployment](adr/0009-single-node-sqlite-and-solid-cable.md) | Proposed | Use one Rails/Puma deployment with SQLite and Solid Cable for the household control plane. |
| [ADR 0010: Interactive agent and cooperative trust boundary](adr/0010-interactive-agent-and-cooperative-trust-boundary.md) | Proposed | Run a per-user WPF agent alongside Explorer rather than a hardened Windows control system. |
| [ADR 0011: Device capabilities and explicit revocation](adr/0011-device-capabilities-and-explicit-revocation.md) | Proposed | Scope device credentials to agent access and distinguish confirmed unenrollment from connection failures. |
| [ADR 0012: Local launcher state and online edit lease](adr/0012-local-launcher-state-and-online-edit-lease.md) | Proposed | Keep pins local while using a server-authorized, online edit lease for changes. |
| [ADR 0013: Single-household tenancy and parent attribution](adr/0013-single-household-tenancy-and-parent-attribution.md) | Proposed | Model one household with a shared parent account and two attribution profiles. |

## System boundary

Sandy has one authoritative Rails deployment and one or more enrolled Windows agents in a single family. Parent phones use the Rails PWA over HTTPS. Agents initiate HTTPS and WebSocket connections to the same public origin; the managed PC never listens on an internet-facing port.

Rails stores durable application, audit, job, cache, and Action Cable data in SQLite. Production is a single-node Rails/Puma deployment using Solid Cable and one persistent Docker volume. See [ADR 0009](adr/0009-single-node-sqlite-and-solid-cable.md).

The Windows agent runs as a per-user, single-instance WPF application in the interactive session. `Sandy.Core` owns platform-independent deadline, synchronization, and launcher-pin behavior; `Sandy.Agent` owns Windows integration. Explorer remains the Windows shell. See [ADR 0010](adr/0010-interactive-agent-and-cooperative-trust-boundary.md).

## Source of truth and convergence

Rails owns device allowance state and publishes complete, versioned snapshots after atomic changes. Agents cache a valid snapshot for short outages, reconcile through HTTPS, and use WebSocket delivery for low-latency updates. Their projected server time accounts for both monotonic and wall-clock elapsed time, so sleep, reboot, logout, and network loss do not pause screen time. See [ADR 0008](adr/0008-server-authoritative-time-and-agent-convergence.md) and the [device protocol](protocol.md).

## Domain model

- **Family** — the household boundary, timezone, and rotatable digest of its enrollment code.
- **Account** — the shared parent login and password digest.
- **ParentProfile** — one of the two attribution identities selected on each parent's phone.
- **Device** — the active or revoked credential digest, allowance window, launcher-edit lease, state version, last heartbeat, reported agent/overlay state, and revocation state.
- **TimeGrant** — immutable duration, prior/resulting deadline, parent, timestamp, and idempotency key.
- **DeviceEvent** — an idempotent agent observation or parent action such as startup, warning, reconnect, overlay, update lifecycle, or immediate screen-time revocation.

Timer state and connection state are orthogonal. The parent UI labels an offline countdown as stale and shows the last authoritative deadline.

## Security boundary

Parent requests use a password-authenticated Rails session. Enrolled agents use a device capability that is limited to that device's API and Action Cable stream. Confirmed revocation is distinct from an unknown credential or connectivity failure. TLS is required outside a trusted development environment, and logs must filter credentials and enrollment secrets. See [ADR 0011](adr/0011-device-capabilities-and-explicit-revocation.md) and the [device protocol](protocol.md).

## Launcher shell

While time is available, Sandy supplies a launcher and bottom AppBar on each monitor. The primary launcher shows the timer and local pins; secondary launchers provide backdrop and status. The agent releases its AppBars for stable fullscreen applications and restores the launcher through Sandy Home. A guardian restores Explorer's taskbar after an unexpected main-process exit.

Pins are local per-user data. A parent-authorized, online edit lease temporarily permits pin changes and access to the normal Windows desktop without stopping synchronization or granting elevation. See [ADR 0012](adr/0012-local-launcher-state-and-online-edit-lease.md).
