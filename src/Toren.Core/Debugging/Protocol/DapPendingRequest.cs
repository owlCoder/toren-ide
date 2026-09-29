namespace Toren.Core.Debugging.Protocol;

public sealed record DapPendingRequest(
    int Sequence,
    string Command);
