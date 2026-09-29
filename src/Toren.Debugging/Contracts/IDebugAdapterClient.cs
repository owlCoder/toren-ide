using Toren.Core.Results;
using Toren.Debugging.Protocol;

namespace Toren.Debugging.Contracts;

public interface IDebugAdapterClient : IAsyncDisposable
{
    Task<Result<DapProtocolMessage>> SendRequestAsync(
        string command,
        object? arguments = null,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<DapProtocolMessage> ReadNotificationsAsync(
        CancellationToken cancellationToken = default);

    Task<int> WaitForExitAsync(CancellationToken cancellationToken = default);

    void Terminate();
}
