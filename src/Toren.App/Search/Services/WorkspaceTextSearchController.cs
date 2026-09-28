using Avalonia.Controls;
using Avalonia.Input;
using AvaloniaEdit;
using Toren.App.Search.Contracts;
using Toren.App.Search.Models;
using Toren.App.Search.Views;

namespace Toren.App.Search.Services;

internal sealed class WorkspaceTextSearchController
{
    private static readonly TimeSpan SearchDebounce = TimeSpan.FromMilliseconds(220);

    private readonly Window _window;
    private readonly Grid _host;
    private readonly TextEditor _editor;
    private readonly WorkspaceTextSearchOverlay _overlay;
    private readonly IWorkspaceTextSearchService _searchService;
    private readonly Func<string?> _workspacePathAccessor;
    private readonly Func<IReadOnlyDictionary<string, string>> _textOverridesAccessor;
    private readonly Func<string, Task> _openFileAsync;
    private readonly Action<string> _setStatus;
    private CancellationTokenSource? _searchCancellation;
    private bool _detached;

    private WorkspaceTextSearchController(
        Window window,
        IWorkspaceTextSearchService searchService,
        Func<string?> workspacePathAccessor,
        Func<IReadOnlyDictionary<string, string>> textOverridesAccessor,
        Func<string, Task> openFileAsync,
        Action<string> setStatus)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
        _searchService = searchService ?? throw new ArgumentNullException(nameof(searchService));
        _workspacePathAccessor = workspacePathAccessor
            ?? throw new ArgumentNullException(nameof(workspacePathAccessor));
        _textOverridesAccessor = textOverridesAccessor
            ?? throw new ArgumentNullException(nameof(textOverridesAccessor));
        _openFileAsync = openFileAsync ?? throw new ArgumentNullException(nameof(openFileAsync));
        _setStatus = setStatus ?? throw new ArgumentNullException(nameof(setStatus));
        _host = window.Content as Grid
            ?? throw new InvalidOperationException("The main window content must be hosted by a Grid.");
        _editor = window.FindControl<TextEditor>("DocumentEditor")
            ?? throw new InvalidOperationException("The document editor could not be located.");
        _overlay = new WorkspaceTextSearchOverlay();
        Grid.SetRow(_overlay, 2);
        Grid.SetColumn(_overlay, 2);
        _host.Children.Add(_overlay);

        _overlay.QueryChanged += Overlay_OnQueryChanged;
        _overlay.SearchOptionsChanged += Overlay_OnSearchOptionsChanged;
        _overlay.ResultRequested += Overlay_OnResultRequested;
        _overlay.CloseRequested += Overlay_OnCloseRequested;
        _window.KeyDown += Window_OnKeyDown;
        _window.Closed += Window_OnClosed;
    }

    public static void Attach(
        Window window,
        IWorkspaceTextSearchService searchService,
        Func<string?> workspacePathAccessor,
        Func<IReadOnlyDictionary<string, string>> textOverridesAccessor,
        Func<string, Task> openFileAsync,
        Action<string> setStatus)
    {
        _ = new WorkspaceTextSearchController(
            window,
            searchService,
            workspacePathAccessor,
            textOverridesAccessor,
            openFileAsync,
            setStatus);
    }

    private void Window_OnKeyDown(object? sender, KeyEventArgs eventArgs)
    {
        var commandModifier = eventArgs.KeyModifiers.HasFlag(KeyModifiers.Control)
            || eventArgs.KeyModifiers.HasFlag(KeyModifiers.Meta);
        if (!commandModifier
            || !eventArgs.KeyModifiers.HasFlag(KeyModifiers.Shift)
            || eventArgs.Key != Key.F)
        {
            return;
        }

        eventArgs.Handled = true;
        ShowOverlay();
    }

    private void ShowOverlay()
    {
        if (string.IsNullOrWhiteSpace(_workspacePathAccessor()))
        {
            _setStatus("Open a workspace before using Find in Files.");
            return;
        }

        _overlay.SetResults([]);
        _overlay.SetStatus("Type to search workspace text");
        _overlay.ShowOverlay();
    }

    private async void Overlay_OnQueryChanged(object? sender, EventArgs eventArgs)
    {
        await RefreshResultsAsync(debounce: true).ConfigureAwait(true);
    }

    private async void Overlay_OnSearchOptionsChanged(object? sender, EventArgs eventArgs)
    {
        await RefreshResultsAsync(debounce: true).ConfigureAwait(true);
    }

    private async Task RefreshResultsAsync(bool debounce)
    {
        CancelPendingSearch();
        if (!_overlay.IsVisible)
        {
            return;
        }

        var workspacePath = _workspacePathAccessor();
        var query = _overlay.Query;
        if (string.IsNullOrWhiteSpace(workspacePath) || string.IsNullOrWhiteSpace(query))
        {
            _overlay.SetResults([]);
            _overlay.SetStatus("Type to search workspace text");
            return;
        }

        var cancellation = new CancellationTokenSource();
        _searchCancellation = cancellation;
        try
        {
            if (debounce)
            {
                await Task.Delay(SearchDebounce, cancellation.Token).ConfigureAwait(true);
            }

            _overlay.SetStatus("Searching…");
            var options = new WorkspaceTextSearchOptions(
                MatchCase: _overlay.MatchCase,
                MatchWholeWord: _overlay.MatchWholeWord,
                UseRegularExpression: _overlay.UseRegularExpression,
                IncludePatterns: _overlay.IncludePatterns,
                ExcludePatterns: _overlay.ExcludePatterns);
            var result = await _searchService
                .SearchAsync(
                    workspacePath,
                    query,
                    options,
                    _textOverridesAccessor(),
                    cancellationToken: cancellation.Token)
                .ConfigureAwait(true);
            if (cancellation.IsCancellationRequested || !_overlay.IsVisible)
            {
                return;
            }

            if (!result.IsSuccess)
            {
                _overlay.SetResults([]);
                _overlay.SetStatus(result.Error.Message);
                _setStatus(result.Error.Message);
                return;
            }

            _overlay.SetResults(result.Value);
            _overlay.SetStatus(result.Value.Count == 0
                ? "No matching text"
                : $"{result.Value.Count} result{(result.Value.Count == 1 ? string.Empty : "s")}");
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        finally
        {
            if (ReferenceEquals(_searchCancellation, cancellation))
            {
                _searchCancellation = null;
            }

            cancellation.Dispose();
        }
    }

    private async void Overlay_OnResultRequested(object? sender, EventArgs eventArgs)
    {
        if (_overlay.SelectedResult is not { } result)
        {
            return;
        }

        CloseOverlay();
        await _openFileAsync(result.FilePath).ConfigureAwait(true);
        NavigateTo(result);
    }

    private void NavigateTo(WorkspaceTextSearchResult result)
    {
        if (!_editor.IsVisible || _editor.Document.LineCount == 0)
        {
            return;
        }

        var line = Math.Clamp(result.Line, 1, _editor.Document.LineCount);
        var documentLine = _editor.Document.GetLineByNumber(line);
        var column = Math.Clamp(result.Column, 1, documentLine.Length + 1);
        _editor.CaretOffset = documentLine.Offset + column - 1;
        _editor.ScrollTo(line, column);
        _editor.Focus();
    }

    private void Overlay_OnCloseRequested(object? sender, EventArgs eventArgs)
    {
        CloseOverlay();
    }

    private void CloseOverlay()
    {
        CancelPendingSearch();
        _overlay.HideOverlay();
        _editor.Focus();
    }

    private void CancelPendingSearch()
    {
        if (_searchCancellation is null)
        {
            return;
        }

        _searchCancellation.Cancel();
        _searchCancellation.Dispose();
        _searchCancellation = null;
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
        CancelPendingSearch();
        _overlay.QueryChanged -= Overlay_OnQueryChanged;
        _overlay.SearchOptionsChanged -= Overlay_OnSearchOptionsChanged;
        _overlay.ResultRequested -= Overlay_OnResultRequested;
        _overlay.CloseRequested -= Overlay_OnCloseRequested;
        _window.KeyDown -= Window_OnKeyDown;
        _window.Closed -= Window_OnClosed;
        _host.Children.Remove(_overlay);
    }
}
