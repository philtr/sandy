# ADR 0005: Publish agent releases from version tags

- Status: Accepted
- Date: 2026-08-26

## Context

Windows acceptance work sometimes needs an installable alpha built from a pull-request branch before that branch is merged. Restricting release publication to the default branch would prevent those test releases or require merging unverified Windows-specific behavior first.

## Decision

Treat an `agent-v*` tag as the publication trigger for the Windows agent, regardless of which branch contains the tagged commit. The release workflow resolves the package version from the tag, restores and tests the solution, publishes the self-contained application, verifies required bundled assets, packages it with Velopack, and creates the GitHub release. Versions containing a prerelease suffix are published as prereleases.

Tags are applied only to an intentionally selected commit. Branch CI remains the earlier feedback loop, and the tag workflow repeats validation against the exact commit being packaged.

## Consequences

- Alpha builds can be installed and tested before their pull request merges.
- A release tag is a consequential publication action and must identify the exact intended commit.
- Release correctness cannot depend on files that exist only on the default branch.
- The release workflow, rather than branch location, is responsible for validating and packaging the tagged source.
