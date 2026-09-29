using NUnit.Framework;
using Toren.App.SourceControl.ViewModels;
using Toren.Core.Results;
using Toren.Git.Contracts;
using Toren.Git.Models;

namespace Toren.UnitTests.SourceControl;

[TestFixture]
public sealed class SourceControlConflictViewModelTests
{
    [Test]
    public async Task RefreshSurfacesConflictAndAllowsResolvedFileToBeStaged()
    {
        var service = new ConflictGitRepositoryService
        {
            CurrentStatus = CreateConflictStatus(),
        };
        var viewModel = new SourceControlViewModel(service);
        viewModel.SetWorkingDirectory(Path.GetTempPath());

        await viewModel.RefreshAsync();

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.HasConflicts, Is.True);
            Assert.That(viewModel.ConflictCount, Is.EqualTo(1));
            Assert.That(viewModel.StatusText, Does.StartWith("1 conflict"));
            Assert.That(viewModel.Changes[0].StatusText, Is.EqualTo("C"));
            Assert.That(viewModel.CanStageSelected, Is.True);
            Assert.That(viewModel.CanCommit, Is.False);
            Assert.That(viewModel.CanSync, Is.False);
            Assert.That(viewModel.CanAbortMerge, Is.True);
        });
    }

    [Test]
    public async Task MergeFailureRefreshesRepositorySoConflictsBecomeVisible()
    {
        var service = new ConflictGitRepositoryService
        {
            CurrentStatus = new GitRepositoryStatus("main", "origin/main", 0, 0, []),
            MergeError = OperationError.Create("git.command.failed", "Automatic merge failed."),
            StatusAfterMerge = CreateConflictStatus(),
        };
        var viewModel = new SourceControlViewModel(service);
        viewModel.SetWorkingDirectory(Path.GetTempPath());
        await viewModel.RefreshAsync();
        viewModel.SelectedBranchIndex = 1;

        await viewModel.MergeSelectedBranchAsync();

        Assert.Multiple(() =>
        {
            Assert.That(service.MergeRequests, Has.Count.EqualTo(1));
            Assert.That(service.MergeRequests[0], Is.EqualTo("feature/git"));
            Assert.That(service.StatusRequestCount, Is.EqualTo(2));
            Assert.That(viewModel.HasConflicts, Is.True);
            Assert.That(viewModel.StatusText, Does.Contain("Automatic merge failed."));
            Assert.That(viewModel.StatusText, Does.Contain("1 conflict"));
        });
    }

    [Test]
    public async Task AbortMergeRefreshesConflictState()
    {
        var service = new ConflictGitRepositoryService
        {
            CurrentStatus = CreateConflictStatus(),
            StatusAfterAbort = new GitRepositoryStatus("main", "origin/main", 0, 0, []),
        };
        var viewModel = new SourceControlViewModel(service);
        viewModel.SetWorkingDirectory(Path.GetTempPath());
        await viewModel.RefreshAsync();

        await viewModel.AbortMergeAsync();

        Assert.Multiple(() =>
        {
            Assert.That(service.AbortRequestCount, Is.EqualTo(1));
            Assert.That(viewModel.HasConflicts, Is.False);
            Assert.That(viewModel.StatusText, Is.EqualTo("Working tree is clean."));
        });
    }

    private static GitRepositoryStatus CreateConflictStatus() =>
        new(
            "main",
            "origin/main",
            0,
            0,
            [new GitChange("src/Conflict.cs", null, 'U', 'U', IsConflicted: true)]);

    private sealed class ConflictGitRepositoryService : IGitRepositoryService
    {
        public GitRepositoryStatus CurrentStatus { get; set; } =
            new("main", "origin/main", 0, 0, []);

        public GitRepositoryStatus? StatusAfterMerge { get; init; }

        public GitRepositoryStatus? StatusAfterAbort { get; init; }

        public OperationError MergeError { get; init; } = OperationError.None;

        public List<string> MergeRequests { get; } = [];

        public int AbortRequestCount { get; private set; }

        public int StatusRequestCount { get; private set; }

        public Task<Result<GitRepositoryStatus>> GetStatusAsync(
            string workingDirectory,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            StatusRequestCount++;
            return Task.FromResult(Result.Success(CurrentStatus));
        }

        public Task<Result<string>> GetDiffAsync(
            string workingDirectory,
            string? path = null,
            bool staged = false,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success("conflict diff"));

        public Task<Result<bool>> StageAsync(string workingDirectory, string path, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success(true));

        public Task<Result<bool>> UnstageAsync(string workingDirectory, string path, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success(true));

        public Task<Result<bool>> CommitAsync(string workingDirectory, string message, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success(true));

        public Task<Result<IReadOnlyList<GitBranchInfo>>> GetBranchesAsync(
            string workingDirectory,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success<IReadOnlyList<GitBranchInfo>>(
                [
                    new GitBranchInfo("main", true, "origin/main"),
                    new GitBranchInfo("feature/git", false, "origin/feature/git"),
                ]));

        public Task<Result<bool>> SwitchBranchAsync(string workingDirectory, string branchName, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success(true));

        public Task<Result<bool>> CreateBranchAsync(string workingDirectory, string branchName, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success(true));

        public Task<Result<bool>> FetchAsync(string workingDirectory, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success(true));

        public Task<Result<bool>> PullAsync(string workingDirectory, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success(true));

        public Task<Result<bool>> PushAsync(string workingDirectory, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success(true));

        public Task<Result<bool>> MergeAsync(
            string workingDirectory,
            string branchName,
            CancellationToken cancellationToken = default)
        {
            MergeRequests.Add(branchName);
            if (StatusAfterMerge is not null)
            {
                CurrentStatus = StatusAfterMerge;
            }

            return Task.FromResult(MergeError.IsNone
                ? Result.Success(true)
                : Result.Failure<bool>(MergeError));
        }

        public Task<Result<bool>> AbortMergeAsync(
            string workingDirectory,
            CancellationToken cancellationToken = default)
        {
            AbortRequestCount++;
            if (StatusAfterAbort is not null)
            {
                CurrentStatus = StatusAfterAbort;
            }

            return Task.FromResult(Result.Success(true));
        }

        public Task<Result<bool>> StashAsync(string workingDirectory, string? message = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success(true));

        public Task<Result<bool>> PopStashAsync(string workingDirectory, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success(true));
    }
}
