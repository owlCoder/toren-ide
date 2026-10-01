using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Toren.App.Terminal.ViewModels;
using Toren.App.ViewModels;
using Toren.App.Views.Terminal;
using Toren.App.Views;

namespace Toren.App.Terminal.Services;

internal sealed class TerminalController : IAsyncDisposable
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
            Header = new ToolTabHeader(window, "Terminal", "TorenIconTerminal"),
            Content = new TerminalPanel { DataContext = _viewModel },
        };
        _toolTabs.Items.Add(_terminalTab);

        _toolTabs.SelectionChanged += ToolTabs_OnSelectionChanged;
        _viewModel.PropertyChanged += Host_OnPropertyChanged;
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

    public async ValueTask DisposeAsync()
    {
        if (_detached)
        {
            return;
        }

        _detached = true;
        _toolTabs.SelectionChanged -= ToolTabs_OnSelectionChanged;
        _viewModel.PropertyChanged -= Host_OnPropertyChanged;
        _shell.PropertyChanged -= Shell_OnPropertyChanged;
        _shell.Explorer.PropertyChanged -= Explorer_OnPropertyChanged;
        _window.Closed -= Window_OnClosed;
        _toolTabs.Items.Remove(_terminalTab);
        await _viewModel.DisposeAsync().ConfigureAwait(true);
    }

    private async void ToolTabs_OnSelectionChanged(object? sender, SelectionChangedEventArgs args)
    {
        if (ReferenceEquals(args.Source, _toolTabs) && ReferenceEquals(_toolTabs.SelectedItem, _terminalTab))
        {
            await StartSelectedSessionAsync().ConfigureAwait(true);
        }
    }

    private async void Host_OnPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(TerminalHostViewModel.SelectedSession)
            && ReferenceEquals(_toolTabs.SelectedItem, _terminalTab))
        {
            await StartSelectedSessionAsync().ConfigureAwait(true);
        }
    }

    private async Task StartSelectedSessionAsync()
    {
        if (_viewModel.SelectedSession is { CanStart: true } session)
        {
            await session.StartAsync().ConfigureAwait(true);
        }

        Dispatcher.UIThread.Post(() =>
        {
            if (!_detached && ReferenceEquals(_toolTabs.SelectedItem, _terminalTab)
                && _terminalTab.Content is TerminalPanel { IsEffectivelyVisible: true } panel)
            {
                panel.FindControl<TextBox>("TerminalInput")?.Focus();
            }
        }, DispatcherPriority.Loaded);
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
        await DisposeAsync().ConfigureAwait(true);
    }
}
