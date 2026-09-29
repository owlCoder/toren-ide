using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Toren.App.Terminal.ViewModels;
using Toren.App.ViewModels;
using Toren.App.Views.Terminal;

namespace Toren.App.Terminal.Services;

internal sealed class TerminalController
{
    private readonly Window _window;
    private readonly MainWindowViewModel _shell;
    private readonly TerminalHostViewModel _viewModel;
    private readonly TabControl _toolTabs;
    private readonly TabItem _terminalTab;
    private bool _detached;

    private TerminalController(
        Window window,
        MainWindowViewModel shell,
        TerminalViewModel initialSession,
        TabControl toolTabs)
    {
        _window = window;
        _shell = shell;
        _viewModel = new TerminalHostViewModel(initialSession);
        _toolTabs = toolTabs;
        _terminalTab = new TabItem
        {
            Header = "Terminal",
            Content = new TerminalPanel { DataContext = _viewModel },
        };
        _toolTabs.Items.Add(_terminalTab);

        _shell.PropertyChanged += Shell_OnPropertyChanged;
        _shell.Explorer.PropertyChanged += Explorer_OnPropertyChanged;
        _window.Closed += Window_OnClosed;
        SynchronizeWorkingDirectory();
    }

    public static void Attach(
        Window window,
        MainWindowViewModel shell,
        TerminalViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(shell);
        ArgumentNullException.ThrowIfNull(viewModel);

        var toolTabs = window.GetLogicalDescendants()
            .OfType<TabControl>()
            .FirstOrDefault(control => control.Classes.Contains("tool-tabs"));
        if (toolTabs is null)
        {
            return;
        }

        _ = new TerminalController(window, shell, viewModel, toolTabs);
    }

    private void Shell_OnPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName == nameof(MainWindowViewModel.WorkspacePath))
        {
            SynchronizeWorkingDirectory();
        }
    }

    private void Explorer_OnPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName == nameof(ExplorerViewModel.IsWorkspaceOpen))
        {
            SynchronizeWorkingDirectory();
        }
    }

    private void SynchronizeWorkingDirectory()
    {
        if (_detached || !_shell.Explorer.IsWorkspaceOpen)
        {
            _viewModel.SetWorkingDirectory(Directory.GetCurrentDirectory());
            return;
        }

        var workspacePath = Path.GetFullPath(_shell.WorkspacePath);
        var workingDirectory = Directory.Exists(workspacePath)
            ? workspacePath
            : Path.GetDirectoryName(workspacePath) ?? Directory.GetCurrentDirectory();
        _viewModel.SetWorkingDirectory(workingDirectory);
    }

    private async void Window_OnClosed(object? sender, EventArgs eventArgs)
    {
        await DetachAsync().ConfigureAwait(true);
    }

    private async Task DetachAsync()
    {
        if (_detached)
        {
            return;
        }

        _detached = true;
        _shell.PropertyChanged -= Shell_OnPropertyChanged;
        _shell.Explorer.PropertyChanged -= Explorer_OnPropertyChanged;
        _window.Closed -= Window_OnClosed;
        _toolTabs.Items.Remove(_terminalTab);
        await _viewModel.DisposeAsync().ConfigureAwait(true);
    }
}
