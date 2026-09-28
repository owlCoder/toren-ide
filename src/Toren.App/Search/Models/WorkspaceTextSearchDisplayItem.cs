namespace Toren.App.Search.Models;

public sealed record WorkspaceTextSearchDisplayItem(
    bool IsHeader,
    string RelativePath,
    int MatchCount,
    WorkspaceTextSearchResult? Result)
{
    public bool IsResult => Result is not null;

    public int Line => Result?.Line ?? 0;

    public int Column => Result?.Column ?? 0;

    public string Preview => Result?.Preview ?? string.Empty;

    public string MatchCountText => $"{MatchCount} match{(MatchCount == 1 ? string.Empty : "es")}";
}
