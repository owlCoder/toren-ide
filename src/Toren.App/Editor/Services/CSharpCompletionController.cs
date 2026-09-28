using Avalonia.Controls;
using Avalonia.Input;
using AvaloniaEdit;
using AvaloniaEdit.CodeCompletion;
using Toren.App.Editor.Models;
using Toren.Language.CSharp.Contracts;
using Toren.Language.CSharp.Models;

namespace Toren.App.Editor.Services;

internal sealed class CSharpCompletionController
{
    private readonly Window _window;
    private readonly TextEditor _editor;
    private readonly ICSharpCompletionService _completionService;
    private readonly Func<CSharpSourceDocument?> _activeDocumentAccessor;
    private readonly Func<Task<CSharpSemanticContext?>> _semanticContextAccessor;
    private CompletionWindow? _completionWindow;
    private CancellationTokenSource? _completionCancellation;
    private bool _detached;

    private CSharpCompletionController(
        Window window,
        ICSharpCompletionService completionService,
        Func<CSharpSourceDocument?> activeDocumentAccessor,
        Func<Task<CSharpSemanticContext?>> semanticContextAccessor)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
        _completionService = completionService ?? throw new ArgumentNullException(nameof(completionService));
        _activeDocumentAccessor = activeDocumentAccessor ?? throw new ArgumentNullException(nameof(activeDocumentAccessor));
        _semanticContextAccessor = semanticContextAccessor ?? throw new ArgumentNullException(nameof(semanticContextAccessor));
        _editor = window.FindControl<TextEditor>("DocumentEditor")
            ?? throw new InvalidOperationException("The document editor could not be located.");

        _editor.TextArea.TextEntered += TextArea_OnTextEntered;
        _editor.TextArea.TextEntering += TextArea_OnTextEntering;
        _editor.TextArea.KeyDown += TextArea_OnKeyDown;
        _window.Closed += Window_OnClosed;
    }

    public static void Attach(
        Window window,
        ICSharpCompletionService completionService,
        Func<CSharpSourceDocument?> activeDocumentAccessor,
        Func<Task<CSharpSemanticContext?>> semanticContextAccessor)
    {
        _ = new CSharpCompletionController(
            window,
            completionService,
            activeDocumentAccessor,
            semanticContextAccessor);
    }

    private async void TextArea_OnTextEntered(object? sender, TextInputEventArgs eventArgs)
    {
        if (eventArgs.Text == ".")
        {
            await ShowCompletionAsync(memberAccessTrigger: true).ConfigureAwait(true);
        }
    }

    private void TextArea_OnTextEntering(object? sender, TextInputEventArgs eventArgs)
    {
        if (_completionWindow is null || string.IsNullOrEmpty(eventArgs.Text))
        {
            return;
        }

        var character = eventArgs.Text[0];
        if (!char.IsLetterOrDigit(character) && character != '_')
        {
            _completionWindow.CompletionList.RequestInsertion(eventArgs);
        }
    }

    private async void TextArea_OnKeyDown(object? sender, KeyEventArgs eventArgs)
    {
        var commandModifier = eventArgs.KeyModifiers.HasFlag(KeyModifiers.Control)
            || eventArgs.KeyModifiers.HasFlag(KeyModifiers.Meta);
        if (!commandModifier || eventArgs.Key != Key.Space)
        {
            return;
        }

        eventArgs.Handled = true;
        await ShowCompletionAsync(memberAccessTrigger: false).ConfigureAwait(true);
    }

    private async Task ShowCompletionAsync(bool memberAccessTrigger)
    {
        var activeDocument = _activeDocumentAccessor();
        if (activeDocument is null || !_editor.IsVisible)
        {
            CloseCompletionWindow();
            return;
        }

        CancelPendingCompletion();
        var cancellation = new CancellationTokenSource();
        _completionCancellation = cancellation;
        var caretOffset = _editor.CaretOffset;
        var location = _editor.Document.GetLocation(caretOffset);
        var context = await _semanticContextAccessor().ConfigureAwait(true);
        if (context is null || cancellation.IsCancellationRequested)
        {
            return;
        }

        var items = await _completionService
            .GetCompletionsAsync(context, location.Line, location.Column, cancellation.Token)
            .ConfigureAwait(true);
        if (cancellation.IsCancellationRequested
            || items.Count == 0
            || _editor.CaretOffset != caretOffset)
        {
            return;
        }

        CloseCompletionWindow();
        var completionWindow = new CompletionWindow(_editor.TextArea);
        completionWindow.StartOffset = memberAccessTrigger
            ? caretOffset
            : FindIdentifierStartOffset(caretOffset);
        completionWindow.Closed += CompletionWindow_OnClosed;

        foreach (var item in items)
        {
            completionWindow.CompletionList.CompletionData.Add(new CSharpCompletionData(item));
        }

        _completionWindow = completionWindow;
        completionWindow.Show();
    }

    private int FindIdentifierStartOffset(int caretOffset)
    {
        var offset = Math.Clamp(caretOffset, 0, _editor.Document.TextLength);
        while (offset > 0)
        {
            var character = _editor.Document.GetCharAt(offset - 1);
            if (!char.IsLetterOrDigit(character) && character != '_')
            {
                break;
            }

            offset--;
        }

        return offset;
    }

    private void CompletionWindow_OnClosed(object? sender, EventArgs eventArgs)
    {
        if (ReferenceEquals(sender, _completionWindow))
        {
            _completionWindow = null;
        }
    }

    private void CloseCompletionWindow()
    {
        if (_completionWindow is null)
        {
            return;
        }

        _completionWindow.Closed -= CompletionWindow_OnClosed;
        _completionWindow.Close();
        _completionWindow = null;
    }

    private void CancelPendingCompletion()
    {
        if (_completionCancellation is null)
        {
            return;
        }

        _completionCancellation.Cancel();
        _completionCancellation.Dispose();
        _completionCancellation = null;
    }

    private void Detach()
    {
        if (_detached)
        {
            return;
        }

        _detached = true;
        CancelPendingCompletion();
        CloseCompletionWindow();
        _editor.TextArea.TextEntered -= TextArea_OnTextEntered;
        _editor.TextArea.TextEntering -= TextArea_OnTextEntering;
        _editor.TextArea.KeyDown -= TextArea_OnKeyDown;
        _window.Closed -= Window_OnClosed;
    }

    private void Window_OnClosed(object? sender, EventArgs eventArgs)
    {
        Detach();
    }
}
