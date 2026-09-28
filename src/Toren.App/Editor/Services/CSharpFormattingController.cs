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
        if (eventArgs.Key != Key.F
            || !eventArgs.KeyModifiers.HasFlag(KeyModifiers.Alt)
            || !eventArgs.KeyModifiers.HasFlag(KeyModifiers.Shift)
            || !_editor.IsVisible)
        {
            return;
        }

        var activeDocument = _activeDocumentAccessor();
        if (activeDocument is null)
        {
            return;
        }

        eventArgs.Handled = true;
        var originalText = activeDocument.Text;
        var caretOffset = _editor.CaretOffset;
        var formatted = await _formattingService.FormatAsync(originalText).ConfigureAwait(true);
        if (!ReferenceEquals(_activeDocumentAccessor(), activeDocument)
            || !activeDocument.Text.Equals(originalText, StringComparison.Ordinal))
        {
            return;
        }

        if (formatted.Equals(originalText, StringComparison.Ordinal))
        {
            _setStatus($"{Path.GetFileName(activeDocument.Path)} is already formatted.");
            return;
        }

        _editor.Text = formatted;
        _editor.CaretOffset = Math.Min(caretOffset, _editor.Document.TextLength);
        _editor.Focus();
        _setStatus($"Formatted {Path.GetFileName(activeDocument.Path)}");
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
