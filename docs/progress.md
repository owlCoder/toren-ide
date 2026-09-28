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
- **Done** — workspace text search is isolated behind `IWorkspaceTextSearchService`; data-only search options carry case/whole-word/regex/include/exclude filters, regular expressions use a bounded evaluation timeout, file discovery and text I/O stay behind existing boundaries, open-buffer overrides preserve unsaved editor content, and file-grouped search presentation with per-file match counts plus navigation stay outside the main window view model;
- **Done** — text-file I/O is isolated behind `ITextDocumentStore`; document models are data-only and UTF-8/UTF-16 encoding is preserved on save;
- **Done** — document-tab persistence is isolated behind `IDocumentSessionStore`; session models are data-only and session-file I/O stays in an adapter;
- **Done** — C# language boundaries are isolated behind syntax, document/workspace compiler-analyzer diagnostics, semantic, completion, formatting, rename, code-action, symbol-index and symbol-search contracts; Roslyn remains inside `Toren.Language` and the app consumes data-only diagnostic/symbol/completion/rename/code-action/context models instead of Roslyn types;
- **Done** — Roslyn compilation/reference setup is centralized in the language layer, project analyzer loading is isolated there, and Roslyn Workspaces remains isolated there for selection formatting and symbol-safe cross-file rename instead of leaking workspace APIs into the shell;
- **Done** — project-graph scoped, project-grouped workspace-analysis and workspace-wide C# source contexts are composed behind `ICSharpSemanticContextProvider`; the provider consumes data-only source snapshots rather than presentation ViewModels, preserves unsaved C# buffers, and project-scoped contexts carry MSBuild-evaluated analyzer paths without exposing Roslyn analyzer types;
- **Done** — live document and full-workspace analysis scheduling are isolated behind `IDocumentDiagnosticsCoordinator` and `IWorkspaceDiagnosticsCoordinator`; cancellation, project grouping and syntax fallback stay out of `MainWindowViewModel`, while Problems presentation/filter/per-file aggregation state is isolated in dedicated view models; severity-filter persistence is isolated behind `IProblemsViewStateStore` plus a controller and stores view preferences only, never diagnostic output; workspace/project/current-document scope resolution is isolated behind `IProblemsWorkspaceScopeService` plus a controller and reuses a shared project-ownership map rather than introducing MSBuild or file-system logic into `ProblemsViewModel`;
- **Done** — editor find/replace/go-to-line, C# definition navigation, hover quick info, completion, document/selection formatting, F2 rename, quick fixes and workspace symbol navigation live in separate interaction controllers instead of being folded into the main window view model;
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
| Workspace/project model | Done | Lazy Explorer, folder/solution/direct-project discovery, concise project labels, evaluated references/metadata, resolved reference edges, and on-demand data-only project graph are implemented behind clean provider/service boundaries |
| Recent workspaces/session restore | Done | Recent workspaces and clean open document tabs persist in OS application data; the last workspace, tab order, and active document are restored on startup |
| AvaloniaEdit integration | Partial | Explorer files open into real tabs backed by AvaloniaEdit; dirty state, active-tab switching, encoding-safe load/save, close protection, Ctrl/Cmd+S, themed in-file find/replace, go-to-line, semantic quick info, completion popup support with typed-prefix ranking and overload details, Shift+Alt+F or Ctrl/Cmd+K,D format document, Ctrl/Cmd+K,F format selection, F2 rename symbol, Ctrl/Cmd+. quick-fix surface, TextMate grammar highlighting with Dark+/Light+ themes, improved line-number gutter/current-line presentation, and document-session restoration are in place; richer editor behavior remains |
| C# language intelligence | Partial | Roslyn-backed live diagnostics use the active project plus transitively referenced project source and MSBuild-evaluated `@(Analyzer)` assemblies when semantic context is available, with syntax-only fallback outside project context. Full workspace scanning now groups source by owning project, compiles each project context once for its own target files, and uses syntax fallback for loose C# files rather than mixing unrelated projects into one compilation. A separate semantic boundary provides F12 cross-file go-to-definition, file-aware Alt+Left navigation back, delayed hover quick info, context-aware completion for visible symbols/C# keywords/member access with typed-prefix ranking and summarized overload details, Roslyn-backed document/selection formatting, and Roslyn Workspaces-backed F2 rename across source references. The code-action pipeline provides data-only insertion quick fixes for `CS1002` missing semicolon, `CS1026` missing closing parenthesis, and `CS1513` missing closing brace, plus a semantic `CS0246` Add using action that resolves accessible framework/workspace type candidates without leaking Roslyn types into the app layer and preserves the document's line-ending style. A source-declaration symbol index powers workspace-wide symbol navigation. Package/framework project semantics and broader semantic/refactoring code-action coverage remain |
| Search / Go to File / symbols | Partial | In-file find/replace supports case matching, next/previous/wrap navigation and replace-one/all; Ctrl/Cmd+G provides go-to-line. Ctrl/Cmd+P opens a ranked workspace file picker, Ctrl/Cmd+T opens a ranked C# symbol picker across searchable C# source in the opened workspace, and Ctrl/Cmd+Shift+F searches text across searchable workspace files with case matching, whole-word mode, bounded regular expressions, wildcard include/exclude path filters, unsaved-buffer overrides and result-to-source navigation. Results are grouped by file with per-file match counts while preserving result order and keyboard navigation across matches. Additional search polish remains |
| .NET SDK discovery | Partial | Installed SDK discovery and workspace-specific `dotnet --version` resolution are in place; running in the workspace directory honors normal `global.json` selection; missing-SDK doctor/remediation remains |
| Restore / build / clean / run | Planned | VS-style toolbar surface is present with honest disabled configuration/startup/run placeholders; structured commands, cancellation, output, and execution arrive in M3 |
| Problems panel | Partial | C# compiler and project-analyzer diagnostics are listed with severity/code/file/location, severity toggles with live counts, filtered-empty state and double-click navigation. Opening/restoring a workspace scans unopened C# files as well as open buffers, groups project analysis by the owning project and its reachable source references, keeps loose files on syntax fallback, and then lets live document diagnostics replace only the edited file. Changing workspace resets the aggregate; closing a clean document no longer discards its workspace diagnostic entry. Severity-filter preferences persist in OS application data through a dedicated store/controller boundary, while diagnostic entries are recomputed and are never persisted across sessions. Workspace/project/current-document scope filtering is available and follows the active document's owning project. MSBuild/NuGet diagnostics remain |
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
- **M2 — Active:** AvaloniaEdit, document/tab lifecycle, TextMate syntax themes, improved editor gutter/current-line presentation, unsaved-change protection, document-session restoration, live debounced project-aware C# compiler/analyzer diagnostics plus full unopened-file workspace scanning with loose-file syntax fallback, a real Problems list with severity filters, persistent filter preferences, workspace aggregation and workspace/project/current-document scopes, problem-to-source navigation, in-file find/replace, go-to-line, ranked Go to File, workspace Find in Files with case matching, whole-word/regex modes, wildcard include/exclude path filters, unsaved-buffer awareness and file-grouped results, graph-scoped cross-file C# definition navigation, hover quick info, prefix-ranked semantic completion with overload summaries, document/selection formatting with VS-style chords, F2 symbol rename, insertion quick fixes for missing `;`, `)` and `}`, semantic `CS0246` Add using fixes, and ranked workspace-wide symbol navigation are in place; next are broader semantic/refactoring code actions, additional completion polish and MSBuild/NuGet problem integration.
- **M3:** build, run, diagnostics; the command-bar UI shell is already present but execution controls intentionally remain disabled until this slice.
- **M4:** Test Explorer.
- **M5:** debugging.
- **M6:** terminal, Git, NuGet.
- **M7:** ASP.NET Core productivity and HTTP client.
- **M8:** EF Core and Docker workflows.
- **M9:** Environment Doctor, settings, packaging, release readiness.

See `docs/product-specification.md` for accepted product scope and `docs/roadmap.md` for milestone definitions.
