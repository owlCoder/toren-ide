# Orchard Core workspace performance review

Measured on macOS arm64 on 1 October 2026, using a Mac mini with an Apple M4 (10 cores), 16 GB RAM and .NET SDK 10.0.100.

This document records two passes on the same machine and checkout. The [first review](#first-review) found that project evaluation dominated workspace opening. The [second pass](#second-pass-shared-snapshot-and-batch-evaluation) removed most of that cost; its design is recorded in [ADR-0011](../decisions/0011-shared-project-snapshot-and-batch-evaluation.md).

## Workload and method

The official [Orchard Core repository](https://github.com/OrchardCMS/OrchardCore/tree/v2.2.1) is pinned to v2.2.1, commit ab45e0c0abaa79167eaa21148f688a1814f02631. It contains 225 project files and 4,815 C# files; OrchardCore.sln includes 220 projects. Toren indexes 8,507 workspace files after excluding generated/build directories. NuGet restore succeeded before measurements.

The committed scripts/performance-probe invokes the real file-index, quick-open, text-search, solution-tree and project-graph services. Indexing has five samples, quick-open ranking has fifteen per query, text search has three per query, and the project graph/tree have one trial per version. Quick open ranks up to 75 results. ContentItem text search stops at 200 results; the missing-token query scans every included C# file. Timings below are service measurements, not UI input-to-paint measurements.

OS caches were not flushed. The final trial follows a Mac restart and a fresh clone/restore of the same commit. These observations are useful for identifying bottlenecks; the before/after graph comparison is indicative, with one trial each.

## First review

### Service results

| Operation | Samples | Median |
| --- | ---: | ---: |
| Workspace file index | 5 | 84.5 ms |
| Quick open: Startup.cs | 15 | 4.49 ms |
| Quick open: ContentItem | 15 | 4.41 ms |
| Quick open: OrchardCore.ContentManagement | 15 | 4.58 ms |
| C# text search: ContentItem (200 results) | 3 | 94.3 ms |
| C# text search: absent token (full scan) | 3 | 232.4 ms |
| Solution project tree | 1 | 118.5 ms |
| Metadata and reference graph, 220 projects | 1 | 113.58 s |

The original sequential graph took 184.06 seconds; the final graph took 113.58 seconds, about 38.3% less time in these trials. Each performs 661 SDK process calls, so process startup/evaluation remains the main cost. Batches retain solution order and propagate cancellation.

The initial parallel native trial exposed excessive MSBuild workers and UI stalls. A shared evaluation runner now limits background evaluations across metadata, references and compiler-reference consumers to four processes, with maxcpucount:1 and nodeReuse:false. Foreground build/run behavior is preserved. Unit tests cover overlapping consumers, foreground commands, cancellation while waiting/running and release after failure. The final standalone probe reached 834.8 MiB RSS including child processes, with at most four descendants; the probe itself reached 118.8 MiB. These are sampled peaks, not absolute lifetime maxima.

### Native application observation

The 20261001.7 branded macOS bundle opened the same restored solution. The project tree was present at the first observation, 4.32 seconds after the Open action including automation and accessibility snapshot overhead. Go to File opened ContentItem.cs while project evaluation continued; Settings filtering/Escape, Git clean status, Find in Files (200 capped results), theme switching and Terminal auto-start/pwd/Kill all responded during background work. Packages opened as an owned window while its project list was loading.

A 180-sample observation at roughly one-second intervals recorded an app RSS peak of 628.2 MiB and process-tree peak of 1656.2 MiB. At most five descendants were observed, including transient Git/terminal work in addition to the four-evaluation budget. The host reported zero swap usage at the end of this observation. No repeated UI stall occurred in this final observation.

Runnable-project preparation was still pending at 363 seconds. Multiple consumers requested a full graph during workspace opening, so the isolated 113.58-second graph result was not the time until every native feature was ready. Avoiding duplicate full evaluations and loading unused tools lazily was left as performance work, which the second pass below addresses. No persistent metadata cache was introduced, to avoid stale references/diagnostics.

Full Orchard compilation/run, complete semantic diagnostics/generator throughput, long-session memory, Windows and Linux performance were not measured in this pass. Native interactions are qualitative checks; no sub-millisecond UI latency claim is made. Raw timings, resource samples and QA artifacts accompany the delivered package.

## Second pass: shared snapshot and batch evaluation

### What was wrong

Tracing the consumers showed three separate costs behind the first review's numbers:

- every consumer of the project graph (startup-project selection, Packages, test explorer, Problems scopes, C# editor features) evaluated all projects again, with three SDK processes per project;
- every editor request for the active document first resolved compiler references for all projects, one more process each, sequentially;
- classifying files by owning project compared each file against every project's source list.

### Method

The probe was extended to measure these paths (see scripts/performance-probe/README.md). "Before" is commit 4dfb621 with the extended probe composed the way the application composed its services at that commit; "after" is the application's current composition. Both ran on the same restored Orchard Core v2.2.1 checkout as the first review. Cold graph and project-list figures are medians of three cold loads after the change and two before; other sample counts are given in the table. Process-tree RSS was sampled every 0.5 s. OS caches were not flushed.

The Orchard checkout is restored but not built, as in the first review. Semantic contexts therefore stop at the missing project references for most projects, in both versions. Toren's own solution, which is built, is used as a second workload for the Roslyn phases.

### Orchard Core, 220 projects

| Operation | Before | After | Change |
| --- | ---: | ---: | ---: |
| Cold project graph | 110.6 s and 111.2 s, 661 process invocations | 7.44 s, 3 invocations | −93.3% time, −99.5% invocations |
| Project list for startup-project selection, Packages and tests | 110.6 s (needed the full graph) | 1.24 s, 2 invocations | −98.9% |
| Two consumers requesting a cold graph together | 172.0 s, 1,322 invocations | 7.17 s, 3 invocations | −95.8% |
| Repeated graph request, inputs unchanged | 110.6 s, 661 invocations | 45.6 ms, none | |
| Project data for one editor request on the active document | 162.9 s, 220 invocations, in addition to a graph load | 47.8 ms, none | |
| Problems scope index (6 and 3 samples) | 4.21 s, 172 MB allocated | 0.11 s, 18.5 MB | −97.4% |
| Project contexts for workspace diagnostics (2 samples each) | 2.01–2.33 s | 0.20–0.23 s | about −90% |
| Workspace file index (20 samples each, alternating builds) | 79.5 ms | 46.4 ms | −41.7% |
| C# text search: ContentItem, 200 results (12 samples each) | 83.2 ms | 51.5 ms | −38.1% |
| C# text search: absent token, full scan (12 samples each) | 184.9 ms | 148.6 ms | −19.7% |
| Quick open (60 samples per query) | 4.6 ms | 4.6 ms | unchanged |
| Solution project tree (4 samples each) | 129.4 ms | 126.3 ms | unchanged |

The batch evaluation and the per-project evaluation it replaces were compared project by project with the probe's `--compare-evaluation` option: all 220 projects were identical, including the MSBuild error text of the three projects in that checkout that are not restored.

When a project cannot be handled by the shared invocation, evaluation falls back to one process per project. Forcing that path for the whole solution (`--per-project`) took 69.6 s and 441 invocations for the graph and 13.8 s and 221 invocations for the project list, because metadata and declared references now come from one evaluation instead of two.

### Memory

The snapshot's path strings are shared between projects: 85,673 references are held by 7,488 string instances (2.4 MiB) instead of 85,673 instances (20.9 MiB).

Evaluating in one invocation costs more memory while it runs. Sampled process-tree RSS peaked at 1,292–1,389 MiB during the roughly seven seconds of batch evaluation, with at most four MSBuild nodes. The per-project evaluation peaked at 791 MiB in a graph-only run and 1,244 MiB in a run that included overlapping consumers, but held that level for the 110–280 seconds it took. The node count trades time for memory:

| MSBuild nodes | Cold graph | Peak process-tree RSS |
| ---: | ---: | ---: |
| 4 (used) | 7.4 s | 1,361 MiB |
| 3 | 8.2 s | 1,214 MiB |
| 2 | 10.6 s | 1,074 MiB |
| 1 | 15.8 s | 997 MiB |

Splitting the solution into several smaller invocations was measured and rejected: at 110 projects per invocation the peak fell only to 1,180 MiB while the cold graph rose to 10.7 s, because every invocation still loads most of the solution as referenced projects.

### Toren solution, 10 projects, built

Both versions produced the same results at the same repository state: 57 documents in the active project's context, 10 project contexts and 1,098 workspace diagnostics.

| Operation | Before | After |
| --- | ---: | ---: |
| Cold project graph | 3.21 s, 31 process invocations | 1.20 s, 3 invocations |
| Project list | 3.21 s (needed the full graph) | 0.56 s, 2 invocations |
| Project data for the first editor request | 3.28 s, 10 invocations | 11.7 ms, none |
| Project data for later editor requests | 20.2 ms | 7.2 ms |
| Roslyn diagnostics for the active document (5 samples, first one cold) | 474 ms, 173 MB allocated | 429 ms, 137 MB |
| Roslyn diagnostics for the workspace (2 samples) | 9.82 s, 3.26 GB allocated | 8.45 s, 2.79 GB |
| Probe process peak RSS | 773 MiB | 706 MiB |

The Roslyn rows reflect sharing metadata references between compilations. In two further alternating runs with only that change toggled, the active-document pass was 465 ms and 397 ms (medians of eight warm samples) and peak RSS was 727–772 MiB and 642–646 MiB.

### Limits

- The native application was not re-measured. The first review's observation that startup-project preparation was pending after 363 seconds corresponds to the service paths above, but end-to-end UI timing, and responsiveness while the batch invocation runs, still need a native pass.
- One machine and one operating system; Windows and Linux were not measured.
- Timings with a single sample are indicative. The overlap figure and the Toren graph "before" figure are single samples.
- A snapshot is re-evaluated whenever a file is added to or removed from the workspace, or a project or MSBuild import changes. On this workload that is one seven-second background evaluation; it is not incremental per project.
- Each Roslyn diagnostics pass still parses and binds its project from scratch. That is now the largest remaining cost of an editor request.

