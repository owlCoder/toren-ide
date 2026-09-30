using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Toren.App.EnvironmentDoctor.ViewModels;
using Toren.App.ViewModels;
using Toren.App.Views.EnvironmentDoctor;

namespace Toren.App.EnvironmentDoctor.Services;

internal sealed class EnvironmentDoctorController
{
    private readonly Window _window;
    private readonly MainWindowViewModel _shell;
    private readonly EnvironmentDoctorViewModel _viewModel;
    private readonly TabControl _toolTabs;
    private readonly TabItem _doctorTab;
    private CancellationTokenSource? _refreshCancellation;
    private bool _detached;

    private EnvironmentDoctorController(
        Window window,
        MainWindowViewModel shell,
        EnvironmentDoctorViewModel viewModel,
        TabControl toolTabs)
    {
        _window = window;
        _shell = shell;
        _viewModel = viewModel;
        _toolTabs = toolTabs;
        _doctorTab = new TabItem
        {
            Header = "DOCTOR",
            Content = new EnvironmentDoctorPanel { DataContext = viewModel },
        };
        _toolTabs.Items.Add(_doctorTab);

        _shell.PropertyChanged += Shell_OnPropertyChanged;
        _shell.Explorer.PropertyChanged += Explorer_OnPropertyChanged;
        _window.Closed += Window_OnClosed;
        SynchronizeWorkspace();
    }

    public static void Attach(
        Window window,
        MainWindowViewModel shell,
        EnvironmentDoctorViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(shell);
        ArgumentNullException.ThrowIfNull(viewModel);

        var toolTabs = window.GetLogicalDescendants()
            .OfType<TabControl>()
            .FirstOrDefault(control => control.Classes.Contains("tool-tabs"));
        if (toolTabs is not null)
        {
            _ = new EnvironmentDoctorController(window, shell, viewModel, toolTabs);
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

    private void SynchronizeWorkspace()
    {
        CancelRefresh();
        var workspacePath = _shell.Explorer.IsWorkspaceOpen ? _shell.WorkspacePath : null;
        _viewModel.SetWorkspace(workspacePath);
        if (string.IsNullOrWhiteSpace(workspacePath))
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
        _shell.PropertyChanged -= Shell_OnPropertyChanged;
        _shell.Explorer.PropertyChanged -= Explorer_OnPropertyChanged;
        _window.Closed -= Window_OnClosed;
        _toolTabs.Items.Remove(_doctorTab);
    }
}
