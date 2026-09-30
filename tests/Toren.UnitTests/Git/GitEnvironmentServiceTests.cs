using NUnit.Framework;
using Toren.Core.Execution.Contracts;
using Toren.Core.Execution.Models;
using Toren.Core.Results;
using Toren.Git.Services;

namespace Toren.UnitTests.Git;

[TestFixture]
public sealed class GitEnvironmentServiceTests
{
    private static readonly string[] VersionArguments = ["--version"];

    [Test]
    public async Task GetVersionUsesStandardGitVersionCommand()
    {
        var runner = new RecordingProcessRunner(
            Result.Success(new ProcessResult(0, "git version 2.51.0\n", string.Empty)));
        var service = new GitEnvironmentService(runner);

        var result = await service.GetVersionAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Is.EqualTo("git version 2.51.0"));
            Assert.That(runner.Request.FileName, Is.EqualTo("git"));
            Assert.That(runner.Request.Arguments, Is.EqualTo(VersionArguments));
        });
    }

    [Test]
    public async Task GetVersionReturnsStableErrorWhenGitCommandFails()
    {
        var runner = new RecordingProcessRunner(
            Result.Success(new ProcessResult(1, string.Empty, "git unavailable")));
        var service = new GitEnvironmentService(runner);

        var result = await service.GetVersionAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo("git.environment.unavailable"));
            Assert.That(result.Error.Message, Is.EqualTo("git unavailable"));
        });
    }

    private sealed class RecordingProcessRunner(Result<ProcessResult> result) : IProcessRunner
    {
        public ProcessRequest Request { get; private set; } = new("placeholder", []);

        public Task<Result<ProcessResult>> RunAsync(
            ProcessRequest request,
            CancellationToken cancellationToken = default)
        {
            Request = request;
            return Task.FromResult(result);
        }
    }
}
