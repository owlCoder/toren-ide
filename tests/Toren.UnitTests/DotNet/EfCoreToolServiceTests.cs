using NUnit.Framework;
using Toren.Core.Execution.Contracts;
using Toren.Core.Execution.Models;
using Toren.Core.Results;
using Toren.DotNet.EntityFramework.Models;
using Toren.DotNet.EntityFramework.Services;

namespace Toren.UnitTests.DotNet;

[TestFixture]
public sealed class EfCoreToolServiceTests
{
    [Test]
    public async Task DetectReturnsVersionWhenDotNetEfIsAvailable()
    {
        using var directory = new TemporaryDirectory();
        var projectPath = CreateProject(directory.Path, "Data.csproj");
        var runner = new SequenceProcessRunner(
            Result.Success(new ProcessResult(0, "Entity Framework Core .NET Command-line Tools 10.0.0\n", string.Empty)));
        var service = new EfCoreToolService(runner);

        var result = await service.DetectAsync(projectPath);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value!.IsAvailable, Is.True);
            Assert.That(result.Value.Version, Is.EqualTo("Entity Framework Core .NET Command-line Tools 10.0.0"));
            Assert.That(string.Join('\u001F', runner.Requests[0].Arguments), Is.EqualTo("ef\u001F--version"));
        });
    }

    [Test]
    public async Task ListMigrationsParsesJsonAndProjectOptions()
    {
        using var directory = new TemporaryDirectory();
        var projectPath = CreateProject(directory.Path, "Data.csproj");
        var startupPath = CreateProject(directory.Path, "Web.csproj");
        var output = "Build started...\n[{\"id\":\"202609300001_Initial\",\"name\":\"Initial\",\"applied\":true},{\"id\":\"202609300002_AddUsers\",\"name\":\"AddUsers\",\"applied\":false}]\n";
        var runner = new SequenceProcessRunner(Result.Success(new ProcessResult(0, output, string.Empty)));
        var service = new EfCoreToolService(runner);

        var result = await service.ListMigrationsAsync(
            new EfCoreProjectRequest(projectPath, startupPath, "AppDbContext"));

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Has.Count.EqualTo(2));
            Assert.That(result.Value![0].Name, Is.EqualTo("Initial"));
            Assert.That(result.Value[0].Applied, Is.True);
            Assert.That(result.Value[1].Applied, Is.False);
            Assert.That(
                string.Join('\u001F', runner.Requests[0].Arguments),
                Is.EqualTo($"ef\u001Fmigrations\u001Flist\u001F--json\u001F--project\u001F{projectPath}\u001F--startup-project\u001F{startupPath}\u001F--context\u001FAppDbContext"));
        });
    }

    [Test]
    public async Task AddRemoveAndUpdateUseStandardEfCommands()
    {
        using var directory = new TemporaryDirectory();
        var projectPath = CreateProject(directory.Path, "Data.csproj");
        var runner = new SequenceProcessRunner(
            SuccessProcessResult(),
            SuccessProcessResult(),
            SuccessProcessResult());
        var service = new EfCoreToolService(runner);

        Assert.That((await service.AddMigrationAsync(
            new EfCoreMigrationRequest(projectPath, "AddOrders", OutputDirectory: "Persistence/Migrations"))).IsSuccess, Is.True);
        Assert.That((await service.RemoveMigrationAsync(new EfCoreProjectRequest(projectPath))).IsSuccess, Is.True);
        Assert.That((await service.UpdateDatabaseAsync(
            new EfCoreDatabaseUpdateRequest(projectPath, "202609300001_Initial"))).IsSuccess, Is.True);

        Assert.Multiple(() =>
        {
            Assert.That(
                string.Join('\u001F', runner.Requests[0].Arguments),
                Is.EqualTo($"ef\u001Fmigrations\u001Fadd\u001FAddOrders\u001F--project\u001F{projectPath}\u001F--output-dir\u001FPersistence/Migrations"));
            Assert.That(
                string.Join('\u001F', runner.Requests[1].Arguments),
                Is.EqualTo($"ef\u001Fmigrations\u001Fremove\u001F--project\u001F{projectPath}"));
            Assert.That(
                string.Join('\u001F', runner.Requests[2].Arguments),
                Is.EqualTo($"ef\u001Fdatabase\u001Fupdate\u001F202609300001_Initial\u001F--project\u001F{projectPath}"));
        });
    }

    [Test]
    public async Task InvalidMigrationJsonReturnsStableError()
    {
        using var directory = new TemporaryDirectory();
        var projectPath = CreateProject(directory.Path, "Data.csproj");
        var runner = new SequenceProcessRunner(
            Result.Success(new ProcessResult(0, "not-json", string.Empty)));
        var service = new EfCoreToolService(runner);

        var result = await service.ListMigrationsAsync(new EfCoreProjectRequest(projectPath));

        Assert.Multiple(() =>
        {
            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo("dotnet.ef.migrations.output.invalid"));
        });
    }

    [Test]
    public async Task MissingProjectDoesNotExecuteProcess()
    {
        using var directory = new TemporaryDirectory();
        var runner = new SequenceProcessRunner(SuccessProcessResult());
        var service = new EfCoreToolService(runner);

        var result = await service.DetectAsync(Path.Combine(directory.Path, "Missing.csproj"));

        Assert.Multiple(() =>
        {
            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo("dotnet.ef.project.unavailable"));
            Assert.That(runner.Requests, Is.Empty);
        });
    }

    private static Result<ProcessResult> SuccessProcessResult() =>
        Result.Success(new ProcessResult(0, string.Empty, string.Empty));

    private static string CreateProject(string directory, string fileName)
    {
        var path = Path.Combine(directory, fileName);
        File.WriteAllText(path, "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        return path;
    }

    private sealed class SequenceProcessRunner(params Result<ProcessResult>[] results) : IProcessRunner
    {
        private readonly Queue<Result<ProcessResult>> _results = new(results);

        public List<ProcessRequest> Requests { get; } = [];

        public Task<Result<ProcessResult>> RunAsync(
            ProcessRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(request);
            return Task.FromResult(_results.Dequeue());
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                $"toren-ef-tests-{Guid.NewGuid():N}");
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
