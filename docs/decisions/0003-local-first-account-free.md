# ADR-0003: Local-first and account-free operation

Status: **Accepted**

## Context

Toren is intended to be usable in classrooms, personal projects, and professional environments without requiring a vendor account or cloud dependency.

## Decision

Core workflows — opening projects, editing, building, running, debugging, testing, and local Git operations — must work without a Toren account and without a Toren cloud backend.

Mandatory telemetry is not allowed. If telemetry is added later, it must be transparent and opt-in.

Network access may still be required for external services such as NuGet feeds, remote Git operations, online documentation, or update checks.

## Consequences

- The application remains useful in offline or restricted environments.
- Authentication is not part of the core startup path.
- Cloud integrations must remain optional adapters rather than product prerequisites.
