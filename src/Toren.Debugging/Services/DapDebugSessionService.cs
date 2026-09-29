using System.Text.Json;
using Toren.Core.Results;
using Toren.Debugging.Contracts;
using Toren.Debugging.Models;
using Toren.Debugging.Protocol;

namespace Toren.Debugging.Services;

public sealed class DapDebugSessionService(
    IDebugAdapterLocator adapterLocator,
    IDebugAdapterTransportFactory transportFactory,
    IDebugAdapterClientFactory clientFactory) : IDebugSessionService
{
    private readonly IDebugAdapterLocator _adapterLocator = adapterLocator
        ?? throw new ArgumentNullException(nameof(adapterLocator));
    private readonly IDebugAdapterTransportFactory _transportFactory = transportFactory
        ?? throw new ArgumentNullException(nameof(transportFactory));
    private readonly IDebugAdapterClientFactory _clientFactory = clientFactory
        ?? throw new ArgumentNullException(nameof(clientFactory));

    public async Task<Result<IDebugSession>> AttachAsync(
        int processId,
        string? workingDirectory = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(processId);

        var adapterResult = _adapterLocator.Locate();
        if (adapterResult.IsFailure)
        {
            return Result.Failure<IDebugSession>(adapterResult.Error);
        }

        var transportResult = _transportFactory.Start(adapterResult.Value!, workingDirectory);
        if (transportResult.IsFailure)
        {
            return Result.Failure<IDebugSession>(transportResult.Error);
        }

        var client = _clientFactory.Create(transportResult.Value!);
        try
        {
            var initialize = await client.SendRequestAsync(
                "initialize",
                new
                {
                    clientID = "toren",
                    clientName = "Toren IDE",
                    adapterID = "coreclr",
                    pathFormat = "path",
                    linesStartAt1 = true,
                    columnsStartAt1 = true,
                    supportsVariableType = true,
                    supportsVariablePaging = true,
                },
                cancellationToken).ConfigureAwait(false);
            if (initialize.IsFailure)
            {
                return await DisposeWithFailureAsync(client, initialize.Error).ConfigureAwait(false);
            }

            var attach = await client.SendRequestAsync(
                "attach",
                new
                {
                    name = "Attach .NET process",
                    type = "coreclr",
                    request = "attach",
                    processId,
                },
                cancellationToken).ConfigureAwait(false);
            if (attach.IsFailure)
            {
                return await DisposeWithFailureAsync(client, attach.Error).ConfigureAwait(false);
            }

            var configurationDone = await client.SendRequestAsync(
                "configurationDone",
                cancellationToken: cancellationToken).ConfigureAwait(false);
            if (configurationDone.IsFailure)
            {
                return await DisposeWithFailureAsync(client, configurationDone.Error).ConfigureAwait(false);
            }

            return Result.Success<IDebugSession>(new DapDebugSession(processId, client));
        }
        catch (OperationCanceledException)
        {
            await client.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private static async Task<Result<IDebugSession>> DisposeWithFailureAsync(
        IDebugAdapterClient client,
        OperationError error)
    {
        await client.DisposeAsync().ConfigureAwait(false);
        return Result.Failure<IDebugSession>(error);
    }

    private sealed class DapDebugSession(int processId, IDebugAdapterClient client) : IDebugSession
    {
        private const string EndedBeforeStopErrorCode = "debug.session.ended-before-stop";
        private const string InvalidStopErrorCode = "debug.session.invalid-stop";

        private readonly IDebugAdapterClient _client = client;
        private int _disconnected;
        private int _disposeState;

        public int ProcessId { get; } = processId;

        public async Task<Result<DebugStopInfo>> WaitForStopAsync(
            CancellationToken cancellationToken = default)
        {
            await foreach (var notification in _client
                .ReadNotificationsAsync(cancellationToken)
                .ConfigureAwait(false))
            {
                if (notification.Kind != DapProtocolMessageKind.Event)
                {
                    continue;
                }

                if (string.Equals(notification.EventName, "stopped", StringComparison.Ordinal))
                {
                    return ParseStop(notification.Payload);
                }

                if (string.Equals(notification.EventName, "exited", StringComparison.Ordinal)
                    || string.Equals(notification.EventName, "terminated", StringComparison.Ordinal))
                {
                    return Result.Failure<DebugStopInfo>(
                        OperationError.Create(
                            EndedBeforeStopErrorCode,
                            "The debuggee ended before the debugger reported a stopped thread."));
                }
            }

            return Result.Failure<DebugStopInfo>(
                OperationError.Create(
                    EndedBeforeStopErrorCode,
                    "The debug adapter ended before reporting a stopped thread."));
        }

        public async Task<Result<bool>> ContinueAsync(
            int threadId,
            CancellationToken cancellationToken = default)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(threadId);
            var response = await _client.SendRequestAsync(
                "continue",
                new { threadId },
                cancellationToken).ConfigureAwait(false);
            return response.IsFailure
                ? Result.Failure<bool>(response.Error)
                : Result.Success(true);
        }

        public async Task<Result<bool>> DisconnectAsync(
            CancellationToken cancellationToken = default)
        {
            var response = await _client.SendRequestAsync(
                "disconnect",
                new
                {
                    restart = false,
                    terminateDebuggee = false,
                },
                cancellationToken).ConfigureAwait(false);
            if (response.IsFailure)
            {
                return Result.Failure<bool>(response.Error);
            }

            Interlocked.Exchange(ref _disconnected, 1);
            return Result.Success(true);
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposeState, 1) != 0)
            {
                return;
            }

            if (Volatile.Read(ref _disconnected) == 0)
            {
                _client.Terminate();
            }

            await _client.DisposeAsync().ConfigureAwait(false);
        }

        private static Result<DebugStopInfo> ParseStop(string payload)
        {
            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;
            if (!root.TryGetProperty("body", out var body)
                || body.ValueKind != JsonValueKind.Object
                || !body.TryGetProperty("threadId", out var threadIdProperty)
                || !threadIdProperty.TryGetInt32(out var threadId)
                || threadId <= 0)
            {
                return Result.Failure<DebugStopInfo>(
                    OperationError.Create(
                        InvalidStopErrorCode,
                        "The debug adapter reported a stopped event without a valid thread id."));
            }

            var reason = body.TryGetProperty("reason", out var reasonProperty)
                && reasonProperty.ValueKind == JsonValueKind.String
                ? reasonProperty.GetString()
                : null;
            return Result.Success(new DebugStopInfo(threadId, reason));
        }
    }
}
