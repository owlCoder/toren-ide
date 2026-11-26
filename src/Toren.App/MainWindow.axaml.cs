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
using Avalonia.VisualTree;
using AvaloniaEdit;
using AvaloniaEdit.Editing;
using TextMateSharp.Grammars;
using Toren.App.Diagnostics.ViewModels;
using Toren.App.Documents.Models;
using Toren.App.Layout.Contracts;
using Toren.App.Layout.Models;
using Toren.App.Settings.Contracts;
using Toren.App.Settings.Models;
using Toren.App.Shell;
using Toren.App.ViewModels;
using Toren.App.Views.Editing;
using Toren.App.Views.Problems;
using Toren.App.Views.Theme;
using Toren.Workspaces.Models;
using TextMateInstallation = AvaloniaEdit.TextMate.TextMate.Installation;

namespace Toren.App;

internal sealed partial class MainWindow : Window, IPanelLayoutHost
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
    private readonly EditorPane _primaryPane;
    private readonly EditorPane _secondaryPane;
    private readonly ThemeToggleIcon _themeToggleIcon;
    private EditorPane _focusedPane;
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
        _primaryPane = CreatePane(DocumentEditor);
        _secondaryPane = CreatePane(SecondaryEditor);
        _focusedPane = _primaryPane;
        ApplyTheme(isDark: true);

        WorkspaceTree.AddHandler(TreeViewItem.ExpandedEvent, WorkspaceNode_OnExpanded);
        WorkspaceTree.Tapped += WorkspaceTree_OnTapped;
        WorkspaceTree.DoubleTapped += WorkspaceTree_OnDoubleTapped;
        _viewModel.Documents.PropertyChanged += Documents_OnPropertyChanged;
        _viewModel.Explorer.NodeRevealed += Explorer_OnNodeRevealed;
        _viewModel.PanelHost = this;
        KeyDown += MainWindow_OnKeyDown;
        Closing += MainWindow_OnClosing;
        Opened += OnOpened;
    }

    protected override void OnClosed(EventArgs eventArgs)
    {
        _viewModel.PanelHost = null;
        _viewModel.Documents.PropertyChanged -= Documents_OnPropertyChanged;
        _viewModel.Explorer.NodeRevealed -= Explorer_OnNodeRevealed;
        _primaryPane.Dispose();
        _secondaryPane.Dispose();
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

    private EditorPane CreatePane(TextEditor editor)
    {
        ConfigureEditor(editor);
        var pane = new EditorPane(editor, _registryOptions);
        pane.Installation.AppliedTheme += (_, installation) => ApplyEditorTheme(pane.Editor, installation);
        pane.TextEdited += Pane_OnTextEdited;
        editor.TextArea.GotFocus += (_, _) => Pane_OnGotFocus(pane);
        return pane;
    }

    private static void ConfigureEditor(TextEditor editor)
    {
        editor.Options.HighlightCurrentLine = true;
        editor.Options.EnableTextDragDrop = true;
        editor.TextArea.RightClickMovesCaret = true;
        editor.LineNumbersMargin = new Thickness(10, 0, 12, 0);

        foreach (var margin in editor.TextArea.LeftMargins.OfType<LineNumberMargin>())
        {
            margin.MinWidthInDigits = 3;
        }
    }

    private IEnumerable<EditorPane> Panes => [_primaryPane, _secondaryPane];

    private async void Pane_OnGotFocus(EditorPane pane)
    {
        if (ReferenceEquals(_focusedPane, pane))
        {
            return;
        }

        _focusedPane = pane;
        await _viewModel.FocusEditorPaneAsync(ReferenceEquals(pane, _secondaryPane)).ConfigureAwait(true);
    }

    private async void Pane_OnTextEdited(object? sender, EventArgs eventArgs)
    {
        // The document text is shared with the view model, so only diagnostics need a nudge.
        // Both panes raise the event for a shared document; the focused one is the editing one.
        if (!ReferenceEquals(sender, _focusedPane))
        {
            return;
        }

        await _viewModel.RefreshActiveDiagnosticsAsync(debounce: true).ConfigureAwait(true);
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
        var documents = _viewModel.Documents;
        switch (eventArgs.PropertyName)
        {
            case nameof(DocumentHostViewModel.PrimaryDocument):
                _primaryPane.Show(documents.PrimaryDocument);
                break;
            case nameof(DocumentHostViewModel.SecondaryDocument):
                _secondaryPane.Show(documents.SecondaryDocument);
                break;
            case nameof(DocumentHostViewModel.SplitOrientation):
                if (documents.SplitOrientation == EditorSplitOrientation.None)
                {
                    _focusedPane = _primaryPane;
                }

                ApplySplitLayout(0.5);
                break;
        }
    }

    /// <summary>
    /// Lays out the editor area: one editor, or two separated by a splitter. Horizontal stacks
    /// them, vertical puts them side by side. <paramref name="ratio"/> is the first editor's share.
    /// </summary>
    private void ApplySplitLayout(double ratio)
    {
        var orientation = _viewModel.Documents.SplitOrientation;
        var isSplit = orientation != EditorSplitOrientation.None;

        EditorArea.RowDefinitions.Clear();
        EditorArea.ColumnDefinitions.Clear();
        SecondaryEditorHost.IsVisible = isSplit;
        EditorSplitter.IsVisible = isSplit;

        Grid.SetRow(PrimaryEditorHost, 0);
        Grid.SetColumn(PrimaryEditorHost, 0);

        if (!isSplit)
        {
            // Hidden children must not point at rows/columns that no longer exist.
            Grid.SetRow(EditorSplitter, 0);
            Grid.SetColumn(EditorSplitter, 0);
            Grid.SetRow(SecondaryEditorHost, 0);
            Grid.SetColumn(SecondaryEditorHost, 0);
            EditorArea.RowDefinitions.Add(new RowDefinition(1, GridUnitType.Star));
            EditorArea.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            return;
        }

        ratio = Math.Clamp(ratio, 0.15, 0.85);
        if (orientation == EditorSplitOrientation.Horizontal)
        {
            EditorArea.ColumnDefinitions.Add(new ColumnDefinition(1, GridUnitType.Star));
            EditorArea.RowDefinitions.Add(new RowDefinition(ratio, GridUnitType.Star) { MinHeight = 80 });
            EditorArea.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            EditorArea.RowDefinitions.Add(new RowDefinition(1 - ratio, GridUnitType.Star) { MinHeight = 80 });

            Grid.SetRow(EditorSplitter, 1);
            Grid.SetColumn(EditorSplitter, 0);
            Grid.SetRow(SecondaryEditorHost, 2);
            Grid.SetColumn(SecondaryEditorHost, 0);
            EditorSplitter.ResizeDirection = GridResizeDirection.Rows;
            EditorSplitter.Height = 5;
            EditorSplitter.Width = double.NaN;
        }
        else
        {
            EditorArea.RowDefinitions.Add(new RowDefinition(1, GridUnitType.Star));
            EditorArea.ColumnDefinitions.Add(new ColumnDefinition(ratio, GridUnitType.Star) { MinWidth = 160 });
            EditorArea.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            EditorArea.ColumnDefinitions.Add(new ColumnDefinition(1 - ratio, GridUnitType.Star) { MinWidth = 160 });

            Grid.SetRow(EditorSplitter, 0);
            Grid.SetColumn(EditorSplitter, 1);
            Grid.SetRow(SecondaryEditorHost, 0);
            Grid.SetColumn(SecondaryEditorHost, 2);
            EditorSplitter.ResizeDirection = GridResizeDirection.Columns;
            EditorSplitter.Width = 5;
            EditorSplitter.Height = double.NaN;
        }
    }

    public PanelLayout CapturePanelLayout()
    {
        var sidebar = ShellGrid.ColumnDefinitions[1].ActualWidth;
        var toolPanel = ToolPanel.IsVisible ? ShellGrid.RowDefinitions[3].ActualHeight : 0d;

        double? ratio = _viewModel.Documents.SplitOrientation switch
        {
            EditorSplitOrientation.Horizontal => Share(PrimaryEditorHost.Bounds.Height, SecondaryEditorHost.Bounds.Height),
            EditorSplitOrientation.Vertical => Share(PrimaryEditorHost.Bounds.Width, SecondaryEditorHost.Bounds.Width),
            _ => null,
        };

        return new PanelLayout(
            sidebar > 0 ? sidebar : null,
            toolPanel > 0 ? toolPanel : null,
            ratio);
    }

    public void ApplyPanelLayout(PanelLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);

        if (layout.SidebarWidth is { } sidebarWidth)
        {
            var column = ShellGrid.ColumnDefinitions[1];
            column.Width = new GridLength(Math.Clamp(sidebarWidth, column.MinWidth, column.MaxWidth));
        }

        if (layout.ToolPanelHeight is { } toolPanelHeight && ToolPanel.IsVisible)
        {
            var row = ShellGrid.RowDefinitions[3];
            var maximum = Math.Max(row.MinHeight, Bounds.Height * 0.6);
            row.Height = new GridLength(Math.Clamp(toolPanelHeight, row.MinHeight, maximum));
        }

        ApplySplitLayout(layout.SplitRatio ?? 0.5);
    }

    private static double? Share(double first, double second) =>
        first + second > 0 ? first / (first + second) : null;

    private void ApplyEditorTheme(TextEditor editor, TextMateInstallation installation)
    {
        ApplyEditorThemeBrush(
            installation,
            "editor.background",
            "TorenBackgroundBrush",
            brush =>
            {
                editor.Background = brush;
                editor.TextArea.Background = brush;
            });
        ApplyEditorThemeBrush(installation, "editor.foreground", "TorenEditorForegroundBrush", brush => editor.Foreground = brush);
        ApplyEditorThemeBrush(
            installation,
            "editor.selectionBackground",
            "TorenEditorSelectionBrush",
            brush => editor.TextArea.SelectionBrush = brush);
        ApplyEditorThemeBrush(
            installation,
            "editor.lineHighlightBackground",
            "TorenEditorLineHighlightBrush",
            brush =>
            {
                editor.TextArea.TextView.CurrentLineBackground = brush;
                // A null pen falls back to AvaloniaEdit's default green outline.
                editor.TextArea.TextView.CurrentLineBorder = new Pen(brush);
            });
        ApplyEditorThemeBrush(
            installation,
            "editorLineNumber.foreground",
            "TorenEditorLineNumberBrush",
            brush => editor.LineNumbersForeground = brush);
    }

    private IBrush? FindThemeBrush(string resourceKey)
    {
        var variant = _isDarkTheme ? ThemeVariant.Dark : ThemeVariant.Light;
        return this.TryFindResource(resourceKey, variant, out var resource) && resource is IBrush brush
            ? brush
            : null;
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

        var theme = _registryOptions.LoadTheme(isDark ? ThemeName.DarkPlus : ThemeName.LightPlus);
        foreach (var pane in Panes)
        {
            pane.SetTheme(theme);
            ApplyEditorTheme(pane.Editor, pane.Installation);
            pane.SetFoldingBrushes(
                FindThemeBrush("TorenTextSubtleBrush"),
                FindThemeBrush("TorenBackgroundBrush"),
                FindThemeBrush("TorenAccentBrush"));
        }

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
        _focusedPane.Editor.Undo();
        _focusedPane.Editor.Focus();
    }

    private void Redo_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        _focusedPane.Editor.Redo();
        _focusedPane.Editor.Focus();
    }

    private void SplitVertical_OnClick(object? sender, RoutedEventArgs eventArgs) =>
        _viewModel.ToggleSplit(EditorSplitOrientation.Vertical);

    private void SplitHorizontal_OnClick(object? sender, RoutedEventArgs eventArgs) =>
        _viewModel.ToggleSplit(EditorSplitOrientation.Horizontal);

    private void CloseSplit_OnClick(object? sender, RoutedEventArgs eventArgs) =>
        _viewModel.Documents.CloseSplit();

    private void ToggleExplorerSync_OnClick(object? sender, RoutedEventArgs eventArgs) =>
        _viewModel.Explorer.SyncWithActiveDocument = !_viewModel.Explorer.SyncWithActiveDocument;

    private async void Breadcrumb_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (sender is Button { Tag: string path })
        {
            await _viewModel.RevealInExplorerAsync(path).ConfigureAwait(true);
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

    private async void WorkspaceTree_OnTapped(object? sender, TappedEventArgs eventArgs)
    {
        // A single click previews a file in the reusable preview tab.
        if (eventArgs.Source is Visual visual
            && visual.FindAncestorOfType<TreeViewItem>() is { DataContext: WorkspaceNodeViewModel node })
        {
            await _viewModel.OpenDocumentAsync(node, preview: true).ConfigureAwait(true);
        }
    }

    private void Explorer_OnNodeRevealed(object? sender, WorkspaceNodeViewModel node)
    {
        // The tree creates containers for freshly expanded nodes a moment later, so retry once.
        Dispatcher.UIThread.Post(() =>
        {
            if (!TryBringIntoView(node))
            {
                DispatcherTimer.RunOnce(() => TryBringIntoView(node), TimeSpan.FromMilliseconds(150));
            }
        }, DispatcherPriority.Background);
    }

    private bool TryBringIntoView(WorkspaceNodeViewModel node)
    {
        var item = WorkspaceTree.GetLogicalDescendants()
            .OfType<TreeViewItem>()
            .FirstOrDefault(candidate => ReferenceEquals(candidate.DataContext, node));
        if (item is null)
        {
            return false;
        }

        item.BringIntoView();
        return true;
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

    private void DocumentTab_OnDoubleTapped(object? sender, TappedEventArgs eventArgs)
    {
        // Double-clicking a preview tab keeps it open.
        if (sender is Control { DataContext: OpenDocumentViewModel document })
        {
            document.IsPreview = false;
            eventArgs.Handled = true;
        }
    }

    private void TogglePinTab_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (sender is Control { DataContext: OpenDocumentViewModel document })
        {
            _viewModel.Documents.TogglePin(document);
            eventArgs.Handled = true;
        }
    }

    private async void CloseDocument_OnClick(object? sender, RoutedEventArgs eventArgs)
    {
        if (sender is Control { DataContext: OpenDocumentViewModel document })
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

        // Activating shows the document in the focused editor before the first await.
        var diagnosticsRefresh = _viewModel.ActivateDocumentAsync(opened.Value);
        var editor = _focusedPane.Editor;
        var line = Math.Clamp(problem.StartLine, 1, editor.Document.LineCount);
        var documentLine = editor.Document.GetLineByNumber(line);
        var column = Math.Clamp(problem.StartColumn, 1, documentLine.Length + 1);
        editor.CaretOffset = documentLine.Offset + column - 1;
        editor.ScrollTo(line, column);
        editor.Focus();
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
