namespace Toren.Debugging.Models;

public sealed record DebugStopInfo(
    int ThreadId,
    string? Reason);
