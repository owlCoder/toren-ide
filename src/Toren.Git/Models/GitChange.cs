namespace Toren.Git.Models;

public sealed record GitChange(
    string Path,
    string? OriginalPath,
    char IndexStatus,
    char WorkTreeStatus,
    bool IsUntracked = false)
{
    public bool IsStaged => !IsUntracked && IndexStatus != '.';

    public bool HasWorkingTreeChange => IsUntracked || WorkTreeStatus != '.';
}
