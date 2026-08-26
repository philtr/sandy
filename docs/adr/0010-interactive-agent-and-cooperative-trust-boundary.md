# ADR 0010: Interactive agent and cooperative trust boundary

- Date: 2026-08-26

## Context

Sandy must render a launcher, taskbar, and expired-session overlay in the
currently signed-in Windows user's desktop. Windows services cannot safely own
that interactive UI. Attempting hardened parental control would require
privileged components and still would not fully cover the Windows secure
desktop, elevated applications, or an administrator.

## Decision

Run Sandy as a per-user, single-instance WPF application in the interactive
session. Keep Explorer as the Windows shell and augment it with Sandy launchers
and AppBars while time is available. A small guardian process restores
Explorer's taskbar if the main agent exits unexpectedly.

Sandy is a household visibility and consistency tool, not a tamper-resistant
security boundary. It does not use a Windows service, `uiAccess`, a kernel
driver, an inbound agent server, an application allowlist, or an attempt to
resist an administrator.

## Consequences

- Desktop lifecycle, overlays, startup, taskbar recovery, and multi-monitor
  behavior are Windows-specific and require real-Windows acceptance testing.
- The agent can provide a familiar desktop experience without replacing the
  operating-system shell or requiring elevation.
- `Ctrl`+`Alt`+`Delete`, secure-desktop UI, elevated programs, other user
  sessions, and some exclusive-fullscreen programs can bypass or appear above
  Sandy UI.
- Security documentation must describe these limits plainly; features must not
  claim stronger enforcement than the chosen trust boundary provides.

## Alternatives considered

### Windows service or replacement shell

These approaches increase installation and recovery risk, do not safely solve
interactive UI ownership, and imply a stronger security commitment than Sandy
intends to make.

### Hardened parental-control agent

Drivers, application control, and anti-tamper mechanisms would significantly
expand the attack surface and operating burden while remaining incomplete
against a local administrator.

### Browser-only launcher

A browser cannot provide the desktop integration, AppBar behavior, and local
window switching required by the product.
