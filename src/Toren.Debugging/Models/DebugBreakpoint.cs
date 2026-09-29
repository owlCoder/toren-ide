namespace Toren.Debugging.Models;

public sealed record DebugBreakpoint(
    int? Id,
    bool Verified,
    int Line,
    string? Message = null);
