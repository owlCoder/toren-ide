using NUnit.Framework;
using Toren.App.Terminal.ViewModels;
using Toren.Core.Execution.Contracts;
using Toren.Core.Execution.Models;
using Toren.Core.Results;

namespace Toren.UnitTests.Terminal;

[TestFixture]
public sealed class TerminalViewModelTests
{
    [Test]
    public async Task StartSubmitAndStopUseInteractiveShellSession()
    {
        var session = new StubInteractiveProcessSession();
        var runner = new StubInteractiveProcessRunner(session);
        var shellProvider = new StubNativeShellProvider();
        await using var viewModel = new TerminalViewModel(runner, shellProvider);
        var workingDirectory = Path.GetTempPath();
        viewModel.SetWorkingDirectory(workingDirectory);

        await viewModel.StartAsync();
        viewModel.InputText = "dotnet --info";
        await viewModel.SubmitAsync();
        await viewModel.StopAsync();

        Assert.Multiple(() =>
        {
            Assert.That(shellProvider.WorkingDirectories, Has.Count.EqualTo(1));
            Assert.That(shellProvider.WorkingDirectories[0], Is.EqualTo(Path.GetFullPath(workingDirectory)));
            Assert.That(runner.Requests, Has.Count.EqualTo(1));
            Assert.That(runner.Requests[0].FileName, Is.EqualTo("test-shell"));
            Assert.That(session.WrittenLines, Has.Count.EqualTo(1));
            Assert.That(session.WrittenLines[0], Is.EqualTo("dotnet --info"));
            Assert.That(session.Terminated, Is.True);
            Assert.That(viewModel.IsRunning, Is.False);
            Assert.That(viewModel.Lines.Select(static line => line.Text), Does.Contain("ready"));
        });
    }

    private sealed class StubNativeShellProvider : INativeShellProvider
    {
        public List<string> WorkingDirectories { get; } = [];

        public ProcessRequest CreateShellRequest(string workingDirectory)
        {
            WorkingDirectories.Add(workingDirectory);
            return new ProcessRequest("test-shell", [], workingDirectory);
        }
    }

    private sealed class StubInteractiveProcessRunner(StubInteractiveProcessSession session)
        : IInteractiveProcessRunner
    {
        public List<ProcessRequest> Requests { get; } = [];

        public Task<Result<IInteractiveProcessSession>> StartAsync(
            ProcessRequest request,
            Action<ProcessOutputLine> onOutput,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(request);
            onOutput(new ProcessOutputLine(ProcessOutputChannel.StandardOutput, "ready"));
            return Task.FromResult(Result.Success<IInteractiveProcessSession>(session));
        }
    }

    private sealed class StubInteractiveProcessSession : IInteractiveProcessSession
    {
        private readonly TaskCompletionSource<Result<ProcessResult>> _completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int ProcessId => 1234;

        public bool HasExited => _completion.Task.IsCompleted;

        public Task<Result<ProcessResult>> Completion => _completion.Task;

        public List<string> WrittenLines { get; } = [];

        public bool Terminated { get; private set; }

        public Task<Result<bool>> WriteLineAsync(
            string text,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            WrittenLines.Add(text);
            return Task.FromResult(Result.Success(true));
        }

        public Task<Result<bool>> TerminateAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Terminated = true;
            _completion.TrySetResult(Result.Success(new ProcessResult(0, string.Empty, string.Empty)));
            return Task.FromResult(Result.Success(true));
        }

        public ValueTask DisposeAsync()
        {
            _completion.TrySetResult(Result.Success(new ProcessResult(0, string.Empty, string.Empty)));
            return ValueTask.CompletedTask;
        }
    }
}
