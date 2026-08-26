# ADR 0002: Keep advance warnings passive

- Date: 2026-08-26

## Context

Advance screen-time warnings must be noticeable without taking keyboard or controller input from a running application. A globally topmost WPF window alone is insufficient: fullscreen and borderless applications can change the effective z-order, and activating a notice can interrupt play.

## Decision

Show the 15-minute and 5-minute warning windows and the final-minute countdown as passive, non-activating tool windows. Position and resize them without activation or owner-z-order changes. While a compatible foreground application occupies its monitor, temporarily associate the warning surface with that foreground window so it stays above borderless fullscreen content.

Do not promise that ordinary WPF warning windows will appear over exclusive fullscreen. Spoken threshold cues are the reliable advance-warning channel in that mode.

## Consequences

- Advance warnings do not take focus or consume application input.
- Visual warnings work for windowed and borderless fullscreen applications when Windows permits normal desktop composition.
- Exclusive-fullscreen users can hear advance warnings even when the visual surface is hidden.
- Window ownership must be refreshed when the foreground fullscreen target changes and cleared when it is no longer valid.
