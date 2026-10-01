using System.Diagnostics;
using System.Text.Json;
using Toren.App.Diagnostics.Services;
using Toren.App.Documents.Adapters;
using Toren.App.Editor.Services;
using Toren.App.Search.Models;
using Toren.App.Search.Services;
using Toren.Core.Execution.Contracts;
using Toren.Core.Execution.Models;
using Toren.Core.Results;
using Toren.Language.CSharp.Models;
using Toren.Language.CSharp.Services;
using Toren.Platform.Execution.Adapters;
using Toren.Workspaces.Adapters;
using Toren.Workspaces.Contracts;
using Toren.Workspaces.Models;
using Toren.Workspaces.Services;

if (args.Length < 2)
{
    Console.Error.WriteLine("Usage: PerformanceProbe <solution> <output.json> [--skip-graph] [--skip-overlap] [--per-project] [--nodes <count>] [--compare-evaluation] [--compare-diagnostics] [--active-document <file name>]");
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
double? graphStringPayloadMb = null;
void Save() => File.WriteAllText(output, JsonSerializer.Serialize(new { solution, utc = DateTime.UtcNow, samples, peakProcessRssMb = peak / 1048576.0, graphStringPayloadMb }, serializerOptions));
async Task Measure(string phase, Func<Task<(bool Ok, int Count, string? Error)>> operation)
{
    var before = runner.Calls;
    var allocatedBefore = GC.GetTotalAllocatedBytes();
    var watch = Stopwatch.StartNew();
    bool ok; int count; string? error;
    try { (ok, count, error) = await operation(); }
    catch (OperationCanceledException) { ok = false; count = 0; error = "Cancelled after the measurement limit"; }
    watch.Stop();
    var sample = new Sample(phase, watch.Elapsed.TotalMilliseconds, ok, count, runner.Calls - before,
        (GC.GetTotalAllocatedBytes() - allocatedBefore) / 1048576.0, error);
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
var documentStore = new FileTextDocumentStore();
var textSearch = new WorkspaceTextSearchService(files, documentStore);
foreach (var query in new[] { "ContentItem", "TorenBenchmarkMissingToken_20261001" })
    for (var i = 0; i < 3; i++) await Measure($"text_search:{query}", async () =>
    {
        var result = await textSearch.SearchAsync(solution, query, new WorkspaceTextSearchOptions(IncludePatterns: "*.cs"));
        return (result.IsSuccess, result.Value?.Count ?? 0, result.IsFailure ? result.Error.Message : null);
    });
var evaluationRunner = new MsBuildEvaluationProcessRunner(runner);
var solutionProvider = new DotNetSolutionProjectProvider(runner);
var tree = new WorkspaceTreeService(solutionProvider, new MsBuildProjectReferenceProvider(evaluationRunner));
var classifier = new WorkspaceClassifier();
classifier.TryClassifyFile(solution, out var workspace);
var root = tree.CreateRoot(workspace!).Value!;
await Measure("solution_tree", async () => { var result = await tree.GetChildrenAsync(root); return (result.IsSuccess, result.Value?.Count ?? 0, result.IsFailure ? result.Error.Message : null); });
if (args.Contains("--skip-graph"))
{
    Save();
    return;
}

// Composed as the application composes it: one shared snapshot per open workspace, evaluated in one
// MSBuild invocation. --per-project measures the fallback that starts a process per project instead.
var perProjectEvaluation = new MsBuildProjectEvaluationProvider(evaluationRunner);
IProjectEvaluationProvider evaluation = args.Contains("--per-project")
    ? perProjectEvaluation
    : new MsBuildBatchProjectEvaluationProvider(
        runner,
        perProjectEvaluation,
        Array.IndexOf(args, "--nodes") is var nodes and >= 0 && nodes + 1 < args.Length
            ? int.Parse(args[nodes + 1], System.Globalization.CultureInfo.InvariantCulture)
            : null);
WorkspaceProjectGraphService CreateProjectService() => new(
    new WorkspaceProjectGraphEvaluator(new FileSystemFolderProjectProvider(), solutionProvider, evaluation),
    new FileSystemProjectEvaluationInputStampProvider(files));

using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(900));

// The first cold graph is kept: later phases reuse it.
using var projects = CreateProjectService();
WorkspaceProjectGraph? graph = null;
await Measure("project_graph", async () =>
{
    var result = await projects.LoadAsync(workspace!, timeout.Token);
    if (result.IsSuccess) graph = result.Value;
    return (result.IsSuccess, result.Value?.Projects.Count ?? 0, result.IsFailure ? result.Error.Message : null);
});
if (graph is not null)
{
    // What the snapshot keeps alive is dominated by path strings; count each string instance once.
    var strings = new HashSet<string>(ReferenceEqualityComparer.Instance);
    var references = 0;
    foreach (var metadata in graph.Projects.Select(static project => project.Metadata))
        foreach (var path in metadata.SourcePaths.Concat(metadata.AnalyzerPaths).Concat(metadata.AdditionalFilePaths)
                     .Concat(metadata.AnalyzerConfigPaths).Concat(metadata.ReferencePaths ?? []))
        {
            references++;
            strings.Add(path);
        }
    graphStringPayloadMb = strings.Sum(static path => 22L + (path.Length * 2)) / 1048576.0;
    Console.WriteLine($"Graph path strings: {references} references, {strings.Count} instances, {graphStringPayloadMb:F1} MiB");
}

// Two more cold graphs: evaluation plus design-time compiler inputs.
for (var i = 0; i < 2; i++)
{
    using var cold = CreateProjectService();
    await Measure("project_graph", async () =>
    {
        var result = await cold.LoadAsync(workspace!, timeout.Token);
        return (result.IsSuccess, result.Value?.Projects.Count ?? 0, result.IsFailure ? result.Error.Message : null);
    });
}

// The project list alone, as the startup-project selector, Packages and the test explorer request it.
for (var i = 0; i < 3; i++)
{
    using var catalogOnly = CreateProjectService();
    await Measure("project_catalog", async () =>
    {
        var result = await catalogOnly.GetProjectsAsync(workspace!, timeout.Token);
        return (result.IsSuccess, result.Value?.Count ?? 0, result.IsFailure ? result.Error.Message : null);
    });
}

for (var i = 0; i < 5; i++) await Measure("project_graph_reuse", async () =>
{
    var result = await projects.LoadAsync(workspace!, timeout.Token);
    return (result.IsSuccess, result.Value?.Projects.Count ?? 0, result.IsFailure ? result.Error.Message : null);
});

if (!args.Contains("--skip-overlap"))
{
    // Two consumers asking for a cold graph at the same time, as tool windows do while a workspace opens.
    using var overlapping = CreateProjectService();
    await Measure("project_graph_overlap_x2", async () =>
    {
        var results = await Task.WhenAll(overlapping.LoadAsync(workspace!, timeout.Token), overlapping.LoadAsync(workspace!, timeout.Token));
        var failed = results.FirstOrDefault(static result => result.IsFailure);
        return (failed is null, results[0].Value?.Projects.Count ?? 0, failed?.Error.Message);
    });
}

if (graph is null)
{
    Save();
    return;
}

if (args.Contains("--compare-evaluation"))
{
    // Verifies that the shared invocation and the per-project processes describe every project identically.
    await Measure("evaluation_equivalence", async () =>
    {
        var reference = new WorkspaceProjectGraphEvaluator(new FileSystemFolderProjectProvider(), solutionProvider, perProjectEvaluation);
        var evaluated = await reference.EvaluateAsync(workspace!, timeout.Token);
        var expected = evaluated.IsSuccess ? await reference.ResolveCompilerInputsAsync(evaluated.Value, timeout.Token) : evaluated;
        if (!expected.IsSuccess) return (false, 0, expected.Error.Message);
        var catalog = await projects.GetProjectsAsync(workspace!, timeout.Token);
        var different = new List<string>();
        for (var i = 0; i < graph.Projects.Count; i++)
        {
            if (JsonSerializer.Serialize(evaluated.Value!.Projects[i]) != JsonSerializer.Serialize(catalog.Value![i]))
                different.Add($"evaluation:{graph.Projects[i].Path}");
            // Diagnostics text is compared by code; the shared invocation reports the same MSBuild errors line by line.
            var left = expected.Value.Projects[i]; var right = graph.Projects[i];
            if (JsonSerializer.Serialize(left with { Metadata = left.Metadata with { CompilerInputsError = default } })
                != JsonSerializer.Serialize(right with { Metadata = right.Metadata with { CompilerInputsError = default } })
                || left.Metadata.CompilerInputsError.Code != right.Metadata.CompilerInputsError.Code)
                different.Add($"compiler-inputs:{graph.Projects[i].Path}");
        }
        var messages = Enumerable.Range(0, graph.Projects.Count)
            .Where(i => expected.Value.Projects[i].Metadata.CompilerInputsError.Message != graph.Projects[i].Metadata.CompilerInputsError.Message)
            .ToArray();
        Console.WriteLine($"Projects with a compiler-input error: {graph.Projects.Count(static project => !project.Metadata.CompilerInputsError.IsNone)}; differing error text: {messages.Length}");
        foreach (var i in messages.Take(1))
            Console.WriteLine($"per-project: {expected.Value.Projects[i].Metadata.CompilerInputsError.Message}\nshared: {graph.Projects[i].Metadata.CompilerInputsError.Message}");
        return (different.Count == 0, graph.Projects.Count - different.Count, different.Count == 0 ? null : string.Join("; ", different.Take(5)));
    });
}

var scope = new WorkspaceProblemsScopeService(classifier, projects, files);
for (var i = 0; i < 3; i++) await Measure("problems_scope", async () =>
{
    var result = await scope.BuildAsync(solution, timeout.Token);
    return (result.IsSuccess, result.Value?.Projects.Count ?? 0, result.IsFailure ? result.Error.Message : null);
});

var contexts = new CSharpSemanticContextProvider(classifier, projects, files, documentStore);
var activeDocumentName = Array.IndexOf(args, "--active-document") is var option and >= 0 && option + 1 < args.Length
    ? args[option + 1]
    : "ContentItem.cs";
var activePath = graph.Projects
    .SelectMany(static project => project.Metadata.SourcePaths)
    .First(path => Path.GetFileName(path) == activeDocumentName);
var activeDocument = new CSharpSourceDocument(activePath, File.ReadAllText(activePath));
// One editor request (diagnostics, hover, completion) for the active document.
for (var i = 0; i < 4; i++) await Measure(i == 0 ? "semantic_context_active_cold" : "semantic_context_active_warm", async () =>
{
    var context = await contexts.CreateAsync(solution, activeDocument, [activeDocument], timeout.Token);
    return (context is not null, context?.Documents.Count ?? 0, context is null ? "No context" : null);
});
for (var i = 0; i < 2; i++) await Measure("semantic_context_workspace_warm", async () =>
{
    var result = await contexts.CreateWorkspaceProjectContextsAsync(solution, [activeDocument], timeout.Token);
    return (result is not null && result.ProjectSystemError.IsNone, result?.ProjectContexts.Count ?? 0,
        result is null ? "No contexts" : result.ProjectSystemError.IsNone ? null : result.ProjectSystemError.Message);
});

// Roslyn analysis on top of the contexts: what one diagnostics pass costs once project data is ready.
var diagnostics = new RoslynCSharpDiagnosticService();
for (var i = 0; i < 5; i++) await Measure("diagnostics_active", async () =>
{
    var context = await contexts.CreateAsync(solution, activeDocument, [activeDocument], timeout.Token);
    if (context is null) return (false, 0, "No context");
    return (true, (await diagnostics.AnalyzeAsync(context, timeout.Token)).Count, (string?)null);
});
for (var i = 0; i < 2; i++) await Measure("diagnostics_workspace", async () =>
{
    var result = await contexts.CreateWorkspaceProjectContextsAsync(solution, [activeDocument], timeout.Token);
    if (result is null) return (false, 0, "No contexts");
    var count = 0;
    foreach (var project in result.ProjectContexts)
        count += (await diagnostics.AnalyzeDocumentsAsync(project.SemanticContext, project.DocumentPaths, timeout.Token)).Sum(static document => document.Diagnostics.Count);
    return (result.ProjectSystemError.IsNone, count, result.ProjectSystemError.IsNone ? null : result.ProjectSystemError.Message);
});

if (args.Contains("--compare-diagnostics"))
{
    // Verifies that analyzing one document alone reports what whole-project analysis reports for it.
    await Measure("diagnostics_equivalence", async () =>
    {
        var result = await contexts.CreateWorkspaceProjectContextsAsync(solution, [], timeout.Token);
        if (result is null) return (false, 0, "No contexts");
        var compared = 0;
        var different = new List<string>();
        foreach (var project in result.ProjectContexts)
        {
            var whole = await diagnostics.AnalyzeDocumentsAsync(project.SemanticContext, project.DocumentPaths, timeout.Token);
            foreach (var document in whole)
            {
                var alone = await diagnostics.AnalyzeAsync(project.SemanticContext with { ActiveDocumentPath = document.FilePath }, timeout.Token);
                compared++;
                if (JsonSerializer.Serialize(alone) != JsonSerializer.Serialize(document.Diagnostics))
                {
                    different.Add($"{document.FilePath}: whole [{string.Join(",", document.Diagnostics.Select(static d => $"{d.Id}@{d.StartLine}"))}] alone [{string.Join(",", alone.Select(static d => $"{d.Id}@{d.StartLine}"))}]");
                }
            }
        }
        Console.WriteLine($"Documents compared: {compared}; differing: {different.Count}");
        foreach (var line in different.Take(12)) Console.WriteLine($"  {line}");
        return (different.Count == 0, compared, different.Count == 0 ? null : $"{different.Count} documents differ");
    });
}

Save();

sealed record Sample(string Phase, double Milliseconds, bool Success, int ResultCount, int ProcessCalls, double AllocatedMb, string? Error);

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
