# Toren iconography

Toren uses a single, consistent vector icon language across the desktop shell and Explorer. Icons should remain compact, legible at IDE density, and subordinate to the content they represent.

## Source and licensing

The current shell and file-type vector paths are sourced from the Pictogrammers Material Design Icons distribution. Toren keeps the upstream license text in [`docs/licenses/material-design-icons.txt`](../licenses/material-design-icons.txt). Small Toren-owned structural Explorer glyphs may be used when a simpler outline better matches IDE density.

When adding or replacing an icon:

- prefer non-brand icons from the same Pictogrammers Material Design Icons family when an existing glyph fits;
- Toren-owned glyphs should stay simple, geometric, and scoped to the UI region that owns them;
- keep required source/license attribution in the repository;
- do not copy Microsoft, Visual Studio, JetBrains, or other proprietary product assets;
- do not add a brand icon or an icon with additional licensing/attribution requirements unless that requirement is explicitly documented alongside the asset;
- keep shared vector path data in the central Toren style resources; Explorer-only structural geometry can stay with `ExplorerNodeIcon` when it has no reuse outside that control.

## Visual rules

Shell command icons use the same nominal 24-unit source grid and are rendered at consistent sizes appropriate to the surrounding control.

Explorer icons use an 18 x 18 layout slot, but the visible glyph should normally render at roughly 15–16 px and be optically centered within that slot. Structural nodes such as solutions, projects, and references use restrained outline glyphs and muted colors. File-type colors may vary semantically, but should remain lower-saturation than primary actions or selection states.

Explorer colors are semantic design tokens defined in `src/Toren.App/Styles/TorenStyles.axaml`. They should stay subtle enough for a professional desktop IDE while making common node and file categories distinguishable at a glance.

Use the existing Explorer categories before introducing a new one:

- folder / open folder;
- solution;
- project / test project;
- references;
- C# code;
- data;
- markup;
- configuration;
- document;
- container/Docker;
- image;
- generic file.

A new category is justified only when it gives users durable, useful information and is backed by deterministic classification plus regression tests.

## Accessibility and interaction

Icons are supplementary to visible text and must not be the only way information is communicated. Interactive shell icons require an accessible name or tooltip through their owning control. Explorer file-type color differences are decorative; the file or node name remains the primary identifier.
