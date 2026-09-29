namespace Toren.Core.Debugging.Protocol;

public sealed record DapProtocolFrame(
    bool IsComplete,
    string? Payload,
    int BytesConsumed)
{
    public static DapProtocolFrame Incomplete { get; } = new(false, null, 0);

    public static DapProtocolFrame Complete(string payload, int bytesConsumed)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bytesConsumed);
        return new DapProtocolFrame(true, payload, bytesConsumed);
    }
}
