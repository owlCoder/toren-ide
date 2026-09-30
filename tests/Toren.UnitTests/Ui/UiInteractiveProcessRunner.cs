using Toren.Core.Execution.Contracts;
using Toren.Core.Execution.Models;
using Toren.Core.Results;

namespace Toren.UnitTests.Ui;

internal sealed class UiInteractiveProcessRunner : IInteractiveProcessRunner
{
    public List<Session> Sessions { get; } = [];

    public Task<Result<IInteractiveProcessSession>> StartAsync(ProcessRequest request, Action<ProcessOutputLine> onOutput,
        CancellationToken cancellationToken = default)
    {
        var session = new Session();
        Sessions.Add(session);
        onOutput(new ProcessOutputLine(ProcessOutputChannel.StandardOutput, "Shell ready"));
        return Task.FromResult(Result.Success<IInteractiveProcessSession>(session));
    }

    internal sealed class Session : IInteractiveProcessSession
    {
        private readonly TaskCompletionSource<Result<ProcessResult>> _completion = new();
        public int ProcessId => 1234;
        public bool HasExited => _completion.Task.IsCompleted;
        public Task<Result<ProcessResult>> Completion => _completion.Task;
        public bool Killed { get; private set; }
        public Task<Result<bool>> WriteLineAsync(string text, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success(true));
        public Task<Result<bool>> TerminateAsync(CancellationToken cancellationToken = default)
        {
            Killed = true;
            _completion.TrySetResult(Result.Success(new ProcessResult(0, "", "")));
            return Task.FromResult(Result.Success(true));
        }

        public ValueTask DisposeAsync()
        {
            Killed = true;
            _completion.TrySetResult(Result.Success(new ProcessResult(0, "", "")));
            return ValueTask.CompletedTask;
        }
    }
}
