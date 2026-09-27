# ADR-0009: Result pattern for expected failures

- Status: Accepted
- Date: 2026-09-27

## Context

An IDE routinely interacts with external processes, SDKs, files, Git, package feeds, debuggers, and local services. Many failures in these integrations are expected operational outcomes rather than exceptional programming faults. Using exceptions for every expected outcome makes control flow harder to follow, encourages broad catch blocks, and makes UI error handling inconsistent.

## Decision

Toren uses an explicit `Result<T>` model for recoverable, expected failures that callers are expected to handle.

Examples include:

- an executable is not installed or cannot be started;
- an external tool exits with a non-zero code;
- a requested optional capability is unavailable;
- an operation is rejected by a known precondition that can be presented to the user.

Exceptions remain appropriate for:

- invalid programmer arguments and broken API contracts;
- violated internal invariants;
- cancellation (`OperationCanceledException`);
- unexpected framework/runtime faults that the current boundary cannot handle meaningfully.

Errors use stable machine-readable codes plus user-readable messages. A layer may translate a lower-level error into a domain-specific error, but it should not silently discard useful context.

The result pattern is not mandatory for pure methods where a conventional `Try...` API or nullable value communicates absence more clearly.

## Consequences

- expected failure paths are visible in method signatures;
- presentation code can display recoverable failures without exception-driven control flow;
- infrastructure adapters can preserve cancellation and unexpected faults while returning known operational failures;
- contributors must avoid wrapping every method in `Result<T>` when it does not improve clarity.
