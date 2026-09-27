# Contributing to Toren IDE

Thanks for your interest in Toren IDE.

The project is in an early architecture/bootstrap phase. Before starting a larger change, open an issue or discussion describing the problem and proposed direction so that implementation work does not outrun the product architecture.

## Development principles

- Keep Toren interoperable with standard .NET tooling.
- Do not introduce proprietary project/build formats.
- Prefer clear subsystem boundaries over global service locators or shared mutable state.
- Prefer existing toolchains over reimplementing compilers, Git, NuGet, MSBuild, test platforms, or debugger engines.
- Keep macOS, Windows, and Linux in mind when designing filesystem, process, terminal, and UI behavior.
- New networked features must not make an account or cloud backend mandatory for local development.
- Avoid adding dependencies without reviewing license compatibility and maintenance health.

## Workflow

1. Create or choose an issue for non-trivial work.
2. Create a focused branch.
3. Keep commits understandable and reasonably scoped.
4. Add or update tests where behavior changes.
5. Update documentation/ADRs when architecture or product behavior changes.
6. Open a pull request with a clear problem statement and implementation summary.

## Pull requests

A pull request should explain:

- what problem it solves;
- why the chosen approach fits Toren's product principles;
- platform impact;
- testing performed;
- screenshots for visible UI changes;
- any new dependency and its license.

Keep unrelated refactoring out of feature/fix pull requests when possible.

## Architecture decisions

Significant technical decisions should be recorded in `docs/decisions/` as Architecture Decision Records (ADRs). Existing accepted decisions should be changed by a new superseding ADR rather than silently rewritten.

## Code style

The repository `.editorconfig` is authoritative for baseline formatting. Additional analyzers and build-time checks will be added as the implementation scaffold stabilizes.

## Licensing

By contributing to this repository, you agree that your contributions are provided under the repository's Apache License 2.0 unless explicitly documented otherwise.
