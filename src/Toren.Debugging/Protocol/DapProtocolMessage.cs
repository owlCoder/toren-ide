namespace Toren.Debugging.Protocol;

public enum DapProtocolMessageKind
{
    Request,
    Response,
    Event,
}

public sealed record DapProtocolMessage(
    int Sequence,
    DapProtocolMessageKind Kind,
    string? Command,
    string? EventName,
    int? RequestSequence,
    bool? Success,
    string Payload);
