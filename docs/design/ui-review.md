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

`tests/Toren.UnitTests/Ui` contains 40 UI cases: eight shell configurations, eight editor-overlay interaction cases, one theme synchronization case, two document-editor theme cases, eight Settings/Packages window configurations, two About dialog cases, one terminal lifecycle case, three solution-command/progress cases, one problem-click navigation case and six panel visibility/expansion cases. Rendering uses Skia with isolated temporary application profiles, without launching workspace commands or changing the user's saved preferences/session.

| Window size | Theme | Sidebar width |
| --- | --- | --- |
| 1080 × 700 | Dark and Light | 240, 270 and 420 |
| 1440 × 900 | Dark and Light | 270 |

Each shell configuration checks the five bottom panels, every sidebar tool and its nested views, populated diagnostics, Welcome with long recent-workspace paths, and three search/navigation popovers with thirty results. Geometry assertions verify usable tool height, visible selected headers, contained toolbar buttons and bounded result lists. The editor cases exercise selection/activation through keyboard input as well as initialized Find/Replace, Go to Line, rename, code-action and hover fields.

## Modern palette and rounded controls

The dark theme uses cool graphite surfaces: `#101218` for the editor, `#181C25` for sidebars and panels, `#141820` for chrome, and `#202633` for raised controls. Muted indigo selections and `#A2AEFF` focus accents separate active tools without bright full-width bars. Primary text uses `#E1E7F0`; secondary text and borders keep a clear hierarchy. The light theme uses cool off-white surfaces and a darker `#5262B8` accent.

Shared radii are 8 px for inputs, buttons and tabs, 12 px for cards/popovers, and 6 px for result rows. The activity rail uses inset 40 px controls in 48 px slots. Document and tool tabs have rounded active backgrounds; keyboard focus remains visible. Settings groups and Packages result/installed regions use aligned cards, while dialog headers keep compact close controls. The command bar keeps its existing height and gives Build a distinct primary treatment.

Fluent control states, selected list rows, problem filters and disabled commands consume the shell palette. TextMate retains syntax highlighting but consumes Toren's editor background, foreground, line-number, selection and current-line brushes in both themes. An explicit current-line pen prevents AvaloniaEdit's fallback outline. The existing editor cases verify palette consistency with a C# document open after theme changes.

The modern UI pass passed all 465 Release tests with warnings treated as errors and produced 239 Skia screenshots across the supported geometry matrix. Native macOS review covered Settings and Packages, dark/light switching, a C# document opened through Go to File, and successful Build with command disabling and status-bar progress on the isolated ParcelBox QA solution.

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

## Dialog and sidebar layout polish

Shared header, card, field-label, status-badge and icon-action styles make each tool follow the same hierarchy. Settings category badges, Packages search/installed cards and the About details card use the rounded shell surfaces. Dialog titles and editor search/rename/code-action headers carry icons without replacing their accessible labels.

Source Control separates the commit card from collapsible branch/sync actions in a bounded scroll region; the change list and diff retain their own viewport. The empty-state indicator follows the observable change count. Debug uses named icon commands, a labelled attach card and a view selector so its inspection area stays useful at 240 px. HTTP groups request/environment/send controls, EF Core separates migration and database cards, Docker uses a two-column command group, and Test Explorer groups projects with count badges and compact Run/Debug actions. Explorer keeps the workspace summary in an inset card.

Editor overlays defer keyboard focus until the shown control has completed layout. Go to File, symbols, workspace search, Find/Replace/Go to Line, rename and quick fixes focus their primary input or selected result automatically. Ctrl/Cmd+Shift+F is reserved for workspace search; document Find no longer consumes that chord. Interaction checks verify this focus without a compensating click and verify quick-fix activation with Enter. About closes with Escape without closing its owner.

The final pass passed 469 Release tests with warnings treated as errors, including 40 UI cases and 249 Skia captures. Expanded Git commands are checked in every shell configuration, along with the populated change-list empty state and the retained diff height. Native macOS checks used the isolated ParcelBox QA workspace: Settings filtering and focus, Packages installed references, Source Control expansion and scrolling, Debug command names, HTTP, EF/Docker layouts, discovery of 14 tests, About/Escape, theme switching and successful Build with progress. The final package also verifies native Go to File typing without a click, four Find in Files results, distinct Find shortcuts and Escape closure. EF Core command execution is not verified because dotnet-ef is unavailable; Docker execution and a live debug attachment were outside this visual pass.

## Project diagnostics and source navigation

A single click or Enter on a problem opens its source document, activates the editor and moves to its reported line/column. Diagnostics without a source location show their message in the status bar.

Compiler contexts use evaluated Compile items, global usings, nullable/language/output options, framework/user preprocessor symbols, resolved references and generator inputs/options. Referenced projects with resolved assemblies are analyzed separately instead of injecting duplicate source types. Linked Compile files use explicit project ownership. Roslyn runs all source generators, including Razor; incremental generators retain their individual wrappers. Build diagnostics remain an independent Problems source.

A source-pipeline check against the local ParcelBox solution reproduced 923 diagnostics before correction (missing SDK global usings/options, duplicate referenced types and missing Razor context). After correction the same workspace returned zero diagnostics across 133 compilation inputs, including generated C# files. Injecting an unresolved symbol into an in-memory Web/Program.cs override produced the expected CS0103 at line 36, confirming real errors remain visible without modifying the reference project. Regression tests cover project options, multiple incremental generators with additional inputs, evaluated compiler metadata, linked ownership and concurrent terminal starts.

The review supplements the earlier native macOS checks of document navigation, both themes, splitters and settings. The latest tool reorganization is covered by background Skia rendering and interaction tests. Final accessibility, performance and core-workflow review still belongs to the exact promoted packages on all three platforms.

## Application branding and bottom-panel follow-up

The application now ships PNG/ICO/ICNS branding, with the same geometric T mark in the title bar, Welcome screen and About window. A vector BrandMark control preserves sharp rendering at different scale factors. Both macOS packaging workflows install the ICNS in Contents/Resources and declare CFBundleIconFile. Committed icon assets are generated on macOS with Pillow and iconutil through scripts/generate-brand-assets.py.

Output groups its selectors and uses named compact Build/Run actions with icon buttons for Rebuild, Clean, Cancel and Clear. Terminal has a labelled session selector, New/Close actions, compact Start/Kill/Clear controls and a focused command row. Its automatic start and kill behavior is unchanged and still covered by the lifecycle case. ASP.NET now has a consistent icon/header, compact secrets actions and a certificate/API region. Fluent input/button/dropdown state aliases are provided for both themes, keeping disabled light controls on the shell palette.

This pass passed 474 Release tests with warnings treated as errors, including 40 UI cases and 249 rendered screenshots. The large-repository measurements and background SDK process budget are documented separately in performance-review.md.
