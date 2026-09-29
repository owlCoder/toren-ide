using NUnit.Framework;
using Toren.Core.Execution.Contracts;
using Toren.Core.Execution.Models;
using Toren.Core.Results;
using Toren.DotNet.Testing.Models;
using Toren.DotNet.Testing.Services;

namespace Toren.UnitTests.Testing;

[TestFixture]
public sealed class DotNetMtpTestDiscoveryServiceTests
{
    [Test]
    public async Task DiscoveryRequestsStructuredMtpTestListAndReturnsUid()
    {
        using var workspace = new TemporaryWorkspace();
        var projectPath = workspace.CreateProject();
        var runner = new RecordingProcessRunner(
            """
            {
              "schemaVersion": 1,
              "tests": [
                {
                  "uid": "uid-123",
                  "displayName": "Passes",
                  "type": {
                    "namespace": "Sample.Tests",
                    "typeName": "SmokeTests",
                    "methodName": "Passes"
                  }
                }
              ]
            }
            """);
        var service = new DotNetTestDiscoveryService(runner);

        var result = await service.DiscoverAsync(new DotNetTestDiscoveryRequest(projectPath));
        var tests = result.Value!;
        var processRequest = runner.Request!;

        Assert.That(result.IsSuccess, Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(tests, Has.Count.EqualTo(1));
            Assert.That(tests[0].RunnerId, Is.EqualTo("uid-123"));
            Assert.That(
                processRequest.Arguments,
                Is.EqualTo(new[]
                {
                    "test",
                    "--project",
                    projectPath,
                    "--nologo",
                    "--verbosity",
                    "quiet",
                    "--no-ansi",
                    "--no-progress",
                    "--",
                    "--list-tests",
                    "json",
                }));
        });
    }

    private sealed class RecordingProcessRunner(string standardOutput) : IProcessRunner
    {
        public ProcessRequest? Request { get; private set; }

        public Task<Result<ProcessResult>> RunAsync(
            ProcessRequest request,
            CancellationToken cancellationToken = default)
        {
            Request = request;
            return Task.FromResult(Result.Success(new ProcessResult(0, standardOutput, string.Empty)));
        }
    }

    private sealed class TemporaryWorkspace : IDisposable
    {
        private readonly string _root = Path.Combine(
            Path.GetTempPath(),
            $"toren-mtp-discovery-{Guid.NewGuid():N}");

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
