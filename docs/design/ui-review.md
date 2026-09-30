# Responsive UI review

Source-build review performed on macOS arm64 on 2026-09-30. This supplements the release checklist; it does not close the Windows/Linux or signed release-artifact gates in `docs/progress.md`.

## Changes and behavior

- The sidebar resizes from 240 to 420 logical pixels. The editor retains a 600-pixel minimum width. The tool panel starts at 320 pixels and retains a 300-pixel minimum height so wrapped Git commands still leave room for the diff. Both splitters support dragging and keyboard focus.
- Tool headers scroll horizontally and the selected tool is brought into view. The duplicate Terminal placeholder and empty Tests tool tab are removed; Test Explorer is available through its activity button.
- Output selectors/actions, Test Explorer actions, Git commands, debug commands and Compose actions wrap within the available width. Settings labels and controls share aligned columns. Long status text and recent workspace paths trim inside their columns instead of displacing adjacent controls. EF Core input hints fit their fields, with optional/default behavior explained in tooltips.
- Go to File, workspace symbols, Find in Files and code actions use constrained result regions. Long lists scroll within the editor viewport.
- Editor overlays use Avalonia's generated XAML initializer so named controls are populated before commands access them. This fixes the null-control crash found when invoking Go to File and also covers Find/Replace, Go to Line, rename, quick fixes and hover.
- The theme shown in Settings follows the status-bar toggle, so changing an editor setting does not revert a theme selected from the status bar.

## Automated coverage

`tests/Toren.UnitTests/Ui` contains 15 UI cases: eight shell configurations, six editor-overlay interaction cases and one theme synchronization case. Rendering uses Skia with isolated temporary application profiles, without launching workspace commands or changing the user's saved preferences/session.

| Window size | Theme | Sidebar width |
| --- | --- | --- |
| 1080 × 700 | Dark and Light | 240, 270 and 420 |
| 1440 × 900 | Dark and Light | 270 |

Each shell configuration checks the eleven runtime tool panels, Test Explorer, populated diagnostics, Welcome with long recent-workspace paths, and three search/navigation popovers with thirty results. Geometry assertions verify usable tool height, visible selected headers, contained toolbar buttons and bounded result lists. The editor cases exercise selection/activation through keyboard input as well as initialized Find/Replace, Go to Line, rename, code-action and hover fields.

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

## Native macOS checks

The source-built app was inspected in both themes. Go to File opened a real C# document after filtering, Find opened inside the editor, both splitters changed their corresponding regions, and Settings search showed aligned keyboard controls. Native UI checks complement background rendering; they do not substitute for accessibility, performance and core-workflow review of the exact promoted release packages on all three platforms.
