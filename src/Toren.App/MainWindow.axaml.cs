using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.Threading;
using AvaloniaEdit.Editing;
using AvaloniaEdit.TextMate;
using TextMateSharp.Grammars;
using Toren.App.Diagnostics.ViewModels;
using Toren.App.Settings.Contracts;
using Toren.App.Settings.Models;
using Toren.App.Shell;
using Toren.App.ViewModels;
using Toren.App.Views.Problems;
using Toren.App.Views.Theme;
using Toren.Workspaces.Models;
using TextMateInstallation = AvaloniaEdit.TextMate.TextMate.Installation;

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
    private readonly IApplicationSettingsStore _applicationSettingsStore;
    private readonly RegistryOptions _registryOptions;
    private readonly TextMateInstallation _textMateInstallation;
    private readonly ThemeToggleIcon _themeToggleIcon;
    private bool _synchronizingEditorText;
    private bool _sessionCloseInProgress;
    private bool _sessionPersistedForClose;
    private bool _isDarkTheme = true;

    public MainWindow(
        MainWindowViewModel viewModel,
        IApplicationSettingsStore applicationSettingsStore)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(applicationSettingsStore);

        _viewModel = viewModel;
        _applicationSettingsStore = applicationSettingsStore;
        DataContext = viewModel;

        InitializeComponent();
        InstallProblemsPanel();
        _ = ToolPanelController.For(this);
        ToolTabs.SelectionChanged += ToolTabs_OnSelectionChanged;
        _themeToggleIcon = new ThemeToggleIcon();
        ThemeToggleButton.Content = _themeToggleIcon;

        _registryOptions = new RegistryOptions(ThemeName.DarkPlus);
        _textMateInstallation = DocumentEditor.InstallTextMate(_registryOptions);
        _textMateInstallation.AppliedTheme += TextMateInstallation_OnAppliedTheme;
        ConfigureEditor();
        ApplyTheme(isDark: true);

        WorkspaceTree.AddHandler(TreeViewItem.ExpandedEvent, WorkspaceNode_OnExpanded);
        WorkspaceTree.DoubleTapped += WorkspaceTree_OnDoubleTapped;
        DocumentEditor.TextChanged += DocumentEditor_OnTextChanged;
        _viewModel.Documents.PropertyChanged += Documents_OnPropertyChanged;
        KeyDown += MainWindow_OnKeyDown;
        Closing += MainWindow_OnClosing;
        Opened += OnOpened;
    }

    protected override void OnClosed(EventArgs eventArgs)
    {
        _textMateInstallation.AppliedTheme -= TextMateInstallation_OnAppliedTheme;
        _textMateInstallation.Dispose();
        _viewModel.Dispose();
        base.OnClosed(eventArgs);
    }

    private void InstallProblemsPanel()
    {
        var toolTabs = this.GetLogicalDescendants()
            .OfType<TabControl>()
            .FirstOrDefault(control => control.Classes.Contains("tool-tabs"));
        var problemsTab = toolTabs?.Items.OfType<TabItem>().FirstOrDefault();
        if (problemsTab is null)
        {
            return;
        }

        var panel = new ProblemsPanel();
        panel.ProblemActivated += NavigateToProblem;
        problemsTab.Content = panel;
        toolTabs!.SelectedIndex = 0;
    }

    private void ToolTabs_OnSelectionChanged(object? sender, SelectionChangedEventArgs eventArgs)
    {
        if (ReferenceEquals(eventArgs.Source, ToolTabs) && ToolTabs.SelectedItem is TabItem selectedTab)
        {
            Dispatcher.UIThread.Post(selectedTab.BringIntoView, DispatcherPriority.Loaded);
        }
    }

    private void ConfigureEditor()
    {
        DocumentEditor.Options.HighlightCurrentLine = true;
        DocumentEditor.Options.EnableTextDragDrop = true;
        DocumentEditor.TextArea.RightClickMovesCaret = true;
        DocumentEditor.LineNumbersMargin = new Thickness(10, 0, 12, 0);

        foreach (var margin in DocumentEditor.TextArea.LeftMargins.OfType<LineNumberMargin>())
        {
            margin.MinWidthInDigits = 3;
        }
    }

    private async void OnOpened(object? sender, EventArgs eventArgs)
    {
        var settings = await _applicationSettingsStore.LoadAsync().ConfigureAwait(true);
        if (settings.IsSuccess)
        {
            ApplyTheme(settings.Value!.Theme == ApplicationThemePreference.Dark);
        }
        else
        {
            _viewModel.SetStatus(settings.Error.Message);
        }

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
        ApplyGrammar(document?.Path);

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

    private void ApplyGrammar(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            _textMateInstallation.SetGrammar(null);
            return;
        }

        var language = _registryOptions.GetLanguageByExtension(Path.GetExtension(path));
        var scopeName = language is null
            ? null
            : _registryOptions.GetScopeByLanguageId(language.Id);
        _textMateInstallation.SetGrammar(scopeName);
    }

    private async void DocumentEditor_OnTextChanged(object? sender, EventArgs eventArgs)
    {
        if (_synchronizingEditorText || _viewModel.Documents.ActiveDocument is not { } document)
        {
            return;
        }

        if (document.Text.Equals(DocumentEditor.Text, StringComparison.Ordinal))
        {
            return;
        }

        document.Text = DocumentEditor.Text;
        await _viewModel.RefreshActiveDiagnosticsAsync(debounce: true).ConfigureAwait(true);
    }

    private void TextMateInstallation_OnAppliedTheme(object? sender, TextMateInstallation installation)
    {
        ApplyEditorThemeBrush(
            installation,
            "editor.background",
            "TorenBackgroundBrush",
            brush =>
            {
                DocumentEditor.Background = brush;
                DocumentEditor.TextArea.Background = brush;
            });
        ApplyEditorThemeBrush(installation, "editor.foreground", "TorenEditorForegroundBrush", brush => DocumentEditor.Foreground = brush);
        ApplyEditorThemeBrush(
            installation,
            "editor.selectionBackground",
            "TorenEditorSelectionBrush",
            brush => DocumentEditor.TextArea.SelectionBrush = brush);
        ApplyEditorThemeBrush(
            installation,
            "editor.lineHighlightBackground",
            "TorenEditorLineHighlightBrush",
            brush =>
            {
                DocumentEditor.TextArea.TextView.CurrentLineBackground = brush;
                // A null pen falls back to AvaloniaEdit's default green outline.
                DocumentEditor.TextArea.TextView.CurrentLineBorder = new Pen(brush);
            });
        ApplyEditorThemeBrush(
            installation,
            "editorLineNumber.foreground",
            "TorenEditorLineNumberBrush",
            brush => DocumentEditor.LineNumbersForeground = brush);
    }

    private bool ApplyEditorThemeBrush(
        TextMateInstallation installation,
        string textMateKey,
        string resourceKey,
        Action<IBrush> apply)
    {
        var variant = _isDarkTheme ? ThemeVariant.Dark : ThemeVariant.Light;
        if (this.TryFindResource(resourceKey, variant, out var resource) && resource is IBrush brush)
        {
            apply(brush);
            return true;
        }

        return ApplyTextMateBrush(installation, textMateKey, apply);
    }

    private static bool ApplyTextMateBrush(
        TextMateInstallation installation,
        string colorKey,
        Action<IBrush> apply)
    {
        if (!installation.TryGetThemeColor(colorKey, out var colorValue)
            || !Color.TryParse(colorValue, out var color))
        {
            return false;
        }

        apply(new SolidColorBrush(color));
        return true;
    }

    private void ApplyTheme(bool isDark)
    {
        _isDarkTheme = isDark;
        if (Application.Current is { } application)
        {
            application.RequestedThemeVariant = isDark
                ? ThemeVariant.Dark
                : ThemeVariant.Light;
        }

        _textMateInstallation.SetTheme(
            _registryOptions.LoadTheme(isDark ? ThemeName.DarkPlus : ThemeName.LightPlus));
        TextMateInstallation_OnAppliedTheme(this, _textMateInstallation);
        _themeToggleIcon.SetDarkMode(isDark);
        ToolTip.SetTip(
            ThemeToggleButton,
            isDark ? "Switch to light theme" : "Switch to dark theme");
    }

    private async void ToggleTheme_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        ApplyTheme(!_isDarkTheme);
        var saved = await _applicationSettingsStore.SaveAsync(
            new ApplicationSettings(
                _isDarkTheme ? ApplicationThemePreference.Dark : ApplicationThemePreference.Light))
            .ConfigureAwait(true);
        if (saved.IsFailure)
        {
            _viewModel.SetStatus(saved.Error.Message);
        }
    }

    private async void SaveActiveDocument_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        await _viewModel.SaveActiveDocumentAsync().ConfigureAwait(true);
    }

    private void Undo_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        DocumentEditor.Undo();
        DocumentEditor.Focus();
    }

    private void Redo_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        DocumentEditor.Redo();
        DocumentEditor.Focus();
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

    private async void DocumentTab_OnPointerPressed(object? sender, PointerPressedEventArgs eventArgs)
    {
        if (sender is not Control { DataContext: OpenDocumentViewModel document })
        {
            return;
        }

        var properties = eventArgs.GetCurrentPoint(this).Properties;
        if (properties.IsMiddleButtonPressed)
        {
            await _viewModel.CloseDocumentAsync(document).ConfigureAwait(true);
            eventArgs.Handled = true;
            return;
        }

        if (properties.IsLeftButtonPressed)
        {
            await _viewModel.ActivateDocumentAsync(document).ConfigureAwait(true);
            eventArgs.Handled = true;
        }
    }

    private async void CloseDocument_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (sender is Button { DataContext: OpenDocumentViewModel document })
        {
            await _viewModel.CloseDocumentAsync(document).ConfigureAwait(true);
            eventArgs.Handled = true;
        }
    }

    private async void NavigateToProblem(ProblemItemViewModel problem)
    {
        if (!problem.CanNavigate)
        {
            _viewModel.SetStatus(problem.Message);
            return;
        }

        var opened = await _viewModel.Documents.OpenAsync(problem.FilePath).ConfigureAwait(true);
        if (!opened.IsSuccess)
        {
            _viewModel.SetStatus(opened.Error.Message);
            return;
        }

        var diagnosticsRefresh = _viewModel.ActivateDocumentAsync(opened.Value);
        var line = Math.Clamp(problem.StartLine, 1, DocumentEditor.Document.LineCount);
        var documentLine = DocumentEditor.Document.GetLineByNumber(line);
        var column = Math.Clamp(problem.StartColumn, 1, documentLine.Length + 1);
        DocumentEditor.CaretOffset = documentLine.Offset + column - 1;
        DocumentEditor.ScrollTo(line, column);
        DocumentEditor.Focus();
        _viewModel.SetStatus($"Opened {opened.Value.Title}:{line}");
        await diagnosticsRefresh.ConfigureAwait(true);
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
