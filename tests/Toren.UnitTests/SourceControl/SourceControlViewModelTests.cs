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
                [new GitChange("src/Program.cs", null, 'M', '.')]));
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
            Assert.That(service.DiffRequests, Is.Empty);
        });
    }

    private sealed class StubGitRepositoryService(GitRepositoryStatus status) : IGitRepositoryService
    {
        public List<(string Path, bool Staged)> DiffRequests { get; } = [];

        public Task<Result<GitRepositoryStatus>> GetStatusAsync(
            string workingDirectory,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Result.Success(status));
        }

        public Task<Result<string>> GetDiffAsync(
            string workingDirectory,
            string? path = null,
            bool staged = false,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var resolvedPath = path ?? string.Empty;
            DiffRequests.Add((resolvedPath, staged));
            return Task.FromResult(Result.Success($"diff:{resolvedPath}:staged={staged}"));
        }
    }
}
