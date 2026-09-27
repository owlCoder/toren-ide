using Toren.Core.Execution.Contracts;
using Toren.Core.Execution.Models;
using Toren.Core.Results;

namespace Toren.UnitTests.TestDoubles;

internal sealed class FakeProcessRunner(
    string standardOutput,
    int exitCode = 0,
    string standardError = "") : IProcessRunner
{
    public ProcessRequest? LastRequest { get; private set; }

    public Task<Result<ProcessResult>> RunAsync(
        ProcessRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LastRequest = request;
        return Task.FromResult(Result.Success(new ProcessResult(exitCode, standardOutput, standardError)));
    }
}
