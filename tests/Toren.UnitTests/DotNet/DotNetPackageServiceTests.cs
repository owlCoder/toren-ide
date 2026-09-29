using NUnit.Framework;
using Toren.Core.Execution.Contracts;
using Toren.Core.Execution.Models;
using Toren.Core.Results;
using Toren.DotNet.Packages.Services;

namespace Toren.UnitTests.DotNet;

[TestFixture]
public sealed class DotNetPackageServiceTests
{
    [Test]
    public async Task SearchUsesJsonPagingPrereleaseAndSources()
    {
        using var directory = new TemporaryDirectory();
        var runner = new RecordingProcessRunner(
            Result.Success(new ProcessResult(
                0,
                "{\"version\":2,\"problems\":[],\"searchResult\":[]}",
                string.Empty)));
        var service = new DotNetPackageService(runner);

        var result = await service.SearchAsync(
            directory.Path,
            "json",
            ["https://api.nuget.org/v3/index.json", "https://feed.example/v3/index.json"],
            skip: 5,
            take: 10,
            includePrerelease: true);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(runner.Requests, Has.Count.EqualTo(1));
            Assert.That(runner.Requests[0].FileName, Is.EqualTo("dotnet"));
            Assert.That(runner.Requests[0].WorkingDirectory, Is.EqualTo(directory.Path));
            Assert.That(
                string.Join('\u001F', runner.Requests[0].Arguments),
                Is.EqualTo(
                    "package\u001Fsearch\u001Fjson\u001F--format\u001Fjson\u001F--skip\u001F5\u001F--take\u001F10\u001F--prerelease\u001F--source\u001Fhttps://api.nuget.org/v3/index.json\u001F--source\u001Fhttps://feed.example/v3/index.json"));
        });
    }

    [Test]
    public async Task InstalledUsesVersionedJsonOutputForProject()
    {
        using var directory = new TemporaryDirectory();
        var projectPath = System.IO.Path.Combine(directory.Path, "App.csproj");
        await File.WriteAllTextAsync(projectPath, "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        var runner = new RecordingProcessRunner(
            Result.Success(new ProcessResult(
                0,
                "{\"version\":1,\"projects\":[]}",
                string.Empty)));
        var service = new DotNetPackageService(runner);

        var result = await service.GetInstalledAsync(projectPath);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(runner.Requests, Has.Count.EqualTo(1));
            Assert.That(
                string.Join('\u001F', runner.Requests[0].Arguments),
                Is.EqualTo($"package\u001Flist\u001F--project\u001F{projectPath}\u001F--format\u001Fjson\u001F--output-version\u001F1"));
        });
    }

    [Test]
    public async Task AddSupportsVersionAndCustomSource()
    {
        using var directory = new TemporaryDirectory();
        var projectPath = CreateProject(directory.Path);
        var runner = CreateSuccessfulRunner();
        var service = new DotNetPackageService(runner);

        var result = await service.AddAsync(
            projectPath,
            "Newtonsoft.Json",
            "13.0.3",
            "https://feed.example/v3/index.json");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(
                string.Join('\u001F', runner.Requests[0].Arguments),
                Is.EqualTo($"package\u001Fadd\u001FNewtonsoft.Json\u001F--version\u001F13.0.3\u001F--source\u001Fhttps://feed.example/v3/index.json\u001F--project\u001F{projectPath}"));
        });
    }

    [Test]
    public async Task UpdateSupportsExplicitTargetVersion()
    {
        using var directory = new TemporaryDirectory();
        var projectPath = CreateProject(directory.Path);
        var runner = CreateSuccessfulRunner();
        var service = new DotNetPackageService(runner);

        var result = await service.UpdateAsync(projectPath, "Newtonsoft.Json", "13.0.4");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(
                string.Join('\u001F', runner.Requests[0].Arguments),
                Is.EqualTo($"package\u001Fupdate\u001FNewtonsoft.Json@13.0.4\u001F--project\u001F{projectPath}"));
        });
    }

    [Test]
    public async Task RemoveUsesProjectAndPackageId()
    {
        using var directory = new TemporaryDirectory();
        var projectPath = CreateProject(directory.Path);
        var runner = CreateSuccessfulRunner();
        var service = new DotNetPackageService(runner);

        var result = await service.RemoveAsync(projectPath, "Newtonsoft.Json");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(
                string.Join('\u001F', runner.Requests[0].Arguments),
                Is.EqualTo($"package\u001Fremove\u001FNewtonsoft.Json\u001F--project\u001F{projectPath}"));
        });
    }

    [Test]
    public async Task NonZeroCommandReturnsPackageError()
    {
        using var directory = new TemporaryDirectory();
        var projectPath = CreateProject(directory.Path);
        var runner = new RecordingProcessRunner(
            Result.Success(new ProcessResult(1, string.Empty, "Package does not exist.")));
        var service = new DotNetPackageService(runner);

        var result = await service.RemoveAsync(projectPath, "Missing.Package");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo("dotnet.package.command.failed"));
            Assert.That(result.Error.Message, Is.EqualTo("Package does not exist."));
        });
    }

    private static string CreateProject(string directory)
    {
        var projectPath = System.IO.Path.Combine(directory, "App.csproj");
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
                $"toren-nuget-tests-{Guid.NewGuid():N}");
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
