using Toren.Core.Execution.Models;
using Toren.Core.Results;
using Toren.DotNet.Testing.Contracts;
using Toren.DotNet.Testing.Models;

namespace Toren.App.Debugging.Services;

public sealed class DebuggerAttachingTestDebugService(
    IDotNetTestDebugService testDebugService,
    DebugSessionCoordinator debugSessionCoordinator) : IDotNetTestDebugService
{
    private readonly IDotNetTestDebugService _testDebugService = testDebugService
        ?? throw new ArgumentNullException(nameof(testDebugService));
    private readonly DebugSessionCoordinator _debugSessionCoordinator = debugSessionCoordinator
        ?? throw new ArgumentNullException(nameof(debugSessionCoordinator));

    public async Task<Result<IDotNetTestDebugSession>> StartAsync(
        DotNetTestRunRequest request,
        Action<ProcessOutputLine> onOutput,
        CancellationToken cancellationToken = default)
    {
        var started = await _testDebugService
            .StartAsync(request, onOutput, cancellationToken)
            .ConfigureAwait(false);
        if (started.IsFailure)
        {
            return started;
        }

        var testSession = started.Value!;
        var attached = await _debugSessionCoordinator
            .AttachAsync(testSession.ProcessId, cancellationToken)
            .ConfigureAwait(false);
        if (attached.IsFailure)
        {
            testSession.Terminate();
            await testSession.DisposeAsync().ConfigureAwait(false);
            return Result.Failure<IDotNetTestDebugSession>(attached.Error);
        }

        return Result.Success<IDotNetTestDebugSession>(
            new AttachedTestDebugSession(testSession, _debugSessionCoordinator));
    }

    private sealed class AttachedTestDebugSession(
        IDotNetTestDebugSession testSession,
        DebugSessionCoordinator debugSessionCoordinator) : IDotNetTestDebugSession
    {
        private readonly IDotNetTestDebugSession _testSession = testSession;
        private readonly DebugSessionCoordinator _debugSessionCoordinator = debugSessionCoordinator;
        private int _disposeState;

        public int ProcessId => _testSession.ProcessId;

        public Task<Result<ProcessResult>> Completion => _testSession.Completion;

        public void Terminate() => _testSession.Terminate();

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposeState, 1) != 0)
            {
                return;
            }

            try
            {
                await _debugSessionCoordinator.DisconnectAsync().ConfigureAwait(false);
            }
            finally
            {
                await _testSession.DisposeAsync().ConfigureAwait(false);
            }
        }
    }
}
