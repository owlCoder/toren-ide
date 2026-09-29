using NUnit.Framework;
using Toren.App.Terminal.ViewModels;
using Toren.Core.Execution.Contracts;
using Toren.Core.Execution.Models;
using Toren.Core.Results;

namespace Toren.UnitTests.Terminal;

[TestFixture]
public sealed class TerminalHostViewModelTests
{
    [Test]
    public async Task AddAndCloseSessionKeepOtherProcessAlive()
    {
        var firstProcess = new StubInteractiveProcessSession(101);
        var secondProcess = new StubInteractiveProcessSession(202);
        var runner = new QueueInteractiveProcessRunner(firstProcess, secondProcess);
        var shellProvider = new StubNativeShellProvider();
        var first = new TerminalViewModel(runner, shellProvider, "Terminal 1");
        await using var host = new TerminalHostViewModel(first);
        host.SetWorkingDirectory(Path.GetTempPath());

        await first.StartAsync();
        var second = host.AddSession();
        await second.StartAsync();

        Assert.Multiple(() =>
        {
            Assert.That(host.Sessions, Has.Count.EqualTo(2));
            Assert.That(host.SelectedSession, Is.SameAs(second));
            Assert.That(host.CanCloseSession, Is.True);
            Assert.That(first.IsRunning, Is.True);
            Assert.That(second.IsRunning, Is.True);
            Assert.That(second.Title, Is.EqualTo("Terminal 2"));
        });

        await host.CloseSelectedSessionAsync();

        Assert.Multiple(() =>
        {
            Assert.That(host.Sessions, Has.Count.EqualTo(1));
            Assert.That(host.SelectedSession, Is.SameAs(first));
            Assert.That(firstProcess.Terminated, Is.False);
            Assert.That(secondProcess.Terminated, Is.True);
            Assert.That(first.IsRunning, Is.True);
        });
    }

    [Test]
    public void NewSessionUsesLatestWorkspaceDirectory()
    {
        var runner = new QueueInteractiveProcessRunner(new StubInteractiveProcessSession(101));
        var shellProvider = new StubNativeShellProvider();
        var first = new TerminalViewModel(runner, shellProvider, "Terminal 1");
        using var directory = new TemporaryDirectory();
        var host = new TerminalHostViewModel(first);
        host.SetWorkingDirectory(directory.Path);

        var second = host.AddSession();

        Assert.Multiple(() =>
        {
            Assert.That(first.WorkingDirectory, Is.EqualTo(directory.Path));
            Assert.That(second.WorkingDirectory, Is.EqualTo(directory.Path));
        });
    }

    private sealed class StubNativeShellProvider : INativeShellProvider
    {
        public ProcessRequest CreateShellRequest(string workingDirectory) =>
            new("test-shell", [], workingDirectory);
    }

    private sealed class QueueInteractiveProcessRunner(params StubInteractiveProcessSession[] sessions)
        : IInteractiveProcessRunner
    {
        private readonly Queue<StubInteractiveProcessSession> _sessions = new(sessions);

        public Task<Result<IInteractiveProcessSession>> StartAsync(
            ProcessRequest request,
            Action<ProcessOutputLine> onOutput,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IInteractiveProcessSession session = _sessions.Dequeue();
            return Task.FromResult(Result.Success(session));
        }
    }

    private sealed class StubInteractiveProcessSession(int processId) : IInteractiveProcessSession
    {
        private readonly TaskCompletionSource<Result<ProcessResult>> _completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int ProcessId { get; } = processId;

        public bool HasExited => _completion.Task.IsCompleted;

        public Task<Result<ProcessResult>> Completion => _completion.Task;

        public bool Terminated { get; private set; }

        public Task<Result<bool>> WriteLineAsync(
            string text,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success(true));

        public Task<Result<bool>> TerminateAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Terminated = true;
            _completion.TrySetResult(Result.Success(new ProcessResult(0, string.Empty, string.Empty)));
            return Task.FromResult(Result.Success(true));
        }

        public ValueTask DisposeAsync()
        {
            if (!HasExited)
            {
                Terminated = true;
                _completion.TrySetResult(Result.Success(new ProcessResult(0, string.Empty, string.Empty)));
            }

            return ValueTask.CompletedTask;
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"toren-terminal-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
