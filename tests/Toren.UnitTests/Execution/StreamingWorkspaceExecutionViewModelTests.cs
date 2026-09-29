using NUnit.Framework;
using Toren.App.Execution.ViewModels;
using Toren.Core.Results;
using Toren.DotNet.Execution.Contracts;
using Toren.DotNet.Execution.Models;
using Toren.Workspaces.Models;

namespace Toren.UnitTests.Execution;

[TestFixture]
public sealed class StreamingWorkspaceExecutionViewModelTests
{
    [Test]
    public async Task StreamingCommandAddsLiveLinesWithoutDuplicatingBufferedResult()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var service = new FakeStreamingCommandService();
            using var viewModel = new WorkspaceExecutionViewModel(service);
            viewModel.SetWorkspace(new WorkspaceDescriptor(directory, "Workspace", WorkspaceKind.Folder));

            await viewModel.ExecuteAsync(DotNetCommandKind.Build);

            Assert.Multiple(() =>
            {
                Assert.That(viewModel.OutputLines, Has.Count.EqualTo(4));
                Assert.That(viewModel.OutputLines[0].IsCommand, Is.True);
                Assert.That(viewModel.OutputLines[1].Text, Is.EqualTo("compile output"));
                Assert.That(viewModel.OutputLines[1].IsStandardOutput, Is.True);
                Assert.That(viewModel.OutputLines[2].Text, Is.EqualTo("compile warning"));
                Assert.That(viewModel.OutputLines[2].IsStandardError, Is.True);
                Assert.That(viewModel.OutputLines[3].IsStatus, Is.True);
            });
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"toren-streaming-vm-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }

    private sealed class FakeStreamingCommandService : IStreamingDotNetCommandService
    {
        public Task<Result<DotNetCommandResult>> ExecuteAsync(
            DotNetCommandRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(
                Result.Success(
                    new DotNetCommandResult(request.Kind, 0, "compile output", "compile warning")));

        public Task<Result<DotNetCommandResult>> ExecuteStreamingAsync(
            DotNetCommandRequest request,
            Action<DotNetCommandOutputLine> onOutput,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            onOutput(new DotNetCommandOutputLine(
                DotNetCommandOutputStream.StandardOutput,
                "compile output"));
            onOutput(new DotNetCommandOutputLine(
                DotNetCommandOutputStream.StandardError,
                "compile warning"));
            return Task.FromResult(
                Result.Success(
                    new DotNetCommandResult(request.Kind, 0, "compile output", "compile warning")));
        }
    }
}
