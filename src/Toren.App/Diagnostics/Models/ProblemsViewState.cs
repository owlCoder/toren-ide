namespace Toren.App.Diagnostics.Models;

public sealed record ProblemsViewState(
    bool ShowErrors,
    bool ShowWarnings,
    bool ShowInfo)
{
    public static ProblemsViewState Default { get; } = new(
        ShowErrors: true,
        ShowWarnings: true,
        ShowInfo: true);
}
