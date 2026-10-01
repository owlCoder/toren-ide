using System.Diagnostics;
using System.Text.Json;
using Toren.App.Documents.Adapters;
using Toren.App.Search.Models;
using Toren.App.Search.Services;
using Toren.Core.Execution.Contracts;
using Toren.Core.Execution.Models;
using Toren.Core.Results;
using Toren.Platform.Execution.Adapters;
using Toren.Workspaces.Adapters;
using Toren.Workspaces.Models;
using Toren.Workspaces.Services;

if (args.Length < 2)
{
    Console.Error.WriteLine("Usage: PerformanceProbe <solution> <output.json> [--skip-graph]");
    return;
}

var solution = Path.GetFullPath(args[0]);
var output = Path.GetFullPath(args[1]);
Directory.SetCurrentDirectory(Path.GetDirectoryName(solution)!);
var samples = new List<Sample>();
var runner = new CountingRunner();
var files = new FileSystemWorkspaceFileProvider();
IReadOnlyList<WorkspaceFileEntry> index = [];
var process = Process.GetCurrentProcess();
long peak = 0;
using var timer = new Timer(_ => { try { process.Refresh(); Interlocked.Exchange(ref peak, Math.Max(Interlocked.Read(ref peak), process.WorkingSet64)); } catch (InvalidOperationException) { } }, null, 0, 100);
var serializerOptions = new JsonSerializerOptions { WriteIndented = true };
void Save() => File.WriteAllText(output, JsonSerializer.Serialize(new { solution, utc = DateTime.UtcNow, samples, peakProcessRssMb = peak / 1048576.0 }, serializerOptions));
async Task Measure(string phase, Func<Task<(bool Ok, int Count, string? Error)>> operation)
{
    var before = runner.Calls;
    var watch = Stopwatch.StartNew();
    bool ok; int count; string? error;
    try { (ok, count, error) = await operation(); }
    catch (OperationCanceledException) { ok = false; count = 0; error = "Cancelled after the 360-second measurement limit"; }
    watch.Stop();
    var sample = new Sample(phase, watch.Elapsed.TotalMilliseconds, ok, count, runner.Calls - before, error);
    samples.Add(sample); Save();
    Console.WriteLine(JsonSerializer.Serialize(sample));
}
for (var i = 0; i < 5; i++) await Measure($"file_index_{i + 1}", async () =>
{
    var result = await files.GetFilesAsync(solution);
    if (result.IsSuccess) index = result.Value;
    return (result.IsSuccess, result.Value?.Count ?? 0, result.IsFailure ? result.Error.Message : null);
});
var search = new WorkspaceFileSearchService();
foreach (var query in new[] { "Startup.cs", "ContentItem", "OrchardCore.ContentManagement" })
    for (var i = 0; i < 15; i++) await Measure($"quick_open:{query}", () => Task.FromResult((true, search.Search(index, query, 75).Count, (string?)null)));
var textSearch = new WorkspaceTextSearchService(files, new FileTextDocumentStore());
foreach (var query in new[] { "ContentItem", "TorenBenchmarkMissingToken_20261001" })
    for (var i = 0; i < 3; i++) await Measure($"text_search:{query}", async () =>
    {
        var result = await textSearch.SearchAsync(solution, query, new WorkspaceTextSearchOptions(IncludePatterns: "*.cs"));
        return (result.IsSuccess, result.Value?.Count ?? 0, result.IsFailure ? result.Error.Message : null);
    });
var evaluationRunner = new MsBuildEvaluationProcessRunner(runner);
var solutionProvider = new DotNetSolutionProjectProvider(runner);
var tree = new WorkspaceTreeService(solutionProvider, new MsBuildProjectReferenceProvider(evaluationRunner));
var workspace = new WorkspaceDescriptor(solution, "OrchardCore", WorkspaceKind.Solution);
var root = tree.CreateRoot(workspace).Value!;
await Measure("solution_tree", async () => { var result = await tree.GetChildrenAsync(root); return (result.IsSuccess, result.Value?.Count ?? 0, result.IsFailure ? result.Error.Message : null); });
if (!args.Contains("--skip-graph"))
{
    var graph = new WorkspaceProjectGraphService(new FileSystemFolderProjectProvider(), solutionProvider, new MsBuildProjectMetadataProvider(evaluationRunner), new MsBuildProjectReferenceProvider(evaluationRunner));
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(360));
    await Measure("project_graph", async () => { var result = await graph.LoadAsync(workspace, timeout.Token); return (result.IsSuccess, result.Value?.Projects.Count ?? 0, result.IsFailure ? result.Error.Message : null); });
}
Save();
sealed record Sample(string Phase, double Milliseconds, bool Success, int ResultCount, int ProcessCalls, string? Error);
sealed class CountingRunner : IProcessRunner
{
    private readonly SystemProcessRunner _inner = new();
    private int _calls;
    public int Calls => Volatile.Read(ref _calls);
    public async Task<Result<ProcessResult>> RunAsync(ProcessRequest request, CancellationToken token = default)
    {
        var count = Interlocked.Increment(ref _calls);
        if (count % 30 == 0) Console.WriteLine($"MSBuild process calls: {count}");
        return await _inner.RunAsync(request, token);
    }
}
