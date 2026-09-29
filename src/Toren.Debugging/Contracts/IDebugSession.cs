using Toren.Core.Results;
using Toren.Debugging.Models;

namespace Toren.Debugging.Contracts;

public interface IDebugSession : IAsyncDisposable
{
    int ProcessId { get; }

    Task<Result<DebugStopInfo>> WaitForStopAsync(CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<DebugBreakpoint>>> SetBreakpointsAsync(
        string sourcePath,
        IReadOnlyList<DebugSourceBreakpoint> breakpoints,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<DebugStackFrame>>> GetStackTraceAsync(
        int threadId,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<DebugScope>>> GetScopesAsync(
        int frameId,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<DebugVariable>>> GetVariablesAsync(
        int variablesReference,
        CancellationToken cancellationToken = default);

    Task<Result<DebugEvaluationResult>> EvaluateAsync(
        string expression,
        int? frameId = null,
        DebugEvaluationContext context = DebugEvaluationContext.Watch,
        CancellationToken cancellationToken = default);

    Task<Result<bool>> ContinueAsync(int threadId, CancellationToken cancellationToken = default);

    Task<Result<bool>> PauseAsync(int threadId, CancellationToken cancellationToken = default);

    Task<Result<bool>> StepOverAsync(int threadId, CancellationToken cancellationToken = default);

    Task<Result<bool>> StepIntoAsync(int threadId, CancellationToken cancellationToken = default);

    Task<Result<bool>> StepOutAsync(int threadId, CancellationToken cancellationToken = default);

    Task<Result<bool>> RunToCursorAsync(
        int threadId,
        string sourcePath,
        int line,
        int? column = null,
        CancellationToken cancellationToken = default);

    Task<Result<bool>> RestartAsync(CancellationToken cancellationToken = default);

    Task<Result<bool>> StopAsync(CancellationToken cancellationToken = default);

    Task<Result<bool>> DisconnectAsync(CancellationToken cancellationToken = default);
}
