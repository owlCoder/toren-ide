# Workspace module

The workspace module keeps Explorer orchestration separate from external tooling and persistence concerns.

- `WorkspaceClassifier` recognizes folders and standard `.sln`, `.slnx`, and `.csproj` paths.
- `WorkspaceTreeService` composes Explorer nodes and owns lazy filesystem traversal. It depends on workspace contracts instead of invoking external tools directly.
- `DotNetSolutionProjectProvider` is the adapter for `dotnet sln ... list` and returns solution project paths.
- `MsBuildProjectReferenceProvider` is the adapter for evaluated MSBuild `ProjectReference`, `PackageReference`, and `FrameworkReference` items.
- `MsBuildProjectMetadataProvider` evaluates project properties through MSBuild, including target frameworks, output/assembly identity, test-project state, Central Package Management, and standard imported props/targets paths.
- solution project labels use the shortest unique dotted suffix so common prefixes do not dominate the Explorer while names remain unambiguous.
- `FileRecentWorkspaceStore` persists IDE-only workspace history in the OS application-data directory and never writes into a solution or project.

Models remain data-only. External process, filesystem, evaluation, and persistence failures are surfaced through Toren `Result<T>` values at their respective boundaries. Evaluated metadata is exposed through contracts so later editor/build features do not need to parse project XML or invoke MSBuild directly.
