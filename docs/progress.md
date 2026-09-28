# Toren IDE — MVP 1.0 progress

This file is the repository source of truth for implementation status. Update it in the same pull request that materially changes a feature state.

Status legend: **Done** = implemented and validated in CI; **Partial** = useful foundation exists but the MVP capability is incomplete; **Planned** = accepted MVP scope with no complete implementation yet; **Post-1.0** = intentionally outside MVP 1.0.

## Current quality gate

The OSS maintainability and UI/UX baseline is in place. Cross-platform visual review remains open while product work proceeds:

- **Done** — cross-platform Avalonia shell and shared design tokens;
- **Done** — Toren-specific application identity and About experience;
- **Done** — custom Toren window chrome/title bars for the main shell and application dialogs;
- **Done** — activity rail disabled/planned states render without native-theme visual artifacts;
- **Done** — explicit `Result<T>` semantics for expected operational failures;
- **Done** — feature-oriented source layout (`Contracts`, `Models`, `Services`, `Adapters`, etc. when meaningful);
- **Done** — workspace tree orchestration depends on solution/project-reference contracts; `dotnet`/MSBuild process details live behind adapters and workspace models remain data-only;
- **Done** — evaluated project metadata is isolated behind `IProjectMetadataProvider`; MSBuild-specific evaluation stays in an adapter instead of leaking into Explorer or models;
- **Done** — evaluated folder/solution/project graph composition is isolated behind `IWorkspaceProjectGraphService`; graph models remain data-only and project-reference edges carry resolved paths;
- **Done** — plain-folder project discovery is isolated behind `IFolderProjectProvider` and skips generated/IDE directories plus reparse points;
- **Done** — workspace SDK resolution is isolated behind `IDotNetSdkResolver` and uses the workspace directory so standard `global.json` selection rules stay authoritative;
- **Done** — workspace file discovery and ranked quick-open search are isolated behind `IWorkspaceFileProvider` and `IWorkspaceFileSearchService`; generated/IDE directories stay out of the file index;
- **Done** — workspace text search is isolated behind `IWorkspaceTextSearchService`; file discovery and text I/O stay behind existing boundaries, open-buffer overrides preserve unsaved editor content, and search presentation/navigation stay outside the main window view model;
- **Done** — text-file I/O is isolated behind `ITextDocumentStore`; document models are data-only and UTF-8/UTF-16 encoding is preserved on save;
- **Done** — document-tab persistence is isolated behind `IDocumentSessionStore`; session models are data-only and session-file I/O stays in an adapter;
- **Done** — C# language boundaries are isolated behind syntax, compiler-diagnostic, semantic, completion, formatting, rename, symbol-index and symbol-search contracts; Roslyn remains inside `Toren.Language` and the app consumes data-only diagnostic/symbol/completion/rename/context models instead of Roslyn types;
- **Done** — Roslyn compilation/reference setup is centralized in the language layer, and Roslyn Workspaces is isolated there for symbol-safe cross-file rename rather than leaking workspace APIs into the shell;
- **Done** — project-graph scoped C# source context is composed behind `ICSharpSemanticContextProvider`; the provider consumes data-only source snapshots rather than presentation ViewModels, and cross-file source locations remain data-only;
- **Done** — live document analysis scheduling is isolated behind `IDocumentDiagnosticsCoordinator`; debounce/cancellation and syntax fallback stay out of `MainWindowViewModel`, while Problems presentation state is isolated in dedicated view models;
- **Done** — editor find/replace/go-to-line, C# definition navigation, hover quick info, completion, format-document, F2 rename and project symbol navigation live in separate interaction controllers instead of being folded into the main window view model;
- **Done** — rename result application is isolated from Roslyn: open documents become normal dirty editor buffers, closed files preserve their detected encoding, and failed multi-file saves attempt rollback before surfacing the storage error;
- **Done** — editor syntax presentation uses TextMate grammars/themes instead of the basic built-in highlighting palette; dark/light editor colors switch together with the shell;
- **Done** — strict analyzers, warnings-as-errors, NUnit tests, Windows/macOS/Linux CI;
- **Done** — standard `.sln`, `.slnx`, `.csproj` interoperability remains an invariant;
- **Done in Q0, pending cross-platform visual review** — consistent Toren vector activity and aligned action icons, compact shell surfaces and typography, a shell-colored Open menu in the title bar, interactive Welcome with balanced columns and honest planned states, closable document tab, tool tabs without theme underlines, and a simplified About window with one close control.
- **Partial** — native title-bar behavior, light/dark visual parity, focus and contrast, and final spacing still need hands-on visual review on macOS, Windows, and Linux before the Q0 quality gate is closed.

## MVP 1.0 feature matrix

| Area | Status | Current state / next acceptance point |
| --- | --- | --- |
| Cross-platform app foundation | Done | Builds/tests on macOS, Windows, Linux |
| Toren visual language / shell | Partial | Dark/light shell palettes, refined footer theme toggle, Toren vector icons, VS-style command bar shell, interactive Welcome, closable tabs, Explorer and tool-window polish are in place; cross-platform visual review and remaining surfaces still need validation |
| Workspace classification | Done | Folder / `.sln` / `.slnx` / `.csproj` detection |
| Workspace/project model | Done | Lazy Explorer, folder/solution/direct-project discovery, concise project labels, evaluated references/metadata, resolved project-reference edges, and on-demand data-only project graph are implemented behind clean provider/service boundaries |
| Recent workspaces/session restore | Done | Recent workspaces and clean open document tabs persist in OS application data; the last workspace, tab order, and active document are restored on startup |
| AvaloniaEdit integration | Partial | Explorer files open into real tabs backed by AvaloniaEdit; dirty state, active-tab switching, encoding-safe load/save, close protection, Ctrl/Cmd+S, themed in-file find/replace, go-to-line, semantic quick info, first completion popup support, Shift+Alt+F format document, F2 rename symbol, TextMate grammar highlighting with Dark+/Light+ themes, improved line-number gutter/current-line presentation, and document-session restoration are in place; richer editor behavior remains |
| C# language intelligence | Partial | Roslyn-backed live diagnostics use the active project plus transitively referenced project source when semantic context is available, with syntax-only fallback outside project context. A separate semantic boundary provides F12 cross-file go-to-definition, file-aware Alt+Left navigation back, delayed hover quick info, initial context-aware completion for visible symbols/C# keywords/member access, and Roslyn Workspaces-backed F2 rename across source references. A source-declaration symbol index powers project symbol navigation, and an initial Roslyn syntax formatter is exposed through Format Document. Package/framework project semantics, analyzer diagnostics, richer completion ranking/details and code actions remain |
| Search / Go to File / symbols | Partial | In-file find/replace supports case matching, next/previous/wrap navigation and replace-one/all; Ctrl/Cmd+G provides go-to-line. Ctrl/Cmd+P opens a ranked workspace file picker, Ctrl/Cmd+T opens a ranked C# symbol picker over the active project and transitively referenced project source, and Ctrl/Cmd+Shift+F searches text across searchable workspace files with case matching, unsaved-buffer overrides and result-to-source navigation. Broader solution-wide symbol scope and richer search filtering remain |
| .NET SDK discovery | Partial | Installed SDK discovery and workspace-specific `dotnet --version` resolution are in place; running in the workspace directory honors normal `global.json` selection; missing-SDK doctor/remediation remains |
| Restore / build / clean / run | Planned | VS-style toolbar surface is present with honest disabled configuration/startup/run placeholders; structured commands, cancellation, output, and execution arrive in M3 |
| Problems panel | Partial | Live active-document C# compiler diagnostics are listed with severity/code/file/location and double-click navigation; project-context source resolves across transitively referenced source projects, with syntax fallback when no semantic context is available. Analyzer, MSBuild, NuGet, workspace aggregation, filtering, and persistence remain |
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
| Settings / keybindings | Planned | Runtime dark/light switching exists in the shell; persisted theme preference, searchable settings, editor/tool configuration, and keybinding management remain |
| Packaging and updates | Planned | Signed/notarized macOS, Windows/Linux packaging, stable/preview channels |
| Database explorer | Post-1.0 | PostgreSQL/SQL Server/SQLite browsing and SQL editor |
| General extension marketplace | Post-1.0 | Architecture may be extensible, marketplace is not MVP scope |
| Mandatory account/cloud | Post-1.0 | Explicit non-goal; Toren remains account-free/local-first |

## Delivery slices

- **M0 — Done:** foundation, standard formats, cross-platform CI.
- **Q0 — UI round delivered:** owner visual review on macOS is complete; dark/light editor/shell palette, refined theme glyph, and command-bar follow-up are implemented, while cross-platform visual review remains open.
- **M1 — Done:** folder/solution/project opening, lazy Explorer, recent-workspace restore, clean discovery/evaluation provider boundaries, evaluated project metadata/references, resolved reference edges, and on-demand workspace project graph are in place.
- **M2 — Active:** AvaloniaEdit, document/tab lifecycle, TextMate syntax themes, improved editor gutter/current-line presentation, unsaved-change protection, document-session restoration, live debounced project-aware C# compiler diagnostics with syntax fallback, a real Problems list, problem-to-source navigation, in-file find/replace, go-to-line, ranked Go to File, workspace Find in Files with unsaved-buffer awareness, graph-scoped cross-file C# definition navigation, hover quick info, initial semantic completion, format document, F2 symbol rename, and ranked project symbol navigation are in place; next are analyzer diagnostics, code actions, broader solution symbol scope, richer search filtering, and completion/formatter polish.
- **M3:** build, run, diagnostics; the command-bar UI shell is already present but execution controls intentionally remain disabled until this slice.
- **M4:** Test Explorer.
- **M5:** debugging.
- **M6:** terminal, Git, NuGet.
- **M7:** ASP.NET Core productivity and HTTP client.
- **M8:** EF Core and Docker workflows.
- **M9:** Environment Doctor, settings, packaging, release readiness.

See `docs/product-specification.md` for accepted product scope and `docs/roadmap.md` for milestone definitions.
