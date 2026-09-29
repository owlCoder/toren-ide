using NUnit.Framework;
using Toren.App.Debugging.Services;
using Toren.Core.Execution.Models;
using Toren.Core.Results;
using Toren.Debugging.Contracts;
using Toren.Debugging.Models;
using Toren.DotNet.Testing.Contracts;
using Toren.DotNet.Testing.Models;

namespace Toren.UnitTests.Debugging;

[TestFixture]
public sealed class DebuggerAttachingTestDebugServiceTests
{
    private static readonly int[] ExpectedAttachedProcessIds = [4321];

    [Test]
    public async Task StartAttachesDebuggerToPublishedTestHostAndDisconnectsOnDispose()
    {
        var testSession = new StubTestDebugSession(4321);
        var debugSession = new StubDebugSession(4321);
        var sessionService = new StubDebugSessionService(Result.Success<IDebugSession>(debugSession));
        await using var coordinator = new DebugSessionCoordinator(sessionService);
        var service = new DebuggerAttachingTestDebugService(
            new StubTestDebugService(Result.Success<IDotNetTestDebugSession>(testSession)),
            coordinator);

        var started = await service.StartAsync(
            new DotNetTestRunRequest("Sample.Tests.csproj", "Sample.Tests.Test"),
            static _ => { });

        Assert.That(started.IsSuccess, Is.True);
        Assert.Multiple(() =>
        {
            Assert.That(sessionService.ProcessIds, Is.EqualTo(ExpectedAttachedProcessIds));
            Assert.That(coordinator.IsAttached, Is.True);
            Assert.That(coordinator.ProcessId, Is.EqualTo(4321));
        });

        await started.Value!.DisposeAsync();

        Assert.Multiple(() =>
        {
            Assert.That(debugSession.DisconnectCount, Is.EqualTo(1));
            Assert.That(debugSession.DisposeCount, Is.EqualTo(1));
            Assert.That(testSession.DisposeCount, Is.EqualTo(1));
            Assert.That(coordinator.IsAttached, Is.False);
        });
    }

    [Test]
    public async Task AttachFailureTerminatesWaitingTestHost()
    {
        var testSession = new StubTestDebugSession(9876);
        var error = OperationError.Create("debug.adapter.not-found", "Debugger missing.");
        var sessionService = new StubDebugSessionService(Result.Failure<IDebugSession>(error));
        await using var coordinator = new DebugSessionCoordinator(sessionService);
        var service = new DebuggerAttachingTestDebugService(
            new StubTestDebugService(Result.Success<IDotNetTestDebugSession>(testSession)),
            coordinator);

        var started = await service.StartAsync(
            new DotNetTestRunRequest("Sample.Tests.csproj", "Sample.Tests.Test"),
            static _ => { });

        Assert.Multiple(() =>
        {
            Assert.That(started.IsFailure, Is.True);
            Assert.That(started.Error.Code, Is.EqualTo(error.Code));
            Assert.That(testSession.TerminateCount, Is.EqualTo(1));
            Assert.That(testSession.DisposeCount, Is.EqualTo(1));
            Assert.That(coordinator.IsAttached, Is.False);
        });
    }

    private sealed class StubTestDebugService(Result<IDotNetTestDebugSession> result) : IDotNetTestDebugService
    {
        public Task<Result<IDotNetTestDebugSession>> StartAsync(
            DotNetTestRunRequest request,
            Action<ProcessOutputLine> onOutput,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(result);
        }
    }

    private sealed class StubTestDebugSession(int processId) : IDotNetTestDebugSession
    {
        public int ProcessId { get; } = processId;

        public Task<Result<ProcessResult>> Completion { get; } =
            Task.FromResult(Result.Success(new ProcessResult(0, string.Empty, string.Empty)));

        public int TerminateCount { get; private set; }

        public int DisposeCount { get; private set; }

        public void Terminate() => TerminateCount++;

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class StubDebugSessionService(Result<IDebugSession> result) : IDebugSessionService
    {
        public List<int> ProcessIds { get; } = [];

        public Task<Result<IDebugSession>> AttachAsync(
            int processId,
            string? workingDirectory = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ProcessIds.Add(processId);
            return Task.FromResult(result);
        }
    }

    private sealed class StubDebugSession(int processId) : IDebugSession
    {
        public int ProcessId { get; } = processId;

        public int DisconnectCount { get; private set; }

        public int DisposeCount { get; private set; }

        public Task<Result<DebugStopInfo>> WaitForStopAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success(new DebugStopInfo(1, "breakpoint")));

        public Task<Result<IReadOnlyList<DebugBreakpoint>>> SetBreakpointsAsync(
            string sourcePath,
            IReadOnlyList<DebugSourceBreakpoint> breakpoints,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success<IReadOnlyList<DebugBreakpoint>>([]));

        public Task<Result<IReadOnlyList<DebugStackFrame>>> GetStackTraceAsync(
            int threadId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success<IReadOnlyList<DebugStackFrame>>([]));

        public Task<Result<IReadOnlyList<DebugScope>>> GetScopesAsync(
            int frameId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success<IReadOnlyList<DebugScope>>([]));

        public Task<Result<IReadOnlyList<DebugVariable>>> GetVariablesAsync(
            int variablesReference,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success<IReadOnlyList<DebugVariable>>([]));

        public Task<Result<DebugEvaluationResult>> EvaluateAsync(
            string expression,
            int? frameId = null,
            DebugEvaluationContext context = DebugEvaluationContext.Watch,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success(new DebugEvaluationResult(string.Empty, null, 0)));

        public Task<Result<bool>> ContinueAsync(int threadId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success(true));

        public Task<Result<bool>> PauseAsync(int threadId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success(true));

        public Task<Result<bool>> StepOverAsync(int threadId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success(true));

        public Task<Result<bool>> StepIntoAsync(int threadId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success(true));

        public Task<Result<bool>> StepOutAsync(int threadId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success(true));

        public Task<Result<bool>> RunToCursorAsync(
            int threadId,
            string sourcePath,
            int line,
            int? column = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success(true));

        public Task<Result<bool>> RestartAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success(true));

        public Task<Result<bool>> StopAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success(true));

        public Task<Result<bool>> DisconnectAsync(CancellationToken cancellationToken = default)
        {
            DisconnectCount++;
            return Task.FromResult(Result.Success(true));
        }

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return ValueTask.CompletedTask;
        }
    }
}
