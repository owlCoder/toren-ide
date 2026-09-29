using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Toren.App.Execution.ViewModels;
using Toren.App.ViewModels;
using Toren.App.Views.Output;
using Toren.DotNet.Execution.Models;
using Toren.Workspaces.Contracts;

namespace Toren.App.Execution.Services;

internal sealed class WorkspaceExecutionController
{
    private readonly Window _window;
    private readonly MainWindowViewModel _shell;
    private readonly IWorkspaceClassifier _workspaceClassifier;
    private readonly WorkspaceExecutionViewModel _execution;
    private bool _detached;

    private WorkspaceExecutionController(
        Window window,
        MainWindowViewModel shell,
        IWorkspaceClassifier workspaceClassifier,
        WorkspaceExecutionViewModel execution)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
        _shell = shell ?? throw new ArgumentNullException(nameof(shell));
        _workspaceClassifier = workspaceClassifier
            ?? throw new ArgumentNullException(nameof(workspaceClassifier));
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
        WorkspaceExecutionViewModel execution)
    {
        _ = new WorkspaceExecutionController(window, shell, workspaceClassifier, execution);
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
        if (!_shell.Explorer.IsWorkspaceOpen)
        {
            _execution.SetWorkspace(null);
            return;
        }

        var path = _shell.WorkspacePath;
        if (Directory.Exists(path))
        {
            _execution.SetWorkspace(_workspaceClassifier.ClassifyDirectory(path));
            return;
        }

        _execution.SetWorkspace(
            _workspaceClassifier.TryClassifyFile(path, out var workspace)
                ? workspace
                : null);
    }

    private async void Window_OnKeyDown(object? sender, KeyEventArgs eventArgs)
    {
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

    private void Detach()
    {
        if (_detached)
        {
            return;
        }

        _detached = true;
        _shell.PropertyChanged -= Shell_OnPropertyChanged;
        _shell.Explorer.PropertyChanged -= Explorer_OnPropertyChanged;
        _window.KeyDown -= Window_OnKeyDown;
        _window.Closed -= Window_OnClosed;
        _execution.Dispose();
    }
}
