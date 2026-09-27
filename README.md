# Toren IDE

[![CI](https://github.com/owlCoder/toren-ide/actions/workflows/ci.yml/badge.svg)](https://github.com/owlCoder/toren-ide/actions/workflows/ci.yml)

Toren IDE is a cross-platform, local-first development environment for modern .NET and ASP.NET Core development.

## Project status

**MVP 1.0 is under active implementation.** Development is organized as small vertical slices that keep the repository buildable and testable across macOS, Windows, and Linux.

The foundation and quality/UI baseline are in place. The next product slice is the real workspace/project model and Explorer. See [`docs/progress.md`](docs/progress.md) for the live feature matrix and [`docs/roadmap.md`](docs/roadmap.md) for delivery slices.

## Product principles

- **Cross-platform:** macOS, Windows, and Linux are first-class targets.
- **Local-first:** no account, mandatory cloud service, or mandatory telemetry is required.
- **Standard .NET projects:** Toren works with existing `.sln`, `.slnx`, and `.csproj` projects and does not introduce a proprietary project format.
- **Interoperable:** projects opened in Toren must remain usable in Visual Studio, JetBrains Rider, VS Code, and the `dotnet` CLI.
- **Toolchain-oriented:** Toren orchestrates established .NET tooling instead of replacing compilers, MSBuild, Git, NuGet, or test platforms.
- **Maintainable OSS:** clean dependency direction, explicit failure semantics, strict CI, and contributor-readable code are product requirements.
- **Open source:** the project is developed in public under the Apache License 2.0.

## Technology

- C# 14 / .NET 10
- Avalonia UI
- AvaloniaEdit (editor integration is an MVP slice)
- standard .NET CLI / MSBuild tooling
- Roslyn-backed language services behind an LSP-like boundary
- debugging behind a DAP-compatible boundary
- system Git and standard NuGet project mechanisms

## Repository layout

```text
src/
  Toren.App          # Avalonia composition root and desktop UI
  Toren.Core         # dependency-free contracts, results, and primitives
  Toren.Platform     # operating-system/process adapters
  Toren.Workspaces   # workspace/project concepts
  Toren.DotNet       # .NET SDK and toolchain integration

tests/
  Toren.UnitTests
```

Within feature modules, source roles are separated when meaningful (`Contracts`, `Models`, `Services`, `Adapters`, `Parsing`, `Errors`). Empty layers are not created for symmetry.

Architecture decisions live in [`docs/decisions/`](docs/decisions/) and contributor maintainability rules in [`docs/engineering/maintainability.md`](docs/engineering/maintainability.md).

## Build from source

Prerequisites:

- a supported .NET 10 SDK;
- Git;
- the native prerequisites required by Avalonia for your operating system.

```bash
git clone https://github.com/owlCoder/toren-ide.git
cd toren-ide
dotnet restore Toren.slnx
dotnet build Toren.slnx
dotnet run --project src/Toren.App/Toren.App.csproj
```

`global.json` permits compatible .NET 10 feature bands so contributors can use a newer installed .NET 10 SDK without changing repository files.

## Product documentation

- [`docs/product-specification.md`](docs/product-specification.md) — accepted product scope;
- [`docs/progress.md`](docs/progress.md) — live implementation status;
- [`docs/roadmap.md`](docs/roadmap.md) — MVP delivery slices;
- [`docs/design/ui-principles.md`](docs/design/ui-principles.md) — Toren UI/UX principles;
- [`docs/engineering/maintainability.md`](docs/engineering/maintainability.md) — code-quality baseline.

## Contributing

Please read [`CONTRIBUTING.md`](CONTRIBUTING.md) before opening a pull request. New code should preserve module boundaries, use standard .NET formats/toolchains, include appropriate tests, update progress when status changes, and keep `main` buildable on all supported platforms.

## License

Licensed under the [Apache License 2.0](LICENSE).
