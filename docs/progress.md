# Toren IDE — MVP 1.0 progress

This file is the repository source of truth for implementation status. Update it in the same pull request that materially changes a feature state.

Status legend: **Done** = implemented and validated in CI; **Partial** = useful foundation exists but the MVP capability is incomplete; **Planned** = accepted MVP scope with no complete implementation yet; **Post-1.0** = intentionally outside MVP 1.0.

## Current quality gate

The OSS maintainability and UI/UX baseline is in place. Cross-platform visual review remains open while M1 workspace work proceeds:

- **Done** — cross-platform Avalonia shell and shared design tokens;
- **Done** — Toren-specific application identity and About experience;
- **Done** — custom Toren window chrome/title bars for the main shell and application dialogs;
- **Done** — activity rail disabled/planned states render without native-theme visual artifacts;
- **Done** — explicit `Result<T>` semantics for expected operational failures;
- **Done** — feature-oriented source layout (`Contracts`, `Models`, `Services`, `Adapters`, etc. when meaningful);
- **Done** — workspace tree orchestration depends on solution/project-reference contracts; `dotnet`/MSBuild process details live behind adapters and workspace models remain data-only;
- **Done** — workspace SDK resolution is isolated behind `IDotNetSdkResolver` and uses the workspace directory so standard `global.json` selection rules stay authoritative;
- **Done** — strict analyzers, warnings-as-errors, NUnit tests, Windows/macOS/Linux CI;
- **Done** — standard `.sln`, `.slnx`, `.csproj` interoperability remains an invariant;
- **Done in Q0, pending cross-platform visual review** — consistent Toren vector activity and aligned action icons, compact shell surfaces and typography, a shell-colored Open menu in the title bar, interactive Welcome with balanced columns and honest planned states, closable document tab, tool tabs without theme underlines, and a simplified About window with one close control.
- **Partial** — native title-bar behavior, focus and contrast, and final spacing still need hands-on visual review on macOS, Windows, and Linux before the Q0 quality gate is closed.

## MVP 1.0 feature matrix

| Area | Status | Current state / next acceptance point |
| --- | --- | --- |
| Cross-platform app foundation | Done | Builds/tests on macOS, Windows, Linux |
| Toren visual language / shell | Partial | Shared tokens, Toren vector icons, interactive Welcome, closable tab, Explorer and tool-window polish; cross-platform visual review remains |
| Workspace classification | Done | Folder / `.sln` / `.slnx` / `.csproj` detection |
| Workspace/project model | Partial | Lazy Explorer shows physical files and solution projects through a dedicated solution provider; project labels use shortest unique names; reference expansion uses evaluated MSBuild `ProjectReference`, `PackageReference`, and `FrameworkReference` items behind a dedicated adapter; broader evaluated graph metadata remains |
| Recent workspaces/session restore | Partial | Recent workspaces persist in OS application data and the last available workspace is restored; document session state remains |
| AvaloniaEdit integration | Planned | Real editable documents and tabs |
| C# language intelligence | Planned | Roslyn-backed completion, diagnostics, navigation, rename |
| Search / Go to File / symbols | Planned | Workspace-wide navigation |
| .NET SDK discovery | Partial | Installed SDK discovery and workspace-specific `dotnet --version` resolution are in place; running in the workspace directory honors normal `global.json` selection; missing-SDK doctor/remediation remains |
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
- **Q0 — UI round delivered:** owner visual review on macOS is complete; cross-platform visual review remains open.
- **M1 — Active:** workspace and project system; lazy Explorer, workspace history, concise project labels, clean provider boundaries, evaluated reference expansion, and workspace-aware SDK resolution are in place, with broader evaluated graph metadata and remaining workflow details next.
- **M2:** editor and C# language intelligence.
- **M3:** build, run, diagnostics.
- **M4:** Test Explorer.
- **M5:** debugging.
- **M6:** terminal, Git, NuGet.
- **M7:** ASP.NET Core productivity and HTTP client.
- **M8:** EF Core and Docker workflows.
- **M9:** Environment Doctor, settings, packaging, release readiness.

See `docs/product-specification.md` for accepted product scope and `docs/roadmap.md` for milestone definitions.
