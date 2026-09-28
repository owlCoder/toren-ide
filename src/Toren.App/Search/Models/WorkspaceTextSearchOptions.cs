namespace Toren.App.Search.Models;

public sealed record WorkspaceTextSearchOptions(
    bool MatchCase = false,
    string IncludePatterns = "",
    string ExcludePatterns = "");
