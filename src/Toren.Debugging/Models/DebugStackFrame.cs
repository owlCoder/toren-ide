namespace Toren.Debugging.Models;

public sealed record DebugStackFrame(
    int Id,
    string Name,
    string? SourcePath,
    int? Line,
    int? Column);
