# ADR-0008: Maintainability and clean architecture as product constraints

- Status: Accepted
- Date: 2026-09-27

## Context

Toren IDE is intended to become a long-lived open-source product. Contributors will have different levels of experience, and many changes will be made by people who did not design the original subsystem. A codebase that only the original authors can safely change would fail the product goal even if the IDE itself works.

The project therefore needs explicit architecture and maintainability rules before the feature surface grows.

## Decision

Maintainability, SOLID principles, clean dependency direction, automated tests, and understandable module ownership are non-functional product requirements.

Toren follows these rules:

1. Product behavior is separated from UI and infrastructure details.
2. Avalonia views contain presentation and unavoidable window/platform interaction only.
3. View models coordinate presentation state and user intent; they do not implement external toolchains.
4. External systems such as `dotnet`, MSBuild, Git, NuGet, Docker, debugger adapters, file-system integrations, and operating-system services are accessed through explicit contracts.
5. Infrastructure adapters depend on contracts rather than forcing callers to depend on infrastructure implementations.
6. Feature modules own their concepts and expose the smallest practical public surface.
7. New abstractions must solve a real coupling, testability, or ownership problem; abstraction for its own sake is avoided.
8. Constructor injection is preferred for dependencies. Hidden service locators and global mutable state are not allowed.
9. Asynchronous APIs propagate cancellation where practical.
10. Expected failures use explicit result/error models where appropriate; exceptions are not used as ordinary control flow.
11. Platform-independent behavior is covered by automated tests.
12. CI analyzers remain strict and warnings are treated as errors.
13. Significant architecture changes require an ADR.
14. A contributor should be able to understand and modify a feature without learning the entire IDE.

## Dependency direction

A normal feature should follow this direction:

```text
Presentation (Avalonia)
        |
        v
Application / feature contracts and use cases
        |
        v
Core feature model

Infrastructure adapters --------> application/feature contracts
```

UI and infrastructure are outer concerns. Core/application behavior does not reference Avalonia or operating-system UI APIs.

## Code-behind policy

Code-behind is allowed only for concerns that inherently require a view/window instance, including native dialogs, drag/drop, focus, window lifecycle, and similar desktop integration. It must immediately delegate the resulting intent/data to a view model or application service.

Business/product logic, workspace parsing, build orchestration, Git behavior, test discovery, and similar concerns must not be implemented in views.

## Consequences

- Some features require small interfaces and adapters before implementation.
- Tests can run without launching the desktop application.
- Modules may be split when boundaries become real, but the solution will not accumulate empty ceremonial layers.
- Pull requests can be reviewed against explicit architecture rules instead of personal style preferences.
- Junior contributors have clearer ownership boundaries and safer places to make changes.
