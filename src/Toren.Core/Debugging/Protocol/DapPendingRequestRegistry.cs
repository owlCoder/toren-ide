using System.Collections.Concurrent;
using Toren.Core.Results;

namespace Toren.Core.Debugging.Protocol;

public sealed class DapPendingRequestRegistry
{
    private const string InvalidResponseErrorCode = "debug.dap.response.invalid";
    private const string UnknownResponseErrorCode = "debug.dap.response.unknown-request";
    private const string CommandMismatchErrorCode = "debug.dap.response.command-mismatch";
    private readonly ConcurrentDictionary<int, DapPendingRequest> _pending = new();

    public int Count => _pending.Count;

    public void Register(DapOutboundRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_pending.TryAdd(
                request.Sequence,
                new DapPendingRequest(request.Sequence, request.Command)))
        {
            throw new InvalidOperationException(
                $"DAP request sequence {request.Sequence} is already pending.");
        }
    }

    public Result<DapPendingRequest> Complete(DapProtocolMessage response)
    {
        ArgumentNullException.ThrowIfNull(response);

        if (response.Kind != DapProtocolMessageKind.Response
            || response.RequestSequence is not { } requestSequence
            || string.IsNullOrWhiteSpace(response.Command))
        {
            return Result.Failure<DapPendingRequest>(
                OperationError.Create(
                    InvalidResponseErrorCode,
                    "Only DAP response messages with request correlation can complete a pending request."));
        }

        if (!_pending.TryGetValue(requestSequence, out var pending))
        {
            return Result.Failure<DapPendingRequest>(
                OperationError.Create(
                    UnknownResponseErrorCode,
                    $"No pending DAP request exists for response request_seq {requestSequence}."));
        }

        if (!string.Equals(pending.Command, response.Command, StringComparison.Ordinal))
        {
            return Result.Failure<DapPendingRequest>(
                OperationError.Create(
                    CommandMismatchErrorCode,
                    $"DAP response command '{response.Command}' does not match pending command '{pending.Command}'."));
        }

        if (!_pending.TryRemove(requestSequence, out var completed))
        {
            return Result.Failure<DapPendingRequest>(
                OperationError.Create(
                    UnknownResponseErrorCode,
                    $"DAP request {requestSequence} completed concurrently."));
        }

        return Result.Success(completed);
    }

    public void Clear()
    {
        _pending.Clear();
    }
}
