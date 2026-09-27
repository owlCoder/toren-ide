# ADR-0004: Orchestrate existing development toolchains

Status: **Accepted**

## Context

A desktop IDE needs build, package, version-control, test, and related workflows, but reimplementing mature development toolchains would increase risk and maintenance cost while reducing interoperability.

## Decision

Toren will orchestrate established tools instead of replacing them.

For the initial .NET-focused product this means, where practical:

- `dotnet` CLI and MSBuild for project/build workflows;
- Roslyn-backed language intelligence;
- system Git for version control;
- NuGet and standard project mechanisms for packages;
- standard .NET test platforms/frameworks;
- standard EF Core tooling;
- Docker CLI/Compose for container workflows.

Toren provides a coherent UI, process orchestration, structured output, diagnostics, navigation, and user experience around these tools.

## Consequences

- Projects remain compatible with the wider .NET ecosystem.
- Tool discovery/version handling becomes a first-class concern.
- Process execution must be cancellable, observable, and secure.
- Toren must present useful errors when required external tools are missing or incompatible.
