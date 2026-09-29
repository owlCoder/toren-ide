namespace Toren.App.Debugging.ViewModels;

public sealed record DebugLocalItemViewModel(
    string ScopeName,
    string Name,
    string Value,
    string? Type,
    int VariablesReference);
