namespace Toren.App.Debugging.ViewModels;

public sealed record DebugBreakpointItemViewModel(
    string SourcePath,
    int Line,
    string? Condition,
    bool IsVerified,
    string? Message)
{
    public string FileName => Path.GetFileName(SourcePath);
}
