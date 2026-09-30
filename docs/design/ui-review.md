# Responsive UI review

Source-build review performed on macOS arm64 on 2026-09-30 and 2026-10-01. This supplements the release checklist; it does not close the Windows/Linux or signed release-artifact gates in `docs/progress.md`.

## Changes and behavior

- The sidebar resizes from 240 to 420 logical pixels. The editor retains a 600-pixel minimum width. The tool panel starts at 320 pixels and retains a 300-pixel minimum height so bottom tool actions leave usable output space. Both splitters support dragging and keyboard focus.
- The bottom panel contains only Problems, Output, ASP.NET, Doctor and Terminal, each with an icon. Explorer, Source Control, Tests, Debug, HTTP and Data share the left sidebar with a single active activity button. Sidebar tools preserve their view models while switching; leaving Tests cancels pending discovery.
- Settings and Packages open in owned, resizable windows. Repeated activation focuses the current window, closing/reopening retains panel state, and their theme follows the owner. Escape closes a tool window.
- Open moved to the command bar alongside Save/Undo/Redo, Build Solution, Rebuild Solution, Clean Solution, configuration/startup selection, Run and Stop. An indeterminate status-bar progress indicator remains visible for build/tool commands until execution completes or cancels; long-running application sessions keep their Run status without an indefinite build indicator. Non-run commands target the whole opened solution.
- Selecting Terminal starts the selected native-shell session immediately; switching tabs retains it. New sessions auto-start when Terminal is visible. Kill terminates the selected process tree. A pending-start guard prevents duplicate launches and disposes late arrivals after shutdown.
- Output selectors/actions, Test Explorer actions, Git commands, debug commands and Compose actions wrap within the available width. Settings labels and controls share aligned columns. Long status text and recent workspace paths trim inside their columns instead of displacing adjacent controls. EF Core input hints fit their fields, with optional/default behavior explained in tooltips.
- Go to File, workspace symbols, Find in Files and code actions use constrained result regions. Long lists scroll within the editor viewport.
- Editor overlays use Avalonia's generated XAML initializer so named controls are populated before commands access them. This fixes the null-control crash found when invoking Go to File and also covers Find/Replace, Go to Line, rename, quick fixes and hover.
- The theme shown in Settings follows the status-bar toggle, so changing an editor setting does not revert a theme selected from the status bar.

## Automated coverage

`tests/Toren.UnitTests/Ui` contains 30 UI cases: eight shell configurations, six editor-overlay interaction cases, one theme synchronization case, two document-editor theme cases, eight Settings/Packages window configurations, one terminal lifecycle case, three solution-command/progress cases and one problem-click navigation case. Rendering uses Skia with isolated temporary application profiles, without launching workspace commands or changing the user's saved preferences/session.

| Window size | Theme | Sidebar width |
| --- | --- | --- |
| 1080 × 700 | Dark and Light | 240, 270 and 420 |
| 1440 × 900 | Dark and Light | 270 |

Each shell configuration checks the five bottom panels, every sidebar tool and its nested views, populated diagnostics, Welcome with long recent-workspace paths, and three search/navigation popovers with thirty results. Geometry assertions verify usable tool height, visible selected headers, contained toolbar buttons and bounded result lists. The editor cases exercise selection/activation through keyboard input as well as initialized Find/Replace, Go to Line, rename, code-action and hover fields.

## Dark theme refinement

The surface hierarchy uses the [VS Code 2026 dark theme](https://github.com/microsoft/vscode/blob/main/extensions/theme-defaults/themes/2026-dark.json) as a reference, verified against the locally installed VS Code 1.140.0. The editor uses `#121314`, sidebar/panels/chrome use `#191A1B`, raised surfaces use `#202122`, and dividers use `#2A2B2C`. Text, icons and blue focus/selection accents are restrained to reduce glare while keeping secondary text readable.

Fluent's dark palette and button/input/dropdown states share the shell tokens, including disabled and popup states. Control-fill accents use `#297AA0` so white labels remain legible; focus/link accents use the lighter `#48A0C7`. TextMate retains syntax highlighting but consumes Toren's dark editor background, foreground, line-number, selection and current-line brushes instead of restoring its lighter Dark+ surfaces. An explicit current-line pen prevents AvaloniaEdit's green fallback outline. The two editor cases open a C# fixture, change themes with that document open, select text and verify palette consistency before capturing the rendered editor.

Run all quality checks:

```bash
dotnet restore Toren.slnx
dotnet build Toren.slnx --configuration Release --no-restore -p:TreatWarningsAsErrors=true
dotnet test Toren.slnx --configuration Release --no-build --no-restore
```

Produce PNGs for visual review (optional):

```bash
TOREN_UI_CAPTURE_DIR=/absolute/path/to/captures dotnet test tests/Toren.UnitTests/Toren.UnitTests.csproj --configuration Release --filter FullyQualifiedName~Toren.UnitTests.Ui
```

## Project diagnostics and source navigation

A single click or Enter on a problem opens its source document, activates the editor and moves to its reported line/column. Diagnostics without a source location show their message in the status bar.

Compiler contexts use evaluated Compile items, global usings, nullable/language/output options, framework/user preprocessor symbols, resolved references and generator inputs/options. Referenced projects with resolved assemblies are analyzed separately instead of injecting duplicate source types. Linked Compile files use explicit project ownership. Roslyn runs all source generators, including Razor; incremental generators retain their individual wrappers. Build diagnostics remain an independent Problems source.

A source-pipeline check against the local ParcelBox solution reproduced 923 diagnostics before correction (missing SDK global usings/options, duplicate referenced types and missing Razor context). After correction the same workspace returned zero diagnostics across 133 compilation inputs, including generated C# files. Injecting an unresolved symbol into an in-memory Web/Program.cs override produced the expected CS0103 at line 36, confirming real errors remain visible without modifying the reference project. Regression tests cover project options, multiple incremental generators with additional inputs, evaluated compiler metadata, linked ownership and concurrent terminal starts.

The review supplements the earlier native macOS checks of document navigation, both themes, splitters and settings. The latest tool reorganization is covered by background Skia rendering and interaction tests. Final accessibility, performance and core-workflow review still belongs to the exact promoted packages on all three platforms.
