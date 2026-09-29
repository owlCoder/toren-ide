using System.Text.Json;
using System.Text.Json.Serialization;

namespace Toren.Core.Debugging.Protocol;

public sealed class DapRequestEncoder(DapSequenceGenerator sequenceGenerator)
{
    private readonly DapSequenceGenerator _sequenceGenerator = sequenceGenerator
        ?? throw new ArgumentNullException(nameof(sequenceGenerator));

    public DapOutboundRequest Encode(string command, object? arguments = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command);

        var sequence = _sequenceGenerator.Next();
        var payload = JsonSerializer.Serialize(
            new RequestEnvelope(sequence, "request", command, arguments));
        return new DapOutboundRequest(
            sequence,
            command,
            DapMessageFraming.Encode(payload));
    }

    private sealed record RequestEnvelope(
        [property: JsonPropertyName("seq")] int Sequence,
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("command")] string Command,
        [property: JsonPropertyName("arguments"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] object? Arguments);
}
