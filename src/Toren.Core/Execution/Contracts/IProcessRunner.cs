using Toren.Core.Execution.Models;
using Toren.Core.Results;

namespace Toren.Core.Execution.Contracts;

public interface IProcessRunner
{
    Task<Result<ProcessResult>> RunAsync(
        ProcessRequest request,
        CancellationToken cancellationToken = default);
}
