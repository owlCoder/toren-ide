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
        var processResult = result.Value;
        var request = runner.Request;
        Assert.That(processResult, Is.Not.Null);
        Assert.That(request, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(processResult!.ExitCode, Is.EqualTo(1));
            Assert.That(request!.FileName, Is.EqualTo("dotnet"));
            Assert.That(
                request.Arguments,
                Is.EqualTo(new[]
                {
                    "test",
                    projectPath,
                    "--configuration",
                    "Release",
                    "--framework",
                    "net10.0",
                }));
            Assert.That(request.WorkingDirectory, Is.EqualTo(Path.GetDirectoryName(projectPath)));
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
        var request = runner.Request;
        Assert.That(request, Is.Not.Null);
        Assert.That(
            request!.Arguments,
            Is.EqualTo(new[]
            {
                "test",
                projectPath,
                "--filter",
                "FullyQualifiedName=Tests.Sample.Passes",
            }));
    }

    [Test]
    public async Task MicrosoftTestingPlatformUsesProjectModeForWholeProjectRun()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteGlobalJson("Microsoft.Testing.Platform");
        var projectPath = workspace.CreateProject();
        var runner = new RecordingStreamingProcessRunner();
        var service = new DotNetTestRunService(runner);

        var result = await service.RunAsync(
            new DotNetTestRunRequest(projectPath, "Release", "net10.0"),
            static _ => { });

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(
            runner.Request!.Arguments,
            Is.EqualTo(new[]
            {
                "test",
                "--project",
                projectPath,
                "--no-ansi",
                "--no-progress",
                "--configuration",
                "Release",
                "--framework",
                "net10.0",
            }));
    }

    [Test]
    public async Task MicrosoftTestingPlatformSelectedTestRequiresRunnerIdentity()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteGlobalJson("Microsoft.Testing.Platform");
        var projectPath = workspace.CreateProject();
        var runner = new RecordingStreamingProcessRunner();
        var service = new DotNetTestRunService(runner);

        var result = await service.RunAsync(
            new DotNetTestRunRequest(projectPath, FullyQualifiedName: "Tests.Sample.Passes"),
            static _ => { });

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Error.Code, Is.EqualTo("dotnet.test-run.mtp-selection-identity-required"));
            Assert.That(runner.Request, Is.Null);
        });
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

    private sealed class TemporaryWorkspace : IDisposable
    {
        private readonly string _root = Path.Combine(
            Path.GetTempPath(),
            $"toren-test-run-{Guid.NewGuid():N}");

        public TemporaryWorkspace()
        {
            Directory.CreateDirectory(_root);
        }

        public void WriteGlobalJson(string runner)
        {
            File.WriteAllText(
                Path.Combine(_root, "global.json"),
                $$"""
                {
                  "test": {
                    "runner": "{{runner}}"
                  }
                }
                """);
        }

        public string CreateProject()
        {
            var path = Path.Combine(_root, "Tests.csproj");
            File.WriteAllText(path, "<Project Sdk=\"Microsoft.NET.Sdk\" />");
            return Path.GetFullPath(path);
        }

        public void Dispose()
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
