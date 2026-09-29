using Toren.Core.Results;
using Toren.DotNet.Execution.Models;

namespace Toren.DotNet.Execution.Contracts;

public interface IStreamingDotNetCommandService : IDotNetCommandService
{
    Task<Result<DotNetCommandResult>> ExecuteStreamingAsync(
        DotNetCommandRequest request,
        Action<DotNetCommandOutputLine> onOutput,
        CancellationToken cancellationToken = default);
}
