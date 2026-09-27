# Workspace module

The workspace module keeps Explorer orchestration separate from external tooling and persistence concerns.

- `WorkspaceClassifier` recognizes folders and standard `.sln`, `.slnx`, and `.csproj` paths.
- `WorkspaceTreeService` composes Explorer nodes and owns lazy filesystem traversal. It depends on workspace contracts instead of invoking external tools directly.
- `DotNetSolutionProjectProvider` is the adapter for `dotnet sln ... list` and returns solution project paths.
- `MsBuildProjectReferenceProvider` is the adapter for evaluated MSBuild `ProjectReference`, `PackageReference`, and `FrameworkReference` items.
- solution project labels use the shortest unique dotted suffix so common prefixes do not dominate the Explorer while names remain unambiguous.
- `FileRecentWorkspaceStore` persists IDE-only workspace history in the OS application-data directory and never writes into a solution or project.

Models remain data-only. External process, filesystem, evaluation, and persistence failures are surfaced through Toren `Result<T>` values at their respective boundaries.
