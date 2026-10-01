# ADR-0011: Shared workspace project snapshot and batch evaluation

- Status: Accepted
- Date: 2026-10-01

## Context

Every tool that needs project information — startup-project selection, Packages, the test explorer, Problems scopes and all C# editor features — asked `IWorkspaceProjectGraphService` for the graph, and each request evaluated every project from scratch with three `dotnet msbuild` processes per project. Compiler references were then resolved with a fourth process per project, sequentially, on every editor request.

On Orchard Core v2.2.1 (220 projects) one graph load took 110.6 s and 661 process invocations, two overlapping consumers took 172.0 s and 1,322, and a single editor request spent 162.9 s resolving references before it could answer. The earlier performance review recorded that startup-project preparation was still pending after 363 seconds. Measurements and method are in [the performance review](../design/performance-review.md).

The review also rejected a persistent metadata cache, because stale references produce wrong diagnostics. That constraint stands.

## Decision

### One evaluated snapshot per open workspace

`WorkspaceProjectGraphService` owns the evaluated state of the open workspace and is the only implementation of two contracts:

- `IWorkspaceProjectCatalog` — the evaluated projects (target frameworks, output type, test flag, declared references). Enough for project lists; needs no targets.
- `IWorkspaceProjectGraphService` — the same projects with design-time compiler inputs (sources, analyzers, global usings, reference assemblies).

Compiler inputs are resolved on first demand, so consumers of the catalog are not delayed by them.

Overlapping requests share one evaluation. Waiting is cancellable per caller, but the evaluation belongs to the workspace: a caller that stops waiting does not cancel work others, or the next request, need. An evaluation is cancelled when its workspace is replaced and nobody is waiting for it, or when the service is disposed.

Nothing is written to disk and nothing survives the process. Opening another workspace replaces the snapshot.

### Reuse only while the inputs are unchanged

A finished snapshot is reused only if the files it was evaluated from are unchanged. `IProjectEvaluationInputStampProvider` supplies two stamps:

- the **workspace stamp**, taken before evaluation starts: every file path under the workspace directory (globs make any added or removed file a potential input) and the size and modification time of solutions, projects, `.props`, `.targets`, `.rsp`, `global.json`, `NuGet.config` and `packages.lock.json`, including the well-known ones in parent directories;
- the **project stamp**, taken once evaluation has revealed them: each project file, its `Directory.Build.props`/`.targets`, `Directory.Packages.props` and NuGet assets file, wherever they are.

Each request compares the current stamps with the snapshot's. Because project-stamp inputs cannot be known before evaluating, a snapshot that sees one of them modified after its evaluation began serves only the callers that were already waiting. The invariant is:

> A request never receives a snapshot whose tracked inputs changed between the start of that snapshot's evaluation and the start of the request.

Failed evaluations are never kept. Whether a resolved reference assembly exists is checked each time it is used (`ProjectCompilationReferenceResolver`), so building a referenced project is picked up without re-evaluating.

### Evaluate the workspace in one MSBuild invocation

`IProjectEvaluationProvider` is batch-oriented: it receives all projects of a stage. How many processes that takes is the adapter's concern, not the graph composition's.

`MsBuildBatchProjectEvaluationProvider` generates a temporary traversal project that runs one MSBuild task over all projects. A small targets file, embedded in `Toren.Workspaces` and injected through the documented `CustomBeforeMicrosoftCommonTargets` and `CustomBeforeMicrosoftCommonCrossTargetingTargets` hooks, makes each project write its evaluated properties and items to its own file. MSBuild then loads the SDK once and shares imports and referenced-project evaluations between projects. The invocation uses at most four nodes with node reuse disabled.

Building any target makes MSBuild run a project's initial targets first, and packages use those to change items; the test SDK adds a generated entry point this way. The evaluation stage must describe the project as evaluated, so it writes its records from an initial target of its own, which the early import places ahead of the initial targets contributed by packages and `Directory.Build.targets`. The compiler-input stage runs the same design-time targets as the per-project query and writes afterwards.

`MsBuildProjectEvaluationProvider`, which evaluates each project with its own process through `-getProperty`/`-getItem`, remains the reference behavior. The batch provider delegates to it for any project the shared invocation produced no result for, so failure semantics and error codes are those of the per-project path. Both build their models from the same intermediate data (`MsBuildEvaluationData`).

This stays within ADR-0002 and ADR-0004: it is the `dotnet` CLI and MSBuild, the traversal project and the targets live in a private temporary directory for the duration of one invocation, and nothing is added to the user's projects.

## Consequences

- On Orchard Core a cold graph takes 7.4 s and 3 process invocations instead of 110.6 s and 661; the project list alone takes 1.2 s. Repeated requests cost a directory listing (about 46 ms on that workspace) and no processes.
- The shared invocation holds every project in memory while it runs: sampled process-tree RSS peaked at about 1.3–1.4 GiB for the seven seconds, against 0.8–1.2 GiB held for minutes by per-project evaluation. Fewer nodes or smaller invocations were measured and trade little memory for much time.
- Adding or removing any file in the workspace, or editing a project or import, causes one re-evaluation on the next request. Editing source text does not.
- Inputs outside the tracked set are not detected: imports in directories that are neither inside the workspace nor reported by evaluation, environment variables, and changes to the installed SDK. Reopening the workspace re-evaluates.
- A project that sets `CustomBeforeMicrosoftCommonTargets` itself is evaluated without its own value during batch evaluation, because the injected global property takes precedence. The machine-wide default file is still imported.
- Initial targets declared by the project file itself, or by imports that precede the common targets, run before the evaluation-stage records are written. Items they add appear in the project catalog, where a plain evaluation would not show them. The graph with compiler inputs is unaffected, because design-time targets run initial targets on both paths.
- Equivalence between the shared invocation and the per-project queries is pinned by tests that run the real SDK (`MsBuildEvaluationToolchainTests`) and can be checked on any solution with the performance probe's `--compare-evaluation` option.
- `IProjectMetadataProvider` and `IProjectCompilationReferenceProvider` are removed. Their behavior is covered by `IProjectEvaluationProvider` and `ProjectMetadata.ReferencePaths` / `CompilerInputsError`.
- The evaluation budget of `MsBuildEvaluationProcessRunner` now governs the per-project fallback and on-demand Explorer reference queries. A batch invocation bounds itself to four nodes, so a concurrent on-demand query can briefly add to that.
