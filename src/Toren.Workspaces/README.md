# Workspace module

The workspace module keeps Explorer orchestration separate from external tooling and persistence concerns.

- `WorkspaceClassifier` recognizes folders and standard `.sln`, `.slnx`, and `.csproj` paths.
- `WorkspaceTreeService` composes Explorer nodes and owns lazy filesystem traversal. It depends on workspace contracts instead of invoking external tools directly.
- `DotNetSolutionProjectProvider` is the adapter for `dotnet sln ... list` and returns solution project paths.
- `FileSystemFolderProjectProvider` discovers `.csproj` files for plain-folder workspaces while skipping build/IDE metadata directories and reparse points.
- `MsBuildProjectReferenceProvider` is the adapter for evaluated MSBuild `ProjectReference`, `PackageReference`, and `FrameworkReference` items of a single project; the Explorer uses it when a References node is expanded.
- `IProjectEvaluationProvider` evaluates all projects of a workspace in two stages: evaluation (target frameworks, output/assembly identity, test-project state, Central Package Management, imported props/targets paths, declared references) and design-time compiler inputs (sources, analyzers, global usings, reference assemblies).
  - `MsBuildBatchProjectEvaluationProvider` runs each stage as one MSBuild invocation over all projects, using the collection targets in `Adapters/Toren.Evaluation.targets`.
  - `MsBuildProjectEvaluationProvider` runs one process per project and is the reference behavior; the batch provider falls back to it for projects it could not handle.
- `WorkspaceProjectGraphEvaluator` composes folder, solution, and direct-project workspaces from provider contracts into data-only `WorkspaceProjectGraph` / `WorkspaceProject` models. It does not invoke external tools or scan the filesystem directly.
- `WorkspaceProjectGraphService` owns the evaluated snapshot of the open workspace. It implements `IWorkspaceProjectCatalog` (evaluated projects, for project lists) and `IWorkspaceProjectGraphService` (projects with compiler inputs), shares one evaluation between overlapping consumers, and reuses a finished snapshot only while `IProjectEvaluationInputStampProvider` reports its input files unchanged. Nothing is persisted. See [ADR-0011](../../docs/decisions/0011-shared-project-snapshot-and-batch-evaluation.md).
- `ProjectCompilationReferenceResolver` turns evaluated metadata into usable reference assemblies and reports unrestored or unbuilt projects.
- solution/project labels use the shortest unique dotted suffix so common prefixes do not dominate the Explorer while names remain unambiguous.
- `FileRecentWorkspaceStore` persists IDE-only workspace history in the OS application-data directory and never writes into a solution or project.

Models remain data-only. External process, filesystem, evaluation, and persistence failures are surfaced through Toren `Result<T>` values at their respective boundaries. Evaluated metadata and graph composition are exposed through contracts so later editor/build features do not need to parse project XML, scan workspace folders, or invoke MSBuild directly.
