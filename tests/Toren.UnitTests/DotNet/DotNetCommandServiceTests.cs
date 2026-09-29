using NUnit.Framework;
using Toren.Core.Execution.Contracts;
using Toren.Core.Execution.Models;
using Toren.Core.Results;
using Toren.DotNet.Execution.Models;
using Toren.DotNet.Execution.Services;
using Toren.UnitTests.TestDoubles;

namespace Toren.UnitTests.DotNet;

[TestFixture]
public sealed class DotNetCommandServiceTests
{
    [Test]
    public async Task RestoreBuildsWorkspaceTargetRequest()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var runner = new FakeProcessRunner("restored");
            var service = new DotNetCommandService(runner);

            var result = await service.ExecuteAsync(
                new DotNetCommandRequest(
                    DotNetCommandKind.Restore,
                    directory,
                    "Toren.sln"));

            Assert.Multiple(() =>
            {
                Assert.That(result.IsSuccess, Is.True);
                Assert.That(result.Value!.Succeeded, Is.True);
                Assert.That(runner.LastRequest!.FileName, Is.EqualTo("dotnet"));
                Assert.That(runner.LastRequest.WorkingDirectory, Is.EqualTo(directory));
                Assert.That(string.Join("|", runner.LastRequest.Arguments), Is.EqualTo("restore|Toren.sln"));
            });
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public async Task BuildAddsConfigurationFrameworkAndNoRestore()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var runner = new FakeProcessRunner("built");
            var service = new DotNetCommandService(runner);

            var result = await service.ExecuteAsync(
                new DotNetCommandRequest(
                    DotNetCommandKind.Build,
                    directory,
                    "src/App/App.csproj",
                    Configuration: "Release",
                    TargetFramework: "net10.0",
                    NoRestore: true));

            Assert.Multiple(() =>
            {
                Assert.That(result.IsSuccess, Is.True);
                Assert.That(
                    string.Join("|", runner.LastRequest!.Arguments),
                    Is.EqualTo("build|src/App/App.csproj|--configuration|Release|--framework|net10.0|--no-restore"));
            });
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public async Task RebuildUsesNonIncrementalBuild()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var runner = new FakeProcessRunner("rebuilt");
            var service = new DotNetCommandService(runner);

            var result = await service.ExecuteAsync(
                new DotNetCommandRequest(DotNetCommandKind.Rebuild, directory, "App.csproj"));

            Assert.Multiple(() =>
            {
                Assert.That(result.IsSuccess, Is.True);
                Assert.That(
                    string.Join("|", runner.LastRequest!.Arguments),
                    Is.EqualTo("build|App.csproj|--no-incremental"));
            });
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public async Task RunUsesProjectOption()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var runner = new FakeProcessRunner("running");
            var service = new DotNetCommandService(runner);

            var result = await service.ExecuteAsync(
                new DotNetCommandRequest(
                    DotNetCommandKind.Run,
                    directory,
                    "App.csproj",
                    Configuration: "Debug"));

            Assert.Multiple(() =>
            {
                Assert.That(result.IsSuccess, Is.True);
                Assert.That(
                    string.Join("|", runner.LastRequest!.Arguments),
                    Is.EqualTo("run|--project|App.csproj|--configuration|Debug"));
            });
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public async Task NonZeroExitRemainsCommandResultWithOutput()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var service = new DotNetCommandService(
                new FakeProcessRunner("build output", exitCode: 1, standardError: "build failed"));

            var result = await service.ExecuteAsync(
                new DotNetCommandRequest(DotNetCommandKind.Build, directory));

            Assert.Multiple(() =>
            {
                Assert.That(result.IsSuccess, Is.True);
                Assert.That(result.Value!.Succeeded, Is.False);
                Assert.That(result.Value.ExitCode, Is.EqualTo(1));
                Assert.That(result.Value.StandardOutput, Is.EqualTo("build output"));
                Assert.That(result.Value.StandardError, Is.EqualTo("build failed"));
            });
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public async Task MissingWorkingDirectoryFailsBeforeStartingProcess()
    {
        var missing = Path.Combine(Path.GetTempPath(), $"toren-command-missing-{Guid.NewGuid():N}");
        var runner = new FakeProcessRunner(string.Empty);
        var service = new DotNetCommandService(runner);

        var result = await service.ExecuteAsync(
            new DotNetCommandRequest(DotNetCommandKind.Clean, missing));

        Assert.Multiple(() =>
        {
            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo("dotnet.command.working-directory.unavailable"));
            Assert.That(runner.LastRequest, Is.Null);
        });
    }

    [Test]
    public async Task ProcessStartFailureIsMappedToCommandError()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var service = new DotNetCommandService(new FailingProcessRunner());

            var result = await service.ExecuteAsync(
                new DotNetCommandRequest(DotNetCommandKind.Publish, directory));

            Assert.Multiple(() =>
            {
                Assert.That(result.IsFailure, Is.True);
                Assert.That(result.Error.Code, Is.EqualTo("dotnet.command.start.failed"));
                Assert.That(result.Error.Message, Does.Contain("missing dotnet"));
            });
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"toren-command-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }

    private sealed class FailingProcessRunner : IProcessRunner
    {
        public Task<Result<ProcessResult>> RunAsync(
            ProcessRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(
                Result.Failure<ProcessResult>(
                    OperationError.Create("process.start.failed", "missing dotnet")));
        }
    }
}
