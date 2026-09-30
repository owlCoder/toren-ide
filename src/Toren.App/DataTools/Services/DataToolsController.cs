using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Toren.App.DataTools.ViewModels;
using Toren.App.Execution.ViewModels;
using Toren.App.ViewModels;
using Toren.App.Views.DataTools;

namespace Toren.App.DataTools.Services;

internal sealed class DataToolsController
{
    private readonly Window _window;
    private readonly MainWindowViewModel _shell;
    private readonly WorkspaceExecutionViewModel _execution;
    private readonly DataToolsViewModel _viewModel;
    private readonly TabControl _toolTabs;
    private readonly TabItem _dataTab;
    private CancellationTokenSource? _refreshCancellation;
    private bool _detached;

    private DataToolsController(
        Window window,
        MainWindowViewModel shell,
        WorkspaceExecutionViewModel execution,
        DataToolsViewModel viewModel,
        TabControl toolTabs)
    {
        _window = window;
        _shell = shell;
        _execution = execution;
        _viewModel = viewModel;
        _toolTabs = toolTabs;
        _dataTab = new TabItem
        {
            Header = "DATA",
            Content = new DataToolsPanel { DataContext = viewModel },
        };
        _toolTabs.Items.Add(_dataTab);

        _shell.PropertyChanged += Shell_OnPropertyChanged;
        _shell.Explorer.PropertyChanged += Explorer_OnPropertyChanged;
        _execution.PropertyChanged += Execution_OnPropertyChanged;
        _window.Closed += Window_OnClosed;
        SynchronizeWorkspace();
        SynchronizeProject();
    }

    public static void Attach(
        Window window,
        MainWindowViewModel shell,
        WorkspaceExecutionViewModel execution,
        DataToolsViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(shell);
        ArgumentNullException.ThrowIfNull(execution);
        ArgumentNullException.ThrowIfNull(viewModel);

        var toolTabs = window.GetLogicalDescendants()
            .OfType<TabControl>()
            .FirstOrDefault(control => control.Classes.Contains("tool-tabs"));
        if (toolTabs is not null)
        {
            _ = new DataToolsController(window, shell, execution, viewModel, toolTabs);
        }
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

    private void Execution_OnPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName == nameof(WorkspaceExecutionViewModel.SelectedRunTargetIndex))
        {
            SynchronizeProject();
        }
    }

    private void SynchronizeWorkspace()
    {
        _viewModel.SetWorkspace(
            _shell.Explorer.IsWorkspaceOpen ? _shell.WorkspacePath : null);
    }

    private void SynchronizeProject()
    {
        CancelRefresh();
        var projectPath = _execution.SelectedRunTarget?.ProjectPath;
        _viewModel.SetProject(projectPath);
        if (projectPath is null)
        {
            return;
        }

        var cancellation = new CancellationTokenSource();
        _refreshCancellation = cancellation;
        _ = RefreshEfAsync(cancellation);
    }

    private async Task RefreshEfAsync(CancellationTokenSource cancellation)
    {
        try
        {
            await _viewModel.RefreshEfAsync(cancellation.Token).ConfigureAwait(true);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        finally
        {
            if (ReferenceEquals(_refreshCancellation, cancellation))
            {
                _refreshCancellation = null;
            }

            cancellation.Dispose();
        }
    }

    private void CancelRefresh()
    {
        var cancellation = Interlocked.Exchange(ref _refreshCancellation, null);
        cancellation?.Cancel();
    }

    private void Window_OnClosed(object? sender, EventArgs eventArgs) => Detach();

    private void Detach()
    {
        if (_detached)
        {
            return;
        }

        _detached = true;
        CancelRefresh();
        _shell.PropertyChanged -= Shell_OnPropertyChanged;
        _shell.Explorer.PropertyChanged -= Explorer_OnPropertyChanged;
        _execution.PropertyChanged -= Execution_OnPropertyChanged;
        _window.Closed -= Window_OnClosed;
        _toolTabs.Items.Remove(_dataTab);
    }
}
