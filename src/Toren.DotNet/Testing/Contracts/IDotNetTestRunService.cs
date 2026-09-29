using Toren.Core.Execution.Models;
using Toren.Core.Results;
using Toren.DotNet.Testing.Models;

namespace Toren.DotNet.Testing.Contracts;

public interface IDotNetTestRunService
{
    Task<Result<ProcessResult>> RunAsync(
        DotNetTestRunRequest request,
        Action<ProcessOutputLine> onOutput,
        CancellationToken cancellationToken = default);
}
