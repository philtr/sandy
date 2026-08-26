# ADR 0006: Use Conventional Commits

- Status: Accepted
- Date: 2026-08-26

## Context

Commit subjects are part of Sandy's long-lived project history. Free-form subjects make it harder to scan changes, distinguish product work from maintenance, and generate useful release notes or changelogs consistently.

## Decision

Use the Conventional Commits structure for Sandy commits:

```text
<type>[optional scope][optional !]: <description>
```

Use `feat` for user-visible capabilities, `fix` for corrections, and the standard supporting types such as `docs`, `test`, `refactor`, `perf`, `build`, `ci`, `style`, `chore`, and `revert` when they better describe the change. Add a concise scope such as `agent`, `server`, or `adr` when it clarifies the affected area. Write the description as a short imperative phrase without a trailing period.

Mark a breaking change with `!` before the colon and explain it in the commit body or a `BREAKING CHANGE:` footer. Keep commits focused so that one subject accurately describes the change.

## Consequences

- Commit history communicates the kind and affected area of each change at a glance.
- Release notes and changelogs can be derived from predictable commit metadata.
- Contributors must choose an appropriate type and may need to split unrelated changes into separate commits.
- Correcting nonconforming commits already shared on a working branch requires coordinating a history rewrite.
