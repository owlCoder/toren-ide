using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Toren.App.ViewModels;
using Toren.App.Views;

namespace Toren.App;

public sealed partial class MainWindow : Window
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
        Opened += OnOpened;
    }

    private async void OnOpened(object? sender, EventArgs eventArgs)
    {
        await _viewModel.InitializeAsync().ConfigureAwait(true);
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
            _viewModel.OpenDirectory(folders[0].Path.LocalPath);
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

        if (!_viewModel.TryOpenWorkspaceFile(files[0].Path.LocalPath))
        {
            _viewModel.SetStatus("The selected file is not a supported .NET workspace.");
        }
    }

    private async void About_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        await AboutWindow.ShowAsync(this).ConfigureAwait(true);
    }
}
