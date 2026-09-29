namespace Toren.Debugging.Models;

public sealed record DebugEvaluationResult(
    string Value,
    string? Type,
    int VariablesReference);
