using Toren.Core.Execution.Models;
using Toren.Core.Results;

namespace Toren.Core.Execution.Contracts;

public interface IInteractiveProcessSession : IAsyncDisposable
{
    int ProcessId { get; }

    bool HasExited { get; }

    Task<Result<ProcessResult>> Completion { get; }

    Task<Result<bool>> WriteLineAsync(
        string text,
        CancellationToken cancellationToken = default);

    Task<Result<bool>> TerminateAsync(CancellationToken cancellationToken = default);
}
