# Device Protocol

All timestamps are ISO 8601 UTC values. All JSON responses use `Content-Type: application/json`. The initial protocol has `schema_version: 1`; additive fields may be ignored, while breaking changes require a new schema version.

## Authentication

Enrollment is unauthenticated but requires the family join code. On success, the response includes the device token exactly once. Subsequent HTTP calls send:

```http
Authorization: Bearer <device-token>
```

Action Cable authenticates with the same token in the WebSocket connection URL or its negotiated connection parameters. The production URL must use `wss://`. Tokens must never be written to logs.

Common responses are `401` for an absent/invalid token, `403` for a revoked device, `409` for an idempotency conflict, `422` for invalid input, and `429` for enrollment/login throttling.

## Authoritative snapshot

Every state fetch, heartbeat response, enrollment response, and `timer_state` broadcast carries a complete snapshot:

```json
{
  "schema_version": 1,
  "state_version": 4,
  "server_time": "2026-08-23T21:00:00Z",
  "expires_at": "2026-08-23T21:30:00Z",
  "remaining_seconds": 1800,
  "timer_status": "active",
  "heartbeat_interval_seconds": 30
}
```

`expires_at` is nullable and is the authority. `remaining_seconds` is a display/bootstrap convenience calculated at `server_time`; clients must not repeatedly decrement that transmitted number. `timer_status` is `active` or `expired`. `state_version` increases whenever the authoritative deadline changes.

An agent accepts a snapshot when it has a greater version, or when it has the same version and refreshes clock calibration. It never restores an older deadline from a lower-version message. After process restart it may enforce a valid cached snapshot immediately, but must reconnect and reconcile without waiting for its normal heartbeat interval.

## Endpoints

### `POST /api/v1/enrollments`

Request:

```json
{
  "join_code": "ABCD-EFGH",
  "device_name": "Gaming PC",
  "agent_version": "1.0.0",
  "platform": "windows"
}
```

A successful response contains `device_id`, the one-time `device_token`, and `timer_state`. Once the response is stored successfully, the agent must use the issued token rather than retry enrollment; an enrollment retry can intentionally create a second device record.

### `GET /api/v1/state`

Returns the authenticated device's current snapshot. Agents call it at startup, after reconnect, after resume, and immediately before applying an update.

### `POST /api/v1/heartbeats`

Request fields include `agent_version`, `overlay_active`, and optional diagnostic state. The server records presence and returns the current snapshot. Send every 30 seconds while connected; do not overlap heartbeat requests.

### `POST /api/v1/events`

Posts a bounded batch of events. Each event has a client-generated UUID, type, occurred-at timestamp, and small JSON metadata object. Duplicate UUIDs are successful no-ops. Unknown event types are rejected rather than silently persisted.

### `POST /devices/:id/time_grants`

This browser/session endpoint accepts a duration of 5, 15, 30, or 60 minutes, a parent profile belonging to the current family, and an idempotency key. Five-minute grants can be repeated to make any five-minute increment. It creates the audit record and authoritative state atomically, then broadcasts after commit.

### `POST /devices/:id/screen_time_revocation`

This browser/session endpoint immediately replaces a future deadline with server-now, increments `state_version`, records the selected parent and prior deadline in the audit history, and broadcasts the resulting expired snapshot after commit. It is an idempotent end-current-allowance action, not pause/resume; a later time grant starts from server-now as usual.

## Action Cable

An authenticated agent subscribes to `DeviceChannel`; the server derives the device from the connection and ignores a client-supplied device ID. Messages have an explicit type and complete payload:

```json
{
  "type": "timer_state",
  "timer_state": {
    "schema_version": 1,
    "state_version": 5,
    "server_time": "2026-08-23T21:05:00Z",
    "expires_at": "2026-08-23T22:05:00Z",
    "remaining_seconds": 3600,
    "timer_status": "active",
    "heartbeat_interval_seconds": 30
  }
}
```

WebSocket delivery is an optimization, not a correctness dependency. Reconnect uses exponential backoff with jitter; the HTTP state request and recurring heartbeat close any gap.
