# Architecture

## System boundary

Sandy has one authoritative Rails deployment and one or more enrolled Windows agents in a single family. Parent phones use the Rails PWA over HTTPS. Agents initiate HTTPS and WebSocket connections to the same public origin; the managed PC never listens on an internet-facing port.

Rails stores durable application, audit, job, cache, and Action Cable data in SQLite files under `/rails/storage`. Production runs one Puma process by default. Solid Cable provides cross-request Action Cable delivery without Redis. A single persistent Docker volume contains every production database.

The Windows agent runs in the interactive user's session because services cannot safely own desktop UI. It is a per-user, single-instance WPF application. Platform-independent deadline, synchronization, and launcher-pin persistence live in `Sandy.Core`; Windows startup, DPAPI, WPF windows, AppBars, monitor handling, keyboard hooks, and updates live in `Sandy.Agent`. Explorer remains the Windows shell.

## Source of truth and convergence

Rails owns `Device.expires_at` and a monotonically increasing `state_version`. A time grant is an atomic transaction:

```text
new deadline = max(current deadline, server time) + grant duration
```

The transaction records its before/after values and parent attribution, increments the device version, and broadcasts the complete resulting state only after commit. A unique idempotency key makes an HTTP retry return the existing grant rather than add time twice. Database serialization or a row lock ensures two distinct simultaneous grants both accumulate.

A parent can revoke the current allowance immediately. Rails atomically replaces the deadline with server-now, increments the same state version, records the parent and prior deadline as a device event, and broadcasts an expired snapshot. Revocation does not bank or pause unused time; a subsequent grant resumes from server-now.

A parent can also grant an absolute 30-minute launcher-edit lease. The lease is authoritative session state and is broadcast like timer changes. The agent permits pin mutations only when the timer is active, the lease has not expired according to corrected server time, and its current connection state is online. Pins are local per-user data and are not server policy.

The agent records a received server timestamp, absolute deadline, local UTC timestamp, monotonic timestamp, and state version. It advances normally from monotonic elapsed time and compares corrected wall-clock time after suspend/resume. A valid cached snapshot lets enforcement continue while Rails is unavailable. Reconnection always fetches or receives a complete snapshot; a higher `state_version` supersedes local state. A heartbeat every 30 seconds repairs a missed WebSocket notification.

Wall-clock time is intentional: sleep, reboot, logout, and network loss do not pause or bank screen time.

## Domain model

- **Family** — the household boundary, timezone, and rotatable digest of its enrollment code.
- **Account** — the shared parent login and password digest.
- **ParentProfile** — one of the two attribution identities selected on each parent's phone.
- **Device** — the active or revoked credential digest, allowance window, launcher-edit lease, state version, last heartbeat, reported agent/overlay state, and revocation state.
- **TimeGrant** — immutable duration, prior/resulting deadline, parent, timestamp, and idempotency key.
- **DeviceEvent** — an idempotent agent observation or parent action such as startup, warning, reconnect, overlay, update lifecycle, or immediate screen-time revocation.

Timer state and connection state are orthogonal. A heartbeat no older than 75 seconds is `online`; otherwise the device is `offline`. An online device is `active` when its deadline is in the future and `expired` otherwise. The parent UI labels an offline countdown as stale and shows the last authoritative deadline.

## Security boundary

Parent requests use a password-authenticated Rails session, secure HTTP-only cookies, CSRF protection, and rate limiting. The selected parent profile is signed cookie state for attribution, not a second authentication factor.

Enrollment exchanges a rate-limited, human-readable family join code for a random 256-bit device token. Rails stores only token/code digests. Because the join-code digest is one-way, the parent UI cannot redisplay an existing code; it instead explains the limitation and requires explicit confirmation before generating a replacement. The agent protects the token with current-user DPAPI. A device token authorizes only that device's API and Action Cable stream; it cannot grant time or enter the parent UI. Revocation moves the token digest into a tombstone, allowing Rails to return the machine-readable `device_revoked` result to agent 2.0 without permitting state access. The agent clears the matching enrollment/cache, restores Explorer's taskbar, and shows current-code re-enrollment. The existing family recovery setting remains available only to pre-2.0 agents: it can return a long-lived schema-1 active snapshot from state and heartbeat endpoints, including for pre-tombstone credentials whose device record is gone. Archiving remains separate dashboard metadata and is permitted only after revocation.

## Launcher shell

While time is available, Sandy owns one launcher surface and one bottom AppBar per monitor. The primary launcher contains the timer and the manual pin grid; secondary launchers provide the backdrop and status. Sandy hides Explorer taskbars only after its AppBars have registered and a separate guardian process is healthy. The guardian restores Explorer taskbars if the main process exits or stops renewing its five-second lease. Clean shutdown and update handoff also restore Explorer first.

Applications are never cataloged in the background. During an online parent-authorized edit lease, the user can enumerate Start shortcuts and `shell:AppsFolder` on demand, choose a shortcut/URL/executable, drop a supported file, or enter an absolute URI. Sandy stores at most 15 pins atomically in `%LocalAppData%\Sandy\launcher-pins.json` and caches icons extracted from explicitly chosen files in `launcher-icons`. Missing targets fail locally and remain editable on the next lease.

The Sandy taskbar inventories ordinary top-level windows, groups them by packaged-app identity or executable, and provides best-effort activation, minimization, and instance selection. It does not reproduce Explorer's notification area. A foreground window that stably occupies its monitor's full physical bounds causes only that monitor's Sandy AppBar to release its work area and hide; exit is debounced independently. Sandy Home restores the launcher and AppBars.

TLS is required outside a trusted development environment. Filter setup tokens, join codes, credentials, authorization headers, and device tokens from application logs.

## Deliberate limitations

- `Ctrl`+`Alt`+`Delete` remains available, and terminating the agent is possible.
- Elevated applications, secure-desktop UI, other Windows user sessions, and some exclusive-fullscreen applications can appear above or bypass a normal WPF overlay.
- The taskbar guardian is a recovery helper, not a security watchdog. There is no service, `uiAccess`, kernel driver, inbound agent server, application allowlist, or attempt to resist an administrator.
- Sandy suppresses Windows-key shell chords only as casual-access friction. It does not stop links, child processes, or unpinned applications from opening.
- Version 1 has immediate grants only: no pause, schedule, banked balance, or usage-metered clock.
