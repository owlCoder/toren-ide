# Maintainability baseline

Toren IDE is an open-source product expected to be maintained by contributors with different experience levels. Maintainability is therefore a product requirement, not a cleanup task.

## Principles

- Prefer clear code over clever code.
- Keep dependencies pointing inward toward small abstractions and domain/application concepts.
- Keep Avalonia, OS, process, Git, NuGet, MSBuild, debugger, and other infrastructure details at the edges.
- Views render state and forward UI events; product rules do not live in views or code-behind.
- View models coordinate presentation state, not toolchain implementation details.
- External processes are accessed behind interfaces and return explicit result models.
- New modules must represent real feature boundaries; do not create empty layers for architectural symmetry.
- Favor constructor injection and immutable dependencies.
- Prefer small cohesive types with one reason to change.
- Public contracts must have stable naming and predictable error semantics.
- Cancellation must flow through asynchronous I/O and external-process operations.
- Expected recoverable failures use `Result<T>`; exceptions are reserved for invalid programmer usage, broken invariants, cancellation, and unexpected faults.
- All platform-independent behavior requires automated tests.
- Bug fixes should add a regression test when practical.
- Warnings and analyzer findings remain errors in CI unless a documented decision explains an exception.

## Dependency rule

The desktop UI may depend on product/application abstractions. Product logic must not depend on Avalonia or operating-system UI APIs.

A typical dependency direction is:

```text
Toren.App / UI
      |
      v
feature/application contracts
      |
      v
core models and policies

infrastructure adapters ---> feature/application contracts
```

Infrastructure implements contracts; it does not define business behavior for callers.

## Source layout

Feature folders separate roles when doing so makes ownership clearer:

```text
Feature/
  Contracts/
  Models/
  Services/
  Adapters/
  Parsing/
  Errors/
```

Do not create empty folders or projects merely to match this diagram. A small feature may start with fewer folders and grow when a real boundary appears.

## Failure semantics

Use `Result<T>` when a caller is expected to recover from or present a failure, such as a missing executable or a non-zero tool exit code. Use a conventional `Try...` method when absence is simple and local. Throw for programmer contract violations and truly exceptional states. Do not catch an exception merely to convert every unknown fault into a generic failure.

Error codes should be stable and namespaced by capability, for example `dotnet.sdk.discovery.failed`.

## UI code

Code-behind is limited to operations that inherently require a view/window instance, such as native file pickers, window lifecycle, drag/drop, focus, native application menus, and other platform UI integration. Such code should immediately delegate results to a view model or application service.

Reusable controls must not reach directly into Git, `dotnet`, file-system processes, or other infrastructure.

## Reviews

Every non-trivial pull request should answer:

1. What boundary owns this behavior?
2. Can the behavior be tested without launching the desktop UI?
3. Does this introduce unnecessary coupling to Avalonia, the OS, or a specific tool implementation?
4. Is the naming understandable to a contributor seeing this code for the first time?
5. Is there a smaller, simpler design that preserves the same extensibility?
6. Are success, failure, and cancellation paths covered where relevant?
7. Does the change preserve standard .NET project interoperability?
8. If feature status changed, was `docs/progress.md` updated?

## Documentation expectations

Architecture-changing decisions require an ADR. New feature modules should have a short README once their public surface is large enough that ownership is not obvious from the code itself. Implementation status is tracked in `docs/progress.md` and should be updated in the same pull request that materially changes feature status.

The goal is not maximum abstraction. The goal is a codebase in which a new contributor can locate behavior, understand dependencies, run tests, and make a safe change without knowing the entire IDE.
