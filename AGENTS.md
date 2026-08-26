# Sandy contributor guidance

## Start with the architecture

Read [the repository overview](README.md) and the relevant product and technical
documentation before changing behavior. The [ADR index](docs/architecture.md#architecture-decision-records)
links to Sandy's architecture decision records in `docs/adr/`.

Every committed ADR is binding project direction. Read every ADR relevant to the
work before designing or implementing it. Do not silently diverge from an ADR:
record a material change in a new or superseding ADR before implementing the
new direction.

## Develop test-first

For behavioral changes, use TDD:

1. Write a focused test that fails for the intended behavior.
2. Make the smallest implementation change that makes it pass.
3. Refactor while the test suite remains green.
4. Run the applicable verification before handing off the change.

For documentation-only changes, validate the changed content, links, and
commands instead of adding automated tests.

Run the relevant suite from the repository root:

- Rails server: `cd server && bin/rails test`
- .NET agent: `dotnet test agent/Sandy.slnx -c Release`

The WPF host requires Windows for runtime validation. Follow
[the Windows acceptance checklist](docs/windows-acceptance.md) before an agent
release.

## Commit history

Follow [ADR 0006](docs/adr/0006-use-conventional-commits.md):

```text
<type>[optional scope][optional !]: <description>
```

Use `feat` and `fix` for product changes, and use an appropriate supporting
type such as `docs`, `test`, `refactor`, `perf`, `build`, `ci`, `style`,
`chore`, or `revert`. Use a concise scope such as `agent`, `server`, or `adr`
when it clarifies the change. Keep the description imperative, concise, and
without a trailing period; keep each commit focused on one change.
