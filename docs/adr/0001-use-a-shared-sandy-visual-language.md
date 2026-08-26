# ADR 0001: Use a shared Sandy visual language

- Date: 2026-08-26

## Context

The launcher, enrollment flow, timer warnings, expiration overlay, and parent PWA are parts of one product, but they had accumulated different colors, controls, spacing, and visual emphasis. This made system-owned warnings and setup screens feel unrelated to the launcher that children and parents recognize as Sandy.

## Decision

Use the launcher as the reference visual language for Sandy-owned interfaces. Windows surfaces share application-level WPF resources for the palette, typography, fields, buttons, cards, and warning/error colors. Enrollment, warning, countdown, and expiration windows compose those resources according to their interaction needs. The Rails PWA uses the same overall brand direction while retaining native web layout and behavior.

Visual consistency does not change window activation policy: interactive enrollment and parent controls can take focus, while passive timer notices remain non-activating.

## Consequences

- New Sandy UI should reuse shared tokens and styles before adding one-off values.
- Platform-specific layouts can differ, but their color, hierarchy, and component treatment should remain recognizably Sandy.
- Shared-resource changes have a wider visual impact and require checking the launcher, enrollment, warnings, and expiration UI together.
