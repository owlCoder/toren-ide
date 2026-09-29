using NUnit.Framework;
using Toren.App.Execution.ViewModels;
using Toren.Core.Results;
using Toren.DotNet.Execution.Contracts;
using Toren.DotNet.Execution.Models;
using Toren.Workspaces.Models;

namespace Toren.UnitTests.Execution;

[TestFixture]
public sealed class WorkspaceExecutionViewModelTests
{
    [Test]
    public async Task BuildMapsSolutionWorkspaceAndCapturesStructuredOutput()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var solutionPath = Path.Combine(directory, "Sample.sln");
            var service = new CapturingCommandService(
                Result.Success(
                    new DotNetCommandResult(
                        DotNetCommandKind.Build,
                        1,
                        "compile line",
                        "error line")));
            using var viewModel = new WorkspaceExecutionViewModel(service)
            {
                ConfigurationIndex = 1,
            };
            viewModel.SetWorkspace(new WorkspaceDescriptor(solutionPath, "Sample", WorkspaceKind.Solution));

            await viewModel.ExecuteAsync(DotNetCommandKind.Build);

            Assert.Multiple(() =>
            {
                Assert.That(service.LastRequest, Is.Not.Null);
                Assert.That(service.LastRequest!.WorkingDirectory, Is.EqualTo(directory));
                Assert.That(service.LastRequest.TargetPath, Is.EqualTo(Path.GetFullPath(solutionPath)));
                Assert.That(service.LastRequest.Configuration, Is.EqualTo("Release"));
                Assert.That(viewModel.OutputLines, Has.Count.EqualTo(4));
                Assert.That(viewModel.OutputLines[0].IsCommand, Is.True);
                Assert.That(viewModel.OutputLines[1].IsStandardOutput, Is.True);
                Assert.That(viewModel.OutputLines[2].IsStandardError, Is.True);
                Assert.That(viewModel.OutputLines[3].IsStatus, Is.True);
                Assert.That(viewModel.StatusText, Does.Contain("exit code 1"));
                Assert.That(viewModel.CanExecute, Is.True);
            });
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public async Task FolderWorkspaceUsesWorkingDirectoryWithoutTarget()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var service = new CapturingCommandService(
                Result.Success(new DotNetCommandResult(DotNetCommandKind.Restore, 0, string.Empty, string.Empty)));
            using var viewModel = new WorkspaceExecutionViewModel(service);
            viewModel.SetWorkspace(new WorkspaceDescriptor(directory, "Folder", WorkspaceKind.Folder));

            await viewModel.ExecuteAsync(DotNetCommandKind.Restore);

            Assert.Multiple(() =>
            {
                Assert.That(service.LastRequest, Is.Not.Null);
                Assert.That(service.LastRequest!.WorkingDirectory, Is.EqualTo(Path.GetFullPath(directory)));
                Assert.That(service.LastRequest.TargetPath, Is.Null);
                Assert.That(service.LastRequest.Configuration, Is.Null);
            });
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public async Task CancelStopsActiveCommandAndRestoresExecutionState()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var service = new BlockingCommandService();
            using var viewModel = new WorkspaceExecutionViewModel(service);
            viewModel.SetWorkspace(new WorkspaceDescriptor(directory, "Folder", WorkspaceKind.Folder));

            var execution = viewModel.ExecuteAsync(DotNetCommandKind.Build);
            await service.Started.Task;
            Assert.That(viewModel.CanCancel, Is.True);

            viewModel.Cancel();
            await execution;

            Assert.Multiple(() =>
            {
                Assert.That(viewModel.IsRunning, Is.False);
                Assert.That(viewModel.CanExecute, Is.True);
                Assert.That(viewModel.StatusText, Is.EqualTo("Build canceled."));
                Assert.That(viewModel.OutputLines[^1].IsStatus, Is.True);
            });
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"toren-execution-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }

    private sealed class CapturingCommandService(Result<DotNetCommandResult> result) : IDotNetCommandService
    {
        public DotNetCommandRequest? LastRequest { get; private set; }

        public Task<Result<DotNetCommandResult>> ExecuteAsync(
            DotNetCommandRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastRequest = request;
            return Task.FromResult(result);
        }
    }

    private sealed class BlockingCommandService : IDotNetCommandService
    {
        public TaskCompletionSource<bool> Started { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<Result<DotNetCommandResult>> ExecuteAsync(
            DotNetCommandRequest request,
            CancellationToken cancellationToken = default)
        {
            Started.TrySetResult(true);
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("The blocking command should only finish through cancellation.");
        }
    }
}
