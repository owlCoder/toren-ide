using Avalonia.Controls;
using Avalonia.Input;
using AvaloniaEdit;
using Toren.App.Editor.Views;
using Toren.Language.CSharp.Contracts;
using Toren.Language.CSharp.Models;

namespace Toren.App.Editor.Services;

internal sealed class CSharpSymbolNavigationController
{
    private const int MaxResults = 75;

    private readonly Window _window;
    private readonly TextEditor _editor;
    private readonly Grid _host;
    private readonly CSharpSymbolQuickOpenOverlay _overlay;
    private readonly ICSharpSymbolIndexService _symbolIndexService;
    private readonly ICSharpSymbolSearchService _symbolSearchService;
    private readonly Func<Task<CSharpSemanticContext?>> _semanticContextAccessor;
    private readonly Func<string, Task> _openFileAsync;
    private readonly Action<string> _setStatus;
    private IReadOnlyList<CSharpWorkspaceSymbol> _symbols = [];
    private int _loadGeneration;
    private bool _detached;

    private CSharpSymbolNavigationController(
        Window window,
        ICSharpSymbolIndexService symbolIndexService,
        ICSharpSymbolSearchService symbolSearchService,
        Func<Task<CSharpSemanticContext?>> semanticContextAccessor,
        Func<string, Task> openFileAsync,
        Action<string> setStatus)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
        _symbolIndexService = symbolIndexService ?? throw new ArgumentNullException(nameof(symbolIndexService));
        _symbolSearchService = symbolSearchService ?? throw new ArgumentNullException(nameof(symbolSearchService));
        _semanticContextAccessor = semanticContextAccessor ?? throw new ArgumentNullException(nameof(semanticContextAccessor));
        _openFileAsync = openFileAsync ?? throw new ArgumentNullException(nameof(openFileAsync));
        _setStatus = setStatus ?? throw new ArgumentNullException(nameof(setStatus));
        _editor = window.FindControl<TextEditor>("DocumentEditor")
            ?? throw new InvalidOperationException("The document editor could not be located.");
        _host = window.Content as Grid
            ?? throw new InvalidOperationException("The main window content must be hosted by a Grid.");
        _overlay = new CSharpSymbolQuickOpenOverlay();
        Grid.SetRow(_overlay, 2);
        Grid.SetColumn(_overlay, 2);
        _host.Children.Add(_overlay);

        _overlay.QueryChanged += Overlay_OnQueryChanged;
        _overlay.SymbolRequested += Overlay_OnSymbolRequested;
        _overlay.CloseRequested += Overlay_OnCloseRequested;
        _window.KeyDown += Window_OnKeyDown;
        _window.Closed += Window_OnClosed;
    }

    public static void Attach(
        Window window,
        ICSharpSymbolIndexService symbolIndexService,
        ICSharpSymbolSearchService symbolSearchService,
        Func<Task<CSharpSemanticContext?>> semanticContextAccessor,
        Func<string, Task> openFileAsync,
        Action<string> setStatus)
    {
        _ = new CSharpSymbolNavigationController(
            window,
            symbolIndexService,
            symbolSearchService,
            semanticContextAccessor,
            openFileAsync,
            setStatus);
    }

    private async void Window_OnKeyDown(object? sender, KeyEventArgs eventArgs)
    {
        var commandModifier = eventArgs.KeyModifiers.HasFlag(KeyModifiers.Control)
            || eventArgs.KeyModifiers.HasFlag(KeyModifiers.Meta);
        if (!commandModifier || eventArgs.Key != Key.T)
        {
            return;
        }

        eventArgs.Handled = true;
        await ShowAsync().ConfigureAwait(true);
    }

    private async Task ShowAsync()
    {
        var generation = ++_loadGeneration;
        _symbols = [];
        _overlay.SetResults([]);
        _overlay.SetStatus("Indexing workspace symbols…");
        _overlay.ShowOverlay();

        var context = await _semanticContextAccessor().ConfigureAwait(true);
        if (generation != _loadGeneration || !_overlay.IsVisible)
        {
            return;
        }

        if (context is null)
        {
            _overlay.SetStatus("Open a .NET workspace to browse C# symbols.");
            return;
        }

        _symbols = await _symbolIndexService.GetSymbolsAsync(context).ConfigureAwait(true);
        if (generation != _loadGeneration || !_overlay.IsVisible)
        {
            return;
        }

        RefreshResults();
    }

    private void RefreshResults()
    {
        var results = _symbolSearchService.Search(_symbols, _overlay.Query, MaxResults);
        _overlay.SetResults(results);
        _overlay.SetStatus(results.Count == 0
            ? "No matching symbols"
            : $"{results.Count} shown · {_symbols.Count} workspace symbols");
    }

    private void Overlay_OnQueryChanged(object? sender, EventArgs eventArgs)
    {
        if (_symbols.Count > 0)
        {
            RefreshResults();
        }
    }

    private async void Overlay_OnSymbolRequested(object? sender, EventArgs eventArgs)
    {
        if (_overlay.SelectedSymbol is not { Location.FilePath: { Length: > 0 } filePath } symbol)
        {
            return;
        }

        CloseOverlay();
        await _openFileAsync(filePath).ConfigureAwait(true);
        NavigateTo(symbol.Location);
    }

    private void NavigateTo(CSharpSourceLocation location)
    {
        var line = Math.Clamp(location.Line, 1, _editor.Document.LineCount);
        var documentLine = _editor.Document.GetLineByNumber(line);
        var column = Math.Clamp(location.Column, 1, documentLine.Length + 1);
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
        _loadGeneration++;
        _overlay.HideOverlay();
        _editor.Focus();
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
        _overlay.SymbolRequested -= Overlay_OnSymbolRequested;
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
