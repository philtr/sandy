# ADR 0003: Minimize fullscreen applications at expiration

- Date: 2026-08-26

## Context

At expiration, an exclusive or aggressively topmost fullscreen application can remain above Sandy's blocking overlay. Closing or terminating the application would enforce visibility, but it could also discard unsaved state and makes an expired timer behave like an application-management policy.

## Decision

When screen time expires, minimize the current fullscreen application before focusing Sandy's expiration overlays. Keep the application process running and restore neither its window nor its input until normal Windows behavior or the user does so after more time is granted.

Sandy does not interpret expiration as an instruction to close or terminate the foreground application.

## Consequences

- The expiration UI becomes visible without destroying game or application state.
- A child can resume the existing application after a parent grants more time.
- Minimization is best-effort and remains subject to Windows privilege and secure-desktop boundaries.
- The enforcement path must continue even if the foreground window cannot be minimized.
