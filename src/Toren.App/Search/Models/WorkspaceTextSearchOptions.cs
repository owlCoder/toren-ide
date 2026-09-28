namespace Toren.App.Search.Models;

public sealed record WorkspaceTextSearchOptions(
    bool MatchCase = false,
    bool MatchWholeWord = false,
    bool UseRegularExpression = false,
    string IncludePatterns = "",
    string ExcludePatterns = "");
