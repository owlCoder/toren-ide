# Toren IDE — Product Specification

Status: **Draft / v0.1**

This document defines the current product direction for Toren IDE. It is intentionally focused on product scope and engineering constraints rather than implementation details.

## 1. Product definition

Toren IDE is a cross-platform, local-first integrated development environment focused on modern .NET and ASP.NET Core development.

Toren should provide the subset of a full desktop IDE that is most useful for day-to-day application development while remaining smaller, understandable, and open source.

### Primary goals

- First-class macOS, Windows, and Linux support.
- Strong C# and modern .NET development experience.
- Strong ASP.NET Core development experience.
- Standard .NET project compatibility without lock-in.
- Account-free and cloud-optional operation.
- Fast startup, predictable behavior, and a clean desktop UI.
- A codebase that can be maintained as a serious open-source product.

### Non-goals

Toren is not intended to reproduce every Visual Studio feature.

Initial non-goals include:

- proprietary project/build formats;
- a general extension marketplace in the first release;
- mandatory cloud services;
- mandatory accounts;
- built-in AI as a core dependency;
- Kubernetes administration tooling;
- visual UI designers;
- Azure-specific enterprise tooling;
- a replacement for database administration products;
- a replacement for Docker Desktop;
- implementing a new compiler, build system, package manager, Git engine, or test framework.

## 2. Product invariants

The following are product-level constraints.

1. A project opened or created in Toren remains a standard .NET project.
2. `.sln`, `.slnx`, and `.csproj` remain authoritative project formats.
3. Projects must remain usable in Visual Studio, JetBrains Rider, VS Code, and the `dotnet` CLI.
4. Toren must not require a Toren account to edit, build, run, debug, or test local projects.
5. Toren must not require a Toren cloud backend for normal local development.
6. Toren must use established toolchains where practical instead of reimplementing them.
7. IDE-only state must not become a build dependency.

## 3. Supported platforms

Target platforms:

- macOS Apple Silicon;
- macOS Intel where practical;
- Windows x64;
- Windows ARM64 where practical;
- Linux x64;
- Linux ARM64 where practical.

The UI and architecture are cross-platform from the beginning. macOS is an important first-class target because modern Visual Studio for Mac no longer exists, but Toren is not a macOS-only product.

## 4. Technology direction

Current direction:

- Language: C#
- Runtime: modern supported .NET SDK
- Desktop UI: Avalonia UI
- Text editor: AvaloniaEdit
- Architecture: modular desktop application
- .NET build/project integration: `dotnet` CLI / MSBuild
- Language intelligence: Roslyn-backed implementation behind an LSP-like internal boundary
- Debugging: debug-adapter boundary compatible with DAP concepts
- Version control: system Git
- Package management: NuGet / standard .NET project mechanisms

The application itself should not be unnecessarily tied to a single installed SDK version. Project SDK resolution must respect normal .NET SDK rules, including `global.json`.

## 5. Workspace and project system

Toren should support:

- opening a folder;
- `.sln` solutions;
- `.slnx` solutions;
- `.csproj` projects;
- multi-project solutions;
- recent projects/workspaces;
- workspace/session restoration;
- solution/project view;
- physical filesystem view;
- project references;
- package references;
- framework references;
- `global.json`;
- `Directory.Build.props`;
- `Directory.Build.targets`;
- `Directory.Packages.props`;
- `NuGet.Config`;
- `.editorconfig`;
- `launchSettings.json`;
- `appsettings*.json`.

Toren must not introduce a proprietary replacement for these formats.

## 6. Project creation

Project creation should use standard installed .NET templates through `dotnet new`.

Capabilities:

- discover installed templates;
- create solution;
- create project;
- add existing project;
- add/remove project references;
- expose user-installed `dotnet new` templates automatically.

## 7. C# editor

Core C# capabilities:

- syntax highlighting;
- semantic highlighting;
- completion / IntelliSense;
- parameter information;
- hover information and documentation;
- diagnostics and squiggles;
- quick fixes;
- code actions;
- Go to Definition;
- Go to Implementation;
- Find References;
- Rename Symbol;
- format document/selection;
- code folding;
- breadcrumbs;
- document outline;
- solution-wide symbol search.

General editor capabilities:

- tabs and pinned tabs;
- split editor;
- multiple cursors;
- column selection;
- find/replace;
- regular-expression search;
- search in files;
- Go to File;
- Go to Line;
- bracket matching;
- indent guides;
- optional whitespace rendering;
- configurable fonts;
- configurable keyboard shortcuts;
- optional autosave;
- restore open documents between sessions.

## 8. Supporting file formats

Toren is .NET-focused but must handle common files in real .NET repositories comfortably.

Planned support includes:

- C#;
- JSON;
- XML;
- YAML;
- Markdown;
- HTML;
- CSS;
- JavaScript;
- TypeScript;
- SQL;
- shell scripts;
- PowerShell;
- Dockerfile;
- Razor `.cshtml`;
- Blazor `.razor`.

Support does not imply that Toren aims to become a general-purpose IDE for every language.

## 9. .NET SDK and build tooling

Capabilities:

- detect installed .NET SDKs;
- honor `global.json`;
- detect target frameworks;
- detect missing SDKs;
- Restore;
- Build;
- Rebuild;
- Clean;
- Run;
- Publish;
- Debug/Release configurations;
- multi-targeting support;
- structured build output;
- build cancellation.

Toren orchestrates the standard .NET toolchain instead of embedding a proprietary compiler/build system.

## 10. ASP.NET Core development

Planned support:

- ASP.NET Core Web API;
- Minimal APIs;
- MVC;
- Razor Pages;
- Blazor;
- Worker Service;
- `launchSettings.json` profiles;
- environment selection;
- environment variables;
- HTTPS development certificate status;
- application URL detection;
- Open Browser action;
- OpenAPI/Swagger shortcut;
- User Secrets integration.

## 11. Diagnostics

A shared Problems panel should surface:

- compiler diagnostics;
- Roslyn analyzer diagnostics;
- MSBuild errors/warnings;
- NuGet problems;
- Toren IDE diagnostics.

Capabilities:

- severity filtering;
- solution/project/current-document filtering;
- click diagnostic to navigate to source;
- clear origin/source of each diagnostic.

## 12. Debugging

A real debugger is required for the product to qualify as a practical IDE.

Planned capabilities:

- breakpoints;
- conditional breakpoints;
- exception breakpoints;
- Continue;
- Pause;
- Stop;
- Restart;
- Step Into;
- Step Over;
- Step Out;
- Run to Cursor;
- Locals;
- Watch;
- Call Stack;
- Breakpoints panel;
- Debug Console;
- variable hover while paused.

The UI should communicate with a replaceable debugger backend through a clean adapter boundary.

## 13. Testing

Planned support:

- NUnit;
- xUnit;
- MSTest;
- Microsoft Testing Platform;
- test discovery;
- Test Explorer;
- Run All;
- Run Selected;
- Debug Test;
- Rerun Failed;
- stop test run;
- assertion/failure output;
- stack-trace navigation.

Toren must use standard test ecosystems and must not require a Toren-specific test framework.

## 14. Integrated terminal

Terminal is a first-class IDE feature.

Capabilities:

- native user shell;
- multiple terminals;
- split terminal;
- rename terminal;
- kill process/session;
- clear terminal;
- copy/paste;
- Open Terminal Here from project/filesystem nodes.

Default shell behavior should follow the host OS and user environment.

## 15. Git

Git integration uses the system Git installation.

Planned capabilities:

- repository detection;
- status;
- diff;
- inline diff;
- stage/unstage;
- commit;
- branch list;
- create branch;
- switch branch;
- pull;
- push;
- fetch;
- merge;
- stash;
- history/log;
- conflict UI.

Provider-specific workflows such as GitHub Issues, pull-request review, GitLab, and Azure DevOps are outside the initial core scope.

## 16. NuGet and references

Planned NuGet capabilities:

- search packages;
- installed packages;
- package versions;
- pre-release toggle;
- install;
- update;
- uninstall;
- transitive dependency view;
- Central Package Management;
- custom feeds;
- authentication for private feeds.

Reference views should expose:

- `ProjectReference`;
- `PackageReference`;
- `FrameworkReference`;
- SDKs;
- analyzers.

## 17. HTTP/API development

Toren should support `.http` files as a lightweight API client.

Planned capabilities:

- Send Request;
- environments/variables;
- request history;
- response body;
- response headers;
- formatted JSON responses.

This feature should work well for local ASP.NET Core API development without requiring an external API client for common workflows.

## 18. EF Core

Planned EF Core integration:

- detect EF Core tooling;
- list migrations;
- Add Migration;
- Remove Migration;
- Update Database;
- show migration command output.

The implementation should invoke standard EF Core tooling rather than introducing a separate migration model.

## 19. Docker

Docker is optional and must never be required to use Toren itself.

Planned capabilities:

- Dockerfile editing support;
- Compose file support;
- `compose up`;
- `compose down`;
- `compose build`;
- logs;
- container status in a later iteration.

Toren does not attempt to replace a container engine or Docker Desktop.

## 20. Database tools — later scope

Potential post-1.0 capabilities:

- PostgreSQL;
- SQL Server;
- SQLite;
- connection manager;
- schema browser;
- table browser;
- SQL query editor.

This must remain intentionally smaller than a dedicated database IDE.

## 21. Environment Doctor

Environment diagnostics are an important differentiator, particularly for clean-machine setup and teaching environments.

Checks may include:

- .NET SDK availability;
- required SDK from `global.json`;
- Git availability;
- Docker availability;
- HTTPS development certificate status;
- Node.js availability when relevant;
- missing project prerequisites.

The UI should explain problems and provide actionable remediation guidance.

## 22. Settings and UX

Settings areas:

- Appearance;
- Editor;
- Fonts;
- Terminal;
- Git;
- .NET;
- NuGet;
- Build;
- Debug;
- Files;
- Keyboard.

Also planned:

- settings search;
- command palette;
- keyboard shortcut editor;
- notifications;
- light/dark themes;
- native-feeling window behavior on each OS.

IDE-only settings should live in normal OS application-data locations unless project-local state is genuinely required.

## 23. Local-first and privacy

Toren is local-first.

- No account is required.
- No Toren cloud service is required.
- Mandatory telemetry is not allowed.
- Core edit/build/run/debug/test workflows work locally.

Network access is naturally still required for external resources such as fresh NuGet packages, remote Git operations, or online documentation.

If telemetry is introduced in the future, it must be transparent and opt-in.

## 24. Updates and distribution

Planned release channels:

- Stable;
- Preview.

Distribution targets:

- macOS `.app` / `.dmg`, signed and notarized for public releases;
- Windows installer plus portable archive;
- Linux AppImage initially, with `.deb`/`.rpm` considered later.

GitHub Releases can serve as the initial distribution/update source.

## 25. Extensibility

A public extension marketplace is not a 1.0 requirement.

However, core subsystems should avoid hard-coded coupling and should expose clean internal extension points for areas such as:

- editor services;
- language services;
- tool windows;
- project systems;
- debug adapters.

The extension API should be designed only after the core host/lifecycle model is stable.

## 26. Licensing

Current project license: Apache License 2.0.

The dependency policy must ensure that bundled dependencies and redistributed components are compatible with the project's distribution model and license obligations.

## 27. Initial implementation milestones

These are the original milestone sketches. The current delivery order and status are maintained in `docs/roadmap.md` and `docs/progress.md`.

### M0 — Repository and architecture bootstrap

- product specification;
- ADRs;
- CI skeleton;
- coding standards;
- base solution structure;
- Avalonia application shell.

### M1 — Usable editor shell

- workspace/folder open;
- project explorer;
- text editor;
- tabs;
- search;
- terminal;
- settings basics.

### M2 — .NET project workflow

- `.sln`/`.slnx`/`.csproj` loading;
- SDK detection;
- restore/build/run;
- output and Problems panels;
- `dotnet new` integration.

### M3 — C# IDE experience

- language service;
- completion;
- diagnostics;
- navigation;
- rename/refactoring essentials.

### M4 — Developer workflow

- Test Explorer;
- Git;
- NuGet;
- ASP.NET Core launch profiles;
- `.http` support;
- Environment Doctor.

### M5 — Debugging and advanced .NET workflow

- debugger;
- EF Core tooling;
- Docker Compose integration;
- release-quality macOS/Windows/Linux packaging.

Milestone contents are planning guidance and can be adjusted as implementation experience grows.
