namespace Toren.Git.Models;

public sealed record GitRepositoryStatus(
    string? BranchName,
    string? UpstreamName,
    int AheadCount,
    int BehindCount,
    IReadOnlyList<GitChange> Changes)
{
    public bool IsClean => Changes.Count == 0;
}
