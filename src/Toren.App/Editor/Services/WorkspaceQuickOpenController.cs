using Avalonia.Controls;
using Avalonia.Input;
using Toren.App.Editor.Views;
using Toren.Workspaces.Contracts;
using Toren.Workspaces.Models;

namespace Toren.App.Editor.Services;

internal sealed class WorkspaceQuickOpenController
{
    private const int MaxResults = 75;

    private readonly Window _window;
    private readonly Grid _host;
    private readonly WorkspaceQuickOpenOverlay _overlay;
    private readonly IWorkspaceFileProvider _fileProvider;
    private readonly IWorkspaceFileSearchService _searchService;
    private readonly Func<string?> _workspacePathAccessor;
    private readonly Func<string, Task> _openFileAsync;
    private readonly Action<string> _setStatus;
    private IReadOnlyList<WorkspaceFileEntry> _workspaceFiles = [];
    private int _loadGeneration;
    private bool _detached;

    private WorkspaceQuickOpenController(
        Window window,
        IWorkspaceFileProvider fileProvider,
        IWorkspaceFileSearchService searchService,
        Func<string?> workspacePathAccessor,
        Func<string, Task> openFileAsync,
        Action<string> setStatus)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
        _fileProvider = fileProvider ?? throw new ArgumentNullException(nameof(fileProvider));
        _searchService = searchService ?? throw new ArgumentNullException(nameof(searchService));
        _workspacePathAccessor = workspacePathAccessor ?? throw new ArgumentNullException(nameof(workspacePathAccessor));
        _openFileAsync = openFileAsync ?? throw new ArgumentNullException(nameof(openFileAsync));
        _setStatus = setStatus ?? throw new ArgumentNullException(nameof(setStatus));
        _host = window.Content as Grid
            ?? throw new InvalidOperationException("The main window content must be hosted by a Grid.");
        _overlay = new WorkspaceQuickOpenOverlay();
        Grid.SetRow(_overlay, 2);
        Grid.SetColumn(_overlay, 2);
        _host.Children.Add(_overlay);

        _overlay.QueryChanged += Overlay_OnQueryChanged;
        _overlay.FileRequested += Overlay_OnFileRequested;
        _overlay.CloseRequested += Overlay_OnCloseRequested;
        _window.KeyDown += Window_OnKeyDown;
        _window.Closed += Window_OnClosed;
    }

    public static void Attach(
        Window window,
        IWorkspaceFileProvider fileProvider,
        IWorkspaceFileSearchService searchService,
        Func<string?> workspacePathAccessor,
        Func<string, Task> openFileAsync,
        Action<string> setStatus)
    {
        _ = new WorkspaceQuickOpenController(
            window,
            fileProvider,
            searchService,
            workspacePathAccessor,
            openFileAsync,
            setStatus);
    }

    private async void Window_OnKeyDown(object? sender, KeyEventArgs eventArgs)
    {
        var commandModifier = eventArgs.KeyModifiers.HasFlag(KeyModifiers.Control)
            || eventArgs.KeyModifiers.HasFlag(KeyModifiers.Meta);
        if (!commandModifier || eventArgs.Key != Key.P)
        {
            return;
        }

        eventArgs.Handled = true;
        await ShowAsync().ConfigureAwait(true);
    }

    private async Task ShowAsync()
    {
        var workspacePath = _workspacePathAccessor();
        if (string.IsNullOrWhiteSpace(workspacePath))
        {
            _setStatus("Open a workspace before using Go to File.");
            return;
        }

        var generation = ++_loadGeneration;
        _workspaceFiles = [];
        _overlay.SetResults([]);
        _overlay.SetStatus("Indexing workspace files…");
        _overlay.ShowOverlay();

        var files = await _fileProvider.GetFilesAsync(workspacePath).ConfigureAwait(true);
        if (generation != _loadGeneration || !_overlay.IsVisible)
        {
            return;
        }

        if (!files.IsSuccess)
        {
            _overlay.SetStatus(files.Error.Message);
            _setStatus(files.Error.Message);
            return;
        }

        _workspaceFiles = files.Value;
        RefreshResults();
    }

    private void RefreshResults()
    {
        var results = _searchService.Search(_workspaceFiles, _overlay.Query, MaxResults);
        _overlay.SetResults(results);
        _overlay.SetStatus(results.Count == 0
            ? "No matching files"
            : $"{results.Count} shown · {_workspaceFiles.Count} workspace files");
    }

    private void Overlay_OnQueryChanged(object? sender, EventArgs eventArgs)
    {
        if (_workspaceFiles.Count > 0)
        {
            RefreshResults();
        }
    }

    private async void Overlay_OnFileRequested(object? sender, EventArgs eventArgs)
    {
        if (_overlay.SelectedFile is not { } file)
        {
            return;
        }

        CloseOverlay();
        await _openFileAsync(file.Path).ConfigureAwait(true);
    }

    private void Overlay_OnCloseRequested(object? sender, EventArgs eventArgs)
    {
        CloseOverlay();
    }

    private void CloseOverlay()
    {
        _loadGeneration++;
        _overlay.HideOverlay();
    }

    private void Detach()
    {
        if (_detached)
        {
            return;
        }

        _detached = true;
        _loadGeneration++;
        _overlay.QueryChanged -= Overlay_OnQueryChanged;
        _overlay.FileRequested -= Overlay_OnFileRequested;
        _overlay.CloseRequested -= Overlay_OnCloseRequested;
        _window.KeyDown -= Window_OnKeyDown;
        _window.Closed -= Window_OnClosed;
        _host.Children.Remove(_overlay);
    }

    private void Window_OnClosed(object? sender, EventArgs eventArgs)
    {
        Detach();
    }
}
