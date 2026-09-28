using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using AvaloniaEdit;
using Toren.App.Editor.Views;
using Toren.Language.CSharp.Contracts;
using Toren.Language.CSharp.Models;

namespace Toren.App.Editor.Services;

internal sealed class CSharpQuickInfoController
{
    private static readonly TimeSpan HoverDelay = TimeSpan.FromMilliseconds(450);

    private readonly Window _window;
    private readonly TextEditor _editor;
    private readonly Grid _host;
    private readonly CSharpQuickInfoView _view;
    private readonly ICSharpSemanticService _semanticService;
    private readonly Func<CSharpSourceDocument?> _activeDocumentAccessor;
    private readonly Func<Task<CSharpSemanticContext?>> _semanticContextAccessor;
    private CancellationTokenSource? _hoverCancellation;
    private HoverTarget? _lastTarget;
    private bool _detached;

    private CSharpQuickInfoController(
        Window window,
        ICSharpSemanticService semanticService,
        Func<CSharpSourceDocument?> activeDocumentAccessor,
        Func<Task<CSharpSemanticContext?>> semanticContextAccessor)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
        _semanticService = semanticService ?? throw new ArgumentNullException(nameof(semanticService));
        _activeDocumentAccessor = activeDocumentAccessor ?? throw new ArgumentNullException(nameof(activeDocumentAccessor));
        _semanticContextAccessor = semanticContextAccessor ?? throw new ArgumentNullException(nameof(semanticContextAccessor));
        _editor = window.FindControl<TextEditor>("DocumentEditor")
            ?? throw new InvalidOperationException("The document editor could not be located.");
        _host = _editor.Parent as Grid
            ?? throw new InvalidOperationException("The document editor must be hosted by a Grid.");
        _view = new CSharpQuickInfoView
        {
            IsVisible = false,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
        };
        _host.Children.Add(_view);

        _editor.TextArea.TextView.PointerMoved += TextView_OnPointerMoved;
        _editor.TextArea.TextView.PointerExited += TextView_OnPointerExited;
        _editor.TextArea.TextView.PointerPressed += TextView_OnPointerPressed;
        _editor.TextChanged += Editor_OnTextChanged;
        _window.Closed += Window_OnClosed;
    }

    public static void Attach(
        Window window,
        ICSharpSemanticService semanticService,
        Func<CSharpSourceDocument?> activeDocumentAccessor,
        Func<Task<CSharpSemanticContext?>> semanticContextAccessor)
    {
        _ = new CSharpQuickInfoController(
            window,
            semanticService,
            activeDocumentAccessor,
            semanticContextAccessor);
    }

    private async void TextView_OnPointerMoved(object? sender, PointerEventArgs eventArgs)
    {
        var activeDocument = _activeDocumentAccessor();
        if (activeDocument is null || !_editor.IsVisible)
        {
            Hide();
            return;
        }

        var textView = _editor.TextArea.TextView;
        var viewPoint = eventArgs.GetPosition(textView);
        var documentPoint = viewPoint + textView.ScrollOffset;
        var position = textView.GetPositionFloor(documentPoint);
        if (position is null || position.Value.Line < 1 || position.Value.Column < 1)
        {
            Hide();
            return;
        }

        var target = new HoverTarget(activeDocument.Path, position.Value.Line, position.Value.Column);
        if (_lastTarget == target && _view.IsVisible)
        {
            PositionView(eventArgs.GetPosition(_host));
            return;
        }

        _lastTarget = target;
        CancelPendingHover();
        var cancellation = new CancellationTokenSource();
        _hoverCancellation = cancellation;

        try
        {
            await Task.Delay(HoverDelay, cancellation.Token).ConfigureAwait(true);
            var currentDocument = _activeDocumentAccessor();
            if (currentDocument is null
                || !currentDocument.Path.Equals(target.FilePath, StringComparison.Ordinal)
                || cancellation.IsCancellationRequested)
            {
                return;
            }

            var symbol = await ResolveSymbolAsync(
                    currentDocument,
                    target.Line,
                    target.Column,
                    cancellation.Token)
                .ConfigureAwait(true);
            if (symbol is null || cancellation.IsCancellationRequested)
            {
                Hide();
                return;
            }

            _view.SetSymbol(symbol);
            PositionView(eventArgs.GetPosition(_host));
            _view.IsVisible = true;
        }
        catch (OperationCanceledException)
        {
            // Pointer moved before hover resolution completed.
        }
    }

    private async Task<CSharpSymbolInfo?> ResolveSymbolAsync(
        CSharpSourceDocument activeDocument,
        int line,
        int column,
        CancellationToken cancellationToken)
    {
        var context = await _semanticContextAccessor().ConfigureAwait(true);
        if (context is not null)
        {
            var contextualSymbol = await _semanticService
                .GetSymbolAsync(context, line, column, cancellationToken)
                .ConfigureAwait(true);
            if (contextualSymbol is not null)
            {
                return contextualSymbol;
            }
        }

        return await _semanticService
            .GetSymbolAsync(activeDocument.Text, line, column, cancellationToken)
            .ConfigureAwait(true);
    }

    private void PositionView(Point pointerPosition)
    {
        const double horizontalOffset = 14;
        const double verticalOffset = 18;
        const double estimatedWidth = 420;
        const double estimatedHeight = 86;

        var left = Math.Clamp(
            pointerPosition.X + horizontalOffset,
            0,
            Math.Max(0, _host.Bounds.Width - estimatedWidth));
        var top = Math.Clamp(
            pointerPosition.Y + verticalOffset,
            0,
            Math.Max(0, _host.Bounds.Height - estimatedHeight));
        _view.Margin = new Thickness(left, top, 0, 0);
    }

    private void TextView_OnPointerExited(object? sender, PointerEventArgs eventArgs)
    {
        Hide();
    }

    private void TextView_OnPointerPressed(object? sender, PointerPressedEventArgs eventArgs)
    {
        Hide();
    }

    private void Editor_OnTextChanged(object? sender, EventArgs eventArgs)
    {
        Hide();
    }

    private void Hide()
    {
        CancelPendingHover();
        _lastTarget = null;
        _view.IsVisible = false;
    }

    private void CancelPendingHover()
    {
        if (_hoverCancellation is null)
        {
            return;
        }

        _hoverCancellation.Cancel();
        _hoverCancellation.Dispose();
        _hoverCancellation = null;
    }

    private void Detach()
    {
        if (_detached)
        {
            return;
        }

        _detached = true;
        CancelPendingHover();
        _editor.TextArea.TextView.PointerMoved -= TextView_OnPointerMoved;
        _editor.TextArea.TextView.PointerExited -= TextView_OnPointerExited;
        _editor.TextArea.TextView.PointerPressed -= TextView_OnPointerPressed;
        _editor.TextChanged -= Editor_OnTextChanged;
        _window.Closed -= Window_OnClosed;
        _host.Children.Remove(_view);
    }

    private void Window_OnClosed(object? sender, EventArgs eventArgs)
    {
        Detach();
    }

    private sealed record HoverTarget(string FilePath, int Line, int Column);
}
