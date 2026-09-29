using NUnit.Framework;
using Toren.App.SourceControl.ViewModels;
using Toren.Core.Results;
using Toren.Git.Contracts;
using Toren.Git.Models;

namespace Toren.UnitTests.SourceControl;

[TestFixture]
public sealed class SourceControlViewModelTests
{
    [Test]
    public async Task RefreshMapsBranchChangesAndLoadsSelectedDiff()
    {
        var service = new StubGitRepositoryService(
            new GitRepositoryStatus(
                "main",
                "origin/main",
                2,
                1,
                [new GitChange("src/Program.cs", null, 'M', '.')]),
            [
                new GitBranchInfo("main", true, "origin/main"),
                new GitBranchInfo("feature/git", false, "origin/feature/git"),
            ]);
        var viewModel = new SourceControlViewModel(service);
        viewModel.SetWorkingDirectory(Path.GetTempPath());

        await viewModel.RefreshAsync();

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.BranchText, Is.EqualTo("main → origin/main · ↑2 ↓1"));
            Assert.That(viewModel.StatusText, Is.EqualTo("1 change."));
            Assert.That(viewModel.Changes, Has.Count.EqualTo(1));
            Assert.That(viewModel.SelectedChangeIndex, Is.EqualTo(0));
            Assert.That(viewModel.DiffText, Is.EqualTo("diff:src/Program.cs:staged=True"));
            Assert.That(viewModel.CanUnstageSelected, Is.True);
            Assert.That(viewModel.Branches, Has.Count.EqualTo(2));
            Assert.That(viewModel.SelectedBranchIndex, Is.EqualTo(0));
            Assert.That(viewModel.SelectedBranch!.Name, Is.EqualTo("main"));
            Assert.That(viewModel.CanSync, Is.True);
        });
    }

    [Test]
    public async Task RefreshShowsUntrackedFileWithoutRequestingDiff()
    {
        var service = new StubGitRepositoryService(
            new GitRepositoryStatus(
                "main",
                null,
                0,
                0,
                [new GitChange("notes.txt", null, '?', '?', IsUntracked: true)]));
        var viewModel = new SourceControlViewModel(service);
        viewModel.SetWorkingDirectory(Path.GetTempPath());

        await viewModel.RefreshAsync();

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.DiffText, Does.StartWith("Untracked file"));
            Assert.That(viewModel.CanStageSelected, Is.True);
            Assert.That(service.DiffRequests, Is.Empty);
        });
    }

    [Test]
    public async Task StageSelectedExecutesMutationAndRefreshesStatus()
    {
        var service = new StubGitRepositoryService(
            new GitRepositoryStatus(
                "main",
                null,
                0,
                0,
                [new GitChange("notes.txt", null, '?', '?', IsUntracked: true)]));
        var viewModel = new SourceControlViewModel(service);
        viewModel.SetWorkingDirectory(Path.GetTempPath());
        await viewModel.RefreshAsync();

        await viewModel.StageSelectedAsync();

        Assert.Multiple(() =>
        {
            Assert.That(service.StageRequests, Has.Count.EqualTo(1));
            Assert.That(service.StageRequests[0], Is.EqualTo("notes.txt"));
            Assert.That(service.StatusRequestCount, Is.EqualTo(2));
            Assert.That(service.BranchRequestCount, Is.EqualTo(2));
        });
    }

    [Test]
    public async Task UnstageSelectedExecutesMutationAndRefreshesStatus()
    {
        var service = new StubGitRepositoryService(
            new GitRepositoryStatus(
                "main",
                null,
                0,
                0,
                [new GitChange("src/Program.cs", null, 'M', '.')]));
        var viewModel = new SourceControlViewModel(service);
        viewModel.SetWorkingDirectory(Path.GetTempPath());
        await viewModel.RefreshAsync();

        await viewModel.UnstageSelectedAsync();

        Assert.Multiple(() =>
        {
            Assert.That(service.UnstageRequests, Has.Count.EqualTo(1));
            Assert.That(service.UnstageRequests[0], Is.EqualTo("src/Program.cs"));
            Assert.That(service.StatusRequestCount, Is.EqualTo(2));
            Assert.That(service.BranchRequestCount, Is.EqualTo(2));
        });
    }

    [Test]
    public async Task CommitRequiresMessageAndStagedChangeThenClearsMessage()
    {
        var service = new StubGitRepositoryService(
            new GitRepositoryStatus(
                "main",
                null,
                0,
                0,
                [new GitChange("src/Program.cs", null, 'M', '.')]));
        var viewModel = new SourceControlViewModel(service);
        viewModel.SetWorkingDirectory(Path.GetTempPath());
        await viewModel.RefreshAsync();

        Assert.That(viewModel.CanCommit, Is.False);
        viewModel.CommitMessage = "Ship source control actions";
        Assert.That(viewModel.CanCommit, Is.True);

        await viewModel.CommitAsync();

        Assert.Multiple(() =>
        {
            Assert.That(service.CommitMessages, Has.Count.EqualTo(1));
            Assert.That(service.CommitMessages[0], Is.EqualTo("Ship source control actions"));
            Assert.That(service.StatusRequestCount, Is.EqualTo(2));
            Assert.That(viewModel.CommitMessage, Is.Empty);
        });
    }

    [Test]
    public async Task SwitchSelectedBranchExecutesAndRefreshesRepositoryState()
    {
        var service = new StubGitRepositoryService(
            new GitRepositoryStatus("main", "origin/main", 0, 0, []),
            [
                new GitBranchInfo("main", true, "origin/main"),
                new GitBranchInfo("feature/git", false, null),
            ]);
        var viewModel = new SourceControlViewModel(service);
        viewModel.SetWorkingDirectory(Path.GetTempPath());
        await viewModel.RefreshAsync();
        viewModel.SelectedBranchIndex = 1;

        Assert.That(viewModel.CanSwitchBranch, Is.True);
        await viewModel.SwitchSelectedBranchAsync();

        Assert.Multiple(() =>
        {
            Assert.That(service.SwitchBranchRequests, Has.Count.EqualTo(1));
            Assert.That(service.SwitchBranchRequests[0], Is.EqualTo("feature/git"));
            Assert.That(service.StatusRequestCount, Is.EqualTo(2));
            Assert.That(service.BranchRequestCount, Is.EqualTo(2));
        });
    }

    [Test]
    public async Task CreateBranchRequiresNameAndClearsItAfterSuccess()
    {
        var service = new StubGitRepositoryService(
            new GitRepositoryStatus("main", null, 0, 0, []));
        var viewModel = new SourceControlViewModel(service);
        viewModel.SetWorkingDirectory(Path.GetTempPath());
        await viewModel.RefreshAsync();

        Assert.That(viewModel.CanCreateBranch, Is.False);
        viewModel.NewBranchName = "feature/new-ui";
        Assert.That(viewModel.CanCreateBranch, Is.True);

        await viewModel.CreateBranchAsync();

        Assert.Multiple(() =>
        {
            Assert.That(service.CreateBranchRequests, Has.Count.EqualTo(1));
            Assert.That(service.CreateBranchRequests[0], Is.EqualTo("feature/new-ui"));
            Assert.That(viewModel.NewBranchName, Is.Empty);
            Assert.That(service.StatusRequestCount, Is.EqualTo(2));
        });
    }

    [Test]
    public async Task FetchPullAndPushUseSharedRefreshPipeline()
    {
        var service = new StubGitRepositoryService(
            new GitRepositoryStatus("main", "origin/main", 0, 0, []));
        var viewModel = new SourceControlViewModel(service);
        viewModel.SetWorkingDirectory(Path.GetTempPath());
        await viewModel.RefreshAsync();

        await viewModel.FetchAsync();
        await viewModel.PullAsync();
        await viewModel.PushAsync();

        Assert.Multiple(() =>
        {
            Assert.That(service.FetchRequestCount, Is.EqualTo(1));
            Assert.That(service.PullRequestCount, Is.EqualTo(1));
            Assert.That(service.PushRequestCount, Is.EqualTo(1));
            Assert.That(service.StatusRequestCount, Is.EqualTo(4));
            Assert.That(service.BranchRequestCount, Is.EqualTo(4));
        });
    }

    [Test]
    public async Task MutationFailureSurfacesStatusAndDoesNotRefresh()
    {
        var service = new StubGitRepositoryService(
            new GitRepositoryStatus(
                "main",
                null,
                0,
                0,
                [new GitChange("src/Program.cs", null, 'M', '.')]))
        {
            MutationError = OperationError.Create("git.command.failed", "commit failed"),
        };
        var viewModel = new SourceControlViewModel(service);
        viewModel.SetWorkingDirectory(Path.GetTempPath());
        await viewModel.RefreshAsync();
        viewModel.CommitMessage = "Message";

        await viewModel.CommitAsync();

        Assert.Multiple(() =>
        {
            Assert.That(viewModel.StatusText, Is.EqualTo("commit failed"));
            Assert.That(viewModel.CommitMessage, Is.EqualTo("Message"));
            Assert.That(service.StatusRequestCount, Is.EqualTo(1));
        });
    }

    private sealed class StubGitRepositoryService : IGitRepositoryService
    {
        private readonly GitRepositoryStatus _status;
        private readonly IReadOnlyList<GitBranchInfo> _branches;

        public StubGitRepositoryService(
            GitRepositoryStatus status,
            IReadOnlyList<GitBranchInfo>? branches = null)
        {
            _status = status;
            _branches = branches
                ?? [new GitBranchInfo(status.BranchName ?? "main", true, status.UpstreamName)];
        }

        public List<(string Path, bool Staged)> DiffRequests { get; } = [];
        public List<string> StageRequests { get; } = [];
        public List<string> UnstageRequests { get; } = [];
        public List<string> CommitMessages { get; } = [];
        public List<string> SwitchBranchRequests { get; } = [];
        public List<string> CreateBranchRequests { get; } = [];
        public int StatusRequestCount { get; private set; }
        public int BranchRequestCount { get; private set; }
        public int FetchRequestCount { get; private set; }
        public int PullRequestCount { get; private set; }
        public int PushRequestCount { get; private set; }
        public OperationError MutationError { get; init; } = OperationError.None;

        public Task<Result<GitRepositoryStatus>> GetStatusAsync(string workingDirectory, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            StatusRequestCount++;
            return Task.FromResult(Result.Success(_status));
        }

        public Task<Result<string>> GetDiffAsync(string workingDirectory, string? path = null, bool staged = false, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var resolvedPath = path ?? string.Empty;
            DiffRequests.Add((resolvedPath, staged));
            return Task.FromResult(Result.Success($"diff:{resolvedPath}:staged={staged}"));
        }

        public Task<Result<bool>> StageAsync(string workingDirectory, string path, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            StageRequests.Add(path);
            return Task.FromResult(CreateMutationResult());
        }

        public Task<Result<bool>> UnstageAsync(string workingDirectory, string path, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            UnstageRequests.Add(path);
            return Task.FromResult(CreateMutationResult());
        }

        public Task<Result<bool>> CommitAsync(string workingDirectory, string message, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CommitMessages.Add(message);
            return Task.FromResult(CreateMutationResult());
        }

        public Task<Result<IReadOnlyList<GitBranchInfo>>> GetBranchesAsync(string workingDirectory, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            BranchRequestCount++;
            return Task.FromResult(Result.Success(_branches));
        }

        public Task<Result<bool>> SwitchBranchAsync(string workingDirectory, string branchName, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SwitchBranchRequests.Add(branchName);
            return Task.FromResult(CreateMutationResult());
        }

        public Task<Result<bool>> CreateBranchAsync(string workingDirectory, string branchName, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CreateBranchRequests.Add(branchName);
            return Task.FromResult(CreateMutationResult());
        }

        public Task<Result<bool>> FetchAsync(string workingDirectory, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            FetchRequestCount++;
            return Task.FromResult(CreateMutationResult());
        }

        public Task<Result<bool>> PullAsync(string workingDirectory, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PullRequestCount++;
            return Task.FromResult(CreateMutationResult());
        }

        public Task<Result<bool>> PushAsync(string workingDirectory, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PushRequestCount++;
            return Task.FromResult(CreateMutationResult());
        }

        public Task<Result<bool>> MergeAsync(string workingDirectory, string branchName, CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateMutationResult());

        public Task<Result<bool>> AbortMergeAsync(string workingDirectory, CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateMutationResult());

        public Task<Result<bool>> StashAsync(string workingDirectory, string? message = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateMutationResult());

        public Task<Result<bool>> PopStashAsync(string workingDirectory, CancellationToken cancellationToken = default) =>
            Task.FromResult(CreateMutationResult());

        private Result<bool> CreateMutationResult() => MutationError.IsNone
            ? Result.Success(true)
            : Result.Failure<bool>(MutationError);
    }
}
