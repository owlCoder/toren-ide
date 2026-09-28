using System.ComponentModel;
using Avalonia.Controls;
using AvaloniaEdit;
using Toren.Language.CSharp.Contracts;
using Toren.Language.CSharp.Models;

namespace Toren.App.Editor.Services;

internal sealed class CSharpSemanticHighlightingController
{
    private static readonly TimeSpan RefreshDelay = TimeSpan.FromMilliseconds(180);

    private readonly Window _window;
    private readonly TextEditor _editor;
    private readonly ICSharpSemanticHighlightingService _highlightingService;
    private readonly Func<CSharpSourceDocument?> _activeDocumentAccessor;
    private readonly Func<Task<CSharpSemanticContext?>> _semanticContextAccessor;
    private readonly INotifyPropertyChanged _documentState;
    private readonly CSharpSemanticHighlightTransformer _transformer = new();
    private CancellationTokenSource? _pendingRefresh;
    private bool _detached;

    private CSharpSemanticHighlightingController(
        Window window,
        ICSharpSemanticHighlightingService highlightingService,
        Func<CSharpSourceDocument?> activeDocumentAccessor,
        Func<Task<CSharpSemanticContext?>> semanticContextAccessor,
        INotifyPropertyChanged documentState)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
        _highlightingService = highlightingService ?? throw new ArgumentNullException(nameof(highlightingService));
        _activeDocumentAccessor = activeDocumentAccessor ?? throw new ArgumentNullException(nameof(activeDocumentAccessor));
        _semanticContextAccessor = semanticContextAccessor ?? throw new ArgumentNullException(nameof(semanticContextAccessor));
        _documentState = documentState ?? throw new ArgumentNullException(nameof(documentState));
        _editor = window.FindControl<TextEditor>("DocumentEditor")
            ?? throw new InvalidOperationException("The document editor could not be located.");

        _editor.TextArea.TextView.LineTransformers.Add(_transformer);
        _editor.TextChanged += Editor_OnTextChanged;
        _documentState.PropertyChanged += DocumentState_OnPropertyChanged;
        _window.Closed += Window_OnClosed;
        QueueRefresh(debounce: false);
    }

    public static void Attach(
        Window window,
        ICSharpSemanticHighlightingService highlightingService,
        Func<CSharpSourceDocument?> activeDocumentAccessor,
        Func<Task<CSharpSemanticContext?>> semanticContextAccessor,
        INotifyPropertyChanged documentState)
    {
        _ = new CSharpSemanticHighlightingController(
            window,
            highlightingService,
            activeDocumentAccessor,
            semanticContextAccessor,
            documentState);
    }

    private void Editor_OnTextChanged(object? sender, EventArgs eventArgs)
    {
        QueueRefresh(debounce: true);
    }

    private void DocumentState_OnPropertyChanged(object? sender, PropertyChangedEventArgs eventArgs)
    {
        QueueRefresh(debounce: false);
    }

    private void QueueRefresh(bool debounce)
    {
        _pendingRefresh?.Cancel();
        var cancellation = new CancellationTokenSource();
        _pendingRefresh = cancellation;
        _ = RefreshAsync(cancellation, debounce);
    }

    private async Task RefreshAsync(CancellationTokenSource cancellation, bool debounce)
    {
        try
        {
            if (debounce)
            {
                await Task.Delay(RefreshDelay, cancellation.Token).ConfigureAwait(true);
            }

            var activeDocument = _activeDocumentAccessor();
            if (activeDocument is null
                || !Path.GetExtension(activeDocument.Path).Equals(".cs", StringComparison.OrdinalIgnoreCase))
            {
                ApplyHighlights([]);
                return;
            }

            var analyzedPath = activeDocument.Path;
            var analyzedText = activeDocument.Text;
            var context = await _semanticContextAccessor().ConfigureAwait(true)
                ?? new CSharpSemanticContext(
                    analyzedPath,
                    [new CSharpSourceDocument(analyzedPath, analyzedText)]);
            var highlights = await _highlightingService
                .GetHighlightsAsync(context, cancellation.Token)
                .ConfigureAwait(true);
            var currentDocument = _activeDocumentAccessor();
            if (cancellation.IsCancellationRequested
                || currentDocument is null
                || !currentDocument.Path.Equals(analyzedPath, StringComparison.Ordinal)
                || !currentDocument.Text.Equals(analyzedText, StringComparison.Ordinal))
            {
                return;
            }

            ApplyHighlights(highlights);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
        }
        finally
        {
            if (ReferenceEquals(_pendingRefresh, cancellation))
            {
                _pendingRefresh = null;
            }

            cancellation.Dispose();
        }
    }

    private void ApplyHighlights(IReadOnlyList<CSharpSemanticHighlight> highlights)
    {
        if (_detached)
        {
            return;
        }

        _transformer.SetHighlights(highlights);
        _editor.TextArea.TextView.Redraw();
    }

    private void Detach()
    {
        if (_detached)
        {
            return;
        }

        _detached = true;
        _pendingRefresh?.Cancel();
        _editor.TextChanged -= Editor_OnTextChanged;
        _documentState.PropertyChanged -= DocumentState_OnPropertyChanged;
        _window.Closed -= Window_OnClosed;
        _editor.TextArea.TextView.LineTransformers.Remove(_transformer);
    }

    private void Window_OnClosed(object? sender, EventArgs eventArgs)
    {
        Detach();
    }
}
