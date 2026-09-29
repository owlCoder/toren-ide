using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Toren.App.Execution.Models;
using Toren.DotNet.Execution.Contracts;
using Toren.DotNet.Execution.Models;
using Toren.Workspaces.Models;

namespace Toren.App.Execution.ViewModels;

public sealed partial class WorkspaceExecutionViewModel(IDotNetCommandService commandService)
    : ObservableObject, IDisposable
{
    private readonly IDotNetCommandService _commandService = commandService
        ?? throw new ArgumentNullException(nameof(commandService));
    private WorkspaceDescriptor? _workspace;
    private CancellationTokenSource? _executionCancellation;
    private bool _disposed;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Configuration))]
    private int _configurationIndex;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanExecute))]
    [NotifyPropertyChangedFor(nameof(CanRun))]
    [NotifyPropertyChangedFor(nameof(CanCancel))]
    private bool _isRunning;

    [ObservableProperty]
    private DotNetCommandKind? _currentCommandKind;

    [ObservableProperty]
    private string _statusText = "No .NET command has run yet.";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanRun))]
    [NotifyPropertyChangedFor(nameof(SelectedRunTarget))]
    private int _selectedRunTargetIndex = -1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TargetFramework))]
    private int _selectedTargetFrameworkIndex = -1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LaunchProfile))]
    [NotifyPropertyChangedFor(nameof(SelectedLaunchProfile))]
    private int _selectedLaunchProfileIndex = -1;

    public event EventHandler<WorkspaceCommandCompletedEventArgs>? CommandCompleted;

    public ObservableCollection<ExecutionOutputLineViewModel> OutputLines { get; } = new();

    public ObservableCollection<WorkspaceExecutionTarget> RunTargets { get; } = new();

    public ObservableCollection<string> TargetFrameworks { get; } = new();

    public ObservableCollection<DotNetLaunchProfile> LaunchProfiles { get; } = new();

    public string Configuration => ConfigurationIndex == 1 ? "Release" : "Debug";

    public WorkspaceExecutionTarget? SelectedRunTarget =>
        SelectedRunTargetIndex >= 0 && SelectedRunTargetIndex < RunTargets.Count
            ? RunTargets[SelectedRunTargetIndex]
            : null;

    public string? TargetFramework =>
        SelectedTargetFrameworkIndex >= 0 && SelectedTargetFrameworkIndex < TargetFrameworks.Count
            ? TargetFrameworks[SelectedTargetFrameworkIndex]
            : null;

    public DotNetLaunchProfile? SelectedLaunchProfile =>
        SelectedLaunchProfileIndex >= 0 && SelectedLaunchProfileIndex < LaunchProfiles.Count
            ? LaunchProfiles[SelectedLaunchProfileIndex]
            : null;

    public string? LaunchProfile => SelectedLaunchProfile?.Name;

    public bool CanExecute => _workspace is not null && !IsRunning;

    public bool CanRun => SelectedRunTarget is not null && !IsRunning;

    public bool CanCancel => IsRunning;

    public bool HasRunTargets => RunTargets.Count > 0;

    public bool HasTargetFrameworks => TargetFrameworks.Count > 0;

    public bool HasLaunchProfiles => LaunchProfiles.Count > 0;

    public bool HasOutput => OutputLines.Count > 0;

    public bool IsOutputEmpty => !HasOutput;

    public void SetWorkspace(WorkspaceDescriptor? workspace)
    {
        if (Equals(_workspace, workspace))
        {
            return;
        }

        Cancel();
        _workspace = workspace;
        ClearRunTargets();
        ClearOutput();
        StatusText = workspace is null
            ? "Open a .NET workspace to run commands."
            : $"Loading runnable projects for {workspace.DisplayName}…";
        OnPropertyChanged(nameof(CanExecute));
        OnPropertyChanged(nameof(CanRun));
    }

    public void SetRunTargets(IReadOnlyList<WorkspaceExecutionTarget> targets)
    {
        ArgumentNullException.ThrowIfNull(targets);
        ClearRunTargets();
        foreach (var target in targets)
        {
            RunTargets.Add(target);
        }

        SelectedRunTargetIndex = RunTargets.Count > 0 ? 0 : -1;
        StatusText = _workspace is null
            ? "Open a .NET workspace to run commands."
            : RunTargets.Count > 0
                ? $"Ready to run .NET commands for {_workspace.DisplayName}."
                : $"No runnable projects were found in {_workspace.DisplayName}.";
        OnPropertyChanged(nameof(HasRunTargets));
        OnPropertyChanged(nameof(CanRun));
    }

    public void SetRunTargetLoadError(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        ClearRunTargets();
        StatusText = message;
    }

    public void SetLaunchProfiles(IReadOnlyList<DotNetLaunchProfile> profiles)
    {
        ArgumentNullException.ThrowIfNull(profiles);
        LaunchProfiles.Clear();
        foreach (var profile in profiles)
        {
            LaunchProfiles.Add(profile);
        }

        SelectedLaunchProfileIndex = LaunchProfiles.Count > 0 ? 0 : -1;
        OnPropertyChanged(nameof(HasLaunchProfiles));
        OnPropertyChanged(nameof(LaunchProfile));
        OnPropertyChanged(nameof(SelectedLaunchProfile));
    }

    public void SetLaunchProfileLoadError(string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        SetLaunchProfiles([]);
        StatusText = $"Launch profiles unavailable: {message}";
    }

    public async Task ExecuteAsync(
        DotNetCommandKind kind,
        CancellationToken cancellationToken = default)
    {
        var canStart = kind == DotNetCommandKind.Run ? CanRun : CanExecute;
        if (_disposed || _workspace is null || !canStart)
        {
            return;
        }

        using var executionCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _executionCancellation = executionCancellation;
        IsRunning = true;
        CurrentCommandKind = kind;
        var workspace = _workspace;
        var displayName = kind.ToString();
        var synchronizationContext = SynchronizationContext.Current;
        AddOutput($"[{displayName}] {workspace.DisplayName}", ExecutionOutputLineKind.Command);
        StatusText = $"{displayName} running…";

        try
        {
            var request = CreateRequest(kind, workspace);
            var isStreaming = _commandService is IStreamingDotNetCommandService;
            var result = isStreaming
                ? await ((IStreamingDotNetCommandService)_commandService)
                    .ExecuteStreamingAsync(
                        request,
                        line => ReportStreamingOutput(line, synchronizationContext),
                        executionCancellation.Token)
                    .ConfigureAwait(true)
                : await _commandService
                    .ExecuteAsync(request, executionCancellation.Token)
                    .ConfigureAwait(true);

            if (!result.IsSuccess)
            {
                AddOutput(result.Error.Message, ExecutionOutputLineKind.StandardError);
                StatusText = $"{displayName} could not start.";
                AddOutput(StatusText, ExecutionOutputLineKind.Status);
                return;
            }

            if (!isStreaming)
            {
                AddLines(result.Value.StandardOutput, ExecutionOutputLineKind.StandardOutput);
                AddLines(result.Value.StandardError, ExecutionOutputLineKind.StandardError);
            }

            StatusText = result.Value.Succeeded
                ? $"{displayName} succeeded."
                : $"{displayName} failed with exit code {result.Value.ExitCode}.";
            AddOutput(StatusText, ExecutionOutputLineKind.Status);
            CommandCompleted?.Invoke(
                this,
                new WorkspaceCommandCompletedEventArgs(workspace, result.Value));
        }
        catch (OperationCanceledException) when (executionCancellation.IsCancellationRequested)
        {
            StatusText = $"{displayName} canceled.";
            AddOutput(StatusText, ExecutionOutputLineKind.Status);
        }
        finally
        {
            if (ReferenceEquals(_executionCancellation, executionCancellation))
            {
                _executionCancellation = null;
            }

            CurrentCommandKind = null;
            IsRunning = false;
        }
    }

    public void Cancel()
    {
        _executionCancellation?.Cancel();
    }

    public void ClearOutput()
    {
        if (OutputLines.Count == 0)
        {
            return;
        }

        OutputLines.Clear();
        OnPropertyChanged(nameof(HasOutput));
        OnPropertyChanged(nameof(IsOutputEmpty));
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Cancel();
    }

    partial void OnSelectedRunTargetIndexChanged(int value)
    {
        RefreshTargetFrameworks();
        SetLaunchProfiles([]);
        OnPropertyChanged(nameof(CanRun));
    }

    private DotNetCommandRequest CreateRequest(DotNetCommandKind kind, WorkspaceDescriptor workspace)
    {
        if (kind == DotNetCommandKind.Run && SelectedRunTarget is { } runTarget)
        {
            var projectPath = Path.GetFullPath(runTarget.ProjectPath);
            return new DotNetCommandRequest(
                kind,
                Path.GetDirectoryName(projectPath) ?? Directory.GetCurrentDirectory(),
                projectPath,
                Configuration,
                TargetFramework,
                LaunchProfile: LaunchProfile);
        }

        var fullPath = Path.GetFullPath(workspace.Path);
        var workingDirectory = workspace.Kind == WorkspaceKind.Folder
            ? fullPath
            : Path.GetDirectoryName(fullPath) ?? Directory.GetCurrentDirectory();
        var targetPath = workspace.Kind == WorkspaceKind.Folder ? null : fullPath;
        var configuration = kind == DotNetCommandKind.Restore ? null : Configuration;

        return new DotNetCommandRequest(
            kind,
            workingDirectory,
            targetPath,
            configuration);
    }

    private void RefreshTargetFrameworks()
    {
        TargetFrameworks.Clear();
        if (SelectedRunTarget is { } target)
        {
            foreach (var targetFramework in target.TargetFrameworks
                         .Where(static value => !string.IsNullOrWhiteSpace(value))
                         .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                TargetFrameworks.Add(targetFramework);
            }
        }

        SelectedTargetFrameworkIndex = TargetFrameworks.Count > 0 ? 0 : -1;
        OnPropertyChanged(nameof(HasTargetFrameworks));
        OnPropertyChanged(nameof(TargetFramework));
    }

    private void ClearRunTargets()
    {
        RunTargets.Clear();
        TargetFrameworks.Clear();
        LaunchProfiles.Clear();
        SelectedRunTargetIndex = -1;
        SelectedTargetFrameworkIndex = -1;
        SelectedLaunchProfileIndex = -1;
        OnPropertyChanged(nameof(HasRunTargets));
        OnPropertyChanged(nameof(HasTargetFrameworks));
        OnPropertyChanged(nameof(HasLaunchProfiles));
        OnPropertyChanged(nameof(CanRun));
        OnPropertyChanged(nameof(TargetFramework));
        OnPropertyChanged(nameof(LaunchProfile));
        OnPropertyChanged(nameof(SelectedLaunchProfile));
    }

    private void ReportStreamingOutput(
        DotNetCommandOutputLine line,
        SynchronizationContext? synchronizationContext)
    {
        var kind = line.Channel == DotNetCommandOutputChannel.StandardError
            ? ExecutionOutputLineKind.StandardError
            : ExecutionOutputLineKind.StandardOutput;
        if (synchronizationContext is null
            || ReferenceEquals(SynchronizationContext.Current, synchronizationContext))
        {
            AddOutput(line.Text, kind);
            return;
        }

        synchronizationContext.Send(
            static state =>
            {
                var report = (StreamingOutputReport)state!;
                report.ViewModel.AddOutput(report.Text, report.Kind);
            },
            new StreamingOutputReport(this, line.Text, kind));
    }

    private void AddLines(string output, ExecutionOutputLineKind kind)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return;
        }

        foreach (var line in output.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'))
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                AddOutput(line, kind);
            }
        }
    }

    private void AddOutput(string text, ExecutionOutputLineKind kind)
    {
        OutputLines.Add(new ExecutionOutputLineViewModel(text, kind));
        OnPropertyChanged(nameof(HasOutput));
        OnPropertyChanged(nameof(IsOutputEmpty));
    }

    private sealed record StreamingOutputReport(
        WorkspaceExecutionViewModel ViewModel,
        string Text,
        ExecutionOutputLineKind Kind);
}
