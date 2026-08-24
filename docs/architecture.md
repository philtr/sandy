# Architecture

## System boundary

Sandy has one authoritative Rails deployment and one or more enrolled Windows agents in a single family. Parent phones use the Rails PWA over HTTPS. Agents initiate HTTPS and WebSocket connections to the same public origin; the managed PC never listens on an internet-facing port.

Rails stores durable application, audit, job, cache, and Action Cable data in SQLite files under `/rails/storage`. Production runs one Puma process by default. Solid Cable provides cross-request Action Cable delivery without Redis. A single persistent Docker volume contains every production database.

The Windows agent runs in the interactive user's session because services cannot safely own desktop UI. It is a per-user, single-instance WPF application. Platform-independent deadline and synchronization logic lives in `Sandy.Core`; Windows startup, DPAPI, WPF windows, monitor handling, keyboard hooks, and updates live in `Sandy.Agent`.

## Source of truth and convergence

Rails owns `Device.expires_at` and a monotonically increasing `state_version`. A time grant is an atomic transaction:

```text
new deadline = max(current deadline, server time) + grant duration
```

The transaction records its before/after values and parent attribution, increments the device version, and broadcasts the complete resulting state only after commit. A unique idempotency key makes an HTTP retry return the existing grant rather than add time twice. Database serialization or a row lock ensures two distinct simultaneous grants both accumulate.

A parent can revoke the current allowance immediately. Rails atomically replaces the deadline with server-now, increments the same state version, records the parent and prior deadline as a device event, and broadcasts an expired snapshot. Revocation does not bank or pause unused time; a subsequent grant resumes from server-now.

The agent records a received server timestamp, absolute deadline, local UTC timestamp, monotonic timestamp, and state version. It advances normally from monotonic elapsed time and compares corrected wall-clock time after suspend/resume. A valid cached snapshot lets enforcement continue while Rails is unavailable. Reconnection always fetches or receives a complete snapshot; a higher `state_version` supersedes local state. A heartbeat every 30 seconds repairs a missed WebSocket notification.

Wall-clock time is intentional: sleep, reboot, logout, and network loss do not pause or bank screen time.

## Domain model

- **Family** — the household boundary, timezone, and rotatable digest of its enrollment code.
- **Account** — the shared parent login and password digest.
- **ParentProfile** — one of the two attribution identities selected on each parent's phone.
- **Device** — the credential digest, deadline, state version, last heartbeat, reported agent/overlay state, and revocation state.
- **TimeGrant** — immutable duration, prior/resulting deadline, parent, timestamp, and idempotency key.
- **DeviceEvent** — an idempotent agent observation or parent action such as startup, warning, reconnect, overlay, update lifecycle, or immediate screen-time revocation.

Timer state and connection state are orthogonal. A heartbeat no older than 75 seconds is `online`; otherwise the device is `offline`. An online device is `active` when its deadline is in the future and `expired` otherwise. The parent UI labels an offline countdown as stale and shows the last authoritative deadline.

## Security boundary

Parent requests use a password-authenticated Rails session, secure HTTP-only cookies, CSRF protection, and rate limiting. The selected parent profile is signed cookie state for attribution, not a second authentication factor.

Enrollment exchanges a rate-limited, human-readable family join code for a random 256-bit device token. Rails stores only token/code digests. Because the join-code digest is one-way, the parent UI cannot redisplay an existing code; it instead explains the limitation and requires explicit confirmation before generating a replacement. The agent protects the token with current-user DPAPI. A device token authorizes only that device's API and Action Cable stream; it cannot grant time or enter the parent UI. Revocation rejects subsequent HTTP and WebSocket authentication.

TLS is required outside a trusted development environment. Filter setup tokens, join codes, credentials, authorization headers, and device tokens from application logs.

## Deliberate limitations

- `Ctrl`+`Alt`+`Delete` remains available, and terminating the agent is possible.
- Elevated applications, secure-desktop UI, other Windows user sessions, and some exclusive-fullscreen applications can appear above or bypass a normal WPF overlay.
- There is no service, watchdog, `uiAccess`, kernel driver, inbound agent server, or attempt to resist an administrator.
- Version 1 has immediate grants only: no pause, schedule, banked balance, or usage-metered clock.
