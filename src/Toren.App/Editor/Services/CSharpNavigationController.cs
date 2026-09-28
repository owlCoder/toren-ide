using Avalonia.Controls;
using Avalonia.Input;
using AvaloniaEdit;
using Toren.Language.CSharp.Contracts;
using Toren.Language.CSharp.Models;

namespace Toren.App.Editor.Services;

internal sealed class CSharpNavigationController
{
    private readonly Window _window;
    private readonly TextEditor _editor;
    private readonly ICSharpSemanticService _semanticService;
    private readonly Stack<int> _navigationHistory = new();
    private bool _detached;

    private CSharpNavigationController(Window window, ICSharpSemanticService semanticService)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
        _semanticService = semanticService ?? throw new ArgumentNullException(nameof(semanticService));
        _editor = window.FindControl<TextEditor>("DocumentEditor")
            ?? throw new InvalidOperationException("The document editor could not be located.");

        _window.KeyDown += Window_OnKeyDown;
        _window.Closed += Window_OnClosed;
    }

    public static void Attach(Window window, ICSharpSemanticService semanticService)
    {
        _ = new CSharpNavigationController(window, semanticService);
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
            NavigateBack();
            eventArgs.Handled = true;
        }
    }

    private async Task GoToDefinitionAsync()
    {
        var originOffset = _editor.CaretOffset;
        var origin = _editor.Document.GetLocation(originOffset);
        var symbol = await _semanticService
            .GetSymbolAsync(_editor.Text, origin.Line, origin.Column)
            .ConfigureAwait(true);
        if (symbol?.Definition is null)
        {
            return;
        }

        _navigationHistory.Push(originOffset);
        NavigateTo(symbol.Definition);
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

    private void NavigateBack()
    {
        var offset = Math.Clamp(_navigationHistory.Pop(), 0, _editor.Document.TextLength);
        _editor.CaretOffset = offset;
        var location = _editor.Document.GetLocation(offset);
        _editor.ScrollTo(location.Line, location.Column);
        _editor.Focus();
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
}
