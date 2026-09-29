namespace Toren.Git.Models;

public sealed record GitBranchInfo(
    string Name,
    bool IsCurrent,
    string? UpstreamName);
