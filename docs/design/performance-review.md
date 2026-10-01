# Orchard Core workspace performance review

Measured on macOS arm64 on 1 October 2026, using a Mac mini with an Apple M4 (10 cores), 16 GB RAM and .NET SDK 10.0.100.

## Workload and method

The official [Orchard Core repository](https://github.com/OrchardCMS/OrchardCore/tree/v2.2.1) is pinned to v2.2.1, commit ab45e0c0abaa79167eaa21148f688a1814f02631. It contains 225 project files and 4,815 C# files; OrchardCore.sln includes 220 projects. Toren indexes 8,507 workspace files after excluding generated/build directories. NuGet restore succeeded before measurements.

The committed scripts/performance-probe invokes the real file-index, quick-open, text-search, solution-tree and project-graph services. Indexing has five samples, quick-open ranking has fifteen per query, text search has three per query, and the project graph/tree have one trial per version. Quick open ranks up to 75 results. ContentItem text search stops at 200 results; the missing-token query scans every included C# file. Timings below are service measurements, not UI input-to-paint measurements.

OS caches were not flushed. The final trial follows a Mac restart and a fresh clone/restore of the same commit. These observations are useful for identifying bottlenecks; the before/after graph comparison is indicative, with one trial each.

## Final service results

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

## Native application observation

The 20261001.7 branded macOS bundle opened the same restored solution. The project tree was present at the first observation, 4.32 seconds after the Open action including automation and accessibility snapshot overhead. Go to File opened ContentItem.cs while project evaluation continued; Settings filtering/Escape, Git clean status, Find in Files (200 capped results), theme switching and Terminal auto-start/pwd/Kill all responded during background work. Packages opened as an owned window while its project list was loading.

A 180-sample observation at roughly one-second intervals recorded an app RSS peak of 628.2 MiB and process-tree peak of 1656.2 MiB. At most five descendants were observed, including transient Git/terminal work in addition to the four-evaluation budget. The host reported zero swap usage at the end of this observation. No repeated UI stall occurred in this final observation.

Runnable-project preparation was still pending at 363 seconds. Multiple consumers request a full graph during workspace opening, so the isolated 113.58-second graph result is not the time until every native feature is ready. Avoiding duplicate full evaluations and loading unused tools lazily remain performance work. No persistent metadata cache was introduced, to avoid stale references/diagnostics.

Full Orchard compilation/run, complete semantic diagnostics/generator throughput, long-session memory, Windows and Linux performance were not measured in this pass. Native interactions are qualitative checks; no sub-millisecond UI latency claim is made. Raw timings, resource samples and QA artifacts accompany the delivered package.
