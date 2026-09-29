using System.Text.Json;
using Toren.Core.Results;

namespace Toren.Core.Debugging.Protocol;

public static class DapProtocolMessageParser
{
    private const string InvalidJsonErrorCode = "debug.dap.message.json-invalid";
    private const string InvalidEnvelopeErrorCode = "debug.dap.message.envelope-invalid";

    public static Result<DapProtocolMessage> Parse(string payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        try
        {
            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !TryGetInt32(root, "seq", out var sequence)
                || !TryGetString(root, "type", out var type))
            {
                return InvalidEnvelope("A DAP message must contain integer 'seq' and string 'type' properties.");
            }

            return type switch
            {
                "request" => ParseRequest(root, payload, sequence),
                "response" => ParseResponse(root, payload, sequence),
                "event" => ParseEvent(root, payload, sequence),
                _ => InvalidEnvelope($"Unsupported DAP message type '{type}'."),
            };
        }
        catch (JsonException exception)
        {
            return Result.Failure<DapProtocolMessage>(
                OperationError.Create(
                    InvalidJsonErrorCode,
                    $"The DAP message payload is not valid JSON: {exception.Message}"));
        }
    }

    private static Result<DapProtocolMessage> ParseRequest(
        JsonElement root,
        string payload,
        int sequence)
    {
        if (!TryGetString(root, "command", out var command))
        {
            return InvalidEnvelope("A DAP request must contain a string 'command' property.");
        }

        return Result.Success(
            new DapProtocolMessage(
                sequence,
                DapProtocolMessageKind.Request,
                command,
                null,
                null,
                null,
                payload));
    }

    private static Result<DapProtocolMessage> ParseResponse(
        JsonElement root,
        string payload,
        int sequence)
    {
        if (!TryGetInt32(root, "request_seq", out var requestSequence)
            || !TryGetString(root, "command", out var command)
            || !TryGetBoolean(root, "success", out var success))
        {
            return InvalidEnvelope(
                "A DAP response must contain integer 'request_seq', string 'command', and boolean 'success' properties.");
        }

        return Result.Success(
            new DapProtocolMessage(
                sequence,
                DapProtocolMessageKind.Response,
                command,
                null,
                requestSequence,
                success,
                payload));
    }

    private static Result<DapProtocolMessage> ParseEvent(
        JsonElement root,
        string payload,
        int sequence)
    {
        if (!TryGetString(root, "event", out var eventName))
        {
            return InvalidEnvelope("A DAP event must contain a string 'event' property.");
        }

        return Result.Success(
            new DapProtocolMessage(
                sequence,
                DapProtocolMessageKind.Event,
                null,
                eventName,
                null,
                null,
                payload));
    }

    private static Result<DapProtocolMessage> InvalidEnvelope(string message)
    {
        return Result.Failure<DapProtocolMessage>(
            OperationError.Create(InvalidEnvelopeErrorCode, message));
    }

    private static bool TryGetInt32(JsonElement root, string propertyName, out int value)
    {
        value = default;
        return root.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.Number
            && property.TryGetInt32(out value);
    }

    private static bool TryGetString(JsonElement root, string propertyName, out string value)
    {
        value = string.Empty;
        if (!root.TryGetProperty(propertyName, out var property)
            || property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = property.GetString() ?? string.Empty;
        return value.Length > 0;
    }

    private static bool TryGetBoolean(JsonElement root, string propertyName, out bool value)
    {
        value = default;
        if (!root.TryGetProperty(propertyName, out var property))
        {
            return false;
        }

        if (property.ValueKind == JsonValueKind.True)
        {
            value = true;
            return true;
        }

        if (property.ValueKind == JsonValueKind.False)
        {
            value = false;
            return true;
        }

        return false;
    }
}
