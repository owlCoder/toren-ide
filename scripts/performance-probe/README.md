# Workspace service performance probe

Run from the repository root after restoring the target solution:

```sh
dotnet build scripts/performance-probe/PerformanceProbe.csproj -c Release
dotnet scripts/performance-probe/bin/Release/net10.0/PerformanceProbe.dll /absolute/path/Target.sln /absolute/path/results.json
```

Append `--skip-graph` to measure only indexing, quick-open ranking, text search and the solution tree. The full project graph has a 360-second cancellation limit. All operations use the production services and real filesystem/MSBuild processes; the application is not started. Background evaluations use the same shared process budget and single-node/no-reuse flags as the native application.

The JSON records five file-index samples, fifteen quick-open samples per query, three text searches per query and one sample each for solution-tree discovery and project-graph evaluation. Quick open ranks up to 75 files; text search returns at most 200 matches. The missing-token query forces a complete C# scan. RSS is sampled every 100 ms for the probe process only, excluding child SDK processes. Native UI timing, full compilation, diagnostics/generators and build/run throughput require separate measurements. OS disk caches are not flushed. See docs/design/performance-review.md for the Orchard Core trial and its limits.
