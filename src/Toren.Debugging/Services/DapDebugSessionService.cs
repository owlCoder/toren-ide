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
        private const string InvalidBreakpointResponseErrorCode = "debug.session.invalid-breakpoints";
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

        public async Task<Result<IReadOnlyList<DebugBreakpoint>>> SetBreakpointsAsync(
            string sourcePath,
            IReadOnlyList<DebugSourceBreakpoint> breakpoints,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
            ArgumentNullException.ThrowIfNull(breakpoints);
            foreach (var breakpoint in breakpoints)
            {
                ArgumentOutOfRangeException.ThrowIfNegativeOrZero(breakpoint.Line);
            }

            var response = await _client.SendRequestAsync(
                "setBreakpoints",
                new
                {
                    source = new { path = sourcePath },
                    breakpoints = breakpoints
                        .Select(static breakpoint => new
                        {
                            line = breakpoint.Line,
                            condition = breakpoint.Condition,
                        })
                        .ToArray(),
                    sourceModified = false,
                },
                cancellationToken).ConfigureAwait(false);
            return response.IsFailure
                ? Result.Failure<IReadOnlyList<DebugBreakpoint>>(response.Error)
                : ParseBreakpoints(response.Value!.Payload, breakpoints);
        }

        public async Task<Result<IReadOnlyList<DebugStackFrame>>> GetStackTraceAsync(
            int threadId,
            CancellationToken cancellationToken = default)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(threadId);
            var response = await _client.SendRequestAsync(
                "stackTrace",
                new
                {
                    threadId,
                    startFrame = 0,
                    levels = 0,
                },
                cancellationToken).ConfigureAwait(false);
            return response.IsFailure
                ? Result.Failure<IReadOnlyList<DebugStackFrame>>(response.Error)
                : DapDebugInspectionParser.ParseStackTrace(response.Value!.Payload);
        }

        public async Task<Result<IReadOnlyList<DebugScope>>> GetScopesAsync(
            int frameId,
            CancellationToken cancellationToken = default)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(frameId);
            var response = await _client.SendRequestAsync(
                "scopes",
                new { frameId },
                cancellationToken).ConfigureAwait(false);
            return response.IsFailure
                ? Result.Failure<IReadOnlyList<DebugScope>>(response.Error)
                : DapDebugInspectionParser.ParseScopes(response.Value!.Payload);
        }

        public async Task<Result<IReadOnlyList<DebugVariable>>> GetVariablesAsync(
            int variablesReference,
            CancellationToken cancellationToken = default)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(variablesReference);
            var response = await _client.SendRequestAsync(
                "variables",
                new { variablesReference },
                cancellationToken).ConfigureAwait(false);
            return response.IsFailure
                ? Result.Failure<IReadOnlyList<DebugVariable>>(response.Error)
                : DapDebugInspectionParser.ParseVariables(response.Value!.Payload);
        }

        public async Task<Result<DebugEvaluationResult>> EvaluateAsync(
            string expression,
            int? frameId = null,
            DebugEvaluationContext context = DebugEvaluationContext.Watch,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(expression);
            if (frameId.HasValue)
            {
                ArgumentOutOfRangeException.ThrowIfNegativeOrZero(frameId.Value);
            }

            var response = await _client.SendRequestAsync(
                "evaluate",
                new
                {
                    expression,
                    frameId,
                    context = GetEvaluationContext(context),
                },
                cancellationToken).ConfigureAwait(false);
            return response.IsFailure
                ? Result.Failure<DebugEvaluationResult>(response.Error)
                : DapDebugInspectionParser.ParseEvaluation(response.Value!.Payload);
        }

        public Task<Result<bool>> ContinueAsync(
            int threadId,
            CancellationToken cancellationToken = default) =>
            SendThreadCommandAsync("continue", threadId, cancellationToken);

        public Task<Result<bool>> PauseAsync(
            int threadId,
            CancellationToken cancellationToken = default) =>
            SendThreadCommandAsync("pause", threadId, cancellationToken);

        public Task<Result<bool>> StepOverAsync(
            int threadId,
            CancellationToken cancellationToken = default) =>
            SendThreadCommandAsync("next", threadId, cancellationToken);

        public Task<Result<bool>> StepIntoAsync(
            int threadId,
            CancellationToken cancellationToken = default) =>
            SendThreadCommandAsync("stepIn", threadId, cancellationToken);

        public Task<Result<bool>> StepOutAsync(
            int threadId,
            CancellationToken cancellationToken = default) =>
            SendThreadCommandAsync("stepOut", threadId, cancellationToken);

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

        private async Task<Result<bool>> SendThreadCommandAsync(
            string command,
            int threadId,
            CancellationToken cancellationToken)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(threadId);
            var response = await _client.SendRequestAsync(
                command,
                new { threadId },
                cancellationToken).ConfigureAwait(false);
            return response.IsFailure
                ? Result.Failure<bool>(response.Error)
                : Result.Success(true);
        }

        private static string GetEvaluationContext(DebugEvaluationContext context) => context switch
        {
            DebugEvaluationContext.Watch => "watch",
            DebugEvaluationContext.Repl => "repl",
            DebugEvaluationContext.Hover => "hover",
            _ => throw new ArgumentOutOfRangeException(nameof(context)),
        };

        private static Result<IReadOnlyList<DebugBreakpoint>> ParseBreakpoints(
            string payload,
            IReadOnlyList<DebugSourceBreakpoint> requestedBreakpoints)
        {
            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;
            if (!root.TryGetProperty("body", out var body)
                || body.ValueKind != JsonValueKind.Object
                || !body.TryGetProperty("breakpoints", out var breakpointsElement)
                || breakpointsElement.ValueKind != JsonValueKind.Array)
            {
                return Result.Failure<IReadOnlyList<DebugBreakpoint>>(
                    OperationError.Create(
                        InvalidBreakpointResponseErrorCode,
                        "The debug adapter returned an invalid setBreakpoints response."));
            }

            var parsed = new List<DebugBreakpoint>();
            var index = 0;
            foreach (var element in breakpointsElement.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.Object
                    || !element.TryGetProperty("verified", out var verifiedProperty)
                    || verifiedProperty.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                {
                    return Result.Failure<IReadOnlyList<DebugBreakpoint>>(
                        OperationError.Create(
                            InvalidBreakpointResponseErrorCode,
                            "The debug adapter returned a breakpoint without a verification state."));
                }

                int? id = element.TryGetProperty("id", out var idProperty)
                    && idProperty.TryGetInt32(out var parsedId)
                        ? parsedId
                        : null;
                var line = element.TryGetProperty("line", out var lineProperty)
                    && lineProperty.TryGetInt32(out var parsedLine)
                    && parsedLine > 0
                        ? parsedLine
                        : index < requestedBreakpoints.Count
                            ? requestedBreakpoints[index].Line
                            : 0;
                var message = element.TryGetProperty("message", out var messageProperty)
                    && messageProperty.ValueKind == JsonValueKind.String
                        ? messageProperty.GetString()
                        : null;
                parsed.Add(new DebugBreakpoint(id, verifiedProperty.GetBoolean(), line, message));
                index++;
            }

            return Result.Success<IReadOnlyList<DebugBreakpoint>>(parsed);
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
