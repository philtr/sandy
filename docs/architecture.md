# Architecture

## Architecture decision records

Architecture decision records (ADRs) record durable project choices. A committed
ADR is part of the project's direction. Read the relevant ADRs before changing
behavior. Create a new ADR when a change would replace an existing decision.

| ADR | Decision summary |
| --- | --- |
| [ADR 0001: Use a shared Sandy visual language](adr/0001-use-a-shared-sandy-visual-language.md) | Use one Sandy visual language across Windows surfaces and the parent PWA. |
| [ADR 0002: Keep advance warnings passive](adr/0002-keep-advance-warnings-passive.md) | Keep visual warnings passive and use audio for exclusive fullscreen. |
| [ADR 0003: Minimize fullscreen applications at expiration](adr/0003-minimize-fullscreen-apps-at-expiration.md) | Minimize a fullscreen app without closing it when time expires. |
| [ADR 0004: Use spoken threshold cues and session ducking](adr/0004-use-spoken-threshold-cues-and-session-ducking.md) | Play cues at 15, 5, and 1 minute and lower shared audio during each cue. |
| [ADR 0005: Publish agent releases from version tags](adr/0005-publish-agent-releases-from-version-tags.md) | Use `agent-v*` tags to publish validated Windows releases. |
| [ADR 0006: Use Conventional Commits](adr/0006-use-conventional-commits.md) | Use typed commit subjects for consistent history and release metadata. |
| [ADR 0007: Recovery generation and device re-enrollment after database restore](adr/0007-recovery-generation-and-device-reenrollment-after-restore.md) | Rotate recovery data and invalidate device credentials after a restore. |
| [ADR 0008: Server-authoritative time and agent convergence](adr/0008-server-authoritative-time-and-agent-convergence.md) | Let Rails own timer policy and send complete versioned snapshots to agents. |
| [ADR 0009: Single-node SQLite and Solid Cable deployment](adr/0009-single-node-sqlite-and-solid-cable.md) | Run one Rails/Puma deployment with SQLite and Solid Cable. |
| [ADR 0010: Interactive agent and cooperative trust boundary](adr/0010-interactive-agent-and-cooperative-trust-boundary.md) | Run a per-user WPF agent with Explorer instead of a hardened control system. |
| [ADR 0011: Device capabilities and explicit revocation](adr/0011-device-capabilities-and-explicit-revocation.md) | Limit device credentials and distinguish revocation from connection failure. |
| [ADR 0012: Local launcher state and online edit lease](adr/0012-local-launcher-state-and-online-edit-lease.md) | Keep pins local and require an online parent-approved edit lease. |
| [ADR 0013: Single-household tenancy and parent attribution](adr/0013-single-household-tenancy-and-parent-attribution.md) | Model one household with a shared parent account and two profiles. |
| [ADR 0014: Use Hotwire Native for the iOS parent app](adr/0014-use-hotwire-native-for-ios.md) | Use a native iPhone and iPad shell around the Rails parent interface. |

## System boundary

One Rails deployment controls one family and its enrolled Windows agents. Parent
phones use the Rails PWA or the Hotwire Native iOS app over HTTPS. Agents open
HTTPS and WebSocket connections to the same public origin. The managed PC does
not listen on an internet-facing port.

Rails stores application, audit, job, cache, and Action Cable data in SQLite.
Production uses one Rails/Puma node, Solid Cable, and one persistent Docker
volume. See [ADR 0009](adr/0009-single-node-sqlite-and-solid-cable.md).

The Windows agent is a single-instance WPF app in the user's session.
`Sandy.Core` owns timer, synchronization, and launcher-pin logic.
`Sandy.Agent` owns Windows integration. Explorer remains the Windows shell. See
[ADR 0010](adr/0010-interactive-agent-and-cooperative-trust-boundary.md).

## Source of truth and convergence

Rails owns device allowance state. It publishes complete, versioned snapshots
after each atomic change. Agents cache valid snapshots during short outages,
reconcile over HTTPS, and use WebSockets for fast updates. The agent uses both
monotonic and wall-clock time, so sleep, reboot, logout, and network loss do not
pause screen time. See [ADR 0008](adr/0008-server-authoritative-time-and-agent-convergence.md)
and the [device protocol](protocol.md).

## Domain model

- **Family** — Household boundary, time zone, enrollment-code digest, and voice theme.
- **Account** — Shared parent login and password digest.
- **ParentProfile** — One of two parent identities used for attribution.
- **Device** — Credential digest, allowance, edit lease, state version, heartbeat, and revocation state.
- **TimeGrant** — Duration, old and new deadlines, parent, timestamp, and idempotency key.
- **DeviceEvent** — Idempotent agent observation or parent action.

Timer state and connection state are separate. The parent UI marks an offline
countdown as stale and shows the last authoritative deadline.

## Security boundary

Parents use a password-authenticated Rails session. Agents use a device
credential limited to that device's API and Action Cable stream. Confirmed
revocation is different from an unknown credential or a connection failure.
Use TLS outside trusted development environments. Logs must filter credentials
and enrollment secrets. See [ADR 0011](adr/0011-device-capabilities-and-explicit-revocation.md)
and the [device protocol](protocol.md).

## Launcher shell

While time is available, Sandy shows a launcher and bottom AppBar on each
monitor. The primary launcher shows the timer and local pins. Secondary
launchers show status. Sandy releases its AppBars for fullscreen apps and
restores them through Sandy Home. A guardian restores Explorer's taskbar after
an unexpected main-process exit.

Pins are local per-user data. A parent-approved online edit lease allows pin
changes and temporarily shows the normal Windows desktop. It does not stop
synchronization or grant elevation. See [ADR 0012](adr/0012-local-launcher-state-and-online-edit-lease.md).
