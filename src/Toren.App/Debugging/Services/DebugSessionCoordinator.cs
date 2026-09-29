using Toren.Core.Results;
using Toren.Debugging.Contracts;

namespace Toren.App.Debugging.Services;

public sealed class DebugSessionCoordinator(IDebugSessionService sessionService) : IAsyncDisposable
{
    private readonly IDebugSessionService _sessionService = sessionService
        ?? throw new ArgumentNullException(nameof(sessionService));
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IDebugSession? _session;
    private bool _disposed;

    public bool IsAttached => _session is not null;

    public int? ProcessId => _session?.ProcessId;

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
            return Result.Success(true);
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
            await DisposeSessionAsync().ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
            _gate.Dispose();
        }
    }

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
}
