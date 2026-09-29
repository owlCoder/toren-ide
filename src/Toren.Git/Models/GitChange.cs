namespace Toren.Git.Models;

public sealed record GitChange(
    string Path,
    string? OriginalPath,
    char IndexStatus,
    char WorkTreeStatus,
    bool IsUntracked = false,
    bool IsConflicted = false)
{
    public bool IsStaged => !IsUntracked && !IsConflicted && IndexStatus != '.';

    public bool HasWorkingTreeChange => IsUntracked || IsConflicted || WorkTreeStatus != '.';
}
