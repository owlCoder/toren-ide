using Toren.Core.Execution.Models;
using Toren.Core.Results;

namespace Toren.DotNet.Testing.Contracts;

public interface IDotNetTestDebugSession : IAsyncDisposable
{
    int ProcessId { get; }

    Task<Result<ProcessResult>> Completion { get; }

    void Terminate();
}
