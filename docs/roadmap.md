# Toren IDE MVP 1.0 Roadmap

Toren is developed in vertical slices. Every merged slice should keep `main` buildable on macOS, Windows, and Linux.

## M0 — Foundation

- solution and central build configuration;
- .NET 10 SDK policy;
- Avalonia desktop shell;
- process-runner abstraction;
- .NET SDK discovery;
- workspace path classification;
- unit tests;
- cross-platform CI;
- dependency update automation.

## M1 — Workspace and project system

- open folder, `.sln`, `.slnx`, and `.csproj`;
- solution/project explorer;
- lazy file-system tree;
- project/reference model;
- recent workspaces and session restore.

## M2 — Editor and C# language intelligence

- AvaloniaEdit integration;
- tabs and document lifecycle;
- syntax/semantic highlighting;
- Roslyn-backed language-service boundary;
- completion, hover, diagnostics, navigation, rename, formatting, and code actions;
- search, replace, go-to-file, go-to-line, and symbol navigation.

## M3 — Build, run, and diagnostics

- restore/build/rebuild/clean/run/publish;
- cancellation and structured output;
- Problems and Output panels;
- SDK/TFM discovery and `global.json` behavior;
- launch profiles and ASP.NET Core run experience.

## M4 — Testing

- test discovery;
- NUnit, xUnit, MSTest, and Microsoft Testing Platform;
- Test Explorer;
- run/debug selected tests and rerun failures;
- failure and stack-trace navigation.

## M5 — Debugging

- DAP-compatible debug boundary;
- breakpoints and conditional breakpoints;
- continue/pause/stop/restart;
- step into/over/out and run to cursor;
- locals, watch, call stack, breakpoints, and debug console.

## M6 — Developer workflow

- integrated terminal;
- Git status/diff/stage/commit/branch/pull/push/fetch/merge/stash;
- conflict UI;
- NuGet search/install/update/uninstall;
- Central Package Management and custom feeds.

## M7 — ASP.NET Core productivity

- HTTP files and request runner;
- environment variables and response viewer;
- User Secrets;
- OpenAPI/Swagger shortcuts;
- application URL detection and browser launch;
- HTTPS development-certificate diagnostics.

## M8 — Data and containers

- EF Core migration actions;
- Dockerfile and Compose support;
- compose build/up/down/logs;
- database explorer is post-1.0 unless the earlier MVP slices finish with sufficient stability margin.

## M9 — Release readiness

- settings and keyboard shortcuts;
- Environment Doctor;
- accessibility and performance pass;
- recovery/session persistence;
- packaging for macOS, Windows, and Linux;
- macOS signing/notarization path;
- stable update channel and release documentation.

## Definition of MVP 1.0

Toren 1.0 is reached when the core workflow is reliable on all three desktop platforms: open a standard .NET workspace, edit C#, restore/build/run, debug, test, use Git/NuGet, work with common ASP.NET Core workflows, and do so without an account or proprietary project format.
