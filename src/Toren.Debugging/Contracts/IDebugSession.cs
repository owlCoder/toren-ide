using Toren.Core.Results;
using Toren.Debugging.Models;

namespace Toren.Debugging.Contracts;

public interface IDebugSession : IAsyncDisposable
{
    int ProcessId { get; }

    Task<Result<DebugStopInfo>> WaitForStopAsync(CancellationToken cancellationToken = default);

    Task<Result<bool>> ContinueAsync(int threadId, CancellationToken cancellationToken = default);

    Task<Result<bool>> PauseAsync(int threadId, CancellationToken cancellationToken = default);

    Task<Result<bool>> StepOverAsync(int threadId, CancellationToken cancellationToken = default);

    Task<Result<bool>> StepIntoAsync(int threadId, CancellationToken cancellationToken = default);

    Task<Result<bool>> StepOutAsync(int threadId, CancellationToken cancellationToken = default);

    Task<Result<bool>> DisconnectAsync(CancellationToken cancellationToken = default);
}
