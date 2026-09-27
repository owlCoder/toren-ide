# ADR-0007: Modular implementation boundaries

- Status: Accepted
- Date: 2026-09-27

## Context

Toren IDE is a desktop product with a broad feature surface. A single UI-heavy project would make the codebase difficult to test and would couple platform details to core product behavior. At the same time, creating a separate assembly for every future feature before it contains meaningful code would create unnecessary ceremony.

## Decision

Toren uses a pragmatic modular architecture with dependency direction toward small, platform-independent contracts.

The initial implementation is split into the following modules:

- `Toren.App` — Avalonia composition root and desktop UI;
- `Toren.Core` — dependency-free shared contracts and primitives;
- `Toren.Platform` — operating-system/process adapters implementing core contracts;
- `Toren.Workspaces` — workspace and project-selection concepts;
- `Toren.DotNet` — .NET SDK/toolchain integration behind explicit interfaces;
- `Toren.UnitTests` — fast tests for platform-independent behavior.

New assemblies are introduced when a feature has a real boundary, independent lifecycle, or testability benefit. Empty architectural layers are not created preemptively.

UI code may coordinate platform UI operations such as native file pickers, but product logic and external tool orchestration must remain outside views.

## Consequences

- Core behavior can be tested without Avalonia.
- Platform/process code is replaceable behind interfaces.
- The application project remains the composition root rather than the owner of product logic.
- Feature modules can be extracted or expanded as the MVP grows without introducing proprietary project formats or toolchains.
