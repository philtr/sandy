# Windows Release Acceptance

Run this checklist on actual x64 Windows 11 hardware or a VM before a stable release. Repeat the compatibility subset on the supported Windows 10 build. Record OS/build, display topology, DPI, agent version, server version, and results.

## Install, startup, and recovery

- Install as a standard user and confirm no administrator or inbound-firewall prompt is required.
- Complete first-boot enrollment and verify the token is not present in logs or plaintext configuration.
- Confirm only one agent instance runs and it starts at that user's next interactive logon.
- Verify no Start Menu, AppsFolder, Steam, or filesystem application enumeration occurs during ordinary startup.
- Reboot and verify cached state is enforced before network reconciliation completes.
- Verify invalid server URL, invalid join code, TLS error, unknown credential, and an explicitly unenrolled PC produce the intended recovery state without leaking credentials.
- Restart Explorer and verify Sandy re-registers its AppBars and hides recreated Explorer taskbars only after it is healthy.
- Crash, hang, kill, update, uninstall, and disable Sandy startup in separate trials; verify Explorer taskbars return and the user is never left without either taskbar.
- Confirm `Ctrl`+`Alt`+`Delete`, sign-out, and restarting Explorer remain deliberate recovery paths.

## Launcher and app editing

- Check the supplied mockup layout at 100%, 125%, 150%, and mixed DPI: primary timer card/grid, secondary backdrop/status, and coherent dark warning/expired visuals.
- Exercise 0, 1, 8, 11, and 15 pins and grid reflow; confirm the permanent **More** tile remains present.
- With editing locked, open **More** and verify it explains parent authorization without enumerating applications.
- Grant a 30-minute lease from the parent dashboard and verify near-immediate Action Cable delivery and an editing-unlocked countdown.
- Open **More** and add an app from the user Start Menu, common Start Menu, AppsFolder packaged app, `.exe`, `.lnk`, `.url`, file drag/drop, Steam URI, and HTTPS URI.
- Reject relative, malformed, `javascript:`, and `data:` URI targets. Confirm no command-line argument editor is exposed.
- Reorder and unpin entries; restart Sandy and confirm atomic persistence.
- Renew the lease and verify it resets to 30 minutes rather than stacking; use **Lock app editing now** and verify mutations stop.
- Disconnect networking during editing; verify editing locks immediately while committed pins remain launchable.
- While editing is unlocked, use **Account → Open Windows desktop**; confirm Sandy's AppBars release, Explorer's taskbar appears, and timer synchronization continues. Confirm Sandy Home returns to the launcher and hides Explorer again.
- Repeat the Windows-desktop escape, then expire the edit lease, disconnect networking, and expire screen time; each condition must close the escape automatically without a restart or sign-out.
- Let the lease expire during editing; verify the editor closes or becomes read-only.
- Expire screen time during editing; verify **Time's up** takes precedence immediately.
- Confirm app editing never grants Windows elevation and never bypasses screen-time expiration.
- Move/delete shortcut and executable targets and verify launch failures are explained without crashing or silently removing pins.

## Taskbar, windows, and full screen

- Verify normal maximized windows respect each Sandy AppBar's reserved work area.
- Launch desktop and packaged apps; verify pinned/run indicators, MRU activation, active-click minimize, and the multiple-instance chooser.
- Move representative windows across one to three monitors and confirm their running button follows the representative window.
- Verify Home button, either Windows-key tap, and `Ctrl`+`Alt`+`S` all restore/focus the existing launcher without duplicate windows or flicker.
- Verify Windows-key shell chords do not open Start, Search, Run, Settings, Explorer, or the Power User menu; a chord must not invoke Home on key-up.
- Verify `Win`+`Space` still opens the native keyboard-layout switcher and does not invoke Sandy Home on release.
- Confirm `Alt`+`Tab`, `Alt`+`Esc`, `Alt`+`F4`, `Ctrl`+`Esc`, and ordinary application shortcuts work while time remains.
- Test windowed, maximized, borderless full-screen, video full-screen, and exclusive full-screen applications.
- For borderless full-screen, verify the AppBar releases only that monitor after entry debounce and returns after exit/minimize/close/foreground loss without rapid flicker.
- Let the final-minute notice appear over both borderless and exclusive full-screen games; verify the notice stays above the game without taking focus or minimizing it, and that the game process remains running.
- Let time expire in a full-screen game; verify Sandy immediately minimizes the game and displays the blocking overlay without waiting for keyboard input, while leaving the game process running.
- Invoke Sandy Home from full screen and verify the application backgrounds/minimizes where possible, the launcher returns, and AppBars reserve their edges again.
- Change display resolution, scale, orientation, monitor topology, and full-screen monitor while running.
- Verify the passive network status icon, account/lock/sign-out, and sleep/restart/shutdown controls. Confirm Sandy does not emulate third-party notification-area icons.

## Timer and enforcement

- Grant 1, 5, 15, 30, and 60 minutes from each parent's installed PWA and verify attribution/history, live launcher/taskbar countdown, and allowance progress.
- Send two distinct grants concurrently; verify both accumulate. Retry the same idempotency key; verify it applies once.
- Confirm a normal WebSocket grant reaches the PC within two seconds.
- Disconnect the network while active; confirm monotonic local countdown continues. Reconnect and confirm authoritative convergence.
- Block WebSockets but allow HTTPS; confirm a heartbeat repairs state within one interval.
- Exercise sleep/wake, manual wall-clock changes, server restart, agent restart, and corrupt/missing local cache.
- Cross 15-minute and 5-minute thresholds normally and by reconciliation; verify each warning is shown once for that countdown epoch.
- Verify the final minute is prominent and counts live without blocking ordinary use.
- At zero, confirm Sandy AppBars release their work areas and every monitor receives a borderless topmost overlay at all supported DPI/topologies.
- Play audio from an unmuted default output; at zero, verify it mutes, then grant time and verify the prior state returns. Repeat with an endpoint that was already muted.
- Connect/disconnect/rearrange monitors while expired; verify overlays follow the topology.
- While expired, verify Win, Alt-Tab, Alt-Esc, Alt-F4, Ctrl-Esc, and Ctrl-Shift-Esc are suppressed and Home only refocuses the overlay.
- Grant new time while overlays are visible; verify overlays/hooks disappear, audio returns, launcher/AppBars restore, and no logout/restart is needed.
- Confirm countdown and synchronization continue while Sandy taskbars are hidden by full-screen applications.

## Revocation, updates, and release

- Unenroll a current agent and confirm it receives `device_revoked`, stops old synchronization, removes enforcement UI/hooks/audio mute, restores Explorer taskbars, clears credential/cache, preserves pins, and requires the current join code.
- Test a pre-migration already-unenrolled credential: generic unauthorized remains fail-closed but offers current-code re-enrollment without treating network errors as unenrollment.
- Confirm unenrolled credentials cannot fetch state, heartbeat, events, timer data, or launcher-edit authorization.
- Confirm startup and six-hour update checks against the configured public GitHub release source.
- While active, verify an update downloads without prompting and does not restart the agent.
- After at least 60 seconds expired, verify the agent refreshes state before applying, restores Explorer during handoff, restarts, restores cache/launcher, and reconnects.
- Grant time during the update boundary and confirm application is deferred.
- Interrupt a download and simulate an unreachable/corrupt release; verify the installed version keeps working and Explorer taskbars recover if Sandy fails.
- Verify Authenticode signatures on the executable/installer, including the guardian entry path, and inspect SmartScreen on a clean machine.
- Confirm there is no Explorer replacement, process allowlist, arbitrary process killing, enterprise policy, automatic app discovery, Steam library parsing, or notification-area emulation.

Release only when Rails tests, timer-core tests, WPF build/publish CI, this checklist, and backup/restore validation pass. Keep failures and accepted Windows/WPF limitations in the release notes.
