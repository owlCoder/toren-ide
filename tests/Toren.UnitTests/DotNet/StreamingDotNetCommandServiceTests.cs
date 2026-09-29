using NUnit.Framework;
using Toren.Core.Execution.Contracts;
using Toren.Core.Execution.Models;
using Toren.Core.Results;
using Toren.DotNet.Execution.Models;
using Toren.DotNet.Execution.Services;

namespace Toren.UnitTests.DotNet;

[TestFixture]
public sealed class StreamingDotNetCommandServiceTests
{
    [Test]
    public async Task StreamingExecutionMapsProcessOutputAndPreservesFinalResult()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var runner = new FakeStreamingProcessRunner();
            var service = new DotNetCommandService(runner);
            var lines = new List<DotNetCommandOutputLine>();

            var result = await service.ExecuteStreamingAsync(
                new DotNetCommandRequest(DotNetCommandKind.Build, directory),
                lines.Add);

            Assert.That(result.IsSuccess, Is.True);
            var commandResult = result.Value!;
            Assert.Multiple(() =>
            {
                Assert.That(commandResult.ExitCode, Is.EqualTo(1));
                Assert.That(commandResult.StandardOutput, Is.EqualTo("first"));
                Assert.That(commandResult.StandardError, Is.EqualTo("second"));
                Assert.That(lines, Has.Count.EqualTo(2));
                Assert.That(lines[0].Channel, Is.EqualTo(DotNetCommandOutputChannel.StandardOutput));
                Assert.That(lines[0].Text, Is.EqualTo("first"));
                Assert.That(lines[1].Channel, Is.EqualTo(DotNetCommandOutputChannel.StandardError));
                Assert.That(lines[1].Text, Is.EqualTo("second"));
            });
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public async Task StreamingFallsBackToBufferedRunnerWhenStreamingIsUnavailable()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var runner = new BufferedProcessRunner();
            var service = new DotNetCommandService(runner);
            var lines = new List<DotNetCommandOutputLine>();

            var result = await service.ExecuteStreamingAsync(
                new DotNetCommandRequest(DotNetCommandKind.Restore, directory),
                lines.Add);

            Assert.Multiple(() =>
            {
                Assert.That(result.IsSuccess, Is.True);
                Assert.That(lines, Has.Count.EqualTo(2));
                Assert.That(lines[0].Text, Is.EqualTo("restore output"));
                Assert.That(lines[1].Text, Is.EqualTo("restore warning"));
            });
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"toren-streaming-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }

    private sealed class FakeStreamingProcessRunner : IStreamingProcessRunner
    {
        public Task<Result<ProcessResult>> RunAsync(
            ProcessRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success(new ProcessResult(1, "first", "second")));

        public Task<Result<ProcessResult>> RunStreamingAsync(
            ProcessRequest request,
            Action<ProcessOutputLine> onOutput,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            onOutput(new ProcessOutputLine(ProcessOutputChannel.StandardOutput, "first"));
            onOutput(new ProcessOutputLine(ProcessOutputChannel.StandardError, "second"));
            return Task.FromResult(Result.Success(new ProcessResult(1, "first", "second")));
        }
    }

    private sealed class BufferedProcessRunner : IProcessRunner
    {
        public Task<Result<ProcessResult>> RunAsync(
            ProcessRequest request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(
                Result.Success(new ProcessResult(0, "restore output", "restore warning")));
        }
    }
}
