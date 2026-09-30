# Toren IDE UI principles

Toren should look and behave like a serious modern developer tool rather than a sample Avalonia application. Visual quality, information hierarchy, density, keyboard accessibility, and consistency are product requirements.

## Product character

Toren is:

- focused;
- calm;
- dense enough for professional development;
- visually consistent across macOS, Windows, and Linux;
- native-feeling without trying to imitate one operating system exactly;
- keyboard-friendly;
- clear at a glance.

Toren is not:

- a collection of default framework controls placed in panels;
- visually noisy;
- dependent on decorative gradients or oversized cards;
- a clone of Visual Studio, Rider, VS Code, or any single existing IDE.

## Shell hierarchy

The primary desktop shell is organized into stable regions:

```text
Title / command area
----------------------------------------------------
Activity | Primary side bar | Editor / documents
bar      |                  |
         |                  |
----------------------------------------------------
Bottom panel: Problems / Output / Terminal / tool tabs
----------------------------------------------------
Status bar
```

Optional contextual secondary panes may appear on the right, but permanent screen space should not be consumed by information that can live in the status bar, tool window, or command palette.

Test Explorer replaces the primary sidebar through the Tests activity button. Sidebar and bottom-panel splitters support pointer dragging and keyboard resizing. Tool headers scroll horizontally when needed, and selecting a tool brings its tab into view.

## Visual language

### Spacing

Use a compact 4 px base grid where practical. Common spacing values are 4, 8, 12, 16, 24, and 32 px.

### Shape

- Small corner radii.
- Flat surfaces.
- Borders and tonal separation before shadows.
- Avoid card-heavy interfaces inside the editor shell.
- Text actions with leading icons use one shared icon size and center the icon with the label; button chrome and icon weight must remain visually balanced.

### Color

- Neutral dark and light themes first.
- One restrained accent color for focus, selection, and primary actions.
- Semantic colors are reserved for success, warning, error, Git state, diagnostics, and test state.
- Contrast must remain sufficient for long coding sessions.

### Typography

- UI typography uses a clean sans-serif system.
- Editor typography is separately configurable and monospace.
- Tool-window labels use compact hierarchy rather than large headings.
- Avoid excessive uppercase outside small tool-window section labels.

## Interaction

- Every major action must eventually be reachable from keyboard and command palette.
- Common operations should have stable shortcuts.
- Tool windows should preserve state where practical.
- Empty states explain the next useful action without filling the screen with prose.
- Loading, unavailable, and error states must be explicit.
- Destructive actions require appropriate confirmation, not blanket confirmation for ordinary operations.

## Cross-platform behavior

The shared design system defines layout, typography, spacing, colors, and interaction hierarchy. Platform adapters may adjust details such as window chrome, native dialogs, menu placement, keyboard labels, and expected platform conventions.

Do not branch the entire UI per operating system.

## Component strategy

Reusable UI primitives belong in a small design-system area rather than being restyled independently in every feature. Examples include:

- tool-window headers;
- toolbar buttons;
- activity-bar items;
- tabs;
- status-bar items;
- empty states;
- splitters;
- diagnostic badges;
- search boxes.

Feature-specific views compose these primitives.

## Accessibility

- Keyboard navigation is required.
- Focus state must be visible.
- Color must not be the only carrier of meaning.
- Text scaling and readable contrast must be considered from the beginning.
- Tooltips and accessible names should exist for icon-only actions.

## Review checklist

A UI pull request should answer:

1. Does this use an existing Toren component/style before introducing a new variant?
2. Is the action reachable by keyboard or designed so it can be?
3. Does the empty/loading/error state make sense?
4. Is permanent screen space justified?
5. Does the UI remain understandable on macOS, Windows, and Linux?
6. Is the feature usable without relying only on color?
7. Does the view contain product logic that should live outside the UI?
