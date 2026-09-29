namespace Toren.Debugging.Protocol;

public sealed record DapPendingRequest(
    int Sequence,
    string Command);
