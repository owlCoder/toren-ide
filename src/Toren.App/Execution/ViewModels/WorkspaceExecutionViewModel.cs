using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
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
    [NotifyPropertyChangedFor(nameof(CanCancel))]
    private bool _isRunning;

    [ObservableProperty]
    private string _statusText = "No .NET command has run yet.";

    public ObservableCollection<ExecutionOutputLineViewModel> OutputLines { get; } = new();

    public string Configuration => ConfigurationIndex == 1 ? "Release" : "Debug";

    public bool CanExecute => _workspace is not null && !IsRunning;

    public bool CanCancel => IsRunning;

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
        ClearOutput();
        StatusText = workspace is null
            ? "Open a .NET workspace to run commands."
            : $"Ready to run .NET commands for {workspace.DisplayName}.";
        OnPropertyChanged(nameof(CanExecute));
    }

    public async Task ExecuteAsync(
        DotNetCommandKind kind,
        CancellationToken cancellationToken = default)
    {
        if (_disposed || _workspace is null || IsRunning)
        {
            return;
        }

        using var executionCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _executionCancellation = executionCancellation;
        IsRunning = true;
        var workspace = _workspace;
        var displayName = kind.ToString();
        AddOutput($"[{displayName}] {workspace.DisplayName}", ExecutionOutputLineKind.Command);
        StatusText = $"{displayName} running…";

        try
        {
            var result = await _commandService
                .ExecuteAsync(CreateRequest(kind, workspace), executionCancellation.Token)
                .ConfigureAwait(true);

            if (!result.IsSuccess)
            {
                AddOutput(result.Error.Message, ExecutionOutputLineKind.StandardError);
                StatusText = $"{displayName} could not start.";
                AddOutput(StatusText, ExecutionOutputLineKind.Status);
                return;
            }

            AddLines(result.Value.StandardOutput, ExecutionOutputLineKind.StandardOutput);
            AddLines(result.Value.StandardError, ExecutionOutputLineKind.StandardError);

            StatusText = result.Value.Succeeded
                ? $"{displayName} succeeded."
                : $"{displayName} failed with exit code {result.Value.ExitCode}.";
            AddOutput(StatusText, ExecutionOutputLineKind.Status);
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

    private DotNetCommandRequest CreateRequest(DotNetCommandKind kind, WorkspaceDescriptor workspace)
    {
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
}
