using System.ComponentModel;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Toren.App.Shell;
using Toren.App.SourceControl.ViewModels;
using Toren.App.ViewModels;
using Toren.App.Views.SourceControl;

namespace Toren.App.SourceControl.Services;

internal sealed class SourceControlController
{
    private readonly Window _window;
    private readonly MainWindowViewModel _shell;
    private readonly SourceControlViewModel _viewModel;
    private readonly Action<string> _setStatus;
    private readonly Button _sourceControlButton;
    private readonly SidebarController _sidebar;
    private bool _detached;

    private SourceControlController(
        Window window,
        MainWindowViewModel shell,
        SourceControlViewModel viewModel,
        Action<string> setStatus,
        Button sourceControlButton)
    {
        _window = window;
        _shell = shell;
        _viewModel = viewModel;
        _setStatus = setStatus;
        _sourceControlButton = sourceControlButton;
        _sidebar = SidebarController.For(window);
        _sidebar.Register(window, "SourceControlActivityButton", new SourceControlPanel { DataContext = viewModel });

        _sourceControlButton.IsEnabled = true;
        ToolTip.SetTip(_sourceControlButton, "Source Control");
        AutomationProperties.SetName(_sourceControlButton, "Source Control");
        _sourceControlButton.Click += SourceControlButton_OnClick;
        _shell.PropertyChanged += Shell_OnPropertyChanged;
        _shell.Explorer.PropertyChanged += Explorer_OnPropertyChanged;
        _window.Closed += Window_OnClosed;
        SynchronizeWorkspace();
    }

    public static void Attach(
        Window window,
        MainWindowViewModel shell,
        SourceControlViewModel viewModel,
        Action<string> setStatus)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(shell);
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(setStatus);

        var sourceControlButton = window.FindControl<Button>("SourceControlActivityButton");
        if (sourceControlButton is null)
        {
            return;
        }

        _ = new SourceControlController(
            window,
            shell,
            viewModel,
            setStatus,
            sourceControlButton);
    }

    private async void SourceControlButton_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        _sidebar.Show("SourceControlActivityButton");
        await RefreshAsync().ConfigureAwait(true);
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
        if (_detached || !_shell.Explorer.IsWorkspaceOpen)
        {
            _viewModel.SetWorkingDirectory(null);
            return;
        }

        var workspacePath = Path.GetFullPath(_shell.WorkspacePath);
        var workingDirectory = Directory.Exists(workspacePath)
            ? workspacePath
            : Path.GetDirectoryName(workspacePath);
        _viewModel.SetWorkingDirectory(workingDirectory);
    }

    private async Task RefreshAsync()
    {
        await _viewModel.RefreshAsync().ConfigureAwait(true);
        _setStatus(_viewModel.StatusText);
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
        _sourceControlButton.Click -= SourceControlButton_OnClick;
        _shell.PropertyChanged -= Shell_OnPropertyChanged;
        _shell.Explorer.PropertyChanged -= Explorer_OnPropertyChanged;
        _window.Closed -= Window_OnClosed;

    }
}
