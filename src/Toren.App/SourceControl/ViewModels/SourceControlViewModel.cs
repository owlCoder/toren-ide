using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Toren.Core.Results;
using Toren.Git.Contracts;
using Toren.Git.Models;

namespace Toren.App.SourceControl.ViewModels;

public sealed partial class SourceControlViewModel(IGitRepositoryService repositoryService) : ObservableObject
{
    private readonly IGitRepositoryService _repositoryService = repositoryService
        ?? throw new ArgumentNullException(nameof(repositoryService));
    private string? _workingDirectory;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedChange))]
    [NotifyPropertyChangedFor(nameof(CanStageSelected))]
    [NotifyPropertyChangedFor(nameof(CanUnstageSelected))]
    private int _selectedChangeIndex = -1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedBranch))]
    [NotifyPropertyChangedFor(nameof(CanSwitchBranch))]
    [NotifyPropertyChangedFor(nameof(CanMerge))]
    private int _selectedBranchIndex = -1;

    [ObservableProperty]
    private string _branchText = "No repository";

    [ObservableProperty]
    private string _statusText = "Open a Git workspace to inspect source control.";

    [ObservableProperty]
    private string _diffText = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBusy))]
    private bool _isRefreshing;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBusy))]
    [NotifyPropertyChangedFor(nameof(CanStageSelected))]
    [NotifyPropertyChangedFor(nameof(CanUnstageSelected))]
    [NotifyPropertyChangedFor(nameof(CanCommit))]
    [NotifyPropertyChangedFor(nameof(CanSwitchBranch))]
    [NotifyPropertyChangedFor(nameof(CanCreateBranch))]
    [NotifyPropertyChangedFor(nameof(CanSync))]
    [NotifyPropertyChangedFor(nameof(CanMerge))]
    [NotifyPropertyChangedFor(nameof(CanAbortMerge))]
    [NotifyPropertyChangedFor(nameof(CanStash))]
    [NotifyPropertyChangedFor(nameof(CanPopStash))]
    private bool _isMutating;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanCommit))]
    private string _commitMessage = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanCreateBranch))]
    private string _newBranchName = string.Empty;

    public ObservableCollection<SourceControlChangeViewModel> Changes { get; } = new();

    public ObservableCollection<GitBranchInfo> Branches { get; } = new();

    public SourceControlChangeViewModel? SelectedChange =>
        SelectedChangeIndex >= 0 && SelectedChangeIndex < Changes.Count
            ? Changes[SelectedChangeIndex]
            : null;

    public GitBranchInfo? SelectedBranch =>
        SelectedBranchIndex >= 0 && SelectedBranchIndex < Branches.Count
            ? Branches[SelectedBranchIndex]
            : null;

    public bool HasChanges => Changes.Count > 0;

    public bool HasConflicts => Changes.Any(static change => change.IsConflicted);

    public int ConflictCount => Changes.Count(static change => change.IsConflicted);

    public bool IsBusy => IsRefreshing || IsMutating;

    public bool CanStageSelected => !IsMutating && SelectedChange?.HasWorkingTreeChange == true;

    public bool CanUnstageSelected => !IsMutating && SelectedChange?.IsStaged == true;

    public bool CanCommit =>
        !IsMutating
        && !HasConflicts
        && !string.IsNullOrWhiteSpace(CommitMessage)
        && Changes.Any(static change => change.IsStaged);

    public bool CanSwitchBranch => !IsMutating && !HasConflicts && SelectedBranch is { IsCurrent: false };

    public bool CanCreateBranch =>
        !IsMutating
        && !HasConflicts
        && _workingDirectory is not null
        && !string.IsNullOrWhiteSpace(NewBranchName);

    public bool CanSync => !IsMutating && !HasConflicts && _workingDirectory is not null && Branches.Count > 0;

    public bool CanMerge => !IsMutating && !HasConflicts && SelectedBranch is { IsCurrent: false };

    public bool CanAbortMerge => !IsMutating && HasConflicts;

    public bool CanStash => !IsMutating && !HasConflicts && _workingDirectory is not null && HasChanges;

    public bool CanPopStash => !IsMutating && !HasConflicts && _workingDirectory is not null;

    public void SetWorkingDirectory(string? workingDirectory)
    {
        _workingDirectory = string.IsNullOrWhiteSpace(workingDirectory)
            ? null
            : Path.GetFullPath(workingDirectory);
        Changes.Clear();
        Branches.Clear();
        SelectedChangeIndex = -1;
        SelectedBranchIndex = -1;
        DiffText = string.Empty;
        CommitMessage = string.Empty;
        NewBranchName = string.Empty;
        BranchText = "No repository";
        StatusText = _workingDirectory is null
            ? "Open a Git workspace to inspect source control."
            : "Refresh source control status.";
        NotifyChangeState();
        NotifyBranchState();
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
                Branches.Clear();
                SelectedChangeIndex = -1;
                SelectedBranchIndex = -1;
                DiffText = string.Empty;
                BranchText = "No repository";
                StatusText = statusResult.Error.Message;
                NotifyChangeState();
                NotifyBranchState();
                return;
            }

            var status = statusResult.Value!;
            BranchText = CreateBranchText(status.BranchName, status.UpstreamName, status.AheadCount, status.BehindCount);
            Changes.Clear();
            foreach (var change in status.Changes)
            {
                Changes.Add(new SourceControlChangeViewModel(change));
            }

            StatusText = CreateStatusText(status.IsClean, status.Changes.Count, ConflictCount);
            SelectedChangeIndex = Changes.Count > 0 ? 0 : -1;
            NotifyChangeState();

            await LoadBranchesAsync(cancellationToken).ConfigureAwait(true);
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

    public Task StageSelectedAsync(CancellationToken cancellationToken = default)
    {
        if (_workingDirectory is null || !CanStageSelected || SelectedChange is not { } selected)
        {
            return Task.CompletedTask;
        }

        return RunMutationAsync(
            token => _repositoryService.StageAsync(_workingDirectory, selected.Path, token),
            cancellationToken: cancellationToken);
    }

    public Task UnstageSelectedAsync(CancellationToken cancellationToken = default)
    {
        if (_workingDirectory is null || !CanUnstageSelected || SelectedChange is not { } selected)
        {
            return Task.CompletedTask;
        }

        return RunMutationAsync(
            token => _repositoryService.UnstageAsync(_workingDirectory, selected.Path, token),
            cancellationToken: cancellationToken);
    }

    public Task CommitAsync(CancellationToken cancellationToken = default)
    {
        if (_workingDirectory is null || !CanCommit)
        {
            return Task.CompletedTask;
        }

        var message = CommitMessage;
        return RunMutationAsync(
            token => _repositoryService.CommitAsync(_workingDirectory, message, token),
            clearCommitMessage: true,
            cancellationToken: cancellationToken);
    }

    public Task SwitchSelectedBranchAsync(CancellationToken cancellationToken = default)
    {
        if (_workingDirectory is null || !CanSwitchBranch || SelectedBranch is not { } branch)
        {
            return Task.CompletedTask;
        }

        return RunMutationAsync(
            token => _repositoryService.SwitchBranchAsync(_workingDirectory, branch.Name, token),
            cancellationToken: cancellationToken);
    }

    public Task CreateBranchAsync(CancellationToken cancellationToken = default)
    {
        if (_workingDirectory is null || !CanCreateBranch)
        {
            return Task.CompletedTask;
        }

        var branchName = NewBranchName;
        return RunMutationAsync(
            token => _repositoryService.CreateBranchAsync(_workingDirectory, branchName, token),
            clearNewBranchName: true,
            cancellationToken: cancellationToken);
    }

    public Task FetchAsync(CancellationToken cancellationToken = default) =>
        RunSyncMutationAsync(token => _repositoryService.FetchAsync(_workingDirectory!, token), cancellationToken);

    public Task PullAsync(CancellationToken cancellationToken = default) =>
        RunSyncMutationAsync(token => _repositoryService.PullAsync(_workingDirectory!, token), cancellationToken);

    public Task PushAsync(CancellationToken cancellationToken = default) =>
        RunSyncMutationAsync(token => _repositoryService.PushAsync(_workingDirectory!, token), cancellationToken);

    public Task MergeSelectedBranchAsync(CancellationToken cancellationToken = default)
    {
        if (_workingDirectory is null || !CanMerge || SelectedBranch is not { } branch)
        {
            return Task.CompletedTask;
        }

        return RunMutationAsync(
            token => _repositoryService.MergeAsync(_workingDirectory, branch.Name, token),
            refreshOnFailure: true,
            cancellationToken: cancellationToken);
    }

    public Task AbortMergeAsync(CancellationToken cancellationToken = default)
    {
        if (_workingDirectory is null || !CanAbortMerge)
        {
            return Task.CompletedTask;
        }

        return RunMutationAsync(
            token => _repositoryService.AbortMergeAsync(_workingDirectory, token),
            refreshOnFailure: true,
            cancellationToken: cancellationToken);
    }

    public Task StashAsync(CancellationToken cancellationToken = default)
    {
        if (_workingDirectory is null || !CanStash)
        {
            return Task.CompletedTask;
        }

        return RunMutationAsync(
            token => _repositoryService.StashAsync(_workingDirectory, cancellationToken: token),
            cancellationToken: cancellationToken);
    }

    public Task PopStashAsync(CancellationToken cancellationToken = default)
    {
        if (_workingDirectory is null || !CanPopStash)
        {
            return Task.CompletedTask;
        }

        return RunMutationAsync(
            token => _repositoryService.PopStashAsync(_workingDirectory, token),
            refreshOnFailure: true,
            cancellationToken: cancellationToken);
    }

    private Task RunSyncMutationAsync(
        Func<CancellationToken, Task<Result<bool>>> action,
        CancellationToken cancellationToken) =>
        CanSync
            ? RunMutationAsync(action, cancellationToken: cancellationToken)
            : Task.CompletedTask;

    private async Task LoadBranchesAsync(CancellationToken cancellationToken)
    {
        if (_workingDirectory is null)
        {
            return;
        }

        var branchesResult = await _repositoryService
            .GetBranchesAsync(_workingDirectory, cancellationToken)
            .ConfigureAwait(true);
        Branches.Clear();
        SelectedBranchIndex = -1;
        if (branchesResult.IsFailure)
        {
            StatusText = branchesResult.Error.Message;
            NotifyBranchState();
            return;
        }

        foreach (var branch in branchesResult.Value!)
        {
            Branches.Add(branch);
        }

        SelectedBranchIndex = FindCurrentBranchIndex();
        NotifyBranchState();
    }

    private int FindCurrentBranchIndex()
    {
        for (var index = 0; index < Branches.Count; index++)
        {
            if (Branches[index].IsCurrent)
            {
                return index;
            }
        }

        return Branches.Count > 0 ? 0 : -1;
    }

    private async Task RunMutationAsync(
        Func<CancellationToken, Task<Result<bool>>> action,
        bool clearCommitMessage = false,
        bool clearNewBranchName = false,
        bool refreshOnFailure = false,
        CancellationToken cancellationToken = default)
    {
        IsMutating = true;
        try
        {
            var result = await action(cancellationToken).ConfigureAwait(true);
            if (result.IsFailure)
            {
                var errorMessage = result.Error.Message;
                if (refreshOnFailure)
                {
                    await RefreshAsync(cancellationToken).ConfigureAwait(true);
                    StatusText = HasConflicts
                        ? $"{errorMessage} {ConflictCount} conflict{(ConflictCount == 1 ? string.Empty : "s")} need resolution."
                        : errorMessage;
                }
                else
                {
                    StatusText = errorMessage;
                }

                return;
            }

            if (clearCommitMessage)
            {
                CommitMessage = string.Empty;
            }

            if (clearNewBranchName)
            {
                NewBranchName = string.Empty;
            }

            await RefreshAsync(cancellationToken).ConfigureAwait(true);
        }
        finally
        {
            IsMutating = false;
        }
    }

    private void NotifyChangeState()
    {
        OnPropertyChanged(nameof(HasChanges));
        OnPropertyChanged(nameof(HasConflicts));
        OnPropertyChanged(nameof(ConflictCount));
        OnPropertyChanged(nameof(CanStageSelected));
        OnPropertyChanged(nameof(CanUnstageSelected));
        OnPropertyChanged(nameof(CanCommit));
        OnPropertyChanged(nameof(CanSwitchBranch));
        OnPropertyChanged(nameof(CanCreateBranch));
        OnPropertyChanged(nameof(CanSync));
        OnPropertyChanged(nameof(CanMerge));
        OnPropertyChanged(nameof(CanAbortMerge));
        OnPropertyChanged(nameof(CanStash));
        OnPropertyChanged(nameof(CanPopStash));
    }

    private void NotifyBranchState()
    {
        OnPropertyChanged(nameof(SelectedBranch));
        OnPropertyChanged(nameof(CanSwitchBranch));
        OnPropertyChanged(nameof(CanCreateBranch));
        OnPropertyChanged(nameof(CanSync));
        OnPropertyChanged(nameof(CanMerge));
    }

    private static string CreateStatusText(bool isClean, int changeCount, int conflictCount)
    {
        if (conflictCount > 0)
        {
            return $"{conflictCount} conflict{(conflictCount == 1 ? string.Empty : "s")}. Stage resolved files, then commit or abort merge.";
        }

        return isClean
            ? "Working tree is clean."
            : $"{changeCount} change{(changeCount == 1 ? string.Empty : "s")}.";
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
