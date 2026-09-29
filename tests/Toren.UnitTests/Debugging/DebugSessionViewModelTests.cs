using NUnit.Framework;
using Toren.App.Debugging.Services;
using Toren.App.Debugging.ViewModels;
using Toren.Core.Results;
using Toren.Debugging.Contracts;
using Toren.Debugging.Models;

namespace Toren.UnitTests.Debugging;

[TestFixture]
public sealed class DebugSessionViewModelTests
{
    [Test]
    public async Task StopRefreshPopulatesStackLocalsAndWatches()
    {
        var session = new StubSession();
        await using var coordinator = new DebugSessionCoordinator(new StubSessionService(session));
        var viewModel = new DebugSessionViewModel(coordinator)
        {
            WatchExpression = "count + 1",
        };
        await coordinator.AttachAsync(session.ProcessId);
        await viewModel.AddWatchAsync();

        var stopped = await viewModel.WaitForStopAndRefreshAsync();

        Assert.Multiple(() =>
        {
            Assert.That(stopped.IsSuccess, Is.True);
            Assert.That(viewModel.IsStopped, Is.True);
            Assert.That(viewModel.StackFrames, Has.Count.EqualTo(1));
            Assert.That(viewModel.SelectedFrame?.Id, Is.EqualTo(101));
            Assert.That(viewModel.Locals, Has.Count.EqualTo(1));
            Assert.That(viewModel.Locals[0].Name, Is.EqualTo("count"));
            Assert.That(viewModel.Locals[0].Value, Is.EqualTo("5"));
            Assert.That(viewModel.Watches, Has.Count.EqualTo(1));
            Assert.That(viewModel.Watches[0].Value, Is.EqualTo("6"));
            Assert.That(viewModel.CanContinue, Is.True);
        });
    }

    [Test]
    public async Task BreakpointConditionIsForwardedAndResolved()
    {
        var session = new StubSession();
        await using var coordinator = new DebugSessionCoordinator(new StubSessionService(session));
        var viewModel = new DebugSessionViewModel(coordinator);
        await coordinator.AttachAsync(session.ProcessId);

        await viewModel.AddBreakpointAsync("/workspace/Program.cs", 12, "count > 3");

        Assert.Multiple(() =>
        {
            Assert.That(session.RequestedBreakpoints, Has.Count.EqualTo(1));
            Assert.That(session.RequestedBreakpoints[0].Line, Is.EqualTo(12));
            Assert.That(session.RequestedBreakpoints[0].Condition, Is.EqualTo("count > 3"));
            Assert.That(viewModel.Breakpoints, Has.Count.EqualTo(1));
            Assert.That(viewModel.Breakpoints[0].IsVerified, Is.True);
        });
    }

    [Test]
    public async Task ContinueClearsPausedPresentationState()
    {
        var session = new StubSession();
        await using var coordinator = new DebugSessionCoordinator(new StubSessionService(session));
        var viewModel = new DebugSessionViewModel(coordinator);
        await coordinator.AttachAsync(session.ProcessId);
        await viewModel.WaitForStopAndRefreshAsync();

        await viewModel.ContinueAsync();

        Assert.Multiple(() =>
        {
            Assert.That(session.ContinueCount, Is.EqualTo(1));
            Assert.That(viewModel.StackFrames, Is.Empty);
            Assert.That(viewModel.Locals, Is.Empty);
            Assert.That(viewModel.IsStopped, Is.False);
        });
    }

    [Test]
    public async Task ConsoleEvaluationUsesSelectedFrame()
    {
        var session = new StubSession();
        await using var coordinator = new DebugSessionCoordinator(new StubSessionService(session));
        var viewModel = new DebugSessionViewModel(coordinator);
        await coordinator.AttachAsync(session.ProcessId);
        await viewModel.WaitForStopAndRefreshAsync();
        viewModel.ConsoleExpression = "count * 2";

        await viewModel.EvaluateConsoleAsync();

        Assert.Multiple(() =>
        {
            Assert.That(session.LastEvaluationFrameId, Is.EqualTo(101));
            Assert.That(session.LastEvaluationContext, Is.EqualTo(DebugEvaluationContext.Repl));
            Assert.That(viewModel.ConsoleLines, Has.Count.EqualTo(2));
            Assert.That(viewModel.ConsoleLines[1].Text, Is.EqualTo("6"));
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

    private sealed class StubSession : IDebugSession
    {
        public int ProcessId => 4242;

        public int ContinueCount { get; private set; }

        public int? LastEvaluationFrameId { get; private set; }

        public DebugEvaluationContext LastEvaluationContext { get; private set; }

        public List<DebugSourceBreakpoint> RequestedBreakpoints { get; } = [];

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
            RequestedBreakpoints.Clear();
            RequestedBreakpoints.AddRange(breakpoints);
            return Task.FromResult(Result.Success<IReadOnlyList<DebugBreakpoint>>(
                breakpoints
                    .Select(static breakpoint => new DebugBreakpoint(
                        1,
                        true,
                        breakpoint.Line))
                    .ToArray()));
        }

        public Task<Result<IReadOnlyList<DebugStackFrame>>> GetStackTraceAsync(
            int threadId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Result.Success<IReadOnlyList<DebugStackFrame>>(
                [new DebugStackFrame(101, "Program.Main()", "/workspace/Program.cs", 12, 5)]));
        }

        public Task<Result<IReadOnlyList<DebugScope>>> GetScopesAsync(
            int frameId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Result.Success<IReadOnlyList<DebugScope>>(
                [new DebugScope("Locals", 200, false)]));
        }

        public Task<Result<IReadOnlyList<DebugVariable>>> GetVariablesAsync(
            int variablesReference,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Result.Success<IReadOnlyList<DebugVariable>>(
                [new DebugVariable("count", "5", "int", 0)]));
        }

        public Task<Result<DebugEvaluationResult>> EvaluateAsync(
            string expression,
            int? frameId = null,
            DebugEvaluationContext context = DebugEvaluationContext.Watch,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastEvaluationFrameId = frameId;
            LastEvaluationContext = context;
            return Task.FromResult(Result.Success(new DebugEvaluationResult("6", "int", 0)));
        }

        public Task<Result<bool>> ContinueAsync(int threadId, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ContinueCount++;
            return Task.FromResult(Result.Success(true));
        }

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

        public Task<Result<bool>> DisconnectAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success(true));

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
