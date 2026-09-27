using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Toren.App.ViewModels;

namespace Toren.App;

public sealed partial class MainWindow : Window
{
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

        var folder = folders.FirstOrDefault();
        if (folder is not null)
        {
            _viewModel.OpenDirectory(folder.Path.LocalPath);
        }
    }

    private async void OpenWorkspaceFile_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        var files = await StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = "Open .NET solution or project",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType(".NET workspace")
                    {
                        Patterns = new[] { "*.sln", "*.slnx", "*.csproj" },
                    },
                },
            });

        var file = files.FirstOrDefault();
        if (file is null)
        {
            return;
        }

        if (!_viewModel.TryOpenWorkspaceFile(file.Path.LocalPath))
        {
            _viewModel.SetStatus("The selected file is not a supported .NET workspace.");
        }
    }
}
