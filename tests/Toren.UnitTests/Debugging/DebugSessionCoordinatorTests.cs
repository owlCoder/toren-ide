using NUnit.Framework;
using Toren.App.Debugging.Services;
using Toren.Core.Results;
using Toren.Debugging.Contracts;
using Toren.Debugging.Models;

namespace Toren.UnitTests.Debugging;

[TestFixture]
public sealed class DebugSessionCoordinatorTests
{
    private static readonly string[] ExpectedControlCommands =
        ["continue", "pause", "next", "stepIn", "stepOut"];

    [Test]
    public async Task StopAndControlCommandsFlowThroughCoordinator()
    {
        var session = new RecordingSession(2468);
        await using var coordinator = new DebugSessionCoordinator(new StubSessionService(session));

        var attached = await coordinator.AttachAsync(2468);
        var stopped = await coordinator.WaitForStopAsync();
        var breakpoints = await coordinator.SetBreakpointsAsync(
            "/workspace/Program.cs",
            [new DebugSourceBreakpoint(12, "count > 3")]);
        var continued = await coordinator.ContinueAsync(7);
        var paused = await coordinator.PauseAsync(7);
        var stepOver = await coordinator.StepOverAsync(7);
        var stepInto = await coordinator.StepIntoAsync(7);
        var stepOut = await coordinator.StepOutAsync(7);

        Assert.Multiple(() =>
        {
            Assert.That(attached.IsSuccess, Is.True);
            Assert.That(stopped.IsSuccess, Is.True);
            Assert.That(stopped.Value!.ThreadId, Is.EqualTo(7));
            Assert.That(breakpoints.IsSuccess, Is.True);
            Assert.That(continued.IsSuccess, Is.True);
            Assert.That(paused.IsSuccess, Is.True);
            Assert.That(stepOver.IsSuccess, Is.True);
            Assert.That(stepInto.IsSuccess, Is.True);
            Assert.That(stepOut.IsSuccess, Is.True);
            Assert.That(session.SourcePath, Is.EqualTo("/workspace/Program.cs"));
            Assert.That(session.LastThreadId, Is.EqualTo(7));
            Assert.That(session.ControlCommands, Is.EqualTo(ExpectedControlCommands));
            Assert.That(coordinator.StoppedThreadId, Is.Null);
        });
    }

    [Test]
    public async Task WaitingForStopTracksStoppedThreadUntilExecutionResumes()
    {
        var session = new RecordingSession(1357);
        await using var coordinator = new DebugSessionCoordinator(new StubSessionService(session));
        await coordinator.AttachAsync(1357);

        var stopped = await coordinator.WaitForStopAsync();

        Assert.Multiple(() =>
        {
            Assert.That(stopped.IsSuccess, Is.True);
            Assert.That(coordinator.StoppedThreadId, Is.EqualTo(7));
        });

        await coordinator.StepOverAsync(7);
        Assert.That(coordinator.StoppedThreadId, Is.Null);
    }

    [Test]
    public async Task CommandsWithoutAttachedSessionReturnRecoverableFailure()
    {
        await using var coordinator = new DebugSessionCoordinator(new StubSessionService(new RecordingSession(1)));

        var result = await coordinator.ContinueAsync(1);

        Assert.Multiple(() =>
        {
            Assert.That(result.IsFailure, Is.True);
            Assert.That(result.Error.Code, Is.EqualTo("debug.session.not-active"));
        });
    }

    private sealed class StubSessionService(IDebugSession session) : IDebugSessionService
    {
        public Task<Result<IDebugSession>> AttachAsync(
            int processId,
            string? workingDirectory = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Result.Success(session));
        }
    }

    private sealed class RecordingSession(int processId) : IDebugSession
    {
        public int ProcessId { get; } = processId;

        public string? SourcePath { get; private set; }

        public int LastThreadId { get; private set; }

        public List<string> ControlCommands { get; } = [];

        public Task<Result<DebugStopInfo>> WaitForStopAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Result.Success(new DebugStopInfo(7, "breakpoint")));
        }

        public Task<Result<IReadOnlyList<DebugBreakpoint>>> SetBreakpointsAsync(
            string sourcePath,
            IReadOnlyList<DebugSourceBreakpoint> breakpoints,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SourcePath = sourcePath;
            return Task.FromResult(Result.Success<IReadOnlyList<DebugBreakpoint>>(
                breakpoints.Select(static breakpoint => new DebugBreakpoint(1, true, breakpoint.Line)).ToArray()));
        }

        public Task<Result<bool>> ContinueAsync(int threadId, CancellationToken cancellationToken = default) =>
            RecordAsync("continue", threadId, cancellationToken);

        public Task<Result<bool>> PauseAsync(int threadId, CancellationToken cancellationToken = default) =>
            RecordAsync("pause", threadId, cancellationToken);

        public Task<Result<bool>> StepOverAsync(int threadId, CancellationToken cancellationToken = default) =>
            RecordAsync("next", threadId, cancellationToken);

        public Task<Result<bool>> StepIntoAsync(int threadId, CancellationToken cancellationToken = default) =>
            RecordAsync("stepIn", threadId, cancellationToken);

        public Task<Result<bool>> StepOutAsync(int threadId, CancellationToken cancellationToken = default) =>
            RecordAsync("stepOut", threadId, cancellationToken);

        public Task<Result<bool>> DisconnectAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Result.Success(true));
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        private Task<Result<bool>> RecordAsync(
            string command,
            int threadId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastThreadId = threadId;
            ControlCommands.Add(command);
            return Task.FromResult(Result.Success(true));
        }
    }
}
