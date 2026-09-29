using NUnit.Framework;
using Toren.App.Testing.Models;
using Toren.App.Testing.ViewModels;
using Toren.Core.Execution.Models;
using Toren.Core.Results;
using Toren.DotNet.Testing.Contracts;
using Toren.DotNet.Testing.Models;

namespace Toren.UnitTests.Testing;

[TestFixture]
public sealed class TestExplorerViewModelTests
{
    [Test]
    public async Task RerunFailedProjectsOnlyRunsProjectsThatFailedLastRunAll()
    {
        var projectA = new WorkspaceTestProjectDiscovery(
            Path.GetFullPath(Path.Combine("repo", "ATests.csproj")),
            "A.Tests",
            [new DotNetTestCase("A.Tests.Sample.Passes", "Passes")]);
        var projectB = new WorkspaceTestProjectDiscovery(
            Path.GetFullPath(Path.Combine("repo", "BTests.csproj")),
            "B.Tests",
            [new DotNetTestCase("B.Tests.Sample.Passes", "Passes")]);
        var service = new SequencedTestRunService(1, 0, 0);
        using var viewModel = new TestExplorerViewModel(service);
        viewModel.Replace([projectA, projectB]);

        await viewModel.RunAllAsync();

        Assert.Multiple(() =>
        {
            Assert.That(service.ProjectPaths, Is.EqualTo(new[] { projectA.ProjectPath, projectB.ProjectPath }));
            Assert.That(viewModel.HasFailedProjects, Is.True);
            Assert.That(viewModel.CanRerunFailedProjects, Is.True);
        });

        await viewModel.RerunFailedProjectsAsync();

        Assert.Multiple(() =>
        {
            Assert.That(
                service.ProjectPaths,
                Is.EqualTo(new[] { projectA.ProjectPath, projectB.ProjectPath, projectA.ProjectPath }));
            Assert.That(viewModel.HasFailedProjects, Is.False);
            Assert.That(viewModel.CanRerunFailedProjects, Is.False);
            Assert.That(viewModel.StatusText, Is.EqualTo("All previously failed test projects passed."));
        });
    }

    [Test]
    public async Task DebugTestPublishesAttachProcessAndCompletesSession()
    {
        var test = new DotNetTestCase(
            "Sample.Tests.CalculatorTests.Adds_numbers",
            "Adds_numbers",
            "mtp-test-42");
        var project = new WorkspaceTestProjectDiscovery(
            Path.GetFullPath(Path.Combine("repo", "Sample.Tests.csproj")),
            "Sample.Tests",
            [test]);
        var debugService = new StubTestDebugService(4321);
        using var viewModel = new TestExplorerViewModel(
            new SequencedTestRunService(0),
            debugService);
        viewModel.Replace([project]);

        var debugTask = viewModel.DebugTestAsync(project, test);
        await debugService.Started.Task.WaitAsync(TimeSpan.FromSeconds(1));
        await Task.Yield();

        Assert.Multiple(() =>
        {
            Assert.That(debugService.Request, Is.Not.Null);
            Assert.That(debugService.Request!.ProjectPath, Is.EqualTo(project.ProjectPath));
            Assert.That(debugService.Request.FullyQualifiedName, Is.EqualTo(test.FullyQualifiedName));
            Assert.That(debugService.Request.RunnerId, Is.EqualTo(test.RunnerId));
            Assert.That(viewModel.IsRunning, Is.True);
            Assert.That(viewModel.CanStop, Is.True);
            Assert.That(viewModel.StatusText, Does.Contain("4321"));
            Assert.That(viewModel.OutputLines.Any(line => line.Text.Contains("process 4321", StringComparison.Ordinal)), Is.True);
        });

        debugService.Session.Complete(0);
        await debugTask;

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.IsRunning, Is.False);
            Assert.That(viewModel.CanDebug, Is.True);
            Assert.That(viewModel.StatusText, Is.EqualTo("Adds_numbers debug session completed."));
            Assert.That(debugService.Session.IsDisposed, Is.True);
        });
    }

    private sealed class SequencedTestRunService(params int[] exitCodes) : IDotNetTestRunService
    {
        private readonly Queue<int> _exitCodes = new(exitCodes);

        public List<string> ProjectPaths { get; } = [];

        public Task<Result<ProcessResult>> RunAsync(
            DotNetTestRunRequest request,
            Action<ProcessOutputLine> onOutput,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ProjectPaths.Add(request.ProjectPath);
            var exitCode = _exitCodes.Dequeue();
            return Task.FromResult(Result.Success(new ProcessResult(exitCode, string.Empty, string.Empty)));
        }
    }

    private sealed class StubTestDebugService(int processId) : IDotNetTestDebugService
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public StubTestDebugSession Session { get; } = new(processId);

        public DotNetTestRunRequest? Request { get; private set; }

        public Task<Result<IDotNetTestDebugSession>> StartAsync(
            DotNetTestRunRequest request,
            Action<ProcessOutputLine> onOutput,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Request = request;
            onOutput(new ProcessOutputLine(
                ProcessOutputChannel.StandardOutput,
                $"Waiting for debugger to attach... Process Id: {Session.ProcessId}"));
            Started.TrySetResult();
            return Task.FromResult(Result.Success<IDotNetTestDebugSession>(Session));
        }
    }

    private sealed class StubTestDebugSession(int processId) : IDotNetTestDebugSession
    {
        private readonly TaskCompletionSource<Result<ProcessResult>> _completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int ProcessId { get; } = processId;

        public Task<Result<ProcessResult>> Completion => _completion.Task;

        public bool IsDisposed { get; private set; }

        public void Complete(int exitCode)
        {
            _completion.TrySetResult(
                Result.Success(new ProcessResult(exitCode, string.Empty, string.Empty)));
        }

        public void Terminate()
        {
            Complete(1);
        }

        public ValueTask DisposeAsync()
        {
            IsDisposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
