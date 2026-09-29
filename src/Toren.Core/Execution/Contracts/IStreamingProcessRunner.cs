using Toren.Core.Execution.Models;
using Toren.Core.Results;

namespace Toren.Core.Execution.Contracts;

public interface IStreamingProcessRunner : IProcessRunner
{
    Task<Result<ProcessResult>> RunStreamingAsync(
        ProcessRequest request,
        Action<ProcessOutputLine> onOutput,
        CancellationToken cancellationToken = default);
}
