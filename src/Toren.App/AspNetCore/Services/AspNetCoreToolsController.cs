using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Toren.App.AspNetCore.ViewModels;
using Toren.App.Execution.ViewModels;
using Toren.App.Views.AspNetCore;
using Toren.App.Views;

namespace Toren.App.AspNetCore.Services;

internal sealed class AspNetCoreToolsController
{
    private readonly Window _window;
    private readonly WorkspaceExecutionViewModel _execution;
    private readonly AspNetCoreToolsViewModel _viewModel;
    private readonly TabControl _toolTabs;
    private readonly TabItem _aspNetTab;
    private CancellationTokenSource? _refreshCancellation;
    private bool _detached;

    private AspNetCoreToolsController(
        Window window,
        WorkspaceExecutionViewModel execution,
        AspNetCoreToolsViewModel viewModel,
        TabControl toolTabs)
    {
        _window = window;
        _execution = execution;
        _viewModel = viewModel;
        _toolTabs = toolTabs;
        _aspNetTab = new TabItem
        {
            Header = new ToolTabHeader(window, "ASP.NET", "TorenIconMarkupFile"),
            Content = new AspNetCoreToolsPanel { DataContext = viewModel },
        };
        _toolTabs.Items.Add(_aspNetTab);

        _execution.PropertyChanged += Execution_OnPropertyChanged;
        _window.Closed += Window_OnClosed;
        SynchronizeProject();
    }

    public static AspNetCoreToolsController? Attach(
        Window window,
        WorkspaceExecutionViewModel execution,
        AspNetCoreToolsViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(execution);
        ArgumentNullException.ThrowIfNull(viewModel);

        var toolTabs = window.GetLogicalDescendants()
            .OfType<TabControl>()
            .FirstOrDefault(control => control.Classes.Contains("tool-tabs"));
        return toolTabs is null
            ? null
            : new AspNetCoreToolsController(window, execution, viewModel, toolTabs);
    }

    public void SetApplicationUri(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        if (!_detached)
        {
            _viewModel.SetApplicationUri(uri);
        }
    }

    private void Execution_OnPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName == nameof(WorkspaceExecutionViewModel.SelectedRunTargetIndex))
        {
            SynchronizeProject();
        }
        else if (eventArgs.PropertyName == nameof(WorkspaceExecutionViewModel.CurrentCommandKind))
        {
            _viewModel.SetApplicationUri(null);
        }
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
        _ = RefreshAsync(cancellation);
    }

    private async Task RefreshAsync(CancellationTokenSource cancellation)
    {
        try
        {
            await _viewModel.RefreshAsync(cancellation.Token).ConfigureAwait(true);
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
        _execution.PropertyChanged -= Execution_OnPropertyChanged;
        _window.Closed -= Window_OnClosed;
        _toolTabs.Items.Remove(_aspNetTab);
    }
}
