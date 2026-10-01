using System.Text.Json;
using NUnit.Framework;
using Toren.Core.Execution.Contracts;
using Toren.Core.Execution.Models;
using Toren.Core.Results;
using Toren.UnitTests.TestDoubles;
using Toren.Workspaces.Adapters;
using Toren.Workspaces.Models;
using Toren.Workspaces.Services;

namespace Toren.UnitTests.Workspaces;

[TestFixture]
public sealed class MsBuildProjectEvaluationProviderTests
{
    [Test]
    public async Task ReadsEvaluatedProjectMetadata()
    {
        var runner = new FakeProcessRunner(
            "{\"Properties\":{\"TargetFramework\":\"\",\"TargetFrameworks\":\"net8.0;net10.0\",\"OutputType\":\"Exe\",\"AssemblyName\":\"ParcelBox.Api\",\"RootNamespace\":\"ParcelBox.Api\",\"IsTestProject\":\"false\",\"ManagePackageVersionsCentrally\":\"true\",\"DirectoryBuildPropsPath\":\"/repo/Directory.Build.props\",\"DirectoryBuildTargetsPath\":\"/repo/Directory.Build.targets\",\"DirectoryPackagesPropsPath\":\"/repo/Directory.Packages.props\",\"ProjectAssetsFile\":\"/repo/src/ParcelBox.Api/obj/project.assets.json\"},\"Items\":{\"Analyzer\":[{\"Identity\":\"ParcelBox.Analyzers.dll\",\"FullPath\":\"/repo/.nuget/analyzers/ParcelBox.Analyzers.dll\"}]}}");
        var provider = new MsBuildProjectEvaluationProvider(runner);

        var result = await EvaluateAsync(provider, "/repo/src/ParcelBox.Api/ParcelBox.Api.csproj");

        Assert.That(result.IsSuccess, Is.True);
        var metadata = result.Value!.Metadata;
        Assert.Multiple(() =>
        {
            Assert.That(string.Join("|", metadata.TargetFrameworks), Is.EqualTo("net8.0|net10.0"));
            Assert.That(metadata.OutputType, Is.EqualTo("Exe"));
            Assert.That(metadata.AssemblyName, Is.EqualTo("ParcelBox.Api"));
            Assert.That(metadata.RootNamespace, Is.EqualTo("ParcelBox.Api"));
            Assert.That(metadata.IsTestProject, Is.False);
            Assert.That(metadata.UsesCentralPackageManagement, Is.True);
            Assert.That(metadata.DirectoryBuildPropsPath, Is.EqualTo("/repo/Directory.Build.props"));
            Assert.That(metadata.DirectoryBuildTargetsPath, Is.EqualTo("/repo/Directory.Build.targets"));
            Assert.That(metadata.DirectoryPackagesPropsPath, Is.EqualTo("/repo/Directory.Packages.props"));
            Assert.That(metadata.ProjectAssetsFilePath,
                Is.EqualTo(Path.GetFullPath("/repo/src/ParcelBox.Api/obj/project.assets.json")));
            Assert.That(metadata.AnalyzerPaths, Has.Count.EqualTo(1));
            Assert.That(Path.GetFileName(metadata.AnalyzerPaths[0]), Is.EqualTo("ParcelBox.Analyzers.dll"));
            Assert.That(metadata.ReferencePaths, Is.Null);
            Assert.That(metadata.CompilerInputsError.IsNone, Is.True);
            Assert.That(
                string.Join("|", runner.LastRequest!.Arguments),
                Does.Contain("-getProperty:TargetFramework,TargetFrameworks,OutputType,AssemblyName,RootNamespace,IsTestProject,ManagePackageVersionsCentrally,DirectoryBuildPropsPath,DirectoryBuildTargetsPath,DirectoryPackagesPropsPath"));
            Assert.That(string.Join("|", runner.LastRequest.Arguments), Does.Contain("-getItem:Analyzer"));
        });
    }

    [Test]
    public async Task ReadsDeclaredReferencesFromTheSameEvaluationAsTheMetadata()
    {
        var project = Path.Combine(Path.GetTempPath(), "App", "App.csproj");
        var runner = new FakeProcessRunner(
            "{\"Properties\":{\"TargetFramework\":\"net10.0\"},\"Items\":{\"ProjectReference\":[{\"Identity\":\"../Lib/ParcelBox.Application.csproj\"}],\"PackageReference\":[{\"Identity\":\"NUnit\"}],\"FrameworkReference\":[{\"Identity\":\"Microsoft.AspNetCore.App\"}]}}");

        var result = await EvaluateAsync(new MsBuildProjectEvaluationProvider(runner), project);

        Assert.That(result.IsSuccess, Is.True);
        var references = result.Value!.References;
        Assert.Multiple(() =>
        {
            Assert.That(
                string.Join(",", references.Select(reference => $"{reference.Kind}:{reference.Identity}")),
                Is.EqualTo("Project:../Lib/ParcelBox.Application.csproj,Package:NUnit,Framework:Microsoft.AspNetCore.App"));
            Assert.That(references[0].ResolvedPath, Is.EqualTo(Path.GetFullPath(
                Path.Combine(Path.GetTempPath(), "Lib", "ParcelBox.Application.csproj"))));
            Assert.That(references[1].ResolvedPath, Is.Null);
            Assert.That(runner.Requests, Has.Count.EqualTo(1));
            Assert.That(
                string.Join("|", runner.LastRequest!.Arguments),
                Does.Contain("ProjectReference,PackageReference,FrameworkReference"));
            Assert.That(string.Join("|", runner.LastRequest.Arguments), Does.Not.Contain("-target:"));
        });
    }

    [Test]
    public async Task ReadsCompilerInputsIncludingGlobalUsingsAndCompilationOptions()
    {
        var runner = new FakeProcessRunner("""
            {"Properties":{"TargetFramework":"net10.0","OutputType":"Exe","Nullable":"enable","LangVersion":"14.0","DefineConstants":"DEBUG;NET10_0","AllowUnsafeBlocks":"true"},
             "Items":{"Compile":[{"FullPath":"/repo/Program.cs"}],"Using":[{"Identity":"System"},{"Identity":"System.Math","Static":"true"},{"Identity":"System.String","Alias":"Text"}],"ReferencePath":[]}}
            """);
        var provider = new MsBuildProjectEvaluationProvider(runner);

        var evaluation = await EvaluateAsync(provider, "/repo/App.csproj");
        Assert.That(evaluation.IsSuccess, Is.True);
        var result = await ResolveAsync(provider, "/repo/App.csproj", evaluation.Value!.Metadata);

        Assert.That(result.IsSuccess, Is.True);
        Assert.Multiple(() =>
        {
            foreach (var metadata in new[] { evaluation.Value.Metadata, result.Value! })
            {
                Assert.That(metadata.SourcePaths, Is.EqualTo(new[] { Path.GetFullPath("/repo/Program.cs") }));
                Assert.That(string.Join("|", metadata.GlobalUsings), Is.EqualTo("global using System;|global using static System.Math;|global using Text = System.String;"));
                Assert.That(metadata.Nullable, Is.EqualTo("enable"));
                Assert.That(metadata.LanguageVersion, Is.EqualTo("14.0"));
                Assert.That(string.Join("|", metadata.DefineConstants), Is.EqualTo("DEBUG|NET10_0"));
                Assert.That(metadata.AllowUnsafe, Is.True);
            }

            Assert.That(result.Value!.ReferencePaths, Is.Empty);
            Assert.That(runner.Requests, Has.Count.EqualTo(2));
        });
    }

    [Test]
    public async Task UsesSingleTargetFrameworkWhenMultiTargetPropertyIsEmpty()
    {
        var runner = new FakeProcessRunner(
            "{\"Properties\":{\"TargetFramework\":\"net10.0\",\"TargetFrameworks\":\"\",\"IsTestProject\":\"true\"}}");
        var provider = new MsBuildProjectEvaluationProvider(runner);

        var result = await EvaluateAsync(provider, "/repo/tests/ParcelBox.Tests/ParcelBox.Tests.csproj");

        Assert.That(result.IsSuccess, Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(string.Join("|", result.Value!.Metadata.TargetFrameworks), Is.EqualTo("net10.0"));
            Assert.That(result.Value.Metadata.IsTestProject, Is.True);
            Assert.That(result.Value.Metadata.UsesCentralPackageManagement, Is.False);
            Assert.That(result.Value.Metadata.AnalyzerPaths, Is.Empty);
            Assert.That(result.Value.References, Is.Empty);
        });
    }

    [Test]
    public async Task ReportsInvalidEvaluationOutput()
    {
        var provider = new MsBuildProjectEvaluationProvider(new FakeProcessRunner("not-json"));

        var result = await EvaluateAsync(provider, "/repo/App.csproj");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo("workspace.project.metadata.evaluate.failed"));
            Assert.That(result.Error.Message, Does.Contain("invalid evaluation data"));
        });
    }

    [Test]
    public async Task ReportsMsBuildEvaluationFailure()
    {
        var provider = new MsBuildProjectEvaluationProvider(
            new FakeProcessRunner(string.Empty, exitCode: 1, standardError: "evaluation failed"));

        var result = await EvaluateAsync(provider, "/repo/App.csproj");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo("workspace.project.metadata.evaluate.failed"));
            Assert.That(result.Error.Message, Does.Contain("evaluation failed"));
        });
    }

    [Test]
    public async Task ReportsProcessStartFailureDuringEvaluation()
    {
        var provider = new MsBuildProjectEvaluationProvider(new UnavailableProcessRunner());

        var result = await EvaluateAsync(provider, "/repo/App.csproj");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo("workspace.project.metadata.evaluate.failed"));
            Assert.That(result.Error.Message, Does.Contain("dotnet is not installed"));
        });
    }

    [Test]
    public async Task ResolvesCompilerReferencePathsForFirstTargetFramework()
    {
        var referencePaths = new[] { typeof(object).Assembly.Location, typeof(FakeProcessRunner).Assembly.Location };
        var runner = new FakeProcessRunner(JsonSerializer.Serialize(new
        {
            Properties = new { TargetFramework = "net10.0", TargetFrameworks = "net10.0;net8.0" },
            Items = new { ReferencePath = referencePaths.Select(path => new { FullPath = path }).ToArray() },
        }));
        var provider = new MsBuildProjectEvaluationProvider(runner);

        var result = await ResolveAsync(provider, 
            "/repo/src/App/App.csproj",
            CreateEvaluatedMetadata("net10.0", "net8.0"));

        Assert.That(result.IsSuccess, Is.True);
        var arguments = string.Join("|", runner.LastRequest!.Arguments);
        Assert.Multiple(() =>
        {
            Assert.That(result.Value!.ReferencePaths, Is.EquivalentTo(referencePaths));
            Assert.That(result.Value.CompilerInputsError.IsNone, Is.True);
            Assert.That(ProjectCompilationReferenceResolver.Resolve(result.Value).IsSuccess, Is.True);
            Assert.That(string.Join("|", result.Value.TargetFrameworks), Is.EqualTo("net10.0|net8.0"));
            Assert.That(arguments, Does.Match(@"-target:[^|]*\bResolveReferences\b"));
            Assert.That(arguments, Does.Contain("-property:BuildProjectReferences=false"));
            Assert.That(arguments, Does.Contain("-property:TargetFramework=net10.0"));
            Assert.That(arguments, Does.Match(@"-getItem:[^|]*\bReferencePath\b"));
            Assert.That(runner.Requests, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task DoesNotSelectATargetFrameworkWhenEvaluationFoundNone()
    {
        var runner = new FakeProcessRunner("{\"Properties\":{},\"Items\":{\"ReferencePath\":[]}}");

        var result = await ResolveAsync(new MsBuildProjectEvaluationProvider(runner), "/repo/App.csproj", CreateEvaluatedMetadata());

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(string.Join("|", runner.LastRequest!.Arguments), Does.Not.Contain("TargetFramework="));
        });
    }

    [Test]
    public async Task UnrestoredProjectKeepsEvaluatedMetadataAndCarriesTheResolutionError()
    {
        var evaluated = CreateEvaluatedMetadata("net10.0");
        var provider = new MsBuildProjectEvaluationProvider(
            new FakeProcessRunner(string.Empty, exitCode: 1, standardError: "assets file not found"));

        var result = await ResolveAsync(provider, "/repo/App.csproj", evaluated);

        Assert.That(result.IsSuccess, Is.True);
        var references = ProjectCompilationReferenceResolver.Resolve(result.Value!);
        Assert.Multiple(() =>
        {
            Assert.That(result.Value!.OutputType, Is.EqualTo(evaluated.OutputType));
            Assert.That(result.Value.TargetFrameworks, Is.EqualTo(evaluated.TargetFrameworks));
            Assert.That(result.Value.ReferencePaths, Is.Null);
            Assert.That(references.IsFailure, Is.True);
            Assert.That(references.Error.Code, Is.EqualTo("workspace.project.compilation-references.resolve.failed"));
            Assert.That(references.Error.Message, Does.Contain("assets file not found"));
        });
    }

    [Test]
    public async Task ProcessStartFailureDuringCompilerInputResolutionIsCarriedByTheMetadata()
    {
        var result = await ResolveAsync(new MsBuildProjectEvaluationProvider(new UnavailableProcessRunner()), "/repo/App.csproj", CreateEvaluatedMetadata("net10.0"));

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value!.CompilerInputsError.Code,
                Is.EqualTo("workspace.project.compilation-references.resolve.failed"));
            Assert.That(result.Value.CompilerInputsError.Message, Does.Contain("dotnet is not installed"));
        });
    }

    [Test]
    public async Task ReportsMissingReferencePathItems()
    {
        var provider = new MsBuildProjectEvaluationProvider(
            new FakeProcessRunner("{\"Properties\":{\"TargetFramework\":\"net10.0\"}}"));

        var result = await ResolveAsync(provider, "/repo/App.csproj", CreateEvaluatedMetadata("net10.0"));

        Assert.That(result.IsSuccess, Is.True);
        var references = ProjectCompilationReferenceResolver.Resolve(result.Value!);
        Assert.Multiple(() =>
        {
            Assert.That(references.IsFailure, Is.True);
            Assert.That(references.Error.Code, Is.EqualTo("workspace.project.compilation-references.resolve.failed"));
            Assert.That(references.Error.Message, Does.Contain("ReferencePath"));
        });
    }

    [TestCase("{}")]
    [TestCase("[]")]
    [TestCase("not-json")]
    public async Task ReportsInvalidCompilerInputOutput(string output)
    {
        var provider = new MsBuildProjectEvaluationProvider(new FakeProcessRunner(output));

        var result = await ResolveAsync(provider, "/repo/App.csproj", CreateEvaluatedMetadata("net10.0"));

        Assert.Multiple(() =>
        {
            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo("workspace.project.metadata.evaluate.failed"));
        });
    }

    [Test]
    public void CancellationIsObservedBeforeStartingAProcess()
    {
        var runner = new FakeProcessRunner("{}");
        var provider = new MsBuildProjectEvaluationProvider(runner);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.Multiple(() =>
        {
            Assert.CatchAsync<OperationCanceledException>(
                async () => await EvaluateAsync(provider, "/repo/App.csproj", cancellation.Token));
            Assert.CatchAsync<OperationCanceledException>(
                async () => await ResolveAsync(provider, 
                    "/repo/App.csproj", CreateEvaluatedMetadata("net10.0"), cancellation.Token));
            Assert.That(runner.Requests, Is.Empty);
        });
    }

    [Test]
    public async Task LargeSolutionOverlapsEvaluationWithinABoundAndKeepsSolutionOrder()
    {
        var paths = Enumerable.Range(0, 12).Select(index => Path.Combine(Path.GetTempPath(), $"Project{index}.csproj")).ToArray();
        const int bound = 3;
        var runner = new GatedProcessRunner(bound);
        var provider = new MsBuildProjectEvaluationProvider(runner, maxConcurrency: bound);

        var pending = provider.EvaluateAsync(paths);
        await runner.BoundReached.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.That(runner.Started, Is.EqualTo(bound));
        runner.Release.SetResult();
        var result = await pending;

        Assert.That(result.IsSuccess, Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(result.Value!.Select(evaluation => evaluation.Metadata.AssemblyName),
                Is.EqualTo(paths.Select(Path.GetFileNameWithoutExtension)));
            Assert.That(runner.MaximumActive, Is.EqualTo(bound));
            Assert.That(runner.Active, Is.Zero);
            Assert.That(runner.Started, Is.EqualTo(paths.Length));
        });
    }

    [Test]
    public async Task CancellingParallelEvaluationDrainsAllStartedWork()
    {
        var paths = Enumerable.Range(0, 12).Select(index => Path.Combine(Path.GetTempPath(), $"Project{index}.csproj")).ToArray();
        var runner = new GatedProcessRunner(4);
        var provider = new MsBuildProjectEvaluationProvider(runner, maxConcurrency: 4);
        using var cancellation = new CancellationTokenSource();

        var pending = provider.EvaluateAsync(paths, cancellation.Token);
        await runner.BoundReached.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();

        Assert.CatchAsync<OperationCanceledException>(async () => await pending);
        Assert.Multiple(() =>
        {
            Assert.That(runner.Active, Is.Zero);
            Assert.That(runner.Started, Is.EqualTo(4));
        });
    }

    [Test]
    public async Task FirstFailingProjectInOrderIsReportedAndLaterProjectsAreNotStarted()
    {
        var paths = Enumerable.Range(0, 40).Select(index => Path.Combine(Path.GetTempPath(), $"Project{index:00}.csproj")).ToArray();
        var runner = new GatedProcessRunner(bound: 3, failingProjects: [paths[2], paths[3], paths[30]]);
        var provider = new MsBuildProjectEvaluationProvider(runner, maxConcurrency: 4);

        // Two projects are in flight when the third fails; nothing after it is started.
        var pending = provider.EvaluateAsync(paths);
        await runner.BoundReached.Task.WaitAsync(TimeSpan.FromSeconds(5));
        runner.Release.SetResult();
        var result = await pending;

        Assert.Multiple(() =>
        {
            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo("workspace.project.metadata.evaluate.failed"));
            Assert.That(result.Error.Message, Does.Contain($"failed: {paths[2]}"));
            Assert.That(runner.Projects, Is.EquivalentTo(paths.Take(3)));
        });
    }

    [Test]
    public async Task UnrestoredProjectsDoNotStopCompilerInputResolutionOfTheOthers()
    {
        var paths = Enumerable.Range(0, 6).Select(index => Path.Combine(Path.GetTempPath(), $"Project{index}.csproj")).ToArray();
        var runner = new GatedProcessRunner(bound: 1, failingProjects: [paths[1], paths[4]]);
        runner.Release.SetResult();
        var provider = new MsBuildProjectEvaluationProvider(runner, maxConcurrency: 2);

        var result = await provider.ResolveCompilerInputsAsync(
            paths.Select(path => new WorkspaceProject(path, "Project", CreateEvaluatedMetadata("net10.0"), [])).ToArray());

        Assert.That(result.IsSuccess, Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(
                string.Join(",", result.Value!.Select(metadata => metadata.CompilerInputsError.IsNone ? "ok" : "error")),
                Is.EqualTo("ok,error,ok,ok,error,ok"));
            Assert.That(result.Value![1].CompilerInputsError.Message, Does.Contain($"failed: {paths[1]}"));
            Assert.That(runner.Started, Is.EqualTo(paths.Length));
        });
    }

    [Test]
    public void UnexpectedRunnerFaultPropagatesAfterStartedWorkStops()
    {
        var paths = Enumerable.Range(0, 12).Select(index => Path.Combine(Path.GetTempPath(), $"Project{index}.csproj")).ToArray();
        // The first project faults before any other worker has claimed one.
        var runner = new GatedProcessRunner(bound: 1, throwingProject: paths[0]);
        runner.Release.SetResult();
        var provider = new MsBuildProjectEvaluationProvider(runner, maxConcurrency: 4);

        Assert.ThrowsAsync<InvalidOperationException>(async () => await provider.EvaluateAsync(paths));
        Assert.Multiple(() =>
        {
            Assert.That(runner.Active, Is.Zero);
            Assert.That(runner.Started, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task EmptyProjectListNeedsNoProcess()
    {
        var runner = new FakeProcessRunner("{}");
        var provider = new MsBuildProjectEvaluationProvider(runner);

        var evaluations = await provider.EvaluateAsync([]);
        var metadata = await provider.ResolveCompilerInputsAsync([]);

        Assert.Multiple(() =>
        {
            Assert.That(evaluations.Value, Is.Empty);
            Assert.That(metadata.Value, Is.Empty);
            Assert.That(runner.Requests, Is.Empty);
        });
    }

    /// <summary>Stands in for <c>dotnet msbuild</c>: holds each request until released.</summary>
    private sealed class GatedProcessRunner(
        int bound,
        IReadOnlyCollection<string>? failingProjects = null,
        string? throwingProject = null) : IProcessRunner
    {
        private int _started;
        private int _active;
        private int _maximum;

        public TaskCompletionSource BoundReached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public System.Collections.Concurrent.ConcurrentBag<string> Projects { get; } = [];

        public int Started => Volatile.Read(ref _started);

        public int Active => Volatile.Read(ref _active);

        public int MaximumActive => Volatile.Read(ref _maximum);

        public async Task<Result<ProcessResult>> RunAsync(
            ProcessRequest request,
            CancellationToken cancellationToken = default)
        {
            var projectPath = request.Arguments[1];
            Projects.Add(projectPath);
            var active = Interlocked.Increment(ref _active);
            int previous;
            do
            {
                previous = _maximum;
            }
            while (active > previous && Interlocked.CompareExchange(ref _maximum, active, previous) != previous);

            if (Interlocked.Increment(ref _started) == bound)
            {
                BoundReached.TrySetResult();
            }

            try
            {
                if (projectPath == throwingProject)
                {
                    throw new InvalidOperationException("Runner invariant broken.");
                }

                if (failingProjects?.Contains(projectPath) == true)
                {
                    return Result.Success(new ProcessResult(1, string.Empty, $"failed: {projectPath}"));
                }

                await Release.Task.WaitAsync(cancellationToken);
                await Task.Yield();
                return Result.Success(new ProcessResult(
                        0,
                        JsonSerializer.Serialize(new
                        {
                            Properties = new { TargetFramework = "net10.0", AssemblyName = Path.GetFileNameWithoutExtension(projectPath) },
                            Items = new { ReferencePath = Array.Empty<object>() },
                        }),
                        string.Empty));
            }
            finally
            {
                Interlocked.Decrement(ref _active);
            }
        }
    }

    private static async Task<Result<ProjectEvaluation>> EvaluateAsync(
        MsBuildProjectEvaluationProvider provider,
        string projectPath,
        CancellationToken cancellationToken = default)
    {
        var result = await provider.EvaluateAsync([projectPath], cancellationToken);
        return result.IsSuccess
            ? Result.Success(result.Value.Single())
            : Result.Failure<ProjectEvaluation>(result.Error);
    }

    private static async Task<Result<ProjectMetadata>> ResolveAsync(
        MsBuildProjectEvaluationProvider provider,
        string projectPath,
        ProjectMetadata evaluated,
        CancellationToken cancellationToken = default)
    {
        var result = await provider.ResolveCompilerInputsAsync(
            [new WorkspaceProject(projectPath, "App", evaluated, [])],
            cancellationToken);
        return result.IsSuccess
            ? Result.Success(result.Value.Single())
            : Result.Failure<ProjectMetadata>(result.Error);
    }

    private static ProjectMetadata CreateEvaluatedMetadata(params string[] targetFrameworks) =>
        new(targetFrameworks, "Exe", "App", "App", false, false, null, null, null);

    private sealed class UnavailableProcessRunner : IProcessRunner
    {
        public Task<Result<ProcessResult>> RunAsync(
            ProcessRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Failure<ProcessResult>(
                OperationError.Create("process.start.failed", "dotnet is not installed")));
    }
}
