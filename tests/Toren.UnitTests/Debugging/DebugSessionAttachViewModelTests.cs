using NUnit.Framework;
using Toren.App.Debugging.Services;
using Toren.App.Debugging.ViewModels;
using Toren.Core.Results;
using Toren.Debugging.Contracts;
using Toren.Debugging.Models;

namespace Toren.UnitTests.Debugging;

[TestFixture]
public sealed class DebugSessionAttachViewModelTests
{
    private static readonly int[] ExpectedProcessIds = [4321];

    [Test]
    public async Task AttachUsesValidatedProcessIdAndUpdatesSessionState()
    {
        var service = new RecordingSessionService();
        await using var coordinator = new DebugSessionCoordinator(service);
        var viewModel = new DebugSessionViewModel(coordinator)
        {
            AttachProcessId = "4321",
        };

        await viewModel.AttachAsync();

        Assert.Multiple(() =>
        {
            Assert.That(service.ProcessIds, Is.EqualTo(ExpectedProcessIds));
            Assert.That(coordinator.IsAttached, Is.True);
            Assert.That(coordinator.ProcessId, Is.EqualTo(4321));
            Assert.That(viewModel.AttachProcessId, Is.Empty);
            Assert.That(viewModel.StatusText, Is.EqualTo("Attached to process 4321."));
        });
    }

    [TestCase("")]
    [TestCase("abc")]
    [TestCase("0")]
    [TestCase("-5")]
    public async Task AttachRejectsInvalidProcessIdWithoutCallingService(string processId)
    {
        var service = new RecordingSessionService();
        await using var coordinator = new DebugSessionCoordinator(service);
        var viewModel = new DebugSessionViewModel(coordinator)
        {
            AttachProcessId = processId,
        };

        await viewModel.AttachAsync();

        Assert.Multiple(() =>
        {
            Assert.That(service.ProcessIds, Is.Empty);
            Assert.That(coordinator.IsAttached, Is.False);
            Assert.That(viewModel.StatusText, Is.EqualTo("Enter a valid process id."));
        });
    }

    private sealed class RecordingSessionService : IDebugSessionService
    {
        public List<int> ProcessIds { get; } = [];

        public Task<Result<IDebugSession>> AttachAsync(
            int processId,
            string? workingDirectory = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ProcessIds.Add(processId);
            return Task.FromResult(Result.Success<IDebugSession>(new StubSession(processId)));
        }
    }

    private sealed class StubSession(int processId) : IDebugSession
    {
        public int ProcessId { get; } = processId;

        public Task<Result<DebugStopInfo>> WaitForStopAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<IReadOnlyList<DebugBreakpoint>>> SetBreakpointsAsync(
            string sourcePath,
            IReadOnlyList<DebugSourceBreakpoint> breakpoints,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<IReadOnlyList<DebugStackFrame>>> GetStackTraceAsync(
            int threadId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<IReadOnlyList<DebugScope>>> GetScopesAsync(
            int frameId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<IReadOnlyList<DebugVariable>>> GetVariablesAsync(
            int variablesReference,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<DebugEvaluationResult>> EvaluateAsync(
            string expression,
            int? frameId = null,
            DebugEvaluationContext context = DebugEvaluationContext.Watch,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<bool>> ContinueAsync(
            int threadId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<bool>> PauseAsync(
            int threadId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<bool>> StepOverAsync(
            int threadId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<bool>> StepIntoAsync(
            int threadId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<bool>> StepOutAsync(
            int threadId,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<bool>> RunToCursorAsync(
            int threadId,
            string sourcePath,
            int line,
            int? column = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<bool>> RestartAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<bool>> StopAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<bool>> DisconnectAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success(true));

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
