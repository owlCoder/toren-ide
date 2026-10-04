using NUnit.Framework;
using Toren.Core.Results;
using Toren.Workspaces.Contracts;
using Toren.Workspaces.Models;
using Toren.Workspaces.Services;

namespace Toren.UnitTests.Workspaces;

[TestFixture]
public sealed class WorkspaceProjectGraphEvaluatorTests
{
    private static readonly string[] DefaultTargetFrameworks = ["net10.0"];

    [Test]
    public async Task SolutionGraphComposesProjectsMetadataAndEvaluatedReferences()
    {
        var root = Path.Combine(Path.GetTempPath(), "ParcelBox");
        var api = Path.Combine(root, "ParcelBox.Api", "ParcelBox.Api.csproj");
        var locker = Path.Combine(root, "ParcelBox.Simulators.LockerControl", "ParcelBox.Simulators.LockerControl.csproj");
        var folderProvider = new FakeFolderProjectProvider([]);
        var solutionProvider = new FakeSolutionProjectProvider([api, locker]);
        var evaluationProvider = new FakeProjectEvaluationProvider(api, locker);
        var evaluator = new WorkspaceProjectGraphEvaluator(folderProvider, solutionProvider, evaluationProvider);
        var workspace = new WorkspaceDescriptor(
            Path.Combine(root, "ParcelBox.sln"),
            "ParcelBox",
            WorkspaceKind.Solution);

        var result = await evaluator.EvaluateAsync(workspace);

        Assert.That(result.IsSuccess, Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(result.Value!.Projects, Has.Count.EqualTo(2));
            Assert.That(string.Join("|", result.Value.Projects.Select(project => project.DisplayName)),
                Is.EqualTo("Api|LockerControl"));
            Assert.That(result.Value.Projects[0].Metadata.AssemblyName, Is.EqualTo("ParcelBox.Api"));
            Assert.That(string.Join("|", result.Value.Projects[0].Metadata.TargetFrameworks), Is.EqualTo("net10.0"));
            Assert.That(result.Value.Projects[0].References, Has.Count.EqualTo(1));
            Assert.That(result.Value.Projects[0].References[0].ResolvedPath, Is.EqualTo(locker));
            Assert.That(folderProvider.CallCount, Is.Zero);
            Assert.That(solutionProvider.CallCount, Is.EqualTo(1));
            Assert.That(evaluationProvider.EvaluatedProjects, Is.EqualTo(new[] { api, locker }));
            Assert.That(evaluationProvider.CompilerInputProjects, Is.Empty);
        });
    }

    [Test]
    public async Task CompilerInputsAreResolvedPerProjectAndKeepEvaluatedReferences()
    {
        var root = Path.Combine(Path.GetTempPath(), "ParcelBox");
        var api = Path.Combine(root, "ParcelBox.Api", "ParcelBox.Api.csproj");
        var locker = Path.Combine(root, "ParcelBox.Simulators.LockerControl", "ParcelBox.Simulators.LockerControl.csproj");
        var evaluationProvider = new FakeProjectEvaluationProvider(api, locker);
        var evaluator = new WorkspaceProjectGraphEvaluator(
            new FakeFolderProjectProvider([]),
            new FakeSolutionProjectProvider([api, locker]),
            evaluationProvider);
        var evaluated = await evaluator.EvaluateAsync(
            new WorkspaceDescriptor(Path.Combine(root, "ParcelBox.sln"), "ParcelBox", WorkspaceKind.Solution));

        var result = await evaluator.ResolveCompilerInputsAsync(evaluated.Value!);

        Assert.That(result.IsSuccess, Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(evaluated.Value!.Projects.Select(project => project.Metadata.ReferencePaths), Is.All.Null);
            Assert.That(result.Value!.Projects.Select(project => project.Path), Is.EqualTo(new[] { api, locker }));
            Assert.That(result.Value.Projects.Select(project => project.DisplayName),
                Is.EqualTo(evaluated.Value.Projects.Select(project => project.DisplayName)));
            Assert.That(result.Value.Projects[0].Metadata.ReferencePaths, Is.EqualTo(new[] { api + ".ref.dll" }));
            Assert.That(result.Value.Projects[0].References, Is.SameAs(evaluated.Value.Projects[0].References));
            Assert.That(evaluationProvider.EvaluatedProjects, Is.EqualTo(new[] { api, locker }));
            Assert.That(evaluationProvider.CompilerInputProjects, Is.EqualTo(new[] { api, locker }));
        });
    }

    [Test]
    public async Task ProjectWorkspaceDoesNotInvokeDiscoveryProviders()
    {
        var projectPath = Path.Combine(Path.GetTempPath(), "ParcelBox.Api.csproj");
        var folderProvider = new FakeFolderProjectProvider([]);
        var solutionProvider = new FakeSolutionProjectProvider([]);
        var evaluationProvider = new FakeProjectEvaluationProvider(string.Empty, string.Empty);
        var evaluator = new WorkspaceProjectGraphEvaluator(folderProvider, solutionProvider, evaluationProvider);
        var workspace = new WorkspaceDescriptor(projectPath, "ParcelBox.Api", WorkspaceKind.Project);

        var result = await evaluator.EvaluateAsync(workspace);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value!.Projects, Has.Count.EqualTo(1));
            Assert.That(result.Value.Projects[0].Path, Is.EqualTo(Path.GetFullPath(projectPath)));
            Assert.That(result.Value.Projects[0].DisplayName, Is.EqualTo("Api"));
            Assert.That(folderProvider.CallCount, Is.Zero);
            Assert.That(solutionProvider.CallCount, Is.Zero);
            Assert.That(evaluationProvider.EvaluatedProjects, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task FolderWorkspaceDiscoversProjectsThroughFolderProvider()
    {
        var projectPath = Path.Combine(Path.GetTempPath(), "src", "ParcelBox.Api", "ParcelBox.Api.csproj");
        var folderProvider = new FakeFolderProjectProvider([projectPath]);
        var solutionProvider = new FakeSolutionProjectProvider([]);
        var evaluationProvider = new FakeProjectEvaluationProvider(string.Empty, string.Empty);
        var evaluator = new WorkspaceProjectGraphEvaluator(folderProvider, solutionProvider, evaluationProvider);
        var workspace = new WorkspaceDescriptor(Path.GetTempPath(), "Temp", WorkspaceKind.Folder);

        var result = await evaluator.EvaluateAsync(workspace);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value!.Projects, Has.Count.EqualTo(1));
            Assert.That(result.Value.Projects[0].DisplayName, Is.EqualTo("Api"));
            Assert.That(folderProvider.CallCount, Is.EqualTo(1));
            Assert.That(solutionProvider.CallCount, Is.Zero);
            Assert.That(evaluationProvider.EvaluatedProjects, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task DiscoveryFailureStopsGraphCompositionBeforeEvaluation()
    {
        var error = OperationError.Create("test.solution.failed", "solution failed");
        var evaluationProvider = new FakeProjectEvaluationProvider(string.Empty, string.Empty);
        var evaluator = new WorkspaceProjectGraphEvaluator(
            new FakeFolderProjectProvider([]),
            new FakeSolutionProjectProvider([], error),
            evaluationProvider);

        var result = await evaluator.EvaluateAsync(new WorkspaceDescriptor("/work/App.sln", "App", WorkspaceKind.Solution));

        Assert.Multiple(() =>
        {
            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error, Is.EqualTo(error));
            Assert.That(evaluationProvider.EvaluatedProjects, Is.Empty);
        });
    }

    [Test]
    public async Task EvaluationFailureStopsGraphComposition()
    {
        var projectPath = Path.Combine(Path.GetTempPath(), "Broken.csproj");
        var evaluator = new WorkspaceProjectGraphEvaluator(
            new FakeFolderProjectProvider([]),
            new FakeSolutionProjectProvider([]),
            new FailingProjectEvaluationProvider(failEvaluation: true));
        var workspace = new WorkspaceDescriptor(projectPath, "Broken", WorkspaceKind.Project);

        var result = await evaluator.EvaluateAsync(workspace);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo("test.metadata.failed"));
        });
    }

    [Test]
    public async Task CompilerInputFailureIsPropagated()
    {
        var projectPath = Path.Combine(Path.GetTempPath(), "Broken.csproj");
        var evaluator = new WorkspaceProjectGraphEvaluator(
            new FakeFolderProjectProvider([]),
            new FakeSolutionProjectProvider([]),
            new FailingProjectEvaluationProvider(failEvaluation: false));
        var evaluated = await evaluator.EvaluateAsync(new WorkspaceDescriptor(projectPath, "Broken", WorkspaceKind.Project));

        var result = await evaluator.ResolveCompilerInputsAsync(evaluated.Value!);

        Assert.Multiple(() =>
        {
            Assert.That(evaluated.IsSuccess, Is.True);
            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo("test.compiler-inputs.failed"));
        });
    }

    [Test]
    public async Task CancellationIsObservedBeforeDiscovery()
    {
        var solutionProvider = new FakeSolutionProjectProvider([]);
        var evaluator = new WorkspaceProjectGraphEvaluator(
            new FakeFolderProjectProvider([]),
            solutionProvider,
            new FakeProjectEvaluationProvider(string.Empty, string.Empty));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.CatchAsync<OperationCanceledException>(async () => await evaluator.EvaluateAsync(
            new WorkspaceDescriptor("/work/App.sln", "App", WorkspaceKind.Solution), cancellation.Token));
        Assert.That(solutionProvider.CallCount, Is.Zero);
    }

    private static ProjectMetadata CreateMetadata(string projectPath) =>
        new(
            DefaultTargetFrameworks,
            "Library",
            Path.GetFileNameWithoutExtension(projectPath),
            Path.GetFileNameWithoutExtension(projectPath),
            IsTestProject: false,
            UsesCentralPackageManagement: true,
            DirectoryBuildPropsPath: null,
            DirectoryBuildTargetsPath: null,
            DirectoryPackagesPropsPath: null);

    private sealed class FakeFolderProjectProvider(IReadOnlyList<string> projects) : IFolderProjectProvider
    {
        public int CallCount { get; private set; }

        public Task<Result<IReadOnlyList<string>>> GetProjectPathsAsync(
            string folderPath,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            return Task.FromResult(Result.Success(projects));
        }
    }

    private sealed class FakeSolutionProjectProvider(IReadOnlyList<string> projects, OperationError error = default)
        : ISolutionProjectProvider
    {
        public int CallCount { get; private set; }

        public Task<Result<IReadOnlyList<string>>> GetProjectPathsAsync(
            string solutionPath,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            return Task.FromResult(error.IsNone
                ? Result.Success(projects)
                : Result.Failure<IReadOnlyList<string>>(error));
        }
    }

    private sealed class FakeProjectEvaluationProvider(string referencingProject, string referencedProject)
        : IProjectEvaluationProvider
    {
        public List<string> EvaluatedProjects { get; } = [];

        public List<string> CompilerInputProjects { get; } = [];

        public Task<Result<IReadOnlyList<ProjectEvaluation>>> EvaluateAsync(
            IReadOnlyList<string> projectPaths,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            EvaluatedProjects.AddRange(projectPaths);
            return Task.FromResult(Result.Success<IReadOnlyList<ProjectEvaluation>>(projectPaths
                .Select(projectPath => new ProjectEvaluation(
                    CreateMetadata(projectPath),
                    projectPath.Equals(referencingProject, StringComparison.OrdinalIgnoreCase)
                        ? [new ProjectReferenceInfo("../Locker/Locker.csproj", ProjectReferenceKind.Project, referencedProject)]
                        : []))
                .ToArray()));
        }

        public Task<Result<IReadOnlyList<ProjectMetadata>>> ResolveCompilerInputsAsync(
            IReadOnlyList<WorkspaceProject> projects,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CompilerInputProjects.AddRange(projects.Select(static project => project.Path));
            return Task.FromResult(Result.Success<IReadOnlyList<ProjectMetadata>>(projects
                .Select(static project => project.Metadata with { ReferencePaths = [project.Path + ".ref.dll"] })
                .ToArray()));
        }
    }

    private sealed class FailingProjectEvaluationProvider(bool failEvaluation) : IProjectEvaluationProvider
    {
        public Task<Result<IReadOnlyList<ProjectEvaluation>>> EvaluateAsync(
            IReadOnlyList<string> projectPaths,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(failEvaluation
                ? Result.Failure<IReadOnlyList<ProjectEvaluation>>(
                    OperationError.Create("test.metadata.failed", "metadata failed"))
                : Result.Success<IReadOnlyList<ProjectEvaluation>>(projectPaths
                    .Select(static projectPath => new ProjectEvaluation(CreateMetadata(projectPath), []))
                    .ToArray()));

        public Task<Result<IReadOnlyList<ProjectMetadata>>> ResolveCompilerInputsAsync(
            IReadOnlyList<WorkspaceProject> projects,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Failure<IReadOnlyList<ProjectMetadata>>(
                OperationError.Create("test.compiler-inputs.failed", "compiler inputs failed")));
    }
}
