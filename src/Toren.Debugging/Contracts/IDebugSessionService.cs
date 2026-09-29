using Toren.Core.Results;

namespace Toren.Debugging.Contracts;

public interface IDebugSessionService
{
    Task<Result<IDebugSession>> AttachAsync(
        int processId,
        string? workingDirectory = null,
        CancellationToken cancellationToken = default);
}
