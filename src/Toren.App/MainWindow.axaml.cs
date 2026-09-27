using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Toren.App.ViewModels;
using Toren.Workspaces.Models;

namespace Toren.App;

internal sealed partial class MainWindow : Window
{
    private static readonly IReadOnlyList<string> SupportedWorkspacePatterns =
        new[] { "*.sln", "*.slnx", "*.csproj" };

    private static readonly IReadOnlyList<FilePickerFileType> SupportedWorkspaceFileTypes =
        new[]
        {
            new FilePickerFileType(".NET workspace")
            {
                Patterns = SupportedWorkspacePatterns,
            },
        };

    private readonly MainWindowViewModel _viewModel;

    public MainWindow(MainWindowViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        _viewModel = viewModel;
        DataContext = viewModel;

        InitializeComponent();
        WorkspaceTree.AddHandler(TreeViewItem.ExpandedEvent, WorkspaceNode_OnExpanded);
        Opened += OnOpened;
    }

    private async void OnOpened(object? sender, EventArgs eventArgs)
    {
        await _viewModel.InitializeAsync().ConfigureAwait(true);
    }

    private void TitleBar_OnPointerPressed(object? sender, PointerPressedEventArgs eventArgs)
    {
        if (!eventArgs.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        if (eventArgs.ClickCount == 2)
        {
            ToggleWindowState();
            return;
        }

        BeginMoveDrag(eventArgs);
    }

    private void MinimizeWindow_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        WindowState = WindowState.Minimized;
    }

    private void ToggleMaximizeWindow_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        ToggleWindowState();
    }

    private void ToggleWindowState()
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    private void CloseWindow_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        Close();
    }

    private async void OpenFolder_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions
            {
                Title = "Open workspace folder",
                AllowMultiple = false,
            });

        if (folders.Count > 0)
        {
            await _viewModel.OpenDirectoryAsync(folders[0].Path.LocalPath).ConfigureAwait(true);
        }
    }

    private async void OpenWorkspaceFile_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        var files = await StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = "Open .NET solution or project",
                AllowMultiple = false,
                FileTypeFilter = SupportedWorkspaceFileTypes,
            });

        if (files.Count == 0)
        {
            return;
        }

        await _viewModel.OpenWorkspaceFileAsync(files[0].Path.LocalPath).ConfigureAwait(true);
    }

    private async void OpenRecent_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (sender is Button { DataContext: WorkspaceDescriptor workspace })
        {
            await _viewModel.OpenRecentAsync(workspace).ConfigureAwait(true);
        }
    }

    private void CloseWelcome_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        _viewModel.CloseWelcome();
    }

    private async void WorkspaceNode_OnExpanded(object? sender, RoutedEventArgs eventArgs)
    {
        if (eventArgs.Source is TreeViewItem { DataContext: WorkspaceNodeViewModel node })
        {
            await _viewModel.ExpandNodeAsync(node).ConfigureAwait(true);
        }
    }

    private void WelcomeTab_OnPointerPressed(object? sender, PointerPressedEventArgs eventArgs)
    {
        if (eventArgs.GetCurrentPoint(this).Properties.IsMiddleButtonPressed)
        {
            _viewModel.CloseWelcome();
            eventArgs.Handled = true;
        }
    }

    private async void OpenLink_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (sender is not Button { Tag: string url })
        {
            return;
        }

        var launched = await Launcher.LaunchUriAsync(new Uri(url)).ConfigureAwait(true);
        if (!launched)
        {
            _viewModel.SetStatus("Could not open the link in a browser.");
        }
    }
}
