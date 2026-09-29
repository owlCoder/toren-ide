using NUnit.Framework;
using Toren.Core.Execution.Contracts;
using Toren.Core.Execution.Models;
using Toren.Core.Results;
using Toren.DotNet.Testing.Contracts;
using Toren.DotNet.Testing.Models;
using Toren.DotNet.Testing.Services;

namespace Toren.UnitTests.Testing;

[TestFixture]
public sealed class DotNetTestDebugServiceTests
{
    [Test]
    public async Task VstestDebugStartsSelectedTestWithHostDebugEnvironmentAndReturnsPid()
    {
        var runner = new RecordingStreamingProcessRunner(
            new ProcessOutputLine(
                ProcessOutputChannel.StandardOutput,
                "Process Id: 4321, Name: testhost"));
        var service = new DotNetTestDebugService(runner);
        var projectPath = Path.GetFullPath(Path.Combine("repo", "Tests.csproj"));

        var result = await service.StartAsync(
            new DotNetTestRunRequest(
                projectPath,
                Configuration: "Debug",
                FullyQualifiedName: "Tests.Sample.Passes"),
            static _ => { });

        Assert.That(result.IsSuccess, Is.True);
        await using var session = result.Value!;
        var request = runner.Request;
        Assert.That(request, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(session.ProcessId, Is.EqualTo(4321));
            Assert.That(
                request!.Arguments,
                Is.EqualTo(new[]
                {
                    "test",
                    projectPath,
                    "--configuration",
                    "Debug",
                    "--filter",
                    "FullyQualifiedName=Tests.Sample.Passes",
                }));
            Assert.That(request.EnvironmentVariables, Is.Not.Null);
            Assert.That(request.EnvironmentVariables!["VSTEST_HOST_DEBUG"], Is.EqualTo("1"));
        });
    }

    [Test]
    public async Task MicrosoftTestingPlatformDebugUsesRunnerIdentityAndWaitAttachEnvironment()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteGlobalJson("Microsoft.Testing.Platform");
        var projectPath = workspace.CreateProject();
        var runner = new RecordingStreamingProcessRunner(
            new ProcessOutputLine(
                ProcessOutputChannel.StandardOutput,
                "Waiting for debugger to attach... Process Id: 987, Name: Tests"));
        var service = new DotNetTestDebugService(runner);

        var result = await service.StartAsync(
            new DotNetTestRunRequest(
                projectPath,
                FullyQualifiedName: "Tests.Sample.Passes",
                RunnerId: "test-uid-7"),
            static _ => { });

        Assert.That(result.IsSuccess, Is.True);
        await using var session = result.Value!;
        var request = runner.Request;
        Assert.That(request, Is.Not.Null);
        Assert.Multiple(() =>
        {
            Assert.That(session.ProcessId, Is.EqualTo(987));
            Assert.That(
                request!.Arguments,
                Is.EqualTo(new[]
                {
                    "test",
                    "--project",
                    projectPath,
                    "--no-ansi",
                    "--no-progress",
                    "--",
                    "--filter-uid",
                    "test-uid-7",
                }));
            Assert.That(request.EnvironmentVariables, Is.Not.Null);
            Assert.That(
                request.EnvironmentVariables!["TESTINGPLATFORM_WAIT_ATTACH_DEBUGGER"],
                Is.EqualTo("1"));
        });
    }

    [Test]
    public async Task DebugFailsWhenRunnerExitsBeforePublishingProcessId()
    {
        var runner = new RecordingStreamingProcessRunner(
            new ProcessOutputLine(ProcessOutputChannel.StandardOutput, "Starting test execution..."));
        var service = new DotNetTestDebugService(runner);
        var projectPath = Path.GetFullPath(Path.Combine("repo", "Tests.csproj"));

        var result = await service.StartAsync(
            new DotNetTestRunRequest(
                projectPath,
                FullyQualifiedName: "Tests.Sample.Passes"),
            static _ => { });

        Assert.Multiple(() =>
        {
            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo("dotnet.test-debug.pid-not-found"));
        });
    }

    [Test]
    public async Task MicrosoftTestingPlatformDebugRequiresDiscoveredRunnerIdentity()
    {
        using var workspace = new TemporaryWorkspace();
        workspace.WriteGlobalJson("Microsoft.Testing.Platform");
        var projectPath = workspace.CreateProject();
        var runner = new RecordingStreamingProcessRunner();
        var service = new DotNetTestDebugService(runner);

        var result = await service.StartAsync(
            new DotNetTestRunRequest(
                projectPath,
                FullyQualifiedName: "Tests.Sample.Passes"),
            static _ => { });

        Assert.Multiple(() =>
        {
            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo("dotnet.test-debug.mtp-selection-identity-required"));
            Assert.That(runner.Request, Is.Null);
        });
    }

    private sealed class RecordingStreamingProcessRunner(params ProcessOutputLine[] output) : IStreamingProcessRunner
    {
        private readonly ProcessOutputLine[] _output = output;

        public ProcessRequest? Request { get; private set; }

        public Task<Result<ProcessResult>> RunAsync(
            ProcessRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Request = request;
            return Task.FromResult(Result.Success(new ProcessResult(0, string.Empty, string.Empty)));
        }

        public Task<Result<ProcessResult>> RunStreamingAsync(
            ProcessRequest request,
            Action<ProcessOutputLine> onOutput,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Request = request;
            foreach (var line in _output)
            {
                onOutput(line);
            }

            return Task.FromResult(Result.Success(new ProcessResult(0, string.Empty, string.Empty)));
        }
    }

    private sealed class TemporaryWorkspace : IDisposable
    {
        private readonly string _root = Path.Combine(
            Path.GetTempPath(),
            $"toren-test-debug-{Guid.NewGuid():N}");

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
