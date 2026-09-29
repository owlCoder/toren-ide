namespace Toren.Debugging.Models;

public sealed record DebugScope(
    string Name,
    int VariablesReference,
    bool IsExpensive);
