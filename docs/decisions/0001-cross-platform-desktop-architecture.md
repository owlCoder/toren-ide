# ADR-0001: Cross-platform desktop architecture

Status: **Accepted**

## Context

Toren must provide a first-class desktop IDE experience on macOS, Windows, and Linux from one maintainable codebase. macOS is an important target, but the product must not become platform-specific.

## Decision

Toren will be implemented primarily in C#/.NET using Avalonia UI for the desktop application. AvaloniaEdit is the initial editor-component direction.

Platform-specific code is allowed behind explicit abstractions where native behavior requires it, but product architecture and feature design must treat all supported operating systems as first-class targets.

## Consequences

- Core UI and product behavior can share one codebase.
- Native integration points such as terminals, filesystem behavior, signing, packaging, and process management need platform adapters.
- Platform-specific assumptions must not leak into core domain/application services.
