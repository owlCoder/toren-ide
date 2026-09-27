# Toren IDE

[![CI](https://github.com/owlCoder/toren-ide/actions/workflows/ci.yml/badge.svg)](https://github.com/owlCoder/toren-ide/actions/workflows/ci.yml)

Toren IDE is a cross-platform, local-first development environment for modern .NET and ASP.NET Core development.

## Project status

**MVP 1.0 is under active implementation.** Development is organized as small vertical slices that keep the repository buildable and testable across macOS, Windows, and Linux.

The current foundation establishes the desktop shell, clean module boundaries, .NET SDK discovery, workspace classification, tests, and cross-platform CI. See [`docs/roadmap.md`](docs/roadmap.md) for the planned MVP sequence.

## Product principles

- **Cross-platform:** macOS, Windows, and Linux are first-class targets.
- **Local-first:** no account, mandatory cloud service, or mandatory telemetry is required.
- **Standard .NET projects:** Toren works with existing `.sln`, `.slnx`, and `.csproj` projects and does not introduce a proprietary project format.
- **Interoperable:** projects opened in Toren must remain usable in Visual Studio, JetBrains Rider, VS Code, and the `dotnet` CLI.
- **Toolchain-oriented:** Toren orchestrates established .NET tooling instead of replacing compilers, MSBuild, Git, NuGet, or test platforms.
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
  Toren.Core         # dependency-free contracts and primitives
  Toren.Platform     # operating-system/process adapters
  Toren.Workspaces   # workspace/project concepts
  Toren.DotNet       # .NET SDK and toolchain integration

tests/
  Toren.UnitTests
```

The architecture is documented in [`docs/decisions/`](docs/decisions/). Toren adds modules when they represent real feature boundaries; it does not create empty projects for hypothetical future layers.

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

## Product specification

See [`docs/product-specification.md`](docs/product-specification.md) for the working product definition and [`docs/roadmap.md`](docs/roadmap.md) for MVP 1.0 delivery slices.

## Contributing

Please read [`CONTRIBUTING.md`](CONTRIBUTING.md) before opening a pull request. New code should preserve module boundaries, use standard .NET formats/toolchains, include appropriate tests, and keep `main` buildable on all supported platforms.

## License

Licensed under the [Apache License 2.0](LICENSE).
