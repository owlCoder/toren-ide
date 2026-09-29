using CommunityToolkit.Mvvm.ComponentModel;
using Toren.Git.Models;

namespace Toren.App.SourceControl.ViewModels;

public sealed partial class SourceControlChangeViewModel(GitChange change) : ObservableObject
{
    public GitChange Change { get; } = change ?? throw new ArgumentNullException(nameof(change));

    public string Path => Change.Path;

    public string StatusText => Change.IsConflicted
        ? "C"
        : Change.IsUntracked
            ? "U"
            : string.Concat(Change.IndexStatus, Change.WorkTreeStatus).Replace(".", string.Empty, StringComparison.Ordinal);

    public string DisplayPath => Change.OriginalPath is { Length: > 0 } originalPath
        ? $"{originalPath} → {Change.Path}"
        : Change.Path;

    public bool IsStaged => Change.IsStaged;

    public bool HasWorkingTreeChange => Change.HasWorkingTreeChange;

    public bool IsConflicted => Change.IsConflicted;
}
