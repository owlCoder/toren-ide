using NUnit.Framework;
using Toren.Core.Execution.Contracts;
using Toren.Core.Execution.Models;
using Toren.Core.Results;
using Toren.DotNet.UserSecrets.Services;

namespace Toren.UnitTests.DotNet;

[TestFixture]
public sealed class DotNetUserSecretsServiceTests
{
    [Test]
    public async Task ListParsesEntriesAndPreservesEqualsInValues()
    {
        using var directory = new TemporaryDirectory();
        var projectPath = CreateProject(directory.Path);
        var runner = new RecordingProcessRunner(
            Result.Success(new ProcessResult(
                0,
                "ApiKey = abc=def==\nConnectionStrings:Main = Server=localhost;Database=App\n",
                string.Empty)));
        var service = new DotNetUserSecretsService(runner);

        var result = await service.ListAsync(projectPath);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Has.Count.EqualTo(2));
            Assert.That(result.Value![0].Key, Is.EqualTo("ApiKey"));
            Assert.That(result.Value[0].Value, Is.EqualTo("abc=def=="));
            Assert.That(result.Value[1].Value, Is.EqualTo("Server=localhost;Database=App"));
            Assert.That(
                string.Join('\u001F', runner.Requests[0].Arguments),
                Is.EqualTo($"user-secrets\u001Flist\u001F--project\u001F{projectPath}"));
        });
    }

    [Test]
    public async Task SetUsesProjectKeyAndValue()
    {
        using var directory = new TemporaryDirectory();
        var projectPath = CreateProject(directory.Path);
        var runner = CreateSuccessfulRunner();
        var service = new DotNetUserSecretsService(runner);

        var result = await service.SetAsync(projectPath, "ApiKey", "secret value");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(runner.Requests, Has.Count.EqualTo(1));
            Assert.That(runner.Requests[0].FileName, Is.EqualTo("dotnet"));
            Assert.That(runner.Requests[0].WorkingDirectory, Is.EqualTo(directory.Path));
            Assert.That(
                string.Join('\u001F', runner.Requests[0].Arguments),
                Is.EqualTo($"user-secrets\u001Fset\u001FApiKey\u001Fsecret value\u001F--project\u001F{projectPath}"));
        });
    }

    [Test]
    public async Task InitializeRemoveAndClearUseStandardCommands()
    {
        using var directory = new TemporaryDirectory();
        var projectPath = CreateProject(directory.Path);
        var runner = CreateSuccessfulRunner();
        var service = new DotNetUserSecretsService(runner);

        Assert.That((await service.InitializeAsync(projectPath)).IsSuccess, Is.True);
        Assert.That((await service.RemoveAsync(projectPath, "ApiKey")).IsSuccess, Is.True);
        Assert.That((await service.ClearAsync(projectPath)).IsSuccess, Is.True);

        Assert.Multiple(() =>
        {
            Assert.That(
                string.Join('\u001F', runner.Requests[0].Arguments),
                Is.EqualTo($"user-secrets\u001Finit\u001F--project\u001F{projectPath}"));
            Assert.That(
                string.Join('\u001F', runner.Requests[1].Arguments),
                Is.EqualTo($"user-secrets\u001Fremove\u001FApiKey\u001F--project\u001F{projectPath}"));
            Assert.That(
                string.Join('\u001F', runner.Requests[2].Arguments),
                Is.EqualTo($"user-secrets\u001Fclear\u001F--project\u001F{projectPath}"));
        });
    }

    [Test]
    public async Task MissingProjectReturnsStableErrorWithoutExecutingProcess()
    {
        using var directory = new TemporaryDirectory();
        var runner = CreateSuccessfulRunner();
        var service = new DotNetUserSecretsService(runner);

        var result = await service.ListAsync(Path.Combine(directory.Path, "Missing.csproj"));

        Assert.Multiple(() =>
        {
            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo("dotnet.user-secrets.project.unavailable"));
            Assert.That(runner.Requests, Is.Empty);
        });
    }

    [Test]
    public async Task NonZeroCommandReturnsUserSecretsError()
    {
        using var directory = new TemporaryDirectory();
        var projectPath = CreateProject(directory.Path);
        var runner = new RecordingProcessRunner(
            Result.Success(new ProcessResult(1, string.Empty, "UserSecretsId is missing.")));
        var service = new DotNetUserSecretsService(runner);

        var result = await service.ListAsync(projectPath);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo("dotnet.user-secrets.command.failed"));
            Assert.That(result.Error.Message, Is.EqualTo("UserSecretsId is missing."));
        });
    }

    private static string CreateProject(string directory)
    {
        var projectPath = Path.Combine(directory, "App.csproj");
        File.WriteAllText(projectPath, "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        return projectPath;
    }

    private static RecordingProcessRunner CreateSuccessfulRunner() =>
        new(Result.Success(new ProcessResult(0, string.Empty, string.Empty)));

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

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"toren-user-secrets-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
