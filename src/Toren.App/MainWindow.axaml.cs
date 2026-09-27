using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using AvaloniaEdit.Highlighting;
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
    private bool _synchronizingEditorText;
    private bool _sessionCloseInProgress;
    private bool _sessionPersistedForClose;

    public MainWindow(MainWindowViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        _viewModel = viewModel;
        DataContext = viewModel;

        InitializeComponent();
        WorkspaceTree.AddHandler(TreeViewItem.ExpandedEvent, WorkspaceNode_OnExpanded);
        WorkspaceTree.DoubleTapped += WorkspaceTree_OnDoubleTapped;
        DocumentEditor.TextChanged += DocumentEditor_OnTextChanged;
        _viewModel.Documents.PropertyChanged += Documents_OnPropertyChanged;
        KeyDown += MainWindow_OnKeyDown;
        Closing += MainWindow_OnClosing;
        Opened += OnOpened;
    }

    private async void OnOpened(object? sender, EventArgs eventArgs)
    {
        await _viewModel.InitializeAsync().ConfigureAwait(true);
    }

    private async void MainWindow_OnClosing(object? sender, WindowClosingEventArgs eventArgs)
    {
        if (_viewModel.Documents.HasDirtyDocuments)
        {
            eventArgs.Cancel = true;
            _viewModel.SetStatus("Save all modified documents before closing Toren IDE.");
            return;
        }

        if (_sessionPersistedForClose)
        {
            return;
        }

        eventArgs.Cancel = true;
        if (_sessionCloseInProgress)
        {
            return;
        }

        _sessionCloseInProgress = true;
        await _viewModel.PersistDocumentSessionAsync().ConfigureAwait(true);
        _sessionPersistedForClose = true;
        _sessionCloseInProgress = false;
        Close();
    }

    private void Documents_OnPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        if (eventArgs.PropertyName == nameof(DocumentHostViewModel.ActiveDocument))
        {
            SynchronizeEditorFromActiveDocument();
        }
    }

    private void SynchronizeEditorFromActiveDocument()
    {
        var document = _viewModel.Documents.ActiveDocument;
        DocumentEditor.SyntaxHighlighting = document is null
            ? null
            : HighlightingManager.Instance.GetDefinitionByExtension(Path.GetExtension(document.Path));

        var text = document?.Text ?? string.Empty;
        if (DocumentEditor.Text.Equals(text, StringComparison.Ordinal))
        {
            return;
        }

        _synchronizingEditorText = true;
        try
        {
            DocumentEditor.Text = text;
        }
        finally
        {
            _synchronizingEditorText = false;
        }
    }

    private void DocumentEditor_OnTextChanged(object? sender, EventArgs eventArgs)
    {
        if (_synchronizingEditorText || _viewModel.Documents.ActiveDocument is not { } document)
        {
            return;
        }

        if (!document.Text.Equals(DocumentEditor.Text, StringComparison.Ordinal))
        {
            document.Text = DocumentEditor.Text;
        }
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

    private async void WorkspaceTree_OnDoubleTapped(object? sender, TappedEventArgs eventArgs)
    {
        if (WorkspaceTree.SelectedItem is WorkspaceNodeViewModel node)
        {
            await _viewModel.OpenDocumentAsync(node).ConfigureAwait(true);
            eventArgs.Handled = true;
        }
    }

    private void WelcomeTab_OnPointerPressed(object? sender, PointerPressedEventArgs eventArgs)
    {
        var properties = eventArgs.GetCurrentPoint(this).Properties;
        if (properties.IsMiddleButtonPressed)
        {
            _viewModel.CloseWelcome();
            eventArgs.Handled = true;
            return;
        }

        if (properties.IsLeftButtonPressed)
        {
            _viewModel.ActivateWelcome();
            eventArgs.Handled = true;
        }
    }

    private void DocumentTab_OnPointerPressed(object? sender, PointerPressedEventArgs eventArgs)
    {
        if (sender is not Control { DataContext: OpenDocumentViewModel document })
        {
            return;
        }

        var properties = eventArgs.GetCurrentPoint(this).Properties;
        if (properties.IsMiddleButtonPressed)
        {
            _viewModel.CloseDocument(document);
            eventArgs.Handled = true;
            return;
        }

        if (properties.IsLeftButtonPressed)
        {
            _viewModel.ActivateDocument(document);
            eventArgs.Handled = true;
        }
    }

    private void CloseDocument_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (sender is Button { DataContext: OpenDocumentViewModel document })
        {
            _viewModel.CloseDocument(document);
            eventArgs.Handled = true;
        }
    }

    private async void MainWindow_OnKeyDown(object? sender, KeyEventArgs eventArgs)
    {
        var saveModifier = eventArgs.KeyModifiers.HasFlag(KeyModifiers.Control)
            || eventArgs.KeyModifiers.HasFlag(KeyModifiers.Meta);
        if (eventArgs.Key != Key.S || !saveModifier)
        {
            return;
        }

        await _viewModel.SaveActiveDocumentAsync().ConfigureAwait(true);
        eventArgs.Handled = true;
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
