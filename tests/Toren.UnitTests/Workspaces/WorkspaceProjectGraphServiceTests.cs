using NUnit.Framework;
using Toren.Core.Results;
using Toren.Workspaces.Contracts;
using Toren.Workspaces.Errors;
using Toren.Workspaces.Models;
using Toren.Workspaces.Services;

namespace Toren.UnitTests.Workspaces;

[TestFixture]
public sealed class WorkspaceProjectGraphServiceTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);
    private static readonly WorkspaceDescriptor App = CreateWorkspace("App");
    private static readonly WorkspaceDescriptor Other = CreateWorkspace("Other");

    [Test]
    public async Task OverlappingConsumersShareOneEvaluation()
    {
        var gate = NewGate();
        var provider = new ControlledEvaluationProvider { OnEvaluate = async (_, token) => await gate.Task.WaitAsync(token) };
        using var service = CreateService(provider);

        var graphs = Enumerable.Range(0, 3).Select(_ => service.LoadAsync(App)).ToArray();
        var catalogs = Enumerable.Range(0, 2).Select(_ => service.GetProjectsAsync(App)).ToArray();
        await provider.EvaluationStarted.Task.WaitAsync(Timeout);
        gate.SetResult();
        var graphResults = await Task.WhenAll(graphs).WaitAsync(Timeout);
        var catalogResults = await Task.WhenAll(catalogs).WaitAsync(Timeout);

        Assert.Multiple(() =>
        {
            Assert.That(graphResults.Select(result => result.IsSuccess), Is.All.True);
            Assert.That(catalogResults.Select(result => result.IsSuccess), Is.All.True);
            Assert.That(graphResults.Select(result => result.Value), Is.All.SameAs(graphResults[0].Value));
            Assert.That(provider.EvaluationCount, Is.EqualTo(1));
            Assert.That(provider.CompilerInputCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task FinishedSnapshotIsReusedWhileInputsAreUnchanged()
    {
        var provider = new ControlledEvaluationProvider();
        using var service = CreateService(provider);

        var first = await service.LoadAsync(App);
        var second = await service.LoadAsync(App);
        var third = await service.GetProjectsAsync(App);

        Assert.Multiple(() =>
        {
            Assert.That(first.IsSuccess, Is.True);
            Assert.That(second.Value, Is.SameAs(first.Value));
            Assert.That(third.IsSuccess, Is.True);
            Assert.That(provider.EvaluationCount, Is.EqualTo(1));
            Assert.That(provider.CompilerInputCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task CatalogDoesNotResolveCompilerInputsUntilAGraphIsRequested()
    {
        var provider = new ControlledEvaluationProvider();
        using var service = CreateService(provider);

        var projects = await service.GetProjectsAsync(App);
        var compilerInputsAfterCatalog = provider.CompilerInputCount;
        var graph = await service.LoadAsync(App);

        Assert.Multiple(() =>
        {
            Assert.That(projects.Value!.Single().Metadata.ReferencePaths, Is.Null);
            Assert.That(compilerInputsAfterCatalog, Is.Zero);
            Assert.That(graph.Value!.Projects.Single().Metadata.ReferencePaths, Is.Not.Null);
            Assert.That(provider.EvaluationCount, Is.EqualTo(1));
            Assert.That(provider.CompilerInputCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task CatalogIsAvailableWhileCompilerInputsAreStillResolving()
    {
        var gate = NewGate();
        var provider = new ControlledEvaluationProvider { OnResolve = async (_, token) => await gate.Task.WaitAsync(token) };
        using var service = CreateService(provider);

        var graph = service.LoadAsync(App);
        var projects = await service.GetProjectsAsync(App).WaitAsync(Timeout);
        var graphCompletedEarly = graph.IsCompleted;
        gate.SetResult();
        var graphResult = await graph.WaitAsync(Timeout);

        Assert.Multiple(() =>
        {
            Assert.That(projects.IsSuccess, Is.True);
            Assert.That(graphCompletedEarly, Is.False);
            Assert.That(graphResult.IsSuccess, Is.True);
            Assert.That(provider.EvaluationCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task ChangedWorkspaceInputsTriggerReevaluation()
    {
        var provider = new ControlledEvaluationProvider();
        var stamps = new FakeStampProvider();
        using var service = CreateService(provider, stamps);

        var first = await service.LoadAsync(App);
        stamps.WorkspaceStamp = "project file edited";
        var second = await service.LoadAsync(App);
        var third = await service.LoadAsync(App);

        Assert.Multiple(() =>
        {
            Assert.That(second.Value, Is.Not.SameAs(first.Value));
            Assert.That(third.Value, Is.SameAs(second.Value));
            Assert.That(provider.EvaluationCount, Is.EqualTo(2));
            Assert.That(provider.CompilerInputCount, Is.EqualTo(2));
        });
    }

    [Test]
    public async Task ChangedProjectInputsTriggerReevaluation()
    {
        var provider = new ControlledEvaluationProvider();
        var stamps = new FakeStampProvider();
        using var service = CreateService(provider, stamps);

        var first = await service.LoadAsync(App);
        stamps.ProjectStamp = "restored";
        var second = await service.LoadAsync(App);

        Assert.Multiple(() =>
        {
            Assert.That(second.IsSuccess, Is.True);
            Assert.That(second.Value, Is.Not.SameAs(first.Value));
            Assert.That(provider.EvaluationCount, Is.EqualTo(2));
        });
    }

    [Test]
    public async Task SnapshotWhoseInputsWereWrittenDuringEvaluationIsNotReused()
    {
        var provider = new ControlledEvaluationProvider();
        var stamps = new FakeStampProvider { LastWriteTimeUtc = DateTime.MaxValue };
        using var service = CreateService(provider, stamps);

        var first = await service.LoadAsync(App);
        stamps.LastWriteTimeUtc = DateTime.MinValue;
        var second = await service.LoadAsync(App);
        var third = await service.LoadAsync(App);

        Assert.Multiple(() =>
        {
            Assert.That(first.IsSuccess, Is.True);
            Assert.That(second.Value, Is.Not.SameAs(first.Value));
            Assert.That(third.Value, Is.SameAs(second.Value));
            Assert.That(provider.EvaluationCount, Is.EqualTo(2));
        });
    }

    [Test]
    public async Task RequestJoiningAnEvaluationThatRacedWithAWriteGetsAFreshEvaluation()
    {
        var gate = NewGate();
        var provider = new ControlledEvaluationProvider();
        provider.OnEvaluate = async (call, token) =>
        {
            if (call == 1)
            {
                await gate.Task.WaitAsync(token);
            }
        };
        var stamps = new FakeStampProvider { LastWriteTimeUtc = DateTime.MaxValue };
        using var service = CreateService(provider, stamps);

        var starter = service.LoadAsync(App);
        await provider.EvaluationStarted.Task.WaitAsync(Timeout);
        var joiner = service.LoadAsync(App);
        gate.SetResult();
        var starterResult = await starter.WaitAsync(Timeout);
        var joinerResult = await joiner.WaitAsync(Timeout);

        Assert.Multiple(() =>
        {
            Assert.That(starterResult.IsSuccess, Is.True);
            Assert.That(joinerResult.IsSuccess, Is.True);
            Assert.That(joinerResult.Value, Is.Not.SameAs(starterResult.Value));
            Assert.That(provider.EvaluationCount, Is.EqualTo(2));
            Assert.That(provider.CompilerInputCount, Is.EqualTo(2));
        });
    }

    [Test]
    public async Task FailedEvaluationIsNotCached()
    {
        var error = OperationError.Create("test.evaluation.failed", "Evaluation failed.");
        var provider = new ControlledEvaluationProvider { EvaluationFailures = { [1] = error } };
        using var service = CreateService(provider);

        var failed = await service.LoadAsync(App);
        var catalogAfterFailure = await service.GetProjectsAsync(App);
        var recovered = await service.LoadAsync(App);

        Assert.Multiple(() =>
        {
            Assert.That(failed.IsFailure, Is.True);
            Assert.That(failed.Error, Is.EqualTo(error));
            Assert.That(catalogAfterFailure.IsSuccess, Is.True);
            Assert.That(recovered.IsSuccess, Is.True);
            Assert.That(provider.EvaluationCount, Is.EqualTo(2));
        });
    }

    [Test]
    public async Task FailedCompilerInputsAreNotCached()
    {
        var error = OperationError.Create("test.compiler-inputs.failed", "Compiler inputs failed.");
        var provider = new ControlledEvaluationProvider { CompilerInputFailures = { [1] = error } };
        using var service = CreateService(provider);

        var failed = await service.LoadAsync(App);
        var recovered = await service.LoadAsync(App);

        Assert.Multiple(() =>
        {
            Assert.That(failed.IsFailure, Is.True);
            Assert.That(failed.Error, Is.EqualTo(error));
            Assert.That(recovered.IsSuccess, Is.True);
            Assert.That(provider.CompilerInputCount, Is.EqualTo(2));
        });
    }

    [Test]
    public async Task UnexpectedEvaluationFaultPropagatesAndIsNotCached()
    {
        var provider = new ControlledEvaluationProvider();
        provider.OnEvaluate = (call, _) => call == 1
            ? throw new InvalidOperationException("Provider invariant broken.")
            : Task.CompletedTask;
        using var service = CreateService(provider);

        Assert.ThrowsAsync<InvalidOperationException>(async () => await service.LoadAsync(App));
        var recovered = await service.LoadAsync(App);

        Assert.Multiple(() =>
        {
            Assert.That(recovered.IsSuccess, Is.True);
            Assert.That(provider.EvaluationCount, Is.EqualTo(2));
        });
    }

    [Test]
    public async Task CancellingOneConsumerDoesNotCancelTheSharedEvaluation()
    {
        var gate = NewGate();
        var provider = new ControlledEvaluationProvider { OnEvaluate = async (_, token) => await gate.Task.WaitAsync(token) };
        using var service = CreateService(provider);
        using var cancellation = new CancellationTokenSource();

        var cancelled = service.LoadAsync(App, cancellation.Token);
        var remaining = service.LoadAsync(App);
        await provider.EvaluationStarted.Task.WaitAsync(Timeout);
        cancellation.Cancel();
        Assert.CatchAsync<OperationCanceledException>(async () => await cancelled);
        var evaluationCancelled = provider.EvaluationTokens.Single().IsCancellationRequested;
        gate.SetResult();
        var result = await remaining.WaitAsync(Timeout);

        Assert.Multiple(() =>
        {
            Assert.That(evaluationCancelled, Is.False);
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(provider.EvaluationCount, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task EvaluationOutlivesItsOnlyCancelledConsumerAndServesTheNextRequest()
    {
        var gate = NewGate();
        var provider = new ControlledEvaluationProvider { OnEvaluate = async (_, token) => await gate.Task.WaitAsync(token) };
        using var service = CreateService(provider);
        using var cancellation = new CancellationTokenSource();

        var cancelled = service.LoadAsync(App, cancellation.Token);
        await provider.EvaluationStarted.Task.WaitAsync(Timeout);
        cancellation.Cancel();
        Assert.CatchAsync<OperationCanceledException>(async () => await cancelled);
        var next = service.LoadAsync(App);
        gate.SetResult();
        var result = await next.WaitAsync(Timeout);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(provider.EvaluationTokens.Single().IsCancellationRequested, Is.False);
            Assert.That(provider.EvaluationCount, Is.EqualTo(1));
        });
    }

    [Test]
    public void AlreadyCancelledRequestDoesNotStartEvaluation()
    {
        var provider = new ControlledEvaluationProvider();
        using var service = CreateService(provider);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.CatchAsync<OperationCanceledException>(async () => await service.LoadAsync(App, cancellation.Token));
        Assert.That(provider.EvaluationCount, Is.Zero);
    }

    [Test]
    public async Task SwitchingWorkspaceCancelsAnEvaluationNobodyIsWaitingFor()
    {
        var gate = NewGate();
        var provider = new ControlledEvaluationProvider();
        provider.OnEvaluate = async (call, token) =>
        {
            if (call == 1)
            {
                await gate.Task.WaitAsync(token);
            }
        };
        using var service = CreateService(provider);
        using var cancellation = new CancellationTokenSource();

        var abandoned = service.LoadAsync(App, cancellation.Token);
        await provider.EvaluationStarted.Task.WaitAsync(Timeout);
        cancellation.Cancel();
        Assert.CatchAsync<OperationCanceledException>(async () => await abandoned);
        var other = await service.LoadAsync(Other).WaitAsync(Timeout);

        Assert.Multiple(() =>
        {
            Assert.That(other.IsSuccess, Is.True);
            Assert.That(other.Value!.Projects.Single().Path, Is.EqualTo(Other.Path));
            Assert.That(provider.EvaluationTokens[0].IsCancellationRequested, Is.True);
            Assert.That(provider.EvaluationTokens[1].IsCancellationRequested, Is.False);
        });
    }

    [Test]
    public async Task SwitchingWorkspaceLetsRemainingConsumersOfThePreviousOneFinish()
    {
        var gate = NewGate();
        var provider = new ControlledEvaluationProvider();
        provider.OnEvaluate = async (call, token) =>
        {
            if (call == 1)
            {
                await gate.Task.WaitAsync(token);
            }
        };
        using var service = CreateService(provider);

        var previous = service.LoadAsync(App);
        await provider.EvaluationStarted.Task.WaitAsync(Timeout);
        var other = await service.LoadAsync(Other).WaitAsync(Timeout);
        var previousCancelledWhileWaiting = provider.EvaluationTokens[0].IsCancellationRequested;
        gate.SetResult();
        var previousResult = await previous.WaitAsync(Timeout);

        Assert.Multiple(() =>
        {
            Assert.That(other.IsSuccess, Is.True);
            Assert.That(previousCancelledWhileWaiting, Is.False);
            Assert.That(previousResult.IsSuccess, Is.True);
            Assert.That(previousResult.Value!.Projects.Single().Path, Is.EqualTo(App.Path));
            // The late consumer finishes on its own snapshot instead of displacing the new workspace.
            Assert.That(provider.EvaluationCount, Is.EqualTo(2));
        });
    }

    [Test]
    public async Task ReturningToAWorkspaceEvaluatesItAgain()
    {
        var provider = new ControlledEvaluationProvider();
        using var service = CreateService(provider);

        await service.LoadAsync(App);
        await service.LoadAsync(Other);
        var returned = await service.LoadAsync(App);

        Assert.Multiple(() =>
        {
            Assert.That(returned.IsSuccess, Is.True);
            Assert.That(provider.EvaluationCount, Is.EqualTo(3));
        });
    }

    [Test]
    public async Task DisposeStopsEvaluationAndReportsItToWaitingConsumers()
    {
        var provider = new ControlledEvaluationProvider
        {
            OnEvaluate = async (_, token) => await Task.Delay(System.Threading.Timeout.Infinite, token),
        };
        var service = CreateService(provider);

        var pending = service.LoadAsync(App);
        await provider.EvaluationStarted.Task.WaitAsync(Timeout);
        service.Dispose();
        service.Dispose();
        var result = await pending.WaitAsync(Timeout);
        var afterDispose = await service.GetProjectsAsync(App);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error, Is.EqualTo(WorkspaceProjectGraphErrors.EvaluationAbandoned));
            Assert.That(provider.EvaluationTokens.Single().IsCancellationRequested, Is.True);
            Assert.That(afterDispose.IsFailure, Is.True);
            Assert.That(afterDispose.Error, Is.EqualTo(WorkspaceProjectGraphErrors.EvaluationAbandoned));
            Assert.That(provider.EvaluationCount, Is.EqualTo(1));
        });
    }

    private static TaskCompletionSource NewGate() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static WorkspaceDescriptor CreateWorkspace(string name) =>
        new(Path.Combine(Path.GetTempPath(), "toren-graph-service", name, $"{name}.csproj"), name, WorkspaceKind.Project);

    private static WorkspaceProjectGraphService CreateService(
        ControlledEvaluationProvider provider,
        FakeStampProvider? stamps = null) =>
        new(
            new WorkspaceProjectGraphEvaluator(new UnusedProjectProvider(), new UnusedProjectProvider(), provider),
            stamps ?? new FakeStampProvider());

    private sealed class ControlledEvaluationProvider : IProjectEvaluationProvider
    {
        private readonly Lock _gate = new();
        private int _evaluationCount;
        private int _compilerInputCount;

        /// <summary>Runs before an evaluation returns; receives the 1-based call number.</summary>
        public Func<int, CancellationToken, Task> OnEvaluate { get; set; } = static (_, _) => Task.CompletedTask;

        public Func<int, CancellationToken, Task> OnResolve { get; set; } = static (_, _) => Task.CompletedTask;

        public Dictionary<int, OperationError> EvaluationFailures { get; } = [];

        public Dictionary<int, OperationError> CompilerInputFailures { get; } = [];

        public TaskCompletionSource EvaluationStarted { get; } = NewGate();

        public List<CancellationToken> EvaluationTokens { get; } = [];

        public int EvaluationCount => Volatile.Read(ref _evaluationCount);

        public int CompilerInputCount => Volatile.Read(ref _compilerInputCount);

        public async Task<Result<IReadOnlyList<ProjectEvaluation>>> EvaluateAsync(
            IReadOnlyList<string> projectPaths,
            CancellationToken cancellationToken = default)
        {
            var call = Interlocked.Increment(ref _evaluationCount);
            lock (_gate)
            {
                EvaluationTokens.Add(cancellationToken);
            }

            EvaluationStarted.TrySetResult();
            await OnEvaluate(call, cancellationToken);
            return EvaluationFailures.TryGetValue(call, out var error)
                ? Result.Failure<IReadOnlyList<ProjectEvaluation>>(error)
                : Result.Success<IReadOnlyList<ProjectEvaluation>>(projectPaths
                    .Select(static _ => new ProjectEvaluation(
                        new ProjectMetadata(["net10.0"], "Exe", "App", "App", false, false, null, null, null),
                        []))
                    .ToArray());
        }

        public async Task<Result<IReadOnlyList<ProjectMetadata>>> ResolveCompilerInputsAsync(
            IReadOnlyList<WorkspaceProject> projects,
            CancellationToken cancellationToken = default)
        {
            var call = Interlocked.Increment(ref _compilerInputCount);
            await OnResolve(call, cancellationToken);
            return CompilerInputFailures.TryGetValue(call, out var error)
                ? Result.Failure<IReadOnlyList<ProjectMetadata>>(error)
                : Result.Success<IReadOnlyList<ProjectMetadata>>(projects
                    .Select(static project => project.Metadata with { ReferencePaths = [] })
                    .ToArray());
        }
    }

    private sealed class FakeStampProvider : IProjectEvaluationInputStampProvider
    {
        public string WorkspaceStamp { get; set; } = "workspace";

        public string ProjectStamp { get; set; } = "projects";

        public DateTime LastWriteTimeUtc { get; set; } = DateTime.MinValue;

        public Task<string> GetWorkspaceStampAsync(
            WorkspaceDescriptor workspace,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(WorkspaceStamp);
        }

        public Task<ProjectInputStamp> GetProjectStampAsync(
            IReadOnlyList<WorkspaceProject> projects,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new ProjectInputStamp(ProjectStamp, LastWriteTimeUtc));
    }

    private sealed class UnusedProjectProvider : IFolderProjectProvider, ISolutionProjectProvider
    {
        Task<Result<IReadOnlyList<string>>> IFolderProjectProvider.GetProjectPathsAsync(
            string folderPath,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        Task<Result<IReadOnlyList<string>>> ISolutionProjectProvider.GetProjectPathsAsync(
            string solutionPath,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
