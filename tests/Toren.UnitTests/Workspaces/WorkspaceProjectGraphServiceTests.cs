using NUnit.Framework;
using Toren.Core.Results;
using Toren.Workspaces.Contracts;
using Toren.Workspaces.Models;
using Toren.Workspaces.Services;

namespace Toren.UnitTests.Workspaces;

[TestFixture]
public sealed class WorkspaceProjectGraphServiceTests
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
        var metadataProvider = new FakeProjectMetadataProvider();
        var referenceProvider = new FakeProjectReferenceProvider(api, locker);
        var service = new WorkspaceProjectGraphService(
            folderProvider,
            solutionProvider,
            metadataProvider,
            referenceProvider);
        var workspace = new WorkspaceDescriptor(
            Path.Combine(root, "ParcelBox.sln"),
            "ParcelBox",
            WorkspaceKind.Solution);

        var result = await service.LoadAsync(workspace);

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
            Assert.That(metadataProvider.CallCount, Is.EqualTo(2));
            Assert.That(referenceProvider.CallCount, Is.EqualTo(2));
        });
    }

    [Test]
    public async Task ProjectWorkspaceDoesNotInvokeDiscoveryProviders()
    {
        var projectPath = Path.Combine(Path.GetTempPath(), "ParcelBox.Api.csproj");
        var folderProvider = new FakeFolderProjectProvider([]);
        var solutionProvider = new FakeSolutionProjectProvider([]);
        var metadataProvider = new FakeProjectMetadataProvider();
        var referenceProvider = new FakeProjectReferenceProvider(string.Empty, string.Empty);
        var service = new WorkspaceProjectGraphService(
            folderProvider,
            solutionProvider,
            metadataProvider,
            referenceProvider);
        var workspace = new WorkspaceDescriptor(projectPath, "ParcelBox.Api", WorkspaceKind.Project);

        var result = await service.LoadAsync(workspace);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value!.Projects, Has.Count.EqualTo(1));
            Assert.That(result.Value.Projects[0].Path, Is.EqualTo(Path.GetFullPath(projectPath)));
            Assert.That(result.Value.Projects[0].DisplayName, Is.EqualTo("Api"));
            Assert.That(folderProvider.CallCount, Is.Zero);
            Assert.That(solutionProvider.CallCount, Is.Zero);
            Assert.That(metadataProvider.CallCount, Is.EqualTo(1));
            Assert.That(referenceProvider.CallCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task FolderWorkspaceDiscoversProjectsThroughFolderProvider()
    {
        var projectPath = Path.Combine(Path.GetTempPath(), "src", "ParcelBox.Api", "ParcelBox.Api.csproj");
        var folderProvider = new FakeFolderProjectProvider([projectPath]);
        var solutionProvider = new FakeSolutionProjectProvider([]);
        var metadataProvider = new FakeProjectMetadataProvider();
        var referenceProvider = new FakeProjectReferenceProvider(string.Empty, string.Empty);
        var service = new WorkspaceProjectGraphService(
            folderProvider,
            solutionProvider,
            metadataProvider,
            referenceProvider);
        var workspace = new WorkspaceDescriptor(Path.GetTempPath(), "Temp", WorkspaceKind.Folder);

        var result = await service.LoadAsync(workspace);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value!.Projects, Has.Count.EqualTo(1));
            Assert.That(result.Value.Projects[0].DisplayName, Is.EqualTo("Api"));
            Assert.That(folderProvider.CallCount, Is.EqualTo(1));
            Assert.That(solutionProvider.CallCount, Is.Zero);
            Assert.That(metadataProvider.CallCount, Is.EqualTo(1));
            Assert.That(referenceProvider.CallCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task MetadataFailureStopsGraphComposition()
    {
        var projectPath = Path.Combine(Path.GetTempPath(), "Broken.csproj");
        var service = new WorkspaceProjectGraphService(
            new FakeFolderProjectProvider([]),
            new FakeSolutionProjectProvider([]),
            new FailingProjectMetadataProvider(),
            new FakeProjectReferenceProvider(string.Empty, string.Empty));
        var workspace = new WorkspaceDescriptor(projectPath, "Broken", WorkspaceKind.Project);

        var result = await service.LoadAsync(workspace);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo("test.metadata.failed"));
        });
    }

    [Test]
    public async Task LargeSolutionOverlapsEvaluationWithinABoundAndKeepsSolutionOrder()
    {
        var paths = Enumerable.Range(0, 12).Select(index => Path.Combine(Path.GetTempPath(), $"Project{index}.csproj")).ToArray();
        var batchSize = Math.Clamp(Environment.ProcessorCount, 1, 4);
        var metadata = new GatedMetadataProvider(batchSize);
        var graph = new WorkspaceProjectGraphService(new FakeFolderProjectProvider([]),
            new FakeSolutionProjectProvider(paths), metadata, new FakeProjectReferenceProvider("", ""));
        var pending = graph.LoadAsync(new WorkspaceDescriptor("/work/Large.sln", "Large", WorkspaceKind.Solution));
        await metadata.BatchStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.That(metadata.Started, Is.EqualTo(batchSize));
        metadata.Release.SetResult();
        var result = await pending;
        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Value!.Projects.Select(project => project.Path), Is.EqualTo(paths));
        Assert.That(metadata.MaximumActive, Is.EqualTo(batchSize));
        Assert.That(metadata.Active, Is.Zero);
    }

    [Test]
    public async Task CancellingParallelGraphEvaluationDrainsAllStartedWork()
    {
        var paths = Enumerable.Range(0, 12).Select(index => Path.Combine(Path.GetTempPath(), $"Project{index}.csproj")).ToArray();
        var metadata = new GatedMetadataProvider(Math.Clamp(Environment.ProcessorCount, 1, 4));
        var graph = new WorkspaceProjectGraphService(new FakeFolderProjectProvider([]),
            new FakeSolutionProjectProvider(paths), metadata, new FakeProjectReferenceProvider("", ""));
        using var cancellation = new CancellationTokenSource();
        var pending = graph.LoadAsync(new WorkspaceDescriptor("/work/Large.sln", "Large", WorkspaceKind.Solution), cancellation.Token);
        await metadata.BatchStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        Assert.CatchAsync<OperationCanceledException>(async () => await pending);
        Assert.That(metadata.Active, Is.Zero);
    }

    private sealed class GatedMetadataProvider(int batchSize) : IProjectMetadataProvider
    {
        private int _started;
        private int _active;
        private int _maximum;
        public TaskCompletionSource BatchStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Started => Volatile.Read(ref _started);
        public int Active => Volatile.Read(ref _active);
        public int MaximumActive => Volatile.Read(ref _maximum);
        public async Task<Result<ProjectMetadata>> GetMetadataAsync(string projectPath, CancellationToken cancellationToken = default)
        {
            var active = Interlocked.Increment(ref _active);
            int previous;
            do { previous = _maximum; } while (active > previous && Interlocked.CompareExchange(ref _maximum, active, previous) != previous);
            if (Interlocked.Increment(ref _started) == batchSize) BatchStarted.TrySetResult();
            try
            {
                await Release.Task.WaitAsync(cancellationToken);
                return Result.Success(CreateMetadata(projectPath));
            }
            finally { Interlocked.Decrement(ref _active); }
        }
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

    private sealed class FakeSolutionProjectProvider(IReadOnlyList<string> projects) : ISolutionProjectProvider
    {
        public int CallCount { get; private set; }

        public Task<Result<IReadOnlyList<string>>> GetProjectPathsAsync(
            string solutionPath,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            return Task.FromResult(Result.Success(projects));
        }
    }

    private sealed class FakeProjectMetadataProvider : IProjectMetadataProvider
    {
        public int CallCount { get; private set; }

        public Task<Result<ProjectMetadata>> GetMetadataAsync(
            string projectPath,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            return Task.FromResult(Result.Success(CreateMetadata(projectPath)));
        }
    }

    private sealed class FailingProjectMetadataProvider : IProjectMetadataProvider
    {
        public Task<Result<ProjectMetadata>> GetMetadataAsync(
            string projectPath,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Failure<ProjectMetadata>(
                OperationError.Create("test.metadata.failed", "metadata failed")));
    }

    private sealed class FakeProjectReferenceProvider(string referencingProject, string referencedProject)
        : IProjectReferenceProvider
    {
        public int CallCount { get; private set; }

        public Task<Result<IReadOnlyList<ProjectReferenceInfo>>> GetReferencesAsync(
            string projectPath,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            IReadOnlyList<ProjectReferenceInfo> references = projectPath.Equals(
                referencingProject,
                StringComparison.OrdinalIgnoreCase)
                ? [new ProjectReferenceInfo("../Locker/Locker.csproj", ProjectReferenceKind.Project, referencedProject)]
                : [];
            return Task.FromResult(Result.Success(references));
        }
    }
}
