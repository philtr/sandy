# Windows Release Acceptance

Run this checklist on actual x64 Windows 11 hardware or a VM before a stable release. Repeat the compatibility subset on the supported Windows 10 build. Record OS/build, display topology, DPI, agent version, server version, and results.

## Install and startup

- Install as a standard user and confirm no administrator or inbound-firewall prompt is required.
- Complete first-boot enrollment and verify the token is not present in logs or plaintext configuration.
- Confirm only one agent instance runs and it starts at that user's next interactive logon.
- Close the timer window and confirm the agent stays running; left-click the Sandy system tray icon and confirm the timer window reopens and receives focus.
- Reboot and verify cached state is enforced before network reconciliation completes.
- Verify an invalid server URL, invalid join code, TLS error, and revoked device produce actionable status without leaking credentials.

## Timer and synchronization

- Grant 1, 5, 15, 30, and 60 minutes from each parent's installed PWA and verify attribution/history.
- Send two distinct grants concurrently; verify both accumulate. Retry the same idempotency key; verify it applies once.
- Confirm a normal WebSocket grant reaches the PC within two seconds.
- Disconnect the network while active; confirm monotonic local countdown continues. Reconnect and confirm authoritative convergence.
- Block WebSockets but allow HTTPS; confirm a heartbeat repairs state within one interval.
- Exercise sleep/wake, manual wall-clock changes, server restart, agent restart, and a corrupt/missing local cache.

## Warnings and expired state

- Cross 15-minute and 5-minute thresholds normally and by reconciliation; verify each warning is shown once for that countdown epoch.
- Verify the final minute is prominent and counts live without blocking ordinary use.
- At zero, confirm every monitor receives a borderless topmost overlay that covers taskbars at 100%, 125%, 150%, and mixed DPI.
- Play audio from an unmuted default output; at zero, verify it is muted before the expired state appears, then grant time and verify the prior unmuted state is restored. Repeat with an endpoint that was already muted and verify it stays muted after time is granted.
- Connect/disconnect/rearrange monitors while expired; verify overlays follow the new topology.
- Try the Windows keys, Alt-Tab, Alt-Esc, Alt-F4, Ctrl-Esc, and Ctrl-Shift-Esc; verify they are suppressed only while expired.
- Confirm Ctrl-Alt-Delete remains available and document that Task Manager/agent termination is an intentional escape path.
- Grant new time while the overlay is visible; verify hooks and all overlays disappear promptly and the existing session resumes.
- Exercise representative windowed, borderless, and exclusive-fullscreen applications and record any application that can remain above the overlay.

## Updates and recovery

- Confirm startup and six-hour checks against the configured public GitHub release source.
- While active, verify an update downloads without prompting and does not restart the agent.
- After at least 60 seconds expired, verify the agent refreshes authoritative state immediately before applying, installs, restarts, restores cache, and reconnects.
- Grant time during the update boundary and confirm application is deferred.
- Interrupt a download and simulate an unreachable/corrupt release; verify the installed version keeps working.
- Verify Authenticode signatures on the executable and installer and inspect SmartScreen behavior on a clean machine.

Release only when timer-core automated tests, WPF build CI, Rails protocol tests, this checklist, and backup/restore validation all pass. Keep failures and accepted limitations in the release notes.
