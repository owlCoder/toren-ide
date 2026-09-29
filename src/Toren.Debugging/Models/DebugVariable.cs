namespace Toren.Debugging.Models;

public sealed record DebugVariable(
    string Name,
    string Value,
    string? Type,
    int VariablesReference);
