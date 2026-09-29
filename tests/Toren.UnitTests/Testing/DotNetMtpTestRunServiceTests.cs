using NUnit.Framework;
using Toren.Core.Execution.Contracts;
using Toren.Core.Execution.Models;
using Toren.Core.Results;
using Toren.DotNet.Testing.Models;
using Toren.DotNet.Testing.Services;

namespace Toren.UnitTests.Testing;

[TestFixture]
public sealed class DotNetMtpTestRunServiceTests
{
    [Test]
    public async Task SelectedMtpTestUsesDiscoveredUidFilter()
    {
        using var workspace = new TemporaryWorkspace();
        var projectPath = workspace.CreateProject();
        var runner = new RecordingStreamingProcessRunner();
        var service = new DotNetTestRunService(runner);

        var result = await service.RunAsync(
            new DotNetTestRunRequest(
                projectPath,
                FullyQualifiedName: "Sample.Tests.CalculatorTests.Adds",
                RunnerId: "uid-123"),
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
                "--",
                "--filter-uid",
                "uid-123",
            }));
    }

    private sealed class RecordingStreamingProcessRunner : IStreamingProcessRunner
    {
        public ProcessRequest? Request { get; private set; }

        public Task<Result<ProcessResult>> RunAsync(
            ProcessRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success(new ProcessResult(0, string.Empty, string.Empty)));

        public Task<Result<ProcessResult>> RunStreamingAsync(
            ProcessRequest request,
            Action<ProcessOutputLine> onOutput,
            CancellationToken cancellationToken = default)
        {
            Request = request;
            return Task.FromResult(Result.Success(new ProcessResult(0, string.Empty, string.Empty)));
        }
    }

    private sealed class TemporaryWorkspace : IDisposable
    {
        private readonly string _root = Path.Combine(
            Path.GetTempPath(),
            $"toren-mtp-run-{Guid.NewGuid():N}");

        public TemporaryWorkspace()
        {
            Directory.CreateDirectory(_root);
            File.WriteAllText(
                Path.Combine(_root, "global.json"),
                """
                {
                  "test": {
                    "runner": "Microsoft.Testing.Platform"
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
