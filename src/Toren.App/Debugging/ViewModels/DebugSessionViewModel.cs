using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Toren.App.Debugging.Services;
using Toren.Core.Results;
using Toren.Debugging.Models;

namespace Toren.App.Debugging.ViewModels;

public sealed partial class DebugSessionViewModel(DebugSessionCoordinator coordinator) : ObservableObject
{
    private static readonly StringComparer PathComparer =
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;

    private readonly DebugSessionCoordinator _coordinator = coordinator
        ?? throw new ArgumentNullException(nameof(coordinator));
    private readonly Dictionary<string, List<BreakpointDefinition>> _breakpointsBySource =
        new(PathComparer);
    private int _lastThreadId;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanEvaluate))]
    private DebugStackFrame? _selectedFrame;

    [ObservableProperty]
    private string _statusText = "No debug session.";

    [ObservableProperty]
    private string _watchExpression = string.Empty;

    [ObservableProperty]
    private string _consoleExpression = string.Empty;

    [ObservableProperty]
    private string _breakpointCondition = string.Empty;

    public ObservableCollection<DebugStackFrame> StackFrames { get; } = new();

    public ObservableCollection<DebugLocalItemViewModel> Locals { get; } = new();

    public ObservableCollection<DebugWatchItemViewModel> Watches { get; } = new();

    public ObservableCollection<DebugBreakpointItemViewModel> Breakpoints { get; } = new();

    public ObservableCollection<DebugConsoleLineViewModel> ConsoleLines { get; } = new();

    public bool IsAttached => _coordinator.IsAttached;

    public bool IsStopped => _coordinator.StoppedThreadId.HasValue;

    public bool CanContinue => IsStopped && _lastThreadId > 0;

    public bool CanPause => IsAttached && !IsStopped && _lastThreadId > 0;

    public bool CanStep => CanContinue;

    public bool CanRestart => IsAttached;

    public bool CanStop => IsAttached;

    public bool CanRunToCursor => CanContinue;

    public bool CanEvaluate => IsStopped && SelectedFrame is not null;

    public bool HasStackFrames => StackFrames.Count > 0;

    public bool HasLocals => Locals.Count > 0;

    public bool HasWatches => Watches.Count > 0;

    public bool HasBreakpoints => Breakpoints.Count > 0;

    public bool HasConsoleLines => ConsoleLines.Count > 0;

    public void RefreshSessionState()
    {
        if (!IsAttached)
        {
            ClearPausedState();
            _lastThreadId = 0;
            StatusText = "No debug session.";
        }
        else if (!IsStopped)
        {
            StatusText = $"Debugging process {_coordinator.ProcessId}.";
        }

        NotifyStateProperties();
    }

    public async Task<Result<DebugStopInfo>> WaitForStopAndRefreshAsync(
        CancellationToken cancellationToken = default)
    {
        var stopped = await _coordinator.WaitForStopAsync(cancellationToken).ConfigureAwait(true);
        if (stopped.IsFailure)
        {
            return stopped;
        }

        _lastThreadId = stopped.Value!.ThreadId;
        await RefreshStoppedStateAsync(stopped.Value.ThreadId, cancellationToken).ConfigureAwait(true);
        return stopped;
    }

    public async Task RefreshStoppedStateAsync(
        int threadId,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(threadId);
        _lastThreadId = threadId;
        StackFrames.Clear();
        Locals.Clear();
        SelectedFrame = null;

        var stack = await _coordinator.GetStackTraceAsync(threadId, cancellationToken).ConfigureAwait(true);
        if (stack.IsFailure)
        {
            StatusText = stack.Error.Message;
            NotifyCollectionProperties();
            NotifyStateProperties();
            return;
        }

        foreach (var frame in stack.Value)
        {
            StackFrames.Add(frame);
        }

        if (StackFrames.Count > 0)
        {
            await SelectFrameAsync(StackFrames[0], cancellationToken).ConfigureAwait(true);
        }

        await RefreshWatchesAsync(cancellationToken).ConfigureAwait(true);
        StatusText = StackFrames.Count > 0
            ? $"Paused: {StackFrames[0].Name}"
            : $"Paused on thread {threadId}.";
        NotifyCollectionProperties();
        NotifyStateProperties();
    }

    public async Task SelectFrameAsync(
        DebugStackFrame frame,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(frame);
        SelectedFrame = frame;
        Locals.Clear();

        var scopes = await _coordinator.GetScopesAsync(frame.Id, cancellationToken).ConfigureAwait(true);
        if (scopes.IsFailure)
        {
            StatusText = scopes.Error.Message;
            OnPropertyChanged(nameof(HasLocals));
            return;
        }

        foreach (var scope in scopes.Value)
        {
            if (scope.VariablesReference == 0)
            {
                continue;
            }

            var variables = await _coordinator
                .GetVariablesAsync(scope.VariablesReference, cancellationToken)
                .ConfigureAwait(true);
            if (variables.IsFailure)
            {
                StatusText = variables.Error.Message;
                continue;
            }

            foreach (var variable in variables.Value)
            {
                Locals.Add(new DebugLocalItemViewModel(
                    scope.Name,
                    variable.Name,
                    variable.Value,
                    variable.Type,
                    variable.VariablesReference));
            }
        }

        OnPropertyChanged(nameof(HasLocals));
        OnPropertyChanged(nameof(CanEvaluate));
    }

    public async Task AddWatchAsync(CancellationToken cancellationToken = default)
    {
        var expression = WatchExpression.Trim();
        if (expression.Length == 0)
        {
            return;
        }

        var existing = Watches.FirstOrDefault(item =>
            item.Expression.Equals(expression, StringComparison.Ordinal));
        if (existing is null)
        {
            existing = new DebugWatchItemViewModel(expression);
            Watches.Add(existing);
        }

        WatchExpression = string.Empty;
        await RefreshWatchAsync(existing, cancellationToken).ConfigureAwait(true);
        OnPropertyChanged(nameof(HasWatches));
    }

    public void RemoveWatch(DebugWatchItemViewModel item)
    {
        ArgumentNullException.ThrowIfNull(item);
        Watches.Remove(item);
        OnPropertyChanged(nameof(HasWatches));
    }

    public async Task EvaluateConsoleAsync(CancellationToken cancellationToken = default)
    {
        var expression = ConsoleExpression.Trim();
        if (expression.Length == 0)
        {
            return;
        }

        ConsoleExpression = string.Empty;
        ConsoleLines.Add(new DebugConsoleLineViewModel($"> {expression}"));
        if (!CanEvaluate)
        {
            ConsoleLines.Add(new DebugConsoleLineViewModel(
                "Debugger must be paused on a stack frame before evaluating expressions.",
                IsError: true));
            OnPropertyChanged(nameof(HasConsoleLines));
            return;
        }

        var result = await _coordinator.EvaluateAsync(
            expression,
            SelectedFrame!.Id,
            DebugEvaluationContext.Repl,
            cancellationToken).ConfigureAwait(true);
        ConsoleLines.Add(result.IsSuccess
            ? new DebugConsoleLineViewModel(result.Value!.Value)
            : new DebugConsoleLineViewModel(result.Error.Message, IsError: true));
        OnPropertyChanged(nameof(HasConsoleLines));
    }

    public void ClearConsole()
    {
        ConsoleLines.Clear();
        OnPropertyChanged(nameof(HasConsoleLines));
    }

    public async Task AddBreakpointAsync(
        string sourcePath,
        int line,
        string? condition = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(line);
        var fullPath = Path.GetFullPath(sourcePath);
        if (!_breakpointsBySource.TryGetValue(fullPath, out var definitions))
        {
            definitions = [];
            _breakpointsBySource.Add(fullPath, definitions);
        }

        definitions.RemoveAll(item => item.Line == line);
        definitions.Add(new BreakpointDefinition(line, NormalizeCondition(condition)));
        definitions.Sort(static (left, right) => left.Line.CompareTo(right.Line));
        await ApplySourceBreakpointsAsync(fullPath, cancellationToken).ConfigureAwait(true);
    }

    public async Task RemoveBreakpointAsync(
        DebugBreakpointItemViewModel item,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (!_breakpointsBySource.TryGetValue(item.SourcePath, out var definitions))
        {
            return;
        }

        definitions.RemoveAll(definition => definition.Line == item.Line);
        if (definitions.Count == 0)
        {
            _breakpointsBySource.Remove(item.SourcePath);
        }

        await ApplySourceBreakpointsAsync(item.SourcePath, cancellationToken).ConfigureAwait(true);
    }

    public async Task ApplyAllBreakpointsAsync(CancellationToken cancellationToken = default)
    {
        foreach (var sourcePath in _breakpointsBySource.Keys.ToArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            await ApplySourceBreakpointsAsync(sourcePath, cancellationToken).ConfigureAwait(true);
        }
    }

    public async Task ContinueAsync(CancellationToken cancellationToken = default)
    {
        if (!CanContinue)
        {
            return;
        }

        await ResumeAsync(
            _coordinator.ContinueAsync(_lastThreadId, cancellationToken),
            "Continuing…").ConfigureAwait(true);
    }

    public async Task PauseAsync(CancellationToken cancellationToken = default)
    {
        if (!CanPause)
        {
            return;
        }

        var paused = await _coordinator.PauseAsync(_lastThreadId, cancellationToken).ConfigureAwait(true);
        StatusText = paused.IsSuccess ? "Pause requested…" : paused.Error.Message;
    }

    public async Task StepOverAsync(CancellationToken cancellationToken = default) =>
        await StepAsync(_coordinator.StepOverAsync, "Stepping over…", cancellationToken).ConfigureAwait(true);

    public async Task StepIntoAsync(CancellationToken cancellationToken = default) =>
        await StepAsync(_coordinator.StepIntoAsync, "Stepping into…", cancellationToken).ConfigureAwait(true);

    public async Task StepOutAsync(CancellationToken cancellationToken = default) =>
        await StepAsync(_coordinator.StepOutAsync, "Stepping out…", cancellationToken).ConfigureAwait(true);

    public async Task RestartAsync(CancellationToken cancellationToken = default)
    {
        if (!CanRestart)
        {
            return;
        }

        await ResumeAsync(_coordinator.RestartAsync(cancellationToken), "Restarting…").ConfigureAwait(true);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (!CanStop)
        {
            return;
        }

        var stopped = await _coordinator.StopAsync(cancellationToken).ConfigureAwait(true);
        StatusText = stopped.IsSuccess ? "Debug session stopped." : stopped.Error.Message;
        RefreshSessionState();
    }

    public async Task RunToCursorAsync(
        string sourcePath,
        int line,
        int? column = null,
        CancellationToken cancellationToken = default)
    {
        if (!CanRunToCursor)
        {
            return;
        }

        var result = _coordinator.RunToCursorAsync(
            _lastThreadId,
            sourcePath,
            line,
            column,
            cancellationToken);
        await ResumeAsync(result, "Running to cursor…").ConfigureAwait(true);
    }

    public void ResetSession()
    {
        ClearPausedState();
        _lastThreadId = 0;
        StatusText = "No debug session.";
        foreach (var watch in Watches)
        {
            watch.Value = string.Empty;
            watch.Type = null;
            watch.ErrorMessage = null;
        }

        NotifyStateProperties();
        NotifyCollectionProperties();
    }

    private async Task StepAsync(
        Func<int, CancellationToken, Task<Result<bool>>> action,
        string statusText,
        CancellationToken cancellationToken)
    {
        if (!CanStep)
        {
            return;
        }

        await ResumeAsync(action(_lastThreadId, cancellationToken), statusText).ConfigureAwait(true);
    }

    private async Task ResumeAsync(Task<Result<bool>> operation, string statusText)
    {
        var result = await operation.ConfigureAwait(true);
        if (result.IsSuccess)
        {
            ClearPausedState();
            StatusText = statusText;
        }
        else
        {
            StatusText = result.Error.Message;
        }

        NotifyStateProperties();
        NotifyCollectionProperties();
    }

    private async Task RefreshWatchesAsync(CancellationToken cancellationToken)
    {
        foreach (var watch in Watches)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await RefreshWatchAsync(watch, cancellationToken).ConfigureAwait(true);
        }
    }

    private async Task RefreshWatchAsync(
        DebugWatchItemViewModel watch,
        CancellationToken cancellationToken)
    {
        if (!CanEvaluate)
        {
            watch.Value = string.Empty;
            watch.Type = null;
            watch.ErrorMessage = null;
            return;
        }

        var result = await _coordinator.EvaluateAsync(
            watch.Expression,
            SelectedFrame!.Id,
            DebugEvaluationContext.Watch,
            cancellationToken).ConfigureAwait(true);
        if (result.IsSuccess)
        {
            watch.Value = result.Value!.Value;
            watch.Type = result.Value.Type;
            watch.ErrorMessage = null;
        }
        else
        {
            watch.Value = string.Empty;
            watch.Type = null;
            watch.ErrorMessage = result.Error.Message;
        }
    }

    private async Task ApplySourceBreakpointsAsync(
        string sourcePath,
        CancellationToken cancellationToken)
    {
        _breakpointsBySource.TryGetValue(sourcePath, out var definitions);
        definitions ??= [];
        var requested = definitions
            .Select(static definition => new DebugSourceBreakpoint(definition.Line, definition.Condition))
            .ToArray();

        RemoveDisplayedBreakpoints(sourcePath);
        if (!IsAttached)
        {
            foreach (var definition in definitions)
            {
                Breakpoints.Add(new DebugBreakpointItemViewModel(
                    sourcePath,
                    definition.Line,
                    definition.Condition,
                    IsVerified: false,
                    Message: "Pending debug session."));
            }

            OnPropertyChanged(nameof(HasBreakpoints));
            return;
        }

        var result = await _coordinator
            .SetBreakpointsAsync(sourcePath, requested, cancellationToken)
            .ConfigureAwait(true);
        if (result.IsFailure)
        {
            StatusText = result.Error.Message;
            foreach (var definition in definitions)
            {
                Breakpoints.Add(new DebugBreakpointItemViewModel(
                    sourcePath,
                    definition.Line,
                    definition.Condition,
                    IsVerified: false,
                    result.Error.Message));
            }

            OnPropertyChanged(nameof(HasBreakpoints));
            return;
        }

        for (var index = 0; index < definitions.Count; index++)
        {
            var definition = definitions[index];
            var resolved = index < result.Value!.Count ? result.Value[index] : null;
            Breakpoints.Add(new DebugBreakpointItemViewModel(
                sourcePath,
                resolved?.Line > 0 ? resolved.Line : definition.Line,
                definition.Condition,
                resolved?.IsVerified ?? false,
                resolved?.Message));
        }

        OnPropertyChanged(nameof(HasBreakpoints));
    }

    private void RemoveDisplayedBreakpoints(string sourcePath)
    {
        for (var index = Breakpoints.Count - 1; index >= 0; index--)
        {
            if (PathComparer.Equals(Breakpoints[index].SourcePath, sourcePath))
            {
                Breakpoints.RemoveAt(index);
            }
        }
    }

    private void ClearPausedState()
    {
        StackFrames.Clear();
        Locals.Clear();
        SelectedFrame = null;
    }

    private void NotifyStateProperties()
    {
        OnPropertyChanged(nameof(IsAttached));
        OnPropertyChanged(nameof(IsStopped));
        OnPropertyChanged(nameof(CanContinue));
        OnPropertyChanged(nameof(CanPause));
        OnPropertyChanged(nameof(CanStep));
        OnPropertyChanged(nameof(CanRestart));
        OnPropertyChanged(nameof(CanStop));
        OnPropertyChanged(nameof(CanRunToCursor));
        OnPropertyChanged(nameof(CanEvaluate));
    }

    private void NotifyCollectionProperties()
    {
        OnPropertyChanged(nameof(HasStackFrames));
        OnPropertyChanged(nameof(HasLocals));
        OnPropertyChanged(nameof(HasWatches));
        OnPropertyChanged(nameof(HasBreakpoints));
        OnPropertyChanged(nameof(HasConsoleLines));
    }

    private static string? NormalizeCondition(string? condition)
    {
        var normalized = condition?.Trim();
        return string.IsNullOrEmpty(normalized) ? null : normalized;
    }

    private sealed record BreakpointDefinition(int Line, string? Condition);
}
