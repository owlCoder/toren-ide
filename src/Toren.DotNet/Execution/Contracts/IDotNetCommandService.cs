using Toren.Core.Results;
using Toren.DotNet.Execution.Models;

namespace Toren.DotNet.Execution.Contracts;

public interface IDotNetCommandService
{
    Task<Result<DotNetCommandResult>> ExecuteAsync(
        DotNetCommandRequest request,
        CancellationToken cancellationToken = default);
}
