using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Toren.Git.Contracts;

namespace Toren.App.SourceControl.ViewModels;

public sealed partial class SourceControlViewModel(IGitRepositoryService repositoryService) : ObservableObject
{
    private readonly IGitRepositoryService _repositoryService = repositoryService
        ?? throw new ArgumentNullException(nameof(repositoryService));
    private string? _workingDirectory;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedChange))]
    private int _selectedChangeIndex = -1;

    [ObservableProperty]
    private string _branchText = "No repository";

    [ObservableProperty]
    private string _statusText = "Open a Git workspace to inspect source control.";

    [ObservableProperty]
    private string _diffText = string.Empty;

    [ObservableProperty]
    private bool _isRefreshing;

    public ObservableCollection<SourceControlChangeViewModel> Changes { get; } = new();

    public SourceControlChangeViewModel? SelectedChange =>
        SelectedChangeIndex >= 0 && SelectedChangeIndex < Changes.Count
            ? Changes[SelectedChangeIndex]
            : null;

    public bool HasChanges => Changes.Count > 0;

    public void SetWorkingDirectory(string? workingDirectory)
    {
        _workingDirectory = string.IsNullOrWhiteSpace(workingDirectory)
            ? null
            : Path.GetFullPath(workingDirectory);
        Changes.Clear();
        SelectedChangeIndex = -1;
        DiffText = string.Empty;
        BranchText = "No repository";
        StatusText = _workingDirectory is null
            ? "Open a Git workspace to inspect source control."
            : "Refresh source control status.";
        OnPropertyChanged(nameof(HasChanges));
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (_workingDirectory is null || IsRefreshing)
        {
            return;
        }

        IsRefreshing = true;
        try
        {
            var statusResult = await _repositoryService
                .GetStatusAsync(_workingDirectory, cancellationToken)
                .ConfigureAwait(true);
            if (statusResult.IsFailure)
            {
                Changes.Clear();
                SelectedChangeIndex = -1;
                DiffText = string.Empty;
                BranchText = "No repository";
                StatusText = statusResult.Error.Message;
                OnPropertyChanged(nameof(HasChanges));
                return;
            }

            var status = statusResult.Value!;
            BranchText = CreateBranchText(status.BranchName, status.UpstreamName, status.AheadCount, status.BehindCount);
            Changes.Clear();
            foreach (var change in status.Changes)
            {
                Changes.Add(new SourceControlChangeViewModel(change));
            }

            OnPropertyChanged(nameof(HasChanges));
            StatusText = status.IsClean
                ? "Working tree is clean."
                : $"{status.Changes.Count} change{(status.Changes.Count == 1 ? string.Empty : "s")}.";
            SelectedChangeIndex = Changes.Count > 0 ? 0 : -1;
            await LoadSelectedDiffAsync(cancellationToken).ConfigureAwait(true);
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    public async Task LoadSelectedDiffAsync(CancellationToken cancellationToken = default)
    {
        if (_workingDirectory is null || SelectedChange is not { } selected)
        {
            DiffText = string.Empty;
            return;
        }

        if (selected.Change.IsUntracked)
        {
            DiffText = "Untracked file — stage it to view a Git diff.";
            return;
        }

        var staged = selected.IsStaged && !selected.HasWorkingTreeChange;
        var diffResult = await _repositoryService
            .GetDiffAsync(_workingDirectory, selected.Path, staged, cancellationToken)
            .ConfigureAwait(true);
        DiffText = diffResult.IsSuccess
            ? diffResult.Value ?? string.Empty
            : diffResult.Error.Message;
    }

    private static string CreateBranchText(
        string? branchName,
        string? upstreamName,
        int aheadCount,
        int behindCount)
    {
        var branch = branchName ?? "Detached HEAD";
        if (string.IsNullOrWhiteSpace(upstreamName))
        {
            return branch;
        }

        var divergence = aheadCount == 0 && behindCount == 0
            ? string.Empty
            : $" · ↑{aheadCount} ↓{behindCount}";
        return $"{branch} → {upstreamName}{divergence}";
    }
}
