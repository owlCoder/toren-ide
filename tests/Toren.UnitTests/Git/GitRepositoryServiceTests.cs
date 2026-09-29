using NUnit.Framework;
using Toren.Core.Execution.Contracts;
using Toren.Core.Execution.Models;
using Toren.Core.Results;
using Toren.Git.Services;

namespace Toren.UnitTests.Git;

[TestFixture]
public sealed class GitRepositoryServiceTests
{
    [Test]
    public async Task StatusUsesPorcelainV2AndParsesRepositoryState()
    {
        var runner = new RecordingProcessRunner(
            Result.Success(new ProcessResult(
                0,
                "# branch.head main\0? src/New.cs\0",
                string.Empty)));
        var service = new GitRepositoryService(runner);

        var result = await service.GetStatusAsync(Path.GetTempPath());

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value!.BranchName, Is.EqualTo("main"));
            Assert.That(result.Value.Changes, Has.Count.EqualTo(1));
            Assert.That(runner.Requests, Has.Count.EqualTo(1));
            Assert.That(runner.Requests[0].FileName, Is.EqualTo("git"));
            Assert.That(runner.Requests[0].Arguments, Does.Contain("--porcelain=v2"));
            Assert.That(runner.Requests[0].Arguments, Does.Contain("-z"));
        });
    }

    [Test]
    public async Task DiffCanRequestStagedSingleFile()
    {
        var runner = new RecordingProcessRunner(
            Result.Success(new ProcessResult(0, "diff output", string.Empty)));
        var service = new GitRepositoryService(runner);

        var result = await service.GetDiffAsync(
            Path.GetTempPath(),
            "src/Program.cs",
            staged: true);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.EqualTo("diff output"));
            Assert.That(runner.Requests[0].Arguments, Does.Contain("--cached"));
            Assert.That(runner.Requests[0].Arguments, Does.Contain("src/Program.cs"));
        });
    }

    [Test]
    public async Task StageUsesPathSeparatorToProtectFileNames()
    {
        var runner = new RecordingProcessRunner(Result.Success(new ProcessResult(0, string.Empty, string.Empty)));
        var service = new GitRepositoryService(runner);

        var result = await service.StageAsync(Path.GetTempPath(), "-odd name.cs");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(runner.Requests, Has.Count.EqualTo(1));
            Assert.That(runner.Requests[0].Arguments.TakeLast(3), Is.EqualTo(new[] { "add", "--", "-odd name.cs" }));
        });
    }

    [Test]
    public async Task UnstageUsesRestoreStagedForSelectedPath()
    {
        var runner = new RecordingProcessRunner(Result.Success(new ProcessResult(0, string.Empty, string.Empty)));
        var service = new GitRepositoryService(runner);

        var result = await service.UnstageAsync(Path.GetTempPath(), "src/Program.cs");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(
                runner.Requests[0].Arguments.TakeLast(4),
                Is.EqualTo(new[] { "restore", "--staged", "--", "src/Program.cs" }));
        });
    }

    [Test]
    public async Task CommitUsesTrimmedMessage()
    {
        var runner = new RecordingProcessRunner(Result.Success(new ProcessResult(0, string.Empty, string.Empty)));
        var service = new GitRepositoryService(runner);

        var result = await service.CommitAsync(Path.GetTempPath(), "  Add Git actions  ");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(
                runner.Requests[0].Arguments.TakeLast(3),
                Is.EqualTo(new[] { "commit", "--message", "Add Git actions" }));
        });
    }

    [Test]
    public async Task MutationReturnsGitStandardErrorOnNonZeroExit()
    {
        var runner = new RecordingProcessRunner(
            Result.Success(new ProcessResult(1, string.Empty, "nothing to commit")));
        var service = new GitRepositoryService(runner);

        var result = await service.CommitAsync(Path.GetTempPath(), "Message");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo("git.command.failed"));
            Assert.That(result.Error.Message, Is.EqualTo("nothing to commit"));
        });
    }

    private sealed class RecordingProcessRunner(Result<ProcessResult> result) : IProcessRunner
    {
        public List<ProcessRequest> Requests { get; } = [];

        public Task<Result<ProcessResult>> RunAsync(
            ProcessRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(request);
            return Task.FromResult(result);
        }
    }
}
