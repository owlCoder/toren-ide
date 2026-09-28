using Avalonia.Controls;
using Avalonia.Input;
using AvaloniaEdit;
using Toren.Language.CSharp.Contracts;
using Toren.Language.CSharp.Models;

namespace Toren.App.Editor.Services;

internal sealed class CSharpFormattingController
{
    private readonly Window _window;
    private readonly TextEditor _editor;
    private readonly ICSharpFormattingService _formattingService;
    private readonly Func<CSharpSourceDocument?> _activeDocumentAccessor;
    private readonly Action<string> _setStatus;
    private bool _formatChordArmed;
    private bool _detached;

    private CSharpFormattingController(
        Window window,
        ICSharpFormattingService formattingService,
        Func<CSharpSourceDocument?> activeDocumentAccessor,
        Action<string> setStatus)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
        _formattingService = formattingService ?? throw new ArgumentNullException(nameof(formattingService));
        _activeDocumentAccessor = activeDocumentAccessor
            ?? throw new ArgumentNullException(nameof(activeDocumentAccessor));
        _setStatus = setStatus ?? throw new ArgumentNullException(nameof(setStatus));
        _editor = window.FindControl<TextEditor>("DocumentEditor")
            ?? throw new InvalidOperationException("The document editor could not be located.");

        _window.KeyDown += Window_OnKeyDown;
        _window.Closed += Window_OnClosed;
    }

    public static void Attach(
        Window window,
        ICSharpFormattingService formattingService,
        Func<CSharpSourceDocument?> activeDocumentAccessor,
        Action<string> setStatus)
    {
        _ = new CSharpFormattingController(
            window,
            formattingService,
            activeDocumentAccessor,
            setStatus);
    }

    private async void Window_OnKeyDown(object? sender, KeyEventArgs eventArgs)
    {
        if (!_editor.IsVisible)
        {
            _formatChordArmed = false;
            return;
        }

        var commandModifier = eventArgs.KeyModifiers.HasFlag(KeyModifiers.Control)
            || eventArgs.KeyModifiers.HasFlag(KeyModifiers.Meta);
        if (commandModifier && eventArgs.Key == Key.K)
        {
            _formatChordArmed = true;
            eventArgs.Handled = true;
            _setStatus("Format: Ctrl/Cmd+K, D for document · Ctrl/Cmd+K, F for selection");
            return;
        }

        if (_formatChordArmed)
        {
            _formatChordArmed = false;
            if (commandModifier && eventArgs.Key == Key.D)
            {
                eventArgs.Handled = true;
                await FormatDocumentAsync().ConfigureAwait(true);
                return;
            }

            if (commandModifier && eventArgs.Key == Key.F)
            {
                eventArgs.Handled = true;
                await FormatSelectionAsync().ConfigureAwait(true);
                return;
            }
        }

        if (eventArgs.Key == Key.F
            && eventArgs.KeyModifiers.HasFlag(KeyModifiers.Alt)
            && eventArgs.KeyModifiers.HasFlag(KeyModifiers.Shift))
        {
            eventArgs.Handled = true;
            await FormatDocumentAsync().ConfigureAwait(true);
        }
    }

    private async Task FormatDocumentAsync()
    {
        var activeDocument = _activeDocumentAccessor();
        if (activeDocument is null)
        {
            return;
        }

        var originalText = activeDocument.Text;
        var caretOffset = _editor.CaretOffset;
        var formatted = await _formattingService.FormatAsync(originalText).ConfigureAwait(true);
        if (!CanApply(activeDocument, originalText))
        {
            return;
        }

        if (formatted.Equals(originalText, StringComparison.Ordinal))
        {
            _setStatus($"{Path.GetFileName(activeDocument.Path)} is already formatted.");
            return;
        }

        ApplyFormattedText(formatted, caretOffset);
        _setStatus($"Formatted {Path.GetFileName(activeDocument.Path)}");
    }

    private async Task FormatSelectionAsync()
    {
        var activeDocument = _activeDocumentAccessor();
        if (activeDocument is null)
        {
            return;
        }

        var selectionLength = _editor.SelectionLength;
        if (selectionLength <= 0)
        {
            _setStatus("Select C# code before formatting the selection.");
            return;
        }

        var originalText = activeDocument.Text;
        var selectionStart = _editor.SelectionStart;
        var formatted = await _formattingService
            .FormatSelectionAsync(originalText, selectionStart, selectionLength)
            .ConfigureAwait(true);
        if (!CanApply(activeDocument, originalText))
        {
            return;
        }

        if (formatted.Equals(originalText, StringComparison.Ordinal))
        {
            _setStatus("Selection is already formatted.");
            return;
        }

        ApplyFormattedText(formatted, selectionStart);
        _setStatus($"Formatted selection in {Path.GetFileName(activeDocument.Path)}");
    }

    private bool CanApply(CSharpSourceDocument activeDocument, string originalText) =>
        ReferenceEquals(_activeDocumentAccessor(), activeDocument)
        && activeDocument.Text.Equals(originalText, StringComparison.Ordinal);

    private void ApplyFormattedText(string formatted, int caretOffset)
    {
        _editor.Text = formatted;
        _editor.CaretOffset = Math.Min(caretOffset, _editor.Document.TextLength);
        _editor.Focus();
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
        _window.KeyDown -= Window_OnKeyDown;
        _window.Closed -= Window_OnClosed;
    }
}
