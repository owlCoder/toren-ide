using System.Diagnostics;
using Toren.Core.IO;
using Toren.Core.Results;
using Toren.Workspaces.Contracts;
using Toren.Workspaces.Errors;
using Toren.Workspaces.Models;

namespace Toren.Workspaces.Services;

/// <summary>
/// Owns the evaluated snapshot of the open workspace. Overlapping consumers share one
/// evaluation, and a finished snapshot is reused only while the files it was evaluated from
/// are unchanged. Nothing is persisted; switching workspaces replaces the snapshot.
/// </summary>
public sealed class WorkspaceProjectGraphService(
    WorkspaceProjectGraphEvaluator evaluator,
    IProjectEvaluationInputStampProvider stampProvider)
    : IWorkspaceProjectGraphService, IWorkspaceProjectCatalog, IDisposable
{
    private readonly WorkspaceProjectGraphEvaluator _evaluator = evaluator
        ?? throw new ArgumentNullException(nameof(evaluator));
    private readonly IProjectEvaluationInputStampProvider _stampProvider = stampProvider
        ?? throw new ArgumentNullException(nameof(stampProvider));
    private readonly Lock _gate = new();
    private Snapshot? _current;
    private bool _disposed;

    public Task<Result<WorkspaceProjectGraph>> LoadAsync(
        WorkspaceDescriptor workspace,
        CancellationToken cancellationToken = default) =>
        GetAsync(workspace, includeCompilerInputs: true, cancellationToken);

    public async Task<Result<IReadOnlyList<WorkspaceProject>>> GetProjectsAsync(
        WorkspaceDescriptor workspace,
        CancellationToken cancellationToken = default)
    {
        var graph = await GetAsync(workspace, includeCompilerInputs: false, cancellationToken).ConfigureAwait(false);
        return graph.IsSuccess
            ? Result.Success(graph.Value.Projects)
            : Result.Failure<IReadOnlyList<WorkspaceProject>>(graph.Error);
    }

    public void Dispose()
    {
        Snapshot? abandoned;
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            abandoned = _current;
            _current = null;
            if (abandoned is not null)
            {
                abandoned.Retired = true;
            }
        }

        abandoned?.Cancellation.Cancel();
    }

    private async Task<Result<WorkspaceProjectGraph>> GetAsync(
        WorkspaceDescriptor workspace,
        bool includeCompilerInputs,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        cancellationToken.ThrowIfCancellationRequested();

        var requested = Stopwatch.GetTimestamp();
        var workspaceStamp = await _stampProvider
            .GetWorkspaceStampAsync(workspace, cancellationToken)
            .ConfigureAwait(false);

        while (true)
        {
            if (Acquire(workspace, workspaceStamp) is not { } snapshot)
            {
                return Abandoned();
            }

            var reusable = false;
            try
            {
                // Waiting is cancellable per caller; the evaluation itself belongs to the workspace.
                var evaluated = await snapshot.Evaluated.WaitAsync(cancellationToken).ConfigureAwait(false);
                if (!evaluated.Graph.IsSuccess)
                {
                    return evaluated.Graph;
                }

                if (!await IsCurrentAsync(snapshot, evaluated, requested, cancellationToken).ConfigureAwait(false))
                {
                    continue;
                }

                if (!includeCompilerInputs)
                {
                    reusable = true;
                    return evaluated.Graph;
                }

                var graph = await StartCompilerInputs(snapshot, evaluated.Graph.Value)
                    .WaitAsync(cancellationToken)
                    .ConfigureAwait(false);
                reusable = graph.IsSuccess;
                return graph;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Only this caller stopped waiting; a healthy snapshot stays available to others.
                reusable = !snapshot.HasFailedStage;
                throw;
            }
            finally
            {
                Release(snapshot, retire: !reusable);
            }
        }
    }

    /// <summary>
    /// A snapshot that began evaluating before this request must still match the inputs on
    /// disk; one that began afterwards has, by construction, read inputs at least that new.
    /// </summary>
    private async Task<bool> IsCurrentAsync(
        Snapshot snapshot,
        EvaluatedStage evaluated,
        long requested,
        CancellationToken cancellationToken)
    {
        if (snapshot.Started >= requested)
        {
            return true;
        }

        if (evaluated.ChangedDuringEvaluation)
        {
            return false;
        }

        var projectStamp = await _stampProvider
            .GetProjectStampAsync(evaluated.Graph.Value!.Projects, cancellationToken)
            .ConfigureAwait(false);
        return string.Equals(projectStamp.Value, evaluated.ProjectStamp, StringComparison.Ordinal);
    }

    /// <summary>Returns the snapshot to wait on, or <see langword="null"/> once disposed.</summary>
    private Snapshot? Acquire(WorkspaceDescriptor workspace, string workspaceStamp)
    {
        Snapshot? replaced = null;
        Snapshot snapshot;
        lock (_gate)
        {
            if (_disposed)
            {
                return null;
            }

            if (_current is { } current && current.Matches(workspace, workspaceStamp))
            {
                current.Waiters++;
                return current;
            }

            if (_current is { } stale)
            {
                stale.Retired = true;
                replaced = stale.Waiters == 0 ? stale : null;
            }

            snapshot = new Snapshot(workspace, workspaceStamp) { Waiters = 1 };
            // Started on the thread pool so no evaluation work runs under the lock or on the
            // caller's thread, which is the UI thread for most consumers.
            snapshot.Evaluated = Task.Run(() => EvaluateAsync(snapshot));
            _current = snapshot;
        }

        replaced?.Cancellation.Cancel();
        return snapshot;
    }

    /// <summary>
    /// Compiler inputs are resolved on first demand. A snapshot that was replaced meanwhile
    /// still serves the callers holding it: its inputs were current when they asked.
    /// </summary>
    private Task<Result<WorkspaceProjectGraph>> StartCompilerInputs(
        Snapshot snapshot,
        WorkspaceProjectGraph evaluated)
    {
        lock (_gate)
        {
            return snapshot.CompilerInputs ??= Task.Run(() => ResolveCompilerInputsAsync(snapshot, evaluated));
        }
    }

    private void Release(Snapshot snapshot, bool retire)
    {
        var cancel = false;
        lock (_gate)
        {
            snapshot.Waiters--;
            if (retire)
            {
                snapshot.Retired = true;
                if (ReferenceEquals(_current, snapshot))
                {
                    _current = null;
                }
            }

            cancel = snapshot.Retired && snapshot.Waiters == 0;
        }

        if (cancel)
        {
            snapshot.Cancellation.Cancel();
        }
    }

    private async Task<EvaluatedStage> EvaluateAsync(Snapshot snapshot)
    {
        var cancellationToken = snapshot.Cancellation.Token;
        try
        {
            var graph = await _evaluator.EvaluateAsync(snapshot.Workspace, cancellationToken).ConfigureAwait(false);
            if (!graph.IsSuccess)
            {
                return new EvaluatedStage(graph, string.Empty, ChangedDuringEvaluation: false);
            }

            var projectStamp = await _stampProvider
                .GetProjectStampAsync(graph.Value.Projects, cancellationToken)
                .ConfigureAwait(false);
            // These inputs are only discoverable through evaluation, so their state cannot be
            // captured beforehand. A write after the evaluation began may or may not have been
            // read; such a snapshot serves the callers already waiting and is then replaced.
            return new EvaluatedStage(
                graph,
                projectStamp.Value,
                ChangedDuringEvaluation: projectStamp.LastWriteTimeUtc >= snapshot.StartedUtc);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new EvaluatedStage(Abandoned(), string.Empty, ChangedDuringEvaluation: false);
        }
    }

    private async Task<Result<WorkspaceProjectGraph>> ResolveCompilerInputsAsync(
        Snapshot snapshot,
        WorkspaceProjectGraph evaluated)
    {
        var cancellationToken = snapshot.Cancellation.Token;
        try
        {
            return await _evaluator.ResolveCompilerInputsAsync(evaluated, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return Abandoned();
        }
    }

    private static Result<WorkspaceProjectGraph> Abandoned() =>
        Result.Failure<WorkspaceProjectGraph>(WorkspaceProjectGraphErrors.EvaluationAbandoned);

    private sealed class Snapshot(WorkspaceDescriptor workspace, string workspaceStamp)
    {
        public WorkspaceDescriptor Workspace { get; } = workspace;

        /// <summary>Monotonic start, for ordering against requests.</summary>
        public long Started { get; } = Stopwatch.GetTimestamp();

        /// <summary>Wall-clock start, for comparison with file modification times.</summary>
        public DateTime StartedUtc { get; } = DateTime.UtcNow;

        // Never disposed: it is cancelled from outside the lock after the evaluation may
        // already have finished, and it owns no timer or wait handle.
        public CancellationTokenSource Cancellation { get; } = new();

        // Assigned before the snapshot is published.
        public Task<EvaluatedStage> Evaluated { get; set; } = null!;

        public Task<Result<WorkspaceProjectGraph>>? CompilerInputs { get; set; }

        public int Waiters { get; set; }

        public bool Retired { get; set; }

        public bool HasFailedStage =>
            Evaluated is { IsCompleted: true, IsCompletedSuccessfully: false }
            || CompilerInputs is { IsCompleted: true, IsCompletedSuccessfully: false };

        public bool Matches(WorkspaceDescriptor other, string otherStamp) =>
            Workspace.Kind == other.Kind
            && string.Equals(Workspace.Path, other.Path, FileSystemPath.Comparison)
            && string.Equals(workspaceStamp, otherStamp, StringComparison.Ordinal);
    }

    private sealed record EvaluatedStage(
        Result<WorkspaceProjectGraph> Graph,
        string ProjectStamp,
        bool ChangedDuringEvaluation);
}
