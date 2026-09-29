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
