using System.Text.RegularExpressions;
using System.Xml.Linq;
using NUnit.Framework;
using Toren.Core.Execution.Contracts;
using Toren.Core.Execution.Models;
using Toren.Core.Results;
using Toren.Workspaces.Adapters;
using Toren.Workspaces.Contracts;
using Toren.Workspaces.Models;

namespace Toren.UnitTests.Workspaces;

[TestFixture]
public sealed partial class MsBuildBatchProjectEvaluationProviderTests
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "toren-batch-tests");
    private static readonly string App = Path.Combine(Root, "App", "App.csproj");
    private static readonly string Library = Path.Combine(Root, "Library", "Library.csproj");
    private static readonly string Tests = Path.Combine(Root, "Tests", "Tests.csproj");

    [Test]
    public async Task EvaluatesAllProjectsInOneInvocationAndKeepsTheirOrder()
    {
        var runner = BatchRunner.Completing(invocation =>
        {
            // MSBuild finishes projects in any order; results are matched by project path.
            invocation.WriteProject(Tests, "Property\tTargetFramework\tnet10.0", "Property\tIsTestProject\ttrue");
            invocation.WriteProject(Library, "Property\tTargetFrameworks\tnet8.0;net10.0", "Property\tOutputType\tLibrary");
            invocation.WriteProject(
                App,
                "Property\tTargetFramework\tnet10.0",
                "Property\tOutputType\tExe",
                "Property\tDefineConstants\tDEBUG;TRACE",
                "Property\tProjectAssetsFile\t" + Path.Combine(Root, "App", "obj", "project.assets.json"),
                "Compile\t" + Path.Combine(Root, "App", "Program.cs"),
                "Compile\t" + Path.Combine(Root, "App", "Program.cs"),
                "Compile\t" + Path.Combine(Root, "App", "A.cs"),
                "Using\tSystem\t\t",
                "Using\tSystem.Math\ttrue\t",
                "Using\tSystem.String\t\tText",
                "ProjectReference\t../Library/Library.csproj",
                "PackageReference\tNUnit",
                "FrameworkReference\tMicrosoft.AspNetCore.App");
            return new ProcessResult(0, string.Empty, string.Empty);
        });
        var fallback = new RecordingFallbackProvider();
        var provider = new MsBuildBatchProjectEvaluationProvider(runner, fallback, maxNodes: 3);

        var result = await provider.EvaluateAsync([App, Library, Tests]);

        Assert.That(result.IsSuccess, Is.True);
        var invocation = runner.Invocations.Single();
        Assert.Multiple(() =>
        {
            Assert.That(result.Value!, Has.Count.EqualTo(3));
            Assert.That(result.Value![0].Metadata.OutputType, Is.EqualTo("Exe"));
            Assert.That(string.Join("|", result.Value[0].Metadata.DefineConstants), Is.EqualTo("DEBUG|TRACE"));
            Assert.That(result.Value[0].Metadata.SourcePaths, Is.EqualTo(new[]
            {
                Path.Combine(Root, "App", "A.cs"),
                Path.Combine(Root, "App", "Program.cs"),
            }));
            Assert.That(result.Value[0].Metadata.ProjectAssetsFilePath,
                Is.EqualTo(Path.Combine(Root, "App", "obj", "project.assets.json")));
            Assert.That(string.Join("|", result.Value[0].Metadata.GlobalUsings),
                Is.EqualTo("global using System;|global using static System.Math;|global using Text = System.String;"));
            Assert.That(result.Value[0].Metadata.ReferencePaths, Is.Null);
            Assert.That(
                string.Join(",", result.Value[0].References.Select(reference => $"{reference.Kind}:{reference.Identity}")),
                Is.EqualTo("Project:../Library/Library.csproj,Package:NUnit,Framework:Microsoft.AspNetCore.App"));
            Assert.That(result.Value[0].References[0].ResolvedPath, Is.EqualTo(Library));
            Assert.That(string.Join("|", result.Value[1].Metadata.TargetFrameworks), Is.EqualTo("net8.0|net10.0"));
            Assert.That(result.Value[2].Metadata.IsTestProject, Is.True);

            Assert.That(
                string.Join("|", invocation.Arguments),
                Is.EqualTo($"msbuild|{invocation.TraversalPath}|-nologo|-verbosity:quiet|-target:Evaluate|-maxcpucount:3|-nodeReuse:false"));
            Assert.That(invocation.Projects.Select(project => project.Path), Is.EqualTo(new[] { App, Library, Tests }));
            Assert.That(invocation.Projects.Select(project => project.AdditionalProperties), Is.All.Null);
            Assert.That(invocation.Targets, Is.EqualTo("_TorenCollectEvaluation"));
            Assert.That(
                string.Join("|", invocation.Properties.Keys),
                Is.EqualTo("CustomBeforeMicrosoftCommonTargets|CustomBeforeMicrosoftCommonCrossTargetingTargets|_TorenOutputDirectory"));
            Assert.That(invocation.TargetsFileExisted, Is.True);
            Assert.That(invocation.ContinueOnError, Is.True);
            Assert.That(Directory.Exists(Path.GetDirectoryName(invocation.TraversalPath)), Is.False);
            Assert.That(fallback.EvaluatedProjects, Is.Empty);
        });
    }

    [Test]
    public async Task ProjectsWithoutACompleteResultAreEvaluatedIndividually()
    {
        var runner = BatchRunner.Completing(invocation =>
        {
            invocation.WriteProject(App, "Property\tTargetFramework\tnet10.0");
            // Cut short, as when a node is killed while writing.
            invocation.WriteRaw("Project\t" + Library, "Property\tTargetFramework\tnet10.0");
            return new ProcessResult(1, string.Empty, string.Empty);
        });
        var fallback = new RecordingFallbackProvider();
        var provider = new MsBuildBatchProjectEvaluationProvider(runner, fallback);

        var result = await provider.EvaluateAsync([App, Library, Tests]);

        Assert.That(result.IsSuccess, Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(result.Value!.Select(evaluation => evaluation.Metadata.AssemblyName),
                Is.EqualTo(new string?[] { null, "fallback:Library", "fallback:Tests" }));
            Assert.That(fallback.EvaluatedProjects, Is.EqualTo(new[] { Library, Tests }));
            Assert.That(runner.Invocations, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task InvocationRunsNextToTheFirstProjectSoItsGlobalJsonSelectsTheSdk()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"toren-batch-directory-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(directory, "First"));
        try
        {
            var first = Path.Combine(directory, "First", "First.csproj");
            var runner = BatchRunner.Completing(_ => new ProcessResult(0, string.Empty, string.Empty));
            var provider = new MsBuildBatchProjectEvaluationProvider(runner, new RecordingFallbackProvider());

            await provider.EvaluateAsync([first, App]);
            await provider.EvaluateAsync([App, first]);

            Assert.Multiple(() =>
            {
                Assert.That(runner.Invocations[0].WorkingDirectory, Is.EqualTo(Path.Combine(directory, "First")));
                Assert.That(runner.Invocations[1].WorkingDirectory, Is.Null);
            });
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public async Task ProjectsDeclaringInitialTargetsAreEvaluatedWithoutRunningTargets()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"toren-initial-targets-{Guid.NewGuid():N}");
        var plain = Path.Combine(directory, "Plain", "Plain.csproj");
        var own = Path.Combine(directory, "Own", "Own.csproj");
        var inherited = Path.Combine(directory, "Inherited", "Inherited.csproj");
        var props = Path.Combine(directory, "Inherited", "Directory.Build.props");
        foreach (var project in new[] { plain, own, inherited })
        {
            Directory.CreateDirectory(Path.GetDirectoryName(project)!);
        }

        await File.WriteAllTextAsync(plain, "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        await File.WriteAllTextAsync(own, "<!-- comment -->\n<Project Sdk=\"Microsoft.NET.Sdk\" InitialTargets=\"Prepare\" />");
        await File.WriteAllTextAsync(inherited, "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        await File.WriteAllTextAsync(props, "<Project InitialTargets=\"Prepare\"><PropertyGroup /></Project>");
        try
        {
            var runner = BatchRunner.Completing(invocation =>
            {
                invocation.WriteProject(plain, "Property\tAssemblyName\tPlain");
                invocation.WriteProject(own, "Property\tAssemblyName\tOwn");
                invocation.WriteProject(inherited, "Property\tAssemblyName\tInherited", "Property\tDirectoryBuildPropsPath\t" + props);
                return new ProcessResult(0, string.Empty, string.Empty);
            });
            var fallback = new RecordingFallbackProvider();
            var provider = new MsBuildBatchProjectEvaluationProvider(runner, fallback);

            var result = await provider.EvaluateAsync([plain, own, inherited]);

            Assert.That(result.IsSuccess, Is.True);
            Assert.Multiple(() =>
            {
                Assert.That(
                    string.Join("|", result.Value!.Select(evaluation => evaluation.Metadata.AssemblyName)),
                    Is.EqualTo("Plain|fallback:Own|fallback:Inherited"));
                Assert.That(fallback.EvaluatedProjects, Is.EqualTo(new[] { own, inherited }));
            });
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public async Task FailureOfAnIndividuallyEvaluatedProjectIsReported()
    {
        var error = OperationError.Create("workspace.project.metadata.evaluate.failed", "Library is malformed.");
        var runner = BatchRunner.Completing(invocation =>
        {
            invocation.WriteProject(App, "Property\tTargetFramework\tnet10.0");
            return new ProcessResult(0, string.Empty, string.Empty);
        });
        var provider = new MsBuildBatchProjectEvaluationProvider(runner, new RecordingFallbackProvider(error));

        var result = await provider.EvaluateAsync([App, Library]);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error, Is.EqualTo(error));
        });
    }

    [Test]
    public async Task ResolvesCompilerInputsWithTheFirstTargetFrameworkOfEachProject()
    {
        var system = typeof(object).Assembly.Location;
        var runner = BatchRunner.Completing(invocation =>
        {
            invocation.WriteProject(
                App,
                "Property\tTargetFramework\tnet10.0",
                "Property\tNullable\tenable",
                "Property\tGeneratedMSBuildEditorConfigFile\t" + Path.Combine(Root, "App", "obj", "App.editorconfig"),
                "Analyzer\t" + Path.Combine(Root, "analyzers", "Analyzer.dll"),
                "EditorConfigFiles\t" + Path.Combine(Root, ".editorconfig"),
                "AdditionalFiles\t" + Path.Combine(Root, "App", "stylecop.json"),
                "Items\tReferencePath",
                "ReferencePath\t" + system,
                "ReferencePath\t" + Path.Combine(Root, "Library", "bin", "Library.dll"),
                "ReferencePath\t" + system);
            invocation.WriteProject(Library, "Property\tTargetFramework\tnet8.0", "Items\tReferencePath");
            invocation.WriteProject(Tests, "Property\tOutputType\tLibrary");
            return new ProcessResult(0, string.Empty, string.Empty);
        });
        var fallback = new RecordingFallbackProvider();
        var provider = new MsBuildBatchProjectEvaluationProvider(runner, fallback);

        var result = await provider.ResolveCompilerInputsAsync(
        [
            CreateProject(App, "net10.0"),
            CreateProject(Library, "net8.0", "net10.0"),
            CreateProject(Tests),
        ]);

        Assert.That(result.IsSuccess, Is.True);
        var invocation = runner.Invocations.Single();
        Assert.Multiple(() =>
        {
            Assert.That(result.Value![0].Nullable, Is.EqualTo("enable"));
            Assert.That(result.Value[0].ReferencePaths, Is.EquivalentTo(new[]
            {
                system, Path.Combine(Root, "Library", "bin", "Library.dll"),
            }));
            Assert.That(result.Value[0].ReferencePaths, Is.Ordered.Using((IComparer<string>)Toren.Core.IO.FileSystemPath.Comparer));
            Assert.That(result.Value[0].AnalyzerPaths, Is.EqualTo(new[] { Path.Combine(Root, "analyzers", "Analyzer.dll") }));
            Assert.That(result.Value[0].AdditionalFilePaths, Is.EqualTo(new[] { Path.Combine(Root, "App", "stylecop.json") }));
            Assert.That(result.Value[0].AnalyzerConfigPaths, Is.EqualTo(new[]
            {
                Path.Combine(Root, ".editorconfig"), Path.Combine(Root, "App", "obj", "App.editorconfig"),
            }));
            Assert.That(result.Value[0].CompilerInputsError.IsNone, Is.True);
            // An empty reference group is still a resolved one.
            Assert.That(result.Value[1].ReferencePaths, Is.Empty);
            Assert.That(result.Value[1].CompilerInputsError.IsNone, Is.True);
            // Design-time output without the reference group is reported like the per-project query reports it.
            Assert.That(result.Value[2].ReferencePaths, Is.Null);
            Assert.That(result.Value[2].CompilerInputsError.Message, Does.Contain("ReferencePath"));

            Assert.That(invocation.Targets, Is.EqualTo("_TorenCollectCompilerInputs"));
            Assert.That(invocation.Projects.Select(project => project.AdditionalProperties),
                Is.EqualTo(new[] { "TargetFramework=net10.0", "TargetFramework=net8.0", null }));
            Assert.That(invocation.Properties["BuildProjectReferences"], Is.EqualTo("false"));
            Assert.That(invocation.Properties["_TorenCompilerInputs"], Is.EqualTo("true"));
            Assert.That(fallback.CompilerInputProjects, Is.Empty);
        });
    }

    [Test]
    public async Task UnrestoredProjectsCarryTheirOwnMsBuildErrorsWithoutFurtherProcesses()
    {
        var evaluated = CreateProject(Library, "net10.0");
        var runner = BatchRunner.Completing(invocation =>
        {
            invocation.WriteProject(App, "Property\tTargetFramework\tnet10.0", "Items\tReferencePath");
            return new ProcessResult(
                0,
                $"""
                /sdk/Microsoft.PackageDependencyResolution.targets(266,5): error NETSDK1004: Assets file 'project.assets.json' not found. Run a NuGet package restore to generate this file. [{Library}::TargetFramework=net10.0]
                /sdk/Other.targets(1,1): warning XYZ0001: unrelated [not-a-project]
                {Tests}(3,5): error MSB4057: The target "PrepareForBuild" does not exist in the project. [{Tests}]
                """,
                string.Empty);
        });
        var fallback = new RecordingFallbackProvider();
        var provider = new MsBuildBatchProjectEvaluationProvider(runner, fallback);

        var result = await provider.ResolveCompilerInputsAsync([CreateProject(App, "net10.0"), evaluated, CreateProject(Tests)]);

        Assert.That(result.IsSuccess, Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(result.Value![0].CompilerInputsError.IsNone, Is.True);
            Assert.That(result.Value[1].CompilerInputsError.Code,
                Is.EqualTo("workspace.project.compilation-references.resolve.failed"));
            Assert.That(result.Value[1].CompilerInputsError.Message, Does.Contain("NETSDK1004"));
            Assert.That(result.Value[1].CompilerInputsError.Message, Does.EndWith($"[{Library}]"));
            Assert.That(result.Value[1].CompilerInputsError.Message, Does.Not.Contain("MSB4057"));
            Assert.That(result.Value[1].ReferencePaths, Is.Null);
            Assert.That(result.Value[1].OutputType, Is.EqualTo(evaluated.Metadata.OutputType));
            Assert.That(result.Value[2].CompilerInputsError.Message, Does.Contain("MSB4057"));
            Assert.That(fallback.CompilerInputProjects, Is.Empty);
            Assert.That(runner.Invocations, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task ProjectMissingWithoutAnAttributedErrorIsResolvedIndividually()
    {
        var runner = BatchRunner.Completing(invocation =>
        {
            invocation.WriteProject(App, "Property\tTargetFramework\tnet10.0", "Items\tReferencePath");
            return new ProcessResult(1, "MSBUILD : error MSB1025: An internal failure occurred.", string.Empty);
        });
        var fallback = new RecordingFallbackProvider();
        var provider = new MsBuildBatchProjectEvaluationProvider(runner, fallback);

        var result = await provider.ResolveCompilerInputsAsync([CreateProject(App, "net10.0"), CreateProject(Library, "net10.0")]);

        Assert.That(result.IsSuccess, Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(fallback.CompilerInputProjects, Is.EqualTo(new[] { Library }));
            Assert.That(result.Value![1].AssemblyName, Is.EqualTo("fallback:Library"));
        });
    }

    [Test]
    public async Task UnavailableSdkIsReportedWithoutStartingAProcessPerProject()
    {
        var failure = Result.Failure<ProcessResult>(OperationError.Create("process.start.failed", "dotnet is not installed"));
        var fallback = new RecordingFallbackProvider();
        var provider = new MsBuildBatchProjectEvaluationProvider(new BatchRunner(_ => failure), fallback);

        var evaluation = await provider.EvaluateAsync([App, Library]);
        var compilerInputs = await provider.ResolveCompilerInputsAsync([CreateProject(App, "net10.0"), CreateProject(Library)]);

        Assert.Multiple(() =>
        {
            Assert.That(evaluation.IsFailure, Is.True);
            Assert.That(evaluation.Error.Code, Is.EqualTo("workspace.project.metadata.evaluate.failed"));
            Assert.That(evaluation.Error.Message, Does.Contain("dotnet is not installed"));
            Assert.That(compilerInputs.IsSuccess, Is.True);
            Assert.That(compilerInputs.Value!.Select(metadata => metadata.CompilerInputsError.Code),
                Is.All.EqualTo("workspace.project.compilation-references.resolve.failed"));
            Assert.That(compilerInputs.Value![0].CompilerInputsError.Message, Does.Contain("dotnet is not installed"));
            Assert.That(fallback.EvaluatedProjects, Is.Empty);
            Assert.That(fallback.CompilerInputProjects, Is.Empty);
        });
    }

    [Test]
    public async Task UnusableTemporaryFilesFallBackToIndividualEvaluation()
    {
        var runner = BatchRunner.Completing(invocation =>
        {
            Directory.Delete(invocation.Properties["_TorenOutputDirectory"], recursive: true);
            return new ProcessResult(0, string.Empty, string.Empty);
        });
        var fallback = new RecordingFallbackProvider();
        var provider = new MsBuildBatchProjectEvaluationProvider(runner, fallback);

        var evaluation = await provider.EvaluateAsync([App, Library]);
        var compilerInputs = await provider.ResolveCompilerInputsAsync([CreateProject(App, "net10.0")]);

        Assert.Multiple(() =>
        {
            Assert.That(evaluation.IsSuccess, Is.True);
            Assert.That(compilerInputs.IsSuccess, Is.True);
            Assert.That(fallback.EvaluatedProjects, Is.EqualTo(new[] { App, Library }));
            Assert.That(fallback.CompilerInputProjects, Is.EqualTo(new[] { App }));
        });
    }

    [Test]
    public async Task PathsWithMsBuildSpecialCharactersAreEscaped()
    {
        var project = Path.Combine(Root, "50% (final); 'app' & $(more) @here", "App.csproj");
        var runner = BatchRunner.Completing(invocation =>
        {
            invocation.WriteProject(project, "Property\tTargetFramework\tnet10.0", "Items\tReferencePath");
            return new ProcessResult(0, string.Empty, string.Empty);
        });
        var provider = new MsBuildBatchProjectEvaluationProvider(runner, new RecordingFallbackProvider());

        var result = await provider.ResolveCompilerInputsAsync([CreateProject(project, "net10.0")]);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(runner.Invocations.Single().Projects.Single().Path, Is.EqualTo(project));
            Assert.That(runner.Invocations.Single().RawProjectIncludes.Single(), Does.Not.Contain(";").And.Not.Contain("$("));
        });
    }

    [Test]
    public async Task CancellationPropagatesAndLeavesNoTemporaryFiles()
    {
        string? directory = null;
        var runner = BatchRunner.Completing(invocation =>
        {
            directory = Path.GetDirectoryName(invocation.TraversalPath);
            throw new OperationCanceledException();
        });
        var provider = new MsBuildBatchProjectEvaluationProvider(runner, new RecordingFallbackProvider());
        using var cancellation = new CancellationTokenSource();

        await Assert.CatchAsync<OperationCanceledException>(async () => await provider.EvaluateAsync([App], cancellation.Token));
        cancellation.Cancel();
        await Assert.CatchAsync<OperationCanceledException>(async () => await provider.EvaluateAsync([App], cancellation.Token));
        Assert.Multiple(() =>
        {
            Assert.That(directory, Is.Not.Null);
            Assert.That(Directory.Exists(directory), Is.False);
            Assert.That(runner.Invocations, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task EmptyProjectListNeedsNoProcess()
    {
        var runner = BatchRunner.Completing(_ => new ProcessResult(0, string.Empty, string.Empty));
        var provider = new MsBuildBatchProjectEvaluationProvider(runner, new RecordingFallbackProvider());

        var evaluations = await provider.EvaluateAsync([]);
        var metadata = await provider.ResolveCompilerInputsAsync([]);

        Assert.Multiple(() =>
        {
            Assert.That(evaluations.Value, Is.Empty);
            Assert.That(metadata.Value, Is.Empty);
            Assert.That(runner.Invocations, Is.Empty);
        });
    }

    private static WorkspaceProject CreateProject(string path, params string[] targetFrameworks) =>
        new(path, Path.GetFileNameWithoutExtension(path),
            new ProjectMetadata(targetFrameworks, "Exe", null, null, false, false, null, null, null), []);

    [GeneratedRegex("%([0-9A-Fa-f]{2})")]
    private static partial Regex EscapedCharacter();

    /// <summary>Stands in for <c>dotnet msbuild</c> on the generated traversal project.</summary>
    private sealed class BatchRunner(Func<BatchInvocation, Result<ProcessResult>> run) : IProcessRunner
    {
        public static BatchRunner Completing(Func<BatchInvocation, ProcessResult> run) =>
            new(invocation => Result.Success(run(invocation)));

        public List<BatchInvocation> Invocations { get; } = [];

        public Task<Result<ProcessResult>> RunAsync(ProcessRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var invocation = new BatchInvocation(request);
            Invocations.Add(invocation);
            return Task.FromResult(run(invocation));
        }
    }

    private sealed class BatchInvocation
    {
        public BatchInvocation(ProcessRequest request)
        {
            Arguments = request.Arguments;
            WorkingDirectory = request.WorkingDirectory;
            TraversalPath = request.Arguments[1];
            var traversal = XDocument.Load(TraversalPath).Root!;
            var task = traversal.Element("Target")!.Element("MSBuild")!;
            RawProjectIncludes = traversal.Element("ItemGroup")!.Elements("TorenProject")
                .Select(static item => item.Attribute("Include")!.Value)
                .ToArray();
            Projects = traversal.Element("ItemGroup")!.Elements("TorenProject")
                .Select(static item => (Unescape(item.Attribute("Include")!.Value), item.Attribute("AdditionalProperties")?.Value))
                .ToArray();
            Targets = task.Attribute("Targets")!.Value;
            ContinueOnError = task.Attribute("ContinueOnError")?.Value == "true";
            Properties = task.Attribute("Properties")!.Value
                .Split(';')
                .Select(static property => property.Split('=', 2))
                .ToDictionary(static pair => pair[0], static pair => Unescape(pair[1]));
            TargetsFileExisted = File.Exists(Properties["CustomBeforeMicrosoftCommonTargets"])
                && Properties["CustomBeforeMicrosoftCommonTargets"] == Properties["CustomBeforeMicrosoftCommonCrossTargetingTargets"]
                && File.ReadAllText(Properties["CustomBeforeMicrosoftCommonTargets"]).Contains("_TorenCollectCompilerInputs", StringComparison.Ordinal);
        }

        public IReadOnlyList<string> Arguments { get; }

        public string? WorkingDirectory { get; }

        public string TraversalPath { get; }

        public IReadOnlyList<string> RawProjectIncludes { get; }

        public IReadOnlyList<(string Path, string? AdditionalProperties)> Projects { get; }

        public string Targets { get; }

        public bool ContinueOnError { get; }

        public Dictionary<string, string> Properties { get; }

        public bool TargetsFileExisted { get; }

        public void WriteProject(string projectPath, params string[] records) =>
            WriteRaw([$"Project\t{projectPath}", .. records, "End"]);

        public void WriteRaw(params string[] lines) =>
            File.WriteAllLines(
                Path.Combine(Properties["_TorenOutputDirectory"], $"{Guid.NewGuid():N}.txt"),
                lines,
                new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        private static string Unescape(string value) =>
            EscapedCharacter().Replace(value, static match => ((char)Convert.ToInt32(match.Groups[1].Value, 16)).ToString());
    }

    private sealed class RecordingFallbackProvider(OperationError evaluationError = default) : IProjectEvaluationProvider
    {
        public List<string> EvaluatedProjects { get; } = [];

        public List<string> CompilerInputProjects { get; } = [];

        public Task<Result<IReadOnlyList<ProjectEvaluation>>> EvaluateAsync(
            IReadOnlyList<string> projectPaths,
            CancellationToken cancellationToken = default)
        {
            EvaluatedProjects.AddRange(projectPaths);
            return Task.FromResult(evaluationError.IsNone
                ? Result.Success<IReadOnlyList<ProjectEvaluation>>(projectPaths
                    .Select(static path => new ProjectEvaluation(CreateFallbackMetadata(path), []))
                    .ToArray())
                : Result.Failure<IReadOnlyList<ProjectEvaluation>>(evaluationError));
        }

        public Task<Result<IReadOnlyList<ProjectMetadata>>> ResolveCompilerInputsAsync(
            IReadOnlyList<WorkspaceProject> projects,
            CancellationToken cancellationToken = default)
        {
            CompilerInputProjects.AddRange(projects.Select(static project => project.Path));
            return Task.FromResult(Result.Success<IReadOnlyList<ProjectMetadata>>(projects
                .Select(static project => CreateFallbackMetadata(project.Path))
                .ToArray()));
        }

        private static ProjectMetadata CreateFallbackMetadata(string path) =>
            new([], null, $"fallback:{Path.GetFileNameWithoutExtension(path)}", null, false, false, null, null, null);
    }
}
