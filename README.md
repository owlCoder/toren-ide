# Toren IDE

Toren IDE is a cross-platform, local-first development environment for modern .NET and ASP.NET Core development.

## Project status

Toren IDE is in the early design and bootstrap phase. The current focus is to define a stable product scope, architecture, and contributor workflow before implementation expands.

## Product principles

- **Cross-platform:** macOS, Windows, and Linux are first-class targets.
- **Local-first:** no account, mandatory cloud service, or mandatory telemetry is required.
- **Standard .NET projects:** Toren works with existing `.sln`, `.slnx`, and `.csproj` projects and does not introduce a proprietary project format.
- **Interoperable:** projects opened in Toren must remain usable in Visual Studio, JetBrains Rider, VS Code, and the `dotnet` CLI.
- **Toolchain-oriented:** Toren orchestrates established .NET tooling instead of replacing compilers, MSBuild, Git, NuGet, or test platforms.
- **Open source:** the project is developed in public under the Apache License 2.0.

## Planned core capabilities

The product roadmap includes a C# editor with language intelligence, solution/project navigation, .NET SDK discovery, restore/build/run/publish, debugging, test exploration, an integrated terminal, Git, NuGet, ASP.NET Core launch profiles, HTTP files, EF Core tooling, Docker Compose integration, and environment diagnostics.

See [`docs/product-specification.md`](docs/product-specification.md) for the working product definition and [`docs/decisions/`](docs/decisions/) for architectural decisions.

## Technology direction

The current implementation direction is:

- C# / .NET
- Avalonia UI
- AvaloniaEdit
- standard .NET CLI / MSBuild project tooling
- language-service boundary compatible with LSP concepts
- debugger boundary compatible with DAP concepts

These choices are documented as ADRs and may evolve through normal project review.

## Contributing

Toren IDE is not yet accepting large feature implementations while the initial architecture is being established. Small documentation, bootstrap, and design contributions are welcome.

Please read [`CONTRIBUTING.md`](CONTRIBUTING.md) before opening a pull request.

## License

Licensed under the [Apache License 2.0](LICENSE).
