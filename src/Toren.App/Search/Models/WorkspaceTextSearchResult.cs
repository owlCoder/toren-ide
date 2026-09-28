namespace Toren.App.Search.Models;

public sealed record WorkspaceTextSearchResult(
    string FilePath,
    string RelativePath,
    int Line,
    int Column,
    string Preview);
