using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Toren.App.Execution.Contracts;
using Toren.App.Execution.ViewModels;
using Toren.App.ViewModels;
using Toren.App.Views.Output;
using Toren.DotNet.Execution.Models;
using Toren.Workspaces.Contracts;
using Toren.Workspaces.Models;

namespace Toren.App.Execution.Services;

internal sealed class WorkspaceExecutionController
{
    private readonly Window _window;
    private readonly MainWindowViewModel _shell;
    private readonly IWorkspaceClassifier _workspaceClassifier;
    private readonly IWorkspaceExecutionTargetService _targetService;
    private readonly WorkspaceExecutionViewModel _execution;
    private CancellationTokenSource? _targetLoadCancellation;
    private bool _detached;

    private WorkspaceExecutionController(
        Window window,
        MainWindowViewModel shell,
        IWorkspaceClassifier workspaceClassifier,
        IWorkspaceExecutionTargetService targetService,
        WorkspaceExecutionViewModel execution)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
        _shell = shell ?? throw new ArgumentNullException(nameof(shell));
        _workspaceClassifier = workspaceClassifier
            ?? throw new ArgumentNullException(nameof(workspaceClassifier));
        _targetService = targetService ?? throw new ArgumentNullException(nameof(targetService));
        _execution = execution ?? throw new ArgumentNullException(nameof(execution));

        InstallOutputPanel();
        SynchronizeWorkspace();
        _shell.PropertyChanged += Shell_OnPropertyChanged;
        _shell.Explorer.PropertyChanged += Explorer_OnPropertyChanged;
        _window.KeyDown += Window_OnKeyDown;
        _window.Closed += Window_OnClosed;
    }

    public static void Attach(
        Window window,
        MainWindowViewModel shell,
        IWorkspaceClassifier workspaceClassifier,
        IWorkspaceExecutionTargetService targetService,
        WorkspaceExecutionViewModel execution)
    {
        _ = new WorkspaceExecutionController(
            window,
            shell,
            workspaceClassifier,
            targetService,
            execution);
    }

    private void InstallOutputPanel()
    {
        var toolTabs = _window.GetLogicalDescendants()
            .OfType<TabControl>()
            .FirstOrDefault(control => control.Classes.Contains("tool-tabs"));
        var outputTab = toolTabs?.Items.OfType<TabItem>().Skip(1).FirstOrDefault();
        if (outputTab is null)
        {
            return;
        }

        outputTab.Content = new OutputPanel
        {
            DataContext = _execution,
        };
    }

    private void Shell_OnPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName == nameof(MainWindowViewModel.WorkspacePath))
        {
            SynchronizeWorkspace();
        }
    }

    private void Explorer_OnPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName == nameof(ExplorerViewModel.IsWorkspaceOpen))
        {
            SynchronizeWorkspace();
        }
    }

    private void SynchronizeWorkspace()
    {
        CancelTargetLoad();
        var workspace = ResolveWorkspace();
        _execution.SetWorkspace(workspace);
        if (workspace is null)
        {
            return;
        }

        var cancellation = new CancellationTokenSource();
        _targetLoadCancellation = cancellation;
        _ = LoadRunTargetsAsync(workspace, cancellation);
    }

    private WorkspaceDescriptor? ResolveWorkspace()
    {
        if (!_shell.Explorer.IsWorkspaceOpen)
        {
            return null;
        }

        var path = _shell.WorkspacePath;
        if (Directory.Exists(path))
        {
            return _workspaceClassifier.ClassifyDirectory(path);
        }

        return _workspaceClassifier.TryClassifyFile(path, out var workspace)
            ? workspace
            : null;
    }

    private async Task LoadRunTargetsAsync(
        WorkspaceDescriptor workspace,
        CancellationTokenSource cancellation)
    {
        try
        {
            var result = await _targetService
                .GetTargetsAsync(workspace, cancellation.Token)
                .ConfigureAwait(true);
            if (!ReferenceEquals(_targetLoadCancellation, cancellation))
            {
                return;
            }

            if (result.IsSuccess)
            {
                _execution.SetRunTargets(result.Value);
            }
            else
            {
                _execution.SetRunTargetLoadError(result.Error.Message);
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // A newer workspace selection superseded this load.
        }
        finally
        {
            if (ReferenceEquals(_targetLoadCancellation, cancellation))
            {
                _targetLoadCancellation = null;
            }

            cancellation.Dispose();
        }
    }

    private async void Window_OnKeyDown(object? sender, KeyEventArgs eventArgs)
    {
        if (eventArgs.Key == Key.F5 && eventArgs.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            if (_execution.CanCancel)
            {
                eventArgs.Handled = true;
                _execution.Cancel();
            }

            return;
        }

        if (eventArgs.Key == Key.F5 && eventArgs.KeyModifiers == KeyModifiers.None)
        {
            if (_execution.CanRun)
            {
                eventArgs.Handled = true;
                await _execution.ExecuteAsync(DotNetCommandKind.Run).ConfigureAwait(true);
            }

            return;
        }

        var commandModifier = eventArgs.KeyModifiers.HasFlag(KeyModifiers.Control)
            || eventArgs.KeyModifiers.HasFlag(KeyModifiers.Meta);
        if (!commandModifier
            || !eventArgs.KeyModifiers.HasFlag(KeyModifiers.Shift)
            || eventArgs.Key != Key.B
            || !_execution.CanExecute)
        {
            return;
        }

        eventArgs.Handled = true;
        await _execution.ExecuteAsync(DotNetCommandKind.Build).ConfigureAwait(true);
    }

    private void Window_OnClosed(object? sender, EventArgs eventArgs)
    {
        Detach();
    }

    private void CancelTargetLoad()
    {
        _targetLoadCancellation?.Cancel();
        _targetLoadCancellation = null;
    }

    private void Detach()
    {
        if (_detached)
        {
            return;
        }

        _detached = true;
        CancelTargetLoad();
        _shell.PropertyChanged -= Shell_OnPropertyChanged;
        _shell.Explorer.PropertyChanged -= Explorer_OnPropertyChanged;
        _window.KeyDown -= Window_OnKeyDown;
        _window.Closed -= Window_OnClosed;
        _execution.Dispose();
    }
}
