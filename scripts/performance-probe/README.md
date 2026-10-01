# Workspace service performance probe

Run from the repository root after restoring the target solution:

```sh
dotnet build scripts/performance-probe/PerformanceProbe.csproj -c Release
dotnet scripts/performance-probe/bin/Release/net10.0/PerformanceProbe.dll /absolute/path/Target.sln /absolute/path/results.json
```

All operations use the production services and real filesystem/MSBuild processes, composed the way the application composes them; the application is not started. Each sample records elapsed time, the number of SDK process invocations and the managed bytes allocated.

| Phase | Samples | What it measures |
| --- | ---: | --- |
| `file_index_*` | 5 | Workspace file listing |
| `quick_open:*` | 15 per query | Ranking up to 75 files |
| `text_search:*` | 3 per query | C# text search; one query stops at 200 matches, the missing token forces a full scan |
| `solution_tree` | 1 | Solution project discovery for the Explorer |
| `project_graph` | 3 | Cold project graph: evaluation plus design-time compiler inputs, a new service each time |
| `project_catalog` | 3 | Cold project list alone, as run targets, Packages and the test explorer request it |
| `project_graph_reuse` | 5 | A repeated graph request while inputs are unchanged |
| `project_graph_overlap_x2` | 1 | Two consumers requesting a cold graph at the same time |
| `problems_scope` | 3 | Problems scope index over the loaded graph |
| `semantic_context_active_*` | 4 | The project context one editor request needs for the active document |
| `semantic_context_workspace_warm` | 2 | Project contexts for workspace-wide diagnostics |
| `diagnostics_active` | 5 | Context plus Roslyn diagnostics for the active document |
| `diagnostics_workspace` | 2 | Context plus Roslyn diagnostics for every project |

Options:

- `--skip-graph` measures only indexing, quick open, text search and the solution tree.
- `--skip-overlap` skips the overlapping-consumer phase.
- `--per-project` evaluates with one SDK process per project, the fallback path, instead of one MSBuild invocation per stage.
- `--nodes <count>` sets the MSBuild node count of the shared invocation; the application uses up to four.
- `--compare-evaluation` adds an `evaluation_equivalence` phase that evaluates the solution both ways and fails if any project differs.
- `--active-document <file name>` selects the document for the editor phases; the default is `ContentItem.cs`.

The semantic and diagnostics phases need built project references. On a solution that is restored but not built they report the missing references, as the application does, and measure only the work up to that point.

The JSON also records the path-string payload of the loaded graph. RSS is sampled every 100 ms for the probe process only, excluding child SDK processes; sample the process tree externally when that matters. Native UI timing, full compilation and build/run throughput require separate measurements. OS disk caches are not flushed. See docs/design/performance-review.md for the Orchard Core trials and their limits.
