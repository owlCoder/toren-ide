using NUnit.Framework;
using Toren.Core.Execution.Contracts;
using Toren.Core.Execution.Models;
using Toren.Core.Results;
using Toren.DotNet.Testing.Models;
using Toren.DotNet.Testing.Services;

namespace Toren.UnitTests.Testing;

[TestFixture]
public sealed class DotNetTestRunServiceTests
{
    [Test]
    public async Task RunBuildsStandardDotNetTestCommandAndStreamsOutput()
    {
        var runner = new RecordingStreamingProcessRunner();
        var service = new DotNetTestRunService(runner);
        var projectPath = Path.GetFullPath(Path.Combine("repo", "Tests.csproj"));
        var lines = new List<ProcessOutputLine>();

        var result = await service.RunAsync(
            new DotNetTestRunRequest(projectPath, "Release", "net10.0"),
            lines.Add);

        Assert.That(result.IsSuccess, Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(result.Value.ExitCode, Is.EqualTo(1));
            Assert.That(runner.Request, Is.Not.Null);
            Assert.That(runner.Request!.FileName, Is.EqualTo("dotnet"));
            Assert.That(
                runner.Request.Arguments,
                Is.EqualTo(new[]
                {
                    "test",
                    projectPath,
                    "--configuration",
                    "Release",
                    "--framework",
                    "net10.0",
                }));
            Assert.That(runner.Request.WorkingDirectory, Is.EqualTo(Path.GetDirectoryName(projectPath)));
            Assert.That(lines, Has.Count.EqualTo(2));
            Assert.That(lines[0].Channel, Is.EqualTo(ProcessOutputChannel.StandardOutput));
            Assert.That(lines[1].Channel, Is.EqualTo(ProcessOutputChannel.StandardError));
        });
    }

    [Test]
    public async Task RunSelectedTestAddsExactFullyQualifiedNameFilter()
    {
        var runner = new RecordingStreamingProcessRunner();
        var service = new DotNetTestRunService(runner);
        var projectPath = Path.GetFullPath(Path.Combine("repo", "Tests.csproj"));

        var result = await service.RunAsync(
            new DotNetTestRunRequest(
                projectPath,
                FullyQualifiedName: "Tests.Sample.Passes"),
            static _ => { });

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(
            runner.Request!.Arguments,
            Is.EqualTo(new[]
            {
                "test",
                projectPath,
                "--filter",
                "FullyQualifiedName=Tests.Sample.Passes",
            }));
    }

    private sealed class RecordingStreamingProcessRunner : IStreamingProcessRunner
    {
        public ProcessRequest? Request { get; private set; }

        public Task<Result<ProcessResult>> RunAsync(
            ProcessRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success(new ProcessResult(1, "out", "err")));

        public Task<Result<ProcessResult>> RunStreamingAsync(
            ProcessRequest request,
            Action<ProcessOutputLine> onOutput,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Request = request;
            onOutput(new ProcessOutputLine(ProcessOutputChannel.StandardOutput, "out"));
            onOutput(new ProcessOutputLine(ProcessOutputChannel.StandardError, "err"));
            return Task.FromResult(Result.Success(new ProcessResult(1, "out", "err")));
        }
    }
}
