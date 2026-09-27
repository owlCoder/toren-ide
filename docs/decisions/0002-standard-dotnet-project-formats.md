# ADR-0002: Use standard .NET project formats

Status: **Accepted**

## Context

Toren should improve the .NET development experience without creating ecosystem lock-in. Developers must be able to move between Toren, Visual Studio, JetBrains Rider, VS Code, and command-line tooling.

## Decision

Toren will use standard .NET project and workspace formats such as `.sln`, `.slnx`, `.csproj`, `global.json`, `Directory.Build.*`, `Directory.Packages.props`, `NuGet.Config`, and `.editorconfig`.

Toren will not introduce a proprietary project or build format as a requirement for normal development.

IDE-only state may be stored separately, but it must never become necessary to build or run the project outside Toren.

## Consequences

- Projects remain portable and interoperable.
- Project-system work must honor existing .NET/MSBuild semantics rather than simplifying them into an incompatible model.
- Toren-specific metadata must remain optional and non-authoritative.
