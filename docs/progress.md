# Toren IDE — MVP 1.0 progress

This file is the repository source of truth for implementation status. Update it in the same pull request that materially changes a feature state.

Status legend: **Done** = implemented and validated in CI; **Partial** = useful foundation exists but the MVP capability is incomplete; **Planned** = accepted MVP scope with no complete implementation yet; **Post-1.0** = intentionally outside MVP 1.0.

## Current quality gate

Before feature implementation continues, Toren is locking in the OSS maintainability and UI/UX baseline:

- **Done** — cross-platform Avalonia shell and shared design tokens;
- **Done** — macOS application identity and Toren-specific About dialog/menu;
- **Done** — explicit `Result<T>` semantics for expected operational failures;
- **Done** — feature-oriented source layout (`Contracts`, `Models`, `Services`, `Adapters`, etc. when meaningful);
- **Done** — strict analyzers, warnings-as-errors, NUnit tests, Windows/macOS/Linux CI;
- **Done** — standard `.sln`, `.slnx`, `.csproj` interoperability remains an invariant;
- **Partial** — keyboard/accessibility polish will continue as real controls replace placeholders.

## MVP 1.0 feature matrix

| Area | Status | Current state / next acceptance point |
| --- | --- | --- |
| Cross-platform app foundation | Done | Builds/tests on macOS, Windows, Linux |
| Toren visual language / shell | Done | Design tokens, IDE regions, honest empty states, About dialog |
| Workspace classification | Done | Folder / `.sln` / `.slnx` / `.csproj` detection |
| Workspace/project model | Planned | Load solution/project graph and populate Explorer |
| Recent workspaces/session restore | Planned | Persist IDE-only state without affecting builds |
| AvaloniaEdit integration | Planned | Real editable documents and tabs |
| C# language intelligence | Planned | Roslyn-backed completion, diagnostics, navigation, rename |
| Search / Go to File / symbols | Planned | Workspace-wide navigation |
| .NET SDK discovery | Partial | Installed SDK discovery exists; `global.json` resolution/doctor still pending |
| Restore / build / clean / run | Planned | Structured commands, cancellation, output |
| Problems panel | Planned | Compiler/analyzer/MSBuild/NuGet diagnostics |
| Test Explorer | Planned | NUnit/xUnit/MSTest/Microsoft Testing Platform |
| Debugger | Planned | DAP-style boundary, breakpoints, stepping, watches, stack |
| Integrated terminal | Planned | Native shell, multiple sessions, process lifecycle |
| Git | Planned | Status/diff/stage/commit/branch/pull/push/merge/stash/conflicts |
| NuGet | Planned | Search/install/update/remove, CPM, feeds |
| ASP.NET Core run profiles | Planned | `launchSettings.json`, envs, URLs, browser/OpenAPI actions |
| HTTP client | Planned | `.http` files, requests, environments, response viewer |
| EF Core tooling | Planned | Migrations add/remove/list/update database |
| Docker Compose workflows | Planned | Up/down/build/logs; Docker remains optional |
| Environment Doctor | Planned | SDK/Git/Docker/HTTPS checks and actionable remediation |
| Settings / keybindings | Planned | Searchable settings, theme/editor/tool configuration |
| Packaging and updates | Planned | Signed/notarized macOS, Windows/Linux packaging, stable/preview channels |
| Database explorer | Post-1.0 | PostgreSQL/SQL Server/SQLite browsing and SQL editor |
| General extension marketplace | Post-1.0 | Architecture may be extensible, marketplace is not MVP scope |
| Mandatory account/cloud | Post-1.0 | Explicit non-goal; Toren remains account-free/local-first |

## Delivery slices

- **M0 — Done:** foundation, standard formats, cross-platform CI.
- **Q0 — Done:** maintainability, Result pattern, source layout, UI/UX baseline, Toren About/identity.
- **M1 — Next:** workspace and project system.
- **M2:** editor and C# language intelligence.
- **M3:** build, run, diagnostics.
- **M4:** Test Explorer.
- **M5:** debugging.
- **M6:** terminal, Git, NuGet.
- **M7:** ASP.NET Core productivity and HTTP client.
- **M8:** EF Core and Docker workflows.
- **M9:** Environment Doctor, settings, packaging, release readiness.

See `docs/product-specification.md` for accepted product scope and `docs/roadmap.md` for milestone definitions.
