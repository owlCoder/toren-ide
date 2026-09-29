using Toren.Core.Results;
using Toren.Debugging.Contracts;
using Toren.Debugging.Models;

namespace Toren.App.Debugging.Services;

public sealed class DebugSessionCoordinator(IDebugSessionService sessionService) : IAsyncDisposable
{
    private const string NoActiveSessionErrorCode = "debug.session.not-active";

    private readonly IDebugSessionService _sessionService = sessionService
        ?? throw new ArgumentNullException(nameof(sessionService));
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IDebugSession? _session;
    private int _stoppedThreadId;
    private bool _disposed;

    public event EventHandler? StateChanged;

    public bool IsAttached => _session is not null;

    public int? ProcessId => _session?.ProcessId;

    public int? StoppedThreadId
    {
        get
        {
            var threadId = Volatile.Read(ref _stoppedThreadId);
            return threadId > 0 ? threadId : null;
        }
    }

    public async Task<Result<bool>> AttachAsync(
        int processId,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(processId);
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await DisposeSessionAsync().ConfigureAwait(false);
            var attached = await _sessionService
                .AttachAsync(processId, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            if (attached.IsFailure)
            {
                return Result.Failure<bool>(attached.Error);
            }

            _session = attached.Value;
            Interlocked.Exchange(ref _stoppedThreadId, 0);
            NotifyStateChanged();
            return Result.Success(true);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<Result<DebugStopInfo>> WaitForStopAsync(
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        IDebugSession? session;
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            session = _session;
        }
        finally
        {
            _gate.Release();
        }

        if (session is null)
        {
            return NoActiveSession<DebugStopInfo>();
        }

        var stopped = await session.WaitForStopAsync(cancellationToken).ConfigureAwait(false);
        if (stopped.IsSuccess)
        {
            Interlocked.Exchange(ref _stoppedThreadId, stopped.Value!.ThreadId);
            NotifyStateChanged();
        }

        return stopped;
    }

    public Task<Result<IReadOnlyList<DebugBreakpoint>>> SetBreakpointsAsync(
        string sourcePath,
        IReadOnlyList<DebugSourceBreakpoint> breakpoints,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentNullException.ThrowIfNull(breakpoints);
        return WithSessionAsync(
            session => session.SetBreakpointsAsync(sourcePath, breakpoints, cancellationToken),
            cancellationToken);
    }

    public Task<Result<IReadOnlyList<DebugStackFrame>>> GetStackTraceAsync(
        int threadId,
        CancellationToken cancellationToken = default) =>
        WithSessionAsync(
            session => session.GetStackTraceAsync(threadId, cancellationToken),
            cancellationToken);

    public Task<Result<IReadOnlyList<DebugScope>>> GetScopesAsync(
        int frameId,
        CancellationToken cancellationToken = default) =>
        WithSessionAsync(
            session => session.GetScopesAsync(frameId, cancellationToken),
            cancellationToken);

    public Task<Result<IReadOnlyList<DebugVariable>>> GetVariablesAsync(
        int variablesReference,
        CancellationToken cancellationToken = default) =>
        WithSessionAsync(
            session => session.GetVariablesAsync(variablesReference, cancellationToken),
            cancellationToken);

    public Task<Result<DebugEvaluationResult>> EvaluateAsync(
        string expression,
        int? frameId = null,
        DebugEvaluationContext context = DebugEvaluationContext.Watch,
        CancellationToken cancellationToken = default) =>
        WithSessionAsync(
            session => session.EvaluateAsync(expression, frameId, context, cancellationToken),
            cancellationToken);

    public async Task<Result<bool>> ContinueAsync(
        int threadId,
        CancellationToken cancellationToken = default)
    {
        var result = await WithSessionAsync(
            session => session.ContinueAsync(threadId, cancellationToken),
            cancellationToken).ConfigureAwait(false);
        ClearStoppedThreadOnSuccess(result);
        return result;
    }

    public Task<Result<bool>> PauseAsync(
        int threadId,
        CancellationToken cancellationToken = default) =>
        WithSessionAsync(
            session => session.PauseAsync(threadId, cancellationToken),
            cancellationToken);

    public Task<Result<bool>> StepOverAsync(
        int threadId,
        CancellationToken cancellationToken = default) =>
        ResumeAsync(
            session => session.StepOverAsync(threadId, cancellationToken),
            cancellationToken);

    public Task<Result<bool>> StepIntoAsync(
        int threadId,
        CancellationToken cancellationToken = default) =>
        ResumeAsync(
            session => session.StepIntoAsync(threadId, cancellationToken),
            cancellationToken);

    public Task<Result<bool>> StepOutAsync(
        int threadId,
        CancellationToken cancellationToken = default) =>
        ResumeAsync(
            session => session.StepOutAsync(threadId, cancellationToken),
            cancellationToken);

    public Task<Result<bool>> RunToCursorAsync(
        int threadId,
        string sourcePath,
        int line,
        int? column = null,
        CancellationToken cancellationToken = default) =>
        ResumeAsync(
            session => session.RunToCursorAsync(
                threadId,
                sourcePath,
                line,
                column,
                cancellationToken),
            cancellationToken);

    public Task<Result<bool>> RestartAsync(CancellationToken cancellationToken = default) =>
        ResumeAsync(
            session => session.RestartAsync(cancellationToken),
            cancellationToken);

    public async Task<Result<bool>> StopAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_session is null)
            {
                return NoActiveSession<bool>();
            }

            var stopped = await _session.StopAsync(cancellationToken).ConfigureAwait(false);
            if (stopped.IsFailure)
            {
                return stopped;
            }

            await DisposeSessionAsync().ConfigureAwait(false);
            Interlocked.Exchange(ref _stoppedThreadId, 0);
            NotifyStateChanged();
            return stopped;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<Result<bool>> DisconnectAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_session is null)
            {
                return Result.Success(true);
            }

            var disconnected = await _session.DisconnectAsync(cancellationToken).ConfigureAwait(false);
            await DisposeSessionAsync().ConfigureAwait(false);
            Interlocked.Exchange(ref _stoppedThreadId, 0);
            NotifyStateChanged();
            return disconnected;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Interlocked.Exchange(ref _stoppedThreadId, 0);
            await DisposeSessionAsync().ConfigureAwait(false);
            NotifyStateChanged();
        }
        finally
        {
            _gate.Release();
            _gate.Dispose();
        }
    }

    private async Task<Result<bool>> ResumeAsync(
        Func<IDebugSession, Task<Result<bool>>> action,
        CancellationToken cancellationToken)
    {
        var result = await WithSessionAsync(action, cancellationToken).ConfigureAwait(false);
        ClearStoppedThreadOnSuccess(result);
        return result;
    }

    private void ClearStoppedThreadOnSuccess(Result<bool> result)
    {
        if (result.IsSuccess)
        {
            Interlocked.Exchange(ref _stoppedThreadId, 0);
            NotifyStateChanged();
        }
    }

    private async Task<Result<T>> WithSessionAsync<T>(
        Func<IDebugSession, Task<Result<T>>> action,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(action);

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return _session is null
                ? NoActiveSession<T>()
                : await action(_session).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private static Result<T> NoActiveSession<T>() =>
        Result.Failure<T>(
            OperationError.Create(
                NoActiveSessionErrorCode,
                "No debug session is currently attached."));

    private async ValueTask DisposeSessionAsync()
    {
        if (_session is null)
        {
            return;
        }

        var session = _session;
        _session = null;
        await session.DisposeAsync().ConfigureAwait(false);
    }

    private void NotifyStateChanged() => StateChanged?.Invoke(this, EventArgs.Empty);
}
