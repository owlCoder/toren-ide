using Avalonia.Controls;
using Avalonia.Input;
using AvaloniaEdit;
using Toren.Core.IO;
using Toren.Language.CSharp.Contracts;
using Toren.Language.CSharp.Models;

namespace Toren.App.Editor.Services;

internal sealed class CSharpNavigationController
{
    private static readonly StringComparison PathComparison = FileSystemPath.Comparison;

    private readonly Window _window;
    private readonly TextEditor _editor;
    private readonly ICSharpSemanticService _semanticService;
    private readonly Func<CSharpSourceDocument?> _activeDocumentAccessor;
    private readonly Func<Task<CSharpSemanticContext?>> _semanticContextAccessor;
    private readonly Func<string, Task> _openFileAsync;
    private readonly Stack<NavigationPoint> _navigationHistory = new();
    private bool _detached;

    private CSharpNavigationController(
        Window window,
        ICSharpSemanticService semanticService,
        Func<CSharpSourceDocument?> activeDocumentAccessor,
        Func<Task<CSharpSemanticContext?>> semanticContextAccessor,
        Func<string, Task> openFileAsync)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
        _semanticService = semanticService ?? throw new ArgumentNullException(nameof(semanticService));
        _activeDocumentAccessor = activeDocumentAccessor ?? throw new ArgumentNullException(nameof(activeDocumentAccessor));
        _semanticContextAccessor = semanticContextAccessor ?? throw new ArgumentNullException(nameof(semanticContextAccessor));
        _openFileAsync = openFileAsync ?? throw new ArgumentNullException(nameof(openFileAsync));
        _editor = window.FindControl<TextEditor>("DocumentEditor")
            ?? throw new InvalidOperationException("The document editor could not be located.");

        _window.KeyDown += Window_OnKeyDown;
        _window.Closed += Window_OnClosed;
    }

    public static void Attach(
        Window window,
        ICSharpSemanticService semanticService,
        Func<CSharpSourceDocument?> activeDocumentAccessor,
        Func<Task<CSharpSemanticContext?>> semanticContextAccessor,
        Func<string, Task> openFileAsync)
    {
        _ = new CSharpNavigationController(
            window,
            semanticService,
            activeDocumentAccessor,
            semanticContextAccessor,
            openFileAsync);
    }

    private async void Window_OnKeyDown(object? sender, KeyEventArgs eventArgs)
    {
        if (eventArgs.Key == Key.F12 && _editor.IsVisible)
        {
            eventArgs.Handled = true;
            await GoToDefinitionAsync().ConfigureAwait(true);
            return;
        }

        if (eventArgs.Key == Key.Left
            && eventArgs.KeyModifiers.HasFlag(KeyModifiers.Alt)
            && _navigationHistory.Count > 0)
        {
            eventArgs.Handled = true;
            await NavigateBackAsync().ConfigureAwait(true);
        }
    }

    private async Task GoToDefinitionAsync()
    {
        var activeDocument = _activeDocumentAccessor();
        if (activeDocument is null)
        {
            return;
        }

        var origin = _editor.Document.GetLocation(_editor.CaretOffset);
        var symbol = await ResolveSymbolAsync(activeDocument, origin.Line, origin.Column).ConfigureAwait(true);
        if (symbol?.Definition is null)
        {
            return;
        }

        var definitionPath = string.IsNullOrWhiteSpace(symbol.Definition.FilePath)
            ? activeDocument.Path
            : symbol.Definition.FilePath;
        _navigationHistory.Push(new NavigationPoint(activeDocument.Path, origin.Line, origin.Column));
        await NavigateToAsync(
            new CSharpSourceLocation(definitionPath, symbol.Definition.Line, symbol.Definition.Column))
            .ConfigureAwait(true);
    }

    private async Task<CSharpSymbolInfo?> ResolveSymbolAsync(
        CSharpSourceDocument activeDocument,
        int line,
        int column)
    {
        var context = await _semanticContextAccessor().ConfigureAwait(true);
        if (context is not null)
        {
            var contextualSymbol = await _semanticService
                .GetSymbolAsync(context, line, column)
                .ConfigureAwait(true);
            if (contextualSymbol is not null)
            {
                return contextualSymbol;
            }
        }

        return await _semanticService
            .GetSymbolAsync(activeDocument.Text, line, column)
            .ConfigureAwait(true);
    }

    private async Task NavigateToAsync(CSharpSourceLocation location)
    {
        if (!string.IsNullOrWhiteSpace(location.FilePath))
        {
            var activeDocument = _activeDocumentAccessor();
            if (activeDocument is null || !activeDocument.Path.Equals(location.FilePath, PathComparison))
            {
                await _openFileAsync(location.FilePath).ConfigureAwait(true);
                activeDocument = _activeDocumentAccessor();
                if (activeDocument is null || !activeDocument.Path.Equals(location.FilePath, PathComparison))
                {
                    return;
                }
            }
        }

        var line = Math.Clamp(location.Line, 1, _editor.Document.LineCount);
        var documentLine = _editor.Document.GetLineByNumber(line);
        var column = Math.Clamp(location.Column, 1, documentLine.Length + 1);
        _editor.CaretOffset = documentLine.Offset + column - 1;
        _editor.ScrollTo(line, column);
        _editor.Focus();
    }

    private async Task NavigateBackAsync()
    {
        var point = _navigationHistory.Pop();
        await NavigateToAsync(new CSharpSourceLocation(point.FilePath, point.Line, point.Column))
            .ConfigureAwait(true);
    }

    private void Detach()
    {
        if (_detached)
        {
            return;
        }

        _detached = true;
        _window.KeyDown -= Window_OnKeyDown;
        _window.Closed -= Window_OnClosed;
    }

    private void Window_OnClosed(object? sender, EventArgs eventArgs)
    {
        Detach();
    }

    private sealed record NavigationPoint(string FilePath, int Line, int Column);
}
