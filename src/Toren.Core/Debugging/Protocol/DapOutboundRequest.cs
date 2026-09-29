namespace Toren.Core.Debugging.Protocol;

public sealed record DapOutboundRequest(
    int Sequence,
    string Command,
    byte[] Frame);
