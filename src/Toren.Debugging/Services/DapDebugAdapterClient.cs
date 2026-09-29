using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Channels;
using Toren.Core.Results;
using Toren.Debugging.Contracts;
using Toren.Debugging.Protocol;

namespace Toren.Debugging.Services;

public sealed class DapDebugAdapterClient : IDebugAdapterClient
{
    private const string ConnectionClosedErrorCode = "debug.dap.connection.closed";
    private const string ReadFailedErrorCode = "debug.dap.read-failed";
    private const string ResponseFailedErrorCode = "debug.dap.response.failed";
    private const string WriteFailedErrorCode = "debug.dap.write-failed";
    private const string DisposedErrorCode = "debug.dap.client.disposed";

    private readonly IDebugAdapterTransport _transport;
    private readonly DapRequestEncoder _requestEncoder = new(new DapSequenceGenerator());
    private readonly DapPendingRequestRegistry _pendingRequests = new();
    private readonly ConcurrentDictionary<int, TaskCompletionSource<Result<DapProtocolMessage>>> _responses = new();
    private readonly Channel<DapProtocolMessage> _notifications = Channel.CreateUnbounded<DapProtocolMessage>(
        new UnboundedChannelOptions
        {
            AllowSynchronousContinuations = false,
            SingleReader = false,
            SingleWriter = true,
        });
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly object _readLoopGate = new();
    private Task? _readLoop;
    private int _disposeState;

    public DapDebugAdapterClient(IDebugAdapterTransport transport)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
    }

    public async Task<Result<DapProtocolMessage>> SendRequestAsync(
        string command,
        object? arguments = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        ArgumentException.ThrowIfNullOrWhiteSpace(command);

        var request = _requestEncoder.Encode(command, arguments);
        var completion = new TaskCompletionSource<Result<DapProtocolMessage>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingRequests.Register(request);
        if (!_responses.TryAdd(request.Sequence, completion))
        {
            throw new InvalidOperationException($"DAP request sequence {request.Sequence} is already awaiting a response.");
        }

        EnsureReadLoopStarted();
        try
        {
            await _writeLock.WaitAsync(_lifetimeCancellation.Token).ConfigureAwait(false);
            try
            {
                await _transport.WriteStream
                    .WriteAsync(request.Frame.AsMemory(), _lifetimeCancellation.Token)
                    .ConfigureAwait(false);
                await _transport.WriteStream.FlushAsync(_lifetimeCancellation.Token).ConfigureAwait(false);
            }
            finally
            {
                _writeLock.Release();
            }
        }
        catch (IOException exception)
        {
            var error = OperationError.Create(
                WriteFailedErrorCode,
                $"Failed to write DAP request '{command}': {exception.Message}");
            FailAllPending(error);
            return Result.Failure<DapProtocolMessage>(error);
        }
        catch (ObjectDisposedException exception)
        {
            var error = OperationError.Create(
                WriteFailedErrorCode,
                $"Failed to write DAP request '{command}': {exception.Message}");
            FailAllPending(error);
            return Result.Failure<DapProtocolMessage>(error);
        }

        return await completion.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    public IAsyncEnumerable<DapProtocolMessage> ReadNotificationsAsync(
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        EnsureReadLoopStarted();
        return _notifications.Reader.ReadAllAsync(cancellationToken);
    }

    public Task<int> WaitForExitAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        return _transport.WaitForExitAsync(cancellationToken);
    }

    public void Terminate()
    {
        if (Volatile.Read(ref _disposeState) == 0)
        {
            _transport.Terminate();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposeState, 1) != 0)
        {
            return;
        }

        var disposedError = OperationError.Create(
            DisposedErrorCode,
            "The DAP client was disposed before the pending request completed.");
        FailAllPending(disposedError);
        _lifetimeCancellation.Cancel();
        _transport.Terminate();

        Task? readLoop;
        lock (_readLoopGate)
        {
            readLoop = _readLoop;
        }

        if (readLoop is not null)
        {
            try
            {
                await readLoop.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
            {
                // Normal shutdown.
            }
        }

        _notifications.Writer.TryComplete();
        await _transport.DisposeAsync().ConfigureAwait(false);
        _writeLock.Dispose();
        _lifetimeCancellation.Dispose();
    }

    private void EnsureReadLoopStarted()
    {
        lock (_readLoopGate)
        {
            _readLoop ??= ReadLoopAsync();
        }
    }

    private async Task ReadLoopAsync()
    {
        try
        {
            while (!_lifetimeCancellation.IsCancellationRequested)
            {
                var payload = await DapMessageFramingService
                    .ReadPayloadAsync(_transport.ReadStream, _lifetimeCancellation.Token)
                    .ConfigureAwait(false);
                if (payload is null)
                {
                    FailAllPending(OperationError.Create(
                        ConnectionClosedErrorCode,
                        "The debug adapter closed the DAP stream."));
                    break;
                }

                var parsed = DapProtocolMessageParser.Parse(payload);
                if (parsed.IsFailure)
                {
                    FailAllPending(parsed.Error);
                    break;
                }

                var message = parsed.Value;
                if (message.Kind == DapProtocolMessageKind.Response)
                {
                    HandleResponse(message);
                    continue;
                }

                await _notifications.Writer
                    .WriteAsync(message, _lifetimeCancellation.Token)
                    .ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
        {
            // Normal shutdown.
        }
        catch (IOException exception)
        {
            FailAllPending(OperationError.Create(
                ReadFailedErrorCode,
                $"Failed to read from the debug adapter: {exception.Message}"));
        }
        catch (InvalidDataException exception)
        {
            FailAllPending(OperationError.Create(
                ReadFailedErrorCode,
                $"Failed to read from the debug adapter: {exception.Message}"));
        }
        finally
        {
            _notifications.Writer.TryComplete();
        }
    }

    private void HandleResponse(DapProtocolMessage response)
    {
        var completed = _pendingRequests.Complete(response);
        if (completed.IsFailure)
        {
            FailAllPending(completed.Error);
            return;
        }

        if (!_responses.TryRemove(completed.Value.Sequence, out var completion))
        {
            return;
        }

        completion.TrySetResult(response.Success == true
            ? Result.Success(response)
            : Result.Failure<DapProtocolMessage>(CreateResponseError(response)));
    }

    private static OperationError CreateResponseError(DapProtocolMessage response)
    {
        using var document = JsonDocument.Parse(response.Payload);
        var root = document.RootElement;
        var message = root.TryGetProperty("message", out var messageProperty)
            && messageProperty.ValueKind == JsonValueKind.String
            ? messageProperty.GetString()
            : null;
        var command = response.Command ?? "request";
        return OperationError.Create(
            ResponseFailedErrorCode,
            string.IsNullOrWhiteSpace(message)
                ? $"The debug adapter rejected DAP request '{command}'."
                : $"The debug adapter rejected DAP request '{command}': {message}");
    }

    private void FailAllPending(OperationError error)
    {
        foreach (var entry in _responses.ToArray())
        {
            if (_responses.TryRemove(entry.Key, out var completion))
            {
                completion.TrySetResult(Result.Failure<DapProtocolMessage>(error));
            }
        }

        _pendingRequests.Clear();
    }

    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _disposeState) != 0)
        {
            throw new ObjectDisposedException(nameof(DapDebugAdapterClient));
        }
    }
}
