# ADR-0010: Feature-oriented source layout

- Status: Accepted
- Date: 2026-09-27

## Context

As an OSS IDE grows, folders that mix contracts, implementation classes, models, parsers, and adapters become difficult for new contributors to navigate. A predictable source layout reduces the amount of architecture knowledge required before making a safe change.

## Decision

Within a meaningful feature/module boundary, Toren separates source roles when more than one role exists.

Preferred folders are:

```text
Feature/
  Contracts/       public interfaces and stable boundaries
  Models/          data models and value objects
  Services/        application/feature implementations
  Adapters/        infrastructure or external-system implementations
  Parsing/         format-specific parsers when meaningful
  Errors/          feature-specific error factories/codes when meaningful
```

Folders are created only when they contain real code. Toren does not create empty architectural layers for symmetry.

Interfaces should normally live with the boundary they define, while concrete infrastructure implementations live in `Services` or `Adapters`. UI views and view models remain organized under presentation-specific folders in `Toren.App`.

Namespaces follow the source layout so ownership is visible from `using` statements.

## Consequences

- contributors can find contracts and implementations predictably;
- interfaces and concrete implementations are not mixed in one flat folder;
- module boundaries remain explicit without multiplying assemblies unnecessarily;
- moving code is expected when a feature outgrows a simple layout.
