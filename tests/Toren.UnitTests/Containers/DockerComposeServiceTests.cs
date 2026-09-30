using NUnit.Framework;
using Toren.Containers.Models;
using Toren.Containers.Services;
using Toren.Core.Execution.Contracts;
using Toren.Core.Execution.Models;
using Toren.Core.Results;

namespace Toren.UnitTests.Containers;

[TestFixture]
public sealed class DockerComposeServiceTests
{
    [Test]
    public async Task DetectReturnsVersionWhenComposeIsAvailable()
    {
        var runner = new SequenceProcessRunner(
            Result.Success(new ProcessResult(0, "2.35.1\n", string.Empty)));
        var service = new DockerComposeService(runner);

        var result = await service.DetectAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value!.IsAvailable, Is.True);
            Assert.That(result.Value.Version, Is.EqualTo("2.35.1"));
            Assert.That(string.Join('\u001F', runner.Requests[0].Arguments), Is.EqualTo("compose\u001Fversion\u001F--short"));
        });
    }

    [Test]
    public async Task UpDownAndBuildUseComposeFileAndProjectName()
    {
        using var directory = new TemporaryDirectory();
        var composeFile = CreateComposeFile(directory.Path);
        var runner = new SequenceProcessRunner(Success(), Success(), Success());
        var service = new DockerComposeService(runner);
        var request = new DockerComposeRequest(composeFile, "toren-tests");

        Assert.That((await service.UpAsync(request)).IsSuccess, Is.True);
        Assert.That((await service.DownAsync(request)).IsSuccess, Is.True);
        Assert.That((await service.BuildAsync(request)).IsSuccess, Is.True);

        Assert.Multiple(() =>
        {
            Assert.That(
                string.Join('\u001F', runner.Requests[0].Arguments),
                Is.EqualTo($"compose\u001F--file\u001F{composeFile}\u001F--project-name\u001Ftoren-tests\u001Fup\u001F--detach"));
            Assert.That(
                string.Join('\u001F', runner.Requests[1].Arguments),
                Is.EqualTo($"compose\u001F--file\u001F{composeFile}\u001F--project-name\u001Ftoren-tests\u001Fdown"));
            Assert.That(
                string.Join('\u001F', runner.Requests[2].Arguments),
                Is.EqualTo($"compose\u001F--file\u001F{composeFile}\u001F--project-name\u001Ftoren-tests\u001Fbuild"));
            Assert.That(runner.Requests.All(requestItem => requestItem.WorkingDirectory == directory.Path), Is.True);
        });
    }

    [Test]
    public async Task UpCanRunAttached()
    {
        using var directory = new TemporaryDirectory();
        var composeFile = CreateComposeFile(directory.Path);
        var runner = new SequenceProcessRunner(Success());
        var service = new DockerComposeService(runner);

        var result = await service.UpAsync(new DockerComposeRequest(composeFile, Detached: false));

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(runner.Requests[0].Arguments, Does.Not.Contain("--detach"));
        });
    }

    [Test]
    public async Task LogsUseBoundedTailAndReturnStdout()
    {
        using var directory = new TemporaryDirectory();
        var composeFile = CreateComposeFile(directory.Path);
        var runner = new SequenceProcessRunner(
            Result.Success(new ProcessResult(0, "web-1 | listening\n", string.Empty)));
        var service = new DockerComposeService(runner);

        var result = await service.GetLogsAsync(new DockerComposeLogsRequest(composeFile, Tail: 50));

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Value, Does.Contain("listening"));
            Assert.That(
                string.Join('\u001F', runner.Requests[0].Arguments),
                Is.EqualTo($"compose\u001F--file\u001F{composeFile}\u001Flogs\u001F--no-color\u001F--tail\u001F50"));
        });
    }

    [Test]
    public async Task MissingComposeFileDoesNotExecuteProcess()
    {
        using var directory = new TemporaryDirectory();
        var runner = new SequenceProcessRunner(Success());
        var service = new DockerComposeService(runner);

        var result = await service.BuildAsync(
            new DockerComposeRequest(Path.Combine(directory.Path, "compose.yaml")));

        Assert.Multiple(() =>
        {
            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo("docker.compose.file.unavailable"));
            Assert.That(runner.Requests, Is.Empty);
        });
    }

    private static Result<ProcessResult> Success() =>
        Result.Success(new ProcessResult(0, string.Empty, string.Empty));

    private static string CreateComposeFile(string directory)
    {
        var path = Path.Combine(directory, "compose.yaml");
        File.WriteAllText(path, "services:\n  web:\n    image: nginx\n");
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
                $"toren-compose-tests-{Guid.NewGuid():N}");
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
