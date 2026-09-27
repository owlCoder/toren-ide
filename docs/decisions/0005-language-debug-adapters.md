# ADR-0005: Adapter boundaries for language and debugging services

Status: **Accepted**

## Context

Toren needs rich C# language intelligence and a practical debugger without coupling the entire UI directly to one concrete language-server or debugger implementation.

## Decision

Toren will define internal service boundaries for language intelligence and debugging that are compatible with LSP- and DAP-style capability models.

The initial C# language implementation is expected to be Roslyn-backed. Debugging will use a replaceable backend capable of exposing standard debugger operations through a stable Toren abstraction.

The UI should depend on Toren abstractions rather than directly on a specific external server or debug engine.

## Consequences

- Backend implementations can evolve without rewriting editor/debug UI.
- Capability negotiation and lifecycle management become explicit concerns.
- Toren can potentially support additional languages/debuggers later without redesigning the host application.
- The project must avoid leaking backend-specific object models into core UI contracts.
