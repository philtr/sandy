# ADR 0004: Use spoken threshold cues and session ducking

- Status: Accepted
- Date: 2026-08-26

## Context

Exclusive-fullscreen applications can cover normal WPF advance-warning windows. A warning channel independent of desktop z-order is therefore needed. The cue must remain understandable over game, music, and video audio without reducing its own volume.

## Decision

Bundle short PCM WAV cues for the 15-minute, 5-minute, and 1-minute thresholds with the Windows agent. Select and play a cue asynchronously only when `WarningTransitions` emits its matching threshold, preserving once-per-threshold behavior. Keep the existing visual notices alongside the spoken cue.

During playback, use Windows Core Audio session controls to set every active shared-mode audio session outside the Sandy process to 50% of its current volume. Do not change endpoint master volume, because that would also reduce Sandy's spoken cue. Restore a changed session after playback only when its volume still matches the level Sandy applied; a manual volume change made during the cue takes precedence.

Audio playback, device enumeration, ducking, and restoration are best-effort. Failures must not block timer transitions or expiration enforcement, and Sandy must release audio resources and restore any volumes it still owns during shutdown or expiration.

## Consequences

- Advance warnings remain perceivable when exclusive fullscreen prevents a visual overlay.
- Other shared-mode audio becomes quieter while the spoken cue remains at Sandy's session volume.
- Exclusive-mode sessions may not be individually ducked, although Sandy still attempts cue playback.
- Published agent artifacts must contain all three cue assets, and CI and release packaging verify their presence.
- Session restoration requires tracking both the original and applied volume and tolerating device or session disappearance.
