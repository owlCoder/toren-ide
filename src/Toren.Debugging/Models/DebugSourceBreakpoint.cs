namespace Toren.Debugging.Models;

public sealed record DebugSourceBreakpoint(
    int Line,
    string? Condition = null);
