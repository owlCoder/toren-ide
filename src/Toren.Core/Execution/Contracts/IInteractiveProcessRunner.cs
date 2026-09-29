using Toren.Core.Execution.Models;
using Toren.Core.Results;

namespace Toren.Core.Execution.Contracts;

public interface IInteractiveProcessRunner
{
    Task<Result<IInteractiveProcessSession>> StartAsync(
        ProcessRequest request,
        Action<ProcessOutputLine> onOutput,
        CancellationToken cancellationToken = default);
}
